using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Cypher.Compiler;
using CodeExplorer.Cypher.Parser;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

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

    public int CommandTimeoutSeconds { get; set; } = 15;
    public string DbPath => _conn.DataSource;

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
            PRAGMA cache_size = -64000;
            PRAGMA temp_store = MEMORY;
            PRAGMA mmap_size = 268435456;
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
            DELETE FROM edges WHERE rowid NOT IN (
                SELECT min(rowid) FROM edges GROUP BY from_id, to_id, kind
            );
            CREATE UNIQUE INDEX IF NOT EXISTS idx_edges_unique ON edges(from_id, to_id, kind);

            CREATE TABLE IF NOT EXISTS metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            -- Promote workspace package dependencies to direct project DEPENDS_ON links
            INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
            SELECT DISTINCT d.from_id, i.to_id, 'DEPENDS_ON', '{"dependency_type":"library"}'
            FROM edges d
            JOIN edges i ON d.to_id = i.from_id
            WHERE d.kind = 'DEPENDS_ON'
              AND i.kind = 'IMPLEMENTED_BY'
              AND d.from_id != i.to_id
              AND d.from_id LIKE '%:project:%'
              AND i.to_id LIKE '%:project:%';
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
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM edges; DELETE FROM nodes; DELETE FROM metadata;";
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var vacCmd = _conn.CreateCommand())
            {
                vacCmd.CommandText = "VACUUM;";
                await vacCmd.ExecuteNonQueryAsync();
            }

            await using (var walCmd = _conn.CreateCommand())
            {
                walCmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await walCmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ClearWorkspaceAsync(string workspaceIdOrPath)
    {
        if (string.IsNullOrWhiteSpace(workspaceIdOrPath)) return false;

        var raw = workspaceIdOrPath.Trim();
        var normalized = raw.Replace('\\', '/').TrimEnd('/');

        await _lock.WaitAsync();
        try
        {
            var wsId = await FindWorkspaceIdByIdOrPathAsync(raw, normalized);
            if (wsId != null)
            {
                await DeleteWorkspaceHierarchyAsync(wsId);
                return true;
            }

            string absPath = normalized;
            string? relPath = null;

            await using (var rootCmd = _conn.CreateCommand())
            {
                rootCmd.CommandText = "SELECT json_extract(properties, '$.path') FROM nodes WHERE kind = 'Workspace' LIMIT 1;";
                var rootVal = (string?)await rootCmd.ExecuteScalarAsync();
                if (!string.IsNullOrEmpty(rootVal))
                {
                    var normRoot = rootVal.Replace('\\', '/').TrimEnd('/');
                    if (!Path.IsPathRooted(normalized))
                    {
                        absPath = Path.GetFullPath(Path.Combine(normRoot, normalized)).Replace('\\', '/').TrimEnd('/');
                    }
                    else
                    {
                        absPath = Path.GetFullPath(normalized).Replace('\\', '/').TrimEnd('/');
                    }

                    if (absPath.StartsWith(normRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        relPath = Path.GetRelativePath(normRoot, absPath).Replace('\\', '/');
                        if (relPath == ".") relPath = "";
                    }
                }
                else if (!Path.IsPathRooted(normalized))
                {
                    absPath = Path.GetFullPath(normalized).Replace('\\', '/').TrimEnd('/');
                }
            }

            return await DeleteSubpathHierarchyAsync(absPath, relPath);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<bool> DeleteSubpathHierarchyAsync(string absPath, string? relPath)
    {
        await using var cmd = _conn.CreateCommand();
        var normLower = absPath.ToLowerInvariant();
        var normSlashLower = (absPath.TrimEnd('/') + "/").ToLowerInvariant() + "%";

        cmd.Parameters.AddWithValue("@normPath", normLower);
        cmd.Parameters.AddWithValue("@normPathSlash", normSlashLower);

        var relLower = (relPath ?? "").ToLowerInvariant();
        var relSlashLower = string.IsNullOrEmpty(relPath) ? "" : (relPath.TrimEnd('/') + "/").ToLowerInvariant() + "%";
        cmd.Parameters.AddWithValue("@relPath", relLower);
        cmd.Parameters.AddWithValue("@relPathSlash", relSlashLower);
        cmd.Parameters.AddWithValue("@hasRel", !string.IsNullOrEmpty(relPath) ? 1 : 0);

        cmd.CommandText = """
            CREATE TEMP TABLE IF NOT EXISTS temp_ws_del(id TEXT PRIMARY KEY);
            DELETE FROM temp_ws_del;

            -- 1. Match nodes directly by ID prefix
            INSERT OR IGNORE INTO temp_ws_del(id)
            SELECT id FROM nodes
            WHERE lower(replace(id, '\', '/')) LIKE 'workspace:folder:' || @normPath || '/%'
               OR lower(replace(id, '\', '/')) = 'workspace:folder:' || @normPath
               OR (@hasRel = 1 AND (
                   lower(replace(id, '\', '/')) LIKE 'workspace:file:' || @relPath || '/%'
                   OR lower(replace(id, '\', '/')) = 'workspace:file:' || @relPath
                   OR lower(replace(id, '\', '/')) LIKE 'workspace:project:' || @relPath || ':%'
                   OR lower(replace(id, '\', '/')) = 'workspace:project:' || @relPath || ':'
                   OR lower(replace(id, '\', '/')) LIKE 'workspace:symbol:' || @relPath || '/%'
                   OR lower(replace(id, '\', '/')) = 'workspace:symbol:' || @relPath
               ));

            -- 2. Match File nodes by full_path or relative path
            INSERT OR IGNORE INTO temp_ws_del(id)
            SELECT id FROM nodes
            WHERE kind = 'File'
              AND (
                  lower(replace(json_extract(properties, '$.full_path'), '\', '/')) = @normPath
                  OR lower(replace(json_extract(properties, '$.full_path'), '\', '/')) LIKE @normPathSlash
                  OR (@hasRel = 1 AND (
                      lower(replace(json_extract(properties, '$.path'), '\', '/')) = @relPath
                      OR lower(replace(json_extract(properties, '$.path'), '\', '/')) LIKE @relPathSlash
                  ))
              );

            -- 3. Match Project nodes located in one of our deleted folders or by name/path
            INSERT OR IGNORE INTO temp_ws_del(id)
            SELECT id FROM nodes
            WHERE kind = 'Project'
              AND (
                  lower(replace(json_extract(properties, '$.path'), '\', '/')) = @normPath
                  OR (@hasRel = 1 AND (
                      lower(replace(json_extract(properties, '$.path'), '\', '/')) = @relPath
                      OR lower(json_extract(properties, '$.name')) = @relPath
                  ))
              );

            INSERT OR IGNORE INTO temp_ws_del(id)
            SELECT from_id FROM edges
            WHERE kind = 'LOCATED_IN' AND to_id IN (SELECT id FROM temp_ws_del);

            -- 4. Match ProjectSyntax and ProjectSemantic via BELONGS_TO
            INSERT OR IGNORE INTO temp_ws_del(id)
            SELECT from_id FROM edges
            WHERE kind = 'BELONGS_TO' AND to_id IN (SELECT id FROM temp_ws_del);
            """;
        await cmd.ExecuteNonQueryAsync();

        cmd.CommandText = "SELECT COUNT(*) FROM temp_ws_del;";
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        if (count == 0)
        {
            cmd.CommandText = "DROP TABLE IF EXISTS temp_ws_del;";
            await cmd.ExecuteNonQueryAsync();
            return false;
        }

        // 5. Include all levels of child nodes attached via CONTAINS
        while (true)
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO temp_ws_del(id)
                SELECT to_id FROM edges
                WHERE kind = 'CONTAINS'
                  AND from_id IN (SELECT id FROM temp_ws_del)
                  AND to_id NOT IN (SELECT id FROM temp_ws_del);
                """;
            var added = await cmd.ExecuteNonQueryAsync();
            if (added == 0) break;
        }

        cmd.CommandText = """
            -- 6. Fast indexed deletions across edges and nodes
            DELETE FROM edges WHERE from_id IN (SELECT id FROM temp_ws_del);
            DELETE FROM edges WHERE to_id IN (SELECT id FROM temp_ws_del);
            DELETE FROM nodes WHERE id IN (SELECT id FROM temp_ws_del);

            DROP TABLE IF EXISTS temp_ws_del;
            """;
        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    private async Task<string?> FindWorkspaceIdByIdOrPathAsync(string raw, string normalized)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            SELECT id FROM nodes
            WHERE kind = 'Workspace'
              AND (
                  id = @raw
                  OR id = @normalized
                  OR rtrim(replace(lower(json_extract(properties, '$.path')), '\', '/'), '/') = lower(@normalized)
                  OR lower(json_extract(properties, '$.name')) = lower(@raw)
              )
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@raw", raw);
        cmd.Parameters.AddWithValue("@normalized", normalized);
        return (string?)await cmd.ExecuteScalarAsync();
    }

    private async Task DeleteWorkspaceHierarchyAsync(string wsId)
    {
        var wsGlob = wsId + ":*";
        await using var cmd = _conn.CreateCommand();
        cmd.Parameters.AddWithValue("@wsId", wsId);
        cmd.Parameters.AddWithValue("@wsGlob", wsGlob);

        cmd.CommandText = """
            CREATE TEMP TABLE IF NOT EXISTS temp_ws_del(id TEXT PRIMARY KEY);
            DELETE FROM temp_ws_del;

            -- 1. Instant index-range scan for all nodes prefixed with workspace ID
            INSERT OR IGNORE INTO temp_ws_del(id)
            SELECT id FROM nodes WHERE id = @wsId OR id GLOB @wsGlob;
            """;
        await cmd.ExecuteNonQueryAsync();

        // 2. Include all levels of child nodes attached via CONTAINS
        while (true)
        {
            cmd.CommandText = """
                INSERT OR IGNORE INTO temp_ws_del(id)
                SELECT to_id FROM edges
                WHERE kind = 'CONTAINS'
                  AND from_id IN (SELECT id FROM temp_ws_del)
                  AND to_id NOT GLOB @wsGlob
                  AND to_id != @wsId;
                """;
            var added = await cmd.ExecuteNonQueryAsync();
            if (added == 0) break;
        }

        cmd.CommandText = """
            -- 3. Fast indexed deletions across edges and nodes
            DELETE FROM edges WHERE from_id IN (SELECT id FROM temp_ws_del);
            DELETE FROM edges WHERE to_id IN (SELECT id FROM temp_ws_del);
            DELETE FROM nodes WHERE id IN (SELECT id FROM temp_ws_del);

            DROP TABLE IF EXISTS temp_ws_del;
            """;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath)
    {
        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM nodes WHERE kind = 'Workspace' LIMIT 1;";
            var existing = (string?)await cmd.ExecuteScalarAsync();
            return existing ?? "workspace";
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
            const int batchSize = 100;
            var distinctNodes = nodes.Count > 1 ? nodes.DistinctBy(n => n.Id).ToList() : nodes;

            for (int i = 0; i < distinctNodes.Count; i += batchSize)
            {
                var count = Math.Min(batchSize, distinctNodes.Count - i);
                await using var cmd = _conn.CreateCommand();
                cmd.Transaction = tx;

                var sb = new System.Text.StringBuilder(count * 60 + 120);
                sb.Append("INSERT INTO nodes (id, kind, properties) VALUES ");
                for (int j = 0; j < count; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append($"(@i{j}, @k{j}, @p{j})");

                    var node = distinctNodes[i + j];
                    var dict = new Dictionary<string, object>(node.Properties) { ["id"] = node.Id };
                    cmd.Parameters.AddWithValue($"@i{j}", node.Id);
                    cmd.Parameters.AddWithValue($"@k{j}", node.Kind);
                    cmd.Parameters.AddWithValue($"@p{j}", JsonSerializer.Serialize(dict));
                }
                sb.Append(" ON CONFLICT(id) DO UPDATE SET kind = excluded.kind, properties = excluded.properties;");
                cmd.CommandText = sb.ToString();
                cmd.ExecuteNonQuery();
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
            const string emptyPropsJson = "{}";
            const int batchSize = 100;
            var distinctRels = rels.Count > 1 ? rels.DistinctBy(r => (r.From, r.To, r.Kind)).ToList() : rels;

            for (int i = 0; i < distinctRels.Count; i += batchSize)
            {
                var count = Math.Min(batchSize, distinctRels.Count - i);
                await using var cmd = _conn.CreateCommand();
                cmd.Transaction = tx;

                var sb = new System.Text.StringBuilder(count * 60 + 120);
                sb.Append("INSERT INTO edges (from_id, to_id, kind, properties) VALUES ");
                for (int j = 0; j < count; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append($"(@f{j}, @t{j}, @k{j}, @p{j})");

                    var rel = distinctRels[i + j];
                    cmd.Parameters.AddWithValue($"@f{j}", rel.From);
                    cmd.Parameters.AddWithValue($"@t{j}", rel.To);
                    cmd.Parameters.AddWithValue($"@k{j}", rel.Kind);
                    cmd.Parameters.AddWithValue($"@p{j}", rel.Properties == null || rel.Properties.Count == 0
                        ? emptyPropsJson
                        : JsonSerializer.Serialize(rel.Properties));
                }
                sb.Append(" ON CONFLICT(from_id, to_id, kind) DO UPDATE SET properties = excluded.properties;");
                cmd.CommandText = sb.ToString();
                cmd.ExecuteNonQuery();
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

    public async Task<string> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        ValidateQuerySecurity(query);
        var paramDict = ExtractParameters(parameters);
        var ast = CypherQueryParser.Parse(query);
        var compiled = SqliteCompiler.Compile(ast, paramDict);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.CommandText = compiled.Sql;
            foreach (var (k, v) in compiled.Parameters)
            {
                cmd.Parameters.AddWithValue("@" + k, v ?? DBNull.Value);
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            var rows = await ReadRowsAsync(reader, cancellationToken);
            sw.Stop();

            _logger.LogInformation(
                "[DB:Query] Completed in {ElapsedMs:F1}ms (rows: {RowCount}): {QueryPreview}",
                sw.Elapsed.TotalMilliseconds,
                rows.Count,
                GetQueryPreview(query));

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

    public async Task ExecuteWriteAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
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

            _logger.LogInformation("[DB:Write] Write completed in {ElapsedMs:F1}ms: {QueryPreview}",
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

    private async Task<bool> TryExecutePostIndexWriteAsync(string query, Dictionary<string, object?> paramDict)
    {
        if (query.Contains("external_apis") || query.Contains("TRANSITIVELY_CALLS") || query.Contains("ATTRIBUTED_TO"))
        {
            var widPrefix = ResolveWidPrefix(query, paramDict);
            var graphData = await LoadPostIndexGraphDataAsync(widPrefix);
            var result = PostIndexAnalyzer.Analyze(graphData, widPrefix);
            await SavePostIndexResultsAsync(widPrefix, result);
            return true;
        }

        return false;
    }

    private static string ResolveWidPrefix(string query, Dictionary<string, object?> paramDict)
    {
        if (paramDict.TryGetValue("widPrefix", out var wpObj) && wpObj != null)
        {
            return wpObj.ToString() ?? "";
        }

        var match = Regex.Match(query, @"STARTS WITH\s*['""]([^'""]*)['""]", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return "";
    }

    public async Task<Dictionary<(string Kind, string Name), string>> LoadSymbolsByNamesAsync(
        IEnumerable<string> names,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var distinctNames = names
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctNames.Count == 0) return [];

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");
        var namesJson = JsonSerializer.Serialize(distinctNames);
        var result = new Dictionary<(string Kind, string Name), string>();

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.Parameters.AddWithValue("@namesJson", namesJson);
            cmd.Parameters.AddWithValue("@widPrefix", widPrefix);
            cmd.CommandText = """
                WITH target_names(val) AS (
                    SELECT value FROM json_each(@namesJson)
                )
                SELECT n.id, n.kind, json_extract(n.properties, '$.name') AS name,
                       json_extract(p.properties, '$.name') AS parent_name
                FROM nodes n
                JOIN target_names t ON json_extract(n.properties, '$.name') = t.val
                LEFT JOIN edges e ON e.to_id = n.id AND e.kind IN ('HAS_METHOD', 'CONTAINS')
                LEFT JOIN nodes p ON p.id = e.from_id AND p.kind = 'Type'
                WHERE n.kind IN ('Type', 'Function', 'Procedure', 'Table', 'EntryPoint', 'Endpoint')
                  AND (length(@widPrefix) = 0 OR n.id LIKE @widPrefix || '%');
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var kind = reader.GetString(1);
                var name = reader.IsDBNull(2) ? "" : reader.GetString(2);
                var parentName = reader.IsDBNull(3) ? null : reader.GetString(3);

                if (!string.IsNullOrEmpty(name))
                {
                    result[(kind, name)] = id;

                    if (kind == "Endpoint" || kind == "EntryPoint")
                    {
                        result[(kind, name.Replace(":", " "))] = id;
                    }

                    if (kind == "Function" && !string.IsNullOrEmpty(parentName))
                    {
                        result[(kind, $"{parentName}.{name}")] = id;
                    }
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        return result;
    }

    public async Task<Dictionary<string, List<string>>> LoadImplementationsForTypesAsync(
        IEnumerable<string> typeNames,
        string workspaceId,
        CancellationToken cancellationToken = default)
    {
        var distinctTypes = typeNames
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctTypes.Count == 0) return [];

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");
        var typesJson = JsonSerializer.Serialize(distinctTypes);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.Parameters.AddWithValue("@typesJson", typesJson);
            cmd.Parameters.AddWithValue("@widPrefix", widPrefix);
            cmd.CommandText = """
                WITH target_types(val) AS (
                    SELECT value FROM json_each(@typesJson)
                )
                SELECT json_extract(t_target.properties, '$.name') AS iface_name,
                       json_extract(t_impl.properties, '$.name') AS impl_name
                FROM edges e
                JOIN nodes t_target ON t_target.id = e.to_id AND t_target.kind = 'Type'
                JOIN target_types tt ON json_extract(t_target.properties, '$.name') = tt.val
                JOIN nodes t_impl ON t_impl.id = e.from_id AND t_impl.kind = 'Type'
                WHERE e.kind = 'IMPLEMENTS'
                  AND (length(@widPrefix) = 0 OR e.from_id LIKE @widPrefix || '%');
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var iface = reader.IsDBNull(0) ? null : reader.GetString(0);
                var impl = reader.IsDBNull(1) ? null : reader.GetString(1);

                if (!string.IsNullOrEmpty(iface) && !string.IsNullOrEmpty(impl))
                {
                    if (!result.TryGetValue(iface, out var list))
                    {
                        list = [];
                        result[iface] = list;
                    }
                    if (!list.Contains(impl))
                    {
                        list.Add(impl);
                    }
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        return result;
    }

    public async Task<(List<EndpointNode> Endpoints, List<EntryPointNode> EntryPoints, List<ExternalServiceNode> ExternalServices)> LoadLateBindingCandidatesAsync(
        string workspaceId,
        bool needEndpoints,
        bool needExternalServices,
        CancellationToken cancellationToken = default)
    {
        var endpoints = new List<EndpointNode>();
        var entryPoints = new List<EntryPointNode>();
        var externalServices = new List<ExternalServiceNode>();

        if (!needEndpoints && !needExternalServices)
        {
            return (endpoints, entryPoints, externalServices);
        }

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");

        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.Parameters.AddWithValue("@widPrefix", widPrefix);
            cmd.Parameters.AddWithValue("@needEndpoints", needEndpoints ? 1 : 0);
            cmd.Parameters.AddWithValue("@needExternalServices", needExternalServices ? 1 : 0);

            cmd.CommandText = """
                SELECT id, kind, properties
                FROM nodes
                WHERE (length(@widPrefix) = 0 OR id LIKE @widPrefix || '%')
                  AND (
                    (@needEndpoints = 1 AND kind IN ('Endpoint', 'EntryPoint'))
                    OR
                    (@needExternalServices = 1 AND kind = 'ExternalService')
                  );
                """;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var kind = reader.GetString(1);
                var propsJson = reader.GetString(2);

                try
                {
                    using var doc = JsonDocument.Parse(propsJson);
                    var root = doc.RootElement;

                    if (kind == "Endpoint")
                    {
                        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var filePath = root.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "";
                        var httpMethod = root.TryGetProperty("http_method", out var hm) ? hm.GetString() ?? "" : "";
                        var route = root.TryGetProperty("route", out var r) ? r.GetString() ?? "" : "";
                        endpoints.Add(new EndpointNode(id, name, filePath, httpMethod, route));
                    }
                    else if (kind == "EntryPoint")
                    {
                        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var path = root.TryGetProperty("path", out var p) ? p.GetString() ?? "" : "";
                        var type = root.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
                        entryPoints.Add(new EntryPointNode(id, name, path, type));
                    }
                    else if (kind == "ExternalService")
                    {
                        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        var protocol = root.TryGetProperty("protocol", out var pr) ? pr.GetString() ?? "" : (root.TryGetProperty("service_type", out var st) ? st.GetString() ?? "" : "");
                        var domain = root.TryGetProperty("domain_or_service", out var d) ? d.GetString() ?? "" : "";
                        var path = root.TryGetProperty("path", out var p) ? p.GetString() ?? "" : (root.TryGetProperty("target_path", out var tp) ? tp.GetString() ?? "" : "");
                        externalServices.Add(new ExternalServiceNode(id, name, protocol, domain, path));
                    }
                }
                catch
                {
                    // Ignore malformed node properties
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        return (endpoints, entryPoints, externalServices);
    }

    public async Task<PostIndexGraphData> LoadPostIndexGraphDataAsync(string widPrefix)
    {
        await _lock.WaitAsync();
        try
        {
            // 1. Sinks (ExternalService, DB, Database, Query, CloudService)
            var sinks = new Dictionary<string, string>();
            var sinkDomains = new Dictionary<string, string?>();
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, kind, COALESCE(json_extract(properties, '$.domain_or_service'), json_extract(properties, '$.name')) FROM nodes WHERE kind IN ('ExternalService', 'DB', 'Database', 'Query', 'CloudService');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var id = reader.GetString(0);
                    var kind = reader.GetString(1);
                    var domain = reader.IsDBNull(2) ? null : reader.GetString(2);
                    sinks[id] = kind;
                    sinkDomains[id] = domain;
                }
            }

            // 2. CALLS edges (including reversed CALLED_BY, QUERIED_BY, and USES_DB)
            var callsAdjacency = new Dictionary<string, List<string>>();
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT from_id, to_id FROM edges WHERE kind IN ('CALLS', 'CALLS_ENDPOINT', 'USES_DB')
                    UNION
                    SELECT to_id, from_id FROM edges WHERE kind IN ('CALLED_BY', 'QUERIED_BY');
                    """;
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var from = reader.GetString(0);
                    var to = reader.GetString(1);
                    if (!callsAdjacency.TryGetValue(from, out var list))
                    {
                        list = [];
                        callsAdjacency[from] = list;
                    }
                    list.Add(to);
                }
            }

            // 3. IMPLEMENTS, IMPLEMENTED_BY, and EXPOSED_BY edges
            var implements = new Dictionary<string, List<string>>(); // epId -> list of fnIds
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT from_id, to_id, kind FROM edges WHERE kind IN ('IMPLEMENTS', 'IMPLEMENTED_BY', 'EXPOSED_BY');";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var from = reader.GetString(0);
                    var to = reader.GetString(1);
                    var kind = reader.GetString(2);
                    var epId = kind == "IMPLEMENTS" ? to : from;
                    var fnId = kind == "IMPLEMENTS" ? from : to;

                    if (!implements.TryGetValue(epId, out var list))
                    {
                        list = [];
                        implements[epId] = list;
                    }
                    if (!list.Contains(fnId))
                    {
                        list.Add(fnId);
                    }
                }
            }

            // 4. EntryPoints
            var entryPointIds = new List<string>();
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id FROM nodes WHERE kind IN ('EntryPoint', 'Endpoint') AND (length(@widPrefix) = 0 OR id LIKE @widPrefix || '%');";
                cmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    entryPointIds.Add(reader.GetString(0));
                }
            }

            // 5. Callers (Function / Method)
            var callers = new List<string>();
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id FROM nodes WHERE kind IN ('Function', 'Method') AND (length(@widPrefix) = 0 OR id LIKE @widPrefix || '%');";
                cmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    callers.Add(reader.GetString(0));
                }
            }

            // 6. Project to EntryPoints
            var projectToEntryPoints = new Dictionary<string, List<string>>();
            var epSet = new HashSet<string>(entryPointIds);
            await using (var cmd = _conn.CreateCommand())
            {
                cmd.CommandText = """
                    SELECT p.id, e.to_id
                    FROM nodes p
                    JOIN edges e ON e.from_id = p.id AND e.kind IN ('CONTAINS', 'EXPOSES')
                    WHERE p.kind = 'Project' AND (length(@widPrefix) = 0 OR p.id LIKE @widPrefix || '%')
                    UNION
                    SELECT p.id, e2.to_id
                    FROM nodes p
                    JOIN edges e1 ON e1.from_id = p.id AND e1.kind IN ('CONTAINS', 'EXPOSES')
                    JOIN edges e2 ON e2.from_id = e1.to_id AND e2.kind IN ('CONTAINS', 'EXPOSES')
                    WHERE p.kind = 'Project' AND (length(@widPrefix) = 0 OR p.id LIKE @widPrefix || '%')
                    UNION
                    SELECT p.id, e.to_id
                    FROM nodes p
                    JOIN edges b ON b.to_id = p.id AND b.kind = 'BELONGS_TO'
                    JOIN edges e ON e.from_id = b.from_id AND e.kind IN ('CONTAINS', 'EXPOSES')
                    WHERE p.kind = 'Project' AND (length(@widPrefix) = 0 OR p.id LIKE @widPrefix || '%');
                    """;
                cmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var projId = reader.GetString(0);
                    var toId = reader.GetString(1);
                    if (!epSet.Contains(toId)) continue;

                    if (!projectToEntryPoints.TryGetValue(projId, out var epList))
                    {
                        epList = [];
                        projectToEntryPoints[projId] = epList;
                    }
                    if (!epList.Contains(toId))
                    {
                        epList.Add(toId);
                    }
                }
            }

            return new PostIndexGraphData(callsAdjacency, sinks, sinkDomains, callers, implements, entryPointIds, projectToEntryPoints);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SavePostIndexResultsAsync(string widPrefix, PostIndexAnalysisResult result)
    {
        var sw = Stopwatch.StartNew();
        await _lock.WaitAsync();
        try
        {
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();

            // 1. Delete old TRANSITIVELY_CALLS and ATTRIBUTED_TO for workspace
            await using (var delCmd = _conn.CreateCommand())
            {
                delCmd.Transaction = tx;
                delCmd.CommandText = "DELETE FROM edges WHERE kind = 'TRANSITIVELY_CALLS' AND (length(@widPrefix) = 0 OR from_id LIKE @widPrefix || '%');";
                delCmd.Parameters.AddWithValue("@widPrefix", widPrefix);
                await delCmd.ExecuteNonQueryAsync();

                delCmd.CommandText = "DELETE FROM edges WHERE kind = 'ATTRIBUTED_TO' AND (length(@widPrefix) = 0 OR from_id LIKE @widPrefix || '%');";
                await delCmd.ExecuteNonQueryAsync();
            }

            // 2. Insert TRANSITIVELY_CALLS
            if (result.TransitivelyCalls.Count > 0)
            {
                await using var insCmd = _conn.CreateCommand();
                insCmd.Transaction = tx;
                insCmd.CommandText = "INSERT OR REPLACE INTO edges (from_id, to_id, kind, properties) VALUES (@from, @to, 'TRANSITIVELY_CALLS', @props);";
                var pFrom = insCmd.Parameters.Add("@from", SqliteType.Text);
                var pTo = insCmd.Parameters.Add("@to", SqliteType.Text);
                var pProps = insCmd.Parameters.Add("@props", SqliteType.Text);

                foreach (var rel in result.TransitivelyCalls)
                {
                    pFrom.Value = rel.From;
                    pTo.Value = rel.To;
                    pProps.Value = $"{{\"hops\":{rel.Hops}}}";
                    await insCmd.ExecuteNonQueryAsync();
                }
            }

            // 3. Insert ATTRIBUTED_TO
            if (result.AttributedTo.Count > 0)
            {
                await using var insCmd = _conn.CreateCommand();
                insCmd.Transaction = tx;
                insCmd.CommandText = "INSERT OR REPLACE INTO edges (from_id, to_id, kind, properties) VALUES (@from, @to, 'ATTRIBUTED_TO', @props);";
                var pFrom = insCmd.Parameters.Add("@from", SqliteType.Text);
                var pTo = insCmd.Parameters.Add("@to", SqliteType.Text);
                var pProps = insCmd.Parameters.Add("@props", SqliteType.Text);

                foreach (var rel in result.AttributedTo)
                {
                    pFrom.Value = rel.EpId;
                    pTo.Value = rel.SinkId;
                    var props = new Dictionary<string, object>
                    {
                        ["hops"] = rel.Hops,
                        ["sink_kind"] = rel.SinkKind
                    };
                    pProps.Value = JsonSerializer.Serialize(props);
                    await insCmd.ExecuteNonQueryAsync();
                }
            }

            // 4. Update Project external_apis
            if (result.ProjectExternalApis.Count > 0)
            {
                await using var updCmd = _conn.CreateCommand();
                updCmd.Transaction = tx;
                updCmd.CommandText = "UPDATE nodes SET properties = json_set(properties, '$.external_apis', json(@domains)) WHERE id = @id;";
                var pDomains = updCmd.Parameters.Add("@domains", SqliteType.Text);
                var pId = updCmd.Parameters.Add("@id", SqliteType.Text);

                foreach (var (projId, domains) in result.ProjectExternalApis)
                {
                    pId.Value = projId;
                    pDomains.Value = JsonSerializer.Serialize(domains);
                    await updCmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
            sw.Stop();
            _logger.LogInformation(
                "[DB:PostIndex] Saved {TcCount} TRANSITIVELY_CALLS, {AttrCount} ATTRIBUTED_TO, and {ApiCount} project external_apis for prefix '{Prefix}' in {ElapsedMs:F1}ms",
                result.TransitivelyCalls.Count, result.AttributedTo.Count, result.ProjectExternalApis.Count, widPrefix, sw.Elapsed.TotalMilliseconds);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateProjectExternalApisAsync(Dictionary<string, List<string>> projectExternalApis)
    {
        if (projectExternalApis.Count == 0) return;
        await _lock.WaitAsync();
        try
        {
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();
            await using var updCmd = _conn.CreateCommand();
            updCmd.Transaction = tx;
            updCmd.CommandText = "UPDATE nodes SET properties = json_set(properties, '$.external_apis', json(@domains)) WHERE id = @id;";
            var pDomains = updCmd.Parameters.Add("@domains", SqliteType.Text);
            var pId = updCmd.Parameters.Add("@id", SqliteType.Text);

            foreach (var (projId, domains) in projectExternalApis)
            {
                pId.Value = projId;
                pDomains.Value = JsonSerializer.Serialize(domains);
                await updCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
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

    private static void ValidateQuerySecurity(string query)
    {
        CodeExplorer.Cypher.Parser.CypherSecurityValidator.ValidateReadOnly(query);
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
