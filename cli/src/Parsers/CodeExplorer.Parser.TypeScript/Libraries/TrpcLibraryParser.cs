using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class TrpcLibraryParser : DeclarativeSemanticExtension
{
    public TrpcLibraryParser()
    {
        Name = "tRPC";
        Id = "trpc";
        Type = SemanticExtensionType.Framework;
        SupportedPatterns = ["@trpc/server*", "@trpc/client*", "@trpc/react-query*", "@trpc/next*"];
        LibraryRole = LibraryRole.WebService;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression, TreeSitterSyntax.TypeScript.Pair }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => TryExtractTrpcProcedure(node, out _, out _) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = static (node, _, _, _) => TryExtractTrpcProcedure(node, out var kind, out var name) ? $"{kind}:/trpc/{name}" : null,
                CollectReferences = CollectTrpcReferences
            }
        ];
    }

    private static void CollectTrpcReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        // Client call: trpc.user.byId.useQuery(...) or trpcClient.user.create.mutate(...)
        if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName) &&
            propName is "useQuery" or "useMutation" or "useSuspenseQuery" or "mutate" or "mutateAsync" &&
            objNode.IsValid() &&
            objNode.Text.Contains("trpc", StringComparison.OrdinalIgnoreCase))
        {
            var route = objNode.Text;
            var dotIdx = route.IndexOf('.');
            if (dotIdx > 0 && dotIdx < route.Length - 1)
            {
                route = route[(dotIdx + 1)..];
            }
            references.Add(new Reference(scopeSymbolId, "trpc:" + route, OntologyConstants.Relationships.Calls));
        }
    }

    private static bool TryExtractTrpcProcedure(Node node, out string procKind, out string procName)
    {
        procKind = string.Empty;
        procName = string.Empty;

        var callNode = node.Is(TreeSitterSyntax.TypeScript.CallExpression)
            ? node
            : (node.Is(TreeSitterSyntax.TypeScript.Pair)
                ? (node.GetField(TreeSitterSyntax.Fields.Value) ?? (node.Children.Count > 1 ? node.Children[^1] : default))
                : default);

        if (!callNode.IsValid() || !callNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            return false;
        }

        var pairNode = node.Is(TreeSitterSyntax.TypeScript.Pair) ? node : callNode.Parent;
        if (!pairNode.IsValid() || !pairNode.Is(TreeSitterSyntax.TypeScript.Pair))
        {
            return false;
        }

        var keyNode = pairNode.GetField(TreeSitterSyntax.Fields.Key) ?? (pairNode.Children.Count > 0 ? pairNode.Children[0] : default);
        if (!keyNode.IsValid()) return false;

        if (!AstHelper.TryGetMemberAccess(callNode, out var objNode, out var methodName) ||
            methodName is not ("query" or "mutation" or "subscription") ||
            !objNode.IsValid())
        {
            return false;
        }

        var objText = objNode.Text;
        if (!objText.Contains("Procedure", StringComparison.Ordinal) &&
            !objText.Contains("procedure", StringComparison.Ordinal) &&
            !objText.Contains(".input(", StringComparison.Ordinal))
        {
            return false;
        }

        procKind = methodName.ToUpperInvariant();
        procName = keyNode.Text.Trim('"', '\'', '`');
        return !string.IsNullOrEmpty(procName);
    }
}
