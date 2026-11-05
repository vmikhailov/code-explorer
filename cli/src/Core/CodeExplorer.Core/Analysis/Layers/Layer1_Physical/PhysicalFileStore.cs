using System.Collections.Concurrent;
using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Thread-safe in-memory store populated by Layer 1 (<see cref="Layers.Layer1PhysicalParser"/>)
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

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        try
        {
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }
        catch
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }

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
        CancellationToken cancellationToken = default)
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
                ? WorkspaceFileFilter.ClassifyArtifactRole(relativeFile, fileName, firstSpan)
                : null;

            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            Incremental.HashUtility.AppendNormalizedData(sha, firstSpan);

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
                    var emptyHash = Incremental.HashUtility.FinalizeHash(sha);
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
                Incremental.HashUtility.AppendNormalizedData(sha, chunkSpan);
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
                decodedText = new string(charBuf, 0, charCount);
                SetFileText(fullPath, decodedText);
            }

            var contentHash = Incremental.HashUtility.FinalizeHash(sha);
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

    public static string ReadAllText(string fullPath)
    {
        var key = NormalizePath(fullPath);
        if (!string.IsNullOrEmpty(key) && _fileContents.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!File.Exists(fullPath)) return string.Empty;
        var text = File.ReadAllText(fullPath);
        if (!string.IsNullOrEmpty(key))
        {
            // Do not cache unmanaged standalone fallback reads outside Layer 1
        }
        return text;
    }

    public static async ValueTask<string> ReadAllTextAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        var key = NormalizePath(fullPath);
        if (!string.IsNullOrEmpty(key) && _fileContents.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!File.Exists(fullPath)) return string.Empty;
        var text = await File.ReadAllTextAsync(fullPath, cancellationToken);
        if (!string.IsNullOrEmpty(key))
        {
            // Do not cache unmanaged standalone fallback reads outside Layer 1
        }
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
        return await File.ReadAllTextAsync(fullPath, cancellationToken);
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

    public static void ClearWorkspace(string workspaceRootPath)
    {
        var rootKey = NormalizePath(workspaceRootPath);
        if (string.IsNullOrEmpty(rootKey))
        {
            _fileContents.Clear();
            _directoryFiles.Clear();
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
    }
}
