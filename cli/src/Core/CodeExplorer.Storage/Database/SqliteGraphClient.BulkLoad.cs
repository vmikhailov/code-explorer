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
                  PRAGMA wal_checkpoint(TRUNCATE);
                  """
                : """
                  PRAGMA synchronous = NORMAL;
                  PRAGMA wal_autocheckpoint = 1000;
                  PRAGMA cache_size = -64000;
                  PRAGMA wal_checkpoint(TRUNCATE);
                  """;
            await restoreCmd.ExecuteNonQueryAsync(CancellationToken.None);
        }
        catch (SqliteException)
        {
            // Best effort restore when connection is closing
        }
    }
}
