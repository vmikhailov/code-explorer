using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class EfCoreLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "Microsoft.EntityFrameworkCore";
    public string Id => "microsoft.entityframeworkcore";
    public IReadOnlyList<string> SupportedPatterns => ["Microsoft.EntityFrameworkCore"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
