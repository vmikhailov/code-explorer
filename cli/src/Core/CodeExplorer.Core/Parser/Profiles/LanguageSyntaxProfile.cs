using System.Collections.Frozen;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Defines the AST node type classifications and declarative extraction selectors for a specific programming language.
/// This enables BaseParserVisitor to remain decoupled from language-specific grammars (Open-Closed Principle).
/// </summary>
public class LanguageSyntaxProfile
{
    public static readonly LanguageSyntaxProfile Empty = new();

    private static readonly FrozenSet<string> DefaultIdentifierChildTypeSet = new[]
    {
        "identifier",
        "type_identifier",
        "variable_name",
        "name",
        "property_identifier",
        "field_identifier",
        "simple_identifier"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> DefaultObjectCreationNodeTypeSet = new[]
    {
        "object_creation_expression",
        "new_expression"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> DefaultConstructorSelfCallNodeTypeSet = new[]
    {
        "explicit_constructor_invocation",
        "super_constructor_invocation"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> DefaultConstantModifierSet = new[]
    {
        "final",
        "const",
        "readonly"
    }.ToFrozenSet();

    // Declarations
    public FrozenSet<string> ClassDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> InterfaceDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> StructDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> MethodDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> FunctionDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> VariableDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ConstantDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> EnumDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Parameters { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Imports { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Inheritance { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> CatchClauses { get; init; } = FrozenSet<string>.Empty;

    // Expressions
    public FrozenSet<string> Calls { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> StringLiterals { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> StringInterpolations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> BinaryExpressions { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> MemberAccessExpressions { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ElementAccessExpressions { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ExcludedStringInterpolations { get; init; } = FrozenSet<string>.Empty;

    // Declarative Layer 3 Extraction Descriptors
    public string DefaultEnumKind { get; init; } = "Class";
    public string DefaultStructKind { get; init; } = "Struct";

    public IReadOnlyList<string> IdentifierFieldNames { get; init; } = ["name"];
    public FrozenSet<string> IdentifierChildTypes { get; init; } = DefaultIdentifierChildTypeSet;
    public char[] IdentifierTrimChars { get; init; } = [];

    public FrozenSet<string> AnnotationNodeTypes { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> AnnotationContainerNodeTypes { get; init; } = FrozenSet<string>.Empty;

    public FrozenSet<string> SuperclassClauseNodeTypes { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> InterfaceClauseNodeTypes { get; init; } = FrozenSet<string>.Empty;

    public IReadOnlyList<string> CallMethodFields { get; init; } = ["name", "field", "member", "property"];
    public IReadOnlyList<string> CallReceiverFields { get; init; } = ["object", "operand", "scope", "receiver"];
    public string CallMemberSeparator { get; init; } = ".";
    public bool IncludeReceiverInCallName { get; init; } = true;
    public FrozenSet<string> ObjectCreationNodeTypes { get; init; } = DefaultObjectCreationNodeTypeSet;
    public FrozenSet<string> ConstructorSelfCallNodeTypes { get; init; } = DefaultConstructorSelfCallNodeTypeSet;

    public FrozenSet<string> PackageDeclarationNodeTypes { get; init; } = FrozenSet<string>.Empty;
    public IReadOnlyList<string> ImportPathFields { get; init; } = ["path", "name", "source"];
    public FrozenSet<string> ImportIdentifierChildTypes { get; init; } = FrozenSet<string>.Empty;

    public FrozenSet<string> VariableDeclaratorNodeTypes { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ConstantModifierKeywords { get; init; } = DefaultConstantModifierSet;

    public bool IsStringLiteral(string nodeType) => StringLiterals.Contains(nodeType);
    public bool IsInterpolatedString(string nodeType) => StringInterpolations.Contains(nodeType);
    public bool IsBinary(string nodeType) => BinaryExpressions.Contains(nodeType);
    public bool IsMemberAccess(string nodeType) => MemberAccessExpressions.Contains(nodeType);
    public bool IsElementAccess(string nodeType) => ElementAccessExpressions.Contains(nodeType);
    public bool IsCall(string nodeType) => Calls.Contains(nodeType);
    public bool IsCatchClause(string nodeType) => CatchClauses.Contains(nodeType);
}

