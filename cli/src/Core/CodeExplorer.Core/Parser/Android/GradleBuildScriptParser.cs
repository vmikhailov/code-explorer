using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser.Android;

public record GradleBuildInfo(
    string? RootProjectName,
    IReadOnlyList<string> IncludedSubprojects,
    bool HasAppliedProjectPlugins,
    bool IsAndroidApplication,
    bool IsAndroidLibrary,
    bool IsKotlinProject,
    bool IsFlutterProject,
    string? Namespace,
    string? ApplicationId,
    string? MinSdk,
    string? TargetSdk,
    string? CompileSdk,
    string? Group,
    string? Version,
    string? ArchivesBaseName
);

public static class GradleBuildScriptParser
{
    public static GradleBuildInfo ParseDirectory(string directoryPath)
    {
        var settingsFile = FindSettingsGradle(directoryPath);
        var buildFile = FindBuildGradle(directoryPath);

        string? rootProjectName = null;
        var subprojects = new List<string>();

        if (settingsFile != null && File.Exists(settingsFile))
        {
            try
            {
                var settingsText = File.ReadAllText(settingsFile);
                var rootMatch = Regex.Match(settingsText, @"rootProject\.name\s*=\s*['""]([^'""]+)['""]");
                if (rootMatch.Success)
                {
                    rootProjectName = rootMatch.Groups[1].Value.Trim();
                }

                // Match include(":app", ":core") or include ':app', ':core'
                var includeMatches = Regex.Matches(settingsText, @"include\s*(?:\(([^)]+)\)|([^;\r\n]+))");
                foreach (Match m in includeMatches)
                {
                    var clause = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
                    var itemMatches = Regex.Matches(clause, @"['""]:?([a-zA-Z0-9_\-\.:]+)['""]");
                    foreach (Match im in itemMatches)
                    {
                        var raw = im.Groups[1].Value.Trim().TrimStart(':');
                        if (!string.IsNullOrEmpty(raw))
                        {
                            subprojects.Add(raw);
                        }
                    }
                }
            }
            catch { }
        }

        var hasAppliedPlugins = false;
        var isAndroidApp = false;
        var isAndroidLib = false;
        var isKotlin = false;
        var isFlutter = false;
        string? ns = null;
        string? appId = null;
        string? minSdk = null;
        string? targetSdk = null;
        string? compileSdk = null;
        string? group = null;
        string? version = null;
        string? archivesBaseName = null;

        if (buildFile != null && File.Exists(buildFile))
        {
            try
            {
                var buildText = File.ReadAllText(buildFile);

                // Check for plugins
                var pluginMatches = Regex.Matches(buildText, @"(?:id|apply\s+plugin:)\s*[\(""'\s]*([a-zA-Z0-9_.\-]+)[\)""'\s]*(?:\s+apply\s+(true|false))?");
                foreach (Match m in pluginMatches)
                {
                    var pluginId = m.Groups[1].Value.Trim();
                    var applyVal = m.Groups[2].Success ? m.Groups[2].Value.Trim().ToLowerInvariant() : "true";
                    if (applyVal == "false") continue; // plugin declared for subprojects, not applied here

                    if (pluginId.Contains("com.android.application", StringComparison.OrdinalIgnoreCase))
                    {
                        isAndroidApp = true;
                        hasAppliedPlugins = true;
                    }
                    else if (pluginId.Contains("com.android.library", StringComparison.OrdinalIgnoreCase))
                    {
                        isAndroidLib = true;
                        hasAppliedPlugins = true;
                    }

                    if (pluginId.Contains("kotlin", StringComparison.OrdinalIgnoreCase))
                    {
                        isKotlin = true;
                        hasAppliedPlugins = true;
                    }

                    if (pluginId.Contains("dev.flutter", StringComparison.OrdinalIgnoreCase))
                    {
                        isFlutter = true;
                        hasAppliedPlugins = true;
                    }

                    if (pluginId is "java" or "application" or "java-library" or "war" ||
                        pluginId.Contains("org.springframework.boot", StringComparison.OrdinalIgnoreCase) ||
                        pluginId.Contains("io.quarkus", StringComparison.OrdinalIgnoreCase) ||
                        pluginId.Contains("io.micronaut", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAppliedPlugins = true;
                    }
                }

                // Check for kotlin block or kotlin() shorthand
                if (buildText.Contains("kotlin(\"jvm\")", StringComparison.OrdinalIgnoreCase) ||
                    buildText.Contains("kotlin(\"android\")", StringComparison.OrdinalIgnoreCase) ||
                    buildText.Contains("kotlin(\"multiplatform\")", StringComparison.OrdinalIgnoreCase))
                {
                    isKotlin = true;
                    hasAppliedPlugins = true;
                }

                // Fallback plugin detection if syntax is unquoted or alternate
                if (!hasAppliedPlugins)
                {
                    if (buildText.Contains("apply plugin: 'com.android.application'") || buildText.Contains("apply plugin: \"com.android.application\""))
                    {
                        isAndroidApp = true;
                        hasAppliedPlugins = true;
                    }
                    if (buildText.Contains("apply plugin: 'kotlin'") || buildText.Contains("apply plugin: 'kotlin-android'"))
                    {
                        isKotlin = true;
                        hasAppliedPlugins = true;
                    }
                    if (buildText.Contains("apply plugin: 'java'"))
                    {
                        hasAppliedPlugins = true;
                    }
                }

                // Android properties
                var nsMatch = Regex.Match(buildText, @"namespace\s*=\s*['""]([^'""]+)['""]");
                if (nsMatch.Success) ns = nsMatch.Groups[1].Value.Trim();

                var appMatch = Regex.Match(buildText, @"applicationId\s*=\s*['""]([^'""]+)['""]");
                if (appMatch.Success) appId = appMatch.Groups[1].Value.Trim();

                var minMatch = Regex.Match(buildText, @"minSdk(?:Version)?\s*=?\s*([0-9]+)");
                if (minMatch.Success) minSdk = minMatch.Groups[1].Value.Trim();

                var targetMatch = Regex.Match(buildText, @"targetSdk(?:Version)?\s*=?\s*([0-9]+)");
                if (targetMatch.Success) targetSdk = targetMatch.Groups[1].Value.Trim();

                var compileMatch = Regex.Match(buildText, @"compileSdk(?:Version)?\s*=?\s*([0-9]+)");
                if (compileMatch.Success) compileSdk = compileMatch.Groups[1].Value.Trim();

                var grpMatch = Regex.Match(buildText, @"group\s*=\s*['""]([^'""]+)['""]");
                if (grpMatch.Success) group = grpMatch.Groups[1].Value.Trim();

                var verMatch = Regex.Match(buildText, @"version\s*=\s*['""]([^'""]+)['""]");
                if (verMatch.Success) version = verMatch.Groups[1].Value.Trim();

                var baseMatch = Regex.Match(buildText, @"(?:archivesBaseName|base\.archivesName)\s*=\s*['""]([^'""]+)['""]");
                if (baseMatch.Success) archivesBaseName = baseMatch.Groups[1].Value.Trim();
            }
            catch { }
        }

        return new GradleBuildInfo(
            RootProjectName: rootProjectName,
            IncludedSubprojects: subprojects,
            HasAppliedProjectPlugins: hasAppliedPlugins,
            IsAndroidApplication: isAndroidApp,
            IsAndroidLibrary: isAndroidLib,
            IsKotlinProject: isKotlin,
            IsFlutterProject: isFlutter,
            Namespace: ns,
            ApplicationId: appId,
            MinSdk: minSdk,
            TargetSdk: targetSdk,
            CompileSdk: compileSdk,
            Group: group,
            Version: version,
            ArchivesBaseName: archivesBaseName
        );
    }

    public static bool IsMultiProjectContainer(string directoryPath)
    {
        var settingsFile = FindSettingsGradle(directoryPath);
        if (settingsFile == null) return false;

        var info = ParseDirectory(directoryPath);

        // If settings.gradle has declared subprojects and the root itself does not apply any project plugins,
        // it is purely a multi-project container / solution root (e.g. flutter/android), not a leaf project.
        return info.IncludedSubprojects.Count > 0 && !info.HasAppliedProjectPlugins;
    }

    public static string? FindSettingsGradle(string directoryPath)
    {
        var f1 = Path.Combine(directoryPath, "settings.gradle.kts");
        if (File.Exists(f1)) return f1;
        var f2 = Path.Combine(directoryPath, "settings.gradle");
        if (File.Exists(f2)) return f2;
        return null;
    }

    public static string? FindBuildGradle(string directoryPath)
    {
        var f1 = Path.Combine(directoryPath, "build.gradle.kts");
        if (File.Exists(f1)) return f1;
        var f2 = Path.Combine(directoryPath, "build.gradle");
        if (File.Exists(f2)) return f2;
        return null;
    }

    public static string? FindAndroidManifest(string directoryPath)
    {
        var candidates = new[]
        {
            Path.Combine(directoryPath, "src", "main", "AndroidManifest.xml"),
            Path.Combine(directoryPath, "AndroidManifest.xml"),
            Path.Combine(directoryPath, "app", "src", "main", "AndroidManifest.xml")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
