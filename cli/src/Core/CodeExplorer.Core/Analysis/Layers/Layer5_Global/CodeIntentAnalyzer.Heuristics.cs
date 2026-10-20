using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Analysis;

public static partial class CodeIntentAnalyzer
{
    public static WorkspaceDomainsConfig? LoadWorkspaceDomainsConfig(string? workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath) || !Directory.Exists(workspacePath))
            return null;

        var candidates = new[]
        {
            Path.Combine(workspacePath, ".codeexplorer", "domains.json"),
            Path.Combine(workspacePath, "domains.json")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    var config = JsonSerializer.Deserialize<WorkspaceDomainsConfig>(json, options);
                    if (config?.Domains != null && config.Domains.Count > 0)
                    {
                        return config;
                    }
                }
                catch
                {
                    // Ignore parse errors and fall through
                }
            }
        }

        return null;
    }

    public static SystemDomainsResult SynthesizeDomainsTopologically(
        IReadOnlyList<ProjectSignature> signatures,
        WorkspaceDomainsConfig? config = null)
    {
        var serviceToDomain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 0. Classify technical subdomains first using ontology role
        foreach (var sig in signatures)
        {
            var role = sig.Role;

            if (role is "SharedLibrary" or "Library" ||
                (!string.IsNullOrWhiteSpace(sig.RelativePath) &&
                 (sig.RelativePath.Contains("/ui/") || sig.RelativePath.Contains("/components/") || sig.RelativePath.StartsWith("ui/") || sig.RelativePath.StartsWith("packages/ui"))))
            {
                var isUi = !string.IsNullOrWhiteSpace(sig.RelativePath) &&
                           (sig.RelativePath.Contains("/ui/") || sig.RelativePath.Contains("/components/") || sig.RelativePath.StartsWith("ui/") || sig.RelativePath.StartsWith("packages/ui"));

                serviceToDomain[sig.Name] = isUi ? "UserInterface" : "SharedKernel";
                continue;
            }

            if (role is "CliTool")
            {
                serviceToDomain[sig.Name] = "DeveloperTooling";
                continue;
            }

            if (role is "Test")
            {
                serviceToDomain[sig.Name] = "TestingInfrastructure";
                continue;
            }

            var cleanNorm = WorkspaceConventions.NormalizeServiceName(sig.Name);
            if (cleanNorm is "shared-kernel" or "sharedkernel" or "common" or "kernel" or "primitives" or "contracts" or "abstractions")
            {
                serviceToDomain[sig.Name] = "SharedKernel";
                continue;
            }

            var isTooling = (!string.IsNullOrWhiteSpace(sig.RelativePath) &&
                             (sig.RelativePath.StartWithIC("tools/") ||
                              sig.RelativePath.StartWithIC("cli/") ||
                              sig.RelativePath.Contains("/tools/") ||
                              sig.RelativePath.Contains("/cli/"))) ||
                            cleanNorm is "cli" or "tools" or "tooling" or "devtool" or "devtools" ||
                            cleanNorm.EndsWith("-cli") || cleanNorm.StartsWith("cli-") ||
                            cleanNorm.EndsWith("-tool") || cleanNorm.StartsWith("tool-") ||
                            cleanNorm.Contains("migrator") || cleanNorm.Contains("codegen") || cleanNorm.Contains("scaffold");
            if (isTooling)
            {
                serviceToDomain[sig.Name] = "DeveloperTooling";
                continue;
            }

            var isTesting = (!string.IsNullOrWhiteSpace(sig.RelativePath) &&
                             (sig.RelativePath.StartWithIC("tests/") ||
                              sig.RelativePath.Contains("/tests/"))) ||
                            cleanNorm.EndsWith("-test") || cleanNorm.EndsWith("-tests") ||
                            cleanNorm.StartsWith("test-") || cleanNorm.Contains("fixture") || cleanNorm.Contains("mock");
            if (isTesting)
            {
                serviceToDomain[sig.Name] = "TestingInfrastructure";
                continue;
            }
        }

        // Branch A: Workspace has configured domain definitions (.codeexplorer/domains.json)
        if (config != null && config.Domains.Count > 0)
        {
            var domainDescriptions = config.Domains
                .Where(d => !string.IsNullOrWhiteSpace(d.Name))
                .ToDictionary(d => d.Name, d => d.Description ?? $"Domain for {d.Name} capabilities.", StringComparer.OrdinalIgnoreCase);

            var tableMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var topicMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var sig in signatures)
            {
                foreach (var tbl in sig.Tables)
                {
                    if (!tableMap.TryGetValue(tbl, out var list))
                    {
                        list = [];
                        tableMap[tbl] = list;
                    }
                    list.Add(sig.Name);
                }
                foreach (var top in sig.Topics)
                {
                    if (!topicMap.TryGetValue(top, out var list))
                    {
                        list = [];
                        topicMap[top] = list;
                    }
                    list.Add(sig.Name);
                }
            }

            // Match services against configured domains using token and semantic affinity
            foreach (var sig in signatures)
            {
                if (serviceToDomain.ContainsKey(sig.Name)) continue;

                var rawName = sig.Name;
                var normalizedName = WorkspaceConventions.NormalizeServiceName(rawName);
                var splitWords = Regex.Replace(rawName, "([a-z])([A-Z])", "$1 $2")
                    .Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.ToLowerInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var normalizedWords = normalizedName
                    .Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries)
                    .Select(w => w.ToLowerInvariant())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                string? bestDomain = null;
                var bestScore = 0;

                foreach (var domain in config.Domains)
                {
                    if (string.IsNullOrWhiteSpace(domain.Name)) continue;

                    var score = 0;
                    var domainKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        domain.Name.ToLowerInvariant()
                    };

                    if (domain.Keywords != null)
                    {
                        foreach (var kw in domain.Keywords)
                        {
                            if (!string.IsNullOrWhiteSpace(kw))
                                domainKeywords.Add(kw.Trim().ToLowerInvariant());
                        }
                    }

                    foreach (var kw in domainKeywords)
                    {
                        if (normalizedName.Equals(kw, StringComparison.OrdinalIgnoreCase) ||
                            splitWords.Contains(kw) || normalizedWords.Contains(kw))
                        {
                            score += 10;
                        }
                        else if (normalizedName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                                 rawName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            score += 5;
                        }

                        if (!string.IsNullOrWhiteSpace(sig.RelativePath) &&
                            sig.RelativePath.Contains(kw, StringComparison.OrdinalIgnoreCase))
                        {
                            score += 3;
                        }

                        foreach (var tbl in sig.Tables)
                        {
                            if (tbl.Equals(kw, StringComparison.OrdinalIgnoreCase)) score += 6;
                            else if (tbl.Contains(kw, StringComparison.OrdinalIgnoreCase)) score += 3;
                        }

                        foreach (var top in sig.Topics)
                        {
                            if (top.Equals(kw, StringComparison.OrdinalIgnoreCase)) score += 6;
                            else if (top.Contains(kw, StringComparison.OrdinalIgnoreCase)) score += 3;
                        }

                        foreach (var dt in sig.DomainTypes)
                        {
                            if (dt.Equals(kw, StringComparison.OrdinalIgnoreCase)) score += 4;
                            else if (dt.Contains(kw, StringComparison.OrdinalIgnoreCase)) score += 2;
                        }
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDomain = domain.Name;
                    }
                }

                if (bestDomain != null && bestScore >= 4)
                {
                    serviceToDomain[sig.Name] = bestDomain;
                }
            }

            // Propagate assigned domains across shared database tables and message topics
            foreach (var (_, services) in tableMap)
            {
                var existingDomain = services.FirstOrDefault(s => serviceToDomain.ContainsKey(s));
                if (existingDomain != null)
                {
                    var d = serviceToDomain[existingDomain];
                    foreach (var s in services)
                    {
                        serviceToDomain.TryAdd(s, d);
                    }
                }
            }

            foreach (var (_, services) in topicMap)
            {
                var existingDomain = services.FirstOrDefault(s => serviceToDomain.ContainsKey(s));
                if (existingDomain != null)
                {
                    var d = serviceToDomain[existingDomain];
                    foreach (var s in services)
                    {
                        serviceToDomain.TryAdd(s, d);
                    }
                }
            }

            // Fallback for any remaining unassigned services
            foreach (var sig in signatures)
            {
                if (serviceToDomain.ContainsKey(sig.Name)) continue;

                serviceToDomain[sig.Name] = WorkspaceConventions.CanonicalizeDomain(sig.ExistingDomain, sig.Name, sig.RelativePath, sig.Role);
            }

            var groupedConfigured = serviceToDomain
                .GroupBy(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

            var configuredResult = new List<SystemDomainAssignment>();
            foreach (var g in groupedConfigured)
            {
                if (!domainDescriptions.TryGetValue(g.Key, out var desc))
                {
                    desc = $"Business domain for {g.Key} capabilities and services.";
                }
                configuredResult.Add(new SystemDomainAssignment(g.Key, desc, [.. g.Distinct()]));
            }

            return new SystemDomainsResult(configuredResult);
        }

        // Branch B: Generic Topological Clustering (Pure Graph Affinity & Connected Components)
        // No hardcoded company profiles. Uses Disjoint Set Union (DSU) on shared tables, topics, directory namespaces, and stems.
        var dsu = new DisjointSetUnion();
        var businessSignatures = new List<ProjectSignature>();

        foreach (var sig in signatures)
        {
            if (serviceToDomain.ContainsKey(sig.Name)) continue;
            businessSignatures.Add(sig);
            dsu.Add(sig.Name);
        }

        // 1. Data Cohesion: union services sharing database tables
        var genericTableMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var sig in businessSignatures)
        {
            foreach (var tbl in sig.Tables)
            {
                if (!genericTableMap.TryGetValue(tbl, out var list))
                {
                    list = [];
                    genericTableMap[tbl] = list;
                }
                list.Add(sig.Name);
            }
        }
        foreach (var (_, svcs) in genericTableMap)
        {
            for (var i = 1; i < svcs.Count; i++)
            {
                dsu.Union(svcs[0], svcs[i]);
            }
        }

        // 2. Behavioral Cohesion: union services sharing message topics / event streams
        var genericTopicMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var sig in businessSignatures)
        {
            foreach (var top in sig.Topics)
            {
                if (!genericTopicMap.TryGetValue(top, out var list))
                {
                    list = [];
                    genericTopicMap[top] = list;
                }
                list.Add(sig.Name);
            }
        }
        foreach (var (_, svcs) in genericTopicMap)
        {
            for (var i = 1; i < svcs.Count; i++)
            {
                dsu.Union(svcs[0], svcs[i]);
            }
        }

        // 3. Namespace Cohesion: union services residing in the same business directory
        var namespaceMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var sig in businessSignatures)
        {
            var ns = GetDirectoryNamespace(sig.RelativePath);
            if (!string.IsNullOrWhiteSpace(ns))
            {
                if (!namespaceMap.TryGetValue(ns, out var list))
                {
                    list = [];
                    namespaceMap[ns] = list;
                }
                list.Add(sig.Name);
            }
        }
        foreach (var (_, svcs) in namespaceMap)
        {
            for (var i = 1; i < svcs.Count; i++)
            {
                dsu.Union(svcs[0], svcs[i]);
            }
        }

        // 4. Group business services by their DSU root cluster
        var clusters = businessSignatures
            .GroupBy(sig => dsu.Find(sig.Name))
            .ToList();

        foreach (var cluster in clusters)
        {
            var clusterSignatures = cluster.ToList();
            var domainName = DetermineClusterDomainName(clusterSignatures);
            foreach (var sig in clusterSignatures)
            {
                serviceToDomain[sig.Name] = domainName;
            }
        }


        var genericGrouped = serviceToDomain
            .GroupBy(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

        var genericResult = new List<SystemDomainAssignment>();
        foreach (var g in genericGrouped)
        {
            var desc = g.Key switch
            {
                "UserInterface" => "UI component library, design system, and presentation widgets.",
                "SharedKernel" => "Foundational shared models, domain contracts, and common utilities.",
                "DeveloperTooling" => "Developer utilities, code generators, and CLI tools.",
                "TestingInfrastructure" => "Testing harnesses, fixtures, and mock implementations.",
                _ => $"Business domain for {g.Key} capabilities and operations."
            };
            genericResult.Add(new SystemDomainAssignment(g.Key, desc, [.. g.Distinct()]));
        }

        return new SystemDomainsResult(genericResult);
    }

    private static string DetermineClusterDomainName(List<ProjectSignature> signatures)
    {
        if (signatures.Count == 0) return "CoreDomain";

        // 1. Determine canonical macro-domains for all services in this cluster
        var domainCandidates = signatures
            .Select(s => WorkspaceConventions.CanonicalizeDomain(s.ExistingDomain, s.Name, s.RelativePath, s.Role))
            .Where(d => !string.IsNullOrWhiteSpace(d) && !d.Equals("CoreDomain", StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => d, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ToList();

        if (domainCandidates.Count > 0)
        {
            return domainCandidates[0].Key;
        }

        // 2. If majority of services share a directory namespace, canonicalize it
        var nsCounts = signatures
            .Select(s => GetDirectoryNamespace(s.RelativePath))
            .Where(ns => !string.IsNullOrWhiteSpace(ns))
            .GroupBy(ns => ns!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault();

        if (nsCounts != null && (signatures.Count <= 2 || nsCounts.Count() >= signatures.Count / 2))
        {
            var canonicalFromNs = WorkspaceConventions.CanonicalizeDomain(nsCounts.Key, filePath: nsCounts.Key);
            if (!string.IsNullOrWhiteSpace(canonicalFromNs) && !canonicalFromNs.Equals("CoreDomain", StringComparison.OrdinalIgnoreCase))
            {
                return canonicalFromNs;
            }
            return nsCounts.Key;
        }

        // 3. For single or dominant service, canonicalize via conventions
        var primary = signatures[0];
        return WorkspaceConventions.CanonicalizeDomain(primary.ExistingDomain, primary.Name, primary.RelativePath, primary.Role);
    }

    private static string? GetSignificantPrefixStem(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var normalized = WorkspaceConventions.NormalizeServiceName(name);
        var tokens = normalized.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);

        var genericWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "internal", "service", "services", "integration", "adapter", "controller",
            "scheduler", "app", "microservice", "api", "worker", "job",
            "core", "client", "daemon", "svc", "backend", "server", "host",
            "gateway", "proxy", "handler"
        };

        foreach (var token in tokens)
        {
            if (!genericWords.Contains(token) && token.Length > 2)
            {
                return ToPascalCase(token);
            }
        }

        if (tokens.Length > 0)
        {
            return ToPascalCase(tokens[^1]);
        }

        return null;
    }

    private static string? GetDirectoryNamespace(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;
        var segments = relativePath.Replace('\\', '/').Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length <= 1) return null;

        var idx = 0;
        while (idx < segments.Length - 1 && WorkspaceConventions.GenericFolderRoots.Contains(segments[idx]))
        {
            idx++;
        }

        if (idx < segments.Length)
        {
            var candidate = segments[idx];
            if (WorkspaceConventions.GenericFolderRoots.Contains(candidate)) return null;
            var norm = WorkspaceConventions.NormalizeServiceName(candidate);
            if (WorkspaceConventions.GenericFolderRoots.Contains(norm)) return null;
            var pascal = ToPascalCase(norm);
            if (WorkspaceConventions.GenericFolderRoots.Contains(pascal)) return null;
            if (pascal.Length > 2) return pascal;
        }
        return null;
    }

    private sealed class DisjointSetUnion
    {
        private readonly Dictionary<string, string> _parent = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string item)
        {
            _parent.TryAdd(item, item);
        }

        public string Find(string item)
        {
            if (!_parent.TryGetValue(item, out var parent))
            {
                _parent[item] = item;
                return item;
            }

            if (!parent.Equals(item, StringComparison.OrdinalIgnoreCase))
            {
                _parent[item] = Find(parent);
            }

            return _parent[item];
        }

        public void Union(string item1, string item2)
        {
            var root1 = Find(item1);
            var root2 = Find(item2);
            if (!root1.Equals(root2, StringComparison.OrdinalIgnoreCase))
            {
                _parent[root1] = root2;
            }
        }
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var parts = input.Split(['-', '_', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p.Substring(1));
            }
        }
        return sb.ToString();
    }
}
