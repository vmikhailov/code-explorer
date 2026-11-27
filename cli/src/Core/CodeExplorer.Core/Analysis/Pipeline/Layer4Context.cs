using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public sealed class Layer4Context
{
    public WorkspaceScope Scope { get; }
    public GraphWriteSink Sink { get; }
    public GitRepositoryRegistry Git { get; }
    public Layer1Result Layer1 { get; }
    public Layer2Result Layer2 { get; }
    public Layer3Result Layer3 { get; }
    public SymbolResolutionBuffer Symbols { get; }
    public SemanticParseContext Semantic { get; }
    public List<Relationship> GlobalProjectDependencies { get; }
    public List<Relationship> TreeRelationships { get; }
    public IReadOnlySet<string>? IncrementalChangedFiles { get; init; }

    public string WorkspaceId => Scope.WorkspaceId;
    public string AbsoluteWorkspacePath => Scope.AbsoluteWorkspacePath;
    public bool IsSubtreeScan => Scope.IsSubtreeScan;
    public bool LightweightAstScanMode => Scope.LightweightAstScanMode;
    public CancellationToken CancellationToken => Scope.CancellationToken;

    public SemanticStructureNode? SemanticStructure
    {
        get => Semantic.SemanticStructure;
        set => Semantic.SemanticStructure = value;
    }

    public ConcurrentDictionary<string, string> NamedBeanTypes => Semantic.NamedBeanTypes;
    public List<Reference> GlobalReferences => Symbols.GlobalReferences;
    public Layer4Logger Layer4Log => Sink.Layer4Log;
    public PipelineLogger PipelineLog => Sink.PipelineLog;

    public int TotalRelsCount
    {
        get => Sink.TotalRelsCount;
        set => Sink.TotalRelsCount = value;
    }

    public Layer4Context(
        WorkspaceScope scope,
        GraphWriteSink sink,
        GitRepositoryRegistry git,
        Layer1Result layer1,
        Layer2Result layer2,
        Layer3Result layer3,
        SymbolResolutionBuffer symbols,
        SemanticParseContext semantic,
        List<Relationship> globalProjectDependencies,
        List<Relationship> treeRelationships)
    {
        Scope = scope;
        Sink = sink;
        Git = git;
        Layer1 = layer1;
        Layer2 = layer2;
        Layer3 = layer3;
        Symbols = symbols;
        Semantic = semantic;
        GlobalProjectDependencies = globalProjectDependencies;
        if (Semantic.GlobalProjectDependencies.Count > 0)
        {
            GlobalProjectDependencies.AddRange(Semantic.GlobalProjectDependencies);
            Semantic.GlobalProjectDependencies.Clear();
        }
        Semantic.ProjectDependencySink ??= AddGlobalProjectDependency;
        TreeRelationships = treeRelationships;
    }

    public Layer4Context(
        Layer1Result layer1,
        Layer2Result layer2,
        Layer3Result layer3,
        string absoluteWorkspacePath,
        string? workspaceId = null,
        IGraphClient? dbClient = null,
        bool persistToGraphDb = true,
        SymbolResolutionBuffer? symbols = null,
        SemanticParseContext? semantic = null,
        List<Relationship>? globalProjectDependencies = null,
        List<Relationship>? treeRelationships = null,
        CancellationToken cancellationToken = default)
    {
        Scope = new WorkspaceScope(absoluteWorkspacePath, absoluteWorkspacePath, cancellationToken: cancellationToken)
        {
            WorkspaceId = workspaceId ?? layer1.Workspace.Id
        };
        Sink = new GraphWriteSink(dbClient!, null, cancellationToken)
        {
            PersistToGraphDb = persistToGraphDb && dbClient != null
        };
        Git = new GitRepositoryRegistry();
        Layer1 = layer1;
        Layer2 = layer2;
        Layer3 = layer3;
        Symbols = symbols ?? new SymbolResolutionBuffer(layer3.GlobalSymbols, layer3.GlobalReferences);
        GlobalProjectDependencies = globalProjectDependencies ?? [];
        TreeRelationships = treeRelationships ?? [];
        Semantic = semantic ?? new SemanticParseContext(Scope)
        {
            ProjectsStructure = layer2.ProjectsStructure,
            Layer4Log = Sink.Layer4Log,
            GlobalSymbolSink = Symbols.AddGlobalSymbol,
            ProjectDependencySink = AddGlobalProjectDependency
        };
    }

    public void AddGlobalProjectDependency(Relationship dependency)
    {
        lock (GlobalProjectDependencies)
        {
            GlobalProjectDependencies.Add(dependency);
        }
    }

    public void AddGlobalSymbol(NodeKind kind, string name, string id) => Symbols.AddGlobalSymbol(kind, name, id);
    public void AddFileScopedSymbol(string filePath, string kind, string name, string id) => Symbols.AddFileScopedSymbol(filePath, kind, name, id);
    public bool IsKnownWorkspaceCallable(string? targetName) => Symbols.IsKnownWorkspaceCallable(targetName, IsSubtreeScan);
    public bool IsKnownWorkspaceType(string? targetName) => Symbols.IsKnownWorkspaceType(targetName, IsSubtreeScan);

    public GitSettingsNode? FindGitSettingsForPath(string path) =>
        Git.FindForPath(WorkspaceId, AbsoluteWorkspacePath, path);

    public void IncrementNodeKind(string kind) => Sink.IncrementNodeKind(kind);
    public void AddNodesCount(int count) => Sink.AddNodesCount(count);
    public void AddRelsCount(int count) => Sink.AddRelsCount(count);

    public Task EnqueueUploadNodesAsync(List<Node> nodes) => Sink.EnqueueUploadNodesAsync(nodes);
    public Task EnqueueUploadRelationshipsAsync(List<Relationship> rels) => Sink.EnqueueUploadRelationshipsAsync(rels);
    public Task FlushScanIssuesAsync() => Sink.FlushScanIssuesAsync(TreeRelationships);

    public void ReportProgress(int percentage, string phase, string? message = null, int processedFiles = 0, int totalFiles = 0) =>
        Sink.ReportProgress(percentage, phase, message, processedFiles, totalFiles);

    public void TriggerProgressReport() => Sink.TriggerProgressReport();
}
