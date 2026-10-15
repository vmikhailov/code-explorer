using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class ImpactCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(ImpactOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.Symbol))
        {
            PrintError("Error: Target symbol name is required (e.g. 'ce impact <symbol>').");
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var output = await repository.AnalyzeCodeImpactAsync(
            symbolName: opts.Symbol,
            format: opts.Format,
            workspacePath: ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }
}
