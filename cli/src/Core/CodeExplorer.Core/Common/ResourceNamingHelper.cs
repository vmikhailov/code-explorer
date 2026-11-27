using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Layer-neutral helpers for resource naming, engine normalization,
/// configuration key inspection, and table/procedure identifier sanitization.
/// </summary>
public static class ResourceNamingHelper
{
    public static readonly HashSet<string> KnownDbEnginesAndNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgresql", "postgres", "mysql", "mariadb", "sqlite", "sqlite3", "h2",
        "clickhouse", "bigquery", "snowflake", "redis", "mongodb", "mongo",
        "sqlserver", "mssql", "sql server", "oracle", "cassandra",
        "elasticsearch", "neo4j", "dynamodb", "firestore", "cosmosdb",
        "couchdb", "cockroachdb", "tidb", "influxdb", "timescaledb",
        "memcached", "kafka", "rabbitmq", "database", "db", "default",
        "typeorm", "sequelize", "prisma", "drizzle", "dapper", "ef-core"
    };

    public static readonly HashSet<string> AbstractEngineNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgresql", "postgres", "sql server", "sqlserver", "mssql", "mysql", "mariadb", "sqlite", "h2",
        "mongodb", "mongo", "oracle", "snowflake", "database", "db", "default", "relational", "redis", "cache",
        "ef-core", "dapper", "typeorm", "prisma", "hibernate", "nhibernate", "sequelize", "drizzle"
    };

    public static readonly HashSet<string> SystemCatalogTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "pg_database", "pg_class", "pg_namespace", "pg_tables", "pg_views", "pg_matviews", "pg_indexes",
        "pg_attribute", "pg_type", "pg_proc", "pg_locks", "pg_settings", "pg_roles", "pg_user",
        "pg_constraint", "pg_depend", "pg_description", "pg_index", "pg_am", "pg_attrdef",
        "pg_authid", "pg_auth_members", "pg_extension", "pg_inherits", "pg_language", "pg_operator",
        "pg_opclass", "pg_rewrite", "pg_sequence", "pg_shdepend", "pg_statistic", "pg_tablespace",
        "pg_trigger", "dual",
        "columns", "tables", "schemata", "key_column_usage", "table_constraints",
        "referential_constraints", "check_constraints", "constraint_column_usage",
        "constraint_table_usage", "information_schema", "sysdatabases", "sysobjects", "syscolumns", "sysindexes"
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
        // Concrete KeyValue / Cache databases (like Redis) are valid database entities, not abstract relational placeholders
        if (string.Equals(dbType, "cache", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(dbType, "keyvalue", StringComparison.OrdinalIgnoreCase) ||
            (name != null && name.StartsWith("Redis", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

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
        switch (lower)
        {
            case "postgres" or "postgresql" or "npgsql" or "pg":
                return "PostgreSQL";
            case "mysql" or "mysql2":
                return "MySQL";
            case "mariadb":
                return "MariaDB";
            case "sqlite" or "sqlite3":
                return "SQLite";
            case "mssql" or "sqlserver" or "sql server" or "tedious":
                return "SQL Server";
            case "oracle" or "oracledb":
                return "Oracle";
            case "mongodb" or "mongo":
                return "MongoDB";
            case "redis" or "ioredis":
                return "Redis";
            case "clickhouse":
                return "ClickHouse";
            case "bigquery":
                return "BigQuery";
            case "snowflake":
                return "Snowflake";
            case "h2":
                return "H2";
            case "cassandra":
                return "Cassandra";
            case "elasticsearch":
                return "Elasticsearch";
            case "neo4j":
                return "Neo4j";
        }

        if (lower.StartsWith("postgres", StringComparison.Ordinal) || lower.Contains("pgx", StringComparison.Ordinal) || lower.Contains("npgsql", StringComparison.Ordinal) || lower.Contains("lib/pq", StringComparison.Ordinal))
            return "PostgreSQL";
        if (lower.Contains("snowflake", StringComparison.Ordinal))
            return "Snowflake";
        if (lower.Contains("sqlite", StringComparison.Ordinal))
            return "SQLite";
        if (lower.StartsWith("mariadb", StringComparison.Ordinal))
            return "MariaDB";
        if (lower.StartsWith("mysql", StringComparison.Ordinal))
            return "MySQL";
        if (lower.StartsWith("sql server", StringComparison.Ordinal) || lower.StartsWith("mssql", StringComparison.Ordinal) || lower.Contains("sqlclient", StringComparison.Ordinal))
            return "SQL Server";
        if (lower.StartsWith("clickhouse", StringComparison.Ordinal))
            return "ClickHouse";
        if (lower.StartsWith("mongodb", StringComparison.Ordinal))
            return "MongoDB";
        if (lower.StartsWith("redis", StringComparison.Ordinal))
            return "Redis";

        return string.IsNullOrWhiteSpace(engine) || IsGenericConfigKey(lower) ? "PostgreSQL" : engine.Trim();
    }

    private static readonly TimeSpan NamingRegexTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex IndexedPlaceholderDbNameRegex = new(
        @"^(?:project|db|test|item|tenant|schema|table|model)_\d+$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        NamingRegexTimeout);

    private static readonly Regex ValidTablePartCompiledRegex = new(
        @"^[a-zA-Z_][a-zA-Z0-9_\*]*$",
        RegexOptions.Compiled,
        NamingRegexTimeout);

    private static readonly HashSet<string> DisallowedSingleTableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "for", "com", "org", "net", "io", "edu", "gov",
        "old", "new", "self", "this", "cls", "src",
        "none", "true", "false", "pythonpath", "table",
        "attrs", "attr", "kwargs", "args", "props", "plugin", "plugins"
    };

    private static readonly string[] DisallowedCodeClassTableSuffixes =
    [
        "Plugin", "Plugins", "Middleware", "Exception", "Error", "Controller", "Handler", "Factory", "Provider"
    ];

    public static bool IsGenericConfigKey(string key)
    {
        var lower = (key ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(lower)) return true;
        if (IndexedPlaceholderDbNameRegex.IsMatch(lower)) return true;
        return lower is "defaultconnection" or "connectionstring" or "connectionstrings" or
               "database" or "db" or "datasource" or "main" or "default" or
               "relational" or "keyvalue" or "document" or
               "url" or "uri" or "src" or "self" or "cls" or "this" or
               "string" or "str" or "int" or "integer" or "bool" or "boolean" or "object" or "array" or
               "driver" or "driver-class-name" or "driver_class_name" or "driverclassname" or "dialect" or
               "username" or "user" or "password" or "pwd" or "host" or "hostname" or "port" or "ssl" or "sslmode" or
               "template-connection-url" or "template_connection_url" or "templateconnectionurl" or
               "connection-url" or "connection_url" or "connectionurl" or "jdbc-url" or "jdbc_url" or "jdbcurl" or
               "spring.datasource.url" or "spring-datasource" or "database_url" or
               "typeorm" or "ef-core" or "efcore" or "ef core" or "microsoft.entityframeworkcore" or "dapper" or
               "prisma" or "sequelize" or "drizzle" or "hibernate" or "sqlalchemy" or
               "peewee" or "gorm" or "jpa" or "jdbctemplate" or "knex" ||
               lower.Contains("jpa") || lower.Contains("hibernate") || lower.Contains("spring data") ||
               lower.Contains("template-connection") || lower.Contains("templateconnection") ||
               lower.EndsWith("driver-class-name", StringComparison.Ordinal);
    }

    public static string NormalizeAlias(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var clean = raw.Trim().ToLowerInvariant();
        if (clean.StartsWith("postgresql://") || clean.StartsWith("postgres://"))
            clean = clean[(clean.IndexOf("://") + 3)..];
        else if (clean.StartsWith("mysql://") || clean.StartsWith("redis://") || clean.StartsWith("mongodb://") || clean.StartsWith("amqp://") || clean.StartsWith("snowflake://"))
            clean = clean[(clean.IndexOf("://") + 3)..];

        return clean;
    }

    public static bool IsKnownDatabaseEngine(string? engine)
    {
        var lower = (engine ?? "").Trim().ToLowerInvariant();
        return lower is "postgres" or "postgresql" or "npgsql" or "pg" or
               "mysql" or "mysql2" or "mariadb" or
               "sqlite" or "sqlite3" or "h2" or
               "mssql" or "sqlserver" or "sql server" or "tedious" or
               "oracle" or "oracledb" or
               "mongodb" or "mongo" or
               "redis" or "ioredis" or
               "clickhouse" or "bigquery" or "snowflake" or "cassandra" or "elasticsearch" or "neo4j";
    }

    public static bool IsDisallowedDatabaseAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias)) return true;
        var lower = alias.Trim().ToLowerInvariant();
        return IsKnownDatabaseEngine(lower) ||
               IsGenericConfigKey(lower) ||
               lower is "typeorm" or "ef-core" or "microsoft.entityframeworkcore" or "dapper" or
               "prisma" or "sequelize" or "drizzle" or "hibernate" or "sqlalchemy" or
               "peewee" or "gorm" or "jpa" or "jdbctemplate" or "knex" or
               "database" or "db" or "datasource" or "relational" or "keyvalue" or "document" or
               "public" or "dbo" or "main" or "default" ||
               lower.Contains("jpa") || lower.Contains("hibernate") || lower.Contains("spring data");
    }

    public static bool IsSystemCatalogSchema(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema)) return false;
        var trimmed = schema.Trim().Trim('`', '"', '\'', '[', ']').Trim();
        return trimmed.Equals("information_schema", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("pg_catalog", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("sys", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidTableIdentifier(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.IndexOfAny(['\n', '\r', '\t']) >= 0) return false;

        var rawTrimmed = name.Trim();
        if (rawTrimmed.Contains('=') || rawTrimmed.Contains('{') || rawTrimmed.Contains('}') ||
            rawTrimmed.Contains('+') || rawTrimmed.Contains(':') || rawTrimmed.Contains('<') ||
            rawTrimmed.Contains('>') || rawTrimmed.Contains(';'))
        {
            return false;
        }

        var clean = rawTrimmed.Trim('`', '"', '\'', '[', ']').Trim();
        if (clean.Length <= 2 || clean.Length > 128) return false;
        if (clean.Contains(' ') || clean.Contains('\t') || clean.Contains('\n') || clean.Contains('\r') ||
            clean.Contains('"') || clean.Contains('\''))
        {
            return false;
        }

        if (clean.StartsWith('#') || clean.StartsWith('@') || clean.StartsWith('*') || clean.EndsWith('_'))
        {
            return false;
        }

        var parts = clean.Split('.');
        if (parts.Length == 1)
        {
            var singlePart = parts[0].Trim('`', '"', '\'', '[', ']').Trim();
            if (DisallowedSingleTableNames.Contains(singlePart))
            {
                return false;
            }
            for (var s = 0; s < DisallowedCodeClassTableSuffixes.Length; s++)
            {
                if (singlePart.Length > DisallowedCodeClassTableSuffixes[s].Length &&
                    singlePart.EndsWith(DisallowedCodeClassTableSuffixes[s], StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        foreach (var p in parts)
        {
            var trimmedPart = p.Trim('`', '"', '\'', '[', ']').Trim();
            if (string.IsNullOrWhiteSpace(trimmedPart)) return false;
            if (IsSystemCatalogSchema(trimmedPart))
            {
                return false;
            }
            if (IsSqlKeyword(trimmedPart)) return false;
            if (!ValidTablePartCompiledRegex.IsMatch(trimmedPart)) return false;
        }

        var rawLeaf = parts[^1].Trim('`', '"', '\'', '[', ']').Trim();
        if (rawLeaf.Length <= 2) return false;
        var tableName = rawLeaf.ToLowerInvariant();
        if (DisallowedSingleTableNames.Contains(tableName) ||
            tableName.StartsWith("temp_", StringComparison.OrdinalIgnoreCase) ||
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
               "index" or "view" or "database" or "schema" or "values" or "with" or "for";
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
