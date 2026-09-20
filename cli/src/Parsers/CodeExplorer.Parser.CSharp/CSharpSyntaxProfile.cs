using System.Collections.Frozen;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.CSharp;

public static class CSharpSyntaxProfile
{
    public static readonly LanguageSyntaxProfile Instance = new()
    {
        ClassDeclarations = new[]
        {
            "class_declaration",
            "enum_declaration",
            "struct_declaration",
            "record_declaration"
        }.ToFrozenSet(),

        InterfaceDeclarations = new[]
        {
            "interface_declaration"
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
            "invocation_expression"
        }.ToFrozenSet(),

        Inheritance = new[]
        {
            "base_list"
        }.ToFrozenSet(),

        StringLiterals = new[]
        {
            "string_literal",
            "verbatim_string_literal"
        }.ToFrozenSet(),

        ExcludedStringInterpolations = new[]
        {
            "interpolated_string_expression",
            "interpolated_verbatim_string_expression",
            "interpolated_raw_string_expression"
        }.ToFrozenSet()
    };
}
