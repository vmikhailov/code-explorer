using CodeExplorer.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Options;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Commands;

public static class IngestCommandHandler
{
    public static async Task<int> HandleAsync(IngestOptions opts)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddShortConsole();
        });

        var logger = loggerFactory.CreateLogger(typeof(IngestCommandHandler));
        var indexerLogger = loggerFactory.CreateLogger<WorkspaceIndexer>();
        var clientLogger = loggerFactory.CreateLogger<SqliteGraphClient>();

        try
        {
            logger.LogInformation("Scanning and parsing directory: {Directory}...", opts.Dir);
            await using var client = new SqliteGraphClient(opts.DbPath, clientLogger);

            if (opts.ClearAll)
            {
                logger.LogInformation("Performing a global database clear...");
                await client.ClearDatabaseAsync();
            }

            var indexer = new WorkspaceIndexer(client, indexerLogger);

            var shouldClear = (opts.Clear && !opts.ClearAll) || client.IsSchemaOutdated;
            if (client.IsSchemaOutdated)
            {
                logger.LogWarning("Database schema version is outdated (v{Version} < v{Current}). Automatically performing clean rescan...",
                    client.SchemaVersion, SqliteGraphClient.CurrentSchemaVersion);
            }

            var (nodesCount, relsCount, nodesByKind) =
                await indexer.IndexAsync(opts.Dir, opts.Dir, shouldClear);

            logger.LogInformation("Parsed and uploaded {NodesCount} nodes and {RelationshipsCount} relationships successfully!", nodesCount, relsCount);
            logger.LogInformation("Nodes breakdown by kind:");

            foreach (var (kind, count) in nodesByKind)
            {
                logger.LogInformation("  - {Kind}: {Count}", kind, count);
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ingestion Error: {Message}", ex.Message);
            return 1;
        }
    }
}
