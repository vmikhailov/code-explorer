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

public sealed class NativeIntentPredictor : IIntentPredictor
{
    private static readonly object ConfigLock = new();
    private static bool _configured;

    private readonly LLamaWeights _weights;
    private readonly ModelParams _parameters;
    private readonly System.Collections.Concurrent.ConcurrentBag<StatelessExecutor> _executorPool = new();
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

                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p != null)
                    {
                        var str = p.StandardOutput.ReadToEnd().Trim();
                        if (ulong.TryParse(str, out var bytes) && bytes > 0) return bytes;
                    }
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

    public const string SystemDomainsPrompt =
        "You are a Principal Enterprise Software Architect specializing in Domain-Driven Design (DDD).\n" +
        "Analyze the system architecture catalog of services, directory paths, shared database tables, API endpoints, messaging topics, and domain entities.\n" +
        "Your goal is to synthesize a disjoint partition of exactly 6 to 10 cohesive high-level Business Domains (Problem Spaces).\n\n" +
        "FUNDAMENTAL DDD RULES:\n" +
        "1. Problem Space vs Solution Space: A Business Domain represents an overarching business problem space (e.g., Billing, Identity, Inventory, Catalog). An individual service or microservice is only a Bounded Context inside a Domain, NEVER a domain on its own.\n" +
        "2. Data & Entity Cohesion: Services that access the same database tables, manipulate the same entity aggregates, or communicate via domain events belong to the SAME Business Domain.\n" +
        "3. Path & Capability Alignment: Services located in related directory namespaces, or cooperating as API backend, worker, scheduler, or streaming engine for a capability belong to the SAME Business Domain.\n" +
        "4. Disjoint Exhaustive Partition: Group ALL services into exactly 6 to 10 Business Domains. Every service must appear in exactly one domain. No service left behind.\n" +
        "5. Canonical Domain Naming: Name each domain with a single, clear, high-level business concept noun in PascalCase (e.g., derived from the core entity aggregates or business capability).\n\n" +
        "Respond ONLY with valid JSON in this format:\n" +
        "{\n" +
        "  \"domains\": [\n" +
        "    {\n" +
        "      \"name\": \"PascalCaseDomainName\",\n" +
        "      \"description\": \"1-2 sentence description of the business problem space and responsibilities.\",\n" +
        "      \"services\": [\"service-1\", \"service-2\"]\n" +
        "    }\n" +
        "  ]\n" +
        "}\n";

    public const string SystemDomainsJsonGrammar = """
root ::= "{" ws "\"domains\":" ws domain-list "}" ws
domain-list ::= "[" ws (domain-obj ("," ws domain-obj)*)? ws "]"
domain-obj ::= "{" ws "\"name\":" ws string "," ws "\"description\":" ws string "," ws "\"services\":" ws string-list "}" ws
string-list ::= "[" ws (string ("," ws string)*)? ws "]"
string ::= "\"" ([^"\\] | "\\" ["\\/bfnrt])* "\""
ws ::= [ \t\n\r]*
""";

    public static string BuildSystemDomainsUserPrompt(IReadOnlyList<ProjectSignature> signatures)
    {
        var sbUser = new StringBuilder();
        sbUser.AppendLine($"Total Services in System: {signatures.Count}");
        sbUser.AppendLine();
        foreach (var sig in signatures)
        {
            var details = new List<string>();
            if (!string.IsNullOrWhiteSpace(sig.RelativePath))
            {
                details.Add($"Path: {sig.RelativePath.Replace('\\', '/')}");
            }
            if (sig.Tables.Count > 0)
            {
                details.Add($"Tables: [{string.Join(", ", sig.Tables.Take(8))}]");
            }
            if (sig.Endpoints.Count > 0)
            {
                details.Add($"Endpoints: [{string.Join(", ", sig.Endpoints.Take(6))}]");
            }
            if (sig.Topics.Count > 0)
            {
                details.Add($"Topics: [{string.Join(", ", sig.Topics.Take(5))}]");
            }
            if (sig.DomainTypes.Count > 0)
            {
                details.Add($"Entities: [{string.Join(", ", sig.DomainTypes.Take(6))}]");
            }

            var detailStr = details.Count > 0 ? " | " + string.Join(" | ", details) : "";
            sbUser.AppendLine($"- Service: {sig.Name}{detailStr}");
        }
        return sbUser.ToString();
    }

    public static string BuildProjectBoundedContextUserPrompt(ProjectSignature signature)
    {
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
                .Take(12);

            sbUser.AppendLine($"Domain Entities / Aggregates: [{string.Join(", ", prioritizedTypes)}]");
        }
        if (signature.Endpoints.Count > 0)
        {
            sbUser.AppendLine($"API Endpoints: [{string.Join(", ", signature.Endpoints.Take(10))}]");
        }
        if (signature.Topics.Count > 0)
        {
            sbUser.AppendLine($"Message Topics / Queues: [{string.Join(", ", signature.Topics.Take(8))}]");
        }
        return sbUser.ToString();
    }

    public static string BuildMacroDomainsUserPrompt(IReadOnlyList<ProjectBoundedContextResult> contexts)
    {
        var sbUser = new StringBuilder();
        sbUser.AppendLine($"Total Services in System: {contexts.Count}");
        sbUser.AppendLine("Service Bounded Contexts, Aggregates, and Capabilities:");
        sbUser.AppendLine();

        foreach (var ctx in contexts)
        {
            var aggs = ctx.PrimaryAggregates.Count > 0 ? string.Join(", ", ctx.PrimaryAggregates.Take(3)) : "None";
            sbUser.AppendLine($"- {ctx.Service}: {ctx.BoundedContext} | Aggs: [{aggs}] | Domain: {ctx.SuggestedDomain}");
        }
        return sbUser.ToString();
    }

    public static string BuildProjectUserPrompt(ProjectSignature signature, IReadOnlyList<string>? knownDomains = null)
    {
        var sbUser = new StringBuilder();
        sbUser.AppendLine($"Project Name: {signature.Name}");
        if (signature.Endpoints.Count > 0)
        {
            sbUser.AppendLine($"Key Endpoints: {string.Join(", ", signature.Endpoints.Take(12))}");
        }
        if (signature.Tables.Count > 0)
        {
            sbUser.AppendLine($"Tables / Collections: {string.Join(", ", signature.Tables.Take(12))}");
        }
        if (signature.Topics.Count > 0)
        {
            sbUser.AppendLine($"Message Topics: {string.Join(", ", signature.Topics.Take(10))}");
        }
        if (signature.DomainTypes.Count > 0)
        {
            sbUser.AppendLine($"Core Domain Types: {string.Join(", ", signature.DomainTypes.Take(12))}");
        }
        return sbUser.ToString();
    }

    public static string BuildFileUserPrompt(
        string filePath,
        string content,
        string? projectName = null,
        IReadOnlyList<string>? knownDomains = null,
        string? projectDomain = null,
        string? projectRole = null)
    {
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
        return sbUser.ToString();
    }

    public async Task<SystemDomainsResult?> PredictSystemDomainsAsync(
        IReadOnlyList<ProjectSignature> signatures,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (signatures.Count == 0) return null;

        var sbUser = BuildSystemDomainsUserPrompt(signatures);

        var prompt = $"<|im_start|>system\n{SystemDomainsPrompt}<|im_end|>\n"
                   + $"<|im_start|>user\n{sbUser}<|im_end|>\n"
                   + "<|im_start|>assistant\n";

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 1536,
            TokensKeep = 64,
            OverflowStrategy = ContextOverflowStrategy.TruncateAndReprefill,
            AntiPrompts = ["<|im_end|>", "<|endoftext|>"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.1f,
                TopP = 0.95f,
                RepeatPenalty = 1.15f,
                Grammar = new Grammar(SystemDomainsJsonGrammar, "root")
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
            return ParseSystemDomainsJsonResult(raw);
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

    private SystemDomainsResult? ParseSystemDomainsJsonResult(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput)) return null;

        try
        {
            if (RobustJsonParser.TryDeserialize<SystemDomainsResult>(rawOutput, out var parsed, _jsonOptions) &&
                parsed?.Domains != null && parsed.Domains.Count > 0)
            {
                return parsed;
            }
        }
        catch
        {
            // fallback
        }

        return null;
    }

    public const string ProjectBoundedContextPrompt =
        "You are a Principal Software Architect specializing in Domain-Driven Design (DDD).\n" +
        "Analyze the provided Service Signature (service name, relative directory path, database tables, domain entities, and API endpoints).\n" +
        "Extract its architectural Bounded Context, primary Aggregate Roots, core capability, and suggested Problem Space Domain:\n\n" +
        "1. \"service\": The exact service name provided.\n" +
        "2. \"bounded_context\": Canonical Bounded Context name in PascalCase (e.g. \"OrderManagement\", \"PaymentProcessing\", \"InventoryFulfillment\", \"CustomerIdentity\").\n" +
        "3. \"primary_aggregates\": 1 to 3 primary Aggregate Root entity names in PascalCase (e.g. [\"Order\", \"Payment\"]).\n" +
        "4. \"capability\": One clear, concise sentence describing the core business capability delivered by this service.\n" +
        "5. \"suggested_domain\": An overarching high-level Business Domain (Problem Space) noun in PascalCase (e.g. \"Operations\", \"Calculations\", \"Bundles\", \"Domains\", \"Traffic\", \"Identity\", \"Billing\", \"Integrations\", \"Analytics\", \"Metadata\"). Keep it 1 to 2 words maximum (e.g. \"Calculations\", NOT \"CalculationsAndReportingDataManagement\").\n\n" +
        "Respond ONLY with valid JSON.";

    public const string ProjectBoundedContextJsonGrammar = """
root ::= "{" ws "\"service\":" ws string "," ws "\"bounded_context\":" ws string "," ws "\"primary_aggregates\":" ws string-list "," ws "\"capability\":" ws string "," ws "\"suggested_domain\":" ws string "}" ws

string-list ::= "[" ws (string ("," ws string)*)? ws "]"
string ::= "\"" ([^"\\] | "\\" ["\\/bfnrt])* "\""
ws ::= [ \t\n\r]*
""";

    public const string ProjectBoundedContextAgenticPrompt =
        "You are a Principal Software Architect specializing in Domain-Driven Design (DDD).\n" +
        "Your task is to determine the architectural Bounded Context, primary Aggregate Roots, core capability, and suggested Problem Space Domain for a target service.\n" +
        "You have access to tools to query the Code Knowledge Graph:\n" +
        "- get_service_surface: retrieves API endpoints and message queues\n" +
        "- get_data_lineage: retrieves database tables and storage accessed\n" +
        "- get_service_dependencies: retrieves referenced shared libraries and service calls\n" +
        "- get_library_entities: retrieves domain entities declared in a referenced library\n\n" +
        "To explore the service, output JSON with your thought and a tool call:\n" +
        "{\"thought\": \"<reasoning>\", \"action\": \"call_tool\", \"tool\": \"<tool_name>\", \"args\": {\"service\": \"<service_name>\"}}\n\n" +
        "When you have sufficient graph evidence, conclude with:\n" +
        "{\"thought\": \"<reasoning>\", \"action\": \"finish\", \"bounded_context\": \"<PascalCaseContext>\", \"primary_aggregates\": [\"<Aggregate1>\"], \"capability\": \"<One concise capability sentence>\", \"suggested_domain\": \"<1-2 word Domain noun>\"}\n\n" +
        "Respond ONLY with valid JSON.";

    public const string ProjectBoundedContextToolOnlyGrammar = """
root ::= "{" ws call-tool ws "}" ws

call-tool ::= (thought)? "\"action\":" ws "\"call_tool\"," ws "\"tool\":" ws tool-name "," ws "\"args\":" ws tool-args
thought ::= "\"thought\":" ws string "," ws
tool-name ::= "\"get_service_surface\"" | "\"get_data_lineage\"" | "\"get_service_dependencies\"" | "\"get_library_entities\""
tool-args ::= "{" ws (arg-pair | arg-service | arg-library)? ws "}"
arg-pair ::= arg-service "," ws arg-library | arg-library "," ws arg-service
arg-service ::= "\"service\":" ws string
arg-library ::= "\"library\":" ws string

string ::= "\"" ([^"\\] | "\\" ["\\/bfnrt])* "\""
ws ::= [ \t\n\r]*
""";

    public const string ProjectBoundedContextAgenticGrammar = """
root ::= "{" ws (call-tool | finish) ws "}" ws

call-tool ::= (thought)? "\"action\":" ws "\"call_tool\"," ws "\"tool\":" ws tool-name "," ws "\"args\":" ws tool-args
thought ::= "\"thought\":" ws string "," ws
tool-name ::= "\"get_service_surface\"" | "\"get_data_lineage\"" | "\"get_service_dependencies\"" | "\"get_library_entities\""
tool-args ::= "{" ws (arg-pair | arg-service | arg-library)? ws "}"
arg-pair ::= arg-service "," ws arg-library | arg-library "," ws arg-service
arg-service ::= "\"service\":" ws string
arg-library ::= "\"library\":" ws string

finish ::= (thought)? "\"action\":" ws "\"finish\"," ws "\"bounded_context\":" ws string "," ws "\"primary_aggregates\":" ws string-list "," ws "\"capability\":" ws string "," ws "\"suggested_domain\":" ws string

string-list ::= "[" ws (string ("," ws string)*)? ws "]"
string ::= "\"" ([^"\\] | "\\" ["\\/bfnrt])* "\""
ws ::= [ \t\n\r]*
""";

    public const string ProjectBoundedContextFinishGrammar = """
root ::= "{" ws finish ws "}" ws

finish ::= (thought)? "\"action\":" ws "\"finish\"," ws "\"bounded_context\":" ws string "," ws "\"primary_aggregates\":" ws string-list "," ws "\"capability\":" ws string "," ws "\"suggested_domain\":" ws string
thought ::= "\"thought\":" ws string "," ws

string-list ::= "[" ws (string ("," ws string)*)? ws "]"
string ::= "\"" ([^"\\] | "\\" ["\\/bfnrt])* "\""
ws ::= [ \t\n\r]*
""";

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

    private ProjectBoundedContextResult? ParseProjectBoundedContextJsonResult(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput)) return null;

        try
        {
            if (RobustJsonParser.TryDeserialize<ProjectBoundedContextResult>(rawOutput, out var parsed, _jsonOptions) &&
                parsed != null && !string.IsNullOrWhiteSpace(parsed.BoundedContext))
            {
                return parsed;
            }
        }
        catch
        {
            // fallback
        }

        return null;
    }

    public async Task<SystemDomainsResult?> PredictMacroDomainsFromContextsAsync(
        IReadOnlyList<ProjectBoundedContextResult> contexts,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (contexts.Count == 0) return null;

        var sbUser = new StringBuilder();
        sbUser.AppendLine($"Total Services in System: {contexts.Count}");
        sbUser.AppendLine("Service Bounded Contexts, Aggregates, and Capabilities:");
        sbUser.AppendLine();

        foreach (var ctx in contexts)
        {
            var aggs = ctx.PrimaryAggregates.Count > 0 ? string.Join(", ", ctx.PrimaryAggregates.Take(3)) : "None";
            sbUser.AppendLine($"- {ctx.Service}: {ctx.BoundedContext} | Aggs: [{aggs}] | Domain: {ctx.SuggestedDomain}");
        }

        var prompt = $"<|im_start|>system\n{SystemDomainsPrompt}<|im_end|>\n"
                   + $"<|im_start|>user\n{sbUser}<|im_end|>\n"
                   + "<|im_start|>assistant\n";

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 1024,
            TokensKeep = 64,
            OverflowStrategy = ContextOverflowStrategy.TruncateAndReprefill,
            AntiPrompts = ["<|im_end|>", "<|endoftext|>"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.1f,
                TopP = 0.95f,
                RepeatPenalty = 1.15f,
                Grammar = new Grammar(SystemDomainsJsonGrammar, "root")
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
            return ParseSystemDomainsJsonResult(raw);
        }
        catch
        {
            return null;
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

    public const string ProjectSystemPrompt =
        "You are an expert Enterprise Software Architect.\n" +
        "Analyze the provided Project Signature (service name, endpoints, databases, tables, and domain types).\n" +
        "Determine its primary architectural Bounded Context (Domain), its architectural role, and its business purpose.\n\n" +
        "Respond ONLY with valid JSON in this format:\n" +
        "{\n" +
        "  \"domain\": \"PascalCaseDomainName\",\n" +
        "  \"project_role\": \"1-2 concise sentences describing what this service/project does and its business value\",\n" +
        "  \"capabilities\": [\"Capability1\", \"Capability2\"]\n" +
        "}\n";

    public const string ProjectIntentJsonGrammar = """
root ::= "{" ws "\"domain\":" ws string "," ws "\"project_role\":" ws string "," ws "\"capabilities\":" ws string-list "}" ws

string-list ::= "[" ws (string ("," ws string)*)? ws "]"
string ::= "\"" ([^"\\] | "\\" ["\\/bfnrt])* "\""
ws ::= [ \t\n\r]*
""";

    public async Task<ProjectIntentResult?> PredictProjectIntentAsync(
        ProjectSignature signature,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var sbUser = new StringBuilder();
        sbUser.AppendLine($"Project Name: {signature.Name}");
        if (signature.Endpoints.Count > 0)
        {
            sbUser.AppendLine($"Key Endpoints: {string.Join(", ", signature.Endpoints.Take(12))}");
        }
        if (signature.Tables.Count > 0)
        {
            sbUser.AppendLine($"Tables / Collections: {string.Join(", ", signature.Tables.Take(12))}");
        }
        if (signature.Topics.Count > 0)
        {
            sbUser.AppendLine($"Message Topics: {string.Join(", ", signature.Topics.Take(10))}");
        }
        if (signature.DomainTypes.Count > 0)
        {
            sbUser.AppendLine($"Core Domain Types: {string.Join(", ", signature.DomainTypes.Take(12))}");
        }

        var prompt = $"<|im_start|>system\n{ProjectSystemPrompt}<|im_end|>\n"
                   + $"<|im_start|>user\n{sbUser}<|im_end|>\n"
                   + "<|im_start|>assistant\n";

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 256,
            TokensKeep = 64,
            OverflowStrategy = ContextOverflowStrategy.TruncateAndReprefill,
            AntiPrompts = ["<|im_end|>", "<|endoftext|>"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.1f,
                TopP = 0.95f,
                RepeatPenalty = 1.15f,
                Grammar = new Grammar(ProjectIntentJsonGrammar, "root")
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
            return ParseProjectJsonResult(raw);
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

    public const string SystemPrompt =
        "You are an expert Enterprise Software Architect and Static Code Analyzer.\n" +
        "Analyze the provided code module skeleton (namespace, classes, interfaces, dependencies, method signatures, attributes, and internal calls).\n" +
        "Determine its architectural role, bounded context (business domain), mutations, capabilities, and intent for a Code Knowledge Graph.\n\n" +
        "BOUNDED CONTEXT RESOLUTION RULES:\n" +
        "- If 'Parent Project Bounded Context' is provided, ALWAYS reuse that exact domain name unless the module represents an entirely independent cross-cutting utility.\n" +
        "- If 'Known Repository Bounded Contexts' are provided and this file logically belongs to one of them, REUSE that exact domain name.\n" +
        "- If the project/subsystem has a primary domain, align with it rather than fragmenting into multiple fine-grained domain names.\n" +
        "- Only introduce a new domain name if the module represents an entirely distinct business capability.\n\n" +
        "STRICT TAXONOMY RULES:\n" +
        "1. layer MUST be strictly one of: [Domain, Application, Infrastructure, Presentation, Shared]\n" +
        "   - Domain: Enterprise business logic, entities, value objects, domain events, domain service contracts. No external dependencies.\n" +
        "   - Application: Orchestrators, use cases, command/query handlers (CQRS), application DTOs, workflow managers.\n" +
        "   - Infrastructure: Database access (EF, Dapper, SQL), external APIs (Stripe, SendGrid), message queues (RabbitMQ, Kafka), file system.\n" +
        "   - Presentation: HTTP controllers, gRPC services, GraphQL resolvers, CLI commands, UI view models.\n" +
        "   - Shared: Common utilities, generic extensions, primitives, cross-cutting helpers.\n\n" +
        "2. pattern MUST be strictly one of: [Entity, ValueObject, Repository, Service, Controller, CommandHandler, QueryHandler, EventHandler, Adapter, Factory, DTO, Middleware, Policy, Utility]\n\n" +
        "3. domain: Identifies the business capability or Bounded Context (e.g. \"IdentityAndAccess\", \"Billing\", \"Catalog\", \"OrderManagement\", \"Notifications\", \"Telemetry\"). Use PascalCase.\n\n" +
        "4. operation_type: Categorizes state mutation role:\n" +
        "   - \"Query\": Pure read-only operation (fetches, searches, calculates without side effects).\n" +
        "   - \"Command\": State mutation / write operation (inserts, updates, deletes, modifies state).\n" +
        "   - \"EventProducer\": Dispatches or publishes domain / integration events.\n" +
        "   - \"EventHandler\": Subscribes to or consumes events / messages.\n" +
        "   - \"Configuration\": Setup, DI wiring, option models.\n" +
        "   - \"Utility\": Helper, validator, formatting functions.\n\n" +
        "5. target_entities: Array of domain nouns and business entities this module primarily operates on (e.g. [\"Order\", \"PaymentTransaction\"]).\n\n" +
        "6. emitted_events: Array of domain or integration events this component triggers or publishes (e.g. [\"OrderCreatedEvent\", \"PaymentProcessed\"]). Empty array if none.\n\n" +
        "7. capability_tag: Exactly one CamelCase semantic capability tag describing the action for code-search (e.g. \"CreateCustomerOrder\", \"ProcessCreditCardPayment\", \"ValidateUserProfile\").\n\n" +
        "8. is_pure_domain: Boolean (true if this class contains pure business logic without DB, IO, or network dependencies; false if it interacts with external state/IO).\n\n" +
        "9. intent_summary: Exactly 1 or 2 concise sentences describing what this component does, its technical responsibility, and its business value.\n\n" +
        "You MUST respond ONLY with a valid JSON object matching this schema:\n" +
        "{\n" +
        "  \"domain\": \"string\",\n" +
        "  \"layer\": \"string\",\n" +
        "  \"pattern\": \"string\",\n" +
        "  \"operation_type\": \"string\",\n" +
        "  \"target_entities\": [\"string\"],\n" +
        "  \"emitted_events\": [\"string\"],\n" +
        "  \"capability_tag\": \"string\",\n" +
        "  \"is_pure_domain\": true,\n" +
        "  \"intent_summary\": \"string\"\n" +
        "}\n";

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

    public const string IntentJsonGrammar = """
root ::= "{" ws "\"domain\":" ws string "," ws "\"layer\":" ws layer-enum "," ws "\"pattern\":" ws pattern-enum "," ws "\"operation_type\":" ws op-enum "," ws "\"target_entities\":" ws string-list "," ws "\"emitted_events\":" ws string-list "," ws "\"capability_tag\":" ws string "," ws "\"is_pure_domain\":" ws boolean "," ws "\"intent_summary\":" ws string "}" ws

layer-enum ::= "\"Domain\"" | "\"Application\"" | "\"Infrastructure\"" | "\"Presentation\"" | "\"Shared\""

pattern-enum ::= "\"Entity\"" | "\"ValueObject\"" | "\"Repository\"" | "\"Service\"" | "\"Controller\"" | "\"CommandHandler\"" | "\"QueryHandler\"" | "\"EventHandler\"" | "\"Adapter\"" | "\"Factory\"" | "\"DTO\"" | "\"Middleware\"" | "\"Policy\"" | "\"Utility\""

op-enum ::= "\"Query\"" | "\"Command\"" | "\"EventProducer\"" | "\"EventHandler\"" | "\"Configuration\"" | "\"Utility\""

string-list ::= "[" ws (string ("," ws string)*)? ws "]"
string ::= "\"" ([^"\\] | "\\" ["\\/bfnrt])* "\""
boolean ::= "true" | "false"
ws ::= [ \t\n\r]*
""";

    private BatchInferenceResult? ParseJsonResult(string rawOutput, string filePath)
    {
        if (RobustJsonParser.TryDeserialize<BatchInferenceResult>(rawOutput, out var parsed, _jsonOptions) && parsed != null)
        {
            return parsed with { FilePath = filePath };
        }
        return null;
    }

    private ProjectIntentResult? ParseProjectJsonResult(string rawOutput)
    {
        if (RobustJsonParser.TryDeserialize<ProjectIntentResult>(rawOutput, out var parsed, _jsonOptions) &&
            parsed != null && !string.IsNullOrWhiteSpace(parsed.Domain))
        {
            return parsed;
        }
        return null;
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
