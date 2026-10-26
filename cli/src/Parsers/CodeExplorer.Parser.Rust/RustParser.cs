using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// Project and file parser for Rust language support in CodeExplorer.
/// Supports single-crate and virtual workspaces, Cargo.toml manifests, and Tree-sitter Rust AST parsing.
/// </summary>
[ProjectParser(Tier = ParserTier.Primary)]
public class RustParser : IProjectParser, IFileParser
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
        new Libraries.AwsSdkRustLibraryParser()
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
            var wsRootDir = Path.GetDirectoryName(wsRoot)!;
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
                .Select(path => (Path: path, Text: File.ReadAllText(path)))
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

            var source = File.ReadAllText(entryPointPath);
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
                    .Select(d => (d.Dependency, Directory: d.Directory!))
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
                .Select(name => name!.Replace('-', '_'))
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
                    var gpDir = Path.GetDirectoryName(gpCargo)!;
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
                var text = File.ReadAllText(file);
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

    public ProjectClassification? ClassifyProject(ProjectContext context)
    {
        if (context.ManifestProperties != null)
        {
            var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type");
            if (manifestType == "test" || context.ManifestProperties.GetValueOrDefault("is_test_project") == "true") return ProjectClassification.Test;
            if (context.ManifestProperties.GetValueOrDefault("is_orchestrator") == "true" ||
                context.ManifestProperties.GetValueOrDefault("role") == "Library" ||
                manifestType == "library") return ProjectClassification.Library;

            var primaryRole = context.ManifestProperties.GetValueOrDefault("primary_role");
            var role = context.ManifestProperties.GetValueOrDefault("role");
            if (primaryRole == "App" || role == "FrontendApp" || role == "App") return ProjectClassification.WebApp;
            if (primaryRole == "Worker" || role == "Worker") return ProjectClassification.Worker;
            if (primaryRole == "CliTool" || role == "CliTool") return ProjectClassification.CliTool;
            if (primaryRole == "Service" || role == "Service") return ProjectClassification.Service;
        }

        return IProjectParser.DefaultClassifyProject(this, context);
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
            props["primary_role"] = "Test";
            props["role"] = "Test";
            props["kind"] = "TestSuite";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Test" });
            return props;
        }

        // Check standalone binary subfolder (e.g. iris-mpc-bins/bin/retention-reaper)
        if (IsStandaloneBinarySubfolder(directoryPath, out var matchingBinName, out var gpCargo) && gpCargo != null)
        {
            return ExtractStandaloneManifestProperties(directoryPath, filesInDirectory, matchingBinName, gpCargo, dirName, props);
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

        // Check if pure test harness
        if (manifest.Dependencies.Count == 0 && manifest.DevDependencies.Count > 0 && manifest.LibTarget == null && manifest.BinTargets.Count == 0)
        {
            props["manifest_type"] = "test";
            props["is_test_project"] = "true";
            props["primary_role"] = "Test";
            props["role"] = "Test";
            props["kind"] = "TestSuite";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Test" });
            return props;
        }

        var allDeps = manifest.Dependencies
            .Concat(manifest.DevDependencies)
            .Concat(manifest.BuildDependencies)
            .ToList();

        var allDepNames = new HashSet<string>(allDeps.Select(d => d.Name.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
        var runtimeDepNames = new HashSet<string>(manifest.Dependencies.Select(d => d.Name.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

        var isWeb = allDepNames.Any(d => d is "axum" or "actix-web" or "actix_web" or "rocket" or "warp" or "poem" or "salvo");
        var isGrpc = allDepNames.Any(d => d is "tonic" or "tarpc" or "grpc");
        var isCliDep = allDepNames.Any(d => d is "clap" or "clap_builder" or "structopt" or "argh" or "pico-args" or "lexopt" or "ratatui" or "crossterm" or "dialoguer");
        var isWorkerDep = allDepNames.Any(d => d is "aws-sdk-sqs" or "rdkafka" or "lapin" or "async-nats" or "async_nats" or "fang" or "apalis" or "tokio-cron-scheduler");
        var isMigrationDep = allDepNames.Any(d => d is "refinery" or "diesel_migrations" or "diesel-migrations");
        var crateName = manifest.Package?.Name ?? dirName;
        var hasSubsystemCapability = HasArchitecturalSubsystemCapability(directoryPath, runtimeDepNames, crateName);

        var migrationsDir = Path.Combine(directoryPath, "migrations");
        var hasMigrations = isMigrationDep ||
                            (Directory.Exists(migrationsDir) && Directory.EnumerateFiles(migrationsDir, "*.sql", SearchOption.AllDirectories).Any());

        // Model a package with multiple executable targets as a container for those targets,
        // not as a single service inferred from the union of all binary dependencies.
        if (manifest.BinTargets.Count > 1)
        {
            var role = manifest.LibTarget is null ? "CliTool" : "Library";
            props["primary_role"] = role;
            props["role"] = role;
            props["kind"] = role;
            props["manifest_type"] = manifest.LibTarget is null ? "cli" : "library";
            props["framework_type"] = manifest.LibTarget is null ? "cli" : "library";
            props["is_library"] = manifest.LibTarget is null ? "false" : "true";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { role });
            return props;
        }

        var hasDirectBin = manifest.BinTargets.Count > 0 ||
                           File.Exists(Path.Combine(directoryPath, "src", "main.rs")) ||
                           Directory.Exists(Path.Combine(directoryPath, "src", "bin"));
        var hasLibCapability = manifest.LibTarget != null || File.Exists(Path.Combine(directoryPath, "src", "lib.rs"));

        // Inspect companion *-bins crate in workspace
        var companionBinFiles = new List<string>();
        var parentDir = Directory.GetParent(directoryPath)?.FullName;
        if (parentDir != null)
        {
            try
            {
                foreach (var sibDir in Directory.EnumerateDirectories(parentDir))
                {
                    var sibName = Path.GetFileName(sibDir);
                    if (sibName.EndsWith("-bins", StringComparison.OrdinalIgnoreCase) ||
                        sibName.EndsWith("_bins", StringComparison.OrdinalIgnoreCase) ||
                        sibName.Equals("bins", StringComparison.OrdinalIgnoreCase))
                    {
                        var companionSubDir = Path.Combine(sibDir, "bin", crateName);
                        if (!Directory.Exists(companionSubDir))
                        {
                            companionSubDir = Path.Combine(sibDir, "bin", crateName.Replace('-', '_'));
                        }
                        if (Directory.Exists(companionSubDir))
                        {
                            companionBinFiles.AddRange(Directory.EnumerateFiles(companionSubDir, "*.rs", SearchOption.AllDirectories));
                        }
                    }
                }
            }
            catch { }
        }

        var hasCompanionBin = companionBinFiles.Count > 0;
        var hasCompanionServer = companionBinFiles.Any(f => {
            var fn = Path.GetFileName(f).ToLowerInvariant();
            return fn.Contains("server") || fn.Contains("api") || fn.Contains("gateway");
        });
        var hasCompanionWorker = companionBinFiles.Any(f => {
            var fn = Path.GetFileName(f).ToLowerInvariant();
            return fn.Contains("genesis") || fn.Contains("reaper") || fn.Contains("rerandomize") || fn.Contains("consumer");
        });

        // Library crates with their own architectural capability remain subsystems even when
        // sibling binaries consume them; those binaries are modeled as separate projects.
        if (hasLibCapability && !hasDirectBin && hasSubsystemCapability)
        {
            props["primary_role"] = "Library";
            props["role"] = "Library";
            props["kind"] = "Library";
            props["manifest_type"] = "library";
            props["framework_type"] = "library";
            props["is_library"] = "true";
            props["architectural_subsystem"] = "true";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Library" });
            return props;
        }

        // Pure library check:
        // If crate has lib capability, NO direct bin targets, and NO companion server/worker binaries:
        if (hasLibCapability && !hasDirectBin && !hasCompanionServer && !hasCompanionWorker)
        {
            props["primary_role"] = "Library";
            props["role"] = "Library";
            props["kind"] = "Library";
            props["manifest_type"] = "library";
            props["framework_type"] = "library";
            props["is_library"] = "true";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Library" });
            return props;
        }

        var localRsFiles = filesInDirectory.Where(f => f.EndsWith(".rs", StringComparison.OrdinalIgnoreCase)).ToList();
        var allScanFiles = localRsFiles.Concat(companionBinFiles).ToList();
        var combinedSourceText = string.Join(" ", allScanFiles.Select(f => {
            try { return File.ReadAllText(f); } catch { return ""; }
        }));

        var hasServerCode = hasCompanionServer || (hasDirectBin && (isWeb || isGrpc || combinedSourceText.Contains("axum::Router") || combinedSourceText.Contains("start_coordination_server")));
        var hasWorkerCode = hasCompanionWorker || isWorkerDep ||
            dirName.Contains("worker") || dirName.Contains("consumer") || dirName.Contains("scheduler") || dirName.Contains("reaper") ||
            (hasDirectBin && (combinedSourceText.Contains("CronJob") || combinedSourceText.Contains("receive_message")));
        var hasCliCode = (hasDirectBin || hasCompanionBin) && (isCliDep ||
            companionBinFiles.Any(f => {
                var fn = Path.GetFileName(f).ToLowerInvariant();
                return fn.Contains("client") || fn.Contains("cli") || fn.Contains("tool");
            }) || combinedSourceText.Contains("clap::Parser"));

        var hasServiceCapability = (hasDirectBin && (isWeb || isGrpc || hasServerCode)) || hasCompanionServer;
        var hasWorkerCapability = hasWorkerCode;
        var hasCliCapability = hasCliCode;

        string primaryRole;
        if (dirName.Contains("reaper") || dirName.Contains("upgrade-hawk") || (!hasServiceCapability && hasWorkerCapability))
        {
            primaryRole = "Worker";
        }
        else if (hasServiceCapability)
        {
            primaryRole = "Service";
        }
        else if (hasWorkerCapability)
        {
            primaryRole = "Worker";
        }
        else if (hasCliCapability)
        {
            primaryRole = "CliTool";
        }
        else if (hasLibCapability)
        {
            primaryRole = "Library";
        }
        else
        {
            primaryRole = "Library";
        }

        var detectedRoles = new List<string>();
        if (hasServiceCapability) detectedRoles.Add("Service");
        if (hasWorkerCapability) detectedRoles.Add("Worker");
        if (hasCliCapability) detectedRoles.Add("CliTool");
        if (hasLibCapability) detectedRoles.Add("Library");

        var secondaryRoles = detectedRoles
            .Where(r => !string.Equals(r, primaryRole, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        var isLibrary = primaryRole == "Library" && secondaryRoles.Count == 0;

        props["primary_role"] = primaryRole;
        props["role"] = primaryRole;
        props["kind"] = primaryRole;
        props["manifest_type"] = primaryRole == "Service" ? "application" : (primaryRole == "Worker" ? "worker" : (primaryRole == "Library" ? "library" : "cli"));
        props["framework_type"] = primaryRole == "Service" ? (isGrpc ? "api" : "web") : (primaryRole == "Worker" ? "worker" : (primaryRole == "Library" ? "library" : "cli"));
        props["is_library"] = isLibrary ? "true" : "false";
        props["secondary_roles"] = System.Text.Json.JsonSerializer.Serialize(secondaryRoles);
        props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { primaryRole }.Concat(secondaryRoles).Distinct());

        return props;
    }

    private static bool HasArchitecturalSubsystemCapability(
        string directoryPath,
        HashSet<string> dependencyNames,
        string crateName)
    {
        var nameTokens = crateName.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        var hasDomainName = nameTokens.Any(token => token is "cpu" or "gpu" or "store" or
            "engine" or "runtime" or "upgrade" or "protocol" or "network" or "database" or "persistence");
        var hasUtilityName = nameTokens.Any(token => token is "common" or "utils" or "utilities");
        if (hasUtilityName && !hasDomainName)
        {
            return false;
        }

        var hasSubsystemName = hasDomainName || nameTokens.Contains("mpc", StringComparer.OrdinalIgnoreCase);
        var hasStrongSubsystemDependency = dependencyNames.Contains("cudarc") ||
                                           dependencyNames.Contains("sqlx") ||
                                           dependencyNames.Contains("tonic");
        var hasQueueDependency = dependencyNames.Contains("aws-sdk-sqs");
        var hasInfrastructureDependency = hasStrongSubsystemDependency || (hasQueueDependency && !hasUtilityName);
        var shouldMeasureSize = hasSubsystemName;

        if (!hasInfrastructureDependency && !shouldMeasureSize)
        {
            return false;
        }

        var sourceFiles = Directory.EnumerateFiles(directoryPath, "*.rs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
                                segment.Equals("benches", StringComparison.OrdinalIgnoreCase) ||
                                segment.Equals("target", StringComparison.OrdinalIgnoreCase) ||
                                segment.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var source = string.Join('\n', sourceFiles.Select(File.ReadAllText));
        var nonEmptyLines = source.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line));

        if (shouldMeasureSize && nonEmptyLines >= 1500)
        {
            return true;
        }

        if (!hasInfrastructureDependency)
        {
            return false;
        }

        return dependencyNames.Contains("cudarc") ||
               (dependencyNames.Contains("sqlx") &&
                (source.Contains("PgPoolOptions", StringComparison.Ordinal) ||
                 source.Contains("PoolOptions", StringComparison.Ordinal) ||
                 source.Contains("PgConnection", StringComparison.Ordinal) ||
                 source.Contains("sqlx::migrate!", StringComparison.Ordinal))) ||
               (dependencyNames.Contains("tonic") &&
                (source.Contains("Server::builder", StringComparison.Ordinal) ||
                 source.Contains("TcpListener::bind", StringComparison.Ordinal) ||
                 source.Contains("Endpoint::connect", StringComparison.Ordinal))) ||
               (hasQueueDependency && !hasUtilityName &&
                (source.Contains("receive_message", StringComparison.Ordinal) ||
                 source.Contains("send_message", StringComparison.Ordinal)));
    }

    private Dictionary<string, string> ExtractStandaloneManifestProperties(
        string directoryPath,
        string[] filesInDirectory,
        string? matchingBinName,
        string gpCargo,
        string dirName,
        Dictionary<string, string> props)
    {
        var gpManifest = GetOrLoadManifest(gpCargo);
        var binName = (matchingBinName ?? dirName).ToLowerInvariant();

        var allDeps = gpManifest != null ? gpManifest.Dependencies : new List<CargoDependency>();
        var usedCrates = ScanUsedCrates(directoryPath);
        var activeDeps = allDeps.Where(d => usedCrates.Count == 0 || usedCrates.Contains(d.Name.Replace('-', '_')) || usedCrates.Contains(d.Name)).ToList();
        var depNames = new HashSet<string>(activeDeps.Select(d => d.Name.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

        var isWeb = depNames.Any(d => d is "axum" or "actix-web" or "actix_web" or "rocket" or "warp" or "poem" or "salvo");
        var isGrpc = depNames.Any(d => d is "tonic" or "tarpc" or "grpc");
        var isCliDep = depNames.Any(d => d is "clap" or "clap_builder" or "structopt" or "argh" or "pico-args" or "lexopt");
        var isWorkerDep = depNames.Any(d => d is "aws-sdk-sqs" or "rdkafka" or "lapin" or "async-nats" or "async_nats" or "fang" or "apalis" or "tokio-cron-scheduler");

        var rsFiles = filesInDirectory.Where(f => f.EndsWith(".rs", StringComparison.OrdinalIgnoreCase)).ToList();
        var sourceText = string.Join(" ", rsFiles.Select(f => {
            try { return File.ReadAllText(f); } catch { return ""; }
        }));

        var hasServerCode = isWeb || isGrpc || sourceText.Contains("start_coordination_server") || sourceText.Contains("axum::Router") || binName.Contains("server");
        var hasWorkerCode = isWorkerDep || binName.Contains("reaper") || binName.Contains("worker") || binName.Contains("consumer") || binName.Contains("cron") || sourceText.Contains("CronJob") || sourceText.Contains("receive_message");
        var hasCliCode = isCliDep || binName.EndsWith("-cli") || binName.EndsWith("_cli") || binName == "cli" || sourceText.Contains("clap::Parser") || sourceText.Contains("Parser::parse");

        var hasServiceCapability = hasServerCode;
        var hasWorkerCapability = hasWorkerCode;
        var hasCliCapability = hasCliCode;

        string primaryRole;
        if (binName.Contains("reaper") || binName.Contains("cron") || (!hasServiceCapability && hasWorkerCapability))
        {
            primaryRole = "Worker";
        }
        else if (hasServiceCapability)
        {
            primaryRole = "Service";
        }
        else if (hasWorkerCapability)
        {
            primaryRole = "Worker";
        }
        else
        {
            primaryRole = "CliTool";
        }

        var detectedRoles = new List<string>();
        if (hasServiceCapability) detectedRoles.Add("Service");
        if (hasWorkerCapability) detectedRoles.Add("Worker");
        if (hasCliCapability) detectedRoles.Add("CliTool");

        var secondaryRoles = detectedRoles
            .Where(r => !string.Equals(r, primaryRole, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        props["primary_role"] = primaryRole;
        props["role"] = primaryRole;
        props["kind"] = primaryRole;
        props["manifest_type"] = primaryRole == "Service" ? "application" : (primaryRole == "Worker" ? "worker" : "cli");
        props["framework_type"] = primaryRole == "Service" ? (isGrpc ? "api" : "web") : (primaryRole == "Worker" ? "worker" : "cli");
        props["is_library"] = "false";
        props["secondary_roles"] = System.Text.Json.JsonSerializer.Serialize(secondaryRoles);
        props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { primaryRole }.Concat(secondaryRoles).Distinct());
        return props;
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
