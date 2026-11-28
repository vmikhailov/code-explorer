namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer2ProjectParser
{
    private const string ExtOrchestratorKey = "is_orchestrator";
    private const string TrueFlagValue = "true";
    private const string LangTerraform = "terraform";

    private static List<(string Dir, SubtreeLanguageCounts? Counts)> CollectLeafNestedGitRepoNodes(
        IReadOnlyList<ScopedDirectoryTreeNode> roots)
    {
        var results = new List<(string Dir, SubtreeLanguageCounts? Counts)>();
        var stack = new Stack<ScopedDirectoryTreeNode>();
        for (var i = roots.Count - 1; i >= 0; i--)
        {
            stack.Push(roots[i]);
        }

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!current.IsRoot && current.IsGitRepoRoot && current.DescendantGitRepoCount == 0)
            {
                results.Add((current.DirectoryPath, current.LanguageCounts));
                continue;
            }

            for (var i = current.Children.Count - 1; i >= 0; i--)
            {
                stack.Push(current.Children[i]);
            }
        }

        return results;
    }

    private static async Task WalkPreDiscoveredTreeAsync(
        IReadOnlyList<ScopedDirectoryTreeNode> roots,
        DirectoryScanState state)
    {
        for (var i = 0; i < roots.Count; i++)
        {
            await WalkScopedNodeAsync(roots[i], enclosingScope: null, state);
        }
    }

    private static async Task WalkScopedNodeAsync(
        ScopedDirectoryTreeNode node,
        EnclosingProjectScope? enclosingScope,
        DirectoryScanState state)
    {
        if (!node.SubtreeHasCandidateManifest)
        {
            return;
        }

        state.Ctx.CancellationToken.ThrowIfCancellationRequested();

        var dir = node.DirectoryPath;
        if (enclosingScope != null && enclosingScope.IsExcludedSubtree(node.NormalizedPath))
        {
            return;
        }

        var filesInDir = node.FilesInDir;
        if (!node.HasCandidateManifest)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                await WalkScopedNodeAsync(node.Children[i], enclosingScope, state);
            }
            return;
        }

        var isLeafGitRepo = node.IsGitRepoRoot && node.DescendantGitRepoCount == 0;
        var absorbingOwner = ResolveAbsorbingOwner(
            node.NormalizedPath,
            dir,
            filesInDir,
            enclosingScope,
            state,
            precomputedIsLeafGitRepo: isLeafGitRepo,
            precomputedHasSourceFiles: node.HasSourceFiles);
        if (absorbingOwner != null)
        {
            await MergeAbsorbedDependenciesAsync(dir, filesInDir, absorbingOwner, state);
            for (var i = 0; i < node.Children.Count; i++)
            {
                await WalkScopedNodeAsync(node.Children[i], enclosingScope, state);
            }
            return;
        }

        var activeResults = MatchProjectParsersInDirectory(
            dir,
            filesInDir,
            enclosingScope,
            state.Ctx,
            precomputedIsLeafGitRepo: isLeafGitRepo,
            precomputedCounts: node.LanguageCounts);
        if (activeResults.Count == 0)
        {
            if (node.IsRoot || node.HasManifestInDir)
            {
                InspectUmbrellaDirectoryManifests(dir, filesInDir, state);
            }
            for (var i = 0; i < node.Children.Count; i++)
            {
                await WalkScopedNodeAsync(node.Children[i], enclosingScope, state);
            }
            return;
        }

        if (node.IsRoot)
        {
            InspectUmbrellaDirectoryManifests(dir, filesInDir, state, activeResults);
        }

        var nextScope = await InstantiateMatchedProjectsInDirectoryAsync(dir, filesInDir, activeResults, state)
                        ?? enclosingScope;

        for (var i = 0; i < node.Children.Count; i++)
        {
            await WalkScopedNodeAsync(node.Children[i], nextScope, state);
        }
    }

    private static List<(IProjectParser Parser, ProjectParseResult Result)> RankMultiEcosystemMatches(
        string dir,
        List<(IProjectParser Parser, ProjectParseResult Result)> matches,
        SubtreeLanguageCounts? precomputedCounts = null)
    {
        if (matches.Count <= 1 || (precomputedCounts == null && !WorkspaceDirectoryIndex.HasCachedDirectoryInCurrent(dir)))
        {
            return matches;
        }

        var counts = precomputedCounts ?? CountLanguageFilesInSubtree(dir);
        var maxPrimaryAppFiles = 0;
        foreach (var (parser, _) in matches)
        {
            if (ProjectParserPrecedence.GetTier(parser) == ParserTier.Primary &&
                !string.Equals(parser.ProjectType, LangTerraform, StringComparison.OrdinalIgnoreCase))
            {
                var c = GetParserLanguageFileCount(parser.ProjectType, counts);
                if (c > maxPrimaryAppFiles) maxPrimaryAppFiles = c;
            }
        }

        if (maxPrimaryAppFiles == 0)
        {
            return matches;
        }

        var pruned = matches
            .Where(m =>
            {
                if (ProjectParserPrecedence.GetTier(m.Parser) != ParserTier.Primary) return true;
                if (m.Result.ClaimMode == ParserClaimMode.AbsorbSecondaryInDirectory) return true;
                if (m.Result.AdditionalSiblingProjects is { Count: > 0 }) return true;
                if (m.Result.Project.ManifestProperties != null &&
                    m.Result.Project.ManifestProperties.TryGetValue(ExtOrchestratorKey, out var orch) &&
                    orch == TrueFlagValue)
                {
                    return true;
                }
                return GetParserLanguageFileCount(m.Parser.ProjectType, counts) > 0;
            })
            .ToList();

        if (pruned.Count == 0)
        {
            return matches;
        }

        return pruned
            .Select((item, idx) => (
                item,
                idx,
                tier: ProjectParserPrecedence.GetTier(item.Parser) == ParserTier.Primary ? 1 : 0,
                isAppLang: !string.Equals(item.Parser.ProjectType, LangTerraform, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
                count: GetParserLanguageFileCount(item.Parser.ProjectType, counts)))
            .OrderByDescending(x => x.tier)
            .ThenByDescending(x => x.count > 0 ? x.isAppLang : 0)
            .ThenByDescending(x => x.count)
            .ThenBy(x => x.idx)
            .Select(x => x.item)
            .ToList();
    }

    private static SubtreeLanguageCounts CountLanguageFilesInSubtree(string dir)
    {
        int ts = 0, js = 0, py = 0, java = 0, kt = 0, cs = 0, go = 0, rs = 0, php = 0, pl = 0, rb = 0, cpp = 0, swift = 0, tf = 0, cf = 0;
        foreach (var file in WorkspaceDirectoryIndex.EnumerateSubtreeInCurrent(dir))
        {
            switch (Path.GetExtension(file).ToLowerInvariant())
            {
                case ".ts" or ".tsx" or ".mts" or ".cts": ts++; break;
                case ".js" or ".jsx" or ".mjs" or ".cjs": js++; break;
                case ".py" or ".pyi": py++; break;
                case ".java" or ".groovy": java++; break;
                case ".kt": kt++; break;
                case ".cs" or ".razor" or ".cshtml": cs++; break;
                case ".go": go++; break;
                case ".rs": rs++; break;
                case ".php" or ".phtml" or ".inc": php++; break;
                case ".pl" or ".pm": pl++; break;
                case ".rb": rb++; break;
                case ".c" or ".cc" or ".cpp" or ".cxx" or ".h" or ".hpp": cpp++; break;
                case ".swift": swift++; break;
                case ".tf" or ".tfvars" or ".hcl" or ".tofu": tf++; break;
                case ".cfc" or ".cfm": cf++; break;
            }
        }
        return new SubtreeLanguageCounts(ts, js, py, java, kt, cs, go, rs, php, pl, rb, cpp, swift, tf, cf);
    }

    private static int GetParserLanguageFileCount(string projectType, SubtreeLanguageCounts c) =>
        projectType.ToLowerInvariant() switch
        {
            "typescript" => c.TypeScript + c.JavaScript,
            "javascript" => c.JavaScript + c.TypeScript,
            "python" => c.Python,
            "java" => c.Java,
            "kotlin" => c.Kotlin,
            "csharp" => c.CSharp,
            "go" => c.Go,
            "rust" => c.Rust,
            "php" => c.Php,
            "perl" => c.Perl,
            "ruby" => c.Ruby,
            "cpp" => c.Cpp,
            "swift" => c.Swift,
            "terraform" => c.Terraform,
            "coldfusion" => c.ColdFusion,
            _ => 0
        };
}
