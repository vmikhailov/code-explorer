using System.Buffers.Text;
using System.Text;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Cypher.Linq.Entities;

public class GraphEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? FilePath { get; set; }
    public int? LineStart { get; set; }
    public int? LineEnd { get; set; }
    public virtual NodeKind Kind => NodeKind.Unspecified;

    public virtual bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("name"u8))
        {
            Name = ReadString(valSpan) ?? string.Empty;
            return true;
        }
        if (propName.SequenceEqual("display_name"u8) || propName.SequenceEqual("displayName"u8))
        {
            DisplayName = ReadString(valSpan);
            return true;
        }
        if (propName.SequenceEqual("file_path"u8) || propName.SequenceEqual("filePath"u8))
        {
            FilePath = ReadString(valSpan);
            return true;
        }
        if (propName.SequenceEqual("path"u8))
        {
            FilePath ??= ReadString(valSpan);
            return true;
        }
        if (propName.SequenceEqual("line_start"u8) || propName.SequenceEqual("lineStart"u8) || propName.SequenceEqual("start_line"u8))
        {
            LineStart = ReadNullableInt32(valSpan);
            return true;
        }
        if (propName.SequenceEqual("line_end"u8) || propName.SequenceEqual("lineEnd"u8) || propName.SequenceEqual("end_line"u8))
        {
            LineEnd = ReadNullableInt32(valSpan);
            return true;
        }
        return false;
    }

    protected static string? ReadString(ReadOnlySpan<byte> valSpan) =>
        valSpan.IsEmpty ? string.Empty : Encoding.UTF8.GetString(valSpan);

    protected static int? ReadNullableInt32(ReadOnlySpan<byte> valSpan) =>
        Utf8Parser.TryParse(valSpan, out int parsed, out _) ? parsed : null;

    protected static bool ReadBoolean(int valType, ReadOnlySpan<byte> valSpan)
    {
        if (valType == 1) return true;  // JSONB_TRUE
        if (valType == 2) return false; // JSONB_FALSE
        if (!valSpan.IsEmpty)
        {
            return valSpan.SequenceEqual("true"u8) || valSpan.SequenceEqual("True"u8) || valSpan.SequenceEqual("1"u8);
        }
        return false;
    }
}

public class ServiceEntity : GraphEntity
{
    private string? _language;
    private string? _projectType;

    public override NodeKind Kind => NodeKind.Service;
    public string? Framework { get; set; }

    public string? Language
    {
        get => _language ?? _projectType;
        set => _language = value;
    }

    public string? Role { get; set; }
    public string? Layer { get; set; }
    public int? Port { get; set; }
    public bool IsLibrary { get; set; }

    public override bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("framework"u8)) { Framework = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("language"u8)) { _language = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("project_type"u8)) { _projectType = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("role"u8)) { Role = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("layer"u8)) { Layer = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("port"u8)) { Port = ReadNullableInt32(valSpan); return true; }
        if (propName.SequenceEqual("is_library"u8) || propName.SequenceEqual("isLibrary"u8)) { IsLibrary = ReadBoolean(valType, valSpan); return true; }
        return base.TryReadJsonbProperty(propName, valType, valSpan);
    }
}

public class AppEntity : GraphEntity
{
    private string? _language;
    private string? _projectType;

    public override NodeKind Kind => NodeKind.App;
    public string? Framework { get; set; }

    public string? Language
    {
        get => _language ?? _projectType;
        set => _language = value;
    }

    public string? AppType { get; set; }
    public int? Port { get; set; }

    public override bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("framework"u8)) { Framework = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("language"u8)) { _language = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("project_type"u8)) { _projectType = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("app_type"u8) || propName.SequenceEqual("appType"u8)) { AppType = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("port"u8)) { Port = ReadNullableInt32(valSpan); return true; }
        return base.TryReadJsonbProperty(propName, valType, valSpan);
    }
}

public class WorkerEntity : GraphEntity
{
    private string? _language;
    private string? _projectType;

    public override NodeKind Kind => NodeKind.Worker;
    public string? Framework { get; set; }

    public string? Language
    {
        get => _language ?? _projectType;
        set => _language = value;
    }

    public string? QueueType { get; set; }

    public override bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("framework"u8)) { Framework = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("language"u8)) { _language = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("project_type"u8)) { _projectType = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("queue_type"u8) || propName.SequenceEqual("queueType"u8)) { QueueType = ReadString(valSpan); return true; }
        return base.TryReadJsonbProperty(propName, valType, valSpan);
    }
}

public class CliToolEntity : GraphEntity
{
    private string? _language;
    private string? _projectType;

    public override NodeKind Kind => NodeKind.CliTool;
    public string? CommandName { get; set; }

    public string? Language
    {
        get => _language ?? _projectType;
        set => _language = value;
    }

    public override bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("command_name"u8) || propName.SequenceEqual("commandName"u8)) { CommandName = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("language"u8)) { _language = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("project_type"u8)) { _projectType = ReadString(valSpan); return true; }
        return base.TryReadJsonbProperty(propName, valType, valSpan);
    }
}

public class DatabaseEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.Database;
    public string? DbType { get; set; }
    public string? Engine { get; set; }
    public int? Port { get; set; }

    public override bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("db_type"u8) || propName.SequenceEqual("dbType"u8)) { DbType = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("engine"u8)) { Engine = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("port"u8)) { Port = ReadNullableInt32(valSpan); return true; }
        return base.TryReadJsonbProperty(propName, valType, valSpan);
    }
}

public class TopicEntity : GraphEntity
{
    public override NodeKind Kind => NodeKind.Topic;
    public string? BrokerType { get; set; }
    public int? PartitionCount { get; set; }

    public override bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("broker_type"u8) || propName.SequenceEqual("brokerType"u8)) { BrokerType = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("partition_count"u8) || propName.SequenceEqual("partitionCount"u8)) { PartitionCount = ReadNullableInt32(valSpan); return true; }
        return base.TryReadJsonbProperty(propName, valType, valSpan);
    }
}

public class ExternalServiceEntity : GraphEntity
{
    private string? _baseUrl;
    private string? _domainOrService;

    public override NodeKind Kind => NodeKind.ExternalService;
    public string? Protocol { get; set; }

    public string? BaseUrl
    {
        get => _baseUrl ?? _domainOrService;
        set => _baseUrl = value;
    }

    public string? Category { get; set; }

    public override bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("protocol"u8)) { Protocol = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("base_url"u8) || propName.SequenceEqual("baseUrl"u8)) { _baseUrl = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("domain_or_service"u8)) { _domainOrService = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("category"u8)) { Category = ReadString(valSpan); return true; }
        return base.TryReadJsonbProperty(propName, valType, valSpan);
    }
}

public class LibraryEntity : GraphEntity
{
    private string? _packageName;
    private string? _language;
    private string? _projectType;

    public override NodeKind Kind => NodeKind.Library;

    public string? PackageName
    {
        get => _packageName ?? Name;
        set => _packageName = value;
    }

    public string? Version { get; set; }

    public string? Language
    {
        get => _language ?? _projectType;
        set => _language = value;
    }

    public bool IsLibrary { get; set; } = true;

    public override bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("package_name"u8) || propName.SequenceEqual("packageName"u8)) { _packageName = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("version"u8)) { Version = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("language"u8)) { _language = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("project_type"u8)) { _projectType = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("is_library"u8) || propName.SequenceEqual("isLibrary"u8)) { IsLibrary = ReadBoolean(valType, valSpan); return true; }
        return base.TryReadJsonbProperty(propName, valType, valSpan);
    }
}

public class EndpointEntity : GraphEntity
{
    private string? _route;
    private string? _routeTemplate;

    public override NodeKind Kind => NodeKind.Endpoint;

    public string? Route
    {
        get => _route ?? _routeTemplate ?? FilePath;
        set => _route = value;
    }

    public string? HttpMethod { get; set; }
    public string? Protocol { get; set; }
    public int? Port { get; set; }

    public override bool TryReadJsonbProperty(ReadOnlySpan<byte> propName, int valType, ReadOnlySpan<byte> valSpan)
    {
        if (propName.SequenceEqual("route"u8)) { _route = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("route_template"u8)) { _routeTemplate = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("http_method"u8) || propName.SequenceEqual("httpMethod"u8)) { HttpMethod = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("protocol"u8)) { Protocol = ReadString(valSpan); return true; }
        if (propName.SequenceEqual("port"u8)) { Port = ReadNullableInt32(valSpan); return true; }
        return base.TryReadJsonbProperty(propName, valType, valSpan);
    }
}
