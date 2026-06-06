using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptSyntaxEnricher : BaseSyntaxEnricher
{
    public TypeScriptSyntaxEnricher(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree)
        : base(libraryParsers, syntaxTree)
    {
    }
}

