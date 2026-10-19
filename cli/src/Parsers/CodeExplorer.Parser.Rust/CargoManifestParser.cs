using Tomlyn;
using Tomlyn.Model;

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// High-performance TOML parser and resolution engine for Cargo.toml manifests and virtual workspaces.
/// </summary>
public static class CargoManifestParser
{
    public static CargoManifest ParseManifest(string cargoTomlPath, string? workspaceRootCargoTomlPath = null)
    {
        var fullPath = Path.GetFullPath(cargoTomlPath);
        var dirPath = Path.GetDirectoryName(fullPath) ?? string.Empty;

        if (!File.Exists(fullPath))
        {
            return new CargoManifest(fullPath, dirPath);
        }

        TomlTable? model;
        try
        {
            var content = File.ReadAllText(fullPath);
            model = TomlSerializer.Deserialize<TomlTable>(content);
        }
        catch
        {
            return new CargoManifest(fullPath, dirPath);
        }

        if (model == null)
        {
            return new CargoManifest(fullPath, dirPath);
        }

        var manifest = new CargoManifest(fullPath, dirPath);

        // 1. Parse [workspace]
        if (model.TryGetValue("workspace", out var wsObj) && wsObj is TomlTable wsTable)
        {
            manifest.HasWorkspace = true;
            manifest.Workspace = ParseWorkspaceTable(wsTable);
        }

        // 2. Parse [package]
        if (model.TryGetValue("package", out var pkgObj) && pkgObj is TomlTable pkgTable)
        {
            manifest.Package = ParsePackageTable(pkgTable);
        }

        // 3. Parse [lib]
        if (model.TryGetValue("lib", out var libObj) && libObj is TomlTable libTable)
        {
            var name = GetString(libTable, "name") ?? manifest.Package?.Name?.Replace('-', '_') ?? "lib";
            var path = GetString(libTable, "path") ?? "src/lib.rs";
            manifest.LibTarget = new CargoTarget(name, path);
        }
        else if (File.Exists(Path.Combine(dirPath, "src", "lib.rs")))
        {
            var name = manifest.Package?.Name?.Replace('-', '_') ?? "lib";
            manifest.LibTarget = new CargoTarget(name, "src/lib.rs");
        }

        // 4. Parse [[bin]]
        if (model.TryGetValue("bin", out var binObj))
        {
            if (binObj is TomlTableArray binArray)
            {
                foreach (var binTable in binArray)
                {
                    var name = GetString(binTable, "name") ?? manifest.Package?.Name ?? "bin";
                    var path = GetString(binTable, "path") ?? "src/main.rs";
                    manifest.BinTargets.Add(new CargoTarget(name, path));
                }
            }
            else if (binObj is TomlTable singleBin)
            {
                var name = GetString(singleBin, "name") ?? manifest.Package?.Name ?? "bin";
                var path = GetString(singleBin, "path") ?? "src/main.rs";
                manifest.BinTargets.Add(new CargoTarget(name, path));
            }
        }

        // Fallback for default src/main.rs or src/bin/*.rs
        if (manifest.BinTargets.Count == 0 && File.Exists(Path.Combine(dirPath, "src", "main.rs")))
        {
            manifest.BinTargets.Add(new CargoTarget(manifest.Package?.Name ?? "main", "src/main.rs"));
        }

        var binDir = Path.Combine(dirPath, "src", "bin");
        if (Directory.Exists(binDir))
        {
            try
            {
                foreach (var binFile in Directory.EnumerateFiles(binDir, "*.rs", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileNameWithoutExtension(binFile);
                    var rel = Path.GetRelativePath(dirPath, binFile).Replace('\\', '/');
                    if (!manifest.BinTargets.Any(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    {
                        manifest.BinTargets.Add(new CargoTarget(name, rel));
                    }
                }
            }
            catch
            {
                // Ignore IO errors
            }
        }

        // 5. Parse [[test]]
        if (model.TryGetValue("test", out var testObj) && testObj is TomlTableArray testArray)
        {
            foreach (var testTable in testArray)
            {
                var name = GetString(testTable, "name") ?? "test";
                var path = GetString(testTable, "path") ?? $"tests/{name}.rs";
                manifest.TestTargets.Add(new CargoTarget(name, path));
            }
        }

        // 6. Parse [[example]]
        if (model.TryGetValue("example", out var exampleObj) && exampleObj is TomlTableArray exampleArray)
        {
            foreach (var exampleTable in exampleArray)
            {
                var name = GetString(exampleTable, "name") ?? "example";
                var path = GetString(exampleTable, "path") ?? $"examples/{name}.rs";
                manifest.ExampleTargets.Add(new CargoTarget(name, path));
            }
        }

        // 7. Parse [dependencies], [dev-dependencies], [build-dependencies]
        if (model.TryGetValue("dependencies", out var depsObj) && depsObj is TomlTable depsTable)
        {
            manifest.Dependencies.AddRange(ParseDependencies(depsTable, "dependencies", dirPath));
        }

        if (model.TryGetValue("dev-dependencies", out var devDepsObj) && devDepsObj is TomlTable devDepsTable)
        {
            manifest.DevDependencies.AddRange(ParseDependencies(devDepsTable, "dev-dependencies", dirPath));
        }

        if (model.TryGetValue("build-dependencies", out var buildDepsObj) && buildDepsObj is TomlTable buildDepsTable)
        {
            manifest.BuildDependencies.AddRange(ParseDependencies(buildDepsTable, "build-dependencies", dirPath));
        }

        // 8. Parse [target.'...'.dependencies]
        if (model.TryGetValue("target", out var targetObj) && targetObj is TomlTable targetTable)
        {
            foreach (var (_, targetVal) in targetTable)
            {
                if (targetVal is TomlTable targetCfgTable)
                {
                    if (targetCfgTable.TryGetValue("dependencies", out var tDeps) && tDeps is TomlTable tDepsTable)
                    {
                        manifest.Dependencies.AddRange(ParseDependencies(tDepsTable, "dependencies", dirPath));
                    }
                    if (targetCfgTable.TryGetValue("dev-dependencies", out var tDevDeps) && tDevDeps is TomlTable tDevDepsTable)
                    {
                        manifest.DevDependencies.AddRange(ParseDependencies(tDevDepsTable, "dev-dependencies", dirPath));
                    }
                    if (targetCfgTable.TryGetValue("build-dependencies", out var tBuildDeps) && tBuildDeps is TomlTable tBuildDepsTable)
                    {
                        manifest.BuildDependencies.AddRange(ParseDependencies(tBuildDepsTable, "build-dependencies", dirPath));
                    }
                }
            }
        }

        // 9. Resolve workspace inheritance if needed
        ResolveWorkspaceInheritance(manifest, workspaceRootCargoTomlPath);

        return manifest;
    }

    public static string? FindWorkspaceRoot(string startingDir)
    {
        var current = Path.GetFullPath(startingDir);
        while (!string.IsNullOrEmpty(current))
        {
            var candidateCargoToml = Path.Combine(current, "Cargo.toml");
            if (File.Exists(candidateCargoToml))
            {
                try
                {
                    var text = File.ReadAllText(candidateCargoToml);
                    if (text.Contains("[workspace]"))
                    {
                        return candidateCargoToml;
                    }
                }
                catch
                {
                    // Ignore read error and continue ascending
                }
            }

            var parent = Directory.GetParent(current);
            if (parent == null || parent.FullName.Equals(current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            current = parent.FullName;
        }

        return null;
    }

    public static IReadOnlyList<string> GetWorkspaceMembers(string workspaceRootCargoTomlPath)
    {
        var rootDir = Path.GetDirectoryName(Path.GetFullPath(workspaceRootCargoTomlPath)) ?? string.Empty;
        var manifest = ParseManifest(workspaceRootCargoTomlPath);
        if (manifest.Workspace == null || manifest.Workspace.Members.Count == 0)
        {
            return Array.Empty<string>();
        }

        var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var excludeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var excl in manifest.Workspace.Exclude)
        {
            var fullExcl = Path.GetFullPath(Path.Combine(rootDir, excl));
            excludeSet.Add(fullExcl);
        }

        foreach (var memberPattern in manifest.Workspace.Members)
        {
            var normalizedPattern = memberPattern.Replace('\\', '/').Trim();
            if (normalizedPattern.EndsWith("/*", StringComparison.Ordinal))
            {
                var baseDir = Path.GetFullPath(Path.Combine(rootDir, normalizedPattern[..^2]));
                if (Directory.Exists(baseDir))
                {
                    try
                    {
                        foreach (var subDir in Directory.GetDirectories(baseDir))
                        {
                            var fullSubDir = Path.GetFullPath(subDir);
                            if (excludeSet.Contains(fullSubDir)) continue;
                            if (File.Exists(Path.Combine(fullSubDir, "Cargo.toml")))
                            {
                                results.Add(fullSubDir);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore directory scan errors
                    }
                }
            }
            else
            {
                var memberDir = Path.GetFullPath(Path.Combine(rootDir, normalizedPattern));
                if (Directory.Exists(memberDir) && !excludeSet.Contains(memberDir))
                {
                    if (File.Exists(Path.Combine(memberDir, "Cargo.toml")))
                    {
                        results.Add(memberDir);
                    }
                }
            }
        }

        return results.ToList();
    }

    private static CargoWorkspace ParseWorkspaceTable(TomlTable wsTable)
    {
        var ws = new CargoWorkspace();

        if (wsTable.TryGetValue("members", out var mObj) && mObj is TomlArray mArray)
        {
            foreach (var m in mArray)
            {
                if (m is string str) ws.Members.Add(str);
            }
        }

        if (wsTable.TryGetValue("default-members", out var dmObj) && dmObj is TomlArray dmArray)
        {
            foreach (var dm in dmArray)
            {
                if (dm is string str) ws.DefaultMembers.Add(str);
            }
        }

        if (wsTable.TryGetValue("exclude", out var exObj) && exObj is TomlArray exArray)
        {
            foreach (var ex in exArray)
            {
                if (ex is string str) ws.Exclude.Add(str);
            }
        }

        if (wsTable.TryGetValue("package", out var wsPkgObj) && wsPkgObj is TomlTable wsPkgTable)
        {
            ws.PackageVersion = GetStringOrTomlValue(wsPkgTable, "version");
            ws.PackageEdition = GetStringOrTomlValue(wsPkgTable, "edition");
        }

        if (wsTable.TryGetValue("dependencies", out var wsDepsObj) && wsDepsObj is TomlTable wsDepsTable)
        {
            foreach (var dep in ParseDependencies(wsDepsTable, "workspace.dependencies", string.Empty))
            {
                ws.WorkspaceDependencies[dep.Name] = dep;
            }
        }

        return ws;
    }

    private static CargoPackage ParsePackageTable(TomlTable pkgTable)
    {
        var name = GetString(pkgTable, "name") ?? "unknown";
        var version = "0.1.0";
        var versionIsWorkspace = false;
        var edition = "2021";
        var editionIsWorkspace = false;

        if (pkgTable.TryGetValue("version", out var vObj))
        {
            if (vObj is string vStr)
            {
                version = vStr;
            }
            else if (vObj is TomlTable vTable && vTable.TryGetValue("workspace", out var wsVal) && wsVal is bool isWs && isWs)
            {
                versionIsWorkspace = true;
            }
        }

        if (pkgTable.TryGetValue("edition", out var eObj))
        {
            if (eObj is string eStr)
            {
                edition = eStr;
            }
            else if (eObj is long eLong)
            {
                edition = eLong.ToString();
            }
            else if (eObj is TomlTable eTable && eTable.TryGetValue("workspace", out var wsVal) && wsVal is bool isWs && isWs)
            {
                editionIsWorkspace = true;
            }
        }

        var description = GetString(pkgTable, "description");

        return new CargoPackage(name, version, edition)
        {
            Description = description,
            VersionIsWorkspace = versionIsWorkspace,
            EditionIsWorkspace = editionIsWorkspace
        };
    }

    private static List<CargoDependency> ParseDependencies(TomlTable table, string section, string baseDir)
    {
        var list = new List<CargoDependency>();

        foreach (var (key, value) in table)
        {
            if (value is string verStr)
            {
                list.Add(new CargoDependency(key, section) { Version = verStr });
            }
            else if (value is TomlTable depTable)
            {
                var dep = new CargoDependency(key, section);

                if (depTable.TryGetValue("version", out var v) && v is string vStr)
                {
                    dep.Version = vStr;
                }

                if (depTable.TryGetValue("path", out var p) && p is string pStr)
                {
                    dep.Path = pStr;
                    if (!string.IsNullOrEmpty(baseDir))
                    {
                        dep.ResolvedAbsolutePath = Path.GetFullPath(Path.Combine(baseDir, pStr));
                    }
                }

                if (depTable.TryGetValue("workspace", out var ws) && ws is bool isWs)
                {
                    dep.IsWorkspace = isWs;
                }

                if (depTable.TryGetValue("git", out var g) && g is string gStr)
                {
                    dep.Git = gStr;
                }

                if (depTable.TryGetValue("branch", out var b) && b is string bStr)
                {
                    dep.Branch = bStr;
                }

                if (depTable.TryGetValue("tag", out var t) && t is string tStr)
                {
                    dep.Tag = tStr;
                }

                if (depTable.TryGetValue("features", out var f) && f is TomlArray fArr)
                {
                    foreach (var item in fArr)
                    {
                        if (item is string fStr) dep.Features.Add(fStr);
                    }
                }

                list.Add(dep);
            }
        }

        return list;
    }

    private static void ResolveWorkspaceInheritance(CargoManifest manifest, string? workspaceRootCargoTomlPath)
    {
        var wsRoot = workspaceRootCargoTomlPath ?? FindWorkspaceRoot(manifest.DirectoryPath);
        if (string.IsNullOrEmpty(wsRoot) || wsRoot.Equals(manifest.FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!File.Exists(wsRoot))
        {
            return;
        }

        CargoManifest rootManifest;
        try
        {
            rootManifest = ParseManifest(wsRoot);
        }
        catch
        {
            return;
        }

        if (rootManifest.Workspace == null) return;

        // Resolve package version & edition
        if (manifest.Package != null)
        {
            if (manifest.Package.VersionIsWorkspace && !string.IsNullOrEmpty(rootManifest.Workspace.PackageVersion))
            {
                manifest.Package.Version = rootManifest.Workspace.PackageVersion;
            }
            if (manifest.Package.EditionIsWorkspace && !string.IsNullOrEmpty(rootManifest.Workspace.PackageEdition))
            {
                manifest.Package.Edition = rootManifest.Workspace.PackageEdition;
            }
        }

        // Resolve workspace dependencies
        var allDeps = manifest.Dependencies.Concat(manifest.DevDependencies).Concat(manifest.BuildDependencies);
        foreach (var dep in allDeps)
        {
            if (dep.IsWorkspace && rootManifest.Workspace.WorkspaceDependencies.TryGetValue(dep.Name, out var wsDep))
            {
                dep.Version ??= wsDep.Version;
                dep.Path ??= wsDep.Path;
                dep.ResolvedAbsolutePath ??= wsDep.ResolvedAbsolutePath;
                dep.Git ??= wsDep.Git;
                dep.Branch ??= wsDep.Branch;
                dep.Tag ??= wsDep.Tag;
                if (wsDep.Features.Count > 0)
                {
                    foreach (var feat in wsDep.Features)
                    {
                        if (!dep.Features.Contains(feat)) dep.Features.Add(feat);
                    }
                }
            }
        }
    }

    private static string? GetString(TomlTable table, string key)
    {
        return table.TryGetValue(key, out var val) && val is string str ? str : null;
    }

    private static string? GetStringOrTomlValue(TomlTable table, string key)
    {
        if (!table.TryGetValue(key, out var val)) return null;
        if (val is string str) return str;
        if (val is long l) return l.ToString();
        return val.ToString();
    }
}

public class CargoManifest
{
    public string FilePath { get; }
    public string DirectoryPath { get; }
    public bool HasWorkspace { get; set; }
    public bool IsVirtualWorkspace => HasWorkspace && Package == null;
    public CargoWorkspace? Workspace { get; set; }
    public CargoPackage? Package { get; set; }
    public CargoTarget? LibTarget { get; set; }
    public List<CargoTarget> BinTargets { get; } = [];
    public List<CargoTarget> TestTargets { get; } = [];
    public List<CargoTarget> ExampleTargets { get; } = [];
    public List<CargoDependency> Dependencies { get; } = [];
    public List<CargoDependency> DevDependencies { get; } = [];
    public List<CargoDependency> BuildDependencies { get; } = [];

    public CargoManifest(string filePath, string directoryPath)
    {
        FilePath = filePath;
        DirectoryPath = directoryPath;
    }
}

public class CargoWorkspace
{
    public List<string> Members { get; } = [];
    public List<string> DefaultMembers { get; } = [];
    public List<string> Exclude { get; } = [];
    public string? PackageVersion { get; set; }
    public string? PackageEdition { get; set; }
    public Dictionary<string, CargoDependency> WorkspaceDependencies { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public class CargoPackage
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string Edition { get; set; }
    public string? Description { get; set; }
    public bool VersionIsWorkspace { get; set; }
    public bool EditionIsWorkspace { get; set; }

    public CargoPackage(string name, string version, string edition)
    {
        Name = name;
        Version = version;
        Edition = edition;
    }
}

public class CargoTarget
{
    public string Name { get; }
    public string Path { get; }

    public CargoTarget(string name, string path)
    {
        Name = name;
        Path = path;
    }
}

public class CargoDependency
{
    public string Name { get; }
    public string Section { get; }
    public string? Version { get; set; }
    public string? Path { get; set; }
    public string? ResolvedAbsolutePath { get; set; }
    public bool IsWorkspace { get; set; }
    public string? Git { get; set; }
    public string? Branch { get; set; }
    public string? Tag { get; set; }
    public List<string> Features { get; } = [];

    public CargoDependency(string name, string section)
    {
        Name = name;
        Section = section;
    }
}
