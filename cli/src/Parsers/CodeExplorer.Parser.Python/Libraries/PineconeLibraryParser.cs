using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class PineconeLibraryParser : ILibraryParser
{
    public string Type => "db:vector";
    public string Name => "Pinecone";
    public string Id => "pinecone";
    public IReadOnlyList<string> SupportedPatterns => ["pinecone-client"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
