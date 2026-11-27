namespace CodeExplorer.Core.Parser;

/// <summary>
/// Controls how a project parser's match in a directory affects lower-precedence or remaining parsers in that directory.
/// </summary>
public enum ParserClaimMode
{
    /// <summary>
    /// Absorbs Secondary-tier parsers in the same directory (merging their dependencies into this project),
    /// while allowing other independent Primary-tier parsers in the same directory to emit their own projects.
    /// </summary>
    AbsorbSecondaryInDirectory = 0,

    /// <summary>
    /// Stops iteration over all remaining project parsers in the current directory.
    /// </summary>
    StopAllOtherParsersInDirectory = 1,

    /// <summary>
    /// Does not block other project parsers in the current directory.
    /// </summary>
    None = 2
}

/// <summary>
/// Lifecycle or build hook declared inside a project manifest (e.g., npm preinstall/postinstall/build).
/// </summary>
public sealed record ManifestHookEntry(
    string Name,
    string Command,
    string EntryType,
    string HookKind,
    string ManifestFileName = "package.json");

/// <summary>
/// Structured, immutable manifest metadata extracted once by the owning <see cref="IProjectParser"/>
/// so downstream layers and classifiers never re-read or re-parse project manifest files.
/// </summary>
public sealed record ParsedProjectManifestMetadata(
    string? PackageName = null,
    string? PackageVersion = null,
    string? Publisher = null,
    string? MainFile = null,
    string? ModuleFile = null,
    string? Description = null,
    bool HasBin = false,
    bool HasContributes = false,
    bool HasVsCodeEngine = false,
    bool HasNgPackage = false,
    bool IsWebSdk = false,
    bool IsWorkerSdk = false,
    bool IsExecutable = false,
    string? KeywordsJson = null,
    string? ExtensionDependenciesJson = null,
    string? ActivationEvents = null,
    string? NuspecId = null,
    string? NuspecVersion = null,
    IReadOnlyList<ProducedPackageInfo>? NuspecDependencies = null,
    string? GoModuleName = null,
    IReadOnlyDictionary<string, string>? TsConfigPaths = null,
    IReadOnlyList<ManifestHookEntry>? LifecycleHooks = null,
    IReadOnlyList<string>? ExportTargets = null,
    string? DatabaseDriverEngine = null,
    string? DatabaseDriverFamily = null);

/// <summary>
/// Context passed to <see cref="IProjectParser.TryParse"/> for directory-level project boundary evaluation.
/// </summary>
public sealed record ProjectParseContext(
    string DirectoryPath,
    string[] FilesInDirectory,
    string AbsoluteWorkspacePath = "",
    WorkspaceDirectoryIndex? DirectoryIndex = null);

/// <summary>
/// Describes a single parsed project boundary and all metadata, packages, and dependencies extracted once from its manifests.
/// </summary>
public sealed record ParsedProjectDescriptor(
    string Name,
    string DirectoryPath,
    ProjectLanguage ProjectType,
    IReadOnlyDictionary<string, string> ManifestProperties,
    IReadOnlyList<string>? SourceRoots = null,
    IReadOnlyList<string>? AbsorbedSubpaths = null,
    IReadOnlyList<string>? ExcludedSubpaths = null,
    IReadOnlyList<string>? EntryPointPaths = null,
    IReadOnlyList<ParsedProjectDescriptor>? SubProjects = null,
    IReadOnlyCollection<string>? LocalDependencyPaths = null,
    string? RelativeIdPathOverride = null,
    ProducedPackageInfo? ProducedPackage = null,
    ProjectDependencyInfo? Dependencies = null,
    ParsedProjectManifestMetadata? ManifestMetadata = null,
    IReadOnlyList<AdditionalProjectDescriptor>? AdditionalTargets = null)
{
    public ParsedProjectDescriptor(
        string Name,
        string DirectoryPath,
        string ProjectType,
        IReadOnlyDictionary<string, string> ManifestProperties,
        IReadOnlyList<string>? SourceRoots = null,
        IReadOnlyList<string>? AbsorbedSubpaths = null,
        IReadOnlyList<string>? ExcludedSubpaths = null,
        IReadOnlyList<string>? EntryPointPaths = null,
        IReadOnlyList<ParsedProjectDescriptor>? SubProjects = null,
        IReadOnlyCollection<string>? LocalDependencyPaths = null,
        string? RelativeIdPathOverride = null,
        ProducedPackageInfo? ProducedPackage = null,
        ProjectDependencyInfo? Dependencies = null,
        ParsedProjectManifestMetadata? ManifestMetadata = null,
        IReadOnlyList<AdditionalProjectDescriptor>? AdditionalTargets = null)
        : this(
            Name,
            DirectoryPath,
            TaxonomyEnumExtendedExtensions.ParseProjectLanguage(ProjectType),
            ManifestProperties,
            SourceRoots,
            AbsorbedSubpaths,
            ExcludedSubpaths,
            EntryPointPaths,
            SubProjects,
            LocalDependencyPaths,
            RelativeIdPathOverride,
            ProducedPackage,
            Dependencies,
            ManifestMetadata,
            AdditionalTargets)
    {
    }
}

/// <summary>
/// Result of <see cref="IProjectParser.TryParse"/> returning the primary project in the directory,
/// claim semantics for other parsers in the same directory, and any independent sibling projects in the same directory.
/// </summary>
public sealed record ProjectParseResult(
    ParsedProjectDescriptor Project,
    ParserClaimMode ClaimMode = ParserClaimMode.AbsorbSecondaryInDirectory,
    IReadOnlyList<ParsedProjectDescriptor>? AdditionalSiblingProjects = null);

public enum ProjectParserRole
{
    /// <summary>
    /// Standard dialect/framework project parser (e.g. C#, TypeScript, Go, Python, Rust, Java).
    /// Evaluated in the primary pass.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Fallback project parser (e.g. loose scripts) evaluated only after standard
    /// dialect parsers have completed their matching.
    /// </summary>
    Fallback = 1
}

/// <param name="Name">Target name (e.g. the Cargo <c>[[bin]]</c> name).</param>
/// <param name="EntryPointPath">Absolute path of the target's entry point source file.</param>
/// <param name="Properties">Manifest properties attached to the synthesized project node.</param>
/// <param name="LocalDependencyPaths">
/// When set, restricts the target's local project dependencies to these absolute paths instead of inheriting
/// every local dependency of the shared manifest (e.g. one Cargo binary out of many in a crate).
/// </param>
public sealed record AdditionalProjectDescriptor(
    string Name,
    string EntryPointPath,
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyCollection<string>? LocalDependencyPaths = null);

public sealed record ProjectEnrichmentContext(
    string WorkspaceId,
    string AbsoluteWorkspacePath,
    Common.Nodes.Layer2_Boundaries.ProjectNode Project,
    IReadOnlyList<SyntaxTree> ProjectTrees,
    IReadOnlyList<Common.Nodes.Layer2_Boundaries.ProjectNode> AllProjects,
    IReadOnlyDictionary<string, List<SyntaxTree>> TreesByProjectId,
    IReadOnlyList<Common.Nodes.Layer1_Physical.FileNode> AllFiles);

public sealed record ProjectEnrichmentResult(
    IReadOnlyList<Common.Nodes.IOntologyNode> SemanticNodes,
    IReadOnlyList<Database.Relationship> SemanticRelationships,
    IReadOnlyList<Database.Relationship> GlobalRelationships)
{
    public static readonly ProjectEnrichmentResult Empty = new([], [], []);
}

public interface IProjectParser
{
    /// <summary>
    /// The parser role (Standard vs Fallback). Fallback parsers are evaluated only after
    /// standard dialect parsers have completed their matching.
    /// Derived from <see cref="ProjectParserAttribute.Tier"/>.
    /// </summary>
    ProjectParserRole ParserRole => ProjectParserPrecedence.GetTier(this) == ParserTier.Fallback
        ? ProjectParserRole.Fallback
        : ProjectParserRole.Standard;

    /// <summary>
    /// Whether this parser acts as a fallback parser.
    /// </summary>
    bool IsFallback => ParserRole == ProjectParserRole.Fallback;

    /// <summary>
    /// The project type/language identifier (e.g., "csharp", "go", "python", "typescript").
    /// </summary>
    string ProjectType { get; }

    /// <summary>
    /// The directory names that should be excluded when this project type is active.
    /// </summary>
    IReadOnlyCollection<string> ExcludedFolders { get; }

    /// <summary>
    /// Specific manifest file names (e.g., "package.json", "go.mod", "Cargo.toml", "pom.xml", "Directory.Build.props")
    /// recognized by this dialect parser.
    /// </summary>
    IReadOnlyCollection<string> ManifestFileNames => [];

    /// <summary>
    /// Specific manifest file extensions (e.g., ".csproj", ".fsproj", ".vbproj") recognized by this dialect parser.
    /// </summary>
    IReadOnlyCollection<string> ManifestExtensions => [];

    /// <summary>
    /// Checks whether the given file name or path is a project manifest file for this dialect.
    /// </summary>
    bool IsManifestFile(string fileName)
    {
        if (ManifestFileNames.Any(m => m.Equals(fileName, StringComparison.OrdinalIgnoreCase))) return true;
        var ext = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(ext) && ManifestExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks whether a directory should be excluded or explicitly retained by this dialect.
    /// Returns true to exclude, false to explicitly retain/include (overriding name-based exclusion),
    /// or null for no opinion.
    /// </summary>
    bool? CheckDirectoryExclusion(string dirName, string? fullPath = null) => null;

    /// <summary>
    /// Checks whether a directory should be excluded by this dialect.
    /// </summary>
    bool IsExcludedDirectory(string dirName, string? fullPath = null) => CheckDirectoryExclusion(dirName, fullPath) ?? false;

    /// <summary>
    /// Callback during directory scanning allowing dialect-specific configuration registration (e.g. LibMan).
    /// </summary>
    void OnDirectoryScan(string directoryPath, string relativeDir, GitIgnoreMatcher gitignore, Action<string>? log = null) { }

    /// <summary>
    /// Collects dialect-specific ignore patterns (relative to the directory or workspace) when entering a project directory (e.g. LibMan destinations).
    /// </summary>
    void CollectDirectoryIgnorePatterns(string directoryPath, string relativeDir, string workspaceRoot, List<string> outGlobalPatterns, Action<string>? log = null) { }

    /// <summary>
    /// Inspects files discovered in a directory to record dialect-specific binary/native artifacts on the workspace node.
    /// </summary>
    void InspectDirectoryFiles(string directoryPath, FileInfo[] files, Common.Nodes.WorkspaceNode workspace) { }

    /// <summary>
    /// Allocation-free overload inspecting discovered file paths in a directory without <see cref="FileInfo"/> wrappers.
    /// </summary>
    void InspectDirectoryFilePaths(string directoryPath, string[] filePaths, Common.Nodes.WorkspaceNode workspace) { }

    /// <summary>
    /// Indicates whether a directory name represents a bundled/compiled distribution directory for this dialect
    /// that should be included when <see cref="WorkspaceFileFilter.IncludeDistAndMinifiedArtifacts"/> is enabled.
    /// </summary>
    bool IsDistOutputDirectory(string dirName) => false;

    /// <summary>
    /// Checks if the given directory contains a project for this language, based on files in it.
    /// </summary>
    bool IsProjectDirectory(string directoryPath, string[] filesInDirectory);

    /// <summary>
    /// Gets a friendly project name for the project in the given directory (defaults to folder name).
    /// </summary>
    string GetProjectName(string directoryPath, string[] filesInDirectory) => Path.GetFileName(directoryPath.TrimEnd('/', '\\'));

    /// <summary>
    /// Checks if the project in the given directory produces a package, and returns details if so.
    /// </summary>
    Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory);

    /// <summary>
    /// Parses the project dependencies (local project directory paths and external packages) in the given directory.
    /// </summary>
    Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory);

    /// <summary>
    /// Extracts project-level manifest metadata and properties (e.g., manifest_type, has_cli_bin, framework_type, sdk, output_type, is_packable).
    /// </summary>
    Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory) => [];

    /// <summary>
    /// Returns independently deployable targets declared inside a project manifest.
    /// </summary>
    IReadOnlyList<AdditionalProjectDescriptor> GetAdditionalProjects(string directoryPath, string[] filesInDirectory) => [];

    /// <summary>
    /// Attempts to parse a project boundary in the given directory, returning the primary project descriptor,
    /// any nested subprojects or absorbed subpaths, and the parser claim mode for other parsers in the same directory.
    /// </summary>
    bool TryParse(ProjectParseContext context, out ProjectParseResult? result)
    {
        if (!IsProjectDirectory(context.DirectoryPath, context.FilesInDirectory))
        {
            result = null;
            return false;
        }

        var name = GetProjectName(context.DirectoryPath, context.FilesInDirectory);
        if (string.IsNullOrEmpty(name))
        {
            name = Path.GetFileName(context.DirectoryPath.TrimEnd('/', '\\'));
        }

        var props = ExtractManifestProperties(context.DirectoryPath, context.FilesInDirectory);
        var produced = GetProducedPackageAsync(context.DirectoryPath).GetAwaiter().GetResult();
        var deps = ParseDependenciesAsync(context.DirectoryPath).GetAwaiter().GetResult();
        var meta = InspectDirectoryManifestMetadata(context);
        var additional = GetAdditionalProjects(context.DirectoryPath, context.FilesInDirectory);
        List<ParsedProjectDescriptor>? subProjects = null;
        if (additional.Count > 0)
        {
            subProjects = [];
            foreach (var add in additional)
            {
                var entryDir = Path.GetDirectoryName(add.EntryPointPath) ?? context.DirectoryPath;
                var relIdOverride = !string.IsNullOrEmpty(context.AbsoluteWorkspacePath)
                    ? Path.GetRelativePath(context.AbsoluteWorkspacePath, Path.GetFullPath(add.EntryPointPath)).Replace('\\', '/')
                    : null;
                subProjects.Add(new ParsedProjectDescriptor(
                    Name: add.Name,
                    DirectoryPath: entryDir,
                    ProjectType: ProjectType,
                    ManifestProperties: add.Properties,
                    EntryPointPaths: [add.EntryPointPath],
                    LocalDependencyPaths: add.LocalDependencyPaths,
                    RelativeIdPathOverride: relIdOverride));
            }
        }

        var tier = ProjectParserPrecedence.GetTier(this);
        var claimMode = tier == ParserTier.Primary
            ? ParserClaimMode.AbsorbSecondaryInDirectory
            : ParserClaimMode.None;

        result = new ProjectParseResult(
            Project: new ParsedProjectDescriptor(
                Name: name,
                DirectoryPath: context.DirectoryPath,
                ProjectType: ProjectType,
                ManifestProperties: props,
                SubProjects: subProjects,
                ProducedPackage: produced,
                Dependencies: deps,
                ManifestMetadata: meta,
                AdditionalTargets: additional.Count > 0 ? additional : null),
            ClaimMode: claimMode);
        return true;
    }

    /// <summary>
    /// Extracts directory-level manifest metadata (e.g., virtual Cargo workspace, root package.json, tsconfig.base.json,
    /// Directory.Build.props, .nuspec) once even when a directory is an umbrella root rather than a standalone project.
    /// </summary>
    ParsedProjectManifestMetadata? InspectDirectoryManifestMetadata(ProjectParseContext context) => null;

    /// <summary>
    /// Determines whether a child directory <paramref name="childDirectoryPath"/> located inside an already-matched
    /// <paramref name="enclosingProject"/> of this parser may be evaluated by <paramref name="candidateChildParser"/>
    /// as a nested project boundary during top-down directory traversal.
    /// </summary>
    bool CanContainNestedProject(
        ParsedProjectDescriptor enclosingProject,
        string childDirectoryPath,
        string[] childFiles,
        IProjectParser candidateChildParser) =>
        DefaultCanContainNestedProject(enclosingProject, childDirectoryPath, candidateChildParser);

    public static bool DefaultCanContainNestedProject(
        ParsedProjectDescriptor enclosingProject,
        string childDirectoryPath,
        IProjectParser candidateChildParser)
    {
        var normChild = Path.GetFullPath(childDirectoryPath).Replace('\\', '/').TrimEnd('/');

        if (enclosingProject.AbsorbedSubpaths != null)
        {
            foreach (var sub in enclosingProject.AbsorbedSubpaths)
            {
                var fullAbs = Path.GetFullPath(Path.Combine(enclosingProject.DirectoryPath, sub)).Replace('\\', '/').TrimEnd('/');
                if (normChild.Equals(fullAbs, StringComparison.OrdinalIgnoreCase) ||
                    normChild.StartsWith(fullAbs + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        if (enclosingProject.ExcludedSubpaths != null)
        {
            foreach (var sub in enclosingProject.ExcludedSubpaths)
            {
                var fullExcl = Path.GetFullPath(Path.Combine(enclosingProject.DirectoryPath, sub)).Replace('\\', '/').TrimEnd('/');
                if (normChild.Equals(fullExcl, StringComparison.OrdinalIgnoreCase) ||
                    normChild.StartsWith(fullExcl + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Declarative package descriptors (frameworks, test runners, drivers) registered for this language dialect.
    /// </summary>
    IReadOnlyList<PackageDescriptor> Packages => [];

    /// <summary>
    /// Checks whether any of the provided dependency names matches a registered <see cref="PackageDescriptor"/>
    /// with the specified <paramref name="role"/>.
    /// </summary>
    bool HasPackageWithRole(IEnumerable<string> dependencies, LibraryRole role) =>
        Packages.Any(p => p.Role == role && p.MatchesAnyDependency(dependencies));

    /// <summary>
    /// Checks whether any of the provided dependency names matches a registered <see cref="LibraryRole.TestFramework"/> library.
    /// </summary>
    bool HasTestFrameworkDependency(IEnumerable<string> dependencies) =>
        HasPackageWithRole(dependencies, LibraryRole.TestFramework);

    /// <summary>
    /// Checks if the specified file name is a configuration file for this dialect (e.g. appsettings.json, application.properties).
    /// </summary>
    bool IsConfigurationFile(string fileName) => false;

    /// <summary>
    /// Configuration descriptors for framework-specific or dialect-specific configuration keys.
    /// </summary>
    IReadOnlyList<ILibraryConfigurationDescriptor> ConfigurationDescriptors => [];

    /// <summary>
    /// Performs stateless project-level semantic enrichment in Layer 4 over in-memory syntax trees.
    /// </summary>
    Task<ProjectEnrichmentResult> EnrichProjectAsync(ProjectEnrichmentContext context) =>
        Task.FromResult(ProjectEnrichmentResult.Empty);

    /// <summary>
    /// Categorizes the project into a universal semantic classification (Kind and SubKind)
    /// using Inversion of Control across this dialect's packages and manifest properties.
    /// Returns null if this dialect does not have conclusive evidence.
    /// </summary>
    ProjectClassification? ClassifyProject(ProjectContext context) => DefaultClassifyProject(this, context);

    private const string KeyIsOrchestrator = "is_orchestrator";

    public static ProjectClassification? DefaultClassifyProject(IProjectParser parser, ProjectContext context)
    {
        // 1. Dialect Manifest Evidence

        var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
        if (manifestType == "test" || context.ManifestProperties.GetValueOrDefault("is_test_project") == "true") return ProjectClassification.Test;
        if (manifestType == "library")
        {
            if (WorkspaceDetectorCatalog.CurrentScope is { HasCustomDetectors: true } scope &&
                context.ManifestProperties.GetValueOrDefault(KeyIsOrchestrator) != "true")
            {
                var customMatch = ClassifyFromCustomDetectors(parser.ProjectType, context, scope);
                if (customMatch != null) return customMatch;
            }
            return ProjectClassification.Library;
        }
        if (manifestType == "cli" || context.ManifestProperties.GetValueOrDefault("has_cli_bin") == "true") return ProjectClassification.CliApp;
        if (manifestType == "worker") return ProjectClassification.Worker;
        if (manifestType == "migration") return ProjectClassification.DatabaseMigration;
        if (manifestType is "function" or "serverless") return ProjectClassification.FunctionApp;
        if (manifestType == "mobile") return ProjectClassification.MobileApp;
        if (manifestType == "desktop") return ProjectClassification.DesktopApp;

        var frameworkType = context.ManifestProperties.GetValueOrDefault("framework_type")?.ToLowerInvariant();
        if (frameworkType == "frontend" || frameworkType == "web-client") return ProjectClassification.WebApp;
        if (frameworkType == "mobile") return ProjectClassification.MobileApp;
        if (frameworkType == "desktop") return ProjectClassification.DesktopApp;
        if (frameworkType == "cli") return ProjectClassification.CliApp;
        if (frameworkType == "worker") return ProjectClassification.Worker;
        if (frameworkType == "web" || frameworkType == "api") return ProjectClassification.Service;


        // 2. Inversion of Control: Query dialect's test framework packages FIRST!
        var effectivePackages = WorkspaceDetectorCatalog.CurrentScope?.GetEffectivePackages(parser) ?? parser.Packages;
        var testPackages = effectivePackages.Where(p => p.Role == LibraryRole.TestFramework);
        var normProjectName = (context.ProjectName ?? "").Trim().ToLowerInvariant();
        var isTestByName = normProjectName.EndsWith(".tests") ||
                           normProjectName.EndsWith(".unittests") ||
                           normProjectName.EndsWith(".functionaltests") ||
                           normProjectName.EndsWith(".integrationtests") ||
                           normProjectName.EndsWith(".test");

        if (isTestByName)
        {
            return ProjectClassification.Test;
        }

        foreach (var tp in testPackages)
        {
            if (tp.Matches(context))
            {
                return ProjectClassification.Test;
            }
        }

        // 3. Inversion of Control: Query dialect's application framework packages
        var appPackages = effectivePackages.Where(p => p.Role is LibraryRole.WebFramework
            or LibraryRole.WebService
            or LibraryRole.FrontendFramework
            or LibraryRole.WorkerService
            or LibraryRole.FunctionFramework
            or LibraryRole.CliFramework
            or LibraryRole.MobileFramework
            or LibraryRole.DesktopFramework
            or LibraryRole.DatabaseMigration);

        foreach (var pkg in appPackages)
        {
            if (pkg.Matches(context))
            {
                return pkg.Role switch
                {
                    LibraryRole.FrontendFramework => ProjectClassification.WebApp,
                    LibraryRole.WebFramework or LibraryRole.WebService => ProjectClassification.Service,
                    LibraryRole.WorkerService => ProjectClassification.Worker,
                    LibraryRole.FunctionFramework => ProjectClassification.FunctionApp,
                    LibraryRole.CliFramework => ProjectClassification.CliApp,
                    LibraryRole.MobileFramework => ProjectClassification.MobileApp,
                    LibraryRole.DesktopFramework => ProjectClassification.DesktopApp,
                    LibraryRole.DatabaseMigration => ProjectClassification.DatabaseMigration,
                    _ => null
                };
            }
        }

        return null;
    }

    internal static ProjectClassification? ClassifyFromCustomDetectors(
        string projectType,
        ProjectContext context,
        WorkspaceDetectorCatalog scope)
    {
        var canonicalLang = WorkspaceDetectorLanguageMap.NormalizeProjectLanguage(projectType);
        foreach (var custom in scope.CustomDetectors)
        {
            if (!custom.AppliesToLanguage(canonicalLang)) continue;
            foreach (var pkg in custom.GetPackageDescriptors())
            {
                if (!pkg.Matches(context)) continue;
                var mapped = pkg.Role switch
                {
                    LibraryRole.TestFramework => ProjectClassification.Test,
                    LibraryRole.FrontendFramework => ProjectClassification.WebApp,
                    LibraryRole.WebFramework or LibraryRole.WebService => ProjectClassification.Service,
                    LibraryRole.WorkerService => ProjectClassification.Worker,
                    LibraryRole.FunctionFramework => ProjectClassification.FunctionApp,
                    LibraryRole.CliFramework => ProjectClassification.CliApp,
                    LibraryRole.MobileFramework => ProjectClassification.MobileApp,
                    LibraryRole.DesktopFramework => ProjectClassification.DesktopApp,
                    LibraryRole.DatabaseMigration => ProjectClassification.DatabaseMigration,
                    _ => null
                };
                if (mapped != null) return mapped;
            }
        }
        return null;
    }
}
