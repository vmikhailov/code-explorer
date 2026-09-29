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
    ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree);

    /// <summary>
    /// Extracts project-level manifest metadata and properties (e.g., manifest_type, has_cli_bin, framework_type, sdk, output_type, is_packable).
    /// </summary>
    Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory) => [];

    /// <summary>
    /// The library parsers (frameworks, test runners, drivers) registered for this language dialect.
    /// </summary>
    IReadOnlyList<ILibraryParser> LibraryParsers => [];

    /// <summary>
    /// Checks if the specified file name is a configuration file for this dialect (e.g. appsettings.json, application.properties).
    /// </summary>
    bool IsConfigurationFile(string fileName) => false;

    /// <summary>
    /// Configuration descriptors for framework-specific or dialect-specific configuration keys.
    /// </summary>
    IReadOnlyList<ILibraryConfigurationDescriptor> ConfigurationDescriptors => [];

    /// <summary>
    /// Categorizes the project into a universal semantic entity kind (Test, Service, Worker, FrontendApp, CliTool, Library)
    /// using Inversion of Control across this dialect's test libraries, framework libraries, and manifest.
    /// Returns null if this dialect does not have conclusive evidence.
    /// </summary>
    ProjectEntityKind? ClassifyProject(ProjectContext context) => DefaultClassifyProject(this, context);

    public static ProjectEntityKind? DefaultClassifyProject(IProjectParser parser, ProjectContext context)
    {
        // 1. Dialect Manifest Evidence
        if (context.ManifestProperties != null)
        {
            var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
            if (manifestType == "test" || context.ManifestProperties.GetValueOrDefault("is_test_project") == "true") return ProjectEntityKind.Test;
            if (manifestType == "library") return ProjectEntityKind.Library;
            if (manifestType == "cli" || context.ManifestProperties.GetValueOrDefault("has_cli_bin") == "true") return ProjectEntityKind.CliTool;
            if (manifestType == "worker") return ProjectEntityKind.Worker;
            if (manifestType == "migration") return ProjectEntityKind.MigrationTool;
            if (manifestType is "function" or "serverless") return ProjectEntityKind.Function;

            var frameworkType = context.ManifestProperties.GetValueOrDefault("framework_type")?.ToLowerInvariant();
            if (frameworkType == "frontend") return ProjectEntityKind.FrontendApp;
            if (frameworkType == "worker") return ProjectEntityKind.Worker;
            if (frameworkType == "web") return ProjectEntityKind.Service;
        }

        // 2. Inversion of Control: Query dialect's framework library parsers
        var frameworkParsers = parser.LibraryParsers.Where(p => p.LibraryRole != LibraryRole.General && p.LibraryRole != LibraryRole.TestFramework);
        foreach (var fp in frameworkParsers)
        {
            if (fp.MatchesProject(context))
            {
                return fp.LibraryRole switch
                {
                    LibraryRole.WebService => ProjectEntityKind.Service,
                    LibraryRole.WorkerService => ProjectEntityKind.Worker,
                    LibraryRole.FrontendApp => ProjectEntityKind.FrontendApp,
                    LibraryRole.CliTool => ProjectEntityKind.CliTool,
                    LibraryRole.DatabaseMigration => ProjectEntityKind.MigrationTool,
                    _ => ProjectEntityKind.Service
                };
            }
        }

        // 3. Inversion of Control: Query dialect's test library parsers (for dedicated test projects)
        var testParsers = parser.LibraryParsers.Where(p => p.LibraryRole == LibraryRole.TestFramework);
        foreach (var tp in testParsers)
        {
            if (tp.MatchesProject(context))
            {
                return ProjectEntityKind.Test;
            }
        }

        return null;
    }
}
