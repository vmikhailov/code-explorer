using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class NetflixDgsGraphQlJavaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "netflix-dgs-graphql";
    private const string ExtensionName = "Netflix DGS & GraphQL-Java";
    private const string MethodQuery = "QUERY";
    private const string MethodMutation = "MUTATION";
    private const string MethodSubscription = "SUBSCRIPTION";
    private const string DgsQueryAnnotation = "DgsQuery";
    private const string DgsMutationAnnotation = "DgsMutation";
    private const string DgsSubscriptionAnnotation = "DgsSubscription";
    private const string DgsDataAnnotation = "DgsData";
    private const string DgsComponentAnnotation = "DgsComponent";
    private const string FieldKey = "field";
    private const string ValueKey = "value";

    public NetflixDgsGraphQlJavaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns =
        [
            "com.netflix.graphql.dgs*",
            "com.graphql-java*",
            "graphql.kickstart*",
            "graphql.schema*"
        ];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [DgsQueryAnnotation] = MethodQuery,
                    [DgsMutationAnnotation] = MethodMutation,
                    [DgsSubscriptionAnnotation] = MethodSubscription,
                    [DgsDataAnnotation] = MethodQuery
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                RoutePrefixAnnotationNames = new[] { DgsComponentAnnotation }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                PathArgumentKeys = new[] { FieldKey, ValueKey }.ToFrozenSet(StringComparer.Ordinal)
            }
        ];
    }
}
