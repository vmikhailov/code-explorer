using System.Collections.Generic;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Go;

public class GoSemanticAnalyzer : BaseSemanticAnalyzer
{
    public GoSemanticAnalyzer() : base(new GoParser().LibraryParsers)
    {
    }

    public GoSemanticAnalyzer(IEnumerable<ILibraryParser> libraryParsers) : base(libraryParsers)
    {
    }
}
