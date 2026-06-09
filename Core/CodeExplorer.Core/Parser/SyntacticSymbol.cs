using CodeExplorer.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

public class SyntacticSymbol
{
    public string Kind { get; }
    public string Name { get; }
    public Node Node { get; }
    public List<SyntacticSymbol> Children { get; } = new();
    public List<Reference> References { get; } = new();
    public string? Text { get; set; }

    public SyntacticSymbol(string kind, string name, Node node)
    {
        Kind = kind;
        Name = name;
        Node = node;
    }
}
