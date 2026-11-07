using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class SqlDelightLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerSqlDelight = "sqldelight";
    private const string MarkerQueriesDot = "Queries.";

    private static readonly Regex QueriesAccessRegex = new(
        @"(?<tableQueries>[a-zA-Z0-9_]+)Queries\s*\.\s*(?<op>[a-zA-Z0-9_]+)",
        RegexOptions.Compiled);

    public SqlDelightLibraryParser()
    {
        Id = "sqldelight";
        Name = "SQLDelight";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["app.cash.sqldelight*", "com.squareup.sqldelight*"];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerSqlDelight, StringComparison.OrdinalIgnoreCase) &&
            !content.Contains(MarkerQueriesDot, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in QueriesAccessRegex.Matches(content))
        {
            var tablePrefix = match.Groups[1].Value.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(tablePrefix))
            {
                fileNode.References.Add(new Reference(fileNode.Id, tablePrefix, OntologyConstants.Relationships.UsesDb));
            }
        }
    }
}
