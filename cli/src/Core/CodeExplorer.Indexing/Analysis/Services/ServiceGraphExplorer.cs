using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Analysis;

public record ServiceSurfaceDto(
    [property: JsonPropertyName("service")] string Service,
    [property: JsonPropertyName("endpoints")] List<string> Endpoints,
    [property: JsonPropertyName("publishes")] List<string> Publishes,
    [property: JsonPropertyName("subscribes")] List<string> Subscribes
);

public record ServiceDataLineageDto(
    [property: JsonPropertyName("service")] string Service,
    [property: JsonPropertyName("databases")] List<string> Databases,
    [property: JsonPropertyName("tables")] List<string> Tables
);

public record ServiceDependenciesDto(
    [property: JsonPropertyName("service")] string Service,
    [property: JsonPropertyName("referenced_libraries")] List<string> ReferencedLibraries,
    [property: JsonPropertyName("calls_services")] List<string> CallsServices,
    [property: JsonPropertyName("called_by")] List<string> CalledBy
);

public record LibraryEntitiesDto(
    [property: JsonPropertyName("library")] string Library,
    [property: JsonPropertyName("entities")] List<string> Entities
);

public interface IServiceGraphExplorer
{
    Task<ServiceSurfaceDto> GetServiceSurfaceAsync(string serviceName, CancellationToken ct = default);
    Task<ServiceDataLineageDto> GetDataLineageAsync(string serviceName, CancellationToken ct = default);
    Task<ServiceDependenciesDto> GetServiceDependenciesAsync(string serviceName, CancellationToken ct = default);
    Task<LibraryEntitiesDto> GetLibraryEntitiesAsync(string libraryName, CancellationToken ct = default);
}

/// <summary>
/// Deterministic in-memory graph explorer providing fast (&lt; 2ms) architectural micro-queries
/// for local LLM tool calling (ReAct) and graph-native intent distillation.
/// </summary>
public class ServiceGraphExplorer : IServiceGraphExplorer
{
    private readonly IGraphClient _db;

    public ServiceGraphExplorer(IGraphClient db)
    {
        _db = db;
    }

    public async Task<ServiceSurfaceDto> GetServiceSurfaceAsync(string serviceName, CancellationToken ct = default)
    {
        var query = """
            MATCH (s) WHERE (s:Project OR s:Service OR s:Worker OR s:App OR s:Library OR s:CliTool) AND s.name = $name
            OPTIONAL MATCH (ep:Endpoint) WHERE s.path IS NOT NULL AND ep.path STARTS WITH s.path
            OPTIONAL MATCH (s)-[:PUBLISHES_TO|PUBLISHES]->(pub:Topic)
            OPTIONAL MATCH (s)-[:SUBSCRIBES_TO|SUBSCRIBES]->(sub:Topic)
            RETURN s.id AS service_id,
                   collect(DISTINCT ep.name) AS endpoints,
                   collect(DISTINCT pub.name) AS publishes,
                   collect(DISTINCT sub.name) AS subscribes
            """;

        try
        {
            var rows = await _db.QueryAsync<(string ServiceId, List<string> Endpoints, List<string> Publishes, List<string> Subscribes)>(
                query,
                new { name = serviceName },
                ct);

            if (rows.Count > 0)
            {
                var endpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var publishes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var subscribes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    foreach (var ep in row.Endpoints)
                        if (!string.IsNullOrWhiteSpace(ep)) endpoints.Add(ep.Trim());
                    foreach (var pub in row.Publishes)
                        if (!string.IsNullOrWhiteSpace(pub)) publishes.Add(pub.Trim());
                    foreach (var sub in row.Subscribes)
                        if (!string.IsNullOrWhiteSpace(sub)) subscribes.Add(sub.Trim());
                }

                return new ServiceSurfaceDto(serviceName, [.. endpoints], [.. publishes], [.. subscribes]);
            }
        }
        catch
        {
            // Fallback to empty on query error
        }

        return new ServiceSurfaceDto(serviceName, [], [], []);
    }

    public async Task<ServiceDataLineageDto> GetDataLineageAsync(string serviceName, CancellationToken ct = default)
    {
        var query = """
            MATCH (s) WHERE (s:Project OR s:Service OR s:Worker OR s:App OR s:Library OR s:CliTool) AND s.name = $name
            OPTIONAL MATCH (s)-[:USES_DB|TRANSITIVELY_CALLS]->(db:Database)
            OPTIONAL MATCH (s)-[:CONTAINS|USES_DB|ACCESSES_TABLE|READS_FROM|WRITES_TO]->(tb:Table)
            OPTIONAL MATCH (tb2:Table)-[:ACCESSED_BY|QUERIED_BY]->(s)
            OPTIONAL MATCH (tb3:Table) WHERE s.path IS NOT NULL AND tb3.path STARTS WITH s.path
            RETURN s.id AS service_id,
                   collect(DISTINCT db.name) AS databases,
                   collect(DISTINCT coalesce(tb.name, tb2.name, tb3.name)) AS tables
            """;

        try
        {
            var rows = await _db.QueryAsync<(string ServiceId, List<string> Databases, List<string> Tables)>(
                query,
                new { name = serviceName },
                ct);

            if (rows.Count > 0)
            {
                var databases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    foreach (var db in row.Databases)
                        if (!string.IsNullOrWhiteSpace(db)) databases.Add(db.Trim());
                    foreach (var tb in row.Tables)
                        if (!string.IsNullOrWhiteSpace(tb)) tables.Add(tb.Trim());
                }

                return new ServiceDataLineageDto(serviceName, [.. databases], [.. tables]);
            }
        }
        catch
        {
            // Fallback to empty
        }

        return new ServiceDataLineageDto(serviceName, [], []);
    }

    public async Task<ServiceDependenciesDto> GetServiceDependenciesAsync(string serviceName, CancellationToken ct = default)
    {
        var query = """
            MATCH (s) WHERE (s:Project OR s:Service OR s:Worker OR s:App OR s:Library OR s:CliTool) AND s.name = $name
            OPTIONAL MATCH (s)-[r1:DEPENDS_ON]->(lib) WHERE NOT lib:Package AND coalesce(r1.dependency_type, '') != 'service_call' AND (lib:Library OR lib.is_library = true OR lib.role = 'SharedLibrary' OR lib.role = 'Library')
            OPTIONAL MATCH (s)-[r2:SERVICE_CALL|DEPENDS_ON]->(target) WHERE (target:Project OR target:Service OR target:Worker) AND (r2.kind = 'SERVICE_CALL' OR r2.dependency_type = 'service_call')
            OPTIONAL MATCH (caller)-[r3:SERVICE_CALL|DEPENDS_ON]->(s) WHERE (caller:Project OR caller:Service OR caller:Worker) AND (r3.kind = 'SERVICE_CALL' OR r3.dependency_type = 'service_call')
            RETURN s.id AS service_id,
                   collect(DISTINCT lib.name) AS referenced_libraries,
                   collect(DISTINCT target.name) AS calls_services,
                   collect(DISTINCT caller.name) AS called_by
            """;

        try
        {
            var rows = await _db.QueryAsync<(string ServiceId, List<string> ReferencedLibraries, List<string> CallsServices, List<string> CalledBy)>(
                query,
                new { name = serviceName },
                ct);

            if (rows.Count > 0)
            {
                var libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var calls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var calledBy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    foreach (var lib in row.ReferencedLibraries)
                    {
                        var trimmed = lib.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed) && !string.Equals(trimmed, serviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            libs.Add(trimmed);
                        }
                    }
                    foreach (var call in row.CallsServices)
                    {
                        var trimmed = call.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed) && !string.Equals(trimmed, serviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            calls.Add(trimmed);
                        }
                    }
                    foreach (var caller in row.CalledBy)
                    {
                        var trimmed = caller.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmed) && !string.Equals(trimmed, serviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            calledBy.Add(trimmed);
                        }
                    }
                }

                return new ServiceDependenciesDto(serviceName, [.. libs], [.. calls], [.. calledBy]);
            }
        }
        catch
        {
            // Fallback to empty
        }

        return new ServiceDependenciesDto(serviceName, [], [], []);
    }

    public async Task<LibraryEntitiesDto> GetLibraryEntitiesAsync(string libraryName, CancellationToken ct = default)
    {
        var query = """
            MATCH (lib:Project) WHERE lib.name = $library
            OPTIONAL MATCH (item)
            WHERE (item:Type OR item:class OR item:interface OR item:struct OR item:record)
              AND (lib.path IS NOT NULL AND item.path STARTS WITH lib.path)
            RETURN collect(DISTINCT item.name) AS entities
            """;

        try
        {
            var rows = await _db.QueryAsync<List<string>>(query, new { library = libraryName }, ct);
            if (rows.Count > 0)
            {
                var entities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var list in rows)
                {
                    foreach (var raw in list)
                    {
                        var e = raw.Trim();
                        if (!string.IsNullOrWhiteSpace(e) && !e.EndsWith("Tests") && !e.EndsWith("Test") && !e.EndsWith("Fixture"))
                        {
                            entities.Add(e);
                        }
                    }
                }

                return new LibraryEntitiesDto(libraryName, [.. entities.Take(25)]);
            }
        }
        catch
        {
            // Fallback to empty
        }

        return new LibraryEntitiesDto(libraryName, []);
    }
}
