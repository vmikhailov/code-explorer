using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class DaggerHiltKodeinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerDagger = "dagger";
    private const string MarkerKodein = "kodein";
    private const string MarkerTatarka = "tatarka.inject";

    private static readonly Regex BindsProvidesRegex = new(
        @"@(?:Binds|Provides)\b[\s\S]{0,160}?\bfun\s+[a-zA-Z0-9_]+\s*\([^)]*?(?<impl>[A-Z][a-zA-Z0-9_]*)\s*\)\s*:\s*(?<iface>[A-Z][a-zA-Z0-9_]*)",
        RegexOptions.Compiled);

    private static readonly Regex KodeinBindRegex = new(
        @"\bbind(?:Singleton|Provider)?\s*<\s*(?<iface>[A-Z][a-zA-Z0-9_]*)\s*>\s*\(\s*\)\s*(?:with\s+\w+\s*)?\{\s*(?<impl>[A-Z][a-zA-Z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    public DaggerHiltKodeinLibraryParser()
    {
        Id = "dagger-hilt-kodein";
        Name = "Dagger, Hilt & Kodein-DI";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "dagger.*",
            "me.tatarka.inject*",
            "org.kodein.di*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerDagger, StringComparison.Ordinal) &&
            !content.Contains(MarkerKodein, StringComparison.Ordinal) &&
            !content.Contains(MarkerTatarka, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in BindsProvidesRegex.Matches(content))
        {
            var impl = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(impl))
            {
                fileNode.References.Add(new Reference(fileNode.Id, impl, OntologyConstants.Relationships.DependsOn));
            }
        }

        foreach (Match match in KodeinBindRegex.Matches(content))
        {
            var impl = match.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(impl))
            {
                fileNode.References.Add(new Reference(fileNode.Id, impl, OntologyConstants.Relationships.DependsOn));
            }
        }
    }
}
