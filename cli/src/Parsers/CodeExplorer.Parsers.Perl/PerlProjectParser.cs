using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Parser;

[assembly: ParserAssembly]

namespace CodeExplorer.Parsers.Perl;

[ProjectParser(Tier = ParserTier.Primary)]
public class PerlProjectParser : IProjectParser
{
    private const string LangPerl = "perl";
    private const string CpanfileName = "cpanfile";
    private const string MakefilePlName = "Makefile.PL";
    private const string BuildPlName = "Build.PL";
    private const string MetaJsonName = "META.json";
    private const string MetaYmlName = "META.yml";
    private const string DistIniName = "dist.ini";
    private const string PlExt = ".pl";
    private const string PmExt = ".pm";
    private const string TExt = ".t";
    private const string RoleKey = "role";
    private const string KindKey = "kind";
    private const string EntityKindKey = "entity_kind";
    private const string ManifestTypeKey = "manifest_type";
    private const string LanguageKey = "language";
    private const string ProjectTypeKey = "project_type";
    private const string ServiceValue = "Service";
    private const string LibraryValue = "Library";
    private const string CpanManifestValue = "cpan";
    private const string PerlDirectoryManifestValue = "perl-directory";
    private const string DefaultPerlProjectName = "PerlProject";
    private const string LibFolderKeyword = "lib";
    private const char SlashChar = '/';
    private const char BackslashChar = '\\';

    private static readonly char[] PathTrimChars = [SlashChar, BackslashChar];

    private static readonly Regex CpanRequiresRegex = new(
        @"^\s*requires\s+['""](?<pkg>[a-zA-Z0-9_:]+)['""](?:\s*,\s*['""](?<ver>[^'""]+)['""])?",
        RegexOptions.Compiled | RegexOptions.Multiline);

    public string ProjectType => LangPerl;

    public IReadOnlyCollection<string> ExcludedFolders =>
    [
        "blib", ".build", "node_modules", "vendor", ".git", ".svn", "bin", "obj"
    ];

    public IReadOnlyCollection<string> ManifestFileNames =>
    [
        CpanfileName, MakefilePlName, BuildPlName, MetaJsonName, MetaYmlName, DistIniName
    ];

    public bool IsConfigurationFile(string fileName)
    {
        return fileName.Equals(CpanfileName, StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals(MakefilePlName, StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals(BuildPlName, StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals(MetaJsonName, StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals(MetaYmlName, StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals(DistIniName, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> InternalPerlSubfolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "lib", "perllib", "blib", "t", "bin", "script", "scripts", "cron", "cronjobs",
        "cronjobs_old", "tools", "tests", "test", "modules", "inc", "vendor"
    };

    private static readonly HashSet<string> PerlContainerFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "lib", "perllib", "modules", "src", "app"
    };

    public bool CanContainNestedProject(
        ParsedProjectDescriptor enclosingProject,
        string childDirectoryPath,
        string[] childFiles,
        IProjectParser candidateChildParser)
    {
        if (!IProjectParser.DefaultCanContainNestedProject(enclosingProject, childDirectoryPath, candidateChildParser))
        {
            return false;
        }

        var hasExplicitManifest = HasPerlManifest(childFiles) ||
                                  candidateChildParser.ManifestFileNames.Any(m =>
                                      childFiles.Any(f => Path.GetFileName(f).Equals(m, StringComparison.OrdinalIgnoreCase)));
        if (!hasExplicitManifest)
        {
            return false;
        }

        var relToParent = Path.GetRelativePath(enclosingProject.DirectoryPath, childDirectoryPath).Replace('\\', '/').Trim('/');
        var firstSegment = relToParent.Split('/')[0];
        if (InternalPerlSubfolders.Contains(firstSegment))
        {
            return false;
        }

        return true;
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        if (HasPerlManifest(filesInDirectory))
        {
            return true;
        }

        if (!filesInDirectory.Any(IsPerlSourceFile) &&
            !IsPerlContainerRoot(directoryPath, filesInDirectory))
        {
            return false;
        }

        return !HasAncestorPerlProject(directoryPath);
    }

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        _ = filesInDirectory;
        var folderName = Path.GetFileName(directoryPath.TrimEnd(PathTrimChars));
        return string.IsNullOrWhiteSpace(folderName) ? DefaultPerlProjectName : folderName;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        _ = projectDirectory;
        return Task.FromResult<ProducedPackageInfo?>(null);
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var packages = new List<ProducedPackageInfo>();
        var cpanfilePath = Path.Combine(projectDirectory, CpanfileName);
        if (PhysicalFileStore.FileExistsInCachedDirectory(cpanfilePath))
        {
            try
            {
                var content = PhysicalFileStore.ReadAllText(cpanfilePath);
                foreach (Match m in CpanRequiresRegex.Matches(content))
                {
                    var pkg = m.Groups["pkg"].Value.Trim();
                    var ver = m.Groups["ver"].Success ? m.Groups["ver"].Value.Trim() : string.Empty;
                    if (!string.IsNullOrEmpty(pkg))
                    {
                        packages.Add(new ProducedPackageInfo(pkg, ver, CpanManifestValue));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Ignore read failure
            }
        }

        return Task.FromResult(new ProjectDependencyInfo([], packages));
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var hasManifest = HasPerlManifest(filesInDirectory);
        var folderName = Path.GetFileName(directoryPath.TrimEnd(PathTrimChars));
        var hasPlScripts = filesInDirectory.Any(f => f.EndsWith(PlExt, StringComparison.OrdinalIgnoreCase));
        var isLib = !hasPlScripts && folderName.Contains(LibFolderKeyword, StringComparison.OrdinalIgnoreCase);
        var role = isLib ? LibraryValue : ServiceValue;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [RoleKey] = role,
            [KindKey] = role,
            [EntityKindKey] = role,
            [ManifestTypeKey] = hasManifest ? CpanManifestValue : PerlDirectoryManifestValue,
            [LanguageKey] = LangPerl,
            [ProjectTypeKey] = LangPerl
        };
    }

    private static bool HasPerlManifest(string[] filesInDirectory)
    {
        for (var i = 0; i < filesInDirectory.Length; i++)
        {
            var name = Path.GetFileName(filesInDirectory[i]);
            if (name.Equals(CpanfileName, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(MakefilePlName, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(BuildPlName, StringComparison.OrdinalIgnoreCase) ||
                name.Equals(DistIniName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsPerlSourceFile(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.Equals(PlExt, StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(PmExt, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPerlContainerRoot(string directoryPath, string[] filesInDirectory)
    {
        if (!PhysicalFileStore.HasCachedDirectory(directoryPath))
        {
            return false;
        }

        var folderName = Path.GetFileName(directoryPath.TrimEnd(PathTrimChars));
        if (filesInDirectory.Length == 0 && !PerlContainerFolderNames.Contains(folderName))
        {
            return false;
        }

        var childDirs = PhysicalFileStore.GetCachedDirectChildDirectories(directoryPath);
        var perlChildCount = 0;
        for (var i = 0; i < childDirs.Count; i++)
        {
            if (childDirs[i].Files.Any(IsPerlSourceFile))
            {
                perlChildCount++;
                if (perlChildCount >= 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasAncestorPerlProject(string directoryPath)
    {
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

            var parentFiles = PhysicalFileStore.GetDirectoryFiles(current.FullName);
            if (HasPerlManifest(parentFiles) ||
                parentFiles.Any(IsPerlSourceFile) ||
                parentFiles.Any(f => f.EndsWith(".py", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".php", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".phtml", StringComparison.OrdinalIgnoreCase)) ||
                IsPerlContainerRoot(current.FullName, parentFiles))
            {
                return true;
            }

            current = current.Parent;
        }
        return false;
    }
}
