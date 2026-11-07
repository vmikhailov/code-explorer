using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class YarpReverseProxyLibraryParser : DeclarativeSemanticExtension
{
    public YarpReverseProxyLibraryParser()
    {
        Id = "yarp";
        Name = "YARP (Yet Another Reverse Proxy)";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["Yarp.ReverseProxy", "Yarp.ReverseProxy.*"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "MapReverseProxy", "AddReverseProxy", "LoadFromConfig", "LoadFromMemory", "SendAsync" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "yarp:reverse-proxy"
            }
        ];
    }
}
