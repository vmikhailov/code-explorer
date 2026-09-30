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
        return context.Dependencies.Any(d => d.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase)) ||
               context.ManifestProperties.GetValueOrDefault("sdk")?.Equals("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) == true;
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.WebService,
            Capabilities = ComponentCapabilities.HttpEndpoints,
            Metadata = new Dictionary<string, string>
            {
                ["web_framework"] = "aspnetcore"
            }
        };
    }
}
