using System.Collections.Concurrent;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Thread-safe in-memory store populated by Layer 1 (<c>Layer1PhysicalParser</c>)
/// during the single physical directory and file scan. Ensures that once a file or directory listing
/// is read in Layer 1, downstream layers (Layer 2 and Layer 3) consume the in-memory content
/// without returning to disk.
/// </summary>
public static class PhysicalFileStore
{
    private static readonly ConcurrentDictionary<string, string> _fileContents =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, string[]> _directoryFiles =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, byte> _gitRepositoryRoots =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, WorkspaceProjectSetting> _configuredProjects =
        new(StringComparer.OrdinalIgnoreCase);

    public static string NormalizePath(string path) => path.NormalizeFullPath(trimTrailingSlash: true);

    public static string DecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return string.Empty;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes[3..]);
        }
        return Encoding.UTF8.GetString(bytes);
    }

    public readonly record struct StreamedFileResult(
        bool SkipByContent,
        string? ContentHash,
        int? LineCount,
        string? ArtifactRole,
        string? DecodedText);

    /// <summary>
    /// Streams a file from disk in a single pass using pooled buffers (<see cref="ArrayPool{T}"/>),
    /// computing SHA-256, line count, minification/artifact role on the first 8 KB, and decoding UTF-8 text
    /// without allocating full-file byte[] buffers on the heap or LOH.
    /// </summary>
    public static async ValueTask<StreamedFileResult> StreamAndAnalyzeFileAsync(
        string fullPath,
        string relativeFile,
        string fileName,
        long sizeBytes,
        bool isManifest,
        bool isSupportedSourceOrConfig,
        WorkspaceFileFilter fileFilter,
        CancellationToken cancellationToken = default,
        bool computeHash = true)
    {
        const int ChunkSize = 16384;
        var byteBuf = ArrayPool<byte>.Shared.Rent(ChunkSize);
        char[]? charBuf = null;
        try
        {
            await using var stream = new FileStream(
                fullPath,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.ReadWrite,
                    Options = FileOptions.SequentialScan | FileOptions.Asynchronous,
                    BufferSize = 1
                });

            // Fill first chunk (up to 16 KB or EOF) so minification heuristics have at least 8 KB when available
            var firstLen = 0;
            while (firstLen < ChunkSize)
            {
                var r = await stream.ReadAsync(byteBuf.AsMemory(firstLen, ChunkSize - firstLen), cancellationToken);
                if (r == 0) break;
                firstLen += r;
            }

            var firstSpan = byteBuf.AsSpan(0, firstLen);
            var skipByContent = false;
            if (isSupportedSourceOrConfig && fileFilter.ShouldSkipFileBytes(fileName, firstSpan))
            {
                skipByContent = true;
                if (!isManifest)
                {
                    return new StreamedFileResult(true, null, null, null, null);
                }
            }

            var artifactRole = isSupportedSourceOrConfig && !skipByContent
                ? fileFilter.ClassifyFileArtifactRole(relativeFile, fileName, firstSpan)
                : null;

            using var sha = computeHash ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
            if (sha != null)
            {
                Incremental.HashUtility.AppendNormalizedData(sha, firstSpan);
            }

            var newlines = 0;
            byte lastByte = 0;
            var hasBytes = firstLen > 0;
            if (hasBytes)
            {
                newlines += firstSpan.Count((byte)'\n');
                lastByte = firstSpan[^1];
            }

            var shouldDecodeText = !skipByContent && sizeBytes <= 2 * 1024 * 1024;
            Decoder? decoder = null;
            var charCount = 0;
            if (shouldDecodeText)
            {
                if (firstLen == 0)
                {
                    var emptyHash = sha != null ? Incremental.HashUtility.FinalizeHash(sha) : null;
                    SetFileText(fullPath, string.Empty);
                    return new StreamedFileResult(skipByContent, emptyHash, 0, artifactRole, string.Empty);
                }

                var initialCapacity = (int)Math.Clamp(sizeBytes, firstLen, 2 * 1024 * 1024) + 32;
                charBuf = ArrayPool<char>.Shared.Rent(initialCapacity);
                decoder = Encoding.UTF8.GetDecoder();

                var decodeSpan = firstSpan;
                if (decodeSpan.Length >= 3 && decodeSpan[0] == 0xEF && decodeSpan[1] == 0xBB && decodeSpan[2] == 0xBF)
                {
                    decodeSpan = decodeSpan[3..];
                }
                charCount += DecodeChunkToPool(decoder, decodeSpan, flush: false, ref charBuf, charCount);
            }

            while (true)
            {
                var read = await stream.ReadAsync(byteBuf.AsMemory(0, ChunkSize), cancellationToken);
                if (read == 0) break;

                var chunkSpan = byteBuf.AsSpan(0, read);
                if (sha != null)
                {
                    Incremental.HashUtility.AppendNormalizedData(sha, chunkSpan);
                }
                newlines += chunkSpan.Count((byte)'\n');
                lastByte = chunkSpan[^1];
                hasBytes = true;

                if (decoder != null && charBuf != null)
                {
                    charCount += DecodeChunkToPool(decoder, chunkSpan, flush: false, ref charBuf, charCount);
                }
            }

            string? decodedText = null;
            if (decoder != null && charBuf != null)
            {
                charCount += DecodeChunkToPool(decoder, ReadOnlySpan<byte>.Empty, flush: true, ref charBuf, charCount);
                for (var i = 0; i < charCount; i++)
                {
                    if (charBuf[i] == '\r' && (i + 1 >= charCount || charBuf[i + 1] != '\n'))
                    {
                        charBuf[i] = '\n';
                        newlines++;
                        if (i == charCount - 1)
                        {
                            lastByte = (byte)'\n';
                        }
                    }
                }
                decodedText = new string(charBuf, 0, charCount);
                SetFileText(fullPath, decodedText);
            }

            var contentHash = sha != null ? Incremental.HashUtility.FinalizeHash(sha) : null;
            int? lineCount = isSupportedSourceOrConfig && !skipByContent
                ? (!hasBytes ? 0 : (lastByte == (byte)'\n' ? newlines : newlines + 1))
                : null;

            return new StreamedFileResult(skipByContent, contentHash, lineCount, artifactRole, decodedText);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(byteBuf);
            if (charBuf != null)
            {
                ArrayPool<char>.Shared.Return(charBuf);
            }
        }
    }

    private static int DecodeChunkToPool(
        Decoder decoder,
        ReadOnlySpan<byte> bytes,
        bool flush,
        ref char[] charBuf,
        int currentOffset)
    {
        var maxChars = Encoding.UTF8.GetMaxCharCount(bytes.Length) + 4;
        if (currentOffset + maxChars > charBuf.Length)
        {
            var newSize = Math.Max(charBuf.Length * 2, currentOffset + maxChars);
            var nextBuf = ArrayPool<char>.Shared.Rent(newSize);
            charBuf.AsSpan(0, currentOffset).CopyTo(nextBuf);
            ArrayPool<char>.Shared.Return(charBuf);
            charBuf = nextBuf;
        }

        return decoder.GetChars(bytes, charBuf.AsSpan(currentOffset), flush);
    }

    public static void SetFileText(string fullPath, string content)
    {
        var key = NormalizePath(fullPath);
        if (!string.IsNullOrEmpty(key))
        {
            _fileContents[key] = content;
        }
    }

    public static bool TryGetFileText(string fullPath, out string content)
    {
        var key = NormalizePath(fullPath);
        if (!string.IsNullOrEmpty(key) && _fileContents.TryGetValue(key, out var found))
        {
            content = found;
            return true;
        }
        content = string.Empty;
        return false;
    }

    public static void EvictFileText(string fullPath)
    {
        var key = NormalizePath(fullPath);
        if (!string.IsNullOrEmpty(key))
        {
            _fileContents.TryRemove(key, out _);
        }
    }

    public static void RemoveCachedFile(string fullPath)
    {
        var key = NormalizePath(fullPath);
        if (string.IsNullOrEmpty(key)) return;
        _fileContents.TryRemove(key, out _);

        var lastSlash = key.LastIndexOf('/');
        if (lastSlash > 0)
        {
            var parentKey = key[..lastSlash];
            if (_directoryFiles.TryGetValue(parentKey, out var dirFiles) && dirFiles.Length > 0)
            {
                var targetName = key[(lastSlash + 1)..];
                var updated = dirFiles
                    .Where(f => !Path.GetFileName(f).Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (updated.Length != dirFiles.Length)
                {
                    _directoryFiles[parentKey] = updated;
                }
            }
        }
    }

    public static bool FileExistsInCachedDirectory(string fullPath)
    {
        var key = NormalizePath(fullPath);
        if (string.IsNullOrEmpty(key)) return false;
        if (_fileContents.ContainsKey(key)) return true;

        var lastSlash = key.LastIndexOf('/');
        if (lastSlash > 0)
        {
            var parentKey = key[..lastSlash];
            if (_directoryFiles.TryGetValue(parentKey, out var dirFiles))
            {
                var targetName = key[(lastSlash + 1)..];
                for (var i = 0; i < dirFiles.Length; i++)
                {
                    if (Path.GetFileName(dirFiles[i]).Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        return File.Exists(fullPath);
    }

    public static string ReadAllText(string fullPath)
    {
        var key = NormalizePath(fullPath);
        if (!string.IsNullOrEmpty(key))
        {
            if (_fileContents.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var lastSlash = key.LastIndexOf('/');
            if (lastSlash > 0 && _directoryFiles.TryGetValue(key[..lastSlash], out var dirFiles))
            {
                var targetName = key[(lastSlash + 1)..];
                var existsInDir = false;
                for (var i = 0; i < dirFiles.Length; i++)
                {
                    if (Path.GetFileName(dirFiles[i]).Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        existsInDir = true;
                        break;
                    }
                }
                if (!existsInDir)
                {
                    return string.Empty;
                }
            }
        }

        if (!File.Exists(fullPath)) return string.Empty;
        var text = File.ReadAllText(fullPath);
        return text;
    }

    public static async ValueTask<string> ReadAllTextAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var key = NormalizePath(fullPath);
        if (!string.IsNullOrEmpty(key))
        {
            if (_fileContents.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var lastSlash = key.LastIndexOf('/');
            if (lastSlash > 0 && _directoryFiles.TryGetValue(key[..lastSlash], out var dirFiles))
            {
                var targetName = key[(lastSlash + 1)..];
                var existsInDir = false;
                for (var i = 0; i < dirFiles.Length; i++)
                {
                    if (Path.GetFileName(dirFiles[i]).Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        existsInDir = true;
                        break;
                    }
                }
                if (!existsInDir)
                {
                    return string.Empty;
                }
            }
        }

        if (!File.Exists(fullPath)) return string.Empty;
        var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
        return text;
    }

    /// <summary>
    /// Retrieves and removes the preloaded file text from the store so the raw string buffer
    /// is not retained in duplicate once the Layer 3 SyntaxTree is constructed.
    /// </summary>
    public static async ValueTask<string> ConsumeFileTextAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var key = NormalizePath(fullPath);
        if (!string.IsNullOrEmpty(key) && _fileContents.TryRemove(key, out var cached))
        {
            return cached;
        }

        if (!File.Exists(fullPath)) return string.Empty;
        try
        {
            return await File.ReadAllTextAsync(fullPath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    public static IEnumerable<string> ReadLines(string fullPath)
    {
        var text = ReadAllText(fullPath);
        if (string.IsNullOrEmpty(text)) return [];
        return text.Split(["\r\n", "\n"], StringSplitOptions.None);
    }

    public static void SetDirectoryFiles(string directoryPath, string[] files)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key))
        {
            _directoryFiles[key] = files;
        }
    }

    public static bool TryGetCachedDirectoryFiles(string directoryPath, out string[] files)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key) && _directoryFiles.TryGetValue(key, out var cached))
        {
            files = cached;
            return true;
        }
        files = [];
        return false;
    }

    public static bool HasCachedDirectory(string directoryPath)
    {
        var key = NormalizePath(directoryPath);
        return !string.IsNullOrEmpty(key) && _directoryFiles.ContainsKey(key);
    }

    public static List<(string DirectoryPath, string[] Files)> GetCachedDirectChildDirectories(string parentDirectoryPath)
    {
        var parentKey = NormalizePath(parentDirectoryPath);
        if (string.IsNullOrEmpty(parentKey)) return [];

        var prefix = parentKey + "/";
        var result = new List<(string DirectoryPath, string[] Files)>();
        foreach (var (k, arr) in _directoryFiles)
        {
            if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var remainder = k[prefix.Length..];
                if (!remainder.Contains('/'))
                {
                    result.Add((k, arr));
                }
            }
        }
        return result;
    }

    public static IEnumerable<string> EnumerateCachedFilesInSubtree(string rootDirectoryPath)
    {
        var rootKey = NormalizePath(rootDirectoryPath);
        if (string.IsNullOrEmpty(rootKey)) yield break;

        if (_directoryFiles.TryGetValue(rootKey, out var rootFiles))
        {
            foreach (var f in rootFiles) yield return f;
        }

        var prefix = rootKey + "/";
        foreach (var (k, arr) in _directoryFiles)
        {
            if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var f in arr) yield return f;
            }
        }
    }

    public static string[] GetDirectoryFiles(string directoryPath)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key) && _directoryFiles.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!Directory.Exists(directoryPath)) return [];
        try
        {
            var files = Directory.GetFiles(directoryPath);
            if (!string.IsNullOrEmpty(key))
            {
                // Do not cache unmanaged standalone fallback directory reads outside Layer 1
            }
            return files;
        }
        catch
        {
            return [];
        }
    }

    public static void RegisterGitRepositoryRoot(string directoryPath)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key))
        {
            _gitRepositoryRoots[key] = 1;
        }
    }

    public static bool IsGitRepositoryRoot(string directoryPath)
    {
        var key = NormalizePath(directoryPath);
        return !string.IsNullOrEmpty(key) && _gitRepositoryRoots.ContainsKey(key);
    }

    public static bool HasChildGitRepositories(string directoryPath)
    {
        var parentKey = NormalizePath(directoryPath);
        if (string.IsNullOrEmpty(parentKey)) return false;

        var prefix = parentKey + "/";
        foreach (var k in _gitRepositoryRoots.Keys)
        {
            if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static void RegisterConfiguredProjects(string workspaceRootPath, IReadOnlyList<WorkspaceProjectSetting>? projects)
    {
        if (projects is not { Count: > 0 }) return;
        foreach (var p in projects)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.Path)) continue;
            var absPath = Path.Combine(workspaceRootPath, p.Path);
            var key = NormalizePath(absPath);
            if (!string.IsNullOrEmpty(key))
            {
                _configuredProjects[key] = p;
            }
        }
    }

    public static bool TryGetConfiguredProject(string directoryPath, out WorkspaceProjectSetting? setting)
    {
        var key = NormalizePath(directoryPath);
        if (!string.IsNullOrEmpty(key) && _configuredProjects.TryGetValue(key, out setting))
        {
            return true;
        }
        setting = null;
        return false;
    }

    public static bool HasChildConfiguredProjects(string directoryPath)
    {
        var parentKey = NormalizePath(directoryPath);
        if (string.IsNullOrEmpty(parentKey)) return false;

        var prefix = parentKey + "/";
        foreach (var k in _configuredProjects.Keys)
        {
            if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static void ClearWorkspace(string workspaceRootPath)
    {
        var rootKey = NormalizePath(workspaceRootPath);
        if (string.IsNullOrEmpty(rootKey))
        {
            _fileContents.Clear();
            _directoryFiles.Clear();
            _gitRepositoryRoots.Clear();
            _configuredProjects.Clear();
            return;
        }

        var prefix = rootKey + "/";
        foreach (var k in _fileContents.Keys)
        {
            if (k.Equals(rootKey, StringComparison.OrdinalIgnoreCase) ||
                k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _fileContents.TryRemove(k, out _);
            }
        }

        foreach (var k in _directoryFiles.Keys)
        {
            if (k.Equals(rootKey, StringComparison.OrdinalIgnoreCase) ||
                k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _directoryFiles.TryRemove(k, out _);
            }
        }

        foreach (var k in _gitRepositoryRoots.Keys)
        {
            if (k.Equals(rootKey, StringComparison.OrdinalIgnoreCase) ||
                k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _gitRepositoryRoots.TryRemove(k, out _);
            }
        }

        foreach (var k in _configuredProjects.Keys)
        {
            if (k.Equals(rootKey, StringComparison.OrdinalIgnoreCase) ||
                k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                _configuredProjects.TryRemove(k, out _);
            }
        }
    }
}
