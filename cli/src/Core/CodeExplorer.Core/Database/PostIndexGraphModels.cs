using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

public record PostIndexGraphData(
    Dictionary<string, List<string>> CallsAdjacency,
    Dictionary<string, string> Sinks,
    Dictionary<string, string?> SinkDomains,
    List<string> Callers,
    Dictionary<string, List<string>> Implements,
    List<string> EntryPointIds,
    Dictionary<string, List<string>> ProjectToEntryPoints
);

public record PostIndexAnalysisResult(
    List<(string From, string To, int Hops)> TransitivelyCalls,
    List<(string EpId, string SinkId, int Hops, string SinkKind)> AttributedTo,
    Dictionary<string, List<string>> ProjectExternalApis
);

public record MacroEdgeSnapshot(
    string Source,
    string Target,
    RelationshipKind Kind,
    Dictionary<string, string> Properties
);

public record EdgeNormalizationSnapshot(
    Dictionary<string, (NodeKind Kind, bool IsLibrary)> NodeMap,
    List<MacroEdgeSnapshot> Edges
);

public record EdgeNormalizationUpdate(
    RelationshipKind NormalizedKind,
    string PropsJson,
    string Source,
    string Target,
    RelationshipKind RawKind
);

public record EndpointCandidateSnapshot(
    string Id,
    string Name,
    string Route,
    string Method,
    string Path
);

public record ExternalServiceCandidateSnapshot(
    string Id,
    string Name,
    string Domain,
    string Path,
    string InferredFromPath,
    string IsRelativePath,
    string IsExternal,
    string InferredFromIdentifier
);

public record ExternalServicePurgeSnapshot(
    List<(string Id, string Name, string? Path)> Projects,
    List<EndpointCandidateSnapshot> Endpoints,
    HashSet<string> ServicesCallingEndpoints,
    List<ExternalServiceCandidateSnapshot> ExternalServices
);

public record DbProjectSnapshot(
    string Id,
    string Name,
    string? Path,
    string Role,
    string IsTestProp,
    string EntityRole
);

public record DbAccessEdgeSnapshot(
    string Source,
    string Target,
    RelationshipKind Kind,
    string SrcRole,
    string SrcPath,
    string SrcIsTest,
    string TgtRole,
    string TgtPath,
    string TgtIsTest
);

public record RawDatabaseNodeSnapshot(
    string Id,
    string Name,
    string DbClassification,
    string? Engine,
    string? Schema,
    string? Path,
    bool IsTest,
    string Role,
    bool IsImplicitSchema,
    bool IsExplicitSchema,
    bool FromConfig
);

public record DomainEdgeTargetRemap(
    string LoserId,
    string WinnerId,
    string Prefix
);

public record CanonicalDatabasePropagationInfo(
    string Id,
    string Name,
    string Engine
);
