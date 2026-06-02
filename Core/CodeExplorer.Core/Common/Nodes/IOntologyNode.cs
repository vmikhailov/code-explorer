namespace CodeExplorer.Common;

public interface IOntologyNode
{
    string Id { get; }
    string Kind { get; }
    Dictionary<string, string>? Extensions { get; }
    List<IOntologyNode> Children { get; }
    List<Reference> References { get; }
}
