using System.Text.Json;
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
        var engine = new IdentifierSearchEngine();
        var results = await engine.SearchAsync(client.Connection, opts.Query, opts.Kind, opts.Limit);

        if (results.Count == 0)
        {
            Console.WriteLine($"No identifiers found matching '{opts.Query}'.");
            return 0;
        }

        if (opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine($"### Code Identifier Search Results for `{opts.Query}`\n");
        Console.WriteLine($"**Matches Found**: {results.Count}\n");
        Console.WriteLine("| Score | Kind | Name | Full Name / Route | Location (File:Line) |");
        Console.WriteLine("| :--- | :--- | :--- | :--- | :--- |");

        foreach (var r in results)
        {
            var loc = !string.IsNullOrEmpty(r.Path) ? (r.StartLine > 0 ? $"{r.Path}:{r.StartLine}" : r.Path) : "-";
            Console.WriteLine($"| {r.Score:F2} | `{r.Kind}` | `{r.Name}` | `{r.FullName}` | `{loc}` |");
        }

        return 0;
    }
}
