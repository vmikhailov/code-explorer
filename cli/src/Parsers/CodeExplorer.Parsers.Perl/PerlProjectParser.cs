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
        "lib", "libs", "perllib", "blib", "t", "bin", "script", "scripts", "cron", "cronjobs",
        "cronjobs_old", "CronJobs", "tools", "tests", "test", "modules", "inc", "vendor",
        "db", "date", "math", "mreg", "badman", "events", "promos", "socket", "billing",
        "payment", "pricing", "renewals", "positions", "riskRating", "action_trigger",
        "dd24_plausib_checks", "dd24Socket", "opmode", "logs-analyzer",
        "common", "wlconfig", "configParser", "gunter"
    };

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

        if (candidateChildParser.ProjectType is "perl" or "php" or "python" or "script")
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
        var segments = relToParent.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segments.Length; i++)
        {
            if (InternalPerlSubfolders.Contains(segments[i]))
            {
                return false;
            }
        }

        return true;
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        if (HasPerlManifest(filesInDirectory))
        {
            return !HasAncestorPerlProject(directoryPath);
        }

        if (PhysicalFileStore.HasChildGitRepositories(directoryPath) ||
            PhysicalFileStore.HasChildConfiguredProjects(directoryPath))
        {
            return false;
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

    private static bool HasPythonOrPhpServiceManifest(string directoryPath, string[] filesInDirectory)
    {
        for (var i = 0; i < filesInDirectory.Length; i++)
        {
            var fn = Path.GetFileName(filesInDirectory[i]);
            if (fn.Equals("composer.json", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("composer.lock", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("setup.py", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("Pipfile", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals(".coveragerc", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "requirements", "requirements.txt")) ||
               PhysicalFileStore.FileExistsInCachedDirectory(Path.Combine(directoryPath, "public", "index.php"));
    }

    private static bool IsPerlContainerRoot(string directoryPath, string[] filesInDirectory)
    {
        if (!PhysicalFileStore.HasCachedDirectory(directoryPath))
        {
            return false;
        }

        var folderName = Path.GetFileName(directoryPath.TrimEnd(PathTrimChars));
        var parentDir = Directory.GetParent(directoryPath);
        if (parentDir != null &&
            PhysicalFileStore.HasCachedDirectory(parentDir.FullName) &&
            InternalPerlSubfolders.Contains(folderName))
        {
            return false;
        }

        if (HasPythonOrPhpServiceManifest(directoryPath, filesInDirectory))
        {
            return false;
        }

        if (PhysicalFileStore.IsGitRepositoryRoot(directoryPath) &&
            !PhysicalFileStore.HasChildGitRepositories(directoryPath))
        {
            var phpCount = 0;
            var pyCount = 0;
            var perlCount = 0;
            var otherCodeCount = 0;
            var hasOtherManifest = false;
            foreach (var file in PhysicalFileStore.EnumerateCachedFilesInSubtree(directoryPath))
            {
                var fn = Path.GetFileName(file);
                var lowerFn = fn.ToLowerInvariant();
                var lowerExt = Path.GetExtension(lowerFn);
                if (lowerFn is "terragrunt.hcl" or "root.hcl" or "box.json" or "application.cfc" or "application.cfm" or "pom.xml" or "build.gradle" or "build.gradle.kts" ||
                    lowerExt is ".tf" or ".tofu" ||
                    fn.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                    fn.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
                    fn.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase) ||
                    fn.Equals("composer.json", StringComparison.OrdinalIgnoreCase) ||
                    fn.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                    fn.Equals("go.mod", StringComparison.OrdinalIgnoreCase) ||
                    fn.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase))
                {
                    hasOtherManifest = true;
                    break;
                }
                if (IsPerlSourceFile(file)) perlCount++;
                else if (file.EndsWith(".py", StringComparison.OrdinalIgnoreCase)) pyCount++;
                else if (file.EndsWith(".php", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".phtml", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".inc", StringComparison.OrdinalIgnoreCase)) phpCount++;
                else if (lowerExt is ".cs" or ".ts" or ".tsx" or ".go" or ".rs" or ".java" or ".kt" or ".cfc" or ".cfm")
                {
                    otherCodeCount++;
                }
            }
            if (!hasOtherManifest && perlCount > 0 && perlCount > phpCount && perlCount > pyCount && perlCount >= otherCodeCount)
            {
                return true;
            }
        }

        var childDirs = PhysicalFileStore.GetCachedDirectChildDirectories(directoryPath);
        var perlChildCount = 0;
        for (var i = 0; i < childDirs.Count; i++)
        {
            var childName = Path.GetFileName(childDirs[i].DirectoryPath.TrimEnd(PathTrimChars));
            if (!InternalPerlSubfolders.Contains(childName))
            {
                continue;
            }

            if (PhysicalFileStore.EnumerateCachedFilesInSubtree(childDirs[i].DirectoryPath).Any(IsPerlSourceFile))
            {
                perlChildCount++;
                if (perlChildCount >= 2)
                {
                    return true;
                }
            }
        }

        if (perlChildCount == 1 &&
            filesInDirectory.Any(f =>
            {
                var fn = Path.GetFileName(f);
                return fn.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                       fn.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
                       fn.Equals("docker-compose.yaml", StringComparison.OrdinalIgnoreCase);
            }))
        {
            return true;
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
                parentFiles.Any(f => f.EndsWith(".py", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".php", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".phtml", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)) ||
                HasPythonOrPhpServiceManifest(current.FullName, parentFiles) ||
                IsPerlContainerRoot(current.FullName, parentFiles))
            {
                return true;
            }

            current = current.Parent;
        }
        return false;
    }
}
