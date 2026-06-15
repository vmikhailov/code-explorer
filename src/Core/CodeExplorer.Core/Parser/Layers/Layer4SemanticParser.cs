using CodeExplorer.Common;
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
        ctx.Log("[Layer4SemanticParser] Starting semantic enrichment pass...");

        var semanticNodeId = $"{ctx.WorkspaceId}:semantic_structure";
        var semanticStructureNode = new SemanticStructureNode(semanticNodeId, "SemanticStructure", l3Result.Prev.Prev.Workspace.Path);
        l3Result.Prev.Prev.Workspace.Children.Add(semanticStructureNode);
        ctx.SemanticStructure = semanticStructureNode;

        var semanticNodes = new List<IOntologyNode>();
        var semanticRelationships = new List<Relationship>();

        foreach (var project in l3Result.Prev.Projects)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            var projectSemanticId = $"{ctx.WorkspaceId}:project:{project.Path}:project_semantic";
            var projectSemanticNode = new ProjectSemanticNode(projectSemanticId, "ProjectSemantic", project.Path);
            semanticStructureNode.Children.Add(projectSemanticNode);

            var belongsToRel = Relationship.FromRelationship(new BelongsToRelationship(projectSemanticId, project.Id));
            semanticRelationships.Add(belongsToRel);

            // Find project parser
            var projectAbsDir = Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, project.Path)).Replace('\\', '/');
            var filesInDir = Directory.GetFiles(projectAbsDir);
            var projectParser = WorkspaceIndexer._projectParsers.FirstOrDefault(p => p.IsProjectDirectory(projectAbsDir, filesInDir));

            if (projectParser == null) continue;

            // Get syntax trees belonging to this project
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
                if (!projectSemanticNode.Children.Any(c => c.Id == semNode.Id))
                {
                    projectSemanticNode.Children.Add(semNode);
                    semanticNodes.Add(semNode);
                }
            }

            // Group EntryPoints
            GroupEntryPoints(projectSemanticNode, projectTrees, ctx);
        }

        // 3. Upload the entire Workspace Node tree using OntologyUploader
        ctx.CancellationToken.ThrowIfCancellationRequested();
        ctx.Log("[Layer4SemanticParser] Uploading the entire Workspace Node tree...");
        await OntologyUploader.UploadNodeTreeAsync(l3Result.Prev.Prev.Workspace, null, ctx);

        ctx.Log($"[Layer4SemanticParser] Semantic enrichment pass complete. Identified {semanticNodes.Count} semantic nodes.");
        return new Layer4Result(l3Result, semanticStructureNode, semanticNodes, semanticRelationships);
    }

    private static bool IsEnclosedInProject(FileNode file, ProjectNode project, List<ProjectNode> projects)
    {
        ProjectNode? bestMatch = null;
        int bestMatchLength = -1;

        foreach (var p in projects)
        {
            var pPath = p.Path;
            if (pPath == "")
            {
                if (bestMatchLength < 0)
                {
                    bestMatch = p;
                    bestMatchLength = 0;
                }
                continue;
            }

            var pPrefix = pPath + "/";
            if (file.Path.StartsWith(pPrefix, StringComparison.OrdinalIgnoreCase))
            {
                if (pPrefix.Length > bestMatchLength)
                {
                    bestMatch = p;
                    bestMatchLength = pPrefix.Length;
                }
            }
        }

        return bestMatch?.Id == project.Id;
    }

    private static void CollectSemanticNodes(IOntologyNode node, List<IOntologyNode> semanticNodes)
    {
        foreach (var child in node.Children)
        {
            if (child is DatabaseNode || child is EndpointNode || child is QueryNode || child is ExternalServiceNode || child is TopicNode || child is CloudServiceNode || child is ApiInUseNode)
            {
                semanticNodes.Add(child);
            }
            CollectSemanticNodes(child, semanticNodes);
        }
    }

    private void GroupEntryPoints(ProjectSemanticNode? projectSemanticNode, List<SyntaxTree> projectTrees, ParsingContext ctx)
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
            if (projectSemanticNode != null)
            {
                foreach (var ep in entryPoints)
                {
                    projectSemanticNode.Children.Add(ep);

                    if (parentMap.TryGetValue(ep.Id, out var parentId))
                    {
                        var implRel = new ImplementedByRelationship(ep.Id, parentId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(implRel));
                    }
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
