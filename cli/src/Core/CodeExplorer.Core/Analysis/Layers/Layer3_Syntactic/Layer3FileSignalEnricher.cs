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
    private static readonly Regex CSharpDbContextClassRegex = new(@"class\s+([A-Za-z0-9_]+)\s*:\s*(?:[A-Za-z0-9_.]+\.)?(?:Identity)?DbContext\b", RegexOptions.Compiled);
    private static readonly Regex CSharpBaseConnectionNameRegex = new(@":\s*base\s*\(\s*@?[""'](?:name\s*=\s*)?([A-Za-z0-9_.\-:]+)[""']\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RustDeclarationLinePattern = new(
        @"^[ \t]*(?:#\[.*|(?:pub(?:\([^)]*\))?[ \t]+)?[A-Za-z_][A-Za-z0-9_]*[ \t]*(?::[^:].*|,[ \t]*(?://.*)?\r?)|(?:pub(?:\([^)]*\))?[ \t]+)?(?:async[ \t]+)?fn[ \t]+.*)$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex RustEncryptedSharesFieldRegex = new(@"\b(?:encrypted_)?shares?_bucket(?:_name)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustDbChunksFieldRegex = new(@"\b(?:(?:db|database)_)?(?:chunks?|wal|parquet)_bucket(?:_name)?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustGraphCheckpointFieldRegex = new(@"\b(?:graph_)?checkpoint_(?:s3_client|bucket(?:_name)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustKmsFieldRegex = new(@"\b(?:kms_.*key|key_.*kms|kms_key_arns?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustS3ReadRegex = new(@"\b(?:get_object|head_object|list_objects(?:_v2)?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustGraphReadRegex = new(@"\b(?:download_graph_checkpoint\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustS3WriteRegex = new(@"\b(?:put_object|upload_part|complete_multipart_upload)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustGraphWriteRegex = new(@"\b(?:upload_graph_checkpoint\w*)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustKmsReadRegex = new(@"\b(?:decrypt|describe_key|get_public_key)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustKmsWriteRegex = new(@"\b(?:encrypt|generate_data_key|create_key)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustPeerConfigRegex = new(@"\b(?:party_id|node_id|peer_id|node_hostnames|service_ports|service_outbound_ports)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustPeerListenerRegex = new(@"(?:TcpListener::bind|Server::builder|axum::serve)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustPeerConnectorRegex = new(@"(?:TcpStream::connect|Endpoint::connect|Channel::from_shared|connect_lazy)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RustPeerAddressingUsageRegex = new(@"\.\s*(?:node_hostnames|service_ports|service_outbound_ports|peer_addresses)\b", RegexOptions.Compiled);

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
    private const string TrueValue = "true";
    private const string SigPg = "pg";
    private const string SigMongo = "mongo";
    private const string SigRedis = "redis";
    private const string SigMySql = "mysql";
    private const string SigSqlite = "sqlite";
    private const string SigSqlServer = "sqlserver";
    private const string SigConnStr = "connstr";
    private const string TokEncryptedSharesBucket = "encrypted_shares_bucket";
    private const string TokDatabaseChunksBucket = "database_chunks_bucket";
    private const string TokGraphCheckpointBucket = "graph_checkpoint_bucket";
    private const string TokKmsKeyArns = "kms_key_arns";
    private const string TokGetObject = "get_object";
    private const string TokDownloadGraphCheckpoint = "download_graph_checkpoint";
    private const string TokPutObject = "put_object";
    private const string TokUploadGraphCheckpoint = "upload_graph_checkpoint";
    private const string TokDecrypt = "decrypt";
    private const string TokEncrypt = "encrypt";
    private const string TokPartyId = "party_id";
    private const string TokTcpListenerBind = "TcpListener::bind";
    private const string TokTcpStreamConnect = "TcpStream::connect";
    private const string TokPeerAddresses = ".peer_addresses";
    private const string TokAxumServe = "axum::serve";
    private const string TokCoordination = "coordination";
    private const string TokTonic = "tonic";
    private const string TokNccl = "nccl";

    public static void EnrichFileDatabaseSignals(FileNode? fileNode, string? sourceText, string filePath)
    {
        if (fileNode == null || string.IsNullOrEmpty(sourceText)) return;

        if (EntityOrTableDeclRegex.IsMatch(sourceText))
        {
            fileNode.HasEntityOrTable = true;
            fileNode.Flags |= SymbolFlags.HasEntityOrTable;
        }

        if (sourceText.Contains("DbContext", StringComparison.Ordinal))
        {
            var baseMatch = CSharpBaseConnectionNameRegex.Match(sourceText);
            if (baseMatch.Success)
            {
                fileNode.DbContextBaseConn = baseMatch.Groups[1].Value.Trim();
            }

            var ctxMatches = CSharpDbContextClassRegex.Matches(sourceText);
            if (ctxMatches.Count > 0)
            {
                var classes = new List<string>(ctxMatches.Count);
                foreach (Match m in ctxMatches)
                {
                    var className = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(className) &&
                        !className.Equals("DbContext", StringComparison.OrdinalIgnoreCase) &&
                        !className.Equals("IdentityDbContext", StringComparison.OrdinalIgnoreCase))
                    {
                        classes.Add(className);
                    }
                }
                if (classes.Count > 0)
                {
                    fileNode.DbContextClasses = string.Join(",", classes);
                }
            }
        }

        var rawSchema = ExtractRawSchemaToken(sourceText, filePath);
        if (!string.IsNullOrEmpty(rawSchema))
        {
            fileNode.DeclaredSchemaRaw = rawSchema;
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
        if (!ctx.LightweightAstScanMode && sourceText.Length <= 1024 * 1024)
        {
            if (isRouteOrConfigCandidate(l1File.Path) && !SharedFileParserRegistry.CanParse(l1File.Name))
            {
                RouteDictionaryRegistry.ScanServiceDomains(sourceText);
                RouteDictionaryRegistry.ScanAndRegister(sourceText);
                ConstantRegistry.ScanAndRegisterRegexOnly(l1File.FullPath, sourceText, project.Name);
            }
            else if (l1File.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                     sourceText.Contains("const string", StringComparison.Ordinal))
            {
                ConstantRegistry.ScanAndRegisterCSharp(sourceText, project.Name);
            }
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
            l1File.VerifiedDbSignals = joined;
            syntaxTree.FileNode.VerifiedDbSignals = joined;
        }

        // 3. TypeScript config/datasource driver hint for Layer 4 ResourceSemanticAnalyzer.Drivers
        if (l1File.Name.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) &&
            (l1File.Name.Contains("config", StringComparison.OrdinalIgnoreCase) || l1File.Name.Contains("datasource", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrEmpty(project.DbDriverEngine))
            {
                var lower = sourceText.ToLowerInvariant();
                if (lower.Contains("type: 'postgres'") || lower.Contains("type: \"postgres\"") ||
                    lower.Contains("dialect: 'postgres'") || lower.Contains("dialect: \"postgres\""))
                {
                    project.DbDriverEngine = "PostgreSQL";
                    project.DbDriverFamily = "relational";
                }
                else if (lower.Contains("type: 'mysql'") || lower.Contains("type: \"mysql\"") ||
                         lower.Contains("dialect: 'mysql'") || lower.Contains("dialect: \"mysql\""))
                {
                    project.DbDriverEngine = "MySQL";
                    project.DbDriverFamily = "relational";
                }
                else if (lower.Contains("type: 'sqlite'") || lower.Contains("type: \"sqlite\"") ||
                         lower.Contains("dialect: 'sqlite'") || lower.Contains("dialect: \"sqlite\""))
                {
                    project.DbDriverEngine = "SQLite";
                    project.DbDriverFamily = "relational";
                }
            }
        }

        // 4. Rust topology summary for Layer 4 SemanticTopologySynthesizer
        if (l1File.Name.EndsWith(".rs", StringComparison.OrdinalIgnoreCase) && !WorkspaceConventions.IsTestFilePath(l1File.Path))
        {
            var fullSummary = BuildRustTopologySummary(sourceText);
            if (!string.IsNullOrEmpty(fullSummary))
            {
                l1File.RustTopologySummary = fullSummary;
                syntaxTree.FileNode.RustTopologySummary = fullSummary;
                var bodyOnly = RustDeclarationLinePattern.Replace(sourceText, string.Empty);
                var bodySummary = BuildRustTopologySummary(bodyOnly);
                if (!string.IsNullOrEmpty(bodySummary))
                {
                    l1File.RustTopologyBodySummary = bodySummary;
                    syntaxTree.FileNode.RustTopologyBodySummary = bodySummary;
                }
            }
        }
    }

    private static string BuildRustTopologySummary(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var tokens = new List<string>(12);
        if (RustEncryptedSharesFieldRegex.IsMatch(text)) tokens.Add(TokEncryptedSharesBucket);
        if (RustDbChunksFieldRegex.IsMatch(text)) tokens.Add(TokDatabaseChunksBucket);
        if (RustGraphCheckpointFieldRegex.IsMatch(text)) tokens.Add(TokGraphCheckpointBucket);
        if (RustKmsFieldRegex.IsMatch(text)) tokens.Add(TokKmsKeyArns);
        if (RustS3ReadRegex.IsMatch(text)) tokens.Add(TokGetObject);
        if (RustGraphReadRegex.IsMatch(text)) tokens.Add(TokDownloadGraphCheckpoint);
        if (RustS3WriteRegex.IsMatch(text)) tokens.Add(TokPutObject);
        if (RustGraphWriteRegex.IsMatch(text)) tokens.Add(TokUploadGraphCheckpoint);
        if (RustKmsReadRegex.IsMatch(text)) tokens.Add(TokDecrypt);
        if (RustKmsWriteRegex.IsMatch(text)) tokens.Add(TokEncrypt);
        if (RustPeerConfigRegex.IsMatch(text)) tokens.Add(TokPartyId);
        if (RustPeerListenerRegex.IsMatch(text)) tokens.Add(TokTcpListenerBind);
        if (RustPeerConnectorRegex.IsMatch(text)) tokens.Add(TokTcpStreamConnect);
        if (RustPeerAddressingUsageRegex.IsMatch(text)) tokens.Add(TokPeerAddresses);
        if (text.Contains(TokAxumServe, StringComparison.Ordinal)) tokens.Add(TokAxumServe);
        if (text.Contains(TokCoordination, StringComparison.OrdinalIgnoreCase)) tokens.Add(TokCoordination);
        if (text.Contains(TokTonic, StringComparison.OrdinalIgnoreCase)) tokens.Add(TokTonic);
        if (text.Contains(TokNccl, StringComparison.OrdinalIgnoreCase)) tokens.Add(TokNccl);
        return tokens.Count == 0 ? string.Empty : string.Join(' ', tokens);
    }
}
