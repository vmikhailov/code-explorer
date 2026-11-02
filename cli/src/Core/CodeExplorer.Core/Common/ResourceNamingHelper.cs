using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Layer-neutral helpers for resource naming, engine normalization,
/// configuration key inspection, and table/procedure identifier sanitization.
/// </summary>
public static class ResourceNamingHelper
{
    public static readonly HashSet<string> KnownDbEnginesAndNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgresql", "postgres", "mysql", "mariadb", "sqlite", "sqlite3",
        "clickhouse", "bigquery", "redis", "mongodb", "mongo",
        "sqlserver", "mssql", "sql server", "oracle", "cassandra",
        "elasticsearch", "neo4j", "dynamodb", "firestore", "cosmosdb",
        "couchdb", "cockroachdb", "tidb", "influxdb", "timescaledb",
        "memcached", "kafka", "rabbitmq", "database", "db", "default",
        "typeorm", "sequelize", "prisma", "drizzle", "dapper", "ef-core"
    };

    public static readonly HashSet<string> AbstractEngineNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgresql", "postgres", "sql server", "sqlserver", "mssql", "mysql", "mariadb", "sqlite",
        "mongodb", "mongo", "oracle", "database", "db", "default", "relational", "redis", "cache",
        "ef-core", "dapper", "typeorm", "prisma", "hibernate", "nhibernate", "sequelize", "drizzle"
    };

    public static readonly HashSet<string> SystemCatalogTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "pg_database", "pg_class", "pg_namespace", "pg_tables", "pg_views", "pg_matviews", "pg_indexes",
        "pg_attribute", "pg_type", "pg_proc", "pg_locks", "pg_settings", "pg_roles", "pg_user",
        "pg_constraint", "pg_depend", "pg_description", "pg_index", "pg_am", "pg_attrdef",
        "pg_authid", "pg_auth_members", "pg_extension", "pg_inherits", "pg_language", "pg_operator",
        "pg_opclass", "pg_rewrite", "pg_sequence", "pg_shdepend", "pg_statistic", "pg_tablespace",
        "pg_trigger", "dual"
    };

    public static readonly HashSet<string> KnownTableValuedFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "json_each", "json_tree", "json_extract", "string_split", "openjson", "opendatasource",
        "openquery", "openrowset", "unnest", "flatten", "generate_series", "range", "values",
        "external_query", "read_csv", "read_parquet"
    };

    private static readonly Regex ScopeSuffixRegex = new(
        @"(?:[-_]+service|[-_]+api|[-_]+srv|[-_]+app|[-_]+worker)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> TechnicalLayerSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "logic", "graphql", "grapql", "contracts", "contract", "services", "service",
        "host", "api", "client", "tests", "test", "grpc", "common", "infrastructure", "infra",
        "repository", "repositories", "core", "models", "dto"
    };

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

    public static bool IsAbstractEngineOnlyDatabase(string? name, string? schema, string? dbType, string? id)
    {
        if (!string.IsNullOrEmpty(name))
        {
            var trimmedName = name.Trim();
            if (AbstractEngineNames.Contains(trimmedName)) return true;
        }

        if (!string.IsNullOrEmpty(schema))
        {
            var trimmedSchema = schema.Trim();
            if (trimmedSchema.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                AbstractEngineNames.Contains(trimmedSchema))
            {
                return true;
            }
        }

        if (!string.IsNullOrEmpty(id))
        {
            if (id.EndsWith(":relational:postgresql", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(":relational:sqlserver", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(":relational:mysql", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(":relational:sqlite", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(":relational:database", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(":document:mongodb", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(":cache:redis", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(":keyvalue:redis", StringComparison.OrdinalIgnoreCase) ||
                id.EndsWith(":default:default", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    public static bool IsValidTableIdentifier(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var clean = name.Trim('`', '"', '\'', '[', ']').Trim();
        if (clean.Length < 1 || clean.Length > 128) return false;
        if (clean.Contains(' ') || clean.Contains('\t') || clean.Contains('\n') || clean.Contains('\r')) return false;

        if (clean.StartsWith('#') || clean.StartsWith('@') || clean.EndsWith('_'))
        {
            return false;
        }

        var parts = clean.Split('.');
        foreach (var p in parts)
        {
            var trimmedPart = p.Trim('`', '"', '\'', '[', ']').Trim();
            if (string.IsNullOrWhiteSpace(trimmedPart)) return false;
            if (trimmedPart.Equals("pg_catalog", StringComparison.OrdinalIgnoreCase) ||
                trimmedPart.Equals("information_schema", StringComparison.OrdinalIgnoreCase) ||
                trimmedPart.Equals("sys", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (IsSqlKeyword(trimmedPart)) return false;
            if (!Regex.IsMatch(trimmedPart, @"^[a-zA-Z_][a-zA-Z0-9_\*]*$")) return false;
        }

        var tableName = parts[^1].Trim('`', '"', '\'', '[', ']').Trim().ToLowerInvariant();
        if (tableName.StartsWith("temp_", StringComparison.OrdinalIgnoreCase) ||
            tableName.StartsWith("tmp_", StringComparison.OrdinalIgnoreCase) ||
            tableName.StartsWith("pg_stat_", StringComparison.OrdinalIgnoreCase) ||
            tableName.StartsWith("pg_toast", StringComparison.OrdinalIgnoreCase) ||
            tableName.StartsWith("sqlite_", StringComparison.OrdinalIgnoreCase) ||
            SystemCatalogTables.Contains(tableName) ||
            KnownTableValuedFunctions.Contains(tableName))
        {
            return false;
        }

        return true;
    }

    public static bool IsSqlKeyword(string word)
    {
        var lower = word.ToLowerInvariant();
        return lower is "select" or "from" or "where" or "insert" or "into" or "update" or "set" or
               "delete" or "join" or "left" or "right" or "inner" or "outer" or "cross" or
               "group" or "by" or "order" or "having" or "limit" or "offset" or
               "union" or "all" or "distinct" or "as" or "on" or "and" or "or" or "not" or
               "null" or "is" or "in" or "exists" or "between" or "like" or "case" or "when" or
               "then" or "else" or "end" or "create" or "alter" or "drop" or "table" or
               "index" or "view" or "database" or "schema" or "values" or "with";
    }

    public static string CleanProjectNameToDomain(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName)) return string.Empty;
        var pName = projectName.Trim();

        var parts = pName.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (parts.Count > 1 && TechnicalLayerSuffixes.Contains(parts[^1]))
        {
            parts.RemoveAt(parts.Count - 1);
        }

        var servicesIdx = parts.FindLastIndex(p => p.Equals("services", StringComparison.OrdinalIgnoreCase) || p.Equals("service", StringComparison.OrdinalIgnoreCase));
        string segment;
        if (parts.Count >= 2 && (parts[^1].Equals("Bff", StringComparison.OrdinalIgnoreCase) || parts[^1].Equals("Bffs", StringComparison.OrdinalIgnoreCase)))
        {
            segment = parts[^2] + "-" + parts[^1];
        }
        else if (servicesIdx >= 0 && servicesIdx + 1 < parts.Count)
        {
            segment = parts[servicesIdx + 1];
        }
        else
        {
            segment = parts[^1];
        }

        var cleaned = WorkspaceConventions.NormalizeServiceName(segment);
        if (!cleaned.EndsWith("-bff", StringComparison.OrdinalIgnoreCase) && !cleaned.EndsWith("bff", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = ScopeSuffixRegex.Replace(cleaned, "");
        }
        cleaned = Regex.Replace(cleaned.ToLowerInvariant(), @"[-_]{2,}", "-").Trim('_', '-');
        return cleaned;
    }
}
