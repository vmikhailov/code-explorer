using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

/// <summary>
/// Domain-level ontology graph repository interface providing indexed access to nodes and relationships
/// without coupling to physical database storage drivers (SQLite, ClickHouse, In-Memory).
/// </summary>
public interface IGraphRepository : IAsyncDisposable
{
    ValueTask<Node?> GetNodeByIdAsync(string id, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Node>> GetNodesByKindAsync(string kind, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Node>> GetNodesByKindAsync(NodeKind kind, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Node>> GetAllNodesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<Relationship>> GetAllRelationshipsAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsAsync(string fromNodeId, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsAsync(string fromNodeId, string relationshipKind, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsAsync(string fromNodeId, RelationshipKind relationshipKind, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsAsync(string toNodeId, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsAsync(string toNodeId, string relationshipKind, CancellationToken ct = default);
    ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsAsync(string toNodeId, RelationshipKind relationshipKind, CancellationToken ct = default);

    Task UpsertNodesAsync(IEnumerable<Node> nodes, CancellationToken ct = default);
    Task UpsertRelationshipsAsync(IEnumerable<Relationship> relationships, CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);

    Task<Dictionary<(string Kind, string Name), string>> LoadSymbolsByNamesAsync(
        IEnumerable<string> names,
        string workspaceId,
        CancellationToken ct = default);

    Task<Dictionary<string, List<string>>> LoadImplementationsForTypesAsync(
        IEnumerable<string> typeNames,
        string workspaceId,
        CancellationToken ct = default);

    Task<(List<EndpointNode> Endpoints, List<EntryPointNode> EntryPoints, List<ExternalServiceNode> ExternalServices, List<ProjectNode> Projects)> LoadLateBindingCandidatesAsync(
        string workspaceId,
        bool needEndpoints,
        bool needExternalServices,
        CancellationToken ct = default);
}
