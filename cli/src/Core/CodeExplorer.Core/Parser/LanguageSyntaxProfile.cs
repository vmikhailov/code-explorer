using System.Collections.Frozen;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Defines the AST node type classifications for a specific programming language.
/// This enables BaseParserVisitor to remain completely decoupled from language-specific grammars (Open-Closed Principle).
/// </summary>
public class LanguageSyntaxProfile
{
    public static readonly LanguageSyntaxProfile Empty = new();

    public FrozenSet<string> ClassDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> InterfaceDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> MethodDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> FunctionDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> VariableDeclarations { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Parameters { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Imports { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Calls { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> Inheritance { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> StringLiterals { get; init; } = FrozenSet<string>.Empty;
    public FrozenSet<string> ExcludedStringInterpolations { get; init; } = FrozenSet<string>.Empty;
}
