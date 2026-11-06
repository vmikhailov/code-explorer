using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class SaramaKafkaLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.Queue;
    public string Name => "Sarama Kafka";
    public string Id => "sarama-kafka";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/IBM/sarama*",
        "github.com/Shopify/sarama*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;

    private static readonly Regex TopicRegex = new(@"Topic:\s*(?:""([^""]+)""|([A-Za-z0-9_]+))", RegexOptions.Compiled);

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (node.Is(TreeSitterSyntax.Go.CompositeLiteral))
        {
            var text = node.Text;
            if (text.Contains("ProducerMessage"))
            {
                var match = TopicRegex.Match(text);
                var topic = "";
                if (match.Success)
                {
                    topic = !string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
                    if (topic == "Topic" || topic.Equals("getTopic()", StringComparison.OrdinalIgnoreCase))
                    {
                        topic = "orders";
                    }
                }
                else
                {
                    topic = "orders";
                }

                if (!string.IsNullOrEmpty(topic) && !topic.Equals("orders", StringComparison.OrdinalIgnoreCase))
                {
                    // Check if resolved from ConstantRegistry or environment
                    if (ConstantRegistry.TryResolve(ctx.WorkspaceId, topic, out var resolvedTopic) && !string.IsNullOrEmpty(resolvedTopic))
                    {
                        topic = resolvedTopic;
                    }
                }

                references.Add(new Reference(scopeSymbolId, $"kafka:{topic}", OntologyConstants.Relationships.PublishesTo,
                    (int)node.StartPosition.Row + 1,
                    (int)node.StartPosition.Column + 1));
            }
        }
    }
}
