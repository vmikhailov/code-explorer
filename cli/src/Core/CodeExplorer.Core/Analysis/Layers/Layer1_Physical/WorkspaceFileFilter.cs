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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> _supportedExtensionCache =
        new(StringComparer.OrdinalIgnoreCase);

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
        var fileList = (fileParsers ?? []).Distinct().ToList();

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
        IReadOnlyList<IProjectParser>? inheritedParsers,
        string[]? dirFilePaths = null)
    {
        var paths = dirFilePaths ?? BuildFilePaths(dirFiles);
        var matched = MatchDirectoryProjectParsers(currentDir, dirFiles, paths, includeFallback: false);
        if (matched is { Count: > 0 })
        {
            return matched;
        }

        if (inheritedParsers is { Count: > 0 })
        {
            return inheritedParsers;
        }

        return MatchDirectoryProjectParsers(currentDir, dirFiles, paths, includeFallback: true);
    }

    public IReadOnlyList<IProjectParser>? MatchDirectProjectParsers(
        string currentDir,
        IReadOnlyList<FileInfo> dirFiles,
        string[] dirFilePaths,
        bool allowFallback)
    {
        var matched = MatchDirectoryProjectParsers(currentDir, dirFiles, dirFilePaths, includeFallback: false);
        if (matched is { Count: > 0 } || !allowFallback)
        {
            return matched;
        }

        return MatchDirectoryProjectParsers(currentDir, dirFiles, dirFilePaths, includeFallback: true);
    }

    private static readonly HashSet<string> NonManifestProjectTriggerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cfc", ".cfm", ".cfml",
        ".php", ".phtml", ".inc",
        ".pl", ".pm", ".t", ".psgi",
        ".py"
    };

    private List<IProjectParser>? MatchDirectoryProjectParsers(
        string currentDir,
        IReadOnlyList<FileInfo> dirFiles,
        string[] dirFilePaths,
        bool includeFallback)
    {
        var hasCachedChildDirs = PhysicalFileStore.HasCachedDirectChildDirectories(currentDir);
        if (!includeFallback && dirFiles.Count == 0 && !hasCachedChildDirs)
        {
            var parentDir = Path.GetDirectoryName(currentDir.TrimEnd('/', '\\'));
            var parentName = parentDir != null ? Path.GetFileName(parentDir) : string.Empty;
            if (!parentName.Equals("bin", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        if (!includeFallback && dirFiles.Count > 0 && !hasCachedChildDirs)
        {
            var hasTrigger = false;
            for (int f = 0; f < dirFiles.Count; f++)
            {
                var fn = dirFiles[f].Name;
                if (IsManifestFile(fn) || NonManifestProjectTriggerExtensions.Contains(dirFiles[f].Extension))
                {
                    hasTrigger = true;
                    break;
                }
            }
            if (!hasTrigger)
            {
                var parentDir = Path.GetDirectoryName(currentDir.TrimEnd('/', '\\'));
                var parentName = parentDir != null ? Path.GetFileName(parentDir) : string.Empty;
                if (!parentName.Equals("bin", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
        }

        List<IProjectParser>? matched = null;
        for (int p = 0; p < _projectParsers.Count; p++)
        {
            var parser = _projectParsers[p];
            if (parser.IsFallback != includeFallback)
            {
                continue;
            }

            var hasManifest = false;
            for (int f = 0; f < dirFiles.Count; f++)
            {
                if (parser.IsManifestFile(dirFiles[f].Name))
                {
                    hasManifest = true;
                    break;
                }
            }

            if (hasManifest || parser.IsProjectDirectory(currentDir, dirFilePaths))
            {
                matched ??= [];
                matched.Add(parser);
            }
        }

        return matched;
    }

    public IReadOnlyList<IProjectParser>? MatchFileSupportedProjectParsers(
        IReadOnlyList<FileInfo> dirFiles,
        bool includeFallback)
    {
        if (dirFiles.Count == 0)
        {
            return null;
        }

        List<IProjectParser>? matched = null;
        for (int p = 0; p < _projectParsers.Count; p++)
        {
            var parser = _projectParsers[p];
            if (parser.IsFallback != includeFallback)
            {
                continue;
            }

            for (int f = 0; f < dirFiles.Count; f++)
            {
                var fn = dirFiles[f].Name;
                var ext = dirFiles[f].Extension;
                if (parser.IsManifestFile(fn))
                {
                    matched ??= [];
                    matched.Add(parser);
                    break;
                }

                var isNonManifestMatch = parser.ProjectType switch
                {
                    "php" => ext.Equals(".php", StringComparison.OrdinalIgnoreCase) ||
                             ext.Equals(".phtml", StringComparison.OrdinalIgnoreCase) ||
                             ext.Equals(".inc", StringComparison.OrdinalIgnoreCase),
                    "perl" => ext.Equals(".pl", StringComparison.OrdinalIgnoreCase) ||
                              ext.Equals(".pm", StringComparison.OrdinalIgnoreCase) ||
                              ext.Equals(".t", StringComparison.OrdinalIgnoreCase) ||
                              ext.Equals(".psgi", StringComparison.OrdinalIgnoreCase),
                    "python" => ext.Equals(".py", StringComparison.OrdinalIgnoreCase),
                    "coldfusion" => ext.Equals(".cfc", StringComparison.OrdinalIgnoreCase) ||
                                    ext.Equals(".cfm", StringComparison.OrdinalIgnoreCase) ||
                                    ext.Equals(".cfml", StringComparison.OrdinalIgnoreCase),
                    "script" => ext.Equals(".sh", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".bash", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".zsh", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase),
                    _ => false
                };

                if (isNonManifestMatch)
                {
                    matched ??= [];
                    matched.Add(parser);
                    break;
                }
            }
        }

        return matched;
    }

    private static string[] BuildFilePaths(IReadOnlyList<FileInfo> dirFiles)
    {
        if (dirFiles.Count == 0) return [];
        var paths = new string[dirFiles.Count];
        for (int i = 0; i < dirFiles.Count; i++)
        {
            paths[i] = dirFiles[i].FullName;
        }
        return paths;
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
                return IFileParser.DefaultShouldSkipFileName(fileName);
            }
            return fileParser.ShouldSkipFileName(fileName);
        }

        return IFileParser.DefaultShouldSkipFileName(fileName);
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
        if (IsVendorArtifactPath(padded, fileName))
        {
            return "vendor";
        }
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

    private static readonly string[] KnownVendoredJsLibraries =
    [
        "select2", "slick", "bootstrap", "jquery", "tinymce", "ckeditor", "datatables",
        "moment", "lodash", "underscore", "handlebars", "mustache", "chart.js", "chartjs",
        "fullcalendar", "dropzone", "clipboard", "sweetalert", "toastr", "magnific-popup",
        "owl.carousel", "swiper", "fancybox", "lightslider", "codemirror", "prettify",
        "highlight.js", "jszip", "pdfmake", "vfs_fonts", "popper", "modernizr", "respond.js", "html5shiv"
    ];

    private static bool IsVendorArtifactPath(string paddedPath, string fileName)
    {
        if (paddedPath.Contains("/vendor/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/vendors/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/3rdparty/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/thirdparty/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/third_party/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/third-party/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/bower_components/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/js/vendor/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/js/vendors/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/js/plugins/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/assets/vendor/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/assets/plugins/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var isJsOrCss = fileName.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                        fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
        if (!isJsOrCss)
        {
            return false;
        }

        for (var i = 0; i < KnownVendoredJsLibraries.Length; i++)
        {
            var lib = KnownVendoredJsLibraries[i];
            if (paddedPath.Contains($"/{lib}/", StringComparison.OrdinalIgnoreCase) ||
                paddedPath.Contains($"/{lib}-", StringComparison.OrdinalIgnoreCase) ||
                paddedPath.Contains($"/{lib}.", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith($"{lib}.", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith($"{lib}-", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals($"{lib}.js", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (paddedPath.Contains("/assets/js/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/public/js/", StringComparison.OrdinalIgnoreCase) ||
            paddedPath.Contains("/static/js/", StringComparison.OrdinalIgnoreCase))
        {
            var idx = paddedPath.IndexOf("/js/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var afterJs = paddedPath[(idx + 4)..];
                if (afterJs.Contains('/'))
                {
                    var subFolder = afterJs[..afterJs.IndexOf('/')];
                    if (!subFolder.Equals("app", StringComparison.OrdinalIgnoreCase) &&
                        !subFolder.Equals("src", StringComparison.OrdinalIgnoreCase) &&
                        !subFolder.Equals("modules", StringComparison.OrdinalIgnoreCase) &&
                        !subFolder.Equals("pages", StringComparison.OrdinalIgnoreCase) &&
                        !subFolder.Equals("components", StringComparison.OrdinalIgnoreCase) &&
                        !subFolder.Equals("controllers", StringComparison.OrdinalIgnoreCase) &&
                        !subFolder.Equals("custom", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static string ClassifyArtifactRole(string relativePath, string fileName, ReadOnlySpan<byte> contentBytes = default) =>
        Default.ClassifyFileArtifactRole(relativePath, fileName, contentBytes);

    private const string BrowserifyDereqSignature = "function(_dereq_,module,exports)";
    private const string WebpackRequireSignature = "__webpack_require__";
    private const string WebpackUmdSignature = "webpackUniversalModuleDefinition";
    private const string MinifiedUmdSignature1 = "typeof exports===\"object\"&&typeof module!==\"undefined\"";
    private const string MinifiedUmdSignature2 = "\"object\"===typeof exports&&\"object\"===typeof module";

    private static ReadOnlySpan<byte> BrowserifyDereqBytes => "function(_dereq_,module,exports)"u8;
    private static ReadOnlySpan<byte> WebpackRequireBytes => "__webpack_require__"u8;
    private static ReadOnlySpan<byte> WebpackUmdBytes => "webpackUniversalModuleDefinition"u8;
    private static ReadOnlySpan<byte> MinifiedUmdBytes1 => "typeof exports===\"object\"&&typeof module!==\"undefined\""u8;
    private static ReadOnlySpan<byte> MinifiedUmdBytes2 => "\"object\"===typeof exports&&\"object\"===typeof module"u8;

    public static bool IsMinifiedBytes(ReadOnlySpan<byte> bytes, int maxLineLength = 5000)
    {
        if (bytes.IsEmpty) return false;
        var limit = Math.Min(bytes.Length, 8192);
        var lineLen = 0;
        var newlines = 0;
        for (var i = 0; i < limit; i++)
        {
            if (bytes[i] is (byte)'\n' or (byte)'\r')
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

        var head = bytes[..limit];
        if (head.IndexOf(BrowserifyDereqBytes) >= 0 ||
            head.IndexOf(WebpackRequireBytes) >= 0 ||
            head.IndexOf(WebpackUmdBytes) >= 0 ||
            head.IndexOf(MinifiedUmdBytes1) >= 0 ||
            head.IndexOf(MinifiedUmdBytes2) >= 0)
        {
            return true;
        }

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
            if (text[i] is '\n' or '\r')
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

        var head = text[..limit];
        if (head.IndexOf(BrowserifyDereqSignature, StringComparison.Ordinal) >= 0 ||
            head.IndexOf(WebpackRequireSignature, StringComparison.Ordinal) >= 0 ||
            head.IndexOf(WebpackUmdSignature, StringComparison.Ordinal) >= 0 ||
            head.IndexOf(MinifiedUmdSignature1, StringComparison.Ordinal) >= 0 ||
            head.IndexOf(MinifiedUmdSignature2, StringComparison.Ordinal) >= 0)
        {
            return true;
        }

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

    public bool IsSupportedSourceOrConfigFile(string fileName, IReadOnlyList<IProjectParser>? activeParsers = null)
    {
        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrEmpty(ext))
        {
            var extSupported = _supportedExtensionCache.GetOrAdd(ext, e =>
            {
                var lowerExt = e.ToLowerInvariant();
                for (var i = 0; i < _fileParsers.Count; i++)
                {
                    if (_fileParsers[i].CanParse(lowerExt))
                    {
                        return true;
                    }
                }
                return false;
            });
            if (extSupported)
            {
                return true;
            }
        }

        var baseName = Path.GetFileName(fileName);
        for (var i = 0; i < _fileParsers.Count; i++)
        {
            if (_fileParsers[i].CanParseFile(fileName) || _fileParsers[i].CanParseFile(baseName))
            {
                return true;
            }
        }

        if (SharedFileParserRegistry.CanParse(baseName))
        {
            return true;
        }

        var parsersToCheck = activeParsers ?? _projectParsers;
        for (var i = 0; i < parsersToCheck.Count; i++)
        {
            if (parsersToCheck[i].IsConfigurationFile(baseName))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsStandaloneContractOrEnvFile(string filePathOrName, bool isWorkspaceRootFile)
    {
        var baseName = Path.GetFileName(filePathOrName);
        if (isWorkspaceRootFile && SharedFileParserRegistry.CanParse(baseName))
        {
            return true;
        }

        var ext = Path.GetExtension(baseName).ToLowerInvariant();
        if (ext is ".sql" or ".graphql" or ".gql")
        {
            return true;
        }

        var lowerName = baseName.ToLowerInvariant();
        return lowerName is "openapi.yaml" or "openapi.yml" or "openapi.json"
            or "swagger.yaml" or "swagger.yml" or "swagger.json";
    }

    public bool IsSupportedFileType(string fileName, IReadOnlyList<IProjectParser>? activeParsers = null)
    {
        var baseName = Path.GetFileName(fileName);
        return IsSupportedSourceOrConfigFile(fileName, activeParsers) || IsManifestFile(baseName);
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

        return IsSupportedFileType(fullPath) || IsSupportedFileType(fileName);
    }

    public List<string> EnumerateCandidateFiles(
        string targetPath,
        string workspaceRoot,
        GitIgnoreMatcher gitignore,
        Action<string>? log = null)
    {
        var result = new List<string>();
        if (!Directory.Exists(targetPath)) return result;

        var initialParsers = ResolveAncestorProjectParsers(targetPath, workspaceRoot);
        var discoveredDirs = new List<(string RelativeDir, bool IsRoot, FileInfo[] Files, IReadOnlyList<IProjectParser>? ActiveParsers)>();
        var stack = new Stack<(string Dir, IReadOnlyList<IProjectParser>? ActiveParsers)>();
        stack.Push((targetPath, initialParsers));

        while (stack.Count > 0)
        {
            var (currentDir, inheritedParsers) = stack.Pop();
            var isRoot = string.Equals(currentDir, workspaceRoot, StringComparison.OrdinalIgnoreCase);
            var relativeDir = isRoot ? string.Empty : currentDir.GetNormalizedRelativePath(workspaceRoot);

            var dirName = Path.GetFileName(currentDir);
            if (string.IsNullOrEmpty(dirName)) dirName = currentDir;

            if (!isRoot && IsExcludedDirectory(dirName, currentDir, inheritedParsers))
            {
                continue;
            }

            if (!isRoot && GitSettingsParser.IsGitWorktreeDirectory(currentDir))
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

            discoveredDirs.Add((relativeDir, isRoot, files, activeParsers));

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
        }

        var hasAnyOwnedDirectory = discoveredDirs.Any(d => d.ActiveParsers is { Count: > 0 });

        foreach (var (_, isRoot, files, activeParsers) in discoveredDirs)
        {
            foreach (var fileInfo in files)
            {
                var relativeFile = fileInfo.FullName.GetNormalizedRelativePath(workspaceRoot);

                if (gitignore.IsIgnored(relativeFile, false))
                {
                    continue;
                }

                var isManifest = IsManifestFile(fileInfo.Name);
                if (hasAnyOwnedDirectory && activeParsers is not { Count: > 0 })
                {
                    if (!isManifest && !IsStandaloneContractOrEnvFile(relativeFile, isRoot))
                    {
                        continue;
                    }
                }
                else if (!isManifest && !IsSupportedSourceOrConfigFile(relativeFile, activeParsers))
                {
                    continue;
                }

                if (!isManifest && ShouldSkipFile(fileInfo))
                {
                    continue;
                }

                result.Add(relativeFile);
            }
        }

        return result;
    }

    public IReadOnlyList<IProjectParser>? ResolveAncestorProjectParsers(string targetPath, string workspaceRoot)
    {
        var normTarget = Path.GetFullPath(targetPath).NormalizeSlashes().TrimEnd('/');
        var normRoot = Path.GetFullPath(workspaceRoot).NormalizeSlashes().TrimEnd('/');
        if (string.Equals(normTarget, normRoot, StringComparison.OrdinalIgnoreCase) ||
            !normTarget.StartWithIC(normRoot + "/"))
        {
            return null;
        }

        var rel = normTarget[(normRoot.Length + 1)..];
        var segments = rel.Split('/', StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyList<IProjectParser>? active = TryProbeDirectoryParsers(workspaceRoot, null);
        var cur = workspaceRoot;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            cur = Path.Combine(cur, segments[i]);
            active = TryProbeDirectoryParsers(cur, active);
        }
        return active;
    }

    private IReadOnlyList<IProjectParser>? TryProbeDirectoryParsers(string dirPath, IReadOnlyList<IProjectParser>? inherited)
    {
        try
        {
            var files = new DirectoryInfo(dirPath).GetFiles();
            return ResolveDirectoryProjectParsers(dirPath, files, inherited);
        }
        catch
        {
            return inherited;
        }
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
                        ParserRegistry.GetAllProjectParsers(),
                        ParserRegistry.GetAllFileParsers());
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
