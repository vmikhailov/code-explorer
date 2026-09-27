using CodeExplorer.Common;

namespace CodeExplorer.Core.Common.Nodes;

public interface IOntologyNode
{
    string Id { get; }
    string Kind { get; }
    string Path { get; }
    Dictionary<string, string>? Extensions { get; set; }
    List<IOntologyNode> Children { get; }
    List<Reference> References { get; }
}

public static class OntologyNodeExtensions
{
    public static void SetExtension(this IOntologyNode node, string key, string value)
    {
        node.Extensions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        node.Extensions[key] = value;
    }
}
