using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class SupabaseFirebaseTsLibraryParser : DeclarativeSemanticExtension
{
    public SupabaseFirebaseTsLibraryParser()
    {
        Id = "supabase-firebase-ts";
        Name = "Supabase JS & Firebase Firestore";
        Type = SemanticExtensionType.DocumentDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "@supabase/supabase-js*",
            "firebase-admin*",
            "firebase/firestore*"
        ];
        DataStoreAccessRules =
        [
            new DataStoreAccessRule
            {
                CallMethodNames = new[] { "from", "collection", "rpc" }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
