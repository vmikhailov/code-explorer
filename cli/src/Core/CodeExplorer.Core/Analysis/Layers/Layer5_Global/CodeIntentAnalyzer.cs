using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
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

public static partial class CodeIntentAnalyzer
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
                    ctx.Log($"[Intent     ] Fast-applied cached architectural intents to {applied} nodes.");
                }

                // If projects still lack domain assignments, synthesize them topologically from AST
                var signatures = await ctx.DbClient.LoadProjectSignaturesAsync(ctx.WorkspaceId, cancellationToken);
                var unassigned = signatures.Where(s => string.IsNullOrWhiteSpace(s.ExistingDomain)).ToList();
                // Synthesized domains are a heuristic; with detection off, domains come only from domains.json.
                if (unassigned.Count > 0 && WorkspaceConventions.DomainDetectionEnabled)
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
                                toSave[svc.Trim()] = (d.Name, d.Description, []);
                            }
                        }
                        if (toSave.Count > 0)
                        {
                            await ctx.DbClient.SaveProjectIntentsAsync(ctx.WorkspaceId, toSave, cancellationToken);
                            ctx.Log($"[Intent     ] Materialized {topoDomains.Domains.Count} topological AST domains across {toSave.Count} services.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ctx.LogDebug($"[Intent     ] Note on applying cached intents: {ex.Message}");
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
        string? endpoint = null,
        string? model = null,
        string? apiKey = null,
        IIntentPredictor? customPredictor = null,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldRun(ctx)) return 0;

        IIntentPredictor? predictor = null;
        var envEndpoint = Environment.GetEnvironmentVariable("CODE_INTENT_ENDPOINT");
        var activeEndpoint = !string.IsNullOrWhiteSpace(endpoint) ? endpoint : envEndpoint;

        if (customPredictor != null)
        {
            predictor = customPredictor;
        }
        else if (!string.IsNullOrWhiteSpace(activeEndpoint))
        {
            var activeModel = !string.IsNullOrWhiteSpace(model) ? model : Environment.GetEnvironmentVariable("CODE_INTENT_MODEL");
            var activeApiKey = !string.IsNullOrWhiteSpace(apiKey) ? apiKey : Environment.GetEnvironmentVariable("CODE_INTENT_API_KEY");
            predictor = new OpenAiIntentPredictor(activeEndpoint, activeModel, activeApiKey);
        }
        else
        {
            var modelPath = await ModelManager.EnsureModelAvailableAsync(ctx, cancellationToken);
            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            {
                ctx.Log("[Intent     ] Intent distillation model not available; skipping intent distillation.");
                return 0;
            }

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

            predictor = new NativeIntentPredictor(modelPath, contextSize: contextSize, gpuLayers: gpuLayers);
        }

        try
        {
            using (predictor)
            {
                ctx.Log($"[Intent     ] Compute Device: {predictor.ExecutionDevice}");
                ctx.Log($"[Intent     ] Concurrency: {predictor.Concurrency}x ({predictor.ConcurrencyReason})");

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
                        .Select(s => s.ExistingDomain).OfType<string>().Where(d => !string.IsNullOrWhiteSpace(d))
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
                        ?
                        [
                            .. businessServices.Where(s => filterList.Any(f =>
                                s.Name.ToLowerInvariant().Contains(f) || (!string.IsNullOrEmpty(s.RelativePath) &&
                                                                          s.RelativePath.ToLowerInvariant()
                                                                              .Contains(f))))
                        ]
                        : businessServices;

                    if (targetServices.Count > 0)
                    {
                        var modeText = deep ? "Multi-Turn Agentic ReAct Exploration" : "Single-Shot Graph Traversal";
                        ctx.Log($"[Intent     ] Distilling Bounded Contexts & Aggregates across {targetServices.Count} service(s) on {predictor.ExecutionDevice} (mode: {modeText}, concurrency: {predictor.Concurrency}x)...");

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
                                ctx.Log($"[Intent     ] Matching Bounded Contexts against {domainConfig.Domains.Count} configured domains from .codeexplorer/domains.json...");
                                var topoResult = SynthesizeDomainsTopologically(projectSignatures, domainConfig);
                                if (topoResult?.Domains != null)
                                {
                                    ctx.Log($"[Intent     ] Grouped into {topoResult.Domains.Count} Business Domains via configuration:");
                                    foreach (var d in topoResult.Domains)
                                    {
                                        ctx.Log($"  - [{d.Name}] ({d.Services.Count} services): {d.Description}");
                                        foreach (var svc in d.Services)
                                        {
                                            var cleanSvc = svc.Trim();
                                            var existingSummary = projectIntentsToSave.TryGetValue(cleanSvc, out var existing) ? existing.Summary : d.Description;
                                            var existingAggs = existing.Capabilities ?? [];
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
                                    ctx.Log($"[Intent     ] Synthesizing Macro-Domains via LLM from {allForMacro.Count} Bounded Contexts on {predictor.ExecutionDevice}...");
                                    macroDomains = await predictor.PredictMacroDomainsFromContextsAsync(allForMacro, cancellationToken);
                                }
                                catch (Exception ex)
                                {
                                    ctx.LogWarning($"[Intent     ] Note on LLM macro-domain synthesis: {ex.Message}. Falling back to graph topology synthesis.");
                                }

                                if (macroDomains?.Domains != null && macroDomains.Domains.Count > 0)
                                {
                                    ctx.Log($"[Intent     ] Discovered {macroDomains.Domains.Count} Business Domains via LLM synthesis:");
                                    foreach (var d in macroDomains.Domains)
                                    {
                                        var canonicalName = WorkspaceConventions.CanonicalizeDomain(d.Name);
                                        ctx.Log($"  - [{canonicalName}] ({d.Services.Count} services): {d.Description}");
                                        foreach (var svc in d.Services)
                                        {
                                            var cleanSvc = svc.Trim();
                                            var existingSummary = projectIntentsToSave.TryGetValue(cleanSvc, out var existing) ? existing.Summary : d.Description;
                                            var existingAggs = existing.Capabilities ?? [];
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
                                                var existingAggs = existing.Capabilities ?? [];
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

                            if (string.IsNullOrWhiteSpace(fallbackDomain) || WorkspaceConventions.GenericFolderRoots.Contains(fallbackDomain)) fallbackDomain = "CoreDomain";
                            var fallbackRole = $"Component of {fallbackDomain} domain";
                            projectDomainMap[sig.Name] = (fallbackDomain, fallbackRole);
                            projectIntentsToSave[sig.Name] = (fallbackDomain, fallbackRole, []);
                            unassignedCount++;
                        }

                        if (unassignedCount > 0)
                        {
                            ctx.Log($"[Intent     ] Categorized {unassignedCount} remaining projects via ontology role/directory.");
                        }
                    }

                    if (projectIntentsToSave.Count > 0)
                    {
                        await ctx.DbClient.SaveProjectIntentsAsync(ctx.WorkspaceId, projectIntentsToSave,
                            cancellationToken);

                        ctx.Log(
                            $"[Intent     ] Persisted macro-domain assignments for {projectIntentsToSave.Count} services to graph database.");
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
                            ctx.Log($"[Intent     ] Target Service [{svcName}] mapped to Domain: [{dom}] ({role})");
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
                            norm.StartWithIC(s.RelativePath.Replace('\\', '/') + "/") ||
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
                    ctx.Log($"[Intent     ] Purged {orphanPaths.Count} orphaned/non-project file intents (including .sql).");
                }

                // Realign existing file intents to match their parent project's macro-domain
                var intentsToUpdate = new List<IntentRecord>();
                foreach (var rec in existingIntents)
                {
                    var norm = (rec.FilePath ?? "").Replace('\\', '/');
                    if (norm.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) continue;

                    var matched = sigByPrefix.FirstOrDefault(s =>
                        norm.StartWithIC(s.RelativePath.Replace('\\', '/') + "/") ||
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
                    ctx.Log($"[Intent     ] Realigned {intentsToUpdate.Count} cached file intents to their parent project macro-domain.");
                }

                var appliedCount = await ctx.DbClient.ApplyCachedIntentsToGraphAsync(ctx.WorkspaceId, cancellationToken);
                ctx.Log($"[Intent     ] Architectural intent distillation complete. Enriched {appliedCount} nodes in knowledge graph.");
                return appliedCount;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.LogWarning($"[Intent     ] Intent distillation encountered an issue: {ex.Message}");
            return 0;
        }
    }

    public static string ComputeSha256(ReadOnlySpan<byte> bytes) =>
        Parser.Incremental.HashUtility.ComputeSha256(bytes);

}
