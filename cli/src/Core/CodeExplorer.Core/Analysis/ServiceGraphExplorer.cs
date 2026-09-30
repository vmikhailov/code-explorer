using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;

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
            MATCH (s) WHERE (s:Project OR s:Service OR s:Worker OR s:App) AND s.name = $name
            OPTIONAL MATCH (ep:Endpoint) WHERE s.path IS NOT NULL AND ep.path STARTS WITH s.path
            OPTIONAL MATCH (s)-[:PUBLISHES_TO|PUBLISHES]->(pub:Topic)
            OPTIONAL MATCH (s)-[:SUBSCRIBES_TO|SUBSCRIBES]->(sub:Topic)
            RETURN collect(DISTINCT ep.name) AS endpoints,
                   collect(DISTINCT pub.name) AS publishes,
                   collect(DISTINCT sub.name) AS subscribes
            """;

        try
        {
            var json = await _db.ExecuteQueryAsync(query, new Dictionary<string, object> { ["name"] = serviceName }, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var endpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var publishes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var subscribes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    foreach (var ep in ExtractStringList(row, "endpoints")) endpoints.Add(ep);
                    foreach (var pub in ExtractStringList(row, "publishes")) publishes.Add(pub);
                    foreach (var sub in ExtractStringList(row, "subscribes")) subscribes.Add(sub);
                }

                return new ServiceSurfaceDto(serviceName, endpoints.ToList(), publishes.ToList(), subscribes.ToList());
            }
        }
        catch
        {
            // Fallback to empty on query error
        }

        return new ServiceSurfaceDto(serviceName, new List<string>(), new List<string>(), new List<string>());
    }

    public async Task<ServiceDataLineageDto> GetDataLineageAsync(string serviceName, CancellationToken ct = default)
    {
        var query = """
            MATCH (s) WHERE (s:Project OR s:Service OR s:Worker OR s:App) AND s.name = $name
            OPTIONAL MATCH (s)-[:USES_DB|TRANSITIVELY_CALLS]->(db:Database)
            OPTIONAL MATCH (s)-[:CONTAINS|USES_DB|ACCESSES_TABLE]->(tb:Table)
            OPTIONAL MATCH (tb2:Table)-[:QUERIED_BY]->(s)
            OPTIONAL MATCH (tb3:Table) WHERE s.path IS NOT NULL AND tb3.path STARTS WITH s.path
            RETURN collect(DISTINCT db.name) AS databases,
                   collect(DISTINCT coalesce(tb.name, tb2.name, tb3.name)) AS tables
            """;

        try
        {
            var json = await _db.ExecuteQueryAsync(query, new Dictionary<string, object> { ["name"] = serviceName }, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var databases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    foreach (var db in ExtractStringList(row, "databases")) databases.Add(db);
                    foreach (var tb in ExtractStringList(row, "tables")) tables.Add(tb);
                }

                return new ServiceDataLineageDto(serviceName, databases.ToList(), tables.ToList());
            }
        }
        catch
        {
            // Fallback to empty
        }

        return new ServiceDataLineageDto(serviceName, new List<string>(), new List<string>());
    }

    public async Task<ServiceDependenciesDto> GetServiceDependenciesAsync(string serviceName, CancellationToken ct = default)
    {
        var query = """
            MATCH (s) WHERE (s:Project OR s:Service OR s:Worker OR s:App) AND s.name = $name
            OPTIONAL MATCH (s)-[:DEPENDS_ON]->(lib) WHERE NOT lib:Package
            OPTIONAL MATCH (s)-[:SERVICE_CALL]->(target) WHERE target:Project OR target:Service OR target:Worker
            OPTIONAL MATCH (caller)-[:SERVICE_CALL]->(s) WHERE caller:Project OR caller:Service OR caller:Worker
            RETURN collect(DISTINCT lib.name) AS referenced_libraries,
                   collect(DISTINCT target.name) AS calls_services,
                   collect(DISTINCT caller.name) AS called_by
            """;

        try
        {
            var json = await _db.ExecuteQueryAsync(query, new Dictionary<string, object> { ["name"] = serviceName }, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var calls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var calledBy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    foreach (var lib in ExtractStringList(row, "referenced_libraries"))
                    {
                        if (!string.Equals(lib, serviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            libs.Add(lib);
                        }
                    }
                    foreach (var call in ExtractStringList(row, "calls_services"))
                    {
                        if (!string.Equals(call, serviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            calls.Add(call);
                        }
                    }
                    foreach (var caller in ExtractStringList(row, "called_by"))
                    {
                        if (!string.Equals(caller, serviceName, StringComparison.OrdinalIgnoreCase))
                        {
                            calledBy.Add(caller);
                        }
                    }
                }

                return new ServiceDependenciesDto(serviceName, libs.ToList(), calls.ToList(), calledBy.ToList());
            }
        }
        catch
        {
            // Fallback to empty
        }

        return new ServiceDependenciesDto(serviceName, new List<string>(), new List<string>(), new List<string>());
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
            var json = await _db.ExecuteQueryAsync(query, new Dictionary<string, object> { ["library"] = libraryName }, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var entities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    foreach (var e in ExtractStringList(row, "entities"))
                    {
                        if (!e.EndsWith("Tests") && !e.EndsWith("Test") && !e.EndsWith("Fixture"))
                        {
                            entities.Add(e);
                        }
                    }
                }

                return new LibraryEntitiesDto(libraryName, entities.Take(25).ToList());
            }
        }
        catch
        {
            // Fallback to empty
        }

        return new LibraryEntitiesDto(libraryName, new List<string>());
    }

    private static List<string> ExtractStringList(JsonElement element, string propName)
    {
        var list = new List<string>();
        if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in prop.EnumerateArray())
            {
                var str = item.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    list.Add(str.Trim());
                }
            }
        }
        return list;
    }
}
