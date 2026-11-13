using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.Terraform;
using CodeExplorer.Parser.Terraform.Hcl;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Stateless Layer 2 project parser for Terraform, OpenTofu, and Terragrunt directories.
/// </summary>
[ProjectParser(Tier = ParserTier.Data)]
public sealed class TerraformProjectParser : IProjectParser
{
    private const string TerraformLanguage = "terraform";
    private const string ExtTf = ".tf";
    private const string ExtTofu = ".tofu";
    private const string ExtHcl = ".hcl";
    private const string TerragruntFile = "terragrunt.hcl";
    private const string RootHclFile = "root.hcl";
    private const string LockFileSuffix = ".terraform.lock.hcl";
    private const string TflintFileSuffix = ".tflint.hcl";
    private const string GitDirectoryName = ".git";
    private const string WildcardAllFiles = "*";

    private const string BlockTerraform = "terraform";
    private const string BlockRequiredProviders = "required_providers";
    private const string BlockModule = "module";
    private const string BlockDependency = "dependency";
    private const string BlockDependencies = "dependencies";
    private const string BlockInclude = "include";

    private const string AttrRequiredVersion = "required_version";
    private const string AttrSource = "source";
    private const string AttrVersion = "version";
    private const string AttrConfigPath = "config_path";
    private const string AttrPaths = "paths";
    private const string AttrPath = "path";

    private const string FnReadTerragruntConfig = "read_terragrunt_config";

    private const string PropTerraformKind = "terraform_kind";
    private const string PropRequiredVersion = "required_version";
    private const string KindTerragrunt = "terragrunt";
    private const string KindTerraformAndTerragrunt = "terraform+terragrunt";
    private const string KindOpenTofu = "opentofu";
    private const string KindTerraform = "terraform";
    private const string DefaultVersion = "0.0.0";
    private const string LatestVersion = "latest";
    private const string ProviderPrefix = "provider:";

    private static readonly string[] ExcludedDirectoryNames =
    [
        ".terraform",
        ".terragrunt-cache",
        "terraform.tfstate.d"
    ];

    private static readonly string[] ManifestFiles =
    [
        TerragruntFile,
        RootHclFile
    ];

    private static readonly string[] ManifestExts =
    [
        ExtTf,
        ExtTofu
    ];

    public string ProjectType => TerraformLanguage;

    public IReadOnlyCollection<string> ExcludedFolders => ExcludedDirectoryNames;

    public IReadOnlyCollection<string> ManifestFileNames => ManifestFiles;

    public IReadOnlyCollection<string> ManifestExtensions => ManifestExts;

    public IReadOnlyList<PackageDescriptor> Packages => [];

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(ExtTf, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(ExtTofu, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(TerragruntFile, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.Ordinal);
        var hasTf = filesInDirectory.Any(f => f.EndsWith(ExtTf, StringComparison.OrdinalIgnoreCase));
        var hasTofu = filesInDirectory.Any(f => f.EndsWith(ExtTofu, StringComparison.OrdinalIgnoreCase));
        var hasTg = filesInDirectory.Any(f => Path.GetFileName(f).Equals(TerragruntFile, StringComparison.OrdinalIgnoreCase));

        props[PropTerraformKind] = (hasTg, hasTf || hasTofu) switch
        {
            (true, false) => KindTerragrunt,
            (true, true) => KindTerraformAndTerragrunt,
            (false, _) when hasTofu => KindOpenTofu,
            _ => KindTerraform
        };

        foreach (var file in filesInDirectory)
        {
            if (!IsHclSourceFile(file)) continue;
            var content = SafeReadText(file);
            if (string.IsNullOrEmpty(content)) continue;

            var parsed = HclParser.Parse(content, file);
            foreach (var block in parsed.Blocks)
            {
                if (!string.Equals(block.BlockHeader, BlockTerraform, StringComparison.Ordinal)) continue;
                var reqVer = block.FindAttribute(AttrRequiredVersion)?.Value.TryGetStringLiteral();
                if (!string.IsNullOrEmpty(reqVer))
                {
                    props[PropRequiredVersion] = reqVer;
                }
            }
        }

        return props;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        if (!Directory.Exists(projectDirectory))
        {
            return Task.FromResult<ProducedPackageInfo?>(null);
        }

        var hasModuleFiles = Directory.EnumerateFiles(projectDirectory, WildcardAllFiles, SearchOption.TopDirectoryOnly)
            .Any(f => f.EndsWith(ExtTf, StringComparison.OrdinalIgnoreCase) ||
                      f.EndsWith(ExtTofu, StringComparison.OrdinalIgnoreCase));
        if (!hasModuleFiles)
        {
            return Task.FromResult<ProducedPackageInfo?>(null);
        }

        var name = Path.GetFileName(projectDirectory.TrimEnd('/', '\\'));
        return Task.FromResult<ProducedPackageInfo?>(new ProducedPackageInfo(name, DefaultVersion, TerraformLanguage));
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();
        var seenLocal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPkg = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(projectDirectory))
        {
            return Task.FromResult(new ProjectDependencyInfo(localPaths, externalPackages));
        }

        var normProjectDir = TerraformPathResolver.NormalizeDirPath(Path.GetFullPath(projectDirectory));
        var repoRoot = FindRepoRoot(normProjectDir);

        void AddLocalDir(string? resolvedDir)
        {
            if (string.IsNullOrEmpty(resolvedDir)) return;
            var norm = TerraformPathResolver.NormalizeDirPath(resolvedDir);
            if (string.Equals(norm, normProjectDir, StringComparison.OrdinalIgnoreCase)) return;
            if (seenLocal.Add(norm))
            {
                localPaths.Add(norm);
            }
        }

        void AddPackage(string name, string? version)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (seenPkg.Add(name))
            {
                externalPackages.Add(new ProducedPackageInfo(name, string.IsNullOrWhiteSpace(version) ? LatestVersion : version, TerraformLanguage));
            }
        }

        foreach (var file in Directory.EnumerateFiles(projectDirectory, WildcardAllFiles, SearchOption.TopDirectoryOnly))
        {
            if (!IsHclSourceFile(file)) continue;
            var content = SafeReadText(file);
            if (string.IsNullOrEmpty(content)) continue;

            var hclFile = HclParser.Parse(content, file);
            ExtractBlockDependencies(hclFile, normProjectDir, repoRoot, AddLocalDir, AddPackage);
            ExtractReadTerragruntConfigDependencies(hclFile, normProjectDir, repoRoot, AddLocalDir);
        }

        return Task.FromResult(new ProjectDependencyInfo(localPaths, externalPackages));
    }

    public Task<ProjectEnrichmentResult> EnrichProjectAsync(ProjectEnrichmentContext context) =>
        Task.FromResult(TerraformSemanticEnricher.Enrich(context));

    private static void ExtractBlockDependencies(
        HclFile hclFile,
        string projectDir,
        string repoRoot,
        Action<string?> addLocalDir,
        Action<string, string?> addPackage)
    {
        foreach (var block in hclFile.Blocks)
        {
            switch (block.BlockHeader)
            {
                case BlockModule:
                    ExtractModuleDependency(block, projectDir, repoRoot, addLocalDir, addPackage);
                    break;

                case BlockTerraform:
                    ExtractTerraformBlockDependencies(block, projectDir, repoRoot, addLocalDir, addPackage);
                    break;

                case BlockDependency:
                    var cfgExpr = block.FindAttribute(AttrConfigPath)?.Value;
                    addLocalDir(TerraformPathResolver.EvaluatePathExpr(cfgExpr, projectDir, repoRoot, File.Exists, Directory.Exists));
                    break;

                case BlockDependencies:
                    if (block.FindAttribute(AttrPaths)?.Value is HclExpr.Tuple pathsTuple)
                    {
                        foreach (var elem in pathsTuple.Elements)
                        {
                            addLocalDir(TerraformPathResolver.EvaluatePathExpr(elem, projectDir, repoRoot, File.Exists, Directory.Exists));
                        }
                    }
                    break;

                case BlockInclude:
                    var incPathExpr = block.FindAttribute(AttrPath)?.Value;
                    var incResolved = TerraformPathResolver.EvaluatePathExpr(incPathExpr, projectDir, repoRoot, File.Exists, Directory.Exists);
                    addLocalDir(ToParentDirIfFile(incResolved));
                    break;
            }
        }
    }

    private static void ExtractModuleDependency(
        HclBlock block,
        string projectDir,
        string repoRoot,
        Action<string?> addLocalDir,
        Action<string, string?> addPackage)
    {
        var sourceRaw = block.FindAttribute(AttrSource)?.Value.TryGetStringLiteral();
        if (string.IsNullOrWhiteSpace(sourceRaw)) return;

        var version = block.FindAttribute(AttrVersion)?.Value.TryGetStringLiteral();
        if (TerraformPathResolver.IsLocalModuleSource(sourceRaw))
        {
            var resolved = TerraformPathResolver.ResolveStringPath(sourceRaw, projectDir, repoRoot, File.Exists, Directory.Exists);
            addLocalDir(resolved);
        }
        else
        {
            addPackage(sourceRaw, version);
        }
    }

    private static void ExtractTerraformBlockDependencies(
        HclBlock block,
        string projectDir,
        string repoRoot,
        Action<string?> addLocalDir,
        Action<string, string?> addPackage)
    {
        var sourceExpr = block.FindAttribute(AttrSource)?.Value;
        if (sourceExpr != null)
        {
            var rawLit = sourceExpr.TryGetStringLiteral();
            if (!string.IsNullOrEmpty(rawLit) && TerraformPathResolver.IsRemoteSource(rawLit))
            {
                addPackage(rawLit, null);
            }
            else
            {
                addLocalDir(TerraformPathResolver.EvaluatePathExpr(sourceExpr, projectDir, repoRoot, File.Exists, Directory.Exists));
            }
        }

        foreach (var nested in block.NestedBlocks)
        {
            if (!string.Equals(nested.BlockHeader, BlockRequiredProviders, StringComparison.Ordinal)) continue;
            foreach (var provAttr in nested.Attributes)
            {
                string? provSource = null;
                string? provVersion = null;
                if (provAttr.Value is HclExpr.ObjectExpr obj)
                {
                    provSource = obj.Entries.FirstOrDefault(e => e.Name == AttrSource)?.Value.TryGetStringLiteral();
                    provVersion = obj.Entries.FirstOrDefault(e => e.Name == AttrVersion)?.Value.TryGetStringLiteral();
                }
                else
                {
                    provVersion = provAttr.Value.TryGetStringLiteral();
                }

                var pkgName = string.IsNullOrWhiteSpace(provSource)
                    ? $"{ProviderPrefix}{provAttr.Name}"
                    : provSource;
                addPackage(pkgName, provVersion);
            }
        }
    }

    private static void ExtractReadTerragruntConfigDependencies(
        HclFile hclFile,
        string projectDir,
        string repoRoot,
        Action<string?> addLocalDir)
    {
        foreach (var attr in hclFile.Attributes)
        {
            CollectReadTerragruntConfigFromExpr(attr.Value, projectDir, repoRoot, addLocalDir);
        }

        foreach (var block in hclFile.Blocks)
        {
            CollectReadTerragruntConfigFromBlock(block, projectDir, repoRoot, addLocalDir);
        }
    }

    private static void CollectReadTerragruntConfigFromBlock(
        HclBlock block,
        string projectDir,
        string repoRoot,
        Action<string?> addLocalDir)
    {
        foreach (var attr in block.Attributes)
        {
            CollectReadTerragruntConfigFromExpr(attr.Value, projectDir, repoRoot, addLocalDir);
        }

        foreach (var nested in block.NestedBlocks)
        {
            CollectReadTerragruntConfigFromBlock(nested, projectDir, repoRoot, addLocalDir);
        }
    }

    private static void CollectReadTerragruntConfigFromExpr(
        HclExpr expr,
        string projectDir,
        string repoRoot,
        Action<string?> addLocalDir)
    {
        foreach (var call in expr.EnumerateCalls())
        {
            if (string.Equals(call.FunctionName, FnReadTerragruntConfig, StringComparison.Ordinal) &&
                call.Arguments.Count > 0)
            {
                var resolved = TerraformPathResolver.EvaluatePathExpr(
                    call.Arguments[0],
                    projectDir,
                    repoRoot,
                    File.Exists,
                    Directory.Exists);
                addLocalDir(ToParentDirIfFile(resolved));
            }
        }
    }

    private static string? ToParentDirIfFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        if (File.Exists(path))
        {
            return TerraformPathResolver.NormalizeDirPath(Path.GetDirectoryName(path));
        }
        return Directory.Exists(path) ? TerraformPathResolver.NormalizeDirPath(path) : null;
    }

    private static bool IsHclSourceFile(string filePath)
    {
        var name = Path.GetFileName(filePath);
        if (name.Equals(LockFileSuffix, StringComparison.OrdinalIgnoreCase) ||
            name.Equals(TflintFileSuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return name.EndsWith(ExtTf, StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(ExtTofu, StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(ExtHcl, StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeReadText(string filePath)
    {
        try
        {
            return PhysicalFileStore.ReadAllText(filePath);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FindRepoRoot(string startDir)
    {
        var cur = startDir;
        while (!string.IsNullOrEmpty(cur))
        {
            if (Directory.Exists(Path.Combine(cur, GitDirectoryName)))
            {
                return TerraformPathResolver.NormalizeDirPath(cur);
            }
            var parent = Path.GetDirectoryName(cur);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, cur, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            cur = parent;
        }
        return startDir;
    }
}
