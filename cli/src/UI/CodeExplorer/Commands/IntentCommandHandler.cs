using CodeExplorer.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Options;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Commands;

public static class IntentCommandHandler
{
    public static async Task<int> HandleAsync(IntentOptions opts)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Information)
                .AddShortConsole();
        });

        var logger = loggerFactory.CreateLogger(typeof(IntentCommandHandler));
        var clientLogger = loggerFactory.CreateLogger<SqliteGraphClient>();

        try
        {
            var targetPath = Path.GetFullPath(opts.Path ?? Directory.GetCurrentDirectory());
            var ws = WorkspaceLocator.FindOrThrow(targetPath);

            logger.LogInformation("Workspace: {WorkspaceRoot} ({DbPath})", ws.RootDirectory, ws.DbPath);

            await using var client = new SqliteGraphClient(ws.DbPath, clientLogger);

            if (opts.Clear)
            {
                logger.LogInformation("Clearing intent cache for workspace...");
                await client.ClearIntentsAsync(ws.RootDirectory);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Intent cache cleared successfully.");
                Console.ResetColor();
                return 0;
            }

            if (opts.ResetErrors)
            {
                logger.LogInformation("Resetting intent error counters for workspace...");
                await client.ResetIntentErrorsAsync(ws.RootDirectory);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n✓ Error counters reset. Files can now be re-evaluated.");
                Console.ResetColor();
                return 0;
            }

            var channel = System.Threading.Channels.Channel.CreateUnbounded<Func<Task>>();
            var ctx = new ParsingContext(
                ws.RootDirectory,
                ws.RootDirectory,
                client,
                channel,
                clear: false,
                logger: logger,
                enableIntentAnalysis: true
            );
            ctx.WorkspaceId = ws.RootDirectory;

            logger.LogInformation("Starting architectural intent distillation pass...");
            var applied = await CodeIntentAnalyzer.RunIncrementalIntentAnalysisAsync(ctx, limit: opts.Limit);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✓ Intent distillation finished. Enriched {applied} nodes in knowledge graph.");
            Console.ResetColor();
            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Intent Distillation Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}
