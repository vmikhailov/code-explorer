using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class TypeOrmLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "TypeORM";
    public string Id => "typeorm";
    public IReadOnlyList<string> SupportedPatterns => ["typeorm"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
