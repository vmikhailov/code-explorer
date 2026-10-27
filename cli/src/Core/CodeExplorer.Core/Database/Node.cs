using CodeExplorer.Core.Common.Nodes;

namespace CodeExplorer.Core.Database;

public record Node(string Id, string Kind, Dictionary<string, object> Properties)
{
    public static Node FromNode(IOntologyNode node) => NodeFactory.Create(node);
}
