using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public static class OntologyUploader
{
    public static async Task UploadNodeTreeAsync(IOntologyNode node, string? parentId, ParsingContext ctx)
    {
        var collectedNodes = new List<Node>();
        var collectedRelationships = new List<Relationship>();
        var visitedNodeIds = new HashSet<string>();

        CollectTreeElements(node, null, ctx, collectedNodes, collectedRelationships, visitedNodeIds);

        // Upload nodes in chunks of 1000
        for (var i = 0; i < collectedNodes.Count; i += 1000)
        {
            var chunk = collectedNodes.GetRange(i, Math.Min(1000, collectedNodes.Count - i));
            await ctx.EnqueueUploadNodesAsync(chunk);
        }

        // Upload relationships in chunks of 1000
        for (var i = 0; i < collectedRelationships.Count; i += 1000)
        {
            var chunk = collectedRelationships.GetRange(i, Math.Min(1000, collectedRelationships.Count - i));
            await ctx.EnqueueUploadRelationshipsAsync(chunk);
        }
    }

    private static void CollectTreeElements(
        IOntologyNode node,
        IOntologyNode? parentNode,
        ParsingContext ctx,
        List<Node> collectedNodes,
        List<Relationship> collectedRelationships,
        HashSet<string> visitedNodeIds)
    {
        var isDuplicate = visitedNodeIds.Contains(node.Id);

        // 3. Link to parent if present (we still link, even if node is duplicate, to capture secondary parent relationships)
        if (parentNode != null)
        {
            var ontologyRel = GetRelationship(parentNode.Id, node);
            var dbRel = Relationship.FromRelationship(ontologyRel);
            collectedRelationships.Add(dbRel);
            ctx.AddRelsCount(1);
        }

        if (isDuplicate) return;
        visitedNodeIds.Add(node.Id);

        // 1. Convert and collect the current node
        var dbNode = Node.FromNode(node);
        collectedNodes.Add(dbNode);
        ctx.IncrementNodeKind(node.Kind);
        ctx.AddNodesCount(1);

        // 2. Map global symbols for reference resolution
        if (node.Kind == OntologyConstants.NodeLabels.Type ||
            node.Kind == OntologyConstants.NodeLabels.Function ||
            node.Kind == OntologyConstants.NodeLabels.Procedure ||
            node.Kind == OntologyConstants.NodeLabels.Table ||
            node.Kind == OntologyConstants.NodeLabels.EntryPoint ||
            node.Kind == OntologyConstants.NodeLabels.Endpoint)
        {
            if (dbNode.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
            {
                ctx.AddGlobalSymbol(node.Kind, nameStr, node.Id);
                if (node.Kind == OntologyConstants.NodeLabels.Endpoint || node.Kind == OntologyConstants.NodeLabels.EntryPoint)
                {
                    ctx.AddGlobalSymbol(node.Kind, nameStr.Replace(":", " "), node.Id);
                }
                if (node.Kind == OntologyConstants.NodeLabels.Function && parentNode != null &&
                    parentNode.Kind == OntologyConstants.NodeLabels.Type)
                {
                    var parentName = parentNode.GetType().GetProperty("Name")?.GetValue(parentNode) as string;
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        ctx.AddGlobalSymbol(node.Kind, $"{parentName}.{nameStr}", node.Id);
                    }
                }
            }
        }

        // Special: If Project, link it to GitSettings via USES_GIT and Folder/Workspace via LOCATED_IN
        if (node.Kind == OntologyConstants.NodeLabels.Project)
        {
            var gitDir = Path.Combine(ctx.AbsoluteWorkspacePath, ".git");
            if (Directory.Exists(gitDir))
            {
                var gitSettingsId = $"{ctx.WorkspaceId}:gitsettings";
                var usesGitRel = Relationship.FromRelationship(new UsesGitRelationship(node.Id, gitSettingsId));
                collectedRelationships.Add(usesGitRel);
                ctx.AddRelsCount(1);
            }

            // Emit LOCATED_IN relationship to Folder or Workspace
            string targetId;
            var projectPath = node.Path;
            if (string.IsNullOrEmpty(projectPath) || projectPath == ".")
            {
                targetId = ctx.WorkspaceId;
            }
            else
            {
                var absoluteFolderPath = Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, projectPath)).Replace('\\', '/');
                targetId = $"{ctx.WorkspaceId}:folder:{absoluteFolderPath}";
            }

            var locatedInRel = Relationship.FromRelationship(new LocatedInRelationship(node.Id, targetId));
            collectedRelationships.Add(locatedInRel);
            ctx.AddRelsCount(1);
        }

        // 4. Collect unresolved references/dependencies
        if (node.References.Count > 0)
        {
            ctx.AddGlobalReferences(node.References);
        }

        // 5. Recursively collect all children
        foreach (var child in node.Children)
        {
            CollectTreeElements(child, node, ctx, collectedNodes, collectedRelationships, visitedNodeIds);
        }
    }

    private static IOntologyRelationship GetRelationship(string parentId, IOntologyNode child)
    {
        if (child.Kind == OntologyConstants.NodeLabels.SyntaxStructure || child.Kind == OntologyConstants.NodeLabels.SemanticStructure)
        {
            return new BelongsToRelationship(child.Id, parentId);
        }

        if (parentId.Contains("files_structure") || parentId.Contains("syntax_structure") || parentId.Contains("semantic_structure"))
        {
            return new ContainsRelationship(parentId, child.Id);
        }

        if (child.Kind == OntologyConstants.NodeLabels.Package)
        {
            return new DependsOnRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.Project)
        {
            if (parentId.Contains(":package:"))
            {
                return new ImplementedByRelationship(parentId, child.Id);
            }
            return new LocatedInRelationship(child.Id, parentId);
        }
        if (child.Kind == OntologyConstants.NodeLabels.Database)
        {
            if (parentId.Contains(":symbol:") || parentId.Contains(":function:") || parentId.Contains(":query:"))
            {
                return new QueriedByRelationship(child.Id, parentId);
            }
            return new UsesDbRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.Topic)
        {
            return new PublishedByRelationship(child.Id, parentId);
        }
        if (child.Kind == OntologyConstants.NodeLabels.Endpoint)
        {
            return new ExposedByRelationship(child.Id, parentId);
        }
        if (child.Kind == OntologyConstants.NodeLabels.ApiInUse)
        {
            return new UsesApiRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.CloudService)
        {
            return new UsesCloudRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            if (parentId.Contains(":entrypoints"))
            {
                return new ExposedByRelationship(child.Id, parentId); // EntryPoint -> EXPOSED_BY -> EntryPoints
            }
            return new ImplementedByRelationship(child.Id, parentId); // EntryPoint -> IMPLEMENTED_BY -> Function
        }
        if (child.Kind == OntologyConstants.NodeLabels.ExternalService)
        {
            return new CalledByRelationship(child.Id, parentId); // ExternalService -> CALLED_BY -> Function
        }

        if (IsCodeEntityKind(child.Kind))
        {
            if (parentId.Contains(":file:"))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            if (parentId.Contains(":project:"))
            {
                if (child.Kind == OntologyConstants.NodeLabels.Type)
                {
                    return new DeclaresTypeRelationship(parentId, child.Id);
                }
            }
            if (IsCodeEntityId(parentId))
            {
                // Parent is Type
                if (parentId.Contains(":type:") || parentId.Contains(":Type:") || parentId.Contains(":class:") || parentId.Contains(":interface:"))
                {
                    if (child.Kind == OntologyConstants.NodeLabels.Function)
                    {
                        return new HasMethodRelationship(parentId, child.Id);
                    }
                    if (child.Kind == OntologyConstants.NodeLabels.Member)
                    {
                        return new HasMemberRelationship(parentId, child.Id);
                    }
                }
                // Parent is Function
                if (parentId.Contains(":function:"))
                {
                    if (child.Kind == OntologyConstants.NodeLabels.Member)
                    {
                        return new HasVariableRelationship(parentId, child.Id);
                    }
                }
                return new DeclaresRelationship(parentId, child.Id);
            }
        }

        return new ContainsRelationship(parentId, child.Id);
    }

    private static bool IsCodeEntityId(string id)
    {
        var lower = id.ToLowerInvariant();
        return lower.Contains(":symbol:") ||
               lower.Contains(":class:") ||
               lower.Contains(":interface:") ||
               lower.Contains(":type:") ||
               lower.Contains(":function:") ||
               lower.Contains(":variable:") ||
               lower.Contains(":member:") ||
               lower.Contains(":procedure:") ||
               lower.Contains(":query:") ||
               lower.Contains(":table:");
    }

    private static bool IsCodeEntityKind(string kind)
    {
        return kind == OntologyConstants.NodeLabels.Type ||
               kind == OntologyConstants.NodeLabels.Function ||
               kind == OntologyConstants.NodeLabels.Member ||
               kind == OntologyConstants.NodeLabels.Query ||
               kind == OntologyConstants.NodeLabels.Procedure ||
               kind == OntologyConstants.NodeLabels.Table;
    }
}
