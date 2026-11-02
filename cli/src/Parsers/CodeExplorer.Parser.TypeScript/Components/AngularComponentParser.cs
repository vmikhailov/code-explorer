using CodeExplorer.Core.Parser.Components;

namespace CodeExplorer.Parser.TypeScript.Components;

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
                   d.Equals("ng-packagr", StringComparison.OrdinalIgnoreCase) ||
                   d.StartWithIC("@angular/")) ||
               context.FilesInDirectory.Any(f =>
                   Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase) ||
                   Path.GetFileName(f).Equals("angular.json", StringComparison.OrdinalIgnoreCase));
    }

    public static void EnrichManifestProperties(string directoryPath, string[] filesInDirectory, Dictionary<string, string> props)
    {
        var hasNgPackage = filesInDirectory.Any(f => Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase));
        if (hasNgPackage)
        {
            props["manifest_type"] = "library";
            props["framework_type"] = "frontend";
            props["is_ui_library"] = "true";
            props["is_library"] = "true";
        }
        else if (filesInDirectory.Any(f => Path.GetFileName(f).Equals("angular.json", StringComparison.OrdinalIgnoreCase)) ||
                 File.Exists(Path.Combine(directoryPath, "angular.json")))
        {
            props.TryAdd("framework_type", "frontend");
        }

        var packageJsonPath = Path.Combine(directoryPath, "package.json");
        if (File.Exists(packageJsonPath))
        {
            try
            {
                var content = File.ReadAllText(packageJsonPath);
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("ngPackage", out _))
                {
                    props["manifest_type"] = "library";
                    props["framework_type"] = "frontend";
                    props["is_ui_library"] = "true";
                    props["is_library"] = "true";
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                // Ignore
            }
        }

        var parentDir = Path.GetDirectoryName(directoryPath);
        while (parentDir != null && parentDir.Length >= 3)
        {
            var ancestorAngularJson = Path.Combine(parentDir, "angular.json");
            if (File.Exists(ancestorAngularJson))
            {
                try
                {
                    var json = File.ReadAllText(ancestorAngularJson);
                    using var angDoc = System.Text.Json.JsonDocument.Parse(json);
                    if (angDoc.RootElement.TryGetProperty("projects", out var projectsObj) &&
                        projectsObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        foreach (var pProp in projectsObj.EnumerateObject())
                        {
                            var projVal = pProp.Value;
                            var rootPath = projVal.TryGetProperty("root", out var r) ? r.GetString() : null;
                            if (!string.IsNullOrEmpty(rootPath) &&
                                directoryPath.Replace('\\', '/').EndsWith(rootPath.Replace('\\', '/').Trim('/'), StringComparison.OrdinalIgnoreCase))
                            {
                                if (projVal.TryGetProperty("projectType", out var ptProp))
                                {
                                    var pt = ptProp.GetString()?.ToLowerInvariant();
                                    if (pt == "application")
                                    {
                                        props["manifest_type"] = "application";
                                        props["framework_type"] = "frontend";
                                    }
                                    else if (pt == "library")
                                    {
                                        props["manifest_type"] = "library";
                                        props["framework_type"] = "frontend";
                                        props["is_ui_library"] = "true";
                                        props["is_library"] = "true";
                                    }
                                }
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
                {
                    // Ignore monorepo ancestor parse errors
                }
                break;
            }
            parentDir = Path.GetDirectoryName(parentDir);
        }
    }

    public static void ExtractWorkspaceSubProjects(
        string directoryPath,
        IProjectParser projectParser,
        List<string> sourceRoots,
        List<string> excludedSubpaths,
        List<ParsedProjectDescriptor> subProjects)
    {
        ExtractDeclarativeSubProjects(directoryPath, "angular.json", isFrontendWorkspace: true, projectParser, sourceRoots, excludedSubpaths, subProjects);
    }

    internal static void ExtractDeclarativeSubProjects(
        string directoryPath,
        string manifestFileName,
        bool isFrontendWorkspace,
        IProjectParser projectParser,
        List<string> sourceRoots,
        List<string> excludedSubpaths,
        List<ParsedProjectDescriptor> subProjects)
    {
        var manifestPath = Path.Combine(directoryPath, manifestFileName);
        if (!File.Exists(manifestPath)) return;

        try
        {
            var json = File.ReadAllText(manifestPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("projects", out var projectsObj) ||
                projectsObj.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                return;
            }

            foreach (var projProp in projectsObj.EnumerateObject())
            {
                var childName = projProp.Name;
                var projVal = projProp.Value;
                if (projVal.ValueKind != System.Text.Json.JsonValueKind.Object) continue;

                var rootRel = projVal.TryGetProperty("root", out var rProp) && rProp.ValueKind == System.Text.Json.JsonValueKind.String
                    ? (rProp.GetString() ?? "").Replace('\\', '/').Trim('/')
                    : "";
                var srcRootRel = projVal.TryGetProperty("sourceRoot", out var srProp) && srProp.ValueKind == System.Text.Json.JsonValueKind.String
                    ? (srProp.GetString() ?? "").Replace('\\', '/').Trim('/')
                    : "";
                var projType = projVal.TryGetProperty("projectType", out var ptProp) && ptProp.ValueKind == System.Text.Json.JsonValueKind.String
                    ? (ptProp.GetString() ?? "").ToLowerInvariant()
                    : "application";

                if (string.IsNullOrEmpty(rootRel) || rootRel == ".")
                {
                    if (!string.IsNullOrEmpty(srcRootRel) && !sourceRoots.Contains(srcRootRel, StringComparer.OrdinalIgnoreCase))
                    {
                        sourceRoots.Add(srcRootRel);
                    }
                    if (!excludedSubpaths.Contains("src/assets", StringComparer.OrdinalIgnoreCase) &&
                        Directory.Exists(Path.Combine(directoryPath, "src", "assets")))
                    {
                        excludedSubpaths.Add("src/assets");
                    }
                    continue;
                }

                var childAbsDir = Path.GetFullPath(Path.Combine(directoryPath, rootRel));
                if (!Directory.Exists(childAbsDir)) continue;

                var childFiles = Directory.GetFiles(childAbsDir);
                var childProps = projectParser.ExtractManifestProperties(childAbsDir, childFiles);
                if (projType == "library")
                {
                    childProps["manifest_type"] = "library";
                    childProps["is_library"] = "true";
                    childProps["role"] = "SharedLibrary";
                    childProps["primary_role"] = "Library";
                    if (isFrontendWorkspace)
                    {
                        childProps["framework_type"] = "frontend";
                        childProps["is_ui_library"] = "true";
                    }
                }
                else
                {
                    childProps["manifest_type"] = "application";
                }

                subProjects.Add(new ParsedProjectDescriptor(
                    Name: childName,
                    DirectoryPath: childAbsDir,
                    ProjectType: projectParser.ProjectType,
                    ManifestProperties: childProps));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Ignore
        }
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
