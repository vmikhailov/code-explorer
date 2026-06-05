using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Go;

public class GoSemanticModel : BaseSemanticModel
{
    public GoSemanticModel(SyntaxTree syntaxTree) 
        : base(new GoParser().LibraryParsers, syntaxTree)
    {
    }

    public GoSemanticModel(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree) 
        : base(libraryParsers, syntaxTree)
    {
    }
}
