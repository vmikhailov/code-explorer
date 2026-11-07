using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class SpringCloudGatewayLibraryParser : DeclarativeSemanticExtension
{
    public SpringCloudGatewayLibraryParser()
    {
        Id = "spring-cloud-gateway";
        Name = "Spring Cloud Gateway";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["org.springframework.cloud.gateway.*"];
        InboundRouteCallRules =
        [
            new InboundRouteCallRule
            {
                MethodToHttpVerb = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["path"] = "ALL"
                }.ToFrozenDictionary(StringComparer.Ordinal)
            }
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "uri" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Calls
            }
        ];
    }
}
