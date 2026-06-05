using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptSemanticAnalyzer : BaseSemanticAnalyzer
{
    public TypeScriptSemanticAnalyzer() : base(new TypeScriptParser().LibraryParsers)
    {
    }

    public TypeScriptSemanticAnalyzer(IEnumerable<ILibraryParser> libraryParsers) : base(libraryParsers)
    {
    }
}
