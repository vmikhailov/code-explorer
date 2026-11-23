using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class KtorClientLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerKtorClient = "io.ktor.client";
    private const string MarkerKtorfit = "de.jensklingenberg.ktorfit";
    private const string HttpPrefix = "http:";

    private static readonly Regex KtorfitEndpointRegex = new(
        @"@(?:GET|POST|PUT|DELETE|PATCH|HEAD)\s*\(\s*[""'](?<url>[^""']+)[""']\s*\)",
        RegexOptions.Compiled);

    public KtorClientLibraryParser()
    {
        Id = "ktor-client";
        Name = "Ktor Client & Ktorfit";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["io.ktor.client.*", "io.ktor:ktor-client*", "de.jensklingenberg.ktorfit*"];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerKtorClient, StringComparison.Ordinal) &&
            !content.Contains(MarkerKtorfit, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in KtorfitEndpointRegex.Matches(content))
        {
            var url = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                fileNode.References.Add(new Reference(fileNode.Id, HttpPrefix + url, OntologyConstants.Relationships.Calls));
            }
        }
    }
}
