using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Cypher.Ast;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Linq;
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

    public ILogger Logger
    {
        get => _logger;
        set => _logger = value ?? NullLogger.Instance;
    }

    public int CommandTimeoutSeconds { get; set; } = 15;
    public string DbPath => _conn.DataSource;
    public SqliteConnection Connection => _conn;
    public GraphContext Graph => new(this);

    public const int CurrentSchemaVersion = 3;
    public int SchemaVersion { get; private set; }
    public bool IsSchemaOutdated => SchemaVersion < CurrentSchemaVersion;

    public SqliteGraphClient(string connectionStringOrPath, ILogger<SqliteGraphClient>? logger = null)
    {
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        var cs = ResolveConnectionString(connectionStringOrPath);
        _conn = new SqliteConnection(cs);
        _conn.Open();

        InitializePragmas();
        CheckSchemaVersion();
        SqliteCypherFunctions.Register(_conn);
        CreateIndices();
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



    public async Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath)
    {
        await _lock.WaitAsync();
        try
        {
            var existing = await _conn.ExecuteScalarAsync<string>("SELECT id FROM nodes WHERE kind = 'Workspace' LIMIT 1;");
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
        var propsJson = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["id"] = id,
            ["path"] = normalized
        });

        await _lock.WaitAsync();
        try
        {
            const string sql = """
                INSERT INTO nodes (id, kind, properties) VALUES (@id, 'Workspace', @props)
                ON CONFLICT(id) DO UPDATE SET properties = json_set(properties, '$.path', @path);
                """;
            await _conn.ExecuteAsync(sql, new { id, props = propsJson, path = normalized });
        }
        finally
        {
            _lock.Release();
        }
    }

    private const int DefaultBatchSize = 200;

    private static readonly string[] NodeParamI = Enumerable.Range(0, DefaultBatchSize).Select(j => $"@i{j}").ToArray();
    private static readonly string[] NodeParamK = Enumerable.Range(0, DefaultBatchSize).Select(j => $"@k{j}").ToArray();
    private static readonly string[] NodeParamP = Enumerable.Range(0, DefaultBatchSize).Select(j => $"@p{j}").ToArray();

    private static readonly string[] EdgeParamF = Enumerable.Range(0, DefaultBatchSize).Select(j => $"@f{j}").ToArray();
    private static readonly string[] EdgeParamT = Enumerable.Range(0, DefaultBatchSize).Select(j => $"@t{j}").ToArray();
    private static readonly string[] EdgeParamK = Enumerable.Range(0, DefaultBatchSize).Select(j => $"@k{j}").ToArray();
    private static readonly string[] EdgeParamP = Enumerable.Range(0, DefaultBatchSize).Select(j => $"@p{j}").ToArray();

    private static readonly string FullNodeBatchSql = BuildNodeSql(DefaultBatchSize);
    private static readonly string FullEdgeBatchSql = BuildEdgeSql(DefaultBatchSize);

    private static string BuildNodeSql(int count)
    {
        var sb = new System.Text.StringBuilder(count * 20 + 120);
        sb.Append("INSERT INTO nodes (id, kind, properties) VALUES ");
        for (int j = 0; j < count; j++)
        {
            if (j > 0) sb.Append(',');
            sb.Append($"(@i{j}, @k{j}, @p{j})");
        }
        sb.Append(" ON CONFLICT(id) DO UPDATE SET kind = excluded.kind, properties = excluded.properties;");
        return sb.ToString();
    }

    private static string BuildEdgeSql(int count)
    {
        var sb = new System.Text.StringBuilder(count * 25 + 120);
        sb.Append("INSERT INTO edges (from_id, to_id, kind, properties) VALUES ");
        for (int j = 0; j < count; j++)
        {
            if (j > 0) sb.Append(',');
            sb.Append($"(@f{j}, @t{j}, @k{j}, @p{j})");
        }
        sb.Append(" ON CONFLICT(from_id, to_id, kind) DO UPDATE SET properties = excluded.properties;");
        return sb.ToString();
    }

    public async Task UploadNodesAsync(List<Node> nodes)
    {
        if (nodes.Count == 0) return;

        var sw = Stopwatch.StartNew();
        await _lock.WaitAsync();
        try
        {
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();

            const int batchSize = DefaultBatchSize;
            var distinctNodes = nodes.Count > 1 ? [.. nodes.DistinctBy(n => n.Id)] : nodes;

            var lastReport = Stopwatch.GetTimestamp();
            for (int i = 0; i < distinctNodes.Count; i += batchSize)
            {
                var count = Math.Min(batchSize, distinctNodes.Count - i);

                await using var cmd = _conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = count == DefaultBatchSize ? FullNodeBatchSql : BuildNodeSql(count);

                for (int j = 0; j < count; j++)
                {
                    var node = distinctNodes[i + j];
                    if (!node.Properties.ContainsKey("id"))
                    {
                        node.Properties["id"] = node.Id;
                    }

                    var pI = j < DefaultBatchSize ? NodeParamI[j] : $"@i{j}";
                    var pK = j < DefaultBatchSize ? NodeParamK[j] : $"@k{j}";
                    var pP = j < DefaultBatchSize ? NodeParamP[j] : $"@p{j}";

                    cmd.Parameters.AddWithValue(pI, node.Id);
                    cmd.Parameters.AddWithValue(pK, node.Kind);
                    cmd.Parameters.AddWithValue(pP, JsonSerializer.Serialize(node.Properties));
                }

                cmd.ExecuteNonQuery();

                if (distinctNodes.Count >= 5000 && (i + count == distinctNodes.Count || Stopwatch.GetElapsedTime(lastReport).TotalSeconds >= 3))
                {
                    lastReport = Stopwatch.GetTimestamp();
                    var pct = (double)(i + count) / distinctNodes.Count * 100.0;
                    _logger.LogInformation("[DB:Nodes] Saved: {Processed}/{Total} nodes ({Pct:F0}%)...", i + count, distinctNodes.Count, pct);
                }
            }

            await tx.CommitAsync();
            sw.Stop();

            if (distinctNodes.Count >= 5000)
            {
                _logger.LogInformation("[DB:Nodes] Successfully uploaded {Count} nodes in {ElapsedMs:F0}ms.", nodes.Count, sw.Elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogDebug("[DB:Nodes] Uploaded {Count} nodes in {ElapsedMs:F1}ms", nodes.Count,
                    sw.Elapsed.TotalMilliseconds);
            }
            Analysis.ArchitectureViewEngine.InvalidateCache();
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
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();

            const string emptyPropsJson = "{}";
            const int batchSize = DefaultBatchSize;
            var distinctRels = rels.Count > 1 ? [.. rels.DistinctBy(r => (r.From, r.To, r.Kind))] : rels;

            var lastReport = Stopwatch.GetTimestamp();
            for (int i = 0; i < distinctRels.Count; i += batchSize)
            {
                var count = Math.Min(batchSize, distinctRels.Count - i);

                await using var cmd = _conn.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = count == DefaultBatchSize ? FullEdgeBatchSql : BuildEdgeSql(count);

                for (int j = 0; j < count; j++)
                {
                    var rel = distinctRels[i + j];
                    var pF = j < DefaultBatchSize ? EdgeParamF[j] : $"@f{j}";
                    var pT = j < DefaultBatchSize ? EdgeParamT[j] : $"@t{j}";
                    var pK = j < DefaultBatchSize ? EdgeParamK[j] : $"@k{j}";
                    var pP = j < DefaultBatchSize ? EdgeParamP[j] : $"@p{j}";

                    cmd.Parameters.AddWithValue(pF, rel.From);
                    cmd.Parameters.AddWithValue(pT, rel.To);
                    cmd.Parameters.AddWithValue(pK, rel.Kind);
                    cmd.Parameters.AddWithValue(pP,
                        rel.Properties == null || rel.Properties.Count == 0
                            ? emptyPropsJson
                            : JsonSerializer.Serialize(rel.Properties));
                }

                cmd.ExecuteNonQuery();

                if (distinctRels.Count >= 5000 && (i + count == distinctRels.Count || Stopwatch.GetElapsedTime(lastReport).TotalSeconds >= 3))
                {
                    lastReport = Stopwatch.GetTimestamp();
                    var pct = (double)(i + count) / distinctRels.Count * 100.0;
                    _logger.LogInformation("[DB:Edges] Saved: {Processed}/{Total} relationships ({Pct:F0}%)...", i + count, distinctRels.Count, pct);
                }
            }

            await tx.CommitAsync();
            sw.Stop();

            if (distinctRels.Count >= 5000)
            {
                _logger.LogInformation("[DB:Edges] Successfully uploaded {Count} relationships in {ElapsedMs:F0}ms.", rels.Count, sw.Elapsed.TotalMilliseconds);
            }
            else
            {
                _logger.LogDebug("[DB:Edges] Uploaded {Count} relationships in {ElapsedMs:F1}ms", rels.Count,
                    sw.Elapsed.TotalMilliseconds);
            }
            Analysis.ArchitectureViewEngine.InvalidateCache();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        ValidateQuerySecurity(query);
        var paramDict = ExtractParameters(parameters);

        string sql;
        IReadOnlyDictionary<string, object?> sqlParams;

        if (query.TrimStart().StartWithIC("SELECT"))
        {
            sql = query;
            sqlParams = paramDict;
        }
        else
        {
            var ast = CypherQueryParser.Parse(query);
            var compiled = SqliteCompiler.Compile(ast, paramDict);
            sql = compiled.Sql;
            sqlParams = compiled.Parameters;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.CommandText = sql;

            foreach (var (k, v) in sqlParams)
            {
                cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

            var rows = await ReadRowsAsync(reader, cancellationToken);
            sw.Stop();

            _logger.LogDebug("[DB:Query] Completed in {ElapsedMs:F1}ms (rows: {RowCount}): {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, rows.Count, GetQueryPreview(query));

            return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            sw.Stop();
            _logger.LogError(ex, "[DB:Query] Query timed out after {ElapsedMs:F1}ms (limit: {Timeout}s): {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, CommandTimeoutSeconds, GetQueryPreview(query));
            throw new TimeoutException($"Query execution timed out after {CommandTimeoutSeconds} seconds. Consider narrowing filters or bounding traversal depth.", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> ExecuteQueryAsync(CypherQuery query, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var compiled = SqliteCompiler.Compile(query, parameters);

        await _lock.WaitAsync(ct);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.CommandText = compiled.Sql;

            foreach (var (k, v) in compiled.Parameters)
            {
                cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            }

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var rows = await ReadRowsAsync(reader, ct);
            sw.Stop();

            _logger.LogDebug("[DB:QueryAst] Completed in {ElapsedMs:F1}ms (rows: {RowCount})",
                sw.Elapsed.TotalMilliseconds, rows.Count);

            return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            sw.Stop();
            _logger.LogError(ex, "[DB:QueryAst] Query timed out after {ElapsedMs:F1}ms (limit: {Timeout}s)",
                sw.Elapsed.TotalMilliseconds, CommandTimeoutSeconds);
            throw new TimeoutException($"Query execution timed out after {CommandTimeoutSeconds} seconds.", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<string> ExecuteRawAsync(string cypher, IReadOnlyDictionary<string, object?>? parameters = null, CancellationToken ct = default) =>
        ExecuteQueryAsync(cypher, parameters, ct);

    private static string GetQueryPreview(string query)
    {
        var singleLine = Regex.Replace(query.Trim(), @"\s+", " ");
        return singleLine.Length > 80 ? singleLine[..77] + "..." : singleLine;
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(SqliteDataReader reader, CancellationToken cancellationToken = default)
    {
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = FormatSqliteValue(reader.GetValue(i));
            }
            rows.Add(row);
        }
        return rows;
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
            await using var cmd = _conn.CreateCommand();

            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.CommandText = query;

            foreach (var (k, v) in paramDict)
            {
                cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            sw.Stop();

            _logger.LogDebug("[DB:Write] Write completed in {ElapsedMs:F1}ms: {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, GetQueryPreview(query));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 5 || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            sw.Stop();
            _logger.LogError(ex, "[DB:Write] Write timed out after {ElapsedMs:F1}ms: {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, GetQueryPreview(query));
            throw new TimeoutException($"Database write timed out after {CommandTimeoutSeconds} seconds.", ex);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static object? FormatSqliteValue(object val)
    {
        if (val is DBNull) return null;
        if (val is string s && !string.IsNullOrWhiteSpace(s) && (s.StartsWith('{') || s.StartsWith('[')))
        {
            try
            {
                return JsonDocument.Parse(s).RootElement.Clone();
            }
            catch
            {
                // Not valid JSON, return as string
            }
        }

        return val;
    }

    private static Dictionary<string, object?> ExtractParameters(object? parameters)
    {
        if (parameters == null) return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (parameters is Dictionary<string, object?> dict) return dict;
        if (parameters is IDictionary<string, object> idict)
            return idict.ToDictionary(k => k.Key, v => (object?)v.Value, StringComparer.OrdinalIgnoreCase);

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
            const string sql = "SELECT kind, count(*) AS count FROM nodes GROUP BY kind ORDER BY count(*) DESC;";
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
        _conn.Close();
        _conn.Dispose();
        SqliteConnection.ClearPool(_conn);
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
