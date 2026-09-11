using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Web.Controllers;
using CodeExplorer.Options;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.SQL;
using CodeExplorer.Parser.TypeScript;
using CommandLine;
using CommandLineParser = CommandLine.Parser;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeExplorer;

public class Program
{
    public static WebApplication? App { get; private set; }

    public static async Task<int> Main(string[] args)
    {
        WorkspaceIndexer.Register(new CSharpParser());
        WorkspaceIndexer.Register(new GoParser());
        WorkspaceIndexer.Register(new PythonParser());
        WorkspaceIndexer.Register(new TypeScriptParser());
        WorkspaceIndexer.Register(new JavaScriptParser());
        WorkspaceIndexer.Register(new SqlParser());

        return await CommandLineParser.Default
            .ParseArguments<IngestOptions, QueryOptions, McpOptions>(args)
            .MapResult(
                (IngestOptions opts) => HandleIngestAsync(opts),
                (QueryOptions opts) => HandleQueryAsync(opts),
                (McpOptions opts) => HandleMcpAsync(opts),
                _ => Task.FromResult(1));
    }

    private static async Task<int> HandleIngestAsync(IngestOptions opts)
    {
        CodeExplorer.Core.Parser.ParsingContext.EnableConsoleLogging = true;
        try
        {
            Console.WriteLine($"Scanning and parsing directory: {opts.Dir}...");
            await using var client = new SqliteGraphClient(opts.DbPath);

            if (opts.ClearAll)
            {
                Console.WriteLine("Performing a global database clear...");
                await client.ClearDatabaseAsync();
            }

            var indexer = new WorkspaceIndexer(client);

            var (nodesCount, relsCount, nodesByKind) =
                await indexer.IndexAsync(opts.Dir, opts.Dir, opts.Clear && !opts.ClearAll);

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
            await Console.Error.WriteLineAsync($"Ingestion Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> HandleQueryAsync(QueryOptions opts)
    {
        await using var client = new SqliteGraphClient(opts.DbPath);

        try
        {
            var result = await client.ExecuteQueryAsync(opts.Query);
            Console.WriteLine(result);
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Query Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> HandleMcpAsync(McpOptions opts)
    {
        await using var client = new SqliteGraphClient(opts.DbPath);
        if (opts.Port > 0)
        {
            await RunMcpWebServerAsync(client, opts.Port);
        }
        else
        {
            await RunMcpStdioHostAsync(client);
        }
        return 0;
    }

    private static async Task RunMcpWebServerAsync(SqliteGraphClient client, int port)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("System", LogLevel.Warning);

        ConfigureWebServices(builder.Services, client);

        var app = builder.Build();
        App = app;
        ConfigureWebPipeline(app);

        app.Urls.Add($"http://0.0.0.0:{port}");
        // await Console.Error.WriteLineAsync($"Starting Unified CodeExplorer Web Service on http://localhost:{port}...");
        // await Console.Error.WriteLineAsync($"Swagger UI available at http://localhost:{port}/swagger");
        await app.RunAsync();
    }

    private static void ConfigureWebServices(IServiceCollection services, SqliteGraphClient client)
    {
        services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
        services.AddControllers().AddApplicationPart(typeof(WorkspacesController).Assembly);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c => c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "CodeExplorer API (MCP & REST Management)",
            Version = "v1",
            Description = "Unified server hosting both the MCP SSE transport and REST management controllers."
        }));

        RegisterCommonServices(services, client);

#pragma warning disable MCP9004, MCPEXP002
        services.AddMcpServer().WithHttpTransport(o =>
        {
            o.Stateless = false;
            o.EnableLegacySse = true;
        }).WithTools<McpGraphHandler>();
#pragma warning restore MCP9004, MCPEXP002
    }

    private static void ConfigureWebPipeline(WebApplication app)
    {
        app.UseCors();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeExplorer API v1");
            c.RoutePrefix = "swagger";
        });
        app.MapControllers();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/") { context.Response.Redirect("/swagger"); return; }
            await next();
        });
        app.MapMcp("/mcp");
    }

    private static async Task RunMcpStdioHostAsync(SqliteGraphClient client)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
        builder.Logging.AddFilter("System", LogLevel.Warning);

        RegisterCommonServices(builder.Services, client);
        builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<McpGraphHandler>();

        var host = builder.Build();
        await host.RunAsync();
    }

    private static void RegisterCommonServices(IServiceCollection services, SqliteGraphClient client)
    {
        services.AddSingleton<IGraphClient>(client);
        services.AddSingleton<CodeExplorerRepository>();
        services.AddSingleton<WorkspaceIndexer>();
        services.AddSingleton<IndexingTaskManager>();
        services.AddSingleton<WorkspaceRegistry>();
        services.AddHttpContextAccessor();
    }
}
