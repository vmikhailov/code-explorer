namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class ExposedLibraryParser : DeclarativeSemanticExtension
{
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
}
