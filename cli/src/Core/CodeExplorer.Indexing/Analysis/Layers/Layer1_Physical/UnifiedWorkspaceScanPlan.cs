using System.Runtime.CompilerServices;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Core.Parser.Layers;

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

    public static SubtreeLanguageCounts FromDirectoryFiles(FileInfo[] dirFiles, out bool hasSourceCodeFile)
    {
        int ts = 0, js = 0, py = 0, java = 0, kt = 0, cs = 0, go = 0, rs = 0;
        int php = 0, pl = 0, rb = 0, cpp = 0, swift = 0, tf = 0, cf = 0;
        var foundExtraSource = false;

        for (var i = 0; i < dirFiles.Length; i++)
        {
            var ext = dirFiles[i].Extension;
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
    string[] filesInDir,
    bool hasCandidateManifest)
{
    public string DirectoryPath { get; } = directoryPath;
    public string NormalizedPath { get; } = normalizedPath;
    public string RelativeDir { get; } = relativeDir;
    public bool IsRoot { get; } = isRoot;
    public string[] FilesInDir { get; } = filesInDir;
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
        if (PhysicalFileStore.IsGitRepositoryRoot(dir) ||
            (PhysicalFileStore.HasAnyConfiguredProjects && PhysicalFileStore.TryGetConfiguredProject(dir, out _)))
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
