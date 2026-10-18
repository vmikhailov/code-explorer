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

    private static readonly HashSet<string> KnownDbEnginesAndNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "postgresql", "postgres", "mysql", "mariadb", "sqlite", "sqlite3",
        "clickhouse", "bigquery", "redis", "mongodb", "mongo",
        "sqlserver", "mssql", "sql server", "oracle", "cassandra",
        "elasticsearch", "neo4j", "dynamodb", "firestore", "cosmosdb",
        "couchdb", "cockroachdb", "tidb", "influxdb", "timescaledb",
        "memcached", "kafka", "rabbitmq", "database", "db", "default",
        "typeorm", "sequelize", "prisma", "drizzle", "dapper", "ef-core"
    };

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
            schemaPart = WorkspaceConventions.NormalizeServiceName(schemaPart);
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
            schemaPart = WorkspaceConventions.NormalizeServiceName(schemaPart);
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
                var schemaPart = WorkspaceConventions.NormalizeServiceName(trimmed);
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

            // 1. Check singular vs plural schema pairs
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
                        // If s2 is plural of s1 (s1 + "s" or s1 + "es"), map n1 (singular) -> n2 (plural)
                        if (s2.Equals(s1 + "s", StringComparison.OrdinalIgnoreCase) ||
                            s2.Equals(s1 + "es", StringComparison.OrdinalIgnoreCase))
                        {
                            remap[n1.Id] = n2.Id;
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
                // Prefer a plural or longest schema concrete node
                var winner = concreteNodes
                    .OrderByDescending(n => {
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

        // 2. Gather project references and consolidate databases per service/project
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

                    projectDbEdges.Add((owner, canonicalTarget, rel));
                }
            }
        }

        var edgesByService = projectDbEdges
            .GroupBy(e => {
                var domain = SyntaxEnricher.CleanProjectNameToDomain(e.Owner.Name);
                return !string.IsNullOrEmpty(domain) ? domain : e.Owner.Id;
            }, StringComparer.OrdinalIgnoreCase);

        var globalConsolidationRemap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sGroup in edgesByService)
        {
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
                });

            var localRemap = ResolveDatabaseConsolidations(candidateNodes);
            foreach (var (loser, winner) in localRemap)
            {
                globalConsolidationRemap[loser] = winner;
            }
        }

        if (globalConsolidationRemap.Count > 0)
        {
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
                canonicalDbNodes.Remove(loserId);
            }
        }

        // 3. Add canonical nodes to SemanticStructure and upload
        if (ctx.SemanticStructure != null)
        {
            if (!string.IsNullOrWhiteSpace(primaryRelationalEngine))
            {
                ctx.SemanticStructure.Children.RemoveAll(c => c is DatabaseNode dn &&
                    (string.Equals(dn.Name, "Database", StringComparison.OrdinalIgnoreCase) ||
                     dn.Id.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase) ||
                     dn.Id.EndsWith($":{OntologyConstants.IdPrefixes.Database}:relational:database", StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var node in canonicalDbNodes.Values)
            {
                if (!ctx.SemanticStructure.Children.Any(c => c.Id == node.Id))
                {
                    ctx.SemanticStructure.Children.Add(node);
                }
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, node.Name, node.Id);
            }
        }

        if (canonicalDbNodes.Count > 0)
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
            var target = globalConsolidationRemap.TryGetValue(rawTarget, out var win) ? win : rawTarget;
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

        if (canonicalRels.Count > 0)
        {
            ctx.LogDebug($"[PostIndexAnalyzer] Materializing {canonicalRels.Count} direct project->canonical database relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(canonicalRels);
            ctx.AddRelsCount(canonicalRels.Count);
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
        var projectList = new List<(string Id, string Name, string? Path, bool IsTest)>();
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

                if (!string.IsNullOrEmpty(pId))
                {
                    projectList.Add((pId, pName, pPath, isTest));
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

        var projectDbEdges = new List<((string Id, string Name) Owner, string CanonicalTarget, JsonElement Properties)>();

        using (var edgesDoc = JsonDocument.Parse(relEdgesJson))
        {
            foreach (var row in edgesDoc.RootElement.EnumerateArray())
            {
                var src = GetStringProp(row, "source");
                var tgt = GetStringProp(row, "target");
                if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

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

        var edgesByService = projectDbEdges
            .GroupBy(e => {
                var domain = SyntaxEnricher.CleanProjectNameToDomain(e.Owner.Name);
                return !string.IsNullOrEmpty(domain) ? domain : e.Owner.Id;
            }, StringComparer.OrdinalIgnoreCase);

        var globalConsolidationRemap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sGroup in edgesByService)
        {
            var candidateNodes = sGroup
                .Select(e => canonicalNodes.TryGetValue(e.CanonicalTarget, out var info) ? (e.CanonicalTarget, info) : default)
                .Where(t => t.CanonicalTarget != null)
                .Select(t => {
                    var name = t.info.Name;
                    var engine = t.info.Engine;
                    var schema = name.Contains('.') ? name.Split('.', 2)[1] : "";
                    return (t.CanonicalTarget, name, engine, schema);
                });

            var localRemap = ResolveDatabaseConsolidations(candidateNodes);
            foreach (var (loser, winner) in localRemap)
            {
                globalConsolidationRemap[loser] = winner;
            }
        }

        if (globalConsolidationRemap.Count > 0)
        {
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
            var target = globalConsolidationRemap.TryGetValue(rawTarget, out var win) ? win : rawTarget;
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

            return new CanonicalizeDatabasesResult(nodesToUpload.Count, relationshipsToUpload.Count);
        }
    }
