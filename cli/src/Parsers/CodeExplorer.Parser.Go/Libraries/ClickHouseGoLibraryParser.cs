using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class ClickHouseGoLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> ClickHouseMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Open", "OpenDB", "ParseDSN",
        "Exec", "ExecContext", "Query", "QueryRow", "QueryRowContext",
        "PrepareBatch", "Select", "AsyncInsert", "Do"
    };

    private static readonly Regex TableRegex = new(
        @"\b(?:FROM|JOIN|INTO|UPDATE|TABLE)\s+(?:IF\s+NOT\s+EXISTS\s+|IF\s+EXISTS\s+)?([a-zA-Z0-9_\.\[\]""#@'`\$\{\}\*]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ClickHouseGoLibraryParser()
    {
        Type = SemanticExtensionType.AnalyticsDb;
        Name = "ClickHouse";
        Id = "clickhouse";
        SupportedPatterns =
        [
            "github.com/ClickHouse/clickhouse-go",
            "github.com/ClickHouse/clickhouse-go/v2",
            "github.com/ClickHouse/clickhouse-go/*"
        ];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsClickHouseCall(node, out _, out _) ? OntologyConstants.NodeLabels.Query : null,
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (!IsClickHouseCall(node, out var sqlText, out var method)) return null;
                    if (!string.IsNullOrEmpty(sqlText))
                    {
                        var clean = NestedSqlParser.CleanQueryText(sqlText);
                        if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                        {
                            return $"{firstWord} Query: {clean}";
                        }
                        return $"ClickHouse: {clean}";
                    }
                    return $"ClickHouse: {method ?? "Query"}";
                },
                CollectReferences = CollectClickHouseReferences
            }
        ];
    }

    private static void CollectClickHouseReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsClickHouseCall(node, out var sqlText, out _))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);

                var match = TableRegex.Match(sqlText);
                if (match.Success)
                {
                    var rawTable = match.Groups[1].Value;
                    var parts = rawTable.Split('.').Select(NestedSqlParser.CleanSqlIdentifier).Where(p => !string.IsNullOrEmpty(p)).ToArray();
                    var tbl = parts.Length > 0 ? parts[^1] : null;
                    if (!string.IsNullOrEmpty(tbl) && !NestedSqlParser.IsSqlKeyword(tbl))
                    {
                        references.Add(new Reference(scopeSymbolId, tbl, OntologyConstants.Relationships.DependsOn));
                    }
                }
            }

            references.Add(new Reference(scopeSymbolId, ClickHouseDbId, OntologyConstants.Relationships.UsesDb));
        }
    }

    private const string ClickHouseDbId = "clickhouse";

    private static bool IsClickHouseCall(Node node, out string? sqlText, out string? methodName)
    {
        sqlText = null;
        methodName = null;

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcText = func.Text;

        if (funcText.StartWithIC("clickhouse."))
        {
            methodName = funcText["clickhouse.".Length..];
            return true;
        }

        foreach (var m in ClickHouseMethods)
        {
            if (funcText.EndsWith("." + m, StringComparison.Ordinal) || funcText == m)
            {
                methodName = m;
                var args = GoAstHelper.GetCallArguments(node);
                for (var i = 0; i < Math.Min(args.Count, 4); i++)
                {
                    var candidate = ResolveCandidate(args[i]);
                    if (!string.IsNullOrEmpty(candidate))
                    {
                        if (NestedSqlParser.TryParseSql(candidate, out _, out _) || TableRegex.IsMatch(candidate))
                        {
                            sqlText = candidate;
                            return true;
                        }

                        if (sqlText == null &&
                            !candidate.Equals("ctx", StringComparison.OrdinalIgnoreCase) &&
                            !candidate.StartWithIC("context."))
                        {
                            sqlText = candidate;
                        }
                    }
                }
                return true;
            }
        }

        return false;
    }

    private static string? ResolveCandidate(Node node)
    {
        if (!node.IsValid()) return null;
        if (node.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.RawStringLiteral, TreeSitterSyntax.Go.StringLiteral))
        {
            return node.Text.Trim('"', '`');
        }
        return GoAstHelper.ResolveStringOrVariable(node);
    }
}
