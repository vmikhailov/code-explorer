using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class KtorServerLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerKtorServer = "io.ktor.server";
    private const string ResourceRoutePrefix = "REST:";

    private static readonly Regex ResourceAnnotationRegex = new(
        @"@Resource\s*\(\s*[""'](?<path>/[^""']*)[""']\s*\)",
        RegexOptions.Compiled);

    public KtorServerLibraryParser()
    {
        Id = "ktor-server";
        Name = "Ktor Server";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["io.ktor.server.*", "io.ktor:ktor-server*", "io.ktor:ktor-serialization*", "io.ktor:ktor-bom*"];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerKtorServer, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in ResourceAnnotationRegex.Matches(content))
        {
            var path = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                fileNode.References.Add(new Reference(fileNode.Id, ResourceRoutePrefix + path, OntologyConstants.Relationships.Exposes));
            }
        }
    }
}
