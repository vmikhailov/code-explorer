using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Parser;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Database;

public class SqliteGraphClient : IGraphClient, IDisposable
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

    public SqliteGraphClient(string connectionStringOrPath, ILogger<SqliteGraphClient>? logger = null)
    {
        _logger = (ILogger?)logger ?? NullLogger.Instance;
        var cs = ResolveConnectionString(connectionStringOrPath);
        _conn = new SqliteConnection(cs);
        _conn.Open();

        InitializePragmas();
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

    private void InitializePragmas()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA busy_timeout = 5000;
            """;
        cmd.ExecuteNonQuery();
    }

    private void CreateIndices()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS nodes (
                id TEXT PRIMARY KEY,
                kind TEXT NOT NULL,
                properties JSON NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_nodes_kind ON nodes(kind);
            CREATE INDEX IF NOT EXISTS idx_nodes_kind_path ON nodes(kind, json_extract(properties, '$.path'));
            CREATE INDEX IF NOT EXISTS idx_nodes_kind_file_path ON nodes(kind, json_extract(properties, '$.file_path'));
            CREATE INDEX IF NOT EXISTS idx_nodes_kind_name ON nodes(kind, json_extract(properties, '$.name'));
            CREATE INDEX IF NOT EXISTS idx_nodes_kind_lower_path ON nodes(kind, lower(json_extract(properties, '$.path')));

            CREATE TABLE IF NOT EXISTS edges (
                from_id TEXT NOT NULL,
                to_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                properties JSON NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_edges_from ON edges(from_id);
            CREATE INDEX IF NOT EXISTS idx_edges_to ON edges(to_id);
            CREATE INDEX IF NOT EXISTS idx_edges_kind ON edges(kind);
            CREATE INDEX IF NOT EXISTS idx_edges_from_kind ON edges(from_id, kind);
            CREATE INDEX IF NOT EXISTS idx_edges_to_kind ON edges(to_id, kind);
            CREATE INDEX IF NOT EXISTS idx_edges_from_kind_to ON edges(from_id, kind, to_id);
            CREATE INDEX IF NOT EXISTS idx_edges_to_kind_from ON edges(to_id, kind, from_id);
            CREATE INDEX IF NOT EXISTS idx_edges_kind_from_to ON edges(kind, from_id, to_id);

            CREATE TABLE IF NOT EXISTS metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public Task CreateIndicesAsync()
    {
        CreateIndices();
        return Task.CompletedTask;
    }

    public async Task ClearDatabaseAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM edges; DELETE FROM nodes; DELETE FROM metadata;";
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearWorkspaceAsync(string workspacePath)
    {
        var normalized = workspacePath.Replace('\\', '/');
        await _lock.WaitAsync();
        try
        {
            var wsId = await FindWorkspaceIdByPathAsync(normalized);
            if (wsId != null)
            {
                await DeleteWorkspaceHierarchyAsync(wsId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<string?> FindWorkspaceIdByPathAsync(string normalizedPath)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT id FROM nodes
            WHERE kind = 'Workspace'
              AND replace(lower(json_extract(properties, '$.path')), '\', '/') = replace(lower(@path), '\', '/')
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@path", normalizedPath);
        return (string?)await cmd.ExecuteScalarAsync();
    }

    private async Task DeleteWorkspaceHierarchyAsync(string wsId)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            WITH RECURSIVE ws_nodes(id) AS (
                SELECT @wsId
                UNION
                SELECT e.to_id FROM edges e JOIN ws_nodes w ON e.from_id = w.id WHERE e.kind = 'CONTAINS'
            )
            DELETE FROM edges WHERE from_id IN (SELECT id FROM ws_nodes) OR to_id IN (SELECT id FROM ws_nodes);

            WITH RECURSIVE ws_nodes(id) AS (
                SELECT @wsId
                UNION
                SELECT e.to_id FROM edges e JOIN ws_nodes w ON e.from_id = w.id WHERE e.kind = 'CONTAINS'
            )
            DELETE FROM nodes WHERE id IN (SELECT id FROM ws_nodes);
            """;
        cmd.Parameters.AddWithValue("@wsId", wsId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath)
    {
        var normalized = workspacePath.Replace('\\', '/');
        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                SELECT id FROM nodes
                WHERE kind = 'Workspace'
                  AND replace(lower(json_extract(properties, '$.path')), '\', '/') = replace(lower(@path), '\', '/')
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("@path", normalized);
            var existing = (string?)await cmd.ExecuteScalarAsync();
            if (existing != null) return existing;

            cmd.Parameters.Clear();
            cmd.CommandText = """
                INSERT INTO metadata (key, value) VALUES ('workspace_id', '1')
                ON CONFLICT(key) DO UPDATE SET value = CAST(CAST(value AS INTEGER) + 1 AS TEXT);
                SELECT value FROM metadata WHERE key = 'workspace_id';
                """;
            var nextId = (string?)await cmd.ExecuteScalarAsync();
            return nextId ?? "1";
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
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO nodes (id, kind, properties) VALUES (@id, 'Workspace', @props)
                ON CONFLICT(id) DO UPDATE SET properties = json_set(properties, '$.path', @path);
                """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@props", propsJson);
            cmd.Parameters.AddWithValue("@path", normalized);
            await cmd.ExecuteNonQueryAsync();
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
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();
            await using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO nodes (id, kind, properties) VALUES (@id, @kind, @props)
                ON CONFLICT(id) DO UPDATE SET kind = excluded.kind, properties = excluded.properties;
                """;

            var pId = cmd.Parameters.Add("@id", SqliteType.Text);
            var pKind = cmd.Parameters.Add("@kind", SqliteType.Text);
            var pProps = cmd.Parameters.Add("@props", SqliteType.Text);

            foreach (var node in nodes)
            {
                var dict = new Dictionary<string, object>(node.Properties) { ["id"] = node.Id };
                pId.Value = node.Id;
                pKind.Value = node.Kind;
                pProps.Value = JsonSerializer.Serialize(dict);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            sw.Stop();
            _logger.LogDebug("[DB:Nodes] Uploaded {Count} nodes in {ElapsedMs:F1}ms", nodes.Count, sw.Elapsed.TotalMilliseconds);
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
            await using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO edges (from_id, to_id, kind, properties) VALUES (@from, @to, @kind, @props);";

            var pFrom = cmd.Parameters.Add("@from", SqliteType.Text);
            var pTo = cmd.Parameters.Add("@to", SqliteType.Text);
            var pKind = cmd.Parameters.Add("@kind", SqliteType.Text);
            var pProps = cmd.Parameters.Add("@props", SqliteType.Text);

            foreach (var rel in rels)
            {
                pFrom.Value = rel.From;
                pTo.Value = rel.To;
                pKind.Value = rel.Kind;
                pProps.Value = JsonSerializer.Serialize(rel.Properties);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            sw.Stop();
            _logger.LogDebug("[DB:Edges] Uploaded {Count} relationships in {ElapsedMs:F1}ms", rels.Count, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> ExecuteQueryAsync(string query, object? parameters = null)
    {
        var sw = Stopwatch.StartNew();
        ValidateQuerySecurity(query);
        var paramDict = ExtractParameters(parameters);
        var ast = CypherQueryParser.Parse(query);
        var compiled = SqliteCompiler.Compile(ast, paramDict);

        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = compiled.Sql;
            foreach (var (k, v) in compiled.Parameters)
            {
                cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            }

            await using var reader = await cmd.ExecuteReaderAsync();
            var rows = await ReadRowsAsync(reader);
            sw.Stop();

            _logger.LogInformation(
                "[DB:Query] Completed in {ElapsedMs:F1}ms (rows: {RowCount}): {QueryPreview}",
                sw.Elapsed.TotalMilliseconds,
                rows.Count,
                GetQueryPreview(query));

            return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
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

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(SqliteDataReader reader)
    {
        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
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

    public async Task ExecuteWriteAsync(string query, object? parameters = null)
    {
        var sw = Stopwatch.StartNew();
        var paramDict = ExtractParameters(parameters);
        if (await TryExecutePostIndexWriteAsync(query, paramDict))
        {
            sw.Stop();
            _logger.LogInformation("[DB:Write] PostIndex write completed in {ElapsedMs:F1}ms: {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, GetQueryPreview(query));
            return;
        }

        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = query;
            foreach (var (k, v) in paramDict)
            {
                cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            }
            await cmd.ExecuteNonQueryAsync();
            sw.Stop();

            _logger.LogInformation("[DB:Write] Write completed in {ElapsedMs:F1}ms: {QueryPreview}",
                sw.Elapsed.TotalMilliseconds, GetQueryPreview(query));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<bool> TryExecutePostIndexWriteAsync(string query, Dictionary<string, object?> paramDict)
    {
        if (query.Contains("TRANSITIVELY_CALLS"))
        {
            return await ExecuteTransitivelyCallsWriteAsync(paramDict);
        }

        if (query.Contains("ATTRIBUTED_TO"))
        {
            return await ExecuteAttributedToWriteAsync(paramDict);
        }

        if (query.Contains("SET p.external_apis"))
        {
            return await ExecuteProjectApiAnnotationsWriteAsync(paramDict);
        }

        return false;
    }

    private async Task<bool> ExecuteTransitivelyCallsWriteAsync(Dictionary<string, object?> paramDict)
    {
        var readCypher = """
            MATCH path = (caller:Function)-[:CALLS*1..15]->(sink)
            WHERE caller.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Query)
            RETURN caller.id AS from_id, sink.id AS to_id, min(length(path)) AS hops
            """;
        var jsonResult = await ExecuteQueryAsync(readCypher, paramDict);
        using var doc = JsonDocument.Parse(jsonResult);
        var rels = new List<Relationship>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var from = item.TryGetProperty("from_id", out var fp) && fp.ValueKind == JsonValueKind.String ? fp.GetString() : null;
            var to = item.TryGetProperty("to_id", out var tp) && tp.ValueKind == JsonValueKind.String ? tp.GetString() : null;
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;

            var hops = item.TryGetProperty("hops", out var hp) && hp.ValueKind == JsonValueKind.Number ? hp.GetInt32() : 1;
            rels.Add(new Relationship(from, to, "TRANSITIVELY_CALLS", new() { ["hops"] = hops }));
        }

        await UploadRelationshipsAsync(rels);
        return true;
    }

    private async Task<bool> ExecuteAttributedToWriteAsync(Dictionary<string, object?> paramDict)
    {
        var readCypher = """
            MATCH path = (ep:EntryPoint)<-[:IMPLEMENTS]-(fn:Function)-[:CALLS*0..15]->(sink)
            WHERE ep.id STARTS WITH $widPrefix AND (sink:ExternalService OR sink:DB OR sink:Query)
            RETURN ep.id AS from_id, sink.id AS to_id, min(length(path)) AS hops, labels(sink)[0] AS sinkKind
            """;
        var jsonResult = await ExecuteQueryAsync(readCypher, paramDict);
        using var doc = JsonDocument.Parse(jsonResult);
        var rels = new List<Relationship>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var from = item.TryGetProperty("from_id", out var fp) && fp.ValueKind == JsonValueKind.String ? fp.GetString() : null;
            var to = item.TryGetProperty("to_id", out var tp) && tp.ValueKind == JsonValueKind.String ? tp.GetString() : null;
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) continue;

            var hops = item.TryGetProperty("hops", out var hp) && hp.ValueKind == JsonValueKind.Number ? hp.GetInt32() : 1;
            var sinkKind = item.TryGetProperty("sinkKind", out var sk) && sk.ValueKind == JsonValueKind.String ? sk.GetString() ?? "" : "";
            rels.Add(new Relationship(from, to, "ATTRIBUTED_TO", new() { ["hops"] = hops, ["sink_kind"] = sinkKind }));
        }

        await UploadRelationshipsAsync(rels);
        return true;
    }

    private async Task<bool> ExecuteProjectApiAnnotationsWriteAsync(Dictionary<string, object?> paramDict)
    {
        var readCypher = """
            MATCH (p:Project)-[:CONTAINS|EXPOSES*1..2]->(ep:EntryPoint)-[:ATTRIBUTED_TO]->(es:ExternalService)
            WHERE p.id STARTS WITH $widPrefix
            RETURN p.id AS project_id, collect(DISTINCT es.domain_or_service) AS domains
            """;
        var jsonResult = await ExecuteQueryAsync(readCypher, paramDict);
        using var doc = JsonDocument.Parse(jsonResult);

        await _lock.WaitAsync();
        try
        {
            await UpdateProjectApiAnnotationsInTxAsync(doc.RootElement);
        }
        finally
        {
            _lock.Release();
        }

        return true;
    }

    private async Task UpdateProjectApiAnnotationsInTxAsync(JsonElement root)
    {
        await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();
        await using var cmd = _conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "UPDATE nodes SET properties = json_set(properties, '$.external_apis', json(@domains)) WHERE id = @id;";
        var pDomains = cmd.Parameters.Add("@domains", SqliteType.Text);
        var pId = cmd.Parameters.Add("@id", SqliteType.Text);

        foreach (var item in root.EnumerateArray())
        {
            var id = item.TryGetProperty("project_id", out var idProp) ? idProp.GetString() : null;
            if (string.IsNullOrEmpty(id)) continue;
            pId.Value = id;
            pDomains.Value = item.TryGetProperty("domains", out var dProp) ? dProp.GetRawText() : "[]";
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
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

    private static void ValidateQuerySecurity(string query)
    {
        var forbidden = new[] { "create ", "merge ", "delete ", "remove ", "drop ", "set ", "detach " };
        var lowerQuery = query.ToLowerInvariant();
        foreach (var word in forbidden)
        {
            if (lowerQuery.Contains(word))
            {
                throw new InvalidOperationException($"Security violation: modifying keyword '{word.Trim()}' is not allowed in sandbox mode.");
            }
        }
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
