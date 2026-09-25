using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Detects architectural roles (Service, SharedLibrary, FrontendApp, Worker, DatabaseMigration, CliTool, Test)
/// for projects at indexing time (Layer 2) using manifests, path heuristics, and dependency signals.
/// </summary>
public static class ProjectRoleDetector
{
    private static readonly HashSet<string> TestPackageTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "nunit", "xunit", "mstest", "microsoft.net.test.sdk",
        "jest", "vitest", "mocha", "cypress", "playwright",
        "pytest", "junit"
    };

    private static readonly HashSet<string> FrontendPackageTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "react", "react-dom", "@angular/core", "vue", "svelte", "solid-js", "preact"
    };

    public static (ProjectRole Role, bool IsLibrary) DetectRole(
        string directoryPath,
        string[] filesInDirectory,
        string relativeProjectDir,
        string projectName,
        string projectType,
        IReadOnlyList<string>? dependencies = null,
        IReadOnlyDictionary<string, string>? extensions = null)
    {
        var normName = (projectName ?? "").Trim().ToLowerInvariant();
        var normRelPath = "/" + (relativeProjectDir ?? "").Replace('\\', '/').Trim('/') + "/";
        var normProjType = (projectType ?? "").ToLowerInvariant();

        // 0. Explicit Manifest Evidence (Highest Priority)
        if (extensions != null)
        {
            var manifestType = extensions.GetValueOrDefault("manifest_type")?.ToLowerInvariant();
            var frameworkType = extensions.GetValueOrDefault("framework_type")?.ToLowerInvariant();
            var hasCliBin = extensions.GetValueOrDefault("has_cli_bin") == "true";
            var sdk = extensions.GetValueOrDefault("sdk");

            if (manifestType == "library")
            {
                return (ProjectRole.SharedLibrary, true);
            }
            if (manifestType == "cli" || hasCliBin)
            {
                return (ProjectRole.CliTool, false);
            }
            if (manifestType == "worker" || frameworkType == "worker" || sdk == "Microsoft.NET.Sdk.Worker")
            {
                return (ProjectRole.Worker, false);
            }
            if (frameworkType == "frontend")
            {
                if (!normRelPath.Contains("/src/lib/") && !normRelPath.Contains("/libs/") && !normRelPath.Contains("/lib/"))
                {
                    return (ProjectRole.FrontendApp, false);
                }
                return (ProjectRole.SharedLibrary, true);
            }
        }

        // 1. Test Project Detection
        if (IsTestProject(normName, normRelPath, filesInDirectory, dependencies))
        {
            return (ProjectRole.Test, true);
        }

        // 2. Angular Library / Secondary Entry Point Detection (e.g. ng-package.json, package.json with "ngPackage")
        if (filesInDirectory.Any(f => Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase)))
        {
            return (ProjectRole.SharedLibrary, true);
        }

        var pkgJson = Path.Combine(directoryPath, "package.json");
        if (File.Exists(pkgJson))
        {
            try
            {
                var content = File.ReadAllText(pkgJson);
                if (content.Contains("\"ngPackage\"", StringComparison.OrdinalIgnoreCase))
                {
                    return (ProjectRole.SharedLibrary, true);
                }
            }
            catch { }
        }

        // 3. Explicit Library Directory Paths (/src/lib/, /lib/, /libs/, /packages/)
        if (normRelPath.Contains("/src/lib/") || normRelPath.Contains("/libs/") || normRelPath.Contains("/lib/"))
        {
            return (ProjectRole.SharedLibrary, true);
        }

        // 4. Database Migration Detection
        if (IsDatabaseMigration(normName, normRelPath, normProjType, filesInDirectory))
        {
            return (ProjectRole.DatabaseMigration, false);
        }

        // 5. Frontend Application Detection
        if (IsFrontendApp(normName, normRelPath, normProjType, filesInDirectory, dependencies))
        {
            return (ProjectRole.FrontendApp, false);
        }

        // 6. Worker / Background Consumer Detection
        if (IsWorker(normName, normRelPath, filesInDirectory))
        {
            return (ProjectRole.Worker, false);
        }

        // 7. CLI Tool Detection
        if (IsCliTool(normName, normRelPath))
        {
            return (ProjectRole.CliTool, false);
        }

        // 8. Shared Library Detection (name patterns, other heuristics)
        if (IsSharedLibrary(normName, normRelPath, filesInDirectory, normProjType))
        {
            return (ProjectRole.SharedLibrary, true);
        }

        // 9. Default to Service (executable API / container)
        return (ProjectRole.Service, false);
    }

    private static bool IsTestProject(string name, string relPath, string[] files, IReadOnlyList<string>? deps)
    {
        if (relPath.Contains("/test/") || relPath.Contains("/tests/") ||
            relPath.Contains("/spec/") || relPath.Contains("/specs/") ||
            relPath.Contains("/__tests__/") || relPath.Contains("/e2e/"))
        {
            return true;
        }

        if (name.EndsWith(".tests") || name.EndsWith("-tests") || name.EndsWith("_tests") ||
            name.EndsWith(".test") || name.EndsWith("-test") || name.EndsWith("_test") ||
            name.EndsWith(".specs") || name.EndsWith("-specs") ||
            name.EndsWith(".spec") || name.EndsWith("-spec") ||
            name is "test" or "tests" or "spec" or "specs")
        {
            return true;
        }

        var codeFiles = files.Where(f =>
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
            return true;
        }

        return false;
    }

    private static bool IsDatabaseMigration(string name, string relPath, string projType, string[] files)
    {
        if (projType == "sql") return true;

        if (relPath.Contains("/migration/") || relPath.Contains("/migrations/") ||
            relPath.Contains("/flyway/") || relPath.Contains("/liquibase/") ||
            relPath.Contains("/db-migrations/") || relPath.Contains("/db/migrations/"))
        {
            return true;
        }

        if (name.EndsWith("-migration") || name.EndsWith("-migrations") ||
            name.EndsWith(".migration") || name.EndsWith(".migrations") ||
            name.EndsWith("_migrations") || name == "migrations" || name == "flyway")
        {
            return true;
        }

        return false;
    }

    private static bool IsFrontendApp(string name, string relPath, string projType, string[] files, IReadOnlyList<string>? deps)
    {
        if (relPath.Contains("/src/lib/") || relPath.Contains("/lib/") || relPath.Contains("/libs/"))
        {
            return false;
        }

        if (files.Any(f => Path.GetFileName(f).Equals("ng-package.json", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (files.Any(f =>
        {
            var fn = Path.GetFileName(f).ToLowerInvariant();
            return fn is "vite.config.ts" or "vite.config.js" or "vite.config.mjs" or
                         "next.config.js" or "next.config.mjs" or "next.config.ts" or
                         "nuxt.config.ts" or "nuxt.config.js" or
                         "angular.json" or "vue.config.js" or "svelte.config.js";
        }))
        {
            return true;
        }

        if (deps != null && deps.Any(d => FrontendPackageTokens.Any(token => d.Equals(token, StringComparison.OrdinalIgnoreCase))))
        {
            if (files.Any(f => Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase)) ||
                files.Any(f => Path.GetFileName(f).Equals("main.ts", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(f).Equals("main.tsx", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (relPath.Contains("/frontend/") || relPath.Contains("/client/") ||
            relPath.Contains("/web/"))
        {
            // If it has HTML or TypeScript/JS files, it's frontend
            if (projType is "typescript" or "javascript" || files.Any(f => Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (name.EndsWith("-ui") || name.EndsWith("-web") || name.EndsWith("-frontend") || name.EndsWith("-client") || name.EndsWith("-fe") || name.EndsWith("-landings"))
        {
            return true;
        }

        return false;
    }

    private static bool IsWorker(string name, string relPath, string[] files)
    {
        if (name.EndsWith("-worker") || name.EndsWith("_worker") || name.EndsWith(".worker") ||
            name.EndsWith("-consumer") || name.EndsWith("_consumer") || name.EndsWith(".consumer") ||
            name.EndsWith("-scheduler") || name.EndsWith("_scheduler") || name.EndsWith(".scheduler") ||
            name.EndsWith("-jobs") || name.EndsWith("_jobs") || name.EndsWith(".jobs"))
        {
            return true;
        }

        if (relPath.Contains("/worker/") || relPath.Contains("/workers/") ||
            relPath.Contains("/consumer/") || relPath.Contains("/consumers/") ||
            relPath.Contains("/jobs/") || relPath.Contains("/scheduler/"))
        {
            return true;
        }

        // C# Worker Service check
        var csproj = files.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
        if (csproj != null && File.Exists(csproj))
        {
            try
            {
                var content = File.ReadAllText(csproj);
                if (content.Contains("Microsoft.NET.Sdk.Worker", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch { }
        }

        return false;
    }

    private static bool IsCliTool(string name, string relPath)
    {
        if (name.EndsWith("-cli") || name.EndsWith("_cli") || name.EndsWith(".cli") ||
            name.EndsWith("-tool") || name.EndsWith("_tool") || name.EndsWith(".tool") ||
            name.EndsWith("-console") || name.EndsWith(".console") || name == "cli")
        {
            return true;
        }

        if (relPath.Contains("/cli/") || relPath.Contains("/tools/"))
        {
            return true;
        }

        return false;
    }

    private static bool IsSharedLibrary(string name, string relPath, string[] files, string projType)
    {
        // 1. Explicit library directory paths
        if (relPath.Contains("/libs/") || relPath.Contains("/lib/") || relPath.Contains("/libraries/") ||
            relPath.Contains("/common/") || relPath.Contains("/shared/") || relPath.Contains("/contracts/") ||
            relPath.Contains("/dto/") || relPath.Contains("/dtos/") || relPath.Contains("/packages/"))
        {
            return true;
        }

        // 2. Explicit library name patterns
        if (name == "library" || name.Contains("library") ||
            name.EndsWith("-lib") || name.EndsWith(".lib") ||
            name.EndsWith(".core") || name.EndsWith("-core") ||
            name.EndsWith(".domain") || name.EndsWith("-domain") ||
            name.EndsWith(".models") || name.EndsWith("-models") ||
            name.EndsWith(".model") || name.EndsWith("-model") ||
            name.EndsWith(".entities") || name.EndsWith("-entities") ||
            name.EndsWith(".contracts") || name.EndsWith("-contracts") ||
            name.EndsWith(".dto") || name.EndsWith(".dtos") ||
            name.EndsWith(".types") || name.EndsWith("-types") ||
            name.EndsWith(".common") || name.EndsWith("-common") ||
            name.EndsWith(".shared") || name.EndsWith("-shared") ||
            name.EndsWith(".infra") || name.EndsWith(".infrastructure") ||
            name.EndsWith(".data") || name.EndsWith(".db"))
        {
            return true;
        }

        // 3. C# Class Library check (OutputType Library and not Web SDK)
        var csproj = files.FirstOrDefault(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
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
                    // If not Web, Worker, or Exe, it's a library
                    return true;
                }
            }
            catch { }
        }

        return false;
    }
}
