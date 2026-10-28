using CodeExplorer.Core.Parser.Components;

namespace CodeExplorer.Parser.Python.Components;

/// <summary>
/// Component parser for FastAPI web framework applications.
/// </summary>
public class FastApiComponentParser : IComponentLibraryParser
{
    public string Id => "fastapi";
    public string Name => "FastAPI";

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d => d.Equals("fastapi", StringComparison.OrdinalIgnoreCase) ||
                                              d.StartWithIC("fastapi-"));
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
                ["web_framework"] = "fastapi"
            }
        };
    }
}
