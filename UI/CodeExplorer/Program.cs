using CodeExplorer.Database;
using CodeExplorer.Parser;
using CodeExplorer.Mcp;
using CommandLine;
using CommandLineParser = CommandLine.Parser;

namespace CodeExplorer;

class Program
{
    static async Task<int> Main(string[] args)
    {
        WorkspaceParser.Register(new CSharpParser());
        WorkspaceParser.Register(new GoParser());
        WorkspaceParser.Register(new PythonParser());
        WorkspaceParser.Register(new TypeScriptParser());
        WorkspaceParser.Register(new JavaScriptParser());

        return await CommandLineParser.Default.ParseArguments<IngestOptions, QueryOptions, McpOptions>(args)
            .MapResult(
                (IngestOptions opts) => HandleIngestAsync(opts),
                (QueryOptions opts) => HandleQueryAsync(opts),
                (McpOptions opts) => HandleMcpAsync(opts),
                errs => Task.FromResult(1)
            );
    }

    private static async Task<int> HandleIngestAsync(IngestOptions opts)
    {
        try
        {
            Console.WriteLine($"Scanning and parsing directory: {opts.Dir}...");
            await using var client = new MemgraphClient(opts.BoltUrl, opts.Username, opts.Password);

            if (opts.ClearAll)
            {
                Console.WriteLine("Performing a global database clear...");
                await client.ClearDatabaseAsync();
            }

            var indexer = new WorkspaceIndexerService(client);
            var (nodesCount, relsCount, nodesByKind) = await indexer.IndexWorkspaceAsync(opts.Dir, opts.Clear && !opts.ClearAll);

            Console.WriteLine($"Parsed and uploaded {nodesCount} nodes and {relsCount} relationships successfully!");
            Console.WriteLine("Nodes breakdown by kind:");
            foreach (var kvp in nodesByKind)
            {
                Console.WriteLine($"  - {kvp.Key}: {kvp.Value}");
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Ingestion Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> HandleQueryAsync(QueryOptions opts)
    {
        await using var client = new MemgraphClient(opts.BoltUrl, opts.Username, opts.Password);
        try
        {
            var result = await client.ExecuteQueryAsync(opts.Query);
            Console.WriteLine(result);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Query Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> HandleMcpAsync(McpOptions opts)
    {
        await using var client = new MemgraphClient(opts.BoltUrl, opts.Username, opts.Password);

        if (opts.Port > 0)
        {
            var sseServer = new SseMcpServer(client, opts.Port);
            await sseServer.StartAsync();
        }
        else
        {
            var stdioServer = new McpServer(client);
            await stdioServer.StartAsync();
        }

        return 0;
    }
}
