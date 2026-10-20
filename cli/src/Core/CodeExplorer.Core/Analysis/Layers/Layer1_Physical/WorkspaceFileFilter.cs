namespace CodeExplorer.Core.Parser;

/// <summary>
/// Unified single source of truth for directory traversal, excluded paths, and file filtering.
/// Used symmetrically by Layer 1 full scan, incremental scan, FileWatcher, and project parsers.
/// </summary>
public static class WorkspaceFileFilter
{
    public const long MaxSourceFileSize = 1_048_576; // 1 MB limit for AST parsing

    /// <summary>
    /// Canonical single source of truth for excluded directory names across the entire system.
    /// </summary>
    public static readonly HashSet<string> ExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".github", ".vscode", ".idea", ".vs", ".go", "node_modules",
        "bin", "obj", "dist", "build", ".build", ".next", ".nuxt",
        ".turbo", ".cache", ".output", "out", "coverage", "scratch", "demo",
        "vendor", "bower_components", "third_party", "thirdparty", "3rdparty",
        ".codeexplorer", ".packages", ".agents", ".gemini", ".claude", ".cursor"
    };

    public static readonly HashSet<string> ManifestFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "pom.xml", "go.mod", "Cargo.toml", "Directory.Build.props", "Directory.Build.targets",
        "build.gradle", "build.gradle.kts", "settings.gradle", "settings.gradle.kts", "AndroidManifest.xml"
    };

    public static bool IsExcludedDirectory(string dirName, string? fullPath = null)
    {
        if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
            return IsBuildOutputBinDirectory(fullPath);
        }

        if (ExcludedDirectoryNames.Contains(dirName))
            return true;

        if (dirName.Equals("packages", StringComparison.OrdinalIgnoreCase) && fullPath != null)
        {
            return IsNuGetPackagesDirectory(fullPath);
        }

        return false;
    }

    public static bool IsBuildOutputBinDirectory(string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return true;

        try
        {
            var norm = fullPath.Replace('\\', '/');
            var binIdx = norm.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase);
            var binDir = fullPath;
            if (binIdx >= 0)
            {
                binDir = fullPath[..(binIdx + 4)];
            }
            else if (norm.EndsWith("/bin", StringComparison.OrdinalIgnoreCase))
            {
                binDir = fullPath;
            }

            var parent = Path.GetDirectoryName(binDir);
            if (parent != null)
            {
                var parentName = Path.GetFileName(parent);
                if (parentName.Equals("src", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (File.Exists(Path.Combine(parent, "Cargo.toml")) || File.Exists(Path.Combine(parent, "package.json")))
                    return false;
            }

            if (Directory.Exists(binDir) && Directory.EnumerateFiles(binDir, "*.rs", SearchOption.AllDirectories).Any())
            {
                return false;
            }

            if (fullPath.EndsWith(".rs", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        catch
        {
            return true;
        }

        return true;
    }

    public static bool IsNuGetPackagesDirectory(string fullPath)
    {
        try
        {
            // If it contains JS/TS workspace configuration, it is never a NuGet cache
            if (File.Exists(Path.Combine(fullPath, "package.json")))
                return false;

            var parent = Path.GetDirectoryName(fullPath);
            if (parent != null &&
                (File.Exists(Path.Combine(parent, "pnpm-workspace.yaml")) ||
                 File.Exists(Path.Combine(parent, "lerna.json")) ||
                 File.Exists(Path.Combine(parent, "turbo.json"))))
            {
                return false;
            }

            if (File.Exists(Path.Combine(fullPath, "repositories.config")))
                return true;

            if (parent != null && File.Exists(Path.Combine(parent, "packages.config")))
                return true;

            if (Directory.Exists(fullPath) && Directory.EnumerateFiles(fullPath, "*.nupkg", SearchOption.TopDirectoryOnly).Any())
                return true;
        }
        catch
        {
            // Best effort
        }

        return false;
    }

    public static bool IsManifestFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".csproj" or ".fsproj" or ".vbproj" || ManifestFileNames.Contains(fileName);
    }

    public static bool ShouldSkipFileName(string fileName)
    {
        var fn = fileName.ToLowerInvariant();

        // 1. Mocks and synthetic stubs
        if (fn.Contains("mock")) return true;

        // 2. Scratch, temporary, playground, and debug scratch files
        if (fn.StartsWith("scratch") || fn.Contains(".scratch.") || fn.Contains("_scratch.") ||
            fn.StartsWith("temp_") || fn.StartsWith("tmp_") ||
            fn.EndsWith("_debug.ts") || fn.EndsWith("_debug.js") ||
            fn.StartsWith("debug_") || fn.Contains("playground") || fn.Contains("scratchpad"))
        {
            return true;
        }

        // 3. TypeScript Ambient Declaration files (no executable code/endpoints/calls)
        if (fn.EndsWith(".d.ts")) return true;

        // 4. Minified, bundle, and vendor file conventions
        if (fn.EndsWith(".min.js") || fn.EndsWith(".min.mjs") || fn.EndsWith(".min.cjs") ||
            fn.EndsWith(".min.css") || fn.EndsWith(".bundle.js") || fn.EndsWith(".bundle.min.js")) return true;
        if (fn.Contains(".min.")) return true;

        return false;
    }

    public static bool ShouldSkipFile(FileInfo fileInfo)
    {
        if (ShouldSkipFileName(fileInfo.Name)) return true;

        // Oversized source files (> 1 MB are bundled distributions or generated data tables)
        try
        {
            if (fileInfo.Length > MaxSourceFileSize)
            {
                return true;
            }
        }
        catch
        {
            // Ignore file access errors
        }

        // Minification heuristic: check first 4KB for extremely long lines (> 1000 chars)
        var fileName = fileInfo.Name.ToLowerInvariant();
        if (fileName.EndsWith(".js") || fileName.EndsWith(".ts") || fileName.EndsWith(".css"))
        {
            if (IsMinifiedContent(fileInfo.FullName))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsMinifiedContent(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            var buffer = new char[8192];
            var read = reader.Read(buffer, 0, buffer.Length);
            if (read <= 0) return false;

            var lineLen = 0;
            var newlines = 0;

            for (int i = 0; i < read; i++)
            {
                if (buffer[i] == '\n')
                {
                    newlines++;
                    lineLen = 0;
                }
                else
                {
                    lineLen++;
                    if (lineLen > 1000)
                    {
                        return true;
                    }
                }
            }

            if (lineLen > 1000) return true;
            if (read >= 4096 && newlines <= 3) return true;
        }
        catch
        {
            // Fall through if file unreadable
        }
        return false;
    }

    /// <summary>
    /// Checks if a file is a source code file or configuration file that produces a File node in Layer 1.
    /// Excludes project manifests (.csproj, package.json, etc.) which are handled at Layer 2 (Boundaries).
    /// </summary>
    public static bool IsSupportedSourceOrConfigFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var hasParser = WorkspaceIndexer._fileParsers.Any(p => p.CanParse(ext) || p.CanParseFile(fileName));
        var isConfigFile = ConfigurationParser.IsConfigurationFile(fileName);
        var isAndroidManifest = fileName.Equals("AndroidManifest.xml", StringComparison.OrdinalIgnoreCase);

        return hasParser || isConfigFile || isAndroidManifest;
    }

    /// <summary>
    /// Checks if a file is tracked by the workspace indexer (source files, config files, AND project manifests).
    /// </summary>
    public static bool IsSupportedFileType(string fileName)
    {
        return IsSupportedSourceOrConfigFile(fileName) || IsManifestFile(fileName);
    }

    public static bool IsCandidateSourceFile(string fullPath)
    {
        var normalized = fullPath.Replace('\\', '/');
        var parts = normalized.Split('/');
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (IsExcludedDirectory(parts[i]))
                return false;
        }

        var fileName = Path.GetFileName(normalized);
        if (ShouldSkipFileName(fileName))
            return false;

        return IsSupportedFileType(fileName);
    }

    public static void RegisterLibManExclusions(
        string libmanPath,
        string currentDir,
        string workspaceRoot,
        GitIgnoreMatcher gitignore,
        Action<string>? log = null)
    {
        try
        {
            var content = File.ReadAllText(libmanPath);
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("libraries", out var libs) &&
                libs.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var lib in libs.EnumerateArray())
                {
                    if (lib.TryGetProperty("destination", out var destProp) &&
                        destProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var dest = destProp.GetString();
                        if (!string.IsNullOrWhiteSpace(dest))
                        {
                            var fullDest = Path.GetFullPath(Path.Combine(currentDir, dest));
                            var relDest = Path.GetRelativePath(workspaceRoot, fullDest).Replace('\\', '/').Trim('/');
                            if (!string.IsNullOrEmpty(relDest))
                            {
                                gitignore.AddPattern(relDest + "/");
                                log?.Invoke($"[WorkspaceFileFilter] LibMan: Excluded library destination '{relDest}'");
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"[WorkspaceFileFilter] Error reading libman.json: {ex.Message}");
        }
    }

    /// <summary>
    /// Unified directory and file enumeration that enforces all GitIgnore rules, excluded directories,
    /// libman vendor exclusions, and file-level filters consistently across full and incremental scanning.
    /// </summary>
    public static List<string> EnumerateCandidateFiles(
        string targetPath,
        string workspaceRoot,
        GitIgnoreMatcher gitignore,
        Action<string>? log = null)
    {
        var result = new List<string>();
        if (!Directory.Exists(targetPath)) return result;

        var stack = new Stack<string>();
        stack.Push(targetPath);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            var relativeDir = Path.GetRelativePath(workspaceRoot, currentDir).Replace('\\', '/');
            if (relativeDir == ".") relativeDir = "";

            var dirName = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(dirName)) dirName = currentDir;

            if (IsExcludedDirectory(dirName, currentDir))
            {
                log?.Invoke($"[WorkspaceFileFilter] Skipping excluded directory '{relativeDir}'");
                continue;
            }

            if (!string.IsNullOrEmpty(relativeDir))
            {
                var nestedGitIgnore = Path.Combine(currentDir, ".gitignore");
                if (File.Exists(nestedGitIgnore))
                {
                    gitignore.LoadScopedFile(nestedGitIgnore, relativeDir);
                }
                var nestedCeIgnore = Path.Combine(currentDir, ".codeexplorerignore");
                if (File.Exists(nestedCeIgnore))
                {
                    gitignore.LoadScopedFile(nestedCeIgnore, relativeDir);
                }
            }

            if (!string.IsNullOrEmpty(relativeDir) && gitignore.IsIgnored(relativeDir, true))
            {
                log?.Invoke($"[WorkspaceFileFilter] GitIgnore: Skipping ignored directory '{relativeDir}'");
                continue;
            }

            var libmanPath = Path.Combine(currentDir, "libman.json");
            if (File.Exists(libmanPath))
            {
                RegisterLibManExclusions(libmanPath, currentDir, workspaceRoot, gitignore, log);
            }

            DirectoryInfo dirInfo;
            try
            {
                dirInfo = new DirectoryInfo(currentDir);
            }
            catch
            {
                continue;
            }

            // Enqueue subdirectories
            DirectoryInfo[] subDirs;
            try
            {
                subDirs = dirInfo.GetDirectories();
            }
            catch
            {
                subDirs = [];
            }

            foreach (var subDir in subDirs)
            {
                stack.Push(subDir.FullName);
            }

            // Process files
            FileInfo[] files;
            try
            {
                files = dirInfo.GetFiles();
            }
            catch
            {
                files = [];
            }

            foreach (var fileInfo in files)
            {
                var relativeFile = Path.GetRelativePath(workspaceRoot, fileInfo.FullName).Replace('\\', '/');

                if (gitignore.IsIgnored(relativeFile, false))
                {
                    continue;
                }

                if (!IsSupportedFileType(fileInfo.Name))
                {
                    continue;
                }

                if (ShouldSkipFile(fileInfo))
                {
                    continue;
                }

                result.Add(relativeFile);
            }
        }

        return result;
    }

    /// <summary>
    /// Enumerate files recursively while strictly pruning any ExcludedDirectoryNames.
    /// </summary>
    public static IEnumerable<string> EnumerateFiles(string rootDir, string searchPattern = "*")
    {
        if (!Directory.Exists(rootDir)) yield break;

        var stack = new Stack<string>();
        stack.Push(rootDir);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            var dirName = Path.GetFileName(currentDir);
            if (!string.IsNullOrEmpty(dirName) && IsExcludedDirectory(dirName, currentDir))
            {
                continue;
            }

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch
            {
                continue;
            }

            foreach (var sd in subDirs)
            {
                stack.Push(sd);
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(currentDir, searchPattern);
            }
            catch
            {
                continue;
            }

            foreach (var f in files)
            {
                yield return f.Replace('\\', '/');
            }
        }
    }

    /// <summary>
    /// Enumerate directories recursively while strictly pruning any ExcludedDirectoryNames.
    /// </summary>
    public static IEnumerable<string> EnumerateDirectories(string rootDir)
    {
        if (!Directory.Exists(rootDir)) yield break;

        var stack = new Stack<string>();
        stack.Push(rootDir);

        while (stack.Count > 0)
        {
            var currentDir = stack.Pop();
            var dirName = Path.GetFileName(currentDir);
            if (!string.IsNullOrEmpty(dirName) && IsExcludedDirectory(dirName, currentDir))
            {
                continue;
            }

            if (currentDir != rootDir)
            {
                yield return currentDir.Replace('\\', '/');
            }

            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch
            {
                continue;
            }

            foreach (var sd in subDirs)
            {
                stack.Push(sd);
            }
        }
    }
}
