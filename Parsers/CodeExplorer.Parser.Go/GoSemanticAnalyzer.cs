using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Go;

public class GoSemanticAnalyzer : BaseSemanticAnalyzer
{
    public GoSemanticAnalyzer() : base(new GoParser().LibraryParsers)
    {
    }

    public GoSemanticAnalyzer(IReadOnlyList<ILibraryParser> libraryParsers) : base(libraryParsers)
    {
    }
}
