using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Parser;

public sealed class SemanticParseContext
{
    public WorkspaceScope Scope { get; }
    public string WorkspaceId
    {
        get => Scope.WorkspaceId;
        set => Scope.WorkspaceId = value;
    }

    public string AbsoluteWorkspacePath => Scope.AbsoluteWorkspacePath;
    public ResourceReconciliationService ResourceRegistry { get; }
    public ConcurrentDictionary<string, string> NamedBeanTypes { get; }
    public ProjectsStructureNode? ProjectsStructure { get; set; }
    public SemanticStructureNode? SemanticStructure { get; set; }
    public Layer4Logger Layer4Log { get; init; } = new(NullLogger.Instance);
    public List<Relationship> GlobalProjectDependencies { get; } = [];
    public Action<string, string, string>? GlobalSymbolSink { get; set; }
    public Action<Relationship>? ProjectDependencySink { get; set; }

    public SemanticParseContext(
        WorkspaceScope scope,
        ResourceReconciliationService? resourceRegistry = null,
        ConcurrentDictionary<string, string>? namedBeanTypes = null)
    {
        Scope = scope;
        ResourceRegistry = resourceRegistry ?? new ResourceReconciliationService();
        NamedBeanTypes = namedBeanTypes ?? new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
    }

    public SemanticParseContext(string absoluteWorkspacePath)
        : this(new WorkspaceScope(absoluteWorkspacePath, absoluteWorkspacePath))
    {
    }

    public SemanticParseContext(
        string workspaceId,
        string absoluteWorkspacePath,
        ResourceReconciliationService? resourceRegistry = null)
        : this(
            new WorkspaceScope(absoluteWorkspacePath, absoluteWorkspacePath)
            {
                WorkspaceId = workspaceId
            },
            resourceRegistry)
    {
    }

    public void AddGlobalSymbol(string kind, string name, string id) =>
        GlobalSymbolSink?.Invoke(kind, name, id);

    public void AddGlobalSymbol(NodeKind kind, string name, string id) =>
        GlobalSymbolSink?.Invoke(kind.ToCypherLabel(), name, id);

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _containerChildIds = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string From, string To), byte> _emittedConfigServiceCalls = new();
    private Dictionary<string, string>? _workloadIdByProjectId;
    private int _workloadIndexCount = -1;

    public bool TryRegisterConfigServiceCall(string fromId, string toId) =>
        _emittedConfigServiceCalls.TryAdd((fromId, toId), 0);

    public bool TryAddChildOnce(IOntologyNode container, IOntologyNode child)
    {
        var set = _containerChildIds.GetOrAdd(container.Id, _ =>
        {
            var dict = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            lock (container.Children)
            {
                for (var i = 0; i < container.Children.Count; i++)
                {
                    dict.TryAdd(container.Children[i].Id, 0);
                }
            }
            return dict;
        });

        if (!set.TryAdd(child.Id, 0))
        {
            return false;
        }

        lock (container.Children)
        {
            container.Children.Add(child);
        }
        return true;
    }

    public string? ResolveWorkloadIdByProjectId(string projectId)
    {
        var sem = SemanticStructure;
        if (sem == null || string.IsNullOrEmpty(projectId))
        {
            return null;
        }

        var children = sem.Children;
        if (_workloadIdByProjectId == null || _workloadIndexCount != children.Count)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            lock (children)
            {
                for (var i = 0; i < children.Count; i++)
                {
                    if (children[i] is CompositeNode { ProjectId.Length: > 0 } comp)
                    {
                        map.TryAdd(comp.ProjectId, comp.Id);
                    }
                }
                _workloadIndexCount = children.Count;
            }
            _workloadIdByProjectId = map;
        }

        return _workloadIdByProjectId.GetValueOrDefault(projectId);
    }

    public void AddGlobalProjectDependency(Relationship dependency)
    {
        if (ProjectDependencySink != null)
        {
            ProjectDependencySink(dependency);
            return;
        }

        lock (GlobalProjectDependencies)
        {
            GlobalProjectDependencies.Add(dependency);
        }
    }
}
