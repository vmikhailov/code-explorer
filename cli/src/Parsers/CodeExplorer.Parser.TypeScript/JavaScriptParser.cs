using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Parser.TypeScript;

[ProjectParser(Tier = ParserTier.Secondary)]
public class JavaScriptParser : IProjectParser, IFileParser
{
    public string LanguageName => "javascript";

    public string ProjectType => "javascript";

    public IReadOnlyCollection<string> ExcludedFolders => ["node_modules", "dist", "build", ".next", "out"];

    public IReadOnlyList<PackageDescriptor> Packages => _tsParser.Packages;
    public IReadOnlyList<ISemanticExtension> SemanticExtensions => _tsParser.SemanticExtensions;

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        TypeScriptSyntaxConverter.Instance.Parse(sourceText, LanguageName, filePath, relativePath);

    public LanguageSyntaxProfile SyntaxProfile => TypeScriptSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".jsx", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".mjs", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".cjs", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(CodeExplorer.Core.Parser.Syntax.SyntaxNode rootNode, Action<string, CodeExplorer.Core.Parser.Syntax.SyntaxNode?, string?> registerDeclaration)
    {
        TypeScriptDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        if (TypeScriptParser.HasWranglerManifest(filesInDirectory))
        {
            return true;
        }

        var packageJson = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("package.json", StringComparison.OrdinalIgnoreCase));
        if (packageJson != null)
        {
            if (TypeScriptParser.IsSubpathEntryPointPackageJson(packageJson, directoryPath) ||
                TypeScriptParser.IsAbsorbedWorkersSiteDirectory(directoryPath))
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
            Project = tsResult.Project with { ProjectType = ProjectType }
        };
        return true;
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory) =>
        _tsParser.ExtractManifestProperties(directoryPath, filesInDirectory);

    public IReadOnlyCollection<string> ManifestFileNames => _tsParser.ManifestFileNames;
    public IReadOnlyCollection<string> ManifestExtensions => _tsParser.ManifestExtensions;
    public bool ShouldSkipFileName(string fileName) => _tsParser.ShouldSkipFileName(fileName);

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        return _tsParser.GetProjectName(directoryPath, filesInDirectory);
    }



    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var packageJsonPath = Path.Combine(projectDirectory, "package.json");
        if (!File.Exists(packageJsonPath)) return null;

        try
        {
            var content = await File.ReadAllTextAsync(packageJsonPath);

            using var doc = System.Text.Json.JsonDocument.Parse(content);

            var root = doc.RootElement;

            if (root.TryGetProperty("name", out var nameProp) &&
                nameProp.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var name = nameProp.GetString();
                if (string.IsNullOrEmpty(name)) return null;

                var version = "1.0.0";

                if (root.TryGetProperty("version", out var versionProp) &&
                    versionProp.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    version = versionProp.GetString() ?? "1.0.0";
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
            var content = await File.ReadAllTextAsync(packageJsonPath);

            using var doc = System.Text.Json.JsonDocument.Parse(content);

            var root = doc.RootElement;

            var depProperties = new[] { "dependencies", "devDependencies" };

            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) &&
                    depsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in depsObj.EnumerateObject())
                    {
                        var packageName = prop.Name;
                        var packageVersion = prop.Value.GetString() ?? "unknown";

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

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    private readonly TypeScriptParser _tsParser = new();

    public BaseParserVisitor CreateVisitor(
        CodeExplorer.Core.Parser.Syntax.SyntaxNode rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return _tsParser.CreateVisitor(
            rootNode,
            activeExtensions,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
        );
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return _tsParser.ResolveImportType(importPath, filePath, absoluteWorkspacePath);
    }
}
