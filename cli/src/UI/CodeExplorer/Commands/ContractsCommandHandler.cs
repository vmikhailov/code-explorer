using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class ContractsCommandHandler
{
    public static async Task<int> HandleAsync(ContractsOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.Service))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Service name is required (--service <name>).");
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

        var output = await repository.GetServiceContractsAsync(
            serviceName: opts.Service,
            direction: opts.Direction,
            format: opts.Format,
            workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
