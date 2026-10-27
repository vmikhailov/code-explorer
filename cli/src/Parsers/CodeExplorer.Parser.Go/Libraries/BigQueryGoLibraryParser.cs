using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class BigQueryGoLibraryParser : ISemanticExtension
{
    public string Type => OntologyConstants.LibraryTypes.AnalyticsDb;
    public string Name => "BigQuery";
    public string Id => "bigquery";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "cloud.google.com/go/bigquery",
        "cloud.google.com/go/bigquery/*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> BigQueryMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "NewClient", "Query", "Table", "Dataset", "CreateQueryJob", "Read", "Inserter", "Put"
    };

    private static readonly Regex TableRegex = new(
        @"\b(?:FROM|JOIN|INTO|UPDATE|TABLE)\s+(?:IF\s+NOT\s+EXISTS\s+|IF\s+EXISTS\s+)?([a-zA-Z0-9_\.\[\]""#@'`\$\{\}\*]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsBigQueryCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsBigQueryCall(node, out var sqlText, out var method))
        {
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"BigQuery: {clean}";
            }
            return $"BigQuery: {method ?? "Query"}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        // 1. Detect calls (Query, Table, Inserter, NewClient, etc.)
        if (IsBigQueryCall(node, out var sqlText, out var method))
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

            // Table method call e.g. client.Dataset("...").Table("my_table")
            if (string.Equals(method, "Table", StringComparison.OrdinalIgnoreCase))
            {
                var args = GoAstHelper.GetCallArguments(node);
                if (args.Count > 0)
                {
                    var tableId = ResolveCandidate(args[0]);
                    if (!string.IsNullOrEmpty(tableId) && !WorkspaceConventions.IsPlaceholderName(tableId))
                    {
                        references.Add(new Reference(scopeSymbolId, tableId, OntologyConstants.Relationships.DependsOn));
                    }
                }
            }

            references.Add(new Reference(scopeSymbolId, "bigquery", OntologyConstants.Relationships.UsesDb));
        }

        // 2. Struct tags: detect struct fields tagged with `bigquery:"..."`
        if (node.Is(TreeSitterSyntax.Go.FieldDeclaration))
        {
            var tagNode = node.GetChildForField(TreeSitterSyntax.Fields.Tag);
            if (tagNode.IsValid() && tagNode.Text.Contains("bigquery:"))
            {
                references.Add(new Reference(scopeSymbolId, "bigquery", OntologyConstants.Relationships.UsesDb));
            }
        }
    }

    private static bool IsBigQueryCall(Node node, out string? sqlText, out string? methodName)
    {
        sqlText = null;
        methodName = null;

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcText = func.Text;

        if (funcText.StartWithIC("bigquery."))
        {
            methodName = funcText["bigquery.".Length..];
            return true;
        }

        foreach (var m in BigQueryMethods)
        {
            if (funcText.EndsWith("." + m, StringComparison.Ordinal) || funcText == m)
            {
                methodName = m;
                var args = GoAstHelper.GetCallArguments(node);
                for (var i = 0; i < Math.Min(args.Count, 3); i++)
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
