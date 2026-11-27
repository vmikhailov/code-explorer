using System.Collections.Concurrent;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public sealed class Layer1Context
{
    public WorkspaceScope Scope { get; }
    public GraphWriteSink Sink { get; }
    public GitRepositoryRegistry Git { get; }
    public WorkspaceDirectoryIndex DirectoryIndex { get; }
    public IReadOnlySet<string>? IncrementalChangedFiles { get; init; }
    public IReadOnlySet<string>? IncrementalRelevantDirectories { get; init; }
    public Func<string, string, bool>? CustomFileExclusionPredicate { get; init; }

    public ConcurrentDictionary<string, (string FullPath, string ContentHash, DateTime LastModifiedUtc, long SizeBytes)> DiscoveredManifestFiles { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ConcurrentDictionary<string, (string FullPath, DateTime LastModifiedUtc, long SizeBytes)> DiscoveredManifestPaths { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<(string Label, string Command)> DiscoveredVsCodeFolderOpenTasks { get; } = [];

    public string WorkspaceId
    {
        get => Scope.WorkspaceId;
        set => Scope.WorkspaceId = value;
    }

    public string AbsoluteWorkspacePath => Scope.AbsoluteWorkspacePath;
    public string HostWorkspacePath => Scope.HostWorkspacePath;
    public string ScanPath => Scope.ScanPath;
    public bool IsSubtreeScan => Scope.IsSubtreeScan;
    public WorkspaceFileFilter FileFilter => Scope.FileFilter;
    public bool LightweightAstScanMode => Scope.LightweightAstScanMode;
    public CancellationToken CancellationToken => Scope.CancellationToken;

    public IGraphClient DbClient => Sink.DbClient;
    public bool PersistToGraphDb => Sink.PersistToGraphDb;
    public bool Clear => Sink.Clear;
    public Layer1Logger Layer1Log => Sink.Layer1Log;

    public Layer1Context(
        WorkspaceScope scope,
        GraphWriteSink sink,
        GitRepositoryRegistry? git = null,
        WorkspaceDirectoryIndex? directoryIndex = null)
    {
        Scope = scope;
        Sink = sink;
        Git = git ?? new GitRepositoryRegistry();
        DirectoryIndex = directoryIndex ?? new WorkspaceDirectoryIndex();
    }

    public Layer1Context(
        string absoluteWorkspacePath,
        string hostWorkspacePath,
        IGraphClient? dbClient = null,
        bool clear = false,
        string? scanPath = null,
        bool persistToGraphDb = true,
        WorkspaceFileFilter? fileFilter = null,
        CancellationToken cancellationToken = default)
        : this(
            new WorkspaceScope(absoluteWorkspacePath, hostWorkspacePath, scanPath, cancellationToken)
            {
                FileFilter = fileFilter ?? WorkspaceFileFilter.Default
            },
            new GraphWriteSink(dbClient!, null, cancellationToken)
            {
                PersistToGraphDb = persistToGraphDb && dbClient != null,
                Clear = clear
            })
    {
    }

    public void RegisterGitRepository(string repoAbsDir, GitSettingsNode settings) =>
        Git.Register(repoAbsDir, settings);

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
