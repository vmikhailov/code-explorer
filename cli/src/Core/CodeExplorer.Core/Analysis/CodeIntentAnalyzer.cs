using System.Collections.Concurrent;
using System.Text;
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
            }
            catch (Exception ex)
            {
                ctx.LogDebug($"[CodeIntent] Note on applying cached intents: {ex.Message}");
            }
            return;
        }

        await RunIncrementalIntentAnalysisAsync(ctx, limit: null, cancellationToken);
    }

    public static async Task<int> RunIncrementalIntentAnalysisAsync(
        ParsingContext ctx,
        int? limit = null,
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
            var candidates = await ctx.DbClient.LoadIntentCandidatesAsync(ctx.WorkspaceId, limit, cancellationToken);
            if (candidates.Count == 0)
            {
                ctx.Log("[CodeIntent] No architectural candidates found for intent distillation.");
                return 0;
            }

            // Load existing intent records to enable incremental skipping
            var existingIntents = await ctx.DbClient.LoadExistingIntentsAsync(ctx.WorkspaceId, cancellationToken);
            if (existingIntents.Count == 0 && !string.IsNullOrWhiteSpace(ctx.WorkspaceId))
            {
                existingIntents = await ctx.DbClient.LoadExistingIntentsAsync("", cancellationToken);
            }
            var existingMap = existingIntents.ToDictionary(x => x.FilePath, StringComparer.OrdinalIgnoreCase);

            // Group candidates by distinct resolved file path
            var fileGroups = new Dictionary<string, (string RelativePath, IntentCandidate PrimaryCand, List<IntentCandidate> AllCands)>(StringComparer.OrdinalIgnoreCase);
            foreach (var cand in candidates)
            {
                var fullPath = ResolveCandidateFullPath(cand, ctx);
                if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath)) continue;

                var relPath = !string.IsNullOrEmpty(cand.RelativePath)
                    ? cand.RelativePath.Replace('\\', '/')
                    : Path.GetRelativePath(ctx.AbsoluteWorkspacePath, fullPath).Replace('\\', '/');

                if (!fileGroups.TryGetValue(fullPath, out var group))
                {
                    group = (relPath, cand, [cand]);
                    fileGroups[fullPath] = group;
                }
                else
                {
                    group.AllCands.Add(cand);
                }
            }

            var toProcess = new List<(string FullPath, string RelativePath, string ProjectName, IntentCandidate Cand, string Hash, DateTime LastModifiedUtc)>();
            var skippedClean = 0;
            var skippedErrorLimit = 0;

            foreach (var (fullPath, (relPath, cand, _)) in fileGroups)
            {
                var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
                var hash = ComputeSha256(bytes);
                var lastMod = File.GetLastWriteTimeUtc(fullPath);

                if (existingMap.TryGetValue(relPath, out var existing))
                {
                    if (existing.ErrorCount >= 10)
                    {
                        skippedErrorLimit++;
                        continue;
                    }

                    if (string.Equals(existing.ContentHash, hash, StringComparison.OrdinalIgnoreCase) && existing.Domain != null)
                    {
                        skippedClean++;
                        continue;
                    }
                }

                var projectName = ExtractProjectOrSubsystem(relPath);
                toProcess.Add((fullPath, relPath, projectName, cand, hash, lastMod));
            }

            var gpuLayers = 99;
            if (int.TryParse(Environment.GetEnvironmentVariable("CODE_INTENT_GPU_LAYERS"), out var envLayers))
            {
                gpuLayers = envLayers;
            }

            if (toProcess.Count == 0)
            {
                var (cachedDevice, _) = NativeIntentPredictor.DetectExecutionDevice(gpuLayers);
                ctx.Log($"[CodeIntent] Compute Device: {cachedDevice}");
                ctx.Log($"[CodeIntent] All {fileGroups.Count} architectural files are up-to-date in cache" +
                    (skippedErrorLimit > 0 ? $" ({skippedErrorLimit} files skipped due to >=10 errors; run 'ce intent reset-errors' to retry)" : "") + ".");
                var fastApplied = await ctx.DbClient.ApplyCachedIntentsToGraphAsync(ctx.WorkspaceId, cancellationToken);
                return fastApplied;
            }

            var contextSize = 4096;
            if (int.TryParse(Environment.GetEnvironmentVariable("CODE_INTENT_CONTEXT_SIZE"), out var envContext) && envContext >= 1024)
            {
                contextSize = envContext;
            }

            using var predictor = new NativeIntentPredictor(modelPath, contextSize: contextSize, gpuLayers: gpuLayers);

            ctx.Log($"[CodeIntent] Compute Device: {predictor.ExecutionDevice} (GPU layers: {gpuLayers})");
            ctx.Log($"[CodeIntent] Concurrency: {predictor.Concurrency}x ({predictor.ConcurrencyReason})");
            // 1. Top-Down Phase 1: Determine architectural Bounded Context & Role per project from signatures
            var projectSignatures = await ctx.DbClient.LoadProjectSignaturesAsync(ctx.WorkspaceId, cancellationToken);
            var projectDomainMap = new ConcurrentDictionary<string, (string Domain, string Role)>(StringComparer.OrdinalIgnoreCase);
            var projectIntentsToSave = new Dictionary<string, (string Domain, string Summary, List<string> Capabilities)>(StringComparer.OrdinalIgnoreCase);

            if (projectSignatures.Count > 0)
            {
                var relevantProjects = toProcess
                    .Select(x => x.ProjectName)
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var pendingLlm = new List<ProjectSignature>();
                var cachedCount = 0;
                var trivialCount = 0;

                foreach (var sig in projectSignatures)
                {
                    // 1. Check if project already has intent_domain saved in SQLite
                    if (!string.IsNullOrWhiteSpace(sig.ExistingDomain))
                    {
                        projectDomainMap[sig.Name] = (sig.ExistingDomain, sig.ExistingRole ?? "");
                        cachedCount++;
                        continue;
                    }

                    // 2. Trivial/empty signature (no endpoints, tables, topics, or domain types)
                    var isEmpty = sig.Endpoints.Count == 0 &&
                                  sig.Tables.Count == 0 &&
                                  sig.Topics.Count == 0 &&
                                  sig.DomainTypes.Count == 0;

                    if (isEmpty)
                    {
                        var (fastDomain, fastRole) = ResolveTrivialProjectIntent(sig);
                        projectDomainMap[sig.Name] = (fastDomain, fastRole);
                        projectIntentsToSave[sig.Name] = (fastDomain, fastRole, new List<string>());
                        trivialCount++;
                        continue;
                    }

                    // 3. If limit was passed (e.g. quick testing), skip LLM for projects with no files in toProcess
                    if (limit.HasValue && !relevantProjects.Contains(sig.Name))
                    {
                        var (fastDomain, fastRole) = ResolveTrivialProjectIntent(sig);
                        projectDomainMap[sig.Name] = (fastDomain, fastRole);
                        projectIntentsToSave[sig.Name] = (fastDomain, fastRole, new List<string>());
                        trivialCount++;
                        continue;
                    }

                    pendingLlm.Add(sig);
                }

                if (cachedCount > 0)
                {
                    ctx.Log($"[CodeIntent] Phase 1: Loaded cached Bounded Contexts for {cachedCount} projects.");
                }
                if (trivialCount > 0)
                {
                    ctx.Log($"[CodeIntent] Phase 1: Heuristically resolved {trivialCount} projects with no architectural endpoints/tables.");
                }

                if (pendingLlm.Count > 0)
                {
                    ctx.Log($"[CodeIntent] Phase 1: Distilling Bounded Contexts for {pendingLlm.Count} projects on {predictor.ExecutionDevice} (concurrency: {predictor.Concurrency})...");
                    var pIdx = 0;
                    var pOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = predictor.Concurrency,
                        CancellationToken = cancellationToken
                    };

                    await Parallel.ForEachAsync(pendingLlm, pOptions, async (sig, ct) =>
                    {
                        var pCurrent = Interlocked.Increment(ref pIdx);
                        ctx.Log($"[CodeIntent] Phase 1: [{pCurrent}/{pendingLlm.Count}] Analyzing signature for '{sig.Name}' (EPs: {sig.Endpoints.Count}, Tables: {sig.Tables.Count}, Topics: {sig.Topics.Count}, Types: {sig.DomainTypes.Count})...");

                        try
                        {
                            var pResult = await predictor.PredictProjectIntentAsync(sig, ct);
                            if (pResult != null && !string.IsNullOrWhiteSpace(pResult.Domain))
                            {
                                projectDomainMap[sig.Name] = (pResult.Domain, pResult.ProjectRole ?? "");
                                lock (projectIntentsToSave)
                                {
                                    projectIntentsToSave[sig.Name] = (
                                        pResult.Domain,
                                        pResult.ProjectRole ?? "",
                                        pResult.Capabilities ?? new List<string>()
                                    );
                                }
                                ctx.Log($"[CodeIntent] Phase 1: [{pCurrent}/{pendingLlm.Count}] '{sig.Name}' -> Bounded Context: {pResult.Domain} ({pResult.ProjectRole})");
                            }
                            else
                            {
                                var (fallbackDomain, fallbackRole) = ResolveTrivialProjectIntent(sig);
                                projectDomainMap[sig.Name] = (fallbackDomain, fallbackRole);
                                lock (projectIntentsToSave)
                                {
                                    projectIntentsToSave[sig.Name] = (fallbackDomain, fallbackRole, new List<string>());
                                }
                                ctx.Log($"[CodeIntent] Phase 1: [{pCurrent}/{pendingLlm.Count}] '{sig.Name}' -> Fallback Domain: {fallbackDomain}");
                            }
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            ctx.LogWarning($"[CodeIntent] Project intent failed for '{sig.Name}': {ex.Message}");
                            var (fallbackDomain, fallbackRole) = ResolveTrivialProjectIntent(sig);
                            projectDomainMap[sig.Name] = (fallbackDomain, fallbackRole);
                        }
                    });
                }

                if (projectIntentsToSave.Count > 0)
                {
                    await ctx.DbClient.SaveProjectIntentsAsync(ctx.WorkspaceId, projectIntentsToSave, cancellationToken);
                    ctx.Log($"[CodeIntent] Phase 1 completed: Established Bounded Contexts for {projectDomainMap.Count}/{projectSignatures.Count} projects.");
                }
            }

            // Phase 2: Per-file intent distillation anchored by parent project Bounded Context
            ctx.Log($"[CodeIntent] Phase 2: Running batch intent inference on {toProcess.Count} files with {predictor.Concurrency}x parallel batching on {predictor.ExecutionDevice} ({skippedClean} unchanged, {skippedErrorLimit} error-locked)...");

            var idx = 0;
            var successCount = 0;
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = predictor.Concurrency,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(toProcess, parallelOptions, async (item, ct) =>
            {
                var currentIdx = Interlocked.Increment(ref idx);
                var fileName = Path.GetFileName(item.FullPath);
                ctx.Log($"[CodeIntent] Processing files through LLM: {currentIdx}/{toProcess.Count} ({fileName})...");

                try
                {
                    var content = await File.ReadAllTextAsync(item.FullPath, ct);

                    // Resolve parent project domain anchor
                    string? projectDomain = null;
                    string? projectRole = null;

                    if (!string.IsNullOrWhiteSpace(item.ProjectName) && projectDomainMap.TryGetValue(item.ProjectName, out var pInfo))
                    {
                        projectDomain = pInfo.Domain;
                        projectRole = pInfo.Role;
                    }
                    else
                    {
                        // Fallback matching by longest relative path prefix
                        var matchedSig = projectSignatures
                            .Where(s => !string.IsNullOrEmpty(s.RelativePath) && item.RelativePath.Replace('\\', '/').StartsWith(s.RelativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                            .OrderByDescending(s => s.RelativePath.Length)
                            .FirstOrDefault();

                        if (matchedSig != null && projectDomainMap.TryGetValue(matchedSig.Name, out var sigInfo))
                        {
                            projectDomain = sigInfo.Domain;
                            projectRole = sigInfo.Role;
                        }
                    }

                    var (prediction, rawOutput) = await predictor.PredictWithRawAsync(
                        item.FullPath,
                        content,
                        projectName: item.ProjectName,
                        knownDomains: null, // Fully eliminate runaway snowballing!
                        projectDomain: projectDomain,
                        projectRole: projectRole,
                        cancellationToken: ct
                    );

                    if (prediction != null && !string.IsNullOrWhiteSpace(prediction.Domain))
                    {
                        var record = new IntentRecord(
                            FilePath: item.RelativePath,
                            WorkspaceId: ctx.WorkspaceId,
                            FileId: item.Cand.Id,
                            ContentHash: item.Hash,
                            LastModifiedUtc: item.LastModifiedUtc,
                            Domain: prediction.Domain,
                            Layer: prediction.Layer,
                            Pattern: prediction.Pattern,
                            OperationType: prediction.OperationType,
                            CapabilityTag: prediction.CapabilityTag,
                            IntentSummary: prediction.IntentSummary,
                            TargetEntities: prediction.TargetEntities,
                            EmittedEvents: prediction.EmittedEvents,
                            IsPureDomain: prediction.IsPureDomain,
                            ErrorCount: 0,
                            LastError: null,
                            AnalyzedAtUtc: DateTime.UtcNow
                        );
                        await ctx.DbClient.SaveIntentRecordAsync(record, ct);
                        Interlocked.Increment(ref successCount);
                    }
                    else
                    {
                        ctx.LogWarning($"[CodeIntent] Malformed or empty prediction on '{fileName}' ({currentIdx}/{toProcess.Count})");
                        await ctx.DbClient.IncrementIntentErrorAsync(
                            item.RelativePath,
                            ctx.WorkspaceId,
                            item.Cand.Id,
                            item.Hash,
                            item.LastModifiedUtc,
                            "LLM output could not be parsed into valid architectural intent JSON",
                            ct);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ctx.LogWarning($"[CodeIntent] Error during inference on '{fileName}' ({currentIdx}/{toProcess.Count}): {ex.Message}");
                    await ctx.DbClient.IncrementIntentErrorAsync(
                        item.RelativePath,
                        ctx.WorkspaceId,
                        item.Cand.Id,
                        item.Hash,
                        item.LastModifiedUtc,
                        ex.Message,
                        ct);
                }
            });

            ctx.Log($"[CodeIntent] Completed LLM distillation: {successCount}/{toProcess.Count} succeeded.");
            var appliedCount = await ctx.DbClient.ApplyCachedIntentsToGraphAsync(ctx.WorkspaceId, cancellationToken);
            ctx.Log($"[CodeIntent] Applied architectural intents to {appliedCount} graph nodes.");
            return appliedCount;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.LogWarning($"[CodeIntent] Intent distillation encountered an issue: {ex.Message}");
            return 0;
        }
    }

    private static string ExtractProjectOrSubsystem(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "Default";

        if (parts.Length > 1 && (parts[0] is "src" or "apps" or "packages" or "libs" or "services" or "modules" or "cli"))
        {
            if (parts[0] == "cli" && parts.Length > 2 && parts[1] == "src")
            {
                return parts.Length > 3 ? parts[3] : parts[2];
            }
            return parts[1];
        }

        return parts[0];
    }

    public static string ComputeSha256(ReadOnlySpan<byte> bytes)
    {
        using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);

        // Normalize line endings: strip carriage return '\r' (0x0D) so CRLF and LF yield identical hashes across OSes
        var remaining = bytes;
        while (!remaining.IsEmpty)
        {
            var idx = remaining.IndexOf((byte)'\r');
            if (idx < 0)
            {
                sha.AppendData(remaining);
                break;
            }
            if (idx > 0)
            {
                sha.AppendData(remaining[..idx]);
            }
            remaining = remaining[(idx + 1)..];
        }

        Span<byte> hashBytes = stackalloc byte[32];
        sha.GetHashAndReset(hashBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string? ResolveCandidateFullPath(IntentCandidate cand, ParsingContext ctx)
    {
        if (!string.IsNullOrEmpty(cand.FullPath) && File.Exists(cand.FullPath))
        {
            return Path.GetFullPath(cand.FullPath);
        }

        if (!string.IsNullOrEmpty(cand.RelativePath))
        {
            if (!string.IsNullOrEmpty(ctx.HostWorkspacePath))
            {
                var p1 = Path.GetFullPath(Path.Combine(ctx.HostWorkspacePath, cand.RelativePath));
                if (File.Exists(p1)) return p1;
            }

            if (!string.IsNullOrEmpty(ctx.ScanPath))
            {
                var p2 = Path.GetFullPath(Path.Combine(ctx.ScanPath, cand.RelativePath));
                if (File.Exists(p2)) return p2;
            }
        }

        return null;
    }

    private static (string Domain, string Role) ResolveTrivialProjectIntent(ProjectSignature sig)
    {
        var name = sig.Name;
        var lower = name.ToLowerInvariant();

        if (lower.Contains("ui") || lower.Contains("component") || lower.Contains("style") || lower.Contains("theme") || lower.Contains("frontend"))
            return ("UserInterface", "UI and presentation component library");
        if (lower.Contains("cli") || lower.Contains("tool"))
            return ("DeveloperTooling", "Command-line and developer tooling");
        if (lower.Contains("common") || lower.Contains("core") || lower.Contains("shared") || lower.Contains("util"))
            return ("SharedKernel", "Shared utilities and foundational library");
        if (lower.Contains("gateway") || lower.Contains("proxy") || lower.Contains("ingress"))
            return ("ApiGateway", "API Gateway and reverse proxy routing");
        if (lower.Contains("worker") || lower.Contains("job") || lower.Contains("scheduler"))
            return ("BackgroundProcessing", "Background worker and task scheduler");
        if (lower.Contains("test") || lower.Contains("mock") || lower.Contains("fixture"))
            return ("TestingInfrastructure", "Test fixtures and testing infrastructure");

        var clean = ToPascalCase(name);
        return (!string.IsNullOrWhiteSpace(clean) ? clean : "CoreDomain", "Internal service subsystem");
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
