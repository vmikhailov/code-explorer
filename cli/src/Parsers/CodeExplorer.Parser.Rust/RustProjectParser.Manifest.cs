using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Parser.Rust;

public partial class RustProjectParser
{
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
        }

        return IProjectParser.DefaultClassifyProject(this, context);
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var normPath = "/" + directoryPath.Replace('\\', '/').Trim('/') + "/";
        var dirName = Path.GetFileName(directoryPath.TrimEnd('/', '\\')).ToLowerInvariant();
        var isTestDir = dirName is "test" or "tests" or "benches" ||
                        normPath.EndsWith("/tests/") || normPath.EndsWith("/benches/") ||
                        normPath.EndsWith("/e2e/") || normPath.EndsWith("/qa/");

        if (isTestDir)
        {
            props["manifest_type"] = "test";
            props["is_test_project"] = "true";
            props["primary_role"] = "Test";
            props["role"] = "Test";
            props["kind"] = "TestSuite";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Test" });
            return props;
        }

        // Check standalone binary subfolder (e.g. iris-mpc-bins/bin/retention-reaper)
        if (IsStandaloneBinarySubfolder(directoryPath, out var matchingBinName, out var gpCargo) && gpCargo != null)
        {
            return ExtractStandaloneManifestProperties(directoryPath, filesInDirectory, matchingBinName, gpCargo, dirName, props);
        }

        var cargoTomlPath = Path.Combine(directoryPath, "Cargo.toml");
        if (!File.Exists(cargoTomlPath))
        {
            return props;
        }

        CargoManifest? manifest = null;
        try
        {
            manifest = GetOrLoadManifest(cargoTomlPath);
        }
        catch
        {
            return props;
        }

        if (manifest == null) return props;

        // Check virtual workspace with no package
        if (manifest.IsVirtualWorkspace)
        {
            props["manifest_type"] = "workspace";
            props["is_virtual_workspace"] = "true";
            return props;
        }

        // Check if pure test harness (or crate whose dependencies are solely test frameworks with no lib/bin target)
        var devAndRuntimeDepNames = manifest.Dependencies.Concat(manifest.DevDependencies).Select(d => d.Name).ToList();
        if (manifest.LibTarget == null && manifest.BinTargets.Count == 0 &&
            ((manifest.Dependencies.Count == 0 && manifest.DevDependencies.Count > 0) ||
             ((IProjectParser)this).HasTestFrameworkDependency(devAndRuntimeDepNames)))
        {
            props["manifest_type"] = "test";
            props["is_test_project"] = "true";
            props["primary_role"] = "Test";
            props["role"] = "Test";
            props["kind"] = "TestSuite";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Test" });
            return props;
        }

        var allDeps = manifest.Dependencies
            .Concat(manifest.DevDependencies)
            .Concat(manifest.BuildDependencies)
            .ToList();

        var allDepNames = new HashSet<string>(allDeps.Select(d => d.Name.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);
        var runtimeDepNames = new HashSet<string>(manifest.Dependencies.Select(d => d.Name.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

        var isWeb = allDepNames.Any(d => d is "axum" or "actix-web" or "actix_web" or "rocket" or "warp" or "poem" or "salvo");
        var isGrpc = allDepNames.Any(d => d is "tonic" or "tarpc" or "grpc");
        var isCliDep = allDepNames.Any(d => d is "clap" or "clap_builder" or "structopt" or "argh" or "pico-args" or "lexopt" or "ratatui" or "crossterm" or "dialoguer");
        var isWorkerDep = allDepNames.Any(d => d is "aws-sdk-sqs" or "rdkafka" or "lapin" or "async-nats" or "async_nats" or "fang" or "apalis" or "tokio-cron-scheduler");
        var isMigrationDep = allDepNames.Any(d => d is "refinery" or "diesel_migrations" or "diesel-migrations");
        var crateName = manifest.Package?.Name ?? dirName;
        var hasSubsystemCapability = HasArchitecturalSubsystemCapability(directoryPath, runtimeDepNames, crateName);

        var migrationsDir = Path.Combine(directoryPath, "migrations");
        var hasMigrations = isMigrationDep ||
                            (Directory.Exists(migrationsDir) && Directory.EnumerateFiles(migrationsDir, "*.sql", SearchOption.AllDirectories).Any());

        // Model a package with multiple executable targets as a container for those targets,
        // not as a single service inferred from the union of all binary dependencies.
        if (manifest.BinTargets.Count > 1)
        {
            var role = manifest.LibTarget is null ? "CliTool" : "Library";
            props["primary_role"] = role;
            props["role"] = role;
            props["kind"] = role;
            props["manifest_type"] = manifest.LibTarget is null ? "cli" : "library";
            props["framework_type"] = manifest.LibTarget is null ? "cli" : "library";
            props["is_library"] = manifest.LibTarget is null ? "false" : "true";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { role });
            return props;
        }

        var hasDirectBin = manifest.BinTargets.Count > 0 ||
                           File.Exists(Path.Combine(directoryPath, "src", "main.rs")) ||
                           Directory.Exists(Path.Combine(directoryPath, "src", "bin"));
        var hasLibCapability = manifest.LibTarget != null || File.Exists(Path.Combine(directoryPath, "src", "lib.rs"));

        // Inspect companion *-bins crate in workspace
        var companionBinFiles = new List<string>();
        var parentDir = Directory.GetParent(directoryPath)?.FullName;
        if (parentDir != null)
        {
            try
            {
                foreach (var sibDir in Directory.EnumerateDirectories(parentDir))
                {
                    var sibName = Path.GetFileName(sibDir);
                    if (sibName.EndsWith("-bins", StringComparison.OrdinalIgnoreCase) ||
                        sibName.EndsWith("_bins", StringComparison.OrdinalIgnoreCase) ||
                        sibName.Equals("bins", StringComparison.OrdinalIgnoreCase))
                    {
                        var companionSubDir = Path.Combine(sibDir, "bin", crateName);
                        if (!Directory.Exists(companionSubDir))
                        {
                            companionSubDir = Path.Combine(sibDir, "bin", crateName.Replace('-', '_'));
                        }
                        if (Directory.Exists(companionSubDir))
                        {
                            companionBinFiles.AddRange(Directory.EnumerateFiles(companionSubDir, "*.rs", SearchOption.AllDirectories));
                        }
                    }
                }
            }
            catch
            {
                // ignored: inaccessible sibling directory skipped during companion binary discovery
            }
        }

        var hasCompanionBin = companionBinFiles.Count > 0;
        var hasCompanionServer = companionBinFiles.Any(f => {
            var fn = Path.GetFileName(f).ToLowerInvariant();
            return fn.Contains("server") || fn.Contains("api") || fn.Contains("gateway");
        });
        var hasCompanionWorker = companionBinFiles.Any(f => {
            var fn = Path.GetFileName(f).ToLowerInvariant();
            return fn.Contains("genesis") || fn.Contains("reaper") || fn.Contains("rerandomize") || fn.Contains("consumer");
        });

        // Library crates with their own architectural capability remain subsystems even when
        // sibling binaries consume them; those binaries are modeled as separate projects.
        if (hasLibCapability && !hasDirectBin && hasSubsystemCapability)
        {
            props["primary_role"] = "Library";
            props["role"] = "Library";
            props["kind"] = "Library";
            props["manifest_type"] = "library";
            props["framework_type"] = "library";
            props["is_library"] = "true";
            props["architectural_subsystem"] = "true";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Library" });
            return props;
        }

        // Pure library check:
        // If crate has lib capability, NO direct bin targets, and NO companion server/worker binaries:
        if (hasLibCapability && !hasDirectBin && !hasCompanionServer && !hasCompanionWorker)
        {
            props["primary_role"] = "Library";
            props["role"] = "Library";
            props["kind"] = "Library";
            props["manifest_type"] = "library";
            props["framework_type"] = "library";
            props["is_library"] = "true";
            props["secondary_roles"] = "[]";
            props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { "Library" });
            return props;
        }

        var localRsFiles = filesInDirectory.Where(f => f.EndsWith(".rs", StringComparison.OrdinalIgnoreCase)).ToList();
        var allScanFiles = localRsFiles.Concat(companionBinFiles).ToList();
        var combinedSourceText = string.Join(" ", allScanFiles.Select(f => {
            try { return PhysicalFileStore.ReadAllText(f); } catch { return ""; }
        }));

        var hasServerCode = hasCompanionServer || (hasDirectBin && (isWeb || isGrpc || combinedSourceText.Contains("axum::Router") || combinedSourceText.Contains("start_coordination_server")));
        var hasWorkerCode = hasCompanionWorker || isWorkerDep ||
            dirName.Contains("worker") || dirName.Contains("consumer") || dirName.Contains("scheduler") || dirName.Contains("reaper") ||
            (hasDirectBin && (combinedSourceText.Contains("CronJob") || combinedSourceText.Contains("receive_message")));
        var hasCliCode = (hasDirectBin || hasCompanionBin) && (isCliDep ||
            companionBinFiles.Any(f => {
                var fn = Path.GetFileName(f).ToLowerInvariant();
                return fn.Contains("client") || fn.Contains("cli") || fn.Contains("tool");
            }) || combinedSourceText.Contains("clap::Parser"));

        var hasServiceCapability = (hasDirectBin && (isWeb || isGrpc || hasServerCode)) || hasCompanionServer;
        var hasWorkerCapability = hasWorkerCode;
        var hasCliCapability = hasCliCode;

        string primaryRole;
        if (dirName.Contains("reaper") || dirName.Contains("upgrade-hawk") || (!hasServiceCapability && hasWorkerCapability))
        {
            primaryRole = "Worker";
        }
        else if (hasServiceCapability)
        {
            primaryRole = "Service";
        }
        else if (hasWorkerCapability)
        {
            primaryRole = "Worker";
        }
        else if (hasCliCapability)
        {
            primaryRole = "CliTool";
        }
        else if (hasLibCapability)
        {
            primaryRole = "Library";
        }
        else
        {
            primaryRole = "Library";
        }

        var detectedRoles = new List<string>();
        if (hasServiceCapability) detectedRoles.Add("Service");
        if (hasWorkerCapability) detectedRoles.Add("Worker");
        if (hasCliCapability) detectedRoles.Add("CliTool");
        if (hasLibCapability) detectedRoles.Add("Library");

        var secondaryRoles = detectedRoles
            .Where(r => !string.Equals(r, primaryRole, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        var isLibrary = primaryRole == "Library" && secondaryRoles.Count == 0;

        props["primary_role"] = primaryRole;
        props["role"] = primaryRole;
        props["kind"] = primaryRole;
        props["manifest_type"] = primaryRole == "Service" ? "application" : (primaryRole == "Worker" ? "worker" : (primaryRole == "Library" ? "library" : "cli"));
        props["framework_type"] = primaryRole == "Service" ? (isGrpc ? "api" : "web") : (primaryRole == "Worker" ? "worker" : (primaryRole == "Library" ? "library" : "cli"));
        props["is_library"] = isLibrary ? "true" : "false";
        props["secondary_roles"] = System.Text.Json.JsonSerializer.Serialize(secondaryRoles);
        props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { primaryRole }.Concat(secondaryRoles).Distinct());

        return props;
    }

    private static bool HasArchitecturalSubsystemCapability(
        string directoryPath,
        HashSet<string> dependencyNames,
        string crateName)
    {
        var nameTokens = crateName.Split(['-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        var hasDomainName = nameTokens.Any(token => token is "cpu" or "gpu" or "store" or
            "engine" or "runtime" or "upgrade" or "protocol" or "network" or "database" or "persistence");
        var hasUtilityName = nameTokens.Any(token => token is "common" or "utils" or "utilities");
        if (hasUtilityName && !hasDomainName)
        {
            return false;
        }

        var hasSubsystemName = hasDomainName || nameTokens.Contains("mpc", StringComparer.OrdinalIgnoreCase);
        var hasStrongSubsystemDependency = dependencyNames.Contains("cudarc") ||
                                           dependencyNames.Contains("sqlx") ||
                                           dependencyNames.Contains("tonic");
        var hasQueueDependency = dependencyNames.Contains("aws-sdk-sqs");
        var hasInfrastructureDependency = hasStrongSubsystemDependency || (hasQueueDependency && !hasUtilityName);
        var shouldMeasureSize = hasSubsystemName;

        if (!hasInfrastructureDependency && !shouldMeasureSize)
        {
            return false;
        }

        var sourceFiles = Directory.EnumerateFiles(directoryPath, "*.rs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
                                segment.Equals("benches", StringComparison.OrdinalIgnoreCase) ||
                                segment.Equals("target", StringComparison.OrdinalIgnoreCase) ||
                                segment.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        var source = string.Join('\n', sourceFiles.Select(PhysicalFileStore.ReadAllText));
        var nonEmptyLines = source.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line));

        if (shouldMeasureSize && nonEmptyLines >= 1500)
        {
            return true;
        }

        if (!hasInfrastructureDependency)
        {
            return false;
        }

        return dependencyNames.Contains("cudarc") ||
               (dependencyNames.Contains("sqlx") &&
                (source.Contains("PgPoolOptions", StringComparison.Ordinal) ||
                 source.Contains("PoolOptions", StringComparison.Ordinal) ||
                 source.Contains("PgConnection", StringComparison.Ordinal) ||
                 source.Contains("sqlx::migrate!", StringComparison.Ordinal))) ||
               (dependencyNames.Contains("tonic") &&
                (source.Contains("Server::builder", StringComparison.Ordinal) ||
                 source.Contains("TcpListener::bind", StringComparison.Ordinal) ||
                 source.Contains("Endpoint::connect", StringComparison.Ordinal))) ||
               (hasQueueDependency && !hasUtilityName &&
                (source.Contains("receive_message", StringComparison.Ordinal) ||
                 source.Contains("send_message", StringComparison.Ordinal)));
    }

    private Dictionary<string, string> ExtractStandaloneManifestProperties(
        string directoryPath,
        string[] filesInDirectory,
        string? matchingBinName,
        string gpCargo,
        string dirName,
        Dictionary<string, string> props)
    {
        var gpManifest = GetOrLoadManifest(gpCargo);
        var binName = (matchingBinName ?? dirName).ToLowerInvariant();

        var allDeps = gpManifest != null ? gpManifest.Dependencies : new List<CargoDependency>();
        var usedCrates = ScanUsedCrates(directoryPath);
        var activeDeps = allDeps.Where(d => usedCrates.Count == 0 || usedCrates.Contains(d.Name.Replace('-', '_')) || usedCrates.Contains(d.Name)).ToList();
        var depNames = new HashSet<string>(activeDeps.Select(d => d.Name.ToLowerInvariant()), StringComparer.OrdinalIgnoreCase);

        var isWeb = depNames.Any(d => d is "axum" or "actix-web" or "actix_web" or "rocket" or "warp" or "poem" or "salvo");
        var isGrpc = depNames.Any(d => d is "tonic" or "tarpc" or "grpc");
        var isCliDep = depNames.Any(d => d is "clap" or "clap_builder" or "structopt" or "argh" or "pico-args" or "lexopt");
        var isWorkerDep = depNames.Any(d => d is "aws-sdk-sqs" or "rdkafka" or "lapin" or "async-nats" or "async_nats" or "fang" or "apalis" or "tokio-cron-scheduler");

        var rsFiles = filesInDirectory.Where(f => f.EndsWith(".rs", StringComparison.OrdinalIgnoreCase)).ToList();
        var sourceText = string.Join(" ", rsFiles.Select(f => {
            try { return PhysicalFileStore.ReadAllText(f); } catch { return ""; }
        }));

        var hasServerCode = isWeb || isGrpc || sourceText.Contains("start_coordination_server") || sourceText.Contains("axum::Router") || binName.Contains("server");
        var hasWorkerCode = isWorkerDep || binName.Contains("reaper") || binName.Contains("worker") || binName.Contains("consumer") || binName.Contains("cron") || sourceText.Contains("CronJob") || sourceText.Contains("receive_message");
        var hasCliCode = isCliDep || binName.EndsWith("-cli") || binName.EndsWith("_cli") || binName == "cli" || sourceText.Contains("clap::Parser") || sourceText.Contains("Parser::parse");

        var hasServiceCapability = hasServerCode;
        var hasWorkerCapability = hasWorkerCode;
        var hasCliCapability = hasCliCode;

        string primaryRole;
        if (binName.Contains("reaper") || binName.Contains("cron") || (!hasServiceCapability && hasWorkerCapability))
        {
            primaryRole = "Worker";
        }
        else if (hasServiceCapability)
        {
            primaryRole = "Service";
        }
        else if (hasWorkerCapability)
        {
            primaryRole = "Worker";
        }
        else
        {
            primaryRole = "CliTool";
        }

        var detectedRoles = new List<string>();
        if (hasServiceCapability) detectedRoles.Add("Service");
        if (hasWorkerCapability) detectedRoles.Add("Worker");
        if (hasCliCapability) detectedRoles.Add("CliTool");

        var secondaryRoles = detectedRoles
            .Where(r => !string.Equals(r, primaryRole, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        props["primary_role"] = primaryRole;
        props["role"] = primaryRole;
        props["kind"] = primaryRole;
        props["manifest_type"] = primaryRole == "Service" ? "application" : (primaryRole == "Worker" ? "worker" : "cli");
        props["framework_type"] = primaryRole == "Service" ? (isGrpc ? "api" : "web") : (primaryRole == "Worker" ? "worker" : "cli");
        props["is_library"] = "false";
        props["secondary_roles"] = System.Text.Json.JsonSerializer.Serialize(secondaryRoles);
        props["all_roles"] = System.Text.Json.JsonSerializer.Serialize(new[] { primaryRole }.Concat(secondaryRoles).Distinct());
        return props;
    }

}
