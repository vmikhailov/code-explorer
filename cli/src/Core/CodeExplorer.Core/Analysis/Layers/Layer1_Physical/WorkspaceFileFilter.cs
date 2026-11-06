namespace CodeExplorer.Core.Parser;

/// <summary>
/// Dynamic file and directory filter configured from connected platform-specific parsers.
/// Enforces directory exclusions, manifest identification, file filtering, and deduplication.
/// </summary>
public class WorkspaceFileFilter
{
    public const long MaxSourceFileSize = 1_048_576 * 10; // 10 MB limit for AST parsing

    /// <summary>
    /// Universal developer tooling / VCS directories that are always excluded regardless of connected parsers.
    /// </summary>
    public static IReadOnlySet<string> UniversalExcludedDirectoryNames => WorkspaceSettings.DefaultExcludedDirectories;

    private readonly HashSet<string> _universalExcludedDirectoryNames = new(StringComparer.OrdinalIgnoreCase);
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
                var trimmed = dir.Trim();
                _universalExcludedDirectoryNames.Add(trimmed);
                _excludedDirectoryNames.Add(trimmed);
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

    public IFileParser? GetFileParser(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        for (int i = 0; i < _fileParsers.Count; i++)
        {
            var fp = _fileParsers[i];
            if ((!string.IsNullOrEmpty(ext) && fp.CanParse(ext)) || fp.CanParseFile(fileName))
            {
                return fp;
            }
        }
        return null;
    }

    public IReadOnlyList<IProjectParser>? ResolveDirectoryProjectParsers(
        string currentDir,
        IReadOnlyList<FileInfo> dirFiles,
        IReadOnlyList<IProjectParser>? inheritedParsers)
    {
        List<IProjectParser>? matched = null;
        for (int p = 0; p < _projectParsers.Count; p++)
        {
            var parser = _projectParsers[p];
            for (int f = 0; f < dirFiles.Count; f++)
            {
                if (parser.IsManifestFile(dirFiles[f].Name))
                {
                    matched ??= [];
                    matched.Add(parser);
                    break;
                }
            }
        }

        if (matched is { Count: > 0 })
        {
            return matched;
        }

        return inheritedParsers;
    }

    public bool? CheckDirectoryExclusion(string dirName, string? fullPath = null, IReadOnlyList<IProjectParser>? activeParsers = null)
    {
        var parsers = activeParsers is { Count: > 0 } ? activeParsers : _projectParsers;
        for (int i = 0; i < parsers.Count; i++)
        {
            var decision = parsers[i].CheckDirectoryExclusion(dirName, fullPath);
            if (decision.HasValue)
            {
                return decision.Value;
            }
        }

        return null;
    }

    public bool IsExcludedDirectory(string dirName, string? fullPath = null, IReadOnlyList<IProjectParser>? activeParsers = null)
    {
        if (_universalExcludedDirectoryNames.Contains(dirName))
        {
            return true;
        }

        var parsers = activeParsers is { Count: > 0 } ? activeParsers : _projectParsers;

        var decision = CheckDirectoryExclusion(dirName, fullPath, parsers);
        if (decision.HasValue)
        {
            return decision.Value;
        }

        if (IncludeDistAndMinifiedArtifacts)
        {
            for (int i = 0; i < parsers.Count; i++)
            {
                if (parsers[i].IsDistOutputDirectory(dirName))
                {
                    return false;
                }
            }
        }

        if (activeParsers is { Count: > 0 })
        {
            for (int i = 0; i < activeParsers.Count; i++)
            {
                if (activeParsers[i].ExcludedFolders.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        return _excludedDirectoryNames.Contains(dirName);
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
        var fileParser = GetFileParser(fileName);
        if (fileParser != null)
        {
            if (IncludeDistAndMinifiedArtifacts)
            {
                var lower = fileName.ToLowerInvariant();
                if (!lower.Contains(".min.", StringComparison.Ordinal) &&
                    !lower.Contains(".bundle.", StringComparison.Ordinal) &&
                    fileParser.ShouldSkipFileName(fileName))
                {
                    return true;
                }
                return IProjectParser.DefaultShouldSkipFileName(fileName);
            }
            return fileParser.ShouldSkipFileName(fileName);
        }

        return IProjectParser.DefaultShouldSkipFileName(fileName);
    }

    public bool ShouldSkipFileMetadata(FileInfo fileInfo)
    {
        if (ShouldSkipFileName(fileInfo.Name)) return true;

        try
        {
            var maxAllowedSize = IncludeDistAndMinifiedArtifacts ? 2L * 1024 * 1024 : MaxSourceFileSize;
            if (fileInfo.Length > maxAllowedSize)
            {
                return true;
            }
        }
        catch
        {
            // Ignore file access errors
        }

        return false;
    }

    public bool ShouldSkipFileBytes(string fileName, ReadOnlySpan<byte> contentBytes)
    {
        if (IncludeDistAndMinifiedArtifacts) return false;

        var fileParser = GetFileParser(fileName);
        return fileParser != null && fileParser.ShouldSkipFileBytes(fileName, contentBytes);
    }

    public bool ShouldSkipFile(FileInfo fileInfo)
    {
        if (ShouldSkipFileMetadata(fileInfo)) return true;
        if (IncludeDistAndMinifiedArtifacts) return false;

        var fileParser = GetFileParser(fileInfo.Name);
        return fileParser != null && fileParser.ShouldSkipFile(fileInfo);
    }

    public string ClassifyFileArtifactRole(string relativePath, string fileName, ReadOnlySpan<byte> contentBytes = default)
    {
        var fileParser = GetFileParser(fileName);
        if (fileParser != null)
        {
            var role = fileParser.ClassifyArtifactRole(relativePath, fileName, contentBytes);
            if (!string.Equals(role, "source", StringComparison.Ordinal))
            {
                return role;
            }
        }

        var normPath = relativePath.NormalizeSlashes();
        var padded = "/" + normPath.TrimStart('/');
        if (padded.Contains("/dist/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/build/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/out/", StringComparison.OrdinalIgnoreCase))
        {
            return "dist";
        }
        if (padded.Contains("/scripts/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/tools/", StringComparison.OrdinalIgnoreCase))
        {
            return "script";
        }
        return "source";
    }

    public static string ClassifyArtifactRole(string relativePath, string fileName, ReadOnlySpan<byte> contentBytes = default) =>
        Default.ClassifyFileArtifactRole(relativePath, fileName, contentBytes);

    public static bool IsMinifiedBytes(ReadOnlySpan<byte> bytes, int maxLineLength = 5000)
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
                if (lineLen > maxLineLength) return true;
            }
        }
        if (lineLen > maxLineLength) return true;
        if (limit >= 4096 && newlines <= 3) return true;
        return false;
    }

    public static bool IsMinifiedText(ReadOnlySpan<char> text, int maxLineLength = 5000)
    {
        if (text.IsEmpty) return false;
        var limit = Math.Min(text.Length, 8192);
        var lineLen = 0;
        var newlines = 0;
        for (var i = 0; i < limit; i++)
        {
            if (text[i] == '\n')
            {
                newlines++;
                lineLen = 0;
            }
            else
            {
                lineLen++;
                if (lineLen > maxLineLength) return true;
            }
        }
        if (lineLen > maxLineLength) return true;
        if (limit >= 4096 && newlines <= 3) return true;
        return false;
    }

    public static bool IsMinifiedContent(string filePath)
    {
        if (PhysicalFileStore.TryGetFileText(filePath, out var cachedText))
        {
            return !string.IsNullOrEmpty(cachedText) && IsMinifiedText(cachedText.AsSpan(), maxLineLength: 1000);
        }

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            var buffer = new char[8192];
            var read = reader.Read(buffer, 0, buffer.Length);
            return read > 0 && IsMinifiedText(buffer.AsSpan(0, read), maxLineLength: 1000);
        }
        catch
        {
            return false;
        }
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
        var normalized = fullPath.NormalizeSlashes();
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

        var stack = new Stack<(string Dir, IReadOnlyList<IProjectParser>? ActiveParsers)>();
        stack.Push((targetPath, null));

        while (stack.Count > 0)
        {
            var (currentDir, inheritedParsers) = stack.Pop();
            var isRoot = string.Equals(currentDir, workspaceRoot, StringComparison.OrdinalIgnoreCase);
            var relativeDir = isRoot ? string.Empty : currentDir.GetNormalizedRelativePath(workspaceRoot);

            var dirName = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(dirName)) dirName = currentDir;

            if (IsExcludedDirectory(dirName, currentDir, inheritedParsers))
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

            DirectoryInfo dirInfo;
            try
            {
                dirInfo = new DirectoryInfo(currentDir);
            }
            catch
            {
                continue;
            }

            FileInfo[] files;
            try
            {
                files = dirInfo.GetFiles();
            }
            catch
            {
                files = [];
            }

            var activeParsers = ResolveDirectoryProjectParsers(currentDir, files, inheritedParsers);
            var parsersToNotify = activeParsers is { Count: > 0 } ? activeParsers : _projectParsers;
            for (int i = 0; i < parsersToNotify.Count; i++)
            {
                parsersToNotify[i].OnDirectoryScan(currentDir, relativeDir, gitignore, log);
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
                stack.Push((subDir.FullName, activeParsers));
            }

            // Process files
            foreach (var fileInfo in files)
            {
                var relativeFile = fileInfo.FullName.GetNormalizedRelativePath(workspaceRoot);

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
                yield return f.NormalizeSlashes();
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
                yield return currentDir.NormalizeSlashes();
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
