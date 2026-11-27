using System.Diagnostics;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser.Layers;

public partial class Layer4SemanticParser
{
    public Task<Layer4Result> ParseAsync(Layer3Result l3Result, Layer4Context ctx) =>
        ParseAsync(ctx);

    public async Task<Layer4Result> ParseAsync(Layer4Context ctx)
    {
        var l1Result = ctx.Layer1;
        var l2Result = ctx.Layer2;
        var l3Result = ctx.Layer3;
        var sw = Stopwatch.StartNew();
        ctx.Layer4Log.ScanStarted();

        var semanticNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.SemanticStructure}";
        var semanticStructureNode = new SemanticStructureNode(semanticNodeId, "SemanticStructure", l1Result.Workspace.Path);
        l1Result.Workspace.Children.Add(semanticStructureNode);
        ctx.SemanticStructure = semanticStructureNode;

        var semanticNodes = new List<IOntologyNode>();
        var semanticRelationships = new List<Relationship>();

        var allProjects = l2Result.Projects;
        var allFiles = l1Result.Files;
        var allTrees = l3Result.SyntaxTrees;

        // O(F * P) pre-grouping once instead of O(P * F * P) in inner loops (eliminates 1.72B comparisons)
        var filesByProjectId = new Dictionary<string, List<FileNode>>(allProjects.Count);
        var treesByProjectId = new Dictionary<string, List<SyntaxTree>>(allProjects.Count);
        var fileToProjectId = new Dictionary<string, string>(allFiles.Count, StringComparer.OrdinalIgnoreCase);
        var workspaceRootFiles = new List<FileNode>();

        foreach (var project in allProjects)
        {
            filesByProjectId[project.Id] = [];
            treesByProjectId[project.Id] = [];
        }

        foreach (var file in allFiles)
        {
            var bestProject = Layer2ProjectParser.FindProjectForFilePath(file.Path, allProjects);
            if (bestProject != null)
            {
                filesByProjectId[bestProject.Id].Add(file);
                fileToProjectId[file.Id] = bestProject.Id;
            }
            else
            {
                workspaceRootFiles.Add(file);
            }
        }

        foreach (var st in allTrees)
        {
            if (st.FileNode != null && fileToProjectId.TryGetValue(st.FileNode.Id, out var projId))
            {
                treesByProjectId[projId].Add(st);
            }
        }

        // 1. Parse workspace-level configuration files (e.g. root .env) first
        foreach (var wfile in workspaceRootFiles)
        {
            if (ConfigurationParser.IsConfigurationFile(wfile.Name))
            {
                ConfigurationParser.ParseAndEnrich(wfile.FullPath, wfile.Path, ctx.WorkspaceId, semanticStructureNode, semanticRelationships, ctx.Semantic);
            }
        }

        var totalProjects = allProjects.Count;
        ctx.Layer4Log.EnrichingProjects(totalProjects, allTrees.Count);

        var existingRelKeys = new HashSet<(string From, string To, string Kind)>();
        foreach (var r in semanticRelationships)
        {
            existingRelKeys.Add((r.From, r.To, r.Kind.ToCypherType()));
        }

        // Materialize first-class semantic workload nodes under SemanticStructure before per-project enrichment
        ctx.Layer4Log.MaterializingWorkloads(allProjects.Count);
        var workloadByProjectId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var workloadNodeByProjectId = new Dictionary<string, IOntologyNode>(StringComparer.OrdinalIgnoreCase);
        var semanticStructureChildIds = new HashSet<string>(semanticStructureNode.Children.Select(c => c.Id), StringComparer.Ordinal);
        var workloadChildIdsByWorkload = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        InitializeWorkloadsForProjects(
            allProjects,
            allFiles,
            ctx,
            semanticStructureNode,
            semanticNodes,
            semanticRelationships,
            semanticStructureChildIds,
            workloadChildIdsByWorkload,
            existingRelKeys,
            workloadByProjectId,
            workloadNodeByProjectId);

        await EnrichProjectsSemanticsAsync(
            allProjects,
            allFiles,
            filesByProjectId,
            treesByProjectId,
            workloadNodeByProjectId,
            workloadChildIdsByWorkload,
            semanticStructureNode,
            semanticStructureChildIds,
            semanticNodes,
            semanticRelationships,
            existingRelKeys,
            ctx);

        SemanticTopologySynthesizer.Synthesize(
            allProjects,
            filesByProjectId,
            l2Result.ProjectDependencies,
            l2Result.Packages,
            workloadByProjectId,
            allFiles,
            semanticStructureNode,
            semanticRelationships,
            existingRelKeys,
            ctx.Semantic,
            ctx.CancellationToken);

        MaterializeWorkloadDependencies(
            l2Result,
            workloadByProjectId,
            semanticStructureNode,
            existingRelKeys,
            semanticRelationships);

        LinkEntryPointsToModuleInit(
            l2Result,
            l3Result,
            allFiles,
            allProjects,
            existingRelKeys,
            semanticRelationships);

        // Collect all semantic nodes from projects and semanticStructureNode
        foreach (var project in allProjects)
        {
            CollectSemanticNodes(project, semanticNodes);
        }
        CollectSemanticNodes(semanticStructureNode, semanticNodes);
        semanticNodes = [.. semanticNodes.DistinctBy(n => n.Id)];

        if (semanticRelationships.Count > 0)
        {
            ctx.Layer4Log.UploadingSemanticRelationships(semanticRelationships.Count);
            await ctx.EnqueueUploadRelationshipsAsync(semanticRelationships);
            ctx.TotalRelsCount += semanticRelationships.Count;
        }

        // 3. Upload the entire Workspace Node tree using OntologyUploader
        ctx.CancellationToken.ThrowIfCancellationRequested();
        ctx.Layer4Log.QueuingOntologyTree();
        await OntologyUploader.UploadNodeTreeAsync(l1Result.Workspace, null, ctx);

        // Free any remaining native TreeSitter memory after Layer 4 (unless Layer 5 provenance slicing is enabled)
        if (!ctx.LightweightAstScanMode)
        {
            foreach (var tree in l3Result.SyntaxTrees)
            {
                tree.Dispose();
                tree.RawImports.Clear();
                tree.RawImports.TrimExcess();
            }
        }

        ctx.Layer4Log.ScanCompleted(semanticNodes.Count, sw.ElapsedMilliseconds);
        return new Layer4Result(semanticStructureNode, semanticNodes, semanticRelationships);
    }

    private static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects) =>
        Layer2ProjectParser.IsEnclosedInProject(file, project, projects);

    private static void CollectSemanticNodes(IOntologyNode node, List<IOntologyNode> semanticNodes)
    {
        foreach (var child in node.Children)
        {
            if (child is ModuleNode or DatabaseNode or DbSchemaNode or EndpointNode or QueryNode or ExternalServiceNode or TopicNode or MessageNode or CacheNode or TransactionNode or PolicyNode or JobNode or CloudNode or CloudResourceNode or CloudServiceNode or ApiInUseNode or TableNode or DataSetNode)
            {
                semanticNodes.Add(child);
            }
            CollectSemanticNodes(child, semanticNodes);
        }
    }

    private void GroupEntryPoints(
        IOntologyNode? workloadNode,
        ProjectNode project,
        List<SyntaxTree> projectTrees,
        List<Relationship> semanticRelationships,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        Layer4Context ctx)
    {
        var entryPoints = new List<EntryPointNode>();
        var parentMap = new Dictionary<string, string>();

        foreach (var syntaxTree in projectTrees)
        {
            if (syntaxTree.FileNode != null)
            {
                FindAndCollectEntryPoints(syntaxTree.FileNode, entryPoints, parentMap);
            }
        }

        if (entryPoints.Count > 0)
        {
            foreach (var ep in entryPoints.DistinctBy(e => e.Id))
            {
                if (project.IsLibrary && ep.EntryType is EntryPointType.MainMethod or EntryPointType.TopLevel or EntryPointType.ScriptMain)
                {
                    continue;
                }

                if (project.Children.All(c => c.Id != ep.Id))
                {
                    project.Children.Add(ep);
                }

                if (existingRelKeys.Add((project.Id, ep.Id, OntologyConstants.Relationships.HasEntrypoint)))
                {
                    semanticRelationships.Add(new Relationship(
                        project.Id,
                        ep.Id,
                        OntologyConstants.Relationships.HasEntrypoint));
                }

                if (existingRelKeys.Add((ep.Id, project.Id, OntologyConstants.Relationships.BelongsTo)))
                {
                    semanticRelationships.Add(new Relationship(
                        ep.Id,
                        project.Id,
                        OntologyConstants.Relationships.BelongsTo));
                }

                if (parentMap.TryGetValue(ep.Id, out var parentId) &&
                    !parentId.Contains($":{OntologyConstants.IdPrefixes.File}:", StringComparison.Ordinal))
                {
                    var implRel = new ImplementedByRelationship(ep.Id, parentId);
                    ctx.AddGlobalProjectDependency(Relationship.FromRelationship(implRel));

                    var fnImplRel = new ImplementsRelationship(parentId, ep.Id);
                    ctx.AddGlobalProjectDependency(Relationship.FromRelationship(fnImplRel));
                }
            }
        }
    }

    private void FindAndCollectEntryPoints(
        IOntologyNode node,
        List<EntryPointNode> entryPoints,
        Dictionary<string, string> parentMap)
    {
        var epsInNode = node.Children.OfType<EntryPointNode>().ToList();

        foreach (var ep in epsInNode)
        {
            entryPoints.Add(ep);
            if (!node.Id.Contains($":{OntologyConstants.IdPrefixes.File}:", StringComparison.Ordinal) ||
                !parentMap.ContainsKey(ep.Id))
            {
                parentMap[ep.Id] = node.Id;
            }
            node.Children.Remove(ep);
        }

        var childrenCopy = node.Children.ToList();

        foreach (var child in childrenCopy)
        {
            FindAndCollectEntryPoints(child, entryPoints, parentMap);
        }
    }

    private static void LinkEntryPointsToModuleInit(
        Layer2Result l2Result,
        Layer3Result l3Result,
        List<FileNode> allFiles,
        List<ProjectNode> allProjects,
        HashSet<(string From, string To, string Kind)> existingRelKeys,
        List<Relationship> semanticRelationships)
    {
        var modInitByFileId = new Dictionary<string, FunctionNode>(StringComparer.Ordinal);
        var modInitByFilePath = new Dictionary<string, FunctionNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var st in l3Result.SyntaxTrees)
        {
            var fn = st.FileNode;
            if (fn == null) continue;
            var modInit = fn.Children.OfType<FunctionNode>().FirstOrDefault(f => f.Name == "<module_init>" || f.IsModuleInit == true);
            if (modInit == null) continue;
            modInitByFileId[fn.Id] = modInit;
            if (!string.IsNullOrEmpty(fn.Path))
            {
                modInitByFilePath[fn.Path.Replace('\\', '/').Trim('/')] = modInit;
            }
            if (!string.IsNullOrEmpty(fn.FullPath))
            {
                modInitByFilePath[fn.FullPath.Replace('\\', '/').Trim('/')] = modInit;
            }
        }

        if (modInitByFileId.Count == 0) return;

        foreach (var dep in l2Result.ProjectDependencies)
        {
            if (dep.Kind is RelationshipKind.Triggers or RelationshipKind.DeclaredIn &&
                modInitByFileId.TryGetValue(dep.To, out var targetModInit) &&
                dep.From.Contains($":{OntologyConstants.IdPrefixes.EntryPoint}:", StringComparison.Ordinal))
            {
                if (existingRelKeys.Add((dep.From, targetModInit.Id, OntologyConstants.Relationships.Triggers)))
                {
                    semanticRelationships.Add(new Relationship(dep.From, targetModInit.Id, OntologyConstants.Relationships.Triggers));
                }
            }
        }

        foreach (var proj in allProjects)
        {
            foreach (var ep in proj.Children.OfType<EntryPointNode>())
            {
                if (string.IsNullOrWhiteSpace(ep.FilePath)) continue;
                var normPath = ep.FilePath.Replace('\\', '/').Trim('/');
                if (modInitByFilePath.TryGetValue(normPath, out var modInit))
                {
                    if (existingRelKeys.Add((ep.Id, modInit.Id, OntologyConstants.Relationships.Triggers)))
                    {
                        semanticRelationships.Add(new Relationship(ep.Id, modInit.Id, OntologyConstants.Relationships.Triggers));
                    }
                }
            }
        }
    }
}
