using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class TraceCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(TraceOptions opts)
    {
        var startService = opts.StartService;
        if (string.IsNullOrWhiteSpace(startService))
        {
            PrintError("Error: Starting service is required (--from <service> or --service <service>).");
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);

        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var output = await repository.TraceCrossServiceFlowAsync(startService: startService, entryPoint: opts.Entry,
            maxDepth: opts.MaxDepth, format: opts.Format, workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
