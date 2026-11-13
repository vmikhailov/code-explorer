using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Common;
using CodeExplorer.Parser.Terraform.Hcl;

// Auto-discovered by WorkspaceIndexer.AutoRegisterParsers via reflection.
[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Layer 2 project parser for Terraform / OpenTofu / Terragrunt: detects module and unit
/// directories, extracts their dependencies, and runs the project-scoped Layer 4 enrichment over
/// the files <see cref="TerraformFileParser"/> parsed.
/// </summary>
public class TerraformProjectParser : IProjectParser
{
    private const string TerraformProjectType = "terraform";

    public string ProjectType => TerraformProjectType;

    // Terraform/Terragrunt provider cache, local state, and serverless build directories are
    // never descended into.
    public IReadOnlyCollection<string> ExcludedFolders => [".terraform", ".terragrunt-cache", ".serverless"];

    // terragrunt.hcl marks a Terragrunt unit directory as a project boundary. (.terraform.lock.hcl
    // is excluded from parsing by IsLockFile, not treated as a manifest.)
    public IReadOnlyCollection<string> ManifestFileNames => ["terragrunt.hcl"];

    // A directory is a Terraform/Terragrunt project if it has *.tf/*.tofu or a terragrunt.hcl.
    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var name = Path.GetFileName(file).ToLowerInvariant();
            if (name.EndsWith(".tf") || name.EndsWith(".tofu") || name == "terragrunt.hcl")
                return true;
        }
        return false;
    }

    public bool IsConfigurationFile(string fileName) =>
        fileName.EndsWith(".tfvars", StringComparison.OrdinalIgnoreCase);

    // Classifies an IaC directory as a library (so it is not treated as a microservice), with the
    // infrastructure intent carried in project_kind and the is_terragrunt/is_terraform_module flags.
    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>
        {
            ["project_kind"] = "infrastructure",
            ["manifest_type"] = "library"
        };

        bool Has(string fileName) =>
            filesInDirectory.Any(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));

        if (Has(TerragruntFileName))
        {
            props["is_terragrunt"] = "true";
        }
        else if (Has(VariablesFileName) && Has(OutputsFileName))
        {
            props["is_terraform_module"] = "true";
        }

        return props;
    }

    private const string SourceAttribute = "source";
    private const string ConfigPathAttribute = "config_path";
    private const string PathsAttribute = "paths";
    private const string PathAttribute = "path";
    private const string TerragruntFileName = "terragrunt.hcl";
    private const string VariablesFileName = "variables.tf";
    private const string OutputsFileName = "outputs.tf";
    private const string TerraformFilePattern = "*.tf";
    private const string OpenTofuFilePattern = "*.tofu";
    private const string HclFilePattern = "*.hcl";
    private const string ReadTerragruntConfigFunction = "read_terragrunt_config";
    private const string FindInParentFoldersFunction = "find_in_parent_folders";
    private const string DirnameFunction = "dirname";
    private const string GetRepoRootFunction = "get_repo_root";
    private const string GetTerragruntDirFunction = "get_terragrunt_dir";
    private const string GitDirectoryName = ".git";
    private const string UnknownPackageVersion = "unknown";

    // Surfaces local module sources and Terragrunt dependency/include targets as LocalProjectPaths
    // (which Layer2ProjectParser turns into project-level DEPENDS_ON); registry/git module sources
    // become external packages. Reads the parsed AST, so commented-out blocks are ignored.
    //
    // HCL files are both this project's manifest and its source, so Layer 2 parses the files that
    // ParseAsync parses again in Layer 3. Both read the text Layer 1 cached in PhysicalFileStore,
    // so each file is read from disk once.
    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        // I/O failures propagate to Layer 2, which records them as a scan issue on the project.
        var files = Directory.GetFiles(projectDirectory, TerraformFilePattern, SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(projectDirectory, OpenTofuFilePattern, SearchOption.TopDirectoryOnly))
            .Concat(Directory.GetFiles(projectDirectory, HclFilePattern, SearchOption.TopDirectoryOnly))
            .Where(f => !TerraformFiles.IsLockFile(f));

        foreach (var file in files)
        {
            var content = await PhysicalFileStore.ReadAllTextAsync(file);
            var ast = HclParser.Parse(content);

            // read_terragrunt_config(…) loads another HCL config at parse time → a dependency on
            // that file's directory.
            foreach (var call in FindCalls(ast, ReadTerragruntConfigFunction))
            {
                if (call.Arguments.Count > 0)
                {
                    AddLocalPath(ResolveTerragruntPath(call.Arguments[0], projectDirectory), localProjectPaths);
                }
            }

            foreach (var block in ast.Blocks)
            {
                switch (block.Kind)
                {
                    case HclBlockKind.Module:
                    case HclBlockKind.Terraform:
                        // module { source = … } and Terragrunt terraform { source = … }; the latter
                        // often uses a path function (e.g. "${get_terragrunt_dir()}/../mod").
                        AddModuleSource(FindAttribute(block, SourceAttribute), projectDirectory, localProjectPaths, externalPackages);
                        break;
                    case HclBlockKind.Dependency:
                        AddLocalPath(ResolveTerragruntPath(FindAttribute(block, ConfigPathAttribute)?.Value, projectDirectory), localProjectPaths);
                        break;
                    case HclBlockKind.Dependencies:
                        // dependencies { paths = ["../a", "../b"] }: each entry is a unit directory.
                        if (FindAttribute(block, PathsAttribute)?.Value is HclTuple paths)
                        {
                            foreach (var item in paths.Items)
                            {
                                AddLocalPath(ResolveTerragruntPath(item, projectDirectory), localProjectPaths);
                            }
                        }
                        break;
                    case HclBlockKind.Include:
                        AddLocalPath(ResolveTerragruntPath(FindAttribute(block, PathAttribute)?.Value, projectDirectory), localProjectPaths);
                        break;
                }
            }
        }

        return new ProjectDependencyInfo(
            localProjectPaths.Distinct().ToList(),
            externalPackages);
    }

    // A module/terraform source is local when it resolves to a filesystem path — a relative or
    // absolute path, or a Terragrunt path expression. Otherwise (registry or git source such as
    // "terraform-aws-modules/vpc/aws" or "git::…") it is an external package.
    private static void AddModuleSource(HclAttribute? source, string projectDirectory, List<string> localProjectPaths, List<ProducedPackageInfo> externalPackages)
    {
        if (source == null) return;

        var resolved = ResolveTerragruntPath(source.Value, projectDirectory);
        if (!string.IsNullOrEmpty(resolved))
        {
            localProjectPaths.Add(NormalizeToProjectDir(resolved)!);
            return;
        }

        var name = (source.Value as HclTemplate)?.LiteralValue ?? source.ValueText.Trim('"');
        if (name.Length > 0)
        {
            externalPackages.Add(new ProducedPackageInfo(name, UnknownPackageVersion, TerraformProjectType));
        }
    }

    private static void AddLocalPath(string? path, List<string> localProjectPaths)
    {
        var normalized = NormalizeToProjectDir(path);
        if (!string.IsNullOrEmpty(normalized)) localProjectPaths.Add(normalized!);
    }

    // Projects are keyed by directory, but a resolved Terragrunt path can point at a file (e.g.
    // "…/_service_modules/x.hcl" or a config file). Map such a path to its containing directory so
    // Layer 2 matches a real project node instead of a dangling file-named id.
    private static string? NormalizeToProjectDir(string? path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var trimmed = path.TrimEnd('/');
        if (TerraformFiles.IsTerraformExtension(Path.GetExtension(trimmed)))
        {
            var dir = Path.GetDirectoryName(trimmed);
            return string.IsNullOrEmpty(dir) ? trimmed : dir.NormalizeSlashes();
        }
        return path;
    }

    private static HclAttribute? FindAttribute(HclBlock block, string name) =>
        block.Attributes.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.Ordinal));

    // Every call to `function` anywhere in the file, including inside interpolations.
    private static IEnumerable<HclCall> FindCalls(HclFile file, string function)
    {
        var calls = new List<HclCall>();
        foreach (var attribute in file.Attributes) CollectCalls(attribute.Value, function, calls);
        foreach (var block in file.Blocks) CollectCalls(block, function, calls);
        return calls;
    }

    private static void CollectCalls(HclBlock block, string function, List<HclCall> calls)
    {
        foreach (var attribute in block.Attributes) CollectCalls(attribute.Value, function, calls);
        foreach (var nested in block.Blocks) CollectCalls(nested, function, calls);
    }

    private static void CollectCalls(HclExpr? expr, string function, List<HclCall> calls)
    {
        switch (expr)
        {
            case HclCall call:
                if (string.Equals(call.Function, function, StringComparison.Ordinal)) calls.Add(call);
                foreach (var arg in call.Arguments) CollectCalls(arg, function, calls);
                break;
            case HclTemplate template:
                foreach (var part in template.Parts)
                {
                    if (part is HclTemplateInterpolation i) CollectCalls(i.Expression, function, calls);
                    else if (part is HclTemplateDirective d) CollectCalls(d.Expression, function, calls);
                }
                break;
            case HclGetAttr g: CollectCalls(g.Target, function, calls); break;
            case HclIndex ix: CollectCalls(ix.Target, function, calls); CollectCalls(ix.Key, function, calls); break;
            case HclSplat s: CollectCalls(s.Target, function, calls); break;
            case HclTuple t: foreach (var item in t.Items) CollectCalls(item, function, calls); break;
            case HclObject o:
                foreach (var item in o.Items)
                {
                    CollectCalls(item.KeyExpression, function, calls);
                    CollectCalls(item.Value, function, calls);
                }
                break;
            case HclFor f:
                CollectCalls(f.Collection, function, calls);
                CollectCalls(f.KeyExpression, function, calls);
                CollectCalls(f.Value, function, calls);
                CollectCalls(f.Condition, function, calls);
                break;
            case HclUnary u: CollectCalls(u.Operand, function, calls); break;
            case HclBinary b: CollectCalls(b.Left, function, calls); CollectCalls(b.Right, function, calls); break;
            case HclConditional c:
                CollectCalls(c.Condition, function, calls);
                CollectCalls(c.WhenTrue, function, calls);
                CollectCalls(c.WhenFalse, function, calls);
                break;
        }
    }

    /// <summary>
    /// Best-effort resolution of a Terragrunt path expression to a local path (relative or
    /// absolute), or null when it does not resolve to a local target:
    ///   "../x" / "./x" / "/x"                  -> the path as written
    ///   "${expr}suffix"                        -> resolution of expr, then the literal suffix
    ///   find_in_parent_folders()               -> nearest ancestor containing terragrunt.hcl
    ///   find_in_parent_folders("file.hcl")     -> nearest ancestor containing that file
    ///   dirname(expr)                          -> parent directory of expr's resolution
    ///   get_repo_root() / get_terragrunt_dir() -> repo root / this unit's directory
    /// </summary>
    private static string? ResolveTerragruntPath(HclExpr? expr, string projectDirectory)
    {
        switch (expr)
        {
            case HclTemplate template when template.LiteralValue is { } literal:
                return literal.StartsWith('.') || literal.StartsWith('/') ? literal : null;

            case HclTemplate { Parts: [HclTemplateInterpolation head, ..] } template
                when template.Parts.Skip(1).All(p => p is HclTemplateText):
                var inner = ResolveTerragruntPath(head.Expression, projectDirectory);
                return string.IsNullOrEmpty(inner)
                    ? null
                    : inner.TrimEnd('/') + string.Concat(template.Parts.Skip(1).Cast<HclTemplateText>().Select(p => p.Text));

            case HclCall { Function: DirnameFunction, Arguments: [var argument, ..] }:
                var resolved = ResolveTerragruntPath(argument, projectDirectory);
                return string.IsNullOrEmpty(resolved) ? null : Path.GetDirectoryName(resolved.TrimEnd('/'));

            case HclCall { Function: FindInParentFoldersFunction } call:
                var fileName = call.Arguments is [HclTemplate { LiteralValue: { } name }, ..] ? name : TerragruntFileName;
                return FindInParentFolders(projectDirectory, fileName);

            case HclCall { Function: GetRepoRootFunction }:
                return FindRepoRoot(projectDirectory);

            case HclCall { Function: GetTerragruntDirFunction }:
                return projectDirectory;

            default:
                return null;
        }
    }

    private static string? FindInParentFolders(string startDir, string fileName)
    {
        try
        {
            var dir = Directory.GetParent(Path.GetFullPath(startDir));
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, fileName)))
                    return dir.FullName.NormalizeSlashes();
                dir = dir.Parent;
            }
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An inaccessible ancestor ends the search: the path does not resolve locally.
            return null;
        }
    }

    private static string? FindRepoRoot(string startDir)
    {
        try
        {
            var dir = new DirectoryInfo(Path.GetFullPath(startDir));
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, GitDirectoryName)))
                    return dir.FullName.NormalizeSlashes();
                dir = dir.Parent;
            }
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An inaccessible ancestor ends the search: the repo root is unknown.
            return null;
        }
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
        => Task.FromResult<ProducedPackageInfo?>(null);

    // Project-scoped Layer 4 enrichment: resolves the references carried on this project's
    // TerraformSyntaxTrees into DEPENDS_ON edges and maps cloud resources to semantic nodes, scoped
    // to the module directory. Runs once per project after all its files are parsed.
    public Task EnrichProjectAsync(
        CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode project,
        IReadOnlyList<SyntaxTree> projectTrees,
        ParsingContext ctx)
        => TerraformSyntaxEnricher.EnrichAsync(project, projectTrees, ctx);

}
