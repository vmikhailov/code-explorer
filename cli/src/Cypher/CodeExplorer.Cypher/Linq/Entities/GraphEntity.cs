using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Cypher.Linq.Entities;

public class GraphEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("lineStart")]
    public int? LineStart { get; set; }

    [JsonPropertyName("lineEnd")]
    public int? LineEnd { get; set; }

    [JsonPropertyName("kind")]
    public virtual NodeKind Kind => NodeKind.Unspecified;

    [JsonPropertyName("properties")]
    public Dictionary<string, object?> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public T? GetProperty<T>(string key)
    {
        if (Properties.TryGetValue(key, out var val) && val != null)
        {
            if (val is T direct) return direct;
            if (val is JsonElement je)
            {
                if (typeof(T) == typeof(string)) return (T)(object)je.ToString();
                if (typeof(T) == typeof(bool) && (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False))
                    return (T)(object)je.GetBoolean();
                if (typeof(T) == typeof(int) && je.TryGetInt32(out var i))
                    return (T)(object)i;
            }
            try
            {
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                return (T)Convert.ChangeType(val, targetType);
            }
            catch
            {
                return default;
            }
        }
        return default;
    }
}

public class ServiceEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.Service;

    [JsonPropertyName("framework")]
    public string? Framework
    {
        get => GetProperty<string>("framework");
        set => Properties["framework"] = value;
    }

    [JsonPropertyName("language")]
    public string? Language
    {
        get => GetProperty<string>("language") ?? GetProperty<string>("project_type");
        set => Properties["language"] = value;
    }

    [JsonPropertyName("role")]
    public string? Role
    {
        get => GetProperty<string>("role");
        set => Properties["role"] = value;
    }

    [JsonPropertyName("layer")]
    public string? Layer
    {
        get => GetProperty<string>("layer");
        set => Properties["layer"] = value;
    }

    [JsonPropertyName("port")]
    public int? Port
    {
        get => GetProperty<int?>("port");
        set => Properties["port"] = value;
    }

    [JsonPropertyName("isLibrary")]
    public bool IsLibrary
    {
        get => GetProperty<bool>("is_library") || GetProperty<string>("is_library") == "true";
        set => Properties["is_library"] = value;
    }
}

public class AppEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.App;

    [JsonPropertyName("framework")]
    public string? Framework
    {
        get => GetProperty<string>("framework");
        set => Properties["framework"] = value;
    }

    [JsonPropertyName("language")]
    public string? Language
    {
        get => GetProperty<string>("language") ?? GetProperty<string>("project_type");
        set => Properties["language"] = value;
    }

    [JsonPropertyName("appType")]
    public string? AppType
    {
        get => GetProperty<string>("app_type");
        set => Properties["app_type"] = value;
    }

    [JsonPropertyName("port")]
    public int? Port
    {
        get => GetProperty<int?>("port");
        set => Properties["port"] = value;
    }
}

public class WorkerEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.Worker;

    [JsonPropertyName("framework")]
    public string? Framework
    {
        get => GetProperty<string>("framework");
        set => Properties["framework"] = value;
    }

    [JsonPropertyName("language")]
    public string? Language
    {
        get => GetProperty<string>("language") ?? GetProperty<string>("project_type");
        set => Properties["language"] = value;
    }

    [JsonPropertyName("queueType")]
    public string? QueueType
    {
        get => GetProperty<string>("queue_type");
        set => Properties["queue_type"] = value;
    }
}

public class CliToolEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.CliTool;

    [JsonPropertyName("commandName")]
    public string? CommandName
    {
        get => GetProperty<string>("command_name");
        set => Properties["command_name"] = value;
    }

    [JsonPropertyName("language")]
    public string? Language
    {
        get => GetProperty<string>("language") ?? GetProperty<string>("project_type");
        set => Properties["language"] = value;
    }
}

public class DatabaseEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.Database;

    [JsonPropertyName("dbType")]
    public string? DbType
    {
        get => GetProperty<string>("db_type");
        set => Properties["db_type"] = value;
    }

    [JsonPropertyName("engine")]
    public string? Engine
    {
        get => GetProperty<string>("engine");
        set => Properties["engine"] = value;
    }

    [JsonPropertyName("schema")]
    public string? Schema
    {
        get => GetProperty<string>("schema");
        set => Properties["schema"] = value;
    }

    [JsonPropertyName("port")]
    public int? Port
    {
        get => GetProperty<int?>("port");
        set => Properties["port"] = value;
    }
}

public class TopicEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.Topic;

    [JsonPropertyName("brokerType")]
    public string? BrokerType
    {
        get => GetProperty<string>("broker_type");
        set => Properties["broker_type"] = value;
    }

    [JsonPropertyName("partitionCount")]
    public int? PartitionCount
    {
        get => GetProperty<int?>("partition_count");
        set => Properties["partition_count"] = value;
    }

    [JsonPropertyName("isInternal")]
    public bool IsInternal
    {
        get => GetProperty<bool>("is_internal") || GetProperty<string>("is_internal") == "true";
        set => Properties["is_internal"] = value;
    }
}

public class ExternalServiceEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.ExternalService;

    [JsonPropertyName("protocol")]
    public string? Protocol
    {
        get => GetProperty<string>("protocol");
        set => Properties["protocol"] = value;
    }

    [JsonPropertyName("domainOrService")]
    public string? DomainOrService
    {
        get => GetProperty<string>("domain_or_service");
        set => Properties["domain_or_service"] = value;
    }

    [JsonPropertyName("baseUrl")]
    public string? BaseUrl
    {
        get => GetProperty<string>("base_url");
        set => Properties["base_url"] = value;
    }

    [JsonPropertyName("category")]
    public string? Category
    {
        get => GetProperty<string>("category");
        set => Properties["category"] = value;
    }

    [JsonPropertyName("isExternal")]
    public bool IsExternal
    {
        get => GetProperty<bool>("is_external") || GetProperty<string>("is_external") == "true";
        set => Properties["is_external"] = value;
    }
}

public class LibraryEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.Library;

    [JsonPropertyName("language")]
    public string? Language
    {
        get => GetProperty<string>("language") ?? GetProperty<string>("project_type");
        set => Properties["language"] = value;
    }

    [JsonPropertyName("packageName")]
    public string? PackageName
    {
        get => GetProperty<string>("package_name");
        set => Properties["package_name"] = value;
    }

    [JsonPropertyName("version")]
    public string? Version
    {
        get => GetProperty<string>("version");
        set => Properties["version"] = value;
    }

    [JsonPropertyName("projectType")]
    public string? ProjectType
    {
        get => GetProperty<string>("project_type");
        set => Properties["project_type"] = value;
    }

    [JsonPropertyName("isLibrary")]
    public bool IsLibrary
    {
        get => GetProperty<bool>("is_library") || GetProperty<string>("is_library") == "true";
        set => Properties["is_library"] = value;
    }
}

public class EndpointEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.Endpoint;

    [JsonPropertyName("route")]
    public string? Route
    {
        get => GetProperty<string>("route") ?? RouteTemplate;
        set => Properties["route"] = value;
    }

    [JsonPropertyName("routeTemplate")]
    public string? RouteTemplate
    {
        get => GetProperty<string>("route_template") ?? GetProperty<string>("path");
        set => Properties["route_template"] = value;
    }

    [JsonPropertyName("httpMethod")]
    public string? HttpMethod
    {
        get => GetProperty<string>("http_method");
        set => Properties["http_method"] = value;
    }

    [JsonPropertyName("protocol")]
    public string? Protocol
    {
        get => GetProperty<string>("protocol");
        set => Properties["protocol"] = value;
    }

    [JsonPropertyName("port")]
    public int? Port
    {
        get => GetProperty<int?>("port");
        set => Properties["port"] = value;
    }
}
