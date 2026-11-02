using CodeExplorer.Core.Parser.Components;

namespace CodeExplorer.Parser.TypeScript.Components;

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
        "react", "react-dom", "next", "vue", "nuxt", "svelte", "solid-js", "preact", "react-native", "expo"
    };

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d => FrontendFrameworks.Contains(d)) ||
               context.FilesInDirectory.Any(f => Path.GetFileName(f).Equals("app.json", StringComparison.OrdinalIgnoreCase)) ||
               context.ManifestProperties.GetValueOrDefault("is_vscode_extension") == "true" ||
               context.ManifestProperties.GetValueOrDefault("framework_type") == "extension";
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        var isVsCodeExtension = context.ManifestProperties.GetValueOrDefault("is_vscode_extension") == "true" ||
                                context.ManifestProperties.GetValueOrDefault("framework_type") == "extension";
        var hasNext = context.Dependencies.Any(d => d.Equals("next", StringComparison.OrdinalIgnoreCase));
        var hasNuxt = context.Dependencies.Any(d => d.Equals("nuxt", StringComparison.OrdinalIgnoreCase));
        var hasReactNative = context.Dependencies.Any(d => d.Equals("react-native", StringComparison.OrdinalIgnoreCase) || d.Equals("expo", StringComparison.OrdinalIgnoreCase)) ||
                             context.FilesInDirectory.Any(f => Path.GetFileName(f).Equals("app.json", StringComparison.OrdinalIgnoreCase));

        var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
        var isExplicitLibrary = manifestType == "library" || context.ManifestProperties.GetValueOrDefault("is_library") == "true";

        var hasIndexHtml = context.FilesInDirectory.Any(f =>
            Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase));

        if (isVsCodeExtension || hasNext || hasNuxt || hasReactNative || (manifestType == "application" && !isExplicitLibrary) || (hasIndexHtml && !isExplicitLibrary))
        {
            var appName = isVsCodeExtension
                ? "VS Code Extension"
                : (hasReactNative
                    ? (context.Dependencies.Any(d => d.Equals("expo", StringComparison.OrdinalIgnoreCase)) ? "Expo App" : "React Native App")
                    : (hasNext ? "Next.js" : (hasNuxt ? "Nuxt" : "React App")));

            return new ComponentAnalysisResult
            {
                ComponentId = Id,
                ComponentName = appName,
                Role = LibraryRole.FrontendFramework,
                Capabilities = ComponentCapabilities.FrontendApp,
                Metadata = new Dictionary<string, string>
                {
                    ["ui_type"] = isVsCodeExtension ? "extension" : (hasReactNative ? "mobile" : "application"),
                    ["is_frontend_app"] = "true",
                    ["framework_type"] = isVsCodeExtension ? "extension" : (hasReactNative ? "mobile" : "frontend")
                }
            };
        }

        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.UiComponentLibrary,
            Capabilities = ComponentCapabilities.UiLibrary,
            Metadata = new Dictionary<string, string>
            {
                ["ui_type"] = "library",
                ["is_ui_library"] = "true",
                ["framework_type"] = "frontend"
            }
        };
    }
}
