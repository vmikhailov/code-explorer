using System.Diagnostics.CodeAnalysis;
using CodeExplorer.Core.Parser.Syntax;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

public class SyntacticSymbol(SyntacticSymbolKind kind, string name, SyntaxNode node)
{
    public SyntacticSymbol(string kind, string name, SyntaxNode node)
        : this(TaxonomyEnumExtendedExtensions.ParseSyntacticSymbolKind(kind), name, node)
    {
    }

    public SyntacticSymbolKind Kind { get; } = kind;
    public string Name { get; } = name;
    public SyntaxNode Node { get; } = node;
    public List<SyntacticSymbol> Children { get; } = [];
    public List<Reference> References { get; } = [];
    private string? _text;
    public string? Text
    {
        get => _text ?? (Node.Span.EndByte - Node.Span.StartByte <= 65536 ? Node.Text : string.Empty);
        set => _text = value;
    }
    public string? Protocol { get; set; }
    public bool IsAnonymous { get; set; }
    public string? RequiredRoles { get; set; }
    public string? Policies { get; set; }
    public string? RequestType { get; set; }
    public string? ResponseType { get; set; }
    public OperationType? OperationType { get; set; }
    public string? ParentScopeId { get; set; }

    public string? Callee { get; set; }
    public string? Role { get; set; }
    public int ArgIndex { get; set; }
    public bool IsDecorator { get; set; }
    public bool IsEmptyCatch { get; set; }
    public bool HasCatchComment { get; set; }

    private Dictionary<string, string>? _properties;
    public bool HasProperties => _properties is { Count: > 0 };
    public Dictionary<string, string> Properties => _properties ??= new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetProperty(string key, [NotNullWhen(true)] out string? value)
    {
        if (_properties != null && _properties.TryGetValue(key, out value))
            return true;
        value = null;
        return false;
    }

    public string? GetPropertyOrDefault(string key) =>
        _properties != null && _properties.TryGetValue(key, out var val) ? val : null;
}
