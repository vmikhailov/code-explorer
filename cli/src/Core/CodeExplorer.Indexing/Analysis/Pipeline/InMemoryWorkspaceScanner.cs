using System.Diagnostics;
using System.Threading.Channels;
using CodeExplorer.Core.Analysis.Dataflow;
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
            : null;

        var scope = new WorkspaceScope(fullDir, fullDir, cancellationToken: cancellationToken)
        {
            LightweightAstScanMode = options.LightweightAstMode,
            FileFilter = fileFilter,
            DetectorCatalog = detectorCatalog
        };
        var sink = new GraphWriteSink(inMemoryGraph!, sharedChannel, cancellationToken, progress, logger)
        {
            PersistToGraphDb = buildInMemoryGraph,
            Clear = true
        };

        var prevDataflow = FunctionDataflowAnalyzer.Enabled;
        if (options.LightweightAstMode)
        {
            FunctionDataflowAnalyzer.Enabled = false;
        }

        var totalSw = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();

        Layer1Result l1Result;
        Layer2Result? l2Result = null;
        Layer3Result? l3Result = null;
        Layer4Result? l4Result = null;
        Layer5Result? l5Result = null;
        long l1Ms = 0, l2Ms = 0, l3Ms = 0, l4Ms = 0, l5Ms = 0;

        var git = new GitRepositoryRegistry();
        var dirIndex = new WorkspaceDirectoryIndex();
        var symbols = new SymbolResolutionBuffer();
        var globalProjectDeps = new List<Relationship>();
        var treeRels = new List<Relationship>();
        var semanticCtx = new SemanticParseContext(scope)
        {
            Layer4Log = sink.Layer4Log,
            GlobalSymbolSink = symbols.AddGlobalSymbol,
            ProjectDependencySink = dep =>
            {
                lock (globalProjectDeps)
                {
                    globalProjectDeps.Add(dep);
                }
            }
        };

        WorkspaceDetectorCatalog.CurrentScope = detectorCatalog;
        using var dirIndexScope = WorkspaceDirectoryIndex.EnterScope(dirIndex);
        try
        {
            await using var writer = buildInMemoryGraph ? new DatabasePersistenceWriter(sink) : null;

            var l1Ctx = new Layer1Context(scope, sink, git, dirIndex)
            {
                CustomFileExclusionPredicate = options.FileExclusionPredicate
            };
            l1Result = await new Layer1PhysicalParser().ParseAsync(l1Ctx);
            l1Ms = sw.ElapsedMilliseconds;

            if (options.MaxLayer >= AnalysisLayer.Layer2_Boundaries)
            {
                sw.Restart();
                var l2Ctx = new Layer2Context(scope, sink, l1Result, dirIndex, globalProjectDeps);
                l2Result = await new Layer2ProjectParser().ParseAsync(l2Ctx);
                semanticCtx.ProjectsStructure = l2Result.ProjectsStructure;
                l2Ms = sw.ElapsedMilliseconds;
            }

            if (options.MaxLayer >= AnalysisLayer.Layer5_Global && !options.LightweightAstMode && l2Result != null)
            {
                var streamed = await ProjectStreamingPipeline.RunFusedLayers3And4Async(
                    scope,
                    sink,
                    git,
                    dirIndex,
                    l1Result,
                    l2Result,
                    symbols,
                    semanticCtx,
                    globalProjectDeps,
                    treeRels,
                    customProjectFileFilter: options.ProjectFileFilter);
                l3Result = streamed.Layer3;
                l4Result = streamed.Layer4;
                l3Ms = streamed.Layer3ElapsedMs;
                l4Ms = streamed.Layer4ElapsedMs;
            }
            else
            {
                if (options.MaxLayer >= AnalysisLayer.Layer3_Syntactic && l2Result != null)
                {
                    sw.Restart();
                    var l3Ctx = new Layer3Context(scope, sink, l1Result, l2Result, dirIndex, symbols, semanticCtx, treeRels)
                    {
                        CustomProjectFileFilter = options.ProjectFileFilter
                    };
                    l3Result = await new Layer3SyntacticParser().ParseAsync(l3Ctx);
                    l3Ms = sw.ElapsedMilliseconds;
                }

                if (options.MaxLayer >= AnalysisLayer.Layer4_Semantic && l2Result != null && l3Result != null)
                {
                    sw.Restart();
                    var l4Ctx = new Layer4Context(scope, sink, git, l1Result, l2Result, l3Result, symbols, semanticCtx, globalProjectDeps, treeRels);
                    l4Result = await new Layer4SemanticParser().ParseAsync(l4Ctx);
                    l4Ms = sw.ElapsedMilliseconds;
                }
            }

            if (options.MaxLayer >= AnalysisLayer.Layer5_Global && l2Result != null && l3Result != null && l4Result != null)
            {
                sw.Restart();
                PipelineMemoryEvictor.ReleaseSyntaxTreesAfterLayer4(l3Result, scope.LightweightAstScanMode);
                var l5Ctx = new Layer5Context(
                    scope,
                    sink,
                    l1Result.Workspace,
                    l1Result.FilesStructure,
                    l1Result.Files,
                    l2Result,
                    l3Result.SyntaxStructure,
                    l4Result,
                    symbols,
                    semanticCtx.NamedBeanTypes,
                    globalProjectDeps,
                    treeRels);
                l5Result = await new Layer5AnalysisParser().ParseAsync(l5Ctx);
                l5Ms = sw.ElapsedMilliseconds;
            }
        }
        finally
        {
            WorkspaceDetectorCatalog.CurrentScope = null;
            FunctionDataflowAnalyzer.Enabled = prevDataflow;
        }

        totalSw.Stop();
        var (finalNodesCount, finalRelsCount) = inMemoryGraph != null
            ? await inMemoryGraph.GetGraphCountsAsync(cancellationToken)
            : (sink.TotalNodesCount, sink.TotalRelsCount);

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
