using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Python;

public class PythonSemanticModel : BaseSemanticModel
{
    public PythonSemanticModel(SyntaxTree syntaxTree) 
        : base(new PythonParser().LibraryParsers, syntaxTree)
    {
    }

    public PythonSemanticModel(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree) 
        : base(libraryParsers, syntaxTree)
    {
    }
}
