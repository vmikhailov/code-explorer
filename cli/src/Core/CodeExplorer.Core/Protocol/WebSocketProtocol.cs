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
    public const string GetViewRequest = "GET_VIEW_REQUEST";
    public const string GetSymbolNeighborhoodRequest = "GET_SYMBOL_NEIGHBORHOOD_REQUEST";
    public const string GetCallChainRequest = "GET_CALL_CHAIN_REQUEST";
    public const string GetImpactRequest = "GET_IMPACT_REQUEST";
    public const string ExecuteCypherRequest = "EXECUTE_CYPHER_REQUEST";
    public const string TriggerScanRequest = "TRIGGER_SCAN_REQUEST";
    public const string GetOntologyLayersRequest = "GET_ONTOLOGY_LAYERS_REQUEST";

    // Server -> Client responses
    public const string HandshakeResponse = "HANDSHAKE_RESPONSE";
    public const string PongResponse = "PONG_RESPONSE";
    public const string QueryResponse = "QUERY_RESPONSE";
    public const string GetViewResponse = "GET_VIEW_RESPONSE";
    public const string GetOntologyLayersResponse = "GET_ONTOLOGY_LAYERS_RESPONSE";
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

public class GetViewRequestDto
{
    [JsonPropertyName("view")]
    public string View { get; set; } = "SystemContext"; // "SystemContext", "ServiceFlow", "Component"

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    [JsonPropertyName("includeLibraries")]
    public bool IncludeLibraries { get; set; } = false;
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

public class MetadataResponseDto
{
    [JsonPropertyName("nodeCounts")]
    public Dictionary<string, long> NodeCounts { get; set; } = [];

    [JsonPropertyName("relationshipCounts")]
    public Dictionary<string, long> RelationshipCounts { get; set; } = [];

    [JsonPropertyName("totalNodes")]
    public long TotalNodes { get; set; }

    [JsonPropertyName("totalEdges")]
    public long TotalEdges { get; set; }
}

public class NodesResponseDto
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("nodes")]
    public List<GraphNodeDto> Nodes { get; set; } = [];

    [JsonPropertyName("total")]
    public long Total { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }
}

// ============================================================================
// Ontology Layers DTOs (for Tree & Layer Browsing)
// ============================================================================

public class OntologyCategoryDto
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public long Count { get; set; }

    [JsonPropertyName("layerId")]
    public int LayerId { get; set; }
}

public class OntologyLayerDto
{
    [JsonPropertyName("layerId")]
    public int LayerId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("totalCount")]
    public long TotalCount { get; set; }

    [JsonPropertyName("categories")]
    public List<OntologyCategoryDto> Categories { get; set; } = [];
}

public class OntologyLayersResponseDto
{
    [JsonPropertyName("layers")]
    public List<OntologyLayerDto> Layers { get; set; } = [];

    [JsonPropertyName("totalNodes")]
    public long TotalNodes { get; set; }

    [JsonPropertyName("totalEdges")]
    public long TotalEdges { get; set; }
}

// ============================================================================
// Domain Architecture DTOs (Service Map & Bounded Contexts)
// ============================================================================

public class DomainProjectInfoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kind { get; set; }

    [JsonPropertyName("filePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePath { get; set; }

    [JsonPropertyName("isLibrary")]
    public bool IsLibrary { get; set; }
}

public class DomainEntityDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "Service";

    [JsonPropertyName("displayTag")]
    public string DisplayTag { get; set; } = ":Service";

    [JsonPropertyName("zone")]
    public string Zone { get; set; } = "service";

    [JsonPropertyName("bgColor")]
    public string BgColor { get; set; } = "#e53935";

    [JsonPropertyName("borderColor")]
    public string BorderColor { get; set; } = "#7f1d1d";

    [JsonPropertyName("size")]
    public int Size { get; set; } = 50;

    [JsonPropertyName("framework")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Framework { get; set; }

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    [JsonPropertyName("primaryFilePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrimaryFilePath { get; set; }

    [JsonPropertyName("projects")]
    public List<DomainProjectInfoDto> Projects { get; set; } = [];

    [JsonPropertyName("inboundCallsCount")]
    public int InboundCallsCount { get; set; }

    [JsonPropertyName("outboundCallsCount")]
    public int OutboundCallsCount { get; set; }

    [JsonPropertyName("dbCount")]
    public int DbCount { get; set; }

    [JsonPropertyName("messagingCount")]
    public int MessagingCount { get; set; }
}

public class DomainMacroEdgeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "service_call";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "CALLS";

    [JsonPropertyName("count")]
    public int Count { get; set; } = 1;
}

public class DomainStatsDto
{
    [JsonPropertyName("totalDomains")]
    public int TotalDomains { get; set; }

    [JsonPropertyName("ingress")]
    public int Ingress { get; set; }

    [JsonPropertyName("services")]
    public int Services { get; set; }

    [JsonPropertyName("workers")]
    public int Workers { get; set; }

    [JsonPropertyName("libraries")]
    public int Libraries { get; set; }

    [JsonPropertyName("databases")]
    public int Databases { get; set; }

    [JsonPropertyName("topics")]
    public int Topics { get; set; }

    [JsonPropertyName("external")]
    public int External { get; set; }

    [JsonPropertyName("serviceCalls")]
    public int ServiceCalls { get; set; }

    [JsonPropertyName("messages")]
    public int Messages { get; set; }
}

public class DomainArchitectureDto
{
    [JsonPropertyName("nodes")]
    public List<DomainEntityDto> Nodes { get; set; } = [];

    [JsonPropertyName("edges")]
    public List<DomainMacroEdgeDto> Edges { get; set; } = [];

    [JsonPropertyName("stats")]
    public DomainStatsDto Stats { get; set; } = new();
}

// ============================================================================
// Service Contracts & Execution Flow Tracing DTOs
// ============================================================================

public class ServiceContractDto
{
    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "Service";

    [JsonPropertyName("framework")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Framework { get; set; }

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    [JsonPropertyName("ingressEndpoints")]
    public List<string> IngressEndpoints { get; set; } = [];

    [JsonPropertyName("subscribedTopics")]
    public List<string> SubscribedTopics { get; set; } = [];

    [JsonPropertyName("outboundServiceCalls")]
    public List<string> OutboundServiceCalls { get; set; } = [];

    [JsonPropertyName("publishedTopics")]
    public List<string> PublishedTopics { get; set; } = [];

    [JsonPropertyName("databases")]
    public List<string> Databases { get; set; } = [];

    [JsonPropertyName("externalServices")]
    public List<string> ExternalServices { get; set; } = [];
}

public class CrossServiceHopDto
{
    [JsonPropertyName("step")]
    public int Step { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Protocol { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; set; }
}

public class CrossServiceFlowDto
{
    [JsonPropertyName("startService")]
    public string StartService { get; set; } = string.Empty;

    [JsonPropertyName("entryPoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EntryPoint { get; set; }

    [JsonPropertyName("maxDepth")]
    public int MaxDepth { get; set; }

    [JsonPropertyName("hops")]
    public List<CrossServiceHopDto> Hops { get; set; } = [];

    [JsonPropertyName("visitedServices")]
    public List<string> VisitedServices { get; set; } = [];
}

public class ServiceSummaryDto
{
    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "Service";

    [JsonPropertyName("framework")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Framework { get; set; }

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    [JsonPropertyName("endpointCount")]
    public int EndpointCount { get; set; }

    [JsonPropertyName("databaseCount")]
    public int DatabaseCount { get; set; }

    [JsonPropertyName("topicCount")]
    public int TopicCount { get; set; }

    [JsonPropertyName("externalCount")]
    public int ExternalCount { get; set; }
}

public class ServiceCapabilityItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("protocol")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Protocol { get; set; }

    [JsonPropertyName("method")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Method { get; set; }

    [JsonPropertyName("route")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Route { get; set; }

    [JsonPropertyName("filePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FilePath { get; set; }

    [JsonPropertyName("line")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Line { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Details { get; set; }
}

public class ServiceOntologyGroupDto
{
    [JsonPropertyName("categoryKey")]
    public string CategoryKey { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("items")]
    public List<ServiceCapabilityItemDto> Items { get; set; } = [];
}

public class ServiceOntologyDetailsDto
{
    [JsonPropertyName("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    [JsonPropertyName("serviceId")]
    public string ServiceId { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "Service";

    [JsonPropertyName("framework")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Framework { get; set; }

    [JsonPropertyName("language")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Language { get; set; }

    [JsonPropertyName("groups")]
    public List<ServiceOntologyGroupDto> Groups { get; set; } = [];
}


