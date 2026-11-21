using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CodeExplorer.Parsers.Java;

/// <summary>
/// Parses multi-module Maven POMs (with parent property interpolation) and Gradle build scripts
/// (including nested :module:sub paths, sibling project() references, and gradle/libs.versions.toml).
/// </summary>
internal static partial class JavaBuildManifestHelper
{
    private const string MavenSource = "maven";
    private const string GradleSource = "gradle";
    private const string UnknownVersion = "unknown";
    private const string PomFileName = "pom.xml";
    private const string SettingsGradleName = "settings.gradle";
    private const string SettingsGradleKtsName = "settings.gradle.kts";
    private const string GradleDirName = "gradle";
    private const string VersionCatalogFileName = "libs.versions.toml";

    private const string TagModules = "modules";
    private const string TagModule = "module";
    private const string TagDependencies = "dependencies";
    private const string TagDependency = "dependency";
    private const string TagProperties = "properties";
    private const string TagParent = "parent";
    private const string TagRelativePath = "relativePath";
    private const string TagGroupId = "groupId";
    private const string TagArtifactId = "artifactId";
    private const string TagVersion = "version";

    private const string PropProjectGroupId = "project.groupId";
    private const string PropProjectVersion = "project.version";
    private const string PropParentGroupId = "project.parent.groupId";
    private const string PropParentVersion = "project.parent.version";
    private const string DefaultParentRelPath = "../pom.xml";

    private const string SectionVersions = "[versions]";
    private const string SectionLibraries = "[libraries]";
    private const char ColonChar = ':';
    private const char SlashChar = '/';
    private const char BackslashChar = '\\';
    private const char DotChar = '.';
    private const char DashChar = '-';
    private const char UnderscoreChar = '_';

    [GeneratedRegex(@"\$\{([^}]+)\}", RegexOptions.Compiled)]
    private static partial Regex MavenPropertyRegex();

    [GeneratedRegex(@"include\s*\(?([^\r\n\)]+)\)?", RegexOptions.Compiled)]
    private static partial Regex GradleIncludeLineRegex();

    [GeneratedRegex(@"['""](:?[a-zA-Z0-9_.:-]+)['""]", RegexOptions.Compiled)]
    private static partial Regex QuotedGradlePathRegex();

    [GeneratedRegex(@"project\s*\(\s*['""](:?[a-zA-Z0-9_.:-]+)['""]\s*\)", RegexOptions.Compiled)]
    private static partial Regex GradleProjectDepRegex();

    [GeneratedRegex(@"(?:implementation|api|compileOnly|runtimeOnly|testImplementation)\s*[\('""\s]+([a-zA-Z0-9_.-]+):([a-zA-Z0-9_.-]+)(?::([a-zA-Z0-9_.-]+))?['""\)]", RegexOptions.Compiled)]
    private static partial Regex GradleExternalDepRegex();

    [GeneratedRegex(@"(?:implementation|api|compileOnly|runtimeOnly|testImplementation)\s*\(\s*libs\.([a-zA-Z0-9_.]+)\s*\)", RegexOptions.Compiled)]
    private static partial Regex GradleCatalogDepRegex();

    [GeneratedRegex(@"^([a-zA-Z0-9_.-]+)\s*=\s*(.+)$", RegexOptions.Compiled)]
    private static partial Regex TomlKeyValueRegex();

    [GeneratedRegex(@"module\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled)]
    private static partial Regex TomlModuleRegex();

    [GeneratedRegex(@"group\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled)]
    private static partial Regex TomlGroupRegex();

    [GeneratedRegex(@"name\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled)]
    private static partial Regex TomlNameRegex();

    [GeneratedRegex(@"version\.ref\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled)]
    private static partial Regex TomlVersionRefRegex();

    [GeneratedRegex(@"(?<!\.)version\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled)]
    private static partial Regex TomlVersionLiteralRegex();

    internal static void ParseMavenPomDependencies(
        string projectDirectory,
        XElement root,
        List<string> localProjects,
        List<ProducedPackageInfo> externalPackages)
    {
        var ns = root.GetDefaultNamespace();
        var props = BuildMavenProperties(projectDirectory, root, ns);

        var modulesElem = root.Element(ns + TagModules);
        if (modulesElem != null)
        {
            foreach (var moduleElem in modulesElem.Elements(ns + TagModule))
            {
                var moduleRelPath = moduleElem.Value?.Trim();
                if (!string.IsNullOrEmpty(moduleRelPath))
                {
                    var fullModuleDir = Path.GetFullPath(Path.Combine(projectDirectory, moduleRelPath)).Replace(BackslashChar, SlashChar);
                    localProjects.Add(fullModuleDir);
                }
            }
        }

        var depsElem = root.Element(ns + TagDependencies);
        if (depsElem == null) return;

        foreach (var dep in depsElem.Elements(ns + TagDependency))
        {
            var g = InterpolateMavenProps(dep.Element(ns + TagGroupId)?.Value?.Trim(), props);
            var a = InterpolateMavenProps(dep.Element(ns + TagArtifactId)?.Value?.Trim(), props);
            var v = InterpolateMavenProps(dep.Element(ns + TagVersion)?.Value?.Trim(), props) ?? UnknownVersion;

            if (!string.IsNullOrEmpty(g) && !string.IsNullOrEmpty(a))
            {
                externalPackages.Add(new ProducedPackageInfo($"{g}:{a}", v, MavenSource));
            }
        }
    }

    private static Dictionary<string, string> BuildMavenProperties(string projectDirectory, XElement root, XNamespace ns)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Inherit parent POM properties first so child POM properties can override them
        var parentElem = root.Element(ns + TagParent);
        if (parentElem != null)
        {
            var relPath = parentElem.Element(ns + TagRelativePath)?.Value?.Trim();
            if (string.IsNullOrEmpty(relPath)) relPath = DefaultParentRelPath;
            LoadParentPomProperties(projectDirectory, relPath, props);

            var parentGroup = parentElem.Element(ns + TagGroupId)?.Value?.Trim();
            var parentVer = parentElem.Element(ns + TagVersion)?.Value?.Trim();
            if (!string.IsNullOrEmpty(parentGroup)) props[PropParentGroupId] = parentGroup;
            if (!string.IsNullOrEmpty(parentVer)) props[PropParentVersion] = parentVer;
        }

        var propsElem = root.Element(ns + TagProperties);
        if (propsElem != null)
        {
            foreach (var p in propsElem.Elements())
            {
                var val = p.Value?.Trim();
                if (!string.IsNullOrEmpty(val))
                {
                    props[p.Name.LocalName] = val;
                }
            }
        }

        var ownGroup = root.Element(ns + TagGroupId)?.Value?.Trim()
                       ?? (props.TryGetValue(PropParentGroupId, out var pg) ? pg : null);
        var ownVer = root.Element(ns + TagVersion)?.Value?.Trim()
                     ?? (props.TryGetValue(PropParentVersion, out var pv) ? pv : null);

        if (!string.IsNullOrEmpty(ownGroup)) props[PropProjectGroupId] = ownGroup;
        if (!string.IsNullOrEmpty(ownVer)) props[PropProjectVersion] = ownVer;

        return props;
    }

    private static void LoadParentPomProperties(string projectDirectory, string relPath, Dictionary<string, string> props)
    {
        try
        {
            var candidate = Path.GetFullPath(Path.Combine(projectDirectory, relPath));
            if (Directory.Exists(candidate)) candidate = Path.Combine(candidate, PomFileName);
            if (!File.Exists(candidate)) return;

            var parentDoc = XDocument.Load(candidate);
            var parentRoot = parentDoc.Root;
            if (parentRoot == null) return;

            var pns = parentRoot.GetDefaultNamespace();
            var parentProps = parentRoot.Element(pns + TagProperties);
            if (parentProps == null) return;

            foreach (var p in parentProps.Elements())
            {
                var val = p.Value?.Trim();
                if (!string.IsNullOrEmpty(val))
                {
                    props[p.Name.LocalName] = val;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            // Ignore unreadable or malformed parent POM; child POM parsing continues
        }
    }

    private static string? InterpolateMavenProps(string? value, Dictionary<string, string> props)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains('$')) return value;

        return MavenPropertyRegex().Replace(value, match =>
        {
            var key = match.Groups[1].Value.Trim();
            return props.TryGetValue(key, out var resolved) ? resolved : match.Value;
        });
    }

    internal static void ParseGradleSettingsIncludes(string projectDirectory, string settingsContent, List<string> localProjects)
    {
        foreach (Match lineMatch in GradleIncludeLineRegex().Matches(settingsContent))
        {
            var argsText = lineMatch.Groups[1].Value;
            foreach (Match itemMatch in QuotedGradlePathRegex().Matches(argsText))
            {
                var subDir = ResolveGradleProjectDir(projectDirectory, itemMatch.Groups[1].Value);
                if (subDir != null)
                {
                    localProjects.Add(subDir);
                }
            }
        }
    }

    internal static void ParseGradleBuildDependencies(
        string projectDirectory,
        string buildContent,
        List<string> localProjects,
        List<ProducedPackageInfo> externalPackages)
    {
        var gradleRoot = FindGradleRootDirectory(projectDirectory);

        foreach (Match match in GradleProjectDepRegex().Matches(buildContent))
        {
            var rawPath = match.Groups[1].Value;
            var resolved = ResolveGradleProjectDir(projectDirectory, rawPath)
                           ?? (gradleRoot != null ? ResolveGradleProjectDir(gradleRoot, rawPath) : null);
            if (resolved != null)
            {
                localProjects.Add(resolved);
            }
        }

        foreach (Match match in GradleExternalDepRegex().Matches(buildContent))
        {
            var g = match.Groups[1].Value;
            var a = match.Groups[2].Value;
            var v = match.Groups[3].Success ? match.Groups[3].Value : UnknownVersion;
            externalPackages.Add(new ProducedPackageInfo($"{g}:{a}", v, GradleSource));
        }

        var catalogMatches = GradleCatalogDepRegex().Matches(buildContent);
        if (catalogMatches.Count == 0) return;

        var catalog = LoadVersionCatalog(gradleRoot ?? projectDirectory);
        if (catalog.Count == 0) return;

        foreach (Match match in catalogMatches)
        {
            var normalizedKey = NormalizeCatalogKey(match.Groups[1].Value);
            if (catalog.TryGetValue(normalizedKey, out var pkg))
            {
                externalPackages.Add(pkg);
            }
        }
    }

    private static string? ResolveGradleProjectDir(string baseDir, string gradleProjectPath)
    {
        var trimmed = gradleProjectPath.Trim().TrimStart(ColonChar);
        if (string.IsNullOrEmpty(trimmed)) return null;

        var relPath = trimmed.Replace(ColonChar, Path.DirectorySeparatorChar);
        var fullDir = Path.GetFullPath(Path.Combine(baseDir, relPath)).Replace(BackslashChar, SlashChar);
        return Directory.Exists(fullDir) ? fullDir : null;
    }

    private static string? FindGradleRootDirectory(string startDir)
    {
        var current = startDir;
        for (var depth = 0; depth < 6 && !string.IsNullOrEmpty(current); depth++)
        {
            if (File.Exists(Path.Combine(current, SettingsGradleName)) ||
                File.Exists(Path.Combine(current, SettingsGradleKtsName)) ||
                File.Exists(Path.Combine(current, GradleDirName, VersionCatalogFileName)))
            {
                return current;
            }
            current = Path.GetDirectoryName(current);
        }
        return null;
    }

    private static Dictionary<string, ProducedPackageInfo> LoadVersionCatalog(string rootDir)
    {
        var result = new Dictionary<string, ProducedPackageInfo>(StringComparer.OrdinalIgnoreCase);
        var catalogPath = Path.Combine(rootDir, GradleDirName, VersionCatalogFileName);
        if (!File.Exists(catalogPath)) return result;

        try
        {
            var lines = PhysicalFileStore.ReadAllText(catalogPath).Split('\n');
            var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var section = string.Empty;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;
                if (line.StartsWith('['))
                {
                    section = line;
                    continue;
                }

                var kv = TomlKeyValueRegex().Match(line);
                if (!kv.Success) continue;

                var key = kv.Groups[1].Value.Trim();
                var val = kv.Groups[2].Value.Trim();

                if (section.Equals(SectionVersions, StringComparison.OrdinalIgnoreCase))
                {
                    versions[key] = val.Trim('"', '\'');
                }
                else if (section.Equals(SectionLibraries, StringComparison.OrdinalIgnoreCase))
                {
                    var pkg = ParseCatalogLibraryEntry(val, versions);
                    if (pkg != null)
                    {
                        result[NormalizeCatalogKey(key)] = pkg;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore unreadable version catalog; other Gradle dependencies still resolve
        }

        return result;
    }

    private static ProducedPackageInfo? ParseCatalogLibraryEntry(string val, Dictionary<string, string> versions)
    {
        if (val.StartsWith('"') || val.StartsWith('\''))
        {
            var parts = val.Trim('"', '\'').Split(ColonChar);
            if (parts.Length >= 2)
            {
                var v = parts.Length >= 3 ? parts[2] : UnknownVersion;
                return new ProducedPackageInfo($"{parts[0]}:{parts[1]}", v, GradleSource);
            }
            return null;
        }

        string? coords = null;
        var moduleMatch = TomlModuleRegex().Match(val);
        if (moduleMatch.Success)
        {
            coords = moduleMatch.Groups[1].Value;
        }
        else
        {
            var groupMatch = TomlGroupRegex().Match(val);
            var nameMatch = TomlNameRegex().Match(val);
            if (groupMatch.Success && nameMatch.Success)
            {
                coords = $"{groupMatch.Groups[1].Value}:{nameMatch.Groups[1].Value}";
            }
        }

        if (string.IsNullOrEmpty(coords)) return null;

        var version = UnknownVersion;
        var verRefMatch = TomlVersionRefRegex().Match(val);
        if (verRefMatch.Success && versions.TryGetValue(verRefMatch.Groups[1].Value, out var refVer))
        {
            version = refVer;
        }
        else
        {
            var verLitMatch = TomlVersionLiteralRegex().Match(val);
            if (verLitMatch.Success)
            {
                version = verLitMatch.Groups[1].Value;
            }
        }

        return new ProducedPackageInfo(coords, version, GradleSource);
    }

    private static string NormalizeCatalogKey(string key) =>
        key.Replace(DashChar, DotChar).Replace(UnderscoreChar, DotChar).ToLowerInvariant();
}
