namespace CodeExplorer.Core.Parser.Components.Parsers;

/// <summary>
/// Component parser for Angular projects.
/// Accurately differentiates between Angular Applications (FrontendApp -> Ingress)
/// and Angular Component Libraries (UiLibrary -> Foundation, e.g. inputtext, modal, tooltip).
/// </summary>
public class AngularComponentParser : IComponentLibraryParser
{
    public string Id => "angular";
    public string Name => "Angular";

    public bool CanHandle(ProjectContext context)
    {
        return context.Dependencies.Any(d =>
            d.Equals("@angular/core", StringComparison.OrdinalIgnoreCase) ||
            d.StartsWith("@angular/", StringComparison.OrdinalIgnoreCase));
    }

    public ComponentAnalysisResult AnalyzeManifest(ProjectContext context)
    {
        // Concrete library evidence:
        // 1. Presence of ng-package.json
        // 2. package.json contains "ngPackage"
        // 3. Explicit library manifest flag
        // 4. Presence of public-api.ts / absence of index.html
        var hasNgPackageFile = context.FilesInDirectory.Any(f =>
            Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase));

        var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
        var isLibraryManifest = manifestType == "library" || context.ManifestProperties.GetValueOrDefault("is_library") == "true";

        var hasIndexHtml = context.FilesInDirectory.Any(f =>
            Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase));

        var hasPublicApi = context.FilesInDirectory.Any(f =>
            Path.GetFileName(f).Equals("public-api.ts", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(f).Equals("public_api.ts", StringComparison.OrdinalIgnoreCase));

        var isLibrary = hasNgPackageFile || isLibraryManifest || (hasPublicApi && !hasIndexHtml);

        if (isLibrary)
        {
            return new ComponentAnalysisResult
            {
                ComponentId = Id,
                ComponentName = Name,
                Role = LibraryRole.UiComponentLibrary,
                Capabilities = ComponentCapabilities.UiLibrary,
                Metadata = new Dictionary<string, string>
                {
                    ["ui_type"] = "library",
                    ["is_ui_library"] = "true"
                }
            };
        }

        return new ComponentAnalysisResult
        {
            ComponentId = Id,
            ComponentName = Name,
            Role = LibraryRole.FrontendFramework,
            Capabilities = ComponentCapabilities.FrontendApp,
            Metadata = new Dictionary<string, string>
            {
                ["ui_type"] = "application",
                ["is_frontend_app"] = "true"
            }
        };
    }
}
