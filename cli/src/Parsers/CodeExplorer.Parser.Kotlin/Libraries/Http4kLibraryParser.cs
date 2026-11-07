using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class Http4kLibraryParser : DeclarativeSemanticExtension
{
    private const string ImportHttp4k = "org.http4k";

    private static readonly Regex RouteBindRegex = new(
        @"[""'](?<path>/[^""']*)[""']\s+bind\s+(?:Method\.)?(?<verb>GET|POST|PUT|DELETE|PATCH|OPTIONS|HEAD)",
        RegexOptions.Compiled);

    public Http4kLibraryParser()
    {
        Id = "http4k";
        Name = "http4k";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["org.http4k.*"];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(ImportHttp4k, StringComparison.Ordinal)) return;

        foreach (Match match in RouteBindRegex.Matches(content))
        {
            var path = match.Groups[1].Value.Trim();
            var verb = match.Groups[2].Value.Trim().ToUpperInvariant();
            fileNode.References.Add(new Reference(fileNode.Id, $"{verb}:{path}", OntologyConstants.Relationships.Exposes));
        }
    }
}
