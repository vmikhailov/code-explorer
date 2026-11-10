using CodeExplorer.Cypher.Common;
using Dapper;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Core.Database;

public partial class SqliteGraphClient
{
    private void ThrowIfSchemaOutdated()
    {
        if (IsSchemaOutdated)
        {
            throw new InvalidOperationException(
                $"Database schema version is outdated (v{SchemaVersion} < v{CurrentSchemaVersion}). Please convert the database schema or run 'ce scan'.");
        }
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
                        ver = 1;
                    }
                }
                catch
                {
                    // Ignore
                }
            }

            if (ver == 0)
            {
                CreateIndices();
                cmd.CommandText = $"PRAGMA user_version = {CurrentSchemaVersion};";
                cmd.ExecuteNonQuery();
                SchemaVersion = CurrentSchemaVersion;
                return;
            }
        }

        if (ver > CurrentSchemaVersion)
        {
            SchemaVersion = ver;
            _logger.LogWarning("Database was created with a newer schema version (v{Ver} > v{Current}). Consider upgrading CodeExplorer CLI.", ver, CurrentSchemaVersion);
        }
        else
        {
            SchemaVersion = ver;
        }
    }

    public async Task<bool> UpgradeSchemaAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!IsSchemaOutdated)
            {
                return true;
            }

            var fromVersion = SchemaVersion;
            var upgraded = await TryRunSchemaMigratorAsync(fromVersion, CurrentSchemaVersion, cancellationToken);
            if (upgraded)
            {
                WasMigratedOnOpen = true;
                MigratedFromVersion = fromVersion;
                SchemaVersion = CurrentSchemaVersion;
                return true;
            }

            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public bool TryRunSchemaMigrator(int fromVersion, int toVersion) =>
        TryRunSchemaMigratorAsync(fromVersion, toVersion).GetAwaiter().GetResult();

    public async Task<bool> TryRunSchemaMigratorAsync(int fromVersion, int toVersion, CancellationToken cancellationToken = default)
    {
        if (toVersion > CurrentSchemaVersion)
        {
            // Unknown future schema version: force full rebuild via IsSchemaOutdated = true
            return false;
        }

        try
        {
            _logger.LogInformation("Upgrading database schema from v{From} to v{To}...", fromVersion, toVersion);

            await using var migrateCmd = _conn.CreateCommand();
            migrateCmd.CommandTimeout = 0;
            var sb = new System.Text.StringBuilder();

            if (fromVersion < 3)
            {
                sb.AppendLine("""
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
                    """);
            }

            if (fromVersion < 4)
            {
                sb.AppendLine("""
                    UPDATE nodes SET properties = jsonb(properties) WHERE typeof(properties) = 'text';
                    UPDATE edges SET properties = jsonb(properties) WHERE typeof(properties) = 'text';
                    REINDEX;
                    """);
            }

            if (fromVersion < 5)
            {
                sb.AppendLine("""
                    DROP INDEX IF EXISTS idx_nodes_kind;
                    DROP INDEX IF EXISTS idx_nodes_kind_path;
                    DROP INDEX IF EXISTS idx_nodes_kind_file_path;
                    DROP INDEX IF EXISTS idx_nodes_kind_symbol;
                    DROP INDEX IF EXISTS idx_nodes_kind_lower_path;
                    DROP INDEX IF EXISTS idx_edges_from_kind_to;
                    DROP INDEX IF EXISTS idx_edges_unique;
                    UPDATE nodes SET properties = jsonb_remove(properties, '$.id') WHERE json_type(properties, '$.id') IS NOT NULL;
                    """);
            }

            if (fromVersion < 6)
            {
                sb.AppendLine("""
                    DROP INDEX IF EXISTS idx_nodes_kind;
                    DROP INDEX IF EXISTS idx_nodes_kind_path;
                    DROP INDEX IF EXISTS idx_nodes_kind_file_path;
                    DROP INDEX IF EXISTS idx_nodes_kind_name;
                    DROP INDEX IF EXISTS idx_nodes_name_kind;
                    DROP INDEX IF EXISTS idx_nodes_kind_symbol;
                    DROP INDEX IF EXISTS idx_nodes_kind_lower_path;
                    DROP INDEX IF EXISTS idx_edges_from_kind_to;
                    DROP INDEX IF EXISTS idx_edges_to_kind_from;
                    DROP INDEX IF EXISTS idx_edges_kind_from_to;
                    DROP INDEX IF EXISTS idx_edges_unique;
                    CREATE TABLE nodes_v6 (
                        id TEXT PRIMARY KEY,
                        kind INTEGER NOT NULL,
                        properties JSONB NOT NULL
                    );
                    INSERT INTO nodes_v6 (rowid, id, kind, properties)
                    SELECT rowid, id, CASE WHEN typeof(kind) = 'integer' THEN kind ELSE ce_node_kind_id(kind) END, properties
                    FROM nodes;
                    CREATE TABLE edges_v6 (
                        from_id INTEGER NOT NULL,
                        to_id INTEGER NOT NULL,
                        kind INTEGER NOT NULL,
                        properties JSONB NOT NULL
                    );
                    INSERT OR IGNORE INTO edges_v6 (from_id, to_id, kind, properties)
                    SELECT
                        CASE WHEN typeof(e.from_id) = 'integer' THEN e.from_id ELSE fn.rowid END,
                        CASE WHEN typeof(e.to_id) = 'integer' THEN e.to_id ELSE tn.rowid END,
                        CASE WHEN typeof(e.kind) = 'integer' THEN e.kind ELSE ce_rel_kind_id(e.kind) END,
                        e.properties
                    FROM edges e
                    LEFT JOIN nodes_v6 fn ON typeof(e.from_id) != 'integer' AND fn.id = e.from_id
                    LEFT JOIN nodes_v6 tn ON typeof(e.to_id) != 'integer' AND tn.id = e.to_id
                    WHERE (typeof(e.from_id) = 'integer' OR fn.rowid IS NOT NULL)
                      AND (typeof(e.to_id) = 'integer' OR tn.rowid IS NOT NULL);
                    DROP TABLE edges;
                    DROP TABLE nodes;
                    ALTER TABLE nodes_v6 RENAME TO nodes;
                    ALTER TABLE edges_v6 RENAME TO edges;
                    """);
            }

            sb.AppendLine($"PRAGMA user_version = {toVersion};");
            migrateCmd.CommandText = sb.ToString();
            await migrateCmd.ExecuteNonQueryAsync(cancellationToken);

            if (fromVersion < 6)
            {
                CreateTablesAndAuxiliaryIndices();
                await using var colCmd = _conn.CreateCommand();
                colCmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info('file_registry') WHERE name = 'size_bytes';";
                if (Convert.ToInt32(await colCmd.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    colCmd.CommandText = "ALTER TABLE file_registry ADD COLUMN size_bytes INTEGER NOT NULL DEFAULT -1;";
                    await colCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                CreateGraphSecondaryIndices();
            }

            _logger.LogInformation("Database schema successfully upgraded to v{To}.", toVersion);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Schema migration from v{From} to v{To} encountered an error (will trigger full rebuild): {Message}",
                fromVersion, toVersion, ex.Message);
            return false;
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
        CreateTablesAndAuxiliaryIndices();
        CreateGraphSecondaryIndices();
    }

    private void CreateTablesAndAuxiliaryIndices()
    {
        using var cmd = _conn.CreateCommand();

        cmd.CommandText = """
                          CREATE TABLE IF NOT EXISTS nodes (
                              id TEXT PRIMARY KEY,
                              kind INTEGER NOT NULL,
                              properties JSONB NOT NULL
                          );

                          CREATE TABLE IF NOT EXISTS edges (
                              from_id INTEGER NOT NULL,
                              to_id INTEGER NOT NULL,
                              kind INTEGER NOT NULL,
                              properties JSONB NOT NULL
                          );

                          CREATE TABLE IF NOT EXISTS file_registry (
                              relative_path TEXT PRIMARY KEY,
                              content_hash TEXT NOT NULL,
                              last_modified_utc TEXT NOT NULL,
                              size_bytes INTEGER NOT NULL DEFAULT -1,
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
                          """;
        cmd.ExecuteNonQuery();
    }

    private void CreateGraphSecondaryIndices()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandTimeout = 0;
        cmd.CommandText = """
                          CREATE INDEX IF NOT EXISTS idx_nodes_kind ON nodes(kind);
                          CREATE INDEX IF NOT EXISTS idx_nodes_kind_path ON nodes(kind, json_extract(properties, '$.path')) WHERE json_extract(properties, '$.path') IS NOT NULL;
                          CREATE INDEX IF NOT EXISTS idx_nodes_kind_file_path ON nodes(kind, json_extract(properties, '$.file_path')) WHERE json_extract(properties, '$.file_path') IS NOT NULL;
                          CREATE INDEX IF NOT EXISTS idx_nodes_kind_name ON nodes(kind, json_extract(properties, '$.name')) WHERE json_extract(properties, '$.name') IS NOT NULL;
                          CREATE INDEX IF NOT EXISTS idx_nodes_name_kind ON nodes(json_extract(properties, '$.name'), kind) WHERE json_extract(properties, '$.name') IS NOT NULL;
                          CREATE INDEX IF NOT EXISTS idx_nodes_kind_symbol ON nodes(kind, json_extract(properties, '$.symbol')) WHERE json_extract(properties, '$.symbol') IS NOT NULL;
                          CREATE INDEX IF NOT EXISTS idx_nodes_kind_lower_path ON nodes(kind, lower(json_extract(properties, '$.path'))) WHERE lower(json_extract(properties, '$.path')) IS NOT NULL;

                          CREATE UNIQUE INDEX IF NOT EXISTS idx_edges_from_kind_to ON edges(from_id, kind, to_id);
                          CREATE INDEX IF NOT EXISTS idx_edges_to_kind_from ON edges(to_id, kind, from_id);
                          CREATE INDEX IF NOT EXISTS idx_edges_kind_from_to ON edges(kind, from_id, to_id);
                          """;
        cmd.ExecuteNonQuery();
    }

    private void DropGraphSecondaryIndices()
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandTimeout = 0;
        cmd.CommandText = """
                          DROP INDEX IF EXISTS idx_nodes_kind;
                          DROP INDEX IF EXISTS idx_nodes_kind_path;
                          DROP INDEX IF EXISTS idx_nodes_kind_file_path;
                          DROP INDEX IF EXISTS idx_nodes_kind_name;
                          DROP INDEX IF EXISTS idx_nodes_name_kind;
                          DROP INDEX IF EXISTS idx_nodes_kind_symbol;
                          DROP INDEX IF EXISTS idx_nodes_kind_lower_path;

                          DROP INDEX IF EXISTS idx_edges_from_kind_to;
                          DROP INDEX IF EXISTS idx_edges_to_kind_from;
                          DROP INDEX IF EXISTS idx_edges_kind_from_to;
                          DROP INDEX IF EXISTS idx_edges_unique;
                          """;
        cmd.ExecuteNonQuery();
    }

    public async Task CreateIndicesAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_isBulkLoading)
            {
                await CommitBulkLoadUnlockedAsync();
            }
            CreateIndices();
            await FlushOrPersistTaxonomyUnlockedAsync();
            await using var cmd = _conn.CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(PASSIVE); PRAGMA optimize;";
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
            await WipeDatabaseFastAsync();
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
            if (IsSchemaOutdated)
            {
                await WipeDatabaseFastAsync();
                return true;
            }
            if (_isBulkLoading)
            {
                await CommitBulkLoadUnlockedAsync();
            }
            await InvalidateTaxonomySnapshotUnlockedAsync();
            var wsKindId = GraphKindCodec.EncodeNodeKind(NodeKind.Workspace);
            var projKindId = GraphKindCodec.EncodeNodeKind(NodeKind.Project);
            var totalWorkspaces = await _conn.ExecuteScalarAsync<int>($"SELECT count(DISTINCT id) FROM nodes WHERE kind = {wsKindId};");

            var wsId = await FindWorkspaceIdByIdOrPathAsync(raw, normalized);
            if (wsId != null)
            {
                await DeleteWorkspaceHierarchyAsync(wsId, totalWorkspaces);
                return true;
            }

            // Retrieve all workspaces to find if normalized matches or is a subpath of any workspace
            var workspaces = (await _conn.QueryAsync<(string Id, string Path)>(
                $"SELECT id, json_extract(properties, '$.path') AS Path FROM nodes WHERE kind = {wsKindId};")).AsList();

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

                if (normalized.StartWithIC(normPath + "/"))
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
                        var hasProjectsOutside = await _conn.ExecuteScalarAsync<int>($"""
                            SELECT count(*) FROM nodes
                            WHERE kind = {projKindId}
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
        ResetBulkLoadStateUnlocked();
        if (IsSchemaOutdated)
        {
            await _conn.ExecuteAsync($"""
                DROP TABLE IF EXISTS edges;
                DROP TABLE IF EXISTS nodes;
                DROP TABLE IF EXISTS file_registry;
                DROP TABLE IF EXISTS metadata;
                PRAGMA user_version = {CurrentSchemaVersion};
                """);
            CreateIndices();
            SchemaVersion = CurrentSchemaVersion;
            ResetTaxonomyInMemory();
            WorkspaceIndexer.OnIndexCompletedHook?.Invoke();
            return;
        }

        using var tx = _conn.BeginTransaction();
        try
        {
            await _conn.ExecuteAsync("""
                DELETE FROM edges;
                DELETE FROM nodes;
                DELETE FROM file_registry;
                DELETE FROM metadata;
                """, transaction: tx);
            tx.Commit();
            ResetTaxonomyInMemory();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        WorkspaceIndexer.OnIndexCompletedHook?.Invoke();
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
            var containerKindsSql = GraphKindCodec.NodeKindsInSql(
                NodeKind.File, NodeKind.Folder, NodeKind.Project,
                NodeKind.Endpoint, NodeKind.EntryPoint, NodeKind.ScanIssue);
            var relContainsId = GraphKindCodec.EncodeRelKind(RelationshipKind.Contains);
            await _conn.ExecuteAsync($"""
                CREATE TEMP TABLE IF NOT EXISTS temp_del_subpath_nodes (id INTEGER PRIMARY KEY, is_container INTEGER DEFAULT 0);
                DELETE FROM temp_del_subpath_nodes;

                -- Pass 1: Deterministic ID prefix matching. If wsPrefix is known, uses primary key index on nodes.id!
                INSERT OR IGNORE INTO temp_del_subpath_nodes(id, is_container)
                SELECT rowid,
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
                       OR id LIKE @wsPrefix || 'issue:' || @relPathSlash
                       OR id LIKE @wsPrefix || 'issue:' || @relPathColon
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
                       OR id LIKE '%:issue:' || @relPathSlash
                       OR id LIKE '%:issue:' || @relPathColon
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
                SELECT rowid, 1 FROM nodes
                WHERE kind IN ({containerKindsSql})
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
                    JOIN edges e INDEXED BY idx_edges_from_kind_to ON e.from_id = d.id AND e.kind = {relContainsId}
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
                    DELETE FROM nodes WHERE rowid IN (SELECT id FROM temp_del_subpath_nodes);
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
                    DELETE FROM nodes WHERE rowid IN (SELECT id FROM temp_del_subpath_nodes);
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
            WorkspaceIndexer.OnIndexCompletedHook?.Invoke();
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
        var sql = $"""
            SELECT id FROM nodes
            WHERE kind = {GraphKindCodec.EncodeNodeKind(NodeKind.Workspace)}
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
        var relContainsId = GraphKindCodec.EncodeRelKind(RelationshipKind.Contains);

        using var tx = _conn.BeginTransaction();
        try
        {
            await _conn.ExecuteAsync($"""
                CREATE TEMP TABLE IF NOT EXISTS temp_del_ws_nodes (id INTEGER PRIMARY KEY);
                DELETE FROM temp_del_ws_nodes;

                WITH RECURSIVE del_nodes(id) AS (
                    SELECT rowid FROM nodes
                    WHERE id = @wsId OR id GLOB @wsGlob
                    UNION
                    SELECT e.to_id FROM del_nodes d
                    JOIN edges e INDEXED BY idx_edges_from_kind_to ON e.from_id = d.id AND e.kind = {relContainsId}
                )
                INSERT OR IGNORE INTO temp_del_ws_nodes(id)
                SELECT DISTINCT id FROM del_nodes;

                DELETE FROM edges WHERE from_id IN (SELECT id FROM temp_del_ws_nodes);
                DELETE FROM edges WHERE to_id IN (SELECT id FROM temp_del_ws_nodes);
                DELETE FROM nodes WHERE rowid IN (SELECT id FROM temp_del_ws_nodes);

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
        WorkspaceIndexer.OnIndexCompletedHook?.Invoke();
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
}
