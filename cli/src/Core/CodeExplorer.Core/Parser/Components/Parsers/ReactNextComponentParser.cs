namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for React, Next.js, Vue, Nuxt, and modern frontend frameworks.
/// Distinguishes between UI web applications (Next.js, SPA with index.html) and UI component libraries.
/// </summary>
public class ReactNextComponentParser : IComponentLibraryParser
{
    public string Id => "react-next";
    public string Name => "React/Next.js/Vue";

    private static readonly HashSet<string> FrontendFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "react", "react-dom", "next", "vue", "nuxt", "svelte", "solid-js", "preact"
    };

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d => FrontendFrameworks.Contains(d));
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        var hasNext = context.Dependencies.Any(d => d.Equals("next", StringComparison.OrdinalIgnoreCase));
        var hasNuxt = context.Dependencies.Any(d => d.Equals("nuxt", StringComparison.OrdinalIgnoreCase));

        var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
        var isExplicitLibrary = manifestType == "library" || context.ManifestProperties.GetValueOrDefault("is_library") == "true";

        var hasIndexHtml = context.FilesInDirectory.Any(f =>
            Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase));

        if (hasNext || hasNuxt || (hasIndexHtml && !isExplicitLibrary))
        {
            return new ComponentAnalysisResult
            {
                ComponentId = Id,
                ComponentName = hasNext ? "Next.js" : (hasNuxt ? "Nuxt" : "React App"),
                Role = LibraryRole.FrontendFramework,
                Capabilities = ComponentCapabilities.FrontendApp,
                Metadata = new Dictionary<string, string>
                {
                    ["ui_type"] = "application",
                    ["is_frontend_app"] = "true"
                }
            };
        }

        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = "React/Vue Component Library",
            Role = LibraryRole.UiComponentLibrary,
            Capabilities = ComponentCapabilities.UiLibrary,
            Metadata = new Dictionary<string, string>
            {
                ["ui_type"] = "library",
                ["is_ui_library"] = "true"
            }
        };
    }
}
