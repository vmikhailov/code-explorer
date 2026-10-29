namespace CodeExplorer.Core.Parser;

/// <summary>
/// Dynamic file and directory filter configured from connected platform-specific parsers.
/// Enforces directory exclusions, manifest identification, file filtering, and deduplication.
/// </summary>
public class WorkspaceFileFilter
{
    public const long MaxSourceFileSize = 1_048_576; // 1 MB limit for AST parsing

    /// <summary>
    /// Universal developer tooling / VCS directories that are always excluded regardless of connected parsers.
    /// </summary>
    public static IReadOnlySet<string> UniversalExcludedDirectoryNames => WorkspaceSettings.DefaultExcludedDirectories;

    private readonly HashSet<string> _excludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _manifestFileNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _manifestExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyList<IProjectParser> _projectParsers;
    private readonly IReadOnlyList<IFileParser> _fileParsers;

    public IReadOnlySet<string> ActiveExcludedDirectoryNames => _excludedDirectoryNames;
    public IReadOnlySet<string> ActiveManifestFileNames => _manifestFileNames;
    public IReadOnlySet<string> ActiveManifestExtensions => _manifestExtensions;
    public IReadOnlyList<IProjectParser> ConnectedProjectParsers => _projectParsers;
    public IReadOnlyList<IFileParser> ConnectedFileParsers => _fileParsers;
    public bool IncludeDistAndMinifiedArtifacts { get; init; }

    public WorkspaceFileFilter(
        IEnumerable<IProjectParser>? projectParsers = null,
        IEnumerable<IFileParser>? fileParsers = null,
        IEnumerable<string>? excludedDirectories = null)
    {
        var projList = (projectParsers ?? []).ToList();
        var fileList = (fileParsers ?? []).Concat(projList.OfType<IFileParser>()).Distinct().ToList();

        _projectParsers = projList;
        _fileParsers = fileList;

        var excluded = excludedDirectories ?? WorkspaceSettings.DefaultExcludedDirectories;
        foreach (var dir in excluded)
        {
            if (!string.IsNullOrWhiteSpace(dir))
            {
                _excludedDirectoryNames.Add(dir.Trim());
            }
        }

        foreach (var parser in _projectParsers)
        {
            foreach (var folder in parser.ExcludedFolders)
            {
                if (!string.IsNullOrWhiteSpace(folder))
                {
                    _excludedDirectoryNames.Add(folder.Trim());
                }
            }

            foreach (var manifest in parser.ManifestFileNames)
            {
                if (!string.IsNullOrWhiteSpace(manifest))
                {
                    _manifestFileNames.Add(manifest.Trim());
                }
            }

            foreach (var ext in parser.ManifestExtensions)
            {
                if (!string.IsNullOrWhiteSpace(ext))
                {
                    var normalized = ext.Trim();
                    if (!normalized.StartsWith('.'))
                    {
                        normalized = string.Concat('.', normalized);
                    }
                    _manifestExtensions.Add(normalized);
                }
            }
        }
    }

    public bool? CheckDirectoryExclusion(string dirName, string? fullPath = null)
    {
        foreach (var parser in _projectParsers)
        {
            var decision = parser.CheckDirectoryExclusion(dirName, fullPath);
            if (decision.HasValue)
            {
                return decision.Value;
            }
        }

        return null;
    }

    public bool IsExcludedDirectory(string dirName, string? fullPath = null)
    {
        var decision = CheckDirectoryExclusion(dirName, fullPath);
        if (decision.HasValue)
        {
            return decision.Value;
        }

        if (IncludeDistAndMinifiedArtifacts &&
            (dirName.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
             dirName.Equals("build", StringComparison.OrdinalIgnoreCase) ||
             dirName.Equals("out", StringComparison.OrdinalIgnoreCase) ||
             dirName.Equals("umd", StringComparison.OrdinalIgnoreCase) ||
             dirName.Equals("amd", StringComparison.OrdinalIgnoreCase) ||
             dirName.Equals("esm", StringComparison.OrdinalIgnoreCase) ||
             dirName.Equals("cjs", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (_excludedDirectoryNames.Contains(dirName))
        {
            return true;
        }

        return false;
    }

    public bool IsManifestFile(string fileName)
    {
        if (_manifestFileNames.Contains(fileName)) return true;
        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(ext) && _manifestExtensions.Contains(ext)) return true;

        foreach (var p in _projectParsers)
        {
            if (p.IsManifestFile(fileName)) return true;
        }

        return false;
    }

    public bool ShouldSkipFileName(string fileName)
    {
        foreach (var p in _projectParsers)
        {
            if (IncludeDistAndMinifiedArtifacts)
            {
                var fn = fileName.ToLowerInvariant();
                if (fn.EndsWith(".d.ts") || fn.EndsWith(".d.mts") || fn.EndsWith(".d.cts")) return true;
                return IProjectParser.DefaultShouldSkipFileName(fileName);
            }
            if (p.ShouldSkipFileName(fileName)) return true;
        }

        foreach (var f in _fileParsers)
        {
            if (f.ShouldSkipFileName(fileName)) return true;
        }

        return false;
    }

    public bool ShouldSkipFile(FileInfo fileInfo)
    {
        if (ShouldSkipFileName(fileInfo.Name)) return true;

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

        foreach (var p in _projectParsers)
        {
            if (p.ShouldSkipFile(fileInfo))
            {
                if (!IncludeDistAndMinifiedArtifacts) return true;
            }
        }

        foreach (var f in _fileParsers)
        {
            if (f.ShouldSkipFile(fileInfo))
            {
                if (!IncludeDistAndMinifiedArtifacts) return true;
            }
        }

        return false;
    }

    public static string ClassifyArtifactRole(string relativePath, string fileName, ReadOnlySpan<byte> contentBytes = default)
    {
        var normPath = relativePath.Replace('\\', '/');
        var lowerFile = fileName.ToLowerInvariant();
        if (lowerFile.EndsWith(".d.ts") || lowerFile.EndsWith(".d.mts") || lowerFile.EndsWith(".d.cts"))
        {
            return "declaration";
        }
        if (lowerFile.Contains(".min.") || lowerFile.Contains(".bundle.") || IsMinifiedBytes(contentBytes))
        {
            return "minified";
        }
        var padded = "/" + normPath.TrimStart('/');
        if (padded.Contains("/dist/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/build/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/out/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/umd/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/amd/", StringComparison.OrdinalIgnoreCase))
        {
            return "dist";
        }
        if (padded.Contains("/scripts/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/tools/", StringComparison.OrdinalIgnoreCase) ||
            lowerFile.EndsWith(".sh") || lowerFile.EndsWith(".ps1"))
        {
            return "script";
        }
        return "source";
    }

    public static bool IsMinifiedBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return false;
        var limit = Math.Min(bytes.Length, 8192);
        var lineLen = 0;
        var newlines = 0;
        for (var i = 0; i < limit; i++)
        {
            if (bytes[i] == (byte)'\n')
            {
                newlines++;
                lineLen = 0;
            }
            else
            {
                lineLen++;
                if (lineLen > 500) return true;
            }
        }
        if (lineLen > 500) return true;
        if (limit >= 4096 && newlines <= 3) return true;
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

    public bool IsSupportedSourceOrConfigFile(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var hasParser = _fileParsers.Any(p => p.CanParse(ext) || p.CanParseFile(fileName));
        var isConfigFile = ConfigurationIndex.IsConfigurationFile(fileName) ||
                           _projectParsers.Any(p => p.IsConfigurationFile(fileName));

        return hasParser || isConfigFile;
    }

    public bool IsSupportedFileType(string fileName)
    {
        return IsSupportedSourceOrConfigFile(fileName) || IsManifestFile(fileName);
    }

    public bool IsCandidateSourceFile(string fullPath)
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

    public List<string> EnumerateCandidateFiles(
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
            var isRoot = string.Equals(currentDir, workspaceRoot, StringComparison.OrdinalIgnoreCase);
            var relativeDir = isRoot ? string.Empty : Path.GetRelativePath(workspaceRoot, currentDir).Replace('\\', '/');

            var dirName = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(dirName)) dirName = currentDir;

            if (IsExcludedDirectory(dirName, currentDir))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(relativeDir))
            {
                gitignore.LoadScopedDirectory(currentDir, relativeDir);
            }

            if (!string.IsNullOrEmpty(relativeDir) && gitignore.IsIgnored(relativeDir, true))
            {
                continue;
            }

            foreach (var parser in _projectParsers)
            {
                parser.OnDirectoryScan(currentDir, relativeDir, gitignore, log);
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

    public IEnumerable<string> EnumerateFiles(string rootDir, string? searchPattern = null)
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
                files = searchPattern != null
                    ? Directory.GetFiles(currentDir, searchPattern)
                    : Directory.GetFiles(currentDir);
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

    public IEnumerable<string> EnumerateDirectories(string rootDir)
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

    #region Static Forwarding Fallbacks for Global / Backward-Compatible Access

    private static WorkspaceFileFilter? s_default;
    private static readonly object s_lock = new();

    public static WorkspaceFileFilter Default
    {
        get
        {
            if (s_default == null)
            {
                lock (s_lock)
                {
                    s_default ??= new WorkspaceFileFilter(
                        WorkspaceIndexer.GetAllProjectParsers(),
                        WorkspaceIndexer.GetAllFileParsers());
                }
            }
            return s_default;
        }
    }

    public static void ResetDefault()
    {
        lock (s_lock)
        {
            s_default = null;
        }
    }

    public static IReadOnlySet<string> ExcludedDirectoryNames => Default.ActiveExcludedDirectoryNames;
    public static IReadOnlySet<string> ManifestFileNames => Default.ActiveManifestFileNames;

    #endregion
}
