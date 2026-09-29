using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Extensible, thread-safe registry of project entity classifiers.
/// Evaluates concrete manifest, packaging, dependency, and AST evidence to determine
/// the physical ProjectEntityKind without ad-hoc repository-specific string hacks.
/// </summary>
public static class ProjectEntityClassifierRegistry
{
    private static readonly List<IProjectEntityClassifier> _classifiers = [];

    static ProjectEntityClassifierRegistry()
    {
        Register(new ManifestEvidenceClassifier());
        Register(new TestEvidenceClassifier());
        Register(new FrontendEvidenceClassifier());
        Register(new WorkerEvidenceClassifier());
        Register(new CliEvidenceClassifier());
        Register(new MigrationEvidenceClassifier());
        Register(new LibraryEvidenceClassifier());
    }

    public static void Register(IProjectEntityClassifier classifier)
    {
        lock (_classifiers)
        {
            if (!_classifiers.Any(c => c.GetType() == classifier.GetType()))
            {
                _classifiers.Add(classifier);
                _classifiers.Sort((a, b) => a.Order.CompareTo(b.Order));
            }
        }
    }

    public static ProjectClassification Classify(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyDictionary<string, string>? extensions = null)
    {
        // 1. Inversion of Control: query the pluggable dialect parser for this language
        var normType = (projectType ?? "").ToLowerInvariant();
        if (normType.StartsWith("net") || normType.Contains("c#") || directoryPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            normType = "csharp";
        else if (normType is "ts" or "js" or "javascript" || directoryPath.EndsWith("package.json", StringComparison.OrdinalIgnoreCase))
            normType = "typescript";
        else if (normType is "py" || directoryPath.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
            normType = "python";
        else if (normType is "golang")
            normType = "go";

        var projectContext = new Parser.ProjectContext(
            directoryPath,
            relativeProjectDir,
            projectName,
            normType,
            filesInDirectory,
            dependencies ?? [],
            extensions ?? new Dictionary<string, string>());

        var dialectParser = Parser.WorkspaceIndexer.GetProjectParser(normType) ??
                            (!string.IsNullOrEmpty(projectType) ? Parser.WorkspaceIndexer.GetProjectParser(projectType) : null);
        if (dialectParser != null)
        {
            var dialectClassification = dialectParser.ClassifyProject(projectContext);
            if (dialectClassification != null && dialectClassification.Kind != ProjectEntityKind.Unknown)
            {
                return dialectClassification;
            }
        }

        // 2. Generic evidence-based fallback classifiers
        IProjectEntityClassifier[] snapshot;
        lock (_classifiers)
        {
            snapshot = [.. _classifiers];
        }

        for (int i = 0; i < snapshot.Length; i++)
        {
            var classification = snapshot[i].Classify(
                directoryPath,
                filesInDirectory,
                relativeProjectDir,
                projectName,
                normType,
                dependencies,
                extensions);

            if (classification != null && classification.Kind != ProjectEntityKind.Unknown)
            {
                return classification;
            }
        }

        return ProjectClassification.Service;
    }
}

/// <summary>
/// Classifies projects using explicit manifest properties extracted by language project parsers.
/// </summary>
public sealed class ManifestEvidenceClassifier : IProjectEntityClassifier
{
    public int Order => 10;

    public ProjectClassification? Classify(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, string>? extensions)
    {
        if (extensions == null) return null;

        var manifestType = extensions.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
        var frameworkType = extensions.GetValueOrDefault("framework_type")?.ToLowerInvariant();
        var hasCliBin = extensions.GetValueOrDefault("has_cli_bin") == "true";
        var cloudRuntime = extensions.GetValueOrDefault("cloud_runtime");
        var normRelPath = "/" + (relativeProjectDir ?? "").Replace('\\', '/').Trim('/') + "/";

        if (manifestType == "library") return ProjectClassification.Library;
        if (manifestType == "cli" || hasCliBin) return ProjectClassification.CliApp;
        if (manifestType == "worker" || frameworkType == "worker") return ProjectClassification.Worker;
        if (manifestType == "migration") return ProjectClassification.DatabaseMigration;
        if (manifestType == "test") return ProjectClassification.Test;
        if (manifestType is "function" or "serverless") return ProjectClassification.FunctionApp;
        if (manifestType == "mobile" || frameworkType == "mobile") return ProjectClassification.MobileApp;
        if (manifestType == "desktop" || frameworkType == "desktop") return ProjectClassification.DesktopApp;

        if (cloudRuntime == "cloudflare-worker") return ProjectClassification.Worker;

        if (frameworkType == "frontend")
        {
            if (normRelPath.Contains("/src/lib/") || normRelPath.Contains("/libs/") || normRelPath.Contains("/lib/"))
            {
                return ProjectClassification.Library;
            }
            return ProjectClassification.WebApp;
        }

        if (frameworkType == "web") return ProjectClassification.Service;

        return null;
    }
}

/// <summary>
/// Classifies test suites and verification projects using test SDKs, test dependencies, and test file layouts.
/// </summary>
public sealed class TestEvidenceClassifier : IProjectEntityClassifier
{
    public int Order => 20;

    public ProjectClassification? Classify(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, string>? extensions)
    {
        var normName = (projectName ?? "").Trim().ToLowerInvariant();
        var normRelPath = "/" + (relativeProjectDir ?? "").Replace('\\', '/').Trim('/') + "/";

        if (normRelPath.Contains("/test/") || normRelPath.Contains("/tests/") ||
            normRelPath.Contains("/spec/") || normRelPath.Contains("/specs/") ||
            normRelPath.Contains("/__tests__/") || normRelPath.Contains("/e2e/"))
        {
            return ProjectClassification.Test;
        }

        if (normName.EndsWith(".tests") || normName.EndsWith("-tests") || normName.EndsWith("_tests") ||
            normName.EndsWith(".test") || normName.EndsWith("-test") || normName.EndsWith("_test") ||
            normName.EndsWith(".specs") || normName.EndsWith("-specs") ||
            normName.EndsWith(".spec") || normName.EndsWith("-spec") ||
            normName is "test" or "tests" or "spec" or "specs")
        {
            return ProjectClassification.Test;
        }

        var codeFiles = filesInDirectory.Where(f =>
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            return ext is ".ts" or ".js" or ".cs" or ".java" or ".go" or ".py";
        }).ToList();

        if (codeFiles.Count > 0 && codeFiles.All(f =>
        {
            var fn = Path.GetFileName(f).ToLowerInvariant();
            return fn.EndsWith(".test.ts") || fn.EndsWith(".spec.ts") ||
                   fn.EndsWith(".test.js") || fn.EndsWith(".spec.js") ||
                   fn.EndsWith("tests.cs") || fn.EndsWith("test.cs") ||
                   fn.EndsWith("test.java");
        }))
        {
            return ProjectClassification.Test;
        }

        return null;
    }
}

/// <summary>
/// Classifies frontend web applications using web configs and frontend framework libraries.
/// </summary>
public sealed class FrontendEvidenceClassifier : IProjectEntityClassifier
{
    public int Order => 30;

    private static readonly HashSet<string> FrontendPackageTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "react", "react-dom", "@angular/core", "vue", "svelte", "solid-js", "preact"
    };

    public ProjectClassification? Classify(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, string>? extensions)
    {
        var normRelPath = "/" + (relativeProjectDir ?? "").Replace('\\', '/').Trim('/') + "/";
        var normName = (projectName ?? "").Trim().ToLowerInvariant();

        // Standard library path or Angular library package manifest -> not a frontend application
        if (normRelPath.Contains("/src/lib/") || normRelPath.Contains("/lib/") || normRelPath.Contains("/libs/"))
        {
            return null;
        }

        if (filesInDirectory.Any(f => Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        // Web bundler and framework configs
        if (filesInDirectory.Any(f =>
        {
            var fn = Path.GetFileName(f).ToLowerInvariant();
            return fn is "vite.config.ts" or "vite.config.js" or "vite.config.mjs" or
                         "next.config.js" or "next.config.mjs" or "next.config.ts" or
                         "nuxt.config.ts" or "nuxt.config.js" or
                         "angular.json" or "vue.config.js" or "svelte.config.js";
        }))
        {
            return ProjectClassification.WebApp;
        }

        if (dependencies != null && dependencies.Any(d => FrontendPackageTokens.Contains(d)))
        {
            if (filesInDirectory.Any(f => Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase)) ||
                filesInDirectory.Any(f => Path.GetFileName(f).Equals("main.ts", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).Equals("main.tsx", StringComparison.OrdinalIgnoreCase)))
            {
                return ProjectClassification.WebApp;
            }
        }

        if (normRelPath.Contains("/frontend/") || normRelPath.Contains("/client/") || normRelPath.Contains("/web/"))
        {
            if (projectType is "typescript" or "javascript" || filesInDirectory.Any(f => Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase)))
            {
                return ProjectClassification.WebApp;
            }
        }

        if (normName.EndsWith("-ui") || normName.EndsWith("-web") || normName.EndsWith("-frontend") || normName.EndsWith("-client") || normName.EndsWith("-fe"))
        {
            return ProjectClassification.WebApp;
        }

        return null;
    }
}

/// <summary>
/// Classifies background workers, queue consumers, and schedulers.
/// </summary>
public sealed class WorkerEvidenceClassifier : IProjectEntityClassifier
{
    public int Order => 40;

    public ProjectClassification? Classify(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, string>? extensions)
    {
        var normName = (projectName ?? "").Trim().ToLowerInvariant();
        var normRelPath = "/" + (relativeProjectDir ?? "").Replace('\\', '/').Trim('/') + "/";

        if (normName.EndsWith("-worker") || normName.EndsWith("_worker") || normName.EndsWith(".worker") ||
            normName.EndsWith("-consumer") || normName.EndsWith("_consumer") || normName.EndsWith(".consumer") ||
            normName.EndsWith("-scheduler") || normName.EndsWith("_scheduler") || normName.EndsWith(".scheduler") ||
            normName.EndsWith("-jobs") || normName.EndsWith("_jobs") || normName.EndsWith(".jobs"))
        {
            return ProjectClassification.Worker;
        }

        if (normRelPath.Contains("/worker/") || normRelPath.Contains("/workers/") ||
            normRelPath.Contains("/consumer/") || normRelPath.Contains("/consumers/") ||
            normRelPath.Contains("/jobs/") || normRelPath.Contains("/scheduler/"))
        {
            return ProjectClassification.Worker;
        }

        var csproj = filesInDirectory.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csproj != null && File.Exists(csproj))
        {
            try
            {
                var content = File.ReadAllText(csproj);
                if (content.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase))
                {
                    return ProjectClassification.Worker;
                }
            }
            catch { }
        }

        return null;
    }
}

/// <summary>
/// Classifies CLI tools, console applications, and admin command utilities.
/// </summary>
public sealed class CliEvidenceClassifier : IProjectEntityClassifier
{
    public int Order => 50;

    public ProjectClassification? Classify(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, string>? extensions)
    {
        var normName = (projectName ?? "").Trim().ToLowerInvariant();
        var normRelPath = "/" + (relativeProjectDir ?? "").Replace('\\', '/').Trim('/') + "/";

        if (normName.EndsWith("-cli") || normName.EndsWith("_cli") || normName.EndsWith(".cli") ||
            normName.EndsWith("-tool") || normName.EndsWith("_tool") || normName.EndsWith(".tool") ||
            normName.EndsWith("-console") || normName.EndsWith(".console") || normName == "cli")
        {
            return ProjectClassification.CliApp;
        }

        if (normRelPath.EndsWith("/cli/") || normRelPath.Contains("/src/cli/") || normRelPath.Contains("/tools/") || normRelPath.Contains("/cmd/"))
        {
            if (!normRelPath.Contains("/src/core/") && !normRelPath.Contains("/src/parsers/") && !normRelPath.Contains("/src/cypher/") && !normRelPath.Contains("/src/libs/"))
            {
                return ProjectClassification.CliApp;
            }
        }

        return null;
    }
}

/// <summary>
/// Classifies database migration tools and SQL script projects.
/// </summary>
public sealed class MigrationEvidenceClassifier : IProjectEntityClassifier
{
    public int Order => 60;

    public ProjectClassification? Classify(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, string>? extensions)
    {
        var normName = (projectName ?? "").Trim().ToLowerInvariant();
        var normRelPath = "/" + (relativeProjectDir ?? "").Replace('\\', '/').Trim('/') + "/";

        if (projectType == "sql") return ProjectClassification.DatabaseMigration;

        if (normRelPath.Contains("/migration/") || normRelPath.Contains("/migrations/") ||
            normRelPath.Contains("/flyway/") || normRelPath.Contains("/liquibase/") ||
            normRelPath.Contains("/db-migrations/") || normRelPath.Contains("/db/migrations/"))
        {
            return ProjectClassification.DatabaseMigration;
        }

        if (normName.EndsWith("-migration") || normName.EndsWith("-migrations") ||
            normName.EndsWith(".migration") || normName.EndsWith(".migrations") ||
            normName.EndsWith("_migrations") || normName == "migrations" || normName == "flyway")
        {
            return ProjectClassification.DatabaseMigration;
        }

        return null;
    }
}

/// <summary>
/// Classifies shared libraries, domain contracts, and utility packages based on library conventions.
/// </summary>
public sealed class LibraryEvidenceClassifier : IProjectEntityClassifier
{
    public int Order => 70;

    public ProjectClassification? Classify(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies,
        IReadOnlyDictionary<string, string>? extensions)
    {
        var normName = (projectName ?? "").Trim().ToLowerInvariant();
        var normRelPath = "/" + (relativeProjectDir ?? "").Replace('\\', '/').Trim('/') + "/";

        // 1. Angular library manifest (ng-package.json or ngPackage in package.json)
        if (filesInDirectory.Any(f => Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase)))
        {
            return ProjectClassification.Library;
        }

        var pkgJson = Path.Combine(directoryPath, "package.json");
        if (File.Exists(pkgJson))
        {
            try
            {
                var content = File.ReadAllText(pkgJson);
                if (content.Contains("\"ngPackage\"", StringComparison.OrdinalIgnoreCase))
                {
                    return ProjectClassification.Library;
                }
            }
            catch { }
        }

        // 2. Standard source library directories (/src/lib/, /libs/, /lib/, /packages/)
        if (normRelPath.Contains("/src/lib/") || normRelPath.Contains("/libs/") || normRelPath.Contains("/lib/") ||
            normRelPath.Contains("/packages/") || normRelPath.Contains("/libraries/") ||
            normRelPath.Contains("/common/") || normRelPath.Contains("/shared/") || normRelPath.Contains("/contracts/") ||
            normRelPath.Contains("/dto/") || normRelPath.Contains("/dtos/"))
        {
            return ProjectClassification.Library;
        }

        // 3. Standard architectural library suffixes
        if (normName == "library" ||
            normName.EndsWith("-lib") || normName.EndsWith(".lib") ||
            normName.EndsWith(".core") || normName.EndsWith("-core") ||
            normName.EndsWith(".domain") || normName.EndsWith("-domain") ||
            normName.EndsWith(".models") || normName.EndsWith("-models") ||
            normName.EndsWith(".model") || normName.EndsWith("-model") ||
            normName.EndsWith(".entities") || normName.EndsWith("-entities") ||
            normName.EndsWith(".contracts") || normName.EndsWith("-contracts") ||
            normName.EndsWith(".dto") || normName.EndsWith(".dtos") ||
            normName.EndsWith(".types") || normName.EndsWith("-types") ||
            normName.EndsWith(".common") || normName.EndsWith("-common") ||
            normName.EndsWith(".shared") || normName.EndsWith("-shared") ||
            normName.EndsWith(".infra") || normName.EndsWith(".infrastructure") ||
            normName.EndsWith(".data") || normName.EndsWith(".db"))
        {
            return ProjectClassification.Library;
        }

        // 4. C# Class Library check (OutputType Library and not Web SDK)
        var csproj = filesInDirectory.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csproj != null && File.Exists(csproj))
        {
            try
            {
                var content = File.ReadAllText(csproj);
                var isWebSdk = content.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase);
                var isWorkerSdk = content.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase);
                var isExe = content.Contains("<OutputType>Exe</OutputType>", StringComparison.OrdinalIgnoreCase);

                if (!isWebSdk && !isWorkerSdk && !isExe)
                {
                    if (normName.EndsWith("service", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith("-service", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith(".service", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith("-api", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith(".api", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith("-server", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith(".server", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith("-gateway", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith(".gateway", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith("-backend", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith(".backend", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith("-app", StringComparison.OrdinalIgnoreCase) ||
                        normName.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    return ProjectClassification.Library;
                }
            }
            catch { }
        }

        return null;
    }
}
