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

            var toProcess = new List<(string FullPath, string RelativePath, IntentCandidate Cand, string Hash, DateTime LastModifiedUtc)>();
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

                toProcess.Add((fullPath, relPath, cand, hash, lastMod));
            }

            if (toProcess.Count == 0)
            {
                ctx.Log($"[CodeIntent] All {fileGroups.Count} architectural files are up-to-date in cache" +
                    (skippedErrorLimit > 0 ? $" ({skippedErrorLimit} files skipped due to >=10 errors; run 'ce intent reset-errors' to retry)" : "") + ".");
                var fastApplied = await ctx.DbClient.ApplyCachedIntentsToGraphAsync(ctx.WorkspaceId, cancellationToken);
                return fastApplied;
            }

            ctx.Log($"[CodeIntent] Running batch intent inference on {toProcess.Count} files ({skippedClean} unchanged, {skippedErrorLimit} error-locked)...");

            var gpuLayers = 99;
            if (int.TryParse(Environment.GetEnvironmentVariable("CODE_INTENT_GPU_LAYERS"), out var envLayers))
            {
                gpuLayers = envLayers;
            }

            using var predictor = new NativeIntentPredictor(modelPath, contextSize: 2048, gpuLayers: gpuLayers);

            var idx = 0;
            var successCount = 0;
            foreach (var item in toProcess)
            {
                cancellationToken.ThrowIfCancellationRequested();
                idx++;
                var fileName = Path.GetFileName(item.FullPath);
                ctx.Log($"[CodeIntent] Processing files through LLM: {idx}/{toProcess.Count} ({fileName})...");

                try
                {
                    var content = await File.ReadAllTextAsync(item.FullPath, cancellationToken);
                    var (prediction, rawOutput) = await predictor.PredictWithRawAsync(item.FullPath, content, cancellationToken);

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
                        await ctx.DbClient.SaveIntentRecordAsync(record, cancellationToken);
                        successCount++;
                    }
                    else
                    {
                        ctx.LogWarning($"[CodeIntent] Malformed or empty prediction on '{fileName}' ({idx}/{toProcess.Count})");
                        await ctx.DbClient.IncrementIntentErrorAsync(
                            item.RelativePath,
                            ctx.WorkspaceId,
                            item.Cand.Id,
                            item.Hash,
                            item.LastModifiedUtc,
                            "LLM output could not be parsed into valid architectural intent JSON",
                            cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    ctx.LogWarning($"[CodeIntent] Error during inference on '{fileName}' ({idx}/{toProcess.Count}): {ex.Message}");
                    await ctx.DbClient.IncrementIntentErrorAsync(
                        item.RelativePath,
                        ctx.WorkspaceId,
                        item.Cand.Id,
                        item.Hash,
                        item.LastModifiedUtc,
                        ex.Message,
                        cancellationToken);
                }
            }

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

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
}
