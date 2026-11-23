using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public sealed class RealmRoomKMongoKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerRealm = "io.realm.kotlin";
    private const string MarkerObjectBox = "io.objectbox";
    private const string MarkerKMongo = "org.litote.kmongo";
    private const string MarkerMongoKotlin = "com.mongodb.kotlin";

    private static readonly Regex RealmObjectClassRegex = new(
        @"\bclass\s+(?<cls>[A-Z][a-zA-Z0-9_]*)[^{:]*:\s*(?:RealmObject|EmbeddedRealmObject)\b",
        RegexOptions.Compiled);

    private static readonly Regex MongoCollectionRegex = new(
        @"\.\s*getCollection\s*<\s*(?<doc>[A-Z][a-zA-Z0-9_]*)\s*>",
        RegexOptions.Compiled);

    public RealmRoomKMongoKotlinLibraryParser()
    {
        Id = "realm-kmongo-kotlin";
        Name = "Realm Kotlin, ObjectBox, KMongo & MongoDB Kotlin Driver";
        Type = SemanticExtensionType.DocumentDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "io.realm.kotlin*",
            "io.objectbox*",
            "org.litote.kmongo*",
            "com.mongodb.kotlin*",
            "androidx.room*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (content.Contains(MarkerRealm, StringComparison.Ordinal) ||
            content.Contains(MarkerObjectBox, StringComparison.Ordinal))
        {
            foreach (Match match in RealmObjectClassRegex.Matches(content))
            {
                var cls = match.Groups[1].Value.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(cls))
                {
                    fileNode.References.Add(new Reference(fileNode.Id, cls, OntologyConstants.Relationships.PersistedIn));
                }
            }
        }

        if (content.Contains(MarkerKMongo, StringComparison.Ordinal) ||
            content.Contains(MarkerMongoKotlin, StringComparison.Ordinal))
        {
            foreach (Match match in MongoCollectionRegex.Matches(content))
            {
                var doc = match.Groups[1].Value.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(doc))
                {
                    fileNode.References.Add(new Reference(fileNode.Id, doc, OntologyConstants.Relationships.UsesDb));
                }
            }
        }
    }
}
