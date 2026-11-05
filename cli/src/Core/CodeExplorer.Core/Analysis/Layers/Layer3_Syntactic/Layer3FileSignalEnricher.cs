using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Parser.Layers;

/// <summary>
/// Extracts file-level semantic, route, constant, and database schema signals once during Layer 3
/// while the file text is already in memory, attaching structured extensions to <see cref="FileNode"/>
/// and <see cref="ProjectNode"/> so Layer 4 and Layer 5 never read raw file text.
/// </summary>
public static class Layer3FileSignalEnricher
{
    private static readonly Regex SchemaCallRegex = new(@"HasDefaultSchema\s*\(\s*[""']?([^""'\)]+)[""']?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SchemaConstRegex = new(@"(?:Schema_?Name|Default_?Schema_?Name|Default_?Schema)\s*(?::\s*[^=]+)?=\s*[""']?([^""';]+)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TypeOrmSchemaRegex = new(@"@(?:Entity|ViewEntity|Table)\s*\(\s*\{[^}]*schema\s*:\s*[""']?([A-Za-z0-9_.]+)[""']?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ToTableSchemaRegex = new(@"ToTable\s*\(\s*[^,]+\s*,\s*(?:schema:\s*)?[""']?([A-Za-z0-9_.]+)[""']?\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DataSourceSchemaRegex = new(@"\b(?:schema|defaultSchema)\s*:\s*[""']([A-Za-z0-9_-]+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EntityOrTableDeclRegex = new(@"(?::\s*(?:[A-Za-z0-9_.]+\.)?(?:Identity)?DbContext\b|\bI?DbSet\s*<|\b__tablename__\b|@(?:Entity|Table|ViewEntity)\b|\[\s*Table\s*\(|\bdeclarative_base\b|\bmodelBuilder\.Entity\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public const string ExtHasEntityOrTable = "has_entity_or_table";
    public const string ExtDbContextBaseConn = "dbcontext_base_conn";
    public const string ExtDbContextClasses = "dbcontext_classes";
    public const string ExtDeclaredSchemaRaw = "declared_schema_raw";
    public const string ExtVerifiedDbSignals = "verified_db_signals";
    public const string ExtDbDriverEngine = "db_driver_engine";
    public const string ExtDbDriverType = "db_driver_type";
    public const string ExtDbDriverChecked = "db_driver_checked";
    public const string ExtRustTopologySummary = "rust_topology_summary";
    public const string ExtRustTopologyBodySummary = "rust_topology_body_summary";
    private const string SigPg = "pg";
    private const string SigMongo = "mongo";
    private const string SigRedis = "redis";
    private const string SigMySql = "mysql";
    private const string SigSqlite = "sqlite";
    private const string SigSqlServer = "sqlserver";
    private const string SigConnStr = "connstr";

    public static void EnrichFileDatabaseSignals(FileNode? fileNode, string? sourceText, string filePath)
    {
        if (fileNode == null || string.IsNullOrEmpty(sourceText)) return;

        var signals = fileNode.Signals();
        if (EntityOrTableDeclRegex.IsMatch(sourceText))
        {
            signals.HasEntityOrTable = true;
            fileNode.Flags |= SymbolFlags.HasEntityOrTable;
        }

        var rawSchema = ExtractRawSchemaToken(sourceText, filePath);
        if (!string.IsNullOrEmpty(rawSchema))
        {
            signals.DeclaredSchemaRaw = rawSchema;
        }
    }

    private static string? ExtractRawSchemaToken(string text, string filePath)
    {
        if (!text.Contains("schema", StringComparison.OrdinalIgnoreCase) &&
            !text.Contains("ToTable", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var m1 = SchemaCallRegex.Match(text);
        if (m1.Success)
        {
            return m1.Groups[1].Value.Trim().Trim('\'', '"');
        }

        var m2 = SchemaConstRegex.Match(text);
        if (m2.Success)
        {
            return m2.Groups[1].Value.Trim().Trim('\'', '"');
        }

        var m3 = TypeOrmSchemaRegex.Match(text);
        if (m3.Success)
        {
            return m3.Groups[1].Value.Trim().Trim('\'', '"');
        }

        var m4 = ToTableSchemaRegex.Match(text);
        if (m4.Success)
        {
            return m4.Groups[1].Value.Trim().Trim('\'', '"');
        }

        var m5 = DataSourceSchemaRegex.Match(text);
        if (m5.Success)
        {
            var val = m5.Groups[1].Value.Trim().Trim('\'', '"');
            if (!val.Equals("true", StringComparison.OrdinalIgnoreCase) &&
                !val.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return val;
            }
        }

        return null;
    }

    public static void EnrichFileSemanticSignalsAndRouteConstants(
        FileNode l1File,
        SyntaxTree syntaxTree,
        ProjectNode project,
        ParsingContext ctx,
        Func<string, bool> isRouteOrConfigCandidate)
    {
        var sourceText = syntaxTree.Document?.SourceText ?? l1File.PreloadedContent;
        l1File.PreloadedContent = null;
        syntaxTree.FileNode.PreloadedContent = null;

        if (string.IsNullOrEmpty(sourceText)) return;

        EnrichFileDatabaseSignals(syntaxTree.FileNode, sourceText, l1File.FullPath);
        if (!ReferenceEquals(l1File, syntaxTree.FileNode))
        {
            EnrichFileDatabaseSignals(l1File, sourceText, l1File.FullPath);
        }

        // 1. Pre-scan route & config candidates (in-memory on already-read text)
        if (!ctx.LightweightAstScanMode &&
            sourceText.Length <= 1024 * 1024 &&
            isRouteOrConfigCandidate(l1File.Path) &&
            !SharedFileParserRegistry.CanParse(l1File.Name))
        {
            RouteDictionaryRegistry.ScanServiceDomains(sourceText);
            RouteDictionaryRegistry.ScanAndRegister(sourceText);
            ConstantRegistry.ScanAndRegisterRegexOnly(l1File.FullPath, sourceText, project.Name);
        }

        // 2. Database verification signals for Layer 5 PostIndexAnalyzer.Databases.InMemory.Consolidation
        var dbSignals = new List<string>(4);
        if (sourceText.Contains("PostgresOptions", StringComparison.OrdinalIgnoreCase) || sourceText.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigPg);
        if (sourceText.Contains("MongoOptions", StringComparison.OrdinalIgnoreCase) || sourceText.Contains("MongoDB", StringComparison.OrdinalIgnoreCase) || sourceText.Contains("MongoDbContext", StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigMongo);
        if (sourceText.Contains("RedisOptions", StringComparison.OrdinalIgnoreCase) || sourceText.Contains("StackExchange.Redis", StringComparison.OrdinalIgnoreCase) || sourceText.Contains("ConnectionMultiplexer", StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigRedis);
        if (sourceText.Contains("MySqlOptions", StringComparison.OrdinalIgnoreCase) || sourceText.Contains("MySql", StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigMySql);
        if (sourceText.Contains("SqliteOptions", StringComparison.OrdinalIgnoreCase) || sourceText.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigSqlite);
        if (sourceText.Contains("SqlServerOptions", StringComparison.OrdinalIgnoreCase) || sourceText.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigSqlServer);
        if (sourceText.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase) || sourceText.Contains("GetConnectionString", StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigConnStr);

        if (dbSignals.Count > 0)
        {
            var joined = string.Join(",", dbSignals);
            l1File.Signals().VerifiedDbSignals = joined;
            syntaxTree.FileNode.Signals().VerifiedDbSignals = joined;
        }

        // 3. Language-specific file signal enrichment delegated to the file parser
        var fileParser = syntaxTree.FileParser ?? WorkspaceIndexer.GetParserForFile(l1File.FullPath);
        fileParser?.EnrichFileSignals(l1File, syntaxTree, project, sourceText, ctx);
    }
}
