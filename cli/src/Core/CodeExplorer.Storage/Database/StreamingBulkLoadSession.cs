using System.Buffers;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CodeExplorer.Core.Database;

internal sealed class StreamingBulkLoadSession : IDisposable, IAsyncDisposable
{
    private const string ParamRowId = "@r";
    private const string ParamId = "@i";
    private const string ParamKind = "@k";
    private const string ParamProps = "@p";
    private const string ParamEdgeRowId = "@er";
    private const string ParamFrom = "@f";
    private const string ParamTo = "@t";
    private const string ParamDomains = "@domains";

    private const string InsertFreshNodeSql =
        "INSERT INTO nodes (rowid, id, kind, properties) VALUES (@r, @i, @k, jsonb(@p));";

    private const string UpdateNodeByRowIdSql =
        "UPDATE nodes SET kind = @k, properties = jsonb(@p) WHERE rowid = @r;";

    private const string InsertEmptyEdgeSql =
        "INSERT INTO edges (rowid, from_id, to_id, kind, properties) VALUES (@er, @f, @t, @k, jsonb('{}'));";

    private const string InsertPropEdgeSql =
        "INSERT INTO edges (rowid, from_id, to_id, kind, properties) VALUES (@er, @f, @t, @k, jsonb(@p));";

    private const string UpdateEdgePropsByRowIdSql =
        "UPDATE edges SET properties = jsonb(@p) WHERE rowid = @er;";

    private const string UpdateProjectApisByRowIdSql =
        "UPDATE nodes SET properties = jsonb_set(properties, '$.external_apis', jsonb(@domains)) WHERE rowid = @r;";

    private const string WipeFreshTablesSql =
        "DELETE FROM edges; DELETE FROM nodes;";

    private const string PreloadNodeRowIdsSql =
        "SELECT id, rowid FROM nodes;";

    private const string PreloadEdgeRowIdsSql =
        "SELECT from_id, to_id, kind, rowid FROM edges;";

    private readonly SqliteConnection _conn;
    private readonly SqliteCommand _insertNodeCmd;
    private readonly SqliteParameter _insNodeRowId;
    private readonly SqliteParameter _insNodeId;
    private readonly SqliteParameter _insNodeKind;
    private readonly SqliteParameter _insNodeProps;

    private readonly SqliteCommand _updateNodeCmd;
    private readonly SqliteParameter _updNodeRowId;
    private readonly SqliteParameter _updNodeKind;
    private readonly SqliteParameter _updNodeProps;

    private readonly SqliteCommand _insertEmptyEdgeCmd;
    private readonly SqliteParameter _insEmptyEdgeRowId;
    private readonly SqliteParameter _insEmptyFrom;
    private readonly SqliteParameter _insEmptyTo;
    private readonly SqliteParameter _insEmptyKind;

    private readonly SqliteCommand _insertPropEdgeCmd;
    private readonly SqliteParameter _insPropEdgeRowId;
    private readonly SqliteParameter _insPropFrom;
    private readonly SqliteParameter _insPropTo;
    private readonly SqliteParameter _insPropKind;
    private readonly SqliteParameter _insPropJson;

    private readonly SqliteCommand _updateEdgePropsCmd;
    private readonly SqliteParameter _updEdgeRowId;
    private readonly SqliteParameter _updEdgeJson;

    private readonly ArrayBufferWriter<byte> _nodeBuffer = new(1024);
    private readonly Utf8JsonWriter _nodeJsonWriter;
    private readonly ArrayBufferWriter<byte> _edgeBuffer = new(512);
    private readonly Utf8JsonWriter _edgeJsonWriter;

    private long _nextNodeRowId = 1;
    private long _nextEdgeRowId = 1;
    private bool _completed;
    private bool _disposed;

    public SqliteTransaction Transaction { get; }
    public bool SwitchedToMemoryJournal { get; }
    public Dictionary<string, long> NodeRowIds { get; } = new(4096, StringComparer.Ordinal);
    public Dictionary<(long FromRowId, long ToRowId, RelationshipKind Kind), long> StreamedEdgeRowIds { get; } = new(8192);
    public int StreamedNodeCount { get; private set; }
    public int StreamedEdgeCount { get; private set; }

    private StreamingBulkLoadSession(
        SqliteConnection conn,
        SqliteTransaction tx,
        bool switchedToMemoryJournal)
    {
        _conn = conn;
        Transaction = tx;
        SwitchedToMemoryJournal = switchedToMemoryJournal;
        _nodeJsonWriter = new Utf8JsonWriter(_nodeBuffer);
        _edgeJsonWriter = new Utf8JsonWriter(_edgeBuffer);

        _insertNodeCmd = CreatePreparedCommand(tx, InsertFreshNodeSql);
        _insNodeRowId = _insertNodeCmd.Parameters.Add(ParamRowId, SqliteType.Integer);
        _insNodeId = _insertNodeCmd.Parameters.Add(ParamId, SqliteType.Text);
        _insNodeKind = _insertNodeCmd.Parameters.Add(ParamKind, SqliteType.Integer);
        _insNodeProps = _insertNodeCmd.Parameters.Add(ParamProps, SqliteType.Text);
        _insertNodeCmd.Prepare();

        _updateNodeCmd = CreatePreparedCommand(tx, UpdateNodeByRowIdSql);
        _updNodeRowId = _updateNodeCmd.Parameters.Add(ParamRowId, SqliteType.Integer);
        _updNodeKind = _updateNodeCmd.Parameters.Add(ParamKind, SqliteType.Integer);
        _updNodeProps = _updateNodeCmd.Parameters.Add(ParamProps, SqliteType.Text);
        _updateNodeCmd.Prepare();

        _insertEmptyEdgeCmd = CreatePreparedCommand(tx, InsertEmptyEdgeSql);
        _insEmptyEdgeRowId = _insertEmptyEdgeCmd.Parameters.Add(ParamEdgeRowId, SqliteType.Integer);
        _insEmptyFrom = _insertEmptyEdgeCmd.Parameters.Add(ParamFrom, SqliteType.Integer);
        _insEmptyTo = _insertEmptyEdgeCmd.Parameters.Add(ParamTo, SqliteType.Integer);
        _insEmptyKind = _insertEmptyEdgeCmd.Parameters.Add(ParamKind, SqliteType.Integer);
        _insertEmptyEdgeCmd.Prepare();

        _insertPropEdgeCmd = CreatePreparedCommand(tx, InsertPropEdgeSql);
        _insPropEdgeRowId = _insertPropEdgeCmd.Parameters.Add(ParamEdgeRowId, SqliteType.Integer);
        _insPropFrom = _insertPropEdgeCmd.Parameters.Add(ParamFrom, SqliteType.Integer);
        _insPropTo = _insertPropEdgeCmd.Parameters.Add(ParamTo, SqliteType.Integer);
        _insPropKind = _insertPropEdgeCmd.Parameters.Add(ParamKind, SqliteType.Integer);
        _insPropJson = _insertPropEdgeCmd.Parameters.Add(ParamProps, SqliteType.Text);
        _insertPropEdgeCmd.Prepare();

        _updateEdgePropsCmd = CreatePreparedCommand(tx, UpdateEdgePropsByRowIdSql);
        _updEdgeRowId = _updateEdgePropsCmd.Parameters.Add(ParamEdgeRowId, SqliteType.Integer);
        _updEdgeJson = _updateEdgePropsCmd.Parameters.Add(ParamProps, SqliteType.Text);
        _updateEdgePropsCmd.Prepare();
    }

    public static async Task<StreamingBulkLoadSession> StartAsync(
        SqliteConnection conn,
        bool isFresh,
        bool switchedToMemoryJournal,
        CancellationToken cancellationToken)
    {
        var tx = (SqliteTransaction)await conn.BeginTransactionAsync(cancellationToken);
        var session = new StreamingBulkLoadSession(conn, tx, switchedToMemoryJournal);
        try
        {
            if (isFresh)
            {
                await using var wipeCmd = CreatePreparedCommand(tx, WipeFreshTablesSql);
                await wipeCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                await session.PreloadExistingRowIdsAsync(cancellationToken);
            }

            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    public static bool IsPostIndexStagedNodeKind(NodeKind kind) =>
        kind is NodeKind.Workspace
            or NodeKind.Project
            or NodeKind.ProjectSemantic
            or NodeKind.ProjectSyntax
            or NodeKind.Service
            or NodeKind.App
            or NodeKind.Worker
            or NodeKind.Library
            or NodeKind.Package
            or NodeKind.Database
            or NodeKind.DbSchema
            or NodeKind.DataSet
            or NodeKind.Table
            or NodeKind.Procedure
            or NodeKind.Topic
            or NodeKind.Endpoint
            or NodeKind.EntryPoint
            or NodeKind.ExternalService
            or NodeKind.CloudService;

    public void StreamOrUpdateNode(Node node)
    {
        _nodeBuffer.Clear();
        _nodeJsonWriter.Reset(_nodeBuffer);
        NodeFactory.WriteJsonProperties(node, _nodeJsonWriter);
        _nodeJsonWriter.Flush();
        var propsJson = Encoding.UTF8.GetString(_nodeBuffer.WrittenSpan);

        if (NodeRowIds.TryGetValue(node.Id, out var existingRowId))
        {
            _updNodeRowId.Value = existingRowId;
            _updNodeKind.Value = (int)node.NodeKind;
            _updNodeProps.Value = propsJson;
            _updateNodeCmd.ExecuteNonQuery();
            return;
        }

        var rowId = _nextNodeRowId++;
        NodeRowIds[node.Id] = rowId;
        _insNodeRowId.Value = rowId;
        _insNodeId.Value = node.Id;
        _insNodeKind.Value = (int)node.NodeKind;
        _insNodeProps.Value = propsJson;
        _insertNodeCmd.ExecuteNonQuery();
        StreamedNodeCount++;
    }

    public bool TryStreamImmediateEdge(Relationship rel)
    {
        if (rel.HasTelemetry) return false;
        if (!NodeRowIds.TryGetValue(rel.From, out var fromRowId) ||
            !NodeRowIds.TryGetValue(rel.To, out var toRowId))
        {
            return false;
        }

        var key = (fromRowId, toRowId, rel.Kind);
        if (StreamedEdgeRowIds.ContainsKey(key))
        {
            return true;
        }

        var edgeRowId = _nextEdgeRowId++;
        StreamedEdgeRowIds[key] = edgeRowId;
        _insEmptyEdgeRowId.Value = edgeRowId;
        _insEmptyFrom.Value = fromRowId;
        _insEmptyTo.Value = toRowId;
        _insEmptyKind.Value = (int)rel.Kind;
        _insertEmptyEdgeCmd.ExecuteNonQuery();
        StreamedEdgeCount++;
        return true;
    }

    public void FlushDeferredRelationships(
        Dictionary<(string From, string To, RelationshipKind Kind), Relationship> deferredRelationships)
    {
        foreach (var ((fromId, toId, kind), rel) in deferredRelationships)
        {
            if (!NodeRowIds.TryGetValue(fromId, out var fromRowId) ||
                !NodeRowIds.TryGetValue(toId, out var toRowId))
            {
                continue;
            }

            var key = (fromRowId, toRowId, kind);
            if (StreamedEdgeRowIds.TryGetValue(key, out var existingEdgeRowId))
            {
                if (rel.HasTelemetry)
                {
                    _updEdgeRowId.Value = existingEdgeRowId;
                    _updEdgeJson.Value = rel.SerializeJsonProperties(_edgeBuffer, _edgeJsonWriter);
                    _updateEdgePropsCmd.ExecuteNonQuery();
                }
                continue;
            }

            var edgeRowId = _nextEdgeRowId++;
            StreamedEdgeRowIds[key] = edgeRowId;
            if (!rel.HasTelemetry)
            {
                _insEmptyEdgeRowId.Value = edgeRowId;
                _insEmptyFrom.Value = fromRowId;
                _insEmptyTo.Value = toRowId;
                _insEmptyKind.Value = (int)kind;
                _insertEmptyEdgeCmd.ExecuteNonQuery();
            }
            else
            {
                _insPropEdgeRowId.Value = edgeRowId;
                _insPropFrom.Value = fromRowId;
                _insPropTo.Value = toRowId;
                _insPropKind.Value = (int)kind;
                _insPropJson.Value = rel.SerializeJsonProperties(_edgeBuffer, _edgeJsonWriter);
                _insertPropEdgeCmd.ExecuteNonQuery();
            }
            StreamedEdgeCount++;
        }
    }

    public void ApplyProjectExternalApis(Dictionary<string, List<string>> projectExternalApis)
    {
        if (projectExternalApis.Count == 0) return;

        using var updCmd = CreatePreparedCommand(Transaction, UpdateProjectApisByRowIdSql);
        var pDomains = updCmd.Parameters.Add(ParamDomains, SqliteType.Text);
        var pRowId = updCmd.Parameters.Add(ParamRowId, SqliteType.Integer);
        updCmd.Prepare();

        foreach (var (projId, domains) in projectExternalApis)
        {
            if (!NodeRowIds.TryGetValue(projId, out var rowId)) continue;
            pRowId.Value = rowId;
            pDomains.Value = JsonSerializer.Serialize(domains);
            updCmd.ExecuteNonQuery();
        }
    }

    public async Task DeletePostIndexEdgesForNonFreshAsync(CancellationToken cancellationToken)
    {
        var sql =
            $"DELETE FROM edges WHERE kind IN ({GraphKindCodec.RelKindsInSql(RelationshipKind.TransitivelyCalls, RelationshipKind.AttributedTo)});";
        await using var delCmd = CreatePreparedCommand(Transaction, sql);
        await delCmd.ExecuteNonQueryAsync(cancellationToken);

        var keysToRemove = StreamedEdgeRowIds.Keys
            .Where(static k => k.Kind is RelationshipKind.TransitivelyCalls or RelationshipKind.AttributedTo)
            .ToList();
        for (var i = 0; i < keysToRemove.Count; i++)
        {
            StreamedEdgeRowIds.Remove(keysToRemove[i]);
        }
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_completed) return;
        _completed = true;
        await Transaction.CommitAsync(cancellationToken);
    }

    private async Task PreloadExistingRowIdsAsync(CancellationToken cancellationToken)
    {
        long maxNodeRowId = 0;
        await using (var nodeMapCmd = CreatePreparedCommand(Transaction, PreloadNodeRowIdsSql))
        await using (var reader = await nodeMapCmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetString(0);
                var rowId = reader.GetInt64(1);
                NodeRowIds[id] = rowId;
                if (rowId > maxNodeRowId) maxNodeRowId = rowId;
            }
        }
        _nextNodeRowId = maxNodeRowId + 1;

        long maxEdgeRowId = 0;
        await using (var edgeMapCmd = CreatePreparedCommand(Transaction, PreloadEdgeRowIdsSql))
        await using (var reader = await edgeMapCmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var fromRowId = reader.GetInt64(0);
                var toRowId = reader.GetInt64(1);
                var kind = (RelationshipKind)reader.GetInt32(2);
                var edgeRowId = reader.GetInt64(3);
                StreamedEdgeRowIds[(fromRowId, toRowId, kind)] = edgeRowId;
                if (edgeRowId > maxEdgeRowId) maxEdgeRowId = edgeRowId;
            }
        }
        _nextEdgeRowId = maxEdgeRowId + 1;
    }

    private static SqliteCommand CreatePreparedCommand(SqliteTransaction tx, string sql)
    {
        var cmd = tx.Connection!.CreateCommand();
        cmd.CommandTimeout = 0;
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        return cmd;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _nodeJsonWriter.Dispose();
        _edgeJsonWriter.Dispose();
        _insertNodeCmd.Dispose();
        _updateNodeCmd.Dispose();
        _insertEmptyEdgeCmd.Dispose();
        _insertPropEdgeCmd.Dispose();
        _updateEdgePropsCmd.Dispose();

        if (!_completed)
        {
            try
            {
                Transaction.Rollback();
            }
            catch (SqliteException)
            {
                // Ignore rollback failure when connection is already closed
            }
        }

        Transaction.Dispose();
        NodeRowIds.Clear();
        StreamedEdgeRowIds.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _nodeJsonWriter.DisposeAsync();
        await _edgeJsonWriter.DisposeAsync();
        await _insertNodeCmd.DisposeAsync();
        await _updateNodeCmd.DisposeAsync();
        await _insertEmptyEdgeCmd.DisposeAsync();
        await _insertPropEdgeCmd.DisposeAsync();
        await _updateEdgePropsCmd.DisposeAsync();

        if (!_completed)
        {
            try
            {
                await Transaction.RollbackAsync();
            }
            catch (SqliteException)
            {
                // Ignore rollback failure when connection is already closed
            }
        }

        await Transaction.DisposeAsync();
        NodeRowIds.Clear();
        StreamedEdgeRowIds.Clear();
    }
}
