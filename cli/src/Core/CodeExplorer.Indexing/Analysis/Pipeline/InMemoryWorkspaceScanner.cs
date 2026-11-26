using System.Threading.Channels;
using CodeExplorer.Core.Analysis.Layers;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Core.Parser;

internal static class InMemoryWorkspaceScanner
{
    internal static async Task<WorkspaceScanResult> RunAsync(
        string workspacePath,
        IReadOnlyList<IProjectParser> projectParsers,
        IReadOnlyList<IFileParser> fileParsers,
        WorkspaceScanOptions options,
        CancellationToken cancellationToken,
        IProgress<IndexingProgress>? progress,
        ILogger? logger)
    {
        var fullDir = Path.GetFullPath(workspacePath).Replace('\\', '/');
        if (!Directory.Exists(fullDir))
        {
            throw new DirectoryNotFoundException($"Directory '{fullDir}' does not exist.");
        }

        var fileFilter = new WorkspaceFileFilter(
            projectParsers,
            fileParsers,
            options.ExcludedDirectories)
        {
            IncludeDistAndMinifiedArtifacts = options.IncludeDistAndMinifiedArtifacts
        };

        var detectorCatalog = WorkspaceDetectorLoader.LoadForWorkspace(fullDir, strict: true);
        var buildInMemoryGraph = options.PersistToGraphDb || options.MaxLayer >= AnalysisLayer.Layer4_Semantic;
        var inMemoryGraph = buildInMemoryGraph ? new InMemoryGraphRepository() : null;
        var sharedChannel = buildInMemoryGraph
            ? Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false })
            : null!;

        var ctx = new ParsingContext(
            absoluteWorkspacePath: fullDir,
            hostWorkspacePath: fullDir,
            dbClient: inMemoryGraph!,
            sharedChannel: sharedChannel,
            clear: true,
            cancellationToken: cancellationToken,
            progress: progress,
            logger: logger)
        {
            PersistToGraphDb = buildInMemoryGraph,
            LightweightAstScanMode = options.LightweightAstMode,
            CustomFileExclusionPredicate = options.FileExclusionPredicate,
            CustomProjectFileFilter = options.ProjectFileFilter,
            FileFilter = fileFilter,
            DetectorCatalog = detectorCatalog
        };

        var prevDataflow = CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowAnalyzer.Enabled;
        if (options.LightweightAstMode)
        {
            CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowAnalyzer.Enabled = false;
        }

        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        Layer1Result l1Result;
        Layer2Result? l2Result = null;
        Layer3Result? l3Result = null;
        Layer4Result? l4Result = null;
        Layer5Result? l5Result = null;
        long l1Ms = 0, l2Ms = 0, l3Ms = 0, l4Ms = 0, l5Ms = 0;

        WorkspaceDetectorCatalog.CurrentScope = detectorCatalog;
        try
        {
            await using var writer = buildInMemoryGraph ? new DatabasePersistenceWriter(ctx) : null;

            l1Result = await new Layer1PhysicalParser().ParseAsync(ctx);
            l1Ms = sw.ElapsedMilliseconds;

            if (options.MaxLayer >= AnalysisLayer.Layer2_Boundaries)
            {
                sw.Restart();
                l2Result = await new Layer2ProjectParser().ParseAsync(l1Result, ctx);
                l2Ms = sw.ElapsedMilliseconds;
            }

            if (options.MaxLayer >= AnalysisLayer.Layer3_Syntactic && l2Result != null)
            {
                sw.Restart();
                l3Result = await new Layer3SyntacticParser().ParseAsync(l2Result, ctx);
                l3Ms = sw.ElapsedMilliseconds;
            }

            if (options.MaxLayer >= AnalysisLayer.Layer4_Semantic && l3Result != null)
            {
                sw.Restart();
                l4Result = await new Layer4SemanticParser().ParseAsync(l3Result, ctx);
                l4Ms = sw.ElapsedMilliseconds;
            }

            if (options.MaxLayer >= AnalysisLayer.Layer5_Global && l4Result != null)
            {
                sw.Restart();
                l5Result = await new Layer5AnalysisParser().ParseAsync(l4Result, ctx);
                l5Ms = sw.ElapsedMilliseconds;
            }
        }
        finally
        {
            WorkspaceDetectorCatalog.CurrentScope = null;
            CodeExplorer.Core.Analysis.Dataflow.FunctionDataflowAnalyzer.Enabled = prevDataflow;
        }

        totalSw.Stop();
        var (finalNodesCount, finalRelsCount) = inMemoryGraph != null
            ? await inMemoryGraph.GetGraphCountsAsync(cancellationToken)
            : (ctx.TotalNodesCount, ctx.TotalRelsCount);

        return new WorkspaceScanResult
        {
            Layer1 = l1Result,
            Layer2 = l2Result,
            Layer3 = l3Result,
            Layer4 = l4Result,
            Layer5 = l5Result,
            Graph = inMemoryGraph,
            Timings = new LayerScanTimings(
                L1Ms: l1Ms,
                L2Ms: l2Ms,
                L3Ms: l3Ms,
                L4Ms: l4Ms,
                L5Ms: l5Ms,
                TotalMs: totalSw.ElapsedMilliseconds,
                NodeCount: finalNodesCount,
                RelationshipCount: finalRelsCount)
        };
    }
}
