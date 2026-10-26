using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Hyper HTTP client in Rust.
/// Detects low-level request builder invocations like hyper::Request::builder().uri(url).
/// </summary>
public class HyperLibraryParser : ISemanticExtension
{
    public string Id => "hyper";
    public string Name => "Hyper";
    public string Type => "http-client";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;
    public IReadOnlyList<string> SupportedPatterns => ["hyper*"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsHyperCall(node, out _))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsHyperCall(node, out var target))
        {
            return target;
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsHyperCall(node, out var target) && !string.IsNullOrEmpty(target))
        {
            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
        }
    }

    public static bool IsHyperCall(Node node, out string target)
    {
        target = string.Empty;

        if (node.Type != TreeSitterSyntax.Rust.CallExpression)
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out var receiver, out var methodName, out var args))
        {
            return false;
        }

        if (methodName == "uri" && args.Count > 0 && receiver != null)
        {
            var recText = receiver.Text;
            if (recText.Contains("Request") || recText.Contains("builder") || recText.Contains("hyper"))
            {
                var uri = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(uri))
                {
                    target = RustAstHelper.ExtractHostOrUrl(uri);
                    return true;
                }
            }
        }

        return false;
    }
}
