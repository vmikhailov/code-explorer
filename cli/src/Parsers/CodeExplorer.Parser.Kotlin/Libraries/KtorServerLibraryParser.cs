namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class KtorServerLibraryParser : DeclarativeSemanticExtension
{
    public KtorServerLibraryParser()
    {
        Id = "ktor-server";
        Name = "Ktor Server";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["io.ktor.server.*", "io.ktor:ktor-server*"];
    }
}
