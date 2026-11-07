using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class SupabaseFirebaseKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerSupabase = "supabase";
    private const string MarkerFirebase = "firebase";

    private static readonly Regex TableCollectionRegex = new(
        @"\.\s*(?:from|collection)\s*\(\s*[""'](?<table>[a-zA-Z0-9_\-]+)[""']\s*\)",
        RegexOptions.Compiled);

    public SupabaseFirebaseKotlinLibraryParser()
    {
        Id = "supabase-firebase-kotlin";
        Name = "Supabase-kt & Firebase Kotlin";
        Type = SemanticExtensionType.DocumentDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "io.github.jan.supabase*",
            "dev.gitlive.firebase*",
            "com.google.firebase.firestore*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerSupabase, StringComparison.OrdinalIgnoreCase) &&
            !content.Contains(MarkerFirebase, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        foreach (Match match in TableCollectionRegex.Matches(content))
        {
            var tableName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(tableName))
            {
                fileNode.References.Add(new Reference(fileNode.Id, tableName, OntologyConstants.Relationships.UsesDb));
            }
        }
    }
}
