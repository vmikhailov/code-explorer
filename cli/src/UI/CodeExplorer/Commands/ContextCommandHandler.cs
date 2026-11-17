using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class ContextCommandHandler : BaseCommandHandler
{
    private const string EnvServerUrl = "CE_SERVER_URL";
    private const string MissingQueryError = "Error: Task query is required (e.g. 'ce context \"revoke api key usage limit\"').";

    public static async Task<int> HandleAsync(ContextOptions opts)
    {
        var serverUrl = !string.IsNullOrWhiteSpace(opts.ServerUrl)
            ? opts.ServerUrl.Trim().TrimEnd('/')
            : Environment.GetEnvironmentVariable(EnvServerUrl)?.Trim().TrimEnd('/');

        if (opts.CheckGates)
        {
            return await HandleGateCheckAsync(opts, serverUrl);
        }

        if (string.IsNullOrWhiteSpace(opts.Query))
        {
            PrintError(MissingQueryError);
            return 1;
        }

        if (!string.IsNullOrEmpty(serverUrl))
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var encoded = Uri.EscapeDataString(opts.Query);
            var includeGates = (!opts.NoGates).ToString().ToLowerInvariant();
            var uri = $"{serverUrl}/api/context?q={encoded}&maxAnchors={opts.MaxAnchors}&includeGates={includeGates}";
            var text = await http.GetStringAsync(uri);
            Console.WriteLine(text);
            return 0;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);
        var output = await repository.GetTaskContextAsync(
            opts.Query,
            opts.MaxAnchors,
            !opts.NoGates,
            ws.RootDirectory);

        Console.WriteLine(output);
        return 0;
    }

    private static async Task<int> HandleGateCheckAsync(ContextOptions opts, string? serverUrl)
    {
        if (!string.IsNullOrEmpty(serverUrl))
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var text = await http.GetStringAsync($"{serverUrl}/api/gates/check");
            Console.WriteLine(text);
            return text.StartsWith("status: failed", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }

        var ws = EnsureInitialized(opts.Dir, requireIndexed: true);
        if (ws == null) return 1;

        await using var client = new SqliteGraphClient(ws.DbPath);
        var repository = new CodeExplorerRepository(client, defaultWorkspacePath: ws.RootDirectory);
        var yaml = await repository.CheckSemanticGatesAsync(workspacePath: ws.RootDirectory);
        Console.WriteLine(yaml);
        return yaml.StartsWith("status: failed", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }
}
