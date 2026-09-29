using System.Collections.Frozen;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.TypeScript;

public static class TypeScriptSyntaxProfile
{
    static TypeScriptSyntaxProfile()
    {
        LanguageSyntaxProfileRegistry.Register("typescript", Instance);
        LanguageSyntaxProfileRegistry.Register("javascript", Instance);
    }

    public static readonly LanguageSyntaxProfile Instance = new()
    {
        ClassDeclarations = new[]
        {
            "class_declaration",
            "class_expression"
        }.ToFrozenSet(),

        InterfaceDeclarations = new[]
        {
            "interface_declaration",
            "type_alias_declaration"
        }.ToFrozenSet(),

        EnumDeclarations = new[]
        {
            "enum_declaration"
        }.ToFrozenSet(),

        ConstantDeclarations = new[]
        {
            "variable_declarator",
            "lexical_declaration",
            "public_field_definition",
            "property_definition"
        }.ToFrozenSet(),

        MethodDeclarations = new[]
        {
            "method_definition"
        }.ToFrozenSet(),

        FunctionDeclarations = new[]
        {
            "function_declaration",
            "function_expression",
            "arrow_function"
        }.ToFrozenSet(),

        VariableDeclarations = new[]
        {
            "public_field_definition",
            "property_definition",
            "variable_declarator",
            "lexical_declaration",
            "variable_declaration"
        }.ToFrozenSet(),

        Parameters = new[]
        {
            "required_parameter",
            "optional_parameter",
            "parameter_property"
        }.ToFrozenSet(),

        Imports = new[]
        {
            "import_statement",
            "export_statement"
        }.ToFrozenSet(),

        Calls = new[]
        {
            "call_expression",
            "new_expression"
        }.ToFrozenSet(),

        Inheritance = new[]
        {
            "extends_clause",
            "implements_clause"
        }.ToFrozenSet(),

        StringLiterals = new[]
        {
            "string"
        }.ToFrozenSet(),

        StringInterpolations = new[]
        {
            "template_string"
        }.ToFrozenSet(),

        BinaryExpressions = new[]
        {
            "binary_expression"
        }.ToFrozenSet(),

        MemberAccessExpressions = new[]
        {
            "member_expression"
        }.ToFrozenSet(),

        ElementAccessExpressions = new[]
        {
            "subscript_expression"
        }.ToFrozenSet()
    };
}
