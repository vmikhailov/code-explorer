using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CodeExplorer.Server;

public static partial class ServerRouteBuilderExtensions
{
    private const string RouteApiContext = "/api/context";
    private const string RouteApiGatesCheck = "/api/gates/check";
    private const string ContentTypePlainUtf8 = "text/plain; charset=utf-8";

    internal static void MapAgentContextRoutes(this IEndpointRouteBuilder endpoints, string wsRoot)
    {
        endpoints.MapGet(RouteApiContext, async (
            CodeExplorerRepository repository,
            string? q,
            int? maxAnchors,
            bool? includeGates,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest("Query parameter 'q' is required.");
            }

            var yaml = await repository.GetTaskContextAsync(
                q,
                maxAnchors ?? 8,
                includeGates ?? true,
                wsRoot,
                ct);
            return Results.Text(yaml, ContentTypePlainUtf8);
        });

        endpoints.MapGet(RouteApiGatesCheck, async (
            CodeExplorerRepository repository,
            string? gateId,
            bool? includeTolerated,
            string? fileFilter,
            CancellationToken ct) =>
        {
            var yaml = await repository.CheckSemanticGatesAsync(
                gateId: gateId,
                includeTolerated: includeTolerated ?? false,
                fileFilter: fileFilter,
                workspacePath: wsRoot,
                cancellationToken: ct);
            return Results.Text(yaml, ContentTypePlainUtf8);
        });
    }
}
