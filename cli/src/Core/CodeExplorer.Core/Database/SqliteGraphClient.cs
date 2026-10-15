using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
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
                          PRAGMA auto_vacuum = INCREMENTAL;
                          """;
        cmd.ExecuteNonQuery();
    }

    private void CheckSchemaVersion()
    {
        using var cmd = _conn.CreateCommand();

        cmd.CommandText = "PRAGMA user_version;";
        var result = cmd.ExecuteScalar();
        var ver = result is long l ? (int)l : (result is int i ? i : 0);

        if (ver == 0)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='nodes';";
            var tableExists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;

            if (tableExists)
            {
                cmd.CommandText = "SELECT COUNT(*) FROM nodes LIMIT 1;";

                try
                {
                    var count = Convert.ToInt32(cmd.ExecuteScalar());

                    if (count > 0)
                    {
                        SchemaVersion = 1;
                        return;
                    }
                }
                catch
                {
                    // Ignore
                }
            }

            cmd.CommandText = $"PRAGMA user_version = {CurrentSchemaVersion};";
            cmd.ExecuteNonQuery();
            SchemaVersion = CurrentSchemaVersion;
        }
        else if (ver < CurrentSchemaVersion)
        {
            try
            {
                using var migrateCmd = _conn.CreateCommand();

                migrateCmd.CommandText = $"""
                                          DELETE FROM edges WHERE from_id IN (
                                              SELECT p1.id FROM nodes p1
                                              WHERE p1.kind = 'Project' AND (p1.id LIKE '%:project:%' OR p1.id LIKE '%:project')
                                              AND EXISTS (
                                                  SELECT 1 FROM nodes p2 
                                                  WHERE p2.kind = 'Project' AND p2.id LIKE '%:p:%'
                                                  AND json_extract(p2.properties, '$.path') = json_extract(p1.properties, '$.path')
                                              )
                                          );
                                          DELETE FROM edges WHERE to_id IN (
                                              SELECT p1.id FROM nodes p1
                                              WHERE p1.kind = 'Project' AND (p1.id LIKE '%:project:%' OR p1.id LIKE '%:project')
                                              AND EXISTS (
                                                  SELECT 1 FROM nodes p2 
                                                  WHERE p2.kind = 'Project' AND p2.id LIKE '%:p:%'
                                                  AND json_extract(p2.properties, '$.path') = json_extract(p1.properties, '$.path')
                                              )
                                          );
                                          DELETE FROM nodes 
                                          WHERE kind = 'Project' AND (id LIKE '%:project:%' OR id LIKE '%:project')
                                          AND EXISTS (
                                              SELECT 1 FROM nodes p2 
                                              WHERE p2.kind = 'Project' AND p2.id LIKE '%:p:%'
                                              AND json_extract(p2.properties, '$.path') = json_extract(nodes.properties, '$.path')
                                          );
                                          DELETE FROM edges WHERE from_id IN (
                                              SELECT id FROM nodes WHERE kind IN ('ProjectSemantic', 'ProjectSyntax') AND id LIKE '%:project:%'
                                          );
                                          DELETE FROM edges WHERE to_id IN (
                                              SELECT id FROM nodes WHERE kind IN ('ProjectSemantic', 'ProjectSyntax') AND id LIKE '%:project:%'
                                          );
                                          DELETE FROM nodes WHERE kind IN ('ProjectSemantic', 'ProjectSyntax') AND id LIKE '%:project:%';
                                          DELETE FROM edges WHERE from_id NOT IN (SELECT id FROM nodes) OR to_id NOT IN (SELECT id FROM nodes);
                                          PRAGMA user_version = {CurrentSchemaVersion};
                                          """;
                migrateCmd.ExecuteNonQuery();
                SchemaVersion = CurrentSchemaVersion;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Schema migration from v{From} to v{To} encountered an error: {Message}",
                    ver, CurrentSchemaVersion, ex.Message);
                SchemaVersion = ver;
            }
        }
        else
        {
            SchemaVersion = ver;
        }
    }

    public async Task SetSchemaVersionAsync(int version)
    {
        await _lock.WaitAsync();
        try
        {
            await _conn.ExecuteAsync($"PRAGMA user_version = {version};");
            SchemaVersion = version;
        }
        finally
        {
            _lock.Release();
        }
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
                          CREATE INDEX IF NOT EXISTS idx_nodes_name_kind ON nodes(json_extract(properties, '$.name'), kind);
                          CREATE INDEX IF NOT EXISTS idx_nodes_kind_symbol ON nodes(kind, json_extract(properties, '$.symbol'));
                          CREATE INDEX IF NOT EXISTS idx_nodes_kind_lower_path ON nodes(kind, lower(json_extract(properties, '$.path')));

                          CREATE TABLE IF NOT EXISTS edges (
                              from_id TEXT NOT NULL,
                              to_id TEXT NOT NULL,
                              kind TEXT NOT NULL,
                              properties JSON NOT NULL
                          );
                          DROP INDEX IF EXISTS idx_edges_from;
                          DROP INDEX IF EXISTS idx_edges_to;
                          DROP INDEX IF EXISTS idx_edges_kind;
                          DROP INDEX IF EXISTS idx_edges_from_kind;
                          DROP INDEX IF EXISTS idx_edges_to_kind;

                          CREATE INDEX IF NOT EXISTS idx_edges_from_kind_to ON edges(from_id, kind, to_id);
                          CREATE INDEX IF NOT EXISTS idx_edges_to_kind_from ON edges(to_id, kind, from_id);
                          CREATE INDEX IF NOT EXISTS idx_edges_kind_from_to ON edges(kind, from_id, to_id);
                          DELETE FROM edges WHERE rowid NOT IN (
                              SELECT min(rowid) FROM edges GROUP BY from_id, to_id, kind
                          );
                          CREATE UNIQUE INDEX IF NOT EXISTS idx_edges_unique ON edges(from_id, to_id, kind);

                          CREATE TABLE IF NOT EXISTS file_registry (
                              relative_path TEXT PRIMARY KEY,
                              content_hash TEXT NOT NULL,
                              last_modified_utc TEXT NOT NULL,
                              project_path TEXT NOT NULL,
                              snapshot_json TEXT
                          );
                          CREATE INDEX IF NOT EXISTS idx_file_registry_hash ON file_registry(content_hash);

                          CREATE TABLE IF NOT EXISTS metadata (
                              key TEXT PRIMARY KEY,
                              value TEXT NOT NULL
                          );

                          CREATE TABLE IF NOT EXISTS identifier_index (
                              node_id TEXT PRIMARY KEY,
                              name TEXT NOT NULL,
                              full_name TEXT NOT NULL,
                              kind TEXT NOT NULL,
                              path TEXT,
                              start_line INTEGER DEFAULT 0,
                              embedding BLOB NOT NULL
                          );
                          CREATE INDEX IF NOT EXISTS idx_ididx_name ON identifier_index(name);
                          CREATE INDEX IF NOT EXISTS idx_ididx_fullname ON identifier_index(full_name);
                          CREATE INDEX IF NOT EXISTS idx_ididx_kind ON identifier_index(kind);

                          CREATE VIRTUAL TABLE IF NOT EXISTS identifier_fts USING fts5(
                              name,
                              full_name,
                              path,
                              node_id UNINDEXED,
                              kind UNINDEXED,
                              tokenize='trigram'
                          );

                          CREATE TABLE IF NOT EXISTS intents (
                              file_path TEXT PRIMARY KEY,
                              workspace_id TEXT NOT NULL,
                              file_id TEXT NOT NULL,
                              content_hash TEXT NOT NULL,
                              last_modified_utc TEXT NOT NULL,
                              domain TEXT,
                              layer TEXT,
                              pattern TEXT,
                              operation_type TEXT,
                              capability_tag TEXT,
                              intent_summary TEXT,
                              target_entities TEXT,
                              emitted_events TEXT,
                              is_pure_domain INTEGER,
                              error_count INTEGER DEFAULT 0,
                              last_error TEXT,
                              analyzed_at_utc TEXT
                          );
                          CREATE INDEX IF NOT EXISTS idx_intents_ws ON intents(workspace_id);
                          CREATE INDEX IF NOT EXISTS idx_intents_file_id ON intents(file_id);
                          CREATE INDEX IF NOT EXISTS idx_intents_file_path ON intents(file_path);
                          CREATE INDEX IF NOT EXISTS idx_intents_domain ON intents(domain);
                          CREATE INDEX IF NOT EXISTS idx_intents_hash ON intents(content_hash);
                          CREATE INDEX IF NOT EXISTS idx_intents_errors ON intents(error_count);

                          -- Promote workspace package dependencies to direct project DEPENDS_ON links
                          INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                          SELECT DISTINCT d.from_id, i.to_id, 'DEPENDS_ON', '{"dependency_type":"library"}'
                          FROM edges d
                          JOIN edges i ON d.to_id = i.from_id
                          WHERE d.kind = 'DEPENDS_ON'
                            AND i.kind = 'IMPLEMENTED_BY'
                            AND d.from_id != i.to_id
                            AND (d.from_id LIKE '%:p:%' OR d.from_id LIKE '%:project:%')
                            AND (i.to_id LIKE '%:p:%' OR i.to_id LIKE '%:project:%');

                          -- Promote project_semantic edges to direct Project node edges
                          INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                          SELECT DISTINCT
                              SUBSTR(from_id, 1, LENGTH(from_id) - LENGTH('project_semantic')),
                              to_id,
                              kind,
                              properties
                          FROM edges
                          WHERE from_id LIKE '%:p:%:project_semantic' OR from_id LIKE '%:project:%:project_semantic';

                          INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties)
                          SELECT DISTINCT
                              from_id,
                              SUBSTR(to_id, 1, LENGTH(to_id) - LENGTH('project_semantic')),
                              kind,
                              properties
                          FROM edges
                          WHERE to_id LIKE '%:p:%:project_semantic' OR to_id LIKE '%:project:%:project_semantic';
                          """;
        cmd.ExecuteNonQuery();

        try
        {
            using var alterCmd = _conn.CreateCommand();

            alterCmd.CommandText = "ALTER TABLE file_registry ADD COLUMN snapshot_json TEXT;";
            alterCmd.ExecuteNonQuery();
        }
        catch
        {
            // Column already exists or freshly created
        }

        try
        {
            using var statCmd = _conn.CreateCommand();
            statCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='sqlite_stat1';";
            var hasStats = Convert.ToInt32(statCmd.ExecuteScalar()) > 0;
            if (!hasStats)
            {
                statCmd.CommandText = "ANALYZE;";
                statCmd.ExecuteNonQuery();
            }
            else
            {
                statCmd.CommandText = "PRAGMA optimize;";
                statCmd.ExecuteNonQuery();
            }
        }
        catch
        {
            // Read-only database or in-memory
        }
    }

    public async Task CreateIndicesAsync()
    {
        CreateIndices();
        await _lock.WaitAsync();
        try
        {
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "ANALYZE; PRAGMA optimize;";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to analyze database after indexing.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearDatabaseAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await _conn.ExecuteAsync("""
                DELETE FROM edges;
                DELETE FROM nodes;
                DELETE FROM file_registry;
                DELETE FROM metadata;
                """);
            await _conn.ExecuteAsync("VACUUM;");
            await _conn.ExecuteAsync("PRAGMA wal_checkpoint(TRUNCATE);");

            Analysis.ArchitectureViewEngine.InvalidateCache();
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
            var totalWorkspaces = await _conn.ExecuteScalarAsync<int>("SELECT count(DISTINCT id) FROM nodes WHERE kind = 'Workspace';");

            var wsId = await FindWorkspaceIdByIdOrPathAsync(raw, normalized);
            if (wsId != null)
            {
                await DeleteWorkspaceHierarchyAsync(wsId, totalWorkspaces);
                return true;
            }

            // Retrieve all workspaces to find if normalized matches or is a subpath of any workspace
            var workspaces = (await _conn.QueryAsync<(string Id, string Path)>(
                "SELECT id, json_extract(properties, '$.path') AS Path FROM nodes WHERE kind = 'Workspace';")).AsList();

            (string Id, string Path)? matchingWs = null;

            foreach (var ws in workspaces)
            {
                if (string.IsNullOrEmpty(ws.Path)) continue;
                var normPath = ws.Path.Replace('\\', '/').TrimEnd('/');

                if (string.Equals(normalized, normPath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(raw, ws.Id, StringComparison.OrdinalIgnoreCase))
                {
                    matchingWs = (ws.Id, normPath);
                    break;
                }

                if (normalized.StartsWith(normPath + "/", StringComparison.OrdinalIgnoreCase))
                {
                    matchingWs = (ws.Id, normPath);
                    break;
                }
            }

            if (matchingWs != null)
            {
                var normRoot = matchingWs.Value.Path;
                if (string.Equals(normalized, normRoot, StringComparison.OrdinalIgnoreCase))
                {
                    await DeleteWorkspaceHierarchyAsync(matchingWs.Value.Id, totalWorkspaces);
                    return true;
                }

                var relPath = normalized.Substring(normRoot.Length).TrimStart('/');

                // Fast wipe if all files and projects in this single workspace are under this subpath
                if (totalWorkspaces <= 1)
                {
                    var relSlash = (relPath.TrimEnd('/') + "/").ToLowerInvariant();
                    var hasFilesOutside = await _conn.ExecuteScalarAsync<int>("""
                        SELECT count(*) FROM file_registry
                        WHERE lower(replace(relative_path, '\', '/')) NOT LIKE @relSlash || '%'
                          AND lower(replace(relative_path, '\', '/')) != @relLower;
                        """, new { relSlash, relLower = relPath.ToLowerInvariant() });

                    if (hasFilesOutside == 0)
                    {
                        var normSlash = (normalized.TrimEnd('/') + "/").ToLowerInvariant();
                        var hasProjectsOutside = await _conn.ExecuteScalarAsync<int>("""
                            SELECT count(*) FROM nodes
                            WHERE kind = 'Project'
                              AND lower(replace(json_extract(properties, '$.path'), '\', '/')) NOT LIKE @normSlash || '%'
                              AND lower(replace(json_extract(properties, '$.path'), '\', '/')) != @normLower;
                            """, new { normSlash, normLower = normalized.ToLowerInvariant() });

                        if (hasProjectsOutside == 0)
                        {
                            await WipeDatabaseFastAsync();
                            return true;
                        }
                    }
                }

                return await DeleteSubpathHierarchyAsync(normalized, relPath, matchingWs.Value.Id);
            }

            if (totalWorkspaces <= 1 && workspaces.Count > 0 && !string.IsNullOrEmpty(workspaces[0].Path))
            {
                var singleWs = workspaces[0];
                var normRoot = singleWs.Path.Replace('\\', '/').TrimEnd('/');

                if (!normalized.StartsWith('/') && !normalized.Contains(':'))
                {
                    var absPath = normRoot + "/" + normalized.TrimStart('/');
                    var relPath = normalized.TrimStart('/');
                    return await DeleteSubpathHierarchyAsync(absPath, relPath, singleWs.Id);
                }
            }

            if (totalWorkspaces == 0)
            {
                var anyNodes = await _conn.ExecuteScalarAsync<int>("SELECT count(*) FROM nodes LIMIT 1;");
                if (anyNodes == 0) return false;

                if (normalized.StartsWith('/') || normalized.Contains(':'))
                {
                    await WipeDatabaseFastAsync();
                    return true;
                }
            }

            return await DeleteSubpathHierarchyAsync(normalized, null, null);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task WipeDatabaseFastAsync()
    {
        using var tx = _conn.BeginTransaction();
        try
        {
            await _conn.ExecuteAsync("""
                DELETE FROM edges;
                DELETE FROM nodes;
                DELETE FROM file_registry;
                """, transaction: tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        Analysis.ArchitectureViewEngine.InvalidateCache();
    }

    private async Task<bool> DeleteSubpathHierarchyAsync(string absPath, string? relPath, string? wsId = null)
    {
        var normLower = absPath.ToLowerInvariant();
        var normSlashLower = (absPath.TrimEnd('/') + "/").ToLowerInvariant() + "%";
        var relLower = (relPath ?? "").ToLowerInvariant();
        var relSlashLower = string.IsNullOrEmpty(relPath) ? "" : (relPath.TrimEnd('/') + "/").ToLowerInvariant() + "%";
        var relColonLower = string.IsNullOrEmpty(relPath) ? "" : (relPath.TrimEnd(':') + ":").ToLowerInvariant() + "%";
        var hasRel = !string.IsNullOrEmpty(relPath) ? 1 : 0;
        var wsPrefix = !string.IsNullOrEmpty(wsId) ? wsId + ":" : "";
        var hasWsPrefix = !string.IsNullOrEmpty(wsId) ? 1 : 0;

        var parameters = new
        {
            normPath = normLower,
            normPathSlash = normSlashLower,
            relPath = relLower,
            relPathSlash = relSlashLower,
            relPathColon = relColonLower,
            hasRel,
            wsPrefix,
            hasWsPrefix
        };

        using var tx = _conn.BeginTransaction();
        try
        {
            await _conn.ExecuteAsync("""
                CREATE TEMP TABLE IF NOT EXISTS temp_del_subpath_nodes (id TEXT PRIMARY KEY, is_container INTEGER DEFAULT 0);
                DELETE FROM temp_del_subpath_nodes;

                -- Pass 1: Deterministic ID prefix matching. If wsPrefix is known, uses primary key index on nodes.id!
                INSERT OR IGNORE INTO temp_del_subpath_nodes(id, is_container)
                SELECT id,
                       CASE WHEN id LIKE '%:dir:%' OR id LIKE '%:p:%' OR id LIKE '%:project:%' OR id LIKE '%:f:%' OR id LIKE '%:file:%' THEN 1 ELSE 0 END
                FROM nodes
                WHERE (@hasWsPrefix = 1 AND @hasRel = 1 AND (
                          id LIKE @wsPrefix || 'f:' || @relPath
                       OR id LIKE @wsPrefix || 'f:' || @relPathSlash
                       OR id LIKE @wsPrefix || 'file:' || @relPath
                       OR id LIKE @wsPrefix || 'file:' || @relPathSlash
                       OR id LIKE @wsPrefix || 'sym:' || @relPathSlash
                       OR id LIKE @wsPrefix || 'sym:' || @relPathColon
                       OR id LIKE @wsPrefix || 'symbol:' || @relPathSlash
                       OR id LIKE @wsPrefix || 'symbol:' || @relPathColon
                       OR id LIKE @wsPrefix || 'lit:' || @relPathSlash
                       OR id LIKE @wsPrefix || 'lit:' || @relPathColon
                       OR id LIKE @wsPrefix || 'catch:' || @relPathSlash
                       OR id LIKE @wsPrefix || 'catch:' || @relPathColon
                       OR id LIKE @wsPrefix || 'p:' || @relPath || '%'
                       OR id LIKE @wsPrefix || 'project:' || @relPath || '%'
                       OR id LIKE @wsPrefix || 'git:' || @relPath
                       OR id LIKE @wsPrefix || 'git:' || @relPathSlash
                       OR id LIKE @wsPrefix || 'ep:' || @relPathSlash
                       OR id LIKE @wsPrefix || 'suite:' || @relPathSlash
                      ))
                   OR (@hasWsPrefix = 0 AND @hasRel = 1 AND (
                          id LIKE '%:f:' || @relPath
                       OR id LIKE '%:f:' || @relPathSlash
                       OR id LIKE '%:file:' || @relPath
                       OR id LIKE '%:file:' || @relPathSlash
                       OR id LIKE '%:sym:' || @relPathSlash
                       OR id LIKE '%:sym:' || @relPathColon
                       OR id LIKE '%:symbol:' || @relPathSlash
                       OR id LIKE '%:symbol:' || @relPathColon
                       OR id LIKE '%:lit:' || @relPathSlash
                       OR id LIKE '%:lit:' || @relPathColon
                       OR id LIKE '%:catch:' || @relPathSlash
                       OR id LIKE '%:catch:' || @relPathColon
                       OR id LIKE '%:p:' || @relPath || '%'
                       OR id LIKE '%:project:' || @relPath || '%'
                       OR id LIKE '%:git:' || @relPath
                       OR id LIKE '%:git:' || @relPathSlash
                       OR id LIKE '%:ep:' || @relPathSlash
                       OR id LIKE '%:suite:' || @relPathSlash
                      ))
                   OR id LIKE (@wsPrefix || 'dir:' || @normPath)
                   OR id LIKE (@wsPrefix || 'dir:' || @normPathSlash);

                -- Pass 2: Property matching for nodes that don't embed the relative path into their ID
                INSERT INTO temp_del_subpath_nodes(id, is_container)
                SELECT id, 1 FROM nodes
                WHERE kind IN ('File', 'Folder', 'Project', 'Endpoint', 'EntryPoint')
                  AND (
                      lower(replace(json_extract(properties, '$.path'), '\', '/')) = @normPath
                      OR lower(replace(json_extract(properties, '$.full_path'), '\', '/')) = @normPath
                      OR lower(replace(json_extract(properties, '$.file_path'), '\', '/')) = @normPath
                      OR lower(replace(json_extract(properties, '$.path'), '\', '/')) LIKE @normPathSlash
                      OR lower(replace(json_extract(properties, '$.full_path'), '\', '/')) LIKE @normPathSlash
                      OR lower(replace(json_extract(properties, '$.file_path'), '\', '/')) LIKE @normPathSlash
                      OR (@hasRel = 1 AND (
                          lower(replace(json_extract(properties, '$.path'), '\', '/')) = @relPath
                          OR lower(replace(json_extract(properties, '$.file_path'), '\', '/')) = @relPath
                          OR lower(json_extract(properties, '$.name')) = @relPath
                          OR lower(replace(json_extract(properties, '$.path'), '\', '/')) LIKE @relPathSlash
                          OR lower(replace(json_extract(properties, '$.file_path'), '\', '/')) LIKE @relPathSlash
                      ))
                  )
                ON CONFLICT(id) DO UPDATE SET is_container = 1;

                -- Pass 3: Recursive traversal for child nodes connected via CONTAINS
                WITH RECURSIVE del_rec(id) AS (
                    SELECT id FROM temp_del_subpath_nodes WHERE is_container = 1
                    UNION
                    SELECT e.to_id FROM del_rec d
                    JOIN edges e INDEXED BY idx_edges_from_kind_to ON e.from_id = d.id AND e.kind = 'CONTAINS'
                )
                INSERT OR IGNORE INTO temp_del_subpath_nodes(id, is_container)
                SELECT DISTINCT id, 0 FROM del_rec;
                """, parameters, transaction: tx);

            var deletedCount = await _conn.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM temp_del_subpath_nodes;", transaction: tx);

            if (deletedCount == 0)
            {
                await _conn.ExecuteAsync("DROP TABLE IF EXISTS temp_del_subpath_nodes;", transaction: tx);
                tx.Commit();
                return false;
            }

            var totalNodes = await _conn.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM nodes;", transaction: tx);

            if (deletedCount >= totalNodes - 1)
            {
                // Fast path: ALL (or almost all) nodes in the graph belong to this deleted subpath.
                // Truncate edges instantly instead of performing hundreds of thousands of individual edge deletes!
                await _conn.ExecuteAsync("""
                    DELETE FROM edges;
                    DELETE FROM nodes WHERE id IN (SELECT id FROM temp_del_subpath_nodes);
                    DROP TABLE IF EXISTS temp_del_subpath_nodes;

                    DELETE FROM file_registry
                    WHERE lower(replace(relative_path, '\', '/')) = @normPath
                       OR lower(replace(relative_path, '\', '/')) LIKE @normPathSlash
                       OR (@hasRel = 1 AND (
                           lower(replace(relative_path, '\', '/')) = @relPath
                           OR lower(replace(relative_path, '\', '/')) LIKE @relPathSlash
                       ));
                    """, parameters, transaction: tx);
            }
            else
            {
                await _conn.ExecuteAsync("""
                    DELETE FROM edges WHERE from_id IN (SELECT id FROM temp_del_subpath_nodes);
                    DELETE FROM edges WHERE to_id IN (SELECT id FROM temp_del_subpath_nodes);
                    DELETE FROM nodes WHERE id IN (SELECT id FROM temp_del_subpath_nodes);
                    DROP TABLE IF EXISTS temp_del_subpath_nodes;

                    DELETE FROM file_registry
                    WHERE lower(replace(relative_path, '\', '/')) = @normPath
                       OR lower(replace(relative_path, '\', '/')) LIKE @normPathSlash
                       OR (@hasRel = 1 AND (
                           lower(replace(relative_path, '\', '/')) = @relPath
                           OR lower(replace(relative_path, '\', '/')) LIKE @relPathSlash
                       ));
                    """, parameters, transaction: tx);
            }

            tx.Commit();
            Analysis.ArchitectureViewEngine.InvalidateCache();
            return true;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private async Task<string?> FindWorkspaceIdByIdOrPathAsync(string raw, string normalized)
    {
        const string sql = """
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
        return await _conn.QueryFirstOrDefaultAsync<string>(sql, new { raw, normalized });
    }

    private async Task DeleteWorkspaceHierarchyAsync(string wsId, int totalWorkspaces)
    {
        if (totalWorkspaces <= 1)
        {
            await WipeDatabaseFastAsync();
            return;
        }

        var wsGlob = wsId + ":*";

        using var tx = _conn.BeginTransaction();
        try
        {
            await _conn.ExecuteAsync("""
                CREATE TEMP TABLE IF NOT EXISTS temp_del_ws_nodes (id TEXT PRIMARY KEY);
                DELETE FROM temp_del_ws_nodes;

                WITH RECURSIVE del_nodes(id) AS (
                    SELECT id FROM nodes
                    WHERE id = @wsId OR id GLOB @wsGlob
                    UNION
                    SELECT e.to_id FROM del_nodes d
                    JOIN edges e INDEXED BY idx_edges_from_kind_to ON e.from_id = d.id AND e.kind = 'CONTAINS'
                )
                INSERT OR IGNORE INTO temp_del_ws_nodes(id)
                SELECT DISTINCT id FROM del_nodes;

                DELETE FROM edges WHERE from_id IN (SELECT id FROM temp_del_ws_nodes);
                DELETE FROM edges WHERE to_id IN (SELECT id FROM temp_del_ws_nodes);
                DELETE FROM nodes WHERE id IN (SELECT id FROM temp_del_ws_nodes);

                DROP TABLE IF EXISTS temp_del_ws_nodes;
                """, new { wsId, wsGlob }, transaction: tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        await _conn.ExecuteAsync("DELETE FROM file_registry;");
        Analysis.ArchitectureViewEngine.InvalidateCache();
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

    public async Task UploadNodesAsync(List<Node> nodes)
    {
        if (nodes.Count == 0) return;

        var sw = Stopwatch.StartNew();
        await _lock.WaitAsync();
        try
        {
            await using var tx = (SqliteTransaction)await _conn.BeginTransactionAsync();

            const int batchSize = 200;
            var distinctNodes = nodes.Count > 1 ? [.. nodes.DistinctBy(n => n.Id)] : nodes;

            var lastReport = Stopwatch.GetTimestamp();
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

                sb.Append(
                    " ON CONFLICT(id) DO UPDATE SET kind = excluded.kind, properties = excluded.properties;");
                cmd.CommandText = sb.ToString();
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
            const int batchSize = 200;
            var distinctRels = rels.Count > 1 ? [.. rels.DistinctBy(r => (r.From, r.To, r.Kind))] : rels;

            var lastReport = Stopwatch.GetTimestamp();
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

                    cmd.Parameters.AddWithValue($"@p{j}",
                        rel.Properties == null || rel.Properties.Count == 0
                            ? emptyPropsJson
                            : JsonSerializer.Serialize(rel.Properties));
                }

                sb.Append(" ON CONFLICT(from_id, to_id, kind) DO UPDATE SET properties = excluded.properties;");
                cmd.CommandText = sb.ToString();
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

    public async Task RunIncrementalVacuumAsync(int pages = 500, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await using var cmd = _conn.CreateCommand();

            cmd.CommandText = $"PRAGMA incremental_vacuum({pages});";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
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
