using System.Collections.Frozen;
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
    private const string CommaSeparator = ",";
    private const char CommaChar = ',';
    private const string SoqlWithDataCategory = "WITH DATA CATEGORY";
    private const string TokenPostgresOptions = "PostgresOptions";
    private const string TokenNpgsql = "Npgsql";
    private const string TokenMongoOptions = "MongoOptions";
    private const string TokenMongoDb = "MongoDB";
    private const string TokenMongoDbContext = "MongoDbContext";
    private const string TokenRedisOptions = "RedisOptions";
    private const string TokenStackExchangeRedis = "StackExchange.Redis";
    private const string TokenConnectionMultiplexer = "ConnectionMultiplexer";
    private const string TokenMySqlOptions = "MySqlOptions";
    private const string TokenMySql = "MySql";
    private const string TokenSqliteOptions = "SqliteOptions";
    private const string TokenSqlite = "Sqlite";
    private const string TokenSqlServerOptions = "SqlServerOptions";
    private const string TokenSqlClient = "SqlClient";
    private const string TokenConnectionStrings = "ConnectionStrings";
    private const string TokenGetConnectionString = "GetConnectionString";

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

        // 1. Pre-scan route & config candidates (in-memory on comment-stripped non-test text)
        if (!ctx.LightweightAstScanMode &&
            sourceText.Length <= 1024 * 1024 &&
            !WorkspaceConventions.IsTestFilePath(l1File.Path) &&
            isRouteOrConfigCandidate(l1File.Path) &&
            !SharedFileParserRegistry.CanParse(l1File.Name))
        {
            var strippedText = ConstantRegistry.StripCommentsPreservingStrings(sourceText);
            RouteDictionaryRegistry.ScanServiceDomains(strippedText);
            RouteDictionaryRegistry.ScanAndRegister(strippedText);
            ConstantRegistry.ScanAndRegisterRegexOnly(l1File.FullPath, strippedText, project.Name);
        }

        // 2. Database verification signals for Layer 5 PostIndexAnalyzer.Databases.InMemory.Consolidation
        var dbSignals = new List<string>(4);
        if (sourceText.Contains(TokenPostgresOptions, StringComparison.OrdinalIgnoreCase) || sourceText.Contains(TokenNpgsql, StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigPg);
        if (sourceText.Contains(TokenMongoOptions, StringComparison.OrdinalIgnoreCase) || sourceText.Contains(TokenMongoDb, StringComparison.OrdinalIgnoreCase) || sourceText.Contains(TokenMongoDbContext, StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigMongo);
        if (sourceText.Contains(TokenRedisOptions, StringComparison.OrdinalIgnoreCase) || sourceText.Contains(TokenStackExchangeRedis, StringComparison.OrdinalIgnoreCase) || sourceText.Contains(TokenConnectionMultiplexer, StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigRedis);
        if (sourceText.Contains(TokenMySqlOptions, StringComparison.OrdinalIgnoreCase) || sourceText.Contains(TokenMySql, StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigMySql);
        if (sourceText.Contains(TokenSqliteOptions, StringComparison.OrdinalIgnoreCase) || sourceText.Contains(TokenSqlite, StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigSqlite);
        if (sourceText.Contains(TokenSqlServerOptions, StringComparison.OrdinalIgnoreCase) || sourceText.Contains(TokenSqlClient, StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigSqlServer);
        if (sourceText.Contains(TokenConnectionStrings, StringComparison.OrdinalIgnoreCase) || sourceText.Contains(TokenGetConnectionString, StringComparison.OrdinalIgnoreCase))
            dbSignals.Add(SigConnStr);

        if (dbSignals.Count > 0)
        {
            var joined = string.Join(CommaSeparator, dbSignals);
            l1File.Signals().VerifiedDbSignals = joined;
            syntaxTree.FileNode.Signals().VerifiedDbSignals = joined;
        }

        // 2b. SQL evidence for the generic in-house DB-wrapper detection in Layer 4:
        // documents can be released after Layer 3 (PipelineMemoryEvictor), so the call-shape,
        // SQL-literal, and schema-qualified table evidence is computed here while the AST is
        // still in memory.
        if (!ctx.LightweightAstScanMode)
        {
            var hasSqlCalls = HasStandardSqlClientCallShape(syntaxTree);
            var (hasSqlLiteralNodes, schemaCandidates) = InspectSqlLiteralsAndSchemas(syntaxTree);
            var hasSqlLiterals = hasSqlCalls || hasSqlLiteralNodes;
            if (hasSqlCalls || hasSqlLiterals)
            {
                l1File.Signals().HasSqlClientCallShape = hasSqlCalls;
                l1File.Signals().HasSqlLiteralEvidence = hasSqlLiterals;
                l1File.Signals().SqlSchemaCandidates = schemaCandidates;
                syntaxTree.FileNode.Signals().HasSqlClientCallShape = hasSqlCalls;
                syntaxTree.FileNode.Signals().HasSqlLiteralEvidence = hasSqlLiterals;
                syntaxTree.FileNode.Signals().SqlSchemaCandidates = schemaCandidates;

                // SQL literals and DB-client calls often live in different files of the same
                // project (spec/query modules vs store implementations), so schema candidates
                // and literal evidence are also aggregated at project level (thread-safe:
                // Layer 3 Step 1 is parallel).
                {
                    var projSignals = project.Signals();
                    lock (projSignals)
                    {
                        if (!string.IsNullOrEmpty(schemaCandidates))
                        {
                            var merged = (projSignals.SqlSchemaCandidates?
                                              .Split(CommaChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
                                .Union(schemaCandidates.Split(CommaChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                                    StringComparer.OrdinalIgnoreCase)
                                .Take(10);
                            projSignals.SqlSchemaCandidates = string.Join(CommaChar, merged);
                        }
                        if (hasSqlLiterals)
                        {
                            projSignals.HasSqlLiteralEvidence = true;
                        }
                    }
                }
            }
        }

        // 3. Language-specific file signal enrichment delegated to the file parser
        var fileParser = syntaxTree.FileParser ?? WorkspaceIndexer.GetParserForFile(l1File.FullPath);
        fileParser?.EnrichFileSignals(l1File, syntaxTree, project, sourceText);
    }

    /// <summary>
    /// Standard DB-client method surface shared by database/sql, sqlx, and DB-API style
    /// wrappers (including in-house packages wrapping those drivers). Only unambiguous
    /// database verbs are used — generic verbs like Get/Select are excluded to avoid
    /// confusing HTTP clients and caches with database access.
    /// </summary>
    private static readonly FrozenSet<string> StandardSqlClientMethodNames = new[]
    {
        "Query", "QueryRow", "QueryContext", "QueryRowContext",
        "Exec", "ExecContext", "NamedExec", "Execer",
        "Prepare", "PrepareContext", "PrepareNamed",
        "Begin", "BeginTx", "Commit", "Rollback",
        "Ping", "PingContext",
        "SelectContext", "GetContext", "Rebind"
    }.ToFrozenSet(StringComparer.Ordinal);

    internal static bool HasStandardSqlClientCallShape(SyntaxTree syntaxTree)
    {
        var calls = syntaxTree.Document?.AllCalls;
        if (calls == null || calls.Count == 0) return false;

        foreach (var call in calls)
        {
            if (!string.IsNullOrEmpty(call.MethodName) &&
                StandardSqlClientMethodNames.Contains(call.MethodName))
            {
                return true;
            }
        }
        return false;
    }

    private static readonly Regex SchemaQualifiedTablePattern = new(
        @"(?:FROM|INTO|UPDATE|JOIN|TABLE)\s+([a-zA-Z_][a-zA-Z0-9_]{2,63})\.[a-zA-Z_][a-zA-Z0-9_]{2,63}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly FrozenSet<string> NonSchemaQualifiers = new[]
    {
        "the", "this", "tmp", "temp", "sql", "data", "test", "demo", "sample", "log", "logs"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex SalesforceObjectPattern = new(
        @"(?:FROM|INTO|UPDATE|DELETE\s+FROM)\s+""?[a-zA-Z_][a-zA-Z0-9_]*__(?:c|kav|ka|mdt|e|b|x|hd|Share|History|Feed|Tag|ChangeEvent)""?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Single-pass inspection of the file's AST string literals to detect SQL-shaped literal evidence
    /// and extract up to 5 distinct schema-qualified table references (e.g. "FROM billing.orders").
    /// </summary>
    internal static (bool HasSqlLiterals, string? SchemaCandidates) InspectSqlLiteralsAndSchemas(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.Document;
        if (root == null) return (false, null);

        var hasSqlLiterals = false;
        var schemas = new List<string>();
        InspectSqlLiteralsRecursive(root, ref hasSqlLiterals, schemas);
        return (hasSqlLiterals, schemas.Count > 0 ? string.Join(CommaChar, schemas) : null);
    }

    internal static bool HasSqlShapedStringLiterals(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.Document;
        if (root == null) return false;
        return ContainsSqlShapedLiteral(root);
    }

    private static bool LooksLikeSalesforceSoql(string text) =>
        text.Contains(SoqlWithDataCategory, StringComparison.OrdinalIgnoreCase) ||
        SalesforceObjectPattern.IsMatch(text);

    private static readonly Regex SqlKeywordPrefixPattern = new(
        @"^[\s""'`\(]*(?:--[^\n]*\n[\s""'`\(]*|/\*[\s\S]*?\*/[\s""'`\(]*)*(?:SELECT|INSERT|UPDATE|DELETE|MERGE|REPLACE|CREATE|ALTER|DROP|TRUNCATE|WITH|EXEC|EXECUTE|CALL)(?:[\s/\-\*\(\[""'`])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsSqlCandidateLiteral(ReadOnlySpan<char> span) =>
        span.Length is > 12 and < 4096 && SqlKeywordPrefixPattern.IsMatch(span);

    private static void InspectSqlLiteralsRecursive(
        CodeExplorer.Core.Parser.Syntax.SyntaxNode node,
        ref bool hasSqlLiterals,
        List<string> schemas)
    {
        if (hasSqlLiterals && schemas.Count >= 5) return;

        if (node is CodeExplorer.Core.Parser.Syntax.LiteralExprSyntax
            {
                LiteralKind: CodeExplorer.Core.Parser.Syntax.LiteralSyntaxKind.String or CodeExplorer.Core.Parser.Syntax.LiteralSyntaxKind.RawString
            })
        {
            var span = node.AsSpan().Trim();
            if (IsSqlCandidateLiteral(span))
            {
                var text = span.ToString();
                if (!LooksLikeSalesforceSoql(text))
                {
                    hasSqlLiterals = true;
                    if (schemas.Count < 5)
                    {
                        foreach (var match in SchemaQualifiedTablePattern.Matches(text).Cast<Match>())
                        {
                            var candidate = match.Groups[1].Value;
                            if (!NonSchemaQualifiers.Contains(candidate) &&
                                !schemas.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                            {
                                schemas.Add(candidate);
                                if (schemas.Count >= 5) break;
                            }
                        }
                    }
                }
            }
        }

        foreach (var child in node.Children)
        {
            InspectSqlLiteralsRecursive(child, ref hasSqlLiterals, schemas);
            if (hasSqlLiterals && schemas.Count >= 5) return;
        }
    }

    private static bool ContainsSqlShapedLiteral(CodeExplorer.Core.Parser.Syntax.SyntaxNode node)
    {
        if (node is CodeExplorer.Core.Parser.Syntax.LiteralExprSyntax
            {
                LiteralKind: CodeExplorer.Core.Parser.Syntax.LiteralSyntaxKind.String or CodeExplorer.Core.Parser.Syntax.LiteralSyntaxKind.RawString
            })
        {
            var span = node.AsSpan().Trim();
            if (IsSqlCandidateLiteral(span) && !LooksLikeSalesforceSoql(span.ToString()))
            {
                return true;
            }
        }

        foreach (var child in node.Children)
        {
            if (ContainsSqlShapedLiteral(child)) return true;
        }
        return false;
    }
}
