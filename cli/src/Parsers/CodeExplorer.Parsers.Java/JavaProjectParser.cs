using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using CodeExplorer.Parsers.Android;

[assembly: ParserAssembly]

namespace CodeExplorer.Parsers.Java;

[ProjectParser(Tier = ParserTier.Primary)]
public class JavaProjectParser : IProjectParser
{
    static JavaProjectParser()
    {
        LibraryConfigurationRegistry.Register(new Libraries.SpringFrameworkConfigurationDescriptor());
    }

    private const string PomPackagingPattern = @"<packaging>\s*pom\s*</packaging>";
    private const string WarPackagingPattern = @"<packaging>\s*war\s*</packaging>";
    private const string GradleExecutablePluginPattern =
        @"(?:id\s*\(?\s*['""](?:org\.springframework\.boot|io\.quarkus|io\.micronaut\.application|war|com\.google\.cloud\.tools\.jib|com\.bmuschko\.docker-spring-boot-application)['""]|apply\s*\(?\s*plugin\s*[:=]\s*['""](?:org\.springframework\.boot|io\.quarkus|io\.micronaut\.application|war)['""]|alias\s*\(\s*libs\.plugins\.[a-zA-Z0-9_.]*(?:spring[._-]?boot|quarkus|micronaut[._-]?application)|(?<!\.)\bbootJar\b|\bspringBoot\s*\{)";
    private const string GradleJavaLibraryPluginPattern =
        @"(?:id\s*\(?\s*['""](?:java-library|java-test-fixtures)['""]|apply\s*\(?\s*plugin\s*[:=]\s*['""]java-library['""]|alias\s*\(\s*libs\.plugins\.[a-zA-Z0-9_.]*java[._-]?library)";
    private const string GradleBootJarDisabledPattern =
        @"(?:bootJar\s*\{[^}]*\benabled\s*=\s*false|bootJar\.enabled\s*=\s*false|named\s*<\s*BootJar\s*>\s*\([^)]*\)\s*\{[^}]*\benabled\s*=\s*false)";
    private const string MavenExecutablePluginPattern =
        @"(?:<artifactId>\s*(?:spring-boot-maven-plugin|quarkus-maven-plugin|micronaut-maven-plugin|jib-maven-plugin|dockerfile-maven-plugin)\s*</artifactId>|<mainClass>\s*[^<\s]+\s*</mainClass>)";
    private const string MavenBootSkipPattern =
        @"spring-boot-maven-plugin[\s\S]{0,400}?<skip>\s*true\s*</skip>";
    private static readonly TimeSpan ManifestRegexTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly Regex PomPackagingRegex = new(PomPackagingPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, ManifestRegexTimeout);
    private static readonly Regex WarPackagingRegex = new(WarPackagingPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, ManifestRegexTimeout);
    private static readonly Regex GradleExecutablePluginRegex = new(GradleExecutablePluginPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, ManifestRegexTimeout);
    private static readonly Regex GradleJavaLibraryPluginRegex = new(GradleJavaLibraryPluginPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, ManifestRegexTimeout);
    private static readonly Regex GradleBootJarDisabledRegex = new(GradleBootJarDisabledPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, ManifestRegexTimeout);
    private static readonly Regex MavenExecutablePluginRegex = new(MavenExecutablePluginPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, ManifestRegexTimeout);
    private static readonly Regex MavenBootSkipRegex = new(MavenBootSkipPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, ManifestRegexTimeout);

    public string ProjectType => "java";

    public IReadOnlyCollection<string> ExcludedFolders =>
    [
        "target", "build", ".gradle", ".mvn", "bin", "out", ".idea", ".settings", ".metadata"
    ];

    public IReadOnlyCollection<string> ManifestFileNames =>
    [
        "pom.xml", "build.gradle", "build.gradle.kts", "settings.gradle", "settings.gradle.kts", "AndroidManifest.xml"
    ];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return (lower.EndsWith(".properties") || lower.EndsWith(".yml") || lower.EndsWith(".yaml")) &&
               !lower.StartsWith("gradle") && !lower.StartsWith(".travis");
    }

    public IReadOnlyList<ILibraryConfigurationDescriptor> ConfigurationDescriptors { get; } =
    [
        new Libraries.SpringFrameworkConfigurationDescriptor()
    ];

    public IReadOnlyList<PackageDescriptor> Packages => JavaFileParser.DefaultPackages;

    public JavaProjectParser()
    {
        LibraryConfigurationRegistry.Register(new Libraries.SpringFrameworkConfigurationDescriptor());
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        var hasJavaManifest = false;
        var hasGradleScript = false;
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file);
            if (fileName.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("build.gradle", StringComparison.OrdinalIgnoreCase))
            {
                hasGradleScript = true;
                hasJavaManifest = true;
            }
            else if (fileName.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                     fileName.Equals("settings.gradle", StringComparison.OrdinalIgnoreCase) ||
                     fileName.Equals("settings.gradle.kts", StringComparison.OrdinalIgnoreCase))
            {
                hasJavaManifest = true;
            }
        }

        if (!hasJavaManifest)
        {
            return false;
        }

        if (GradleBuildScriptParser.IsMultiProjectContainer(directoryPath))
        {
            return false;
        }

        if (hasGradleScript)
        {
            var info = GradleBuildScriptParser.ParseDirectory(directoryPath);
            if (info.IsKotlinProject || info.IsAndroidApplication || info.IsAndroidLibrary || info.IsFlutterProject)
            {
                return false;
            }
        }

        return true;
    }

    private sealed record JavaDirectoryManifestBundle(
        string? PomText,
        XDocument? PomDoc,
        string? BuildGradleText,
        string? SettingsGradleText);

    private static JavaDirectoryManifestBundle ReadJavaBundleOnce(string directoryPath)
    {
        string? pomText = null;
        XDocument? pomDoc = null;
        var pomPath = Path.Combine(directoryPath, "pom.xml");
        if (WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(pomPath) && File.Exists(pomPath))
        {
            pomText = FileStreamReader.ReadTextOnce(pomPath);
            if (!string.IsNullOrEmpty(pomText))
            {
                try
                {
                    pomDoc = XDocument.Parse(pomText);
                }
                catch (XmlException)
                {
                    // ignored: malformed pom.xml falls back to text heuristics
                }
            }
        }

        string? gradleText = null;
        var buildGradle = Path.Combine(directoryPath, "build.gradle");
        var buildGradleKts = Path.Combine(directoryPath, "build.gradle.kts");
        var gradleFile = WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(buildGradle) && File.Exists(buildGradle)
            ? buildGradle
            : (WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(buildGradleKts) && File.Exists(buildGradleKts) ? buildGradleKts : null);
        if (gradleFile != null)
        {
            gradleText = FileStreamReader.ReadTextOnce(gradleFile);
        }

        string? settingsText = null;
        var settingsGradle = Path.Combine(directoryPath, "settings.gradle");
        var settingsGradleKts = Path.Combine(directoryPath, "settings.gradle.kts");
        var settingsFile = WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(settingsGradle) && File.Exists(settingsGradle)
            ? settingsGradle
            : (WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(settingsGradleKts) && File.Exists(settingsGradleKts) ? settingsGradleKts : null);
        if (settingsFile != null)
        {
            settingsText = FileStreamReader.ReadTextOnce(settingsFile);
        }

        return new JavaDirectoryManifestBundle(pomText, pomDoc, gradleText, settingsText);
    }

    public bool TryParse(ProjectParseContext context, out ProjectParseResult? result)
    {
        var activeIndex = context.DirectoryIndex ?? WorkspaceDirectoryIndex.Current;
        if (activeIndex != null && activeIndex.TryGetCachedProjectParse(this, context.DirectoryPath, out var cached))
        {
            result = cached.Result;
            return cached.Matched;
        }

        var dir = context.DirectoryPath;
        var files = context.FilesInDirectory;
        if (!IsProjectDirectory(dir, files))
        {
            activeIndex?.StoreProjectParse(this, dir, false, null);
            result = null;
            return false;
        }

        var bundle = ReadJavaBundleOnce(dir);
        var name = ExtractProjectNameFromBundle(dir, bundle);
        if (string.IsNullOrEmpty(name)) name = Path.GetFileName(dir.TrimEnd('/', '\\'));
        var props = ExtractManifestPropertiesFromBundle(dir, files, bundle);
        var produced = ExtractProducedPackageFromBundle(dir, bundle);
        var deps = ExtractDependenciesFromBundle(dir, bundle);

        result = new ProjectParseResult(
            Project: new ParsedProjectDescriptor(
                Name: name,
                DirectoryPath: dir,
                ProjectType: ProjectType,
                ManifestProperties: props,
                ProducedPackage: produced,
                Dependencies: deps),
            ClaimMode: ParserClaimMode.AbsorbSecondaryInDirectory);
        activeIndex?.StoreProjectParse(this, dir, true, result);
        return true;
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory) =>
        ExtractProjectNameFromBundle(directoryPath, ReadJavaBundleOnce(directoryPath));

    private static string ExtractProjectNameFromBundle(string directoryPath, JavaDirectoryManifestBundle bundle)
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

        var folderName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));

        if (bundle.PomDoc?.Root is { } root)
        {
            var ns = root.GetDefaultNamespace();
            var artifactId = root.Element(ns + "artifactId")?.Value?.Trim();
            if (!string.IsNullOrEmpty(artifactId))
            {
                return artifactId;
            }
        }

        if (!string.IsNullOrEmpty(bundle.SettingsGradleText))
        {
            var match = Regex.Match(bundle.SettingsGradleText, @"rootProject\.name\s*=\s*['""]([^'""]+)['""]");
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                return match.Groups[1].Value.Trim();
            }
        }

        if (!string.IsNullOrEmpty(bundle.BuildGradleText))
        {
            var baseNameMatch = Regex.Match(bundle.BuildGradleText, @"(?:archivesBaseName|base\.archivesName)\s*=\s*['""]([^'""]+)['""]");
            if (baseNameMatch.Success && !string.IsNullOrWhiteSpace(baseNameMatch.Groups[1].Value))
            {
                return baseNameMatch.Groups[1].Value.Trim();
            }
        }

        if (folderName.Equals("app", StringComparison.OrdinalIgnoreCase) ||
            folderName.Equals("src", StringComparison.OrdinalIgnoreCase) ||
            folderName.Equals("main", StringComparison.OrdinalIgnoreCase) ||
            folderName.Equals("core", StringComparison.OrdinalIgnoreCase) ||
            folderName.Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            var parentDir = Path.GetDirectoryName(directoryPath.TrimEnd('/', '\\'));
            if (!string.IsNullOrEmpty(parentDir))
            {
                var parentName = Path.GetFileName(parentDir);
                if (!string.IsNullOrEmpty(parentName))
                {
                    return $"{parentName}-{folderName}".ToLowerInvariant();
                }
            }
        }

        return folderName;
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory) =>
        ExtractManifestPropertiesFromBundle(directoryPath, filesInDirectory, ReadJavaBundleOnce(directoryPath));

    private static Dictionary<string, string> ExtractManifestPropertiesFromBundle(
        string directoryPath,
        string[] filesInDirectory,
        JavaDirectoryManifestBundle bundle)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (IsJavaTestModuleDirectory(directoryPath))
        {
            props["manifest_type"] = "test";
            props["role"] = "Test";
            props["primary_role"] = "Test";
            props["is_test_project"] = "true";
            props["is_library"] = "false";
            return props;
        }

        var isSubmodule = HasAncestorJavaBuildRoot(directoryPath);
        var isInternalLibPath = IsInternalLibrarySubmodulePath(directoryPath);
        var hasRunnableEntrypoint = HasDockerfileOrRunnableJavaEntrypoint(directoryPath, filesInDirectory);

        if (!string.IsNullOrEmpty(bundle.BuildGradleText))
        {
            try
            {
                var content = bundle.BuildGradleText;

                var hasAppPlugin = content.Contains("id 'application'") ||
                                   content.Contains("id \"application\"") ||
                                   content.Contains("id(\"application\")") ||
                                   content.Contains("apply plugin: 'application'") ||
                                   content.Contains("apply plugin: \"application\"") ||
                                   content.Contains("mainClass", StringComparison.OrdinalIgnoreCase) ||
                                   content.Contains("mainClassName", StringComparison.OrdinalIgnoreCase);

                var hasGrpc = content.Contains("io.grpc") ||
                              content.Contains("grpc-netty") ||
                              content.Contains("grpc-services") ||
                              content.Contains("grpc-protobuf");

                if (hasGrpc) props["has_grpc"] = "true";

                var hasExecutableGradlePlugin = hasAppPlugin || GradleExecutablePluginRegex.IsMatch(content);
                var isJavaLibraryPlugin = GradleJavaLibraryPluginRegex.IsMatch(content);
                var isBootJarDisabled = GradleBootJarDisabledRegex.IsMatch(content);

                if (content.Contains("com.android.application", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("com.android.library", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("dev.flutter.flutter-gradle-plugin", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("dev.flutter.flutter-plugin-loader", StringComparison.OrdinalIgnoreCase))
                {
                    props["manifest_type"] = "mobile";
                    props["framework_type"] = "mobile";
                    props["sdk"] = "Android";
                    props["framework"] = content.Contains("dev.flutter", StringComparison.OrdinalIgnoreCase)
                        ? "Flutter Android"
                        : "Android";
                }
                else if (!isBootJarDisabled &&
                         (hasExecutableGradlePlugin ||
                          hasRunnableEntrypoint ||
                          (!isSubmodule && !isJavaLibraryPlugin && !isInternalLibPath &&
                           (content.Contains("org.springframework.boot", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("spring-boot-starter-web", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("spring-boot-starter-webflux", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("io.quarkus", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("io.micronaut", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("grpc-netty", StringComparison.OrdinalIgnoreCase) ||
                            content.Contains("grpc-services", StringComparison.OrdinalIgnoreCase)))))
                {
                    props["manifest_type"] = "application";
                    props["framework_type"] = "web";
                    props["role"] = "Service";
                    props["primary_role"] = "Service";
                    props["is_library"] = "false";
                }
                else
                {
                    props["manifest_type"] = "library";
                    props["role"] = "Library";
                    props["primary_role"] = "Library";
                    props["is_library"] = "true";
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // ignored: unreadable build manifest falls back to default properties
            }
        }

        const string keyFrameworkType = "framework_type";
        if (!string.IsNullOrEmpty(bundle.SettingsGradleText) && !props.ContainsKey(keyFrameworkType))
        {
            var content = bundle.SettingsGradleText;
            if (content.Contains("dev.flutter.flutter-plugin-loader", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("com.android.application", StringComparison.OrdinalIgnoreCase))
            {
                props["manifest_type"] = "mobile";
                props["framework_type"] = "mobile";
                props["sdk"] = "android";
            }
        }

        if (!string.IsNullOrEmpty(bundle.PomText))
        {
            var content = bundle.PomText;
            var isPomPackaging = PomPackagingRegex.IsMatch(content);
            if (isPomPackaging)
            {
                props["is_orchestrator"] = "true";
                props["is_build_orchestrator"] = "true";
                props["manifest_type"] = "library";
                props["role"] = "Library";
                props["kind"] = "Library";
                props["primary_role"] = "Library";
                props["is_library"] = "true";
            }
            else
            {
                var isMavenBootSkipped = MavenBootSkipRegex.IsMatch(content);
                var hasRunnableMavenSignal = !isMavenBootSkipped &&
                                             (WarPackagingRegex.IsMatch(content) ||
                                              MavenExecutablePluginRegex.IsMatch(content) ||
                                              hasRunnableEntrypoint);
                var hasStandaloneWebFramework = !isSubmodule && !isInternalLibPath &&
                    (content.Contains("spring-boot-starter-web", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("spring-boot-starter-webflux", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("spring-cloud-config-server", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("spring-boot-admin", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("io.quarkus", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("io.micronaut", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("org.eclipse.jetty", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("tomcat-embed", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("io.dropwizard", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("io.helidon", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("io.ktor", StringComparison.OrdinalIgnoreCase));

                if (hasRunnableMavenSignal || hasStandaloneWebFramework)
                {
                    props["manifest_type"] = "application";
                    props["framework_type"] = "web";
                    props["role"] = "Service";
                    props["primary_role"] = "Service";
                    props["is_library"] = "false";
                }
                else
                {
                    props["manifest_type"] = "library";
                    props["role"] = "Library";
                    props["primary_role"] = "Library";
                    props["is_library"] = "true";
                }
            }
        }

        return props;
    }

    private static bool IsJavaTestModuleDirectory(string directoryPath)
    {
        var folder = Path.GetFileName(directoryPath.TrimEnd('/', '\\')).ToLowerInvariant();
        return folder is "unit-test" or "unit-tests" or "integration-test" or "integration-tests"
                   or "functional-test" or "functional-tests" or "test-utils" or "test-support" or "test-fixtures"
               || folder.EndsWith("-unit-test", StringComparison.Ordinal)
               || folder.EndsWith("-integration-test", StringComparison.Ordinal)
               || folder.EndsWith("-test-utils", StringComparison.Ordinal)
               || folder.EndsWith("-test-support", StringComparison.Ordinal)
               || folder.EndsWith("-test-fixtures", StringComparison.Ordinal);
    }

    private static bool IsInternalLibrarySubmodulePath(string directoryPath)
    {
        var norm = directoryPath.Replace('\\', '/').TrimEnd('/');
        var folder = Path.GetFileName(norm).ToLowerInvariant();
        if (norm.Contains("/core/", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("/common/", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("/commons/", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("/libs/", StringComparison.OrdinalIgnoreCase) ||
            norm.Contains("/shared/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return folder is "core" or "common" or "commons" or "shared" or "api" or "client" or "sdk" or "spi" or "dto" or "model" or "models"
               || folder.EndsWith("-api", StringComparison.Ordinal)
               || folder.EndsWith("-client", StringComparison.Ordinal)
               || folder.EndsWith("-common", StringComparison.Ordinal)
               || folder.EndsWith("-commons", StringComparison.Ordinal)
               || folder.EndsWith("-core", StringComparison.Ordinal)
               || folder.EndsWith("-dto", StringComparison.Ordinal)
               || folder.EndsWith("-model", StringComparison.Ordinal)
               || folder.EndsWith("-models", StringComparison.Ordinal)
               || folder.EndsWith("-sdk", StringComparison.Ordinal)
               || folder.EndsWith("-spi", StringComparison.Ordinal)
               || folder.EndsWith("-starter", StringComparison.Ordinal)
               || folder.EndsWith("-autoconfigure", StringComparison.Ordinal);
    }

    private static bool HasAncestorJavaBuildRoot(string directoryPath)
    {
        var useCacheBound = WorkspaceDirectoryIndex.HasCachedDirectoryInCurrent(directoryPath);
        var current = Directory.GetParent(directoryPath);
        var depth = 0;
        while (current != null && depth < 8)
        {
            depth++;
            if (useCacheBound && !WorkspaceDirectoryIndex.HasCachedDirectoryInCurrent(current.FullName))
            {
                break;
            }
            if (WorkspaceDirectoryIndex.IsGitRepoRootInCurrent(directoryPath))
            {
                return false;
            }

            var parentFiles = WorkspaceDirectoryIndex.GetDirectoryFilesOrFallback(current.FullName);
            for (var i = 0; i < parentFiles.Length; i++)
            {
                var fn = Path.GetFileName(parentFiles[i]).ToLowerInvariant();
                if (fn is "settings.gradle" or "settings.gradle.kts" or "pom.xml" or "build.gradle" or "build.gradle.kts")
                {
                    return true;
                }
            }
            if (WorkspaceDirectoryIndex.IsGitRepoRootInCurrent(current.FullName))
            {
                break;
            }
            current = current.Parent;
        }
        return false;
    }

    private static bool HasDockerfileOrRunnableJavaEntrypoint(string directoryPath, string[] filesInDirectory)
    {
        for (var i = 0; i < filesInDirectory.Length; i++)
        {
            var fn = Path.GetFileName(filesInDirectory[i]).ToLowerInvariant();
            if (fn is "dockerfile")
            {
                return true;
            }
        }

        if (!WorkspaceDirectoryIndex.HasCachedDirectoryInCurrent(directoryPath))
        {
            return false;
        }

        foreach (var file in WorkspaceDirectoryIndex.EnumerateSubtreeInCurrent(directoryPath))
        {
            var norm = file.Replace('\\', '/');
            if (norm.Contains("/src/test/", StringComparison.OrdinalIgnoreCase)) continue;
            var ext = Path.GetExtension(norm).ToLowerInvariant();
            if (ext is not (".java" or ".kt" or ".groovy")) continue;

            var src = FileStreamReader.ReadTextOnce(file);
            if (src.Contains("@SpringBootApplication", StringComparison.Ordinal) ||
                src.Contains("@MicronautApplication", StringComparison.Ordinal) ||
                src.Contains("@QuarkusMain", StringComparison.Ordinal) ||
                src.Contains("static void main(", StringComparison.Ordinal) ||
                src.Contains("fun main(", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory) =>
        Task.FromResult(ExtractProducedPackageFromBundle(projectDirectory, ReadJavaBundleOnce(projectDirectory)));

    private static ProducedPackageInfo? ExtractProducedPackageFromBundle(string projectDirectory, JavaDirectoryManifestBundle bundle)
    {
        const string defaultVer = "1.0.0";
        const string ecoMaven = "maven";
        const string ecoGradle = "gradle";
        const string ecoJava = "java";

        if (bundle.PomDoc?.Root is { } root)
        {
            var ns = root.GetDefaultNamespace();
            var artifactId = root.Element(ns + "artifactId")?.Value?.Trim();
            var groupId = root.Element(ns + "groupId")?.Value?.Trim()
                          ?? root.Element(ns + "parent")?.Element(ns + "groupId")?.Value?.Trim();
            var version = root.Element(ns + "version")?.Value?.Trim()
                          ?? root.Element(ns + "parent")?.Element(ns + "version")?.Value?.Trim()
                          ?? defaultVer;

            if (!string.IsNullOrEmpty(artifactId))
            {
                var pkgName = !string.IsNullOrEmpty(groupId) ? $"{groupId}:{artifactId}" : artifactId;
                return new ProducedPackageInfo(pkgName, version, ecoMaven);
            }
        }

        if (!string.IsNullOrEmpty(bundle.BuildGradleText))
        {
            var text = bundle.BuildGradleText;
            var groupMatch = Regex.Match(text, @"group\s*=\s*['""]([^'""]+)['""]");
            var versionMatch = Regex.Match(text, @"version\s*=\s*['""]([^'""]+)['""]");

            var group = groupMatch.Success ? groupMatch.Groups[1].Value : "";
            var version = versionMatch.Success ? versionMatch.Groups[1].Value : defaultVer;
            var name = Path.GetFileName(projectDirectory);

            var pkgName = !string.IsNullOrEmpty(group) ? $"{group}:{name}" : name;
            return new ProducedPackageInfo(pkgName, version, ecoGradle);
        }

        var folderName = Path.GetFileName(projectDirectory);
        return new ProducedPackageInfo(folderName, defaultVer, ecoJava);
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory) =>
        Task.FromResult(ExtractDependenciesFromBundle(projectDirectory, ReadJavaBundleOnce(projectDirectory)));

    private static ProjectDependencyInfo ExtractDependenciesFromBundle(string projectDirectory, JavaDirectoryManifestBundle bundle)
    {
        var localProjects = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        if (bundle.PomDoc?.Root is { } root)
        {
            JavaBuildManifestHelper.ParseMavenPomDependencies(projectDirectory, root, localProjects, externalPackages);
        }

        if (!string.IsNullOrEmpty(bundle.SettingsGradleText))
        {
            JavaBuildManifestHelper.ParseGradleSettingsIncludes(projectDirectory, bundle.SettingsGradleText, localProjects);
        }

        if (!string.IsNullOrEmpty(bundle.BuildGradleText))
        {
            JavaBuildManifestHelper.ParseGradleBuildDependencies(projectDirectory, bundle.BuildGradleText, localProjects, externalPackages);
        }

        return new ProjectDependencyInfo([.. localProjects.Distinct(StringComparer.OrdinalIgnoreCase)], externalPackages);
    }
}
