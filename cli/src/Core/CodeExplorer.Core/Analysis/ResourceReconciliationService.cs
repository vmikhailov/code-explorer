using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Analysis;

public record CanonicalResource(
    string Id,
    string Name,
    string Engine,
    string DbType,
    string Kind,
    HashSet<string> Aliases,
    Dictionary<string, string> Properties,
    string? DeclaringFilePath = null,
    string? ProjectId = null
);

public record DatabaseUsageRecord(
    string ProjectId,
    string FileId,
    string CallerSymbolId,
    string ProviderOrOrm,
    string EngineHint,
    string DbType,
    bool IsOrm,
    string? TargetAliasOrName = null
);

/// <summary>
/// Reconciles and unifies external resources (databases, message brokers, external services)
/// discovered across configuration manifests, Docker Compose, environment files, and code.
/// Eliminates duplicate database nodes and maps ORM/driver usages to single canonical resources.
/// </summary>
public class ResourceReconciliationService
{
    private readonly ConcurrentDictionary<string, CanonicalResource> _resourcesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _aliasToResourceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DatabaseUsageRecord> _usages = [];
    private readonly object _lock = new();

    public IReadOnlyCollection<CanonicalResource> AllResources => _resourcesById.Values.ToList();

    public static string BuildCanonicalDatabaseId(string workspaceId, string dbType, string canonicalName)
    {
        var cleanName = Regex.Replace((canonicalName ?? "database").ToLowerInvariant().Trim(), @"[^a-z0-9_-]", "_").Trim('_');
        if (string.IsNullOrEmpty(cleanName)) cleanName = "default";
        var cleanType = string.IsNullOrWhiteSpace(dbType) ? "relational" : dbType.ToLowerInvariant().Trim();
        return $"{workspaceId}:res:db:{cleanType}:{cleanName}";
    }

    public static string BuildCanonicalTopicId(string workspaceId, string brokerType, string topicName)
    {
        var cleanName = Regex.Replace((topicName ?? "topic").ToLowerInvariant().Trim(), @"[^a-z0-9_.-]", "_").Trim('_');
        var cleanBroker = string.IsNullOrWhiteSpace(brokerType) ? "broker" : brokerType.ToLowerInvariant().Trim();
        return $"{workspaceId}:res:topic:{cleanBroker}:{cleanName}";
    }

    public static string BuildCanonicalServiceId(string workspaceId, string scope, string host)
    {
        var cleanHost = (host ?? "service").ToLowerInvariant().Trim();
        return $"{workspaceId}:res:service:{scope}:{cleanHost}";
    }

    /// <summary>
    /// Registers or updates a canonical resource discovered from infrastructure/configuration.
    /// </summary>
    public CanonicalResource RegisterResource(
        string workspaceId,
        string rawName,
        string engine,
        string dbType,
        string kind,
        string? declaringFilePath = null,
        string? projectId = null,
        IEnumerable<string>? aliases = null,
        Dictionary<string, string>? properties = null)
    {
        lock (_lock)
        {
            var canonicalName = NormalizeResourceName(rawName, engine);
            var id = BuildCanonicalDatabaseId(workspaceId, dbType, canonicalName);

            if (_resourcesById.TryGetValue(id, out var existing))
            {
                if (aliases != null)
                {
                    foreach (var a in aliases)
                    {
                        var normAlias = NormalizeAlias(a);
                        if (!string.IsNullOrEmpty(normAlias))
                        {
                            existing.Aliases.Add(normAlias);
                            _aliasToResourceId[normAlias] = id;
                        }
                    }
                }
                return existing;
            }

            var aliasSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var normSelf = NormalizeAlias(canonicalName);
            if (!string.IsNullOrEmpty(normSelf))
            {
                aliasSet.Add(normSelf);
                _aliasToResourceId[normSelf] = id;
            }

            if (!string.IsNullOrEmpty(rawName))
            {
                var normRaw = NormalizeAlias(rawName);
                if (!string.IsNullOrEmpty(normRaw))
                {
                    aliasSet.Add(normRaw);
                    _aliasToResourceId[normRaw] = id;
                }
            }

            if (!string.IsNullOrEmpty(engine))
            {
                var normEngine = NormalizeAlias(engine);
                if (!string.IsNullOrEmpty(normEngine))
                {
                    aliasSet.Add(normEngine);
                    // Only map alias to this ID if engine alias isn't claimed yet
                    _aliasToResourceId.TryAdd(normEngine, id);
                }
            }

            if (aliases != null)
            {
                foreach (var a in aliases)
                {
                    var norm = NormalizeAlias(a);
                    if (!string.IsNullOrEmpty(norm))
                    {
                        aliasSet.Add(norm);
                        _aliasToResourceId[norm] = id;
                    }
                }
            }

            var resProps = properties ?? new Dictionary<string, string>();
            resProps["engine"] = engine;
            resProps["db_type"] = dbType;

            var resource = new CanonicalResource(
                id,
                canonicalName,
                engine,
                dbType,
                kind,
                aliasSet,
                resProps,
                declaringFilePath,
                projectId
            );

            _resourcesById[id] = resource;
            return resource;
        }
    }

    /// <summary>
    /// Registers an alias pointing to a canonical resource.
    /// </summary>
    public void RegisterAlias(string alias, string resourceId)
    {
        var norm = NormalizeAlias(alias);
        if (!string.IsNullOrEmpty(norm))
        {
            _aliasToResourceId[norm] = resourceId;
            if (_resourcesById.TryGetValue(resourceId, out var res))
            {
                res.Aliases.Add(norm);
            }
        }

        // If alias is a connection URL (e.g. host:5432/dbname), extract dbname as an additional alias
        if (norm.Contains('/'))
        {
            var slashIdx = norm.LastIndexOf('/');
            if (slashIdx >= 0 && slashIdx < norm.Length - 1)
            {
                var dbSegment = norm[(slashIdx + 1)..];
                var qIdx = dbSegment.IndexOf('?');
                if (qIdx > 0) dbSegment = dbSegment[..qIdx];
                if (!string.IsNullOrEmpty(dbSegment) && !IsGenericConfigKey(dbSegment))
                {
                    _aliasToResourceId.TryAdd(dbSegment, resourceId);
                    if (_resourcesById.TryGetValue(resourceId, out var res))
                    {
                        res.Aliases.Add(dbSegment);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Records a database usage intent from a source file (ORM import, driver client, query).
    /// </summary>
    public void RecordUsage(DatabaseUsageRecord usage)
    {
        lock (_lock)
        {
            _usages.Add(usage);
        }
    }

    /// <summary>
    /// Tries to resolve a resource given an alias, name, or engine.
    /// </summary>
    public CanonicalResource? ResolveResource(string? aliasOrName, string? expectedDbType = null, string? expectedEngine = null)
    {
        if (!string.IsNullOrWhiteSpace(aliasOrName))
        {
            var norm = NormalizeAlias(aliasOrName);
            if (_aliasToResourceId.TryGetValue(norm, out var id) && _resourcesById.TryGetValue(id, out var found))
            {
                return found;
            }

            // Check if alias contains database path segment (e.g. host:5432/dbname)
            if (norm.Contains('/'))
            {
                var slashIdx = norm.LastIndexOf('/');
                if (slashIdx >= 0 && slashIdx < norm.Length - 1)
                {
                    var dbSegment = norm[(slashIdx + 1)..];
                    var qIdx = dbSegment.IndexOf('?');
                    if (qIdx > 0) dbSegment = dbSegment[..qIdx];
                    if (!string.IsNullOrEmpty(dbSegment) && _aliasToResourceId.TryGetValue(dbSegment, out var dbId) && _resourcesById.TryGetValue(dbId, out var dbFound))
                    {
                        return dbFound;
                    }
                }
            }

            // Direct scan by alias or name
            foreach (var res in _resourcesById.Values)
            {
                if (res.Aliases.Contains(norm) ||
                    string.Equals(res.Name, aliasOrName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(res.Engine, aliasOrName, StringComparison.OrdinalIgnoreCase))
                {
                    if (expectedDbType != null && !string.Equals(res.DbType, expectedDbType, StringComparison.OrdinalIgnoreCase))
                        continue;
                    return res;
                }
            }
        }

        // Fallback by engine
        if (!string.IsNullOrWhiteSpace(expectedEngine))
        {
            var matchingEngine = _resourcesById.Values.Where(r => string.Equals(r.Engine, expectedEngine, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matchingEngine.Count == 1) return matchingEngine[0];
            return null; // expectedEngine was specified but not matched; do not guess
        }

        // If no engine was specified (e.g. raw anonymous SQL), and only 1 resource matches dbType
        if (string.IsNullOrWhiteSpace(aliasOrName) && !string.IsNullOrWhiteSpace(expectedDbType))
        {
            var matchingType = _resourcesById.Values.Where(r => string.Equals(r.DbType, expectedDbType, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matchingType.Count == 1) return matchingType[0];
        }

        return null;
    }

    /// <summary>
    /// Normalizes raw names from connection strings and configuration keys into high-signal resource names.
    /// Strips generic words like "Connection", "DefaultConnection", "ConnectionString".
    /// </summary>
    public static string NormalizeResourceName(string rawName, string engine)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.IsNullOrWhiteSpace(engine) ? "Database" : engine;

        var trimmed = rawName.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (IsGenericConfigKey(lower))
        {
            return string.IsNullOrWhiteSpace(engine) ? "Database" : engine;
        }

        // Strip known technical suffixes like ConnectionString, Connection, DbContext, Context
        if (lower.EndsWith("connectionstrings") && trimmed.Length > 17)
        {
            trimmed = trimmed[..^17].TrimEnd('_', '-');
        }
        else if (lower.EndsWith("connectionstring") && trimmed.Length > 16)
        {
            trimmed = trimmed[..^16].TrimEnd('_', '-');
        }
        else if (lower.EndsWith("dbcontext") && trimmed.Length > 9)
        {
            trimmed = trimmed[..^9].TrimEnd('_', '-');
        }
        else if (lower.EndsWith("connection") && trimmed.Length > 10)
        {
            trimmed = trimmed[..^10].TrimEnd('_', '-');
        }
        else if (lower.EndsWith("context") && trimmed.Length > 7)
        {
            trimmed = trimmed[..^7].TrimEnd('_', '-');
        }

        if (string.IsNullOrWhiteSpace(trimmed) || IsGenericConfigKey(trimmed.ToLowerInvariant()))
        {
            return string.IsNullOrWhiteSpace(engine) ? "Database" : engine;
        }

        return trimmed;
    }

    public static bool IsGenericConfigKey(string key)
    {
        var lower = (key ?? "").Trim().ToLowerInvariant();
        return lower is "defaultconnection" or "connectionstring" or "connectionstrings" or
               "database" or "db" or "datasource" or "main" or "default" or
               "spring.datasource.url" or "spring-datasource" or "database_url";
    }

    public static string NormalizeAlias(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var clean = raw.Trim().ToLowerInvariant();
        // Strip common URL prefixes
        if (clean.StartsWith("postgresql://") || clean.StartsWith("postgres://"))
            clean = clean[(clean.IndexOf("://") + 3)..];
        else if (clean.StartsWith("mysql://") || clean.StartsWith("redis://") || clean.StartsWith("mongodb://") || clean.StartsWith("amqp://"))
            clean = clean[(clean.IndexOf("://") + 3)..];

        return clean;
    }
}
