using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class LineageCommandHandler : BaseCommandHandler
{
    private const string MissingTableError = "Error: Table name is required (e.g. 'ce lineage <table>').";

    public static async Task<int> HandleAsync(LineageOptions opts)
    {
        var tableName = opts.EffectiveTable;
        if (string.IsNullOrWhiteSpace(tableName))
        {
            PrintError(MissingTableError);
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var output = await repository.InspectDataLineageAsync(tableName, ws.RootDirectory);
        Console.WriteLine(output);
        return 0;
    }
}
