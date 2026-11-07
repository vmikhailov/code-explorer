namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class KoinLibraryParser : DeclarativeSemanticExtension
{
    public KoinLibraryParser()
    {
        Id = "koin";
        Name = "Koin (Kotlin DI)";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "org.koin.*",
            "io.insert-koin:koin*"
        ];
    }
}
