using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Parser;

public static class OntologyPruner
{
    public static bool PruneEmptyFolders(IOntologyNode node)
    {
        // 1. Recursively prune children first (in reverse to support safe deletion during iteration)
        for (int i = node.Children.Count - 1; i >= 0; i--)
        {
            var child = node.Children[i];
            if (child.Kind == OntologyConstants.NodeLabels.WorkspaceFolder ||
                child.Kind == OntologyConstants.NodeLabels.ProjectFolder ||
                child.Kind == OntologyConstants.NodeLabels.Project)
            {
                var shouldPruneChild = PruneEmptyFolders(child);
                if (shouldPruneChild)
                {
                    node.Children.RemoveAt(i);
                }
            }
        }

        // 2. Check if this node or any descendants have content (File, Package, Class, Query, etc.)
        return !HasContentSubtree(node);
    }

    private static bool HasContentSubtree(IOntologyNode node)
    {
        if (node.Kind == OntologyConstants.NodeLabels.File ||
            node.Kind == OntologyConstants.NodeLabels.EntryPoint ||
            node.Kind == OntologyConstants.NodeLabels.Class ||
            node.Kind == OntologyConstants.NodeLabels.Interface ||
            node.Kind == OntologyConstants.NodeLabels.Function ||
            node.Kind == OntologyConstants.NodeLabels.Query)
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
