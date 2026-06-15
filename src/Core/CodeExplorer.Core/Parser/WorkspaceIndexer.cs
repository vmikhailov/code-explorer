using System;
using System.Linq;
using System.Reflection;
using System.Threading.Channels;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;

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

    private readonly IMemgraphClient _dbClient;

    public WorkspaceIndexer(IMemgraphClient dbClient)
    {
        _dbClient = dbClient;
    }

    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync(
        string hostWorkspacePath,
        string containerWorkspacePath,
        bool clear,
        CancellationToken cancellationToken = default,
        IProgress<IndexingProgress>? progress = null)
    {
        var ctx = CreateContext(hostWorkspacePath, containerWorkspacePath, clear, cancellationToken, progress);

        await RunParsingPipelineAsync(ctx);

        ctx.Log(
            $"[WorkspaceIndexer] Indexing process completed successfully! Total Nodes: {ctx.TotalNodesCount}, Total Relationships: {ctx.TotalRelsCount}.");

        return (ctx.TotalNodesCount, ctx.TotalRelsCount, ctx.NodesByKind);
    }

    private ParsingContext CreateContext(
        string hostWorkspacePath,
        string containerWorkspacePath,
        bool clear,
        CancellationToken cancellationToken,
        IProgress<IndexingProgress>? progress)
    {
        var resolvedPath = PathTools.TranslateHostPathToContainerPath(containerWorkspacePath);

        if (!Directory.Exists(resolvedPath))
        {
            throw new DirectoryNotFoundException(
                $"Directory '{containerWorkspacePath}' (resolved as '{resolvedPath}') does not exist.");
        }

        var absoluteWorkspacePath = Path.GetFullPath(resolvedPath).Replace('\\', '/');

        var sharedChannel = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        return new ParsingContext(absoluteWorkspacePath, hostWorkspacePath, _dbClient, sharedChannel, clear,
            cancellationToken: cancellationToken, progress: progress);
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
