using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Python;

public class PythonSemanticAnalyzer : BaseSemanticAnalyzer
{
    public PythonSemanticAnalyzer() : base(new PythonParser().LibraryParsers)
    {
    }

    public PythonSemanticAnalyzer(IEnumerable<ILibraryParser> libraryParsers) : base(libraryParsers)
    {
    }
}
