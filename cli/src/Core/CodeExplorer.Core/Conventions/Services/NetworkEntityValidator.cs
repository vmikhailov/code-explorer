using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Conventions.Services;

/// <summary>
/// Structural validator and sanitizer for network entity hostnames, service names, and URIs.
/// Validates RFC-compliant DNS labels and URI structures, rejecting syntax fragments,
/// code expressions, language types, and unparsed template variables.
/// </summary>
public sealed class NetworkEntityValidator
{
    private static readonly Regex VersionSegmentRegex = new(@"^v\d+(\.\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ValidDnsLabelRegex = new(@"^[a-z0-9]([a-z0-9\-]*[a-z0-9])?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Ipv4Regex = new(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.Compiled);

    private readonly HashSet<string> _customGarbageHosts;

    public NetworkEntityValidator(IEnumerable<string>? customGarbageHosts = null)
    {
        _customGarbageHosts = new HashSet<string>(customGarbageHosts ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks whether an external service candidate or host domain is considered garbage/phantom.
    /// </summary>
    public bool IsGarbageExternalService(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return true;
        var lower = domain.Trim().Trim('\'', '"', '`').ToLowerInvariant();

        // 1. Primitive types, keywords, and unresolvable placeholders
        if (lower is "*" or "unknown-service" or "httprequest" or "pageurl"
            or "string" or "undefined" or "null" or "void" or "any" or "never" or "object" or "boolean" or "number")
        {
            return true;
        }

        // 2. Wildcard port or path masks and generic localhost wildcards
        if (lower.EndsWith(":*") || lower.EndsWith("/*") || lower is "127.0.0.1:*" or "localhost:*")
        {
            return true;
        }

        // 3. Leading or trailing separators or file extensions
        if (lower.EndsWith('.') || lower.StartsWith('.') || lower.StartsWith('/') || lower.EndsWith('/'))
        {
            return true;
        }

        // 4. Code expressions, property access, AST fragments, and unparsed template interpolations
        if (lower.Contains('!') || lower.Contains("${") || lower.Contains("}}") ||
            lower.EndsWith(".value") || lower.EndsWith(".id") || lower.EndsWith(".key") ||
            lower.StartsWith("config.") || lower.StartsWith("options.") || lower.StartsWith("env.") ||
            lower.Contains("global_env", StringComparison.OrdinalIgnoreCase) ||
            lower.Contains("process.env", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 5. Version-only segments (e.g. "v1", "v2.0")
        if (VersionSegmentRegex.IsMatch(lower))
        {
            return true;
        }

        // 6. Test and mock domains
        if (lower.EndsWith(".test") || lower.EndsWith(".example") || lower.EndsWith(".mock") || lower == "test")
        {
            return true;
        }

        // 7. Generic path segment or variable placeholders when extracted without a host
        if (lower is "service" or "services" or "host" or "hostname" or "url" or "uri" or "base" or "base_url"
            or "http-call" or "not-supported" or "urlquery" or "swagger" or "api" or "app" or "prod" or "dev" or "stage")
        {
            return true;
        }

        // 8. Custom user-configured garbage hosts from .codeexplorer/conventions.json
        if (_customGarbageHosts.Contains(lower))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Validates whether a candidate hostname is a structurally valid DNS host, IP, or single service name.
    /// </summary>
    public static bool IsValidHostOrServiceName(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        var clean = candidate.Trim().Trim('\'', '"', '`');

        // Strip port if present
        var colonIdx = clean.LastIndexOf(':');
        if (colonIdx > 0 && int.TryParse(clean[(colonIdx + 1)..], out _))
        {
            clean = clean[..colonIdx];
        }

        if (clean.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (Ipv4Regex.IsMatch(clean)) return true;

        var labels = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length == 0) return false;

        foreach (var label in labels)
        {
            if (label.Length is < 1 or > 63) return false;
            if (!ValidDnsLabelRegex.IsMatch(label)) return false;
        }

        return true;
    }
}
