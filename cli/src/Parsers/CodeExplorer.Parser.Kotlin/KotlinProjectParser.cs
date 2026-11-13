using System.Text.RegularExpressions;
using CodeExplorer.Parser.Android;
using CodeExplorer.Parser.Shared.Configuration;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Kotlin;

[ProjectParser(Tier = ParserTier.Primary)]
public class KotlinProjectParser : IProjectParser
{
    public string ProjectType => "kotlin";

    public IReadOnlyCollection<string> ExcludedFolders =>
    [
        "build", ".gradle", "bin", "out", ".idea"
    ];

    public IReadOnlyCollection<string> ManifestFileNames =>
    [
        "build.gradle.kts", "build.gradle", "settings.gradle.kts", "settings.gradle", "pom.xml", "AndroidManifest.xml"
    ];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return (lower.EndsWith(".properties") || lower.EndsWith(".yml") || lower.EndsWith(".yaml") || lower.EndsWith(".json")) &&
               !lower.StartsWith("gradle") && !lower.StartsWith(".travis");
    }

    public IReadOnlyList<ILibraryConfigurationDescriptor> ConfigurationDescriptors =>
    [
        new SpringFrameworkConfigurationDescriptor()
    ];

    public IReadOnlyList<PackageDescriptor> Packages => KotlinFileParser.DefaultPackages;

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
                var content = PhysicalFileStore.ReadAllText(pomFile);
                if (content.Contains("kotlin-maven-plugin", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("kotlin-stdlib", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
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
            props["sdk"] = "JVM";
            props["framework"] = "Kotlin";

            var isApp = info.IsApplication;
            var isSpring = false;
            var isKtor = false;
            var buildGradle = Path.Combine(directoryPath, "build.gradle");
            var buildGradleKts = Path.Combine(directoryPath, "build.gradle.kts");
            var gradleFile = File.Exists(buildGradleKts) ? buildGradleKts : (File.Exists(buildGradle) ? buildGradle : null);
            if (gradleFile != null)
            {
                try
                {
                    var content = PhysicalFileStore.ReadAllText(gradleFile);
                    if (content.Contains("org.springframework.boot", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("spring-boot", StringComparison.OrdinalIgnoreCase))
                    {
                        isSpring = true;
                        isApp = true;
                    }
                    if (content.Contains("io.ktor", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("ktor-server", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("plugins.ktor", StringComparison.OrdinalIgnoreCase))
                    {
                        isKtor = true;
                        isApp = true;
                    }
                    if (content.Contains("io.grpc", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("grpc-netty", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("grpc-protobuf", StringComparison.OrdinalIgnoreCase))
                    {
                        props["has_grpc"] = "true";
                        isApp = true;
                    }
                }
                catch
                {
                    // ignored: non-critical pom.xml read failure for spring detection
                }
            }

            var pomFile = Path.Combine(directoryPath, "pom.xml");
            if (File.Exists(pomFile))
            {
                try
                {
                    var content = PhysicalFileStore.ReadAllText(pomFile);
                    if (content.Contains("spring-boot", StringComparison.OrdinalIgnoreCase) ||
                        content.Contains("org.springframework.boot", StringComparison.OrdinalIgnoreCase))
                    {
                        isSpring = true;
                        isApp = true;
                    }
                }
                catch
                {
                    // ignored: non-critical pom.xml read failure for spring detection
                }
            }

            if (isSpring)
            {
                props["framework"] = "Spring Boot";
                props["manifest_type"] = "application";
                props["framework_type"] = "web";
                props["role"] = "Service";
                props["primary_role"] = "Service";
                props["is_library"] = "false";
            }
            else if (isKtor)
            {
                props["framework"] = "Ktor";
                props["manifest_type"] = "application";
                props["framework_type"] = "web";
                props["role"] = "Service";
                props["primary_role"] = "Service";
                props["is_library"] = "false";
            }
            else if (isApp)
            {
                props["manifest_type"] = "application";
                props["framework_type"] = "web";
            }
            else
            {
                props["manifest_type"] = "library";
            }
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
                var content = PhysicalFileStore.ReadAllText(gradleFile);

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

                // Type-safe project accessors: implementation(projects.core)
                var typeSafeProjMatches = Regex.Matches(content, @"\bprojects\.([a-zA-Z0-9_]+)\b");
                foreach (Match match in typeSafeProjMatches)
                {
                    var subName = match.Groups[1].Value;
                    var subDir = Path.GetFullPath(Path.Combine(projectDirectory, subName)).Replace('\\', '/');
                    if (!Directory.Exists(subDir))
                    {
                        subDir = Path.GetFullPath(Path.Combine(projectDirectory, "..", subName)).Replace('\\', '/');
                    }
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

                // Gradle Version Catalog: libs.xyz
                var catalog = FindAndParseVersionCatalog(projectDirectory);
                if (catalog != null && catalog.Count > 0)
                {
                    var libRefMatches = Regex.Matches(content, @"\blibs\.([a-zA-Z0-9_.]+)");
                    foreach (Match match in libRefMatches)
                    {
                        var libKey = match.Groups[1].Value;
                        if (catalog.TryGetValue(libKey, out var pkg))
                        {
                            externalPackages.Add(pkg);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static Dictionary<string, ProducedPackageInfo>? FindAndParseVersionCatalog(string projectDirectory)
    {
        var dir = new DirectoryInfo(projectDirectory);
        while (dir != null)
        {
            var tomlFile = Path.Combine(dir.FullName, "gradle", "libs.versions.toml");
            if (File.Exists(tomlFile))
            {
                return ParseVersionCatalog(tomlFile);
            }
            dir = dir.Parent;
        }
        return null;
    }

    private static Dictionary<string, ProducedPackageInfo> ParseVersionCatalog(string tomlPath)
    {
        var result = new Dictionary<string, ProducedPackageInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var lines = PhysicalFileStore.ReadAllText(tomlPath).Split('\n');
            var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var section = "";

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line.Trim('[', ']').Trim().ToLowerInvariant();
                    continue;
                }

                if (section == "versions")
                {
                    var eqIdx = line.IndexOf('=');
                    if (eqIdx > 0)
                    {
                        var key = line[..eqIdx].Trim();
                        var val = line[(eqIdx + 1)..].Trim().Trim('"', '\'');
                        versions[key] = val;
                        versions[key.Replace('-', '.')] = val;
                        versions[key.Replace('_', '.')] = val;
                    }
                }
                else if (section == "libraries")
                {
                    var eqIdx = line.IndexOf('=');
                    if (eqIdx > 0)
                    {
                        var alias = line[..eqIdx].Trim();
                        var val = line[(eqIdx + 1)..].Trim();

                        string? group = null;
                        string? artifact = null;
                        string? ver = null;

                        var modMatch = Regex.Match(val, @"module\s*=\s*['""]([^'""]+)['""]");
                        if (modMatch.Success)
                        {
                            var parts = modMatch.Groups[1].Value.Split(':');
                            if (parts.Length >= 2)
                            {
                                group = parts[0];
                                artifact = parts[1];
                                if (parts.Length > 2) ver = parts[2];
                            }
                        }
                        else
                        {
                            var gMatch = Regex.Match(val, @"group\s*=\s*['""]([^'""]+)['""]");
                            var nMatch = Regex.Match(val, @"name\s*=\s*['""]([^'""]+)['""]");
                            if (gMatch.Success && nMatch.Success)
                            {
                                group = gMatch.Groups[1].Value;
                                artifact = nMatch.Groups[1].Value;
                            }
                            else
                            {
                                var strMatch = Regex.Match(val, @"^['""]([^'""]+)['""]");
                                if (strMatch.Success)
                                {
                                    var parts = strMatch.Groups[1].Value.Split(':');
                                    if (parts.Length >= 2)
                                    {
                                        group = parts[0];
                                        artifact = parts[1];
                                        if (parts.Length > 2) ver = parts[2];
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(ver))
                        {
                            var verRefMatch = Regex.Match(val, @"version\.ref\s*=\s*['""]([^'""]+)['""]");
                            if (verRefMatch.Success && versions.TryGetValue(verRefMatch.Groups[1].Value, out var resolvedVer))
                            {
                                ver = resolvedVer;
                            }
                            else
                            {
                                var verMatch = Regex.Match(val, @"version\s*=\s*['""]([^'""]+)['""]");
                                if (verMatch.Success)
                                {
                                    ver = verMatch.Groups[1].Value;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(group) && !string.IsNullOrEmpty(artifact))
                        {
                            const string unknownVersion = "unknown";
                            const string gradleEcosystem = "gradle";
                            var pkgInfo = new ProducedPackageInfo($"{group}:{artifact}", ver ?? unknownVersion, gradleEcosystem);
                            result[alias] = pkgInfo;
                            result[alias.Replace('-', '.')] = pkgInfo;
                            result[alias.Replace('_', '.')] = pkgInfo;
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _ = ex;
        }
        return result;
    }
}
