using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Core.Gates.Storage;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class GateCommandHandler : BaseCommandHandler
{
    private const string FormatMarkdown = "markdown";
    private const string MsgNoViolationsMatchingFilter = "No violations matching the specified filter across evaluated gates.";
    private const string MsgNoViolationsDetectedAcrossGates = "No violations detected across evaluated gates.";

    public static async Task<int> HandleAsync(GateOptions opts)
    {
        var action = (opts.Action ?? "check").ToLowerInvariant();
        var targetDir = Path.GetFullPath(opts.Dir ?? Directory.GetCurrentDirectory());

        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null && opts.ShouldAutoInit)
        {
            var dirName = new DirectoryInfo(targetDir).Name;
            ws = WorkspaceLocator.Initialize(targetDir, dirName);
            PrintSuccess($"Initialized CodeExplorer workspace at '{ws.RootDirectory}'.");

            var initRepo = new FileGateRepository(ws.RootDirectory);
            await initRepo.InitStarterGatesAsync(false);
        }

        if (opts.ShouldAutoScan || (opts.ShouldAutoInit && ws != null && !File.Exists(ws.DbPath) && action != "init"))
        {
            if (ws == null)
            {
                PrintError($"Error: No CodeExplorer workspace found at '{targetDir}'. Pass '--init' or run 'ce init' first.");
                return 1;
            }

            Console.WriteLine($"Running pre-gate scan for '{ws.RootDirectory}'...");
            var scanResult = await ScanCommandHandler.HandleAsync(new ScanOptions
            {
                Path = ws.RootDirectory,
                Clear = opts.Clear
            });

            if (scanResult != 0)
            {
                PrintError($"Pre-gate scan failed with exit code {scanResult}. Aborting gate check.");
                return scanResult;
            }
        }

        ws = EnsureInitialized(opts.Dir, requireIndexed: action != "init");
        if (ws == null) return 1;

        var repo = new FileGateRepository(ws.RootDirectory);

        if (action == "init")
        {
            var created = await repo.InitStarterGatesAsync(opts.Update);
            PrintSuccess($"Initialized gate directories in '{Path.Combine(ws.RootDirectory, ".codeexplorer", "gates")}'.");
            Console.WriteLine("Created template '.codeexplorer/gates/example-gate.yaml.example'. Copy and customize YAML manifests to activate gates.");
            return 0;
        }

        using var client = new SqliteGraphClient(ws.DbPath);
        var engine = new GateExecutionEngine(client, repo);

        InvariantScope? scopeFilter = !string.IsNullOrWhiteSpace(opts.Scope) && Enum.TryParse<InvariantScope>(opts.Scope, true, out var s) ? s : null;
        InvariantDimension? dimFilter = !string.IsNullOrWhiteSpace(opts.Dimension) && Enum.TryParse<InvariantDimension>(opts.Dimension, true, out var d) ? d : null;
        GateCategory? categoryFilter = !string.IsNullOrWhiteSpace(opts.Category) && Enum.TryParse<GateCategory>(opts.Category, true, out var c) ? c : null;
        string? platformFilter = !string.IsNullOrWhiteSpace(opts.Platform) ? opts.Platform.Trim() : null;

        if (action == "list")
        {
            var allGates = await repo.GetAllGatesAsync();
            if (scopeFilter.HasValue) allGates = allGates.Where(g => g.Scope == scopeFilter.Value).ToList();
            if (dimFilter.HasValue) allGates = allGates.Where(g => g.Dimension == dimFilter.Value).ToList();
            if (categoryFilter.HasValue) allGates = allGates.Where(g => g.Category == categoryFilter.Value).ToList();
            if (!string.IsNullOrWhiteSpace(platformFilter)) allGates = allGates.Where(g => string.Equals(g.Platform, platformFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            if (allGates.Count == 0 && !opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("No semantic gates configured in this workspace.");
                Console.WriteLine("Create gate YAML manifests in '.codeexplorer/gates/' or run 'ce gate init'.");
                return 0;
            }

            if (opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(JsonSerializer.Serialize(allGates, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            if (opts.Format.Equals(FormatMarkdown, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"### Registered Semantic Invariant Gates ({allGates.Count})\n");
                Console.WriteLine("| ID | Category | Platform | Scope | Dimension | Severity | Source | Name |");
                Console.WriteLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |");
                foreach (var g in allGates)
                {
                    Console.WriteLine($"| `{g.Id}` | {g.Category} | `{g.Platform}` | {g.Scope} | {g.Dimension} | {g.Severity} | {g.Source} | {g.Name} |");
                }
                return 0;
            }

            Console.WriteLine($"\nRegistered Semantic Invariant Gates ({allGates.Count}):\n");
            Console.WriteLine($"{"ID",-15} {"CATEGORY",-15} {"PLATFORM",-10} {"SCOPE",-9} {"DIM",-12} {"SEVERITY",-10} {"SOURCE",-12} {"NAME"}");
            Console.WriteLine(new string('-', 100));

            foreach (var g in allGates)
            {
                var sevColor = g.Severity == GateSeverity.Error ? ConsoleColor.Red : ConsoleColor.Yellow;
                Console.Write($"{g.Id,-15} {g.Category,-15} {g.Platform,-10} {g.Scope,-9} {g.Dimension,-12} ");
                Console.ForegroundColor = sevColor;
                Console.Write($"{g.Severity,-10} ");
                Console.ResetColor();
                Console.WriteLine($"{g.Source,-12} {g.Name}");
            }
            Console.WriteLine();
            return 0;
        }

        if (action == "ratchet")
        {
            List<SemanticGate>? targetGates = null;
            if (!string.IsNullOrWhiteSpace(opts.GateId))
            {
                var gateIds = opts.GateId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                targetGates = new List<SemanticGate>();
                foreach (var id in gateIds)
                {
                    var g = await repo.GetGateByIdAsync(id);
                    if (g == null)
                    {
                        PrintError($"Error: Gate '{id}' not found.");
                        return 1;
                    }
                    targetGates.Add(g);
                }
            }

            var suite = await engine.CheckSuiteAsync(targetGates, scopeFilter, dimFilter, categoryFilter, platformFilter);

            if (opts.Update)
            {
                var updatedCount = 0;
                foreach (var r in suite.Results)
                {
                    var existingBaseline = await repo.LoadBaselineAsync(r.Gate);
                    var updated = RatchetService.CreateUpdatedBaseline(r.Gate, r.Violations, existingBaseline);
                    await repo.SaveBaselineAsync(updated, r.Gate);
                    updatedCount++;
                }

                PrintSuccess($"Successfully ratcheted down and updated baselines for {updatedCount} gate(s).");
                return 0;
            }

            var opportunities = suite.Results.Where(r => r.CanRatchetDown).ToList();
            if (opportunities.Count == 0)
            {
                Console.WriteLine("All gates are currently at their strict baseline limits. No ratchet opportunities found.");
                return 0;
            }

            Console.WriteLine($"\nFound {opportunities.Count} gate(s) with resolved technical debt:\n");
            foreach (var opp in opportunities)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"[{opp.Gate.Id}] ");
                Console.ResetColor();
                Console.WriteLine($"{opp.Gate.Name}: violations reduced from {opp.BaselineCount} -> {opp.Violations.Count}.");
            }

            Console.WriteLine("\nRun 'ce gate ratchet --update' to lock down these improvements.");
            return 0;
        }

        if (action is "details" or "violations" or "show")
        {
            var resultsToInspect = new List<GateCheckResult>();
            if (!string.IsNullOrWhiteSpace(opts.GateId))
            {
                var gateIds = opts.GateId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var id in gateIds)
                {
                    var gate = await repo.GetGateByIdAsync(id);
                    if (gate == null)
                    {
                        PrintError($"Error: Gate '{id}' not found.");
                        return 1;
                    }
                    var singleResult = await engine.CheckGateAsync(gate);
                    resultsToInspect.Add(singleResult);
                }
            }
            else
            {
                var suite = await engine.CheckSuiteAsync(null, scopeFilter, dimFilter, categoryFilter, platformFilter);
                resultsToInspect.AddRange(suite.Results);
            }

            if (action == "details")
            {
                return RenderDetails(resultsToInspect, opts);
            }

            return RenderViolations(resultsToInspect, opts);
        }

        // action == "check"
        if (!string.IsNullOrWhiteSpace(opts.GateId))
        {
            var gateIds = opts.GateId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (gateIds.Length == 1)
            {
                var gate = await repo.GetGateByIdAsync(gateIds[0]);
                if (gate == null)
                {
                    PrintError($"Error: Gate '{gateIds[0]}' not found.");
                    return 1;
                }

                var singleResult = await engine.CheckGateAsync(gate);
                return RenderSingleGate(singleResult, opts);
            }

            var targetGates = new List<SemanticGate>();
            foreach (var id in gateIds)
            {
                var gate = await repo.GetGateByIdAsync(id);
                if (gate == null)
                {
                    PrintError($"Error: Gate '{id}' not found.");
                    return 1;
                }
                targetGates.Add(gate);
            }

            var suite = await engine.CheckSuiteAsync(targetGates, scopeFilter, dimFilter, categoryFilter, platformFilter);
            return RenderSuite(suite, opts);
        }

        var fullSuite = await engine.CheckSuiteAsync(null, scopeFilter, dimFilter, categoryFilter, platformFilter);
        return RenderSuite(fullSuite, opts);
    }

    private static int RenderSingleGate(GateCheckResult r, GateOptions opts)
    {
        var isSingleFail = r.Status is GateCheckStatus.FailedNew or GateCheckStatus.Error
            || ((opts.FailOnWarning || opts.Strict) && r.Status == GateCheckStatus.Warning);

        if (opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var summary = new
            {
                r.Gate.Id,
                r.Gate.Name,
                Category = r.Gate.Category.ToString(),
                r.Gate.Platform,
                Scope = r.Gate.Scope.ToString(),
                Dimension = r.Gate.Dimension.ToString(),
                Severity = r.Gate.Severity.ToString(),
                Status = r.Status.ToString(),
                Violations = r.Violations.Count,
                NewViolations = r.NewViolations.Count,
                ToleratedViolations = r.ToleratedViolations.Count,
                r.CanRatchetDown,
                DurationMs = r.Duration.TotalMilliseconds,
                r.ErrorMessage
            };
            Console.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return isSingleFail ? 1 : 0;
        }

        var (color, label) = r.Status switch
        {
            GateCheckStatus.Passed => (ConsoleColor.Green, "[PASS]"),
            GateCheckStatus.PassedTolerated => (ConsoleColor.Yellow, "[TOLERATED]"),
            GateCheckStatus.Warning => (ConsoleColor.Yellow, "[WARN]"),
            GateCheckStatus.FailedNew => (ConsoleColor.Red, "[FAIL]"),
            _ => (ConsoleColor.DarkRed, "[ERROR]")
        };

        if (opts.Format.Equals(FormatMarkdown, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"### Gate Check: `{r.Gate.Id}`\n");
            Console.WriteLine("| Status | ID | Category | Platform | Scope | Violations | Name | Duration |");
            Console.WriteLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |");
            var st = r.Status switch
            {
                GateCheckStatus.Passed => "PASS",
                GateCheckStatus.PassedTolerated => "TOLERATED",
                GateCheckStatus.Warning => "WARN",
                GateCheckStatus.FailedNew => "FAIL",
                _ => "ERROR"
            };
            var vText = r.NewViolations.Count > 0 ? $"{r.NewViolations.Count} new" : $"{r.Violations.Count} tol";
            if (r.Violations.Count == 0) vText = "0";
            Console.WriteLine($"| {st} | `{r.Gate.Id}` | {r.Gate.Category} | `{r.Gate.Platform}` | {r.Gate.Scope} | {vText} | {r.Gate.Name} | {r.Duration.TotalMilliseconds:F0}ms |\n");
            if (isSingleFail || r.Status == GateCheckStatus.Warning)
            {
                Console.WriteLine($"- **Tip**: Run `ce gate details -g {r.Gate.Id}` to view violation details and remediation.\n");
            }
            return isSingleFail ? 1 : 0;
        }

        Console.Write($"{label} ", color);
        Console.WriteLine($"{r.Gate.Id} [{r.Gate.Category}/{r.Gate.Platform}] - {r.Gate.Name} ({r.Duration.TotalMilliseconds:F0}ms)");
        if (!string.IsNullOrEmpty(r.Gate.Description))
        {
            Console.WriteLine($"  Rule: {r.Gate.Description}");
        }

        var violSummary = r.NewViolations.Count > 0
            ? $"{r.NewViolations.Count} new" + (r.ToleratedViolations.Count > 0 ? $", {r.ToleratedViolations.Count} tolerated" : "")
            : $"{r.Violations.Count} tolerated";
        if (r.Violations.Count == 0) violSummary = "0";
        Console.WriteLine($"  Violations: {violSummary} | Status: {label.Trim('[', ']')}");

        if (r.CanRatchetDown)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Ratchet: Violations reduced from {r.BaselineCount} -> {r.Violations.Count}. Run 'ce gate ratchet --update -g {r.Gate.Id}'.");
            Console.ResetColor();
        }

        if (isSingleFail || r.Status == GateCheckStatus.Warning)
        {
            Console.WriteLine($"  Tip: Run 'ce gate details -g {r.Gate.Id}' to view violation details and remediation.");
        }

        return isSingleFail ? 1 : 0;
    }

    private static int RenderSuite(GateSuiteResult suite, GateOptions opts)
    {
        var failOnWarnings = opts.FailOnWarning || opts.Strict;
        var maxWarningsExceeded = opts.MaxWarnings >= 0 && suite.WarningCount > opts.MaxWarnings;
        var isFailure = suite.FailedCount > 0 || (failOnWarnings && suite.WarningCount > 0) || maxWarningsExceeded;

        if (opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            var summary = new
            {
                suite.TotalGates,
                suite.PassedCount,
                suite.WarningCount,
                suite.FailedCount,
                suite.NewViolationsCount,
                suite.ToleratedViolationsCount,
                suite.RatchetOpportunities,
                suite.IsSuccess,
                DurationMs = suite.TotalDuration.TotalMilliseconds,
                Gates = suite.Results.Select(r => new
                {
                    r.Gate.Id,
                    r.Gate.Name,
                    Category = r.Gate.Category.ToString(),
                    r.Gate.Platform,
                    Scope = r.Gate.Scope.ToString(),
                    Dimension = r.Gate.Dimension.ToString(),
                    Severity = r.Gate.Severity.ToString(),
                    Status = r.Status.ToString(),
                    Violations = r.Violations.Count,
                    NewViolations = r.NewViolations.Count,
                    ToleratedViolations = r.ToleratedViolations.Count,
                    DurationMs = r.Duration.TotalMilliseconds
                })
            };
            Console.WriteLine(JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return isFailure ? 1 : 0;
        }

        if (suite.TotalGates == 0)
        {
            Console.WriteLine("No semantic gates configured in this workspace.");
            Console.WriteLine("Add gate YAML manifests to '.codeexplorer/gates/' or run 'ce gate init'.");
            return 0;
        }

        if (opts.Format.Equals(FormatMarkdown, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("### Semantic Invariant Gates Evaluation\n");
            Console.WriteLine("| Status | ID | Category | Platform | Scope | Violations | Name |");
            Console.WriteLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- |");
            foreach (var r in suite.Results)
            {
                var st = r.Status switch
                {
                    GateCheckStatus.Passed => "PASS",
                    GateCheckStatus.PassedTolerated => "TOLERATED",
                    GateCheckStatus.Warning => "WARN",
                    GateCheckStatus.FailedNew => "FAIL",
                    _ => "ERROR"
                };
                var vText = r.NewViolations.Count > 0 ? $"{r.NewViolations.Count} new" : $"{r.Violations.Count} tol";
                if (r.Violations.Count == 0) vText = "0";
                Console.WriteLine($"| {st} | `{r.Gate.Id}` | {r.Gate.Category} | `{r.Gate.Platform}` | {r.Gate.Scope} | {vText} | {r.Gate.Name} |");
            }
            Console.WriteLine($"\n**Total**: {suite.TotalGates} gates | Passed: {suite.PassedCount} | Warnings: {suite.WarningCount} | Failed: {suite.FailedCount} | Duration: {suite.TotalDuration.TotalMilliseconds:F0}ms\n");

            if (isFailure || suite.WarningCount > 0)
            {
                Console.WriteLine("- **Tip**: Run `ce gate details` to inspect failure details and violation locations.\n");
            }
            return isFailure ? 1 : 0;
        }

        Console.WriteLine("\n--- Semantic Invariant Gates Evaluation ---\n");
        Console.WriteLine($"{"STATUS",-14} {"ID",-15} {"CATEGORY",-15} {"PLATFORM",-10} {"SCOPE",-9} {"VIOLATIONS",-15} {"NAME"}");
        Console.WriteLine(new string('-', 105));

        foreach (var r in suite.Results)
        {
            var (color, label) = r.Status switch
            {
                GateCheckStatus.Passed => (ConsoleColor.Green, "[PASS]"),
                GateCheckStatus.PassedTolerated => (ConsoleColor.Yellow, "[TOLERATED]"),
                GateCheckStatus.Warning => (ConsoleColor.Yellow, "[WARN]"),
                GateCheckStatus.FailedNew => (ConsoleColor.Red, "[FAIL]"),
                _ => (ConsoleColor.DarkRed, "[ERROR]")
            };

            Console.ForegroundColor = color;
            Console.Write($"{label,-14} ");
            Console.ResetColor();

            var vText = r.NewViolations.Count > 0 ? $"{r.NewViolations.Count} new" : $"{r.Violations.Count} tol";
            if (r.Violations.Count == 0) vText = "0";

            Console.WriteLine($"{r.Gate.Id,-15} {r.Gate.Category,-15} {r.Gate.Platform,-10} {r.Gate.Scope,-9} {vText,-15} {r.Gate.Name}");
        }

        Console.WriteLine(new string('-', 105));
        Console.WriteLine($"Total: {suite.TotalGates} gates | Passed: {suite.PassedCount} | Warnings: {suite.WarningCount} | Failed: {suite.FailedCount} | Duration: {suite.TotalDuration.TotalMilliseconds:F0}ms\n");

        if (suite.RatchetOpportunities > 0)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{suite.RatchetOpportunities} gate(s) have fewer violations than allowed by baseline.");
            Console.WriteLine("   Run 'ce gate ratchet --update' to lock down resolved technical debt.");
            Console.ResetColor();
        }

        if (isFailure)
        {
            Console.WriteLine("Tip: Run 'ce gate details' to view failure details and remediation.");
            return 1;
        }

        if (suite.WarningCount > 0)
        {
            Console.WriteLine("Tip: Run 'ce gate details' to view warning details and remediation.");
        }

        PrintSuccess("All architectural semantic gates passed successfully.");
        return 0;
    }

    private static List<(GateViolation Violation, bool IsTolerated)> FilterViolations(GateCheckResult r, GateOptions opts)
    {
        var items = new List<(GateViolation Violation, bool IsTolerated)>();
        var toleratedFp = new HashSet<string>(r.ToleratedViolations.Select(t => t.Fingerprint));
        foreach (var v in r.Violations)
        {
            var isTol = toleratedFp.Contains(v.Fingerprint);
            if (opts.OnlyNew && isTol) continue;
            if (opts.OnlyTolerated && !isTol) continue;

            if (!string.IsNullOrWhiteSpace(opts.FileFilter) &&
                (v.File == null || !v.File.Contains(opts.FileFilter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(opts.SymbolFilter) &&
                (v.Symbol == null || !v.Symbol.Contains(opts.SymbolFilter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(opts.TargetFilter) &&
                (v.Target == null || !v.Target.Contains(opts.TargetFilter, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            items.Add((v, isTol));
        }
        return items;
    }

    private static int RenderDetails(IReadOnlyList<GateCheckResult> results, GateOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.GroupBy))
        {
            return RenderViolations(results, opts);
        }

        var singleTargeted = results.Count == 1 && !string.IsNullOrWhiteSpace(opts.GateId);

        List<GateCheckResult> gatesToDisplay;
        if (singleTargeted)
        {
            gatesToDisplay = results.ToList();
        }
        else if (opts.All)
        {
            gatesToDisplay = results.Where(r => r.Violations.Count > 0 || r.Status == GateCheckStatus.Error).ToList();
        }
        else if (opts.OnlyNew)
        {
            gatesToDisplay = results.Where(r => r.NewViolations.Count > 0 || r.Status is GateCheckStatus.FailedNew or GateCheckStatus.Error).ToList();
        }
        else if (opts.OnlyTolerated)
        {
            gatesToDisplay = results.Where(r => r.ToleratedViolations.Count > 0).ToList();
        }
        else
        {
            var problemGates = results.Where(r => r.Status is GateCheckStatus.FailedNew or GateCheckStatus.Error or GateCheckStatus.Warning || r.NewViolations.Count > 0).ToList();
            gatesToDisplay = problemGates.Count > 0
                ? problemGates
                : results.Where(r => r.Violations.Count > 0).ToList();
        }

        var isJson = opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase);
        var isMarkdown = opts.Format.Equals(FormatMarkdown, StringComparison.OrdinalIgnoreCase);

        var failOnWarnings = opts.FailOnWarning || opts.Strict;
        var maxWarningsExceeded = opts.MaxWarnings >= 0 && results.Count(r => r.Status == GateCheckStatus.Warning) > opts.MaxWarnings;
        var isFailure = results.Any(r => r.Status is GateCheckStatus.FailedNew or GateCheckStatus.Error)
            || (failOnWarnings && results.Any(r => r.Status == GateCheckStatus.Warning))
            || maxWarningsExceeded;

        if (gatesToDisplay.Count == 0)
        {
            if (isJson)
            {
                var emptyPayload = new
                {
                    totalGates = results.Count,
                    isSuccess = !isFailure,
                    message = "All architectural semantic gates passed with zero violations.",
                    gates = Array.Empty<object>()
                };
                Console.WriteLine(JsonSerializer.Serialize(emptyPayload, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
                return 0;
            }

            if (isMarkdown)
            {
                Console.WriteLine("### Semantic Invariant Gate Details\n");
                Console.WriteLine("**All architectural semantic gates passed with zero violations.**\n");
                return 0;
            }

            PrintSuccess("All architectural semantic gates passed with zero violations. No failure details to display.");
            return 0;
        }

        if (isJson)
        {
            var jsonPayload = new
            {
                totalGates = results.Count,
                inspectedGates = gatesToDisplay.Count,
                isSuccess = !isFailure,
                gates = gatesToDisplay.Select(r =>
                {
                    var items = FilterViolations(r, opts);
                    var paged = opts.Limit > 0 ? items.Skip(opts.Offset).Take(opts.Limit).ToList() : items.Skip(opts.Offset).ToList();
                    return new
                    {
                        r.Gate.Id,
                        r.Gate.Name,
                        Category = r.Gate.Category.ToString(),
                        r.Gate.Platform,
                        Scope = r.Gate.Scope.ToString(),
                        Dimension = r.Gate.Dimension.ToString(),
                        Severity = r.Gate.Severity.ToString(),
                        Status = r.Status.ToString(),
                        Description = r.Gate.Description,
                        Remediation = r.Gate.Remediation,
                        TotalViolations = items.Count,
                        NewViolationsCount = items.Count(x => !x.IsTolerated),
                        ToleratedViolationsCount = items.Count(x => x.IsTolerated),
                        Offset = opts.Offset,
                        Limit = opts.Limit,
                        Violations = paged.Select(x => new
                        {
                            Status = x.IsTolerated ? "TOLERATED" : "NEW",
                            File = GateViolation.CleanFilePath(x.Violation.File) != "-" ? GateViolation.CleanFilePath(x.Violation.File) : x.Violation.File,
                            x.Violation.Line,
                            x.Violation.Column,
                            x.Violation.Location,
                            x.Violation.Symbol,
                            x.Violation.Target,
                            x.Violation.Message,
                            x.Violation.Fingerprint,
                            isTolerated = x.IsTolerated
                        })
                    };
                })
            };
            Console.WriteLine(JsonSerializer.Serialize(jsonPayload, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return isFailure ? 1 : 0;
        }

        if (isMarkdown)
        {
            Console.WriteLine("### Semantic Invariant Gate Details\n");
            foreach (var r in gatesToDisplay)
            {
                var st = r.Status switch
                {
                    GateCheckStatus.Passed => "PASS",
                    GateCheckStatus.PassedTolerated => "TOLERATED",
                    GateCheckStatus.Warning => "WARN",
                    GateCheckStatus.FailedNew => "FAIL",
                    _ => "ERROR"
                };
                Console.WriteLine($"#### [{st}] `{r.Gate.Id}` - {r.Gate.Name}\n");
                Console.WriteLine($"- **Status**: `{st}` ({r.NewViolations.Count} new, {r.ToleratedViolations.Count} tolerated)");
                Console.WriteLine($"- **Category**: {r.Gate.Category} | **Platform**: `{r.Gate.Platform}` | **Scope**: {r.Gate.Scope} | **Severity**: {r.Gate.Severity}");
                if (!string.IsNullOrEmpty(r.Gate.Description))
                {
                    Console.WriteLine($"- **Rule**: {r.Gate.Description}");
                }
                if (!string.IsNullOrEmpty(r.Gate.Remediation))
                {
                    Console.WriteLine($"- **Remediation**: {r.Gate.Remediation}");
                }
                Console.WriteLine();

                var items = FilterViolations(r, opts);
                if (items.Count == 0)
                {
                    Console.WriteLine("*No violations matching the specified filter for this gate.*\n");
                    continue;
                }

                var paged = opts.Limit > 0 ? items.Skip(opts.Offset).Take(opts.Limit).ToList() : items.Skip(opts.Offset).ToList();
                Console.WriteLine("| Status | Location | Symbol | Target / Details |");
                Console.WriteLine("| :--- | :--- | :--- | :--- |");
                foreach (var (v, isTol) in paged)
                {
                    var statusStr = isTol ? "TOLERATED" : "NEW";
                    var loc = v.Location != "-" ? $"`{v.Location}`" : "-";
                    var sym = !string.IsNullOrEmpty(v.Symbol) ? $"`{v.Symbol}`" : "-";
                    var tgt = !string.IsNullOrEmpty(v.Target) ? $"`{v.Target}`" : (v.Message ?? "");
                    Console.WriteLine($"| {statusStr} | {loc} | {sym} | {tgt} |");
                }
                if (items.Count > paged.Count)
                {
                    Console.WriteLine($"\n*... and {items.Count - paged.Count} more violation(s). Use `--limit 0` to show all.*");
                }
                Console.WriteLine();
            }

            return isFailure ? 1 : 0;
        }

        // Table / Console format
        Console.WriteLine("\n--- Semantic Invariant Gate Details ---\n");
        for (var idx = 0; idx < gatesToDisplay.Count; idx++)
        {
            var r = gatesToDisplay[idx];
            var (color, label) = r.Status switch
            {
                GateCheckStatus.Passed => (ConsoleColor.Green, "[PASS]"),
                GateCheckStatus.PassedTolerated => (ConsoleColor.Yellow, "[TOLERATED]"),
                GateCheckStatus.Warning => (ConsoleColor.Yellow, "[WARN]"),
                GateCheckStatus.FailedNew => (ConsoleColor.Red, "[FAIL]"),
                _ => (ConsoleColor.DarkRed, "[ERROR]")
            };

            Console.ForegroundColor = color;
            Console.Write($"{label} ");
            Console.ResetColor();
            Console.WriteLine($"{r.Gate.Id} [{r.Gate.Category}/{r.Gate.Platform}] - {r.Gate.Name} ({r.Duration.TotalMilliseconds:F0}ms)");

            if (!string.IsNullOrEmpty(r.Gate.Description))
            {
                Console.WriteLine($"  Rule: {r.Gate.Description}");
            }
            if (!string.IsNullOrEmpty(r.Gate.Remediation))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Remediation: {r.Gate.Remediation}");
                Console.ResetColor();
            }

            var items = FilterViolations(r, opts);
            if (items.Count == 0)
            {
                Console.WriteLine("  No violations detected for this gate.\n");
                continue;
            }

            var paged = opts.Limit > 0 ? items.Skip(opts.Offset).Take(opts.Limit).ToList() : items.Skip(opts.Offset).ToList();
            var newCount = items.Count(x => !x.IsTolerated);
            var tolCount = items.Count(x => x.IsTolerated);
            Console.WriteLine($"\n  Violations ({newCount} new, {tolCount} tolerated) | Showing {paged.Count} of {items.Count}:");

            var statusWidth = Math.Max("STATUS".Length, paged.Count > 0 ? paged.Max(x => (x.IsTolerated ? "[TOL]" : "[NEW]").Length) : 6);
            var locWidth = Math.Max("LOCATION".Length, paged.Count > 0 ? paged.Max(x => (x.Violation.Location != "-" ? x.Violation.Location : "").Length) : 8);
            var symWidth = Math.Max("SYMBOL".Length, paged.Count > 0 ? paged.Max(x => (x.Violation.Symbol ?? "").Length) : 6);
            var tgtWidth = Math.Max("TARGET / CALL".Length, paged.Count > 0 ? paged.Max(x => (!string.IsNullOrEmpty(x.Violation.Target) ? x.Violation.Target : (x.Violation.Message ?? "")).Length) : 13);

            var headerLine = $"  {"STATUS".PadRight(statusWidth)} | {"LOCATION".PadRight(locWidth)} | {"SYMBOL".PadRight(symWidth)} | {"TARGET / CALL".PadRight(tgtWidth)}";
            var dividerLine = $"  {new string('-', statusWidth)}-+-{new string('-', locWidth)}-+-{new string('-', symWidth)}-+-{new string('-', tgtWidth)}";

            Console.WriteLine(headerLine);
            Console.WriteLine(dividerLine);

            foreach (var (v, isTol) in paged)
            {
                var st = isTol ? "[TOL]" : "[NEW]";
                var loc = v.Location != "-" ? v.Location : "";
                var sym = v.Symbol ?? "";
                var tgt = !string.IsNullOrEmpty(v.Target) ? v.Target : (v.Message ?? "");

                var vColor = isTol ? ConsoleColor.Yellow : ConsoleColor.Red;
                Console.Write("  ");
                Console.ForegroundColor = vColor;
                Console.Write(st.PadRight(statusWidth));
                Console.ResetColor();

                Console.WriteLine($" | {loc.PadRight(locWidth)} | {sym.PadRight(symWidth)} | {tgt}");
            }

            Console.WriteLine(dividerLine);
            if (items.Count > paged.Count)
            {
                Console.WriteLine($"  ... and {items.Count - paged.Count} more violation(s). Use '--limit 0' to show all.");
            }

            if (r.CanRatchetDown)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  Ratchet: Violations reduced from {r.BaselineCount} -> {r.Violations.Count}. Run 'ce gate ratchet --update -g {r.Gate.Id}'.");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        return isFailure ? 1 : 0;
    }

    private static int RenderViolations(IReadOnlyList<GateCheckResult> results, GateOptions opts)
    {
        var items = new List<(GateViolation Violation, bool IsTolerated)>();
        foreach (var r in results)
        {
            var toleratedFp = new HashSet<string>(r.ToleratedViolations.Select(t => t.Fingerprint));
            foreach (var v in r.Violations)
            {
                var isTol = toleratedFp.Contains(v.Fingerprint);
                if (opts.OnlyNew && isTol) continue;
                if (opts.OnlyTolerated && !isTol) continue;

                if (!string.IsNullOrWhiteSpace(opts.FileFilter) &&
                    (v.File == null || !v.File.Contains(opts.FileFilter, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(opts.SymbolFilter) &&
                    (v.Symbol == null || !v.Symbol.Contains(opts.SymbolFilter, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(opts.TargetFilter) &&
                    (v.Target == null || !v.Target.Contains(opts.TargetFilter, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                items.Add((v, isTol));
            }
        }

        var isJson = opts.Format.Equals("json", StringComparison.OrdinalIgnoreCase);
        var isMarkdown = opts.Format.Equals("markdown", StringComparison.OrdinalIgnoreCase);

        var targetGate = ResolveTargetGate(results, opts);

        if (items.Count == 0 && string.IsNullOrWhiteSpace(opts.GroupBy))
        {
            if (isJson)
            {
                var payload = new Dictionary<string, object?>
                {
                    ["total"] = 0,
                    ["offset"] = opts.Offset,
                    ["limit"] = opts.Limit,
                    ["violations"] = Array.Empty<object>()
                };
                if (!string.IsNullOrWhiteSpace(opts.GroupBy))
                {
                    payload["groupBy"] = opts.GroupBy.Trim().ToLowerInvariant();
                    payload["totalViolations"] = 0;
                    payload["totalGroups"] = 0;
                    payload["groups"] = Array.Empty<object>();
                }
                if (targetGate != null)
                {
                    payload["gate"] = new
                    {
                        targetGate.Id,
                        targetGate.Name,
                        Category = targetGate.Category.ToString(),
                        targetGate.Platform,
                        Scope = targetGate.Scope.ToString(),
                        Dimension = targetGate.Dimension.ToString(),
                        Severity = targetGate.Severity.ToString(),
                        targetGate.Description,
                        targetGate.Remediation
                    };
                }
                Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            if (isMarkdown)
            {
                if (targetGate != null)
                {
                    RenderGateMarkdownHeader(targetGate);
                }
                else
                {
                    var gateCount = results.Select(r => r.Gate.Id).Distinct().Count();
                    Console.WriteLine($"### Architectural Violations Across {gateCount} Gates\n");
                }
                Console.WriteLine("**No violations detected.**\n");
                return 0;
            }

            if (targetGate != null)
            {
                RenderGateConsoleHeader(targetGate, "Architectural Violations");
                PrintNoViolationsMessage(targetGate, results, opts);
            }
            else
            {
                var gateCount = results.Select(r => r.Gate.Id).Distinct().Count();
                Console.WriteLine($"\n--- Architectural Violations Across {gateCount} Gates ---\n");
                PrintNoViolationsMessageMultiGate(results, opts);
            }
            return 0;
        }

        // Grouped summary view
        if (!string.IsNullOrWhiteSpace(opts.GroupBy))
        {
            var groupBy = opts.GroupBy.Trim().ToLowerInvariant();
            var groups = groupBy switch
            {
                "file" => items
                    .GroupBy(x => GateViolation.CleanFilePath(x.Violation.File) != "-" ? GateViolation.CleanFilePath(x.Violation.File) : "(unknown)")
                    .Select(g => new { Key = g.Key, Count = g.Count(), NewCount = g.Count(x => !x.IsTolerated), ToleratedCount = g.Count(x => x.IsTolerated) })
                    .OrderByDescending(g => g.Count)
                    .ToList(),
                "callee" or "target" => items
                    .GroupBy(x => ExtractCallee(x.Violation.Target))
                    .Select(g => new { Key = g.Key, Count = g.Count(), NewCount = g.Count(x => !x.IsTolerated), ToleratedCount = g.Count(x => x.IsTolerated) })
                    .OrderByDescending(g => g.Count)
                    .ToList(),
                "symbol" => items
                    .GroupBy(x => x.Violation.Symbol ?? "(unknown)")
                    .Select(g => new { Key = g.Key, Count = g.Count(), NewCount = g.Count(x => !x.IsTolerated), ToleratedCount = g.Count(x => x.IsTolerated) })
                    .OrderByDescending(g => g.Count)
                    .ToList(),
                _ => items
                    .GroupBy(x => GateViolation.CleanFilePath(x.Violation.File) != "-" ? GateViolation.CleanFilePath(x.Violation.File) : "(unknown)")
                    .Select(g => new { Key = g.Key, Count = g.Count(), NewCount = g.Count(x => !x.IsTolerated), ToleratedCount = g.Count(x => x.IsTolerated) })
                    .OrderByDescending(g => g.Count)
                    .ToList()
            };

            if (isJson)
            {
                var payload = new Dictionary<string, object?>
                {
                    ["groupBy"] = groupBy,
                    ["totalViolations"] = items.Count,
                    ["totalGroups"] = groups.Count,
                    ["groups"] = groups
                };
                if (targetGate != null)
                {
                    payload["gate"] = new
                    {
                        targetGate.Id,
                        targetGate.Name,
                        Category = targetGate.Category.ToString(),
                        targetGate.Platform,
                        Scope = targetGate.Scope.ToString(),
                        Dimension = targetGate.Dimension.ToString(),
                        Severity = targetGate.Severity.ToString(),
                        targetGate.Description,
                        targetGate.Remediation
                    };
                }
                Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
                return 0;
            }

            if (isMarkdown)
            {
                if (targetGate != null)
                {
                    RenderGateMarkdownHeader(targetGate, $"Violations Summary Grouped by `{groupBy}`");
                }
                else
                {
                    var gateCount = results.Select(r => r.Gate.Id).Distinct().Count();
                    Console.WriteLine($"### Violations Summary Grouped by `{groupBy}` Across {gateCount} Gates\n");
                }

                Console.WriteLine($"- **Total Violations**: {items.Count} across {groups.Count} unique `{groupBy}` targets\n");
                Console.WriteLine($"| Violations | New | Tolerated | {groupBy.ToUpperInvariant()} |");
                Console.WriteLine("| :--- | :--- | :--- | :--- |");
                foreach (var g in groups.Take(opts.Limit > 0 ? opts.Limit : int.MaxValue))
                {
                    Console.WriteLine($"| {g.Count} | {g.NewCount} | {g.ToleratedCount} | `{g.Key}` |");
                }
                return 0;
            }

            if (targetGate != null)
            {
                RenderGateConsoleHeader(targetGate, $"Violations Summary Grouped by '{groupBy}'");
            }
            else
            {
                var gateCount = results.Select(r => r.Gate.Id).Distinct().Count();
                Console.WriteLine($"\n--- Violations Summary Grouped by '{groupBy}' Across {gateCount} Gates ---\n");
            }

            Console.WriteLine($"Total Violations: {items.Count} across {groups.Count} unique {groupBy} targets\n");

            var displayGroups = opts.Limit > 0 ? groups.Take(opts.Limit).ToList() : groups;
            var countWidth = Math.Max("COUNT".Length, displayGroups.Count > 0 ? displayGroups.Max(g => g.Count.ToString().Length) : 5);
            var newWidth = Math.Max("NEW".Length, displayGroups.Count > 0 ? displayGroups.Max(g => g.NewCount.ToString().Length) : 3);
            var tolWidth = Math.Max("TOL".Length, displayGroups.Count > 0 ? displayGroups.Max(g => g.ToleratedCount.ToString().Length) : 3);
            var targetHeader = groupBy.ToUpperInvariant();
            var targetWidth = Math.Max(targetHeader.Length, displayGroups.Count > 0 ? displayGroups.Max(g => g.Key.Length) : 10);

            var groupHeader = $"{"COUNT".PadRight(countWidth)} | {"NEW".PadRight(newWidth)} | {"TOL".PadRight(tolWidth)} | {targetHeader.PadRight(targetWidth)}";
            var groupDivider = $"{new string('-', countWidth)}-+-{new string('-', newWidth)}-+-{new string('-', tolWidth)}-+-{new string('-', targetWidth)}";

            Console.WriteLine(groupHeader);
            Console.WriteLine(groupDivider);

            foreach (var g in displayGroups)
            {
                var countStr = g.Count.ToString().PadRight(countWidth);
                var newStr = g.NewCount.ToString().PadRight(newWidth);
                var tolStr = g.ToleratedCount.ToString().PadRight(tolWidth);

                if (g.NewCount > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write($"{countStr} | {newStr} | ");
                    Console.ResetColor();
                    Console.WriteLine($"{tolStr} | {g.Key}");
                }
                else
                {
                    Console.WriteLine($"{countStr} | {newStr} | {tolStr} | {g.Key}");
                }
            }

            Console.WriteLine(groupDivider);

            if (groups.Count > displayGroups.Count)
            {
                Console.WriteLine($"\n... and {groups.Count - displayGroups.Count} more groups. Use '--limit 0' to show all.");
            }

            var drilldownGate = targetGate?.Id ?? opts.GateId ?? "<gate-id>";
            Console.WriteLine($"\nTip: Drill down into a specific file: ce gate violations -g {drilldownGate} --file <filename>");
            return 0;
        }

        // Detailed list view
        var total = items.Count;
        var paged = items.Skip(opts.Offset);
        if (opts.Limit > 0) paged = paged.Take(opts.Limit);
        var pageList = paged.ToList();

        if (isJson)
        {
            var payload = new Dictionary<string, object?>
            {
                ["total"] = total,
                ["offset"] = opts.Offset,
                ["limit"] = opts.Limit,
                ["violations"] = pageList.Select(x => new
                {
                    x.Violation.GateId,
                    File = GateViolation.CleanFilePath(x.Violation.File) != "-" ? GateViolation.CleanFilePath(x.Violation.File) : x.Violation.File,
                    x.Violation.Line,
                    x.Violation.Column,
                    x.Violation.Location,
                    x.Violation.Symbol,
                    x.Violation.Target,
                    x.Violation.Message,
                    x.Violation.Fingerprint,
                    isTolerated = x.IsTolerated
                })
            };
            if (targetGate != null)
            {
                payload["gate"] = new
                {
                    targetGate.Id,
                    targetGate.Name,
                    Category = targetGate.Category.ToString(),
                    targetGate.Platform,
                    Scope = targetGate.Scope.ToString(),
                    Dimension = targetGate.Dimension.ToString(),
                    Severity = targetGate.Severity.ToString(),
                    targetGate.Description,
                    targetGate.Remediation
                };
            }
            Console.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        var tolCount = items.Count(x => x.IsTolerated);
        var newCount = items.Count(x => !x.IsTolerated);

        if (isMarkdown)
        {
            if (targetGate != null)
            {
                RenderGateMarkdownHeader(targetGate);
            }
            else
            {
                var gateCount = results.Select(r => r.Gate.Id).Distinct().Count();
                Console.WriteLine($"### Architectural Violations Across {gateCount} Gates\n");
            }

            Console.WriteLine($"- **Total**: {total} (New: {newCount}, Tolerated: {tolCount})");
            Console.WriteLine($"- **Showing**: {(total == 0 ? 0 : opts.Offset + 1)}..{opts.Offset + pageList.Count} of {total}\n");

            if (targetGate != null)
            {
                Console.WriteLine("| Status | Location | Symbol | Target / Details |");
                Console.WriteLine("| :--- | :--- | :--- | :--- |");
                foreach (var (v, isTol) in pageList)
                {
                    var st = isTol ? "TOLERATED" : "NEW";
                    var loc = v.Location != "-" ? $"`{v.Location}`" : "-";
                    var sym = !string.IsNullOrEmpty(v.Symbol) ? $"`{v.Symbol}`" : "-";
                    var tgt = !string.IsNullOrEmpty(v.Target) ? $"`{v.Target}`" : (v.Message ?? "");
                    Console.WriteLine($"| {st} | {loc} | {sym} | {tgt} |");
                }
            }
            else
            {
                Console.WriteLine("| Gate | Status | Location | Symbol | Target / Details |");
                Console.WriteLine("| :--- | :--- | :--- | :--- | :--- |");
                foreach (var (v, isTol) in pageList)
                {
                    var st = isTol ? "TOLERATED" : "NEW";
                    var loc = v.Location != "-" ? $"`{v.Location}`" : "-";
                    var sym = !string.IsNullOrEmpty(v.Symbol) ? $"`{v.Symbol}`" : "-";
                    var tgt = !string.IsNullOrEmpty(v.Target) ? $"`{v.Target}`" : (v.Message ?? "");
                    Console.WriteLine($"| `{v.GateId}` | {st} | {loc} | {sym} | {tgt} |");
                }
            }
            return 0;
        }

        if (targetGate != null)
        {
            RenderGateConsoleHeader(targetGate, "Architectural Violations");
        }
        else
        {
            var gateCount = results.Select(r => r.Gate.Id).Distinct().Count();
            Console.WriteLine($"\n--- Architectural Violations Across {gateCount} Gates ---\n");
        }

        Console.WriteLine($"Total: {total} (New: {newCount}, Tolerated: {tolCount}) | Showing: {(total == 0 ? 0 : opts.Offset + 1)}..{opts.Offset + pageList.Count}\n");

        if (targetGate != null)
        {
            var statusWidth = Math.Max("STATUS".Length, pageList.Count > 0 ? pageList.Max(x => (x.IsTolerated ? "[TOL]" : "[NEW]").Length) : 6);
            var locWidth = Math.Max("LOCATION".Length, pageList.Count > 0 ? pageList.Max(x => (x.Violation.Location != "-" ? x.Violation.Location : "").Length) : 8);
            var symWidth = Math.Max("SYMBOL".Length, pageList.Count > 0 ? pageList.Max(x => (x.Violation.Symbol ?? "").Length) : 6);
            var tgtWidth = Math.Max("TARGET / CALL".Length, pageList.Count > 0 ? pageList.Max(x => (!string.IsNullOrEmpty(x.Violation.Target) ? x.Violation.Target : (x.Violation.Message ?? "")).Length) : 13);

            var headerLine = $"{"STATUS".PadRight(statusWidth)} | {"LOCATION".PadRight(locWidth)} | {"SYMBOL".PadRight(symWidth)} | {"TARGET / CALL".PadRight(tgtWidth)}";
            var dividerLine = $"{new string('-', statusWidth)}-+-{new string('-', locWidth)}-+-{new string('-', symWidth)}-+-{new string('-', tgtWidth)}";

            Console.WriteLine(headerLine);
            Console.WriteLine(dividerLine);

            foreach (var (v, isTol) in pageList)
            {
                var st = isTol ? "[TOL]" : "[NEW]";
                var loc = v.Location != "-" ? v.Location : "";
                var sym = v.Symbol ?? "";
                var tgt = !string.IsNullOrEmpty(v.Target) ? v.Target : (v.Message ?? "");

                var color = isTol ? ConsoleColor.Yellow : ConsoleColor.Red;
                Console.ForegroundColor = color;
                Console.Write(st.PadRight(statusWidth));
                Console.ResetColor();

                Console.WriteLine($" | {loc.PadRight(locWidth)} | {sym.PadRight(symWidth)} | {tgt}");
            }

            Console.WriteLine(dividerLine);
        }
        else
        {
            var gateWidth = Math.Max("GATE".Length, pageList.Count > 0 ? pageList.Max(x => x.Violation.GateId.Length) : 4);
            var statusWidth = Math.Max("STATUS".Length, pageList.Count > 0 ? pageList.Max(x => (x.IsTolerated ? "[TOL]" : "[NEW]").Length) : 6);
            var locWidth = Math.Max("LOCATION".Length, pageList.Count > 0 ? pageList.Max(x => (x.Violation.Location != "-" ? x.Violation.Location : "").Length) : 8);
            var symWidth = Math.Max("SYMBOL".Length, pageList.Count > 0 ? pageList.Max(x => (x.Violation.Symbol ?? "").Length) : 6);
            var tgtWidth = Math.Max("TARGET / CALL".Length, pageList.Count > 0 ? pageList.Max(x => (!string.IsNullOrEmpty(x.Violation.Target) ? x.Violation.Target : (x.Violation.Message ?? "")).Length) : 13);

            var headerLine = $"{"GATE".PadRight(gateWidth)} | {"STATUS".PadRight(statusWidth)} | {"LOCATION".PadRight(locWidth)} | {"SYMBOL".PadRight(symWidth)} | {"TARGET / CALL".PadRight(tgtWidth)}";
            var dividerLine = $"{new string('-', gateWidth)}-+-{new string('-', statusWidth)}-+-{new string('-', locWidth)}-+-{new string('-', symWidth)}-+-{new string('-', tgtWidth)}";

            Console.WriteLine(headerLine);
            Console.WriteLine(dividerLine);

            foreach (var (v, isTol) in pageList)
            {
                var st = isTol ? "[TOL]" : "[NEW]";
                var loc = v.Location != "-" ? v.Location : "";
                var sym = v.Symbol ?? "";
                var tgt = !string.IsNullOrEmpty(v.Target) ? v.Target : (v.Message ?? "");

                Console.Write($"{v.GateId.PadRight(gateWidth)} | ");

                var color = isTol ? ConsoleColor.Yellow : ConsoleColor.Red;
                Console.ForegroundColor = color;
                Console.Write(st.PadRight(statusWidth));
                Console.ResetColor();

                Console.WriteLine($" | {loc.PadRight(locWidth)} | {sym.PadRight(symWidth)} | {tgt}");
            }

            Console.WriteLine(dividerLine);
        }

        if (total > opts.Offset + pageList.Count)
        {
            Console.WriteLine($"\nShowing {opts.Offset + pageList.Count} of {total}. Use '--offset {opts.Offset + pageList.Count}' for next page, or '--limit 0' to show all.");
        }
        Console.WriteLine("Use '--group-by file' or '--group-by callee' for aggregated overview.");
        if (string.IsNullOrWhiteSpace(opts.FileFilter))
        {
            var filterHint = targetGate != null ? $"ce gate violations -g {targetGate.Id} --file <filename>" : "ce gate violations --file <filename>";
            Console.WriteLine($"Use '--file <pattern>' to filter by file path (e.g. '{filterHint}').");
        }

        return 0;
    }

    private static SemanticGate? ResolveTargetGate(IReadOnlyList<GateCheckResult> results, GateOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.GateId))
        {
            return results.FirstOrDefault(r => r.Gate.Id.Equals(opts.GateId, StringComparison.OrdinalIgnoreCase))?.Gate;
        }

        if (results.Count == 1)
        {
            return results[0].Gate;
        }

        var distinctGates = results.Select(r => r.Gate).DistinctBy(g => g.Id).ToList();
        return distinctGates.Count == 1 ? distinctGates[0] : null;
    }

    private static void RenderGateConsoleHeader(SemanticGate gate, string? prefix = null)
    {
        var title = string.IsNullOrEmpty(prefix)
            ? $"[{gate.Id}] {gate.Name}"
            : $"{prefix} for [{gate.Id}] {gate.Name}";

        Console.WriteLine($"\n--- {title} ---");
        Console.WriteLine($"Category: {gate.Category} | Platform: {gate.Platform} | Scope: {gate.Scope} | Dimension: {gate.Dimension} | Severity: {gate.Severity}");
        if (!string.IsNullOrWhiteSpace(gate.Description))
        {
            Console.WriteLine($"Rule: {gate.Description}");
        }
        if (!string.IsNullOrWhiteSpace(gate.Remediation))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Remediation: {gate.Remediation}");
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    private static void RenderGateMarkdownHeader(SemanticGate gate, string? prefix = null)
    {
        var title = string.IsNullOrEmpty(prefix)
            ? $"Architectural Violations for `[{gate.Id}]` {gate.Name}"
            : $"{prefix} for `[{gate.Id}]` {gate.Name}";

        Console.WriteLine($"### {title}\n");
        Console.WriteLine($"> **Category**: {gate.Category} | **Platform**: `{gate.Platform}` | **Scope**: {gate.Scope} | **Dimension**: {gate.Dimension} | **Severity**: {gate.Severity}");
        if (!string.IsNullOrWhiteSpace(gate.Description))
        {
            Console.WriteLine($">\n> **Rule**: {gate.Description}");
        }
        if (!string.IsNullOrWhiteSpace(gate.Remediation))
        {
            Console.WriteLine($">\n> **Remediation**: {gate.Remediation}");
        }
        Console.WriteLine();
    }

    private static void PrintNoViolationsMessage(SemanticGate gate, IReadOnlyList<GateCheckResult> results, GateOptions opts)
    {
        var hasActiveViolations = results.Any(r => r.Violations.Count > 0);
        var hasActiveTolerated = results.Any(r => r.ToleratedViolations.Count > 0);
        var hasFilters = !string.IsNullOrWhiteSpace(opts.FileFilter) ||
                         !string.IsNullOrWhiteSpace(opts.SymbolFilter) ||
                         !string.IsNullOrWhiteSpace(opts.TargetFilter);

        if (hasFilters)
        {
            PrintSuccess($"No violations matching the specified filter for [{gate.Id}] {gate.Name}.");
        }
        else if (opts.OnlyNew && hasActiveTolerated)
        {
            var tolCount = results.Sum(r => r.ToleratedViolations.Count);
            PrintSuccess($"No new violations detected for [{gate.Id}] {gate.Name} ({tolCount} active violation(s) tolerated by baseline).");
        }
        else if (opts.OnlyTolerated && !hasActiveTolerated)
        {
            PrintSuccess($"No tolerated violations for [{gate.Id}] {gate.Name}.");
        }
        else
        {
            PrintSuccess($"No violations detected for [{gate.Id}] {gate.Name}.");
        }
    }

    private static void PrintNoViolationsMessageMultiGate(IReadOnlyList<GateCheckResult> results, GateOptions opts)
    {
        var hasActiveTolerated = results.Any(r => r.ToleratedViolations.Count > 0);
        var hasFilters = !string.IsNullOrWhiteSpace(opts.FileFilter) ||
                         !string.IsNullOrWhiteSpace(opts.SymbolFilter) ||
                         !string.IsNullOrWhiteSpace(opts.TargetFilter);

        if (hasFilters)
        {
            PrintSuccess(MsgNoViolationsMatchingFilter);
        }
        else if (opts.OnlyNew && hasActiveTolerated)
        {
            var tolCount = results.Sum(r => r.ToleratedViolations.Count);
            PrintSuccess($"No new violations detected across evaluated gates ({tolCount} active violation(s) tolerated by baseline).");
        }
        else
        {
            PrintSuccess(MsgNoViolationsDetectedAcrossGates);
        }
    }

    private static string ExtractCallee(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return "(unknown)";
        var idx = target.IndexOf('(');
        return idx > 0 ? target[..idx].Trim() : target.Trim();
    }
}
