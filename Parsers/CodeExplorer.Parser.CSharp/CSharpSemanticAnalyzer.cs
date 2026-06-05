using System.Collections.Generic;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.CSharp;

public class CSharpSemanticAnalyzer : BaseSemanticAnalyzer
{
    public CSharpSemanticAnalyzer() : base(new CSharpParser().LibraryParsers)
    {
    }

    public CSharpSemanticAnalyzer(IEnumerable<ILibraryParser> libraryParsers) : base(libraryParsers)
    {
    }
}
