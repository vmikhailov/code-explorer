using CodeExplorer.Parsers.Terraform.Hcl;

namespace CodeExplorer.Parsers.Terraform;

internal sealed class TerraformWorkspaceCache(
    int totalTreeCount,
    int totalFileCount,
    Dictionary<string, TerraformModuleMemoryState> moduleStates,
    HashSet<string> fileSet,
    HashSet<string> dirSet,
    HashSet<string> duplicateResourceShortNames)
{
    private const string WorkspaceCacheKey = "terraform.workspace_cache";
    private const string BlockTerraform = "terraform";
    private const string BlockInclude = "include";
    private const string BlockDependency = "dependency";
    private const string BlockLocals = "locals";
    private const string AttrConfigPath = "config_path";
    private const string AttrSource = "source";
    private const string AttrPath = "path";
    private const string AttrInputs = "inputs";
    private const string ExtHcl = ".hcl";
    private const string DefaultIncludeLabel = "root";
    private const string DotPath = ".";
    private const char SlashChar = '/';
    private const char BackslashChar = '\\';

    public int TotalTreeCount { get; } = totalTreeCount;
    public int TotalFileCount { get; } = totalFileCount;
    public Dictionary<string, TerraformModuleMemoryState> ModuleStates { get; } = moduleStates;
    public HashSet<string> FileSet { get; } = fileSet;
    public HashSet<string> DirSet { get; } = dirSet;
    public HashSet<string> DuplicateResourceShortNames { get; } = duplicateResourceShortNames;

    public static TerraformWorkspaceCache GetOrBuild(ProjectEnrichmentContext context)
    {
        var totalTreeCount = 0;
        foreach (var (_, trees) in context.TreesByProjectId)
        {
            totalTreeCount += trees.Count;
        }

        var totalFileCount = context.AllFiles.Count;
        if (context.SharedEnrichmentCache != null &&
            context.SharedEnrichmentCache.TryGetValue(WorkspaceCacheKey, out var existing) &&
            existing is TerraformWorkspaceCache cached &&
            cached.TotalTreeCount == totalTreeCount &&
            cached.TotalFileCount == totalFileCount)
        {
            return cached;
        }

        var moduleStates = BuildInMemoryWorkspaceModules(context);
        var fileSet = BuildInMemoryFileSet(context);
        var dirSet = BuildInMemoryDirSet(context, moduleStates.Keys);
        var duplicateShortNames = BuildDuplicateResourceShortNames(moduleStates);

        var built = new TerraformWorkspaceCache(
            totalTreeCount,
            totalFileCount,
            moduleStates,
            fileSet,
            dirSet,
            duplicateShortNames);

        if (context.SharedEnrichmentCache != null)
        {
            context.SharedEnrichmentCache[WorkspaceCacheKey] = built;
        }

        return built;
    }

    public static HclDocumentSyntaxNode? TryGetHclDocument(SyntaxTree tree)
    {
        var root = tree.Document?.RootNode;
        if (root == null) return null;
        if (root is HclDocumentSyntaxNode direct) return direct;
        return root.FirstChildOfType<HclDocumentSyntaxNode>();
    }

    public static string CombineWorkspacePath(string workspaceRoot, string relativePath)
    {
        var normRoot = TerraformPathResolver.NormalizeDirPath(workspaceRoot);
        var normRel = NormalizeRelativeDir(relativePath);
        return string.IsNullOrEmpty(normRel) ? normRoot : $"{normRoot}/{normRel}";
    }

    public static string NormalizeRelativeDir(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == DotPath) return string.Empty;
        return path.Replace(BackslashChar, SlashChar).Trim(SlashChar);
    }

    private static Dictionary<string, TerraformModuleMemoryState> BuildInMemoryWorkspaceModules(ProjectEnrichmentContext context)
    {
        var modules = new Dictionary<string, TerraformModuleMemoryState>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, trees) in context.TreesByProjectId)
        {
            foreach (var tree in trees)
            {
                var hclDoc = TryGetHclDocument(tree);
                if (hclDoc == null || tree.FileNode == null)
                {
                    continue;
                }

                var relPath = tree.FileNode.Path.Replace(BackslashChar, SlashChar);
                var moduleDir = TerraformDeclarationMapper.GetModuleDir(relPath);
                if (!modules.TryGetValue(moduleDir, out var state))
                {
                    state = new TerraformModuleMemoryState { ModuleDir = moduleDir };
                    modules[moduleDir] = state;
                }

                state.HclFiles.Add(hclDoc.HclFile);
                foreach (var sym in hclDoc.Declarations)
                {
                    state.AllSymbols.Add(sym);
                    state.SymbolsByCanonical.TryAdd(sym.CanonicalName, sym);
                }

                ExtractTerragruntMetadata(hclDoc.HclFile, tree.FileNode.Id, state);
            }
        }

        return modules;
    }

    private static void ExtractTerragruntMetadata(HclFile hclFile, string fileNodeId, TerraformModuleMemoryState state)
    {
        if (hclFile.FilePath.EndsWith(ExtHcl, StringComparison.OrdinalIgnoreCase))
        {
            state.TerragruntFileNodeId ??= fileNodeId;
        }

        foreach (var block in hclFile.Blocks)
        {
            if (string.Equals(block.BlockHeader, BlockTerraform, StringComparison.Ordinal))
            {
                var srcAttr = block.FindAttribute(AttrSource);
                if (srcAttr != null)
                {
                    state.TerragruntSourceExpr = srcAttr.Value;
                    state.TerragruntFileNodeId ??= fileNodeId;
                }
            }
            else if (string.Equals(block.BlockHeader, BlockInclude, StringComparison.Ordinal))
            {
                var pathAttr = block.FindAttribute(AttrPath);
                if (pathAttr != null)
                {
                    state.TerragruntIncludePathExprs.Add(pathAttr.Value);
                    var incLabel = block.Labels.Count >= 1 ? block.Labels[0] : DefaultIncludeLabel;
                    state.IncludePathExprsBySymbol[$"include.{incLabel}"] = pathAttr.Value;
                }
            }
            else if (string.Equals(block.BlockHeader, BlockDependency, StringComparison.Ordinal) && block.Labels.Count >= 1)
            {
                var cfgAttr = block.FindAttribute(AttrConfigPath);
                if (cfgAttr != null)
                {
                    state.DependencyConfigPathExprs[$"dependency.{block.Labels[0]}"] = cfgAttr.Value;
                }
            }
            else if (string.Equals(block.BlockHeader, BlockLocals, StringComparison.Ordinal))
            {
                foreach (var attr in block.Attributes)
                {
                    if (attr.Value.TryGetStringLiteral() is { Length: > 0 } strLit)
                    {
                        state.LocalStrings[$"local.{attr.Name}"] = strLit;
                    }
                }
            }
        }

        var inputsAttr = hclFile.FindAttribute(AttrInputs);
        if (inputsAttr != null)
        {
            state.TerragruntFileNodeId = fileNodeId;
            state.TerragruntInputsLine = inputsAttr.Line;
            foreach (var entry in TerraformDeclarationMapper.EnumerateObjectEntries(inputsAttr.Value))
            {
                if (!string.IsNullOrEmpty(entry.Name))
                {
                    state.TerragruntInputs[entry.Name] = entry.Value.CollectReferences(entry.Name);
                }
            }
        }
    }

    private static HashSet<string> BuildDuplicateResourceShortNames(Dictionary<string, TerraformModuleMemoryState> allModules)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, modState) in allModules)
        {
            foreach (var sym in modState.AllSymbols)
            {
                if (sym.Kind == TerraformSymbolKind.Resource && !seen.Add(sym.ShortName))
                {
                    duplicates.Add(sym.ShortName);
                }
            }
        }
        return duplicates;
    }

    private static HashSet<string> BuildInMemoryFileSet(ProjectEnrichmentContext context)
    {
        var set = new HashSet<string>(context.AllFiles.Count + 16, StringComparer.OrdinalIgnoreCase);
        foreach (var file in context.AllFiles)
        {
            if (!string.IsNullOrEmpty(file.FullPath))
            {
                set.Add(TerraformPathResolver.NormalizeDirPath(file.FullPath));
            }
            if (!string.IsNullOrEmpty(file.Path))
            {
                var full = CombineWorkspacePath(context.AbsoluteWorkspacePath, file.Path);
                set.Add(TerraformPathResolver.NormalizeDirPath(full));
            }
        }
        return set;
    }

    private static HashSet<string> BuildInMemoryDirSet(ProjectEnrichmentContext context, IReadOnlyCollection<string> moduleDirs)
    {
        var set = new HashSet<string>(context.AllProjects.Count + moduleDirs.Count + 64, StringComparer.OrdinalIgnoreCase)
        {
            TerraformPathResolver.NormalizeDirPath(context.AbsoluteWorkspacePath)
        };

        foreach (var proj in context.AllProjects)
        {
            set.Add(CombineWorkspacePath(context.AbsoluteWorkspacePath, proj.Path));
        }

        foreach (var modDir in moduleDirs)
        {
            set.Add(CombineWorkspacePath(context.AbsoluteWorkspacePath, modDir));
        }

        foreach (var file in context.AllFiles)
        {
            var relDir = TerraformDeclarationMapper.GetModuleDir(file.Path);
            set.Add(CombineWorkspacePath(context.AbsoluteWorkspacePath, relDir));
        }

        return set;
    }
}
