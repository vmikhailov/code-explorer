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

    // Server frameworks: a project depending on one of these is a backend even if it also pulls in
    // react/react-dom (server-side rendering of emails, PDFs, OG images ...).
    private static readonly HashSet<string> BackendFrameworks = new(StringComparer.OrdinalIgnoreCase)
    {
        "@nestjs/core", "express", "fastify", "koa", "@hapi/hapi", "@adonisjs/core", "@feathersjs/feathers"
    };

    // Signals that the project actually builds and serves a browser/mobile UI.
    private static readonly HashSet<string> FrontendBuildSignals = new(StringComparer.OrdinalIgnoreCase)
    {
        "next", "nuxt", "vite", "react-scripts", "@vitejs/plugin-react", "@vitejs/plugin-react-swc", "@vitejs/plugin-vue",
        "webpack-dev-server", "parcel", "@remix-run/react", "gatsby", "astro", "@sveltejs/kit", "react-native", "expo"
    };

    /// <summary>
    /// True when the project is a server application that merely uses a UI library for server-side rendering.
    /// Such projects must not be classified as frontend apps or UI component libraries.
    /// </summary>
    internal static bool IsBackendWithServerSideRendering(ProjectContext context)
    {
        var hasBackendFramework = context.Dependencies.Any(d => BackendFrameworks.Contains(d)) ||
                                  context.FilesInDirectory.Any(f => Path.GetFileName(f).Equals("nest-cli.json", StringComparison.OrdinalIgnoreCase));
        if (!hasBackendFramework) return false;

        var hasFrontendBuild = context.Dependencies.Any(d => FrontendBuildSignals.Contains(d)) ||
                               context.FilesInDirectory.Any(f => Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase));
        return !hasFrontendBuild;
    }

    public bool CanHandle(ProjectContext context)
    {
        if (IsBackendWithServerSideRendering(context)) return false;

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
