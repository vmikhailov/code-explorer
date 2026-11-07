using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class InngestLibraryParser : ISemanticExtension
{
    private const string InngestPrefix = "inngest:";
    private const string CreateFunctionMethod = "createFunction";
    private const string SendMethod = "send";
    private const string SendEventMethod = "sendEvent";

    public string Name => "Inngest";
    public string Id => "inngest";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["inngest", "inngest/*", "@inngest/*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WorkerService;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractCreateFunctionEvent(node, out _) ? OntologyConstants.NodeLabels.EntryPoint : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractCreateFunctionEvent(node, out var eventName) ? $"Inngest:{eventName}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        if (TryExtractCreateFunctionEvent(node, out var subscribedEvent))
        {
            references.Add(new Reference(scopeSymbolId, InngestPrefix + subscribedEvent, OntologyConstants.Relationships.SubscribesTo));
            return;
        }

        if (TryExtractSendEvent(node, out var publishedEvent))
        {
            references.Add(new Reference(scopeSymbolId, InngestPrefix + publishedEvent, OntologyConstants.Relationships.PublishesTo));
        }
    }

    private static bool TryExtractCreateFunctionEvent(Node node, out string eventName)
    {
        eventName = string.Empty;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;
        if (!AstHelper.TryGetMemberAccess(node, out _, out var propName) || propName != CreateFunctionMethod)
        {
            return false;
        }

        var args = AstHelper.GetCallArguments(node);
        if (args.Count < 2) return false;

        var triggerArg = args[1];
        var extracted = ExtractPropertyString(triggerArg, "event") ??
                        ExtractPropertyString(args[0], "id") ??
                        AstHelper.ExtractFirstStringArgument(node);

        if (string.IsNullOrEmpty(extracted)) return false;
        eventName = extracted;
        return true;
    }

    private static bool TryExtractSendEvent(Node node, out string eventName)
    {
        eventName = string.Empty;
        if (!AstHelper.TryGetMemberAccess(node, out var objNode, out var propName)) return false;

        var objText = objNode.IsValid() ? objNode.Text : string.Empty;
        var isSend = (propName == SendMethod && objText.Contains("inngest", StringComparison.OrdinalIgnoreCase)) ||
                     (propName == SendEventMethod && objText.Contains("step", StringComparison.OrdinalIgnoreCase));
        if (!isSend) return false;

        var args = AstHelper.GetCallArguments(node);
        foreach (var arg in args)
        {
            var nameProp = ExtractPropertyString(arg, "name");
            if (!string.IsNullOrEmpty(nameProp))
            {
                eventName = nameProp;
                return true;
            }
        }

        return false;
    }

    private static string? ExtractPropertyString(Node objNode, string keyName)
    {
        if (!objNode.IsValid()) return null;
        foreach (var child in objNode.Descendants())
        {
            if (child.Is(TreeSitterSyntax.TypeScript.Pair))
            {
                var key = child.GetField(TreeSitterSyntax.Fields.Key) ?? (child.Children.Count > 0 ? child.Children[0] : default);
                var val = child.GetField(TreeSitterSyntax.Fields.Value) ?? (child.Children.Count > 1 ? child.Children[^1] : default);
                if (key.IsValid() && val.IsValid() && key.Text.Trim('"', '\'', '`') == keyName)
                {
                    var text = val.Text.Trim('"', '\'', '`', ' ');
                    if (!string.IsNullOrEmpty(text)) return text;
                }
            }
        }

        return null;
    }
}
