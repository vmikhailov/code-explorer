namespace CodeExplorer.Core.Parser.Layers;

internal static class Layer5ImportCandidateResolver
{
    private const char DotChar = '.';
    private const char SlashChar = '/';
    private const char AtChar = '@';
    private const string WildcardSuffix = "/*";
    private const string SrcIndexSuffix = "src/index";
    private const string IndexSuffix = "index";
    private const string JsExt = ".js";
    private const string TsExt = ".ts";
    private const string TsxExt = ".tsx";

    private static readonly string[] CandidateFileSuffixes =
    [
        string.Empty,
        ".ts",
        ".tsx",
        ".js",
        ".jsx",
        ".mjs",
        ".cjs",
        ".php",
        ".phtml",
        ".inc",
        "/index.ts",
        "/index.tsx",
        "/index.js",
        "/index.jsx"
    ];

    public static List<string> ResolveImportCandidateFiles(
        string callerFile,
        string importPath,
        Layer5AnalysisParser.ImportLookups imports,
        Layer5AnalysisParser.SymbolLookups symbols)
    {
        if (imports.CandidateCache != null && imports.CandidateCache.TryGetValue((callerFile, importPath), out var cached))
        {
            return cached;
        }

        var results = new List<string>();

        if (importPath.StartsWith(DotChar) || importPath.StartsWith(SlashChar))
        {
            var callerDir = Path.GetDirectoryName(callerFile)?.NormalizeSlashes() ?? string.Empty;
            var combined = Layer5AnalysisParser.NormalizeRelativePath(string.IsNullOrEmpty(callerDir)
                ? importPath.TrimStart(DotChar, SlashChar)
                : $"{callerDir}/{importPath}");

            AddFileCandidates(combined, results, symbols);
            imports.CandidateCache?[(callerFile, importPath)] = results;
            return results;
        }

        foreach (var (pattern, target) in imports.TsConfigPaths)
        {
            if (pattern.EndsWith(WildcardSuffix, StringComparison.Ordinal) &&
                target.EndsWith(WildcardSuffix, StringComparison.Ordinal))
            {
                var prefix = pattern[..^2];
                if (importPath.StartWithIC(prefix))
                {
                    var remainder = importPath[prefix.Length..];
                    var mapped = target[..^2] + remainder;
                    AddFileCandidates(Layer5AnalysisParser.NormalizeRelativePath(mapped), results, symbols);
                }
            }
            else if (string.Equals(pattern, importPath, StringComparison.OrdinalIgnoreCase))
            {
                AddFileCandidates(Layer5AnalysisParser.NormalizeRelativePath(target), results, symbols);
            }
        }

        if (results.Count > 0)
        {
            imports.CandidateCache?[(callerFile, importPath)] = results;
            return results;
        }

        if (imports.WorkspacePackages.TryGetValue(importPath, out var pkgInfo))
        {
            AddPackageEntryCandidates(pkgInfo, results, symbols);
            Layer5GoSymbolHelper.AddGoPackageDirectoryCandidates(Layer5AnalysisParser.NormalizeRelativePath(pkgInfo.ProjectPath), results, symbols);
            imports.CandidateCache?[(callerFile, importPath)] = results;
            return results;
        }

        var (matchedPkgKey, matchedPkgInfo) = Layer5GoSymbolHelper.FindLongestWorkspaceModuleMatch(importPath, imports.WorkspacePackages);
        if (matchedPkgKey != null && matchedPkgInfo != null)
        {
            var subPath = importPath[(matchedPkgKey.Length + 1)..];
            var subCombined = Layer5AnalysisParser.NormalizeRelativePath($"{matchedPkgInfo.ProjectPath}/{subPath}");
            AddFileCandidates(subCombined, results, symbols);
            Layer5GoSymbolHelper.AddGoPackageDirectoryCandidates(subCombined, results, symbols);
            AddFileCandidates(Layer5AnalysisParser.NormalizeRelativePath($"{matchedPkgInfo.ProjectPath}/src/{subPath}"), results, symbols);
            imports.CandidateCache?[(callerFile, importPath)] = results;
            return results;
        }

        var slashIdx = importPath.IndexOf(SlashChar);
        if (slashIdx > 0)
        {
            var firstSlash = slashIdx;
            if (importPath.StartsWith(AtChar))
            {
                var secondSlash = importPath.IndexOf(SlashChar, firstSlash + 1);
                if (secondSlash > 0) firstSlash = secondSlash;
            }

            var pkgName = importPath[..firstSlash];
            var subPath = importPath[(firstSlash + 1)..];

            if (imports.WorkspacePackages.TryGetValue(pkgName, out var subPkgInfo))
            {
                var subCombined = Layer5AnalysisParser.NormalizeRelativePath($"{subPkgInfo.ProjectPath}/{subPath}");
                AddFileCandidates(subCombined, results, symbols);
                Layer5GoSymbolHelper.AddGoPackageDirectoryCandidates(subCombined, results, symbols);
                AddFileCandidates(Layer5AnalysisParser.NormalizeRelativePath($"{subPkgInfo.ProjectPath}/src/{subPath}"), results, symbols);
                imports.CandidateCache?[(callerFile, importPath)] = results;
                return results;
            }
        }

        if (results.Count == 0)
        {
            Layer3IncludePathResolver.AppendIncludePathCandidates(
                callerFile,
                importPath,
                results,
                rawCand => AddFileCandidates(Layer5AnalysisParser.NormalizeRelativePath(rawCand), results, symbols),
                symbols.FilesByFileName);
        }

        imports.CandidateCache?[(callerFile, importPath)] = results;
        return results;
    }

    private static void AddPackageEntryCandidates(
        Layer5AnalysisParser.PackageTargetInfo pkgInfo,
        List<string> results,
        Layer5AnalysisParser.SymbolLookups symbols)
    {
        if (!string.IsNullOrEmpty(pkgInfo.MainFile))
        {
            AddFileCandidates(Layer5AnalysisParser.NormalizeRelativePath($"{pkgInfo.ProjectPath}/{pkgInfo.MainFile}"), results, symbols);
        }

        AddFileCandidates(Layer5AnalysisParser.NormalizeRelativePath($"{pkgInfo.ProjectPath}/{SrcIndexSuffix}"), results, symbols);
        AddFileCandidates(Layer5AnalysisParser.NormalizeRelativePath($"{pkgInfo.ProjectPath}/{IndexSuffix}"), results, symbols);
    }

    private static void AddFileCandidates(
        string basePath,
        List<string> results,
        Layer5AnalysisParser.SymbolLookups symbols)
    {
        for (var i = 0; i < CandidateFileSuffixes.Length; i++)
        {
            var cand = i == 0 ? basePath : basePath + CandidateFileSuffixes[i];
            if (symbols.KnownFiles.Contains(cand) && !results.Contains(cand))
            {
                results.Add(cand);
            }
        }

        if (basePath.EndsWith(JsExt, StringComparison.OrdinalIgnoreCase))
        {
            var noExt = basePath[..^3];
            var tsCand = noExt + TsExt;
            if (symbols.KnownFiles.Contains(tsCand) && !results.Contains(tsCand))
            {
                results.Add(tsCand);
            }
            var tsxCand = noExt + TsxExt;
            if (symbols.KnownFiles.Contains(tsxCand) && !results.Contains(tsxCand))
            {
                results.Add(tsxCand);
            }
        }
    }
}
