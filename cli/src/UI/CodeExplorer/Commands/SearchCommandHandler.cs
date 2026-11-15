using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class SearchCommandHandler : BaseCommandHandler
{
    private const string MissingQueryError = "Error: Search query is required (e.g. 'ce symbol <name>' or 'ce search <query>').";
    private const string MatchModeHybrid = "hybrid";
    private const string SymLabelClass = "Class";
    private const string SymLabelInterface = "Interface";
    private const string SymLabelType = "Type";
    private const string SymLabelVariable = "Variable";
    private const string SymLabelMember = "Member";
    private const string SymLabelEndpoint = "Endpoint";
    private const string SymLabelTable = "Table";
    private const string SymLabelTopic = "Topic";
    private const string SymLabelProject = "Project";

    public static async Task<int> HandleAsync(SearchOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.Query))
        {
            PrintError(MissingQueryError);
            return 1;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);

        var symbolType = opts.EffectiveSymbolType;
        var limit = Math.Clamp(opts.EffectiveLimit, 1, 500);
        var useHybrid = string.Equals(opts.EffectiveMode, MatchModeHybrid, StringComparison.OrdinalIgnoreCase)
            || symbolType is SymLabelEndpoint or SymLabelTable or SymLabelTopic or SymLabelProject;

        string output;
        if (useHybrid)
        {
            var mappedFilter = symbolType switch
            {
                SymLabelClass or SymLabelInterface => SymLabelType,
                SymLabelVariable => SymLabelMember,
                _ => symbolType
            };
            output = await repository.SearchIdentifiersAsync(opts.Query, mappedFilter, Math.Clamp(limit, 1, 200), ws.RootDirectory);
        }
        else
        {
            output = await repository.FindSymbolAsync(opts.Query, symbolType, limit, ws.RootDirectory);
        }

        Console.WriteLine(output);
        return 0;
    }
}
