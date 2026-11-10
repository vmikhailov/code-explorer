using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class SearchCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(SearchOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.Query))
        {
            PrintError("Error: Search query is required (e.g. 'ce search <query>').");
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);
        var output = await repository.SearchIdentifiersAsync(opts.Query, opts.Kind, opts.Limit, ws.RootDirectory);
        Console.WriteLine(output);
        return 0;
    }
}
