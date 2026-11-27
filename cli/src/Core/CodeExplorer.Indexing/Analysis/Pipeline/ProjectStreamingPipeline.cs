using System.Diagnostics;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

internal static class ProjectStreamingPipeline
{
    public static async Task<ProjectStreamingResult> RunFusedLayers3And4Async(
        WorkspaceScope scope,
        GraphWriteSink sink,
        GitRepositoryRegistry git,
        WorkspaceDirectoryIndex dirIndex,
        Layer1Result l1,
        Layer2Result l2,
        SymbolResolutionBuffer symbols,
        SemanticParseContext semanticCtx,
        List<Relationship> globalProjectDeps,
        List<Relationship> treeRels,
        IReadOnlySet<string>? incrementalChangedFiles = null,
        Func<IReadOnlyList<FileNode>, ProjectNode, Layer2Result, IReadOnlyList<FileNode>>? customProjectFileFilter = null)
    {
        var l3Ctx = new Layer3Context(scope, sink, l1, l2, dirIndex, symbols, semanticCtx, treeRels)
        {
            IncrementalChangedFiles = incrementalChangedFiles,
            CustomProjectFileFilter = customProjectFileFilter
        };

        var l3Parser = new Layer3SyntacticParser();
        var l4Parser = new Layer4SemanticParser();

        l3Ctx.Layer3Log.ScanStarted();
        var l3Sw = Stopwatch.StartNew();
        var l3State = await l3Parser.InitializeStreamingStateAsync(l3Ctx);
        var l3ElapsedMs = l3Sw.ElapsedMilliseconds;

        var emptyL3Result = new Layer3Result(
            l3State.SyntaxStructure,
            [],
            symbols.RawImports,
            symbols.RawVariables,
            symbols.RawTypeBindings,
            symbols.GlobalReferences,
            symbols.GlobalSymbols,
            l3Ctx.DiscoveredContentSkippedFiles);

        var l4Ctx = new Layer4Context(scope, sink, git, l1, l2, emptyL3Result, symbols, semanticCtx, globalProjectDeps, treeRels)
        {
            IncrementalChangedFiles = incrementalChangedFiles
        };

        l4Ctx.Layer4Log.ScanStarted();
        var l4Sw = Stopwatch.StartNew();
        var l4State = l4Parser.InitializeStreamingState(l4Ctx, l3State.FilesByProjectId, l3State.WorkspaceRootFiles);
        var l4ElapsedMs = l4Sw.ElapsedMilliseconds;

        var precomputedStructuralHashes = new Dictionary<string, string>(l1.Files.Count, StringComparer.OrdinalIgnoreCase);
        var sharedVisitedNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var sharedVisitedRelKeys = new HashSet<(string From, string To, RelationshipKind Kind)>();
        var allModuleInits = new List<ModuleInitStub>();
        var enrichedProjectIds = new HashSet<string>(l2.Projects.Count, StringComparer.Ordinal);
        var deferredTerraformProjects = new List<(ProjectNode Project, List<SyntaxTree> TerraformTrees)>();
        const string terraformLang = "terraform";

        var totalProjects = l2.Projects.Count;
        var projectsProcessed = 0;
        var lastL4LogTicks = Environment.TickCount64;
        var parsedTreeCount = 0;

        l3Sw.Restart();
        await foreach (var (project, projectSyntaxNode, parsedFiles) in Layer3SyntacticParser.StreamProjectBatchesAsync(
                           l2,
                           l3Ctx,
                           l3State.SyntaxStructure,
                           l3State.FilesByProjectId,
                           l3State.ParentByChildId,
                           l3State.PrunedFileIds,
                           scope.CancellationToken))
        {
            l3ElapsedMs += l3Sw.ElapsedMilliseconds;

            var projectTrees = new List<SyntaxTree>(parsedFiles.Count);
            var projectRawImports = new List<RawImport>();
            var uploadPairs = new List<(IOntologyNode ParentNode, IOntologyNode FileNode)>(parsedFiles.Count);

            for (var i = 0; i < parsedFiles.Count; i++)
            {
                var (file, syntaxTree, parentNode) = parsedFiles[i];
                if (syntaxTree.RawImports.Count > 0)
                {
                    projectRawImports.AddRange(syntaxTree.RawImports);
                }

                Layer3SyntacticParser.AttachParsedSyntaxTreeToHierarchy(projectSyntaxNode, file, syntaxTree, parentNode, l3Ctx);
                projectTrees.Add(syntaxTree);
                uploadPairs.Add((parentNode, syntaxTree.FileNode));
            }

            parsedTreeCount += projectTrees.Count;
            if (sink.PersistToGraphDb)
            {
                FileRegistrySynchronizer.AppendProjectStructuralHashes(projectTrees, precomputedStructuralHashes);
            }

            List<SyntaxTree>? tfTrees = null;
            for (var t = 0; t < projectTrees.Count; t++)
            {
                if (string.Equals(projectTrees[t].FileParser.LanguageName, terraformLang, StringComparison.OrdinalIgnoreCase))
                {
                    tfTrees ??= [];
                    tfTrees.Add(projectTrees[t]);
                }
            }

            var retainTfAst = project.ProjectType == ProjectLanguage.Terraform || tfTrees is { Count: > 0 };
            if (retainTfAst)
            {
                var retained = tfTrees ?? [];
                l4State.TerraformTreesByProjectId[project.Id] = retained;
                deferredTerraformProjects.Add((project, retained));
            }

            enrichedProjectIds.Add(project.Id);
            projectsProcessed++;
            var projectFiles = l4State.FilesByProjectId.GetValueOrDefault(project.Id) ?? [];

            l4Sw.Restart();
            (lastL4LogTicks, var projectSemanticNodes) = await l4Parser.EnrichSingleProjectStreamingAsync(
                project,
                projectsProcessed,
                totalProjects,
                lastL4LogTicks,
                projectFiles,
                projectTrees,
                l4State,
                l4Ctx);

            var summary = ProjectSummaryBuilder.BuildAndRegister(
                project,
                projectTrees,
                projectSemanticNodes,
                projectRawImports,
                symbols);
            if (summary.ModuleInits.Count > 0)
            {
                allModuleInits.AddRange(summary.ModuleInits);
            }

            await OntologyUploader.UploadProjectSubtreeAsync(
                projectSyntaxNode,
                l3State.SyntaxStructure,
                uploadPairs,
                l4Ctx,
                sharedVisitedNodeIds,
                sharedVisitedRelKeys);

            PipelineMemoryEvictor.EvictCompletedProjectSyntacticBodies(
                projectSyntaxNode,
                projectTrees,
                retainTerraformAst: retainTfAst,
                scope.LightweightAstScanMode);
            l4ElapsedMs += l4Sw.ElapsedMilliseconds;

            l3Sw.Restart();
        }
        l3ElapsedMs += l3Sw.ElapsedMilliseconds;

        // Enrich any configuration-only or zero-AST projects not yielded by StreamProjectBatchesAsync
        l4Sw.Restart();
        for (var i = 0; i < l2.Projects.Count; i++)
        {
            var project = l2.Projects[i];
            if (enrichedProjectIds.Contains(project.Id)) continue;
            var projectFiles = l4State.FilesByProjectId.GetValueOrDefault(project.Id) ?? [];
            if (incrementalChangedFiles != null && projectFiles.Count == 0) continue;

            projectsProcessed++;
            (lastL4LogTicks, var projectSemanticNodes) = await l4Parser.EnrichSingleProjectStreamingAsync(
                project,
                projectsProcessed,
                totalProjects,
                lastL4LogTicks,
                projectFiles,
                [],
                l4State,
                l4Ctx);

            if (project.ProjectType == ProjectLanguage.Terraform)
            {
                l4State.TerraformTreesByProjectId[project.Id] = [];
                deferredTerraformProjects.Add((project, []));
            }

            ProjectSummaryBuilder.BuildAndRegister(project, [], projectSemanticNodes, [], symbols);
        }

        await l4Parser.EnrichDeferredTerraformProjectsAsync(deferredTerraformProjects, l4State, l4Ctx);
        l4ElapsedMs += l4Sw.ElapsedMilliseconds;

        l3Sw.Restart();
        await l3Parser.FinalizeStreamingStateAsync(l3Ctx, l3State, applyWorkspaceSymbolFilter: false);
        l3ElapsedMs += l3Sw.ElapsedMilliseconds;
        l3Ctx.Layer3Log.ScanCompleted(parsedTreeCount, l3ElapsedMs);

        var l4FinalizeSw = Stopwatch.StartNew();
        var l4Result = await l4Parser.FinalizeStreamingStateAsync(
            l4State,
            allModuleInits,
            l4Ctx,
            sharedVisitedNodeIds,
            sharedVisitedRelKeys,
            l4ElapsedMs);
        l4ElapsedMs += l4FinalizeSw.ElapsedMilliseconds;

        sharedVisitedNodeIds.Clear();
        sharedVisitedNodeIds.TrimExcess();
        sharedVisitedRelKeys.Clear();
        sharedVisitedRelKeys.TrimExcess();

        return new ProjectStreamingResult(
            l3State.SyntaxStructure,
            emptyL3Result,
            l4Result,
            parsedTreeCount,
            l3ElapsedMs,
            l4ElapsedMs,
            l3Ctx.DiscoveredContentSkippedFiles,
            precomputedStructuralHashes);
    }
}
