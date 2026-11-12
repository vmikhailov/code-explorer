using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Server;

public static partial class ServerRouteBuilderExtensions
{
    private static void MapOntologySchemaRoute(
        IEndpointRouteBuilder endpoints,
        ILogger logger)
    {
        const string routePath = "/api/ontology";
        const string logStart = "[REST] GET /api/ontology";
        const string logFail = "[REST] Failed /api/ontology";

        endpoints.MapGet(routePath, async (IArchitectureQueryService archQueryService, CancellationToken ct) =>
        {
            try
            {
                logger.LogInformation(logStart);
                var schema = await archQueryService.GetOntologySchemaAsync(ct);
                return Results.Ok(schema);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, logFail);
                return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
            }
        });
    }
}
