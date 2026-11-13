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
        => SyntaxTree.EnrichFileDatabaseSignals(fileNode, sourceText, filePath);

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
        fileParser?.EnrichFileSignals(l1File, syntaxTree, project, sourceText);
    }
}
