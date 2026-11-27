using CodeExplorer.Core.Database;

namespace CodeExplorer.Parsers.TypeScript;

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
                TypeScriptProjectParser.IsAbsorbedWorkersSiteDirectory(directoryPath) ||
                TypeScriptProjectParser.IsVendoredFrontendAssetDirectory(directoryPath))
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
        var activeIndex = context.DirectoryIndex ?? WorkspaceDirectoryIndex.Current;
        if (activeIndex != null && activeIndex.TryGetCachedProjectParse(this, context.DirectoryPath, out var cached))
        {
            result = cached.Result;
            return cached.Matched;
        }

        if (!_tsParser.TryParse(context, out var tsResult) || tsResult == null)
        {
            if (!IsProjectDirectory(context.DirectoryPath, context.FilesInDirectory))
            {
                activeIndex?.StoreProjectParse(this, context.DirectoryPath, false, null);
                result = null;
                return false;
            }

            var name = GetProjectName(context.DirectoryPath, context.FilesInDirectory);
            var props = ExtractManifestProperties(context.DirectoryPath, context.FilesInDirectory);
            var produced = _tsParser.GetProducedPackageAsync(context.DirectoryPath).GetAwaiter().GetResult();
            var deps = _tsParser.ParseDependenciesAsync(context.DirectoryPath).GetAwaiter().GetResult();
            var meta = _tsParser.InspectDirectoryManifestMetadata(context);
            result = new ProjectParseResult(
                new ParsedProjectDescriptor(
                    name,
                    context.DirectoryPath,
                    ProjectType,
                    props,
                    ProducedPackage: produced,
                    Dependencies: deps,
                    ManifestMetadata: meta),
                ParserClaimMode.AbsorbSecondaryInDirectory);
            activeIndex?.StoreProjectParse(this, context.DirectoryPath, true, result);
            return true;
        }

        result = tsResult with
        {
            Project = tsResult.Project with { ProjectType = ProjectLanguage.JavaScript }
        };
        activeIndex?.StoreProjectParse(this, context.DirectoryPath, true, result);
        return true;
    }

    public ParsedProjectManifestMetadata? InspectDirectoryManifestMetadata(ProjectParseContext context) =>
        _tsParser.InspectDirectoryManifestMetadata(context);

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory) =>
        _tsParser.ExtractManifestProperties(directoryPath, filesInDirectory);

    public IReadOnlyCollection<string> ManifestFileNames => _tsParser.ManifestFileNames;
    public IReadOnlyCollection<string> ManifestExtensions => _tsParser.ManifestExtensions;
    public bool IsDistOutputDirectory(string dirName) => _tsParser.IsDistOutputDirectory(dirName);
    public void InspectDirectoryFiles(string directoryPath, FileInfo[] files, Core.Common.Nodes.WorkspaceNode workspace) =>
        _tsParser.InspectDirectoryFiles(directoryPath, files, workspace);
    public void InspectDirectoryFilePaths(string directoryPath, string[] filePaths, Core.Common.Nodes.WorkspaceNode workspace) =>
        _tsParser.InspectDirectoryFilePaths(directoryPath, filePaths, workspace);
    public bool IsConfigurationFile(string fileName) => _tsParser.IsConfigurationFile(fileName);

    public string GetProjectName(string directoryPath, string[] filesInDirectory) =>
        _tsParser.GetProjectName(directoryPath, filesInDirectory);

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory) =>
        _tsParser.GetProducedPackageAsync(projectDirectory);

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory) =>
        _tsParser.ParseDependenciesAsync(projectDirectory);
}
