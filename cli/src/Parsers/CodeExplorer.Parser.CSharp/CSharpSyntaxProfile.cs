using System.Collections.Frozen;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.CSharp;

public static class CSharpSyntaxProfile
{
    static CSharpSyntaxProfile()
    {
        LanguageSyntaxProfileRegistry.Register("c-sharp", Instance);
    }

    public static readonly LanguageSyntaxProfile Instance = new()
    {
        ClassDeclarations = new[]
        {
            "class_declaration",
            "struct_declaration",
            "record_declaration",
            "record_struct_declaration"
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
            "variable_declarator",
            "property_declaration"
        }.ToFrozenSet(),

        MethodDeclarations = new[]
        {
            "method_declaration",
            "constructor_declaration",
            "local_function_statement"
        }.ToFrozenSet(),

        VariableDeclarations = new[]
        {
            "variable_declarator",
            "property_declaration",
            "variable_declaration",
            "field_declaration"
        }.ToFrozenSet(),

        Parameters = new[]
        {
            "parameter"
        }.ToFrozenSet(),

        Imports = new[]
        {
            "using_directive"
        }.ToFrozenSet(),

        Calls = new[]
        {
            "invocation_expression",
            "object_creation_expression"
        }.ToFrozenSet(),

        Inheritance = new[]
        {
            "base_list"
        }.ToFrozenSet(),

        StringLiterals = new[]
        {
            "string_literal",
            "verbatim_string_literal",
            "raw_string_literal",
            "character_literal"
        }.ToFrozenSet(),

        StringInterpolations = new[]
        {
            "interpolated_string_expression",
            "interpolated_verbatim_string_expression",
            "interpolated_raw_string_expression"
        }.ToFrozenSet(),

        BinaryExpressions = new[]
        {
            "binary_expression"
        }.ToFrozenSet(),

        MemberAccessExpressions = new[]
        {
            "member_access_expression"
        }.ToFrozenSet(),

        ElementAccessExpressions = new[]
        {
            "element_access_expression"
        }.ToFrozenSet(),

        ExcludedStringInterpolations = new[]
        {
            "interpolated_string_expression",
            "interpolated_verbatim_string_expression",
            "interpolated_raw_string_expression"
        }.ToFrozenSet()
    };
}
