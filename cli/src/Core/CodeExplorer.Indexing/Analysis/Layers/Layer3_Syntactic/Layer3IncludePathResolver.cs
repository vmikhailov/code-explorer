using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

internal static class Layer3IncludePathResolver
{
    private const string PhpExt = ".php";
    private const string PhtmlExt = ".phtml";
    private const string IncExt = ".inc";
    private const string TplExt = ".tpl";
    private const string PlExt = ".pl";
    private const string PmExt = ".pm";
    private const string ParentDirPrefix = "../";
    private const string CurrentDirPrefix = "./";
    private const string DotSeg = ".";
    private const string ParentSeg = "..";
    private const char SlashChar = '/';
    private const char BackslashChar = '\\';

    private static readonly string[] CandidateSuffixExtensions =
    [
        ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs",
        ".php", ".phtml", ".inc", ".py", ".pl", ".pm",
        "/index.ts", "/index.tsx", "/index.js", "/index.jsx", "/index.php"
    ];

    public static bool IsIncludeFilePath(string path) =>
        path.EndsWith(PhpExt, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(PhtmlExt, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(IncExt, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(TplExt, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(PlExt, StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(PmExt, StringComparison.OrdinalIgnoreCase);

    public static string? TryResolveRelativeFileImport(
        string importerRelativePath,
        string rawImportSpec,
        Dictionary<string, FileNode> fileNodeByPath)
    {
        var lastSlash = importerRelativePath.LastIndexOf(SlashChar);
        var baseDir = lastSlash >= 0 ? importerRelativePath[..lastSlash] : string.Empty;
        var normSpec = rawImportSpec.Replace(BackslashChar, SlashChar);
        var combined = string.IsNullOrEmpty(baseDir) ? normSpec : $"{baseDir}/{normSpec}";

        var stack = new List<string>();
        foreach (var seg in combined.Split(SlashChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg == DotSeg) continue;
            if (seg == ParentSeg)
            {
                if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(seg);
        }

        if (stack.Count == 0) return null;
        var normalizedCandidate = string.Join(SlashChar, stack);

        if (fileNodeByPath.ContainsKey(normalizedCandidate)) return normalizedCandidate;

        for (var i = 0; i < CandidateSuffixExtensions.Length; i++)
        {
            var withExt = normalizedCandidate + CandidateSuffixExtensions[i];
            if (fileNodeByPath.ContainsKey(withExt)) return withExt;
        }

        return null;
    }

    public static Dictionary<string, List<string>> BuildFileSuffixIndex(IEnumerable<string> allFilePaths)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawPath in allFilePaths)
        {
            var norm = rawPath.Replace(BackslashChar, SlashChar).Trim(SlashChar);
            if (string.IsNullOrEmpty(norm)) continue;

            var fileName = Path.GetFileName(norm);
            if (!index.TryGetValue(fileName, out var list))
            {
                list = [];
                index[fileName] = list;
            }
            list.Add(norm);
        }
        return index;
    }

    public static string? TryResolveIncludePathSuffix(
        string importerRelativePath,
        string rawImportSpec,
        Dictionary<string, FileNode> fileNodeByPath,
        Dictionary<string, List<string>> suffixIndex)
    {
        var cleanSpec = rawImportSpec.Replace(BackslashChar, SlashChar).Trim(SlashChar);
        while (cleanSpec.StartsWith(ParentDirPrefix, StringComparison.Ordinal))
        {
            cleanSpec = cleanSpec[3..];
        }
        if (cleanSpec.StartsWith(CurrentDirPrefix, StringComparison.Ordinal))
        {
            cleanSpec = cleanSpec[2..];
        }
        if (string.IsNullOrEmpty(cleanSpec)) return null;

        if (fileNodeByPath.ContainsKey(cleanSpec)) return cleanSpec;

        var fileName = Path.GetFileName(cleanSpec);
        if (!suffixIndex.TryGetValue(fileName, out var candidates) || candidates.Count == 0)
        {
            return null;
        }

        var suffixWithSlash = $"/{cleanSpec}";
        string? bestMatch = null;
        var bestCommonPrefix = -1;
        for (var i = 0; i < candidates.Count; i++)
        {
            var cand = candidates[i];
            if (string.Equals(cand, importerRelativePath, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(cand, cleanSpec, StringComparison.OrdinalIgnoreCase) ||
                cand.EndsWith(suffixWithSlash, StringComparison.OrdinalIgnoreCase))
            {
                var common = CountCommonPathPrefix(importerRelativePath, cand);
                if (common > bestCommonPrefix)
                {
                    bestCommonPrefix = common;
                    bestMatch = cand;
                }
            }
        }

        if (bestMatch != null) return bestMatch;
        return candidates.Count == 1 && !string.Equals(candidates[0], importerRelativePath, StringComparison.OrdinalIgnoreCase)
            ? candidates[0]
            : null;
    }

    public static void AppendIncludePathCandidates(
        string callerFile,
        string importPath,
        List<string> results,
        Action<string> addFileCandidates,
        IReadOnlyDictionary<string, List<string>> filesByFileName)
    {
        var cleanImport = importPath.Replace(BackslashChar, SlashChar).Trim(SlashChar);
        while (cleanImport.StartsWith(ParentDirPrefix, StringComparison.Ordinal))
        {
            cleanImport = cleanImport[3..];
        }
        if (cleanImport.StartsWith(CurrentDirPrefix, StringComparison.Ordinal))
        {
            cleanImport = cleanImport[2..];
        }
        if (string.IsNullOrEmpty(cleanImport)) return;

        var callerDir = Path.GetDirectoryName(callerFile)?.NormalizeSlashes() ?? string.Empty;
        if (!string.IsNullOrEmpty(callerDir))
        {
            addFileCandidates($"{callerDir}/{cleanImport}");
        }
        addFileCandidates(cleanImport);

        if (results.Count == 0 && IsIncludeFilePath(cleanImport))
        {
            var fileName = Path.GetFileName(cleanImport);
            if (!string.IsNullOrEmpty(fileName) && filesByFileName.TryGetValue(fileName, out var indexedCandidates))
            {
                var suffix = $"/{cleanImport}";
                for (var i = 0; i < indexedCandidates.Count; i++)
                {
                    var known = indexedCandidates[i];
                    if ((string.Equals(known, cleanImport, StringComparison.OrdinalIgnoreCase) ||
                         known.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) &&
                        !results.Contains(known))
                    {
                        results.Add(known);
                    }
                }
            }
        }
    }

    private static int CountCommonPathPrefix(string a, string b)
    {
        var len = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < len && char.ToLowerInvariant(a[i]) == char.ToLowerInvariant(b[i]))
        {
            i++;
        }
        return i;
    }
}
