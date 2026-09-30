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
        using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            var remaining = bytes;

            while (!remaining.IsEmpty)
            {
                var idx = remaining.IndexOf((byte)'\r');

                if (idx < 0)
                {
                    sha.AppendData(remaining);
                    break;
                }

                if (idx > 0)
                {
                    sha.AppendData(remaining[..idx]);
                }

                remaining = remaining[(idx + 1)..];
            }

            Span<byte> hashBytes = stackalloc byte[32];
            sha.GetHashAndReset(hashBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
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

        using (var sha = SHA256.Create())
        {
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
