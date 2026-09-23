using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Protocol;

public static class WsMessageTypes
{
    // Client -> Server
    public const string HandshakeRequest = "HANDSHAKE_REQUEST";
    public const string PingRequest = "PING_REQUEST";
    public const string GetArchitectureRequest = "GET_ARCHITECTURE_REQUEST";
    public const string GetDependenciesRequest = "GET_DEPENDENCIES_REQUEST";
    public const string GetSymbolNeighborhoodRequest = "GET_SYMBOL_NEIGHBORHOOD_REQUEST";
    public const string GetCallChainRequest = "GET_CALL_CHAIN_REQUEST";
    public const string GetImpactRequest = "GET_IMPACT_REQUEST";
    public const string ExecuteCypherRequest = "EXECUTE_CYPHER_REQUEST";
    public const string TriggerScanRequest = "TRIGGER_SCAN_REQUEST";

    // Server -> Client responses
    public const string HandshakeResponse = "HANDSHAKE_RESPONSE";
    public const string PongResponse = "PONG_RESPONSE";
    public const string QueryResponse = "QUERY_RESPONSE";
    public const string ErrorResponse = "ERROR_RESPONSE";

    // Server -> Client broadcast events
    public const string GraphPatchEvent = "GRAPH_PATCH_EVENT";
    public const string ScanProgressEvent = "SCAN_PROGRESS_EVENT";
}

public class WsEnvelope
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }
}

public class WsEnvelope<T>
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public T? Payload { get; set; }
}

// ============================================================================
// Graph DTOs
// ============================================================================

public class GraphNodeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; set; }

    [JsonPropertyName("filePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePath { get; set; }

    [JsonPropertyName("lineStart")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LineStart { get; set; }

    [JsonPropertyName("lineEnd")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LineEnd { get; set; }

    [JsonPropertyName("parentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ParentId { get; set; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Properties { get; set; }
}

public class GraphEdgeDto
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; set; }

    [JsonPropertyName("properties")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Properties { get; set; }
}

public class GraphDataDto
{
    [JsonPropertyName("nodes")]
    public List<GraphNodeDto> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<GraphEdgeDto> Edges { get; set; } = [];

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Metadata { get; set; }
}

// ============================================================================
// Request Payloads
// ============================================================================

public class HandshakeRequestDto
{
    [JsonPropertyName("clientVersion")]
    public string ClientVersion { get; set; } = string.Empty;

    [JsonPropertyName("clientName")]
    public string? ClientName { get; set; }

    [JsonPropertyName("workspacePath")]
    public string? WorkspacePath { get; set; }
}

public class PingRequestDto
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

public class GetArchitectureRequestDto
{
    [JsonPropertyName("projectFilter")]
    public string? ProjectFilter { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; } // "graph", "mermaid", "c4"
}

public class GetDependenciesRequestDto
{
    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("includeIncoming")]
    public bool IncludeIncoming { get; set; } = true;

    [JsonPropertyName("includeOutgoing")]
    public bool IncludeOutgoing { get; set; } = true;
}

public class GetSymbolNeighborhoodRequestDto
{
    [JsonPropertyName("symbolName")]
    public string SymbolName { get; set; } = string.Empty;

    [JsonPropertyName("depth")]
    public int Depth { get; set; } = 1;
}

public class GetCallChainRequestDto
{
    [JsonPropertyName("fromSymbol")]
    public string FromSymbol { get; set; } = string.Empty;

    [JsonPropertyName("toSymbol")]
    public string? ToSymbol { get; set; }

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; set; } = 5;
}

public class GetImpactRequestDto
{
    [JsonPropertyName("symbolName")]
    public string SymbolName { get; set; } = string.Empty;
}

public class ExecuteCypherRequestDto
{
    [JsonPropertyName("query")]
    public string Query { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object?>? Parameters { get; set; }
}

public class TriggerScanRequestDto
{
    [JsonPropertyName("targetPath")]
    public string? TargetPath { get; set; }

    [JsonPropertyName("clear")]
    public bool Clear { get; set; }
}

// ============================================================================
// Response Payloads
// ============================================================================

public class HandshakeResponseDto
{
    [JsonPropertyName("serverVersion")]
    public string ServerVersion { get; set; } = string.Empty;

    [JsonPropertyName("workspaceRoot")]
    public string WorkspaceRoot { get; set; } = string.Empty;

    [JsonPropertyName("dbPath")]
    public string DbPath { get; set; } = string.Empty;

    [JsonPropertyName("totalNodes")]
    public long TotalNodes { get; set; }

    [JsonPropertyName("totalEdges")]
    public long TotalEdges { get; set; }

    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; set; } = [];

    [JsonPropertyName("isSchemaOutdated")]
    public bool IsSchemaOutdated { get; set; }

    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; }

    [JsonPropertyName("currentSchemaVersion")]
    public int CurrentSchemaVersion { get; set; }

    [JsonPropertyName("isScanning")]
    public bool IsScanning { get; set; }

    [JsonPropertyName("scanProgress")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScanProgressEventDto? ScanProgress { get; set; }
}

public class PongResponseDto
{
    [JsonPropertyName("clientTimestamp")]
    public long ClientTimestamp { get; set; }

    [JsonPropertyName("serverTimestamp")]
    public long ServerTimestamp { get; set; }
}

public class QueryResponseDto
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("graph")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GraphDataDto? Graph { get; set; }

    [JsonPropertyName("rawJson")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RawJson { get; set; }
}

public class ErrorResponseDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "ERROR";

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; set; }
}

public class GraphPatchEventDto
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("addedNodes")]
    public List<GraphNodeDto> AddedNodes { get; set; } = [];

    [JsonPropertyName("updatedNodes")]
    public List<GraphNodeDto> UpdatedNodes { get; set; } = [];

    [JsonPropertyName("removedNodeIds")]
    public List<string> RemovedNodeIds { get; set; } = [];

    [JsonPropertyName("addedEdges")]
    public List<GraphEdgeDto> AddedEdges { get; set; } = [];

    [JsonPropertyName("removedEdgeIds")]
    public List<string> RemovedEdgeIds { get; set; } = [];
}

public class ScanProgressEventDto
{
    [JsonPropertyName("phase")]
    public string Phase { get; set; } = string.Empty;

    [JsonPropertyName("percentage")]
    public double Percentage { get; set; }

    [JsonPropertyName("currentFile")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CurrentFile { get; set; }

    [JsonPropertyName("totalFiles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TotalFiles { get; set; }

    [JsonPropertyName("processedFiles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ProcessedFiles { get; set; }
}
