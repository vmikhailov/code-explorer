using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class ApolloKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string GraphQlPrefix = "graphql:";
    private const string ImportApollo = "apollographql";

    private static readonly Regex ApolloOperationRegex = new(
        @"\.\s*(?:query|mutation|subscription)\s*\(\s*(?<op>[a-zA-Z0-9_]+)\s*\(",
        RegexOptions.Compiled);

    public ApolloKotlinLibraryParser()
    {
        Id = "apollo-kotlin";
        Name = "Apollo GraphQL Kotlin";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["com.apollographql.apollo*"];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(ImportApollo, StringComparison.Ordinal)) return;

        foreach (Match match in ApolloOperationRegex.Matches(content))
        {
            var opName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(opName))
            {
                fileNode.References.Add(new Reference(fileNode.Id, GraphQlPrefix + opName, OntologyConstants.Relationships.Calls));
            }
        }
    }
}
