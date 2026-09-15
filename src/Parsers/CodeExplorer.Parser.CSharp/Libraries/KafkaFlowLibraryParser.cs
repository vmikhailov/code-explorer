using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class KafkaFlowLibraryParser : ILibraryParser
{
    public string Name => "KafkaFlow";
    public string Id => "kafkaflow";
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["KafkaFlow", "KafkaFlow.*"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> ConsumerInterfaces =
    [
        "IMessageHandler",
        "MessageHandler",
        "IKafkaHandler"
    ];

    private static readonly HashSet<string> EgressMethodNames =
    [
        "Produce",
        "ProduceAsync",
        "Publish",
        "PublishAsync"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsConsumerClass(node) || IsConsumerMethod(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsConsumerClass(node))
        {
            var msg = ExtractConsumerMessageType(node);
            return !string.IsNullOrEmpty(msg) ? $"Consumer:{msg}" : "Consumer";
        }

        if (IsConsumerMethod(node))
        {
            var msg = ExtractConsumerMethodMessageType(node);
            return !string.IsNullOrEmpty(msg) ? $"Consume:{msg}" : "Consume";
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Ingress: Handler classes subscribe to message types
        if (IsConsumerClass(node))
        {
            var msgTypes = ExtractAllConsumerMessageTypes(node);
            foreach (var msg in msgTypes)
            {
                references.Add(new Reference(scopeSymbolId, "kafka:" + msg, OntologyConstants.Relationships.SubscribesTo));
            }
        }

        // 2. Invocations: .Topic("name") or .Topic(Address.TopicName)
        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid() && func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var methodName = func.GetField(TreeSitterSyntax.Fields.Name)?.Text;
                if (methodName == "Topic")
                {
                    var topic = ExtractTopicArgument(node);
                    if (!string.IsNullOrEmpty(topic))
                    {
                        references.Add(new Reference(scopeSymbolId, "kafka:" + topic, OntologyConstants.Relationships.SubscribesTo));
                    }
                }
                else if (methodName == "AddHandler")
                {
                    var handlerType = ExtractGenericTypeArg(node, func);
                    if (!string.IsNullOrEmpty(handlerType))
                    {
                        references.Add(new Reference(scopeSymbolId, handlerType, OntologyConstants.Relationships.Triggers));
                    }
                }
                else if (EgressMethodNames.Contains(methodName ?? ""))
                {
                    if (TryExtractEgressMessage(node, out var egressMsg))
                    {
                        references.Add(new Reference(scopeSymbolId, "kafka:" + egressMsg, OntologyConstants.Relationships.PublishesTo));
                    }
                }
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsConsumerClass(node))
        {
            symbol.Protocol = "kafka";
            var msgTypes = ExtractAllConsumerMessageTypes(node);
            foreach (var msg in msgTypes)
            {
                symbol.References.Add(new Reference(symbol.Name, "kafka:" + msg, OntologyConstants.Relationships.SubscribesTo));
            }
        }
    }

    private static bool IsConsumerClass(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            return false;

        var baseList = node.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return false;

        return ExtractAllConsumerMessageTypes(node).Count > 0;
    }

    private static bool IsConsumerMethod(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.MethodDeclaration)) return false;
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid() || nameNode.Text != "Handle") return false;

        var parent = node.Parent;
        while (parent.IsValid())
        {
            if (parent.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            {
                return IsConsumerClass(parent);
            }
            parent = parent.Parent;
        }

        return false;
    }

    private static string? ExtractConsumerMessageType(Node classNode)
    {
        var all = ExtractAllConsumerMessageTypes(classNode);
        return all.Count > 0 ? all[0] : null;
    }

    private static List<string> ExtractAllConsumerMessageTypes(Node classNode)
    {
        var result = new List<string>();
        var baseList = classNode.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return result;

        foreach (var child in baseList.Children)
        {
            if (child.Is(TreeSitterSyntax.CSharp.GenericName) || child.Text.Contains("IMessageHandler"))
            {
                var idNode = child.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                var idText = idNode.IsValid() ? idNode.Text : child.Text;

                if (ConsumerInterfaces.Any(ci => idText.Contains(ci)))
                {
                    var typeArgList = child.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                    if (typeArgList.IsValid())
                    {
                        var typeNode = typeArgList.Children.FirstOrDefault(c =>
                            c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName, TreeSitterSyntax.CSharp.QualifiedName));
                        if (typeNode.IsValid() && !string.IsNullOrEmpty(typeNode.Text))
                        {
                            result.Add(CleanTypeName(typeNode.Text));
                        }
                    }
                }
            }
        }

        return result;
    }

    private static string? ExtractConsumerMethodMessageType(Node methodNode)
    {
        var paramList = methodNode.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (!paramList.IsValid()) return null;

        foreach (var param in paramList.FindChildrenOfType(TreeSitterSyntax.CSharp.Parameter))
        {
            var typeNode = param.GetField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid() && !string.IsNullOrEmpty(typeNode.Text))
            {
                var text = typeNode.Text;
                if (!text.Contains("Context") && !text.Contains("CancellationToken"))
                {
                    return CleanTypeName(text);
                }
            }
        }

        return null;
    }

    private static string? ExtractTopicArgument(Node invocationNode)
    {
        var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        var firstArg = argList?.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
        if (firstArg == null || !firstArg.IsValid()) return null;

        var str = firstArg.Children.FirstOrDefault(c => c.Type.Contains("string"));
        if (str.IsValid()) return str.Text.Trim('"');

        var expr = firstArg.GetField(TreeSitterSyntax.Fields.Expression)
                   ?? firstArg.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.MemberAccessExpression, TreeSitterSyntax.Common.Identifier));
        if (expr.IsValid())
        {
            if (expr.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            {
                var nameChild = expr.GetField(TreeSitterSyntax.Fields.Name);
                if (nameChild.IsValid()) return nameChild.Text;
            }
            return expr.Text.Trim('"');
        }

        return null;
    }

    private static string? ExtractGenericTypeArg(Node invocationNode, Node funcNode)
    {
        var typeArgs = invocationNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList)
                       ?? funcNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
        var first = typeArgs?.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
        return first.IsValid() ? CleanTypeName(first.Text) : null;
    }

    private static bool TryExtractEgressMessage(Node invocationNode, out string messageType)
    {
        messageType = "";
        var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (!argList.IsValid()) return false;

        var args = argList.FindChildrenOfType(TreeSitterSyntax.CSharp.Argument).ToList();
        if (args.Count == 0) return false;

        var firstArg = args[0];
        var str = firstArg.Children.FirstOrDefault(c => c.Type.Contains("string"));
        if (str.IsValid())
        {
            messageType = str.Text.Trim('"');
            return !string.IsNullOrEmpty(messageType);
        }

        var expr = firstArg.GetField(TreeSitterSyntax.Fields.Expression)
                   ?? firstArg.Children.FirstOrDefault();
        if (expr.IsValid())
        {
            messageType = CleanTypeName(expr.Text);
            return !string.IsNullOrEmpty(messageType);
        }

        return false;
    }

    private static string CleanTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return "";
        var type = rawType.Trim();
        var dotIdx = type.LastIndexOf('.');
        if (dotIdx >= 0 && dotIdx < type.Length - 1)
        {
            type = type.Substring(dotIdx + 1);
        }
        var genericIdx = type.IndexOf('<');
        if (genericIdx > 0)
        {
            type = type.Substring(0, genericIdx);
        }
        return type.Trim('?', ' ');
    }
}
