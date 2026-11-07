namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class KtormLibraryParser : DeclarativeSemanticExtension
{
    public KtormLibraryParser()
    {
        Id = "ktorm";
        Name = "Ktorm";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns =
        [
            "org.ktorm.*",
            "org.ktorm:ktorm*"
        ];
    }
}
