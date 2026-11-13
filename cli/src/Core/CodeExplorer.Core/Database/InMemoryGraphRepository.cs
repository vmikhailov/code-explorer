using System.Collections.Concurrent;
using System.Text.Json;
using CodeExplorer.Core.Analysis;
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
    private const string PropId = "id";
    private const string PropPath = "path";
    private const string PropExternalApis = "external_apis";
    private const string PropName = "name";
    private const string PropFilePath = "file_path";
    private const string PropHttpMethod = "http_method";
    private const string PropRouteTemplate = "route_template";
    private const string PropRoute = "route";
    private const string PropEntryType = "entry_type";
    private const string PropType = "type";
    private const string PropProtocol = "protocol";
    private const string PropServiceType = "service_type";
    private const string PropDomainOrService = "domain_or_service";
    private const string PropDomain = "domain";
    private const string PropService = "service";
    private const string PropTargetPath = "target_path";
    private const string PropRole = "role";
    private const string PropLanguage = "language";
    private const string WorkspaceIdPrefix = "wid:";

    protected internal readonly ConcurrentDictionary<string, Node> NodesById = new(StringComparer.Ordinal);
    protected internal readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Node>> NodesByKind = new(StringComparer.OrdinalIgnoreCase);
    protected internal readonly ConcurrentDictionary<NodeKind, ConcurrentDictionary<string, Node>> NodesByKindEnum = new();
    protected internal readonly ConcurrentDictionary<string, List<Relationship>> OutgoingEdges = new(StringComparer.Ordinal);
    protected internal readonly ConcurrentDictionary<string, List<Relationship>> IncomingEdges = new(StringComparer.Ordinal);
    protected internal readonly Dictionary<(string From, string To, RelationshipKind Kind), Relationship> RelationshipsByKey = new();
    protected internal readonly List<Relationship> AllRelationships = [];
    protected internal readonly Dictionary<string, List<string>> ProjectExternalApis = new(StringComparer.Ordinal);
    protected internal readonly object SyncLock = new();

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
        CancellationToken ct = default) =>
        GetOutgoingRelationshipsAsync(fromNodeId, RelationshipKind.Unspecified, ct);

    public ValueTask<IReadOnlyList<Relationship>> GetOutgoingRelationshipsAsync(
        string fromNodeId,
        string relationshipKind,
        CancellationToken ct = default) =>
        string.IsNullOrEmpty(relationshipKind)
            ? GetOutgoingRelationshipsAsync(fromNodeId, RelationshipKind.Unspecified, ct)
            : GetOutgoingRelationshipsAsync(fromNodeId, EnumExtensions.ParseRelationshipKind(relationshipKind), ct);

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

            var filtered = rels.Where(r => r.Kind == relationshipKind).ToList();
            return ValueTask.FromResult<IReadOnlyList<Relationship>>(filtered);
        }
    }

    public ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsAsync(
        string toNodeId,
        CancellationToken ct = default) =>
        GetIncomingRelationshipsAsync(toNodeId, RelationshipKind.Unspecified, ct);

    public ValueTask<IReadOnlyList<Relationship>> GetIncomingRelationshipsAsync(
        string toNodeId,
        string relationshipKind,
        CancellationToken ct = default) =>
        string.IsNullOrEmpty(relationshipKind)
            ? GetIncomingRelationshipsAsync(toNodeId, RelationshipKind.Unspecified, ct)
            : GetIncomingRelationshipsAsync(toNodeId, EnumExtensions.ParseRelationshipKind(relationshipKind), ct);

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

            var filtered = rels.Where(r => r.Kind == relationshipKind).ToList();
            return ValueTask.FromResult<IReadOnlyList<Relationship>>(filtered);
        }
    }

    public Task UpsertNodesAsync(IEnumerable<Node> nodes, CancellationToken ct = default)
    {
        lock (SyncLock)
        {
            foreach (var node in nodes)
            {
                UpsertNodeUnlocked(node);
            }
        }
        return Task.CompletedTask;
    }

    internal void UpsertNodeUnlocked(Node node)
    {
        if (NodesById.TryGetValue(node.Id, out var prevNode) && prevNode.NodeKind != node.NodeKind)
        {
            if (NodesByKind.TryGetValue(prevNode.NodeKind.ToCypherLabel(), out var prevKindMap))
            {
                prevKindMap.TryRemove(node.Id, out _);
            }
            if (prevNode.NodeKind != NodeKind.Unspecified && NodesByKindEnum.TryGetValue(prevNode.NodeKind, out var prevEnumMap))
            {
                prevEnumMap.TryRemove(node.Id, out _);
            }
        }

        NodesById[node.Id] = node;
        var kindEnum = node.NodeKind;
        var kindMap = NodesByKind.GetOrAdd(kindEnum.ToCypherLabel(), _ => new ConcurrentDictionary<string, Node>(StringComparer.Ordinal));
        kindMap[node.Id] = node;

        if (kindEnum != NodeKind.Unspecified)
        {
            var kindEnumMap = NodesByKindEnum.GetOrAdd(kindEnum, _ => new ConcurrentDictionary<string, Node>(StringComparer.Ordinal));
            kindEnumMap[node.Id] = node;
        }
    }

    public Task UpsertRelationshipsAsync(IEnumerable<Relationship> relationships, CancellationToken ct = default)
    {
        lock (SyncLock)
        {
            foreach (var rel in relationships)
            {
                UpsertRelationshipUnlocked(rel);
            }
        }
        return Task.CompletedTask;
    }

    internal void UpsertRelationshipUnlocked(Relationship rel)
    {
        var key = (rel.From, rel.To, rel.Kind);
        if (RelationshipsByKey.TryGetValue(key, out var existing))
        {
            if (rel.HasTelemetry)
            {
                EvidenceProvenanceHelper.MergeRelationships(existing, rel);
            }
            return;
        }

        RelationshipsByKey[key] = rel;
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

    public Task ClearAsync(CancellationToken ct = default)
    {
        lock (SyncLock)
        {
            NodesById.Clear();
            NodesByKind.Clear();
            NodesByKindEnum.Clear();
            OutgoingEdges.Clear();
            IncomingEdges.Clear();
            RelationshipsByKey.Clear();
            AllRelationships.Clear();
            ProjectExternalApis.Clear();
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
                        result[(node.Kind.ToCypherLabel(), name)] = node.Id;
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
                    var name = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropName);
                    var filePath = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropPath, InMemoryGraphMaintenance.GetNodeStringProperty(node, PropFilePath));
                    var httpMethod = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropHttpMethod);
                    var route = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropRouteTemplate, InMemoryGraphMaintenance.GetNodeStringProperty(node, PropRoute));
                    endpoints.Add(new EndpointNode(node.Id, name, filePath, httpMethod, route));
                }
                else if (node.NodeKind == NodeKind.EntryPoint && needEndpoints)
                {
                    var name = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropName);
                    var path = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropPath, InMemoryGraphMaintenance.GetNodeStringProperty(node, PropFilePath));
                    var type = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropEntryType, InMemoryGraphMaintenance.GetNodeStringProperty(node, PropType));
                    entryPoints.Add(new EntryPointNode(node.Id, name, path, type));
                }
                else if (node.NodeKind == NodeKind.ExternalService && needExternalServices)
                {
                    var name = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropName);
                    var protocol = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropProtocol, InMemoryGraphMaintenance.GetNodeStringProperty(node, PropServiceType));
                    var domain = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropDomainOrService, InMemoryGraphMaintenance.GetNodeStringProperty(node, PropDomain, InMemoryGraphMaintenance.GetNodeStringProperty(node, PropService)));
                    var path = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropPath, InMemoryGraphMaintenance.GetNodeStringProperty(node, PropTargetPath));
                    externalServices.Add(new ExternalServiceNode(node.Id, name, protocol, domain, path));
                }
                else if (node.NodeKind == NodeKind.Project)
                {
                    var name = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropName);
                    var role = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropRole);
                    var lang = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropLanguage);
                    var path = InMemoryGraphMaintenance.GetNodeStringProperty(node, PropPath, InMemoryGraphMaintenance.GetNodeStringProperty(node, PropFilePath));
                    projects.Add(new ProjectNode(node.Id, name, role, lang, path));
                }
            }
        }

        return Task.FromResult((endpoints, entryPoints, externalServices, projects));
    }

    #endregion

    #region IGraphClient Compatibility Implementation

    public virtual Task ClearDatabaseAsync() => ClearAsync();

    public virtual Task<string> GetOrCreateWorkspaceIdAsync(string workspacePath) =>
        Task.FromResult($"{WorkspaceIdPrefix}{WorkspaceCounter++}");

    public virtual Task SaveEmptyWorkspaceNodeAsync(string id, string path) =>
        UpsertNodesAsync([new Node(id, NodeKind.Workspace, new Dictionary<string, object> { [PropId] = id, [PropPath] = path })]);

    public virtual Task UploadNodesAsync(List<Node> nodes) =>
        UpsertNodesAsync(nodes);

    public virtual Task UploadRelationshipsAsync(List<Relationship> rels) =>
        UpsertRelationshipsAsync(rels);

    public virtual Task<CypherQueryResult> ExecuteCypherAsync(string query, object? parameters = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CypherQueryResult([], []));

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;

    #endregion
}
