using System.Text.Json;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.CSharp;

[ProjectParser(Tier = ParserTier.Primary)]
public partial class CSharpProjectParser : IProjectParser
{
    public string ProjectType => "csharp";

    public IReadOnlyCollection<string> ExcludedFolders => ["bin", "obj", ".vs"];
    public IReadOnlyCollection<string> ManifestExtensions => [".csproj", ".fsproj", ".vbproj", ".nuspec"];
    public IReadOnlyCollection<string> ManifestFileNames => ["Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "packages.config"];

    public bool? CheckDirectoryExclusion(string dirName, string? fullPath = null)
    {
        if (dirName.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
            return IsBuildOutputBinDirectory(fullPath);
        }

        if (dirName.Equals("packages", StringComparison.OrdinalIgnoreCase) && fullPath != null)
        {
            if (IsNuGetPackagesDirectory(fullPath))
                return true;
        }

        return null;
    }

    public static bool IsBuildOutputBinDirectory(string? fullPath)
    {
        if (string.IsNullOrEmpty(fullPath)) return true;

        try
        {
            var norm = fullPath.NormalizeSlashes();
            var binIdx = norm.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase);
            var binDir = fullPath;
            if (binIdx >= 0)
            {
                binDir = fullPath[..(binIdx + 4)];
            }
            else if (norm.EndsWith("/bin", StringComparison.OrdinalIgnoreCase))
            {
                binDir = fullPath;
            }

            var parent = Path.GetDirectoryName(binDir);
            if (parent != null)
            {
                var parentName = Path.GetFileName(parent);
                if (parentName.Equals("src", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (Directory.Exists(parent))
                {
                    var hasDotNetManifest = Directory.EnumerateFiles(parent)
                        .Any(f =>
                        {
                            var ext = Path.GetExtension(f);
                            var name = Path.GetFileName(f);
                            return ext.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
                                   ext.Equals(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                                   ext.Equals(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                                   ext.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
                                   ext.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
                                   name.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase);
                        });
                    if (hasDotNetManifest)
                        return true;
                }
            }

            if (Directory.Exists(binDir))
            {
                const string PatternDll = "*.dll";
                const string PatternPdb = "*.pdb";
                const string PatternAny = "*";
                if (Directory.Exists(Path.Combine(binDir, "Debug")) ||
                    Directory.Exists(Path.Combine(binDir, "Release")) ||
                    Directory.EnumerateFiles(binDir, PatternDll, SearchOption.TopDirectoryOnly).Any() ||
                    Directory.EnumerateFiles(binDir, PatternPdb, SearchOption.TopDirectoryOnly).Any())
                {
                    return true;
                }

                if (Directory.EnumerateFiles(binDir, PatternAny, SearchOption.AllDirectories).Any())
                {
                    return false;
                }
            }
        }
        catch
        {
            return true;
        }

        return true;
    }

    public static bool IsNuGetPackagesDirectory(string fullPath)
    {
        try
        {
            if (File.Exists(Path.Combine(fullPath, "repositories.config")))
                return true;

            var parent = Path.GetDirectoryName(fullPath);
            if (parent != null && File.Exists(Path.Combine(parent, "packages.config")))
                return true;

            if (Directory.Exists(fullPath) && Directory.EnumerateFiles(fullPath, "*.nupkg", SearchOption.TopDirectoryOnly).Any())
                return true;
        }
        catch
        {
            // Ignore directory access failure
        }

        return false;
    }

    public void InspectDirectoryFiles(string directoryPath, FileInfo[] files, CodeExplorer.Core.Common.Nodes.WorkspaceNode workspace)
    {
        foreach (var file in files)
        {
            if (file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(workspace).HasDotNetDll = true;
                break;
            }
        }
    }

    public void OnDirectoryScan(string directoryPath, string relativeDir, GitIgnoreMatcher gitignore, Action<string>? log = null)
    {
        var libmanPath = Path.Combine(directoryPath, "libman.json");
        if (File.Exists(libmanPath))
        {
            RegisterLibManExclusions(libmanPath, directoryPath, gitignore, log);
        }
    }

    private sealed record LibManConfig(IReadOnlyList<LibManEntry>? Libraries);
    private sealed record LibManEntry(string? Destination);
    private static readonly JsonSerializerOptions LibManJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static void RegisterLibManExclusions(
        string libmanPath,
        string currentDir,
        GitIgnoreMatcher gitignore,
        Action<string>? log = null)
    {
        try
        {
            var content = PhysicalFileStore.ReadAllText(libmanPath);
            var cfg = JsonSerializer.Deserialize<LibManConfig>(content, LibManJsonOptions);
            if (cfg?.Libraries is not null)
            {
                foreach (var lib in cfg.Libraries)
                {
                    var dest = lib.Destination;
                    if (!string.IsNullOrWhiteSpace(dest))
                    {
                        var fullDest = Path.GetFullPath(Path.Combine(currentDir, dest));
                        var relDest = Path.GetRelativePath(gitignore.WorkspaceRoot, fullDest).Replace('\\', '/').Trim('/');
                        if (!string.IsNullOrEmpty(relDest))
                        {
                            gitignore.AddPattern(relDest + "/");
                            log?.Invoke($"[CSharpProjectParser] LibMan: Excluded library destination '{relDest}'");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"[CSharpProjectParser] Error reading libman.json: {ex.Message}");
        }
    }

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        if (lower.StartsWith("appsettings") && lower.EndsWith(".json"))
        {
            return true;
        }

        if (lower is "web.config" or "app.config")
        {
            return true;
        }

        return false;
    }

    public IReadOnlyList<PackageDescriptor> Packages => CSharpFileParser.DefaultPackages;

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".csproj")
            {
                return true;
            }
        }
        return false;
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var csprojFile = filesInDirectory.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csprojFile != null)
        {
            var name = Path.GetFileNameWithoutExtension(csprojFile);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }
        return Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
    }

    public bool TryParse(ProjectParseContext context, out ProjectParseResult? result)
    {
        var csprojFiles = context.FilesInDirectory
            .Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (csprojFiles.Count == 0)
        {
            result = null;
            return false;
        }

        var nonCsprojFiles = context.FilesInDirectory
            .Where(f => !f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var primaryCsproj = csprojFiles[0];
        var primaryFiles = nonCsprojFiles.Append(primaryCsproj).ToArray();
        var primaryName = GetProjectName(context.DirectoryPath, primaryFiles);
        var primaryProps = ExtractManifestProperties(context.DirectoryPath, primaryFiles);

        List<ParsedProjectDescriptor>? siblingProjects = null;
        if (csprojFiles.Count > 1)
        {
            siblingProjects = [];
            for (var i = 1; i < csprojFiles.Count; i++)
            {
                var sibCsproj = csprojFiles[i];
                var sibFiles = nonCsprojFiles.Append(sibCsproj).ToArray();
                var sibName = Path.GetFileNameWithoutExtension(sibCsproj);
                var sibProps = ExtractManifestProperties(context.DirectoryPath, sibFiles);
                var relIdOverride = !string.IsNullOrEmpty(context.AbsoluteWorkspacePath)
                    ? $"{Path.GetRelativePath(context.AbsoluteWorkspacePath, context.DirectoryPath).Replace('\\', '/').Trim('.').Trim('/')}#{sibName}".TrimStart('#')
                    : $"{Path.GetFileName(context.DirectoryPath.TrimEnd('/', '\\'))}#{sibName}";

                siblingProjects.Add(new ParsedProjectDescriptor(
                    Name: sibName,
                    DirectoryPath: context.DirectoryPath,
                    ProjectType: ProjectType,
                    ManifestProperties: sibProps,
                    RelativeIdPathOverride: relIdOverride));
            }
        }

        result = new ProjectParseResult(
            Project: new ParsedProjectDescriptor(
                Name: primaryName,
                DirectoryPath: context.DirectoryPath,
                ProjectType: ProjectType,
                ManifestProperties: primaryProps),
            ClaimMode: ParserClaimMode.AbsorbSecondaryInDirectory,
            AdditionalSiblingProjects: siblingProjects);
        return true;
    }

    public ProjectClassification? ClassifyProject(ProjectContext context)
    {
        if (context.ManifestProperties != null)
        {
            var manifestType = context.ManifestProperties.GetValueOrDefault("manifest_type");
            if (manifestType == "test" || context.ManifestProperties.GetValueOrDefault("is_test_project") == "true") return ProjectClassification.Test;
            if (context.ManifestProperties.GetValueOrDefault("is_orchestrator") == "true" ||
                context.ManifestProperties.GetValueOrDefault("role") == "Library" ||
                manifestType == "library") return ProjectClassification.Library;

            var primaryRole = context.ManifestProperties.GetValueOrDefault("primary_role");
            var role = context.ManifestProperties.GetValueOrDefault("role");
            if (primaryRole == "App" || role == "FrontendApp" || role == "App") return ProjectClassification.WebApp;
            if (primaryRole == "Worker" || role == "Worker") return ProjectClassification.Worker;
            if (primaryRole == "CliTool" || role == "CliTool") return ProjectClassification.CliTool;
            if (primaryRole == "Service" || role == "Service") return ProjectClassification.Service;

            var sdk = context.ManifestProperties.GetValueOrDefault("sdk");
            if (sdk == "Microsoft.NET.Sdk.Worker") return ProjectClassification.Worker;
            if (sdk == "Microsoft.NET.Sdk.BlazorWebAssembly") return ProjectClassification.WebApp;
            if (sdk == "Microsoft.NET.Sdk.Web") return ProjectClassification.Service;
            if (sdk == "Microsoft.Azure.Functions.Worker") return ProjectClassification.FunctionApp;
        }

        return IProjectParser.DefaultClassifyProject(this, context);
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var csprojFiles = Directory.GetFiles(projectDirectory, "*.csproj");
        if (csprojFiles.Length == 0) return null;

        var csprojFile = csprojFiles[0];
        try
        {
            var content = await PhysicalFileStore.ReadAllTextAsync(csprojFile);
            var doc = System.Xml.Linq.XDocument.Parse(content);

            var propChildren = doc.Root?.Elements()
                .Where(e => e.Name.LocalName == "PropertyGroup")
                .SelectMany(g => g.Elements())
                .ToList();

            // Check IsPackable
            var isPackableStr = propChildren?.FirstOrDefault(e => e.Name.LocalName == "IsPackable")?.Value;
            if (!string.IsNullOrEmpty(isPackableStr) && bool.TryParse(isPackableStr, out var isPackable) && !isPackable)
            {
                return null;
            }

            // Check OutputType (if Exe and not packable, return null)
            var outputType = propChildren?.FirstOrDefault(e => e.Name.LocalName == "OutputType")?.Value;
            var hasGeneratePackageOnBuild = propChildren?.FirstOrDefault(e => e.Name.LocalName == "GeneratePackageOnBuild")?.Value;
            var generateOnBuild = !string.IsNullOrEmpty(hasGeneratePackageOnBuild) &&
                                  bool.TryParse(hasGeneratePackageOnBuild, out var gen) && gen;

            var explicitPackable = !string.IsNullOrEmpty(isPackableStr) &&
                                   bool.TryParse(isPackableStr, out var p) && p;

            if (string.Equals(outputType, "Exe", StringComparison.OrdinalIgnoreCase) && !generateOnBuild && !explicitPackable)
            {
                return null;
            }

            var packageId = propChildren?.FirstOrDefault(e => e.Name.LocalName == "PackageId" && !string.IsNullOrWhiteSpace(e.Value))?.Value
                         ?? propChildren?.FirstOrDefault(e => e.Name.LocalName == "AssemblyName" && !string.IsNullOrWhiteSpace(e.Value))?.Value
                         ?? Path.GetFileNameWithoutExtension(csprojFile);

            var version = propChildren?.FirstOrDefault(e => e.Name.LocalName == "Version" && !string.IsNullOrWhiteSpace(e.Value))?.Value
                       ?? propChildren?.FirstOrDefault(e => e.Name.LocalName == "PackageVersion" && !string.IsNullOrWhiteSpace(e.Value))?.Value
                       ?? "1.0.0";

            return new ProducedPackageInfo(packageId, version, "nuget");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return null;
        }
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        // Check legacy NuGet packages.config
        var packagesConfigFile = Path.Combine(projectDirectory, "packages.config");
        if (File.Exists(packagesConfigFile))
        {
            try
            {
                var pkgContent = await PhysicalFileStore.ReadAllTextAsync(packagesConfigFile);
                var pkgDoc = System.Xml.Linq.XDocument.Parse(pkgContent);
                foreach (var pkg in pkgDoc.Descendants().Where(e => e.Name.LocalName == "package"))
                {
                    var id = pkg.Attribute("id")?.Value;
                    var ver = pkg.Attribute("version")?.Value ?? "unknown";
                    if (!string.IsNullOrEmpty(id))
                    {
                        externalPackages.Add(new ProducedPackageInfo(id, ver, "nuget"));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                // Ignore malformed packages.config
            }
        }

        var csprojFiles = Directory.GetFiles(projectDirectory, "*.csproj");
        foreach (var csprojFile in csprojFiles)
        {
            try
            {
                var content = await PhysicalFileStore.ReadAllTextAsync(csprojFile);
                var doc = System.Xml.Linq.XDocument.Parse(content);

                // Extract local project references (use LocalName to support legacy MSBuild XML namespaces)
                var projectRefs = doc.Descendants().Where(e => e.Name.LocalName == "ProjectReference");
                foreach (var pref in projectRefs)
                {
                    var include = pref.Attribute("Include")?.Value;
                    if (string.IsNullOrEmpty(include)) continue;

                    var resolvedInclude = include.Replace('\\', '/');
                    var csprojDir = (Path.GetDirectoryName(csprojFile) ?? string.Empty).Replace('\\', '/');
                    if (resolvedInclude.Contains("$(SolutionDir)", StringComparison.OrdinalIgnoreCase))
                    {
                        var slnDir = FindSolutionDir(csprojDir).Replace('\\', '/');
                        resolvedInclude = resolvedInclude.Replace("$(SolutionDir)", slnDir.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
                    }
                    if (resolvedInclude.Contains("$(MSBuildThisFileDirectory)", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedInclude = resolvedInclude.Replace("$(MSBuildThisFileDirectory)", csprojDir.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
                    }
                    if (resolvedInclude.Contains("$(ProjectDir)", StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedInclude = resolvedInclude.Replace("$(ProjectDir)", csprojDir.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
                    }

                    var referencedCsprojPath = Path.GetFullPath(Path.Combine(csprojDir, resolvedInclude)).Replace('\\', '/');
                    var referencedProjectDir = Path.GetFullPath(Path.GetDirectoryName(referencedCsprojPath) ?? string.Empty).Replace('\\', '/');
                    referencedProjectDir = ResolveActualDirectoryPath(referencedProjectDir);
                    localProjectPaths.Add(referencedProjectDir);
                }

                // Extract NuGet package references
                var packageRefs = doc.Descendants().Where(e => e.Name.LocalName == "PackageReference");
                foreach (var packRef in packageRefs)
                {
                    var name = packRef.Attribute("Include")?.Value ?? packRef.Attribute("Update")?.Value;
                    var version = packRef.Attribute("Version")?.Value
                                  ?? packRef.Elements().FirstOrDefault(e => e.Name.LocalName == "Version")?.Value
                                  ?? "unknown";
                    if (string.IsNullOrEmpty(name)) continue;

                    externalPackages.Add(new ProducedPackageInfo(name, version, "nuget"));
                }

                // Extract legacy assembly References (e.g. EntityFramework, System.ServiceModel, System.ServiceProcess)
                var assemblyRefs = doc.Descendants().Where(e => e.Name.LocalName == "Reference");
                foreach (var asmRef in assemblyRefs)
                {
                    var rawInclude = asmRef.Attribute("Include")?.Value;
                    if (string.IsNullOrWhiteSpace(rawInclude)) continue;
                    var asmName = rawInclude.Split(',')[0].Trim();
                    if (string.IsNullOrEmpty(asmName) ||
                        asmName.Equals("System", StringComparison.OrdinalIgnoreCase) ||
                        asmName.Equals("System.Core", StringComparison.OrdinalIgnoreCase) ||
                        asmName.Equals("System.Xml", StringComparison.OrdinalIgnoreCase) ||
                        asmName.Equals("System.Xml.Linq", StringComparison.OrdinalIgnoreCase) ||
                        asmName.Equals("Microsoft.CSharp", StringComparison.OrdinalIgnoreCase) ||
                        asmName.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (!externalPackages.Any(p => p.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase)))
                    {
                        externalPackages.Add(new ProducedPackageInfo(asmName, "framework", "nuget"));
                    }
                }

                // Check Web SDK or FrameworkReference
                var sdkAttr = doc.Root?.Attribute("Sdk")?.Value ?? "";
                var hasWebSdk = sdkAttr.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
                                doc.Descendants().Where(e => e.Name.LocalName == "Import").Any(i => (i.Attribute("Sdk")?.Value ?? "").Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase));
                var hasAspNetCoreFrameworkRef = doc.Descendants().Where(e => e.Name.LocalName == "FrameworkReference")
                    .Any(fr => string.Equals(fr.Attribute("Include")?.Value, "Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase));

                if (hasWebSdk || hasAspNetCoreFrameworkRef)
                {
                    externalPackages.Add(new ProducedPackageInfo("Microsoft.AspNetCore.App", "implicit", "nuget"));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                // Ignore malformed csproj or file access issues
            }
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    private static string ResolveActualDirectoryPath(string dirPath)
    {
        if (Directory.Exists(dirPath))
        {
            try
            {
                var parent = Path.GetDirectoryName(dirPath);
                var leaf = Path.GetFileName(dirPath);
                if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf) && Directory.Exists(parent))
                {
                    var match = Directory.EnumerateDirectories(parent)
                        .FirstOrDefault(d => string.Equals(Path.GetFileName(d), leaf, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        return Path.GetFullPath(match).Replace('\\', '/');
                    }
                }
            }
            catch
            {
                // Fallback
            }
            return dirPath;
        }

        try
        {
            var parent = Path.GetDirectoryName(dirPath);
            var leaf = Path.GetFileName(dirPath);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf) && Directory.Exists(parent))
            {
                var match = Directory.EnumerateDirectories(parent)
                    .FirstOrDefault(d => string.Equals(Path.GetFileName(d), leaf, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    return Path.GetFullPath(match).Replace('\\', '/');
                }
            }
        }
        catch
        {
            // Fallback
        }

        return dirPath;
    }

    private static string FindSolutionDir(string startDir)
    {
        var curr = new DirectoryInfo(startDir);
        while (curr != null)
        {
            if (curr.GetFiles("*.sln").Length > 0 || curr.GetFiles("*.slnx").Length > 0)
            {
                return curr.FullName;
            }
            curr = curr.Parent;
        }
        return startDir;
    }
}
