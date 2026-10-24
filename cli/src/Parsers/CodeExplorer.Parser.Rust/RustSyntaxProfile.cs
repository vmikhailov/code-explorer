using System.Collections.Frozen;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Rust;

/// <summary>
/// Defines Tree-sitter AST syntax profile and mappings for Rust.
/// </summary>
public static class RustSyntaxProfile
{
    static RustSyntaxProfile()
    {
        LanguageSyntaxProfileRegistry.Register("rust", Instance);
    }

    public static readonly LanguageSyntaxProfile Instance = new()
    {
        StructDeclarations = new[]
        {
            TreeSitterSyntax.Rust.StructItem,
            TreeSitterSyntax.Rust.UnionItem
        }.ToFrozenSet(),

        ClassDeclarations = new[]
        {
            TreeSitterSyntax.Rust.StructItem,
            TreeSitterSyntax.Rust.UnionItem,
            TreeSitterSyntax.Rust.EnumItem
        }.ToFrozenSet(),

        InterfaceDeclarations = new[]
        {
            TreeSitterSyntax.Rust.TraitItem
        }.ToFrozenSet(),

        EnumDeclarations = new[]
        {
            TreeSitterSyntax.Rust.EnumItem
        }.ToFrozenSet(),

        DefaultEnumKind = "Enum",
        DefaultStructKind = "Struct",

        ConstantDeclarations = new[]
        {
            TreeSitterSyntax.Rust.ConstItem,
            TreeSitterSyntax.Rust.StaticItem
        }.ToFrozenSet(),

        MethodDeclarations = FrozenSet<string>.Empty,

        FunctionDeclarations = new[]
        {
            TreeSitterSyntax.Rust.FunctionItem,
            TreeSitterSyntax.Rust.FunctionSignatureItem
        }.ToFrozenSet(),

        VariableDeclarations = new[]
        {
            TreeSitterSyntax.Rust.LetDeclaration,
            TreeSitterSyntax.Rust.ConstItem,
            TreeSitterSyntax.Rust.StaticItem
        }.ToFrozenSet(),

        Parameters = new[]
        {
            TreeSitterSyntax.Rust.Parameter,
            TreeSitterSyntax.Rust.SelfParameter
        }.ToFrozenSet(),

        Imports = new[]
        {
            TreeSitterSyntax.Rust.UseDeclaration,
            TreeSitterSyntax.Rust.ExternCrateDeclaration
        }.ToFrozenSet(),

        Calls = new[]
        {
            TreeSitterSyntax.Rust.CallExpression,
            TreeSitterSyntax.Rust.MacroInvocation
        }.ToFrozenSet(),

        Inheritance = FrozenSet<string>.Empty,

        StringLiterals = new[]
        {
            TreeSitterSyntax.Rust.StringLiteral,
            TreeSitterSyntax.Rust.RawStringLiteral
        }.ToFrozenSet(),

        StringInterpolations = FrozenSet<string>.Empty,

        BinaryExpressions = new[]
        {
            TreeSitterSyntax.Rust.BinaryExpression
        }.ToFrozenSet(),

        MemberAccessExpressions = new[]
        {
            TreeSitterSyntax.Rust.FieldExpression
        }.ToFrozenSet(),

        ElementAccessExpressions = new[]
        {
            "index_expression"
        }.ToFrozenSet()
    };
}
