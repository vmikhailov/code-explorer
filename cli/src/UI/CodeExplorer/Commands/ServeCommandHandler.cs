using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Core.Parser;
using CodeExplorer.Options;
using CodeExplorer.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Commands;

public static class ServeCommandHandler
{
    public static async Task<int> HandleAsync(ServeOptions opts)
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

        var wsRoot = ws?.RootDirectory ?? (opts.Root != null ? Path.GetFullPath(opts.Root) : Directory.GetCurrentDirectory());

        if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
        {
            var targetDir = ws != null
                ? Path.GetDirectoryName(ws.DbPath)!
                : Path.Combine(wsRoot, ".codeexplorer");

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            dbPath = ws?.DbPath ?? Path.Combine(targetDir, "graph.db");
            WorkspaceLocator.RecordActiveWorkspace(wsRoot);
        }

        var client = new SqliteGraphClient(dbPath);

        var app = CreateWebApplication(opts, wsRoot, client);
        Program.App = app;

        if (client.IsSchemaOutdated && !opts.Quiet)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Database] Database schema version is outdated (v{client.SchemaVersion} < v{SqliteGraphClient.CurrentSchemaVersion}). Server starting; graph rebuild will occur asynchronously.");
            Console.ResetColor();
        }

        await app.StartAsync();

        var serverAddressesFeature = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();
        var boundAddress = serverAddressesFeature?.Addresses.FirstOrDefault() ?? $"http://{opts.Host}:{opts.Port}";
        var boundUri = new Uri(boundAddress);
        var actualPort = boundUri.Port;
        var wsUrl = $"ws://{opts.Host}:{actualPort}/ws";

        // Machine-readable stdout line
        Console.WriteLine($"{{\"status\":\"ready\",\"port\":{actualPort},\"wsUrl\":\"{wsUrl}\",\"httpUrl\":\"{boundAddress}\",\"workspace\":\"{wsRoot.Replace("\\", "/")}\"}}");

        if (client.IsSchemaOutdated)
        {
            var wsHandler = app.Services.GetRequiredService<WebSocketServerHandler>();
            wsHandler.TriggerScan(wsRoot, clear: true);
        }

        if (!opts.Quiet)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ CodeExplorer WebSocket Server running at {wsUrl}");
            Console.WriteLine($"✓ REST API available at {boundAddress}");
            Console.ResetColor();
        }

        await app.WaitForShutdownAsync();
        return 0;
    }

    public static WebApplication CreateWebApplication(ServeOptions opts, string wsRoot, SqliteGraphClient client, string[]? args = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? []);
        builder.Logging.ClearProviders();
        if (!opts.Quiet)
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
            opts.IdleTimeoutSeconds,
            wsRoot,
            AppVersionProvider.GetAppVersion(),
            sp.GetRequiredService<CodeExplorer.Core.Analysis.IArchitectureQueryService>()
        ));

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Listen(System.Net.IPAddress.Parse(opts.Host), opts.Port);
        });

        var app = builder.Build();
        if (client is SqliteGraphClient sqliteClient)
        {
            sqliteClient.Logger = app.Services.GetRequiredService<ILogger<SqliteGraphClient>>();
        }

        app.UseCors();
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(15)
        });

        var wsHandler = app.Services.GetRequiredService<WebSocketServerHandler>();
        var serverLogger = app.Services.GetRequiredService<ILogger<ServeOptions>>();

        app.MapCodeExplorerApi(wsRoot, wsHandler, serverLogger);

        return app;
    }
}
