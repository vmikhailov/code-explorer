using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using CodeExplorer.Core.Parser.Incremental;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Stateless single-pass file streamer and UTF-8 reader.
/// Reads a file from disk once using pooled buffers without retaining raw file text in any global cache.
/// </summary>
public static class FileStreamReader
{
    private const int ChunkSize = 16384;
    private const long MaxDecodedTextBytes = 2 * 1024 * 1024;
    private const int MaxCharLookaheadPadding = 32;
    private const int MaxDecoderPadding = 4;

    public static string DecodeUtf8(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return string.Empty;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes[3..]);
        }
        return Encoding.UTF8.GetString(bytes);
    }

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

            var firstLen = await ReadInitialChunkAsync(stream, byteBuf, cancellationToken);
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
                HashUtility.AppendNormalizedData(sha, firstSpan);
            }

            var newlines = 0;
            byte lastByte = 0;
            var hasBytes = firstLen > 0;
            if (hasBytes)
            {
                newlines += firstSpan.Count((byte)'\n');
                lastByte = firstSpan[^1];
            }

            var shouldDecodeText = !skipByContent && sizeBytes <= MaxDecodedTextBytes;
            Decoder? decoder = null;
            var charCount = 0;
            if (shouldDecodeText)
            {
                if (firstLen == 0)
                {
                    var emptyHash = sha != null ? HashUtility.FinalizeHash(sha) : null;
                    return new StreamedFileResult(skipByContent, emptyHash, 0, artifactRole, string.Empty);
                }

                var initialCapacity = (int)Math.Clamp(sizeBytes, firstLen, MaxDecodedTextBytes) + MaxCharLookaheadPadding;
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
                    HashUtility.AppendNormalizedData(sha, chunkSpan);
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
                NormalizeStandaloneCarriageReturns(charBuf, charCount, ref newlines, ref lastByte);
                decodedText = new string(charBuf, 0, charCount);
            }

            var contentHash = sha != null ? HashUtility.FinalizeHash(sha) : null;
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

    private static async ValueTask<int> ReadInitialChunkAsync(FileStream stream, byte[] byteBuf, CancellationToken ct)
    {
        var firstLen = 0;
        while (firstLen < ChunkSize)
        {
            var r = await stream.ReadAsync(byteBuf.AsMemory(firstLen, ChunkSize - firstLen), ct);
            if (r == 0) break;
            firstLen += r;
        }
        return firstLen;
    }

    private static void NormalizeStandaloneCarriageReturns(char[] charBuf, int charCount, ref int newlines, ref byte lastByte)
    {
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
    }

    private static int DecodeChunkToPool(
        Decoder decoder,
        ReadOnlySpan<byte> bytes,
        bool flush,
        ref char[] charBuf,
        int currentOffset)
    {
        var maxChars = Encoding.UTF8.GetMaxCharCount(bytes.Length) + MaxDecoderPadding;
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

    public static string ReadTextOnce(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;

        var activeIndex = WorkspaceDirectoryIndex.Current;
        if (activeIndex != null && !activeIndex.FileExistsInDirectory(fullPath, defaultWhenUncached: true))
        {
            return string.Empty;
        }

        if (!File.Exists(fullPath)) return string.Empty;
        try
        {
            return File.ReadAllText(fullPath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            activeIndex?.RemoveDirectoryFileEntry(fullPath);
            return string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    public static async ValueTask<string> ReadTextOnceAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return string.Empty;

        var activeIndex = WorkspaceDirectoryIndex.Current;
        if (activeIndex != null && !activeIndex.FileExistsInDirectory(fullPath, defaultWhenUncached: true))
        {
            return string.Empty;
        }

        if (!File.Exists(fullPath)) return string.Empty;
        try
        {
            return await File.ReadAllTextAsync(fullPath, cancellationToken);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            activeIndex?.RemoveDirectoryFileEntry(fullPath);
            return string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    public static IEnumerable<string> ReadLinesOnce(string fullPath)
    {
        var text = ReadTextOnce(fullPath);
        if (string.IsNullOrEmpty(text)) return [];
        return text.Split(["\r\n", "\n"], StringSplitOptions.None);
    }
}
