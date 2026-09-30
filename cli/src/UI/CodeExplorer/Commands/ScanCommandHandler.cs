using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Options;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Commands;

public static class ScanCommandHandler
{
    public static async Task<int> HandleAsync(ScanOptions opts)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddShortConsole();
        });

        var logger = loggerFactory.CreateLogger(typeof(ScanCommandHandler));
        var indexerLogger = loggerFactory.CreateLogger<WorkspaceIndexer>();
        var clientLogger = loggerFactory.CreateLogger<SqliteGraphClient>();

        try
        {
            var targetPath = Path.GetFullPath(opts.Path ?? Directory.GetCurrentDirectory());
            var ws = WorkspaceLocator.FindOrThrow(targetPath);

            logger.LogInformation("Workspace: {WorkspaceRoot} ({DbPath})", ws.RootDirectory, ws.DbPath);
            logger.LogInformation("Scanning:  {TargetPath}...", targetPath);

            await using var client = new SqliteGraphClient(ws.DbPath, clientLogger);

            var shouldClear = opts.Clear || client.IsSchemaOutdated;
            if (client.IsSchemaOutdated)
            {
                logger.LogWarning("Database schema version is outdated (v{Version} < v{Current}). Automatically performing clean rescan...",
                    client.SchemaVersion, SqliteGraphClient.CurrentSchemaVersion);
            }

            if (shouldClear)
            {
                logger.LogInformation("Clearing existing data for {TargetPath}...", targetPath);
                await client.ClearWorkspaceAsync(targetPath);
            }

            var indexer = new WorkspaceIndexer(client, indexerLogger);

            var performedFullIndex = false;
            if (!shouldClear)
            {
                var fileReg = await client.LoadFileRegistryAsync();
                if (fileReg.Count > 0)
                {
                    logger.LogInformation("Running fast incremental scan check...");
                    var changed = await indexer.IndexIncrementalAsync(targetPath, ws.RootDirectory, enableIntentAnalysis: opts.Intent);
                    if (!changed)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n✓ Workspace is up to date (0 changes detected). Use --clear to force a full re-index.");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n✓ Incremental indexing completed successfully!");
                        Console.ResetColor();
                    }

                    var (nodesCount, relsCount) = await client.GetGraphCountsAsync();
                    var nodesByKind = await client.GetNodesBreakdownAsync();
                    PrintNodesBreakdown(nodesCount, relsCount, nodesByKind);
                }
                else
                {
                    performedFullIndex = true;
                }
            }
            else
            {
                performedFullIndex = true;
            }

            if (performedFullIndex)
            {
                var (nodesCount, relsCount, nodesByKind) = await indexer.IndexAsync(targetPath, ws.RootDirectory, clear: shouldClear, enableIntentAnalysis: opts.Intent);
                PrintNodesBreakdown(nodesCount, relsCount, nodesByKind);
            }

            if (opts.Watch)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n[Watch] Watching for file changes in {targetPath} (press Ctrl+C to exit)...");
                Console.ResetColor();

                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                using var watcher = new FileWatcher(
                    targetPath,
                    async batch =>
                    {
                        logger.LogInformation("[Watch] File changes detected ({Count} file(s)): {Files}. Evaluating incremental update...", batch.Count, string.Join(", ", batch));
                        try
                        {
                            var changed = await indexer.IndexIncrementalAsync(targetPath, ws.RootDirectory, cancellationToken: cts.Token, enableIntentAnalysis: opts.Intent);
                            if (changed)
                            {
                                logger.LogInformation("[Watch] Incremental indexing complete.");
                                var (nodesCount, relsCount) = await client.GetGraphCountsAsync(cts.Token);
                                var nodesByKind = await client.GetNodesBreakdownAsync(cts.Token);
                                PrintNodesBreakdown(nodesCount, relsCount, nodesByKind);
                            }
                            else
                            {
                                logger.LogInformation("[Watch] No graph changes needed.");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            // Cancellation requested
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "[Watch] Incremental indexing failed: {Message}", ex.Message);
                        }
                    });

                watcher.Start();

                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("[Watch] Stopped watching.");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Scan Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintNodesBreakdown(int nodesCount, int relsCount, Dictionary<string, int> nodesByKind)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n✓ Successfully indexed {nodesCount} nodes and {relsCount} relationships!");
        Console.ResetColor();

        Console.WriteLine("Nodes breakdown:");
        foreach (var (kind, count) in nodesByKind.OrderByDescending(x => x.Value))
        {
            Console.WriteLine($"  - {kind,-20}: {count,6}");
        }
    }
}
