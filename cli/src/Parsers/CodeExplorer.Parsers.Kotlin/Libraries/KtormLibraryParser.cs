using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class KtormLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerKtorm = "org.ktorm";
    private const string MarkerKomapper = "org.komapper";

    private static readonly Regex KtormTableRegex = new(
        @":\s*(?:Table|BaseTable)\s*<[^>]+>\s*\(\s*[""'](?<tbl>[a-zA-Z0-9_]+)[""']",
        RegexOptions.Compiled);

    private static readonly Regex KomapperTableRegex = new(
        @"@KomapperTable\s*\(\s*(?:name\s*=\s*)?[""'](?<tbl>[a-zA-Z0-9_]+)[""']",
        RegexOptions.Compiled);

    public KtormLibraryParser()
    {
        Id = "ktorm";
        Name = "Ktorm & Komapper";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.ktorm.*",
            "org.ktorm:ktorm*",
            "org.komapper*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerKtorm, StringComparison.Ordinal) &&
            !content.Contains(MarkerKomapper, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in KtormTableRegex.Matches(content))
        {
            var tbl = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(tbl))
            {
                fileNode.References.Add(new Reference(fileNode.Id, tbl, OntologyConstants.Relationships.PersistedIn));
            }
        }

        foreach (Match match in KomapperTableRegex.Matches(content))
        {
            var tbl = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(tbl))
            {
                fileNode.References.Add(new Reference(fileNode.Id, tbl, OntologyConstants.Relationships.PersistedIn));
            }
        }
    }
}
