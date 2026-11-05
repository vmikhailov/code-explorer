using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// Project and file parser for Rust language support in CodeExplorer.
/// Supports single-crate and virtual workspaces, Cargo.toml manifests, and Tree-sitter Rust AST parsing.
/// </summary>
[ProjectParser(Tier = ParserTier.Primary)]
public partial class RustParser : IProjectParser, IFileParser
{
    public string LanguageName => "rust";

    public string ProjectType => "rust";

    public IReadOnlyCollection<string> ExcludedFolders => ["target"];
    public IReadOnlyCollection<string> ManifestFileNames => ["Cargo.toml"];

    public bool? CheckDirectoryExclusion(string dirName, string? fullPath = null)
    {
        if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(fullPath))
        {
            try
            {
                var norm = fullPath.Replace('\\', '/');
                if (norm.EndsWith(".rs", StringComparison.OrdinalIgnoreCase)) return false;
                if (Directory.Exists(fullPath) && Directory.EnumerateFiles(fullPath, "*.rs", SearchOption.AllDirectories).Any()) return false;
            }
            catch
            {
                // ignore
            }
        }
        return null;
    }

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        RustSyntaxConverter.Instance.Parse(sourceText, LanguageName, filePath, relativePath);

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
        new Libraries.AsyncNatsLibraryParser(),
        new Libraries.AwsSdkRustLibraryParser(),
        Libraries.RustTestLibraryParser.Instance
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

    public static string? FindCrateCargoToml(string filePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        while (!string.IsNullOrEmpty(dir))
        {
            var cargoToml = Path.Combine(dir, "Cargo.toml");
            if (File.Exists(cargoToml)) return cargoToml;
            var parent = Directory.GetParent(dir);
            if (parent == null || parent.FullName.Equals(dir, StringComparison.OrdinalIgnoreCase))
                break;
            dir = parent.FullName;
        }
        return null;
    }

    private bool IsStandaloneBinarySubfolder(string directoryPath, out string? matchingBinName, out string? grandparentCargoToml)
    {
        matchingBinName = null;
        grandparentCargoToml = null;

        var fullDir = Path.GetFullPath(directoryPath);
        var dirName = Path.GetFileName(fullDir.TrimEnd('/', '\\'));
        var parentDir = Path.GetDirectoryName(fullDir);
        if (parentDir == null) return false;

        var parentName = Path.GetFileName(parentDir);
        if (!parentName.Equals("bin", StringComparison.OrdinalIgnoreCase)) return false;

        var grandparentDir = Path.GetDirectoryName(parentDir);
        if (grandparentDir == null) return false;

        var gpCargo = Path.Combine(grandparentDir, "Cargo.toml");
        if (!File.Exists(gpCargo)) return false;

        var gpManifest = GetOrLoadManifest(gpCargo);
        if (gpManifest == null) return false;

        if (gpManifest.LibTarget != null || gpManifest.BinTargets.Count <= 1) return false;

        var matchingBin = gpManifest.BinTargets.FirstOrDefault(b =>
            b.Path.Replace('\\', '/').StartWithIC($"bin/{dirName}/"));
        if (matchingBin == null) return false;

        var wsRoot = CargoManifestParser.FindWorkspaceRoot(grandparentDir);
        var wsRootManifest = !string.IsNullOrEmpty(wsRoot) && File.Exists(wsRoot) ? GetOrLoadManifest(wsRoot) : null;
        if (wsRootManifest?.Workspace != null)
        {
            var wsRootDir = Path.GetDirectoryName(wsRoot) ?? string.Empty;
            foreach (var member in wsRootManifest.Workspace.Members)
            {
                var memberToml = Path.Combine(wsRootDir, member, "Cargo.toml");
                if (File.Exists(memberToml))
                {
                    var mManifest = GetOrLoadManifest(memberToml);
                    if (mManifest?.Package?.Name != null &&
                        (mManifest.Package.Name.Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                         mManifest.Package.Name.Replace('-', '_').Equals(dirName.Replace('-', '_'), StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
            }
        }
        else
        {
            var wsParent = Directory.GetParent(grandparentDir)?.FullName;
            if (wsParent != null)
            {
                foreach (var sibling in Directory.EnumerateDirectories(wsParent))
                {
                    if (sibling.Equals(grandparentDir, StringComparison.OrdinalIgnoreCase)) continue;
                    var sibCargo = Path.Combine(sibling, "Cargo.toml");
                    if (File.Exists(sibCargo))
                    {
                        var sManifest = GetOrLoadManifest(sibCargo);
                        if (sManifest?.Package?.Name != null &&
                            (sManifest.Package.Name.Equals(dirName, StringComparison.OrdinalIgnoreCase) ||
                             sManifest.Package.Name.Replace('-', '_').Equals(dirName.Replace('-', '_'), StringComparison.OrdinalIgnoreCase)))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        matchingBinName = matchingBin.Name;
        grandparentCargoToml = gpCargo;
        return true;
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        var cargoToml = filesInDirectory.FirstOrDefault(f => Path.GetFileName(f).Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase));
        if (cargoToml == null)
        {
            return IsStandaloneBinarySubfolder(directoryPath, out _, out _);
        }

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
        else if (IsStandaloneBinarySubfolder(directoryPath, out var matchingBinName, out _))
        {
            return matchingBinName ?? Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
        }

        return Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
    }

    public IReadOnlyList<AdditionalProjectDescriptor> GetAdditionalProjects(string directoryPath, string[] filesInDirectory)
    {
        var cargoTomlPath = Path.Combine(directoryPath, "Cargo.toml");
        var manifest = File.Exists(cargoTomlPath) ? GetOrLoadManifest(cargoTomlPath) : null;
        if (manifest is null || manifest.BinTargets.Count <= 1)
        {
            return [];
        }

        var dockerfiles = Directory.GetParent(directoryPath) is { } workspaceParent
            ? Directory.EnumerateFiles(workspaceParent.FullName, "Dockerfile*", SearchOption.TopDirectoryOnly)
                .Select(path => (Path: path, Text: PhysicalFileStore.ReadAllText(path)))
                .ToArray()
            : [];
        var projects = new List<AdditionalProjectDescriptor>(manifest.BinTargets.Count);

        foreach (var target in manifest.BinTargets)
        {
            var entryPointPath = Path.GetFullPath(Path.Combine(directoryPath, target.Path));
            if (!File.Exists(entryPointPath))
            {
                continue;
            }

            var source = PhysicalFileStore.ReadAllText(entryPointPath);
            var normalizedName = target.Name.Replace('_', '-');
            var dockerService = dockerfiles.Any(d =>
                d.Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Any(line =>
                        (line.Contains("ENTRYPOINT", StringComparison.OrdinalIgnoreCase) ||
                         line.Contains("CMD", StringComparison.OrdinalIgnoreCase)) &&
                        (line.Contains(target.Name, StringComparison.OrdinalIgnoreCase) ||
                         line.Contains(normalizedName, StringComparison.OrdinalIgnoreCase))));
            var name = target.Name.ToLowerInvariant();
            var isWorker = name.Contains("sidecar") || name.Contains("reaper") ||
                           name.Contains("daemon") || name.Contains("worker");
            var isCliTaskByName = name.Contains("client") || name.Contains("cli") ||
                                  name.Contains("migrat") || name.Contains("init") ||
                                  name.Contains("genesis") || name.Contains("seed") ||
                                  name.Contains("rerandom") || name.Contains("encoding") ||
                                  name.Contains("bench") || name.Contains("metric") ||
                                  name.Contains("analysis");
            var hasServerPath = target.Path.Replace('\\', '/').Split('/')
                .Any(segment => segment.Equals("server", StringComparison.OrdinalIgnoreCase) ||
                                Path.GetFileNameWithoutExtension(segment).Equals("server", StringComparison.OrdinalIgnoreCase));
            var isService = !isWorker && !isCliTaskByName &&
                            (dockerService || name.EndsWith("-server", StringComparison.Ordinal) ||
                             hasServerPath ||
                             source.Contains("run_server", StringComparison.Ordinal) ||
                             source.Contains("axum::serve", StringComparison.Ordinal) ||
                             source.Contains("Server::builder", StringComparison.Ordinal) ||
                             source.Contains("start_coordination_server", StringComparison.Ordinal));
            var role = isWorker ? "Worker" : isService ? "Service" : "CliTool";
            var kind = role == "Service" ? "application" : role == "Worker" ? "worker" : "cli";
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["primary_role"] = role,
                ["role"] = role,
                ["kind"] = role,
                ["manifest_type"] = kind,
                ["framework_type"] = role == "Service" ? "api" : kind,
                ["is_library"] = "false",
                ["cargo_bin_target"] = target.Name,
                ["cargo_entrypoint"] = entryPointPath,
                ["cargo_manifest_dir"] = Path.GetFullPath(directoryPath),
                ["secondary_roles"] = "[]",
                ["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { role })
            };

            var binaryFiles = RustModuleReachability.CollectBinaryModuleFiles(entryPointPath);
            var usedCrates = ScanUsedCratesInFiles(binaryFiles);
            IReadOnlyCollection<string>? localDependencyPaths = null;
            if (usedCrates.Count > 0)
            {
                var usedLocalDependencies = manifest.Dependencies
                    .Concat(manifest.BuildDependencies)
                    .Where(dep => usedCrates.Contains(dep.Name.Replace('-', '_')))
                    .Select(dep => (Dependency: dep, Directory: ResolveLocalDependencyDirectory(dep, directoryPath)))
                    .Where(d => d.Directory != null)
                    .Select(d => (d.Dependency, Directory: d.Directory ?? string.Empty))
                    .DistinctBy(d => d.Directory, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                localDependencyPaths = usedLocalDependencies.Select(d => d.Directory).ToList();
                var reachability = ComputeSourceReachability(binaryFiles, usedLocalDependencies);
                if (reachability.Count > 0)
                {
                    properties[OntologyConstants.PropertyNames.SourceReachability] =
                        System.Text.Json.JsonSerializer.Serialize(reachability);
                }
            }

            projects.Add(new AdditionalProjectDescriptor(target.Name, entryPointPath, properties, localDependencyPaths));
        }

        return projects;
    }

    private static string? ResolveLocalDependencyDirectory(CargoDependency dep, string manifestDirectory)
    {
        if (!string.IsNullOrEmpty(dep.ResolvedAbsolutePath) && Directory.Exists(dep.ResolvedAbsolutePath))
        {
            return Path.GetFullPath(dep.ResolvedAbsolutePath);
        }

        if (!string.IsNullOrEmpty(dep.Path))
        {
            var resolved = Path.GetFullPath(Path.Combine(manifestDirectory, dep.Path));
            return Directory.Exists(resolved) ? resolved : null;
        }

        return null;
    }

    private const int MaxReachabilityCrateDepth = 6;

    /// <summary>
    /// Walks the local crate graph from a binary's source files and records, per dependency crate directory,
    /// which of its source files the binary can reach. Crates that are declared but never referenced from
    /// reachable code get an empty set; crates whose usage cannot be resolved are omitted (treated as fully reachable).
    /// </summary>
    private Dictionary<string, string[]> ComputeSourceReachability(
        IReadOnlyList<string> binaryFiles,
        IEnumerable<(CargoDependency Dependency, string Directory)> localDependencies)
    {
        var reachable = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var unknown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(IReadOnlyCollection<string> ConsumerFiles, CargoDependency Dependency, string Directory, int Depth)>();
        foreach (var (dependency, directory) in localDependencies)
        {
            queue.Enqueue((binaryFiles, dependency, directory, 0));
        }

        while (queue.Count > 0)
        {
            var (consumerFiles, dependency, dependencyDirectory, depth) = queue.Dequeue();
            if (unknown.Contains(dependencyDirectory))
            {
                continue;
            }

            var dependencyManifest = LoadCrateManifest(dependencyDirectory);
            var libraryPath = Path.Combine(dependencyDirectory, dependencyManifest?.LibTarget?.Path ?? "src/lib.rs");
            var crateIdentifiers = new[]
                {
                    dependency.Name,
                    dependencyManifest?.LibTarget?.Name,
                    dependencyManifest?.Package?.Name
                }
                .Where(name => !string.IsNullOrEmpty(name))
                .OfType<string>().Select(name => name.Replace('-', '_'))
                .ToHashSet(StringComparer.Ordinal);

            var reachableFiles = RustModuleReachability.ComputeReachableFiles(consumerFiles, crateIdentifiers, libraryPath);
            if (reachableFiles == null)
            {
                if (depth == 0)
                {
                    MarkUnknown(dependencyDirectory, unknown, 0);
                }
                else
                {
                    reachable.TryAdd(dependencyDirectory, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }
                continue;
            }

            if (!reachable.TryGetValue(dependencyDirectory, out var files))
            {
                files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                reachable[dependencyDirectory] = files;
            }

            var added = reachableFiles.Where(files.Add).ToList();
            if (added.Count == 0 || depth >= MaxReachabilityCrateDepth || dependencyManifest == null)
            {
                continue;
            }

            foreach (var nested in dependencyManifest.Dependencies.Concat(dependencyManifest.BuildDependencies))
            {
                var nestedDirectory = ResolveLocalDependencyDirectory(nested, dependencyDirectory);
                if (nestedDirectory != null)
                {
                    queue.Enqueue((added, nested, nestedDirectory, depth + 1));
                }
            }
        }

        return reachable
            .Where(entry => !unknown.Contains(entry.Key))
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private void MarkUnknown(string crateDirectory, HashSet<string> unknown, int depth)
    {
        if (!unknown.Add(crateDirectory) || depth >= MaxReachabilityCrateDepth)
        {
            return;
        }

        var manifest = LoadCrateManifest(crateDirectory);
        if (manifest == null)
        {
            return;
        }

        foreach (var nested in manifest.Dependencies.Concat(manifest.BuildDependencies))
        {
            var nestedDirectory = ResolveLocalDependencyDirectory(nested, crateDirectory);
            if (nestedDirectory != null)
            {
                MarkUnknown(nestedDirectory, unknown, depth + 1);
            }
        }
    }

    private CargoManifest? LoadCrateManifest(string crateDirectory)
    {
        var cargoToml = Path.Combine(crateDirectory, "Cargo.toml");
        return File.Exists(cargoToml) ? GetOrLoadManifest(cargoToml) : null;
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
        var crateCargoToml = FindCrateCargoToml(fullPath);
        CargoManifest? manifest = null;
        if (!string.IsNullOrEmpty(crateCargoToml) && File.Exists(crateCargoToml))
        {
            manifest = GetOrLoadManifest(crateCargoToml);
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
        if (IsStandaloneBinarySubfolder(projectDirectory, out _, out _))
        {
            return null;
        }

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
        if (File.Exists(cargoTomlPath))
        {
            try
            {
                var manifest = GetOrLoadManifest(cargoTomlPath);
                if (manifest != null)
                {
                    var isTestOnly = manifest.BinTargets.Count == 0 && manifest.LibTarget == null && manifest.TestTargets.Count > 0;
                    var localDeps = isTestOnly
                        ? manifest.Dependencies.Concat(manifest.DevDependencies).Concat(manifest.BuildDependencies)
                        : manifest.Dependencies.Concat(manifest.BuildDependencies);

                    foreach (var dep in localDeps)
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
                            if (Directory.Exists(resolved) && !localProjectPaths.Contains(resolved))
                            {
                                localProjectPaths.Add(resolved);
                            }
                        }
                    }

                    var allDeps = manifest.Dependencies
                        .Concat(manifest.DevDependencies)
                        .Concat(manifest.BuildDependencies);

                    foreach (var dep in allDeps)
                    {
                        if (string.IsNullOrEmpty(dep.Path) && string.IsNullOrEmpty(dep.ResolvedAbsolutePath))
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

        if (IsStandaloneBinarySubfolder(projectDirectory, out var matchingBinName, out var gpCargo) && gpCargo != null)
        {
            try
            {
                var gpManifest = GetOrLoadManifest(gpCargo);
                if (gpManifest != null)
                {
                    var gpDir = Path.GetDirectoryName(gpCargo) ?? string.Empty;
                    var usedCrates = ScanUsedCrates(projectDirectory);

                    foreach (var dep in gpManifest.Dependencies)
                    {
                        var normName = dep.Name.Replace('-', '_');
                        var isUsed = usedCrates.Count == 0 || usedCrates.Contains(normName) || usedCrates.Contains(dep.Name);
                        if (!isUsed) continue;

                        if (!string.IsNullOrEmpty(dep.ResolvedAbsolutePath) && Directory.Exists(dep.ResolvedAbsolutePath))
                        {
                            if (!localProjectPaths.Contains(dep.ResolvedAbsolutePath))
                            {
                                localProjectPaths.Add(dep.ResolvedAbsolutePath);
                            }
                        }
                        else if (!string.IsNullOrEmpty(dep.Path))
                        {
                            var resolved = Path.GetFullPath(Path.Combine(gpDir, dep.Path));
                            if (Directory.Exists(resolved) && !localProjectPaths.Contains(resolved))
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
        }

        return await Task.FromResult(new ProjectDependencyInfo(localProjectPaths, externalPackages));
    }

    private static HashSet<string> ScanUsedCrates(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return ScanUsedCratesInFiles(Directory.EnumerateFiles(directoryPath, "*.rs", SearchOption.AllDirectories));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static HashSet<string> ScanUsedCratesInFiles(IEnumerable<string> files)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var file in files)
            {
                var text = PhysicalFileStore.ReadAllText(file);
                var matches = Regex.Matches(text, @"(?:use\s+|extern\s+crate\s+)([a-zA-Z0-9_]+)");
                foreach (Match m in matches)
                {
                    if (m.Success && m.Groups.Count > 1)
                    {
                        used.Add(m.Groups[1].Value);
                    }
                }

                var pathMatches = Regex.Matches(text, @"\b([a-zA-Z0-9_]+)::");
                foreach (Match m in pathMatches)
                {
                    if (m.Success && m.Groups.Count > 1)
                    {
                        used.Add(m.Groups[1].Value);
                    }
                }
            }
        }
        catch
        {
        }

        return used;
    }

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        var crateRootCargo = FindCrateCargoToml(filePath) ?? RustModuleResolver.FindCrateRoot(filePath);
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
