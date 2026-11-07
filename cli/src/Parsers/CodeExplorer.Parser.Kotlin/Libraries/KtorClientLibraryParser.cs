namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class KtorClientLibraryParser : DeclarativeSemanticExtension
{
    public KtorClientLibraryParser()
    {
        Id = "ktor-client";
        Name = "Ktor Client";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["io.ktor.client.*", "io.ktor:ktor-client*"];
    }
}
