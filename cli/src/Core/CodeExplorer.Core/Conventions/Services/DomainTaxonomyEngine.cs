using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Conventions.Services;

/// <summary>
/// Domain-Driven Design (DDD) Macro-Domain and Problem Space taxonomy engine.
/// Maps services, directories, and files to canonical domains using user configuration,
/// architectural role heuristics, and directory structures.
/// </summary>
public sealed class DomainTaxonomyEngine
{
    private static readonly HashSet<string> GenericDomainWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "core", "service", "services", "app", "application", "default", "internal", "microservice",
        "table", "tables", "database", "sql", "boundedcontext", "context"
    };

    private static readonly HashSet<string> SkipRootDirs = WorkspaceConventions.GenericFolderRoots;

    private readonly Dictionary<string, string> _projectToDomain;
    private readonly Dictionary<string, string> _serviceOverrides;
    private readonly Dictionary<string, string> _domainIcons;
    private readonly List<(Regex Regex, string Domain)> _patternToDomain;
    private readonly ServiceIdentityResolver _serviceResolver;

    public DomainTaxonomyEngine(
        IDictionary<string, string>? projectToDomain = null,
        IDictionary<string, string>? serviceOverrides = null,
        IDictionary<string, string>? domainIcons = null,
        IEnumerable<(Regex Regex, string Domain)>? patternToDomain = null,
        ServiceIdentityResolver? serviceResolver = null)
    {
        _projectToDomain = new Dictionary<string, string>(projectToDomain ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        _serviceOverrides = new Dictionary<string, string>(serviceOverrides ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        _domainIcons = new Dictionary<string, string>(domainIcons ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        _patternToDomain = [..(patternToDomain ?? [])];
        _serviceResolver = serviceResolver ?? new ServiceIdentityResolver();
    }

    /// <summary>
    /// Attempts to resolve a user-configured domain icon.
    /// </summary>
    public bool TryGetDomainIcon(string domainName, out string icon)
    {
        return _domainIcons.TryGetValue(domainName, out icon!);
    }

    /// <summary>
    /// Resolves a domain only from an explicit, name-based entry (service override or listed project/service);
    /// never from glob patterns or file paths.
    /// </summary>
    public bool TryGetExplicitDomain(string? projectName, string? filePath, out string domain)
    {
        domain = string.Empty;

        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var p = projectName.Trim();
            var clean = _serviceResolver.NormalizeServiceName(p);
            foreach (var key in new[] { p, clean })
            {
                if (_serviceOverrides.TryGetValue(key, out var ovr) || _projectToDomain.TryGetValue(key, out ovr))
                {
                    domain = ovr;
                    return true;
                }
            }
        }

        // A project inside a listed service folder (e.g. adhub/adhub-cli) belongs to that service's domain.
        var firstSegment = filePath?.Replace('\\', '/').TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrEmpty(firstSegment) && firstSegment != ".." && firstSegment != "." &&
            (_serviceOverrides.TryGetValue(firstSegment, out var byFolder) || _projectToDomain.TryGetValue(firstSegment, out byFolder)))
        {
            domain = byFolder;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to resolve a user-configured domain for a project name or path.
    /// </summary>
    public bool TryGetConfiguredDomain(string? projectName, string? filePath, out string domain)
    {
        domain = string.Empty;

        // 1. Explicit service override takes precedence
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var p = projectName.Trim();
            if (_serviceOverrides.TryGetValue(p, out var ovr))
            {
                domain = ovr;
                return true;
            }
            var clean = _serviceResolver.NormalizeServiceName(p);
            if (_serviceOverrides.TryGetValue(clean, out ovr))
            {
                domain = ovr;
                return true;
            }
        }

        // 2. Exact project name mapping
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var p = projectName.Trim();
            if (_projectToDomain.TryGetValue(p, out var d))
            {
                domain = d;
                return true;
            }
            var clean = _serviceResolver.NormalizeServiceName(p);
            if (_projectToDomain.TryGetValue(clean, out d))
            {
                domain = d;
                return true;
            }
        }

        // 3. Glob patterns against project name or file path
        var normPath = filePath?.Replace('\\', '/');
        if (_patternToDomain.Count > 0)
        {
            foreach (var (regex, targetDomain) in _patternToDomain)
            {
                if (!string.IsNullOrWhiteSpace(projectName) && regex.IsMatch(projectName))
                {
                    domain = targetDomain;
                    return true;
                }
                if (!string.IsNullOrEmpty(normPath) && regex.IsMatch(normPath))
                {
                    domain = targetDomain;
                    return true;
                }
            }
        }

        // 4. File path substring match in configured projects
        if (!string.IsNullOrEmpty(normPath))
        {
            foreach (var (key, d) in _projectToDomain)
            {
                if (normPath.Contains("/" + key + "/", StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + key, StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + key + ".csproj", StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + key + ".fsproj", StringComparison.OrdinalIgnoreCase))
                {
                    domain = d;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Converts a delimited or mixed-case string into PascalCase.
    /// </summary>
    public static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Default";
        var parts = text.Split(['_', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p[1..]);
            }
        }
        return sb.Length > 0 ? sb.ToString() : text;
    }

    /// <summary>
    /// Formats a canonical PascalCase domain name into a clean, human-readable display name.
    /// E.g. "BillingAndPayments" -&gt; "Billing &amp; Payments"
    /// </summary>
    public static string FormatDomainDisplayName(string canonicalDomain)
    {
        if (string.IsNullOrWhiteSpace(canonicalDomain)) return "Core Domain";
        var withSpaces = Regex.Replace(canonicalDomain, "([a-z])([A-Z])", "$1 $2");
        return withSpaces.Replace(" And ", " & ");
    }

    /// <summary>
    /// Canonicalizes any raw domain, project name, or file path into a canonical DDD Problem Space (Macro-Domain).
    /// </summary>
    public string CanonicalizeDomain(
        string? rawDomain,
        string? serviceOrProjectName = null,
        string? filePath = null,
        string? role = null)
    {
        // 1. Explicit user configuration takes absolute precedence
        if (TryGetConfiguredDomain(serviceOrProjectName ?? rawDomain, filePath, out var userConfigured))
        {
            return ToPascalCase(userConfigured);
        }

        // 2. Ontology role classification
        if (role is "SharedLibrary" or "Library") return "SharedKernel";
        if (role is "CliTool") return "DeveloperTooling";
        if (role is "Test") return "TestingInfrastructure";

        // 3. Technical file path heuristics
        var normPath = (filePath ?? "").Replace('\\', '/');
        if (!string.IsNullOrEmpty(normPath))
        {
            if (normPath.StartWithIC("ui/") ||
                normPath.StartWithIC("frontend/") ||
                normPath.StartWithIC("client/") ||
                normPath.StartWithIC("web/") ||
                normPath.Contains("/ui/") ||
                normPath.Contains("/components/") ||
                normPath.Contains("/packages/ui"))
            {
                return "UserInterface";
            }

            if (normPath.StartWithIC("tools/") ||
                normPath.StartWithIC("cli/") ||
                normPath.Contains("/tools/") ||
                normPath.Contains("/cli/"))
            {
                return "DeveloperTooling";
            }

            if (normPath.StartWithIC("tests/") ||
                normPath.Contains("/tests/"))
            {
                return "TestingInfrastructure";
            }
        }

        // 4. If rawDomain is already provided and not a placeholder/generic word, use it
        var cleanRaw = (rawDomain ?? "").Trim();
        if (cleanRaw.StartWithIC("domain:"))
            cleanRaw = cleanRaw["domain:".Length..];
        if (cleanRaw.StartWithIC("dom:"))
            cleanRaw = cleanRaw["dom:".Length..];

        var isRunaway = cleanRaw.Length > 45 ||
                        cleanRaw.Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries).Length > 5 ||
                        FormatDomainDisplayName(cleanRaw).Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 6;

        if (!string.IsNullOrWhiteSpace(cleanRaw) && !GenericDomainWords.Contains(cleanRaw) && !WorkspaceConventions.GenericFolderRoots.Contains(cleanRaw) && !isRunaway)
        {
            return ToPascalCase(cleanRaw);
        }

        // 5. Extract domain from directory structure
        if (!string.IsNullOrEmpty(normPath))
        {
            var sanitizedPath = Regex.Replace(normPath, @"^[a-zA-Z]:[/]", "");
            var segments = sanitizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var meaningful = segments.Take(segments.Length - 1).Where(s => !SkipRootDirs.Contains(s)).ToList();
            if (meaningful.Count >= 1)
            {
                var cleanDir = _serviceResolver.NormalizeServiceName(meaningful[0]);
                if (!WorkspaceConventions.GenericFolderRoots.Contains(cleanDir) && !WorkspaceConventions.GenericFolderRoots.Contains(meaningful[0]))
                {
                    var dirPascal = ToPascalCase(cleanDir);
                    if (!WorkspaceConventions.GenericFolderRoots.Contains(dirPascal) && dirPascal.Length > 2)
                    {
                        return dirPascal;
                    }
                }
            }
        }

        // 6. Infer domain from normalized service or project name
        if (!string.IsNullOrWhiteSpace(serviceOrProjectName))
        {
            var cleanSvc = _serviceResolver.NormalizeServiceName(serviceOrProjectName);
            if (!GenericDomainWords.Contains(cleanSvc))
            {
                var svcPascal = ToPascalCase(cleanSvc);
                if (svcPascal.Length > 2)
                {
                    return svcPascal;
                }
            }
        }

        // 7. Fallback if rawDomain was runaway and directory/project heuristics yielded nothing:
        // Extract at most the first 3 meaningful words instead of returning a runaway sentence
        if (isRunaway && !string.IsNullOrWhiteSpace(cleanRaw))
        {
            var words = cleanRaw.Split([' ', '_', '-', '/', '&', ':', '.'], StringSplitOptions.RemoveEmptyEntries)
                .Where(w => !string.IsNullOrWhiteSpace(w) && w.Length > 1 && !GenericDomainWords.Contains(w))
                .Take(3)
                .ToList();
            if (words.Count > 0)
            {
                var truncated = string.Join("", words.Select(ToPascalCase));
                if (truncated.Length > 2)
                {
                    return truncated;
                }
            }
        }

        return "CoreDomain";
    }
}
