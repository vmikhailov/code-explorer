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

        // 1. Parse workspace-level configuration files (e.g. root .env) first
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
                if (project.Children.All(c => c.Id != semNode.Id))
                {
                    project.Children.Add(semNode);
                    semanticNodes.Add(semNode);
                }
                if (semNode is ExternalServiceNode es)
                {
                    semanticRelationships.Add(new Relationship(
                        project.Id,
                        es.Id,
                        OntologyConstants.Relationships.ServiceCall,
                        new Dictionary<string, object>
                        {
                            ["dependency_type"] = "service_call",
                            ["is_semantic"] = "true"
                        }));
                }
            }

            // Group EntryPoints
            GroupEntryPoints(project, projectTrees, ctx);
        }

        // 4. Materialize first-class semantic workload nodes under SemanticStructure
        var workloadByProjectId = new Dictionary<string, string>();
        foreach (var project in l3Result.Prev.Projects)
        {
            var extensions = new Dictionary<string, string>(project.Extensions ?? new())
            {
                ["project_id"] = project.Id,
                ["role"] = project.Role,
                ["is_library"] = project.IsLibrary ? "true" : "false"
            };

            var cleanName = WorkspaceConventions.NormalizeServiceName(project.Name);
            if (string.IsNullOrEmpty(cleanName)) cleanName = project.Name;
            extensions["raw_name"] = project.Name;
            extensions["clean_name"] = cleanName;

            var isTestProject = project.Role == OntologyConstants.ProjectRoles.Test ||
                                string.Equals(project.Extensions?.GetValueOrDefault("primary_role"), "TestFramework", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(project.Extensions?.GetValueOrDefault("has_test_runner"), "true", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(project.Extensions?.GetValueOrDefault("is_test_project"), "true", StringComparison.OrdinalIgnoreCase);

            IOntologyNode workloadNode = project.Role switch
            {
                OntologyConstants.ProjectRoles.FrontendApp => new AppNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.App}:{cleanName}",
                    cleanName,
                    project.Path,
                    project.ProjectType,
                    extensions),

                OntologyConstants.ProjectRoles.Service => new ServiceNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Service}:{cleanName}",
                    cleanName,
                    project.Path,
                    project.ProjectType,
                    extensions),

                OntologyConstants.ProjectRoles.Worker => new WorkerNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Worker}:{cleanName}",
                    cleanName,
                    project.Path,
                    project.ProjectType,
                    extensions),

                OntologyConstants.ProjectRoles.SharedLibrary => isTestProject
                    ? new TestSuiteNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.TestSuite}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType,
                        extensions)
                    : new LibraryNode(
                        $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Library}:{cleanName}",
                        cleanName,
                        project.Path,
                        project.ProjectType,
                        extensions),

                OntologyConstants.ProjectRoles.CliTool => new CliToolNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.CliTool}:{cleanName}",
                    cleanName,
                    project.Path,
                    project.ProjectType,
                    extensions),

                OntologyConstants.ProjectRoles.Test => new TestSuiteNode(
                    $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.TestSuite}:{cleanName}",
                    cleanName,
                    project.Path,
                    project.ProjectType,
                    extensions),

                _ => isTestProject
                    ? new TestSuiteNode($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.TestSuite}:{cleanName}", cleanName, project.Path, project.ProjectType, extensions)
                    : project.IsLibrary
                        ? new LibraryNode($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Library}:{cleanName}", cleanName, project.Path, project.ProjectType, extensions)
                        : new ServiceNode($"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Service}:{cleanName}", cleanName, project.Path, project.ProjectType, extensions)
            };

            if (!semanticStructureNode.Children.Any(c => c.Id == workloadNode.Id))
            {
                semanticStructureNode.Children.Add(workloadNode);
                semanticNodes.Add(workloadNode);
            }

            semanticRelationships.Add(new Relationship(project.Id, workloadNode.Id, OntologyConstants.Relationships.Deploys, new Dictionary<string, object>()));

            workloadByProjectId[project.Id] = workloadNode.Id;

            // Attach project endpoints, entry points, and external services to the workload node
            foreach (var child in project.Children.Where(c => c is EndpointNode or EntryPointNode or ExternalServiceNode).ToList())
            {
                if (!workloadNode.Children.Any(c => c.Id == child.Id))
                {
                    workloadNode.Children.Add(child);
                }
                if (child is ExternalServiceNode es)
                {
                    if (!semanticRelationships.Any(r => r.From == workloadNode.Id && r.To == es.Id && r.Kind == OntologyConstants.Relationships.ServiceCall))
                    {
                        semanticRelationships.Add(new Relationship(
                            workloadNode.Id,
                            es.Id,
                            OntologyConstants.Relationships.ServiceCall,
                            new Dictionary<string, object>
                            {
                                ["dependency_type"] = "service_call",
                                ["is_semantic"] = "true"
                            }));
                    }
                }
            }
        }

        // Materialize TESTS relationships from TestSuites to target workloads
        foreach (var dep in l3Result.Prev.ProjectDependencies)
        {
            if (dep.Kind == OntologyConstants.Relationships.DependsOn &&
                workloadByProjectId.TryGetValue(dep.From, out var sourceWorkloadId) &&
                workloadByProjectId.TryGetValue(dep.To, out var targetWorkloadId) &&
                sourceWorkloadId != targetWorkloadId)
            {
                var sourceWorkload = semanticStructureNode.Children.FirstOrDefault(c => c.Id == sourceWorkloadId);
                if (sourceWorkload is TestSuiteNode && !semanticRelationships.Any(r => r.From == sourceWorkloadId && r.To == targetWorkloadId && r.Kind == OntologyConstants.Relationships.Tests))
                {
                    semanticRelationships.Add(new Relationship(
                        sourceWorkloadId,
                        targetWorkloadId,
                        OntologyConstants.Relationships.Tests,
                        new Dictionary<string, object>
                        {
                            ["dependency_type"] = "tests"
                        }));
                }
            }
        }

        // Collect all semantic nodes from projects and semanticStructureNode
        foreach (var project in l3Result.Prev.Projects)
        {
            CollectSemanticNodes(project, semanticNodes);
        }
        CollectSemanticNodes(semanticStructureNode, semanticNodes);
        semanticNodes = [.. semanticNodes.DistinctBy(n => n.Id)];

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
            if (child is DatabaseNode || child is EndpointNode || child is QueryNode || child is ExternalServiceNode || child is TopicNode || child is CloudServiceNode || child is ApiInUseNode || child is TableNode || child is DataSetNode || child is ServiceNode || child is AppNode || child is WorkerNode || child is LibraryNode || child is CliToolNode || child is EntryPointNode)
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
