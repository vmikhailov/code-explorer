using System.Collections.Frozen;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Python;

public static class PythonSyntaxProfile
{
    static PythonSyntaxProfile()
    {
        LanguageSyntaxProfileRegistry.Register("python", Instance);
    }

    public static readonly LanguageSyntaxProfile Instance = new()
    {
        ClassDeclarations = new[]
        {
            "class_definition"
        }.ToFrozenSet(),

        InterfaceDeclarations = FrozenSet<string>.Empty,

        EnumDeclarations = FrozenSet<string>.Empty,

        ConstantDeclarations = new[]
        {
            "assignment"
        }.ToFrozenSet(),

        MethodDeclarations = FrozenSet<string>.Empty,

        FunctionDeclarations = new[]
        {
            "function_definition"
        }.ToFrozenSet(),

        VariableDeclarations = new[]
        {
            "assignment",
            "parameters",
            "pattern"
        }.ToFrozenSet(),

        Parameters = FrozenSet<string>.Empty,

        Imports = new[]
        {
            "import_statement",
            "import_from_statement"
        }.ToFrozenSet(),

        Calls = new[]
        {
            "call"
        }.ToFrozenSet(),

        Inheritance = FrozenSet<string>.Empty,

        StringLiterals = new[]
        {
            "string"
        }.ToFrozenSet(),

        StringInterpolations = new[]
        {
            "format_string"
        }.ToFrozenSet(),

        BinaryExpressions = new[]
        {
            "binary_operator"
        }.ToFrozenSet(),

        MemberAccessExpressions = new[]
        {
            "attribute"
        }.ToFrozenSet(),

        ElementAccessExpressions = new[]
        {
            "subscript"
        }.ToFrozenSet()
    };
}
