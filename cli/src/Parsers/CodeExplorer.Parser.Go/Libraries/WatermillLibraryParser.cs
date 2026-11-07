using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class WatermillLibraryParser : ISemanticExtension
{
    private const string WatermillPrefix = "watermill:";

    public string Name => "Watermill";
    public string Id => "watermill";
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/ThreeDotsLabs/watermill*",
        "*watermill*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var funcText = func.Text;
        var args = GoAstHelper.GetCallArguments(node);

        // router.AddHandler(handlerName, subscribeTopic, subscriber, publishTopic, publisher, handlerFunc)
        if (funcText.EndsWith(".AddHandler", StringComparison.Ordinal) && args.Count >= 4)
        {
            var subTopic = GoAstHelper.ResolveStringOrVariable(args[1]);
            var pubTopic = GoAstHelper.ResolveStringOrVariable(args[3]);
            if (!string.IsNullOrEmpty(subTopic))
            {
                references.Add(new Reference(scopeSymbolId, WatermillPrefix + subTopic, OntologyConstants.Relationships.SubscribesTo));
            }
            if (!string.IsNullOrEmpty(pubTopic))
            {
                references.Add(new Reference(scopeSymbolId, WatermillPrefix + pubTopic, OntologyConstants.Relationships.PublishesTo));
            }
        }
        // router.AddNoPublisherHandler(handlerName, subscribeTopic, subscriber, handlerFunc)
        else if (funcText.EndsWith(".AddNoPublisherHandler", StringComparison.Ordinal) && args.Count >= 2)
        {
            var subTopic = GoAstHelper.ResolveStringOrVariable(args[1]);
            if (!string.IsNullOrEmpty(subTopic))
            {
                references.Add(new Reference(scopeSymbolId, WatermillPrefix + subTopic, OntologyConstants.Relationships.SubscribesTo));
            }
        }
        else if (funcText.EndsWith(".Publish", StringComparison.Ordinal) &&
                 funcText.Contains("pub", StringComparison.OrdinalIgnoreCase) &&
                 args.Count >= 1)
        {
            var topic = GoAstHelper.ResolveStringOrVariable(args[0]);
            if (!string.IsNullOrEmpty(topic))
            {
                references.Add(new Reference(scopeSymbolId, WatermillPrefix + topic, OntologyConstants.Relationships.PublishesTo));
            }
        }
    }
}
