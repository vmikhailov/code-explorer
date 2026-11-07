using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class ComposeNavigationKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerVoyager = "voyager";
    private const string MarkerDecompose = "decompose";

    private static readonly Regex NavPushRegex = new(
        @"\.\s*(?:push|replace|replaceAll|bringToFront)\s*\(\s*(?<screen>[A-Z][a-zA-Z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    public ComposeNavigationKotlinLibraryParser()
    {
        Id = "compose-navigation-kotlin";
        Name = "Voyager & Decompose (Compose Multiplatform Navigation)";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "cafe.adriel.voyager*",
            "com.arkivanov.decompose*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerVoyager, StringComparison.Ordinal) &&
            !content.Contains(MarkerDecompose, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in NavPushRegex.Matches(content))
        {
            var screen = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(screen))
            {
                fileNode.References.Add(new Reference(fileNode.Id, screen, OntologyConstants.Relationships.Calls));
            }
        }
    }
}
