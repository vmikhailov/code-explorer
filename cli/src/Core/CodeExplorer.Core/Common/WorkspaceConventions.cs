using CodeExplorer.Core.Conventions;
using CodeExplorer.Core.Conventions.Services;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Thread-safe facade for workspace naming conventions, domains, and entity validation.
/// Delegates to the active <see cref="WorkspaceConventionProfile"/> instance.
/// </summary>
public static class WorkspaceConventions
{
    private static volatile WorkspaceConventionProfile _current = WorkspaceConventionProfile.Default;

    /// <summary>
    /// Gets or sets the active workspace convention profile.
    /// </summary>
    public static WorkspaceConventionProfile Current
    {
        get => _current;
        set => _current = value ?? WorkspaceConventionProfile.Default;
    }

    /// <summary>
    /// Loads custom conventions from .codeexplorer/conventions.json and .codeexplorer/domains.json if present.
    /// </summary>
    public static void LoadFromWorkspace(string workspaceRoot)
    {
        _current = WorkspaceConventionProfile.Load(workspaceRoot);
    }

    /// <summary>
    /// Resets all loaded conventions to default profile.
    /// </summary>
    public static void Clear()
    {
        _current = WorkspaceConventionProfile.Default;
    }

    public static bool TryGetDatabaseAlias(string name, out string alias) => _current.TryGetDatabaseAlias(name, out alias);

    public static bool TryGetServiceAlias(string name, out string alias) => _current.ServiceResolver.TryGetServiceAlias(name, out alias);

    public static void RegisterServiceAlias(string alias, string canonicalName) => _current.ServiceResolver.RegisterAlias(alias, canonicalName);

    public static bool IsTestFilePath(string? path) => _current.IsTestFilePath(path);
    public static bool IsFrontendFilePath(string? path) => _current.IsFrontendFilePath(path);

    public static bool TryGetTopicAlias(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? alias) => _current.Messaging.TryGetTopicAlias(key, out alias);

    public static bool TryGetDomainIcon(string domainName, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? icon) => _current.Domains.TryGetDomainIcon(domainName, out icon);

    /// <summary>
    /// Domain detection (configured domains.json patterns and the path/name/intent heuristics) is switched off for now:
    /// it grouped unrelated services into one domain. While off, a service carries a domain only when the user listed it
    /// explicitly (by name) in domains.json; path and pattern guessing never applies.
    /// </summary>
    public static bool DomainDetectionEnabled { get; set; }

    public static bool TryGetConfiguredDomain(string? projectName, string? filePath, out string domain)
    {
        if (!DomainDetectionEnabled)
        {
            return _current.Domains.TryGetExplicitDomain(projectName, filePath, out domain);
        }

        return _current.Domains.TryGetConfiguredDomain(projectName, filePath, out domain);
    }

    public static bool IsPlaceholderName(string? name) => _current.Messaging.IsPlaceholderName(name);

    public static bool IsValidTopicOrQueueName([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? s) => _current.Messaging.IsValidTopicOrQueueName(s);

    public static string NormalizeTopicName(string raw) => _current.Messaging.NormalizeTopicName(raw);

    public static bool TryMatchRouteFunction(string text, out string routeKey) => _current.TryMatchRouteFunction(text, out routeKey);

    public static string StripServicePrefix(string? name) => _current.ServiceResolver.StripServicePrefix(name);

    public static string NormalizeServiceName(string? name) => _current.ServiceResolver.NormalizeServiceName(name);

    public static string ToPascalCase(string text) => DomainTaxonomyEngine.ToPascalCase(text);

    public static string CanonicalizeDomain(
        string? rawDomain,
        string? serviceOrProjectName = null,
        string? filePath = null,
        string? role = null)
    {
        if (DomainDetectionEnabled)
        {
            return _current.Domains.CanonicalizeDomain(rawDomain, serviceOrProjectName, filePath, role);
        }

        if (_current.Domains.TryGetExplicitDomain(serviceOrProjectName ?? rawDomain, filePath, out var explicitDomain))
        {
            return ToPascalCase(explicitDomain);
        }

        // Detection off: the "domain" is just the service itself.
        var identity = _current.ServiceResolver.NormalizeServiceName(serviceOrProjectName ?? rawDomain);
        return string.IsNullOrWhiteSpace(identity) ? "CoreDomain" : ToPascalCase(identity);
    }

    public static string FormatDomainDisplayName(string canonicalDomain) => DomainTaxonomyEngine.FormatDomainDisplayName(canonicalDomain);

    public static bool IsGarbageExternalService(string domain) => _current.NetworkValidator.IsGarbageExternalService(domain);

    public static bool IsGenericUrlStemOrVariable(string? name) => NetworkEntityValidator.IsGenericUrlStemOrVariable(name);

    public static IReadOnlySet<string> GenericUrlVariableStems => NetworkEntityValidator.GenericUrlVariableStems;

    public static IReadOnlySet<string> HttpMethodVerbs => NetworkEntityValidator.HttpMethodVerbs;

    public static readonly HashSet<string> GenericFolderRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "src", "services", "microservices", "apps", "packages", "libs", "modules", "projects",
        "cmd", "home", "users", "cli", "ui", "uis", "tools", "bffs", "bff", "gateways",
        "apigateways", "spa", "web", "clients", "hosts", "buildingblocks"
    };

    public static int? GetPipelineStageOrder(string? symbolOrPath, string? typeName = null)
    {
        // Pipeline stage order metadata is now managed via architectural attributes/manifests, not ambient string matching.
        return null;
    }
}
