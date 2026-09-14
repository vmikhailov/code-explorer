using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MassTransitLibraryParser : ILibraryParser
{
    public string Name => "MassTransit";
    public string Id => "masstransit";
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["MassTransit"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> ConsumerInterfaces =
    [
        "IConsumer",
        "IJobConsumer",
        "JobConsumer",
        "BatchConsumer",
        "Activity"
    ];

    private static readonly HashSet<string> EgressMethodNames =
    [
        "Publish",
        "PublishAsync",
        "Send",
        "SendAsync",
        "Respond",
        "RespondAsync"
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
        // 1. Ingress: Consumer classes subscribe to message types
        if (IsConsumerClass(node))
        {
            var msgTypes = ExtractAllConsumerMessageTypes(node);
            foreach (var msg in msgTypes)
            {
                references.Add(new Reference(scopeSymbolId, "masstransit:" + msg, OntologyConstants.Relationships.SubscribesTo));
            }
        }

        // 2. Egress: Publish / Send invocations
        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            if (TryExtractEgressMessage(node, out var egressMsg))
            {
                references.Add(new Reference(scopeSymbolId, "masstransit:" + egressMsg, OntologyConstants.Relationships.PublishesTo));
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
        if (!nameNode.IsValid() || nameNode.Text != "Consume") return false;

        return !string.IsNullOrEmpty(ExtractConsumerMethodMessageType(node));
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
            if (child.Is(TreeSitterSyntax.CSharp.GenericName))
            {
                var idNode = child.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                if (idNode.IsValid() && ConsumerInterfaces.Contains(idNode.Text))
                {
                    var typeArgList = child.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                    if (typeArgList.IsValid())
                    {
                        var typeNode = typeArgList.Children.FirstOrDefault(c =>
                            c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName));
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
            if (typeNode.IsValid())
            {
                var text = typeNode.Text;
                if (text.StartsWith("ConsumeContext<") && text.EndsWith('>'))
                {
                    return CleanTypeName(text.Substring("ConsumeContext<".Length, text.Length - "ConsumeContext<".Length - 1));
                }
            }
        }

        return null;
    }

    private static bool TryExtractEgressMessage(Node invocationNode, out string messageType)
    {
        messageType = "";
        var func = invocationNode.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            return false;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;

        // Check generic method: Publish<OrderSubmitted>(...)
        if (nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            var idChild = nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier);
            if (idChild.IsValid() && EgressMethodNames.Contains(idChild.Text))
            {
                var typeArgs = nameNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                if (typeArgs.IsValid())
                {
                    var firstType = typeArgs.Children.FirstOrDefault(c =>
                        c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
                    if (firstType.IsValid())
                    {
                        messageType = CleanTypeName(firstType.Text);
                        return true;
                    }
                }
            }
        }
        // Check non-generic method: Publish(new OrderSubmitted(...))
        else if (EgressMethodNames.Contains(nameNode.Text))
        {
            var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
            if (argList.IsValid())
            {
                var firstArg = argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument);
                if (firstArg.IsValid())
                {
                    var objCreation = firstArg.FindChildOfType(TreeSitterSyntax.CSharp.ObjectCreationExpression);
                    if (objCreation.IsValid())
                    {
                        var typeNode = objCreation.GetField(TreeSitterSyntax.Fields.Type)
                            ?? objCreation.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
                        if (typeNode.IsValid())
                        {
                            messageType = CleanTypeName(typeNode.Text);
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static string CleanTypeName(string typeName)
    {
        var dot = typeName.LastIndexOf('.');
        return dot >= 0 ? typeName[(dot + 1)..] : typeName;
    }
}
