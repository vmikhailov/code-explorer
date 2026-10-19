using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    public Task<CanonicalizeDatabasesResult> CanonicalizeDatabasesAsync(string widPrefix, CancellationToken ct = default)
    {
        return CanonicalizeDatabasesAsync(db, widPrefix, ct);
    }

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

    public static async Task<(List<Relationship> CanonicalRels, Dictionary<string, string> RawToCanonicalMap)> CanonicalizeDatabasesInMemoryAsync(
        ParsingContext ctx,
        Layer4Result l4Result,
        List<Relationship> allRelationships,
        IReadOnlyDictionary<string, ProjectNode>? prebuiltFileToProject = null)
    {
        var rawToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var canonicalDbNodes = new Dictionary<string, DatabaseNode>(StringComparer.OrdinalIgnoreCase);

        // 1. Collect all DatabaseNodes from AST / Semantic tree
        var allDbNodes = new List<DatabaseNode>();
        void CollectDbNodes(IOntologyNode node)
        {
            if (node is DatabaseNode db)
            {
                var eng = (db.Extensions != null && db.Extensions.TryGetValue("engine", out var eVal) && !string.IsNullOrWhiteSpace(eVal))
                    ? eVal
                    : (db.Extensions != null && db.Extensions.TryGetValue("technology", out var tVal) ? tVal : null);
                if (IsLikelyDatabaseName(db.Name, eng) || IsLikelyDatabaseName(db.Id, eng))
                {
                    allDbNodes.Add(db);
                }
            }
            foreach (var child in node.Children)
            {
                CollectDbNodes(child);
            }
        }

        CollectDbNodes(l4Result.SemanticStructure);
        CollectDbNodes(l4Result.Prev.Prev.Prev.Workspace);
        
        foreach (var p in l4Result.Prev.Prev.Projects) CollectDbNodes(p);
        foreach (var f in l4Result.Prev.Prev.Prev.Files) CollectDbNodes(f);

        var projects = l4Result.Prev.Prev.Projects;
        var files = l4Result.Prev.Prev.Prev.Files;
        var fileToProject = prebuiltFileToProject != null
            ? (prebuiltFileToProject as Dictionary<string, ProjectNode> ?? new Dictionary<string, ProjectNode>(prebuiltFileToProject, StringComparer.OrdinalIgnoreCase))
            : Layer2ProjectParser.BuildFileToProjectMap(files, projects);

        bool IsTestSource(string fromId, string? fromPath)
        {
            if (!string.IsNullOrEmpty(fromPath) && WorkspaceConventions.IsTestFilePath(fromPath))
                return true;

            if (fileToProject.TryGetValue(fromId, out var p))
            {
                if (p.IsTest || string.Equals(p.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                    (p.Extensions != null && p.Extensions.TryGetValue("is_test", out var it) && (it == "true" || it == "1")) ||
                    WorkspaceConventions.IsTestFilePath(p.Path))
                {
                    return true;
                }
            }

            var matchingProj = projects.FirstOrDefault(pr => pr.Id == fromId || fromId.StartsWith(pr.Id, StringComparison.OrdinalIgnoreCase));
            if (matchingProj != null)
            {
                if (matchingProj.IsTest || string.Equals(matchingProj.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                    (matchingProj.Extensions != null && matchingProj.Extensions.TryGetValue("is_test", out var it) && (it == "true" || it == "1")) ||
                    WorkspaceConventions.IsTestFilePath(matchingProj.Path))
                {
                    return true;
                }
            }

            if (WorkspaceConventions.IsTestFilePath(fromId) ||
                fromId.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
                fromId.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) ||
                fromId.Contains(".Tests", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        var nonTestAccessedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in allRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.Contains)
                continue;

            var isDbOrTableRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                                 rel.Kind == OntologyConstants.Relationships.Configures ||
                                 rel.Kind == OntologyConstants.Relationships.ReadsFrom ||
                                 rel.Kind == OntologyConstants.Relationships.WritesTo ||
                                 rel.Kind == OntologyConstants.Relationships.DependsOn;
            if (!isDbOrTableRel) continue;

            if (!rel.To.Contains(":db:") && !rel.To.Contains(":database:") && !rel.To.Contains(":res:db:"))
                continue;

            var filePath = rel.Properties != null && rel.Properties.TryGetValue("file_path", out var fp) ? fp?.ToString() : null;
            if (!IsTestSource(rel.From, filePath))
            {
                nonTestAccessedTargets.Add(rel.To);
                var parts = rel.To.Split(':');
                var accum = "";
                for (int i = 0; i < parts.Length; i++)
                {
                    accum = i == 0 ? parts[i] : $"{accum}:{parts[i]}";
                    nonTestAccessedTargets.Add(accum);
                }
            }
        }

        var primaryRelationalEngine = allDbNodes
            .Where(d => {
                var isDeclaredInProduction = !string.IsNullOrEmpty(d.Path) && !WorkspaceConventions.IsTestFilePath(d.Path);
                var isExplicitTest = (d.Extensions != null && d.Extensions.TryGetValue("is_test", out var itVal) && (itVal == "true" || itVal == "1")) ||
                                     (d.Extensions != null && d.Extensions.TryGetValue("role", out var rVal) && rVal.Equals("Test", StringComparison.OrdinalIgnoreCase)) ||
                                     WorkspaceConventions.IsTestFilePath(d.Path);
                var hasNonTest = (!isExplicitTest && isDeclaredInProduction) ||
                                 nonTestAccessedTargets.Contains(d.Id) ||
                                 d.Children.Any(c => nonTestAccessedTargets.Contains(c.Id));
                return hasNonTest && (string.IsNullOrEmpty(d.DbType) || d.DbType.Equals("relational", StringComparison.OrdinalIgnoreCase));
            })
            .Select(d => {
                if (d.Extensions != null && d.Extensions.TryGetValue("engine", out var eng) && !string.IsNullOrWhiteSpace(eng))
                    return eng;
                if (!string.IsNullOrEmpty(d.Name) && d.Name.Contains('.'))
                    return d.Name.Split('.', 2)[0];
                return d.Name;
            })
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e) && 
                                 !e.Equals("Database", StringComparison.OrdinalIgnoreCase) && 
                                 !e.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                                 !ResourceReconciliationService.IsGenericConfigKey(e) &&
                                 IsRelationalEngine(e));

        foreach (var db in allDbNodes)
        {
            var isDeclaredInProduction = !string.IsNullOrEmpty(db.Path) && !WorkspaceConventions.IsTestFilePath(db.Path);
            var isExplicitTest = (db.Extensions != null && db.Extensions.TryGetValue("is_test", out var itVal) && (itVal == "true" || itVal == "1")) ||
                                 (db.Extensions != null && db.Extensions.TryGetValue("role", out var rVal) && rVal.Equals("Test", StringComparison.OrdinalIgnoreCase)) ||
                                 WorkspaceConventions.IsTestFilePath(db.Path);
            var hasNonTestAccess = (!isExplicitTest && isDeclaredInProduction) ||
                                   nonTestAccessedTargets.Contains(db.Id) ||
                                   db.Children.Any(c => nonTestAccessedTargets.Contains(c.Id));
            if (!hasNonTestAccess)
            {
                continue;
            }
            var rawEngine = (db.Extensions != null && db.Extensions.TryGetValue("engine", out var eng) && !string.IsNullOrWhiteSpace(eng))
                ? eng
                : null;
            var rawSchema = (db.Extensions != null && db.Extensions.TryGetValue("schema", out var sch) && !string.IsNullOrWhiteSpace(sch))
                ? sch
                : null;
            var (cName, cType, cKey) = CanonicalizeDatabase(db.Name, db.DbType, rawEngine, rawSchema, primaryRelationalEngine);
            var canonicalId = BuildCanonicalDatabaseId(db.Id, cType, cKey, ctx.WorkspaceId);
            rawToCanonical[db.Id] = canonicalId;

            var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                ? rawEngine
                : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);

            var schemaToUse = cName.Contains('.') ? cName.Split('.', 2)[1] : (rawSchema ?? "");

            if (!canonicalDbNodes.TryGetValue(canonicalId, out var existingNode) ||
                (existingNode.Extensions != null && existingNode.Extensions.TryGetValue("engine", out var exEng) && exEng.Equals(existingNode.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
            {
                var dispName = cName.Contains('.')
                    ? (cName.Equals("SQLite.main", StringComparison.OrdinalIgnoreCase) ? "SQLite" : cName)
                    : (!string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                        ? $"{cName} ({engineToUse}) [{cType}]"
                        : $"{cName} [{cType}]");

                var canonicalNode = new DatabaseNode(
                    canonicalId,
                    cName,
                    db.Path ?? "",
                    cType,
                    new Dictionary<string, string>
                    {
                        ["name"] = cName,
                        ["display_name"] = dispName,
                        ["db_type"] = cType,
                        ["role"] = "database",
                        ["engine"] = engineToUse,
                        ["technology"] = engineToUse,
                        ["schema"] = schemaToUse,
                        ["is_canonical"] = "true",
                        ["is_semantic_entity"] = "true",
                        ["layer"] = StandardLayers.Foundation.LayerId,
                        ["layerId"] = StandardLayers.Foundation.LayerId,
                        ["layerName"] = StandardLayers.Foundation.LayerName,
                        ["layerColor"] = StandardLayers.Foundation.Color,
                        ["layerIcon"] = StandardLayers.Foundation.Icon
                    }
                );
                canonicalDbNodes[canonicalId] = canonicalNode;
            }

            var targetCanonical = canonicalDbNodes[canonicalId];
            foreach (var child in db.Children)
            {
                if (!targetCanonical.Children.Any(c => c.Id == child.Id))
                {
                    targetCanonical.Children.Add(child);
                }
            }

            // Also map child DataSetNodes (representing schemas in SQL) to their canonical database nodes
            foreach (var ds in db.Children.OfType<DataSetNode>())
            {
                var (dsCName, dsCType, dsCKey) = CanonicalizeDatabase(db.Name, db.DbType, rawEngine, ds.Name, primaryRelationalEngine);
                var dsCanonicalId = BuildCanonicalDatabaseId(ds.Id, dsCType, dsCKey, ctx.WorkspaceId);
                rawToCanonical[ds.Id] = dsCanonicalId;

                var dsEngine = dsCName.Contains('.')
                    ? dsCName.Split('.', 2)[0]
                    : (!string.IsNullOrWhiteSpace(primaryRelationalEngine) ? primaryRelationalEngine : engineToUse);

                if (!canonicalDbNodes.ContainsKey(dsCanonicalId))
                {
                    var dsNode = new DatabaseNode(
                        dsCanonicalId,
                        dsCName,
                        ds.Path ?? "",
                        dsCType,
                        new Dictionary<string, string>
                        {
                            ["name"] = dsCName,
                            ["display_name"] = dsCName,
                            ["db_type"] = dsCType,
                            ["role"] = "database",
                            ["engine"] = dsEngine,
                            ["technology"] = dsEngine,
                            ["schema"] = ds.Name,
                            ["is_canonical"] = "true",
                            ["is_semantic_entity"] = "true",
                            ["layer"] = StandardLayers.Foundation.LayerId,
                            ["layerId"] = StandardLayers.Foundation.LayerId,
                            ["layerName"] = StandardLayers.Foundation.LayerName,
                            ["layerColor"] = StandardLayers.Foundation.Color,
                            ["layerIcon"] = StandardLayers.Foundation.Icon
                        }
                    );
                    canonicalDbNodes[dsCanonicalId] = dsNode;
                }
            }
        }

        // Also inspect all relationships for DB targets not caught above
        foreach (var rel in allRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.Contains)
                continue;

            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          rel.Kind == OntologyConstants.Relationships.ReadsFrom ||
                          rel.Kind == OntologyConstants.Relationships.WritesTo ||
                          rel.Kind == OntologyConstants.Relationships.DependsOn;
            if (!isDbRel && !rel.To.Contains(":db:") && !rel.To.Contains(":database:") && !rel.To.Contains(":res:db:"))
                continue;

            if (!rawToCanonical.ContainsKey(rel.To))
            {
                var filePath = rel.Properties != null && rel.Properties.TryGetValue("file_path", out var fp) ? fp?.ToString() : null;
                if (IsTestSource(rel.From, filePath)) continue;
                if (!nonTestAccessedTargets.Contains(rel.To)) continue;

                var parts = rel.To.Split(':');
                var rawName = parts.Length > 0 ? parts[^1] : "";
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                var rawType = "relational";
                if (rel.Properties != null && rel.Properties.TryGetValue("db_type", out var dtObj) && dtObj != null)
                {
                    rawType = dtObj.ToString() ?? "relational";
                }
                var rawEngine = rel.Properties != null && rel.Properties.TryGetValue("engine", out var engObj) && engObj != null
                    ? engObj.ToString()
                    : null;
                var rawSchema = rel.Properties != null && rel.Properties.TryGetValue("schema", out var schObj) && schObj != null
                    ? schObj.ToString()
                    : null;

                // STRICT VALIDATION: Only canonicalize if rawName is a genuine database!
                if (!IsLikelyDatabaseName(rawName, rawEngine))
                {
                    continue;
                }

                var (cName, cType, cKey) = CanonicalizeDatabase(rawName, rawType, rawEngine, rawSchema, primaryRelationalEngine);
                var canonicalId = BuildCanonicalDatabaseId(rel.To, cType, cKey, ctx.WorkspaceId);
                rawToCanonical[rel.To] = canonicalId;

                var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    ? rawEngine
                    : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);

                var schemaToUse = cName.Contains('.') ? cName.Split('.', 2)[1] : (rawSchema ?? "");

                if (!canonicalDbNodes.TryGetValue(canonicalId, out var existingNode) ||
                    (existingNode.Extensions != null && existingNode.Extensions.TryGetValue("engine", out var exEng) && exEng.Equals(existingNode.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
                {
                    var dispName = cName.Contains('.')
                        ? (cName.Equals("SQLite.main", StringComparison.OrdinalIgnoreCase) ? "SQLite" : cName)
                        : (!string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                            ? $"{cName} ({engineToUse}) [{cType}]"
                            : $"{cName} [{cType}]");

                    var canonicalNode = new DatabaseNode(
                        canonicalId,
                        cName,
                        "",
                        cType,
                        new Dictionary<string, string>
                        {
                            ["name"] = cName,
                            ["display_name"] = dispName,
                            ["db_type"] = cType,
                            ["role"] = "database",
                            ["engine"] = engineToUse,
                            ["technology"] = engineToUse,
                            ["schema"] = schemaToUse,
                            ["is_canonical"] = "true",
                            ["is_semantic_entity"] = "true",
                            ["layer"] = StandardLayers.Foundation.LayerId,
                            ["layerId"] = StandardLayers.Foundation.LayerId,
                            ["layerName"] = StandardLayers.Foundation.LayerName,
                            ["layerColor"] = StandardLayers.Foundation.Color,
                            ["layerIcon"] = StandardLayers.Foundation.Icon
                        }
                    );
                    canonicalDbNodes[canonicalId] = canonicalNode;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(primaryRelationalEngine))
        {
            var (defaultCName, defaultCType, defaultCKey) = CanonicalizeDatabase("Database", "relational", null, null, primaryRelationalEngine);
            var defaultCanonicalId = BuildCanonicalDatabaseId($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:database", defaultCType, defaultCKey, ctx.WorkspaceId);
            rawToCanonical["workspace:database:relational:database"] = defaultCanonicalId;
            rawToCanonical[$"{ctx.WorkspaceId}:database:relational:database"] = defaultCanonicalId;
            rawToCanonical[$"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Database}:relational:database"] = defaultCanonicalId;
            rawToCanonical[$"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:database"] = defaultCanonicalId;

            canonicalDbNodes.Remove("workspace:database:relational:database");
            canonicalDbNodes.Remove($"{ctx.WorkspaceId}:database:relational:database");
            canonicalDbNodes.Remove($"{OntologyConstants.IdPrefixes.Workspace}:{OntologyConstants.IdPrefixes.Database}:relational:database");
            canonicalDbNodes.Remove($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Database}:relational:database");
        }

        // 1.5. Track AST-verified database usages per project/service domain and prune unverified config DB edges
        var projectsByDomain = projects
            .GroupBy(p => {
                var d = SyntaxEnricher.CleanProjectNameToDomain(p.Name);
                return !string.IsNullOrEmpty(d) ? d : p.Id;
            }, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var domainVerifiedEngines = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var domainVerifiedDbTypes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var domainVerifiedKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var domainHasCodeFiles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var (domain, domainProjs) in projectsByDomain)
        {
            var domainProjIds = domainProjs.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Also include referenced projects via DEPENDS_ON within the domain or related
            foreach (var rel in allRelationships.Where(r => r.Kind == OntologyConstants.Relationships.DependsOn))
            {
                if (domainProjIds.Contains(rel.From)) domainProjIds.Add(rel.To);
            }

            var domainFileNodes = files.Where(f => fileToProject.TryGetValue(f.Id, out var p) && domainProjIds.Contains(p.Id) && !IsTestSource(f.Id, f.Path)).ToList();
            var domainCodeFiles = domainFileNodes.Where(f =>
                !f.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.EndsWith(".config", StringComparison.OrdinalIgnoreCase)).ToList();

            domainHasCodeFiles[domain] = domainCodeFiles.Count > 0;

            var vEngines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vDbTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            domainVerifiedEngines[domain] = vEngines;
            domainVerifiedDbTypes[domain] = vDbTypes;
            domainVerifiedKeys[domain] = vKeys;

            if (domainCodeFiles.Count == 0)
            {
                // Synthetic / config-only test: all config DBs are considered verified
                continue;
            }

            // 1. AST usages from non-config database relationships
            foreach (var rel in allRelationships)
            {
                if (rel.Kind == OntologyConstants.Relationships.Contains) continue;
                var isDbUsageRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                                   rel.Kind == OntologyConstants.Relationships.ReadsFrom ||
                                   rel.Kind == OntologyConstants.Relationships.WritesTo ||
                                   rel.Kind == OntologyConstants.Relationships.QueriedBy;
                if (!isDbUsageRel) continue;

                var isFromDomain = domainProjIds.Contains(rel.From) ||
                                   (fileToProject.TryGetValue(rel.From, out var pOwner) && domainProjIds.Contains(pOwner.Id));
                if (!isFromDomain) continue;

                var targetDb = allDbNodes.FirstOrDefault(d => d.Id == rel.To || rel.To.StartsWith(d.Id, StringComparison.OrdinalIgnoreCase));
                var isFromConfig = (targetDb?.Extensions != null && targetDb.Extensions.TryGetValue("from_config", out var fc) && fc == "true") ||
                                   (rel.Properties != null && rel.Properties.TryGetValue("from_config", out var rfc) && rfc?.ToString() == "true");

                if (!isFromConfig && targetDb != null)
                {
                    var eng = targetDb.Extensions?.GetValueOrDefault("engine") ?? (targetDb.Name.Contains('.') ? targetDb.Name.Split('.', 2)[0] : targetDb.Name);
                    if (!string.IsNullOrWhiteSpace(eng) && !eng.Equals("Database", StringComparison.OrdinalIgnoreCase) && !eng.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        vEngines.Add(ResourceReconciliationService.NormalizeEngineName(eng));
                    }
                    if (!string.IsNullOrWhiteSpace(targetDb.DbType))
                    {
                        vDbTypes.Add(targetDb.DbType);
                    }
                }
            }

            // 2. Package references (NuGet/NPM) in projects of this domain
            foreach (var proj in domainProjs)
            {
                foreach (var pkg in proj.Children.OfType<PackageNode>())
                {
                    var pkgLower = pkg.Name.ToLowerInvariant();
                    if (pkgLower.Contains("npgsql") || pkgLower.Contains("postgres"))
                    {
                        vEngines.Add("PostgreSQL");
                        vDbTypes.Add("relational");
                    }
                    if (pkgLower.Contains("mongo"))
                    {
                        vEngines.Add("MongoDB");
                        vDbTypes.Add("document");
                    }
                    if (pkgLower.Contains("redis") || pkgLower.Contains("stackexchange.redis"))
                    {
                        vEngines.Add("Redis");
                        vDbTypes.Add("cache");
                    }
                    if (pkgLower.Contains("mysql") || pkgLower.Contains("pomelo"))
                    {
                        vEngines.Add("MySQL");
                        vDbTypes.Add("relational");
                    }
                    if (pkgLower.Contains("sqlclient") || pkgLower.Contains("sqlserver"))
                    {
                        vEngines.Add("SQL Server");
                        vDbTypes.Add("relational");
                    }
                    if (pkgLower.Contains("sqlite"))
                    {
                        vEngines.Add("SQLite");
                        vDbTypes.Add("relational");
                    }
                    if (pkgLower.Contains("marten"))
                    {
                        vEngines.Add("PostgreSQL");
                        vDbTypes.Add("relational");
                        vDbTypes.Add("document");
                    }
                    if (pkgLower.Contains("entityframeworkcore") || pkgLower.Contains("dapper"))
                    {
                        vDbTypes.Add("relational");
                    }
                }
            }

            // 3. Scan code file content for config keys or driver usage
            foreach (var cf in domainCodeFiles)
            {
                var absPath = Path.IsPathRooted(cf.Path)
                    ? cf.Path
                    : Path.Combine(ctx.AbsoluteWorkspacePath, cf.Path);

                if (File.Exists(absPath))
                {
                    try
                    {
                        var text = File.ReadAllText(absPath);
                        if (text.Contains("PostgresOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("PostgresOptions");
                            vEngines.Add("PostgreSQL");
                            vDbTypes.Add("relational");
                        }
                        if (text.Contains("MongoOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("MongoDB", StringComparison.OrdinalIgnoreCase) || text.Contains("MongoDbContext", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("MongoOptions");
                            vEngines.Add("MongoDB");
                            vDbTypes.Add("document");
                        }
                        if (text.Contains("RedisOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("StackExchange.Redis", StringComparison.OrdinalIgnoreCase) || text.Contains("ConnectionMultiplexer", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("RedisOptions");
                            vEngines.Add("Redis");
                            vDbTypes.Add("cache");
                        }
                        if (text.Contains("MySqlOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("MySql", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("MySqlOptions");
                            vEngines.Add("MySQL");
                            vDbTypes.Add("relational");
                        }
                        if (text.Contains("SqliteOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("SqliteOptions");
                            vEngines.Add("SQLite");
                            vDbTypes.Add("relational");
                        }
                        if (text.Contains("SqlServerOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("SqlServerOptions");
                            vEngines.Add("SQL Server");
                            vDbTypes.Add("relational");
                        }
                        if (text.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase) || text.Contains("GetConnectionString", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("ConnectionStrings");
                        }
                    }
                    catch { }
                }
            }
        }

        // Prune unverified config USES_DB edges
        var unverifiedEdges = new List<Relationship>();
        foreach (var rel in allRelationships)
        {
            if (rel.Kind != OntologyConstants.Relationships.UsesDb) continue;

            var targetDb = allDbNodes.FirstOrDefault(d => d.Id == rel.To || rel.To.StartsWith(d.Id, StringComparison.OrdinalIgnoreCase));
            var isConfigDb = targetDb?.Extensions != null && targetDb.Extensions.TryGetValue("from_config", out var fc) && fc == "true";
            if (!isConfigDb || targetDb == null) continue;

            var owner = projects.FirstOrDefault(p => p.Id == rel.From || rel.From.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase))
                        ?? (fileToProject.TryGetValue(rel.From, out var fp) ? fp : null);
            if (owner == null) continue;

            var domain = SyntaxEnricher.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;

            if (domainHasCodeFiles.TryGetValue(domain, out var hasFiles) && !hasFiles)
            {
                // Synthetic / config-only test project: keep
                continue;
            }

            var eng = targetDb.Extensions?.GetValueOrDefault("engine") ?? (targetDb.Name.Contains('.') ? targetDb.Name.Split('.', 2)[0] : targetDb.Name);
            var normEng = ResourceReconciliationService.NormalizeEngineName(eng);
            var key = targetDb.Extensions?.GetValueOrDefault("config_key");

            var vEngines = domainVerifiedEngines.GetValueOrDefault(domain, []);
            var vDbTypes = domainVerifiedDbTypes.GetValueOrDefault(domain, []);
            var vKeys = domainVerifiedKeys.GetValueOrDefault(domain, []);

            var isVerified = vEngines.Contains(normEng) ||
                             (!string.IsNullOrEmpty(key) && (vKeys.Contains(key) || key.Split([':', '.']).Any(part => vKeys.Contains(part)))) ||
                             (targetDb.DbType.Equals("relational", StringComparison.OrdinalIgnoreCase) && vDbTypes.Contains("relational"));

            if (!isVerified)
            {
                unverifiedEdges.Add(rel);
            }
        }

        if (unverifiedEdges.Count > 0)
        {
            foreach (var unv in unverifiedEdges)
            {
                allRelationships.Remove(unv);
                ctx.TreeRelationships.Remove(unv);
                ctx.GlobalProjectDependencies.Remove(unv);
            }

            // Remove any unverified config databases that now have 0 non-test incoming edges
            var remainingTargets = allRelationships.Where(r => r.Kind == OntologyConstants.Relationships.UsesDb).Select(r => r.To).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var orphanedConfigDbs = allDbNodes.Where(d => d.Extensions != null && d.Extensions.TryGetValue("from_config", out var fc) && fc == "true" && !remainingTargets.Contains(d.Id)).ToList();
            foreach (var oDb in orphanedConfigDbs)
            {
                allDbNodes.Remove(oDb);
                canonicalDbNodes.Remove(oDb.Id);
                if (rawToCanonical.TryGetValue(oDb.Id, out var cId))
                {
                    canonicalDbNodes.Remove(cId);
                    rawToCanonical.Remove(oDb.Id);
                }
            }
        }

        // 2. Gather project references and consolidate databases per service/project
        var libraryProjects = projects.Where(p => 
            p.Role is "Library" or "SharedLibrary" ||
            p.IsLibrary ||
            p.EntityKind == ProjectEntityKind.Library ||
            p.Path.Contains("/Infrastructure/", StringComparison.OrdinalIgnoreCase) ||
            p.Path.Contains("\\Infrastructure\\", StringComparison.OrdinalIgnoreCase)
        ).ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);

        var serviceToReferencedLibs = new Dictionary<string, List<ProjectNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in allRelationships.Where(r => r.Kind == OntologyConstants.Relationships.DependsOn))
        {
            if (libraryProjects.TryGetValue(rel.To, out var libProj))
            {
                if (!serviceToReferencedLibs.TryGetValue(rel.From, out var libList))
                {
                    libList = [];
                    serviceToReferencedLibs[rel.From] = libList;
                }
                libList.Add(libProj);
            }
        }

        // Detect libraries referenced by multiple distinct service domains (e.g. IntegrationEventLogEF)
        var libReferencingDomains = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (svcId, libs) in serviceToReferencedLibs)
        {
            var svcProj = projects.FirstOrDefault(p => p.Id == svcId) ?? (fileToProject.TryGetValue(svcId, out var fp) ? fp : null);
            if (svcProj == null || IsTestSource(svcProj.Id, svcProj.Path)) continue;
            var dom = SyntaxEnricher.CleanProjectNameToDomain(svcProj.Name);
            if (string.IsNullOrEmpty(dom)) dom = svcProj.Id;
            foreach (var l in libs)
            {
                if (!libReferencingDomains.TryGetValue(l.Id, out var domSet))
                {
                    domSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    libReferencingDomains[l.Id] = domSet;
                }
                domSet.Add(dom);
            }
        }

        var multiDomainSharedLibs = libReferencingDomains
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => kvp.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool IsGenericOrLibraryDbId(string targetId)
        {
            if (targetId.EndsWith(":relational:postgresql", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":document:mongodb", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":keyvalue:redis", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":relational:database", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            var node = canonicalDbNodes.GetValueOrDefault(targetId);
            if (node == null) return true;
            var eng = node.Extensions?.GetValueOrDefault("engine") ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[0] : node.Name);
            var sch = node.Extensions?.GetValueOrDefault("schema") ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[1] : "");
            return string.IsNullOrWhiteSpace(sch) ||
                   sch.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                   sch.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase) ||
                   sch.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                   node.Name.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                   node.Name.EndsWith(".integrationeventlogef", StringComparison.OrdinalIgnoreCase);
        }

        var concreteEnginesCount = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !s.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase);
            })
            .GroupBy(n => n.Extensions?.GetValueOrDefault("engine") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var projectDbEdges = new List<(ProjectNode Owner, string CanonicalTarget, Relationship OriginalRel)>();

        foreach (var rel in allRelationships)
        {
            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          rawToCanonical.ContainsKey(rel.To);
            if (!isDbRel) continue;

            if (rawToCanonical.TryGetValue(rel.To, out var canonicalTarget))
            {
                var owner = projects.FirstOrDefault(p => p.Id == rel.From || rel.From.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase))
                            ?? (fileToProject.TryGetValue(rel.From, out var fp) ? fp : null);

                if (owner != null && owner.Id != canonicalTarget)
                {
                    if (IsTestSource(owner.Id, owner.Path))
                    {
                        continue;
                    }

                    var isMultiDomainLib = multiDomainSharedLibs.Contains(owner.Id);
                    var isGenericTarget = IsGenericOrLibraryDbId(canonicalTarget);

                    // A shared library referenced by >1 distinct service domains that has no database of its own
                    // must not create a project-level USES_DB edge to a generic db or service db.
                    if (!isMultiDomainLib || !isGenericTarget)
                    {
                        projectDbEdges.Add((owner, canonicalTarget, rel));
                    }

                    // Also attribute this DB target to any service that references this owner library
                    // BUT: when owner is a multi-domain shared library with a generic target, do NOT propagate it
                    // because each calling service domain will use its own database!
                    if (!isMultiDomainLib || !isGenericTarget)
                    {
                        foreach (var (svcId, libs) in serviceToReferencedLibs)
                        {
                            if (libs.Any(l => l.Id == owner.Id))
                            {
                                var svcProj = projects.FirstOrDefault(p => p.Id == svcId);
                                if (svcProj != null && !IsTestSource(svcProj.Id, svcProj.Path))
                                {
                                    projectDbEdges.Add((svcProj, canonicalTarget, rel));
                                }
                            }
                        }
                    }
                }
            }
        }

        var edgesByService = projectDbEdges
            .GroupBy(e => {
                var domain = SyntaxEnricher.CleanProjectNameToDomain(e.Owner.Name);
                return !string.IsNullOrEmpty(domain) ? domain : e.Owner.Id;
            }, StringComparer.OrdinalIgnoreCase);

        var globalConsolidationRemap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var domainScopedRemap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var sGroup in edgesByService)
        {
            var domainKey = sGroup.Key;
            var candidateNodes = sGroup
                .Select(e => canonicalDbNodes.GetValueOrDefault(e.CanonicalTarget))
                .Where(n => n != null)
                .Select(n => {
                    var name = n!.Name;
                    var engine = (n.Extensions != null && n.Extensions.TryGetValue("engine", out var eng) && !string.IsNullOrWhiteSpace(eng))
                        ? eng
                        : (name.Contains('.') ? name.Split('.', 2)[0] : name);
                    var schema = (n.Extensions != null && n.Extensions.TryGetValue("schema", out var sc) && !string.IsNullOrWhiteSpace(sc))
                        ? sc
                        : (name.Contains('.') ? name.Split('.', 2)[1] : "");
                    return (n.Id, name, engine, schema);
                })
                .DistinctBy(t => t.Id)
                .ToList();

            var localRemap = ResolveDatabaseConsolidations(candidateNodes);
            foreach (var (loser, winner) in localRemap)
            {
                var loserNode = canonicalDbNodes.GetValueOrDefault(loser);
                var loserEng = loserNode?.Extensions?.GetValueOrDefault("engine") ?? (loserNode?.Name.Contains('.') == true ? loserNode.Name.Split('.', 2)[0] : loserNode?.Name ?? "");
                var isLoserGeneric = IsGenericOrLibraryDbId(loser) || IsAbstractEngineOnlyDatabase(loserNode?.Name, loserNode?.Extensions?.GetValueOrDefault("schema"), loserNode?.DbType, loser);

                if (isLoserGeneric)
                {
                    // Multi-database engine or abstract engine node: do NOT remap generic node globally! Scope to domain.
                    if (!domainScopedRemap.TryGetValue(domainKey, out var dMap))
                    {
                        dMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        domainScopedRemap[domainKey] = dMap;
                    }
                    dMap[loser] = winner;
                }
                else
                {
                    globalConsolidationRemap[loser] = winner;
                }
            }

            // Consolidate single configured database with AST schema nodes for the service
            foreach (var engGroup in candidateNodes.GroupBy(n => n.engine, StringComparer.OrdinalIgnoreCase))
            {
                var engList = engGroup.ToList();
                if (engList.Count <= 1) continue;

                var cfgNodes = engList.Where(n => {
                    var nodeObj = canonicalDbNodes.GetValueOrDefault(n.Id);
                    return nodeObj?.Extensions != null && nodeObj.Extensions.TryGetValue("from_config", out var fc) && fc == "true";
                }).ToList();

                var astNodes = engList.Where(n => !cfgNodes.Any(c => c.Id == n.Id)).ToList();

                if (cfgNodes.Count == 1 && astNodes.Count > 0)
                {
                    var cfg = cfgNodes[0];
                    foreach (var ast in astNodes)
                    {
                        var astStem = ResourceReconciliationService.StripDatabaseSuffix(ast.schema);
                        var cfgStem = ResourceReconciliationService.StripDatabaseSuffix(cfg.schema);
                        if ((string.Equals(astStem, cfgStem, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(cfgStem, astStem + "s", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(cfgStem, astStem + "es", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(astStem, cfgStem + "s", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(astStem, cfgStem + "es", StringComparison.OrdinalIgnoreCase)) &&
                            !string.Equals(ast.schema, cfg.schema, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!cfg.schema.Equals(cfgStem, StringComparison.OrdinalIgnoreCase) && ast.schema.Equals(astStem, StringComparison.OrdinalIgnoreCase))
                            {
                                globalConsolidationRemap[cfg.Id] = ast.Id;
                            }
                            else
                            {
                                globalConsolidationRemap[ast.Id] = cfg.Id;
                            }
                        }
                    }
                }
            }
        }

        // Guard: globalConsolidationRemap must NEVER map an engine-only ID
        foreach (var k in globalConsolidationRemap.Keys.ToList())
        {
            var node = canonicalDbNodes.GetValueOrDefault(k);
            if (IsGenericOrLibraryDbId(k) || IsAbstractEngineOnlyDatabase(node?.Name, node?.Extensions?.GetValueOrDefault("schema"), node?.DbType, k))
            {
                globalConsolidationRemap.Remove(k);
            }
        }

        if (globalConsolidationRemap.Count > 0)
        {
            foreach (var (loserId, winnerId) in globalConsolidationRemap)
            {
                rawToCanonical[loserId] = winnerId;
            }

            foreach (var k in rawToCanonical.Keys.ToList())
            {
                var cur = rawToCanonical[k];
                if (globalConsolidationRemap.TryGetValue(cur, out var win))
                {
                    rawToCanonical[k] = win;
                }
            }

            foreach (var (loserId, winnerId) in globalConsolidationRemap)
            {
                if (canonicalDbNodes.TryGetValue(loserId, out var loserNode) &&
                    canonicalDbNodes.TryGetValue(winnerId, out var winnerNode))
                {
                    foreach (var child in loserNode.Children)
                    {
                        if (!winnerNode.Children.Any(c => c.Id == child.Id))
                        {
                            winnerNode.Children.Add(child);
                        }
                    }
                }
                canonicalDbNodes.Remove(loserId);
            }
        }

        // Remove any generic engine-only or library-only database nodes when concrete database nodes exist
        var concreteEngines = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                return !string.IsNullOrWhiteSpace(s) && !s.Equals("default", StringComparison.OrdinalIgnoreCase);
            })
            .Select(n => n.Extensions?.GetValueOrDefault("engine") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genericOrLibraryDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var eng = n.Extensions?.GetValueOrDefault("engine") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name);
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isGenericOrLibrary = string.IsNullOrWhiteSpace(s) ||
                                         s.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                         s.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase) ||
                                         n.Name.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                                         n.Name.EndsWith(".integrationeventlogef", StringComparison.OrdinalIgnoreCase);
                return isGenericOrLibrary && concreteEngines.Contains(eng);
            })
            .ToList();

        foreach (var purgeNode in genericOrLibraryDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        // Remove any abstract engine-only database nodes
        var abstractDbsToPurge = canonicalDbNodes.Values
            .Where(n => IsAbstractEngineOnlyDatabase(n.Name, n.Extensions?.GetValueOrDefault("schema"), n.DbType, n.Id))
            .ToList();

        foreach (var purgeNode in abstractDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        // Remove any test database nodes (name/schema ends with -test, _test, or test config with 0 non-test callers)
        var testDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isTestName = s.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 s.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
                var isExplicitTest = (n.Extensions != null && n.Extensions.TryGetValue("is_test", out var it) && (it == "true" || it == "1")) ||
                                     (n.Extensions != null && n.Extensions.TryGetValue("role", out var r) && r.Equals("Test", StringComparison.OrdinalIgnoreCase)) ||
                                     WorkspaceConventions.IsTestFilePath(n.Path);
                return (isTestName || isExplicitTest) && !nonTestAccessedTargets.Contains(n.Id);
            })
            .ToList();

        foreach (var purgeNode in testDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        foreach (var cNode in canonicalDbNodes.Values)
        {
            var eng = cNode.Extensions?.GetValueOrDefault("engine") ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[0] : cNode.Name);
            var sch = cNode.Extensions?.GetValueOrDefault("schema") ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[1] : "");

            cNode.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
            foreach (var child in cNode.Children)
            {
                if (child.Extensions != null)
                {
                    child.Extensions["engine"] = eng;
                    if (!string.IsNullOrEmpty(sch)) child.Extensions["schema"] = sch;
                }
                if (child is DataSetNode ds)
                {
                    ds.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
                    foreach (var tc in ds.Children)
                    {
                        if (tc.Extensions != null)
                        {
                            tc.Extensions["engine"] = eng;
                            tc.Extensions["schema"] = !string.IsNullOrEmpty(ds.Name) ? ds.Name : sch;
                        }
                    }
                }
            }
        }

        // 3. Add canonical nodes to SemanticStructure and upload
        var semanticStructure = ctx.SemanticStructure ?? l4Result.SemanticStructure;
        if (semanticStructure != null)
        {
            if (!string.IsNullOrWhiteSpace(primaryRelationalEngine))
            {
                semanticStructure.Children.RemoveAll(c => c is DatabaseNode dn &&
                    (string.Equals(dn.Name, "Database", StringComparison.OrdinalIgnoreCase) ||
                     dn.Id.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase) ||
                     dn.Id.EndsWith($":{OntologyConstants.IdPrefixes.Database}:relational:database", StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var node in canonicalDbNodes.Values)
            {
                if (!semanticStructure.Children.Any(c => c.Id == node.Id))
                {
                    semanticStructure.Children.Add(node);
                }
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, node.Name, node.Id);
            }
        }

        if (canonicalDbNodes.Count > 0 && ctx.DbClient != null)
        {
            ctx.LogDebug($"[PostIndexAnalyzer] Materializing {canonicalDbNodes.Count} canonical Database nodes into graph...");
            await ctx.DbClient.UploadNodesAsync([.. canonicalDbNodes.Values.Select(Node.FromNode)]);
            ctx.AddNodesCount(canonicalDbNodes.Count);
        }

        // 4. Link projects to canonical database nodes
        var canonicalRels = new List<Relationship>();
        var seen = new HashSet<(string From, string To)>();

        foreach (var (owner, rawTarget, rel) in projectDbEdges)
        {
            var domain = SyntaxEnricher.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;

            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
            {
                target = dWin;
            }
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
            {
                target = win;
            }

            var targetNode = canonicalDbNodes.GetValueOrDefault(target);
            var targetEng = targetNode?.Extensions?.GetValueOrDefault("engine") ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
            if (IsGenericOrLibraryDbId(target) && concreteEnginesCount.GetValueOrDefault(targetEng) > 0)
            {
                continue;
            }

            if (owner.Id != target && seen.Add((owner.Id, target)))
            {
                var props = new Dictionary<string, object>
                {
                    ["dependency_type"] = "database",
                    ["is_semantic"] = "true",
                    ["is_canonical"] = "true"
                };
                if (rel.Properties != null)
                {
                    foreach (var (k, v) in rel.Properties)
                    {
                        if (v != null && !props.ContainsKey(k))
                        {
                            props[k] = v;
                        }
                    }
                }
                canonicalRels.Add(new Relationship(
                    owner.Id,
                    target,
                    OntologyConstants.Relationships.UsesDb,
                    props
                ));
            }
            else if (rel.Properties != null)
            {
                var existing = canonicalRels.FirstOrDefault(r => r.From == owner.Id && r.To == target);
                if (existing?.Properties != null)
                {
                    foreach (var (k, v) in rel.Properties)
                    {
                        if (v != null && !existing.Properties.ContainsKey(k))
                        {
                            existing.Properties[k] = v;
                        }
                    }
                }
            }
        }

        if (canonicalRels.Count > 0 && ctx.DbClient != null)
        {
            ctx.LogDebug($"[PostIndexAnalyzer] Materializing {canonicalRels.Count} direct project->canonical database relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(canonicalRels);
            ctx.AddRelsCount(canonicalRels.Count);
        }

        if (ctx.DbClient != null)
        {
            // Apply domain-scoped remappings to edges
            foreach (var (domain, dMap) in domainScopedRemap)
            {
                var domProjs = projectsByDomain.GetValueOrDefault(domain, []);
                var domProjIds = domProjs.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var (loserId, winnerId) in dMap)
                {
                    foreach (var pId in domProjIds)
                    {
                        await ctx.DbClient.ExecuteWriteAsync(
                            "UPDATE OR IGNORE edges SET to_id = @winnerId WHERE to_id = @loserId AND from_id LIKE @pIdPrefix;",
                            new { winnerId, loserId, pIdPrefix = $"{pId}%" });
                    }
                }
            }

            // Clean up duplicate non-canonical database nodes
            foreach (var (rawId, canonicalId) in rawToCanonical)
            {
                if (!string.Equals(rawId, canonicalId, StringComparison.Ordinal))
                {
                    await ctx.DbClient.ExecuteWriteAsync("UPDATE OR IGNORE edges SET to_id = @canonicalId WHERE to_id = @rawId;", new { canonicalId, rawId });
                    await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE to_id = @rawId;", new { rawId });
                    await ctx.DbClient.ExecuteWriteAsync("UPDATE OR IGNORE edges SET from_id = @canonicalId WHERE from_id = @rawId;", new { canonicalId, rawId });
                    await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE from_id = @rawId;", new { rawId });
                    await ctx.DbClient.ExecuteWriteAsync("DELETE FROM nodes WHERE id = @rawId;", new { rawId });
                }
            }

            // Remove any improper CONTAINS edges pointing to Database nodes (Databases are infrastructure, only contained in semantic_structure)
            await ctx.DbClient.ExecuteWriteAsync(
                "DELETE FROM edges WHERE kind = 'CONTAINS' AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database') AND from_id NOT LIKE '%semantic_structure%';");

            // Purge any remaining phantom Database nodes that were not canonicalized
            var validIds = canonicalDbNodes.Keys.ToList();
            if (validIds.Count > 0)
            {
                var placeholders = string.Join(",", validIds.Select((_, i) => $"@p{i}"));
                var paramDict = new Dictionary<string, object>();
                for (int i = 0; i < validIds.Count; i++) paramDict[$"p{i}"] = validIds[i];

                await ctx.DbClient.ExecuteWriteAsync(
                    $"DELETE FROM edges WHERE (to_id NOT IN ({placeholders}) AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database')) OR (from_id NOT IN ({placeholders}) AND from_id IN (SELECT id FROM nodes WHERE kind = 'Database'));",
                    paramDict);
                await ctx.DbClient.ExecuteWriteAsync(
                    $"DELETE FROM nodes WHERE kind = 'Database' AND id NOT IN ({placeholders});",
                    paramDict);
            }
            else
            {
                await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE to_id IN (SELECT id FROM nodes WHERE kind = 'Database') OR from_id IN (SELECT id FROM nodes WHERE kind = 'Database');");
                await ctx.DbClient.ExecuteWriteAsync("DELETE FROM nodes WHERE kind = 'Database';");
            }
        }

        return (canonicalRels, rawToCanonical);
    }

    public static async Task<CanonicalizeDatabasesResult> CanonicalizeDatabasesAsync(
        IGraphClient db,
        string widPrefix,
        CancellationToken cancellationToken = default)
    {
        var rawToCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var canonicalNodes = new Dictionary<string, (string Name, string DbType, string Key, string Engine)>(StringComparer.OrdinalIgnoreCase);

        // 1. Query projects to identify test projects
        var projQuery = "MATCH (p:Project) RETURN p.id AS id, p.name AS name, p.path AS path, p.role AS role, json_extract(p.properties, '$.is_test') AS is_test, json_extract(p.properties, '$.entity_kind') AS entity_kind";
        var projsJson = await db.ExecuteQueryAsync(projQuery, null, cancellationToken);
        var projectList = new List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)>();
        using (var projsDoc = JsonDocument.Parse(projsJson))
        {
            foreach (var row in projsDoc.RootElement.EnumerateArray())
            {
                var pId = GetStringProp(row, "id");
                var pName = GetStringProp(row, "name");
                var pPath = row.TryGetProperty("path", out var pt) && pt.ValueKind == JsonValueKind.String ? pt.GetString() : null;
                var pRole = row.TryGetProperty("role", out var rl) && rl.ValueKind == JsonValueKind.String ? rl.GetString() : "";
                var isTestProp = row.TryGetProperty("is_test", out var it) && it.ValueKind == JsonValueKind.String ? it.GetString() : "";
                var entityKind = row.TryGetProperty("entity_kind", out var ek) && ek.ValueKind == JsonValueKind.String ? ek.GetString() : "";

                var isTest = string.Equals(pRole, "Test", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(pRole, "TestSuite", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(isTestProp, "true", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(isTestProp, "1", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(entityKind, "Test", StringComparison.OrdinalIgnoreCase) ||
                             WorkspaceConventions.IsTestFilePath(pPath) ||
                             (!string.IsNullOrEmpty(pId) && (pId.Contains("/tests/", StringComparison.OrdinalIgnoreCase) || pId.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) || pId.Contains(".Tests", StringComparison.OrdinalIgnoreCase)));

                var isLibrary = string.Equals(pRole, "Library", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(pRole, "SharedLibrary", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(entityKind, "Library", StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrEmpty(pPath) && (pPath.Contains("/Infrastructure/", StringComparison.OrdinalIgnoreCase) || pPath.Contains("\\Infrastructure\\", StringComparison.OrdinalIgnoreCase)));

                if (!string.IsNullOrEmpty(pId))
                {
                    projectList.Add((pId, pName, pPath, isTest, isLibrary));
                }
            }
        }

        // 2. Query relationships to find targets accessed by non-test callers
        var edgesQuery = "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON', 'READS_FROM', 'WRITES_TO'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties, src.role AS src_role, src.path AS src_path, json_extract(src.properties, '$.is_test') AS src_is_test";
        var edgesJson = await db.ExecuteQueryAsync(edgesQuery, null, cancellationToken);
        var nonTestAccessedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var edgesDoc = JsonDocument.Parse(edgesJson))
        {
            foreach (var row in edgesDoc.RootElement.EnumerateArray())
            {
                var src = GetStringProp(row, "source");
                var tgt = GetStringProp(row, "target");
                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

                var srcRole = row.TryGetProperty("src_role", out var sr) && sr.ValueKind == JsonValueKind.String ? sr.GetString() : "";
                var srcPath = row.TryGetProperty("src_path", out var sp) && sp.ValueKind == JsonValueKind.String ? sp.GetString() : "";
                var srcIsTest = row.TryGetProperty("src_is_test", out var sit) && sit.ValueKind == JsonValueKind.String ? sit.GetString() : "";

                var matchingProj = projectList.FirstOrDefault(p => p.Id == src || src.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(p.Path) && src.Contains(p.Path, StringComparison.OrdinalIgnoreCase)));

                var isTest = (!string.IsNullOrEmpty(matchingProj.Id) && matchingProj.IsTest) ||
                             string.Equals(srcRole, "Test", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(srcRole, "TestSuite", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(srcIsTest, "true", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(srcIsTest, "1", StringComparison.OrdinalIgnoreCase) ||
                             WorkspaceConventions.IsTestFilePath(srcPath) ||
                             WorkspaceConventions.IsTestFilePath(src) ||
                             src.Contains("/tests/", StringComparison.OrdinalIgnoreCase) ||
                             src.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) ||
                             src.Contains(".Tests", StringComparison.OrdinalIgnoreCase);

                if (!isTest)
                {
                    nonTestAccessedTargets.Add(tgt);
                    var parts = tgt.Split(':');
                    var accum = "";
                    for (int i = 0; i < parts.Length; i++)
                    {
                        accum = i == 0 ? parts[i] : $"{accum}:{parts[i]}";
                        nonTestAccessedTargets.Add(accum);
                    }
                }
            }
        }

        // 3. Query databases
        var dbQuery = "MATCH (d:Database) RETURN d.id AS id, d.name AS name, d.db_type AS db_type, d.engine AS engine, d.schema AS schema, d.path AS path, json_extract(d.properties, '$.is_test') AS is_test, d.role AS role";
        var dbJson = await db.ExecuteQueryAsync(dbQuery, null, cancellationToken);
        string? primaryRelationalEngine = null;
        using (var dbDoc = JsonDocument.Parse(dbJson))
        {
            foreach (var row in dbDoc.RootElement.EnumerateArray())
            {
                var dt = row.TryGetProperty("db_type", out var dtElem) && dtElem.ValueKind == JsonValueKind.String ? dtElem.GetString() : "relational";
                if (!string.Equals(dt, "relational", StringComparison.OrdinalIgnoreCase)) continue;

                var eng = row.TryGetProperty("engine", out var eg) && eg.ValueKind == JsonValueKind.String ? eg.GetString() : null;
                var nm = GetStringProp(row, "name");
                var candidate = !string.IsNullOrWhiteSpace(eng) && !eng.Equals("Database", StringComparison.OrdinalIgnoreCase) && !eng.Equals("default", StringComparison.OrdinalIgnoreCase)
                    ? eng
                    : (nm.Contains('.') ? nm.Split('.', 2)[0] : nm);

                if (!string.IsNullOrWhiteSpace(candidate) &&
                    !candidate.Equals("Database", StringComparison.OrdinalIgnoreCase) &&
                    !candidate.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                    !ResourceReconciliationService.IsGenericConfigKey(candidate) &&
                    IsRelationalEngine(candidate))
                {
                    primaryRelationalEngine = candidate;
                    break;
                }
            }

            foreach (var row in dbDoc.RootElement.EnumerateArray())
            {
                var id = GetStringProp(row, "id");
                var name = GetStringProp(row, "name", id);
                var rawEngine = row.TryGetProperty("engine", out var eg) && eg.ValueKind == JsonValueKind.String ? eg.GetString() : null;
                if (!IsLikelyDatabaseName(name, rawEngine) && !IsLikelyDatabaseName(id, rawEngine)) continue;

                var dPath = row.TryGetProperty("path", out var dp) && dp.ValueKind == JsonValueKind.String ? dp.GetString() : null;
                var isDeclaredInProduction = !string.IsNullOrEmpty(dPath) && !WorkspaceConventions.IsTestFilePath(dPath);
                var isExplicitTest = (row.TryGetProperty("is_test", out var dit) && dit.ValueKind == JsonValueKind.String && (dit.GetString() == "true" || dit.GetString() == "1")) ||
                                     (row.TryGetProperty("role", out var drl) && drl.ValueKind == JsonValueKind.String && string.Equals(drl.GetString(), "Test", StringComparison.OrdinalIgnoreCase)) ||
                                     WorkspaceConventions.IsTestFilePath(dPath);

                var hasNonTestAccess = (!isExplicitTest && isDeclaredInProduction) || nonTestAccessedTargets.Contains(id);
                if (!hasNonTestAccess)
                {
                    continue;
                }

                var dbType = row.TryGetProperty("db_type", out var dt) && dt.ValueKind == JsonValueKind.String ? (dt.GetString() ?? "Database") : "Database";
                var rawSchema = row.TryGetProperty("schema", out var sc) && sc.ValueKind == JsonValueKind.String ? sc.GetString() : null;

                var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType, rawEngine, rawSchema, primaryRelationalEngine);
                var canonicalId = BuildCanonicalDatabaseId(id, cType, cKey, widPrefix);
                rawToCanonical[id] = canonicalId;

                var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    ? rawEngine
                    : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);

                if (!canonicalNodes.TryGetValue(canonicalId, out var existing) ||
                    (existing.Engine.Equals(existing.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
                {
                    canonicalNodes[canonicalId] = (cName, cType, cKey, engineToUse);
                }
            }
        }

        // Also map any dataset (schema) nodes
        try
        {
            var dsQuery = "MATCH (ds:DataSet) RETURN ds.id AS id, ds.name AS name";
            var dsJson = await db.ExecuteQueryAsync(dsQuery, null, cancellationToken);
            using var dsDoc = JsonDocument.Parse(dsJson);

            foreach (var row in dsDoc.RootElement.EnumerateArray())
            {
                var dsId = GetStringProp(row, "id");
                var dsName = GetStringProp(row, "name");
                if (string.IsNullOrEmpty(dsId) || string.IsNullOrEmpty(dsName)) continue;
                if (!nonTestAccessedTargets.Contains(dsId)) continue;

                var targetEngine = !string.IsNullOrWhiteSpace(primaryRelationalEngine) ? primaryRelationalEngine : "PostgreSQL";
                var (cName, cType, cKey) = CanonicalizeDatabase(targetEngine, "relational", null, dsName, primaryRelationalEngine);
                var canonicalId = BuildCanonicalDatabaseId(dsId, cType, cKey, widPrefix);
                rawToCanonical[dsId] = canonicalId;

                if (!canonicalNodes.ContainsKey(canonicalId))
                {
                    canonicalNodes[canonicalId] = (cName, cType, cKey, targetEngine);
                }
            }
        }
        catch { }

        if (canonicalNodes.Count == 0)
        {
            return new CanonicalizeDatabasesResult(0, 0);
        }

        // 4. Query relationships targeting any database and consolidate per service/project
        var relEdgesQuery =
            "MATCH (src)-[r]->(tgt) WHERE r.kind IN ['USES_DB', 'CONFIGURES', 'DEPENDS_ON'] RETURN src.id AS source, tgt.id AS target, r.kind AS kind, r.properties AS properties";
        var relEdgesJson = await db.ExecuteQueryAsync(relEdgesQuery, null, cancellationToken);

        var libraryProjects = projectList.Where(p => p.IsLibrary).ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);
        var serviceToReferencedLibs = new Dictionary<string, List<(string Id, string Name)>>(StringComparer.OrdinalIgnoreCase);

        var projectDbEdges = new List<((string Id, string Name) Owner, string CanonicalTarget, JsonElement Properties)>();

        using (var edgesDoc = JsonDocument.Parse(relEdgesJson))
        {
            foreach (var row in edgesDoc.RootElement.EnumerateArray())
            {
                var src = GetStringProp(row, "source");
                var tgt = GetStringProp(row, "target");
                var kind = GetStringProp(row, "kind");
                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

                if (string.Equals(kind, "DEPENDS_ON", StringComparison.OrdinalIgnoreCase))
                {
                    if (libraryProjects.TryGetValue(tgt, out var lib))
                    {
                        if (!serviceToReferencedLibs.TryGetValue(src, out var lList))
                        {
                            lList = [];
                            serviceToReferencedLibs[src] = lList;
                        }
                        lList.Add((lib.Id, lib.Name));
                    }
                    continue;
                }

                if (rawToCanonical.TryGetValue(tgt, out var canonicalId))
                {
                    // Resolve owning project
                    string? projId = null;
                    string projName = "";
                    var exactProj = projectList.FirstOrDefault(p => p.Id == src);

                    if (!string.IsNullOrEmpty(exactProj.Id))
                    {
                        if (exactProj.IsTest) continue;
                        projId = exactProj.Id;
                        projName = exactProj.Name;
                    }
                    else
                    {
                        var matched = projectList.FirstOrDefault(p =>
                            src.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(p.Path) &&
                             src.Contains(p.Path, StringComparison.OrdinalIgnoreCase)));
                        if (matched.IsTest) continue;
                        projId = matched.Id;
                        projName = matched.Name;
                    }

                    if (!string.IsNullOrEmpty(projId) && projId != canonicalId)
                    {
                        row.TryGetProperty("properties", out var pElem);
                        projectDbEdges.Add(((projId, projName), canonicalId, pElem.Clone()));
                    }
                }
            }
        }

        // Detect libraries referenced by multiple distinct service domains (e.g. IntegrationEventLogEF)
        var libReferencingDomains = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (svcId, libs) in serviceToReferencedLibs)
        {
            var svcProj = projectList.FirstOrDefault(p => p.Id == svcId);
            if (svcProj.IsTest || string.IsNullOrEmpty(svcProj.Id)) continue;
            var dom = SyntaxEnricher.CleanProjectNameToDomain(svcProj.Name);
            if (string.IsNullOrEmpty(dom)) dom = svcProj.Id;
            foreach (var l in libs)
            {
                if (!libReferencingDomains.TryGetValue(l.Id, out var domSet))
                {
                    domSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    libReferencingDomains[l.Id] = domSet;
                }
                domSet.Add(dom);
            }
        }

        var multiDomainSharedLibs = libReferencingDomains
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => kvp.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool IsGenericOrLibraryDbId(string targetId)
        {
            if (targetId.EndsWith(":relational:postgresql", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":document:mongodb", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":keyvalue:redis", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":relational:database", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!canonicalNodes.TryGetValue(targetId, out var info)) return true;
            var eng = info.Engine;
            var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
            return string.IsNullOrWhiteSpace(sch) ||
                   sch.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                   sch.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase) ||
                   sch.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                   info.Name.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                   info.Name.EndsWith(".integrationeventlogef", StringComparison.OrdinalIgnoreCase);
        }

        var concreteEnginesCount = canonicalNodes.Values
            .Where(info => {
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !s.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase);
            })
            .GroupBy(info => info.Engine, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        // Filter out edges from multiDomainSharedLibs to generic databases, and propagate non-generic lib edges to services
        var filteredProjectDbEdges = new List<((string Id, string Name) Owner, string CanonicalTarget, JsonElement Properties)>();
        foreach (var edge in projectDbEdges)
        {
            var isMultiDomainLib = multiDomainSharedLibs.Contains(edge.Owner.Id);
            var isGenericTarget = IsGenericOrLibraryDbId(edge.CanonicalTarget);

            if (!isMultiDomainLib || !isGenericTarget)
            {
                filteredProjectDbEdges.Add(edge);
            }

            if (!isMultiDomainLib || !isGenericTarget)
            {
                foreach (var (svcId, libs) in serviceToReferencedLibs)
                {
                    if (libs.Any(l => l.Id == edge.Owner.Id))
                    {
                        var svcProj = projectList.FirstOrDefault(p => p.Id == svcId);
                        if (!string.IsNullOrEmpty(svcProj.Id) && !svcProj.IsTest)
                        {
                            filteredProjectDbEdges.Add(((svcProj.Id, svcProj.Name), edge.CanonicalTarget, edge.Properties));
                        }
                    }
                }
            }
        }
        projectDbEdges = filteredProjectDbEdges;

        var edgesByService = projectDbEdges
            .GroupBy(e => {
                var domain = SyntaxEnricher.CleanProjectNameToDomain(e.Owner.Name);
                return !string.IsNullOrEmpty(domain) ? domain : e.Owner.Id;
            }, StringComparer.OrdinalIgnoreCase);

        var globalConsolidationRemap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var domainScopedRemap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var sGroup in edgesByService)
        {
            var domainKey = sGroup.Key;
            var candidateNodes = sGroup
                .Select(e => canonicalNodes.TryGetValue(e.CanonicalTarget, out var info) ? (e.CanonicalTarget, info) : default)
                .Where(t => t.CanonicalTarget != null)
                .Select(t => {
                    var name = t.info.Name;
                    var engine = t.info.Engine;
                    var schema = name.Contains('.') ? name.Split('.', 2)[1] : "";
                    return (t.CanonicalTarget, name, engine, schema);
                })
                .DistinctBy(t => t.CanonicalTarget)
                .ToList();

            var localRemap = ResolveDatabaseConsolidations(candidateNodes);
            foreach (var (loser, winner) in localRemap)
            {
                var loserNode = canonicalNodes.TryGetValue(loser, out var lInfo) ? lInfo : default;
                var loserEng = loserNode.Engine ?? (loserNode.Name?.Contains('.') == true ? loserNode.Name.Split('.', 2)[0] : loserNode.Name ?? "");
                var loserSch = loserNode.Name?.Contains('.') == true ? loserNode.Name.Split('.', 2)[1] : "";
                var isLoserGeneric = IsGenericOrLibraryDbId(loser) || IsAbstractEngineOnlyDatabase(loserNode.Name, loserSch, loserNode.DbType, loser);

                if (isLoserGeneric)
                {
                    if (!domainScopedRemap.TryGetValue(domainKey, out var dMap))
                    {
                        dMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        domainScopedRemap[domainKey] = dMap;
                    }
                    dMap[loser] = winner;
                }
                else
                {
                    globalConsolidationRemap[loser] = winner;
                }
            }

            // Consolidate single configured database with AST schema nodes for the service
            foreach (var engGroup in candidateNodes.GroupBy(n => n.engine, StringComparer.OrdinalIgnoreCase))
            {
                var engList = engGroup.ToList();
                if (engList.Count <= 1) continue;

                var cfgNodes = engList.Where(n => {
                    // Check if node has a catalog/schema name without generic prefix
                    var s = n.schema;
                    return !string.IsNullOrWhiteSpace(s) && !s.Equals("default", StringComparison.OrdinalIgnoreCase);
                }).ToList();

                if (cfgNodes.Count > 1)
                {
                    // Check if one has clean stem (ordering) and another has db suffix (orderingdb)
                    for (int i = 0; i < cfgNodes.Count; i++)
                    {
                        var n1 = cfgNodes[i];
                        var s1Stem = ResourceReconciliationService.StripDatabaseSuffix(n1.schema);
                        for (int j = 0; j < cfgNodes.Count; j++)
                        {
                            if (i == j) continue;
                            var n2 = cfgNodes[j];
                            var s2Stem = ResourceReconciliationService.StripDatabaseSuffix(n2.schema);
                            if (string.Equals(s1Stem, s2Stem, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!n2.schema.Equals(s2Stem, StringComparison.OrdinalIgnoreCase) && n1.schema.Equals(s1Stem, StringComparison.OrdinalIgnoreCase))
                                {
                                    globalConsolidationRemap[n2.CanonicalTarget] = n1.CanonicalTarget;
                                }
                            }
                            else if (string.Equals(s1Stem, s2Stem + "s", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(s1Stem, s2Stem + "es", StringComparison.OrdinalIgnoreCase))
                            {
                                globalConsolidationRemap[n2.CanonicalTarget] = n1.CanonicalTarget;
                            }
                            else if (string.Equals(s2Stem, s1Stem + "s", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(s2Stem, s1Stem + "es", StringComparison.OrdinalIgnoreCase))
                            {
                                globalConsolidationRemap[n1.CanonicalTarget] = n2.CanonicalTarget;
                            }
                        }
                    }
                }
            }
        }

        // Guard: globalConsolidationRemap must NEVER map an engine-only ID
        foreach (var k in globalConsolidationRemap.Keys.ToList())
        {
            var node = canonicalNodes.TryGetValue(k, out var cInfo) ? cInfo : default;
            var sch = node.Name?.Contains('.') == true ? node.Name.Split('.', 2)[1] : "";
            if (IsGenericOrLibraryDbId(k) || IsAbstractEngineOnlyDatabase(node.Name, sch, node.DbType, k))
            {
                globalConsolidationRemap.Remove(k);
            }
        }

        if (globalConsolidationRemap.Count > 0)
        {
            foreach (var (loserId, winnerId) in globalConsolidationRemap)
            {
                rawToCanonical[loserId] = winnerId;
            }

            foreach (var k in rawToCanonical.Keys.ToList())
            {
                var cur = rawToCanonical[k];
                if (globalConsolidationRemap.TryGetValue(cur, out var win))
                {
                    rawToCanonical[k] = win;
                }
            }
            foreach (var loserId in globalConsolidationRemap.Keys)
            {
                canonicalNodes.Remove(loserId);
            }
        }

        // Remove any generic engine-only or library-only database nodes when concrete database nodes exist
        var concreteEngines = canonicalNodes.Values
            .Where(info => {
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                return !string.IsNullOrWhiteSpace(s) && !s.Equals("default", StringComparison.OrdinalIgnoreCase);
            })
            .Select(info => info.Engine)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genericOrLibraryDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                var isGenericOrLibrary = string.IsNullOrWhiteSpace(s) ||
                                         s.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                         s.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase) ||
                                         info.Name.Equals(info.Engine, StringComparison.OrdinalIgnoreCase) ||
                                         info.Name.EndsWith(".integrationeventlogef", StringComparison.OrdinalIgnoreCase);
                return isGenericOrLibrary && concreteEngines.Contains(info.Engine);
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var purgeId in genericOrLibraryDbsToPurge)
        {
            canonicalNodes.Remove(purgeId);
            rawToCanonical.Remove(purgeId);
        }

        // Remove any abstract engine-only database nodes
        var abstractDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                return IsAbstractEngineOnlyDatabase(info.Name, sch, info.DbType, id);
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var purgeId in abstractDbsToPurge)
        {
            canonicalNodes.Remove(purgeId);
            rawToCanonical.Remove(purgeId);
        }

        // Remove any test database nodes
        var testDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                var isTestName = sch.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 sch.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                                 info.Name.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 info.Name.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
                return isTestName && !nonTestAccessedTargets.Contains(id);
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var purgeId in testDbsToPurge)
        {
            canonicalNodes.Remove(purgeId);
            rawToCanonical.Remove(purgeId);
        }

        // 2. Upload canonical Database nodes (only active, consolidated nodes)
        var nodesToUpload = new List<Node>();
        foreach (var (canonicalId, (cName, cType, cKey, engineToUse)) in canonicalNodes)
        {
            var dispName = cName.Contains('.')
                ? (cName.Equals("SQLite.main", StringComparison.OrdinalIgnoreCase) ? "SQLite" : cName)
                : (!string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                    ? $"{cName} ({engineToUse}) [{cType}]"
                    : $"{cName} [{cType}]");

            var schemaToUse = cName.Contains('.') ? cName.Split('.', 2)[1] : "";

            var props = new Dictionary<string, object>
            {
                ["name"] = cName,
                ["display_name"] = dispName,
                ["db_type"] = cType,
                ["role"] = "database",
                ["engine"] = engineToUse,
                ["technology"] = engineToUse,
                ["schema"] = schemaToUse,
                ["is_canonical"] = "true",
                ["is_semantic_entity"] = "true",
                ["layer"] = StandardLayers.Foundation.LayerId,
                ["layerId"] = StandardLayers.Foundation.LayerId,
                ["layerName"] = StandardLayers.Foundation.LayerName,
                ["layerColor"] = StandardLayers.Foundation.Color,
                ["layerIcon"] = StandardLayers.Foundation.Icon
            };
            nodesToUpload.Add(new Node(canonicalId, "Database", props));
        }

        if (nodesToUpload.Count > 0)
        {
            await db.UploadNodesAsync(nodesToUpload);
        }

        // 3. Upload relationships targeting consolidated canonical databases
        var relationshipsToUpload = new List<Relationship>();
        var seen = new HashSet<(string From, string To)>();

        foreach (var (owner, rawTarget, propsElem) in projectDbEdges)
        {
            var domain = SyntaxEnricher.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;

            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
            {
                target = dWin;
            }
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
            {
                target = win;
            }

            if (!canonicalNodes.ContainsKey(target))
            {
                continue;
            }

            var targetNode = canonicalNodes.TryGetValue(target, out var tInfo) ? tInfo : default;
            var targetEng = targetNode.Engine ?? (targetNode.Name?.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode.Name ?? "");
            if (IsGenericOrLibraryDbId(target) && concreteEnginesCount.GetValueOrDefault(targetEng) > 0)
            {
                continue;
            }

            if (owner.Id != target && seen.Add((owner.Id, target)))
            {
                var props = new Dictionary<string, object>
                {
                    ["dependency_type"] = "database",
                    ["is_semantic"] = "true",
                    ["is_canonical"] = "true"
                };

                if (propsElem.ValueKind == JsonValueKind.Object)
                {
                    foreach (var jp in propsElem.EnumerateObject())
                    {
                        if (!props.ContainsKey(jp.Name))
                        {
                            props[jp.Name] = jp.Value.ToString();
                        }
                    }
                }

                relationshipsToUpload.Add(new Relationship(owner.Id, target,
                    OntologyConstants.Relationships.UsesDb, props));
            }
        }

        if (relationshipsToUpload.Count > 0)
        {
            await db.UploadRelationshipsAsync(relationshipsToUpload);
        }

        // Apply domain-scoped remappings to edges
        foreach (var (domain, dMap) in domainScopedRemap)
        {
            var domProjIds = projectList.Where(p => {
                var d = SyntaxEnricher.CleanProjectNameToDomain(p.Name);
                if (string.IsNullOrEmpty(d)) d = p.Id;
                return d.Equals(domain, StringComparison.OrdinalIgnoreCase);
            }).Select(p => p.Id).ToList();

            foreach (var (loserId, winnerId) in dMap)
            {
                foreach (var pId in domProjIds)
                {
                    await db.ExecuteWriteAsync(
                        "UPDATE OR IGNORE edges SET to_id = @winnerId WHERE to_id = @loserId AND from_id LIKE @pIdPrefix;",
                        new { winnerId, loserId, pIdPrefix = $"{pId}%" }, cancellationToken);
                }
            }
        }

            // 5. Clean up duplicate / project-scoped database nodes where id != canonicalId
            foreach (var (rawId, canonicalId) in rawToCanonical)
            {
                if (!string.Equals(rawId, canonicalId, StringComparison.Ordinal))
                {
                    await db.ExecuteWriteAsync(
                        "UPDATE OR IGNORE edges SET to_id = @canonicalId WHERE to_id = @rawId;",
                        new { canonicalId, rawId }, cancellationToken);

                    await db.ExecuteWriteAsync("DELETE FROM edges WHERE to_id = @rawId;", new { rawId },
                        cancellationToken);

                    await db.ExecuteWriteAsync(
                        "UPDATE OR IGNORE edges SET from_id = @canonicalId WHERE from_id = @rawId;",
                        new { canonicalId, rawId }, cancellationToken);

                    await db.ExecuteWriteAsync("DELETE FROM edges WHERE from_id = @rawId;", new { rawId },
                        cancellationToken);

                    await db.ExecuteWriteAsync("DELETE FROM nodes WHERE id = @rawId;", new { rawId },
                        cancellationToken);
                }
            }

            // Remove any improper CONTAINS edges pointing to Database nodes (Databases are infrastructure, only contained in semantic_structure)
            await db.ExecuteWriteAsync(
                "DELETE FROM edges WHERE kind = 'CONTAINS' AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database') AND from_id NOT LIKE '%semantic_structure%';",
                null, cancellationToken);

            // Purge any remaining phantom Database nodes that were not canonicalized
            var validIds = canonicalNodes.Keys.ToList();

            if (validIds.Count > 0)
            {
                var placeholders = string.Join(",", validIds.Select((_, i) => $"@p{i}"));
                var paramDict = new Dictionary<string, object>();
                for (int i = 0; i < validIds.Count; i++) paramDict[$"p{i}"] = validIds[i];

                await db.ExecuteWriteAsync(
                    $"DELETE FROM edges WHERE (to_id NOT IN ({placeholders}) AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database')) OR (from_id NOT IN ({placeholders}) AND from_id IN (SELECT id FROM nodes WHERE kind = 'Database'));",
                    paramDict, cancellationToken);

                await db.ExecuteWriteAsync(
                    $"DELETE FROM nodes WHERE kind = 'Database' AND id NOT IN ({placeholders});", paramDict,
                    cancellationToken);
            }
            else
            {
                await db.ExecuteWriteAsync("DELETE FROM edges WHERE to_id IN (SELECT id FROM nodes WHERE kind = 'Database') OR from_id IN (SELECT id FROM nodes WHERE kind = 'Database');", null, cancellationToken);
                await db.ExecuteWriteAsync("DELETE FROM nodes WHERE kind = 'Database';", null, cancellationToken);
            }

            // Purge any invalid Table nodes (e.g. containing spaces like 'ordering api')
            await db.ExecuteWriteAsync(
                "DELETE FROM edges WHERE to_id IN (SELECT id FROM nodes WHERE kind = 'Table' AND json_extract(properties, '$.name') LIKE '% %') OR from_id IN (SELECT id FROM nodes WHERE kind = 'Table' AND json_extract(properties, '$.name') LIKE '% %');",
                null, cancellationToken);
            await db.ExecuteWriteAsync(
                "DELETE FROM nodes WHERE kind = 'Table' AND json_extract(properties, '$.name') LIKE '% %';",
                null, cancellationToken);

            // Propagate canonical engine and schema to child DataSets and Tables
            foreach (var (canonicalId, (cName, _, _, engineToUse)) in canonicalNodes)
            {
                var sch = cName.Contains('.') ? cName.Split('.', 2)[1] : "";
                if (string.IsNullOrWhiteSpace(engineToUse)) continue;

                await db.ExecuteWriteAsync(@"
                    UPDATE nodes 
                    SET properties = json_set(COALESCE(NULLIF(properties, ''), '{}'), '$.engine', @engineToUse, '$.schema', COALESCE(json_extract(properties, '$.name'), @sch))
                    WHERE id IN (
                        SELECT to_id FROM edges WHERE from_id = @canonicalId AND kind = 'CONTAINS'
                    ) AND kind = 'DataSet';",
                    new { canonicalId, engineToUse, sch }, cancellationToken);

                await db.ExecuteWriteAsync(@"
                    UPDATE nodes 
                    SET properties = json_set(COALESCE(NULLIF(properties, ''), '{}'), '$.engine', @engineToUse, '$.schema', @sch)
                    WHERE id IN (
                        SELECT to_id FROM edges WHERE from_id = @canonicalId AND kind = 'CONTAINS'
                    ) AND kind = 'Table';",
                    new { canonicalId, engineToUse, sch }, cancellationToken);

                await db.ExecuteWriteAsync(@"
                    UPDATE nodes 
                    SET properties = json_set(COALESCE(NULLIF(properties, ''), '{}'), 
                        '$.engine', @engineToUse, 
                        '$.schema', COALESCE((SELECT json_extract(ds.properties, '$.name') FROM nodes ds JOIN edges e2 ON ds.id = e2.from_id WHERE e2.to_id = nodes.id AND e2.kind = 'CONTAINS' LIMIT 1), @sch)
                    )
                    WHERE id IN (
                        SELECT e2.to_id FROM edges e1
                        JOIN edges e2 ON e1.to_id = e2.from_id AND e2.kind = 'CONTAINS'
                        WHERE e1.from_id = @canonicalId AND e1.kind = 'CONTAINS'
                    ) AND kind = 'Table';",
                    new { canonicalId, engineToUse, sch }, cancellationToken);
            }

            return new CanonicalizeDatabasesResult(nodesToUpload.Count, relationshipsToUpload.Count);
        }
    }
