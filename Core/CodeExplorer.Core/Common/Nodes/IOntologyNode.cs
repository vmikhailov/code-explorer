using CodeExplorer.Common;

namespace CodeExplorer.Core.Common.Nodes;

public interface IOntologyNode
{
    string Id { get; }
    string Kind { get; }
    Dictionary<string, string>? Extensions { get; }
    List<IOntologyNode> Children { get; }
    List<Reference> References { get; }
}
