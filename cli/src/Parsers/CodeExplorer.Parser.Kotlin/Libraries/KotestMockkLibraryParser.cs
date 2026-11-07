using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class KotestMockkLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerKotest = "io.kotest";
    private const string MarkerMockk = "io.mockk";

    private static readonly Regex MockkTargetRegex = new(
        @"\b(?:mockk|spyk|relaxedMockk)\s*<\s*(?<target>[A-Z][a-zA-Z0-9_]*)\s*>",
        RegexOptions.Compiled);

    public KotestMockkLibraryParser()
    {
        Id = "kotest-mockk";
        Name = "Kotest & MockK";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.TestFramework;
        SupportedPatterns = ["io.kotest*", "io.mockk*"];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerKotest, StringComparison.Ordinal) &&
            !content.Contains(MarkerMockk, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in MockkTargetRegex.Matches(content))
        {
            var target = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(target))
            {
                fileNode.References.Add(new Reference(fileNode.Id, target, OntologyConstants.Relationships.Calls));
            }
        }
    }
}
