using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Analysis;

public partial class ArchitectureViewEngine
{
    private static readonly string[] ContextPalette = [
        "#3b82f6", // Blue
        "#10b981", // Emerald
        "#8b5cf6", // Purple
        "#f59e0b", // Amber
        "#ec4899", // Pink
        "#06b6d4", // Cyan
        "#f97316", // Orange
        "#14b8a6", // Teal
        "#a855f7", // Violet
        "#6366f1", // Indigo
        "#84cc16", // Lime
        "#ef4444"  // Rose
    ];

    public record BoundedContextCategory(
        string CanonicalKey,
        string CanonicalName,
        string DisplayName,
        string DomainId,
        string DomainName,
        string DomainType,
        string Summary,
        string BgColor,
        string BorderColor
    )
    {
        public void Deconstruct(out string canonicalKey, out string canonicalName, out string displayName, out string summary, out string bgColor, out string borderColor)
        {
            canonicalKey = CanonicalKey;
            canonicalName = CanonicalName;
            displayName = DisplayName;
            summary = Summary;
            bgColor = BgColor;
            borderColor = BorderColor;
        }
    }

    public static BoundedContextCategory CategorizeBoundedContext(string? rawDomain, string? filePath = null, string? projectName = null)
    {
        // 1. Check if user configured explicit domain via .codeexplorer/domains.json or conventions.json
        if (WorkspaceConventions.TryGetConfiguredDomain(projectName ?? rawDomain, filePath, out var userConfigured))
        {
            var p = ToPascalCase(userConfigured);
            var k = p.ToLowerInvariant();
            var dName = WorkspaceConventions.FormatDomainDisplayName(p);
            return new BoundedContextCategory(
                k, p, dName,
                $"domain:{k}", dName, "Core",
                $"Configured bounded context for {dName}.",
                "#2563eb", "#1d4ed8"
            );
        }

        var candidate = (projectName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = (rawDomain ?? "").Trim();
        }

        if (candidate.StartWithIC("context:"))
            candidate = candidate["context:".Length..];
        if (candidate.StartWithIC("dom:"))
            candidate = candidate["dom:".Length..];
        if (candidate.StartWithIC("domain:"))
            candidate = candidate["domain:".Length..];

        if (IsRunawayDomainName(candidate))
        {
            candidate = WorkspaceConventions.CanonicalizeDomain(candidate, projectName, filePath);
        }

        // Clean project name to domain if it contains dots or resembles a project name
        if (candidate.Contains('.') || candidate.Contains('/') || candidate.Contains('\\'))
        {
            var cleanedProj = ResourceSemanticAnalyzer.CleanProjectNameToDomain(candidate);
            if (!string.IsNullOrWhiteSpace(cleanedProj))
                candidate = cleanedProj;
        }

        // If candidate is still empty or generic, check filePath
        if (string.IsNullOrWhiteSpace(candidate) && !string.IsNullOrWhiteSpace(filePath))
        {
            var normPath = filePath.Replace('\\', '/');
            if (normPath.StartWithIC("ui/") || 
                normPath.StartWithIC("frontend/") ||
                normPath.StartWithIC("client/") ||
                normPath.StartWithIC("web/"))
            {
                candidate = "UserInterface";
            }
            else
            {
                var segments = normPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length - 1; i++)
                {
                    var s = segments[i];
                    if (!s.Equals("src", StringComparison.OrdinalIgnoreCase) &&
                        !s.Equals("services", StringComparison.OrdinalIgnoreCase) &&
                        !s.Equals("apps", StringComparison.OrdinalIgnoreCase) &&
                        !s.Equals("packages", StringComparison.OrdinalIgnoreCase) &&
                        !s.Equals("modules", StringComparison.OrdinalIgnoreCase))
                    {
                        candidate = s;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(candidate))
            candidate = "SharedKernel";

        // Normalize service / worker / app prefixes
        var cleaned = candidate;
        foreach (var prefix in new[] { "internalservice", "internal-service-", "internal-service", "internal-", "service", "worker", "integration-service-", "integration-service", "integration-" })
        {
            if (cleaned.StartWithIC(prefix) && cleaned.Length > prefix.Length + 2)
            {
                cleaned = cleaned[prefix.Length..].TrimStart('_', '-', '.');
            }
        }
        if (cleaned.StartWithIC("app") &&
            (cleaned.StartWithIC("app_") ||
             cleaned.StartWithIC("app-") ||
             (cleaned.Length > 3 && char.IsUpper(cleaned[3]))))
        {
            cleaned = cleaned[3..].TrimStart('_', '-');
        }

        // Clean and normalize candidate into canonical Bounded Context
        var lowerNorm = cleaned.ToLowerInvariant().Replace("-", "").Replace("_", "");
        if (lowerNorm.Contains("front") || lowerNorm.Contains("ui") || lowerNorm.StartsWith("fe"))
        {
            cleaned = "Frontend";
        }

        var rawName = !string.IsNullOrWhiteSpace(projectName) ? projectName.Trim() : (!string.IsNullOrWhiteSpace(candidate) ? candidate : cleaned);
        var pascal = ToPascalCase(cleaned);
        var key = pascal.ToLowerInvariant();
        var displayName = Regex.Replace(pascal, "([a-z])([A-Z])", "$1 $2");

        // 2. Classify into Domain (Problem Space) via CanonicalizeDomain
        var canonicalDomain = WorkspaceConventions.CanonicalizeDomain(rawDomain, projectName, filePath);
        var domPascal = ToPascalCase(canonicalDomain);
        var domKey = domPascal.ToLowerInvariant();
        var domDisplayName = string.Equals(domPascal, pascal, StringComparison.OrdinalIgnoreCase)
            ? rawName
            : WorkspaceConventions.FormatDomainDisplayName(domPascal);

        // Deterministic color assignment based on domain id
        var (bgCol, borderCol) = ResolveContextColors("", $"domain:{domKey}");

        return new BoundedContextCategory(
            key,
            pascal,
            displayName,
            $"domain:{domKey}",
            domDisplayName,
            "",
            $"Bounded context for {displayName} (Domain: {domDisplayName}).",
            bgCol,
            borderCol
        );
    }

    private static readonly string[] GenericDomainIcons =
    [
        "🏛️", "📦", "⚙️", "🌐", "📊", "🔒", "💬", "💼", "🔄", "🧩", "🎯", "📡", "🚀"
    ];

    public static string ResolveDomainIcon(string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName)) return "🎯";

        if (WorkspaceConventions.TryGetDomainIcon(domainName, out var configuredIcon))
        {
            return configuredIcon;
        }

        var lower = domainName.ToLowerInvariant();
        if (lower.Contains("billing") || lower.Contains("payment") || lower.Contains("finance"))
            return "💳";
        if (lower.Contains("advert") || lower.Contains("marketing") || lower.Contains("partner"))
            return "📢";
        if (lower.Contains("campaign") || lower.Contains("offer"))
            return "🎯";
        if (lower.Contains("traffic") || lower.Contains("routing") || lower.Contains("gateway"))
            return "🌐";
        if (lower.Contains("domain") || lower.Contains("dns"))
            return "🏷️";
        if (lower.Contains("config") || lower.Contains("setting"))
            return "⚙️";
        if (lower.Contains("analytic") || lower.Contains("stat") || lower.Contains("metric") || lower.Contains("report") || lower.Contains("log"))
            return "📊";
        if (lower.Contains("operation") || lower.Contains("workflow") || lower.Contains("schedule") || lower.Contains("process"))
            return "🔄";
        if (lower.Contains("ident") || lower.Contains("auth") || lower.Contains("user") || lower.Contains("security"))
            return "🔒";
        if (lower.Contains("ui") || lower.Contains("interface") || lower.Contains("presentation") || lower.Contains("frontend"))
            return "🖥️";
        if (lower.Contains("shared") || lower.Contains("kernel") || lower.Contains("common") || lower.Contains("library"))
            return "📚";
        if (lower.Contains("tool") || lower.Contains("cli") || lower.Contains("dev"))
            return "🛠️";
        if (lower.Contains("test") || lower.Contains("fixture"))
            return "🧪";

        var hash = (uint)Math.Abs(domainName.GetHashCode());
        return GenericDomainIcons[hash % GenericDomainIcons.Length];
    }

    private static (string BgColor, string BorderColor) ResolveContextColors(string domainType, string key)
    {
        var hash = Math.Abs(key.GetHashCode());
        return (hash % 6) switch
        {
            0 => ("#2563eb", "#1d4ed8"),
            1 => ("#059669", "#047857"),
            2 => ("#4f46e5", "#4338ca"),
            3 => ("#0891b2", "#0e7490"),
            4 => ("#d97706", "#b45309"),
            _ => ("#7c3aed", "#6d28d9")
        };
    }

    private static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Default";
        var parts = text.Split(['_', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p[1..]);
            }
        }
        return sb.Length > 0 ? sb.ToString() : text;
    }

    public async Task<BoundedContextMapDto> GetBoundedContextMapAsync(string? workspaceId = null, CancellationToken ct = default)
    {
        var intents = await db.LoadExistingIntentsAsync(workspaceId ?? "", ct);
        if (intents.Count == 0 && !string.IsNullOrWhiteSpace(workspaceId))
        {
            intents = await db.LoadExistingIntentsAsync("", ct);
            if (intents.Count > 0)
            {
                workspaceId = "";
            }
        }
        var result = new BoundedContextMapDto
        {
            HasIntents = intents.Count > 0,
            TotalIntents = intents.Count
        };

        var signatures = await db.LoadProjectSignaturesAsync(workspaceId ?? "", ct);

        if (signatures.Count > 0 && (signatures.Any(s => !string.IsNullOrWhiteSpace(s.ExistingDomain)) || intents.Count == 0))
        {
            return await BuildAstBoundedContextMapAsync(signatures, workspaceId, ct);
        }

        // Map files to enclosing project boundaries using signatures
        var sigByPrefix = signatures
            .Where(s => !string.IsNullOrEmpty(s.RelativePath))
            .OrderByDescending(s => s.RelativePath.Length)
            .ToList();
        var rootSig = signatures.FirstOrDefault(s => string.IsNullOrEmpty(s.RelativePath) || s.RelativePath == ".");

        // Group intents by canonical Bounded Context
        var domainGroups = new Dictionary<string, (BoundedContextCategory Category, List<IntentRecord> Records)>(StringComparer.OrdinalIgnoreCase);
        foreach (var rec in intents)
        {
            if (string.IsNullOrWhiteSpace(rec.Domain) && string.IsNullOrWhiteSpace(rec.FilePath)) continue;
            var normRel = (rec.FilePath ?? "").Replace('\\', '/');
            var matchedProj = sigByPrefix.FirstOrDefault(s =>
                normRel.StartWithIC(s.RelativePath.Replace('\\', '/') + "/") ||
                normRel.Equals(s.RelativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                ?? rootSig;

            if (matchedProj != null && (matchedProj.Role is "Test" or "TestSuite" || IsTestOrDocsProject(matchedProj.Name, matchedProj.RelativePath)))
            {
                continue;
            }

            if (IsTestOrDocsProject(null, rec.FilePath))
            {
                continue;
            }

            var recDomain = rec.Domain;
            if (IsRunawayDomainName(recDomain))
            {
                recDomain = WorkspaceConventions.CanonicalizeDomain(recDomain, matchedProj?.Name, rec.FilePath);
            }

            if (IsTestOrDocsDomain(null, recDomain))
            {
                continue;
            }

            var cat = CategorizeBoundedContext(recDomain, rec.FilePath, matchedProj?.Name);
            if (IsTestOrDocsDomain(cat.DomainId, cat.DomainName) || IsTestOrDocsDomain(cat.CanonicalKey, cat.DisplayName))
            {
                continue;
            }

            if (!domainGroups.TryGetValue(cat.CanonicalKey, out var group))
            {
                group = (cat, []);
                domainGroups[cat.CanonicalKey] = group;
            }
            group.Records.Add(rec);
        }

        var contexts = new List<BoundedContextItemDto>();

        foreach (var (canonicalKey, (cat, records)) in domainGroups)
        {
            var layers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var patterns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var operations = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var entities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var emittedEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var handledEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var projects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<BoundedContextFileDto>();

            var pureCount = 0;
            string? representativeSummary = null;

            foreach (var r in records)
            {
                if (r.IsPureDomain == true) pureCount++;

                if (!string.IsNullOrEmpty(r.Layer))
                    layers[r.Layer] = layers.GetValueOrDefault(r.Layer) + 1;
                if (!string.IsNullOrEmpty(r.Pattern))
                    patterns[r.Pattern] = patterns.GetValueOrDefault(r.Pattern) + 1;
                if (!string.IsNullOrEmpty(r.OperationType))
                    operations[r.OperationType] = operations.GetValueOrDefault(r.OperationType) + 1;

                if (r.TargetEntities != null)
                {
                    foreach (var ent in r.TargetEntities.Where(e => !string.IsNullOrWhiteSpace(e)))
                        entities.Add(ent.Trim());
                }

                if (!string.IsNullOrWhiteSpace(r.CapabilityTag))
                    capabilities.Add(r.CapabilityTag.Trim());

                if (r.EmittedEvents != null)
                {
                    foreach (var ev in r.EmittedEvents.Where(e => !string.IsNullOrWhiteSpace(e)))
                        emittedEvents.Add(ev.Trim());
                }

                if (r.Pattern is "EventHandler" or "Consumer" && !string.IsNullOrWhiteSpace(r.CapabilityTag))
                {
                    handledEvents.Add(r.CapabilityTag.Trim());
                }

                // Deduce project name from file path if matching a recognized project boundary
                var normPath = r.FilePath.Replace('\\', '/');
                var matchedProj = sigByPrefix.FirstOrDefault(s =>
                    normPath.StartWithIC(s.RelativePath.Replace('\\', '/') + "/") ||
                    normPath.Equals(s.RelativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    ?? rootSig;

                if (matchedProj != null)
                {
                    projects.Add(matchedProj.Name);
                }

                if (representativeSummary == null && !string.IsNullOrWhiteSpace(r.IntentSummary))
                {
                    representativeSummary = r.IntentSummary;
                }

                files.Add(new BoundedContextFileDto
                {
                    FilePath = r.FilePath,
                    Layer = r.Layer,
                    Pattern = r.Pattern,
                    OperationType = r.OperationType,
                    CapabilityTag = r.CapabilityTag,
                    Summary = r.IntentSummary,
                    IsPureDomain = r.IsPureDomain
                });
            }

            var purityPct = records.Count > 0 ? (double)pureCount / records.Count * 100.0 : 0.0;

            contexts.Add(new BoundedContextItemDto
            {
                Id = $"context:{canonicalKey.ToLowerInvariant()}",
                Name = cat.CanonicalName,
                DisplayName = cat.DisplayName,
                DomainId = cat.DomainId,
                DomainName = cat.DomainName,
                DomainType = cat.DomainType,
                Summary = cat.Summary ?? representativeSummary,
                FileCount = records.Count,
                PureDomainCount = pureCount,
                PurityPercentage = Math.Round(purityPct, 1),
                Layers = layers,
                Patterns = patterns,
                Operations = operations,
                TargetEntities = [.. entities.OrderBy(e => e)],
                Capabilities = [.. capabilities.OrderBy(c => c)],
                EmittedEvents = [.. emittedEvents.OrderBy(e => e)],
                HandledEvents = [.. handledEvents.OrderBy(e => e)],
                Projects = [.. projects.OrderBy(p => p)],
                Files = [.. files.OrderBy(f => f.FilePath)],
                BgColor = cat.BgColor,
                BorderColor = cat.BorderColor,
                Size = Math.Min(140, Math.Max(70, 60 + (int)Math.Sqrt(records.Count) * 7))
            });
        }

        result.Contexts = [.. contexts.OrderByDescending(c => c.FileCount)];
        result.TotalPureDomains = contexts.Count(c => c.PurityPercentage > 50.0);

        // Aggregate domains
        var domainMap = new Dictionary<string, DomainDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in contexts)
        {
            if (!domainMap.TryGetValue(c.DomainId, out var dom))
            {
                var icon = ResolveDomainIcon(c.DomainName);

                dom = new DomainDto
                {
                    Id = c.DomainId,
                    Name = c.DomainName.Replace(" ", ""),
                    DisplayName = c.DomainName,
                    DomainType = "",
                    Icon = icon,
                    Description = $"Domain encompassing {c.DomainName} capabilities and bounded contexts.",
                    BoundedContextIds = [],
                    TotalFiles = 0,
                    TotalEntities = 0
                };
                domainMap[c.DomainId] = dom;
            }

            dom.BoundedContextIds.Add(c.Id);
            dom.TotalFiles += c.FileCount;
            dom.TotalEntities += c.TargetEntities.Count;
        }

        result.Domains =
        [
            .. domainMap.Values.OrderByDescending(d => d.TotalFiles)
        ];

        // Load strategic cross-domain interactions grouped by canonical Bounded Contexts
        var interactions = new List<BoundedContextInteractionDto>();
        var crossCalls = await db.LoadCrossDomainInteractionsAsync(workspaceId ?? "", ct);

        var macroCalls = new List<(string SourceKey, string TargetKey, string EdgeKind, int Count)>();
        foreach (var c in crossCalls)
        {
            var src = CategorizeBoundedContext(c.SourceDomain).CanonicalKey;
            var tgt = CategorizeBoundedContext(c.TargetDomain).CanonicalKey;
            if (!src.Equals(tgt, StringComparison.OrdinalIgnoreCase))
            {
                macroCalls.Add((src, tgt, c.EdgeKind, c.InteractionCount));
            }
        }

        var groupedCalls = macroCalls
            .GroupBy(c => (Source: c.SourceKey, Target: c.TargetKey));

        foreach (var group in groupedCalls)
        {
            var srcId = $"context:{group.Key.Source.ToLowerInvariant()}";
            var tgtId = $"context:{group.Key.Target.ToLowerInvariant()}";
            var totalCount = group.Sum(x => x.Count);
            var details = group.Select(x => $"{x.EdgeKind} ({x.Count})").Distinct().ToList();

            var primaryItem = group.OrderByDescending(x => x.Count).First();
            var cat = (primaryItem.EdgeKind.ToUpperInvariant()) switch
            {
                "PUBLISHES" or "SUBSCRIBES" or "TRIGGERS" or "CONSUMES" => "messaging",
                "USES_DB" or "ACCESSES_TABLE" => "database",
                _ => "service_call"
            };

            interactions.Add(new BoundedContextInteractionDto
            {
                Id = $"edge:{srcId}->{tgtId}",
                Source = srcId,
                Target = tgtId,
                InteractionType = cat,
                Label = totalCount > 1 ? $"{totalCount} calls" : primaryItem.EdgeKind,
                Count = totalCount,
                Details = details
            });
        }

        result.Interactions = interactions;
        return result;
    }

    private async Task<BoundedContextMapDto> BuildAstBoundedContextMapAsync(List<ProjectSignature> signatures, string? workspaceId, CancellationToken ct)
    {
        var hasAssignedDomains = signatures.Any(s => !string.IsNullOrWhiteSpace(s.ExistingDomain));
        var result = new BoundedContextMapDto
        {
            HasIntents = hasAssignedDomains,
            TotalIntents = signatures.Count(s => !string.IsNullOrWhiteSpace(s.ExistingDomain))
        };

        // If projects lack domain assignments, synthesize cohesive macro-domains topologically from AST
        var needsSynthesis = WorkspaceConventions.DomainDetectionEnabled &&
                             signatures.Any(s => string.IsNullOrWhiteSpace(s.ExistingDomain));
        var topoAssignments = needsSynthesis
            ? CodeIntentAnalyzer.SynthesizeDomainsTopologically(signatures)
            : null;
        var serviceToDomain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (topoAssignments?.Domains != null)
        {
            foreach (var d in topoAssignments.Domains)
            {
                foreach (var svc in d.Services)
                {
                    serviceToDomain[svc] = d.Name;
                }
            }
        }

        var contextGroups = new Dictionary<string, (BoundedContextCategory Category, List<ProjectSignature> Projects)>(StringComparer.OrdinalIgnoreCase);
        foreach (var sig in signatures)
        {
            if (sig.Role is "Test" or "TestSuite" || IsTestOrDocsProject(sig.Name, sig.RelativePath))
            {
                continue;
            }

            var effectiveDomain = !string.IsNullOrWhiteSpace(sig.ExistingDomain)
                ? sig.ExistingDomain
                : (serviceToDomain.TryGetValue(sig.Name, out var td) ? td : null);

            if (IsRunawayDomainName(effectiveDomain))
            {
                effectiveDomain = WorkspaceConventions.CanonicalizeDomain(effectiveDomain, sig.Name, sig.RelativePath, sig.Role);
            }

            if (IsTestOrDocsDomain(null, effectiveDomain))
            {
                continue;
            }

            var cat = CategorizeBoundedContext(effectiveDomain, sig.RelativePath, sig.Name);

            // The map lists services only; a library outside every configured domain is not a domain of its own.
            if (!WorkspaceConventions.DomainDetectionEnabled &&
                sig.Role is "SharedLibrary" or "Library" &&
                !WorkspaceConventions.TryGetConfiguredDomain(sig.Name, sig.RelativePath, out _))
            {
                continue;
            }

            if (IsTestOrDocsDomain(cat.DomainId, cat.DomainName) || IsTestOrDocsDomain(cat.CanonicalKey, cat.DisplayName))
            {
                continue;
            }

            if (!contextGroups.TryGetValue(cat.CanonicalKey, out var grp))
            {
                grp = (cat, []);
                contextGroups[cat.CanonicalKey] = grp;
            }
            grp.Projects.Add(sig);
        }

        var contexts = new List<BoundedContextItemDto>();
        foreach (var (canonicalKey, (cat, projs)) in contextGroups)
        {
            var entities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var emittedEvents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var projectNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in projs)
            {
                projectNames.Add(p.Name);
                foreach (var dt in p.DomainTypes) entities.Add(dt);
                foreach (var ep in p.Endpoints) capabilities.Add(ep);
                foreach (var top in p.Topics) emittedEvents.Add(top);
            }

            contexts.Add(new BoundedContextItemDto
            {
                Id = $"context:{canonicalKey.ToLowerInvariant()}",
                Name = cat.CanonicalName,
                DisplayName = cat.DisplayName,
                DomainId = cat.DomainId,
                DomainName = cat.DomainName,
                DomainType = cat.DomainType,
                Summary = cat.Summary,
                FileCount = projs.Count * 5,
                PureDomainCount = 0,
                PurityPercentage = 0.0,
                TargetEntities = [.. entities.OrderBy(e => e)],
                Capabilities = [.. capabilities.OrderBy(c => c)],
                EmittedEvents = [.. emittedEvents.OrderBy(e => e)],
                HandledEvents = [],
                Projects = [.. projectNames.OrderBy(p => p)],
                Files = [],
                BgColor = cat.BgColor,
                BorderColor = cat.BorderColor,
                Size = Math.Min(140, Math.Max(70, 60 + (int)Math.Sqrt(projs.Count * 10) * 7))
            });
        }

        result.Contexts = [.. contexts.OrderByDescending(c => c.Projects.Count)];

        var domainMap = new Dictionary<string, DomainDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in contexts)
        {
            if (!domainMap.TryGetValue(c.DomainId, out var dom))
            {
                var icon = ResolveDomainIcon(c.DomainName);

                dom = new DomainDto
                {
                    Id = c.DomainId,
                    Name = c.DomainName.Replace(" ", ""),
                    DisplayName = c.DomainName,
                    DomainType = "",
                    Icon = icon,
                    Description = $"Domain encompassing {c.DomainName} capabilities and bounded contexts.",
                    BoundedContextIds = [],
                    TotalFiles = 0,
                    TotalEntities = 0
                };
                domainMap[c.DomainId] = dom;
            }

            dom.BoundedContextIds.Add(c.Id);
            dom.TotalFiles += c.FileCount;
            dom.TotalEntities += c.TargetEntities.Count;
        }

        result.Domains =
        [
            .. domainMap.Values.OrderByDescending(d => d.TotalFiles)
        ];

        var crossCalls = await db.LoadCrossDomainInteractionsAsync(workspaceId ?? "", ct);
        var macroCalls = new List<(string SourceKey, string TargetKey, string EdgeKind, int Count)>();
        foreach (var c in crossCalls)
        {
            var src = CategorizeBoundedContext(c.SourceDomain).CanonicalKey;
            var tgt = CategorizeBoundedContext(c.TargetDomain).CanonicalKey;
            if (!src.Equals(tgt, StringComparison.OrdinalIgnoreCase))
            {
                macroCalls.Add((src, tgt, c.EdgeKind, c.InteractionCount));
            }
        }

        var groupedCalls = macroCalls.GroupBy(c => (Source: c.SourceKey, Target: c.TargetKey));
        var interactions = new List<BoundedContextInteractionDto>();
        foreach (var group in groupedCalls)
        {
            var srcId = $"context:{group.Key.Source.ToLowerInvariant()}";
            var tgtId = $"context:{group.Key.Target.ToLowerInvariant()}";
            var totalCount = group.Sum(x => x.Count);
            var details = group.Select(x => $"{x.EdgeKind} ({x.Count})").Distinct().ToList();
            var primaryItem = group.OrderByDescending(x => x.Count).First();
            var cat = (primaryItem.EdgeKind.ToUpperInvariant()) switch
            {
                "PUBLISHES" or "SUBSCRIBES" or "TRIGGERS" or "CONSUMES" => "messaging",
                "USES_DB" or "ACCESSES_TABLE" => "database",
                _ => "service_call"
            };

            interactions.Add(new BoundedContextInteractionDto
            {
                Id = $"edge:{srcId}->{tgtId}",
                Source = srcId,
                Target = tgtId,
                InteractionType = cat,
                Label = totalCount > 1 ? $"{totalCount} calls" : primaryItem.EdgeKind,
                Count = totalCount,
                Details = details
            });
        }

        result.Interactions = interactions;
        return result;
    }

    public async Task<GraphDataDto> GetBoundedContextGraphAsync(string? workspaceId = null, CancellationToken ct = default)
    {
        var map = await GetBoundedContextMapAsync(workspaceId, ct);
        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "BoundedContexts",
                ["level"] = "BoundedContexts",
                ["graphType"] = "contexts",
                ["hasIntents"] = map.HasIntents ? "true" : "false",
                ["totalContexts"] = map.Contexts.Count.ToString(),
                ["totalDomains"] = map.Domains.Count.ToString(),
                ["domains"] = JsonSerializer.Serialize(map.Domains),
                ["totalIntents"] = map.TotalIntents.ToString(),
                ["totalPureDomains"] = map.TotalPureDomains.ToString()
            }
        };

        foreach (var c in map.Contexts)
        {
            graph.Nodes.Add(new GraphNodeDto
            {
                Id = c.Id,
                Kind = NodeKind.BoundedContext,
                Name = c.Name,
                DisplayName = c.DisplayName,
                Properties = new Dictionary<string, string>
                {
                    ["displayTag"] = ":BoundedContext",
                    ["domain"] = c.Name,
                    ["displayName"] = c.DisplayName,
                    ["domainId"] = c.DomainId,
                    ["domainName"] = c.DomainName,
                    ["domainType"] = c.DomainType,
                    ["summary"] = c.Summary ?? "",
                    ["fileCount"] = c.FileCount.ToString(),
                    ["pureCount"] = c.PureDomainCount.ToString(),
                    ["purity"] = c.PurityPercentage.ToString("F1"),
                    ["entities"] = JsonSerializer.Serialize(c.TargetEntities),
                    ["capabilities"] = JsonSerializer.Serialize(c.Capabilities),
                    ["events"] = JsonSerializer.Serialize(c.EmittedEvents),
                    ["handledEvents"] = JsonSerializer.Serialize(c.HandledEvents),
                    ["layers"] = JsonSerializer.Serialize(c.Layers),
                    ["patterns"] = JsonSerializer.Serialize(c.Patterns),
                    ["operations"] = JsonSerializer.Serialize(c.Operations),
                    ["projects"] = JsonSerializer.Serialize(c.Projects),
                    ["files"] = JsonSerializer.Serialize(c.Files),
                    ["bgColor"] = c.BgColor,
                    ["borderColor"] = c.BorderColor,
                    ["size"] = c.Size.ToString()
                }
            });
        }

        var validIds = map.Contexts.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in map.Interactions.Where(e => validIds.Contains(e.Source) && validIds.Contains(e.Target)))
        {
            graph.Edges.Add(new GraphEdgeDto
            {
                Id = edge.Id,
                Source = edge.Source,
                Target = edge.Target,
                Kind = edge.Label,
                Category = edge.InteractionType,
                Properties = new Dictionary<string, string>
                {
                    ["count"] = edge.Count.ToString(),
                    ["label"] = edge.Label,
                    ["category"] = edge.InteractionType,
                    ["details"] = JsonSerializer.Serialize(edge.Details)
                }
            });
        }

        return graph;
    }

    // =========================================================================
    // 7. Service Contracts & Cross-Service Flow Tracing
    // =========================================================================

    public async Task<ServiceContractDto> GetServiceContractsAsync(string serviceName, string direction = "all", CancellationToken ct = default)
    {
        var archGraph = await BuildBaseArchitectureTopologyAsync(includeLibraries: true, includeTopics: true, projectFilter: null, sharedDatabasesOnly: false, ct: ct);
        var targetNode = archGraph.Nodes.FirstOrDefault(n => n.Name.Equals(serviceName, StringComparison.OrdinalIgnoreCase) || n.Id.Equals(serviceName, StringComparison.OrdinalIgnoreCase));

        var contract = new ServiceContractDto
        {
            ServiceName = targetNode?.Name ?? serviceName,
            Kind = targetNode?.Kind ?? NodeKind.Service,
            Framework = targetNode?.Properties?.GetValueOrDefault("framework"),
            Language = targetNode?.Properties?.GetValueOrDefault("language") ?? targetNode?.Properties?.GetValueOrDefault("project_type")
        };

        if (targetNode == null)
        {
            return contract;
        }

        var targetId = targetNode.Id;

        // Ingress
        if (direction is "all" or "ingress")
        {
            try
            {
                var epQuery = "MATCH (s)-[:CONTAINS]->(ep:Endpoint) WHERE s.id = $id OR s.name = $name RETURN ep.name AS epName";
                var epJson = await db.ExecuteQueryAsync(epQuery, new Dictionary<string, object> { ["id"] = targetId, ["name"] = serviceName }, ct);

                using var epDoc = JsonDocument.Parse(epJson);

                foreach (var row in epDoc.RootElement.EnumerateArray())
                {
                    var epName = row.GetStringProp("epName");

                    if (!string.IsNullOrEmpty(epName) && !contract.IngressEndpoints.Contains(epName))
                    {
                        contract.IngressEndpoints.Add(epName);
                    }
                }
            }
            catch { }

            foreach (var edge in archGraph.Edges)
            {
                if (edge.Target.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                {
                    if (edge.Category == "messaging" || edge.Kind is "SUBSCRIBES_TO" or "SUBSCRIBED_BY" or "TRIGGERS")
                    {
                        var srcNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Source, StringComparison.OrdinalIgnoreCase));
                        var topicName = srcNode?.Name ?? edge.Source;
                        var isInternal = edge.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                         srcNode?.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                         string.Equals(srcNode?.Properties?.GetValueOrDefault("broker_type"), "mediatr", StringComparison.OrdinalIgnoreCase) ||
                                         edge.Source.Contains(":mediatr:");
                        if (!isInternal && !contract.SubscribedTopics.Contains(topicName))
                        {
                            contract.SubscribedTopics.Add(topicName);
                        }
                    }
                }
                else if (edge.Source.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                {
                    var tgtNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Target, StringComparison.OrdinalIgnoreCase));
                    var tgtName = tgtNode?.Name ?? edge.Target;
                    var isTopic = tgtNode?.Kind == NodeKind.Topic || edge.Category == "messaging";

                    if (isTopic && edge.Kind is "SUBSCRIBES_TO" or "SUBSCRIBED_BY" or "SUBSCRIBES" or "CONSUMES")
                    {
                        var isInternal = edge.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                         tgtNode?.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                         string.Equals(tgtNode?.Properties?.GetValueOrDefault("broker_type"), "mediatr", StringComparison.OrdinalIgnoreCase) ||
                                         edge.Target.Contains(":mediatr:");
                        if (!isInternal && !contract.SubscribedTopics.Contains(tgtName))
                        {
                            contract.SubscribedTopics.Add(tgtName);
                        }
                    }
                }
            }
        }

        // Egress
        if (direction is "all" or "egress")
        {
            foreach (var edge in archGraph.Edges)
            {
                if (edge.Source.Equals(targetId, StringComparison.OrdinalIgnoreCase))
                {
                    var tgtNode = archGraph.Nodes.FirstOrDefault(n => n.Id.Equals(edge.Target, StringComparison.OrdinalIgnoreCase));
                    var tgtName = tgtNode?.Name ?? edge.Target;

                    if (edge.Category == "database" || edge.Kind == "USES_DB" || tgtNode?.Kind == NodeKind.Database)
                    {
                        if (!contract.Databases.Contains(tgtName)) contract.Databases.Add(tgtName);
                    }
                    else if (edge.Category == "messaging" || edge.Kind is "PUBLISHES_TO" or "PUBLISHES" or "TRIGGERS" || tgtNode?.Kind == NodeKind.Topic)
                    {
                        if (edge.Kind is not ("SUBSCRIBES_TO" or "SUBSCRIBED_BY" or "SUBSCRIBES" or "CONSUMES"))
                        {
                            var isInternal = edge.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                             tgtNode?.Properties?.GetValueOrDefault("is_internal") == "true" ||
                                             string.Equals(tgtNode?.Properties?.GetValueOrDefault("broker_type"), "mediatr", StringComparison.OrdinalIgnoreCase) ||
                                             edge.Target.Contains(":mediatr:");
                            if (!isInternal && !contract.PublishedTopics.Contains(tgtName)) contract.PublishedTopics.Add(tgtName);
                        }
                    }
                    else if (tgtNode?.Kind == NodeKind.ExternalService)
                    {
                        if (!contract.ExternalServices.Contains(tgtName)) contract.ExternalServices.Add(tgtName);
                    }
                    else if (edge.Category == "service_call" || edge.Kind is "SERVICE_CALL" or "CALLS_ENDPOINT")
                    {
                        if (!contract.OutboundServiceCalls.Contains(tgtName)) contract.OutboundServiceCalls.Add(tgtName);
                    }
                }
            }
        }

        return contract;
    }

    public async Task<CrossServiceFlowDto> TraceCrossServiceFlowAsync(string startService, string? entryPoint = null, int maxDepth = 3, CancellationToken ct = default)
    {
        var archGraph = await BuildBaseArchitectureTopologyAsync(includeLibraries: true, includeTopics: true, projectFilter: null, sharedDatabasesOnly: false, ct: ct);
        var startNode = archGraph.Nodes.FirstOrDefault(n => n.Name.Equals(startService, StringComparison.OrdinalIgnoreCase) || n.Id.Equals(startService, StringComparison.OrdinalIgnoreCase));

        var flow = new CrossServiceFlowDto
        {
            StartService = startNode?.Name ?? startService,
            EntryPoint = entryPoint,
            MaxDepth = Math.Clamp(maxDepth, 1, 5)
        };

        if (startNode == null)
        {
            return flow;
        }

        var nodeLookup = archGraph.Nodes.GroupBy(n => n.Id, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startNode.Id };
        var queue = new Queue<(string CurrentId, int Depth)>();
        queue.Enqueue((startNode.Id, 0));

        int step = 1;
        while (queue.Count > 0)
        {
            var (currId, depth) = queue.Dequeue();
            var currNode = nodeLookup.GetValueOrDefault(currId);
            if (currNode != null && !flow.VisitedServices.Contains(currNode.Name))
            {
                flow.VisitedServices.Add(currNode.Name);
            }

            if (depth >= flow.MaxDepth) continue;

            foreach (var edge in archGraph.Edges)
            {
                if (edge.Source.Equals(currId, StringComparison.OrdinalIgnoreCase))
                {
                    var tgtNode = nodeLookup.GetValueOrDefault(edge.Target);
                    var tgtName = tgtNode?.Name ?? edge.Target;

                    flow.Hops.Add(new CrossServiceHopDto
                    {
                        Step = step++,
                        Source = currNode?.Name ?? currId,
                        Target = tgtName,
                        Kind = edge.Kind,
                        Protocol = edge.Properties?.GetValueOrDefault("protocol") ?? edge.Category,
                        Details = edge.Properties?.GetValueOrDefault("via_library") != null ? $"via {edge.Properties["via_library"]}" : null
                    });

                    if (visited.Add(edge.Target))
                    {
                        queue.Enqueue((edge.Target, depth + 1));
                    }
                }
            }
        }

        return flow;
    }
}
