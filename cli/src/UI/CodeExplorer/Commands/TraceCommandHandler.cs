using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class TraceCommandHandler
{
    public static async Task<int> HandleAsync(TraceOptions opts)
    {
        var startService = opts.StartService;
        if (string.IsNullOrWhiteSpace(startService))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Starting service is required (--from <service> or --service <service>).");
            Console.ResetColor();
            return 1;
        }

        var targetDir = Path.GetFullPath(opts.Dir ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: No CodeExplorer workspace found at '{targetDir}'. Run 'ce init' first.");
            Console.ResetColor();
            return 1;
        }

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var output = await repository.TraceCrossServiceFlowAsync(
            startService: startService,
            entryPoint: opts.Entry,
            maxDepth: opts.MaxDepth,
            format: opts.Format,
            workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
