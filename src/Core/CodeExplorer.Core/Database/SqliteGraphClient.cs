using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Database;

public class SqliteGraphClient : IDatabaseClient
{
    private readonly string _connectionString;
    private readonly string _dbFilePath;

    public bool IsCypherSupported => false;

    public SqliteGraphClient(string dbFilePath)
    {
        _dbFilePath = Path.GetFullPath(dbFilePath);
        var dir = Path.GetDirectoryName(_dbFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _connectionString = $"Data Source={_dbFilePath};Cache=Shared;";
    }

    private SqliteConnection CreateConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    public async Task CreateIndicesAsync()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        // Create tables
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS workspaces (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                path TEXT UNIQUE NOT NULL,
                name TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS nodes (
                urn TEXT PRIMARY KEY,
                kind TEXT NOT NULL,
                workspace_id INTEGER NOT NULL,
                name TEXT NOT NULL,
                properties TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS edges (
                from_urn TEXT NOT NULL,
                to_urn TEXT NOT NULL,
                kind TEXT NOT NULL,
                properties TEXT NOT NULL,
                PRIMARY KEY (from_urn, to_urn, kind)
            );

            CREATE INDEX IF NOT EXISTS idx_nodes_workspace ON nodes(workspace_id);
            CREATE INDEX IF NOT EXISTS idx_nodes_kind ON nodes(kind);
            CREATE INDEX IF NOT EXISTS idx_nodes_name ON nodes(name);
            CREATE INDEX IF NOT EXISTS idx_edges_from ON edges(from_urn);
            CREATE INDEX IF NOT EXISTS idx_edges_to ON edges(to_urn);
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearDatabaseAsync()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM edges; DELETE FROM nodes; DELETE FROM workspaces;";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearWorkspaceAsync(string workspacePath)
    {
        var wsId = await GetWorkspaceIdInternalAsync(workspacePath);
        if (wsId == null) return;

        using var conn = CreateConnection();
        using var transaction = conn.BeginTransaction();
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;

            // Delete edges where either side belongs to the workspace nodes
            cmd.CommandText = @"
                DELETE FROM edges 
                WHERE from_urn IN (SELECT urn FROM nodes WHERE workspace_id = $wsId)
                   OR to_urn IN (SELECT urn FROM nodes WHERE workspace_id = $wsId)";
            cmd.Parameters.AddWithValue("$wsId", wsId.Value);
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = "DELETE FROM nodes WHERE workspace_id = $wsId";
            await cmd.ExecuteNonQueryAsync();

            cmd.CommandText = "DELETE FROM workspaces WHERE id = $wsId";
            await cmd.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath)
    {
        var wsId = await GetWorkspaceIdInternalAsync(workspacePath);
        if (wsId != null)
        {
            return wsId.Value.ToString();
        }

        var normalizedPath = workspacePath.Replace('\\', '/').TrimEnd('/');
        var folderName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrEmpty(folderName)) folderName = normalizedPath;

        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO workspaces (path, name) VALUES ($path, $name); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$path", normalizedPath);
        cmd.Parameters.AddWithValue("$name", folderName);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? "1";
    }

    private async Task<long?> GetWorkspaceIdInternalAsync(string workspacePath)
    {
        var normalizedPath = workspacePath.Replace('\\', '/').TrimEnd('/');
        var normalizedAlt = normalizedPath.Contains('/') ? normalizedPath.Replace('/', '\\') : normalizedPath.Replace('\\', '/');

        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id FROM workspaces WHERE lower(path) = lower($path) OR lower(path) = lower($altPath)";
        cmd.Parameters.AddWithValue("$path", normalizedPath);
        cmd.Parameters.AddWithValue("$altPath", normalizedAlt);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return reader.GetInt64(0);
        }
        return null;
    }

    public async Task SaveEmptyWorkspaceNodeAsync(string id, string path)
    {
        // Handled as part of GetOrCreateWorkspaceIdAsync, but here for compatibility.
        // We will insert/update the workspace node in the nodes table.
        var node = new Node(
            id,
            OntologyConstants.NodeLabels.Workspace,
            new Dictionary<string, object>
            {
                ["id"] = id,
                ["path"] = path.Replace('\\', '/'),
                ["name"] = Path.GetFileName(path) ?? path
            }
        );
        await UploadNodesAsync(new List<Node> { node });
    }

    public async Task UploadNodesAsync(List<Node> nodes)
    {
        if (nodes.Count == 0) return;

        using var conn = CreateConnection();
        using var transaction = conn.BeginTransaction();
        try
        {
            foreach (var node in nodes)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO nodes (urn, kind, workspace_id, name, properties) 
                    VALUES ($urn, $kind, $wsId, $name, $properties)
                    ON CONFLICT(urn) DO UPDATE SET 
                        kind = excluded.kind,
                        workspace_id = excluded.workspace_id,
                        name = excluded.name,
                        properties = excluded.properties";

                var wsIdStr = node.Id.Split(':')[0];
                int.TryParse(wsIdStr, out var wsId);

                string nameStr = "";
                if (node.Properties.TryGetValue("name", out var nObj) && nObj != null)
                {
                    nameStr = nObj.ToString() ?? "";
                }

                cmd.Parameters.AddWithValue("$urn", node.Id);
                cmd.Parameters.AddWithValue("$kind", node.Kind);
                cmd.Parameters.AddWithValue("$wsId", wsId);
                cmd.Parameters.AddWithValue("$name", nameStr);
                cmd.Parameters.AddWithValue("$properties", JsonSerializer.Serialize(node.Properties));

                await cmd.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task UploadRelationshipsAsync(List<Relationship> rels)
    {
        if (rels.Count == 0) return;

        using var conn = CreateConnection();
        using var transaction = conn.BeginTransaction();
        try
        {
            foreach (var rel in rels)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO edges (from_urn, to_urn, kind, properties) 
                    VALUES ($from, $to, $kind, $properties)
                    ON CONFLICT(from_urn, to_urn, kind) DO UPDATE SET 
                        properties = excluded.properties";

                cmd.Parameters.AddWithValue("$from", rel.From);
                cmd.Parameters.AddWithValue("$to", rel.To);
                cmd.Parameters.AddWithValue("$kind", rel.Kind);
                cmd.Parameters.AddWithValue("$properties", JsonSerializer.Serialize(rel.Properties));

                await cmd.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public Task<string> ExecuteQueryAsync(string query, object? parameters = null)
    {
        throw new NotSupportedException("Custom Cypher queries are not supported on the SQLite backend.");
    }

    public Task ExecuteWriteAsync(string query, object? parameters = null)
    {
        throw new NotSupportedException("Custom Cypher queries are not supported on the SQLite backend.");
    }

    // SQLite retrieval methods for the in-memory graph
    public async Task<List<Node>> FetchAllWorkspaceNodesAsync(int workspaceId)
    {
        var result = new List<Node>();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT urn, kind, properties FROM nodes WHERE workspace_id = $wsId";
        cmd.Parameters.AddWithValue("$wsId", workspaceId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var urn = reader.GetString(0);
            var kind = reader.GetString(1);
            var propsJson = reader.GetString(2);
            var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsJson) ?? [];

            result.Add(new Node(urn, kind, props));
        }
        return result;
    }

    public async Task<List<Relationship>> FetchAllWorkspaceRelationshipsAsync(int workspaceId)
    {
        var result = new List<Relationship>();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT from_urn, to_urn, kind, properties FROM edges 
            WHERE from_urn IN (SELECT urn FROM nodes WHERE workspace_id = $wsId)
               OR to_urn IN (SELECT urn FROM nodes WHERE workspace_id = $wsId)";
        cmd.Parameters.AddWithValue("$wsId", workspaceId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var from = reader.GetString(0);
            var to = reader.GetString(1);
            var kind = reader.GetString(2);
            var propsJson = reader.GetString(3);
            var props = JsonSerializer.Deserialize<Dictionary<string, object>>(propsJson) ?? [];

            result.Add(new Relationship(from, to, kind, props));
        }
        return result;
    }

    public async Task<List<(string Id, string Path, string Name)>> GetAllWorkspacesAsync()
    {
        var list = new List<(string, string, string)>();
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, path, name FROM workspaces";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add((reader.GetInt64(0).ToString(), reader.GetString(1), reader.GetString(2)));
        }
        return list;
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        await Task.CompletedTask;
    }
}
