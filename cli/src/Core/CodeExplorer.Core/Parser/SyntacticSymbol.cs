using CodeExplorer.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class SyntacticSymbol(string kind, string name, Node node)
{
    public string Kind { get; } = kind;
    public string Name { get; } = name;
    public Node Node { get; } = node;
    public List<SyntacticSymbol> Children { get; } = [];
    public List<Reference> References { get; } = [];
    public string? Text { get; set; }
    public string? Protocol { get; set; }
    public bool IsAnonymous { get; set; }
    public string? RequiredRoles { get; set; }
    public string? Policies { get; set; }
    public string? RequestType { get; set; }
    public string? ResponseType { get; set; }
    public string? OperationType { get; set; }
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);
}
