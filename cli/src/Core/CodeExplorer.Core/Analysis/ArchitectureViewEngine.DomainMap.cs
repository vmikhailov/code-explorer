using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;
using ArchitectureViewType = CodeExplorer.Core.Analysis.ArchitectureViewType;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    // =========================================================================
    // 6. Domain Architecture (Bounded Contexts & Service Map)
    // =========================================================================

    private static readonly Regex SubProjectSuffixRegex =
        new(@"\.(Logic|Client|Contracts|Data|Core|Domain|Infrastructure|Api|Service|Services|Web|Worker|Test|Tests|Shared|Models|Dto|SDK|UnitTests|IntegrationTests)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] IngressKeywords = ["admin", "app", "ui", "fe", "gateway", "bff", "portal", "web", "client-app", "landing"];
    private static readonly string[] IngressFrameworks = ["angular", "react", "vue", "svelte", "next", "vite", "blazor", "express", "fastify"];

    internal static bool IsAspireOrchestrator(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        var clean = name.Trim();
        if (clean.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^".csproj".Length];
        }
        return clean.EndsWith(".AppHost", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("AppHost", StringComparison.OrdinalIgnoreCase) ||
               clean.Contains(".AppHost.", StringComparison.OrdinalIgnoreCase) ||
               clean.Contains("Aspire.AppHost", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsRunawayDomainName(string? rawDomain)
    {
        if (string.IsNullOrWhiteSpace(rawDomain)) return false;
        var clean = rawDomain.Trim();
        if (clean.StartsWith("domain:", StringComparison.OrdinalIgnoreCase)) clean = clean["domain:".Length..].Trim();
        if (clean.StartsWith("dom:", StringComparison.OrdinalIgnoreCase)) clean = clean["dom:".Length..].Trim();
        if (clean.Length > 40) return true;
        var words = clean.Split([' ', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 4) return true;
        var formatted = WorkspaceConventions.FormatDomainDisplayName(ToPascalCase(clean));
        if (formatted.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 5) return true;
        return false;
    }

    internal static bool IsTestOrDocsDomain(string? domainKey, string? domainDisplayName)
    {
        if (!string.IsNullOrWhiteSpace(domainKey))
        {
            var cleanKey = domainKey.Trim();
            if (cleanKey.EndsWith(":testinginfrastructure", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.EndsWith(":developertooling", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.EndsWith(":telemetrydocs", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.EndsWith(":tests", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.EndsWith(":test", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.EndsWith(":homebrew", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.EndsWith(":scripts", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.EndsWith(":site", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.EndsWith(":.agents", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.EndsWith(":agents", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.Contains("testinginfrastructure", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.Contains("telemetrydocs", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.Contains("tests", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.Contains("homebrew", StringComparison.OrdinalIgnoreCase) ||
                cleanKey.Contains(".agents", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(domainDisplayName))
        {
            var disp = domainDisplayName.Trim();
            if (string.Equals(disp, "Testing Infrastructure", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(disp, "Developer Tooling", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(disp, "Telemetry Docs", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(disp, "Homebrew", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(disp, "Scripts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(disp, "Site", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(disp, "Agents", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(disp, ".agents", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (disp.Contains("Tests", StringComparison.OrdinalIgnoreCase) ||
                disp.Contains("Testing", StringComparison.OrdinalIgnoreCase) ||
                disp.EndsWith("Docs", StringComparison.OrdinalIgnoreCase) ||
                disp.EndsWith("Documentation", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsTestOrDocsProject(string? projectName, string? filePath)
    {
        var name = (projectName ?? "").Trim().ToLowerInvariant();
        var rawPath = (filePath ?? "").Replace('\\', '/').Trim().ToLowerInvariant();
        var pathWithoutExt = rawPath.EndsWith(".csproj") || rawPath.EndsWith(".fsproj") || rawPath.EndsWith(".json")
            ? Path.GetFileNameWithoutExtension(rawPath).ToLowerInvariant()
            : Path.GetFileName(rawPath.TrimEnd('/')).ToLowerInvariant();

        string[] testDocsSuffixes = ["-docs", "_docs", "docs", "test", "tests", "tracetesting", "loadgenerator", "benchmark", "benchmarks", "homebrew", "scripts", "site", ".agents"];
        foreach (var s in testDocsSuffixes)
        {
            if (name == s || name.EndsWith(s) || pathWithoutExt == s || pathWithoutExt.EndsWith(s))
            {
                return true;
            }
        }

        if (name.Contains("tracetesting") || name.Contains("loadgenerator") || name.Contains("benchmark") ||
            rawPath.Contains("/tracetesting/") || rawPath.Contains("/loadgenerator/") || rawPath.Contains("/benchmarks/") ||
            rawPath.Contains("/test/") || rawPath.Contains("/tests/") || rawPath.Contains("/docs/") ||
            rawPath.Contains("/homebrew/") || rawPath.Contains("/scripts/") || rawPath.Contains("/site/") || rawPath.Contains("/.agents/"))
        {
            return true;
        }

        return false;
    }

    private static bool IsTestOrDocsProjectOrDomain(GraphNodeDto node, string domainKey, string domainDisplayName)
    {
        if (IsTestOrDocsDomain(domainKey, domainDisplayName)) return true;
        if (string.Equals(node.Properties?.GetValueOrDefault("role"), "ScriptProject", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return IsTestOrDocsProject(node.Name, node.FilePath);
    }

    public static (string DomainKey, string DomainDisplayName, bool IsIngressHint) ExtractDomainKey(GraphNodeDto node)
    {
        var name = node.Name ?? "";
        var path = (node.FilePath ?? "").Replace('\\', '/').ToLowerInvariant();
        var lowerName = name.ToLowerInvariant();
        var isAspire = IsAspireOrchestrator(name);

        // 0. Explicit domain / intent_domain from node properties
        if (node.Properties != null)
        {
            if (node.Properties.TryGetValue("domain", out var expDomain) && !string.IsNullOrWhiteSpace(expDomain) && !IsRunawayDomainName(expDomain))
            {
                var clean = ToPascalCase(expDomain);
                var isIngressHint = !isAspire && (IngressKeywords.Any(kw => lowerName.Contains(kw)) ||
                                    string.Equals(node.Properties.GetValueOrDefault("layer"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(node.Properties.GetValueOrDefault("layerId"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase));
                var domainKey = node.Properties.TryGetValue("domainId", out var expId) && !string.IsNullOrWhiteSpace(expId)
                    ? expId
                    : $"domain:{clean.ToLowerInvariant()}";
                var disp = node.Properties.TryGetValue("domainDisplayName", out var expDisp) && !string.IsNullOrWhiteSpace(expDisp)
                    ? expDisp
                    : WorkspaceConventions.FormatDomainDisplayName(clean);
                return (domainKey, disp, isIngressHint);
            }
            if (node.Properties.TryGetValue("intent_domain", out var intDomain) && !string.IsNullOrWhiteSpace(intDomain) && !IsRunawayDomainName(intDomain))
            {
                var clean = ToPascalCase(intDomain);
                var isIngressHint = !isAspire && (IngressKeywords.Any(kw => lowerName.Contains(kw)) ||
                                    string.Equals(node.Properties.GetValueOrDefault("layer"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(node.Properties.GetValueOrDefault("layerId"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase));
                return ($"domain:{clean.ToLowerInvariant()}", WorkspaceConventions.FormatDomainDisplayName(clean), isIngressHint);
            }
        }

        // 0b. Configured workspace domain (.codeexplorer/domains.json or .codeexplorer/conventions.json)
        if (WorkspaceConventions.TryGetConfiguredDomain(name, node.FilePath, out var configuredDomain))
        {
            var clean = ToPascalCase(configuredDomain);
            var isIngressHint = !isAspire && (IngressKeywords.Any(kw => lowerName.Contains(kw)) ||
                                string.Equals(node.Properties?.GetValueOrDefault("layer"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(node.Properties?.GetValueOrDefault("layerId"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase));
            return ($"domain:{clean.ToLowerInvariant()}", WorkspaceConventions.FormatDomainDisplayName(clean), isIngressHint);
        }

        // 1. Suffix match
        var suffixMatch = SubProjectSuffixRegex.Match(name);
        if (suffixMatch.Success)
        {
            var matchedSuffix = suffixMatch.Groups[1].Value;
            var isTestSuffix = matchedSuffix is "Test" or "Tests" or "UnitTests" or "IntegrationTests";
            var parentName = name[..suffixMatch.Index];
            var dotParts = parentName.Split('.');
            var shortName = dotParts[^1];
            var isIngressHint = !isAspire && (IngressKeywords.Any(kw => lowerName.Contains(kw)) ||
                                string.Equals(node.Properties?.GetValueOrDefault("layer"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(node.Properties?.GetValueOrDefault("layerId"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase));
            var displayName = isTestSuffix ? $"{shortName} Tests" : $"{shortName} Service";
            return ($"domain:{parentName.ToLowerInvariant()}", displayName, isIngressHint);
        }

        // 2. Directory structure (safe against Windows drive letters C:/ and container roots)
        if (!string.IsNullOrEmpty(path))
        {
            var cleanPath = Regex.Replace(path, @"^[a-zA-Z]:[/]", "").TrimStart('/');
            var parts = cleanPath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                            .Where(p => p != ".." && p != "." && !Regex.IsMatch(p, @"^[a-zA-Z]:$"))
                            .ToArray();

            var skipRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "src", "services", "microservices", "apps", "packages", "libs", "modules", "projects", "cmd", "home", "users", "cli", "ui", "uis", "tools", "bffs", "gateways", "apigateways"
            };

            var hasFileExt = parts.Length > 0 && Path.HasExtension(parts[^1]);
            var candidateParts = hasFileExt ? parts.Take(parts.Length - 1) : parts;
            var meaningful = candidateParts.Where(p => !skipRoots.Contains(p)).ToList();
            if (meaningful.Count >= 1)
            {
                var folder = meaningful[0];
                var cleanName = ToPascalCase(WorkspaceConventions.NormalizeServiceName(folder));
                var isIngressHint = !isAspire && (IngressKeywords.Any(kw => folder.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                                    string.Equals(node.Properties?.GetValueOrDefault("layer"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase));
                return ($"domain:{folder.ToLowerInvariant()}", WorkspaceConventions.FormatDomainDisplayName(cleanName), isIngressHint);
            }
        }

        // 3. Standalone fallback
        var framework = node.Properties?.GetValueOrDefault("framework", "") ?? "";
        var isIngress = !isAspire && (node.Kind is NodeKind.App or NodeKind.Ingress ||
                        string.Equals(node.Properties?.GetValueOrDefault("role"), "FrontendApp", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(node.Properties?.GetValueOrDefault("role"), "App", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(node.Properties?.GetValueOrDefault("layer"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(node.Properties?.GetValueOrDefault("layerId"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                        IngressKeywords.Any(kw => lowerName.Contains(kw)) ||
                        IngressFrameworks.Any(fw => framework.Contains(fw, StringComparison.OrdinalIgnoreCase)));

        var cleanSvc = WorkspaceConventions.NormalizeServiceName(name);
        var svcPascal = ToPascalCase(cleanSvc);
        if (svcPascal.Length > 2)
        {
            return ($"domain:{cleanSvc.ToLowerInvariant()}", WorkspaceConventions.FormatDomainDisplayName(svcPascal), isIngress);
        }

        return ("domain:coredomain", "Core Domain", isIngress);
    }

    public static bool IsInternalTopic(GraphNodeDto node)
    {
        if (node.Kind != NodeKind.Topic) return false;
        var broker = node.Properties?.GetValueOrDefault("broker_type")?.ToLowerInvariant() ?? "";
        return broker is "mediatr" or "in-memory" ||
               string.Equals(node.Properties?.GetValueOrDefault("is_internal"), "true", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(node.Properties?.GetValueOrDefault("scope"), "internal", StringComparison.OrdinalIgnoreCase) ||
               node.Id.Contains(":mediatr:", StringComparison.OrdinalIgnoreCase) ||
               node.Id.Contains(":in-memory:", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<DomainArchitectureDto> GetDomainArchitectureAsync(bool includeLibraries = false, CancellationToken ct = default)
    {
        var archGraph = await BuildBaseArchitectureTopologyAsync(includeLibraries: includeLibraries, includeTopics: false, projectFilter: null, sharedDatabasesOnly: false, ct: ct);
        var result = new DomainArchitectureDto();

        var projToDomainMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var domainProjectsMap = new Dictionary<string, List<DomainProjectInfoDto>>(StringComparer.OrdinalIgnoreCase);
        var domainPrimaryMap = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var domainZoneMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var domainNameMap = new Dictionary<string, (string Name, string DisplayName, string? Framework, string? Language)>(StringComparer.OrdinalIgnoreCase);

        // 1. Categorize Project-like nodes into domains
        foreach (var node in archGraph.Nodes)
        {
            if (!IsProjectNodeKind(node.Kind)) continue;

            if (node.Kind is NodeKind.TestSuite or NodeKind.Test ||
                string.Equals(node.Properties?.GetValueOrDefault("role"), "Test", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(node.Properties?.GetValueOrDefault("role"), "TestSuite", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(node.Properties?.GetValueOrDefault("entity_kind"), "Test", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsAspireOrchestrator(node.Name))
            {
                continue;
            }

            var (domainKey, domainDisplayName, isIngressHint) = ExtractDomainKey(node);
            if (IsTestOrDocsProjectOrDomain(node, domainKey, domainDisplayName))
            {
                continue;
            }

            projToDomainMap[node.Id] = domainKey;
            projToDomainMap[node.Name] = domainKey;

            var isLib = IsLibraryProject(node);

            if (!domainProjectsMap.TryGetValue(domainKey, out var pList))
            {
                pList = [];
                domainProjectsMap[domainKey] = pList;

                var isIngress = !isLib &&
                                (node.Kind is NodeKind.App or NodeKind.Ingress ||
                                 string.Equals(node.Properties?.GetValueOrDefault("role"), "FrontendApp", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(node.Properties?.GetValueOrDefault("role"), "App", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(node.Properties?.GetValueOrDefault("layer"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(node.Properties?.GetValueOrDefault("layerId"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                                 isIngressHint) &&
                                node.Kind != NodeKind.Worker &&
                                node.Properties?.GetValueOrDefault("role") != "Worker" &&
                                node.Kind != NodeKind.CliTool &&
                                node.Properties?.GetValueOrDefault("role") != "CliTool";
                domainZoneMap[domainKey] = isIngress ? "ingress" : "service";
                domainNameMap[domainKey] = (node.Name, domainDisplayName, node.Properties?.GetValueOrDefault("framework"), node.Properties?.GetValueOrDefault("language") ?? node.Properties?.GetValueOrDefault("project_type"));
            }

            var resolvedKind = node.Kind;
            if (resolvedKind == NodeKind.Project)
            {
                var role = node.Properties?.GetValueOrDefault("role");
                if (role is "CliTool" or "ScriptProject") resolvedKind = NodeKind.CliTool;
                else if (role is "FrontendApp" or "App") resolvedKind = NodeKind.App;
                else if (role is "Worker") resolvedKind = NodeKind.Worker;
                else if (role is "SharedLibrary" or "Library") resolvedKind = NodeKind.Library;
            }

            pList.Add(new DomainProjectInfoDto
            {
                Id = node.Id,
                Name = node.Name,
                Kind = resolvedKind,
                Role = node.Properties?.GetValueOrDefault("role"),
                FilePath = node.FilePath,
                IsLibrary = isLib
            });

            if (!domainPrimaryMap.TryGetValue(domainKey, out var currPrimary) || (IsLibraryProject(currPrimary) && !isLib))
            {
                domainPrimaryMap[domainKey] = node;
                if (!isLib)
                {
                    domainNameMap[domainKey] = (node.Name, domainDisplayName, node.Properties?.GetValueOrDefault("framework"), node.Properties?.GetValueOrDefault("language") ?? node.Properties?.GetValueOrDefault("project_type"));
                    var isIngress = (node.Kind is NodeKind.App or NodeKind.Ingress ||
                                     string.Equals(node.Properties?.GetValueOrDefault("role"), "FrontendApp", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(node.Properties?.GetValueOrDefault("role"), "App", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(node.Properties?.GetValueOrDefault("layer"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(node.Properties?.GetValueOrDefault("layerId"), StandardLayers.Ingress.LayerId, StringComparison.OrdinalIgnoreCase) ||
                                     isIngressHint) &&
                                    node.Kind != NodeKind.Worker &&
                                    node.Properties?.GetValueOrDefault("role") != "Worker" &&
                                    node.Kind != NodeKind.CliTool &&
                                    node.Properties?.GetValueOrDefault("role") != "CliTool";
                    domainZoneMap[domainKey] = isIngress ? "ingress" : "service";
                }
            }
        }

        // 2. Identify Infrastructure Nodes (Databases, Topics, ExternalServices)
        var dbNodes = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var topicNodes = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var extNodes = new Dictionary<string, GraphNodeDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in archGraph.Nodes)
        {
            if (node.Kind == NodeKind.Database)
            {
                dbNodes[node.Id] = node;
                projToDomainMap[node.Id] = node.Id;
            }
            else if (node.Kind == NodeKind.Topic)
            {
                if (IsInternalTopic(node))
                {
                    continue;
                }
                var name = node.DisplayName ?? node.Name ?? "";
                if (string.IsNullOrWhiteSpace(name) ||
                    name.StartsWith(':') ||
                    name.Equals("Topic", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("string", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                topicNodes[node.Id] = node;
                projToDomainMap[node.Id] = node.Id;
            }
            else if (node.Kind == NodeKind.ExternalService)
            {
                extNodes[node.Id] = node;
                projToDomainMap[node.Id] = node.Id;
            }
        }

        // 3. Aggregate Macro Edges between Domains & Infrastructure
        var macroEdges = new Dictionary<string, DomainMacroEdgeDto>(StringComparer.OrdinalIgnoreCase);
        var inCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var outCalls = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dbUsage = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var msgUsage = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in archGraph.Edges)
        {
            var srcDomain = projToDomainMap.GetValueOrDefault(edge.Source) ?? (dbNodes.ContainsKey(edge.Source) ? edge.Source : null) ?? (topicNodes.ContainsKey(edge.Source) ? edge.Source : null);
            var tgtDomain = projToDomainMap.GetValueOrDefault(edge.Target) ?? (dbNodes.ContainsKey(edge.Target) ? edge.Target : null) ?? (topicNodes.ContainsKey(edge.Target) ? edge.Target : null) ?? (extNodes.ContainsKey(edge.Target) ? edge.Target : null);

            if (srcDomain == null || tgtDomain == null || srcDomain.Equals(tgtDomain, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string cat;
            string label;

            var isTopicEdge = topicNodes.ContainsKey(tgtDomain) || topicNodes.ContainsKey(srcDomain);

            if (dbNodes.ContainsKey(tgtDomain) || edge.Category == "database" || edge.Kind == "USES_DB")
            {
                cat = "database";
                label = "USES_DB";
                if (!dbUsage.ContainsKey(srcDomain)) dbUsage[srcDomain] = new(StringComparer.OrdinalIgnoreCase);
                dbUsage[srcDomain].Add(tgtDomain);
            }
            else if (isTopicEdge)
            {
                cat = "messaging";
                var isPub = edge.Kind is "PUBLISHES_TO" or "PUBLISHED_BY" or "PUBLISHES";
                var isSub = !isPub && (edge.Kind is "SUBSCRIBES_TO" or "SUBSCRIBED_BY" or "SUBSCRIBES" || (topicNodes.ContainsKey(srcDomain) && !topicNodes.ContainsKey(tgtDomain)));
                if (isSub)
                {
                    label = "SUBSCRIBES";
                }
                else
                {
                    label = "PUBLISHES";
                }
                if (topicNodes.ContainsKey(tgtDomain))
                {
                    if (!msgUsage.ContainsKey(srcDomain)) msgUsage[srcDomain] = new(StringComparer.OrdinalIgnoreCase);
                    msgUsage[srcDomain].Add(tgtDomain);
                }
                else if (topicNodes.ContainsKey(srcDomain))
                {
                    if (!msgUsage.ContainsKey(tgtDomain)) msgUsage[tgtDomain] = new(StringComparer.OrdinalIgnoreCase);
                    msgUsage[tgtDomain].Add(srcDomain);
                }
            }
            else if (extNodes.ContainsKey(tgtDomain))
            {
                cat = "external";
                label = "CALLS";
            }
            else if (edge.Kind is "SUBSCRIBES_TO" or "SUBSCRIBED_BY" or "PUBLISHES_TO" or "PUBLISHED_BY")
            {
                // Direct pub/sub chord between services without a topic node is a phantom artifact; ignore it
                continue;
            }
            else
            {
                cat = "service_call";
                label = "CALLS";
            }

            if (cat == "service_call")
            {
                outCalls[srcDomain] = outCalls.GetValueOrDefault(srcDomain, 0) + 1;
                inCalls[tgtDomain] = inCalls.GetValueOrDefault(tgtDomain, 0) + 1;
            }

            var edgeKey = $"{srcDomain}->{tgtDomain}:{cat}";
            if (macroEdges.TryGetValue(edgeKey, out var existing))
            {
                existing.Count++;
                existing.IsTransitive = existing.IsTransitive || edge.IsTransitive;
                existing.IsInferred = existing.IsInferred || edge.IsInferred;
            }
            else
            {
                macroEdges[edgeKey] = new DomainMacroEdgeDto
                {
                    Id = edgeKey,
                    Source = srcDomain,
                    Target = tgtDomain,
                    Category = cat,
                    Label = label,
                    Count = 1,
                    IsTransitive = edge.IsTransitive,
                    IsInferred = edge.IsInferred
                };
            }
        }

        // 4. Build Domain Entities
        var stats = new DomainStatsDto();
        var nodes = new List<DomainEntityDto>();

        foreach (var (domainId, meta) in domainNameMap)
        {
            var projects = domainProjectsMap.GetValueOrDefault(domainId, []);
            var primary = domainPrimaryMap.GetValueOrDefault(domainId);

            var isLibDomain = projects.Count > 0
                ? projects.All(p => p.IsLibrary || p.Kind is NodeKind.Library or NodeKind.SharedLibrary)
                : (primary != null && IsLibraryProject(primary));

            if (isLibDomain)
            {
                // Pure library domains MUST NOT appear on Domain Service Map
                continue;
            }

            var zone = domainZoneMap.GetValueOrDefault(domainId, "service");
            var isIngress = zone == "ingress";
            var isWorker = primary?.Kind == NodeKind.Worker ||
                           primary?.Properties?.GetValueOrDefault("role") == "Worker" ||
                           projects.Any(p => p.Kind == NodeKind.Worker);
            var nonLibProjects = projects.Where(p => !p.IsLibrary && p.Kind is not (NodeKind.Library or NodeKind.SharedLibrary)).ToList();
            var isCli = (nonLibProjects.Count > 0 && nonLibProjects.All(p => p.Kind == NodeKind.CliTool || string.Equals(p.Role, "CliTool", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Role, "ScriptProject", StringComparison.OrdinalIgnoreCase))) ||
                        primary?.Kind == NodeKind.CliTool ||
                        string.Equals(primary?.Properties?.GetValueOrDefault("role"), "CliTool", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(primary?.Properties?.GetValueOrDefault("role"), "ScriptProject", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(primary?.Properties?.GetValueOrDefault("entity_kind"), "CliTool", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(primary?.Properties?.GetValueOrDefault("entity_kind"), "ScriptProject", StringComparison.OrdinalIgnoreCase);

            var isApp = (primary?.Kind == NodeKind.App ||
                         string.Equals(primary?.Properties?.GetValueOrDefault("role"), "FrontendApp", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(primary?.Properties?.GetValueOrDefault("role"), "App", StringComparison.OrdinalIgnoreCase) ||
                         projects.Any(p => p.Kind == NodeKind.App || string.Equals(p.Role, "FrontendApp", StringComparison.OrdinalIgnoreCase) || string.Equals(p.Role, "App", StringComparison.OrdinalIgnoreCase)));

            string tag;
            NodeKind nodeKind;
            string bgColor;
            string borderColor;
            int size;

            if (isCli)
            {
                stats.CliTools++;
                tag = ":CLI";
                nodeKind = NodeKind.CliTool;
                bgColor = "#0284c7";
                borderColor = "#0369a1";
                size = 50;
            }
            else if (isApp)
            {
                stats.Ingress++;
                tag = ":App";
                nodeKind = NodeKind.App;
                bgColor = "#0288d1";
                borderColor = "#01579b";
                size = 54;
            }
            else if (isWorker)
            {
                stats.Workers++;
                tag = ":Worker";
                nodeKind = NodeKind.Worker;
                bgColor = "#c026d3";
                borderColor = "#86198f";
                size = 48;
            }
            else
            {
                stats.Services++;
                tag = ":Service";
                nodeKind = NodeKind.Service;
                bgColor = "#e53935";
                borderColor = "#7f1d1d";
                size = 50;
            }

            nodes.Add(new DomainEntityDto
            {
                Id = domainId,
                Name = meta.Name,
                DisplayName = meta.DisplayName,
                Kind = nodeKind,
                DisplayTag = tag,
                Zone = zone,
                BgColor = bgColor,
                BorderColor = borderColor,
                Size = size,
                Framework = meta.Framework,
                Language = meta.Language,
                PrimaryFilePath = primary?.FilePath ?? projects.FirstOrDefault()?.FilePath,
                Projects = projects,
                InboundCallsCount = inCalls.GetValueOrDefault(domainId),
                OutboundCallsCount = outCalls.GetValueOrDefault(domainId),
                DbCount = dbUsage.GetValueOrDefault(domainId)?.Count ?? 0,
                MessagingCount = msgUsage.GetValueOrDefault(domainId)?.Count ?? 0
            });
        }

        // Databases
        foreach (var (dbId, dbNode) in dbNodes)
        {
            var isUsed = macroEdges.Values.Any(e => e.Target.Equals(dbId, StringComparison.OrdinalIgnoreCase) || e.Source.Equals(dbId, StringComparison.OrdinalIgnoreCase));
            if (!isUsed) continue;

            stats.Databases++;
            nodes.Add(new DomainEntityDto
            {
                Id = dbId,
                Name = dbNode.Name,
                DisplayName = dbNode.DisplayName ?? dbNode.Name,
                Kind = NodeKind.Database,
                DisplayTag = ":DB",
                Zone = "database",
                BgColor = "#7b1fa2",
                BorderColor = "#4a148c",
                Size = 46,
                Framework = dbNode.Properties?.GetValueOrDefault("db_type", "relational")
            });
        }

        // Topics
        foreach (var (tId, tNode) in topicNodes)
        {
            var isUsed = macroEdges.Values.Any(e => e.Target.Equals(tId, StringComparison.OrdinalIgnoreCase) || e.Source.Equals(tId, StringComparison.OrdinalIgnoreCase));
            if (!isUsed && topicNodes.Count > 25) continue;

            stats.Topics++;
            nodes.Add(new DomainEntityDto
            {
                Id = tId,
                Name = tNode.Name,
                DisplayName = tNode.DisplayName ?? tNode.Name,
                Kind = NodeKind.Topic,
                DisplayTag = ":Topic",
                Zone = "topic",
                BgColor = "#f59e0b",
                BorderColor = "#b45309",
                Size = 44,
                Framework = tNode.Properties?.GetValueOrDefault("broker_type", "Topic")
            });
        }

        // External Services
        foreach (var (extId, extNode) in extNodes)
        {
            var isUsed = macroEdges.Values.Any(e => e.Target.Equals(extId, StringComparison.OrdinalIgnoreCase) || e.Source.Equals(extId, StringComparison.OrdinalIgnoreCase));
            if (!isUsed) continue;

            stats.External++;
            nodes.Add(new DomainEntityDto
            {
                Id = extId,
                Name = extNode.Name,
                DisplayName = extNode.DisplayName ?? extNode.Name,
                Kind = NodeKind.ExternalService,
                DisplayTag = ":External",
                Zone = "external",
                BgColor = "#26a69a",
                BorderColor = "#004d40",
                Size = 44,
                Framework = extNode.Properties?.GetValueOrDefault("service_type", "External")
            });
        }

        var validNodeIds = nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filteredEdges = macroEdges.Values.Where(e => validNodeIds.Contains(e.Source) && validNodeIds.Contains(e.Target)).ToList();

        stats.TotalDomains = nodes.Count(n => n.Kind is NodeKind.Service or NodeKind.Ingress or NodeKind.App or NodeKind.CliTool or NodeKind.Worker);
        stats.ServiceCalls = filteredEdges.Where(e => e.Category == "service_call").Sum(e => e.Count);
        stats.Messages = filteredEdges.Where(e => e.Category == "messaging").Sum(e => e.Count);

        result.Nodes = nodes;
        result.Edges = filteredEdges;
        result.Stats = stats;
        return result;
    }

    public async Task<GraphDataDto> GetDomainArchitectureGraphAsync(bool includeLibraries = false, bool sharedDatabasesOnly = false, CancellationToken ct = default)
    {
        var domainDto = await GetDomainArchitectureAsync(includeLibraries, ct);
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "DomainMap",
                ["level"] = "DomainMap",
                ["graphType"] = "architecture"
            }
        };

        foreach (var d in domainDto.Nodes)
        {
            graph.Nodes.Add(new GraphNodeDto
            {
                Id = d.Id,
                Kind = d.Kind,
                Name = d.Name,
                DisplayName = d.DisplayName,
                FilePath = d.PrimaryFilePath,
                Properties = new Dictionary<string, string>
                {
                    ["displayTag"] = d.DisplayTag,
                    ["zone"] = d.Zone,
                    ["bgColor"] = d.BgColor,
                    ["borderColor"] = d.BorderColor,
                    ["size"] = d.Size.ToString(),
                    ["inboundCalls"] = d.InboundCallsCount.ToString(),
                    ["outboundCalls"] = d.OutboundCallsCount.ToString(),
                    ["dbCount"] = d.DbCount.ToString(),
                    ["messagingCount"] = d.MessagingCount.ToString(),
                    ["framework"] = d.Framework ?? "",
                    ["language"] = d.Language ?? ""
                }
            });
        }

        foreach (var e in domainDto.Edges)
        {
            graph.Edges.Add(new GraphEdgeDto
            {
                Id = e.Id,
                Source = e.Source,
                Target = e.Target,
                Kind = e.Label,
                Category = e.Category,
                Properties = new Dictionary<string, string>
                {
                    ["count"] = e.Count.ToString(),
                    ["label"] = e.Label,
                    ["category"] = e.Category
                }
            });
        }

        if (sharedDatabasesOnly)
        {
            var dbInDegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var edge in graph.Edges)
            {
                if (edge.Category == "database" || edge.Kind == "USES_DB")
                {
                    dbInDegree[edge.Target] = dbInDegree.GetValueOrDefault(edge.Target) + 1;
                }
            }

            var singleConnDbIds = graph.Nodes
                .Where(n => n.Kind == NodeKind.Database && dbInDegree.GetValueOrDefault(n.Id) <= 1)
                .Select(n => n.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (singleConnDbIds.Count > 0)
            {
                graph.Nodes.RemoveAll(n => singleConnDbIds.Contains(n.Id));
                graph.Edges.RemoveAll(e => singleConnDbIds.Contains(e.Target) || singleConnDbIds.Contains(e.Source));
            }
        }

        return graph;
    }

    public async Task<GraphDataDto> GetTieredArchitectureGraphAsync(bool includeLibraries = true, CancellationToken ct = default)
    {
        var dbKey = ResolveDbCachePrefix();
        var cacheKey = $"{dbKey}:tiers:libs={includeLibraries}";
        if (SystemContextCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var arch = await BuildBaseArchitectureTopologyAsync(
            includeLibraries: includeLibraries,
            includeTopics: false,
            projectFilter: null,
            sharedDatabasesOnly: false,
            ct: ct);

        arch.Metadata ??= new Dictionary<string, string>();
        arch.Metadata["view"] = "Tiers";
        arch.Metadata["level"] = "Tiers";
        arch.Metadata["graphType"] = "tiers";

        SystemContextCache[cacheKey] = arch;
        return arch;
    }
}
