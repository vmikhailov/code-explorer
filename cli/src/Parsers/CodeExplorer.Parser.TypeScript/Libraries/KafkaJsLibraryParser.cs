using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class KafkaJsLibraryParser : ILibraryParser
{
    public string Type => "cloud";
    public string Name => "KafkaJS";
    public string Id => "kafkajs";
    public IReadOnlyList<string> SupportedPatterns => ["kafkajs"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            if (string.Equals(propName, "send", StringComparison.OrdinalIgnoreCase))
            {
                var topic = ExtractTopicFromConfig(node);
                if (!string.IsNullOrEmpty(topic))
                {
                    references.Add(new Reference(scopeSymbolId, "kafka:" + topic, OntologyConstants.Relationships.PublishesTo));
                }
            }
            else if (string.Equals(propName, "subscribe", StringComparison.OrdinalIgnoreCase))
            {
                var topics = ExtractTopicsFromSubscribeConfig(node);
                foreach (var topic in topics)
                {
                    if (!string.IsNullOrEmpty(topic))
                    {
                        references.Add(new Reference(scopeSymbolId, "kafka:" + topic, OntologyConstants.Relationships.SubscribesTo));
                    }
                }
            }
        }
    }

    private static string? ExtractTopicFromConfig(Node callNode)
    {
        var args = AstHelper.GetCallArguments(callNode);
        if (args.Count == 0) return null;

        if (AstHelper.TryGetObjectProperty(args[0], "topic", out var valNode))
        {
            return AstHelper.ResolveStringOrTemplate(valNode);
        }

        return null;
    }

    private static List<string> ExtractTopicsFromSubscribeConfig(Node callNode)
    {
        var result = new List<string>();
        var args = AstHelper.GetCallArguments(callNode);
        if (args.Count == 0) return result;

        if (AstHelper.TryGetObjectProperty(args[0], "topic", out var topicVal))
        {
            var topic = AstHelper.ResolveStringOrTemplate(topicVal);
            if (!string.IsNullOrEmpty(topic)) result.Add(topic);
        }

        if (AstHelper.TryGetObjectProperty(args[0], "topics", out var topicsVal) && topicsVal.IsValid())
        {
            foreach (var item in topicsVal.Children)
            {
                var topic = AstHelper.ResolveStringOrTemplate(item);
                if (!string.IsNullOrEmpty(topic)) result.Add(topic);
            }
        }

        return result;
    }
}
