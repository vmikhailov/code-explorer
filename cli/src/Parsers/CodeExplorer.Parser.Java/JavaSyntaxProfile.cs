using System.Collections.Frozen;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Java;

public static class JavaSyntaxProfile
{
    static JavaSyntaxProfile()
    {
        LanguageSyntaxProfileRegistry.Register("java", Instance);
    }

    public static readonly LanguageSyntaxProfile Instance = new()
    {
        ClassDeclarations = new[]
        {
            "class_declaration",
            "record_declaration",
            "annotation_type_declaration"
        }.ToFrozenSet(),

        InterfaceDeclarations = new[]
        {
            "interface_declaration"
        }.ToFrozenSet(),

        EnumDeclarations = new[]
        {
            "enum_declaration"
        }.ToFrozenSet(),

        ConstantDeclarations = new[]
        {
            "field_declaration",
            "constant_declaration"
        }.ToFrozenSet(),

        MethodDeclarations = new[]
        {
            "method_declaration",
            "constructor_declaration",
            "compact_constructor_declaration"
        }.ToFrozenSet(),

        VariableDeclarations = new[]
        {
            "field_declaration",
            "constant_declaration",
            "variable_declarator",
            "local_variable_declaration"
        }.ToFrozenSet(),

        Parameters = new[]
        {
            "formal_parameter",
            "spread_parameter",
            "receiver_parameter"
        }.ToFrozenSet(),

        Imports = new[]
        {
            "import_declaration",
            "package_declaration"
        }.ToFrozenSet(),

        Calls = new[]
        {
            "method_invocation",
            "object_creation_expression",
            "explicit_constructor_invocation",
            "super_constructor_invocation"
        }.ToFrozenSet(),

        Inheritance = new[]
        {
            "superclass",
            "super_interfaces",
            "extends_interfaces"
        }.ToFrozenSet(),

        StringLiterals = new[]
        {
            "string_literal",
            "text_block"
        }.ToFrozenSet(),

        StringInterpolations = FrozenSet<string>.Empty,

        BinaryExpressions = new[]
        {
            "binary_expression"
        }.ToFrozenSet(),

        MemberAccessExpressions = new[]
        {
            "field_access"
        }.ToFrozenSet(),

        ElementAccessExpressions = new[]
        {
            "array_access"
        }.ToFrozenSet()
    };
}
