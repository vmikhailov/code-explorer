using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;

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

    public IReadOnlyCollection<CanonicalResource> AllResources => [.. _resourcesById.Values];

    public static string BuildCanonicalDatabaseId(string workspaceId, string dbType, string canonicalName)
    {
        var cleanType = string.IsNullOrWhiteSpace(dbType) ? "relational" : dbType.ToLowerInvariant().Trim();
        var cleanName = Regex.Replace((canonicalName ?? "database").ToLowerInvariant().Trim(), @"[^a-z0-9_-]", "_").Trim('_');
        if (string.IsNullOrEmpty(cleanName)) cleanName = "default";
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
        var cleanHost = WorkspaceConventions.NormalizeServiceName(host);
        if (string.IsNullOrEmpty(cleanHost)) cleanHost = (host ?? "service").ToLowerInvariant().Trim();
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
            if (WorkspaceConventions.TryGetDatabaseAlias(canonicalName, out var dbAlias))
            {
                canonicalName = dbAlias;
            }
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
                if (!string.IsNullOrEmpty(normRaw) && !IsDisallowedDatabaseAlias(normRaw))
                {
                    aliasSet.Add(normRaw);
                    _aliasToResourceId[normRaw] = id;
                }
            }

            var strippedRaw = StripDatabaseSuffix(rawName);
            if (!string.IsNullOrEmpty(strippedRaw) && !string.Equals(strippedRaw, rawName, StringComparison.OrdinalIgnoreCase))
            {
                var normStripped = NormalizeAlias(strippedRaw);
                if (!string.IsNullOrEmpty(normStripped) && !IsDisallowedDatabaseAlias(normStripped))
                {
                    aliasSet.Add(normStripped);
                    _aliasToResourceId[normStripped] = id;
                }
            }

            var strippedCanonical = StripDatabaseSuffix(canonicalName);
            if (!string.IsNullOrEmpty(strippedCanonical) && !string.Equals(strippedCanonical, canonicalName, StringComparison.OrdinalIgnoreCase))
            {
                var normStrippedC = NormalizeAlias(strippedCanonical);
                if (!string.IsNullOrEmpty(normStrippedC) && !IsDisallowedDatabaseAlias(normStrippedC))
                {
                    aliasSet.Add(normStrippedC);
                    _aliasToResourceId[normStrippedC] = id;
                }
            }

            // Also register {engine}.{alias} so e.g. "PostgreSQL.ordering" resolves to orderingdb
            if (!string.IsNullOrWhiteSpace(engine) && !IsGenericConfigKey(engine))
            {
                var normEng = NormalizeEngineName(engine).ToLowerInvariant();
                foreach (var a in aliasSet.ToList())
                {
                    if (!a.Contains('.') && !IsGenericConfigKey(a))
                    {
                        var prefixed = $"{normEng}.{a}";
                        _aliasToResourceId.TryAdd(prefixed, id);
                    }
                }
            }

            // Note: engine is intentionally NOT registered as an alias in aliasSet or _aliasToResourceId.
            // Engine names (e.g. "postgresql", "redis") are technology families, not unique resource aliases.
            // Matching by engine is handled dynamically in ResolveResource when unique.

            if (aliases != null)
            {
                foreach (var a in aliases)
                {
                    var norm = NormalizeAlias(a);
                    if (!string.IsNullOrEmpty(norm) && !IsDisallowedDatabaseAlias(norm))
                    {
                        aliasSet.Add(norm);
                        _aliasToResourceId[norm] = id;
                    }
                }
            }

            // If this is a concrete database engine, retire and transfer any previous generic "Database" placeholders of the same dbType
            // ONLY within the same project (or unattached). Do not collapse placeholders across different projects!
            var isConcrete = !IsGenericConfigKey(engine) && !string.Equals(engine, "Database", StringComparison.OrdinalIgnoreCase);
            if (isConcrete)
            {
                var genericPlaceholders = _resourcesById.Values
                    .Where(r => string.Equals(r.DbType, dbType, StringComparison.OrdinalIgnoreCase) &&
                                (IsGenericConfigKey(r.Engine) || string.Equals(r.Name, "Database", StringComparison.OrdinalIgnoreCase)) &&
                                (projectId == null || r.ProjectId == null || string.Equals(r.ProjectId, projectId, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var placeholder in genericPlaceholders)
                {
                    _resourcesById.TryRemove(placeholder.Id, out _);
                    foreach (var a in placeholder.Aliases)
                    {
                        if (!IsDisallowedDatabaseAlias(a))
                        {
                            aliasSet.Add(a);
                            _aliasToResourceId[a] = id;
                        }
                    }
                    _aliasToResourceId[placeholder.Id] = id;
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
        if (!string.IsNullOrEmpty(norm) && !IsDisallowedDatabaseAlias(norm))
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
                if (!string.IsNullOrEmpty(dbSegment) && !IsDisallowedDatabaseAlias(dbSegment))
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
            if (_resourcesById.TryGetValue(aliasOrName, out var directById))
            {
                if (expectedDbType == null || string.Equals(directById.DbType, expectedDbType, StringComparison.OrdinalIgnoreCase))
                    return directById;
            }

            var norm = NormalizeAlias(aliasOrName);
            if (_aliasToResourceId.TryGetValue(norm, out var id) && _resourcesById.TryGetValue(id, out var found))
            {
                return found;
            }

            foreach (var variant in GetPluralSingularVariants(norm))
            {
                if (_aliasToResourceId.TryGetValue(variant, out var vId) && _resourcesById.TryGetValue(vId, out var vFound))
                {
                    if ((expectedEngine == null || string.Equals(NormalizeEngineName(vFound.Engine), NormalizeEngineName(expectedEngine), StringComparison.OrdinalIgnoreCase)) &&
                        (expectedDbType == null || string.Equals(vFound.DbType, expectedDbType, StringComparison.OrdinalIgnoreCase)))
                        return vFound;
                }
            }

            // If alias contains technology prefix (e.g. PostgreSQL.ordering or postgresql.orderingdb)
            if (norm.Contains('.'))
            {
                var dotParts = norm.Split('.', 2);
                var engPrefix = NormalizeEngineName(dotParts[0]);
                var schSuffix = dotParts[1].Trim();
                var strippedSch = StripDatabaseSuffix(schSuffix);

                if (_aliasToResourceId.TryGetValue(schSuffix, out var schId) && _resourcesById.TryGetValue(schId, out var schFound))
                {
                    if (string.Equals(NormalizeEngineName(schFound.Engine), engPrefix, StringComparison.OrdinalIgnoreCase))
                        return schFound;
                }
                foreach (var variant in GetPluralSingularVariants(schSuffix))
                {
                    if (_aliasToResourceId.TryGetValue(variant, out var vSchId) && _resourcesById.TryGetValue(vSchId, out var vSchFound))
                    {
                        if (string.Equals(NormalizeEngineName(vSchFound.Engine), engPrefix, StringComparison.OrdinalIgnoreCase))
                            return vSchFound;
                    }
                }
                if (!string.Equals(strippedSch, schSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    if (_aliasToResourceId.TryGetValue(strippedSch, out var strId) && _resourcesById.TryGetValue(strId, out var strFound))
                    {
                        if (string.Equals(NormalizeEngineName(strFound.Engine), engPrefix, StringComparison.OrdinalIgnoreCase))
                            return strFound;
                    }
                    foreach (var variant in GetPluralSingularVariants(strippedSch))
                    {
                        if (_aliasToResourceId.TryGetValue(variant, out var vStrId) && _resourcesById.TryGetValue(vStrId, out var vStrFound))
                        {
                            if (string.Equals(NormalizeEngineName(vStrFound.Engine), engPrefix, StringComparison.OrdinalIgnoreCase))
                                return vStrFound;
                        }
                    }
                }
            }

            var strippedNorm = StripDatabaseSuffix(norm);
            if (!string.Equals(strippedNorm, norm, StringComparison.OrdinalIgnoreCase))
            {
                if (_aliasToResourceId.TryGetValue(strippedNorm, out var nStrId) && _resourcesById.TryGetValue(nStrId, out var nStrFound))
                {
                    if (expectedDbType == null || string.Equals(nStrFound.DbType, expectedDbType, StringComparison.OrdinalIgnoreCase))
                        return nStrFound;
                }
                foreach (var variant in GetPluralSingularVariants(strippedNorm))
                {
                    if (_aliasToResourceId.TryGetValue(variant, out var vnStrId) && _resourcesById.TryGetValue(vnStrId, out var vnStrFound))
                    {
                        if ((expectedEngine == null || string.Equals(NormalizeEngineName(vnStrFound.Engine), NormalizeEngineName(expectedEngine), StringComparison.OrdinalIgnoreCase)) &&
                            (expectedDbType == null || string.Equals(vnStrFound.DbType, expectedDbType, StringComparison.OrdinalIgnoreCase)))
                            return vnStrFound;
                    }
                }
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

            // Direct scan by alias or name (engine matching is evaluated dynamically below)
            foreach (var res in _resourcesById.Values)
            {
                if (res.Aliases.Contains(norm) ||
                    string.Equals(res.Name, aliasOrName, StringComparison.OrdinalIgnoreCase))
                {
                    if (expectedDbType != null && !string.Equals(res.DbType, expectedDbType, StringComparison.OrdinalIgnoreCase))
                        continue;
                    return res;
                }

                foreach (var variant in GetPluralSingularVariants(norm))
                {
                    var schPart = res.Name.Contains('.') ? res.Name.Split('.', 2)[1] : res.Name;
                    if (res.Aliases.Contains(variant) ||
                        string.Equals(res.Name, variant, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(schPart, variant, StringComparison.OrdinalIgnoreCase))
                    {
                        if (expectedDbType != null && !string.Equals(res.DbType, expectedDbType, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (expectedEngine != null && !string.Equals(NormalizeEngineName(res.Engine), NormalizeEngineName(expectedEngine), StringComparison.OrdinalIgnoreCase))
                            continue;
                        return res;
                    }
                }
            }
        }

        // Fallback by engine: only if no specific name/schema was requested, or alias is a generic client/engine reference
        var isGenericAlias = string.IsNullOrWhiteSpace(aliasOrName) ||
                             IsGenericConfigKey(aliasOrName) ||
                             IsKnownDatabaseEngine(aliasOrName) ||
                             aliasOrName.EndsWith("_client", StringComparison.OrdinalIgnoreCase) ||
                             aliasOrName.EndsWith("-client", StringComparison.OrdinalIgnoreCase) ||
                             (!string.IsNullOrWhiteSpace(expectedEngine) && 
                              (aliasOrName.Equals(expectedEngine, StringComparison.OrdinalIgnoreCase) ||
                               aliasOrName.StartsWith($"{expectedEngine}_", StringComparison.OrdinalIgnoreCase)));

        var targetEngine = !string.IsNullOrWhiteSpace(expectedEngine)
            ? NormalizeEngineName(expectedEngine)
            : (IsKnownDatabaseEngine(aliasOrName) ? NormalizeEngineName(aliasOrName) : null);

        if (isGenericAlias && !string.IsNullOrWhiteSpace(targetEngine))
        {
            var matchingEngine = _resourcesById.Values.Where(r => string.Equals(r.Engine, targetEngine, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matchingEngine.Count == 1) return matchingEngine[0];
            return null; // targetEngine was specified but ambiguous (multiple databases) or not matched; do not guess
        }

        // If no engine was specified (e.g. raw anonymous SQL), and only 1 resource matches dbType
        if (string.IsNullOrWhiteSpace(aliasOrName) && !string.IsNullOrWhiteSpace(expectedDbType))
        {
            var matchingType = _resourcesById.Values.Where(r => string.Equals(r.DbType, expectedDbType, StringComparison.OrdinalIgnoreCase)).ToList();
            var concrete = matchingType.Where(r => !IsGenericConfigKey(r.Engine) && !string.Equals(r.Name, "Database", StringComparison.OrdinalIgnoreCase)).ToList();
            if (concrete.Count == 1) return concrete[0];
            if (matchingType.Count == 1) return matchingType[0];
        }

        return null;
    }

    private static IEnumerable<string> GetPluralSingularVariants(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) yield break;
        yield return s + "s";
        yield return s + "es";
        if (s.EndsWith("es", StringComparison.OrdinalIgnoreCase) && s.Length > 4)
        {
            yield return s[..^2];
        }
        if (s.EndsWith("s", StringComparison.OrdinalIgnoreCase) && s.Length > 3)
        {
            yield return s[..^1];
        }
    }

    /// <summary>
    /// Normalizes raw names from connection strings and configuration keys into high-signal resource names.
    /// Strips generic words like "Connection", "DefaultConnection", "ConnectionString".
    /// </summary>
    public static string NormalizeResourceName(string rawName, string engine)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return NormalizeEngineName(engine);

        var trimmed = rawName.Trim();
        var isEngineGeneric = string.IsNullOrWhiteSpace(engine) ||
                              IsGenericConfigKey(engine) ||
                              engine.Equals("relational", StringComparison.OrdinalIgnoreCase) ||
                              engine.Equals("keyvalue", StringComparison.OrdinalIgnoreCase) ||
                              engine.Equals("document", StringComparison.OrdinalIgnoreCase) ||
                              engine.Equals("search", StringComparison.OrdinalIgnoreCase) ||
                              engine.Equals("graph", StringComparison.OrdinalIgnoreCase) ||
                              engine.Equals("analytics", StringComparison.OrdinalIgnoreCase) ||
                              engine.Equals("nosql", StringComparison.OrdinalIgnoreCase);

        var safeEngine = isEngineGeneric ? "Database" : engine;

        var lower = trimmed.ToLowerInvariant();

        if (IsGenericConfigKey(lower))
        {
            return safeEngine;
        }

        if (trimmed.Contains('.'))
        {
            var dotParts = trimmed.Split('.', 2);
            var eng = NormalizeEngineName(dotParts[0]);
            var sch = dotParts[1].Trim();
            if (!string.IsNullOrEmpty(sch) && !IsGenericConfigKey(sch))
            {
                var strippedSch = StripDatabaseSuffix(sch);
                var full = $"{eng}.{strippedSch}";
                return WorkspaceConventions.TryGetDatabaseAlias(full, out var dbAlias) ? dbAlias : full;
            }
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
            return safeEngine;
        }

        return trimmed;
    }

    public static bool IsEngineDbName(string lower)
    {
        return lower is "mongodb" or "cosmosdb" or "dynamodb" or "couchdb" or "mariadb" or 
                        "influxdb" or "duckdb" or "leveldb" or "rocksdb" or "alloydb" or "tidb";
    }

    public static string StripDatabaseSuffix(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        var trimmed = name.Trim();
        var lower = trimmed.ToLowerInvariant();

        if (IsEngineDbName(lower))
        {
            return trimmed;
        }

        if (lower.EndsWith("_database") && trimmed.Length > 9 && (trimmed.Length - 9) >= 3)
        {
            trimmed = trimmed[..^9].TrimEnd('_', '-');
        }
        else if (lower.EndsWith("database") && trimmed.Length > 8 && (trimmed.Length - 8) >= 3)
        {
            trimmed = trimmed[..^8].TrimEnd('_', '-');
        }
        else if (lower.EndsWith("_db") && trimmed.Length > 3 && (trimmed.Length - 3) >= 3)
        {
            trimmed = trimmed[..^3].TrimEnd('_', '-');
        }
        else if (lower.EndsWith("-db") && trimmed.Length > 3 && (trimmed.Length - 3) >= 3)
        {
            trimmed = trimmed[..^3].TrimEnd('_', '-');
        }
        else if (lower.EndsWith("db") && trimmed.Length > 2 && (trimmed.Length - 2) >= 3)
        {
            if (!IsEngineDbName(lower))
            {
                trimmed = trimmed[..^2].TrimEnd('_', '-');
            }
        }

        return trimmed;
    }

    public static string NormalizeEngineName(string? engine)
    {
        var lower = (engine ?? "").Trim().ToLowerInvariant();
        return lower switch
        {
            "postgres" or "postgresql" or "npgsql" or "pg" => "PostgreSQL",
            "mysql" or "mysql2" => "MySQL",
            "mariadb" => "MariaDB",
            "sqlite" or "sqlite3" => "SQLite",
            "mssql" or "sqlserver" or "sql server" or "tedious" => "SQL Server",
            "oracle" or "oracledb" => "Oracle",
            "mongodb" or "mongo" => "MongoDB",
            "redis" or "ioredis" => "Redis",
            "clickhouse" => "ClickHouse",
            "bigquery" => "BigQuery",
            "cassandra" => "Cassandra",
            "elasticsearch" => "Elasticsearch",
            "neo4j" => "Neo4j",
            _ => string.IsNullOrWhiteSpace(engine) || IsGenericConfigKey(lower) ? "PostgreSQL" : engine.Trim()
        };
    }

    public static bool IsGenericConfigKey(string key)
    {
        var lower = (key ?? "").Trim().ToLowerInvariant();
        return lower is "defaultconnection" or "connectionstring" or "connectionstrings" or
               "database" or "db" or "datasource" or "main" or "default" or
               "relational" or "keyvalue" or "document" or
               "spring.datasource.url" or "spring-datasource" or "database_url" or
               "typeorm" or "ef-core" or "efcore" or "ef core" or "microsoft.entityframeworkcore" or "dapper" or
               "prisma" or "sequelize" or "drizzle" or "hibernate" or "sqlalchemy" or
               "peewee" or "gorm" or "jpa" or "jdbctemplate" or "knex" ||
               lower.Contains("jpa") || lower.Contains("hibernate") || lower.Contains("spring data");
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

    public static bool IsKnownDatabaseEngine(string? engine)
    {
        var lower = (engine ?? "").Trim().ToLowerInvariant();
        return lower is "postgres" or "postgresql" or "npgsql" or "pg" or
               "mysql" or "mysql2" or "mariadb" or
               "sqlite" or "sqlite3" or
               "mssql" or "sqlserver" or "sql server" or "tedious" or
               "oracle" or "oracledb" or
               "mongodb" or "mongo" or
               "redis" or "ioredis" or
               "clickhouse" or "bigquery" or "cassandra" or "elasticsearch" or "neo4j";
    }

    public static bool IsDisallowedDatabaseAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return true;
        var lower = alias.Trim().ToLowerInvariant();
        return IsKnownDatabaseEngine(lower) ||
               lower is "typeorm" or "ef-core" or "microsoft.entityframeworkcore" or "dapper" or
               "prisma" or "sequelize" or "drizzle" or "hibernate" or "sqlalchemy" or
               "peewee" or "gorm" or "jpa" or "jdbctemplate" or "knex" or
               "database" or "db" or "datasource" or "relational" or "keyvalue" or "document" or
               "public" or "dbo" or "main" or "default" ||
               lower.Contains("jpa") || lower.Contains("hibernate") || lower.Contains("spring data");
    }
}
