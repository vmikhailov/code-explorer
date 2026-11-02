using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Server;

public static class CodeExplorerServerHost
{
    public static WebApplication? ActiveApp { get; set; }

    public static WebApplication CreateServeApplication(
        string host,
        int port,
        int idleTimeoutSeconds,
        bool quiet,
        string wsRoot,
        SqliteGraphClient client,
        string[]? args = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? []);
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

        builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
        builder.Services.RegisterCommonServices(client, wsRoot);
        builder.Services.AddSingleton(sp => new WebSocketServerHandler(
            client,
            sp.GetRequiredService<CodeExplorerRepository>(),
            sp.GetRequiredService<WorkspaceIndexer>(),
            sp.GetRequiredService<ILogger<WebSocketServerHandler>>(),
            sp.GetService<IHostApplicationLifetime>(),
            idleTimeoutSeconds,
            wsRoot,
            AppVersionProvider.GetAppVersion(),
            sp.GetRequiredService<IArchitectureQueryService>()
        ));

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Listen(System.Net.IPAddress.Parse(host), port);
        });

        var app = builder.Build();
        client.Logger = app.Services.GetRequiredService<ILogger<SqliteGraphClient>>();

        app.UseCors();
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(15)
        });

        var wsHandler = app.Services.GetRequiredService<WebSocketServerHandler>();
        var serverLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CodeExplorer.Server");

        app.MapCodeExplorerApi(wsRoot, wsHandler, serverLogger);

        return app;
    }

    public static async Task RunServeServerAsync(
        string host,
        int port,
        int idleTimeoutSeconds,
        bool quiet,
        string wsRoot,
        SqliteGraphClient client)
    {
        var app = CreateServeApplication(host, port, idleTimeoutSeconds, quiet, wsRoot, client);
        ActiveApp = app;

        if (client.IsSchemaOutdated && !quiet)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Database   ] Database schema version is outdated (v{client.SchemaVersion} < v{SqliteGraphClient.CurrentSchemaVersion}). Server starting; graph rebuild will occur asynchronously.");
            Console.ResetColor();
        }

        await app.StartAsync();

        var serverAddressesFeature = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();
        var boundAddress = serverAddressesFeature?.Addresses.FirstOrDefault() ?? $"http://{host}:{port}";
        var boundUri = new Uri(boundAddress);
        var actualPort = boundUri.Port;
        var wsUrl = $"ws://{host}:{actualPort}/ws";

        var appVersion = AppVersionProvider.GetAppVersion();
        var wsJson = wsRoot.Replace("\\", "/");
        Console.WriteLine(
            $"{{\"status\":\"ready\",\"port\":{actualPort},\"wsUrl\":\"{wsUrl}\",\"httpUrl\":\"{boundAddress}\"," +
            $"\"workspace\":\"{wsJson}\",\"version\":\"{appVersion}\"}}");

        if (client.IsSchemaOutdated)
        {
            var wsHandler = app.Services.GetRequiredService<WebSocketServerHandler>();
            wsHandler.TriggerScan(wsRoot, clear: true);
        }

        if (!quiet)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ CodeExplorer WebSocket Server running at {wsUrl}");
            Console.WriteLine($"✓ REST API available at {boundAddress}");
            Console.ResetColor();
        }

        await app.WaitForShutdownAsync();
    }

    public static async Task RunMcpWebServerAsync(
        SqliteGraphClient client,
        int port,
        string? workspaceRoot = null,
        bool isStandby = false,
        bool quiet = false)
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
        ActiveApp = app;
        client.Logger = app.Services.GetRequiredService<ILogger<SqliteGraphClient>>();
        ConfigureMcpWebPipeline(app, quiet);

        app.Urls.Add($"http://127.0.0.1:{port}");
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

    public static void ConfigureMcpWebPipeline(WebApplication app, bool quiet = false)
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
                        app.Logger.LogInformation("[MCP         ] {Method} {Path} completed with {StatusCode} in {ElapsedMs:F1}ms",
                            method, path, context.Response.StatusCode, sw.Elapsed.TotalMilliseconds);
                    }
                }
            });
        }

        app.UseCors();
        app.MapGet("/", () => Results.Ok(new { service = "CodeExplorer MCP", transport = "http", endpoint = "/mcp" }));
        app.MapMcp("/mcp");
    }

    public static async Task RunMcpStdioHostAsync(
        SqliteGraphClient client,
        string? workspaceRoot = null,
        bool isStandby = false,
        bool quiet = false)
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
            var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CodeExplorer.Mcp");
            logger.LogWarning("[MCP Standby] Server started in Standby Mode (no workspace bound). Specify 'workspacePath' on tool calls or set WORKSPACE_ROOT environment variable.");
        }
        await host.RunAsync();
    }
}
