using System.Buffers;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Parser;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Database;

public partial class SqliteGraphClient : IGraphClient, IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ILogger _logger;
    private bool _isDisposed;
    private bool _isBulkLoading;
    private bool _bulkIsFreshDb;
    private bool _bulkDropIdentifierIndex;
    private readonly Dictionary<string, (NodeKind Kind, string PropsJson)> _bulkNodes = new(StringComparer.Ordinal);
    private readonly Dictionary<(string From, string To, RelationshipKind Kind), Relationship> _bulkRelationships = new();
    private readonly Dictionary<string, List<string>> _bulkProjectExternalApis = new(StringComparer.Ordinal);

    public ILogger Logger
    {
        get => _logger;
        set => _logger = value ?? NullLogger.Instance;
    }

    private GraphContext? _graph;
    private IGraphRepository? _repository;

    public int CommandTimeoutSeconds { get; set; } = 15;
    public int? MaxPathTraversals { get; set; } = SqliteCompiler.DefaultMaxPathTraversals;
    public string DbPath => _conn.DataSource;
    public SqliteConnection Connection => _conn;
    public GraphContext Graph => _graph ??= new(this);
    public IGraphRepository AsRepository() => _repository ??= new PersistedGraphRepository(this);

    public const int CurrentSchemaVersion = 6;
    public int SchemaVersion { get; private set; }
    public bool IsSchemaOutdated => SchemaVersion < CurrentSchemaVersion;
    public bool WasMigratedOnOpen { get; private set; }
    public int? MigratedFromVersion { get; private set; }

    public SqliteGraphClient(string connectionStringOrPath, ILogger<SqliteGraphClient>? logger = null)
    {
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        var cs = ResolveConnectionString(connectionStringOrPath);
        _conn = new SqliteConnection(cs);
        _conn.Open();

        InitializePragmas();
        SqliteCypherFunctions.Register(_conn);
        CheckSchemaVersion();
    }

    private static string ResolveConnectionString(string connectionStringOrPath)
    {
        if (connectionStringOrPath.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return "Data Source=:memory:;Mode=Memory;Cache=Shared";
        }

        if (connectionStringOrPath.Contains('='))
        {
            return connectionStringOrPath;
        }

        var fullPath = Path.GetFullPath(connectionStringOrPath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return $"Data Source={fullPath};Pooling=False";
    }



    public async Task BeginBulkLoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isBulkLoading) return;

            const int wsKind = (int)NodeKind.Workspace;
            var checkNonWsSql = $"SELECT count(*) FROM nodes WHERE kind != {wsKind} LIMIT 1;";
            var selectWsSql = $"SELECT id AS Id, json(properties) AS PropsJson FROM nodes WHERE kind = {wsKind};";
            var hasNonWorkspaceNodes = await _conn.ExecuteScalarAsync<int>(checkNonWsSql) > 0;
            var hasEdges = await _conn.ExecuteScalarAsync<int>("SELECT count(*) FROM edges LIMIT 1;") > 0;
            _bulkIsFreshDb = !hasNonWorkspaceNodes && !hasEdges;

            _bulkNodes.Clear();
            _bulkRelationships.Clear();
            _bulkProjectExternalApis.Clear();
            _bulkDropIdentifierIndex = false;
            _isBulkLoading = true;

            if (_bulkIsFreshDb)
            {
                var wsRows = await _conn.QueryAsync<(string Id, string PropsJson)>(selectWsSql);
                foreach (var ws in wsRows)
                {
                    _bulkNodes[ws.Id] = (NodeKind.Workspace, ws.PropsJson);
                }
                ResetTaxonomyInMemory();
                DropGraphSecondaryIndices();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CommitBulkLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!_isBulkLoading) return;
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isBulkLoading)
            {
                await CommitBulkLoadUnlockedAsync(cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private void ResetBulkLoadStateUnlocked()
    {
        var wasFresh = _isBulkLoading && _bulkIsFreshDb;
        _isBulkLoading = false;
        _bulkIsFreshDb = false;
        _bulkDropIdentifierIndex = false;
        _bulkNodes.Clear();
        _bulkRelationships.Clear();
        _bulkProjectExternalApis.Clear();
        if (wasFresh)
        {
            CreateGraphSecondaryIndices();
        }
    }

    private async Task CommitBulkLoadUnlockedAsync(CancellationToken cancellationToken = default)
    {
        if (!_isBulkLoading) return;
        _isBulkLoading = false;
        var isFresh = _bulkIsFreshDb;
        _bulkIsFreshDb = false;
        var dropIdIdx = _bulkDropIdentifierIndex;
        _bulkDropIdentifierIndex = false;

        var sw = Stopwatch.StartNew();
        var nodesCount = _bulkNodes.Count;
        var relsCount = _bulkRelationships.Count;

        try
        {
            if (nodesCount > 0 || relsCount > 0 || _bulkProjectExternalApis.Count > 0)
            {
                await using (var pragmaCmd = _conn.CreateCommand())
                {
                    pragmaCmd.CommandText = """
                                            PRAGMA synchronous = OFF;
                                            PRAGMA wal_autocheckpoint = 0;
                                            PRAGMA cache_size = -256000;
                                            """;
                    await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync(cancellationToken);

                if (isFresh)
                {
                    await using var delWsCmd = _conn.CreateCommand();
                    delWsCmd.CommandTimeout = 0;
                    delWsCmd.Transaction = tx;
                    delWsCmd.CommandText = "DELETE FROM nodes;";
                    await delWsCmd.ExecuteNonQueryAsync(cancellationToken);
                }
                else if (dropIdIdx)
                {
                    await using var delCmd = _conn.CreateCommand();
                    delCmd.CommandTimeout = 0;
                    delCmd.Transaction = tx;
                    delCmd.CommandText = $"DELETE FROM edges WHERE kind IN ({GraphKindCodec.RelKindsInSql(RelationshipKind.TransitivelyCalls, RelationshipKind.AttributedTo)});";
                    await delCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                const string paramRowId = "@r";
                const string paramI = "@i";
                const string paramK = "@k";
                const string paramP = "@p";
                const string paramDomains = "@domains";
                const string paramId = "@id";
                const string paramF = "@f";
                const string paramT = "@t";

                var nodeRowIds = relsCount > 0
                    ? new Dictionary<string, long>(Math.Max(nodesCount, 1024), StringComparer.Ordinal)
                    : null;

                if (nodesCount > 0)
                {
                    await using var nodeCmd = _conn.CreateCommand();
                    nodeCmd.CommandTimeout = 0;
                    nodeCmd.Transaction = tx;
                    nodeCmd.CommandText = isFresh
                        ? "INSERT INTO nodes (rowid, id, kind, properties) VALUES (@r, @i, @k, jsonb(@p));"
                        : "INSERT INTO nodes (id, kind, properties) VALUES (@i, @k, jsonb(@p)) ON CONFLICT(id) DO UPDATE SET kind = excluded.kind, properties = excluded.properties;";
                    var pRowId = isFresh ? nodeCmd.Parameters.Add(paramRowId, SqliteType.Integer) : null;
                    var pId = nodeCmd.Parameters.Add(paramI, SqliteType.Text);
                    var pKind = nodeCmd.Parameters.Add(paramK, SqliteType.Integer);
                    var pProps = nodeCmd.Parameters.Add(paramP, SqliteType.Text);
                    nodeCmd.Prepare();

                    long nextRowId = 1;
                    foreach (var (id, (kind, propsJson)) in _bulkNodes)
                    {
                        if (pRowId != null)
                        {
                            var rowId = nextRowId++;
                            pRowId.Value = rowId;
                            nodeRowIds?[id] = rowId;
                        }
                        pId.Value = id;
                        pKind.Value = (int)kind;
                        pProps.Value = propsJson;
                        nodeCmd.ExecuteNonQuery();
                    }
                }

                if (_bulkProjectExternalApis.Count > 0)
                {
                    await using var updCmd = _conn.CreateCommand();
                    updCmd.CommandTimeout = 0;
                    updCmd.Transaction = tx;
                    updCmd.CommandText =
                        "UPDATE nodes SET properties = jsonb_set(properties, '$.external_apis', jsonb(@domains)) WHERE id = @id;";
                    var pDomains = updCmd.Parameters.Add(paramDomains, SqliteType.Text);
                    var pId = updCmd.Parameters.Add(paramId, SqliteType.Text);
                    updCmd.Prepare();

                    foreach (var (projId, domains) in _bulkProjectExternalApis)
                    {
                        pId.Value = projId;
                        pDomains.Value = JsonSerializer.Serialize(domains);
                        updCmd.ExecuteNonQuery();
                    }
                }

                if (relsCount > 0 && nodeRowIds != null)
                {
                    if (!isFresh)
                    {
                        await using var mapCmd = _conn.CreateCommand();
                        mapCmd.CommandTimeout = 0;
                        mapCmd.Transaction = tx;
                        mapCmd.CommandText = "SELECT id, rowid FROM nodes;";
                        await using var mapReader = await mapCmd.ExecuteReaderAsync(cancellationToken);
                        while (await mapReader.ReadAsync(cancellationToken))
                        {
                            nodeRowIds[mapReader.GetString(0)] = mapReader.GetInt64(1);
                        }
                    }

                    await using var relCmd = _conn.CreateCommand();
                    relCmd.CommandTimeout = 0;
                    relCmd.Transaction = tx;
                    relCmd.CommandText = isFresh
                        ? "INSERT INTO edges (from_id, to_id, kind, properties) VALUES (@f, @t, @k, jsonb(@p));"
                        : "INSERT INTO edges (from_id, to_id, kind, properties) VALUES (@f, @t, @k, jsonb(@p)) ON CONFLICT(from_id, to_id, kind) DO UPDATE SET properties = excluded.properties;";
                    var pFrom = relCmd.Parameters.Add(paramF, SqliteType.Integer);
                    var pTo = relCmd.Parameters.Add(paramT, SqliteType.Integer);
                    var pKind = relCmd.Parameters.Add(paramK, SqliteType.Integer);
                    var pProps = relCmd.Parameters.Add(paramP, SqliteType.Text);
                    relCmd.Prepare();

                    var bufferWriter = new ArrayBufferWriter<byte>(512);
                    using var jsonWriter = new Utf8JsonWriter(bufferWriter);

                    foreach (var ((fromId, toId, kind), rel) in _bulkRelationships)
                    {
                        if (!nodeRowIds.TryGetValue(fromId, out var fromRowId) ||
                            !nodeRowIds.TryGetValue(toId, out var toRowId))
                        {
                            continue;
                        }

                        pFrom.Value = fromRowId;
                        pTo.Value = toRowId;
                        pKind.Value = (int)kind;
                        pProps.Value = rel.SerializeJsonProperties(bufferWriter, jsonWriter);
                        relCmd.ExecuteNonQuery();
                    }
                }

                await tx.CommitAsync(cancellationToken);
            }

            if (dropIdIdx)
            {
                try
                {
                    await using var dropIdxCmd = _conn.CreateCommand();
                    dropIdxCmd.CommandTimeout = CommandTimeoutSeconds;
                    dropIdxCmd.CommandText = "DROP TABLE IF EXISTS identifier_index; DROP TABLE IF EXISTS identifier_fts;";
                    await dropIdxCmd.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Database   ] Failed to invalidate identifier vector index: {Message}", ex.Message);
                }
            }
        }
        finally
        {
            _bulkNodes.Clear();
            _bulkRelationships.Clear();
            _bulkProjectExternalApis.Clear();

            if (isFresh)
            {
                CreateGraphSecondaryIndices();
            }

            try
            {
                await using var restoreCmd = _conn.CreateCommand();
                restoreCmd.CommandText = """
                                         PRAGMA synchronous = NORMAL;
                                         PRAGMA wal_autocheckpoint = 1000;
                                         PRAGMA cache_size = -64000;
                                         """;
                await restoreCmd.ExecuteNonQueryAsync(CancellationToken.None);
            }
            catch
            {
                // Best effort restore
            }
        }

        sw.Stop();
        if (nodesCount > 0 || relsCount > 0)
        {
            _logger.LogInformation("[Database   ] Bulk-flushed {Nodes:N0} nodes and {Rels:N0} relationships (with index build) in {ElapsedMs:N0}ms.",
                nodesCount, relsCount, sw.Elapsed.TotalMilliseconds);
            WorkspaceIndexer.OnIndexCompletedHook?.Invoke();
        }
    }

    public async Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath)
    {
        await _lock.WaitAsync();
        try
        {
            if (_isBulkLoading)
            {
                foreach (var (id, (kind, _)) in _bulkNodes)
                {
                    if (kind == NodeKind.Workspace) return id;
                }
            }
            var existing = await _conn.ExecuteScalarAsync<string>($"SELECT id FROM nodes WHERE kind = {GraphKindCodec.EncodeNodeKind(NodeKind.Workspace)} LIMIT 1;");
            return existing ?? "ws";
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveEmptyWorkspaceNodeAsync(string id, string path)
    {
        var normalized = path.Replace('\\', '/');
        var propsJson = JsonSerializer.Serialize(new { id, path = normalized });

        await _lock.WaitAsync();
        try
        {
            if (_isBulkLoading)
            {
                if (_bulkNodes.TryGetValue(id, out var existingWs))
                {
                    using var doc = JsonDocument.Parse(existingWs.PropsJson);
                    var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var p in doc.RootElement.EnumerateObject())
                    {
                        dict[p.Name] = p.Value.Clone();
                    }
                    dict["path"] = normalized;
                    _bulkNodes[id] = (existingWs.Kind, JsonSerializer.Serialize(dict));
                }
                else
                {
                    string? existingName = null;
                    if (!_bulkIsFreshDb)
                    {
                        existingName = await _conn.ExecuteScalarAsync<string?>(
                            "SELECT json_extract(properties, '$.name') FROM nodes WHERE id = @id LIMIT 1;",
                            new { id });
                    }

                    _bulkNodes[id] = (NodeKind.Workspace, !string.IsNullOrWhiteSpace(existingName)
                        ? JsonSerializer.Serialize(new { id, name = existingName, path = normalized })
                        : propsJson);
                }
                RecordWorkspaceNodeInTaxonomy(id);
                return;
            }

            const string sql = """
                INSERT INTO nodes (id, kind, properties) VALUES (@id, @kind, jsonb(@props))
                ON CONFLICT(id) DO UPDATE SET properties = jsonb_set(properties, '$.path', @path);
                """;
            await _conn.ExecuteAsync(sql, new { id, kind = GraphKindCodec.EncodeNodeKind(NodeKind.Workspace), props = propsJson, path = normalized });
            RecordWorkspaceNodeInTaxonomy(id);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UploadNodesAsync(List<Node> nodes)
    {
        if (nodes.Count == 0) return;

        var sw = Stopwatch.StartNew();
        await _lock.WaitAsync();
        try
        {
            var distinctNodes = nodes.Count > 1 ? [.. nodes.DistinctBy(n => n.Id)] : nodes;

            var bufferWriter = new ArrayBufferWriter<byte>(1024);
            using var jsonWriter = new Utf8JsonWriter(bufferWriter);

            if (_isBulkLoading)
            {
                for (var i = 0; i < distinctNodes.Count; i++)
                {
                    var node = distinctNodes[i];
                    bufferWriter.Clear();
                    jsonWriter.Reset(bufferWriter);
                    NodeFactory.WriteJsonProperties(node, jsonWriter);
                    jsonWriter.Flush();
                    _bulkNodes[node.Id] = (node.Kind, Encoding.UTF8.GetString(bufferWriter.WrittenSpan));
                }

                RecordNodesInTaxonomy(distinctNodes);
                return;
            }

            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();

            await using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO nodes (id, kind, properties) VALUES (@i, @k, jsonb(@p)) ON CONFLICT(id) DO UPDATE SET kind = excluded.kind, properties = excluded.properties;";
            var pId = cmd.Parameters.Add("@i", SqliteType.Text);
            var pKind = cmd.Parameters.Add("@k", SqliteType.Integer);
            var pProps = cmd.Parameters.Add("@p", SqliteType.Text);
            cmd.Prepare();

            var lastReport = Stopwatch.GetTimestamp();
            for (var i = 0; i < distinctNodes.Count; i++)
            {
                var node = distinctNodes[i];
                bufferWriter.Clear();
                jsonWriter.Reset(bufferWriter);
                NodeFactory.WriteJsonProperties(node, jsonWriter);
                jsonWriter.Flush();

                pId.Value = node.Id;
                pKind.Value = (int)node.Kind;
                pProps.Value = Encoding.UTF8.GetString(bufferWriter.WrittenSpan);
                cmd.ExecuteNonQuery();

                if (distinctNodes.Count >= 10000 && ((i + 1) % 2000 == 0 || i + 1 == distinctNodes.Count) &&
                    (i + 1 == distinctNodes.Count || Stopwatch.GetElapsedTime(lastReport).TotalSeconds >= 3))
                {
                    lastReport = Stopwatch.GetTimestamp();
                    var pct = (double)(i + 1) / distinctNodes.Count * 100.0;
                    _logger.LogDebug("[Database   ] Saved {Processed}/{Total} nodes ({Pct:F0}%)...", i + 1, distinctNodes.Count, pct);
                }
            }

            await tx.CommitAsync();
            RecordNodesInTaxonomy(distinctNodes);
            sw.Stop();

            _logger.LogDebug("[Database   ] Uploaded {Count} nodes in {ElapsedMs:F0}ms", nodes.Count, sw.Elapsed.TotalMilliseconds);
            WorkspaceIndexer.OnIndexCompletedHook?.Invoke();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UploadRelationshipsAsync(List<Relationship> rels)
    {
        if (rels.Count == 0) return;

        var sw = Stopwatch.StartNew();
        await _lock.WaitAsync();
        try
        {
            var distinctRels = DeduplicateAndMergeRelationships(rels);

            var bufferWriter = new ArrayBufferWriter<byte>(512);
            using var jsonWriter = new Utf8JsonWriter(bufferWriter);

            if (_isBulkLoading)
            {
                for (var i = 0; i < distinctRels.Count; i++)
                {
                    var rel = distinctRels[i];
                    var key = (rel.From, rel.To, rel.Kind);
                    if (!_bulkRelationships.TryGetValue(key, out var existing))
                    {
                        _bulkRelationships[key] = rel;
                    }
                    else if (rel.HasTelemetry)
                    {
                        EvidenceProvenanceHelper.MergeRelationships(existing, rel);
                    }
                }

                RecordRelationshipsInTaxonomy(distinctRels);
                return;
            }

            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();

            await using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO edges (from_id, to_id, kind, properties) SELECT fn.rowid, tn.rowid, @k, jsonb(@p) FROM nodes fn, nodes tn WHERE fn.id = @f AND tn.id = @t ON CONFLICT(from_id, to_id, kind) DO UPDATE SET properties = excluded.properties;";
            var pFrom = cmd.Parameters.Add("@f", SqliteType.Text);
            var pTo = cmd.Parameters.Add("@t", SqliteType.Text);
            var pKind = cmd.Parameters.Add("@k", SqliteType.Integer);
            var pProps = cmd.Parameters.Add("@p", SqliteType.Text);
            cmd.Prepare();

            var lastReport = Stopwatch.GetTimestamp();

            for (var i = 0; i < distinctRels.Count; i++)
            {
                var rel = distinctRels[i];
                pFrom.Value = rel.From;
                pTo.Value = rel.To;
                pKind.Value = (int)rel.Kind;
                pProps.Value = rel.SerializeJsonProperties(bufferWriter, jsonWriter);
                cmd.ExecuteNonQuery();

                if (distinctRels.Count >= 10000 && ((i + 1) % 2000 == 0 || i + 1 == distinctRels.Count) &&
                    (i + 1 == distinctRels.Count || Stopwatch.GetElapsedTime(lastReport).TotalSeconds >= 3))
                {
                    lastReport = Stopwatch.GetTimestamp();
                    var pct = (double)(i + 1) / distinctRels.Count * 100.0;
                    _logger.LogDebug("[Database   ] Saved {Processed}/{Total} relationships ({Pct:F0}%)...", i + 1, distinctRels.Count, pct);
                }
            }

            await tx.CommitAsync();
            RecordRelationshipsInTaxonomy(distinctRels);
            sw.Stop();

            _logger.LogDebug("[Database   ] Uploaded {Count} relationships in {ElapsedMs:F0}ms", rels.Count, sw.Elapsed.TotalMilliseconds);
            WorkspaceIndexer.OnIndexCompletedHook?.Invoke();
        }
        finally
        {
            _lock.Release();
        }
    }

    private static List<Relationship> DeduplicateAndMergeRelationships(List<Relationship> rels)
    {
        if (rels.Count <= 1) return rels;

        var unique = new List<Relationship>(rels.Count);
        var byKey = new Dictionary<(string From, string To, RelationshipKind Kind), Relationship>(rels.Count);

        foreach (var rel in rels)
        {
            var key = (rel.From, rel.To, rel.Kind);
            if (!byKey.TryGetValue(key, out var existing))
            {
                byKey[key] = rel;
                unique.Add(rel);
            }
            else if (rel.HasTelemetry)
            {
                EvidenceProvenanceHelper.MergeRelationships(existing, rel);
            }
        }

        return unique;
    }

    public async Task<CypherQueryResult> ExecuteCypherAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteCompiledReaderAsync(
            query,
            parameters,
            static (reader, ct) => DbRowMaterializer.ReadDynamicResultAsync(reader, ct),
            cancellationToken);
    }

    public async Task<List<T>> QueryAsync<T>(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteCompiledReaderAsync(
            query,
            parameters,
            static (reader, ct) => DbRowMaterializer.ReadListAsync<T>(reader, ct),
            cancellationToken);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteCompiledReaderAsync(
            query,
            parameters,
            static (reader, ct) => DbRowMaterializer.ReadFirstOrDefaultAsync<T>(reader, ct),
            cancellationToken);
    }

    public async Task<T?> QueryScalarAsync<T>(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        return await ExecuteCompiledReaderAsync(
            query,
            parameters,
            static (reader, ct) => DbRowMaterializer.ReadScalarAsync<T>(reader, ct),
            cancellationToken);
    }

    private async Task<TResult> ExecuteCompiledReaderAsync<TResult>(
        string query,
        object? parameters,
        Func<SqliteDataReader, CancellationToken, Task<TResult>> readerFunc,
        CancellationToken cancellationToken)
    {
        ThrowIfSchemaOutdated();
        var sw = Stopwatch.StartNew();
        ValidateQuerySecurity(query);
        var paramDict = ExtractParameters(parameters);

        string sql;
        IReadOnlyDictionary<string, object?> sqlParams;

        const string selectKeyword = "SELECT";
        if (query.TrimStart().StartWithIC(selectKeyword))
        {
            sql = query;
            sqlParams = paramDict;
        }
        else
        {
            var ast = CypherQueryParser.Parse(query);
            var compiled = SqliteCompiler.Compile(ast, paramDict, maxPathTraversals: MaxPathTraversals);
            sql = compiled.Sql;
            sqlParams = compiled.Parameters;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (HasPendingBulkMutations)
            {
                await CommitBulkLoadUnlockedAsync(cancellationToken);
            }

            await using var cmd = _conn.CreateCommand();
            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.CommandText = sql;

            foreach (var (k, v) in sqlParams)
            {
                cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            }

            using var timeoutCts = CommandTimeoutSeconds > 0
                ? new CancellationTokenSource(TimeSpan.FromSeconds(CommandTimeoutSeconds))
                : null;
            using var linkedCts = timeoutCts != null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
                : null;
            var effectiveCt = linkedCts?.Token ?? cancellationToken;

            await using var reader = await cmd.ExecuteReaderAsync(effectiveCt);
            var result = await readerFunc(reader, effectiveCt);
            sw.Stop();

            _logger.LogDebug("[Database   ] Completed in {ElapsedMs:F1}ms: {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, GetQueryPreview(query));

            return result;
        }
        catch (Exception ex) when (ex is OperationCanceledException or SqliteException { SqliteErrorCode: 5 or 9 } || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            sw.Stop();
            _logger.LogError(ex, "[Database   ] Query timed out after {ElapsedMs:F1}ms (limit: {Timeout}s): {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, CommandTimeoutSeconds, GetQueryPreview(query));
            throw new TimeoutException($"Query execution timed out after {CommandTimeoutSeconds} seconds. Consider narrowing filters or bounding traversal depth.", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool HasPendingBulkMutations =>
        _isBulkLoading && (_bulkNodes.Count > 1 || _bulkRelationships.Count > 0 || _bulkProjectExternalApis.Count > 0);

    public async Task<int> ExecuteCountAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        ThrowIfSchemaOutdated();
        await _lock.WaitAsync(ct);
        try
        {
            if (HasPendingBulkMutations) await CommitBulkLoadUnlockedAsync(ct);
            return await DbEntityMaterializer.ExecuteScalarCountAsync(_conn, query, parameters, CommandTimeoutSeconds, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ExecuteAnyAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        ThrowIfSchemaOutdated();
        await _lock.WaitAsync(ct);
        try
        {
            if (HasPendingBulkMutations) await CommitBulkLoadUnlockedAsync(ct);
            return await DbEntityMaterializer.ExecuteScalarAnyAsync(_conn, query, parameters, CommandTimeoutSeconds, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<TElement>> ExecuteListAsync<TElement>(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        ThrowIfSchemaOutdated();
        await _lock.WaitAsync(ct);
        try
        {
            if (HasPendingBulkMutations) await CommitBulkLoadUnlockedAsync(ct);
            return await DbEntityMaterializer.ExecuteReaderListAsync<TElement>(_conn, query, parameters, CommandTimeoutSeconds, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TElement?> ExecuteFirstOrDefaultAsync<TElement>(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        ThrowIfSchemaOutdated();
        await _lock.WaitAsync(ct);
        try
        {
            if (HasPendingBulkMutations) await CommitBulkLoadUnlockedAsync(ct);
            return await DbEntityMaterializer.ExecuteReaderFirstOrDefaultAsync<TElement>(_conn, query, parameters, CommandTimeoutSeconds, ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string GetQueryPreview(string query)
    {
        var singleLine = Regex.Replace(query.Trim(), @"\s+", " ");
        return singleLine.Length > 80 ? singleLine[..77] + "..." : singleLine;
    }

    /// <summary>
    /// Executes a custom operation directly against the underlying SQLite connection under the client lock.
    /// </summary>
    public async Task<T> ExecuteRawAsync<T>(
        Func<SqliteConnection, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isBulkLoading) await CommitBulkLoadUnlockedAsync(cancellationToken);
            return await action(_conn);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ExecuteWriteAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var paramDict = ExtractParameters(parameters);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isBulkLoading)
            {
                await CommitBulkLoadUnlockedAsync(cancellationToken);
            }

            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.CommandText = query;

            foreach (var (k, v) in paramDict)
            {
                cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            sw.Stop();

            _logger.LogDebug("[Database   ] Write completed in {ElapsedMs:F1}ms: {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, GetQueryPreview(query));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            sw.Stop();
            _logger.LogError(ex, "[Database   ] Write timed out after {ElapsedMs:F1}ms: {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, GetQueryPreview(query));
            throw new TimeoutException($"Database write timed out after {CommandTimeoutSeconds} seconds.", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static Dictionary<string, object?> ExtractParameters(object? parameters)
    {
        if (parameters == null) return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (parameters is Dictionary<string, object?> dict) return dict;
        if (parameters is IReadOnlyDictionary<string, object?> roDict)
            return roDict.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        if (parameters is IDictionary<string, object> idict)
            return idict.ToDictionary(k => k.Key, v => (object?)v.Value, StringComparer.OrdinalIgnoreCase);
        if (parameters is IReadOnlyDictionary<string, string> strDict)
            return strDict.ToDictionary(k => k.Key, v => (object?)v.Value, StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            result[prop.Name] = prop.GetValue(parameters);
        }

        return result;
    }



    public async Task<Dictionary<string, int>> GetNodesBreakdownAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isBulkLoading) await CommitBulkLoadUnlockedAsync(cancellationToken);
            const string sql = "SELECT ce_node_kind(kind) AS kind, count(*) AS count FROM nodes GROUP BY kind ORDER BY count(*) DESC;";
            var rows = await _conn.QueryAsync<(string kind, int count)>(new CommandDefinition(sql, cancellationToken: cancellationToken));
            return rows.ToDictionary(r => r.kind, r => r.count, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(int NodesCount, int RelationshipsCount)> GetGraphCountsAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isBulkLoading) await CommitBulkLoadUnlockedAsync(cancellationToken);
            const string sql = "SELECT (SELECT count(*) FROM nodes) AS NodesCount, (SELECT count(*) FROM edges) AS RelationshipsCount;";
            return await _conn.QuerySingleOrDefaultAsync<(int, int)>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        }
        finally
        {
            _lock.Release();
        }
    }

    private static void ValidateQuerySecurity(string query)
    {
        CypherSecurityValidator.ValidateReadOnly(query);
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        if (_isBulkLoading)
        {
            try
            {
                await CommitBulkLoadUnlockedAsync();
            }
            catch
            {
                // Best effort flush on dispose
            }
        }
        await _conn.CloseAsync();
        await _conn.DisposeAsync();
        SqliteConnection.ClearPool(_conn);
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        if (_isBulkLoading)
        {
            try
            {
                CommitBulkLoadUnlockedAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // Best effort flush on dispose
            }
        }
        _conn.Close();
        _conn.Dispose();
        SqliteConnection.ClearPool(_conn);
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
