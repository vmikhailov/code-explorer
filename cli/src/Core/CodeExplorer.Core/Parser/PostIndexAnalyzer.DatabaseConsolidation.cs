using System.Text.RegularExpressions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
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

    public static bool IsValidTableIdentifier(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var clean = name.Trim('`', '"', '\'', '[', ']').Trim();
        if (clean.Length < 1 || clean.Length > 128) return false;
        if (clean.Contains(' ') || clean.Contains('\t') || clean.Contains('\n') || clean.Contains('\r')) return false;
        var parts = clean.Split('.');
        foreach (var p in parts)
        {
            var trimmedPart = p.Trim('`', '"', '\'', '[', ']').Trim();
            if (string.IsNullOrWhiteSpace(trimmedPart)) return false;
            if (NestedSqlParser.IsSqlKeyword(trimmedPart)) return false;
            if (!Regex.IsMatch(trimmedPart, @"^[a-zA-Z_][a-zA-Z0-9_\*]*$")) return false;
        }
        return true;
    }

    public static bool IsLikelyDatabaseName(string? name, string? engine = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var trimmed = name.Trim();
        var lower = trimmed.ToLowerInvariant();
        if (lower.Length <= 1) return false;
        if (char.IsDigit(lower[0])) return false;
        
        // Reject SQL keywords, stopwords, functions, and PostgreSQL system catalogs
        if (NestedSqlParser.IsSqlKeyword(lower)) return false;
        if (lower.StartsWith("pg_") || lower.StartsWith("information_schema") || lower == "sys" || lower == "guest") return false;

        // <Technology>.<Schema> pattern (e.g. PostgreSQL.tournament, PostgreSQL.tree_rules)
        if (trimmed.Contains('.'))
        {
            var parts = trimmed.Split('.', 2);
            var tech = parts[0].ToLowerInvariant();
            var schema = parts[1].ToLowerInvariant();

            // Reject system schemas in <Technology>.<Schema>
            if (schema == "information_schema" || schema.StartsWith("pg_") || schema == "sys" || schema == "guest")
            {
                return false;
            }

            if (KnownDbEnginesAndNames.Contains(tech))
            {
                if (!IsValidSchemaIdentifier(schema))
                {
                    return false;
                }
                return true;
            }
        }

        // If a concrete known database engine is associated (e.g. PostgreSQL, MySQL, Redis, Mongo),
        // and name is not a generic config token, recognize it as a valid database even with custom project name (e.g. "Lightning")
        if (!string.IsNullOrWhiteSpace(engine) &&
            !engine.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
            !engine.Equals("default", StringComparison.OrdinalIgnoreCase) &&
            KnownDbEnginesAndNames.Contains(engine.ToLowerInvariant()))
        {
            if (!ResourceReconciliationService.IsGenericConfigKey(lower))
            {
                return true;
            }
        }

        // Matches known engine names or aliases
        if (KnownDbEnginesAndNames.Contains(lower)) return true;
        if (lower.StartsWith("redis_") || lower.StartsWith("postgres_") || lower.StartsWith("db_")) return true;
        if (lower.EndsWith("_db") || lower.EndsWith("-db") || lower.EndsWith("database")) return true;

        // Reject if it contains table or function indicators
        if (lower.Contains("_tb") || lower.Contains("_table") || lower.Contains("_history") || 
            lower.Contains("_rules") || lower.Contains("jsonb_") || lower.Contains("dblink")) return false;

        return false;
    }

    public static bool IsValidSchemaIdentifier(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema)) return false;
        var clean = schema.Trim('\'', '"', '`', ' ', ';');
        if (clean.Length < 2 || clean.Length > 64) return false;
        if (clean.Contains('\n') || clean.Contains('\r') || clean.Contains(';') ||
            clean.Contains('(') || clean.Contains(')') || clean.Contains(' ') ||
            clean.Contains(',') || clean.Contains('=') || clean.Contains('[') || clean.Contains(']'))
        {
            return false;
        }
        if (clean.Equals("default", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("dbo", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("public", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("main", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (NestedSqlParser.IsSqlKeyword(clean)) return false;
        if (clean.EndsWith("Schema", StringComparison.OrdinalIgnoreCase) &&
            !clean.Equals("schema", StringComparison.OrdinalIgnoreCase) &&
            !clean.Equals("public", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return Regex.IsMatch(clean, @"^[a-zA-Z_][a-zA-Z0-9_-]*$");
    }

    public static (string CanonicalName, string CanonicalType, string CanonicalKey) CanonicalizeDatabase(
        string rawName,
        string? rawDbType,
        string? rawEngine = null,
        string? rawSchema = null,
        string? fallbackEngine = null)
    {
        var trimmed = (rawName ?? "").Trim();
        var type = string.IsNullOrWhiteSpace(rawDbType) ? "relational" : rawDbType.Trim().ToLowerInvariant();

        // If rawEngine is generic/default and fallbackEngine is provided, use fallbackEngine
        var effectiveRawEngine = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase) && !rawEngine.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? rawEngine
            : fallbackEngine;

        // 1. If rawName contains '.', it is already in <Technology>.<Schema> format (e.g. PostgreSQL.tournament or Database.tree_rules)
        if (trimmed.Contains('.'))
        {
            var dotParts = trimmed.Split('.', 2);
            var techPrefix = dotParts[0].Trim();
            if ((techPrefix.Equals("Database", StringComparison.OrdinalIgnoreCase) || techPrefix.Equals("default", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(effectiveRawEngine))
            {
                techPrefix = effectiveRawEngine;
            }

            var (techName, techType, techKey) = CanonicalizeTechnologyOnly(techPrefix, type);
            var schemaPart = dotParts[1].Trim();
            schemaPart = ResourceReconciliationService.StripDatabaseSuffix(WorkspaceConventions.NormalizeServiceName(schemaPart));
            schemaPart = schemaPart.Replace("--", "-").Trim('_', '-');

            if (schemaPart.Equals("dbo", StringComparison.OrdinalIgnoreCase) && techName.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                schemaPart = "public";
            }
            if ((schemaPart.Equals("dbo", StringComparison.OrdinalIgnoreCase) || schemaPart.Equals("default", StringComparison.OrdinalIgnoreCase)) && techName.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
            {
                schemaPart = "main";
            }

            var schemaClean = Regex.Replace(schemaPart.ToLowerInvariant(), @"[^a-z0-9_-]", "_").Trim('_');
            if (string.IsNullOrEmpty(schemaClean))
            {
                schemaClean = GetDefaultSchemaForEngine(techName, techType);
                schemaPart = schemaClean;
            }

            return ($"{techName}.{schemaPart}", techType, $"{techKey}:{schemaClean}");
        }

        // 2. If an explicit rawSchema is provided and valid
        if (!string.IsNullOrWhiteSpace(rawSchema) && 
            !rawSchema.Equals("database", StringComparison.OrdinalIgnoreCase) &&
            IsValidSchemaIdentifier(rawSchema))
        {
            var effectiveTech = !string.IsNullOrWhiteSpace(effectiveRawEngine)
                ? effectiveRawEngine
                : trimmed;
            if ((effectiveTech.Equals("Database", StringComparison.OrdinalIgnoreCase) || effectiveTech.Equals("default", StringComparison.OrdinalIgnoreCase)) &&
                !string.IsNullOrWhiteSpace(fallbackEngine))
            {
                effectiveTech = fallbackEngine;
            }

            var (techName, techType, techKey) = CanonicalizeTechnologyOnly(effectiveTech, type);
            var schemaPart = rawSchema.Trim();
            schemaPart = ResourceReconciliationService.StripDatabaseSuffix(WorkspaceConventions.NormalizeServiceName(schemaPart));
            schemaPart = schemaPart.Replace("--", "-").Trim('_', '-');

            if (schemaPart.Equals("dbo", StringComparison.OrdinalIgnoreCase) && techName.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                schemaPart = "public";
            }
            if ((schemaPart.Equals("dbo", StringComparison.OrdinalIgnoreCase) || schemaPart.Equals("default", StringComparison.OrdinalIgnoreCase)) && techName.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
            {
                schemaPart = "main";
            }

            var schemaClean = Regex.Replace(schemaPart.ToLowerInvariant(), @"[^a-z0-9_-]", "_").Trim('_');
            if (string.IsNullOrEmpty(schemaClean)) schemaClean = GetDefaultSchemaForEngine(techName, techType);
            return ($"{techName}.{schemaPart}", techType, $"{techKey}:{schemaClean}");
        }

        // 3. Fallback when rawName is "Database" or "default" without schema
        if ((trimmed.Equals("Database", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("default", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(effectiveRawEngine))
        {
            var (techName, techType, techKey) = CanonicalizeTechnologyOnly(effectiveRawEngine, type);
            var defaultSchema = GetDefaultSchemaForEngine(techName, techType);
            var schemaClean = Regex.Replace(defaultSchema.ToLowerInvariant(), @"[^a-z0-9_-]", "_").Trim('_');
            return ($"{techName}.{defaultSchema}", techType, $"{techKey}:{schemaClean}");
        }

        // 4. Config-created DB node: rawName is a catalog/schema (e.g. "customers") and explicit rawEngine is a known engine (e.g. "PostgreSQL", "MongoDB")
        var explicitEngine = !string.IsNullOrWhiteSpace(rawEngine) &&
                             !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
                             !rawEngine.Equals("default", StringComparison.OrdinalIgnoreCase)
            ? rawEngine
            : null;

        if (!string.IsNullOrWhiteSpace(explicitEngine) &&
            !trimmed.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Equals("default", StringComparison.OrdinalIgnoreCase) &&
            IsValidSchemaIdentifier(trimmed))
        {
            var (techName, techType, techKey) = CanonicalizeTechnologyOnly(explicitEngine, type);
            if (!trimmed.Equals(techName, StringComparison.OrdinalIgnoreCase) &&
                !trimmed.Equals(techKey, StringComparison.OrdinalIgnoreCase) &&
                !KnownDbEnginesAndNames.Contains(trimmed.ToLowerInvariant()))
            {
                var schemaPart = ResourceReconciliationService.StripDatabaseSuffix(WorkspaceConventions.NormalizeServiceName(trimmed));
                schemaPart = schemaPart.Replace("--", "-").Trim('_', '-');

                if (schemaPart.Equals("dbo", StringComparison.OrdinalIgnoreCase) && techName.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                {
                    schemaPart = "public";
                }
                if ((schemaPart.Equals("dbo", StringComparison.OrdinalIgnoreCase) || schemaPart.Equals("default", StringComparison.OrdinalIgnoreCase)) && techName.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
                {
                    schemaPart = "main";
                }

                var schemaClean = Regex.Replace(schemaPart.ToLowerInvariant(), @"[^a-z0-9_-]", "_").Trim('_');
                if (string.IsNullOrEmpty(schemaClean)) schemaClean = GetDefaultSchemaForEngine(techName, techType);
                return ($"{techName}.{schemaPart}", techType, $"{techKey}:{schemaClean}");
            }
        }

        return CanonicalizeTechnologyOnly(trimmed, type);
    }

    private static string GetDefaultSchemaForEngine(string engine, string dbType)
    {
        var lower = engine.ToLowerInvariant();
        if (lower.Contains("postgres")) return "public";
        if (lower.Contains("sqlserver") || lower.Contains("sql server") || lower.Contains("mssql")) return "dbo";
        if (lower.Contains("sqlite")) return "main";
        if (lower.Contains("redis")) return "cache";
        if (lower.Contains("mongo")) return "default";
        if (lower.Contains("bigquery")) return "default";
        if (dbType.Equals("cache", StringComparison.OrdinalIgnoreCase)) return "cache";
        return "public";
    }

    public static bool IsRelationalEngine(string? engine)
    {
        if (string.IsNullOrWhiteSpace(engine)) return false;
        var lower = engine.ToLowerInvariant();
        return lower.Contains("postgres") ||
               lower.Contains("mysql") ||
               lower.Contains("mariadb") ||
               lower.Contains("sqlserver") || lower.Contains("sql server") || lower.Contains("mssql") ||
               lower.Contains("sqlite") ||
               lower.Contains("oracle") ||
               lower.Contains("clickhouse") ||
               lower.Contains("bigquery");
    }

    private static (string CanonicalName, string CanonicalType, string CanonicalKey) CanonicalizeTechnologyOnly(string rawName, string rawDbType)
    {
        var trimmed = (rawName ?? "").Trim();
        var lower = trimmed.ToLowerInvariant();
        var type = string.IsNullOrWhiteSpace(rawDbType) ? "relational" : rawDbType.Trim().ToLowerInvariant();

        switch (lower)
        {
            case "database":
            case "db":
            case "typeorm":
            case "microsoft.entityframeworkcore":
            case "entity framework core":
            case "ef core":
            case "efcore":
            case "ef-core":
            case "dapper":
            case "sequelize":
            case "prisma":
            case "drizzle":
            case "drizzle orm":
            case "defaultconnection":
                return ("Database", "relational", "database");
            case "postgres":
            case "postgresql":
            case "npgsql":
            case "pg":
                return ("PostgreSQL", "relational", "postgresql");
            case "redis":
            case "ioredis":
                return ("Redis", "cache", "redis");
            case "mysql":
            case "mysql2":
                return ("MySQL", "relational", "mysql");
            case "mariadb":
                return ("MariaDB", "relational", "mariadb");
            case "sqlite":
            case "sqlite3":
                return ("SQLite", "relational", "sqlite");
            case "mongodb":
            case "mongo":
                return ("MongoDB", "document", "mongodb");
            case "clickhouse":
                return ("ClickHouse", "analytics", "clickhouse");
            case "bigquery":
                return ("BigQuery", "analytics", "bigquery");
            case "mssql":
            case "sqlserver":
            case "sql server":
            case "tedious":
                return ("SQL Server", "relational", "sqlserver");
            case "oracle":
            case "oracledb":
                return ("Oracle", "relational", "oracle");
            case "cassandra":
                return ("Cassandra", "nosql", "cassandra");
            case "elasticsearch":
                return ("Elasticsearch", "search", "elasticsearch");
            case "neo4j":
                return ("Neo4j", "graph", "neo4j");
            default:
                if (lower.StartsWith("redis_"))
                {
                    return ("Redis", "cache", "redis");
                }
                var normName = ResourceReconciliationService.NormalizeResourceName(trimmed, type);
                var canonicalKey = Regex.Replace(normName.ToLowerInvariant(), @"[^a-z0-9_-]", "_").Trim('_');
                if (string.IsNullOrEmpty(canonicalKey)) canonicalKey = "db";
                return (normName, type, canonicalKey);
        }
    }

    public static string BuildCanonicalDatabaseId(string? rawId, string cType, string cKey, string? defaultWorkspaceId = null)
    {
        var wid = OntologyConstants.IdPrefixes.Workspace;
        if (!string.IsNullOrEmpty(rawId) && rawId.Contains(':'))
        {
            var firstPart = rawId.Split(':')[0];
            if (!string.IsNullOrWhiteSpace(firstPart))
            {
                wid = firstPart;
            }
        }
        else if (!string.IsNullOrEmpty(defaultWorkspaceId))
        {
            var trimmed = defaultWorkspaceId.TrimEnd(':');
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                wid = trimmed;
            }
        }

        return $"{wid}:{OntologyConstants.IdPrefixes.Database}:{cType.ToLowerInvariant()}:{cKey.ToLowerInvariant()}";
    }

    public static Dictionary<string, string> ResolveDatabaseConsolidations(
        IEnumerable<(string Id, string Name, string Engine, string Schema)> candidateNodes)
    {
        var remap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nodesByEngine = candidateNodes
            .GroupBy(n => n.Engine, StringComparer.OrdinalIgnoreCase);

        foreach (var group in nodesByEngine)
        {
            var list = group.DistinctBy(n => n.Id).ToList();
            if (list.Count <= 1) continue;

            // 1. Check equivalent schema / catalog pairs (stem vs stem+db, singular vs plural, etc.)
            for (int i = 0; i < list.Count; i++)
            {
                var n1 = list[i];
                var s1 = n1.Schema;
                if (string.IsNullOrEmpty(s1) && n1.Name.Contains('.')) s1 = n1.Name.Split('.', 2)[1];

                for (int j = 0; j < list.Count; j++)
                {
                    if (i == j) continue;
                    var n2 = list[j];
                    var s2 = n2.Schema;
                    if (string.IsNullOrEmpty(s2) && n2.Name.Contains('.')) s2 = n2.Name.Split('.', 2)[1];

                    if (!string.IsNullOrEmpty(s1) && !string.IsNullOrEmpty(s2))
                    {
                        var s1Clean = s1.Trim().ToLowerInvariant();
                        var s2Clean = s2.Trim().ToLowerInvariant();

                        var s1Stem = ResourceReconciliationService.StripDatabaseSuffix(s1Clean);
                        var s2Stem = ResourceReconciliationService.StripDatabaseSuffix(s2Clean);

                        var s1Singular = s1Stem.EndsWith("es") ? s1Stem[..^2] : (s1Stem.EndsWith("s") ? s1Stem[..^1] : s1Stem);
                        var s2Singular = s2Stem.EndsWith("es") ? s2Stem[..^2] : (s2Stem.EndsWith("s") ? s2Stem[..^1] : s2Stem);

                        if (s1Singular.Equals(s2Singular, StringComparison.OrdinalIgnoreCase))
                        {
                            var hasSuffix1 = !s1Clean.Equals(s1Stem, StringComparison.OrdinalIgnoreCase);
                            var hasSuffix2 = !s2Clean.Equals(s2Stem, StringComparison.OrdinalIgnoreCase);

                            if (hasSuffix2 && !hasSuffix1)
                            {
                                // n2 has db suffix, n1 is clean stem -> map n2 -> n1
                                remap[n2.Id] = n1.Id;
                            }
                            else if (hasSuffix1 && !hasSuffix2)
                            {
                                // n1 has db suffix, n2 is clean stem -> map n1 -> n2
                                remap[n1.Id] = n2.Id;
                            }
                            else if (s2Clean.Equals(s1Clean + "s", StringComparison.OrdinalIgnoreCase) ||
                                     s2Clean.Equals(s1Clean + "es", StringComparison.OrdinalIgnoreCase))
                            {
                                // If s2 is plural of s1 (s1 + "s" or s1 + "es"), map n1 (singular) -> n2 (plural)
                                remap[n1.Id] = n2.Id;
                            }
                        }
                    }
                }
            }

            // 2. Generic engine-only node alongside concrete schema node
            var concreteNodes = list.Where(n => {
                var s = n.Schema;
                if (string.IsNullOrEmpty(s) && n.Name.Contains('.')) s = n.Name.Split('.', 2)[1];
                return !string.IsNullOrWhiteSpace(s) && !s.Equals("default", StringComparison.OrdinalIgnoreCase);
            }).ToList();

            if (concreteNodes.Count > 0)
            {
                // Prefer a clean stem without redundant db suffix, or plural / longest schema concrete node
                var winner = concreteNodes
                    .OrderBy(n => {
                        var s = n.Schema;
                        if (string.IsNullOrEmpty(s) && n.Name.Contains('.')) s = n.Name.Split('.', 2)[1];
                        var stem = ResourceReconciliationService.StripDatabaseSuffix(s);
                        return !s.Equals(stem, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    })
                    .ThenByDescending(n => {
                        var s = n.Schema;
                        if (string.IsNullOrEmpty(s) && n.Name.Contains('.')) s = n.Name.Split('.', 2)[1];
                        return s.Length;
                    })
                    .First();

                while (remap.TryGetValue(winner.Id, out var nextWinner) && nextWinner != winner.Id)
                {
                    winner = list.First(n => n.Id == nextWinner);
                }

                var genericNodes = list.Where(n => {
                    var s = n.Schema;
                    if (string.IsNullOrEmpty(s) && n.Name.Contains('.')) s = n.Name.Split('.', 2)[1];
                    return string.IsNullOrWhiteSpace(s) || s.Equals("default", StringComparison.OrdinalIgnoreCase);
                }).ToList();

                foreach (var genNode in genericNodes)
                {
                    remap[genNode.Id] = winner.Id;
                }
            }
        }

        // Resolve transitive remappings
        foreach (var k in remap.Keys.ToList())
        {
            var target = remap[k];
            while (remap.TryGetValue(target, out var nextTarget) && nextTarget != target)
            {
                target = nextTarget;
            }
            remap[k] = target;
        }

        return remap;
    }
}
