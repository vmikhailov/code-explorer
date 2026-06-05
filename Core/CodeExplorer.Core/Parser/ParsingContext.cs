using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class ParsingContext
{
    public string AbsoluteWorkspacePath { get; }
    public string HostWorkspacePath { get; }
    public MemgraphClient DbClient { get; }
    public Channel<Func<Task>> SharedChannel { get; }
    public bool Clear { get; }
    public string WorkspaceId { get; set; } = string.Empty;

    private readonly System.Diagnostics.Stopwatch _sessionStopwatch = System.Diagnostics.Stopwatch.StartNew();

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

    public void AddRawImport(RawImport imp)
    {
        lock (RawImports)
        {
            RawImports.Add(imp);
        }
    }

    public void AddRawVariable(RawVariable var)
    {
        lock (RawVariables)
        {
            RawVariables.Add(var);
        }
    }

    public Dictionary<string, int> NodesByKind { get; } = new(StringComparer.OrdinalIgnoreCase);

    private int _totalNodesCount;
    private int _totalRelsCount;

    public int TotalNodesCount
    {
        get { lock (this) return _totalNodesCount; }
        set { lock (this) _totalNodesCount = value; }
    }

    public int TotalRelsCount
    {
        get { lock (this) return _totalRelsCount; }
        set { lock (this) _totalRelsCount = value; }
    }

    public void IncrementNodeKind(string kind)
    {
        lock (NodesByKind)
        {
            if (!NodesByKind.TryGetValue(kind, out var count)) count = 0;
            NodesByKind[kind] = count + 1;
        }
    }

    public void AddNodesCount(int count)
    {
        lock (this)
        {
            _totalNodesCount += count;
        }
    }

    public void AddRelsCount(int count)
    {
        lock (this)
        {
            _totalRelsCount += count;
        }
    }

    public void AddGlobalSymbol(string kind, string name, string id)
    {
        lock (GlobalSymbols)
        {
            GlobalSymbols[(kind, name)] = id;
        }
    }

    public void AddGlobalReferences(IEnumerable<Reference> references)
    {
        lock (GlobalReferences)
        {
            GlobalReferences.AddRange(references);
        }
    }

    public void AddGlobalProjectDependency(Relationship dependency)
    {
        lock (GlobalProjectDependencies)
        {
            GlobalProjectDependencies.Add(dependency);
        }
    }

    private int _nodesPersisted;
    private int _relsPersisted;
    private int _lastReportedNodes;
    private int _lastReportedRels;

    public void RecordNodesPersisted(int count)
    {
        lock (this)
        {
            _nodesPersisted += count;
            ReportProgressIfNeeded();
        }
    }

    public void RecordRelationshipsPersisted(int count)
    {
        lock (this)
        {
            _relsPersisted += count;
            ReportProgressIfNeeded();
        }
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

    public async Task EnqueueUploadNodesAsync(List<CodeExplorer.Core.Database.Node> nodes)
    {
        if (nodes == null || nodes.Count == 0) return;
        var copy = new List<CodeExplorer.Core.Database.Node>(nodes);
        await SharedChannel.Writer.WriteAsync(async () =>
        {
            await DbClient.UploadNodesAsync(copy);
            RecordNodesPersisted(copy.Count);
        });
    }

    public async Task EnqueueUploadRelationshipsAsync(List<Relationship> rels)
    {
        if (rels == null || rels.Count == 0) return;
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
        MemgraphClient dbClient, 
        Channel<Func<Task>> sharedChannel,
        bool clear = false,
        Dictionary<(string Kind, string Name), string>? globalSymbols = null,
        List<Reference>? globalReferences = null,
        List<Relationship>? globalProjectDependencies = null)
    {
        AbsoluteWorkspacePath = absoluteWorkspacePath.Replace('\\', '/');
        HostWorkspacePath = hostWorkspacePath;
        DbClient = dbClient;
        SharedChannel = sharedChannel;
        Clear = clear;
        GlobalSymbols = globalSymbols ?? new Dictionary<(string Kind, string Name), string>();
        GlobalReferences = globalReferences ?? [];
        GlobalProjectDependencies = globalProjectDependencies ?? [];
    }
}
