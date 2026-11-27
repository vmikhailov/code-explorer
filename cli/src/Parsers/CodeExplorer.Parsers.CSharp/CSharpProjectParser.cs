using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using CodeExplorer.Core.Common.Nodes;

[assembly: ParserAssembly]

namespace CodeExplorer.Parsers.CSharp;

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

    public void InspectDirectoryFiles(string directoryPath, FileInfo[] files, WorkspaceNode workspace)
    {
        foreach (var file in files)
        {
            if (file.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                AnalysisSignalStore.Signals(workspace).HasDotNetDll = true;
                break;
            }
        }
    }

    public void InspectDirectoryFilePaths(string directoryPath, string[] filePaths, WorkspaceNode workspace)
    {
        for (var i = 0; i < filePaths.Length; i++)
        {
            if (filePaths[i].EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                AnalysisSignalStore.Signals(workspace).HasDotNetDll = true;
                break;
            }
        }
    }

    public void OnDirectoryScan(string directoryPath, string relativeDir, GitIgnoreMatcher gitignore, Action<string>? log = null)
    {
        var libmanPath = Path.Combine(directoryPath, "libman.json");
        if (WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(libmanPath))
        {
            RegisterLibManExclusions(libmanPath, directoryPath, gitignore, log);
        }
    }

    public void CollectDirectoryIgnorePatterns(string directoryPath, string relativeDir, string workspaceRoot, List<string> outGlobalPatterns, Action<string>? log = null)
    {
        const string libmanFileName = "libman.json";
        const string slash = "/";
        var libmanPath = Path.Combine(directoryPath, libmanFileName);
        if (!WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(libmanPath))
        {
            return;
        }

        try
        {
            var content = FileStreamReader.ReadTextOnce(libmanPath);
            var cfg = JsonSerializer.Deserialize<LibManConfig>(content, LibManJsonOptions);
            if (cfg?.Libraries is null) return;
            foreach (var lib in cfg.Libraries)
            {
                if (string.IsNullOrWhiteSpace(lib.Destination)) continue;
                var fullDest = Path.GetFullPath(Path.Combine(directoryPath, lib.Destination));
                var relDest = Path.GetRelativePath(workspaceRoot, fullDest).Replace('\\', '/').Trim('/');
                if (!string.IsNullOrEmpty(relDest))
                {
                    outGlobalPatterns.Add(relDest + slash);
                    log?.Invoke($"[CSharpProjectParser] LibMan: Excluded library destination '{relDest}'");
                }
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"[CSharpProjectParser] Error reading libman.json: {ex.Message}");
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
            var content = FileStreamReader.ReadTextOnce(libmanPath);
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
        var activeIndex = context.DirectoryIndex ?? WorkspaceDirectoryIndex.Current;
        if (activeIndex != null && activeIndex.TryGetCachedProjectParse(this, context.DirectoryPath, out var cached))
        {
            result = cached.Result;
            return cached.Matched;
        }

        var csprojFiles = context.FilesInDirectory
            .Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (csprojFiles.Count == 0)
        {
            activeIndex?.StoreProjectParse(this, context.DirectoryPath, false, null);
            result = null;
            return false;
        }

        var nonCsprojFiles = context.FilesInDirectory
            .Where(f => !f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var legacyPackagesConfig = ParseLegacyPackagesConfig(context.FilesInDirectory);
        var dirMetadata = ExtractDirectoryLevelMetadata(context.DirectoryPath, context.FilesInDirectory);

        var primaryDescriptor = ParseSingleCsprojDescriptor(
            context.DirectoryPath,
            csprojFiles[0],
            nonCsprojFiles,
            legacyPackagesConfig,
            dirMetadata,
            relIdOverride: null);

        List<ParsedProjectDescriptor>? siblingProjects = null;
        if (csprojFiles.Count > 1)
        {
            siblingProjects = [];
            for (var i = 1; i < csprojFiles.Count; i++)
            {
                var sibCsproj = csprojFiles[i];
                var sibName = Path.GetFileNameWithoutExtension(sibCsproj);
                var relIdOverride = !string.IsNullOrEmpty(context.AbsoluteWorkspacePath)
                    ? $"{Path.GetRelativePath(context.AbsoluteWorkspacePath, context.DirectoryPath).Replace('\\', '/').Trim('.').Trim('/')}#{sibName}".TrimStart('#')
                    : $"{Path.GetFileName(context.DirectoryPath.TrimEnd('/', '\\'))}#{sibName}";

                siblingProjects.Add(ParseSingleCsprojDescriptor(
                    context.DirectoryPath,
                    sibCsproj,
                    nonCsprojFiles,
                    legacyPackagesConfig,
                    dirMetadata,
                    relIdOverride));
            }
        }

        result = new ProjectParseResult(
            Project: primaryDescriptor,
            ClaimMode: ParserClaimMode.AbsorbSecondaryInDirectory,
            AdditionalSiblingProjects: siblingProjects);
        activeIndex?.StoreProjectParse(this, context.DirectoryPath, true, result);
        return true;
    }

    public ParsedProjectManifestMetadata? InspectDirectoryManifestMetadata(ProjectParseContext context) =>
        ExtractDirectoryLevelMetadata(context.DirectoryPath, context.FilesInDirectory);

    private ParsedProjectDescriptor ParseSingleCsprojDescriptor(
        string directoryPath,
        string csprojFile,
        string[] nonCsprojFiles,
        IReadOnlyList<ProducedPackageInfo> legacyPackagesConfig,
        ParsedProjectManifestMetadata? dirMetadata,
        string? relIdOverride)
    {
        var csprojName = Path.GetFileNameWithoutExtension(csprojFile);
        if (string.IsNullOrWhiteSpace(csprojName))
        {
            csprojName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));
        }

        var filesWithThisCsproj = nonCsprojFiles.Append(csprojFile).ToArray();
        var content = File.Exists(csprojFile) ? FileStreamReader.ReadTextOnce(csprojFile) : string.Empty;
        XDocument? doc = null;
        if (!string.IsNullOrEmpty(content))
        {
            try
            {
                doc = XDocument.Parse(content);
            }
            catch (XmlException)
            {
                // Malformed XML still permits regex-based manifest property heuristics below
            }
        }

        var props = ExtractManifestPropertiesFromContent(directoryPath, csprojFile, content, filesWithThisCsproj);
        var produced = doc != null ? ExtractProducedPackageFromDoc(csprojFile, doc) : null;
        var deps = ExtractDependenciesFromDoc(csprojFile, doc, legacyPackagesConfig);

        const string keySdk = "sdk";
        const string keyOutputType = "output_type";
        const string keyIsExecutable = "is_executable";
        var sdk = props.GetValueOrDefault(keySdk) ?? "";
        var outputType = props.GetValueOrDefault(keyOutputType) ?? "";
        var isWeb = sdk.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase);
        var isWorker = sdk.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase);
        var isExe = outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase) ||
                    outputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase) ||
                    props.GetValueOrDefault(keyIsExecutable) == "true";

        var mergedMeta = (dirMetadata ?? new ParsedProjectManifestMetadata()) with
        {
            IsWebSdk = isWeb,
            IsWorkerSdk = isWorker,
            IsExecutable = isExe,
            FriendAssemblies = doc != null ? ExtractInternalsVisibleToFromDoc(doc) : null
        };

        return new ParsedProjectDescriptor(
            Name: csprojName,
            DirectoryPath: directoryPath,
            ProjectType: ProjectType,
            ManifestProperties: props,
            RelativeIdPathOverride: relIdOverride,
            ProducedPackage: produced,
            Dependencies: deps,
            ManifestMetadata: mergedMeta);
    }

    private static IReadOnlyList<string>? ExtractInternalsVisibleToFromDoc(XDocument doc)
    {
        const string ivtElem = "InternalsVisibleTo";
        const string asmAttrElem = "AssemblyAttribute";
        const string ivtAttrName = "InternalsVisibleToAttribute";
        const string param1Elem = "_Parameter1";
        const string includeAttr = "Include";

        List<string>? friends = null;
        foreach (var elem in doc.Descendants())
        {
            var local = elem.Name.LocalName;
            if (local.Equals(ivtElem, StringComparison.OrdinalIgnoreCase))
            {
                var raw = elem.Attribute(includeAttr)?.Value;
                AddFriendAssembly(raw, ref friends);
            }
            else if (local.Equals(asmAttrElem, StringComparison.OrdinalIgnoreCase))
            {
                var attrType = elem.Attribute(includeAttr)?.Value;
                if (attrType != null && attrType.Contains(ivtAttrName, StringComparison.OrdinalIgnoreCase))
                {
                    var p1 = elem.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(param1Elem, StringComparison.OrdinalIgnoreCase))?.Value;
                    AddFriendAssembly(p1, ref friends);
                }
            }
        }

        return friends;

        static void AddFriendAssembly(string? raw, ref List<string>? list)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            var commaIdx = raw.IndexOf(',');
            var clean = (commaIdx > 0 ? raw[..commaIdx] : raw).Trim();
            if (clean.Length == 0 || clean.Contains("$(")) return;
            list ??= [];
            if (!list.Contains(clean, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(clean);
            }
        }
    }

    private static ParsedProjectManifestMetadata? ExtractDirectoryLevelMetadata(string directoryPath, string[] filesInDirectory)
    {
        string? nuspecId = null;
        string? nuspecVer = null;
        List<ProducedPackageInfo>? nuspecDeps = null;

        foreach (var f in filesInDirectory)
        {
            if (!f.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase)) continue;
            var nuspecXml = FileStreamReader.ReadTextOnce(f);
            if (string.IsNullOrEmpty(nuspecXml)) continue;

            try
            {
                var xdoc = XDocument.Parse(nuspecXml);
                var metadata = xdoc.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("metadata", StringComparison.OrdinalIgnoreCase));
                nuspecId ??= metadata?.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("id", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                nuspecVer ??= metadata?.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("version", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

                foreach (var depElem in xdoc.Descendants().Where(e => e.Name.LocalName.Equals("dependency", StringComparison.OrdinalIgnoreCase)))
                {
                    var depId = depElem.Attribute(OntologyConstants.ManifestFields.Id)?.Value?.Trim();
                    var depVer = depElem.Attribute(OntologyConstants.ManifestFields.Version)?.Value?.Trim() ?? "*";
                    if (string.IsNullOrWhiteSpace(depId)) continue;
                    nuspecDeps ??= [];
                    nuspecDeps.Add(new ProducedPackageInfo(depId, depVer, OntologyConstants.ManifestFields.NuGetEcosystem));
                }
            }
            catch (XmlException)
            {
                // Ignore malformed .nuspec XML
            }
        }

        var (dbEngine, dbFamily) = DetectMsBuildPropsDbDriver(directoryPath);
        if (nuspecId == null && nuspecVer == null && nuspecDeps == null && dbEngine == null)
        {
            return null;
        }

        return new ParsedProjectManifestMetadata(
            NuspecId: nuspecId,
            NuspecVersion: nuspecVer,
            NuspecDependencies: nuspecDeps,
            DatabaseDriverEngine: dbEngine,
            DatabaseDriverFamily: dbFamily);
    }

    private static (string? Engine, string? Family) DetectMsBuildPropsDbDriver(string directoryPath)
    {
        var curr = directoryPath;
        for (var depth = 0; depth < 6 && !string.IsNullOrEmpty(curr); depth++)
        {
            foreach (var propFile in new[] { "Directory.Packages.props", "Directory.Build.props" })
            {
                var propPath = Path.Combine(curr, propFile);
                if (!WorkspaceDirectoryIndex.FileExistsInCurrentOrDisk(propPath)) continue;
                var content = FileStreamReader.ReadTextOnce(propPath);
                if (string.IsNullOrEmpty(content)) continue;

                if (content.Contains("npgsql", StringComparison.OrdinalIgnoreCase)) return ("PostgreSQL", "relational");
                if (content.Contains("sqlclient", StringComparison.OrdinalIgnoreCase) || content.Contains("entityframeworkcore.sqlserver", StringComparison.OrdinalIgnoreCase)) return ("SQL Server", "relational");
                if (content.Contains("mysql", StringComparison.OrdinalIgnoreCase) || content.Contains("pomelo", StringComparison.OrdinalIgnoreCase)) return ("MySQL", "relational");
                if (content.Contains("sqlite", StringComparison.OrdinalIgnoreCase)) return ("SQLite", "relational");
                if (content.Contains("oracle", StringComparison.OrdinalIgnoreCase)) return ("Oracle", "relational");
            }
            curr = Path.GetDirectoryName(curr);
        }
        return (null, null);
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

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var dirFiles = WorkspaceDirectoryIndex.GetDirectoryFilesOrFallback(projectDirectory);
        if (TryParse(new ProjectParseContext(projectDirectory, dirFiles), out var parsed) && parsed != null)
        {
            return Task.FromResult(parsed.Project.ProducedPackage);
        }
        return Task.FromResult<ProducedPackageInfo?>(null);
    }

    private static ProducedPackageInfo? ExtractProducedPackageFromDoc(string csprojFile, XDocument doc)
    {
        var propChildren = doc.Root?.Elements()
            .Where(e => e.Name.LocalName == "PropertyGroup")
            .SelectMany(g => g.Elements())
            .ToList();

        var isPackableStr = propChildren?.FirstOrDefault(e => e.Name.LocalName == "IsPackable")?.Value;
        if (!string.IsNullOrEmpty(isPackableStr) && bool.TryParse(isPackableStr, out var isPackable) && !isPackable)
        {
            return null;
        }

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

        return new ProducedPackageInfo(packageId, version, OntologyConstants.ManifestFields.NuGetEcosystem);
    }

    private static List<ProducedPackageInfo> ParseLegacyPackagesConfig(string[] dirFiles)
    {
        const string unknownVer = "unknown";
        var packages = new List<ProducedPackageInfo>();
        var packagesConfigFile = dirFiles.FirstOrDefault(f => Path.GetFileName(f).Equals("packages.config", StringComparison.OrdinalIgnoreCase));
        if (packagesConfigFile == null) return packages;

        try
        {
            var pkgContent = FileStreamReader.ReadTextOnce(packagesConfigFile);
            if (string.IsNullOrEmpty(pkgContent)) return packages;
            var pkgDoc = XDocument.Parse(pkgContent);
            foreach (var pkg in pkgDoc.Descendants().Where(e => e.Name.LocalName == "package"))
            {
                var id = pkg.Attribute(OntologyConstants.ManifestFields.Id)?.Value;
                var ver = pkg.Attribute(OntologyConstants.ManifestFields.Version)?.Value ?? unknownVer;
                if (!string.IsNullOrEmpty(id))
                {
                    packages.Add(new ProducedPackageInfo(id, ver, OntologyConstants.ManifestFields.NuGetEcosystem));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            // Ignore malformed packages.config
        }

        return packages;
    }

    private static ProjectDependencyInfo ExtractDependenciesFromDoc(
        string csprojFile,
        XDocument? doc,
        IReadOnlyList<ProducedPackageInfo> legacyPackagesConfig)
    {
        const string attrInclude = "Include";
        const string attrUpdate = "Update";
        const string attrVersion = "Version";
        const string attrSdk = "Sdk";
        const string unknownVer = "unknown";
        const string frameworkVer = "framework";
        const string implicitVer = "implicit";
        const string aspNetCoreApp = "Microsoft.AspNetCore.App";

        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>(legacyPackagesConfig);
        if (doc == null)
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        var csprojDir = (Path.GetDirectoryName(csprojFile) ?? string.Empty).Replace('\\', '/');
        foreach (var pref in doc.Descendants().Where(e => e.Name.LocalName == "ProjectReference"))
        {
            var include = pref.Attribute(attrInclude)?.Value;
            if (string.IsNullOrEmpty(include)) continue;

            var resolvedInclude = include.Replace('\\', '/');
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

        foreach (var packRef in doc.Descendants().Where(e => e.Name.LocalName == "PackageReference"))
        {
            var name = packRef.Attribute(attrInclude)?.Value ?? packRef.Attribute(attrUpdate)?.Value;
            var version = packRef.Attribute(attrVersion)?.Value
                          ?? packRef.Elements().FirstOrDefault(e => e.Name.LocalName == attrVersion)?.Value
                          ?? unknownVer;
            if (string.IsNullOrEmpty(name)) continue;

            externalPackages.Add(new ProducedPackageInfo(name, version, OntologyConstants.ManifestFields.NuGetEcosystem));
        }

        foreach (var asmRef in doc.Descendants().Where(e => e.Name.LocalName == "Reference"))
        {
            var rawInclude = asmRef.Attribute(attrInclude)?.Value;
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
                externalPackages.Add(new ProducedPackageInfo(asmName, frameworkVer, OntologyConstants.ManifestFields.NuGetEcosystem));
            }
        }

        var sdkAttr = doc.Root?.Attribute(attrSdk)?.Value ?? "";
        var hasWebSdk = sdkAttr.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) ||
                        doc.Descendants().Where(e => e.Name.LocalName == "Import").Any(i => (i.Attribute(attrSdk)?.Value ?? "").Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase));
        var hasAspNetCoreFrameworkRef = doc.Descendants().Where(e => e.Name.LocalName == "FrameworkReference")
            .Any(fr => string.Equals(fr.Attribute(attrInclude)?.Value, aspNetCoreApp, StringComparison.OrdinalIgnoreCase));

        if (hasWebSdk || hasAspNetCoreFrameworkRef)
        {
            externalPackages.Add(new ProducedPackageInfo(aspNetCoreApp, implicitVer, OntologyConstants.ManifestFields.NuGetEcosystem));
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var dirFiles = WorkspaceDirectoryIndex.GetDirectoryFilesOrFallback(projectDirectory);
        if (TryParse(new ProjectParseContext(projectDirectory, dirFiles), out var parsed) && parsed != null)
        {
            if (parsed.AdditionalSiblingProjects is not { Count: > 0 } && parsed.Project.Dependencies != null)
            {
                return Task.FromResult(parsed.Project.Dependencies);
            }

            var mergedLocal = new List<string>(parsed.Project.Dependencies?.LocalProjectPaths ?? []);
            var mergedExt = new List<ProducedPackageInfo>(parsed.Project.Dependencies?.ExternalPackages ?? []);
            if (parsed.AdditionalSiblingProjects != null)
            {
                foreach (var sib in parsed.AdditionalSiblingProjects)
                {
                    if (sib.Dependencies == null) continue;
                    mergedLocal.AddRange(sib.Dependencies.LocalProjectPaths);
                    mergedExt.AddRange(sib.Dependencies.ExternalPackages);
                }
            }
            return Task.FromResult(new ProjectDependencyInfo(mergedLocal, mergedExt));
        }

        var legacyOnly = ParseLegacyPackagesConfig(dirFiles);
        return Task.FromResult(new ProjectDependencyInfo([], legacyOnly));
    }

    private static string ResolveActualDirectoryPath(string dirPath)
    {
        if (WorkspaceDirectoryIndex.HasCachedDirectoryInCurrent(dirPath) || OperatingSystem.IsWindows())
        {
            return dirPath;
        }

        if (Directory.Exists(dirPath))
        {
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
        var curr = startDir;
        while (!string.IsNullOrEmpty(curr))
        {
            var files = WorkspaceDirectoryIndex.GetDirectoryFilesOrFallback(curr);
            if (files.Any(f => f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
            {
                return curr;
            }
            var parent = Path.GetDirectoryName(curr);
            if (string.IsNullOrEmpty(parent) || string.Equals(parent, curr, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
            curr = parent;
        }
        return startDir;
    }
}
