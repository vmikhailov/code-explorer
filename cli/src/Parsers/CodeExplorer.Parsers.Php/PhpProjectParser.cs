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
        "vendor", "node_modules", ".git", ".svn", "var", "cache", "tmp", "bin", "obj"
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

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        if (HasComposerManifest(filesInDirectory))
        {
            return true;
        }

        var hasPhpFiles = filesInDirectory.Any(IsPhpSourceFile);
        if (!hasPhpFiles)
        {
            return false;
        }

        return !HasAncestorPhpProject(directoryPath);
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var composerPath = Path.Combine(directoryPath, ComposerJsonFile);
        if (File.Exists(composerPath))
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
        if (File.Exists(composerPath))
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
        if (File.Exists(composerPath))
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
        if (File.Exists(composerPath))
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

    private static bool HasAncestorPhpProject(string directoryPath)
    {
        var current = Directory.GetParent(directoryPath);
        var depth = 0;
        while (current != null && depth < 5)
        {
            depth++;
            if (File.Exists(Path.Combine(current.FullName, ComposerJsonFile)) ||
                File.Exists(Path.Combine(current.FullName, ComposerLockFile)))
            {
                return true;
            }

            try
            {
                var parentFiles = Directory.GetFiles(current.FullName);
                if (parentFiles.Any(IsPhpSourceFile))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
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
