using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public sealed class Layer3Context
{
    private static readonly IReadOnlyList<IProjectEnrichment> EmptyEnrichments = [];

    public WorkspaceScope Scope { get; }
    public GraphWriteSink Sink { get; }
    public Layer1Result Layer1 { get; }
    public Layer2Result Layer2 { get; }
    public WorkspaceDirectoryIndex DirectoryIndex { get; }
    public SymbolResolutionBuffer Symbols { get; }
    public SemanticParseContext Semantic { get; }
    public List<Relationship> TreeRelationships { get; }
    public IReadOnlySet<string>? IncrementalChangedFiles { get; init; }
    public Func<IReadOnlyList<FileNode>, ProjectNode, Layer2Result, IReadOnlyList<FileNode>>? CustomProjectFileFilter { get; init; }

    public SyntaxStructureNode? SyntaxStructure { get; set; }

    public ConcurrentDictionary<string, (string FullPath, DateTime LastModifiedUtc, long SizeBytes)> DiscoveredContentSkippedFiles { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IProjectEnrichment> WorkspaceEnrichments => Layer2.WorkspaceEnrichments ?? EmptyEnrichments;

    public string WorkspaceId => Scope.WorkspaceId;
    public string AbsoluteWorkspacePath => Scope.AbsoluteWorkspacePath;
    public bool IsSubtreeScan => Scope.IsSubtreeScan;
    public WorkspaceFileFilter FileFilter => Scope.FileFilter;
    public WorkspaceDetectorCatalog DetectorCatalog => Scope.DetectorCatalog;
    public bool LightweightAstScanMode => Scope.LightweightAstScanMode;
    public CancellationToken CancellationToken => Scope.CancellationToken;
    public bool PersistToGraphDb => Sink.PersistToGraphDb;
    public Layer1Logger Layer1Log => Sink.Layer1Log;
    public Layer3Logger Layer3Log => Sink.Layer3Log;

    public List<RawImport> RawImports => Symbols.RawImports;
    public List<RawVariable> RawVariables => Symbols.RawVariables;
    public List<RawTypeBinding> RawTypeBindings => Symbols.RawTypeBindings;
    public List<Reference> GlobalReferences => Symbols.GlobalReferences;
    public Dictionary<(string Kind, string Name), string> GlobalSymbols => Symbols.GlobalSymbols;
    public ConcurrentDictionary<string, SymbolScopeInfo> SymbolScopes => Symbols.SymbolScopes;
    public ConcurrentDictionary<string, string> FileNamespaces => Symbols.FileNamespaces;
    public ConcurrentDictionary<string, HashSet<string>> FileImportedNamespaces => Symbols.FileImportedNamespaces;

    public bool HasWorkspaceSymbolFilter
    {
        get => Symbols.HasWorkspaceSymbolFilter;
        set => Symbols.HasWorkspaceSymbolFilter = value;
    }

    public Layer3Context(
        WorkspaceScope scope,
        GraphWriteSink sink,
        Layer1Result layer1,
        Layer2Result layer2,
        WorkspaceDirectoryIndex? directoryIndex = null,
        SymbolResolutionBuffer? symbols = null,
        SemanticParseContext? semantic = null,
        List<Relationship>? treeRelationships = null)
    {
        Scope = scope;
        Sink = sink;
        Layer1 = layer1;
        Layer2 = layer2;
        DirectoryIndex = directoryIndex ?? new WorkspaceDirectoryIndex();
        Symbols = symbols ?? new SymbolResolutionBuffer();
        Semantic = semantic ?? new SemanticParseContext(scope)
        {
            ProjectsStructure = layer2.ProjectsStructure,
            Layer4Log = sink.Layer4Log,
            GlobalSymbolSink = Symbols.AddGlobalSymbol
        };
        TreeRelationships = treeRelationships ?? [];
    }

    public Layer3Context(
        Layer1Result layer1,
        Layer2Result layer2,
        string absoluteWorkspacePath,
        string? workspaceId = null,
        WorkspaceDirectoryIndex? directoryIndex = null,
        IGraphClient? dbClient = null,
        bool persistToGraphDb = false,
        WorkspaceFileFilter? fileFilter = null,
        WorkspaceDetectorCatalog? detectorCatalog = null,
        CancellationToken cancellationToken = default)
        : this(
            new WorkspaceScope(absoluteWorkspacePath, absoluteWorkspacePath, cancellationToken: cancellationToken)
            {
                WorkspaceId = workspaceId ?? layer1.Workspace.Id,
                FileFilter = fileFilter ?? WorkspaceFileFilter.Default,
                DetectorCatalog = detectorCatalog ?? WorkspaceDetectorCatalog.Empty
            },
            new GraphWriteSink(dbClient!, null, cancellationToken)
            {
                PersistToGraphDb = persistToGraphDb && dbClient != null
            },
            layer1,
            layer2,
            directoryIndex)
    {
    }

    public void RegisterKnownCallableName(string? name) => Symbols.RegisterKnownCallableName(name);
    public void RegisterKnownTypeName(string? name) => Symbols.RegisterKnownTypeName(name);

    public Task EnqueueUploadRelationshipsAsync(List<Relationship> rels) =>
        Sink.EnqueueUploadRelationshipsAsync(rels);

    public void AddRelsCount(int count) => Sink.AddRelsCount(count);

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
}
