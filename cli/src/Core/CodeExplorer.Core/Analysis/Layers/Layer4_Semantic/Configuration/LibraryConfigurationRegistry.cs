using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Thread-safe registry for pluggable ILibraryConfigurationDescriptor instances.
/// Allows language-specific parser assemblies to register configuration handlers without polluting Core.
/// </summary>
public static class LibraryConfigurationRegistry
{
    private static readonly List<ILibraryConfigurationDescriptor> _descriptors = [];
    private static volatile ILibraryConfigurationDescriptor[] _snapshot = [];

    static LibraryConfigurationRegistry()
    {
        Register(new DatabaseConfigurationDescriptor());
        Register(new MessageBrokerConfigurationDescriptor());
        Register(new CloudServicesConfigurationDescriptor());
    }

    public static void Register(ILibraryConfigurationDescriptor descriptor)
    {
        lock (_descriptors)
        {
            if (!_descriptors.Any(d => d.GetType() == descriptor.GetType()))
            {
                _descriptors.Add(descriptor);
                _descriptors.Sort((a, b) => a.Order.CompareTo(b.Order));
                _snapshot = [.. _descriptors];
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
        SemanticParseContext ctx)
    {
        var snapshot = _snapshot;
        for (var i = 0; i < snapshot.Length; i++)
        {
            if (snapshot[i].TryHandle(key, value, relativePath, fileNodeId, workspaceId, containerNode, relationships, ctx))
            {
                return true;
            }
        }

        return false;
    }
}
