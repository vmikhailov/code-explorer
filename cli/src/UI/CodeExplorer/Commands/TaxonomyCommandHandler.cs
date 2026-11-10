using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class TaxonomyCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(TaxonomyOptions opts)
    {
        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var taxonomyYaml = await repository.GetTaxonomyAsync(ws.RootDirectory);
        Console.WriteLine(taxonomyYaml);
        return 0;
    }
}
