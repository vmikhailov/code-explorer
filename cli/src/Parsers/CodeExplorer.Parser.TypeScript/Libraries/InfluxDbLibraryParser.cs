using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class InfluxDbLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> InfluxMethods = new(StringComparer.Ordinal)
    {
        "writePoint", "writePoints", "writeRecord", "writeRecords",
        "queryRows", "queryRaw", "collectRows", "iterateRows"
    };

    public InfluxDbLibraryParser()
    {
        Type = SemanticExtensionType.TimeSeriesDb;
        Name = "InfluxDB";
        Id = "influxdb";
        SupportedPatterns = ["@influxdata/influxdb-client", "influxdb"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsInfluxCall(node) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsInfluxCall(node)) return null;
                    if (AstHelper.TryGetMemberAccess(node, out var objNode, out var propName))
                    {
                        var target = objNode.IsValid() ? objNode.Text : "influx";
                        return $"InfluxDB: {target}.{propName}";
                    }
                    return "InfluxDB Query";
                }
            }
        ];
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
