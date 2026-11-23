using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class KotlinxSerializationLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerSerialization = "kotlinx.serialization";
    private const string MarkerMoshi = "com.squareup.moshi";

    private static readonly Regex DecodeFromStringRegex = new(
        @"\bdecodeFromString\s*<\s*(?<dto>[A-Z][a-zA-Z0-9_]*)\s*>",
        RegexOptions.Compiled);

    private static readonly Regex MoshiAdapterRegex = new(
        @"\.adapter\s*\(\s*(?<dto>[A-Z][a-zA-Z0-9_]*)::class",
        RegexOptions.Compiled);

    public KotlinxSerializationLibraryParser()
    {
        Id = "kotlinx-serialization";
        Name = "kotlinx.serialization & Moshi";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "kotlinx.serialization.*",
            "org.jetbrains.kotlinx:kotlinx-serialization*",
            "com.squareup.moshi*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerSerialization, StringComparison.Ordinal) &&
            !content.Contains(MarkerMoshi, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in DecodeFromStringRegex.Matches(content))
        {
            var dto = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(dto))
            {
                fileNode.References.Add(new Reference(fileNode.Id, dto, OntologyConstants.Relationships.References));
            }
        }

        foreach (Match match in MoshiAdapterRegex.Matches(content))
        {
            var dto = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(dto))
            {
                fileNode.References.Add(new Reference(fileNode.Id, dto, OntologyConstants.Relationships.References));
            }
        }
    }
}
