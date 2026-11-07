namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class GraphQlKotlinLibraryParser : DeclarativeSemanticExtension
{
    public GraphQlKotlinLibraryParser()
    {
        Id = "graphql-kotlin";
        Name = "graphql-kotlin & DGS";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "com.expediagroup.graphql.*",
            "com.expediagroup:graphql-kotlin*",
            "com.netflix.graphql.dgs.*"
        ];
    }
}
