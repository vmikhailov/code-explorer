using CodeExplorer.Common;

namespace CodeExplorer.Core.Common.Nodes;

public interface IOntologyNode
{
    string Id { get; }
    string Kind { get; }
    string Path { get; }
    Dictionary<string, string>? Extensions { get; }
    List<IOntologyNode> Children { get; }
    List<Reference> References { get; }
}

public static class OntologyNodeExtensions
{
    public static void SetExtension(this IOntologyNode node, string key, string value)
    {
        var extensions = node.Extensions;
        if (extensions == null)
        {
            extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var backingField = node.GetType().GetField("<Extensions>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (backingField != null)
            {
                backingField.SetValue(node, extensions);
            }
            else
            {
                var prop = node.GetType().GetProperty("Extensions");
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(node, extensions);
                }
            }
        }
        if (node.Extensions != null)
        {
            node.Extensions[key] = value;
        }
    }
}
