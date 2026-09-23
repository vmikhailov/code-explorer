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
        IReadOnlyList<string>? dependencies = null)
    {
        var normName = (projectName ?? "").Trim().ToLowerInvariant();
        var normRelPath = "/" + (relativeProjectDir ?? "").Replace('\\', '/').Trim('/') + "/";
        var normProjType = (projectType ?? "").ToLowerInvariant();

        // 1. Test Project Detection
        if (IsTestProject(normName, normRelPath, filesInDirectory, dependencies))
        {
            return (ProjectRole.Test, true);
        }

        // 2. Database Migration Detection
        if (IsDatabaseMigration(normName, normRelPath, normProjType, filesInDirectory))
        {
            return (ProjectRole.DatabaseMigration, false);
        }

        // 3. Frontend Application Detection
        if (IsFrontendApp(normName, normRelPath, normProjType, filesInDirectory, dependencies))
        {
            return (ProjectRole.FrontendApp, false);
        }

        // 4. Worker / Background Consumer Detection
        if (IsWorker(normName, normRelPath, filesInDirectory))
        {
            return (ProjectRole.Worker, false);
        }

        // 5. CLI Tool Detection
        if (IsCliTool(normName, normRelPath))
        {
            return (ProjectRole.CliTool, false);
        }

        // 6. Shared Library Detection
        if (IsSharedLibrary(normName, normRelPath, filesInDirectory, normProjType))
        {
            return (ProjectRole.SharedLibrary, true);
        }

        // 7. Default to Service (executable API / container)
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
            name.EndsWith(".spec") || name.EndsWith("-spec"))
        {
            return true;
        }

        if (deps != null && deps.Any(d => TestPackageTokens.Any(token => d.Contains(token, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        if (files.Any(f =>
        {
            var fn = Path.GetFileName(f);
            return fn.EndsWith(".test.ts", StringComparison.OrdinalIgnoreCase) ||
                   fn.EndsWith(".spec.ts", StringComparison.OrdinalIgnoreCase) ||
                   fn.EndsWith(".test.js", StringComparison.OrdinalIgnoreCase) ||
                   fn.EndsWith("Tests.cs", StringComparison.OrdinalIgnoreCase) ||
                   fn.EndsWith("Test.cs", StringComparison.OrdinalIgnoreCase) ||
                   fn.EndsWith("Test.java", StringComparison.OrdinalIgnoreCase);
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
            return true;
        }

        if (relPath.Contains("/frontend/") || relPath.Contains("/client/") ||
            relPath.Contains("/web/") || relPath.Contains("/ui/"))
        {
            // If it has HTML or TypeScript/JS files, it's frontend
            if (projType is "typescript" or "javascript" || files.Any(f => Path.GetFileName(f).Equals("index.html", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        if (name.EndsWith("-ui") || name.EndsWith("-web") || name.EndsWith("-frontend") || name.EndsWith("-client"))
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
