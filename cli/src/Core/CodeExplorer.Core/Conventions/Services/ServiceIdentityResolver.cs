using System.Collections.Concurrent;

namespace CodeExplorer.Core.Conventions.Services;

/// <summary>
/// Normalizes service identities, strips configured or standard architectural prefixes,
/// and resolves service aliases.
/// </summary>
public sealed class ServiceIdentityResolver
{
    private static readonly string[] DefaultGenericPrefixes =
    [
        "internal-service-",
        "integration-service-",
        "external-service-",
        "internal--",
        "integration--",
        "external--",
        "internal-",
        "integration-",
        "external-",
        "service-",
        "srv-"
    ];

    private readonly List<string> _configuredPrefixes;
    private readonly ConcurrentDictionary<string, string> _serviceAliases;

    public ServiceIdentityResolver(
        IEnumerable<string>? customPrefixes = null,
        IDictionary<string, string>? serviceAliases = null)
    {
        _configuredPrefixes = [..(customPrefixes ?? [])];
        _serviceAliases = new ConcurrentDictionary<string, string>(serviceAliases ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves a configured service alias if present (e.g. syskv -> kv).
    /// </summary>
    public bool TryGetServiceAlias(string name, out string alias)
    {
        return _serviceAliases.TryGetValue(name, out alias!);
    }

    /// <summary>
    /// Registers a service alias dynamically (e.g. from Layer 2 project discovery or configuration).
    /// </summary>
    public void RegisterAlias(string alias, string canonicalName)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(canonicalName)) return;
        _serviceAliases[alias.Trim()] = canonicalName.Trim();
    }

    /// <summary>
    /// Registers multiple service aliases dynamically.
    /// </summary>
    public void RegisterAliases(IEnumerable<KeyValuePair<string, string>> aliases)
    {
        foreach (var kvp in aliases)
        {
            RegisterAlias(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Strips generic microservice prefixes while preserving original casing.
    /// </summary>
    public string StripServicePrefix(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var clean = name.Trim();

        bool changed;
        do
        {
            changed = false;
            foreach (var p in _configuredPrefixes)
            {
                if (clean.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean[p.Length..];
                    changed = true;
                    break;
                }
            }
            if (changed) continue;

            foreach (var p in DefaultGenericPrefixes)
            {
                if (clean.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean[p.Length..];
                    changed = true;
                    break;
                }
            }
        } while (changed);

        clean = clean.Trim('-', '_');
        return string.IsNullOrEmpty(clean) ? name.Trim() : clean;
    }

    /// <summary>
    /// Normalizes service and project names: converts to lowercase, strips prefixes,
    /// and resolves configured service aliases.
    /// </summary>
    public string NormalizeServiceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var clean = name.Trim().ToLowerInvariant();

        var stripped = StripServicePrefix(clean).ToLowerInvariant();
        var candidate = string.IsNullOrEmpty(stripped) ? clean : stripped;

        if (_serviceAliases.TryGetValue(candidate, out var aliased))
        {
            return aliased.ToLowerInvariant();
        }

        return candidate;
    }
}
