using System.Diagnostics;
using CodeExplorer.Options;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Commands;

public static class ScanCommandHandler
{
    public static async Task<int> HandleAsync(ScanOptions opts)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information).AddShortConsole();
        });

        var logger = loggerFactory.CreateLogger(typeof(ScanCommandHandler));
        var indexerLogger = loggerFactory.CreateLogger<WorkspaceIndexer>();
        var clientLogger = loggerFactory.CreateLogger<SqliteGraphClient>();

        try
        {
            var targetPath = Path.GetFullPath(opts.Path ?? Directory.GetCurrentDirectory());
            var ws = WorkspaceLocator.Find(targetPath);
            if (ws == null)
            {
                if (opts.ShouldAutoInit)
                {
                    var name = new DirectoryInfo(targetPath).Name;
                    ws = WorkspaceLocator.Initialize(targetPath, name);
                    logger.LogInformation("Initialized new CodeExplorer workspace at '{WorkspaceRoot}'", ws.RootDirectory);
                }
                else
                {
                    ws = WorkspaceLocator.FindOrThrow(targetPath);
                }
            }

            logger.LogInformation("Workspace: {WorkspaceRoot} ({DbPath})", ws.RootDirectory, ws.DbPath);
            logger.LogInformation("Scanning:  {TargetPath}...", targetPath);

            await using var client = new SqliteGraphClient(ws.DbPath, clientLogger);

            var shouldClear = opts.ShouldClear;
            var hasExistingRegistry = !shouldClear && await client.HasFileRegistryDataAsync();

            if (!shouldClear && client.IsSchemaOutdated)
            {
                if (hasExistingRegistry)
                {
                    logger.LogInformation(
                        "Database schema version is outdated (v{Version} < v{Current}). Converting database schema...",
                        client.SchemaVersion, SqliteGraphClient.CurrentSchemaVersion);
                    if (await client.UpgradeSchemaAsync())
                    {
                        logger.LogInformation(
                            "Database schema successfully converted to v{Current}.",
                            SqliteGraphClient.CurrentSchemaVersion);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Database schema version is outdated (v{Version} < v{Current}). Automatically performing clean rescan...",
                            client.SchemaVersion, SqliteGraphClient.CurrentSchemaVersion);
                        shouldClear = true;
                        hasExistingRegistry = false;
                    }
                }
                else
                {
                    shouldClear = true;
                }
            }

            var indexer = new WorkspaceIndexer(client, indexerLogger);
            var scanSw = Stopwatch.StartNew();

            var result = await indexer.IndexAsync(
                targetPath,
                ws.RootDirectory,
                clear: shouldClear,
                enableIntentAnalysis: false);
            scanSw.Stop();

            if (!result.HasChanges)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(
                    $"\n✓ Workspace is up to date (0 changes detected) in {scanSw.ElapsedMilliseconds:N0}ms. Use --clear to force a full re-index.");
                Console.ResetColor();
            }
            else if (result.Mode is IndexExecutionMode.Incremental or IndexExecutionMode.LogicOnly)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Incremental indexing completed successfully in {scanSw.Elapsed.TotalSeconds:F2}s ({scanSw.ElapsedMilliseconds:N0}ms)!");
                Console.ResetColor();
            }

            PrintNodesBreakdown(result.NodesCount, result.RelationshipsCount, result.NodesByKind, scanSw.Elapsed);

            if (opts.Watch)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;

                Console.WriteLine(
                    $"\n[Watch       ] Watching for file changes in {targetPath} (press Ctrl+C to exit)...");
                Console.ResetColor();

                using var cts = new CancellationTokenSource();

                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                using var watcher = new FileWatcher(targetPath, async batch =>
                {
                    logger.LogInformation(
                        "[Watch       ] File changes detected ({Count} file(s)): {Files}. Evaluating incremental update...",
                        batch.Count, string.Join(", ", batch));

                    try
                    {
                        var watchResult = await indexer.IndexAsync(
                            targetPath,
                            ws.RootDirectory,
                            cancellationToken: cts.Token,
                            enableIntentAnalysis: false,
                            knownChangedFiles: batch);

                        if (watchResult.HasChanges)
                        {
                            logger.LogInformation("[Watch       ] Incremental indexing complete.");
                            PrintNodesBreakdown(watchResult.NodesCount, watchResult.RelationshipsCount, watchResult.NodesByKind);
                        }
                        else
                        {
                            logger.LogInformation("[Watch       ] No graph changes needed.");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Cancellation requested
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "[Watch       ] Incremental indexing failed: {Message}",
                            ex.Message);
                    }
                });

                watcher.Start();

                try
                {
                    await Task.Delay(Timeout.Infinite, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("[Watch       ] Stopped watching.");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Scan Error: {ex}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintNodesBreakdown(int nodesCount, int relsCount, Dictionary<string, int> nodesByKind, TimeSpan? elapsed = null)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        var timeSuffix = elapsed.HasValue ? $" in {elapsed.Value.TotalSeconds:F2}s ({elapsed.Value.TotalMilliseconds:N0}ms)" : string.Empty;
        Console.WriteLine($"\n✓ Successfully indexed {nodesCount} nodes and {relsCount} relationships{timeSuffix}!");
        Console.ResetColor();

        Console.WriteLine("Nodes breakdown:");
        foreach (var (kind, count) in nodesByKind
                     .Where(x => !CodeExplorerRepository.IsTemporarilyHiddenTaxonomyLabel(x.Key))
                     .OrderByDescending(x => x.Value))
        {
            Console.WriteLine($"  - {kind,-20}: {count,6}");
        }
    }
}
