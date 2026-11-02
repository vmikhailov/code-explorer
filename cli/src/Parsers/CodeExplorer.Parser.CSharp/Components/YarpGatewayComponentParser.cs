using CodeExplorer.Core.Parser.Components;

namespace CodeExplorer.Parser.CSharp.Components;

/// <summary>
/// Component parser for API Gateways and reverse proxies in .NET (YARP, Ocelot).
/// </summary>
public class YarpGatewayComponentParser : IComponentLibraryParser
{
    public string Id => "yarp-gateway";
    public string Name => "YARP / Ocelot Gateway";

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d =>
            d.StartWithIC("Yarp.ReverseProxy") ||
            d.StartWithIC("Ocelot"));
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.ApiGateway,
            Capabilities = ComponentCapabilities.ApiGateway | ComponentCapabilities.HttpEndpoints,
            Metadata = new Dictionary<string, string>
            {
                ["gateway_type"] = "reverse_proxy",
                ["is_api_gateway"] = "true"
            }
        };
    }
}
