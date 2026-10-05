using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.Android;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Kotlin;

public class KotlinProjectParser : IProjectParser
{
    public string LanguageName => "kotlin";

    public string ProjectType => "kotlin";

    public IReadOnlyCollection<string> ExcludedFolders =>
    [
        "build", ".gradle", "bin", "out", ".idea"
    ];

    public bool IsConfigurationFile(string fileName) => false;

    public IReadOnlyList<ILibraryConfigurationDescriptor> ConfigurationDescriptors => [];

    public IReadOnlyList<PackageDescriptor> Packages => [];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => [];

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        // 1. Detect multi-project containers (e.g. flutter/android with settings.gradle.kts and no project plugins applied to root)
        if (GradleBuildScriptParser.IsMultiProjectContainer(directoryPath))
        {
            return false;
        }

        // 2. Check for build.gradle.kts or build.gradle
        var hasBuildGradleKts = filesInDirectory.Any(f => Path.GetFileName(f).Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase));
        var hasBuildGradle = filesInDirectory.Any(f => Path.GetFileName(f).Equals("build.gradle", StringComparison.OrdinalIgnoreCase));

        if (hasBuildGradleKts || hasBuildGradle)
        {
            var info = GradleBuildScriptParser.ParseDirectory(directoryPath);
            if (info.IsKotlinProject || info.IsAndroidApplication || info.IsAndroidLibrary || info.IsFlutterProject)
            {
                return true;
            }

            // Check if directory contains any .kt files
            if (HasKotlinFiles(directoryPath))
            {
                return true;
            }
        }

        // 3. Maven pom.xml with kotlin
        var pomFile = Path.Combine(directoryPath, "pom.xml");
        if (File.Exists(pomFile))
        {
            try
            {
                var content = File.ReadAllText(pomFile);
                if (content.Contains("kotlin-maven-plugin", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("kotlin-stdlib", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var manifestPath = GradleBuildScriptParser.FindAndroidManifest(directoryPath);
        if (manifestPath != null)
        {
            var manifest = AndroidManifestParser.ParseFile(manifestPath);
            if (!string.IsNullOrWhiteSpace(manifest?.AppLabel))
            {
                return $"{manifest.AppLabel} (Android)";
            }
        }

        var buildInfo = GradleBuildScriptParser.ParseDirectory(directoryPath);
        if (buildInfo.IsAndroidApplication)
        {
            if (!string.IsNullOrWhiteSpace(buildInfo.ApplicationId))
            {
                var segment = buildInfo.ApplicationId.Split('.')[^1];
                var title = char.ToUpperInvariant(segment[0]) + segment[1..];
                return $"{title} (Android)";
            }
            if (!string.IsNullOrWhiteSpace(buildInfo.Namespace))
            {
                var segment = buildInfo.Namespace.Split('.')[^1];
                var title = char.ToUpperInvariant(segment[0]) + segment[1..];
                return $"{title} (Android)";
            }
        }

        if (!string.IsNullOrWhiteSpace(buildInfo.ArchivesBaseName))
        {
            return buildInfo.ArchivesBaseName;
        }

        if (!string.IsNullOrWhiteSpace(buildInfo.RootProjectName))
        {
            return buildInfo.RootProjectName;
        }

        var folderName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
        if (buildInfo.IsAndroidApplication)
        {
            return $"{folderName} (Android)";
        }

        return folderName;
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var info = GradleBuildScriptParser.ParseDirectory(directoryPath);

        if (info.IsAndroidApplication || info.IsAndroidLibrary)
        {
            props["manifest_type"] = "mobile";
            props["framework_type"] = "mobile";
            props["sdk"] = "Android";

            if (info.IsFlutterProject)
            {
                props["framework"] = "Flutter Android";
            }
            else
            {
                props["framework"] = "Android";
            }

            if (!string.IsNullOrEmpty(info.Namespace)) props["namespace"] = info.Namespace;
            if (!string.IsNullOrEmpty(info.ApplicationId)) props["application_id"] = info.ApplicationId;
            if (!string.IsNullOrEmpty(info.MinSdk)) props["min_sdk"] = info.MinSdk;
            if (!string.IsNullOrEmpty(info.TargetSdk)) props["target_sdk"] = info.TargetSdk;
            if (!string.IsNullOrEmpty(info.CompileSdk)) props["compile_sdk"] = info.CompileSdk;

            var manifestPath = GradleBuildScriptParser.FindAndroidManifest(directoryPath);
            if (manifestPath != null)
            {
                var manifest = AndroidManifestParser.ParseFile(manifestPath);
                if (manifest != null)
                {
                    if (!string.IsNullOrEmpty(manifest.AppLabel)) props["app_label"] = manifest.AppLabel;
                    if (!string.IsNullOrEmpty(manifest.PackageName)) props["package_name"] = manifest.PackageName;
                    if (manifest.Permissions.Count > 0)
                    {
                        props["permissions"] = System.Text.Json.JsonSerializer.Serialize(manifest.Permissions);
                    }
                    if (manifest.DeepLinks.Count > 0)
                    {
                        props["has_deep_links"] = "true";
                    }
                }
            }
        }
        else if (info.IsKotlinProject)
        {
            props["manifest_type"] = "library";
            props["sdk"] = "JVM";
            props["framework"] = "Kotlin";
        }

        return props;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var info = GradleBuildScriptParser.ParseDirectory(projectDirectory);
        var name = info.ArchivesBaseName ?? info.RootProjectName ?? Path.GetFileName(projectDirectory);
        var version = info.Version ?? "1.0.0";
        var pkgName = !string.IsNullOrEmpty(info.Group) ? $"{info.Group}:{name}" : name;

        return Task.FromResult<ProducedPackageInfo?>(new ProducedPackageInfo(pkgName, version, "gradle"));
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjects = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var buildGradle = Path.Combine(projectDirectory, "build.gradle");
        var buildGradleKts = Path.Combine(projectDirectory, "build.gradle.kts");
        var gradleFile = File.Exists(buildGradleKts) ? buildGradleKts : (File.Exists(buildGradle) ? buildGradle : null);

        if (gradleFile != null)
        {
            try
            {
                var content = File.ReadAllText(gradleFile);

                // Project dependencies: project(":subproject") or project(':subproject')
                var projectDepMatches = Regex.Matches(content, @"project\s*\(\s*['""]:?([a-zA-Z0-9_\-.:]+)['""]\s*\)");
                foreach (Match match in projectDepMatches)
                {
                    var subName = match.Groups[1].Value.TrimStart(':');
                    var subDir = Path.GetFullPath(Path.Combine(projectDirectory, subName)).Replace('\\', '/');
                    if (Directory.Exists(subDir))
                    {
                        localProjects.Add(subDir);
                    }
                }

                // External dependencies:
                // implementation("group:artifact:version") or implementation 'group:artifact:version' or coreLibraryDesugaring("...")
                var depMatches = Regex.Matches(content, @"(?:implementation|api|compileOnly|runtimeOnly|testImplementation|coreLibraryDesugaring)\s*[\('""\s]+([a-zA-Z0-9_.\-]+):([a-zA-Z0-9_.\-]+)(?::([a-zA-Z0-9_.\-]+))?['""\)]");
                foreach (Match match in depMatches)
                {
                    var g = match.Groups[1].Value;
                    var a = match.Groups[2].Value;
                    var v = match.Groups[3].Success ? match.Groups[3].Value : "unknown";
                    externalPackages.Add(new ProducedPackageInfo($"{g}:{a}", v, "gradle"));
                }
            }
            catch { }
        }

        return Task.FromResult(new ProjectDependencyInfo(
            [.. localProjects.Distinct(StringComparer.OrdinalIgnoreCase)],
            externalPackages
        ));
    }

    private static bool HasKotlinFiles(string directoryPath)
    {
        try
        {
            return Directory.EnumerateFiles(directoryPath, "*.kt", SearchOption.AllDirectories).Any();
        }
        catch
        {
            return false;
        }
    }
}
