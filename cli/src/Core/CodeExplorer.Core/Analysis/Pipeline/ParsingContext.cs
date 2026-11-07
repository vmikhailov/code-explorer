using System.Threading.Channels;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Parser;

public enum SymbolVisibility : byte
{
    Private = 0,
    FilePrivate = 1,
    Protected = 2,
    Internal = 3,
    Public = 4
}

public readonly record struct SymbolScopeInfo(
    string SymbolId,
    string FilePath,
    string? ProjectName,
    string? Namespace,
    string? EnclosingTypeName,
    SymbolVisibility Visibility,
    bool IsTest = false);

public static class WorkspaceSymbolScopeExtensions
{
    public static void RegisterKnownCallableName(this ParsingContext ctx, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        ctx.KnownWorkspaceCallableNames.TryAdd(name, 0);
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0 && lastDot + 1 < name.Length)
        {
            ctx.KnownWorkspaceCallableNames.TryAdd(name[(lastDot + 1)..], 0);
        }
    }

    public static void RegisterKnownTypeName(this ParsingContext ctx, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        ctx.KnownWorkspaceTypeNames.TryAdd(name, 0);
        ctx.KnownWorkspaceCallableNames.TryAdd(name, 0);
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0 && lastDot + 1 < name.Length)
        {
            var shortName = name[(lastDot + 1)..];
            ctx.KnownWorkspaceTypeNames.TryAdd(shortName, 0);
            ctx.KnownWorkspaceCallableNames.TryAdd(shortName, 0);
        }
    }

    public static bool IsKnownWorkspaceCallable(this ParsingContext ctx, string? targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return false;
        if (!ctx.HasWorkspaceSymbolFilter || ctx.IsSubtreeScan) return true;
        if (ctx.KnownWorkspaceCallableNames.ContainsKey(targetName)) return true;
        var lastDot = targetName.LastIndexOf('.');
        if (lastDot >= 0 && lastDot + 1 < targetName.Length)
        {
            var lookup = ctx.KnownWorkspaceCallableNames.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.ContainsKey(targetName.AsSpan(lastDot + 1))) return true;
        }
        return false;
    }

    public static bool IsKnownWorkspaceType(this ParsingContext ctx, string? targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName)) return false;
        if (!ctx.HasWorkspaceSymbolFilter || ctx.IsSubtreeScan) return true;
        if (ctx.KnownWorkspaceTypeNames.ContainsKey(targetName)) return true;
        var lastDot = targetName.LastIndexOf('.');
        if (lastDot >= 0 && lastDot + 1 < targetName.Length)
        {
            var lookup = ctx.KnownWorkspaceTypeNames.GetAlternateLookup<ReadOnlySpan<char>>();
            if (lookup.ContainsKey(targetName.AsSpan(lastDot + 1))) return true;
        }
        return false;
    }

    internal static void RegisterSymbolByKind(this ParsingContext ctx, string kind, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (kind is OntologyConstants.NodeLabels.Type or "Class" or "Interface" or "Struct" or "Enum")
        {
            ctx.RegisterKnownTypeName(name);
        }
        else if (kind is OntologyConstants.NodeLabels.Function or OntologyConstants.NodeLabels.Procedure or OntologyConstants.NodeLabels.Endpoint or OntologyConstants.NodeLabels.EntryPoint)
        {
            ctx.RegisterKnownCallableName(name);
        }
    }
}

public class ParsingContext
{
    public string AbsoluteWorkspacePath { get; }
    public string HostWorkspacePath { get; }
    public string ScanPath { get; }
    public bool IsSubtreeScan => !string.Equals(ScanPath, AbsoluteWorkspacePath, StringComparison.OrdinalIgnoreCase);
    public IGraphClient DbClient { get; }
    public IGraphRepository Repository { get; }
    public Channel<Func<Task>> SharedChannel { get; }
    public bool Clear { get; }
    public bool EnableIntentAnalysis { get; set; }
    public bool PersistToGraphDb { get; init; } = true;
    public bool LightweightAstScanMode { get; set; }
    public Func<string, string, bool>? CustomFileExclusionPredicate { get; set; }
    public Func<IReadOnlyList<FileNode>, ProjectNode, Layer2Result, IReadOnlyList<FileNode>>? CustomProjectFileFilter { get; set; }
    public CancellationToken CancellationToken { get; }
    public string WorkspaceId { get; set; } = string.Empty;
    public WorkspaceNode? Workspace { get; set; }
    public ProjectsStructureNode? ProjectsStructure { get; set; }
    public SyntaxStructureNode? SyntaxStructure { get; set; }
    public SemanticStructureNode? SemanticStructure { get; set; }
    public ResourceReconciliationService ResourceRegistry { get; } = new();
    public WorkspaceFileFilter FileFilter { get; set; } = WorkspaceFileFilter.Default;

    /// <summary>Shared-parser results for files that belong to no project (workspace root files).</summary>
    public List<IProjectEnrichment> WorkspaceEnrichments { get; } = [];

    /// <summary>Manifest files discovered and hashed during the Layer 1 physical scan.</summary>
    public System.Collections.Concurrent.ConcurrentDictionary<string, (string FullPath, string ContentHash, DateTime LastModifiedUtc, long SizeBytes)> DiscoveredManifestFiles { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>TypeScript path aliases (compilerOptions.paths) extracted in Layer 2.</summary>
    public Dictionary<string, string> TsConfigPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    private readonly IProgress<IndexingProgress>? _progress;
    private readonly object _progressLock = new();
    private readonly ILogger _logger;

    public ILogger Logger => _logger;
    public Layer1Logger Layer1Log => new(_logger);
    public Layer2Logger Layer2Log => new(_logger);
    public Layer3Logger Layer3Log => new(_logger);
    public Layer4Logger Layer4Log => new(_logger);
    public Layer5Logger Layer5Log => new(_logger);
    public PostIndexLogger PostIndexLog => new(_logger);
    public IntentLogger IntentLog => new(_logger);
    public PipelineLogger PipelineLog => new(_logger);

    public Dictionary<(string Kind, string Name), string> GlobalSymbols { get; }
    public Dictionary<SymbolKey, string> GlobalSymbolKeys { get; } = new();
    public Dictionary<(string FilePath, string Kind, string Name), string> FileScopedSymbols { get; } = new();
    public List<Reference> GlobalReferences { get; }
    public List<Relationship> GlobalProjectDependencies { get; }
    public List<Relationship> TreeRelationships { get; } = [];

    public System.Collections.Concurrent.ConcurrentDictionary<string, SymbolScopeInfo> SymbolScopes { get; } = new(StringComparer.Ordinal);
    public System.Collections.Concurrent.ConcurrentDictionary<string, string> FileNamespaces { get; } = new(StringComparer.OrdinalIgnoreCase);
    public System.Collections.Concurrent.ConcurrentDictionary<string, HashSet<string>> FileImportedNamespaces { get; } = new(StringComparer.OrdinalIgnoreCase);
    public System.Collections.Concurrent.ConcurrentDictionary<string, byte> KnownWorkspaceCallableNames { get; } = new(StringComparer.Ordinal);
    public System.Collections.Concurrent.ConcurrentDictionary<string, byte> KnownWorkspaceTypeNames { get; } = new(StringComparer.Ordinal);
    public bool HasWorkspaceSymbolFilter { get; set; }

    public List<RawImport> RawImports { get; } = [];
    public List<RawVariable> RawVariables { get; } = [];
    public List<RawTypeBinding> RawTypeBindings { get; } = [];
    public Dictionary<string, int> NodesByKind { get; } = new(StringComparer.OrdinalIgnoreCase);

    private int _totalNodesCount;
    private int _totalRelsCount;

    public int TotalNodesCount
    {
        get => _totalNodesCount;
        set => _totalNodesCount = value;
    }

    public int TotalRelsCount
    {
        get => _totalRelsCount;
        set => _totalRelsCount = value;
    }

    public void IncrementNodeKind(string kind)
    {
        lock (NodesByKind)
        {
            var count = NodesByKind.GetValueOrDefault(kind, 0);
            NodesByKind[kind] = count + 1;
        }
    }

    public void AddNodesCount(int count)
    {
        _totalNodesCount += count;
    }

    public void AddRelsCount(int count)
    {
        _totalRelsCount += count;
    }

    public void AddGlobalSymbol(string kind, string name, string id)
    {
        GlobalSymbols[(kind, name)] = id;
        GlobalSymbolKeys[new SymbolKey(kind, name)] = id;
        this.RegisterSymbolByKind(kind, name);
    }

    public void AddGlobalSymbol(NodeKind kind, string name, string id)
    {
        var label = kind.ToCypherLabel();
        GlobalSymbols[(label, name)] = id;
        GlobalSymbolKeys[new SymbolKey(kind, name)] = id;
        this.RegisterSymbolByKind(label, name);
    }

    public void AddGlobalSymbol(SymbolKey key, string id)
    {
        var label = key.Kind.ToCypherLabel();
        GlobalSymbols[(label, key.Name)] = id;
        GlobalSymbolKeys[key] = id;
        this.RegisterSymbolByKind(label, key.Name);
    }

    public void AddFileScopedSymbol(string filePath, string kind, string name, string id)
    {
        FileScopedSymbols[(filePath, kind, name)] = id;
        this.RegisterSymbolByKind(kind, name);
    }

    public void AddGlobalReferences(IEnumerable<Reference> references)
    {
        GlobalReferences.AddRange(references);
    }

    public void AddGlobalProjectDependency(Relationship dependency)
    {
        GlobalProjectDependencies.Add(dependency);
    }

    private int _nodesPersisted;
    private int _relsPersisted;
    private int _lastReportedNodes;
    private int _lastReportedRels;
    private long _lastNodeReportTicks;
    private long _lastRelReportTicks;
    private int _currentPercentage;
    private string _currentPhase = "Starting";
    private string? _currentProgressMessage;
    private int _currentTotalFiles;
    private int _currentProcessedFiles;

    public void ReportProgress(int percentage, string phase, string? message = null, int processedFiles = 0, int totalFiles = 0)
    {
        _currentPercentage = Math.Clamp(percentage, 0, 100);
        _currentPhase = phase;
        _currentProgressMessage = message;
        if (totalFiles > 0) _currentTotalFiles = totalFiles;
        if (processedFiles > 0) _currentProcessedFiles = processedFiles;
        TriggerProgressReport();
    }

    public void RecordNodesPersisted(int count)
    {
        _nodesPersisted += count;
        ReportNodesProgressIfNeeded();
        TriggerProgressReport();
    }

    public void RecordRelationshipsPersisted(int count)
    {
        _relsPersisted += count;
        ReportRelationshipsProgressIfNeeded();
        TriggerProgressReport();
    }

    private void ReportNodesProgressIfNeeded()
    {
        var now = Environment.TickCount64;
        if (_nodesPersisted - _lastReportedNodes >= 10000)
        {
            PipelineLog.PersistedNodesProgress(_nodesPersisted);
            _lastReportedNodes = _nodesPersisted;
            _lastNodeReportTicks = now;
        }
    }

    private void ReportRelationshipsProgressIfNeeded()
    {
        var now = Environment.TickCount64;
        if (_relsPersisted - _lastReportedRels >= 10000)
        {
            PipelineLog.PersistedRelationshipsProgress(_relsPersisted);
            _lastReportedRels = _relsPersisted;
            _lastRelReportTicks = now;
        }
    }

    public void FlushPersistenceProgressReport()
    {
        var now = Environment.TickCount64;
        if (_nodesPersisted > _lastReportedNodes)
        {
            PipelineLog.PersistedNodesFinal(_nodesPersisted);
            _lastReportedNodes = _nodesPersisted;
            _lastNodeReportTicks = now;
        }
        if (_relsPersisted > _lastReportedRels)
        {
            PipelineLog.PersistedRelationshipsFinal(_relsPersisted);
            _lastReportedRels = _relsPersisted;
            _lastRelReportTicks = now;
        }
    }

    public int TotalNodesPersisted => _nodesPersisted;
    public int TotalRelsPersisted => _relsPersisted;

    public void TriggerProgressReport()
    {
        if (_progress == null) return;
        lock (_progressLock)
        {
            Dictionary<string, int> nodesByKindCopy;
            lock (NodesByKind)
            {
                nodesByKindCopy = new Dictionary<string, int>(NodesByKind);
            }
            var snapshot = new IndexingProgress(
                _nodesPersisted,
                _relsPersisted,
                _totalNodesCount,
                _totalRelsCount,
                nodesByKindCopy,
                _currentPercentage,
                _currentPhase,
                _currentProgressMessage,
                _currentTotalFiles,
                _currentProcessedFiles
            );
            _progress.Report(snapshot);
        }
    }

    public async Task EnqueueUploadNodesAsync(List<Node> nodes)
    {
        if (nodes.Count == 0) return;
        if (!PersistToGraphDb)
        {
            RecordNodesPersisted(nodes.Count);
            return;
        }

        var copy = new List<Node>(nodes);
        await SharedChannel.Writer.WriteAsync(async () =>
        {
            if (Repository != null)
            {
                await Repository.UpsertNodesAsync(copy, CancellationToken);
            }
            else if (DbClient != null)
            {
                await DbClient.UploadNodesAsync(copy);
            }
            RecordNodesPersisted(copy.Count);
        }, CancellationToken).ConfigureAwait(false);
    }

    public async Task EnqueueUploadRelationshipsAsync(List<Relationship> rels)
    {
        if (rels.Count == 0) return;
        if (!PersistToGraphDb)
        {
            RecordRelationshipsPersisted(rels.Count);
            return;
        }
        var copy = new List<Relationship>(rels);
        await SharedChannel.Writer.WriteAsync(async () =>
        {
            if (Repository != null)
            {
                await Repository.UpsertRelationshipsAsync(copy, CancellationToken);
            }
            else if (DbClient != null)
            {
                await DbClient.UploadRelationshipsAsync(copy);
            }
            RecordRelationshipsPersisted(copy.Count);
        }, CancellationToken);
    }

    public async Task WaitForQueueDrainedAsync()
    {
        if (!PersistToGraphDb) return;
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await SharedChannel.Writer.WriteAsync(() =>
        {
            tcs.SetResult();
            return Task.CompletedTask;
        }, CancellationToken).ConfigureAwait(false);

        await tcs.Task;
        FlushPersistenceProgressReport();

        try
        {
            await Task.CompletedTask // Defer ANALYZE and WAL checkpoint to CreateIndicesAsync at the end of the pipeline
                .ConfigureAwait(false);
        }
        catch
        {
            // In-memory or test databases might not support WAL checkpoint or ANALYZE
        }
    }

    public ParsingContext(
        string absoluteWorkspacePath,
        string hostWorkspacePath,
        ResourceReconciliationService? resourceRegistry = null)
        : this(absoluteWorkspacePath, hostWorkspacePath, (IGraphClient)null!, Channel.CreateUnbounded<Func<Task>>())
    {
        if (resourceRegistry != null)
        {
            ResourceRegistry = resourceRegistry;
        }
    }

    public ParsingContext(
        string absoluteWorkspacePath,
        string hostWorkspacePath,
        IGraphClient dbClient,
        Channel<Func<Task>> sharedChannel,
        bool clear = false,
        string? scanPath = null,
        Dictionary<(string Kind, string Name), string>? globalSymbols = null,
        List<Reference>? globalReferences = null,
        List<Relationship>? globalProjectDependencies = null,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null,
        ILogger? logger = null,
        bool enableIntentAnalysis = false)
    {
        AbsoluteWorkspacePath = absoluteWorkspacePath.Replace('\\', '/');
        HostWorkspacePath = hostWorkspacePath;
        ScanPath = string.IsNullOrWhiteSpace(scanPath) ? AbsoluteWorkspacePath : scanPath.Replace('\\', '/');
        DbClient = dbClient;
        Repository = dbClient switch
        {
            IGraphRepository repo => repo,
            null => null!,
            _ => new PersistedGraphRepository(dbClient)
        };
        SharedChannel = sharedChannel;
        Clear = clear;
        EnableIntentAnalysis = enableIntentAnalysis;
        CancellationToken = cancellationToken;
        GlobalSymbols = globalSymbols ?? new Dictionary<(string Kind, string Name), string>();
        GlobalReferences = globalReferences ?? [];
        GlobalProjectDependencies = globalProjectDependencies ?? [];
        _progress = progress;
        _logger = logger ?? NullLogger.Instance;
    }

    public Dictionary<string, GitSettingsNode> DiscoveredGitRepositories { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void RegisterGitRepository(string repoAbsDir, GitSettingsNode settings)
    {
        var norm = Path.GetFullPath(repoAbsDir).Replace('\\', '/').TrimEnd('/');
        DiscoveredGitRepositories[norm] = settings;
    }

    public GitSettingsNode? FindGitSettingsForPath(string path)
    {
        var absPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(AbsoluteWorkspacePath, path));
        var norm = absPath.Replace('\\', '/').TrimEnd('/');

        GitSettingsNode? bestMatch = null;
        int bestLen = -1;

        foreach (var (repoDir, settings) in DiscoveredGitRepositories)
        {
            if (norm.Equals(repoDir, StringComparison.OrdinalIgnoreCase) ||
                norm.StartWithIC(repoDir + "/"))
            {
                if (repoDir.Length > bestLen)
                {
                    bestLen = repoDir.Length;
                    bestMatch = settings;
                }
            }
        }

        if (bestMatch != null) return bestMatch;

        // Fallback: search upward for .git
        var root = GitSettingsParser.FindRepoRoot(absPath);
        if (root != null)
        {
            var parsed = GitSettingsParser.Parse(WorkspaceId, AbsoluteWorkspacePath, root);
            if (parsed != null)
            {
                RegisterGitRepository(root, parsed);
                return parsed;
            }
        }

        return null;
    }

    public System.Collections.Concurrent.ConcurrentDictionary<string, (ScanIssueNode Issue, string? ParentNodeId, bool IsFileParent)> ScanIssues { get; } = new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _flushedScanIssueIds = new(StringComparer.Ordinal);

    public string NormalizeRelativePath(string? relativeOrFullPath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrFullPath)) return "";
        var clean = relativeOrFullPath.Replace('\\', '/');
        if (!string.IsNullOrEmpty(AbsoluteWorkspacePath) && Path.IsPathRooted(clean))
        {
            try
            {
                var rel = Path.GetRelativePath(AbsoluteWorkspacePath, clean).Replace('\\', '/');
                return rel == "." ? "" : rel;
            }
            catch
            {
                return clean;
            }
        }
        return clean == "." ? "" : clean.TrimStart('/');
    }

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
        bool isFileParent = true,
        bool logToConsole = true,
        string? logTag = null)
    {
        var relPath = NormalizeRelativePath(relativeOrFullPath);
        var wsId = string.IsNullOrEmpty(WorkspaceId) ? "ws" : WorkspaceId;
        var linePart = startLine ?? 0;
        var colPart = startCol ?? 0;
        var issueName = !string.IsNullOrWhiteSpace(name) ? name : category;
        var issueId = $"{wsId}:{Common.OntologyConstants.IdPrefixes.ScanIssue}:{relPath}:{stage}:{category}:{linePart}:{colPart}";

        var issue = new ScanIssueNode(
            Id: issueId,
            Name: issueName,
            Path: relPath,
            Stage: stage,
            Category: category,
            Severity: severity,
            Message: message,
            StartLine: startLine,
            EndLine: endLine,
            StartCol: startCol,
            EndCol: endCol,
            ExceptionType: exception?.GetType().Name,
            Language: language);

        var resolvedParentId = parentNodeId;
        if (string.IsNullOrEmpty(resolvedParentId))
        {
            resolvedParentId = isFileParent && !string.IsNullOrEmpty(relPath)
                ? $"{wsId}:{Common.OntologyConstants.IdPrefixes.File}:{relPath}"
                : $"{wsId}:{Common.OntologyConstants.IdPrefixes.FilesStructure}";
        }

        if (ScanIssues.TryAdd(issueId, (issue, resolvedParentId, isFileParent)))
        {
            IncrementNodeKind(Common.OntologyConstants.NodeLabels.ScanIssue);
            if (logToConsole)
            {
                var tag = logTag ?? (stage switch
                {
                    "Layer1_Physical" => "[Layer 1    ]",
                    "Layer2_Boundaries" => "[Layer 2    ]",
                    "Layer3_Syntactic" or "Layer3_Visitor" => "[Layer 3    ]",
                    "Layer4_Semantic" => "[Layer 4    ]",
                    "Layer5_Global" => "[Layer 5    ]",
                    _ => "[ScanIssue  ]"
                });
                var locSuffix = startLine.HasValue ? $":{startLine.Value}{(startCol.HasValue ? $":{startCol.Value}" : "")}" : "";
                var location = $"{relPath}{locSuffix}";
                if (string.Equals(severity, "error", StringComparison.OrdinalIgnoreCase))
                    PipelineLog.ScanIssueError(tag, category, location, message, exception);
                else
                    PipelineLog.ScanIssueWarning(tag, category, location, message, exception);
            }
        }

        return issue;
    }

    public async Task FlushScanIssuesAsync()
    {
        if (!PersistToGraphDb || DbClient == null || ScanIssues.IsEmpty) return;

        var nodesToUpload = new List<Node>();
        var relsToUpload = new List<Relationship>();

        foreach (var (id, entry) in ScanIssues)
        {
            if (!_flushedScanIssueIds.TryAdd(id, 0)) continue;

            nodesToUpload.Add(Node.FromNode(entry.Issue));
            if (!string.IsNullOrEmpty(entry.ParentNodeId))
            {
                var containsRel = new Relationship(entry.ParentNodeId, entry.Issue.Id, Common.OntologyConstants.Relationships.Contains);
                relsToUpload.Add(containsRel);
                TreeRelationships.Add(containsRel);
                if (entry.IsFileParent)
                {
                    var declaredInRel = new Relationship(entry.Issue.Id, entry.ParentNodeId, Common.OntologyConstants.Relationships.DeclaredIn);
                    relsToUpload.Add(declaredInRel);
                    TreeRelationships.Add(declaredInRel);
                }
            }
        }

        if (nodesToUpload.Count > 0)
        {
            AddNodesCount(nodesToUpload.Count);
            await EnqueueUploadNodesAsync(nodesToUpload).ConfigureAwait(false);
        }
        if (relsToUpload.Count > 0)
        {
            AddRelsCount(relsToUpload.Count);
            await EnqueueUploadRelationshipsAsync(relsToUpload).ConfigureAwait(false);
        }
    }
}
