using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringEventsLibraryParser : ILibraryParser
{
    public string Name => "Spring Events";
    public string Id => "spring-events";
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["org.springframework.context.event.*", "org.springframework.context.ApplicationEventPublisher"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsEventListenerMethod(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsEventListenerMethod(node))
        {
            var eventType = ExtractEventParameterType(node);
            return !string.IsNullOrEmpty(eventType) ? $"EventListener:{eventType}" : "EventListener";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Ingress: @EventListener methods
        if (IsEventListenerMethod(node))
        {
            var eventType = ExtractEventParameterType(node);
            if (!string.IsNullOrEmpty(eventType))
            {
                references.Add(new Reference(scopeSymbolId, "spring:" + eventType, OntologyConstants.Relationships.SubscribesTo));
            }
        }

        // 2. Egress: publisher.publishEvent(new OrderCreatedEvent(...))
        if (node.Is(TreeSitterSyntax.Java.MethodInvocation))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            if (nameNode.IsValid() && nameNode.Text == "publishEvent")
            {
                var argList = node.FindChildOfType(TreeSitterSyntax.Java.ArgumentList);
                if (argList.IsValid())
                {
                    var firstArg = argList.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Java.ObjectCreationExpression));
                    if (firstArg.IsValid())
                    {
                        var typeNode = firstArg.GetField(TreeSitterSyntax.Fields.Type);
                        if (typeNode.IsValid())
                        {
                            references.Add(new Reference(scopeSymbolId, "spring:" + typeNode.Text, OntologyConstants.Relationships.PublishesTo));
                        }
                    }
                }
            }
        }
    }

    private static bool IsEventListenerMethod(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Java.MethodDeclaration)) return false;
        var modifiers = node.FindChildOfType(TreeSitterSyntax.Java.Modifiers);
        if (!modifiers.IsValid()) return false;

        foreach (var child in modifiers.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
            {
                var nameNode = child.GetField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid() && (nameNode.Text == "EventListener" || nameNode.Text == "TransactionalEventListener"))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string? ExtractEventParameterType(Node methodNode)
    {
        var paramList = methodNode.FindChildOfType(TreeSitterSyntax.Java.FormalParameters);
        if (!paramList.IsValid()) return null;

        var firstParam = paramList.FindChildOfType(TreeSitterSyntax.Java.FormalParameter);
        if (!firstParam.IsValid()) return null;

        var typeNode = firstParam.GetField(TreeSitterSyntax.Fields.Type);
        return typeNode.IsValid() ? typeNode.Text : null;
    }
}
