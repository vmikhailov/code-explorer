namespace CodeExplorer.Parser.Rust;

/// <summary>
/// Resolves Rust module hierarchies, file-to-module mappings, and use declaration paths
/// according to Rust 2018/2021 edition rules.
/// </summary>
public static class RustModuleResolver
{
    private static readonly HashSet<string> StandardCrates = new(StringComparer.OrdinalIgnoreCase)
    {
        "std", "core", "alloc", "proc_macro", "test"
    };

    /// <summary>
    /// Finds the crate root file (src/lib.rs or src/main.rs) for a given Rust source file.
    /// </summary>
    public static string? FindCrateRoot(string sourceFilePath)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(sourceFilePath));
        while (!string.IsNullOrEmpty(dir))
        {
            var cargoToml = Path.Combine(dir, "Cargo.toml");
            if (File.Exists(cargoToml))
            {
                var libRs = Path.Combine(dir, "src", "lib.rs");
                if (File.Exists(libRs)) return libRs;

                var mainRs = Path.Combine(dir, "src", "main.rs");
                if (File.Exists(mainRs)) return mainRs;

                return cargoToml;
            }

            var parent = Directory.GetParent(dir);
            if (parent == null || parent.FullName.Equals(dir, StringComparison.OrdinalIgnoreCase))
                break;
            dir = parent.FullName;
        }

        return null;
    }

    /// <summary>
    /// Resolves an explicit module declaration (mod foo;) to its corresponding file path.
    /// Checks foo.rs and foo/mod.rs according to Rust 2018 and Rust 2015 path rules.
    /// </summary>
    public static string? ResolveModuleFile(string currentFilePath, string moduleName)
    {
        if (string.IsNullOrWhiteSpace(currentFilePath) || string.IsNullOrWhiteSpace(moduleName))
            return null;

        var fullPath = Path.GetFullPath(currentFilePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrEmpty(dir)) return null;

        var fileName = Path.GetFileName(fullPath);
        var isModOrRoot = fileName.Equals("mod.rs", StringComparison.OrdinalIgnoreCase) ||
                          fileName.Equals("lib.rs", StringComparison.OrdinalIgnoreCase) ||
                          fileName.Equals("main.rs", StringComparison.OrdinalIgnoreCase);

        // 1. Check in the current directory: dir/foo.rs
        var candidate1 = Path.Combine(dir, $"{moduleName}.rs");
        if (File.Exists(candidate1)) return candidate1;

        // 2. Check directory module: dir/foo/mod.rs
        var candidate2 = Path.Combine(dir, moduleName, "mod.rs");
        if (File.Exists(candidate2)) return candidate2;

        // 3. If current file is e.g. services.rs (not mod.rs/lib.rs), check dir/services/foo.rs
        if (!isModOrRoot)
        {
            var fileStem = Path.GetFileNameWithoutExtension(fullPath);
            var candidate3 = Path.Combine(dir, fileStem, $"{moduleName}.rs");
            if (File.Exists(candidate3)) return candidate3;

            var candidate4 = Path.Combine(dir, fileStem, moduleName, "mod.rs");
            if (File.Exists(candidate4)) return candidate4;
        }

        return null;
    }

    /// <summary>
    /// Computes the logical Rust module path (e.g., crate::services::order) for a source file.
    /// </summary>
    public static string GetLogicalModulePath(string sourceFilePath, string crateRootDir)
    {
        var relPath = Path.GetRelativePath(crateRootDir, sourceFilePath).Replace('\\', '/');
        if (relPath.StartWithIC("src/"))
        {
            relPath = relPath["src/".Length..];
        }

        if (relPath.Equals("lib.rs", StringComparison.OrdinalIgnoreCase) ||
            relPath.Equals("main.rs", StringComparison.OrdinalIgnoreCase))
        {
            return "crate";
        }

        if (relPath.EndsWith("/mod.rs", StringComparison.OrdinalIgnoreCase))
        {
            var modDir = relPath[..^"/mod.rs".Length];
            return "crate::" + modDir.Replace('/', ':');
        }

        if (relPath.EndsWith(".rs", StringComparison.OrdinalIgnoreCase))
        {
            var withoutExt = relPath[..^".rs".Length];
            return "crate::" + withoutExt.Replace('/', ':');
        }

        return "crate";
    }

    /// <summary>
    /// Resolves whether a Rust use path or import path is internal or external.
    /// </summary>
    public static ImportType ResolveUsePath(
        string usePath,
        string currentFilePath,
        CargoManifest? manifest)
    {
        if (string.IsNullOrWhiteSpace(usePath))
            return ImportType.External;

        var trimmed = usePath.Trim();

        // 1. Rust keywords at path start indicate internal module navigation
        if (trimmed.StartsWith("crate::", StringComparison.Ordinal) ||
            trimmed.StartsWith("super::", StringComparison.Ordinal) ||
            trimmed.StartsWith("self::", StringComparison.Ordinal) ||
            trimmed is "crate" or "super" or "self")
        {
            return ImportType.Internal;
        }

        // 2. Extract leading path segment (e.g. "tokio" from "tokio::sync::Mutex")
        var sepIdx = trimmed.IndexOf("::", StringComparison.Ordinal);
        var leadingSegment = sepIdx > 0 ? trimmed[..sepIdx] : trimmed;

        // 3. Rust standard library crates are external
        if (StandardCrates.Contains(leadingSegment))
        {
            return ImportType.External;
        }

        // 4. Check Cargo manifest dependencies and package name
        if (manifest != null)
        {
            var normPkgName = manifest.Package?.Name?.Replace('-', '_');
            if (!string.IsNullOrEmpty(normPkgName) &&
                leadingSegment.Equals(normPkgName, StringComparison.OrdinalIgnoreCase))
            {
                return ImportType.Internal;
            }

            var matchesDep = manifest.Dependencies.Any(d => d.Name.Replace('-', '_').Equals(leadingSegment, StringComparison.OrdinalIgnoreCase)) ||
                             manifest.DevDependencies.Any(d => d.Name.Replace('-', '_').Equals(leadingSegment, StringComparison.OrdinalIgnoreCase)) ||
                             manifest.BuildDependencies.Any(d => d.Name.Replace('-', '_').Equals(leadingSegment, StringComparison.OrdinalIgnoreCase));

            if (matchesDep)
            {
                return ImportType.External;
            }
        }

        // 5. Check if leadingSegment maps to a local module file in the current directory or crate
        if (ResolveModuleFile(currentFilePath, leadingSegment) != null)
        {
            return ImportType.Internal;
        }

        var crateDir = manifest?.DirectoryPath ?? Path.GetDirectoryName(currentFilePath);
        if (!string.IsNullOrEmpty(crateDir))
        {
            var rootSrcCandidate = Path.Combine(crateDir, "src", $"{leadingSegment}.rs");
            if (File.Exists(rootSrcCandidate)) return ImportType.Internal;

            var rootModCandidate = Path.Combine(crateDir, "src", leadingSegment, "mod.rs");
            if (File.Exists(rootModCandidate)) return ImportType.Internal;
        }

        return ImportType.External;
    }
}
