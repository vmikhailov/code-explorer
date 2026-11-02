using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

public static class OntologyPruner
{
    public static bool PruneEmptyFolders(IOntologyNode node)
    {
        // 1. Recursively prune children first (in reverse to support safe deletion during iteration)
        for (var i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            if (child.Kind is NodeKind.Folder or NodeKind.FilesStructure or NodeKind.ProjectsStructure or NodeKind.SyntaxStructure or NodeKind.ProjectSyntax or NodeKind.SemanticStructure ||
                child is ProjectNode)
            {
                var shouldPruneChild = PruneEmptyFolders(child);
                if (shouldPruneChild)
                {
                    node.Children.RemoveAt(i);
                }
            }
        }

        // 2. Check if this node or any descendants have content (File, Package, Type, Endpoint, Query, etc.)
        return !HasContentSubtree(node);
    }

    private static bool HasContentSubtree(IOntologyNode node)
    {
        if (node.Kind is NodeKind.File or NodeKind.EntryPoint or NodeKind.Endpoint or NodeKind.Database or NodeKind.Topic or NodeKind.CloudService or NodeKind.ExternalService or NodeKind.ApiInUse or NodeKind.Type or NodeKind.Function or NodeKind.Query or NodeKind.GitSettings)
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (HasContentSubtree(child)) return true;
        }

        return false;
    }
}
