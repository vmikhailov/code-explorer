using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the async-nats client in Rust.
/// Detects client.publish("subject", ...) and client.subscribe("subject").
/// Emits Topic nodes and PUBLISHES_TO / SUBSCRIBES_TO relationships.
/// </summary>
public class AsyncNatsLibraryParser : ISemanticExtension
{
    public string Id => "async-nats";
    public string Name => "async-nats (NATS)";
    public string Type => "queue";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.MessageBroker;
    public IReadOnlyList<string> SupportedPatterns => ["async-nats*", "async_nats*"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsNatsCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Topic;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsNatsCall(node, out var subject, out _))
        {
            return subject;
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
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

        if (node.Type != TreeSitterSyntax.Rust.CallExpression)
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
