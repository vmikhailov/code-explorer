using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Parser;

public interface IProjectParser
{
    /// <summary>
    /// The project type/language identifier (e.g., "csharp", "go", "python", "typescript").
    /// </summary>
    string ProjectType { get; }

    /// <summary>
    /// The directory names that should be excluded when this project type is active.
    /// </summary>
    IReadOnlyCollection<string> ExcludedFolders { get; }

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
    /// Gets the syntax enricher for this project type.
    /// </summary>
    ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(SemanticExtensions, syntaxTree, Packages);

    /// <summary>
    /// Extracts project-level manifest metadata and properties (e.g., manifest_type, has_cli_bin, framework_type, sdk, output_type, is_packable).
    /// </summary>
    Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory) => [];

    /// <summary>
    /// Declarative package descriptors (frameworks, test runners, drivers) registered for this language dialect.
    /// </summary>
    IReadOnlyList<PackageDescriptor> Packages => [];

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
        if (context.ManifestProperties != null)
        {
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
        }

        // 2. Inversion of Control: Query dialect's application framework packages
        var appPackages = parser.Packages.Where(p => p.Role is LibraryRole.WebFramework
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
                    LibraryRole.WebFramework => ProjectClassification.Service,
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

        // 3. Inversion of Control: Query dialect's test framework packages
        var testPackages = parser.Packages.Where(p => p.Role == LibraryRole.TestFramework);
        foreach (var tp in testPackages)
        {
            if (tp.Matches(context))
            {
                return ProjectClassification.Test;
            }
        }

        return null;
    }
}
