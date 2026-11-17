using System.Buffers;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CodeExplorer.Core.Database;

public partial class SqliteGraphClient
{
    private async Task<bool> ConfigureBulkLoadPragmasUnlockedAsync(bool isFresh, CancellationToken cancellationToken)
    {
        _ = isFresh;
        await using var pragmaCmd = _conn.CreateCommand();
        pragmaCmd.CommandText = """
                                PRAGMA synchronous = OFF;
                                PRAGMA wal_autocheckpoint = 0;
                                PRAGMA temp_store = MEMORY;
                                PRAGMA cache_size = -256000;
                                """;
        await pragmaCmd.ExecuteNonQueryAsync(cancellationToken);
        return false;
    }

    private async Task RestorePostBulkLoadPragmasUnlockedAsync(bool switchedToMemoryJournal)
    {
        try
        {
            await using var restoreCmd = _conn.CreateCommand();
            restoreCmd.CommandText = switchedToMemoryJournal
                ? """
                  PRAGMA journal_mode = WAL;
                  PRAGMA synchronous = NORMAL;
                  PRAGMA wal_autocheckpoint = 1000;
                  PRAGMA cache_size = -64000;
                  """
                : """
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

    private void BulkInsertRelationshipsUnlocked(
        SqliteTransaction tx,
        bool isFresh,
        Dictionary<string, long> nodeRowIds)
    {
        const string paramF = "@f";
        const string paramT = "@t";
        const string paramK = "@k";
        const string paramP = "@p";

        using var emptyRelCmd = _conn.CreateCommand();
        emptyRelCmd.CommandTimeout = 0;
        emptyRelCmd.Transaction = tx;
        emptyRelCmd.CommandText = isFresh
            ? "INSERT INTO edges (from_id, to_id, kind, properties) VALUES (@f, @t, @k, jsonb('{}'));"
            : "INSERT INTO edges (from_id, to_id, kind, properties) VALUES (@f, @t, @k, jsonb('{}')) ON CONFLICT(from_id, to_id, kind) DO UPDATE SET properties = excluded.properties;";
        var pEmptyFrom = emptyRelCmd.Parameters.Add(paramF, SqliteType.Integer);
        var pEmptyTo = emptyRelCmd.Parameters.Add(paramT, SqliteType.Integer);
        var pEmptyKind = emptyRelCmd.Parameters.Add(paramK, SqliteType.Integer);
        emptyRelCmd.Prepare();

        using var propRelCmd = _conn.CreateCommand();
        propRelCmd.CommandTimeout = 0;
        propRelCmd.Transaction = tx;
        propRelCmd.CommandText = isFresh
            ? "INSERT INTO edges (from_id, to_id, kind, properties) VALUES (@f, @t, @k, jsonb(@p));"
            : "INSERT INTO edges (from_id, to_id, kind, properties) VALUES (@f, @t, @k, jsonb(@p)) ON CONFLICT(from_id, to_id, kind) DO UPDATE SET properties = excluded.properties;";
        var pPropFrom = propRelCmd.Parameters.Add(paramF, SqliteType.Integer);
        var pPropTo = propRelCmd.Parameters.Add(paramT, SqliteType.Integer);
        var pPropKind = propRelCmd.Parameters.Add(paramK, SqliteType.Integer);
        var pPropJson = propRelCmd.Parameters.Add(paramP, SqliteType.Text);
        propRelCmd.Prepare();

        var bufferWriter = new ArrayBufferWriter<byte>(512);
        using var jsonWriter = new Utf8JsonWriter(bufferWriter);

        foreach (var ((fromId, toId, kind), rel) in _bulkRelationships)
        {
            if (!nodeRowIds.TryGetValue(fromId, out var fromRowId) ||
                !nodeRowIds.TryGetValue(toId, out var toRowId))
            {
                continue;
            }

            if (!rel.HasTelemetry)
            {
                pEmptyFrom.Value = fromRowId;
                pEmptyTo.Value = toRowId;
                pEmptyKind.Value = (int)kind;
                emptyRelCmd.ExecuteNonQuery();
            }
            else
            {
                pPropFrom.Value = fromRowId;
                pPropTo.Value = toRowId;
                pPropKind.Value = (int)kind;
                pPropJson.Value = rel.SerializeJsonProperties(bufferWriter, jsonWriter);
                propRelCmd.ExecuteNonQuery();
            }
        }
    }
}
