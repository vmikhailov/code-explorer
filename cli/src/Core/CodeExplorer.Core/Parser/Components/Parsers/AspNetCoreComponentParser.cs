namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for ASP.NET Core applications.
/// </summary>
public class AspNetCoreComponentParser : IComponentLibraryParser
{
    public string Id => "aspnetcore";
    public string Name => "ASP.NET Core";

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d => d.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase) ||
                                             d.StartsWith("Microsoft.AspNet.WebApi", StringComparison.OrdinalIgnoreCase) ||
                                             d.StartsWith("Microsoft.AspNet.Mvc", StringComparison.OrdinalIgnoreCase)) ||
               context.ManifestProperties.GetValueOrDefault("sdk")?.Equals("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) == true ||
               string.Equals(context.ManifestProperties.GetValueOrDefault("framework_type"), "web", StringComparison.OrdinalIgnoreCase);
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        var isCore = context.Dependencies.Any(d => d.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase)) ||
                     context.ManifestProperties.GetValueOrDefault("sdk")?.Equals("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) == true;

        return new ComponentAnalysisResult
        {
            ComponentId = isCore ? "aspnetcore" : "aspnet",
            ComponentName = isCore ? "ASP.NET Core" : "ASP.NET",
            Role = LibraryRole.WebService,
            Capabilities = ComponentCapabilities.HttpEndpoints,
            Metadata = new Dictionary<string, string>
            {
                ["web_framework"] = isCore ? "aspnetcore" : "aspnet"
            }
        };
    }
}
