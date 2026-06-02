using System;
using System.IO;
using System.Threading.Tasks;
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
        // Dynamically register the dialect parsers
        TreeSitterParser.Register(new CSharpParser());
        TreeSitterParser.Register(new GoParser());
        TreeSitterParser.Register(new PythonParser());
        TreeSitterParser.Register(new TypeScriptParser());

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
            var (nodesCount, relsCount) = await indexer.IndexWorkspaceAsync(opts.Dir, opts.Clear && !opts.ClearAll);
            
            Console.WriteLine($"Parsed and uploaded {nodesCount} nodes and {relsCount} relationships successfully!");
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
