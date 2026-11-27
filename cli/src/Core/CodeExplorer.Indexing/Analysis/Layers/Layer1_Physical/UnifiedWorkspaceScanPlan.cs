using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

internal readonly record struct DirectoryFileEntry(
    string Name,
    string FullPath,
    long SizeBytes,
    DateTime LastModifiedUtc);

internal static class SinglePassDirectoryReader
{
    private const string DotGitName = ".git";
    private const string DotGitIgnoreName = ".gitignore";
    private const string DotCeIgnoreName = ".codeexplorerignore";
    private const string DotVscodeName = ".vscode";

    private static readonly EnumerationOptions SingleDirOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false
    };

    private readonly record struct RawEntry(
        bool IsDirectory,
        string Name,
        string FullPath,
        long SizeBytes,
        DateTime LastModifiedUtc);

    public static void ReadEntries(
        string currentDir,
        out DirectoryFileEntry[] files,
        out string[] filePaths,
        out string[] subDirPaths,
        out bool hasGitPointer,
        out bool hasGitSubDir,
        out bool hasVscodeSubDir,
        out string? gitIgnorePath,
        out string? ceIgnorePath)
    {
        hasGitPointer = false;
        hasGitSubDir = false;
        hasVscodeSubDir = false;
        gitIgnorePath = null;
        ceIgnorePath = null;

        var fileList = new List<DirectoryFileEntry>();
        var subDirList = new List<(string Name, string FullPath)>();

        var enumerable = new FileSystemEnumerable<RawEntry>(
            currentDir,
            static (ref FileSystemEntry entry) => new RawEntry(
                entry.IsDirectory,
                entry.FileName.ToString(),
                entry.ToFullPath(),
                entry.IsDirectory ? 0L : entry.Length,
                entry.LastWriteTimeUtc.UtcDateTime),
            SingleDirOptions);

        foreach (var raw in enumerable)
        {
            if (raw.IsDirectory)
            {
                if (raw.Name.Equals(DotGitName, StringComparison.OrdinalIgnoreCase)) hasGitSubDir = true;
                else if (raw.Name.Equals(DotVscodeName, StringComparison.OrdinalIgnoreCase)) hasVscodeSubDir = true;
                subDirList.Add((raw.Name, raw.FullPath));
            }
            else
            {
                if (raw.Name.Equals(DotGitName, StringComparison.OrdinalIgnoreCase)) hasGitPointer = true;
                else if (raw.Name.Equals(DotGitIgnoreName, StringComparison.OrdinalIgnoreCase)) gitIgnorePath = raw.FullPath;
                else if (raw.Name.Equals(DotCeIgnoreName, StringComparison.OrdinalIgnoreCase)) ceIgnorePath = raw.FullPath;
                fileList.Add(new DirectoryFileEntry(raw.Name, raw.FullPath, raw.SizeBytes, raw.LastModifiedUtc));
            }
        }

        if (fileList.Count == 0)
        {
            files = [];
            filePaths = [];
        }
        else
        {
            fileList.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            files = fileList.ToArray();
            filePaths = new string[files.Length];
            for (var i = 0; i < files.Length; i++) filePaths[i] = files[i].FullPath;
        }

        if (subDirList.Count == 0)
        {
            subDirPaths = [];
        }
        else
        {
            subDirList.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
            subDirPaths = new string[subDirList.Count];
            for (var i = 0; i < subDirList.Count; i++) subDirPaths[i] = subDirList[i].FullPath;
        }
    }
}

internal readonly record struct SubtreeLanguageCounts(
    int TypeScript,
    int JavaScript,
    int Python,
    int Java,
    int Kotlin,
    int CSharp,
    int Go,
    int Rust,
    int Php,
    int Perl,
    int Ruby,
    int Cpp,
    int Swift,
    int Terraform,
    int ColdFusion)
{
    private static readonly HashSet<string> ExtraSourceCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vue", ".svelte", ".fs", ".vb", ".scala", ".dart", ".sql", ".graphql", ".gql", ".proto"
    };

    public SubtreeLanguageCounts Add(in SubtreeLanguageCounts other) => new(
        TypeScript + other.TypeScript,
        JavaScript + other.JavaScript,
        Python + other.Python,
        Java + other.Java,
        Kotlin + other.Kotlin,
        CSharp + other.CSharp,
        Go + other.Go,
        Rust + other.Rust,
        Php + other.Php,
        Perl + other.Perl,
        Ruby + other.Ruby,
        Cpp + other.Cpp,
        Swift + other.Swift,
        Terraform + other.Terraform,
        ColdFusion + other.ColdFusion);

    public bool HasAnyCount =>
        (TypeScript | JavaScript | Python | Java | Kotlin | CSharp | Go | Rust |
         Php | Perl | Ruby | Cpp | Swift | Terraform | ColdFusion) != 0;

    public static SubtreeLanguageCounts FromDirectoryFiles(DirectoryFileEntry[] dirFiles, out bool hasSourceCodeFile)
    {
        int ts = 0, js = 0, py = 0, java = 0, kt = 0, cs = 0, go = 0, rs = 0;
        int php = 0, pl = 0, rb = 0, cpp = 0, swift = 0, tf = 0, cf = 0;
        var foundExtraSource = false;

        for (var i = 0; i < dirFiles.Length; i++)
        {
            var ext = Path.GetExtension(dirFiles[i].Name);
            if (string.IsNullOrEmpty(ext)) continue;

            switch (ext.ToLowerInvariant())
            {
                case ".ts" or ".tsx" or ".mts" or ".cts":
                    ts++;
                    break;
                case ".js" or ".jsx" or ".mjs" or ".cjs":
                    js++;
                    break;
                case ".py" or ".pyi":
                    py++;
                    break;
                case ".java" or ".groovy":
                    java++;
                    break;
                case ".kt" or ".kts":
                    kt++;
                    break;
                case ".cs" or ".razor" or ".cshtml":
                    cs++;
                    break;
                case ".go":
                    go++;
                    break;
                case ".rs":
                    rs++;
                    break;
                case ".php" or ".phtml" or ".inc":
                    php++;
                    break;
                case ".pl" or ".pm":
                    pl++;
                    break;
                case ".rb":
                    rb++;
                    break;
                case ".c" or ".cc" or ".cpp" or ".cxx" or ".h" or ".hpp":
                    cpp++;
                    break;
                case ".swift":
                    swift++;
                    break;
                case ".tf" or ".tfvars" or ".hcl" or ".tofu":
                    tf++;
                    break;
                case ".cfc" or ".cfm":
                    cf++;
                    break;
                default:
                    if (!foundExtraSource && ExtraSourceCodeExtensions.Contains(ext))
                    {
                        foundExtraSource = true;
                    }
                    break;
            }
        }

        var counts = new SubtreeLanguageCounts(ts, js, py, java, kt, cs, go, rs, php, pl, rb, cpp, swift, tf, cf);
        hasSourceCodeFile = counts.HasAnyCount || foundExtraSource;
        return counts;
    }
}

internal sealed class ScopedDirectoryTreeNode(
    string directoryPath,
    string normalizedPath,
    string relativeDir,
    bool isRoot,
    DirectoryFileEntry[] dirFiles,
    string[] filesInDir,
    IOntologyNode fileParentNode,
    FolderNode? folderNode,
    GitIgnoreScopeChain gitScope,
    bool keptForNegation,
    bool hasManifestInDir,
    bool hasCandidateManifest)
{
    public string DirectoryPath { get; } = directoryPath;
    public string NormalizedPath { get; } = normalizedPath;
    public string RelativeDir { get; } = relativeDir;
    public bool IsRoot { get; } = isRoot;
    public DirectoryFileEntry[] DirFiles { get; } = dirFiles;
    public string[] FilesInDir { get; } = filesInDir;
    public IOntologyNode FileParentNode { get; } = fileParentNode;
    public FolderNode? FolderNode { get; } = folderNode;
    public GitIgnoreScopeChain GitScope { get; } = gitScope;
    public bool KeptForNegation { get; } = keptForNegation;
    public bool HasManifestInDir { get; } = hasManifestInDir;
    public bool HasCandidateManifest { get; set; } = hasCandidateManifest;
    public bool SubtreeHasCandidateManifest { get; set; } = hasCandidateManifest;
    public bool IsGitRepoRoot { get; set; }
    public int DescendantGitRepoCount { get; set; }
    public bool HasSourceFiles { get; set; }
    public SubtreeLanguageCounts LanguageCounts { get; set; }
    public List<ScopedDirectoryTreeNode> Children { get; } = [];
}

internal sealed record UnifiedWorkspaceScanPlan(
    IReadOnlyList<ScopedDirectoryTreeNode> RootNodes,
    IReadOnlyList<string> ManifestDirectories,
    IReadOnlyList<string> FallbackCandidateDirectories);

internal static class UnifiedScanPlanStore
{
    private static readonly ConditionalWeakTable<Layer1Result, UnifiedWorkspaceScanPlan> Plans = new();

    public static void Attach(Layer1Result result, UnifiedWorkspaceScanPlan plan) =>
        Plans.AddOrUpdate(result, plan);

    public static bool TryGet(Layer1Result result, out UnifiedWorkspaceScanPlan? plan) =>
        Plans.TryGetValue(result, out plan);
}

internal static class ProjectTriggerDetector
{
    private const string BuiltInParserNamespacePrefix = "CodeExplorer.Parsers";
    private const string BinFolderName = "bin";

    internal static readonly HashSet<string> CandidateTriggerFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "package.json", "tsconfig.json", "tsconfig.base.json", "jsconfig.json",
        "project.json", "ng-package.json", "wrangler.toml", "wrangler.jsonc",
        "wrangler.json", "angular.json", "nest-cli.json",
        "pom.xml", "build.gradle", "build.gradle.kts", "settings.gradle", "settings.gradle.kts", "AndroidManifest.xml",
        "go.mod", "Cargo.toml",
        "requirements.txt", "pyproject.toml", "setup.py", "setup.cfg", "Pipfile", "alembic.ini", "manage.py",
        "composer.json", "composer.lock",
        "cpanfile", "Makefile.PL", "Build.PL", "META.json", "META.yml", "dist.ini",
        "terragrunt.hcl", "root.hcl",
        "box.json", "Application.cfc", "Application.cfm",
        "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", "packages.config",
        "Dockerfile", "compose.yaml", "compose.yml", "docker-compose.yml", "docker-compose.yaml"
    };

    internal static readonly HashSet<string> CandidateTriggerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csproj", ".fsproj", ".vbproj", ".nuspec",
        ".tf", ".tofu",
        ".cfc", ".cfm", ".cfml",
        ".php", ".phtml", ".inc",
        ".pl", ".pm", ".t", ".psgi",
        ".py"
    };

    public static bool HasCandidateProjectTrigger(string dir, string[] filesInDir)
    {
        if (WorkspaceDirectoryIndex.IsGitRepoRootInCurrent(dir) ||
            (WorkspaceDirectoryIndex.HasAnyConfiguredProjectsInCurrent && WorkspaceDirectoryIndex.TryGetConfiguredProjectInCurrent(dir, out _)))
        {
            return true;
        }

        if (filesInDir.Length == 0)
        {
            return IsPotentialManifestlessBinarySubfolder(dir);
        }

        for (var i = 0; i < filesInDir.Length; i++)
        {
            var fileName = Path.GetFileName(filesInDir[i]);
            if (CandidateTriggerFileNames.Contains(fileName))
            {
                return true;
            }

            var ext = Path.GetExtension(fileName);
            if (!string.IsNullOrEmpty(ext) && CandidateTriggerExtensions.Contains(ext))
            {
                return true;
            }
        }

        return IsPotentialManifestlessBinarySubfolder(dir) ||
               WorkspaceIndexer._projectParsers.Any(p => !p.IsFallback && p.GetType().Namespace?.StartsWith(BuiltInParserNamespacePrefix, StringComparison.Ordinal) != true);
    }

    public static bool IsPotentialManifestlessBinarySubfolder(string dir)
    {
        var parentDir = Path.GetDirectoryName(dir.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(parentDir)) return false;
        var parentName = Path.GetFileName(parentDir);
        return parentName.Equals(BinFolderName, StringComparison.OrdinalIgnoreCase);
    }
}
