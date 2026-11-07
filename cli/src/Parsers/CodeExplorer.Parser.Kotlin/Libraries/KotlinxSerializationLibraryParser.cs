namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class KotlinxSerializationLibraryParser : DeclarativeSemanticExtension
{
    public KotlinxSerializationLibraryParser()
    {
        Id = "kotlinx-serialization";
        Name = "kotlinx.serialization";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "kotlinx.serialization.*",
            "org.jetbrains.kotlinx:kotlinx-serialization*"
        ];
    }
}
