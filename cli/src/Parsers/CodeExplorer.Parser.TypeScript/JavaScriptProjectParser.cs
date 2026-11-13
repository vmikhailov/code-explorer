using CodeExplorer.Core.Database;

namespace CodeExplorer.Parser.TypeScript;

[ProjectParser(Tier = ParserTier.Secondary)]
public class JavaScriptProjectParser : IProjectParser
{
    private readonly TypeScriptProjectParser _tsParser = new();

    public string ProjectType => "javascript";

    public IReadOnlyCollection<string> ExcludedFolders => ["node_modules", "dist", "build", ".next", "out"];

    public IReadOnlyList<PackageDescriptor> Packages => TypeScriptFileParser.DefaultPackages;

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        if (TypeScriptProjectParser.HasWranglerManifest(filesInDirectory))
        {
            return true;
        }

        var packageJson = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase));
        if (packageJson != null)
        {
            if (TypeScriptProjectParser.IsSubpathEntryPointPackageJson(packageJson, directoryPath) ||
                TypeScriptProjectParser.IsAbsorbedWorkersSiteDirectory(directoryPath))
            {
                return false;
            }
            return true;
        }

        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "jsconfig.json")
            {
                return true;
            }
        }
        return false;
    }

    public bool TryParse(ProjectParseContext context, out ProjectParseResult? result)
    {
        if (!_tsParser.TryParse(context, out var tsResult) || tsResult == null)
        {
            if (!IsProjectDirectory(context.DirectoryPath, context.FilesInDirectory))
            {
                result = null;
                return false;
            }

            var name = GetProjectName(context.DirectoryPath, context.FilesInDirectory);
            var props = ExtractManifestProperties(context.DirectoryPath, context.FilesInDirectory);
            result = new ProjectParseResult(
                new ParsedProjectDescriptor(name, context.DirectoryPath, ProjectType, props),
                ParserClaimMode.AbsorbSecondaryInDirectory);
            return true;
        }

        result = tsResult with
        {
            Project = tsResult.Project with { ProjectType = CodeExplorer.Cypher.Common.ProjectLanguage.JavaScript }
        };
        return true;
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory) =>
        _tsParser.ExtractManifestProperties(directoryPath, filesInDirectory);

    public IReadOnlyCollection<string> ManifestFileNames => _tsParser.ManifestFileNames;
    public IReadOnlyCollection<string> ManifestExtensions => _tsParser.ManifestExtensions;
    public bool IsDistOutputDirectory(string dirName) => _tsParser.IsDistOutputDirectory(dirName);
    public void InspectDirectoryFiles(string directoryPath, FileInfo[] files, CodeExplorer.Core.Common.Nodes.WorkspaceNode workspace) =>
        _tsParser.InspectDirectoryFiles(directoryPath, files, workspace);
    public bool IsConfigurationFile(string fileName) => _tsParser.IsConfigurationFile(fileName);

    public string GetProjectName(string directoryPath, string[] filesInDirectory) =>
        _tsParser.GetProjectName(directoryPath, filesInDirectory);

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var packageJsonPath = Path.Combine(projectDirectory, "package.json");
        if (!File.Exists(packageJsonPath)) return null;

        try
        {
            var content = await PhysicalFileStore.ReadAllTextAsync(packageJsonPath);
            var root = CypherValue.TryParseJsonText(content);

            if (root.TryGetProperty("name", out var nameProp) &&
                nameProp.Kind == CypherValueKind.String)
            {
                var name = nameProp.AsNullableString();
                if (string.IsNullOrEmpty(name)) return null;

                const string defaultVersion = "1.0.0";
                var version = defaultVersion;

                if (root.TryGetProperty("version", out var versionProp) &&
                    versionProp.Kind == CypherValueKind.String)
                {
                    version = versionProp.AsString(defaultVersion);
                }

                return new ProducedPackageInfo(name, version, "npm");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Ignore
        }

        return null;
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var packageJsonPath = Path.Combine(projectDirectory, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        try
        {
            var content = await PhysicalFileStore.ReadAllTextAsync(packageJsonPath);
            var root = CypherValue.TryParseJsonText(content);

            var depProperties = new[] { "dependencies", "devDependencies" };

            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) &&
                    depsObj.Kind == CypherValueKind.Object)
                {
                    const string defaultUnknownVersion = "unknown";
                    foreach (var prop in depsObj.AsObject())
                    {
                        var packageName = prop.Key;
                        var packageVersion = prop.Value.AsString(defaultUnknownVersion);

                        // Check if it is a local file/path reference (e.g. file:../lib or workspace:../lib)
                        if (packageVersion.StartsWith("file:", StringComparison.Ordinal) ||
                            (packageVersion.StartsWith("workspace:", StringComparison.Ordinal) &&
                             (packageVersion.Contains('/') || packageVersion.Contains('\\'))))
                        {
                            var relativePath = packageVersion[(packageVersion.IndexOf(':') + 1)..];

                            if (!string.IsNullOrEmpty(relativePath) && (relativePath.StartsWith('.') ||
                                                                        relativePath.StartsWith('/') ||
                                                                        relativePath.StartsWith('\\')))
                            {
                                try
                                {
                                    var referencedDir =
                                        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(packageJsonPath) ?? string.Empty,
                                            relativePath)).Replace('\\', '/');

                                    if (Directory.Exists(referencedDir) || File.Exists(referencedDir))
                                    {
                                        localProjectPaths.Add(referencedDir);
                                        continue;
                                    }
                                }
                                catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
                                {
                                    // Fallback to external package
                                }
                            }
                        }

                        // Treat as npm package reference
                        externalPackages.Add(new ProducedPackageInfo(packageName, packageVersion, "npm", propName == "devDependencies" ? "dev" : "runtime"));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Ignore
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }
}
