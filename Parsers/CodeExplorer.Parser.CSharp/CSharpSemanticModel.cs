using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.CSharp;

public class CSharpSemanticModel : BaseSemanticModel
{
    public CSharpSemanticModel(SyntaxTree syntaxTree) 
        : base(new CSharpParser().LibraryParsers, syntaxTree)
    {
    }

    public CSharpSemanticModel(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree) 
        : base(libraryParsers, syntaxTree)
    {
    }
}
