using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;

namespace CodeExplorer.Core.Analysis;

public record DistillEnvironment(string DirectoryPath, string ModelPath);

public record BatchInferenceItem(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("file_path")] string FilePath
);

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

        var env = FindDistillEnvironment(ctx);
        if (env == null)
        {
            ctx.Log("[CodeIntent] Intent distillation model not detected; skipping intent enrichment pass.");
            return;
        }

        ctx.Log($"[CodeIntent] Found code-intent-distill at '{env.DirectoryPath}' with model '{env.ModelPath}'.");

        try
        {
            var candidates = await ctx.DbClient.LoadIntentCandidatesAsync(ctx.WorkspaceId, cancellationToken);
            if (candidates.Count == 0)
            {
                ctx.Log("[CodeIntent] No architectural candidates found for intent distillation.");
                return;
            }

            // Map unique resolved absolute file paths to list of node IDs
            var fileToNodes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var cand in candidates)
            {
                var fullPath = ResolveCandidateFullPath(cand, ctx);
                if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                {
                    if (!fileToNodes.TryGetValue(fullPath, out var list))
                    {
                        list = [];
                        fileToNodes[fullPath] = list;
                    }
                    list.Add(cand.Id);
                }
            }

            if (fileToNodes.Count == 0)
            {
                ctx.Log("[CodeIntent] No candidate files exist on disk; skipping intent distillation.");
                return;
            }

            ctx.Log($"[CodeIntent] Running batch intent inference on {fileToNodes.Count} files ({candidates.Count} nodes)...");

            var predictions = await RunBatchInferenceAsync(env, fileToNodes.Keys, cancellationToken);
            if (predictions.Count == 0)
            {
                ctx.Log("[CodeIntent] Batch inference returned 0 predictions.");
                return;
            }

            // Expand predictions for all associated nodes
            var enrichedResults = new List<CodeIntentPredictionResult>();
            foreach (var pred in predictions)
            {
                if (string.IsNullOrEmpty(pred.FilePath) || !fileToNodes.TryGetValue(pred.FilePath, out var nodeIds))
                {
                    continue;
                }

                foreach (var nodeId in nodeIds)
                {
                    enrichedResults.Add(new CodeIntentPredictionResult(
                        nodeId,
                        pred.FilePath,
                        pred.Domain,
                        pred.Layer,
                        pred.Pattern,
                        pred.OperationType,
                        pred.CapabilityTag,
                        pred.IntentSummary,
                        pred.IsPureDomain,
                        pred.TargetEntities,
                        pred.EmittedEvents
                    ));
                }
            }

            if (enrichedResults.Count > 0)
            {
                await ctx.DbClient.SaveIntentPredictionsAsync(ctx.WorkspaceId, enrichedResults, cancellationToken);
                ctx.Log($"[CodeIntent] Enriched {enrichedResults.Count} nodes across {fileToNodes.Count} files with architectural intent.");
            }
        }
        catch (Exception ex)
        {
            ctx.Log($"[CodeIntent] Intent enrichment encountered an issue: {ex.Message}; continuing gracefully.");
        }
    }

    public static DistillEnvironment? FindDistillEnvironment(ParsingContext ctx)
    {
        var candidateDirs = new List<string>();

        // 1. Explicit environment variable
        var envVar = Environment.GetEnvironmentVariable("CODE_INTENT_DIR");
        if (!string.IsNullOrWhiteSpace(envVar))
        {
            candidateDirs.Add(envVar);
        }

        // 2. Relative paths from workspace or current directory
        if (!string.IsNullOrWhiteSpace(ctx.HostWorkspacePath))
        {
            candidateDirs.Add(Path.Combine(ctx.HostWorkspacePath, "..", "code-intent-distill"));
            candidateDirs.Add(Path.Combine(ctx.HostWorkspacePath, "..", "..", "code-intent-distill"));
        }
        candidateDirs.Add(Path.Combine(Environment.CurrentDirectory, "..", "code-intent-distill"));
        candidateDirs.Add(Path.Combine(Environment.CurrentDirectory, "..", "..", "code-intent-distill"));
        candidateDirs.Add(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "code-intent-distill"));

        // 3. Well-known local directory
        candidateDirs.Add(@"C:\Work\Personal\code-intent-distill");

        foreach (var dir in candidateDirs)
        {
            try
            {
                var fullDir = Path.GetFullPath(dir);
                if (!Directory.Exists(fullDir)) continue;

                var pyproject = Path.Combine(fullDir, "pyproject.toml");
                if (!File.Exists(pyproject)) continue;

                var v2Model = Path.Combine(fullDir, "models", "ce-intent-v2");
                if (Directory.Exists(v2Model))
                {
                    return new DistillEnvironment(fullDir, "./models/ce-intent-v2");
                }

                var v1Model = Path.Combine(fullDir, "models", "ce-intent-v1");
                if (Directory.Exists(v1Model))
                {
                    return new DistillEnvironment(fullDir, "./models/ce-intent-v1");
                }
            }
            catch
            {
                // Ignore path errors
            }
        }

        return null;
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

    private static async Task<List<BatchInferenceResult>> RunBatchInferenceAsync(
        DistillEnvironment env,
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        var tempInput = Path.Combine(Path.GetTempPath(), $"ce_intent_in_{Guid.NewGuid():N}.jsonl");
        var tempOutput = Path.Combine(Path.GetTempPath(), $"ce_intent_out_{Guid.NewGuid():N}.jsonl");

        try
        {
            // 1. Write batch input JSONL
            await using (var writer = new StreamWriter(tempInput, false, System.Text.Encoding.UTF8))
            {
                foreach (var path in filePaths)
                {
                    var item = new BatchInferenceItem(path, path);
                    var line = JsonSerializer.Serialize(item, JsonOptions);
                    await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
                }
            }

            // 2. Invoke uv run code-intent batch
            var psi = new ProcessStartInfo
            {
                FileName = "uv",
                Arguments = $"run --directory \"{env.DirectoryPath}\" code-intent batch --input \"{tempInput}\" --output \"{tempOutput}\" --model \"{env.ModelPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3), cancellationToken);
            var processTask = process.WaitForExitAsync(cancellationToken);

            var completedTask = await Task.WhenAny(processTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                try { process.Kill(true); } catch { /* Ignore */ }
                throw new TimeoutException("Code-intent batch inference timed out after 3 minutes.");
            }

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken);
                throw new InvalidOperationException($"code-intent batch exited with code {process.ExitCode}: {stderr}");
            }

            // 3. Read output JSONL
            var results = new List<BatchInferenceResult>();
            if (File.Exists(tempOutput))
            {
                using var reader = new StreamReader(tempOutput, System.Text.Encoding.UTF8);
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line)) continue;
                    try
                    {
                        var res = JsonSerializer.Deserialize<BatchInferenceResult>(line, JsonOptions);
                        if (res != null) results.Add(res);
                    }
                    catch
                    {
                        // Skip malformed lines
                    }
                }
            }

            return results;
        }
        finally
        {
            try { if (File.Exists(tempInput)) File.Delete(tempInput); } catch { /* Ignore */ }
            try { if (File.Exists(tempOutput)) File.Delete(tempOutput); } catch { /* Ignore */ }
        }
    }
}
