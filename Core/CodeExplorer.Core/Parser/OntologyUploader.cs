using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeExplorer.Common;
using CodeExplorer.Database;

namespace CodeExplorer.Parser;

public static class OntologyUploader
{
    public static async Task UploadNodeTreeAsync(IOntologyNode node, string? parentId, ParsingContext ctx)
    {
        // 1. Convert and upload the current node
        var dbNode = Node.FromNode(node);
        await ctx.EnqueueUploadNodesAsync(new List<Node> { dbNode });
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
            await ctx.EnqueueUploadRelationshipsAsync(new List<Relationship> { dbRel });
            ctx.AddRelsCount(1);
        }

        // 4. Collect unresolved references/dependencies
        if (node.References.Count > 0)
        {
            ctx.AddGlobalReferences(node.References);
        }

        // 5. Recursively upload all children
        foreach (var child in node.Children)
        {
            await UploadNodeTreeAsync(child, node.Id, ctx);
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

        return new ContainsRelationship(parentId, child.Id);
    }
}
