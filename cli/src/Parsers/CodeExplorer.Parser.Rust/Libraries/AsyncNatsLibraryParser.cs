using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the async-nats client in Rust.
/// Detects client.publish("subject", ...) and client.subscribe("subject").
/// Emits Topic nodes and PUBLISHES_TO / SUBSCRIBES_TO relationships.
/// </summary>
public class AsyncNatsLibraryParser : DeclarativeSemanticExtension
{
    public AsyncNatsLibraryParser()
    {
        Id = "async-nats";
        Name = "async-nats (NATS)";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns = ["async-nats*", "async_nats*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet([TreeSitterSyntax.Rust.CallExpression]),
                MapNodeType = (node, ctx) => IsNatsCall(node, out _, out _) ? OntologyConstants.NodeLabels.Topic : null,
                ExtractIdentifier = (node, _, _, _) => IsNatsCall(node, out var subject, out _) ? subject : null,
                CollectReferences = CollectReferencesCore,
                EnrichSymbol = EnrichSymbolCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsNatsCall(node, out var subject, out var isPublish) && !string.IsNullOrEmpty(subject))
        {
            var relKind = isPublish
                ? OntologyConstants.Relationships.PublishesTo
                : OntologyConstants.Relationships.SubscribesTo;

            references.Add(new Reference(scopeSymbolId, "nats:" + subject, relKind));
            references.Add(new Reference(scopeSymbolId, subject, relKind));
        }
    }

    private static void EnrichSymbolCore(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsNatsCall(node, out _, out _))
        {
            symbol.Properties["broker_type"] = "nats";
        }
    }

    public static bool IsNatsCall(Node node, out string subject, out bool isPublish)
    {
        subject = string.Empty;
        isPublish = false;

        if (!node.Is(TreeSitterSyntax.Rust.CallExpression))
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out var receiver, out var methodName, out var args))
        {
            return false;
        }

        if (string.IsNullOrEmpty(methodName)) return false;

        if (methodName.Equals("publish", StringComparison.OrdinalIgnoreCase) && args.Count > 0)
        {
            var cand = RustAstHelper.ExtractStringLiteral(args[0]);
            if (!string.IsNullOrEmpty(cand))
            {
                subject = cand;
                isPublish = true;
                return true;
            }
        }

        if (methodName.Equals("subscribe", StringComparison.OrdinalIgnoreCase) && args.Count > 0)
        {
            // If receiver is nats or client
            var recText = receiver != null ? receiver.Text.ToLowerInvariant() : "";
            if (recText.Contains("nats") || recText.Contains("nc") || recText.Contains("client"))
            {
                var cand = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(cand))
                {
                    subject = cand;
                    isPublish = false;
                    return true;
                }
            }
        }

        return false;
    }
}
