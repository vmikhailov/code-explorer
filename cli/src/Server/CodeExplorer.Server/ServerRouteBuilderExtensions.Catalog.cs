using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Server;

public static partial class ServerRouteBuilderExtensions
{
    private const string RouteCatalogEntryPoints = "/api/catalog/entrypoints";
    private const string RouteCatalogDatabases = "/api/catalog/databases";
    private const string LogFailCatalogEntryPoints = "[REST] Failed /api/catalog/entrypoints";
    private const string LogFailCatalogDatabases = "[REST] Failed /api/catalog/databases";

    private static void MapCatalogApiRoutes(IEndpointRouteBuilder endpoints, ILogger logger)
    {
        endpoints.MapGet(RouteCatalogEntryPoints, async (IGraphClient graphClient, CancellationToken ct) =>
        {
            try
            {
                var engine = new ArchitectureViewEngine(graphClient, logger);
                var items = await engine.GetCatalogEntryPointsAsync(ct);
                return Results.Ok(new { items, count = items.Count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, LogFailCatalogEntryPoints);
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });

        endpoints.MapGet(RouteCatalogDatabases, async (IGraphClient graphClient, CancellationToken ct) =>
        {
            try
            {
                var engine = new ArchitectureViewEngine(graphClient, logger);
                var items = await engine.GetCatalogDatabasesAsync(ct);
                return Results.Ok(new { items, count = items.Count });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, LogFailCatalogDatabases);
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
