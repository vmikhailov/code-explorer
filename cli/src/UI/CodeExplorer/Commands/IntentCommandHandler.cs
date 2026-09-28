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

            // Handle --stop
            if (opts.Stop)
            {
                var (stopped, pid, message) = IntentProcessManager.StopRunningProcess(ws.RootDirectory);
                if (stopped)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n✓ {message}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\nℹ {message}");
                    Console.ResetColor();
                }
                return 0;
            }

            // Handle --status
            if (opts.Status)
            {
                var (isRunning, lockInfo, elapsed) = IntentProcessManager.GetStatus(ws.RootDirectory);
                if (isRunning && lockInfo != null && elapsed.HasValue)
                {
                    var elapsedStr = elapsed.Value.TotalMinutes >= 1
                        ? $"{(int)elapsed.Value.TotalMinutes}m {elapsed.Value.Seconds}s"
                        : $"{elapsed.Value.Seconds}s";

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\n● Intent distillation is currently ACTIVE:");
                    Console.WriteLine($"  - Process ID: {lockInfo.ProcessId}");
                    Console.WriteLine($"  - Started:    {lockInfo.LockAcquiredTime.ToLocalTime():yyyy-MM-dd HH:mm:ss} ({elapsedStr} ago)");
                    Console.WriteLine($"  - Workspace:  {lockInfo.WorkspaceRoot}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n○ No intent distillation process is currently active for this workspace.");
                    Console.ResetColor();
                }
                return 0;
            }

            // Handle --force (terminate any existing process before proceeding)
            if (opts.Force)
            {
                var (stopped, pid, _) = IntentProcessManager.StopRunningProcess(ws.RootDirectory);
                if (stopped && pid > 0)
                {
                    logger.LogInformation("Force-stopped previous intent distillation process (PID: {Pid})", pid);
                }
            }

            // Check if another distillation process is currently active
            using var lockHandle = IntentProcessManager.TryAcquireLock(ws.RootDirectory, out var lockError);
            if (lockHandle == null)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine($"\n⚠ {lockError}");
                Console.Error.WriteLine("  Use 'ce intent --stop' to terminate the existing process, or 'ce intent --force' to override.");
                Console.ResetColor();
                return 1;
            }

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

            var (detectedDevice, _) = NativeIntentPredictor.DetectExecutionDevice();
            logger.LogInformation("Compute device: {Device}", detectedDevice);

            using var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler cancelHandler = (_, e) =>
            {
                e.Cancel = true;
                logger.LogWarning("Stopping intent distillation pass (cancellation requested)...");
                cts.Cancel();
            };
            Console.CancelKeyPress += cancelHandler;

            try
            {
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
                var applied = await CodeIntentAnalyzer.RunIncrementalIntentAnalysisAsync(ctx, limit: opts.Limit, cancellationToken: cts.Token);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n✓ Intent distillation finished. Enriched {applied} nodes in knowledge graph.");
                Console.ResetColor();
                return 0;
            }
            finally
            {
                Console.CancelKeyPress -= cancelHandler;
            }
        }
        catch (OperationCanceledException)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n⚠ Intent distillation was stopped.");
            Console.ResetColor();
            return 130;
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
