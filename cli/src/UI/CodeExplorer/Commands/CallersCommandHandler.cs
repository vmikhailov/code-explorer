using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class CallersCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(CallersOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.Target))
        {
            PrintError("Error: Target name is required (e.g. 'ce callers <target>').");
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var output = await repository.GetCallersAsync(
            target: opts.Target,
            format: opts.Format,
            includeTests: opts.IncludeTests,
            workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
