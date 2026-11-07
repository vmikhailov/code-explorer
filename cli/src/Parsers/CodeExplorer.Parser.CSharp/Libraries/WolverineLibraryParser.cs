using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class WolverineLibraryParser : DeclarativeSemanticExtension
{
    private const string WolverinePrefix = "wolverine:";
    private const string HandlerSuffix = "Handler";
    private const string WolverineHandlerAttr = "WolverineHandler";

    private static readonly HashSet<string> HandlerMethods = new(StringComparer.Ordinal)
    {
        "Handle", "HandleAsync", "Consume", "ConsumeAsync"
    };

    private static readonly HashSet<string> EgressMethods = new(StringComparer.Ordinal)
    {
        "InvokeAsync", "PublishAsync", "SendAsync", "BroadcastToTopicAsync"
    };

    public WolverineLibraryParser()
    {
        Id = "wolverine";
        Name = "Wolverine";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["WolverineFx", "WolverineFx.*", "Wolverine", "Wolverine.*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet(
                [
                    TreeSitterSyntax.CSharp.MethodDeclaration,
                    TreeSitterSyntax.CSharp.InvocationExpression
                ]),
                MapNodeType = (node, ctx) => IsWolverineHandlerMethod(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = (node, _, _, _) => IsWolverineHandlerMethod(node, out var msgType) ? $"WolverineHandler:{msgType}" : null,
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsWolverineHandlerMethod(node, out var handledMessage))
        {
            references.Add(new Reference(scopeSymbolId, WolverinePrefix + handledMessage, OntologyConstants.Relationships.SubscribesTo));
            references.Add(new Reference(scopeSymbolId, handledMessage, OntologyConstants.Relationships.UsesType));
            return;
        }

        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return;

        if (TryExtractWolverineEgress(node, out var egressMsg))
        {
            references.Add(new Reference(scopeSymbolId, WolverinePrefix + egressMsg, OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static bool IsWolverineHandlerMethod(Node node, out string messageType)
    {
        messageType = string.Empty;
        if (!node.Is(TreeSitterSyntax.CSharp.MethodDeclaration)) return false;

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid() || !HandlerMethods.Contains(nameNode.Text)) return false;

        var parentClass = FindParentClass(node);
        if (!parentClass.IsValid()) return false;

        var classNameNode = parentClass.GetField(TreeSitterSyntax.Fields.Name);
        var isNamedHandler = classNameNode.IsValid() && classNameNode.Text.EndsWith(HandlerSuffix, StringComparison.Ordinal);
        var hasAttr = HasWolverineAttribute(node) || HasWolverineAttribute(parentClass);
        if (!isNamedHandler && !hasAttr) return false;

        var paramList = node.FindChildOfType(TreeSitterSyntax.CSharp.ParameterList);
        if (!paramList.IsValid()) return false;

        var firstParam = paramList.FindChildOfType(TreeSitterSyntax.CSharp.Parameter);
        var typeNode = firstParam.IsValid() ? firstParam.GetField(TreeSitterSyntax.Fields.Type) : default;
        if (!typeNode.IsValid() || string.IsNullOrEmpty(typeNode.Text)) return false;

        messageType = CleanTypeName(typeNode.Text);
        return !string.IsNullOrEmpty(messageType);
    }

    private static bool HasWolverineAttribute(Node node)
    {
        foreach (var attrList in node.FindChildrenOfType(TreeSitterSyntax.CSharp.AttributeList))
        {
            foreach (var attr in attrList.FindChildrenOfType(TreeSitterSyntax.CSharp.Attribute))
            {
                if (attr.Text.Contains(WolverineHandlerAttr, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TryExtractWolverineEgress(Node invocation, out string messageType)
    {
        messageType = string.Empty;
        var func = invocation.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return false;

        var nameNode = func.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;

        var idNode = nameNode.Is(TreeSitterSyntax.CSharp.GenericName)
            ? nameNode.FindChildOfType(TreeSitterSyntax.Common.Identifier)
            : default;
        var methodName = idNode.IsValid() ? idNode.Text : nameNode.Text;

        if (!EgressMethods.Contains(methodName)) return false;

        var argList = invocation.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        var firstArg = argList.IsValid() ? argList.FindChildOfType(TreeSitterSyntax.CSharp.Argument) : default;
        if (firstArg.IsValid())
        {
            var objCreation = firstArg.FindChildOfType(TreeSitterSyntax.CSharp.ObjectCreationExpression);
            if (objCreation.IsValid())
            {
                var typeNode = objCreation.GetField(TreeSitterSyntax.Fields.Type) ??
                               objCreation.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier));
                if (typeNode.IsValid())
                {
                    messageType = CleanTypeName(typeNode.Text);
                    return true;
                }
            }
        }

        if (nameNode.Is(TreeSitterSyntax.CSharp.GenericName))
        {
            var typeArgs = nameNode.FindChildOfType(TreeSitterSyntax.CSharp.TypeArgumentList);
            var firstType = typeArgs.IsValid()
                ? typeArgs.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.CSharp.TypeIdentifier, TreeSitterSyntax.Common.Identifier))
                : default;
            if (firstType.IsValid())
            {
                messageType = CleanTypeName(firstType.Text);
                return true;
            }
        }

        return false;
    }

    private static Node? FindParentClass(Node node)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.CSharp.ClassDeclaration)) return curr;
            curr = curr.Parent;
        }
        return null;
    }

    private static string CleanTypeName(string raw)
    {
        var dot = raw.LastIndexOf('.');
        return dot >= 0 ? raw[(dot + 1)..] : raw;
    }
}
