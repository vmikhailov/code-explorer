using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class KoinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerKoin = "org.koin";

    private static readonly Regex KoinBindRegex = new(
        @"\b(?:single|factory|scoped|viewModel|worker)(?:Of)?\s*(?:<\s*(?<type>[A-Z][a-zA-Z0-9_]*)\s*>|\(\s*::(?<ctor>[A-Z][a-zA-Z0-9_]*)|\s*\{\s*(?<impl>[A-Z][a-zA-Z0-9_]*)\s*\()",
        RegexOptions.Compiled);

    public KoinLibraryParser()
    {
        Id = "koin";
        Name = "Koin (Kotlin DI)";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "org.koin.*",
            "io.insert-koin:koin*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerKoin, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in KoinBindRegex.Matches(content))
        {
            var target = match.Groups[1].Success && !string.IsNullOrEmpty(match.Groups[1].Value)
                ? match.Groups[1].Value.Trim()
                : match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value)
                    ? match.Groups[2].Value.Trim()
                    : match.Groups[3].Value.Trim();
            if (!string.IsNullOrEmpty(target))
            {
                fileNode.References.Add(new Reference(fileNode.Id, target, OntologyConstants.Relationships.DependsOn));
            }
        }
    }
}
