using System.Security.Cryptography;
using System.Text;

namespace CodeExplorer.Core.Gates.Engine;

/// <summary>
/// Generates stable, line-shift immune fingerprints for semantic gate violations.
/// Fingerprints remain stable across file edits, refactorings, and line additions.
/// </summary>
public static class GateFingerprintGenerator
{
    public static string Generate(string gateId, string? file, string? symbol, string? target, int occurrenceIndex = 1)
    {
        var normGate = (gateId ?? "").Trim();
        var normFile = NormalizeFilePath(file);
        var normSymbol = (symbol ?? "").Trim();
        var normTarget = (target ?? "").Trim();

        var key = occurrenceIndex <= 1
            ? $"{normGate}|{normFile}|{normSymbol}|{normTarget}"
            : $"{normGate}|{normFile}|{normSymbol}|{normTarget}|#{occurrenceIndex}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexStringLower(hashBytes);
    }

    public static string NormalizeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        var p = path.Replace('\\', '/').Trim();
        while (p.StartsWith("./", StringComparison.Ordinal))
        {
            p = p[2..];
        }
        return p.TrimStart('/');
    }
}
