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

        CollectTreeElements(node, null, ctx, collectedNodes, collectedRelationships);

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
        List<Relationship> collectedRelationships)
    {
        // 1. Convert and collect the current node
        var dbNode = Node.FromNode(node);
        collectedNodes.Add(dbNode);
        ctx.IncrementNodeKind(node.Kind);
        ctx.AddNodesCount(1);

        // 2. Map global symbols for reference resolution
        if (node.Kind == OntologyConstants.NodeLabels.Class ||
            node.Kind == OntologyConstants.NodeLabels.Interface ||
            node.Kind == OntologyConstants.NodeLabels.Function ||
            node.Kind == OntologyConstants.NodeLabels.Procedure ||
            node.Kind == OntologyConstants.NodeLabels.Table ||
            node.Kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            if (dbNode.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
            {
                ctx.AddGlobalSymbol(node.Kind, nameStr, node.Id);
                if (node.Kind == OntologyConstants.NodeLabels.Function && parentNode != null &&
                    (parentNode.Kind == OntologyConstants.NodeLabels.Class || parentNode.Kind == OntologyConstants.NodeLabels.Interface))
                {
                    var parentName = parentNode.GetType().GetProperty("Name")?.GetValue(parentNode) as string;
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        ctx.AddGlobalSymbol(node.Kind, $"{parentName}.{nameStr}", node.Id);
                    }
                }
            }
        }

        // 3. Link to parent if present
        if (parentNode != null)
        {
            var ontologyRel = GetRelationship(parentNode.Id, node);
            var dbRel = Relationship.FromRelationship(ontologyRel);
            collectedRelationships.Add(dbRel);
            ctx.AddRelsCount(1);
        }

        // Removed direct Project-to-EntryPoint EXPOSES relationship as they are now grouped under EntryPoints node

        // Special: If Project, link it to GitSettings via USES_GIT
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
        }

        // 4. Collect unresolved references/dependencies
        if (node.References.Count > 0)
        {
            ctx.AddGlobalReferences(node.References);
        }

        // 5. Recursively collect all children
        foreach (var child in node.Children)
        {
            CollectTreeElements(child, node, ctx, collectedNodes, collectedRelationships);
        }
    }

    private static IOntologyRelationship GetRelationship(string parentId, IOntologyNode child)
    {
        if (child.Kind == OntologyConstants.NodeLabels.Package)
        {
            return new DependsOnRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.Project && parentId.Contains(":package:"))
        {
            return new ImplementedByRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.DB)
        {
            return new UsesDbRelationship(parentId, child.Id);
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
                return new ExposesRelationship(parentId, child.Id); // EntryPoints -> EXPOSES -> EntryPoint
            }
            return new ImplementedByRelationship(child.Id, parentId); // EntryPoint -> IMPLEMENTED_BY -> Function
        }
        if (child.Kind == OntologyConstants.NodeLabels.ExternalService)
        {
            return new CallsRelationship(parentId, child.Id); // Function -> CALLS -> ExternalService
        }

        return new ContainsRelationship(parentId, child.Id);
    }

    private static string FindProjectDirectory(string filePath, string workspacePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir != null && dir.Replace('\\', '/').StartsWith(workspacePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.GetFiles(dir, "*.csproj").Length > 0 ||
                File.Exists(Path.Combine(dir, "package.json")) ||
                File.Exists(Path.Combine(dir, "go.mod")))
            {
                return dir.Replace('\\', '/');
            }
            dir = Path.GetDirectoryName(dir);
        }
        return (Path.GetDirectoryName(filePath) ?? "").Replace('\\', '/');
    }
}
