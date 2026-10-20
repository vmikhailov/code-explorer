using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Database;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

namespace CodeExplorer.Core.Analysis;

public record ProjectIntentResult(
    [property: JsonPropertyName("domain")] string? Domain,
    [property: JsonPropertyName("project_role")] string? ProjectRole,
    [property: JsonPropertyName("capabilities")] List<string>? Capabilities
);

public record ProjectBoundedContextResult(
    [property: JsonPropertyName("service")] string Service,
    [property: JsonPropertyName("bounded_context")] string BoundedContext,
    [property: JsonPropertyName("primary_aggregates")] List<string> PrimaryAggregates,
    [property: JsonPropertyName("capability")] string Capability,
    [property: JsonPropertyName("suggested_domain")] string SuggestedDomain
);

public record SystemDomainAssignment(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("services")] List<string> Services
);

public record SystemDomainsResult(
    [property: JsonPropertyName("domains")] List<SystemDomainAssignment> Domains
);

public sealed partial class NativeIntentPredictor : IIntentPredictor
{
    private static readonly object ConfigLock = new();
    private static bool _configured;

    private readonly LLamaWeights _weights;
    private readonly ModelParams _parameters;
    private readonly System.Collections.Concurrent.ConcurrentBag<StatelessExecutor> _executorPool = [];
    private readonly SemaphoreSlim _semaphore;
    private readonly int _concurrency;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;
    private static readonly NativeLogConfig.LLamaLogCallback SilentLlamaLog = (_, _) => { };

    public int Concurrency => _concurrency;
    public string ConcurrencyReason { get; }
    public string ExecutionDevice { get; }
    public bool IsGpuAccelerated { get; }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr GetBackendDescriptionDelegate(IntPtr handle);

    public static (string DeviceName, bool IsGpu) DetectExecutionDevice(int gpuLayers = 99)
    {
        if (gpuLayers <= 0)
        {
            return ("CPU (0 GPU layers specified)", false);
        }

        try
        {
            lock (ConfigLock)
            {
                if (!_configured)
                {
                    NativeLibraryConfig.All
                        .WithCuda()
                        .WithVulkan()
                        .WithAutoFallback()
                        .WithLogCallback((_, _) => { });

                    NativeLogConfig.llama_log_set(SilentLlamaLog);
                    _configured = true;
                }
            }

            var supportsGpu = NativeApi.llama_supports_gpu_offload();
            if (!supportsGpu)
            {
                return ("CPU (LLama native library without GPU offload)", false);
            }

            var modules = System.Diagnostics.Process.GetCurrentProcess().Modules
                .Cast<System.Diagnostics.ProcessModule>()
                .ToList();

            var ggmlMod = modules.FirstOrDefault(m =>
                m.ModuleName.StartsWith("ggml-", StringComparison.OrdinalIgnoreCase) &&
                !m.ModuleName.Equals("ggml-base.dll", StringComparison.OrdinalIgnoreCase) &&
                !m.ModuleName.Equals("ggml-cpu.dll", StringComparison.OrdinalIgnoreCase));

            string backendName = "GPU";
            if (ggmlMod != null)
            {
                var modName = ggmlMod.ModuleName.ToLowerInvariant();
                var filePath = (ggmlMod.FileName ?? "").ToLowerInvariant();
                if (modName.Contains("vulkan") || filePath.Contains("vulkan"))
                    backendName = "Vulkan";
                else if (modName.Contains("cuda") || filePath.Contains("cuda"))
                    backendName = "CUDA";
                else if (modName.Contains("metal") || filePath.Contains("metal"))
                    backendName = "Metal";
            }

            string? deviceDescription = null;
            var baseMod = modules.FirstOrDefault(m => m.ModuleName.Equals("ggml-base.dll", StringComparison.OrdinalIgnoreCase));
            if (baseMod != null && NativeLibrary.TryGetExport(baseMod.BaseAddress, "ggml_backend_dev_description", out var descPtr))
            {
                var getDesc = Marshal.GetDelegateForFunctionPointer<GetBackendDescriptionDelegate>(descPtr);
                var devCount = (int)(ulong)NativeApi.ggml_backend_dev_count();
                for (var i = 0; i < devCount; i++)
                {
                    var dev = NativeApi.ggml_backend_dev_get((UIntPtr)i);
                    if (dev != IntPtr.Zero)
                    {
                        var strPtr = getDesc(dev);
                        if (strPtr != IntPtr.Zero)
                        {
                            var desc = Marshal.PtrToStringUTF8(strPtr);
                            if (!string.IsNullOrWhiteSpace(desc) && !desc.Equals("CPU", StringComparison.OrdinalIgnoreCase))
                            {
                                deviceDescription = desc;
                                break;
                            }
                        }
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(deviceDescription))
            {
                return ($"GPU ({backendName}: {deviceDescription})", true);
            }

            return ($"GPU ({backendName})", true);
        }
        catch
        {
            return ("CPU (Fallback)", false);
        }
    }

    public static ulong GetTotalMemoryBytes()
    {
        try
        {
            var mem = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            if (mem > 0) return (ulong)mem;
        }
        catch { }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("sysctl", "-n hw.memsize")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var p = System.Diagnostics.Process.Start(psi);

                if (p != null)
                {
                    var str = p.StandardOutput.ReadToEnd().Trim();
                    if (ulong.TryParse(str, out var bytes) && bytes > 0) return bytes;
                }
            }
            catch { }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists("/proc/meminfo"))
        {
            try
            {
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && ulong.TryParse(parts[1], out var kb) && kb > 0)
                        {
                            return kb * 1024UL;
                        }
                    }
                }
            }
            catch { }
        }

        return (ulong)Math.Max(1, Environment.ProcessorCount) * 2UL * 1024 * 1024 * 1024;
    }

    public static (int Concurrency, string Reason) CalculateOptimalConcurrency(
        int? explicitConcurrency,
        bool isGpu,
        string executionDevice,
        int gpuLayers,
        int? cpuCoreCount = null,
        ulong? memoryBytes = null)
    {
        // 1. Explicit override via caller parameter
        if (explicitConcurrency.HasValue && explicitConcurrency.Value > 0)
        {
            return (explicitConcurrency.Value, $"explicitly requested ({explicitConcurrency.Value}x)");
        }

        // 2. Explicit override via environment variable
        if (int.TryParse(Environment.GetEnvironmentVariable("CODE_INTENT_CONCURRENCY"), out var envC) && envC > 0)
        {
            return (envC, $"configured via CODE_INTENT_CONCURRENCY ({envC}x)");
        }

        var cpuCores = cpuCoreCount ?? Environment.ProcessorCount;
        var totalMemBytes = memoryBytes ?? GetTotalMemoryBytes();
        var totalMemGb = totalMemBytes / (1024.0 * 1024.0 * 1024.0);

        var isOsx = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        var isMetal = isOsx && (executionDevice.Contains("Metal", StringComparison.OrdinalIgnoreCase) || isGpu);
        var isCudaOrVulkan = isGpu && !isMetal;

        // 3. Apple Silicon Metal GPU:
        // Metal command queue serializes and synchronizes on ggml_metal_synchronize.
        // On base/Pro Apple Silicon (or < 32 GB unified RAM / < 12 CPU cores), concurrent contexts
        // cause severe command buffer contention and memory thrashing.
        // High-end M-series Max/Ultra with >= 32 GB RAM and >= 12 cores can benefit from 2x parallelism.
        if (isMetal && gpuLayers > 0)
        {
            if (totalMemGb >= 32.0 && cpuCores >= 12)
            {
                return (2, $"auto-tuned for Apple Silicon Max/Ultra ({cpuCores} cores, {totalMemGb:F1} GB unified RAM)");
            }

            return (1, $"auto-tuned for Apple Silicon Metal ({cpuCores} cores, {totalMemGb:F1} GB unified RAM - single stream prevents command queue contention)");
        }

        // 4. Discrete GPU (CUDA / Vulkan on Windows / Linux):
        if (isCudaOrVulkan && gpuLayers > 0)
        {
            if (totalMemGb >= 32.0 && cpuCores >= 16)
            {
                return (4, $"auto-tuned for high-end GPU workstation ({cpuCores} cores, {totalMemGb:F1} GB RAM)");
            }
            if (totalMemGb >= 16.0 && cpuCores >= 8)
            {
                return (2, $"auto-tuned for discrete GPU ({cpuCores} cores, {totalMemGb:F1} GB RAM)");
            }
            return (1, $"auto-tuned for discrete GPU ({cpuCores} cores, {totalMemGb:F1} GB RAM)");
        }

        // 5. CPU-only fallback:
        if (totalMemGb < 12.0 || cpuCores <= 4)
        {
            return (1, $"auto-tuned for CPU inference ({cpuCores} cores, {totalMemGb:F1} GB RAM)");
        }

        var cpuConcurrency = Math.Clamp(cpuCores / 4, 1, 3);
        return (cpuConcurrency, $"auto-tuned for multi-core CPU ({cpuCores} cores, {totalMemGb:F1} GB RAM)");
    }

    public NativeIntentPredictor(string modelPath, int contextSize = 4096, int gpuLayers = 99, int? concurrency = null)
    {
        lock (ConfigLock)
        {
            if (!_configured)
            {
                NativeLibraryConfig.All
                    .WithCuda()
                    .WithVulkan()
                    .WithAutoFallback()
                    .WithLogCallback((_, _) => { });

                NativeLogConfig.llama_log_set(SilentLlamaLog);
                _configured = true;
            }
        }

        var (deviceName, isGpu) = DetectExecutionDevice(gpuLayers);
        ExecutionDevice = deviceName;
        IsGpuAccelerated = isGpu;

        _parameters = new ModelParams(modelPath)
        {
            ContextSize = (uint)contextSize,
            GpuLayerCount = gpuLayers
        };

        _weights = LLamaWeights.LoadFromFile(_parameters);

        var (concurrencyVal, reason) = CalculateOptimalConcurrency(concurrency, isGpu, deviceName, gpuLayers);
        _concurrency = Math.Max(1, concurrencyVal);
        ConcurrencyReason = reason;
        _semaphore = new SemaphoreSlim(_concurrency, _concurrency);

        for (var i = 0; i < _concurrency; i++)
        {
            _executorPool.Add(new StatelessExecutor(_weights, _parameters));
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<BatchInferenceResult?> PredictAsync(
        string filePath,
        string content,
        string? projectName = null,
        IReadOnlyList<string>? knownDomains = null,
        string? projectDomain = null,
        string? projectRole = null,
        CancellationToken cancellationToken = default)
    {
        var (result, _) = await PredictWithRawAsync(filePath, content, projectName, knownDomains, projectDomain, projectRole, cancellationToken);
        return result;
    }


    public async Task<ProjectBoundedContextResult?> PredictProjectBoundedContextAsync(
        ProjectSignature signature,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sbUser = new StringBuilder();
        sbUser.AppendLine($"Service Name: {signature.Name}");
        if (!string.IsNullOrWhiteSpace(signature.Role))
        {
            sbUser.AppendLine($"Workload Role: {signature.Role}");
        }
        if (!string.IsNullOrWhiteSpace(signature.RelativePath))
        {
            sbUser.AppendLine($"Path: {signature.RelativePath.Replace('\\', '/')}");
        }
        if (signature.InboundCallers != null && signature.InboundCallers.Count > 0)
        {
            sbUser.AppendLine($"Called By (Inbound Clients): [{string.Join(", ", signature.InboundCallers.Take(6))}]");
        }
        if (signature.OutboundCalls != null && signature.OutboundCalls.Count > 0)
        {
            sbUser.AppendLine($"Calls (Outbound Services): [{string.Join(", ", signature.OutboundCalls.Take(6))}]");
        }
        if (signature.ReferencedLibraries != null && signature.ReferencedLibraries.Count > 0)
        {
            sbUser.AppendLine($"Referenced Libraries: [{string.Join(", ", signature.ReferencedLibraries.Take(6))}]");
        }
        if (signature.Tables.Count > 0)
        {
            sbUser.AppendLine($"Database Tables: [{string.Join(", ", signature.Tables.Take(12))}]");
        }
        if (signature.DomainTypes.Count > 0)
        {
            var prioritizedTypes = signature.DomainTypes
                .OrderByDescending(t =>
                {
                    var lower = t.ToLowerInvariant();
                    if (lower.EndsWith("module") || lower.EndsWith("config") || lower.EndsWith("options") || lower.EndsWith("constant") || lower.EndsWith("constants")) return 0;
                    if (lower.EndsWith("dto") || lower.EndsWith("request") || lower.EndsWith("response")) return 2;
                    if (lower.EndsWith("service") || lower.EndsWith("handler") || lower.EndsWith("repository") || lower.EndsWith("gateway")) return 3;
                    if (lower.EndsWith("entity") || lower.EndsWith("model") || lower.EndsWith("aggregate") || lower.EndsWith("item")) return 5;
                    return 1;
                })
                .Take(15)
                .ToList();

            sbUser.AppendLine($"Domain Entities / Models: [{string.Join(", ", prioritizedTypes)}]");
        }
        if (signature.Endpoints.Count > 0)
        {
            sbUser.AppendLine($"API Endpoints: [{string.Join(", ", signature.Endpoints.Take(10))}]");
        }
        if (signature.Topics.Count > 0)
        {
            sbUser.AppendLine($"Message Topics / Events: [{string.Join(", ", signature.Topics.Take(8))}]");
        }

        var prompt = $"<|im_start|>system\n{ProjectBoundedContextPrompt}<|im_end|>\n"
                   + $"<|im_start|>user\n{sbUser}<|im_end|>\n"
                   + "<|im_start|>assistant\n";

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 384,
            TokensKeep = 64,
            OverflowStrategy = ContextOverflowStrategy.TruncateAndReprefill,
            AntiPrompts = ["<|im_end|>", "<|endoftext|>"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.1f,
                TopP = 0.95f,
                RepeatPenalty = 1.15f,
                Grammar = new Grammar(ProjectBoundedContextJsonGrammar, "root")
            }
        };

        await _semaphore.WaitAsync(cancellationToken);
        StatelessExecutor? executor = null;
        try
        {
            if (!_executorPool.TryTake(out executor))
            {
                executor = new StatelessExecutor(_weights, _parameters);
            }

            var sb = new StringBuilder();
            await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
            {
                sb.Append(token);
            }

            var raw = sb.ToString();
            return ParseProjectBoundedContextJsonResult(raw);
        }
        finally
        {
            if (executor != null)
            {
                _executorPool.Add(executor);
            }
            _semaphore.Release();
        }
    }

    public async Task<ProjectBoundedContextResult?> PredictProjectBoundedContextAgenticAsync(
        ProjectSignature signature,
        IServiceGraphExplorer explorer,
        int maxTurns = 3,
        Action<string>? logger = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var promptBuilder = new StringBuilder();
        promptBuilder.Append($"<|im_start|>system\n{ProjectBoundedContextAgenticPrompt}<|im_end|>\n");
        promptBuilder.Append($"<|im_start|>user\nService Name: {signature.Name}\nWorkload Role: {signature.Role ?? "Service"}\nPath: {signature.RelativePath.Replace('\\', '/')}\n<|im_end|>\n");

        await _semaphore.WaitAsync(cancellationToken);
        StatelessExecutor? executor = null;
        try
        {
            if (!_executorPool.TryTake(out executor))
            {
                executor = new StatelessExecutor(_weights, _parameters);
            }

            for (var turn = 1; turn <= maxTurns; turn++)
            {
                var isFinalTurn = turn == maxTurns;
                string grammar;
                if (turn == 1)
                {
                    grammar = ProjectBoundedContextToolOnlyGrammar;
                }
                else if (isFinalTurn)
                {
                    grammar = ProjectBoundedContextFinishGrammar;
                }
                else
                {
                    grammar = ProjectBoundedContextAgenticGrammar;
                }

                var currentPrompt = promptBuilder.ToString() + "<|im_start|>assistant\n";

                var inferenceParams = new InferenceParams
                {
                    MaxTokens = 384,
                    TokensKeep = 64,
                    OverflowStrategy = ContextOverflowStrategy.TruncateAndReprefill,
                    AntiPrompts = ["<|im_end|>", "<|endoftext|>"],
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        Temperature = 0.1f,
                        TopP = 0.95f,
                        RepeatPenalty = 1.15f,
                        Grammar = new Grammar(grammar, "root")
                    }
                };

                var sb = new StringBuilder();
                await foreach (var token in executor.InferAsync(currentPrompt, inferenceParams, cancellationToken))
                {
                    sb.Append(token);
                }

                var rawResponse = sb.ToString().Trim();

                // Parse action
                using var doc = JsonDocument.Parse(rawResponse);

                if (doc.RootElement.TryGetProperty("thought", out var thoughtProp))
                {
                    var thought = thoughtProp.GetString();
                    if (!string.IsNullOrWhiteSpace(thought))
                    {
                        logger?.Invoke($"    [{signature.Name}] {thought}");
                    }
                }

                var action = doc.RootElement.TryGetProperty("action", out var actProp) ? actProp.GetString() : "finish";

                if (action == "finish" || isFinalTurn)
                {
                    var bc = doc.RootElement.TryGetProperty("bounded_context", out var bcp) ? bcp.GetString() ?? signature.Name : signature.Name;
                    var cap = doc.RootElement.TryGetProperty("capability", out var capp) ? capp.GetString() ?? $"Handles {signature.Name} business capabilities." : $"Handles {signature.Name} business capabilities.";
                    var dom = doc.RootElement.TryGetProperty("suggested_domain", out var domp) ? domp.GetString() ?? "Core" : "Core";

                    bc = SanitizePredictedDomainName(bc);
                    dom = SanitizePredictedDomainName(dom);

                    var aggs = new List<string>();
                    if (doc.RootElement.TryGetProperty("primary_aggregates", out var aggsProp) && aggsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in aggsProp.EnumerateArray())
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrWhiteSpace(s)) aggs.Add(s);
                        }
                    }

                    logger?.Invoke($"    [{signature.Name}] (Turn {turn}/{maxTurns}) Distilled: BoundedContext={bc} | Domain={dom} | Aggregates=[{string.Join(", ", aggs)}]");
                    return new ProjectBoundedContextResult(signature.Name, bc, aggs, cap, dom);
                }

                // If calling a tool
                if (action == "call_tool")
                {
                    var toolName = doc.RootElement.TryGetProperty("tool", out var tp) ? tp.GetString() : "";
                    var args = doc.RootElement.TryGetProperty("args", out var argsp) ? argsp : default;

                    var targetService = (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("service", out var sp)) ? sp.GetString() : signature.Name;
                    var targetLibrary = (args.ValueKind == JsonValueKind.Object && args.TryGetProperty("library", out var lp)) ? lp.GetString() : "";

                    logger?.Invoke($"    -> [{signature.Name}] (Turn {turn}) Tool Call: {toolName}({(string.IsNullOrEmpty(targetLibrary) ? targetService : targetService + ", lib=" + targetLibrary)})");

                    string observationJson;
                    switch (toolName)
                    {
                        case "get_service_surface":
                            var surface = await explorer.GetServiceSurfaceAsync(targetService ?? signature.Name, cancellationToken);
                            observationJson = JsonSerializer.Serialize(surface);
                            break;
                        case "get_data_lineage":
                            var lineage = await explorer.GetDataLineageAsync(targetService ?? signature.Name, cancellationToken);
                            observationJson = JsonSerializer.Serialize(lineage);
                            break;
                        case "get_service_dependencies":
                            var deps = await explorer.GetServiceDependenciesAsync(targetService ?? signature.Name, cancellationToken);
                            observationJson = JsonSerializer.Serialize(deps);
                            break;
                        case "get_library_entities":
                            var lib = await explorer.GetLibraryEntitiesAsync(targetLibrary ?? "", cancellationToken);
                            observationJson = JsonSerializer.Serialize(lib);
                            break;
                        default:
                            observationJson = "{\"status\": \"unknown_tool\"}";
                            break;
                    }

                    logger?.Invoke($"       Observation: {observationJson}");

                    promptBuilder.Append($"<|im_start|>assistant\n{rawResponse}<|im_end|>\n");
                    promptBuilder.Append($"<|im_start|>user\nObservation: {observationJson}\n<|im_end|>\n");
                }
            }
        }
        finally
        {
            if (executor != null)
            {
                _executorPool.Add(executor);
            }
            _semaphore.Release();
        }

        return null;
    }


    public async Task<(BatchInferenceResult? Result, string RawOutput)> PredictWithRawAsync(
        string filePath,
        string content,
        string? projectName = null,
        IReadOnlyList<string>? knownDomains = null,
        string? projectDomain = null,
        string? projectRole = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var lang = DetectLanguage(filePath);
        var truncated = TruncateContent(content, 4000);
        var fileName = Path.GetFileName(filePath);

        var sbUser = new StringBuilder();
        sbUser.AppendLine($"Language: {lang}");
        sbUser.AppendLine($"File: {fileName}");
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            sbUser.AppendLine($"Project / Subsystem: {projectName}");
        }
        if (!string.IsNullOrWhiteSpace(projectDomain))
        {
            sbUser.AppendLine($"Parent Project Bounded Context: {projectDomain}");
            if (!string.IsNullOrWhiteSpace(projectRole))
            {
                sbUser.AppendLine($"Parent Project Purpose: {projectRole}");
            }
        }
        else if (knownDomains != null && knownDomains.Count > 0)
        {
            var domainList = string.Join(", ", knownDomains.Take(12));
            sbUser.AppendLine($"Known Repository Bounded Contexts: [{domainList}]");
        }
        sbUser.AppendLine();
        sbUser.AppendLine("Code Skeleton:");
        sbUser.AppendLine("```");
        sbUser.AppendLine(truncated);
        sbUser.AppendLine("```");

        var prompt = $"<|im_start|>system\n{SystemPrompt}<|im_end|>\n"
                   + $"<|im_start|>user\n{sbUser}<|im_end|>\n"
                   + "<|im_start|>assistant\n";

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 512,
            TokensKeep = 128,
            OverflowStrategy = ContextOverflowStrategy.TruncateAndReprefill,
            AntiPrompts = ["<|im_end|>", "<|endoftext|>"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.1f,
                TopP = 0.95f,
                RepeatPenalty = 1.15f,
                Grammar = new Grammar(IntentJsonGrammar, "root")
            }
        };

        await _semaphore.WaitAsync(cancellationToken);
        StatelessExecutor? executor = null;
        try
        {
            if (!_executorPool.TryTake(out executor))
            {
                executor = new StatelessExecutor(_weights, _parameters);
            }

            var sb = new StringBuilder();
            await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
            {
                sb.Append(token);
            }

            var raw = sb.ToString();
            var parsed = ParseJsonResult(raw, filePath);
            return (parsed, raw);
        }
        finally
        {
            if (executor != null)
            {
                _executorPool.Add(executor);
            }
            _semaphore.Release();
        }
    }


    private static string TruncateContent(string content, int maxChars)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= maxChars)
        {
            return content;
        }

        var lines = content.Split('\n');
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            if (sb.Length + line.Length > maxChars) break;
            sb.AppendLine(line);
        }

        return sb.Length > 0 ? sb.ToString() : content[..maxChars];
    }

    private static string DetectLanguage(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "csharp",
            ".ts" => "typescript",
            ".tsx" => "typescript",
            ".js" => "javascript",
            ".jsx" => "javascript",
            ".py" => "python",
            ".go" => "go",
            ".java" => "java",
            ".rs" => "rust",
            ".cpp" or ".c" or ".h" => "cpp",
            ".sql" => "sql",
            _ => "text"
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _semaphore.Dispose();
            _weights.Dispose();
        }
    }
}
