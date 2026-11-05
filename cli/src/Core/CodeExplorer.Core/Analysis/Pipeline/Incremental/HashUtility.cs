using System.Buffers;
using System.Security.Cryptography;

namespace CodeExplorer.Core.Parser.Incremental;

/// <summary>
/// Fast hash utility that normalizes CRLF and LF line endings to ensure consistent hashes across OSes.
/// </summary>
public static class HashUtility
{
    public static string ComputeSha256(ReadOnlySpan<byte> bytes)
    {
        // Normalize line endings: strip carriage return '\r' (0x0D) so CRLF and LF yield identical hashes across OSes
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        AppendNormalizedData(sha, bytes);
        return FinalizeHash(sha);
    }

    public static void AppendNormalizedData(IncrementalHash sha, ReadOnlySpan<byte> bytes)
    {

        while (!bytes.IsEmpty)
        {
            var idx = bytes.IndexOf((byte)'\r');

            if (idx < 0)
            {
                sha.AppendData(bytes);
                break;
            }

            if (idx > 0)
            {
                sha.AppendData(bytes[..idx]);
            }

            bytes = bytes[(idx + 1)..];
        }

    }

    public static string FinalizeHash(IncrementalHash sha)
    {
        Span<byte> hashBytes = stackalloc byte[32];
        sha.GetHashAndReset(hashBytes);
        return Convert.ToHexStringLower(hashBytes);
    }

    public static string ComputeSha256FromFile(string fullPath)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            using var fs = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1,
                FileOptions.SequentialScan);

            int bytesRead;
            while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                AppendNormalizedData(sha, buffer.AsSpan(0, bytesRead));
            }

            return FinalizeHash(sha);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static async ValueTask<string> ComputeSha256FromFileAsync(
        string fullPath,
        CancellationToken cancellationToken = default)
    {
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            await using var fs = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            int bytesRead;
            while ((bytesRead = await fs.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                AppendNormalizedData(sha, buffer.AsSpan(0, bytesRead));
            }

            return FinalizeHash(sha);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string UnusedHashMarker(ReadOnlySpan<byte> hashBytes)
    {
        return Convert.ToHexStringLower(hashBytes);
    }

    public static string ComputeSha256(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"; // Empty SHA-256
        }

        // Fast path: normalize \r in string
        var clean = text.Replace("\r", "");
        var bytes = System.Text.Encoding.UTF8.GetBytes(clean);

        using var sha = SHA256.Create();

        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
