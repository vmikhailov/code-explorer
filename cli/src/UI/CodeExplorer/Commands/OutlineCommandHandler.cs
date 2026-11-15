using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class OutlineCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(OutlineOptions opts)
    {
        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var output = await repository.GetFileOutlineAsync(opts.EffectivePath, ws.RootDirectory);
        Console.WriteLine(output);
        return 0;
    }
}
