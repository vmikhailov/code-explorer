using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;

namespace CodeExplorer.Core.Parser;

public class ParsingContext
{
    public string AbsoluteWorkspacePath { get; }
    public string HostWorkspacePath { get; }
    public IMemgraphClient DbClient { get; }
    public Channel<Func<Task>> SharedChannel { get; }
    public bool Clear { get; }
    public CancellationToken CancellationToken { get; }
    public string WorkspaceId { get; set; } = string.Empty;
    public ProjectsStructureNode? ProjectsStructure { get; set; }
    public SyntaxStructureNode? SyntaxStructure { get; set; }
    public SemanticStructureNode? SemanticStructure { get; set; }

    private readonly System.Diagnostics.Stopwatch _sessionStopwatch = System.Diagnostics.Stopwatch.StartNew();
    private readonly IProgress<IndexingProgress>? _progress;
    private readonly object _progressLock = new();

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Console.Error.WriteLine($"[{timestamp}] [+{_sessionStopwatch.ElapsedMilliseconds}ms] {message}");
    }
    
    public Dictionary<(string Kind, string Name), string> GlobalSymbols { get; }
    public List<Reference> GlobalReferences { get; }
    public List<Relationship> GlobalProjectDependencies { get; }
    
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

    public void RecordNodesPersisted(int count)
    {
        _nodesPersisted += count;
        ReportProgressIfNeeded();
        TriggerProgressReport();
    }

    public void RecordRelationshipsPersisted(int count)
    {
        _relsPersisted += count;
        ReportProgressIfNeeded();
        TriggerProgressReport();
    }

    private void ReportProgressIfNeeded()
    {
        if (_nodesPersisted - _lastReportedNodes >= 500 || _relsPersisted - _lastReportedRels >= 500)
        {
            Log($"[PersistenceProgress] Saved: {_nodesPersisted} nodes, {_relsPersisted} relationships to database...");
            _lastReportedNodes = _nodesPersisted;
            _lastReportedRels = _relsPersisted;
        }
    }

    public int GetTotalNodesPersisted() => _nodesPersisted;
    public int GetTotalRelsPersisted() => _relsPersisted;

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
                nodesByKindCopy
            );
            _progress.Report(snapshot);
        }
    }

    public async Task EnqueueUploadNodesAsync(List<Node> nodes)
    {
        if (nodes.Count == 0) return;

        var copy = new List<Node>(nodes);
        await SharedChannel.Writer.WriteAsync(async () =>
        {
            await DbClient.UploadNodesAsync(copy);
            RecordNodesPersisted(copy.Count);
        });
    }

    public async Task EnqueueUploadRelationshipsAsync(List<Relationship> rels)
    {
        if (rels.Count == 0) return;
        var copy = new List<Relationship>(rels);
        await SharedChannel.Writer.WriteAsync(async () =>
        {
            await DbClient.UploadRelationshipsAsync(copy);
            RecordRelationshipsPersisted(copy.Count);
        });
    }

    public ParsingContext(
        string absoluteWorkspacePath, 
        string hostWorkspacePath,
        IMemgraphClient dbClient, 
        Channel<Func<Task>> sharedChannel,
        bool clear = false,
        Dictionary<(string Kind, string Name), string>? globalSymbols = null,
        List<Reference>? globalReferences = null,
        List<Relationship>? globalProjectDependencies = null,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null)
    {
        AbsoluteWorkspacePath = absoluteWorkspacePath.Replace('\\', '/');
        HostWorkspacePath = hostWorkspacePath;
        DbClient = dbClient;
        SharedChannel = sharedChannel;
        Clear = clear;
        CancellationToken = cancellationToken;
        GlobalSymbols = globalSymbols ?? new Dictionary<(string Kind, string Name), string>();
        GlobalReferences = globalReferences ?? [];
        GlobalProjectDependencies = globalProjectDependencies ?? [];
        _progress = progress;
    }
}
