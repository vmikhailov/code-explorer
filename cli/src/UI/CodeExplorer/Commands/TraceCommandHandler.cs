using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class TraceCommandHandler : BaseCommandHandler
{
    private const string SubcommandFlow = "flow";
    private const string MissingFlowServiceError = "Error: Starting service is required for flow tracing (--from <service> or --service <service>).";
    private const string MissingCallChainEndpointsError = "Error: Provide both start and end functions ('ce trace <startFn> <endFn>') or use 'ce trace flow --from <service>'.";

    public static async Task<int> HandleAsync(TraceOptions opts)
    {
        var isFlowMode = string.Equals(opts.First, SubcommandFlow, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(opts.StartService)
                && string.IsNullOrWhiteSpace(opts.StartFunctionOption)
                && string.IsNullOrWhiteSpace(opts.EndFunctionOption)
                && string.IsNullOrWhiteSpace(opts.Second));

        if (isFlowMode)
        {
            var startService = !string.IsNullOrWhiteSpace(opts.StartService)
                ? opts.StartService
                : (!string.Equals(opts.First, SubcommandFlow, StringComparison.OrdinalIgnoreCase) ? opts.First : opts.Second);
            if (string.IsNullOrWhiteSpace(startService))
            {
                PrintError(MissingFlowServiceError);
                return 1;
            }

            var wsFlow = EnsureInitialized(opts.Dir, requireIndexed: true);
            if (wsFlow == null) return 1;

            await using var flowClient = new SqliteGraphClient(wsFlow.DbPath);
            var flowRepo = new CodeExplorerRepository(flowClient, defaultWorkspacePath: wsFlow.RootDirectory);
            var flowDepth = Math.Clamp(opts.MaxDepth ?? 3, 1, 10);
            var flowOutput = await flowRepo.TraceCrossServiceFlowAsync(
                startService: startService,
                entryPoint: opts.Entry,
                maxDepth: flowDepth,
                workspacePath: wsFlow.RootDirectory);

            Console.WriteLine(flowOutput);
            return 0;
        }

        var startFunction = !string.IsNullOrWhiteSpace(opts.StartFunctionOption) ? opts.StartFunctionOption : opts.First;
        var endFunction = !string.IsNullOrWhiteSpace(opts.EndFunctionOption) ? opts.EndFunctionOption : opts.Second;
        if (string.IsNullOrWhiteSpace(startFunction) || string.IsNullOrWhiteSpace(endFunction))
        {
            PrintError(MissingCallChainEndpointsError);
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);
        var callChainDepth = Math.Clamp(opts.MaxDepth ?? 5, 1, 10);
        var output = await repository.GetCallChainAsync(
            startFunction: startFunction,
            endFunction: endFunction,
            maxDepth: callChainDepth,
            workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
