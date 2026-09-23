using CodeExplorer.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Diagrams;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Server;

public static class ServerRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapCodeExplorerApi(
        this IEndpointRouteBuilder endpoints,
        string wsRoot,
        WebSocketServerHandler wsHandler,
        ILogger logger)
    {
        endpoints.Map("/ws", async (HttpContext context) =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await wsHandler.HandleConnectionAsync(webSocket, context.RequestAborted);
            }
            else
            {
                logger.LogWarning("[HTTP] Non-WebSocket request rejected at /ws");
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync("WebSocket connection expected at /ws");
            }
        });

        endpoints.MapGet("/", () =>
        {
            logger.LogInformation("[REST] GET / (metadata)");
            return Results.Ok(new
            {
                service = "CodeExplorer API Server",
                version = AppVersionProvider.GetAppVersion(),
                wsEndpoint = "/ws",
                workspace = wsRoot
            });
        });

        endpoints.MapGet("/api/status", async (IGraphClient graphClient) =>
        {
            try
            {
                logger.LogInformation("[REST] GET /api/status");
                var nodeCountJson = await graphClient.ExecuteQueryAsync("MATCH (n) RETURN count(n) AS cnt");
                var edgeCountJson = await graphClient.ExecuteQueryAsync("MATCH ()-[r]->() RETURN count(r) AS cnt");
                return Results.Ok(new
                {
                    status = "ok",
                    workspace = wsRoot,
                    nodes = nodeCountJson,
                    edges = edgeCountJson
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REST] Failed /api/status");
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        endpoints.MapGet("/api/view", async (IArchitectureQueryService archQueryService, string? view, string? scope, bool? includeLibraries) =>
        {
            try
            {
                var viewType = (view?.ToLowerInvariant()) switch
                {
                    "serviceflow" or "flow" or "c2" => ArchitectureViewType.ServiceFlow,
                    "component" or "c3" => ArchitectureViewType.Component,
                    _ => ArchitectureViewType.SystemContext
                };
                var graph = await archQueryService.GetViewAsync(new ArchitectureViewRequest
                {
                    ViewType = viewType,
                    Scope = scope,
                    IncludeLibraries = includeLibraries ?? false
                });
                return Results.Ok(graph);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REST] Failed /api/view");
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        endpoints.MapGet("/api/architecture", async (IArchitectureQueryService archQueryService, string? project) =>
        {
            try
            {
                logger.LogInformation("[REST] GET /api/architecture (project: {Project})", project ?? "all");
                var graph = await archQueryService.GetArchitectureGraphAsync(project);
                return Results.Ok(graph);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REST] Failed /api/architecture");
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        endpoints.MapGet("/api/projects", async (IArchitectureQueryService archQueryService) =>
        {
            try
            {
                logger.LogInformation("[REST] GET /api/projects");
                var projects = await archQueryService.GetAllProjectsAsync();
                return Results.Ok(projects);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REST] Failed /api/projects");
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        endpoints.MapGet("/api/dependencies", async (IArchitectureQueryService archQueryService, string? project) =>
        {
            try
            {
                logger.LogInformation("[REST] GET /api/dependencies (project: {Project})", project ?? "default");
                var graph = await archQueryService.GetProjectNeighborhoodAsync(project);
                return Results.Ok(graph);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REST] Failed /api/dependencies");
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        endpoints.MapGet("/api/metadata", async (IArchitectureQueryService archQueryService) =>
        {
            try
            {
                logger.LogInformation("[REST] GET /api/metadata");
                var metadata = await archQueryService.GetMetadataAsync();
                return Results.Ok(metadata);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REST] Failed /api/metadata");
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        endpoints.MapGet("/api/nodes", async (IArchitectureQueryService archQueryService, string? kind, int? offset, int? limit, string? search) =>
        {
            try
            {
                logger.LogInformation("[REST] GET /api/nodes (kind: {Kind}, offset: {Offset}, limit: {Limit})", kind ?? "all", offset ?? 0, limit ?? 50);
                var nodes = await archQueryService.GetNodesAsync(kind, offset ?? 0, limit ?? 50, search);
                return Results.Ok(nodes);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REST] Failed /api/nodes");
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        endpoints.MapGet("/api/diagram", async (IGraphClient graphClient, string? type, string? format, string? project, CancellationToken ct) =>
        {
            try
            {
                var diagramType = type ?? "architecture";
                var diagramFormat = format ?? "mermaid";
                logger.LogInformation("[REST] GET /api/diagram (type: {Type}, format: {Format}, project: {Project})", diagramType, diagramFormat, project ?? "all");
                var diagram = await DiagramExporter.ExportAsync(graphClient, diagramFormat, diagramType, project, ct);
                return Results.Text(diagram, "text/plain");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[REST] Failed /api/diagram");
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        return endpoints;
    }
}
