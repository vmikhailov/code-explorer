using System.Text.Json;
using CodePassport.Cli.Options;
using CodePassport.Engine.Analysis;
using CodePassport.Engine.Models;

namespace CodePassport.Cli.Commands;

public static class AuditCommandHandler
{
    public static async Task<int> HandleAsync(AuditOptions options)
    {
        var fullRoot = Path.GetFullPath(options.Path);
        var generator = new PassportGenerator();
        var passports = new List<PackagePassport>();

        var nodeModules = Path.Combine(fullRoot, "node_modules");
        if (Directory.Exists(nodeModules))
        {
            foreach (var pkgDir in EnumerateNodeModulesPackages(nodeModules))
            {
                try
                {
                    passports.Add(await generator.GenerateAsync(pkgDir));
                }
                catch (Exception)
                {
                    // Skip broken package directory
                }
            }
        }
        else
        {
            passports.Add(await generator.GenerateAsync(fullRoot));
        }

        if (options.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(passports, InspectCommandHandler.JsonOptions));
            return 0;
        }

        Console.WriteLine($"=== CodePassport Supply-Chain Audit ({passports.Count} package(s)) ===");
        foreach (var p in passports.OrderByDescending(x => x.Verdict).ThenByDescending(x => x.Tier))
        {
            var caps = p.ActiveCapabilities.Count > 0 ? string.Join(", ", p.ActiveCapabilities) : "PURE";
            Console.WriteLine($"  [{p.Verdict}] {p.Ecosystem}:{p.PackageName}@{p.Version} | {p.Tier} | {caps}");
            foreach (var tc in p.ToxicCombinations)
            {
                Console.WriteLine($"      [!!!] {tc.Id} ({tc.Archetype}): {string.Join(" + ", tc.MatchedCapabilities)}");
            }
        }

        return passports.Any(p => p.Verdict == RiskVerdict.CriticalDrift) ? 2 : 0;
    }

    private static IEnumerable<string> EnumerateNodeModulesPackages(string nodeModulesDir)
    {
        foreach (var subDir in Directory.EnumerateDirectories(nodeModulesDir))
        {
            var name = Path.GetFileName(subDir);
            if (name.StartsWith('.')) continue;

            if (name.StartsWith('@'))
            {
                foreach (var scopedPkg in Directory.EnumerateDirectories(subDir))
                {
                    if (File.Exists(Path.Combine(scopedPkg, "package.json")))
                    {
                        yield return scopedPkg;
                    }
                }
            }
            else if (File.Exists(Path.Combine(subDir, "package.json")))
            {
                yield return subDir;
            }
        }
    }
}
