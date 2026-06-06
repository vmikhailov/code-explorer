using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Python;

public class PythonSyntaxEnricher : BaseSyntaxEnricher
{


    public PythonSyntaxEnricher(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree) 
        : base(libraryParsers, syntaxTree)
    {
    }
}
