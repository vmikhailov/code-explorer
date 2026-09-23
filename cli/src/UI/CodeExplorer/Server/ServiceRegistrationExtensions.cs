using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CodeExplorer.Server;

public static class ServiceRegistrationExtensions
{
    public static void RegisterCommonServices(this IServiceCollection services, SqliteGraphClient client, string? workspaceRoot = null)
    {
        services.AddSingleton<IGraphClient>(client);
        services.AddSingleton<CodeExplorer.Core.Analysis.IArchitectureQueryService>(sp =>
            new CodeExplorer.Core.Analysis.ArchitectureQueryService(sp.GetRequiredService<IGraphClient>()));
        services.AddSingleton<ProjectQueryManager>();
        services.AddSingleton(sp => new CodeExplorerRepository(
            sp.GetRequiredService<IGraphClient>(),
            sp.GetRequiredService<ProjectQueryManager>(),
            workspaceRoot
        ));
        services.AddSingleton<WorkspaceIndexer>();
        services.AddHttpContextAccessor();
    }

    public static void ConfigureMcpWebServices(this IServiceCollection services, SqliteGraphClient client, string? workspaceRoot = null)
    {
        services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
        services.RegisterCommonServices(client, workspaceRoot);

#pragma warning disable MCP9004, MCPEXP002
        services.AddMcpServer().WithHttpTransport(o =>
        {
            o.Stateless = false;
            o.EnableLegacySse = true;
        }).WithTools<McpGraphHandler>();
#pragma warning restore MCP9004, MCPEXP002
    }
}
