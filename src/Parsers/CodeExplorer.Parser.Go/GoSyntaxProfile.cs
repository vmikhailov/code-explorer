using System.Collections.Frozen;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Go;

public static class GoSyntaxProfile
{
    public static readonly LanguageSyntaxProfile Instance = new()
    {
        ClassDeclarations = new[]
        {
            "type_spec",
            "type_declaration",
            "struct_type"
        }.ToFrozenSet(),

        InterfaceDeclarations = new[]
        {
            "interface_type"
        }.ToFrozenSet(),

        MethodDeclarations = new[]
        {
            "method_declaration"
        }.ToFrozenSet(),

        FunctionDeclarations = new[]
        {
            "function_declaration"
        }.ToFrozenSet(),

        VariableDeclarations = new[]
        {
            "const_spec",
            "var_spec",
            "short_var_declaration",
            "field_declaration"
        }.ToFrozenSet(),

        Parameters = new[]
        {
            "parameter_declaration"
        }.ToFrozenSet(),

        Imports = new[]
        {
            "import_spec",
            "import_declaration"
        }.ToFrozenSet(),

        Calls = new[]
        {
            "call_expression"
        }.ToFrozenSet(),

        Inheritance = FrozenSet<string>.Empty,

        StringLiterals = new[]
        {
            "interpreted_string_literal",
            "raw_string_literal",
            "string_literal"
        }.ToFrozenSet()
    };
}
