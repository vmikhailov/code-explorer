using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public sealed class Layer5Context
{
    private const string DefaultWorkspaceId = "ws0";
    private static readonly Dictionary<string, string> EmptyStringMap = new(StringComparer.OrdinalIgnoreCase);

    public WorkspaceScope Scope { get; }
    public GraphWriteSink Sink { get; }
    public WorkspaceNode Workspace { get; }
    public FilesStructureNode FilesStructure { get; }
    public List<FileNode> Files { get; }
    public Layer2Result Layer2 { get; }
    public SyntaxStructureNode? SyntaxStructure { get; }
    public Layer4Result Layer4 { get; }
    public SymbolResolutionBuffer Symbols { get; }
    public ConcurrentDictionary<string, string> NamedBeanTypes { get; }
    public List<Relationship> GlobalProjectDependencies { get; }
    public List<Relationship> TreeRelationships { get; }
    public IReadOnlySet<string>? IncrementalChangedFiles { get; init; }

    public string WorkspaceId
    {
        get => Scope.WorkspaceId;
        set => Scope.WorkspaceId = value;
    }

    public string AbsoluteWorkspacePath => Scope.AbsoluteWorkspacePath;
    public bool IsSubtreeScan => Scope.IsSubtreeScan;
    public bool LightweightAstScanMode => Scope.LightweightAstScanMode;
    public CancellationToken CancellationToken => Scope.CancellationToken;

    public IGraphClient DbClient => Sink.DbClient;
    public ProjectsStructureNode? ProjectsStructure => Layer2.ProjectsStructure;
    public List<ProjectNode> Projects => Layer2.Projects;
    public SemanticStructureNode SemanticStructure => Layer4.SemanticStructure;
    public IReadOnlyDictionary<string, string> TsConfigPaths => Layer2.TsConfigPaths ?? EmptyStringMap;
    public IReadOnlyDictionary<string, string> WorkspaceGoModules => Layer2.WorkspaceGoModules ?? EmptyStringMap;

    public Dictionary<(string Kind, string Name), string> GlobalSymbols => Symbols.GlobalSymbols;
    public Dictionary<(string FilePath, string Kind, string Name), string> FileScopedSymbols => Symbols.FileScopedSymbols;
    public List<(string FilePath, string Kind, string Name, string Id)> OverloadedFileScopedSymbols => Symbols.OverloadedFileScopedSymbols;
    public List<Reference> GlobalReferences => Symbols.GlobalReferences;
    public ConcurrentDictionary<string, SymbolScopeInfo> SymbolScopes => Symbols.SymbolScopes;
    public ConcurrentDictionary<string, string> FileNamespaces => Symbols.FileNamespaces;
    public ConcurrentDictionary<string, HashSet<string>> FileImportedNamespaces => Symbols.FileImportedNamespaces;
    public List<RawImport> RawImports => Symbols.RawImports;
    public List<RawVariable> RawVariables => Symbols.RawVariables;
    public List<RawTypeBinding> RawTypeBindings => Symbols.RawTypeBindings;
    public HashSet<string> KnownFunctionIds => Symbols.KnownFunctionIds;

    public Layer5Logger Layer5Log => Sink.Layer5Log;
    public PostIndexLogger PostIndexLog => Sink.PostIndexLog;

    public int TotalRelsCount
    {
        get => Sink.TotalRelsCount;
        set => Sink.TotalRelsCount = value;
    }

    public Layer5Context(
        WorkspaceScope scope,
        GraphWriteSink sink,
        WorkspaceNode workspace,
        FilesStructureNode filesStructure,
        List<FileNode> files,
        Layer2Result layer2,
        SyntaxStructureNode? syntaxStructure,
        Layer4Result layer4,
        SymbolResolutionBuffer symbols,
        ConcurrentDictionary<string, string>? namedBeanTypes = null,
        List<Relationship>? globalProjectDependencies = null,
        List<Relationship>? treeRelationships = null)
    {
        Scope = scope;
        Sink = sink;
        Workspace = workspace;
        FilesStructure = filesStructure;
        Files = files;
        Layer2 = layer2;
        SyntaxStructure = syntaxStructure;
        Layer4 = layer4;
        Symbols = symbols;
        NamedBeanTypes = namedBeanTypes ?? new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        GlobalProjectDependencies = globalProjectDependencies ?? [];
        TreeRelationships = treeRelationships ?? [];
    }

    public Layer5Context(
        string absoluteWorkspacePath,
        IGraphClient? dbClient = null,
        string? workspaceId = null,
        WorkspaceNode? workspace = null,
        FilesStructureNode? filesStructure = null,
        List<FileNode>? files = null,
        Layer2Result? layer2 = null,
        SyntaxStructureNode? syntaxStructure = null,
        Layer4Result? layer4 = null,
        SymbolResolutionBuffer? symbols = null,
        List<Relationship>? globalProjectDependencies = null,
        List<Relationship>? treeRelationships = null,
        string? scanPath = null,
        CancellationToken cancellationToken = default)
    {
        var wsId = workspaceId ?? workspace?.Id ?? DefaultWorkspaceId;
        Scope = new WorkspaceScope(absoluteWorkspacePath, absoluteWorkspacePath, scanPath, cancellationToken)
        {
            WorkspaceId = wsId
        };
        Sink = new GraphWriteSink(dbClient!, null, cancellationToken)
        {
            PersistToGraphDb = dbClient != null
        };
        Workspace = workspace ?? new WorkspaceNode(wsId, Path.GetFileName(absoluteWorkspacePath), absoluteWorkspacePath);
        FilesStructure = filesStructure ?? new FilesStructureNode(
            $"{wsId}:{OntologyConstants.IdPrefixes.FilesStructure}",
            OntologyConstants.NodeLabels.FilesStructure,
            absoluteWorkspacePath);
        Files = files ?? [];
        Layer2 = layer2 ?? new Layer2Result(
            new ProjectsStructureNode(
                $"{wsId}:{OntologyConstants.IdPrefixes.ProjectsStructure}",
                OntologyConstants.NodeLabels.ProjectsStructure,
                absoluteWorkspacePath),
            [],
            [],
            []);
        SyntaxStructure = syntaxStructure;
        Layer4 = layer4 ?? new Layer4Result(
            new SemanticStructureNode(
                $"{wsId}:{OntologyConstants.IdPrefixes.SemanticStructure}",
                OntologyConstants.NodeLabels.SemanticStructure,
                absoluteWorkspacePath),
            [],
            []);
        Symbols = symbols ?? new SymbolResolutionBuffer();
        NamedBeanTypes = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        GlobalProjectDependencies = globalProjectDependencies ?? [];
        TreeRelationships = treeRelationships ?? [];
    }

    public void AddGlobalSymbol(string kind, string name, string id) => Symbols.AddGlobalSymbol(kind, name, id);
    public void AddGlobalSymbol(NodeKind kind, string name, string id) => Symbols.AddGlobalSymbol(kind, name, id);
    public void AddFileScopedSymbol(string filePath, string kind, string name, string id) => Symbols.AddFileScopedSymbol(filePath, kind, name, id);
    public void AddGlobalReferences(IEnumerable<Reference> references) => Symbols.AddGlobalReferences(references);
    public void IncrementNodeKind(string kind) => Sink.IncrementNodeKind(kind);
    public void AddNodesCount(int count) => Sink.AddNodesCount(count);
    public void AddRelsCount(int count) => Sink.AddRelsCount(count);

    public Task EnqueueUploadNodesAsync(List<Node> nodes) => Sink.EnqueueUploadNodesAsync(nodes);
    public Task EnqueueUploadRelationshipsAsync(List<Relationship> rels) => Sink.EnqueueUploadRelationshipsAsync(rels);
    public Task FlushScanIssuesAsync() => Sink.FlushScanIssuesAsync(TreeRelationships);
    public Task WaitForQueueDrainedAsync() => Sink.WaitForQueueDrainedAsync();

    public void ReportProgress(int percentage, string phase, string? message = null, int processedFiles = 0, int totalFiles = 0) =>
        Sink.ReportProgress(percentage, phase, message, processedFiles, totalFiles);

    public ScanIssueNode RecordScanIssue(
        string stage,
        string category,
        string? relativeOrFullPath,
        string message,
        string? name = null,
        string severity = "warning",
        int? startLine = null,
        int? endLine = null,
        int? startCol = null,
        int? endCol = null,
        Exception? exception = null,
        string? language = null,
        string? parentNodeId = null,
        bool isFileParent = true) =>
        Sink.RecordScanIssue(
            Scope,
            stage,
            category,
            relativeOrFullPath,
            message,
            name,
            severity,
            startLine,
            endLine,
            startCol,
            endCol,
            exception,
            language,
            parentNodeId,
            isFileParent);

    public void EvictLayer3ResolutionBuffers() =>
        Symbols.EvictResolutionBuffers(LightweightAstScanMode);

    public void EvictPostIndexBuffers()
    {
        if (LightweightAstScanMode) return;

        TreeRelationships.Clear();
        TreeRelationships.TrimExcess();
        GlobalProjectDependencies.Clear();
        GlobalProjectDependencies.TrimExcess();
        Symbols.EvictGlobalSymbolsAndFunctions(LightweightAstScanMode);
    }
}
