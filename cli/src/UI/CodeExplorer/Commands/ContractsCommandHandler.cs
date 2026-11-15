using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class ContractsCommandHandler : BaseCommandHandler
{
    private const string MissingServiceError = "Error: Service name is required (e.g. 'ce contracts <service>' or 'ce contracts -s <service>').";
    private const string DirectionAll = "all";
    private const string DirectionEntryPoints = "entry_points";
    private const string DirectionEntryPointsHyphen = "entry-points";
    private const string DirectionEntryPointsCompact = "entrypoints";
    private const string DirectionFlow = "flow";

    public static async Task<int> HandleAsync(ContractsOptions opts)
    {
        var serviceName = opts.EffectiveService;
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            PrintError(MissingServiceError);
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var normDirection = string.IsNullOrWhiteSpace(opts.Direction) ? DirectionAll : opts.Direction.Trim().ToLowerInvariant();
        var output = normDirection switch
        {
            DirectionEntryPoints or DirectionEntryPointsHyphen or DirectionEntryPointsCompact =>
                await repository.GetProjectEntryPointsAsync(serviceName, ws.RootDirectory),
            DirectionFlow =>
                await repository.TraceCrossServiceFlowAsync(serviceName, opts.EntryPoint, Math.Clamp(opts.MaxDepth, 1, 10), ws.RootDirectory),
            _ =>
                await repository.GetServiceContractsAsync(serviceName: serviceName, direction: normDirection, workspacePath: ws.RootDirectory)
        };

        Console.WriteLine(output);
        return 0;
    }
}
