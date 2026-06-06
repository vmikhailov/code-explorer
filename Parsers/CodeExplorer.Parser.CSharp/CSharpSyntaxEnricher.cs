using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.CSharp;

public class CSharpSyntaxEnricher : BaseSyntaxEnricher
{


    public CSharpSyntaxEnricher(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree) 
        : base(libraryParsers, syntaxTree)
    {
    }
}
