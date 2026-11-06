using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Cypher.Common;
using CodeExplorer.Cypher.Linq;

namespace CodeExplorer.Core.Database;

/// <summary>
/// Persistent graph repository backed by an underlying <see cref="IGraphClient"/> storage driver (e.g. SQLite, ClickHouse).
/// </summary>
public class PersistedGraphRepository(IGraphClient client) : IGraphRepository
{
    private readonly IGraphClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public IGraphClient Client => _client;

    public async ValueTask<WorkspaceNode?> GetWorkspaceAsync(CancellationToken ct = default)
    {
        try
        {
            var wsEntity = await _client.Graph.Workspaces.FirstOrDefaultAsync(ct);
            if (wsEntity is not null && !string.IsNullOrEmpty(wsEntity.Id))
            {
                return new WorkspaceNode(wsEntity.Id, wsEntity.Name, wsEntity.FilePath ?? string.Empty)
                {
                    IndexedAt = wsEntity.IndexedAt
                };
            }
        }
        catch (NotSupportedException)
        {
            // Fallback for custom IGraphClient implementations that do not implement ICypherQueryExecutor
        }

        var nodes = await GetNodesByKindAsync(NodeKind.Workspace, ct);
        return nodes.Count > 0 ? NodeFactory.ToWorkspaceNode(nodes[0]) : null;
    }

    private const string QueryNodeById = "MATCH (n) WHERE n.id = $id RETURN n LIMIT 1";
    private const string QueryNodesByKind = "MATCH (n) WHERE n.kind = $kind RETURN n";
    private const string QueryAllNodes = "MATCH (n) RETURN n";
    private const string QueryAllRelationships = "MATCH ()-[r]->() RETURN r";

    public async ValueTask<Node?> GetNodeByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(id)) return null;

        return await _client.QueryFirstOrDefaultAsync<Node>(
            QueryNodeById,
            new { id },
            ct);
    }

    public async ValueTask<IReadOnlyList<Node>> GetNodesByKindAsync(string kind, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(kind)) return [];

        return await _client.QueryAsync<Node>(
            QueryNodesByKind,
            new { kind },
            ct);
    }

    public ValueTask<IReadOnlyList<Node>> GetNodesByKindAsync(NodeKind kind, CancellationToken ct = default)
    {
        if (kind == NodeKind.Unspecified) return ValueTask.FromResult<IReadOnlyList<Node>>([]);
        return GetNodesByKindAsync(kind.ToCypherLabel(), ct);
    }

    public async ValueTask<IReadOnlyList<Node>> GetAllNodesAsync(CancellationToken ct = default)
    {
        return await _client.QueryAsync<Node>(
            QueryAllNodes,
            null,
            ct);
    }

    public async ValueTask<IReadOnlyList<Relationship>> GetAllRelationshipsAsync(CancellationToken ct = default)
    {
        return await _client.QueryAsync<Relationship>(
            QueryAllRelationships,
            null,
            ct);
    }

    public ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsAsync(
        string fromNodeId,
        CancellationToken ct = default)
    {
        return GetOutgoingRelationshipsInternalAsync(fromNodeId, null, ct);
    }

    public ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsAsync(
        string fromNodeId,
        string relationshipKind,
        CancellationToken ct = default)
    {
        return GetOutgoingRelationshipsInternalAsync(fromNodeId, relationshipKind, ct);
    }

    public ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsAsync(
        string fromNodeId,
        RelationshipKind relationshipKind,
        CancellationToken ct = default)
    {
        if (relationshipKind == RelationshipKind.Unspecified)
        {
            return GetOutgoingRelationshipsInternalAsync(fromNodeId, null, ct);
        }
        return GetOutgoingRelationshipsInternalAsync(fromNodeId, relationshipKind.ToCypherType(), ct);
    }

    private async ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsInternalAsync(
        string fromNodeId,
        string? relationshipKind,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(fromNodeId)) return [];

        string query;
        object parameters;

        if (string.IsNullOrEmpty(relationshipKind))
        {
            query = "MATCH (a)-[r]->(b) WHERE a.id = $fromId RETURN r";
            parameters = new { fromId = fromNodeId };
        }
        else
        {
            query = "MATCH (a)-[r]->(b) WHERE a.id = $fromId AND type(r) = $relKind RETURN r";
            parameters = new { fromId = fromNodeId, relKind = relationshipKind };
        }

        return await _client.QueryAsync<Relationship>(query, parameters, ct);
    }

    public ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsAsync(
        string toNodeId,
        CancellationToken ct = default)
    {
        return GetIncomingRelationshipsInternalAsync(toNodeId, null, ct);
    }

    public ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsAsync(
        string toNodeId,
        string relationshipKind,
        CancellationToken ct = default)
    {
        return GetIncomingRelationshipsInternalAsync(toNodeId, relationshipKind, ct);
    }

    public ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsAsync(
        string toNodeId,
        RelationshipKind relationshipKind,
        CancellationToken ct = default)
    {
        if (relationshipKind == RelationshipKind.Unspecified)
        {
            return GetIncomingRelationshipsInternalAsync(toNodeId, null, ct);
        }
        return GetIncomingRelationshipsInternalAsync(toNodeId, relationshipKind.ToCypherType(), ct);
    }

    private async ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsInternalAsync(
        string toNodeId,
        string? relationshipKind,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(toNodeId)) return [];

        string query;
        object parameters;

        if (string.IsNullOrEmpty(relationshipKind))
        {
            query = "MATCH (a)-[r]->(b) WHERE b.id = $toId RETURN r";
            parameters = new { toId = toNodeId };
        }
        else
        {
            query = "MATCH (a)-[r]->(b) WHERE b.id = $toId AND type(r) = $relKind RETURN r";
            parameters = new { toId = toNodeId, relKind = relationshipKind };
        }

        return await _client.QueryAsync<Relationship>(query, parameters, ct);
    }

    public Task UpsertNodesAsync(IEnumerable<Node> nodes, CancellationToken ct = default) =>
        _client.UploadNodesAsync(nodes as List<Node> ?? nodes.ToList());

    public Task UpsertRelationshipsAsync(IEnumerable<Relationship> relationships, CancellationToken ct = default) =>
        _client.UploadRelationshipsAsync(relationships as List<Relationship> ?? relationships.ToList());

    public Task ClearAsync(CancellationToken ct = default) =>
        _client.ClearDatabaseAsync();

    public Task<Dictionary<(string Kind, string Name), string>> LoadSymbolsByNamesAsync(
        IEnumerable<string> names,
        string workspaceId,
        CancellationToken ct = default) =>
        _client.LoadSymbolsByNamesAsync(names, workspaceId, ct);

    public Task<Dictionary<string, List<string>>> LoadImplementationsForTypesAsync(
        IEnumerable<string> typeNames,
        string workspaceId,
        CancellationToken ct = default) =>
        _client.LoadImplementationsForTypesAsync(typeNames, workspaceId, ct);

    public Task<(List<EndpointNode> Endpoints, List<EntryPointNode> EntryPoints, List<ExternalServiceNode> ExternalServices, List<ProjectNode> Projects)> LoadLateBindingCandidatesAsync(
        string workspaceId,
        bool needEndpoints,
        bool needExternalServices,
        CancellationToken ct = default) =>
        _client.LoadLateBindingCandidatesAsync(workspaceId, needEndpoints, needExternalServices, ct);

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
