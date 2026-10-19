using System.Collections.Concurrent;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// Project and file parser for Rust language support in CodeExplorer.
/// Supports single-crate and virtual workspaces, Cargo.toml manifests, and Tree-sitter Rust AST parsing.
/// </summary>
public class RustParser : IProjectParser, IFileParser
{
    public string LanguageName => "rust";

    public string ProjectType => "rust";

    public IReadOnlyCollection<string> ExcludedFolders => ["target"];

    public bool UsesTreeSitter => true;

    public LanguageSyntaxProfile SyntaxProfile => RustSyntaxProfile.Instance;

    public IReadOnlyList<PackageDescriptor> Packages { get; }

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
    [
        new Libraries.AxumLibraryParser(),
        new Libraries.ActixWebLibraryParser(),
        new Libraries.RocketLibraryParser(),
        new Libraries.TonicLibraryParser(),
        new Libraries.ReqwestLibraryParser(),
        new Libraries.HyperLibraryParser(),
        new Libraries.SqlxLibraryParser(),
        new Libraries.SeaOrmLibraryParser(),
        new Libraries.DieselLibraryParser(),
        new Libraries.RdKafkaRustLibraryParser(),
        new Libraries.LapinLibraryParser(),
        new Libraries.AsyncNatsLibraryParser()
    ];

    public RustParser()
    {
        Packages = [.. SemanticExtensions.SelectMany(e => e.GetPackageDescriptors())];
    }

    private readonly ConcurrentDictionary<string, CargoManifest?> _manifestCache = new(StringComparer.OrdinalIgnoreCase);

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".rs", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith(".env") || lower == "cargo.lock";
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        var cargoToml = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase));
        if (cargoToml == null) return false;

        try
        {
            var manifest = GetOrLoadManifest(cargoToml);
            if (manifest != null && manifest.IsVirtualWorkspace)
            {
                return false;
            }
        }
        catch
        {
            // Ignore
        }

        return true;
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var cargoTomlPath = Path.Combine(directoryPath, "Cargo.toml");
        if (File.Exists(cargoTomlPath))
        {
            try
            {
                var manifest = GetOrLoadManifest(cargoTomlPath);
                if (manifest?.Package != null && !string.IsNullOrEmpty(manifest.Package.Name))
                {
                    return manifest.Package.Name;
                }
            }
            catch
            {
                // Fall back to folder name
            }
        }
        return Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        RustDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        var fullPath = Path.Combine(absoluteWorkspacePath, relativePath);
        var crateRootCargo = RustModuleResolver.FindCrateRoot(fullPath);
        CargoManifest? manifest = null;
        if (!string.IsNullOrEmpty(crateRootCargo) && File.Exists(crateRootCargo))
        {
            manifest = GetOrLoadManifest(crateRootCargo);
        }

        return new RustFileVisitor(
            rootNode,
            activeExtensions,
            this,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry,
            manifest
        );
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var cargoTomlPath = Path.Combine(projectDirectory, "Cargo.toml");
        if (!File.Exists(cargoTomlPath)) return null;

        try
        {
            var manifest = GetOrLoadManifest(cargoTomlPath);
            if (manifest?.Package != null && !string.IsNullOrEmpty(manifest.Package.Name))
            {
                var version = manifest.Package.Version ?? "0.1.0";
                return await Task.FromResult(new ProducedPackageInfo(manifest.Package.Name, version, "rust"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore
        }

        return null;
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var cargoTomlPath = Path.Combine(projectDirectory, "Cargo.toml");
        if (!File.Exists(cargoTomlPath))
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        try
        {
            var manifest = GetOrLoadManifest(cargoTomlPath);
            if (manifest != null)
            {
                var allDeps = manifest.Dependencies
                    .Concat(manifest.DevDependencies)
                    .Concat(manifest.BuildDependencies);

                foreach (var dep in allDeps)
                {
                    if (!string.IsNullOrEmpty(dep.ResolvedAbsolutePath) && Directory.Exists(dep.ResolvedAbsolutePath))
                    {
                        if (!localProjectPaths.Contains(dep.ResolvedAbsolutePath))
                        {
                            localProjectPaths.Add(dep.ResolvedAbsolutePath);
                        }
                    }
                    else if (!string.IsNullOrEmpty(dep.Path))
                    {
                        var resolved = Path.GetFullPath(Path.Combine(projectDirectory, dep.Path));
                        if (!localProjectPaths.Contains(resolved))
                        {
                            localProjectPaths.Add(resolved);
                        }
                    }
                    else
                    {
                        var version = dep.Version ?? "1.0.0";
                        if (!externalPackages.Any(p => p.Name.Equals(dep.Name, StringComparison.OrdinalIgnoreCase)))
                        {
                            externalPackages.Add(new ProducedPackageInfo(dep.Name, version, "rust"));
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore
        }

        return await Task.FromResult(new ProjectDependencyInfo(localProjectPaths, externalPackages));
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var normPath = "/" + directoryPath.Replace('\\', '/').Trim('/') + "/";
        var dirName = Path.GetFileName(directoryPath.TrimEnd('/', '\\')).ToLowerInvariant();
        var isTestDir = dirName is "test" or "tests" or "benches" ||
                        normPath.EndsWith("/tests/") || normPath.EndsWith("/benches/") ||
                        normPath.EndsWith("/e2e/") || normPath.EndsWith("/qa/");

        if (isTestDir)
        {
            props["manifest_type"] = "test";
            props["is_test_project"] = "true";
            return props;
        }

        var cargoTomlPath = Path.Combine(directoryPath, "Cargo.toml");
        if (!File.Exists(cargoTomlPath))
        {
            return props;
        }

        CargoManifest? manifest = null;
        try
        {
            manifest = GetOrLoadManifest(cargoTomlPath);
        }
        catch
        {
            return props;
        }

        if (manifest == null) return props;

        // Check virtual workspace with no package
        if (manifest.IsVirtualWorkspace)
        {
            props["manifest_type"] = "workspace";
            props["is_virtual_workspace"] = "true";
            return props;
        }

        var allDeps = manifest.Dependencies
            .Concat(manifest.DevDependencies)
            .Concat(manifest.BuildDependencies)
            .ToList();

        var allDepNames = new HashSet<string>(allDeps.Select(d => d.Name.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

        // Check if pure test harness
        if (manifest.Dependencies.Count == 0 && manifest.DevDependencies.Count > 0 && manifest.LibTarget == null && manifest.BinTargets.Count == 0)
        {
            props["manifest_type"] = "test";
            props["is_test_project"] = "true";
            return props;
        }

        // Framework and dependency detection
        var isWeb = allDepNames.Any(d => d is "axum" or "actix-web" or "actix_web" or "rocket" or "warp" or "poem" or "salvo");
        var isGrpc = allDepNames.Any(d => d is "tonic" or "tarpc" or "grpc");
        var isCliDep = allDepNames.Any(d => d is "clap" or "structopt" or "argh" or "ratatui" or "crossterm" or "dialoguer");
        var isWorkerDep = allDepNames.Any(d => d is "rdkafka" or "lapin" or "async-nats" or "async_nats" or "fang" or "apalis");
        var isMigrationDep = allDepNames.Any(d => d is "refinery" or "diesel_migrations" or "diesel-migrations");

        var migrationsDir = Path.Combine(directoryPath, "migrations");
        var hasMigrations = isMigrationDep ||
                            (Directory.Exists(migrationsDir) && Directory.EnumerateFiles(migrationsDir, "*.sql", SearchOption.AllDirectories).Any());

        var rsFiles = filesInDirectory.Where(f => f.EndsWith(".rs", StringComparison.OrdinalIgnoreCase)).ToList();
        var hasBin = manifest.BinTargets.Count > 0 ||
                     File.Exists(Path.Combine(directoryPath, "src", "main.rs")) ||
                     Directory.Exists(Path.Combine(directoryPath, "src", "bin"));
        var hasLib = manifest.LibTarget != null || File.Exists(Path.Combine(directoryPath, "src", "lib.rs"));

        var isWorkerName = dirName.Contains("worker") || dirName.Contains("consumer") || dirName.Contains("scheduler") ||
                           rsFiles.Any(f => Path.GetFileName(f).ToLowerInvariant().Contains("worker") || Path.GetFileName(f).ToLowerInvariant().Contains("consumer"));

        if (hasMigrations)
        {
            props["manifest_type"] = "migration";
            props["framework_type"] = "migration";
        }
        else if (hasBin && (isWorkerDep || isWorkerName))
        {
            props["manifest_type"] = "worker";
            props["framework_type"] = "worker";
        }
        else if (hasBin && (isWeb || isGrpc))
        {
            props["manifest_type"] = "application";
            props["framework_type"] = isGrpc ? "api" : "web";
            if (isGrpc) props["has_grpc"] = "true";
        }
        else if (hasBin && (isCliDep || dirName.EndsWith("-cli") || dirName.EndsWith("_cli") || dirName == "cli" || dirName.EndsWith("-tool") || dirName.EndsWith("_tool") || normPath.Contains("/src/bin/")))
        {
            props["manifest_type"] = "cli";
            props["framework_type"] = "cli";
            props["has_cli_bin"] = "true";
        }
        else if (hasBin)
        {
            props["manifest_type"] = "application";
            props["framework_type"] = isWeb ? "web" : "application";
        }
        else if (hasLib)
        {
            props["manifest_type"] = "library";
            props["framework_type"] = "library";
        }
        else
        {
            props["manifest_type"] = "library";
            props["framework_type"] = "library";
        }

        return props;
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        var crateRootCargo = RustModuleResolver.FindCrateRoot(filePath);
        CargoManifest? manifest = null;
        if (!string.IsNullOrEmpty(crateRootCargo) && File.Exists(crateRootCargo))
        {
            manifest = GetOrLoadManifest(crateRootCargo);
        }

        return RustModuleResolver.ResolveUsePath(importPath, filePath, manifest);
    }

    private CargoManifest? GetOrLoadManifest(string cargoTomlPath)
    {
        return _manifestCache.GetOrAdd(cargoTomlPath, path =>
        {
            if (File.Exists(path))
            {
                return CargoManifestParser.ParseManifest(path);
            }
            return null;
        });
    }
}
