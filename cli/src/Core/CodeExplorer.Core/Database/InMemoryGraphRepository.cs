using System.Collections.Concurrent;
using System.Text.Json;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

/// <summary>
/// High-performance, pure RAM indexed graph repository.
/// Indexes nodes and relationships with O(1) hash structures without SQLite disk overhead.
/// Also implements <see cref="IGraphClient"/> for seamless drop-in backwards compatibility with existing pipelines.
/// </summary>
public class InMemoryGraphRepository : IGraphRepository, IGraphClient
{
    protected readonly ConcurrentDictionary<string, Node> NodesById = new(StringComparer.Ordinal);
    protected readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Node>> NodesByKind = new(StringComparer.OrdinalIgnoreCase);
    protected readonly ConcurrentDictionary<NodeKind, ConcurrentDictionary<string, Node>> NodesByKindEnum = new();
    protected readonly ConcurrentDictionary<string, List<Relationship>> OutgoingEdges = new(StringComparer.Ordinal);
    protected readonly ConcurrentDictionary<string, List<Relationship>> IncomingEdges = new(StringComparer.Ordinal);
    protected readonly List<Relationship> AllRelationships = [];
    protected readonly object SyncLock = new();

    public List<Node> UploadedNodes
    {
        get
        {
            lock (SyncLock)
            {
                return NodesById.Values.ToList();
            }
        }
    }

    public List<Relationship> UploadedRelationships
    {
        get
        {
            lock (SyncLock)
            {
                return [.. AllRelationships];
            }
        }
    }

    public int WorkspaceCounter { get; set; } = 1;

    #region IGraphRepository Implementation

    public async ValueTask<WorkspaceNode?> GetWorkspaceAsync(CancellationToken ct = default)
    {
        var nodes = await GetNodesByKindAsync(NodeKind.Workspace, ct);
        return nodes.Count > 0 ? NodeFactory.ToWorkspaceNode(nodes[0]) : null;
    }

    public ValueTask<Node?> GetNodeByIdAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(id)) return ValueTask.FromResult<Node?>(null);
        NodesById.TryGetValue(id, out var node);
        return ValueTask.FromResult(node);
    }

    public ValueTask<IReadOnlyList<Node>> GetNodesByKindAsync(string kind, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(kind) || !NodesByKind.TryGetValue(kind, out var map))
        {
            return ValueTask.FromResult<IReadOnlyList<Node>>([]);
        }

        lock (SyncLock)
        {
            return ValueTask.FromResult<IReadOnlyList<Node>>(map.Values.ToList());
        }
    }

    public ValueTask<IReadOnlyList<Node>> GetNodesByKindAsync(NodeKind kind, CancellationToken ct = default)
    {
        if (kind == NodeKind.Unspecified || !NodesByKindEnum.TryGetValue(kind, out var map))
        {
            return ValueTask.FromResult<IReadOnlyList<Node>>([]);
        }

        lock (SyncLock)
        {
            return ValueTask.FromResult<IReadOnlyList<Node>>(map.Values.ToList());
        }
    }

    public ValueTask<IReadOnlyList<Node>> GetAllNodesAsync(CancellationToken ct = default)
    {
        lock (SyncLock)
        {
            return ValueTask.FromResult<IReadOnlyList<Node>>(NodesById.Values.ToList());
        }
    }

    public ValueTask<IReadOnlyList<Relationship>> GetAllRelationshipsAsync(CancellationToken ct = default)
    {
        lock (SyncLock)
        {
            return ValueTask.FromResult<IReadOnlyList<Relationship>>([.. AllRelationships]);
        }
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

    private ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsInternalAsync(
        string fromNodeId,
        string? relationshipKind,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(fromNodeId)) return ValueTask.FromResult<IReadOnlyList<Relationship>>([]);

        lock (SyncLock)
        {
            if (!OutgoingEdges.TryGetValue(fromNodeId, out var rels))
            {
                return ValueTask.FromResult<IReadOnlyList<Relationship>>([]);
            }

            if (string.IsNullOrEmpty(relationshipKind))
            {
                return ValueTask.FromResult<IReadOnlyList<Relationship>>([.. rels]);
            }

            var filtered = rels.Where(r => string.Equals(r.Kind, relationshipKind, StringComparison.OrdinalIgnoreCase)).ToList();
            return ValueTask.FromResult<IReadOnlyList<Relationship>>(filtered);
        }
    }

    public ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsAsync(
        string fromNodeId,
        RelationshipKind relationshipKind,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(fromNodeId)) return ValueTask.FromResult<IReadOnlyList<Relationship>>([]);

        lock (SyncLock)
        {
            if (!OutgoingEdges.TryGetValue(fromNodeId, out var rels))
            {
                return ValueTask.FromResult<IReadOnlyList<Relationship>>([]);
            }

            if (relationshipKind == RelationshipKind.Unspecified)
            {
                return ValueTask.FromResult<IReadOnlyList<Relationship>>([.. rels]);
            }

            var filtered = rels.Where(r => EnumExtensions.ParseRelationshipKind(r.Kind) == relationshipKind).ToList();
            return ValueTask.FromResult<IReadOnlyList<Relationship>>(filtered);
        }
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

    private ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsInternalAsync(
        string toNodeId,
        string? relationshipKind,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(toNodeId)) return ValueTask.FromResult<IReadOnlyList<Relationship>>([]);

        lock (SyncLock)
        {
            if (!IncomingEdges.TryGetValue(toNodeId, out var rels))
            {
                return ValueTask.FromResult<IReadOnlyList<Relationship>>([]);
            }

            if (string.IsNullOrEmpty(relationshipKind))
            {
                return ValueTask.FromResult<IReadOnlyList<Relationship>>([.. rels]);
            }

            var filtered = rels.Where(r => string.Equals(r.Kind, relationshipKind, StringComparison.OrdinalIgnoreCase)).ToList();
            return ValueTask.FromResult<IReadOnlyList<Relationship>>(filtered);
        }
    }

    public ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsAsync(
        string toNodeId,
        RelationshipKind relationshipKind,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(toNodeId)) return ValueTask.FromResult<IReadOnlyList<Relationship>>([]);

        lock (SyncLock)
        {
            if (!IncomingEdges.TryGetValue(toNodeId, out var rels))
            {
                return ValueTask.FromResult<IReadOnlyList<Relationship>>([]);
            }

            if (relationshipKind == RelationshipKind.Unspecified)
            {
                return ValueTask.FromResult<IReadOnlyList<Relationship>>([.. rels]);
            }

            var filtered = rels.Where(r => EnumExtensions.ParseRelationshipKind(r.Kind) == relationshipKind).ToList();
            return ValueTask.FromResult<IReadOnlyList<Relationship>>(filtered);
        }
    }

    public Task UpsertNodesAsync(IEnumerable<Node> nodes, CancellationToken ct = default)
    {
        lock (SyncLock)
        {
            foreach (var node in nodes)
            {
                NodesById[node.Id] = node;
                var kindMap = NodesByKind.GetOrAdd(node.Kind, _ => new ConcurrentDictionary<string, Node>(StringComparer.Ordinal));
                kindMap[node.Id] = node;

                var kindEnum = EnumExtensions.ParseNodeKind(node.Kind);
                if (kindEnum != NodeKind.Unspecified)
                {
                    var kindEnumMap = NodesByKindEnum.GetOrAdd(kindEnum, _ => new ConcurrentDictionary<string, Node>(StringComparer.Ordinal));
                    kindEnumMap[node.Id] = node;
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task UpsertRelationshipsAsync(IEnumerable<Relationship> relationships, CancellationToken ct = default)
    {
        lock (SyncLock)
        {
            foreach (var rel in relationships)
            {
                AllRelationships.Add(rel);

                if (!OutgoingEdges.TryGetValue(rel.From, out var outList))
                {
                    outList = [];
                    OutgoingEdges[rel.From] = outList;
                }
                outList.Add(rel);

                if (!IncomingEdges.TryGetValue(rel.To, out var inList))
                {
                    inList = [];
                    IncomingEdges[rel.To] = inList;
                }
                inList.Add(rel);
            }
        }
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken ct = default)
    {
        lock (SyncLock)
        {
            NodesById.Clear();
            NodesByKind.Clear();
            NodesByKindEnum.Clear();
            OutgoingEdges.Clear();
            IncomingEdges.Clear();
            AllRelationships.Clear();
        }
        return Task.CompletedTask;
    }

    public Task<Dictionary<(string Kind, string Name), string>> LoadSymbolsByNamesAsync(
        IEnumerable<string> names,
        string workspaceId,
        CancellationToken ct = default)
    {
        var targetNames = new HashSet<string>(names.Where(n => !string.IsNullOrWhiteSpace(n)), StringComparer.Ordinal);
        if (targetNames.Count == 0) return Task.FromResult(new Dictionary<(string Kind, string Name), string>());

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");
        var result = new Dictionary<(string Kind, string Name), string>();

        lock (SyncLock)
        {
            foreach (var node in NodesById.Values)
            {
                if (widPrefix.Length > 0 && !node.Id.StartsWith(widPrefix, StringComparison.Ordinal)) continue;

                if (node.NodeKind is NodeKind.Type or NodeKind.Function or NodeKind.Procedure or NodeKind.Table or NodeKind.EntryPoint or NodeKind.Endpoint)
                {
                    var name = node.GetName();
                    if (!string.IsNullOrEmpty(name) && targetNames.Contains(name))
                    {
                        result[(node.Kind, name)] = node.Id;
                    }
                }
            }
        }

        return Task.FromResult(result);
    }

    public Task<Dictionary<string, List<string>>> LoadImplementationsForTypesAsync(
        IEnumerable<string> typeNames,
        string workspaceId,
        CancellationToken ct = default)
    {
        var targetTypes = new HashSet<string>(typeNames.Where(t => !string.IsNullOrWhiteSpace(t)), StringComparer.Ordinal);
        var result = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (targetTypes.Count == 0) return Task.FromResult(result);

        lock (SyncLock)
        {
            foreach (var rel in AllRelationships)
            {
                if (rel.RelationshipKind is RelationshipKind.Implements or RelationshipKind.InheritsFrom)
                {
                    if (NodesById.TryGetValue(rel.To, out var targetNode))
                    {
                        var targetName = targetNode.GetName();
                        if (!string.IsNullOrEmpty(targetName) && targetTypes.Contains(targetName))
                        {
                            if (!result.TryGetValue(targetName, out var impls))
                            {
                                impls = [];
                                result[targetName] = impls;
                            }
                            impls.Add(rel.From);
                        }
                    }
                }
            }
        }

        return Task.FromResult(result);
    }

    public Task<(List<EndpointNode> Endpoints, List<EntryPointNode> EntryPoints, List<ExternalServiceNode> ExternalServices, List<ProjectNode> Projects)> LoadLateBindingCandidatesAsync(
        string workspaceId,
        bool needEndpoints,
        bool needExternalServices,
        CancellationToken ct = default)
    {
        var endpoints = new List<EndpointNode>();
        var entryPoints = new List<EntryPointNode>();
        var externalServices = new List<ExternalServiceNode>();
        var projects = new List<ProjectNode>();

        if (!needEndpoints && !needExternalServices)
        {
            return Task.FromResult((endpoints, entryPoints, externalServices, projects));
        }

        var widPrefix = string.IsNullOrEmpty(workspaceId) ? "" : (workspaceId.EndsWith(':') ? workspaceId : workspaceId + ":");

        lock (SyncLock)
        {
            foreach (var node in NodesById.Values)
            {
                if (widPrefix.Length > 0 && !node.Id.StartsWith(widPrefix, StringComparison.Ordinal)) continue;

                if (node.OntologyNode != null)
                {
                    switch (node.OntologyNode)
                    {
                        case EndpointNode ep when needEndpoints:
                            endpoints.Add(ep);
                            continue;
                        case EntryPointNode entry when needEndpoints:
                            entryPoints.Add(entry);
                            continue;
                        case ExternalServiceNode ext when needExternalServices:
                            externalServices.Add(ext);
                            continue;
                        case ProjectNode proj:
                            projects.Add(proj);
                            continue;
                    }
                }

                if (node.NodeKind == NodeKind.Endpoint && needEndpoints)
                {
                    var name = GetProp(node, "name");
                    var filePath = GetProp(node, "path", GetProp(node, "file_path"));
                    var httpMethod = GetProp(node, "http_method");
                    var route = GetProp(node, "route_template", GetProp(node, "route"));
                    endpoints.Add(new EndpointNode(node.Id, name, filePath, httpMethod, route));
                }
                else if (node.NodeKind == NodeKind.EntryPoint && needEndpoints)
                {
                    var name = GetProp(node, "name");
                    var path = GetProp(node, "path", GetProp(node, "file_path"));
                    var type = GetProp(node, "entry_type", GetProp(node, "type"));
                    entryPoints.Add(new EntryPointNode(node.Id, name, path, type));
                }
                else if (node.NodeKind == NodeKind.ExternalService && needExternalServices)
                {
                    var name = GetProp(node, "name");
                    var protocol = GetProp(node, "protocol", GetProp(node, "service_type"));
                    var domain = GetProp(node, "domain_or_service", GetProp(node, "domain", GetProp(node, "service")));
                    var path = GetProp(node, "path", GetProp(node, "target_path"));
                    externalServices.Add(new ExternalServiceNode(node.Id, name, protocol, domain, path));
                }
                else if (node.NodeKind == NodeKind.Project)
                {
                    var name = GetProp(node, "name");
                    var role = GetProp(node, "role");
                    var lang = GetProp(node, "language");
                    var path = GetProp(node, "path", GetProp(node, "file_path"));
                    projects.Add(new ProjectNode(node.Id, name, role, lang, path));
                }
            }
        }

        return Task.FromResult((endpoints, entryPoints, externalServices, projects));

        static string GetProp(Node node, string key, string fallback = "")
        {
            if (node.Properties.TryGetValue(key, out var val) && val is not null)
            {
                return val.ToString() ?? fallback;
            }
            return fallback;
        }
    }

    #endregion

    #region IGraphClient Compatibility Layer

    public Task CreateIndicesAsync() => Task.CompletedTask;

    public Task ClearDatabaseAsync() => ClearAsync();

    public Task<bool> ClearWorkspaceAsync(string workspacePath) => Task.FromResult(true);

    public Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath) =>
        Task.FromResult(WorkspaceCounter.ToString());

    public Task SaveEmptyWorkspaceNodeAsync(string id, string path) => Task.CompletedTask;

    public Task UploadNodesAsync(List<Node> nodes) => UpsertNodesAsync(nodes);

    public Task UploadRelationshipsAsync(List<Relationship> rels) => UpsertRelationshipsAsync(rels);

    public Task<string> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default)
    {
        lock (SyncLock)
        {
            return Task.FromResult(JsonSerializer.Serialize(NodesById.Values));
        }
    }

    public Task ExecuteWriteAsync(string query, object? parameters = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    #endregion
}
