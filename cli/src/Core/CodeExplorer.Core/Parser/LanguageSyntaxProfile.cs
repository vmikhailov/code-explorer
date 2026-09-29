using System.Collections.Frozen;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Defines the AST node type classifications for a specific programming language.
/// This enables BaseParserVisitor to remain completely decoupled from language-specific grammars (Open-Closed Principle).
/// </summary>
public class LanguageSyntaxProfile
{
    public static readonly LanguageSyntaxProfile Empty = new();

    // Declarations
    public FrozenSet<string> ClassDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> InterfaceDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> MethodDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> FunctionDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> VariableDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ConstantDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> EnumDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Parameters { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Imports { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Inheritance { get; init; } = FrozenSet<string>.Empty;

    // Expressions
    public FrozenSet<string> Calls { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> StringLiterals { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> StringInterpolations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> BinaryExpressions { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> MemberAccessExpressions { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ElementAccessExpressions { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ExcludedStringInterpolations { get; init; } = FrozenSet<string>.Empty;

    public bool IsStringLiteral(string nodeType) => StringLiterals.Contains(nodeType);
    public bool IsInterpolatedString(string nodeType) => StringInterpolations.Contains(nodeType);
    public bool IsBinary(string nodeType) => BinaryExpressions.Contains(nodeType);
    public bool IsMemberAccess(string nodeType) => MemberAccessExpressions.Contains(nodeType);
    public bool IsElementAccess(string nodeType) => ElementAccessExpressions.Contains(nodeType);
    public bool IsCall(string nodeType) => Calls.Contains(nodeType);
}
