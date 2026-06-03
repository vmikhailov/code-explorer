using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CodeExplorer.Common;
using CodeExplorer.Database;

namespace CodeExplorer.Parser;

public static class OntologyUploader
{
    public static async Task UploadNodeTreeAsync(IOntologyNode node, string? parentId, ParsingContext ctx)
    {
        var collectedNodes = new List<Node>();
        var collectedRelationships = new List<Relationship>();

        CollectTreeElements(node, parentId, ctx, collectedNodes, collectedRelationships);

        // Upload nodes in chunks of 1000
        for (int i = 0; i < collectedNodes.Count; i += 1000)
        {
            var chunk = collectedNodes.GetRange(i, Math.Min(1000, collectedNodes.Count - i));
            await ctx.EnqueueUploadNodesAsync(chunk);
        }

        // Upload relationships in chunks of 1000
        for (int i = 0; i < collectedRelationships.Count; i += 1000)
        {
            var chunk = collectedRelationships.GetRange(i, Math.Min(1000, collectedRelationships.Count - i));
            await ctx.EnqueueUploadRelationshipsAsync(chunk);
        }
    }

    private static void CollectTreeElements(
        IOntologyNode node, 
        string? parentId, 
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
            node.Kind == OntologyConstants.NodeLabels.Table)
        {
            if (dbNode.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
            {
                ctx.AddGlobalSymbol(node.Kind, nameStr, node.Id);
            }
        }

        // 3. Link to parent if present
        if (parentId != null)
        {
            var ontologyRel = GetRelationship(parentId, node);
            var dbRel = Relationship.FromRelationship(ontologyRel);
            collectedRelationships.Add(dbRel);
            ctx.AddRelsCount(1);
        }

        // Special: If EntryPoint, also link Project to EntryPoint via EXPOSES
        if (node.Kind == OntologyConstants.NodeLabels.EntryPoint && node.Extensions != null && node.Extensions.TryGetValue("file_path", out var relativeFilePath))
        {
            var fullPath = Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, relativeFilePath));
            var projectDir = FindProjectDirectory(fullPath, ctx.AbsoluteWorkspacePath);
            var projectNodeId = $"project:{projectDir}:";
            var exposesRel = Relationship.FromRelationship(new ExposesRelationship(projectNodeId, node.Id));
            collectedRelationships.Add(exposesRel);
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
            CollectTreeElements(child, node.Id, ctx, collectedNodes, collectedRelationships);
        }
    }

    private static IOntologyRelationship GetRelationship(string parentId, IOntologyNode child)
    {
        if (child.Kind == OntologyConstants.NodeLabels.Package)
        {
            return new DependsOnRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.Project && parentId.StartsWith("package:"))
        {
            return new ImplementedByRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.DB)
        {
            return new UsesDbRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            return new TriggersRelationship(child.Id, parentId); // EntryPoint -> TRIGGERS -> Function
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
