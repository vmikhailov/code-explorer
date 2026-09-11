using System.Threading.Channels;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CodeExplorer.Core.Parser;

public class WorkspaceIndexer
{
    internal static readonly List<IProjectParser> _projectParsers = [];
    internal static readonly List<IFileParser> _fileParsers = [];

    public static void Register(object parser)
    {
        if (parser is IProjectParser projectParser)
        {
            if (_projectParsers.All(p => p.GetType() != projectParser.GetType()))
                _projectParsers.Add(projectParser);
        }

        if (parser is IFileParser fileParser)
        {
            if (_fileParsers.All(p => p.GetType() != fileParser.GetType()))
                _fileParsers.Add(fileParser);
        }
    }

    private readonly IGraphClient _dbClient;
    private readonly ILogger<WorkspaceIndexer> _logger;

    public WorkspaceIndexer(IGraphClient dbClient, ILogger<WorkspaceIndexer>? logger = null)
    {
        _dbClient = dbClient;
        _logger = logger ?? NullLogger<WorkspaceIndexer>.Instance;
    }

    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync(
        string workspacePath,
        bool clear,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null)
    {
        var ctx = CreateContext(workspacePath, clear, cancellationToken, progress);

        await RunParsingPipelineAsync(ctx);

        ctx.Log(
            $"[WorkspaceIndexer] Indexing process completed successfully! Total Nodes: {ctx.TotalNodesCount}, Total Relationships: {ctx.TotalRelsCount}.");

        return (ctx.TotalNodesCount, ctx.TotalRelsCount, ctx.NodesByKind);
    }

    public Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync(
        string hostWorkspacePath,
        string containerWorkspacePath,
        bool clear,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null) =>
        IndexAsync(string.IsNullOrEmpty(containerWorkspacePath) ? hostWorkspacePath : containerWorkspacePath, clear, cancellationToken, progress);

    private ParsingContext CreateContext(
        string workspacePath,
        bool clear,
        CancellationToken cancellationToken,
        IProgress<IndexingProgress>? progress)
    {
        if (!Directory.Exists(workspacePath))
        {
            throw new DirectoryNotFoundException($"Directory '{workspacePath}' does not exist.");
        }

        var absoluteWorkspacePath = Path.GetFullPath(workspacePath).Replace('\\', '/');

        var sharedChannel = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        return new ParsingContext(absoluteWorkspacePath, absoluteWorkspacePath, _dbClient, sharedChannel, clear,
            cancellationToken: cancellationToken, progress: progress, logger: _logger);
    }

    private async Task RunParsingPipelineAsync(ParsingContext ctx)
    {
        await using (new DatabasePersistenceWriter(ctx))
        {
            await PrepareDatabaseAsync(ctx);
            var l1 = await new Layer1PhysicalParser().ParseAsync(ctx);
            ctx.TriggerProgressReport();
            var l2 = await new Layer2ProjectParser().ParseAsync(l1, ctx);
            ctx.TriggerProgressReport();
            var l3 = await new Layer3SyntacticParser().ParseAsync(l2, ctx);
            ctx.TriggerProgressReport();
            var l4 = await new Layer4SemanticParser().ParseAsync(l3, ctx);
            ctx.TriggerProgressReport();
            await new Layer5AnalysisParser().ParseAsync(l4, ctx);
            ctx.TriggerProgressReport();
        }

        LogPersistenceSummary(ctx);
    }

    private async Task PrepareDatabaseAsync(ParsingContext ctx)
    {
        if (ctx.Clear)
        {
            ctx.Log($"[WorkspaceIndexer] Clearing previous root workspace data for '{ctx.HostWorkspacePath}'...");
            await _dbClient.ClearWorkspaceAsync(ctx.HostWorkspacePath);
        }

        await _dbClient.CreateIndicesAsync();
    }

    private void LogPersistenceSummary(ParsingContext ctx)
    {
        ctx.Log(
            $"[WorkspaceIndexer] All background channel persistence writes completed! Total parsed: {ctx.GetTotalNodesPersisted()} nodes, {ctx.GetTotalRelsPersisted()} relationships.");
    }

}
