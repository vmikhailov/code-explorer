using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Analysis;

public partial class ResourceReconciliationService
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
            if (NestedSqlParser.IsSqlKeyword(trimmedPart)) return false;
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

    private static readonly Regex ScopeSuffixRegex = new(
        @"(?:[-_]+service|[-_]+api|[-_]+srv|[-_]+app|[-_]+worker)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> TechnicalLayerSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "logic", "graphql", "grapql", "contracts", "contract", "services", "service",
        "host", "api", "client", "tests", "test", "grpc", "common", "infrastructure", "infra",
        "repository", "repositories", "core", "models", "dto"
    };

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
