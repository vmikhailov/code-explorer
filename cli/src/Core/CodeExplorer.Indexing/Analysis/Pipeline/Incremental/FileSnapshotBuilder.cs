using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser.Incremental;

internal static class FileSnapshotBuilder
{
    private const string SemanticFootprintName = "<semantic>";
    private const string AnonymousFunctionPrefix = "<anon_";
    private const string SummarySeparator = ";";

    public static FileGraphSnapshot BuildFromSyntaxTree(
        SyntaxTree syntaxTree,
        string relPath,
        string contentHash,
        DateTime lastModifiedUtc,
        bool includeSemanticFootprint)
    {
        var symbols = new Dictionary<string, SymbolFootprint>(StringComparer.Ordinal);
        if (syntaxTree.FileNode != null)
        {
            CollectSymbols(syntaxTree.FileNode, syntaxTree.FileNode.References, symbols);
        }

        if (includeSemanticFootprint && syntaxTree.SemanticSymbols.Count > 0)
        {
            var semSummary = string.Join(
                SummarySeparator,
                syntaxTree.SemanticSymbols
                    .Select(s => $"{s.Kind}:{s.Name}:{s.Callee}")
                    .OrderBy(x => x, StringComparer.Ordinal));
            var semHash = HashUtility.ComputeSha256(semSummary);
            var semKey = $"{relPath}:{SemanticFootprintName}";
            symbols[semKey] = new SymbolFootprint(
                semKey,
                SemanticFootprintName,
                NodeKind.File,
                semHash,
                semHash,
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                0, 0, 0, 0);
        }

        return new FileGraphSnapshot(relPath, contentHash, lastModifiedUtc, symbols);
    }

    public static void CollectSymbols(
        IOntologyNode node,
        List<CodeExplorer.Common.Reference> references,
        Dictionary<string, SymbolFootprint> symbols,
        ILookup<string, CodeExplorer.Common.Reference>? refsByScope = null)
    {
        refsByScope ??= references.ToLookup(r => r.ScopeSymbolId ?? string.Empty, StringComparer.Ordinal);
        foreach (var child in node.Children)
        {
            if (!IsTrackableSymbolNode(child))
            {
                if (child.Children.Count > 0)
                {
                    CollectSymbols(child, references, symbols, refsByScope);
                }
                continue;
            }

            var footprint = CreateSymbolFootprint(child, refsByScope);
            symbols[footprint.SymbolId] = footprint;

            if (child.Children.Count > 0)
            {
                CollectSymbols(child, references, symbols, refsByScope);
            }
        }
    }

    private static bool IsTrackableSymbolNode(IOntologyNode child)
    {
        if (child is FunctionNode { Name: var fnName } &&
            fnName.StartsWith(AnonymousFunctionPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return child is FunctionNode
            or TypeNode
            or MemberNode
            {
                MemberKind: MemberKind.Field
                    or MemberKind.Property
                    or MemberKind.Event
                    or MemberKind.Constant
                    or MemberKind.EnumMember
            };
    }

    private static SymbolFootprint CreateSymbolFootprint(
        IOntologyNode child,
        ILookup<string, CodeExplorer.Common.Reference> refsByScope)
    {
        var (name, startLine, endLine, startCol, endCol) = child switch
        {
            FunctionNode fn => (fn.Name, fn.StartLine, fn.EndLine, fn.StartCol, fn.EndCol),
            TypeNode tn => (tn.Name, tn.StartLine, tn.EndLine, tn.StartCol, tn.EndCol),
            MemberNode mn => (mn.Name, mn.StartLine, mn.EndLine, mn.StartCol, mn.EndCol),
            _ => (child.Id, 0, 0, 0, 0)
        };

        var symbolKey = StripLineSuffix(child.Id);
        var symbolRefs = child.References.Concat(refsByScope[child.Id]);

        var calls = symbolRefs
            .Where(r => r.RelationshipKind is RelationshipKind.Calls
                or RelationshipKind.CalledBy
                or RelationshipKind.CallsEndpoint
                or RelationshipKind.ServiceCall
                or RelationshipKind.TransitivelyCalls)
            .Select(r => r.TargetName)
            .ToHashSet(StringComparer.Ordinal);

        var types = symbolRefs
            .Where(r => r.RelationshipKind is RelationshipKind.UsesType or RelationshipKind.OfType)
            .Select(r => r.TargetName)
            .ToHashSet(StringComparer.Ordinal);

        var sig = $"{child.Kind.ToCypherLabel()}:{name}";
        var sigHash = HashUtility.ComputeSha256(sig);

        var bodySummary = string.Join(SummarySeparator, calls.OrderBy(x => x, StringComparer.Ordinal).Concat(types.OrderBy(x => x, StringComparer.Ordinal)));
        var bodyHash = HashUtility.ComputeSha256(bodySummary);

        return new SymbolFootprint(
            symbolKey,
            name,
            child.Kind,
            sigHash,
            bodyHash,
            calls,
            types,
            startLine,
            endLine,
            startCol,
            endCol);
    }

    private static string StripLineSuffix(string symbolId)
    {
        var atIdx = symbolId.LastIndexOf('@');
        if (atIdx <= 0) return symbolId;
        for (var i = atIdx + 1; i < symbolId.Length; i++)
        {
            if (!char.IsDigit(symbolId[i])) return symbolId;
        }
        return symbolId[..atIdx];
    }
}
