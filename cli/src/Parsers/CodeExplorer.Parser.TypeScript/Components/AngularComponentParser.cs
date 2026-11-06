using CodeExplorer.Core.Database;
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
                var content = PhysicalFileStore.ReadAllText(packageJsonPath);
                var root = CypherValue.TryParseJsonText(content);
                if (root.Kind == CypherValueKind.Object &&
                    root.TryGetProperty("ngPackage", out _))
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
                    var json = PhysicalFileStore.ReadAllText(ancestorAngularJson);
                    var angRoot = CypherValue.TryParseJsonText(json);
                    if (angRoot.TryGetProperty("projects", out var projectsObj) &&
                        projectsObj.Kind == CypherValueKind.Object)
                    {
                        foreach (var pProp in projectsObj.AsObject())
                        {
                            var projVal = pProp.Value;
                            var rootPath = projVal.TryGetProperty("root", out var r) ? r.AsNullableString() : null;
                            if (!string.IsNullOrEmpty(rootPath) &&
                                directoryPath.Replace('\\', '/').EndsWith(rootPath.Replace('\\', '/').Trim('/'), StringComparison.OrdinalIgnoreCase))
                            {
                                if (projVal.TryGetProperty("projectType", out var ptProp))
                                {
                                    var pt = ptProp.AsNullableString()?.ToLowerInvariant();
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
            var json = PhysicalFileStore.ReadAllText(manifestPath);
            var root = CypherValue.TryParseJsonText(json);
            if (!root.TryGetProperty("projects", out var projectsObj) ||
                projectsObj.Kind != CypherValueKind.Object)
            {
                return;
            }

            foreach (var projProp in projectsObj.AsObject())
            {
                var childName = projProp.Key;
                var projVal = projProp.Value;
                if (projVal.Kind != CypherValueKind.Object) continue;

                var rootRel = projVal.TryGetProperty("root", out var rProp) && rProp.Kind == CypherValueKind.String
                    ? rProp.AsString().Replace('\\', '/').Trim('/')
                    : "";
                var srcRootRel = projVal.TryGetProperty("sourceRoot", out var srProp) && srProp.Kind == CypherValueKind.String
                    ? srProp.AsString().Replace('\\', '/').Trim('/')
                    : "";
                var projType = projVal.TryGetProperty("projectType", out var ptProp) && ptProp.Kind == CypherValueKind.String
                    ? ptProp.AsString().ToLowerInvariant()
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
