using System.Threading.Channels;
using CodeExplorer.Database;
using CodeExplorer.Common;

namespace CodeExplorer.Parser;

public class ParsingContext
{
    public string AbsoluteWorkspacePath { get; }
    public MemgraphClient DbClient { get; }
    public Channel<Func<Task>> SharedChannel { get; }
    public bool Clear { get; }
    
    public Dictionary<(string Kind, string Name), string> GlobalSymbols { get; }
    public List<Reference> GlobalReferences { get; }
    public List<Relationship> GlobalProjectDependencies { get; }
    
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
            Console.Error.WriteLine($"[PersistenceProgress] Saved: {_nodesPersisted} nodes, {_relsPersisted} relationships to database...");
            _lastReportedNodes = _nodesPersisted;
            _lastReportedRels = _relsPersisted;
        }
    }

    public int GetTotalNodesPersisted() => _nodesPersisted;
    public int GetTotalRelsPersisted() => _relsPersisted;

    public async Task EnqueueUploadNodesAsync(List<Node> nodes)
    {
        if (nodes == null || nodes.Count == 0) return;
        var copy = new List<Node>(nodes);
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
        MemgraphClient dbClient, 
        Channel<Func<Task>> sharedChannel,
        bool clear = false,
        Dictionary<(string Kind, string Name), string>? globalSymbols = null,
        List<Reference>? globalReferences = null,
        List<Relationship>? globalProjectDependencies = null)
    {
        AbsoluteWorkspacePath = absoluteWorkspacePath.Replace('\\', '/');
        DbClient = dbClient;
        SharedChannel = sharedChannel;
        Clear = clear;
        GlobalSymbols = globalSymbols ?? new Dictionary<(string Kind, string Name), string>();
        GlobalReferences = globalReferences ?? new List<Reference>();
        GlobalProjectDependencies = globalProjectDependencies ?? new List<Relationship>();
    }
}
