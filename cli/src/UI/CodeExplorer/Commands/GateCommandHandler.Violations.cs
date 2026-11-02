using System.Text.Json;
using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Core.Gates.Storage;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public partial class GateCommandHandler : BaseCommandHandler
{
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
