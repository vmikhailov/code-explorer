namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class ArrowKotlinLibraryParser : DeclarativeSemanticExtension
{
    public ArrowKotlinLibraryParser()
    {
        Id = "arrow-kt";
        Name = "Arrow-kt";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "arrow.core.*",
            "arrow.fx.*",
            "io.arrow-kt:arrow*"
        ];
    }
}
