using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Tonic gRPC framework in Rust.
/// Detects server gRPC service implementations annotated with #[tonic::async_trait]
/// and client invocations (e.g. GreeterClient::connect(...)).
/// </summary>
public class TonicLibraryParser : ISemanticExtension
{
    public string Id => "tonic";
    public string Name => "Tonic (gRPC)";
    public string Type => "framework";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;
    public IReadOnlyList<string> SupportedPatterns => ["tonic*"];

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsTonicServerMethod(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (IsTonicClientCall(node, out _))
        {
            return OntologyConstants.NodeLabels.ExternalService;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsTonicServerMethod(node, out var traitName, out var methodName))
        {
            return $"RPC:/{traitName}/{methodName}";
        }

        if (IsTonicClientCall(node, out var target))
        {
            return target;
        }

        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsTonicServerMethod(node, out _, out var methodName))
        {
            references.Add(new Reference(scopeSymbolId, methodName, OntologyConstants.Relationships.Triggers));
        }
        else if (IsTonicClientCall(node, out var target))
        {
            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsTonicServerMethod(node, out _, out _) || IsTonicClientCall(node, out _))
        {
            symbol.Protocol = "gRPC";
            symbol.Properties["protocol"] = "gRPC";
        }
    }

    public static bool IsTonicServerMethod(Node node, out string traitName, out string methodName)
    {
        traitName = string.Empty;
        methodName = string.Empty;

        if (node.Type != TreeSitterSyntax.Rust.FunctionItem && node.Type != TreeSitterSyntax.Rust.FunctionSignatureItem)
        {
            return false;
        }

        var parent = node.Parent;
        if (!parent.IsValid()) return false;

        // Ancestor should be impl_item
        Node? implNode = null;
        var curr = parent;
        while (curr.IsValid())
        {
            if (curr.Type == TreeSitterSyntax.Rust.ImplItem)
            {
                implNode = curr;
                break;
            }
            curr = curr.Parent;
        }

        if (implNode == null || !implNode.IsValid()) return false;

        var hasTonicAttr = implNode.Children.Any(c =>
            c.Type == TreeSitterSyntax.Rust.AttributeItem &&
            (c.Text.Contains("tonic::async_trait", StringComparison.OrdinalIgnoreCase) ||
             c.Text.Contains("async_trait", StringComparison.OrdinalIgnoreCase) ||
             c.Text.Contains("tonic", StringComparison.OrdinalIgnoreCase)))
            || implNode.Text.Contains("tonic")
            || HasPrecedingTonicAttribute(implNode);

        if (!hasTonicAttr)
        {
            return false;
        }

        var traitChild = implNode.GetChildForField(TreeSitterSyntax.Rust.Trait);
        if (traitChild.IsValid())
        {
            traitName = traitChild.Text.Split("::").Last().Trim();
        }
        else
        {
            var forIdx = -1;
            for (var i = 0; i < implNode.Children.Count; i++)
            {
                if (implNode.Children[i].Text == "for")
                {
                    forIdx = i;
                    break;
                }
            }

            if (forIdx > 0)
            {
                var candidate = implNode.Children.Take(forIdx)
                    .LastOrDefault(c => c.IsAny(TreeSitterSyntax.Rust.TypeIdentifier, TreeSitterSyntax.Rust.ScopedTypeIdentifier, TreeSitterSyntax.Rust.GenericType, TreeSitterSyntax.Rust.Identifier));
                if (candidate.IsValid())
                {
                    traitName = candidate.Text.Split("::").Last().Trim();
                }
            }
        }

        if (string.IsNullOrEmpty(traitName))
        {
            var text = implNode.Text;
            var implIdx = text.IndexOf("impl ", StringComparison.Ordinal);
            var forIdx = text.IndexOf(" for ", StringComparison.Ordinal);
            if (implIdx >= 0 && forIdx > implIdx)
            {
                traitName = text[(implIdx + 5)..forIdx].Trim().Split("::").Last().Trim();
            }
        }

        if (string.IsNullOrEmpty(traitName)) return false;

        var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                       node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.Identifier);

        if (nameNode.IsValid())
        {
            methodName = nameNode.Text;
        }

        if (string.IsNullOrEmpty(methodName))
        {
            var fnText = node.Text;
            var fnIdx = fnText.IndexOf("fn ", StringComparison.Ordinal);
            if (fnIdx >= 0)
            {
                var parenIdx = fnText.IndexOf('(', fnIdx);
                if (parenIdx > fnIdx)
                {
                    methodName = fnText[(fnIdx + 3)..parenIdx].Trim();
                }
            }
        }

        return !string.IsNullOrEmpty(traitName) && !string.IsNullOrEmpty(methodName);
    }

    public static bool IsTonicClientCall(Node node, out string target)
    {
        target = string.Empty;

        if (node.Type != TreeSitterSyntax.Rust.CallExpression)
        {
            return false;
        }

        if (!RustAstHelper.GetMethodCallInfo(node, out _, out var methodName, out var args))
        {
            return false;
        }

        if (string.IsNullOrEmpty(methodName)) return false;

        if (methodName.EndsWith("Client::connect", StringComparison.OrdinalIgnoreCase) ||
            methodName.EndsWith("connect", StringComparison.OrdinalIgnoreCase) && node.Text.Contains("Client"))
        {
            if (args.Count > 0)
            {
                var hostArg = RustAstHelper.ExtractStringLiteral(args[0]);
                if (!string.IsNullOrEmpty(hostArg))
                {
                    target = RustAstHelper.ExtractHostOrUrl(hostArg);
                    return true;
                }
            }

            var clientPrefix = methodName.Split("::")[0];
            target = clientPrefix;
            return true;
        }

        return false;
    }

    private static bool HasPrecedingTonicAttribute(Node implNode)
    {
        var parent = implNode.Parent;
        if (!parent.IsValid()) return false;
        var children = parent.Children;
        var idx = -1;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].Id == implNode.Id)
            {
                idx = i;
                break;
            }
        }
        if (idx > 0)
        {
            for (var i = idx - 1; i >= 0; i--)
            {
                var prev = children[i];
                if (prev.Type == TreeSitterSyntax.Rust.AttributeItem)
                {
                    if (prev.Text.Contains("tonic", StringComparison.OrdinalIgnoreCase) ||
                        prev.Text.Contains("async_trait", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                else
                {
                    break;
                }
            }
        }
        return false;
    }
}
