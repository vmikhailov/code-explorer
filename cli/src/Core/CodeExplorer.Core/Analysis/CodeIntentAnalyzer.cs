using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Core.Analysis;


public record BatchInferenceResult(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("file_path")] string? FilePath,
    [property: JsonPropertyName("domain")] string? Domain,
    [property: JsonPropertyName("layer")] string? Layer,
    [property: JsonPropertyName("pattern")] string? Pattern,
    [property: JsonPropertyName("operation_type")] string? OperationType,
    [property: JsonPropertyName("capability_tag")] string? CapabilityTag,
    [property: JsonPropertyName("intent_summary")] string? IntentSummary,
    [property: JsonPropertyName("is_pure_domain")] bool? IsPureDomain,
    [property: JsonPropertyName("target_entities")] List<string>? TargetEntities,
    [property: JsonPropertyName("emitted_events")] List<string>? EmittedEvents
);

public record ConfiguredDomainDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("keywords")] List<string>? Keywords = null
);

public record WorkspaceDomainsConfig(
    [property: JsonPropertyName("domains")] List<ConfiguredDomainDefinition> Domains
);

public static class CodeIntentAnalyzer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool Enabled { get; set; } = true;

    public static bool ShouldRun(ParsingContext ctx)
    {
        if (!Enabled) return false;
        if (Environment.GetEnvironmentVariable("DISABLE_CODE_INTENT") == "1") return false;

        // Skip during automated test runner execution unless explicitly enabled
        if (Environment.GetEnvironmentVariable("ENABLE_CODE_INTENT_IN_TESTS") != "1")
        {
            var isTestRunner = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.FullName != null && (a.FullName.Contains("testhost", StringComparison.OrdinalIgnoreCase) || a.FullName.Contains("nunit", StringComparison.OrdinalIgnoreCase)));
            if (isTestRunner) return false;
        }

        return true;
    }

    public static async Task EnrichAsync(ParsingContext ctx, CancellationToken cancellationToken = default)
    {
        if (!ShouldRun(ctx)) return;

        if (!ctx.EnableIntentAnalysis)
        {
            // Fast path: Apply existing cached architectural intents in <50ms without running LLM
            try
            {
                var applied = await ctx.DbClient.ApplyCachedIntentsToGraphAsync(ctx.WorkspaceId, cancellationToken);
                if (applied > 0)
                {
                    ctx.Log($"[CodeIntent] Fast-applied cached architectural intents to {applied} nodes.");
                }

                // If projects still lack domain assignments, synthesize them topologically from AST
                var signatures = await ctx.DbClient.LoadProjectSignaturesAsync(ctx.WorkspaceId, cancellationToken);
                var unassigned = signatures.Where(s => string.IsNullOrWhiteSpace(s.ExistingDomain)).ToList();
                if (unassigned.Count > 0)
                {
                    var wsPath = !string.IsNullOrEmpty(ctx.HostWorkspacePath)
                        ? ctx.HostWorkspacePath
                        : ctx.AbsoluteWorkspacePath;
                    var domainConfig = LoadWorkspaceDomainsConfig(wsPath);
                    var topoDomains = SynthesizeDomainsTopologically(signatures, domainConfig);
                    if (topoDomains.Domains is { Count: > 0 })
                    {
                        var toSave = new Dictionary<string, (string Domain, string Summary, List<string> Capabilities)>(StringComparer.OrdinalIgnoreCase);
                        foreach (var d in topoDomains.Domains)
                        {
                            foreach (var svc in d.Services)
                            {
                                toSave[svc.Trim()] = (d.Name, d.Description, new List<string>());
                            }
                        }
                        if (toSave.Count > 0)
                        {
                            await ctx.DbClient.SaveProjectIntentsAsync(ctx.WorkspaceId, toSave, cancellationToken);
                            ctx.Log($"[CodeIntent] Materialized {topoDomains.Domains.Count} topological AST domains across {toSave.Count} services.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ctx.LogDebug($"[CodeIntent] Note on applying cached intents: {ex.Message}");
            }
            return;
        }

        await RunIncrementalIntentAnalysisAsync(ctx, limit: null, serviceFilter: null, domainsOnly: false, reanalyze: false, deep: false, cancellationToken: cancellationToken);
    }

    public static async Task<int> RunIncrementalIntentAnalysisAsync(
        ParsingContext ctx,
        int? limit = null,
        string? serviceFilter = null,
        bool domainsOnly = false,
        bool reanalyze = false,
        bool deep = false,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldRun(ctx)) return 0;

        var modelPath = await ModelManager.EnsureModelAvailableAsync(ctx, cancellationToken);
        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
        {
            ctx.Log("[CodeIntent] Intent distillation model not available; skipping intent distillation.");
            return 0;
        }

        try
        {
            var gpuLayers = 99;
            if (int.TryParse(Environment.GetEnvironmentVariable("CODE_INTENT_GPU_LAYERS"), out var envLayers))
            {
                gpuLayers = envLayers;
            }

            var contextSize = 8192;
            if (int.TryParse(Environment.GetEnvironmentVariable("CODE_INTENT_CONTEXT_SIZE"), out var envContext) && envContext >= 1024)
            {
                contextSize = envContext;
            }

            using (var predictor = new NativeIntentPredictor(modelPath, contextSize: contextSize, gpuLayers: gpuLayers))
            {
                ctx.Log($"[CodeIntent] Compute Device: {predictor.ExecutionDevice} (GPU layers: {gpuLayers})");
                ctx.Log($"[CodeIntent] Concurrency: {predictor.Concurrency}x ({predictor.ConcurrencyReason})");

                // Top-Down Phase 0: Whole-System Macro-Domain Synthesis (DDD Problem Space)
                var projectSignatures =
                    await ctx.DbClient.LoadProjectSignaturesAsync(ctx.WorkspaceId, cancellationToken);

                var projectDomainMap =
                    new ConcurrentDictionary<string, (string Domain, string Role)>(StringComparer.OrdinalIgnoreCase);

                var projectIntentsToSave =
                    new Dictionary<string, (string Domain, string Summary, List<string> Capabilities)>(StringComparer
                        .OrdinalIgnoreCase);

                if (projectSignatures.Count > 0)
                {
                    var distinctExistingDomains = projectSignatures
                        .Where(s => !string.IsNullOrWhiteSpace(s.ExistingDomain)).Select(s => s.ExistingDomain!)
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                    var unclassifiedCount = projectSignatures.Count(s => string.IsNullOrWhiteSpace(s.ExistingDomain));

                    // If projects lack domain assignment, or if existing domains are fragmented (more than 10 micro-domains across the system), synthesize cohesive macro-domains
                    var needsGlobalSynthesis = unclassifiedCount > 0 ||
                                               (projectSignatures.Count >= 8 && distinctExistingDomains.Count > 10);

                    var filterList = !string.IsNullOrWhiteSpace(serviceFilter)
                        ? serviceFilter.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim().ToLowerInvariant())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .ToList()
                        : new List<string>();

                    var businessServices = new List<ProjectSignature>();
                    var technicalContexts = new List<ProjectBoundedContextResult>();

                    foreach (var sig in projectSignatures)
                    {
                        var role = sig.Role;

                        // 1. Semantic classification from Graph Ontology
                        if (role is "SharedLibrary" or "Library")
                        {
                            var isUi = !string.IsNullOrWhiteSpace(sig.RelativePath) &&
                                       (sig.RelativePath.Contains("/ui/") || sig.RelativePath.Contains("/components/") || sig.RelativePath.Contains("/packages/ui"));

                            if (isUi)
                            {
                                technicalContexts.Add(new ProjectBoundedContextResult(
                                    sig.Name, "PresentationComponents", ["Widget"], "UI presentation component and frontend widget.", "UserInterface"));
                            }
                            else
                            {
                                technicalContexts.Add(new ProjectBoundedContextResult(
                                    sig.Name, "SharedKernel", ["CommonModel"], "Shared cross-cutting primitives, contracts, and common utilities.", "SharedKernel"));
                            }
                            continue;
                        }

                        if (role is "CliTool")
                        {
                            technicalContexts.Add(new ProjectBoundedContextResult(
                                sig.Name, "DeveloperTooling", ["CliCommand"], "Developer CLI and migration tooling.", "DeveloperTooling"));
                            continue;
                        }

                        if (role is "Test")
                        {
                            technicalContexts.Add(new ProjectBoundedContextResult(
                                sig.Name, "TestingInfrastructure", ["TestFixture"], "Automated test fixtures and testing harness.", "TestingInfrastructure"));
                            continue;
                        }

                        // Workloads (Service, Worker, FrontendApp, or unclassified fallback) are business services!
                        businessServices.Add(sig);
                    }

                    var targetServices = filterList.Count > 0
                        ? businessServices.Where(s => filterList.Any(f => s.Name.ToLowerInvariant().Contains(f) || (!string.IsNullOrEmpty(s.RelativePath) && s.RelativePath.ToLowerInvariant().Contains(f)))).ToList()
                        : businessServices;

                    if (targetServices.Count > 0)
                    {
                        var modeText = deep ? "Multi-Turn Agentic ReAct Exploration" : "Single-Shot Graph Traversal";
                        ctx.Log($"[CodeIntent] Distilling Bounded Contexts & Aggregates across {targetServices.Count} service(s) on {predictor.ExecutionDevice} (mode: {modeText}, concurrency: {predictor.Concurrency}x)...");

                        var distilledContexts = new ConcurrentBag<ProjectBoundedContextResult>();
                        var explorer = new ServiceGraphExplorer(ctx.DbClient);

                        var tasks = targetServices.Select(async sig =>
                        {
                            try
                            {
                                ProjectBoundedContextResult? pbc;
                                if (deep)
                                {
                                    pbc = await predictor.PredictProjectBoundedContextAgenticAsync(
                                        sig,
                                        explorer,
                                        maxTurns: 3,
                                        logger: msg => ctx.Log(msg),
                                        cancellationToken);
                                }
                                else
                                {
                                    pbc = await predictor.PredictProjectBoundedContextAsync(sig, cancellationToken);
                                }

                                if (pbc != null)
                                {
                                    var canonicalDomain = WorkspaceConventions.CanonicalizeDomain(pbc.SuggestedDomain, sig.Name, sig.RelativePath, sig.Role);
                                    var pbcCanonical = pbc with { SuggestedDomain = canonicalDomain };
                                    distilledContexts.Add(pbcCanonical);
                                    ctx.Log($"  - [{sig.Name}] -> Context: {pbc.BoundedContext} | Aggregates: [{(pbc.PrimaryAggregates.Count > 0 ? string.Join(", ", pbc.PrimaryAggregates) : "None")}] | Domain: {canonicalDomain}");
                                    projectDomainMap[sig.Name] = (canonicalDomain, pbc.Capability);
                                    projectIntentsToSave[sig.Name] = (canonicalDomain, pbc.Capability, pbc.PrimaryAggregates);
                                }
                            }
                            catch (Exception ex)
                            {
                                ctx.Log($"  [!] Failed to distill context for {sig.Name}: {ex.Message}");
                            }
                        });

                        await Task.WhenAll(tasks);

                        var wsPath = !string.IsNullOrEmpty(ctx.HostWorkspacePath)
                            ? ctx.HostWorkspacePath
                            : ctx.AbsoluteWorkspacePath;
                        var domainConfig = LoadWorkspaceDomainsConfig(wsPath);

                        // If whole-system analysis, synthesize Macro-Domains from Bounded Contexts
                        if (filterList.Count == 0)
                        {
                            var allForMacro = distilledContexts.Concat(technicalContexts).ToList();

                            if (domainConfig != null && domainConfig.Domains.Count > 0)
                            {
                                ctx.Log($"[CodeIntent] Matching Bounded Contexts against {domainConfig.Domains.Count} configured domains from .codeexplorer/domains.json...");
                                var topoResult = SynthesizeDomainsTopologically(projectSignatures, domainConfig);
                                if (topoResult?.Domains != null)
                                {
                                    ctx.Log($"[CodeIntent] Grouped into {topoResult.Domains.Count} Business Domains via configuration:");
                                    foreach (var d in topoResult.Domains)
                                    {
                                        ctx.Log($"  - [{d.Name}] ({d.Services.Count} services): {d.Description}");
                                        foreach (var svc in d.Services)
                                        {
                                            var cleanSvc = svc.Trim();
                                            var existingSummary = projectIntentsToSave.TryGetValue(cleanSvc, out var existing) ? existing.Summary : d.Description;
                                            var existingAggs = existing.Capabilities ?? new List<string>();
                                            projectDomainMap[cleanSvc] = (d.Name, existingSummary);
                                            projectIntentsToSave[cleanSvc] = (d.Name, existingSummary, existingAggs);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                SystemDomainsResult? macroDomains = null;
                                try
                                {
                                    ctx.Log($"[CodeIntent] Synthesizing Macro-Domains via LLM from {allForMacro.Count} Bounded Contexts on {predictor.ExecutionDevice}...");
                                    macroDomains = await predictor.PredictMacroDomainsFromContextsAsync(allForMacro, cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    ctx.LogWarning($"[CodeIntent] Note on LLM macro-domain synthesis: {ex.Message}. Falling back to graph topology synthesis.");
                                }

                                if (macroDomains?.Domains != null && macroDomains.Domains.Count > 0)
                                {
                                    ctx.Log($"[CodeIntent] Discovered {macroDomains.Domains.Count} Business Domains via LLM synthesis:");
                                    foreach (var d in macroDomains.Domains)
                                    {
                                        var canonicalName = WorkspaceConventions.CanonicalizeDomain(d.Name);
                                        ctx.Log($"  - [{canonicalName}] ({d.Services.Count} services): {d.Description}");
                                        foreach (var svc in d.Services)
                                        {
                                            var cleanSvc = svc.Trim();
                                            var existingSummary = projectIntentsToSave.TryGetValue(cleanSvc, out var existing) ? existing.Summary : d.Description;
                                            var existingAggs = existing.Capabilities ?? new List<string>();
                                            projectDomainMap[cleanSvc] = (canonicalName, existingSummary);
                                            projectIntentsToSave[cleanSvc] = (canonicalName, existingSummary, existingAggs);
                                        }
                                    }
                                }
                                else
                                {
                                    var topoResult = SynthesizeDomainsTopologically(projectSignatures, null);
                                    if (topoResult?.Domains != null)
                                    {
                                        foreach (var d in topoResult.Domains)
                                        {
                                            var canonicalName = WorkspaceConventions.CanonicalizeDomain(d.Name);
                                            foreach (var svc in d.Services)
                                            {
                                                var cleanSvc = svc.Trim();
                                                var existingSummary = projectIntentsToSave.TryGetValue(cleanSvc, out var existing) ? existing.Summary : d.Description;
                                                var existingAggs = existing.Capabilities ?? new List<string>();
                                                projectDomainMap[cleanSvc] = (canonicalName, existingSummary);
                                                projectIntentsToSave[cleanSvc] = (canonicalName, existingSummary, existingAggs);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Targeted services: reconcile domain via topological graph affinity (shared tables, topics, and stems)
                            var enrichedTargetSignatures = new List<ProjectSignature>();
                            foreach (var sig in targetServices)
                            {
                                var tables = sig.Tables;
                                if (tables.Count == 0)
                                {
                                    var lineage = await explorer.GetDataLineageAsync(sig.Name, cancellationToken);
                                    tables = lineage.Tables;
                                }
                                enrichedTargetSignatures.Add(sig with { Tables = tables });
                            }

                            if (enrichedTargetSignatures.Count > 1)
                            {
                                var topoResult = SynthesizeDomainsTopologically(enrichedTargetSignatures, domainConfig);
                                if (topoResult?.Domains != null)
                                {
                                    foreach (var d in topoResult.Domains)
                                    {
                                        foreach (var svc in d.Services)
                                        {
                                            var cleanSvc = svc.Trim();
                                            if (projectIntentsToSave.TryGetValue(cleanSvc, out var existing))
                                            {
                                                projectDomainMap[cleanSvc] = (d.Name, existing.Summary);
                                                projectIntentsToSave[cleanSvc] = (d.Name, existing.Summary, existing.Capabilities);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Table-based domain propagation for any unassigned services
                    var tableToDomain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var sig in projectSignatures)
                    {
                        if (projectDomainMap.TryGetValue(sig.Name, out var assigned))
                        {
                            foreach (var tbl in sig.Tables)
                            {
                                tableToDomain[tbl] = assigned.Domain;
                            }
                        }
                    }

                    // If analyzing the whole workspace (no -s filter), ensure any remaining unclassified projects receive an ontology role/directory domain
                    if (filterList.Count == 0)
                    {
                        var unassignedCount = 0;
                        foreach (var sig in projectSignatures)
                        {
                            if (projectDomainMap.ContainsKey(sig.Name)) continue;

                            string fallbackDomain;
                            if (sig.Role is "SharedLibrary" or "Library")
                                fallbackDomain = "SharedKernel";
                            else if (sig.Role is "CliTool")
                                fallbackDomain = "DeveloperTooling";
                            else if (sig.Role is "Test")
                                fallbackDomain = "TestingInfrastructure";
                            else
                            {
                                fallbackDomain = WorkspaceConventions.CanonicalizeDomain(null, sig.Name, sig.RelativePath, sig.Role);
                            }

                            if (string.IsNullOrWhiteSpace(fallbackDomain)) fallbackDomain = "CoreDomain";
                            var fallbackRole = $"Component of {fallbackDomain} domain";
                            projectDomainMap[sig.Name] = (fallbackDomain, fallbackRole);
                            projectIntentsToSave[sig.Name] = (fallbackDomain, fallbackRole, new List<string>());
                            unassignedCount++;
                        }

                        if (unassignedCount > 0)
                        {
                            ctx.Log($"[CodeIntent] Categorized {unassignedCount} remaining projects via ontology role/directory.");
                        }
                    }

                    if (projectIntentsToSave.Count > 0)
                    {
                        await ctx.DbClient.SaveProjectIntentsAsync(ctx.WorkspaceId, projectIntentsToSave,
                            cancellationToken);

                        ctx.Log(
                            $"[CodeIntent] Persisted macro-domain assignments for {projectIntentsToSave.Count} services to graph database.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(serviceFilter))
                {
                    var filters = serviceFilter.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim().ToLowerInvariant())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();

                    foreach (var (svcName, (dom, role)) in projectDomainMap)
                    {
                        if (filters.Any(f => svcName.ToLowerInvariant().Contains(f)))
                        {
                            ctx.Log($"[CodeIntent] Target Service [{svcName}] mapped to Domain: [{dom}] ({role})");
                        }
                    }
                }

                // Load existing intent records to purge any stale/orphan entries

                // Load existing intent records to enable incremental skipping
                var existingIntents = await ctx.DbClient.LoadExistingIntentsAsync(ctx.WorkspaceId, cancellationToken);
                if (existingIntents.Count == 0 && !string.IsNullOrWhiteSpace(ctx.WorkspaceId))
                {
                    existingIntents = await ctx.DbClient.LoadExistingIntentsAsync("", cancellationToken);
                }
                var existingMap = existingIntents.ToDictionary(x => x.FilePath, StringComparer.OrdinalIgnoreCase);

                var sigByPrefix = projectSignatures
                    .Where(s => !string.IsNullOrEmpty(s.RelativePath))
                    .OrderByDescending(s => s.RelativePath.Length)
                    .ToList();
                var rootSig = projectSignatures.FirstOrDefault(s => string.IsNullOrEmpty(s.RelativePath) || s.RelativePath == ".");

                // Purge orphan intents that belong to no recognized project (e.g. standalone scripts, sql/ folder)
                var orphanPaths = existingIntents
                    .Where(rec =>
                    {
                        var norm = (rec.FilePath ?? "").Replace('\\', '/');
                        if (norm.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) return true;
                        var matched = sigByPrefix.FirstOrDefault(s =>
                            norm.StartsWith(s.RelativePath.Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase) ||
                            norm.Equals(s.RelativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                            ?? rootSig;
                        return matched == null;
                    })
                    .Select(rec => rec.FilePath)
                    .ToList();

                if (orphanPaths.Count > 0)
                {
                    await ctx.DbClient.PurgeIntentsByPathsAsync(orphanPaths, cancellationToken);
                    foreach (var op in orphanPaths)
                    {
                        existingMap.Remove(op);
                    }
                    ctx.Log($"[CodeIntent] Purged {orphanPaths.Count} orphaned/non-project file intents (including .sql).");
                }

                // Realign existing file intents to match their parent project's macro-domain
                var intentsToUpdate = new List<IntentRecord>();
                foreach (var rec in existingIntents)
                {
                    var norm = (rec.FilePath ?? "").Replace('\\', '/');
                    if (norm.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) continue;

                    var matched = sigByPrefix.FirstOrDefault(s =>
                        norm.StartsWith(s.RelativePath.Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase) ||
                        norm.Equals(s.RelativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                        ?? rootSig;

                    if (matched != null && projectDomainMap.TryGetValue(matched.Name, out var pInfo))
                    {
                        if (!string.Equals(rec.Domain, pInfo.Domain, StringComparison.OrdinalIgnoreCase))
                        {
                            intentsToUpdate.Add(rec with { Domain = pInfo.Domain });
                        }
                    }
                }

                if (intentsToUpdate.Count > 0)
                {
                    foreach (var rec in intentsToUpdate)
                    {
                        await ctx.DbClient.SaveIntentRecordAsync(rec, cancellationToken);
                    }
                    ctx.Log($"[CodeIntent] Realigned {intentsToUpdate.Count} cached file intents to their parent project macro-domain.");
                }

                var appliedCount = await ctx.DbClient.ApplyCachedIntentsToGraphAsync(ctx.WorkspaceId, cancellationToken);
                ctx.Log($"[CodeIntent] Architectural intent distillation complete. Enriched {appliedCount} nodes in knowledge graph.");
                return appliedCount;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.LogWarning($"[CodeIntent] Intent distillation encountered an issue: {ex.Message}");
            return 0;
        }
    }

    public static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Parser.Incremental.HashUtility.ComputeSha256(bytes);


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
                             (sig.RelativePath.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                              sig.RelativePath.StartsWith("cli/", StringComparison.OrdinalIgnoreCase) ||
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
                             (sig.RelativePath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
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
                        list = new List<string>();
                        tableMap[tbl] = list;
                    }
                    list.Add(sig.Name);
                }
                foreach (var top in sig.Topics)
                {
                    if (!topicMap.TryGetValue(top, out var list))
                    {
                        list = new List<string>();
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
                configuredResult.Add(new SystemDomainAssignment(g.Key, desc, g.Distinct().ToList()));
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
                    list = new List<string>();
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
                    list = new List<string>();
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
                    list = new List<string>();
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
            genericResult.Add(new SystemDomainAssignment(g.Key, desc, g.Distinct().ToList()));
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
            "core", "client", "daemon", "svc", "ats", "backend", "server", "host",
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

        var skipRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "src", "services", "apps", "packages", "modules", "libs", "projects"
        };

        var idx = 0;
        while (idx < segments.Length - 1 && skipRoots.Contains(segments[idx]))
        {
            idx++;
        }

        if (idx < segments.Length)
        {
            var candidate = segments[idx];
            var norm = WorkspaceConventions.NormalizeServiceName(candidate);
            var pascal = ToPascalCase(norm);
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
