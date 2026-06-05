using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.CSharp;

public class CSharpSemanticAnalyzer : BaseSemanticAnalyzer
{
    public CSharpSemanticAnalyzer() : base(new CSharpParser().LibraryParsers)
    {
    }

    public CSharpSemanticAnalyzer(IReadOnlyList<ILibraryParser> libraryParsers) : base(libraryParsers)
    {
    }
}
