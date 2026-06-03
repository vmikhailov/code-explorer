using CodeExplorer.Database;
using CodeExplorer.Mcp;
using CodeExplorer.Parser;
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
        WorkspaceParser.Register(new CSharpParser());
        WorkspaceParser.Register(new GoParser());
        WorkspaceParser.Register(new PythonParser());
        WorkspaceParser.Register(new TypeScriptParser());
        WorkspaceParser.Register(new JavaScriptParser());
        WorkspaceParser.Register(new SqlParser());

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
            await Console.Error.WriteLineAsync($"Ingestion Error: {ex.Message}");
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
            await Console.Error.WriteLineAsync($"Query Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> HandleMcpAsync(McpOptions opts)
    {
        await using var client = new MemgraphClient(opts.BoltUrl, opts.Username, opts.Password);

        if (opts.Port > 0)
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
            builder.Logging.AddFilter("System", LogLevel.Warning);

            builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

            // Explicitly register controllers assembly to ensure discovery of REST controllers
            builder.Services.AddControllers()
                .AddApplicationPart(typeof(Web.Controllers.WorkspacesController).Assembly);

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "CodeExplorer API (MCP & REST Management)",
                    Version = "v1",
                    Description = "Unified server hosting both the Model Context Protocol (MCP) SSE transport and REST management controllers."
                });
            });

            // Register database client and other services
            builder.Services.AddSingleton(client);
            builder.Services.AddSingleton<CodeExplorerRepository>();

            // Register official MCP server
#pragma warning disable MCP9004
            builder.Services.AddMcpServer()
                .WithHttpTransport(options =>
                {
                    options.Stateless = false;
                    options.EnableLegacySse = true;
                })
                .WithTools<McpGraphHandler>();
#pragma warning restore MCP9004

            var app = builder.Build();
            App = app;
            app.UseCors();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "CodeExplorer API v1");
                c.RoutePrefix = "swagger";
            });

            app.MapControllers();

            app.MapGet("/", async context =>
            {
                context.Response.Redirect("/swagger");
                await Task.CompletedTask;
            });

            // Map MCP endpoints (exposing GET /sse and POST /messages by default)
            app.MapMcp();

            app.Urls.Add($"http://0.0.0.0:{opts.Port}");

            await Console.Error.WriteLineAsync($"Starting Unified CodeExplorer Web Service (MCP + REST Management) on http://localhost:{opts.Port}...");
            await Console.Error.WriteLineAsync($"Swagger UI available at http://localhost:{opts.Port}/swagger");
            await app.RunAsync();
        }
        else
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
            builder.Logging.AddFilter("System", LogLevel.Warning);

            builder.Services.AddSingleton(client);
            builder.Services.AddSingleton<CodeExplorerRepository>();

            // Register official MCP server with Stdio transport
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<McpGraphHandler>();

            var host = builder.Build();

            await host.RunAsync();
        }

        return 0;
    }
}
