using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class DependenciesCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(DependenciesOptions opts)
    {
        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);

        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var output = await repository.GetProjectDependenciesAsync(projectFilter: opts.Project, format: opts.Format,
            limit: opts.Limit, type: opts.Type, workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
