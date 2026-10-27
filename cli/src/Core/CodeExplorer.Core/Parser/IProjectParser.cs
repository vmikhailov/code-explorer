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
/// Context passed to <see cref="IProjectParser.TryParse"/> for directory-level project boundary evaluation.
/// </summary>
public sealed record ProjectParseContext(
    string DirectoryPath,
    string[] FilesInDirectory,
    string AbsoluteWorkspacePath = "");

/// <summary>
/// Describes a single parsed project boundary and its optional nested subprojects, absorbed subpaths, and excluded asset subpaths.
/// </summary>
public sealed record ParsedProjectDescriptor(
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
    string? RelativeIdPathOverride = null);

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
    /// Default rule for skipping synthetic stubs, mocks, and scratch files.
    /// </summary>
    public static bool DefaultShouldSkipFileName(string fileName)
    {
        var fn = fileName.ToLowerInvariant();
        if (fn.Contains("mock")) return true;
        if (fn.StartWithIC("scratch") || fn.Contains(".scratch.") || fn.Contains("_scratch.") ||
            fn.StartWithIC("temp_") || fn.StartWithIC("tmp_") ||
            fn.StartWithIC("debug_") || fn.Contains("playground") || fn.Contains("scratchpad"))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checks whether a file with the given name should be skipped by this dialect (e.g. ambient declaration .d.ts, minified bundles).
    /// </summary>
    bool ShouldSkipFileName(string fileName) => DefaultShouldSkipFileName(fileName);

    /// <summary>
    /// Checks whether a file should be skipped by this dialect (e.g. minified content, generated bundles).
    /// </summary>
    bool ShouldSkipFile(FileInfo fileInfo) => false;

    /// <summary>
    /// Callback during directory scanning allowing dialect-specific configuration registration (e.g. LibMan).
    /// </summary>
    void OnDirectoryScan(string directoryPath, string relativeDir, GitIgnoreMatcher gitignore, Action<string>? log = null) { }

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
                SubProjects: subProjects),
            ClaimMode: claimMode);
        return true;
    }

    /// <summary>
    /// Declarative package descriptors (frameworks, test runners, drivers) registered for this language dialect.
    /// Derived dynamically from all registered SemanticExtensions by default.
    /// </summary>
    IReadOnlyList<PackageDescriptor> Packages => [.. SemanticExtensions.SelectMany(e => e.GetPackageDescriptors())];

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
    /// Semantic AST extensions (e.g. Tree-sitter visitors) registered for this language dialect.
    /// </summary>
    IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    /// <summary>
    /// Checks if the specified file name is a configuration file for this dialect (e.g. appsettings.json, application.properties).
    /// </summary>
    bool IsConfigurationFile(string fileName) => false;

    /// <summary>
    /// Configuration descriptors for framework-specific or dialect-specific configuration keys.
    /// </summary>
    IReadOnlyList<ILibraryConfigurationDescriptor> ConfigurationDescriptors => [];

    /// <summary>
    /// Categorizes the project into a universal semantic classification (Kind and SubKind)
    /// using Inversion of Control across this dialect's packages and manifest properties.
    /// Returns null if this dialect does not have conclusive evidence.
    /// </summary>
    ProjectClassification? ClassifyProject(ProjectContext context) => DefaultClassifyProject(this, context);

    public static ProjectClassification? DefaultClassifyProject(IProjectParser parser, ProjectContext context)
    {
        // 1. Dialect Manifest Evidence

        var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
        if (manifestType == "test" || context.ManifestProperties.GetValueOrDefault("is_test_project") == "true") return ProjectClassification.Test;
        if (manifestType == "library") return ProjectClassification.Library;
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
        var testPackages = parser.Packages.Where(p => p.Role == LibraryRole.TestFramework);
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
        var appPackages = parser.Packages.Where(p => p.Role is LibraryRole.WebFramework
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
}
