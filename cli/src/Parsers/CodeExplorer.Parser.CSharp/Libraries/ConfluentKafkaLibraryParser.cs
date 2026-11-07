using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class ConfluentKafkaLibraryParser : DeclarativeSemanticExtension
{
    private static readonly Regex TopicFallbackRegex = new(@"\?\?\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex StringLiteralRegex = new(@"""([^""]+)""", RegexOptions.Compiled);

    public ConfluentKafkaLibraryParser()
    {
        Id = "confluent-kafka";
        Name = "Confluent.Kafka";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["Confluent.Kafka*", "Confluent.Kafka"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet([TreeSitterSyntax.CSharp.InvocationExpression]),
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.CSharp.InvocationExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.CSharp.MemberAccessExpression)) return;

        var methodName = func.GetChildFieldText(TreeSitterSyntax.Fields.Name);
        if (string.IsNullOrEmpty(methodName)) return;

        var args = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList);
        if (!args.IsValid()) return;

        if (methodName.Equals("Subscribe", StringComparison.Ordinal))
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.CSharp.Argument));
            var topic = ExtractTopicName(firstArg, node, ctx);
            if (!string.IsNullOrEmpty(topic))
            {
                references.Add(new Reference(scopeSymbolId, $"kafka:{topic}", OntologyConstants.Relationships.SubscribesTo,
                    (int)node.StartPosition.Row + 1,
                    (int)node.StartPosition.Column + 1));
            }
        }
        else if (methodName.Equals("Produce", StringComparison.Ordinal) || methodName.Equals("ProduceAsync", StringComparison.Ordinal))
        {
            var firstArg = args.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.CSharp.Argument));
            var topic = ExtractTopicName(firstArg, node, ctx);
            if (!string.IsNullOrEmpty(topic))
            {
                references.Add(new Reference(scopeSymbolId, $"kafka:{topic}", OntologyConstants.Relationships.PublishesTo,
                    (int)node.StartPosition.Row + 1,
                    (int)node.StartPosition.Column + 1));
            }
        }
    }

    private static string ExtractTopicName(Node? argNode, Node invocationNode, ParsingContext ctx)
    {
        if (!argNode.IsValid()) return "";

        var text = argNode.Text;
        var litMatch = StringLiteralRegex.Match(text);
        if (litMatch.Success) return litMatch.Groups[1].Value;

        var varName = text.Trim();
        if (ConstantRegistry.TryResolve(ctx.WorkspaceId, varName, out var resolved) && !string.IsNullOrEmpty(resolved))
        {
            return resolved;
        }

        // Look upwards in the class or file for field/variable initialization with fallback (e.g. ?? "orders")
        var curr = invocationNode;
        while (curr.Parent.IsValid())
        {
            curr = curr.Parent;
        }

        var fullText = curr.Text;
        var declIdx = fullText.IndexOf(varName, StringComparison.Ordinal);
        if (declIdx >= 0)
        {
            var lineEnd = fullText.IndexOf(';', declIdx);
            if (lineEnd > declIdx)
            {
                var declSnippet = fullText.Substring(declIdx, lineEnd - declIdx);
                var fallbackMatch = TopicFallbackRegex.Match(declSnippet);
                if (fallbackMatch.Success) return fallbackMatch.Groups[1].Value;

                var strMatch = StringLiteralRegex.Match(declSnippet);
                if (strMatch.Success) return strMatch.Groups[1].Value;
            }
        }

        return varName;
    }
}
