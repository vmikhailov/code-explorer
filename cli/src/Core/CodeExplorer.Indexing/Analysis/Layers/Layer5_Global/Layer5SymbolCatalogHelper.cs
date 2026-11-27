using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser.Layers;

internal static class Layer5SymbolCatalogHelper
{
    private const char BackslashChar = '\\';
    private const char SlashChar = '/';
    private const char DotChar = '.';
    private const char ColonChar = ':';
    private const string GoExt = ".go";
    private const string GoTestExt = "_test.go";

    public static (
        Dictionary<string, List<string>> FilesByFileName,
        Dictionary<string, List<string>> GoFilesByDir) BuildFileLookupIndices(HashSet<string> knownFiles)
    {
        var filesByFileName = new Dictionary<string, List<string>>(knownFiles.Count / 2 + 16, StringComparer.OrdinalIgnoreCase);
        var goFilesByDir = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawPath in knownFiles)
        {
            var norm = rawPath.IndexOf(BackslashChar) >= 0
                ? rawPath.Replace(BackslashChar, SlashChar).Trim(SlashChar)
                : rawPath.Trim(SlashChar);
            if (string.IsNullOrEmpty(norm)) continue;

            var fileName = Path.GetFileName(norm);
            if (!string.IsNullOrEmpty(fileName))
            {
                if (!filesByFileName.TryGetValue(fileName, out var nameList))
                {
                    nameList = [];
                    filesByFileName[fileName] = nameList;
                }
                nameList.Add(norm);
            }

            if (norm.EndsWith(GoExt, StringComparison.OrdinalIgnoreCase) &&
                !norm.EndsWith(GoTestExt, StringComparison.OrdinalIgnoreCase))
            {
                var lastSlash = norm.LastIndexOf(SlashChar);
                var fileDir = lastSlash > 0 ? norm[..lastSlash] : string.Empty;
                if (!goFilesByDir.TryGetValue(fileDir, out var goList))
                {
                    goList = [];
                    goFilesByDir[fileDir] = goList;
                }
                goList.Add(norm);
            }
        }

        return (filesByFileName, goFilesByDir);
    }

    public static void DeduplicateStringLists<TKey>(Dictionary<TKey, List<string>> dict, StringComparer itemComparer)
        where TKey : notnull
    {
        HashSet<string>? reusableSet = null;
        foreach (var list in dict.Values)
        {
            if (list.Count <= 1) continue;
            if (list.Count == 2)
            {
                if (itemComparer.Equals(list[0], list[1]))
                {
                    list.RemoveAt(1);
                }
                continue;
            }

            reusableSet ??= new HashSet<string>(itemComparer);
            reusableSet.Clear();
            var writeIdx = 0;
            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (reusableSet.Add(item))
                {
                    list[writeIdx++] = item;
                }
            }
            if (writeIdx < list.Count)
            {
                list.RemoveRange(writeIdx, list.Count - writeIdx);
            }
        }
    }

    public static bool MatchesScopeIdZeroAlloc(
        string scopeSymbolId,
        string? normCallerFile,
        string? enclosingTypeName,
        string scopeId,
        string? scopeMarker)
    {
        if (string.IsNullOrEmpty(scopeId)) return false;

        if (!string.IsNullOrEmpty(scopeMarker) &&
            scopeSymbolId.Contains(scopeMarker, StringComparison.Ordinal))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(enclosingTypeName) &&
            string.Equals(enclosingTypeName, scopeId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (ContainsColonSegmentIgnoreCase(scopeSymbolId.AsSpan(), scopeId.AsSpan()))
        {
            return true;
        }

        if (ContainsFileStemSegmentIgnoreCase(scopeSymbolId.AsSpan(), scopeId.AsSpan()))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(normCallerFile) &&
            ContainsFileStemSegmentIgnoreCase(normCallerFile.AsSpan(), scopeId.AsSpan()))
        {
            return true;
        }

        return false;
    }

    public static List<Layer5AnalysisParser.IndexedBinding>? CollectScopedGlobalBindingCandidates(
        Dictionary<string, List<Layer5AnalysisParser.IndexedBinding>> byScope,
        string scopeSymbolId,
        string? normCallerFile,
        string? enclosingTypeName,
        Dictionary<string, List<string>> baseTypesByDerived)
    {
        List<Layer5AnalysisParser.IndexedBinding>? result = null;
        HashSet<string>? probedScopes = null;

        void Probe(ReadOnlySpan<char> tokenSpan)
        {
            if (tokenSpan.IsEmpty) return;
            var alt = byScope.GetAlternateLookup<ReadOnlySpan<char>>();
            if (!alt.TryGetValue(tokenSpan, out var matchedKey, out var matchedList)) return;
            probedScopes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!probedScopes.Add(matchedKey)) return;
            result ??= new List<Layer5AnalysisParser.IndexedBinding>(matchedList.Count);
            result.AddRange(matchedList);
        }

        if (!string.IsNullOrEmpty(enclosingTypeName))
        {
            Probe(enclosingTypeName.AsSpan());
            if (baseTypesByDerived.TryGetValue(enclosingTypeName, out var baseTypes))
            {
                for (var i = 0; i < baseTypes.Count; i++)
                {
                    Probe(baseTypes[i].AsSpan());
                }
            }
        }

        var symSpan = scopeSymbolId.AsSpan();
        var start = 0;
        for (var i = 0; i <= symSpan.Length; i++)
        {
            if (i == symSpan.Length || symSpan[i] == ColonChar)
            {
                if (i > start)
                {
                    var seg = symSpan[start..i];
                    Probe(seg);
                    ProbeFileStemsInPath(seg, ref result, ref probedScopes, byScope);
                }
                start = i + 1;
            }
        }

        if (!string.IsNullOrEmpty(normCallerFile))
        {
            ProbeFileStemsInPath(normCallerFile.AsSpan(), ref result, ref probedScopes, byScope);
        }

        return result;
    }

    private static void ProbeFileStemsInPath(
        ReadOnlySpan<char> pathSpan,
        ref List<Layer5AnalysisParser.IndexedBinding>? result,
        ref HashSet<string>? probedScopes,
        Dictionary<string, List<Layer5AnalysisParser.IndexedBinding>> byScope)
    {
        var alt = byScope.GetAlternateLookup<ReadOnlySpan<char>>();
        for (var i = 0; i < pathSpan.Length; i++)
        {
            if (pathSpan[i] is SlashChar or BackslashChar && i + 1 < pathSpan.Length)
            {
                var rest = pathSpan[(i + 1)..];
                var nextDot = rest.IndexOf(DotChar);
                if (nextDot > 0)
                {
                    var stem = rest[..nextDot];
                    if (alt.TryGetValue(stem, out var matchedKey, out var matchedList))
                    {
                        probedScopes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (probedScopes.Add(matchedKey))
                        {
                            result ??= new List<Layer5AnalysisParser.IndexedBinding>(matchedList.Count);
                            result.AddRange(matchedList);
                        }
                    }
                }
            }
        }
    }

    private static bool ContainsColonSegmentIgnoreCase(ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle)
    {
        var idx = 0;
        while (idx < haystack.Length)
        {
            var rel = haystack[idx..].IndexOf(ColonChar);
            if (rel < 0) break;
            var afterColon = idx + rel + 1;
            if (afterColon + needle.Length <= haystack.Length &&
                haystack.Slice(afterColon, needle.Length).Equals(needle, StringComparison.OrdinalIgnoreCase))
            {
                var endPos = afterColon + needle.Length;
                if (endPos == haystack.Length || haystack[endPos] == ColonChar)
                {
                    return true;
                }
            }
            idx = afterColon;
        }
        return false;
    }

    private static bool ContainsFileStemSegmentIgnoreCase(ReadOnlySpan<char> haystack, ReadOnlySpan<char> needle)
    {
        for (var i = 0; i + 1 + needle.Length < haystack.Length; i++)
        {
            if (haystack[i] is SlashChar or BackslashChar &&
                haystack[i + 1 + needle.Length] == DotChar &&
                haystack.Slice(i + 1, needle.Length).Equals(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static void PopulateMembersAndFriendsFromSummaries(
        IReadOnlyList<ProjectScanSummary> summaries,
        Dictionary<(string FilePath, string TypeName, string MemberName), string> membersByType,
        List<(string MemberId, string TypeId, string DataType)> memberTypeCandidates,
        Dictionary<string, IReadOnlySet<string>> friendAssembliesByProject)
    {
        var typeInfoById = new Dictionary<string, (string Name, string NormFile)>(StringComparer.Ordinal);
        for (var s = 0; s < summaries.Count; s++)
        {
            var summary = summaries[s];
            if (summary.FriendAssemblies.Count > 0 && !string.IsNullOrEmpty(summary.ProjectName))
            {
                friendAssembliesByProject[summary.ProjectName] = summary.FriendAssemblies;
            }

            var stubs = summary.AddressableSymbols;
            for (var i = 0; i < stubs.Count; i++)
            {
                var stub = stubs[i];
                if (stub.Kind == NodeKind.Type)
                {
                    typeInfoById[stub.Id] = (stub.Name, (stub.FilePath ?? string.Empty).Replace(BackslashChar, SlashChar));
                }
            }
        }

        for (var s = 0; s < summaries.Count; s++)
        {
            var stubs = summaries[s].AddressableSymbols;
            for (var i = 0; i < stubs.Count; i++)
            {
                var stub = stubs[i];
                if (stub.Kind != NodeKind.Member || string.IsNullOrEmpty(stub.EnclosingTypeId)) continue;
                if (!typeInfoById.TryGetValue(stub.EnclosingTypeId, out var ownerType)) continue;

                membersByType.TryAdd((ownerType.NormFile, ownerType.Name, stub.Name), stub.Id);
                membersByType.TryAdd((string.Empty, ownerType.Name, stub.Name), stub.Id);
                if (!string.IsNullOrWhiteSpace(stub.DeclaredTypeExpr))
                {
                    memberTypeCandidates.Add((stub.Id, stub.EnclosingTypeId, stub.DeclaredTypeExpr));
                }
            }
        }
    }

    public static void CollectMemberSymbols(
        IOntologyNode node,
        TypeNode? enclosingType,
        Dictionary<(string FilePath, string TypeName, string MemberName), string> membersByType,
        List<(string MemberId, string TypeId, string DataType)> memberTypeCandidates)
    {
        if (node is TypeNode tn)
        {
            enclosingType = tn;
        }
        else if (node is MemberNode mn && enclosingType != null)
        {
            var normFile = (enclosingType.FilePath ?? string.Empty).Replace(BackslashChar, SlashChar);
            membersByType.TryAdd((normFile, enclosingType.Name, mn.Name), mn.Id);
            membersByType.TryAdd((string.Empty, enclosingType.Name, mn.Name), mn.Id);
            if (!string.IsNullOrWhiteSpace(mn.DataType))
            {
                memberTypeCandidates.Add((mn.Id, enclosingType.Id, mn.DataType));
            }
        }

        foreach (var child in node.Children)
        {
            CollectMemberSymbols(child, enclosingType, membersByType, memberTypeCandidates);
        }
    }

    public static void CollectTableAndProcedureSymbols(
        IOntologyNode node,
        Dictionary<string, string> tableSymbols,
        Dictionary<string, string> procedureSymbols)
    {
        if (node is TableNode tbl && !string.IsNullOrEmpty(tbl.Name))
        {
            tableSymbols.TryAdd(tbl.Name, tbl.Id);
            if (!string.IsNullOrEmpty(tbl.Schema))
            {
                tableSymbols.TryAdd(string.Concat(tbl.Schema, DotChar, tbl.Name), tbl.Id);
            }
        }
        else if (node is ProcedureNode proc && !string.IsNullOrEmpty(proc.Name))
        {
            procedureSymbols.TryAdd(proc.Name, proc.Id);
            if (!string.IsNullOrEmpty(proc.Schema))
            {
                procedureSymbols.TryAdd(string.Concat(proc.Schema, DotChar, proc.Name), proc.Id);
            }
        }

        foreach (var child in node.Children)
        {
            CollectTableAndProcedureSymbols(child, tableSymbols, procedureSymbols);
        }
    }
}
