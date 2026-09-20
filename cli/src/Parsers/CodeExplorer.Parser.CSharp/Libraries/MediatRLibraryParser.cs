using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class MediatRLibraryParser : ILibraryParser
{
    public string Name => "MediatR";
    public string Id => "mediatr";
    public string Type => OntologyConstants.LibraryTypes.Framework;
    public IReadOnlyList<string> SupportedPatterns => ["MediatR", "MediatR.*", "*Mediator*"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;

    private static readonly HashSet<string> HandlerInterfaces =
    [
        "IRequestHandler",
        "INotificationHandler",
        "IStreamRequestHandler",
        "RequestHandler",
        "NotificationHandler",
        "IMediatorHandler",
        "ICommandHandler",
        "IQueryHandler"
    ];

    private static readonly HashSet<string> EgressMethodNames =
    [
        "Send",
        "SendAsync",
        "Publish",
        "PublishAsync",
        "CreateStream",
        "Dispatch",
        "DispatchAsync"
    ];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHandlerClass(node) || IsHandleMethod(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsHandlerClass(node))
        {
            var req = ExtractHandlerRequestType(node);
            return !string.IsNullOrEmpty(req) ? $"Handler:{req}" : "Handler";
        }

        if (IsHandleMethod(node))
        {
            var req = ExtractHandleMethodRequestType(node);
            return !string.IsNullOrEmpty(req) ? $"Handle:{req}" : "Handle";
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Ingress: Handler classes subscribe to request/notification types
        if (IsHandlerClass(node))
        {
            var reqTypes = ExtractAllHandlerRequestTypes(node);
            foreach (var req in reqTypes)
            {
                references.Add(new Reference(scopeSymbolId, "mediatr:" + req, OntologyConstants.Relationships.SubscribesTo));
            }
        }

        // 2. Egress: mediator.Send(...) / mediator.Publish(...)
        if (node.Is(TreeSitterSyntax.CSharp.InvocationExpression))
        {
            if (TryExtractEgressRequest(node, out var reqType))
            {
                references.Add(new Reference(scopeSymbolId, "mediatr:" + reqType, OntologyConstants.Relationships.PublishesTo));
            }
        }
    }

    private static bool IsHandlerClass(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.RecordDeclaration))
            return false;

        var baseList = node.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return false;

        return ExtractAllHandlerRequestTypes(node).Count > 0;
    }

    private static bool IsHandleMethod(Node node)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.MethodDeclaration)) return false;
        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid() || nameNode.Text != "Handle") return false;

        return !string.IsNullOrEmpty(ExtractHandleMethodRequestType(node));
    }

    private static string? ExtractHandlerRequestType(Node classNode)
    {
        var all = ExtractAllHandlerRequestTypes(classNode);
        return all.Count > 0 ? all[0] : null;
    }

    private static List<string> ExtractAllHandlerRequestTypes(Node classNode)
    {
        var result = new List<string>();
        var baseList = classNode.FindChildOfType(TreeSitterSyntax.CSharp.BaseList);
        if (!baseList.IsValid()) return result;

        foreach (var child in baseList.Children)
        {
            if (child.Is(TreeSitterSyntax.CSharp.GenericName))
            {
                var idNode = child.FindChildOfType(TreeSitterSyntax.Common.Identifier);
                if (idNode.IsValid() && HandlerInterfaces.Contains(idNode.Text))
                {
                    var typeArgList = child.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
                    if (typeArgList.IsValid())
                    {
                        var firstType = typeArgList.Children.FirstOrDefault(c =>
                            c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier, TreeSitterSyntax.CSharp.GenericName));
                        if (firstType.IsValid() && !string.IsNullOrEmpty(firstType.Text))
                        {
                            result.Add(CleanTypeName(firstType.Text));
                        }
                    }
                }
            }
        }

        return result;
    }

    private static string? ExtractHandleMethodRequestType(Node methodNode)
    {
        var paramList = methodNode.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (!paramList.IsValid()) return null;

        var firstParam = paramList.FindChildOfType(TreeSitterSyntax.CSharp.Parameter);
        if (firstParam.IsValid())
        {
            var typeNode = firstParam.GetField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid() && !string.IsNullOrEmpty(typeNode.Text))
            {
                var text = typeNode.Text;
                // Avoid CancellationToken or non-request parameters
                if (text != "CancellationToken" && !text.EndsWith("Context"))
                {
                    return CleanTypeName(text);
                }
            }
        }

        return null;
    }

    private static bool TryExtractEgressRequest(Node invocationNode, out string requestType)
    {
        requestType = "";
        var func = invocationNode.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression))
            return false;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;

        var methodName = nameNode.Is(TreeSitterSyntax.CSharp.GenericName)
            ? nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)?.Text
            : nameNode.Text;

        if (methodName == null || !EgressMethodNames.Contains(methodName))
            return false;

        // Check if receiver expression suggests mediator or sender
        var exprNode = func.GetField(TreeSitterSyntax.Fields.Expression);
        if (exprNode.IsValid())
        {
            var recv = exprNode.Text.ToLowerInvariant();
            if (!recv.Contains("mediat") && !recv.Contains("sender") && !recv.Contains("publisher") && recv != "bus")
            {
                // If receiver doesn't mention mediator, only accept if generic or object creation is explicitly a request
                if (!nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
                {
                    return false;
                }
            }
        }

        // Generic: Send<Response>(request) or Send(request)
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
                        requestType = CleanTypeName(typeNode.Text);
                        return true;
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
