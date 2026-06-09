using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class InfluxDbLibraryParser : ILibraryParser
{
    public string Type => "db:timeseries";
    public string Name => "InfluxDB";
    public string Id => "influxdb";
    public IReadOnlyList<string> SupportedPatterns => ["influxdb"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
