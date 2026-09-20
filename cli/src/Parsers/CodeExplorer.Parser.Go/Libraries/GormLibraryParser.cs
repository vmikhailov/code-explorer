using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GormLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "GORM";
    public string Id => "gorm";
    public IReadOnlyList<string> SupportedPatterns => ["gorm.io/gorm"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
