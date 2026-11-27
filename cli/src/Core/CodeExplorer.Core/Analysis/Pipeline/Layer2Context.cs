using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public sealed class Layer2Context
{
    private static readonly IReadOnlyList<(string Label, string Command)> EmptyTasks = [];

    public WorkspaceScope Scope { get; }
    public GraphWriteSink Sink { get; }
    public Layer1Result Layer1 { get; }
    public WorkspaceDirectoryIndex DirectoryIndex { get; }
    public List<Relationship> GlobalProjectDependencies { get; }

    public ProjectsStructureNode? ProjectsStructure { get; set; }
    public List<IProjectEnrichment> WorkspaceEnrichments { get; } = [];
    public Dictionary<string, string> TsConfigPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> WorkspaceGoModules { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, (string Engine, string Family)> DirectoryManifestDrivers { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<(string Label, string Command)> DiscoveredVsCodeFolderOpenTasks =>
        Layer1.DiscoveredVsCodeFolderOpenTasks ?? EmptyTasks;

    public string WorkspaceId => Scope.WorkspaceId;
    public string AbsoluteWorkspacePath => Scope.AbsoluteWorkspacePath;
    public string ScanPath => Scope.ScanPath;
    public bool IsSubtreeScan => Scope.IsSubtreeScan;
    public WorkspaceFileFilter FileFilter => Scope.FileFilter;
    public CancellationToken CancellationToken => Scope.CancellationToken;
    public bool PersistToGraphDb => Sink.PersistToGraphDb;
    public Layer1Logger Layer1Log => Sink.Layer1Log;
    public Layer2Logger Layer2Log => Sink.Layer2Log;

    public Layer2Context(
        WorkspaceScope scope,
        GraphWriteSink sink,
        Layer1Result layer1,
        WorkspaceDirectoryIndex? directoryIndex = null,
        List<Relationship>? globalProjectDependencies = null)
    {
        Scope = scope;
        Sink = sink;
        Layer1 = layer1;
        DirectoryIndex = directoryIndex ?? new WorkspaceDirectoryIndex();
        GlobalProjectDependencies = globalProjectDependencies ?? [];
    }

    public Layer2Context(
        Layer1Result layer1,
        string absoluteWorkspacePath,
        string? hostWorkspacePath = null,
        string? workspaceId = null,
        WorkspaceDirectoryIndex? directoryIndex = null,
        IGraphClient? dbClient = null,
        bool persistToGraphDb = false,
        WorkspaceFileFilter? fileFilter = null,
        string? scanPath = null,
        CancellationToken cancellationToken = default)
        : this(
            new WorkspaceScope(absoluteWorkspacePath, hostWorkspacePath ?? absoluteWorkspacePath, scanPath, cancellationToken)
            {
                WorkspaceId = workspaceId ?? layer1.Workspace.Id,
                FileFilter = fileFilter ?? WorkspaceFileFilter.Default
            },
            new GraphWriteSink(dbClient!, null, cancellationToken)
            {
                PersistToGraphDb = persistToGraphDb && dbClient != null
            },
            layer1,
            directoryIndex)
    {
    }

    public void AddGlobalProjectDependency(Relationship dependency)
    {
        lock (GlobalProjectDependencies)
        {
            GlobalProjectDependencies.Add(dependency);
        }
    }

    public Task EnqueueUploadRelationshipsAsync(List<Relationship> rels) =>
        Sink.EnqueueUploadRelationshipsAsync(rels);

    public void AddRelsCount(int count) => Sink.AddRelsCount(count);

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
