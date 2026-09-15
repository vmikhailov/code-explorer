using CodeExplorer.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class SyntacticSymbol
{
    public string Kind { get; }
    public string Name { get; }
    public Node Node { get; }
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

    public SyntacticSymbol(string kind, string name, Node node)
    {
        Kind = kind;
        Name = name;
        Node = node;
    }
}
