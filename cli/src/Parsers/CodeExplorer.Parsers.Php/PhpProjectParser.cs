using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser;

[assembly: ParserAssembly]

namespace CodeExplorer.Parsers.Php;

[ProjectParser(Tier = ParserTier.Primary)]
public class PhpProjectParser : IProjectParser
{
    private const string LangPhp = "php";
    private const string ComposerJsonFile = "composer.json";
    private const string ComposerLockFile = "composer.lock";
    private const string PhpExt = ".php";
    private const string PhtmlExt = ".phtml";
    private const string IncExt = ".inc";
    private const string YmlExt = ".yml";
    private const string YamlExt = ".yaml";
    private const string XmlExt = ".xml";
    private const string IniExt = ".ini";
    private const string NameProp = "name";
    private const string RequireProp = "require";
    private const string RequireDevProp = "require-dev";
    private const string AutoloadProp = "autoload";
    private const string Psr4Prop = "psr-4";
    private const string Psr0Prop = "psr-0";
    private const string RoleKey = "role";
    private const string KindKey = "kind";
    private const string EntityKindKey = "entity_kind";
    private const string ManifestTypeKey = "manifest_type";
    private const string LanguageKey = "language";
    private const string ProjectTypeKey = "project_type";
    private const string FrameworkKey = "framework";
    private const string OrmKey = "orm";
    private const string ServiceValue = "Service";
    private const string LibraryValue = "Library";
    private const string ComposerManifest = "composer";
    private const string DefaultVersion = "1.0.0";
    private const string UnmanagedPhpManifest = "php-directory";
    private const string SymfonyKeyword = "symfony/";
    private const string SymfonyFramework = "Symfony";
    private const string ZendKeyword = "zendframework";
    private const string ZendClassPrefix = "Zend_Controller_Action";
    private const string ZendFramework = "Zend Framework 1";
    private const string KirbyKeyword = "getkirby/";
    private const string KirbyCmsKeyword = "kirby";
    private const string KirbyFramework = "Kirby CMS";
    private const string LaravelKeyword = "laravel/";
    private const string IlluminateKeyword = "illuminate/";
    private const string LaravelFramework = "Laravel / Illuminate";
    private const string DoctrineKeyword = "doctrine/orm";
    private const string DoctrineDbalKeyword = "doctrine/dbal";
    private const string DoctrineOrmName = "Doctrine ORM";
    private const string NetteDbKeyword = "nette/database";
    private const string NetteDbName = "Nette Database";
    private const string LibraryDirPrefix = "library";
    private const string LibDirPrefix = "lib-";
    private const string GwebLibsPrefix = "gweb-libs";
    private const string ExtPackagePrefix = "ext-";
    private const string DefaultPhpProjectName = "PhpProject";
    private const char SlashChar = '/';
    private const char BackslashChar = '\\';

    private static readonly char[] PathTrimChars = [SlashChar, BackslashChar];

    public string ProjectType => LangPhp;

    public IReadOnlyCollection<string> ExcludedFolders =>
    [
        "vendor", "node_modules", ".git", ".svn", "templates_c", "bin", "obj"
    ];

    public IReadOnlyCollection<string> ManifestFileNames =>
    [
        ComposerJsonFile, ComposerLockFile
    ];

    public bool IsConfigurationFile(string fileName)
    {
        if (fileName.Equals(ComposerJsonFile, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(ComposerLockFile, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(fileName);
        return ext.Equals(YmlExt, StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(YamlExt, StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(XmlExt, StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(IniExt, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> InternalPhpSubfolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "application", "app", "src", "public", "www", "htdocs", "web", "html", "site", "kirby",
        "views", "controllers", "models", "modules", "library", "lib", "libs", "includes", "inc",
        "vendor", "node_modules", "3rdParty", "3rdparty", "thirdparty", "third_party", "external",
        "assets", "static", "js", "css", "skin", "skins", "themes", "tpl", "templates",
        "scripts", "cron", "cronjobs", "cronjobs_old", "CronJobs", "tools", "tests", "test",
        "Oauth", "Retail", "WL", "common", "wlconfig", "configParser",
        "db", "date", "math", "mreg", "badman", "events", "promos", "socket", "billing",
        "payment", "pricing", "renewals", "positions", "riskRating", "action_trigger",
        "dd24_plausib_checks", "dd24Socket", "opmode", "logs-analyzer", "gunter",
        "content", "media", "cache", "storage", "config", "configs", "data", "docs", "sql", "migrations"
    };

    private static readonly HashSet<string> PhpPrimaryAppSubfolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "application", "public", "www", "htdocs", "web", "html", "site"
    };

    private static bool IsVendoredSubfolderName(string folderName)
    {
        return folderName.StartsWith("kirby-", StringComparison.OrdinalIgnoreCase) ||
               folderName.StartsWith("jquery-", StringComparison.OrdinalIgnoreCase) ||
               folderName.StartsWith("jquery.", StringComparison.OrdinalIgnoreCase) ||
               folderName.StartsWith("bootstrap-", StringComparison.OrdinalIgnoreCase) ||
               folderName.StartsWith("Stuk-", StringComparison.OrdinalIgnoreCase) ||
               folderName.StartsWith("tinymce", StringComparison.OrdinalIgnoreCase) ||
               folderName.StartsWith("ckeditor", StringComparison.OrdinalIgnoreCase) ||
               folderName.StartsWith("select2", StringComparison.OrdinalIgnoreCase) ||
               folderName.StartsWith("datatables", StringComparison.OrdinalIgnoreCase) ||
               folderName.EndsWith(".custom", StringComparison.OrdinalIgnoreCase);
    }

    public bool CanContainNestedProject(
        ParsedProjectDescriptor enclosingProject,
        string childDirectoryPath,
        string[] childFiles,
        IProjectParser candidateChildParser)
    {
        if (PhysicalFileStore.TryGetConfiguredProject(childDirectoryPath, out _) ||
            (PhysicalFileStore.IsGitRepositoryRoot(childDirectoryPath) && !PhysicalFileStore.HasChildGitRepositories(childDirectoryPath)))
        {
            return true;
        }

        if (!IProjectParser.DefaultCanContainNestedProject(enclosingProject, childDirectoryPath, candidateChildParser))
        {
            return false;
        }

        if (candidateChildParser.ProjectType is "php" or "perl" or "python" or "script")
        {
            return false;
        }

        var hasExplicitManifest = HasComposerManifest(childFiles) ||
                                  candidateChildParser.ManifestFileNames.Any(m =>
                                      childFiles.Any(f => Path.GetFileName(f).Equals(m, StringComparison.OrdinalIgnoreCase)));
        if (!hasExplicitManifest)
        {
            return false;
        }

        var relToParent = Path.GetRelativePath(enclosingProject.DirectoryPath, childDirectoryPath).Replace('\\', '/').Trim('/');
        var segments = relToParent.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (InternalPhpSubfolders.Contains(segments[i]) || IsVendoredSubfolderName(segments[i]))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        if (HasComposerManifest(filesInDirectory))
        {
            return !HasAncestorPhpProject(directoryPath);
        }

        if (PhysicalFileStore.HasChildGitRepositories(directoryPath) ||
            PhysicalFileStore.HasChildConfiguredProjects(directoryPath))
        {
            return false;
        }

        var hasPhpFiles = filesInDirectory.Any(IsPhpSourceFile) ||
                          IsPhpApplicationOrContainerRoot(directoryPath, filesInDirectory);
        if (!hasPhpFiles)
        {
            return false;
        }

        return !HasAncestorPhpProject(directoryPath);
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var composerPath = Path.Combine(directoryPath, ComposerJsonFile);
        if (PhysicalFileStore.FileExistsInCachedDirectory(composerPath))
        {
            var composerName = TryReadComposerName(composerPath);
            if (!string.IsNullOrWhiteSpace(composerName))
            {
                return composerName;
            }
        }

        var folderName = Path.GetFileName(directoryPath.TrimEnd(PathTrimChars));
        return string.IsNullOrWhiteSpace(folderName) ? DefaultPhpProjectName : folderName;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var composerPath = Path.Combine(projectDirectory, ComposerJsonFile);
        if (PhysicalFileStore.FileExistsInCachedDirectory(composerPath))
        {
            var name = TryReadComposerName(composerPath);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return Task.FromResult<ProducedPackageInfo?>(new ProducedPackageInfo(name, DefaultVersion, ComposerManifest));
            }
        }

        return Task.FromResult<ProducedPackageInfo?>(null);
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var packages = new List<ProducedPackageInfo>();
        var composerPath = Path.Combine(projectDirectory, ComposerJsonFile);
        if (PhysicalFileStore.FileExistsInCachedDirectory(composerPath))
        {
            TryExtractComposerPackages(composerPath, packages);
        }

        return Task.FromResult(new ProjectDependencyInfo([], packages));
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var hasComposer = HasComposerManifest(filesInDirectory);
        var folderName = Path.GetFileName(directoryPath.TrimEnd(PathTrimChars));
        var isLibrary = folderName.StartsWith(LibraryDirPrefix, StringComparison.OrdinalIgnoreCase) ||
                        folderName.StartsWith(LibDirPrefix, StringComparison.OrdinalIgnoreCase) ||
                        folderName.StartsWith(GwebLibsPrefix, StringComparison.OrdinalIgnoreCase);
        var role = isLibrary ? LibraryValue : ServiceValue;

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RoleKey] = role,
            [KindKey] = role,
            [EntityKindKey] = role,
            [ManifestTypeKey] = hasComposer ? ComposerManifest : UnmanagedPhpManifest,
            [LanguageKey] = LangPhp,
            [ProjectTypeKey] = LangPhp
        };

        var composerPath = Path.Combine(directoryPath, ComposerJsonFile);
        if (PhysicalFileStore.FileExistsInCachedDirectory(composerPath))
        {
            EnrichFromComposer(composerPath, props);
        }

        return props;
    }

    private static bool HasComposerManifest(string[] filesInDirectory)
    {
        for (var i = 0; i < filesInDirectory.Length; i++)
        {
            var name = Path.GetFileName(filesInDirectory[i]);
            if (name.Equals(ComposerJsonFile, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(ComposerLockFile, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsPhpSourceFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(PhpExt, StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(PhtmlExt, StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(IncExt, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPhpWebEntrypoint(string directoryPath)
    {
        return PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "public", "index.php")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "www", "index.php")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "htdocs", "index.php")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "web", "index.php")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "html", "index.php")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "application", "Bootstrap.php")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "application", "configs", "application.ini")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "artisan"));
    }

    private static bool HasPythonServiceManifest(string directoryPath, string[] filesInDirectory)
    {
        for (var i = 0; i < filesInDirectory.Length; i++)
        {
            var fn = Path.GetFileName(filesInDirectory[i]);
            if (fn.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("setup.py", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("Pipfile", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals(".coveragerc", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "requirements", "requirements.txt"));
    }

    private static bool IsPhpApplicationOrContainerRoot(string directoryPath, string[] filesInDirectory)
    {
        if (HasPhpWebEntrypoint(directoryPath))
        {
            return true;
        }

        if (!PhysicalFileStore.HasCachedDirectory(directoryPath))
        {
            return false;
        }

        var folderName = Path.GetFileName(directoryPath.TrimEnd(PathTrimChars));
        var parentDir = Directory.GetParent(directoryPath);
        if (parentDir != null &&
            PhysicalFileStore.HasCachedDirectory(parentDir.FullName) &&
            (InternalPhpSubfolders.Contains(folderName) || IsVendoredSubfolderName(folderName)))
        {
            return false;
        }

        if (HasPythonServiceManifest(directoryPath, filesInDirectory))
        {
            return false;
        }

        if (PhysicalFileStore.IsGitRepositoryRoot(directoryPath) &&
            !PhysicalFileStore.HasChildGitRepositories(directoryPath) &&
            !SubtreeHasOtherPrimaryManifests(directoryPath))
        {
            var phpCount = 0;
            var pyCount = 0;
            var perlCount = 0;
            var otherCodeCount = 0;
            foreach (var file in PhysicalFileStore.EnumerateCachedFilesInSubtree(directoryPath))
            {
                if (IsPhpSourceFile(file)) phpCount++;
                else if (file.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) pyCount++;
                else if (file.EndsWith(".pl", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".pm", StringComparison.OrdinalIgnoreCase)) perlCount++;
                else if (Path.GetExtension(file).ToLowerInvariant() is
                         ".cs" or ".ts" or ".tsx" or ".go" or ".rs" or ".java" or ".kt" or ".tf" or ".tofu" or ".cfc" or ".cfm")
                {
                    otherCodeCount++;
                }
            }
            if (phpCount > 0 && phpCount >= pyCount && phpCount >= perlCount && phpCount >= otherCodeCount)
            {
                return true;
            }
        }

        var childDirs = PhysicalFileStore.GetCachedDirectChildDirectories(directoryPath);
        if (childDirs.Count == 0)
        {
            return false;
        }

        var internalPhpChildCount = 0;
        var hasPrimaryAppChild = false;
        for (var i = 0; i < childDirs.Count; i++)
        {
            var childName = Path.GetFileName(childDirs[i].DirectoryPath.TrimEnd(PathTrimChars));
            if (!InternalPhpSubfolders.Contains(childName) && !IsVendoredSubfolderName(childName))
            {
                continue;
            }

            if (PhysicalFileStore.EnumerateCachedFilesInSubtree(childDirs[i].DirectoryPath).Any(IsPhpSourceFile))
            {
                internalPhpChildCount++;
                if (PhpPrimaryAppSubfolders.Contains(childName))
                {
                    hasPrimaryAppChild = true;
                }
            }
        }

        if (hasPrimaryAppChild || internalPhpChildCount >= 2)
        {
            return true;
        }

        if (internalPhpChildCount == 1 &&
            filesInDirectory.Any(f =>
            {
                var fn = Path.GetFileName(f);
                return fn.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                       fn.Equals("compose.yaml", StringComparison.OrdinalIgnoreCase) ||
                       fn.Equals("compose.yml", StringComparison.OrdinalIgnoreCase) ||
                       fn.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
                       fn.Equals("docker-compose.yaml", StringComparison.OrdinalIgnoreCase) ||
                       fn.Equals(".htaccess", StringComparison.OrdinalIgnoreCase);
            }))
        {
            return true;
        }

        return false;
    }

    private static bool SubtreeHasOtherPrimaryManifests(string directoryPath)
    {
        foreach (var file in PhysicalFileStore.EnumerateCachedFilesInSubtree(directoryPath))
        {
            var fn = Path.GetFileName(file);
            var lowerFn = fn.ToLowerInvariant();
            var lowerExt = Path.GetExtension(lowerFn);
            if (lowerFn is "terragrunt.hcl" or "root.hcl" or "box.json" or "application.cfc" or "application.cfm" ||
                lowerExt is ".tf" or ".tofu" ||
                fn.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                fn.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("setup.py", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("go.mod", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("build.gradle", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("build.gradle.kts", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAncestorPhpProject(string directoryPath)
    {
        if ((PhysicalFileStore.IsGitRepositoryRoot(directoryPath) && !PhysicalFileStore.HasChildGitRepositories(directoryPath)) ||
            (PhysicalFileStore.HasAnyConfiguredProjects && PhysicalFileStore.TryGetConfiguredProject(directoryPath, out _)))
        {
            return false;
        }

        var useCacheBound = PhysicalFileStore.HasCachedDirectory(directoryPath);
        var current = Directory.GetParent(directoryPath);
        var depth = 0;
        while (current != null && depth < 16)
        {
            depth++;
            if (useCacheBound && !PhysicalFileStore.HasCachedDirectory(current.FullName))
            {
                break;
            }

            if (PhysicalFileStore.HasChildGitRepositories(current.FullName) ||
                PhysicalFileStore.HasChildConfiguredProjects(current.FullName))
            {
                break;
            }

            var parentFiles = PhysicalFileStore.GetDirectoryFiles(current.FullName);
            if (HasComposerManifest(parentFiles) ||
                parentFiles.Any(IsPhpSourceFile) ||
                parentFiles.Any(f => f.EndsWith(".py", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".pl", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".pm", StringComparison.OrdinalIgnoreCase)) ||
                HasPythonServiceManifest(current.FullName, parentFiles) ||
                IsPhpApplicationOrContainerRoot(current.FullName, parentFiles))
            {
                return true;
            }

            if (PhysicalFileStore.IsGitRepositoryRoot(current.FullName))
            {
                break;
            }

            current = current.Parent;
        }
        return false;
    }

    private static string? TryReadComposerName(string composerPath)
    {
        try
        {
            var json = PhysicalFileStore.ReadAllText(composerPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(NameProp, out var nameEl) &&
                nameEl.ValueKind == JsonValueKind.String)
            {
                var raw = nameEl.GetString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    return raw.Trim();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
        return null;
    }

    private static void TryExtractComposerPackages(string composerPath, List<ProducedPackageInfo> packages)
    {
        try
        {
            var json = PhysicalFileStore.ReadAllText(composerPath);
            using var doc = JsonDocument.Parse(json);
            ReadPackageSection(doc.RootElement, RequireProp, packages);
            ReadPackageSection(doc.RootElement, RequireDevProp, packages);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Ignore malformed composer.json
        }
    }

    private static void ReadPackageSection(JsonElement root, string sectionName, List<ProducedPackageInfo> packages)
    {
        if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var prop in section.EnumerateObject())
        {
            if (prop.Name.Equals(LangPhp, StringComparison.OrdinalIgnoreCase) ||
                prop.Name.StartsWith(ExtPackagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var version = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? string.Empty : string.Empty;
            packages.Add(new ProducedPackageInfo(prop.Name, version, ComposerManifest));
        }
    }

    private static void EnrichFromComposer(string composerPath, Dictionary<string, string> props)
    {
        try
        {
            var content = PhysicalFileStore.ReadAllText(composerPath);
            if (content.Contains(SymfonyKeyword, StringComparison.OrdinalIgnoreCase))
            {
                props[FrameworkKey] = SymfonyFramework;
            }
            else if (content.Contains(ZendKeyword, StringComparison.OrdinalIgnoreCase) ||
                     content.Contains(ZendClassPrefix, StringComparison.OrdinalIgnoreCase))
            {
                props[FrameworkKey] = ZendFramework;
            }
            else if (content.Contains(KirbyKeyword, StringComparison.OrdinalIgnoreCase) ||
                     content.Contains(KirbyCmsKeyword, StringComparison.OrdinalIgnoreCase))
            {
                props[FrameworkKey] = KirbyFramework;
            }
            else if (content.Contains(LaravelKeyword, StringComparison.OrdinalIgnoreCase) ||
                     content.Contains(IlluminateKeyword, StringComparison.OrdinalIgnoreCase))
            {
                props[FrameworkKey] = LaravelFramework;
            }

            if (content.Contains(DoctrineKeyword, StringComparison.OrdinalIgnoreCase) ||
                content.Contains(DoctrineDbalKeyword, StringComparison.OrdinalIgnoreCase))
            {
                props[OrmKey] = DoctrineOrmName;
            }
            else if (content.Contains(NetteDbKeyword, StringComparison.OrdinalIgnoreCase))
            {
                props[OrmKey] = NetteDbName;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore IO errors during enrichment
        }
    }
}
