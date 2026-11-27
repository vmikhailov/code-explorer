using System.Collections.Concurrent;
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
