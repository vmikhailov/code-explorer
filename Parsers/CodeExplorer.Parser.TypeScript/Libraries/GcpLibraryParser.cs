using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class GcpLibraryParser : ILibraryParser
{
    public string Type => "cloud";
    public string Name => "GCP";
    public string Id => "gcp";
    public IReadOnlyList<string> SupportedPatterns => ["@google-cloud", "firebase", "firebase-admin"];
    public string? CloudService => "GCP";

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
