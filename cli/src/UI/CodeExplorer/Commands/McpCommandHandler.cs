using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Options;
using CodeExplorer.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Commands;

public static class McpCommandHandler
{
    public static async Task<int> HandleAsync(McpOptions opts)
    {
        WorkspaceInfo? ws = null;
        string? dbPath = null;
        if (!string.IsNullOrWhiteSpace(opts.DbPath))
        {
            dbPath = Path.GetFullPath(opts.DbPath);
            ws = WorkspaceLocator.FindFromDbPath(dbPath);
        }
        else
        {
            ws = WorkspaceLocator.FindWithFallbacks(opts.Root);
            if (ws != null)
            {
                dbPath = ws.DbPath;
                WorkspaceLocator.RecordActiveWorkspace(ws.RootDirectory);
            }
        }

        var isStandby = string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath);
        if (isStandby && string.IsNullOrEmpty(dbPath))
        {
            dbPath = "Data Source=:memory:;Mode=Memory;Cache=Shared";
        }

        await using var client = new SqliteGraphClient(dbPath!);
        var wsRoot = ws?.RootDirectory;

        if (opts.Port > 0)
        {
            await RunMcpWebServerAsync(client, opts.Port, wsRoot, isStandby, opts.Quiet);
        }
        else
        {
            await RunMcpStdioHostAsync(client, wsRoot, isStandby, opts.Quiet);
        }

        return 0;
    }

    private static async Task RunMcpWebServerAsync(SqliteGraphClient client, int port, string? workspaceRoot = null, bool isStandby = false, bool quiet = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        if (!quiet)
        {
            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
            builder.Logging.AddFilter("System", LogLevel.Warning);
            builder.Logging.AddShortConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        }
        else
        {
            builder.Logging.SetMinimumLevel(LogLevel.None);
        }

        builder.Services.ConfigureMcpWebServices(client, workspaceRoot);

        var app = builder.Build();
        Program.App = app;
        client.Logger = app.Services.GetRequiredService<ILogger<SqliteGraphClient>>();
        ConfigureWebPipeline(app, quiet);

        app.Urls.Add($"http://0.0.0.0:{port}");
        if (!quiet)
        {
            if (isStandby)
            {
                app.Logger.LogWarning("[MCP Standby] Server started in Standby Mode (no workspace bound). Specify 'workspacePath' on tool calls or set WORKSPACE_ROOT environment variable.");
            }
            else
            {
                app.Logger.LogInformation("Starting CodeExplorer MCP HTTP Service on http://localhost:{Port} (endpoint: /mcp)...", port);
            }
        }
        await app.RunAsync();
    }

    private static void ConfigureWebPipeline(WebApplication app, bool quiet = false)
    {
        if (!quiet)
        {
            app.Use(async (context, next) =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var path = context.Request.Path.Value ?? "";
                var method = context.Request.Method;

                try
                {
                    await next();
                }
                finally
                {
                    sw.Stop();
                    var contentType = context.Response.ContentType ?? string.Empty;
                    if (!contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
                    {
                        app.Logger.LogInformation("[MCP] {Method} {Path} completed with {StatusCode} in {ElapsedMs:F1}ms",
                            method, path, context.Response.StatusCode, sw.Elapsed.TotalMilliseconds);
                    }
                }
            });
        }

        app.UseCors();
        app.MapGet("/", () => Results.Ok(new { service = "CodeExplorer MCP", transport = "http", endpoint = "/mcp" }));
        app.MapMcp("/mcp");
    }

    private static async Task RunMcpStdioHostAsync(SqliteGraphClient client, string? workspaceRoot = null, bool isStandby = false, bool quiet = false)
    {
        var builder = Host.CreateApplicationBuilder();
        if (!quiet)
        {
            builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
            builder.Logging.AddFilter("System", LogLevel.Warning);
            builder.Logging.AddShortConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        }
        else
        {
            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(LogLevel.None);
        }

        builder.Services.RegisterCommonServices(client, workspaceRoot);
        builder.Services.AddMcpServer().WithStdioServerTransport().WithTools<McpGraphHandler>();

        var host = builder.Build();
        client.Logger = host.Services.GetRequiredService<ILogger<SqliteGraphClient>>();
        if (isStandby && !quiet)
        {
            var logger = host.Services.GetRequiredService<ILogger<McpOptions>>();
            logger.LogWarning("[MCP Standby] Server started in Standby Mode (no workspace bound). Specify 'workspacePath' on tool calls or set WORKSPACE_ROOT environment variable.");
        }
        await host.RunAsync();
    }
}
