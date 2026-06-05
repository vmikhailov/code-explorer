using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class InfluxDbLibraryParser : ILibraryParser
{
    public string Name => "InfluxDbLibraryParser";
    public string LibraryType => "db:timeseries";
    public string LibraryName => "InfluxDB";
    public string LibraryId => "influxdb";
    public IEnumerable<string> SupportedLibraries => ["influxdb"];

    public string? MapNodeType(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public string? ExtractIdentifier(Node node, ParsingContext ctx) => throw new NotImplementedException();
    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx) => throw new NotImplementedException();
}
