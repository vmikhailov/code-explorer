using System.Diagnostics.CodeAnalysis;
using CodeExplorer.Core.Common.Nodes;
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
    public SymbolFlags Flags { get; set; }
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
    public int? StageOrder { get; set; }
    public TypeKind? TypeKind { get; set; }
    public MemberKind? MemberKind { get; set; }
    public string? DataType { get; set; }
    public string? GraphqlField { get; set; }
    public string? GraphqlSchema { get; set; }
    public string? MessagingDirection { get; set; }

    public bool IsDecorator
    {
        get => Flags.HasFlag(SymbolFlags.IsDecorator);
        set => Flags = value ? Flags | SymbolFlags.IsDecorator : Flags & ~SymbolFlags.IsDecorator;
    }

    public bool IsEmptyCatch
    {
        get => Flags.HasFlag(SymbolFlags.IsEmpty);
        set => Flags = value ? Flags | SymbolFlags.IsEmpty : Flags & ~SymbolFlags.IsEmpty;
    }

    public bool HasCatchComment
    {
        get => Flags.HasFlag(SymbolFlags.HasComment);
        set => Flags = value ? Flags | SymbolFlags.HasComment : Flags & ~SymbolFlags.HasComment;
    }

    private Dictionary<string, string>? _properties;
    public bool HasProperties => _properties is { Count: > 0 } ||
                                 !string.IsNullOrEmpty(GraphqlField) ||
                                 !string.IsNullOrEmpty(GraphqlSchema) ||
                                 !string.IsNullOrEmpty(MessagingDirection);
    public Dictionary<string, string> Properties => _properties ??= new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetProperty(string key, [NotNullWhen(true)] out string? value)
    {
        switch (key)
        {
            case "graphql_field" when !string.IsNullOrEmpty(GraphqlField):
                value = GraphqlField;
                return true;
            case "graphql_schema" when !string.IsNullOrEmpty(GraphqlSchema):
                value = GraphqlSchema;
                return true;
            case "messaging_direction" when !string.IsNullOrEmpty(MessagingDirection):
                value = MessagingDirection;
                return true;
            case "data_type" when !string.IsNullOrEmpty(DataType):
                value = DataType;
                return true;
            case "type_kind" when TypeKind.HasValue:
                value = TaxonomyViewEnumExtensions.FormatEnumValue(TypeKind.Value);
                return true;
            case "member_kind" when MemberKind.HasValue:
                value = TaxonomyViewEnumExtensions.FormatEnumValue(MemberKind.Value);
                return true;
            case "stage_order" when StageOrder.HasValue:
                value = StageOrder.Value.ToString();
                return true;
            case "is_test" when Flags.HasFlag(SymbolFlags.IsTest):
                value = "true";
                return true;
        }
        if (_properties != null && _properties.TryGetValue(key, out value))
            return true;
        value = null;
        return false;
    }

    public string? GetPropertyOrDefault(string key) =>
        TryGetProperty(key, out var val) ? val : null;
}
