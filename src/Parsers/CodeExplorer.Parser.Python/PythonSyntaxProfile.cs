using System.Collections.Frozen;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Python;

public static class PythonSyntaxProfile
{
    public static readonly LanguageSyntaxProfile Instance = new()
    {
        ClassDeclarations = new[]
        {
            "class_definition"
        }.ToFrozenSet(),

        InterfaceDeclarations = FrozenSet<string>.Empty,

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
        }.ToFrozenSet()
    };
}
