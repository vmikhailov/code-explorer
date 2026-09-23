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

            if (opts.Clear)
            {
                logger.LogInformation("Clearing existing data for {TargetPath}...", targetPath);
                await client.ClearWorkspaceAsync(targetPath);
            }

            var indexer = new WorkspaceIndexer(client, indexerLogger);
            var (nodesCount, relsCount, nodesByKind) = await indexer.IndexAsync(targetPath, ws.RootDirectory, clear: false);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Successfully indexed {nodesCount} nodes and {relsCount} relationships!");
            Console.ResetColor();

            Console.WriteLine("Nodes breakdown:");
            foreach (var (kind, count) in nodesByKind.OrderByDescending(x => x.Value))
            {
                Console.WriteLine($"  - {kind,-20}: {count,6}");
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
}
