using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class GraphQlKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerExpedia = "com.expediagroup.graphql";
    private const string MarkerDgs = "com.netflix.graphql.dgs";
    private const string GraphQlPrefix = "GRAPHQL:";

    private static readonly Regex DgsResolverRegex = new(
        @"@(?<kind>DgsQuery|DgsMutation|DgsSubscription|DgsData|QueryMapping|MutationMapping|SubscriptionMapping)\b(?:\s*\(\s*(?:(?:field|value)\s*=\s*)?[""'](?<field>[a-zA-Z0-9_]+)[""']\s*\))?[\s\S]{0,120}?\bfun\s+(?<fn>[a-zA-Z0-9_]+)",
        RegexOptions.Compiled);

    private static readonly Regex ExpediaOperationClassRegex = new(
        @"\bclass\s+(?<cls>[A-Z][a-zA-Z0-9_]*)\s*(?:\([^)]*\))?\s*:\s*(?:Query|Mutation|Subscription)\b",
        RegexOptions.Compiled);

    public GraphQlKotlinLibraryParser()
    {
        Id = "graphql-kotlin";
        Name = "graphql-kotlin & Netflix DGS";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "com.expediagroup.graphql.*",
            "com.expediagroup:graphql-kotlin*",
            "com.netflix.graphql.dgs.*",
            "com.graphql-java*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerExpedia, StringComparison.Ordinal) &&
            !content.Contains(MarkerDgs, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in DgsResolverRegex.Matches(content))
        {
            var field = match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value)
                ? match.Groups[2].Value.Trim()
                : match.Groups[3].Value.Trim();
            if (!string.IsNullOrEmpty(field))
            {
                fileNode.References.Add(new Reference(fileNode.Id, GraphQlPrefix + field, OntologyConstants.Relationships.Exposes));
            }
        }

        foreach (Match match in ExpediaOperationClassRegex.Matches(content))
        {
            var cls = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(cls))
            {
                fileNode.References.Add(new Reference(fileNode.Id, GraphQlPrefix + cls, OntologyConstants.Relationships.Exposes));
            }
        }
    }
}
