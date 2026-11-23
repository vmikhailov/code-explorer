using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class ExposedLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerExposed = "org.jetbrains.exposed";

    private static readonly Regex ExposedTableObjectRegex = new(
        @"\bobject\s+(?<cls>[A-Z][a-zA-Z0-9_]*)\s*:\s*(?:IntIdTable|LongIdTable|UUIDTable|IdTable|Table)\s*(?:<[^>]+>\s*)?\(\s*(?:(?:name\s*=\s*)?[""'](?<tbl>[^""']+)[""'])?",
        RegexOptions.Compiled);

    public ExposedLibraryParser()
    {
        Id = "exposed";
        Name = "JetBrains Exposed";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.jetbrains.exposed.*",
            "org.jetbrains.exposed:exposed*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerExposed, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in ExposedTableObjectRegex.Matches(content))
        {
            var cls = match.Groups[1].Value.Trim();
            var tbl = match.Groups[2].Success && !string.IsNullOrWhiteSpace(match.Groups[2].Value)
                ? match.Groups[2].Value.Trim()
                : cls.ToLowerInvariant();
            if (!string.IsNullOrEmpty(tbl))
            {
                fileNode.References.Add(new Reference(fileNode.Id, tbl, OntologyConstants.Relationships.PersistedIn));
            }
        }
    }
}
