using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class InfluxDbLibraryParser : ILibraryParser
{
    public string Type => "db:timeseries";
    public string Name => "InfluxDB";
    public string Id => "influxdb";
    public IReadOnlyList<string> SupportedPatterns => ["@influxdata/influxdb-client", "influxdb"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> InfluxMethods = new(StringComparer.Ordinal)
    {
        "writePoint", "writePoints", "writeRecord", "writeRecords",
        "queryRows", "queryRaw", "collectRows", "iterateRows"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsInfluxCall(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsInfluxCall(node))
        {
            if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
            {
                var target = objNode.IsValid() ? objNode.Text : "influx";
                return $"InfluxDB: {target}.{propName}";
            }

            return "InfluxDB Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // Timeseries queries represent Query nodes
    }

    private static bool IsInfluxCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        if (AstHelper.TryGetMemberAccess(node, out _, out var propName))
        {
            return propName != null && InfluxMethods.Contains(propName);
        }

        return false;
    }
}
