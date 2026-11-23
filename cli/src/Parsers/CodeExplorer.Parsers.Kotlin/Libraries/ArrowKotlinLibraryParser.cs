using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class ArrowKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerArrow = "arrow.";
    private const string MarkerCircuitBreaker = "CircuitBreaker";
    private const string ResiliencePrefix = "arrow-resilience:";

    private static readonly Regex ScheduleRetryRegex = new(
        @"\bSchedule\s*\.\s*(?<policy>exponential|recurs|spaced|linear|fibonacci)\b",
        RegexOptions.Compiled);

    public ArrowKotlinLibraryParser()
    {
        Id = "arrow-kt";
        Name = "Arrow-kt & Arrow Resilience";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "arrow.core.*",
            "arrow.fx.*",
            "arrow.resilience.*",
            "io.arrow-kt:arrow*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerArrow, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in ScheduleRetryRegex.Matches(content))
        {
            var policy = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(policy))
            {
                fileNode.References.Add(new Reference(fileNode.Id, ResiliencePrefix + policy, OntologyConstants.Relationships.Calls));
            }
        }

        if (content.Contains(MarkerCircuitBreaker, StringComparison.Ordinal))
        {
            fileNode.References.Add(new Reference(fileNode.Id, ResiliencePrefix + "circuit-breaker", OntologyConstants.Relationships.Calls));
        }
    }
}
