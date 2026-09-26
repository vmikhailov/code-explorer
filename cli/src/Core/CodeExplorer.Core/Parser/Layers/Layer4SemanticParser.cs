using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser.Layers;

public class Layer4SemanticParser
{
    public async Task<Layer4Result> ParseAsync(Layer3Result l3Result, ParsingContext ctx)
    {
        ctx.Log("[Layer4] Starting semantic enrichment pass...");

        var semanticNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.SemanticStructure}";
        var semanticStructureNode = new SemanticStructureNode(semanticNodeId, "SemanticStructure", l3Result.Prev.Prev.Workspace.Path);
        l3Result.Prev.Prev.Workspace.Children.Add(semanticStructureNode);
        ctx.SemanticStructure = semanticStructureNode;

        var semanticNodes = new List<IOntologyNode>();
        var semanticRelationships = new List<Relationship>();
        var nProject = 0;

        // 1. Parse workspace-level configuration and infrastructure files (e.g. docker-compose.yml, root .env) first
        var workspaceRootFiles = l3Result.Prev.Prev.Files.Where(f => !l3Result.Prev.Projects.Any(p => IsEnclosedInProject(f, p, l3Result.Prev.Projects))).ToList();
        foreach (var wfile in workspaceRootFiles)
        {
            if (ConfigurationParser.IsConfigurationFile(wfile.Name))
            {
                ConfigurationParser.ParseAndEnrich(wfile.FullPath, wfile.Path, ctx.WorkspaceId, semanticStructureNode, semanticRelationships, ctx);
            }
        }

        foreach (var project in l3Result.Prev.Projects)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            nProject++;
            ctx.Log($"[Layer4] Enriching project {nProject} of {l3Result.Prev.Projects.Count} at:'{project.Path}' with semantic information...");

            // Find project parser
            var projectAbsDir = Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, project.Path)).Replace('\\', '/');
            var filesInDir = Directory.GetFiles(projectAbsDir);
            var projectParser = WorkspaceIndexer._projectParsers.FirstOrDefault(p => p.IsProjectDirectory(projectAbsDir, filesInDir));

            if (projectParser == null) continue;

            // 2. Parse project-level configuration files before syntax enrichment so resources are available in registry
            var projectFiles = l3Result.Prev.Prev.Files.Where(f => IsEnclosedInProject(f, project, l3Result.Prev.Projects)).ToList();
            foreach (var pfile in projectFiles)
            {
                if (ConfigurationParser.IsConfigurationFile(pfile.Name))
                {
                    ConfigurationParser.ParseAndEnrich(pfile.FullPath, pfile.Path, ctx.WorkspaceId, project, semanticRelationships, ctx);
                }
            }

            // 3. Get syntax trees belonging to this project and run semantic enrichers
            var projectTrees = l3Result.SyntaxTrees.Where(st => IsEnclosedInProject(st.FileNode, project, l3Result.Prev.Projects)).ToList();

            foreach (var syntaxTree in projectTrees)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var enricher = projectParser.GetSyntaxEnricher(syntaxTree);
                await enricher.EnrichAsync(project, ctx);
            }

            // Collect semantic nodes parsed from files
            var projectSemanticNodes = new List<IOntologyNode>();
            foreach (var syntaxTree in projectTrees)
            {
                if (syntaxTree.FileNode != null)
                {
                    CollectSemanticNodes(syntaxTree.FileNode, projectSemanticNodes);
                }
            }

            foreach (var semNode in projectSemanticNodes)
            {
                if (!project.Children.Any(c => c.Id == semNode.Id))
                {
                    project.Children.Add(semNode);
                    semanticNodes.Add(semNode);
                }
            }

            // Group EntryPoints
            GroupEntryPoints(project, projectTrees, ctx);
        }

        // 4. Materialize first-class semantic workload nodes under SemanticStructure
        foreach (var project in l3Result.Prev.Projects)
        {
            var extensions = new Dictionary<string, string>(project.Extensions ?? new())
            {
                ["project_id"] = project.Id,
                ["role"] = project.Role,
                ["is_library"] = project.IsLibrary ? "true" : "false"
            };

            IOntologyNode workloadNode = project.Role switch
            {
                OntologyConstants.ProjectRoles.FrontendApp => new AppNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.App}:{project.Name}",
                    project.Name,
                    project.Path,
                    project.ProjectType,
                    extensions),

                OntologyConstants.ProjectRoles.Service => new ServiceNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Service}:{project.Name}",
                    project.Name,
                    project.Path,
                    project.ProjectType,
                    extensions),

                OntologyConstants.ProjectRoles.Worker => new WorkerNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Worker}:{project.Name}",
                    project.Name,
                    project.Path,
                    project.ProjectType,
                    extensions),

                OntologyConstants.ProjectRoles.SharedLibrary => new LibraryNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Library}:{project.Name}",
                    project.Name,
                    project.Path,
                    project.ProjectType,
                    extensions),

                OntologyConstants.ProjectRoles.CliTool => new CliToolNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.CliTool}:{project.Name}",
                    project.Name,
                    project.Path,
                    project.ProjectType,
                    extensions),

                _ => project.IsLibrary
                    ? new LibraryNode($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Library}:{project.Name}", project.Name, project.Path, project.ProjectType, extensions)
                    : new ServiceNode($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Service}:{project.Name}", project.Name, project.Path, project.ProjectType, extensions)
            };

            if (!semanticStructureNode.Children.Any(c => c.Id == workloadNode.Id))
            {
                semanticStructureNode.Children.Add(workloadNode);
                semanticNodes.Add(workloadNode);
            }

            semanticRelationships.Add(new Relationship(project.Id, workloadNode.Id, OntologyConstants.Relationships.Deploys, new Dictionary<string, object>()));

            // Attach project endpoints and entry points to the workload node
            foreach (var child in project.Children.Where(c => c is EndpointNode or EntryPointNode).ToList())
            {
                if (!workloadNode.Children.Any(c => c.Id == child.Id))
                {
                    workloadNode.Children.Add(child);
                }
            }
        }

        // Collect all semantic nodes from projects and semanticStructureNode
        foreach (var project in l3Result.Prev.Projects)
        {
            CollectSemanticNodes(project, semanticNodes);
        }
        CollectSemanticNodes(semanticStructureNode, semanticNodes);
        semanticNodes = semanticNodes.DistinctBy(n => n.Id).ToList();

        if (semanticRelationships.Count > 0)
        {
            await ctx.DbClient.UploadRelationshipsAsync(semanticRelationships);
            ctx.TotalRelsCount += semanticRelationships.Count;
        }

        // 3. Upload the entire Workspace Node tree using OntologyUploader
        ctx.CancellationToken.ThrowIfCancellationRequested();
        ctx.Log("[Layer4] Uploading the entire Workspace Node tree...");
        await OntologyUploader.UploadNodeTreeAsync(l3Result.Prev.Prev.Workspace, null, ctx);

        ctx.Log($"[Layer4] Semantic enrichment pass complete. Identified {semanticNodes.Count} semantic nodes.");
        return new Layer4Result(l3Result, semanticStructureNode, semanticNodes, semanticRelationships);
    }

    private static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects) =>
        Layer2ProjectParser.IsEnclosedInProject(file, project, projects);

    private static void CollectSemanticNodes(IOntologyNode node, List<IOntologyNode> semanticNodes)
    {
        foreach (var child in node.Children)
        {
            if (child is DatabaseNode || child is EndpointNode || child is QueryNode || child is ExternalServiceNode || child is TopicNode || child is CloudServiceNode || child is ApiInUseNode || child is TableNode || child is DataSetNode || child is ServiceNode || child is AppNode || child is WorkerNode || child is LibraryNode || child is CliToolNode)
            {
                semanticNodes.Add(child);
            }
            CollectSemanticNodes(child, semanticNodes);
        }
    }

    private void GroupEntryPoints(ProjectNode? project, List<SyntaxTree> projectTrees, ParsingContext ctx)
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

        if (entryPoints.Count > 0 && project != null)
        {
            foreach (var ep in entryPoints)
            {
                project.Children.Add(ep);

                if (parentMap.TryGetValue(ep.Id, out var parentId))
                {
                    var implRel = new ImplementedByRelationship(ep.Id, parentId);
                    ctx.AddGlobalProjectDependency(Relationship.FromRelationship(implRel));
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
            parentMap[ep.Id] = node.Id;
            node.Children.Remove(ep);
        }

        var childrenCopy = node.Children.ToList();

        foreach (var child in childrenCopy)
        {
            FindAndCollectEntryPoints(child, entryPoints, parentMap);
        }
    }
}
