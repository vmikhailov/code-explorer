using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringKafkaLibraryParser : ISemanticExtension
{
    public string Id => "kafka";
    public string Name => "Apache Kafka";
    public string Type => OntologyConstants.LibraryTypes.Queue;
    public IReadOnlyList<string> SupportedPatterns => ["org.springframework.kafka.*", "org.apache.kafka.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor(Id, Name, LibraryRole, "library", SupportedPatterns);
    }

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsKafkaListener(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsKafkaListener(node))
        {
            var topic = ExtractTopic(node);
            return $"kafka:{topic}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Ingress: @KafkaListener annotations
        if (IsKafkaListener(node))
        {
            var topics = ExtractAllTopics(node);
            foreach (var topic in topics)
            {
                references.Add(new Reference(scopeSymbolId, "kafka:" + topic, OntologyConstants.Relationships.SubscribesTo));
            }

            var methodName = FindEnclosingMethodName(node);
            if (!string.IsNullOrEmpty(methodName))
            {
                references.Add(new Reference(scopeSymbolId, methodName, OntologyConstants.Relationships.Triggers));
            }
        }

        // 2. Egress: kafkaTemplate.send(topic, ...) or producer.send(new ProducerRecord<>(topic, ...))
        if (node.Is(TreeSitterSyntax.Java.MethodInvocation))
        {
            var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
            var methodName = nameNode.IsValid() ? nameNode.Text : null;
            if (methodName is "send" or "sendDefault" or "execute")
            {
                var objNode = node.GetField(TreeSitterSyntax.Fields.Object);
                var objText = objNode.IsValid() ? objNode.Text : "";
                var lowerObj = objText.ToLowerInvariant();

                if (lowerObj.Contains("kafka") || lowerObj.Contains("producer") || lowerObj.Contains("template"))
                {
                    var topic = ExtractProducerTopic(node);
                    if (!string.IsNullOrEmpty(topic))
                    {
                        references.Add(new Reference(scopeSymbolId, "kafka:" + topic, OntologyConstants.Relationships.PublishesTo));
                    }
                }
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsKafkaListener(node))
        {
            symbol.Protocol = "kafka";
            var topics = ExtractAllTopics(node);
            foreach (var topic in topics)
            {
                symbol.References.Add(new Reference(symbol.Name, "kafka:" + topic, OntologyConstants.Relationships.SubscribesTo));
            }
        }
    }

    public static bool IsKafkaListener(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
            return false;

        var nameNode = node.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                       ?? node.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);
        if (!nameNode.IsValid()) return false;

        var name = nameNode.Text;
        if (name.Contains('.')) name = name[(name.LastIndexOf('.') + 1)..];

        return name is "KafkaListener" or "KafkaHandler";
    }

    public static string ExtractTopic(Node annotNode)
    {
        var all = ExtractAllTopics(annotNode);
        return all.Count > 0 ? all[0] : "unknown-topic";
    }

    public static List<string> ExtractAllTopics(Node annotNode)
    {
        var list = new List<string>();

        // 1. Check element_value_pair where key is topics, topicPattern, or value
        foreach (var child in annotNode.Children)
        {
            if (child.Type == "annotation_argument_list")
            {
                foreach (var argChild in child.Children)
                {
                    if (argChild.Type == "element_value_pair")
                    {
                        var keyNode = argChild.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                        if (keyNode.IsValid() && (keyNode.Text is "topics" or "topicPattern" or "value"))
                        {
                            ExtractStringsFromTree(argChild, list);
                        }
                    }
                    else if (argChild.Is(TreeSitterSyntax.Java.StringLiteral))
                    {
                        var val = argChild.Text.Trim('"', '\'');
                        if (!string.IsNullOrEmpty(val) && !list.Contains(val)) list.Add(val);
                    }
                }
            }
        }

        if (list.Count == 0)
        {
            ExtractStringsFromTree(annotNode, list);
        }

        if (list.Count == 0)
        {
            var text = annotNode.Text;
            if (text.Contains('"'))
            {
                var first = text.IndexOf('"');
                var last = text.LastIndexOf('"');
                if (last > first) list.Add(text[(first + 1)..last]);
            }
        }

        return list;
    }

    private static void ExtractStringsFromTree(Node node, List<string> results)
    {
        if (node.Is(TreeSitterSyntax.Java.StringLiteral))
        {
            var clean = node.Text.Trim('"', '\'');
            if (!string.IsNullOrEmpty(clean) && !results.Contains(clean))
            {
                results.Add(clean);
            }
            return;
        }

        foreach (var child in node.Children)
        {
            ExtractStringsFromTree(child, results);
        }
    }

    private static string? FindEnclosingMethodName(Node annotNode)
    {
        var current = annotNode.Parent;
        while (current.IsValid())
        {
            if (current.Is(TreeSitterSyntax.Java.MethodDeclaration))
            {
                var nameField = current.GetField(TreeSitterSyntax.Fields.Name);
                if (nameField.IsValid()) return nameField.Text;

                foreach (var child in current.Children)
                {
                    if (child.Is(TreeSitterSyntax.Java.Identifier)) return child.Text;
                }
            }
            current = current.Parent;
        }
        return null;
    }

    private static string? ExtractProducerTopic(Node invocationNode)
    {
        var argList = invocationNode.FindChildOfType(TreeSitterSyntax.Java.ArgumentList);
        if (!argList.IsValid()) return null;

        var args = new List<Node>();
        foreach (var c in argList.Children)
        {
            if (c.Type != "(" && c.Type != ")" && c.Type != ",")
            {
                args.Add(c);
            }
        }

        if (args.Count == 0) return null;

        var firstArg = args[0];
        // 1. Direct string literal
        if (firstArg.Is(TreeSitterSyntax.Java.StringLiteral))
        {
            return firstArg.Text.Trim('"', '\'');
        }

        // 2. new ProducerRecord<>("topic", ...)
        if (firstArg.Is(TreeSitterSyntax.Java.ObjectCreationExpression))
        {
            var recArgList = firstArg.FindChildOfType(TreeSitterSyntax.Java.ArgumentList);
            if (recArgList.IsValid())
            {
                var str = recArgList.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
                if (str.IsValid()) return str.Text.Trim('"', '\'');
            }
        }

        // 3. Identifier / Constant
        if (firstArg.IsAny(TreeSitterSyntax.Java.Identifier, TreeSitterSyntax.Java.ScopedIdentifier))
        {
            var text = firstArg.Text.Trim('"', '\'');
            if (text.Contains('.')) text = text[(text.LastIndexOf('.') + 1)..];
            return text;
        }

        return null;
    }
}
