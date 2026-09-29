using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Thread-safe registry for pluggable ILibraryConfigurationDescriptor instances.
/// Allows language-specific parser assemblies to register configuration handlers without polluting Core.
/// </summary>
public static class LibraryConfigurationRegistry
{
    private static readonly List<ILibraryConfigurationDescriptor> _descriptors = [];

    public static void Register(ILibraryConfigurationDescriptor descriptor)
    {
        lock (_descriptors)
        {
            if (!_descriptors.Any(d => d.GetType() == descriptor.GetType()))
            {
                _descriptors.Add(descriptor);
            }
        }
    }

    public static bool TryProcess(
        string key,
        string value,
        string relativePath,
        string fileNodeId,
        string workspaceId,
        IOntologyNode containerNode,
        List<Relationship> relationships,
        ParsingContext ctx)
    {
        ILibraryConfigurationDescriptor[] snapshot;
        lock (_descriptors)
        {
            snapshot = [.. _descriptors];
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i].TryHandle(key, value, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx))
            {
                return true;
            }
        }

        return false;
    }
}
