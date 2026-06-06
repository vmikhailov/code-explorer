using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Go;

public class GoSyntaxEnricher : BaseSyntaxEnricher
{


    public GoSyntaxEnricher(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree) 
        : base(libraryParsers, syntaxTree)
    {
    }
}
