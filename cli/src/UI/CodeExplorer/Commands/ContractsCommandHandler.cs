using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class ContractsCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(ContractsOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.Service))
        {
            PrintError("Error: Service name is required (--service <name>).");
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);

        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var output = await repository.GetServiceContractsAsync(serviceName: opts.Service, direction: opts.Direction,
            format: opts.Format, workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
