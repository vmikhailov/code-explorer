using System.Text;
using System.Text.Json;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

namespace CodeExplorer.Core.Analysis;

public sealed class NativeIntentPredictor : IDisposable
{
    private static readonly object ConfigLock = new();
    private static bool _configured;

    private readonly LLamaWeights _weights;
    private readonly ModelParams _parameters;
    private readonly StatelessExecutor _executor;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;
    private static readonly NativeLogConfig.LLamaLogCallback SilentLlamaLog = (_, _) => { };

    public NativeIntentPredictor(string modelPath, int contextSize = 2048, int gpuLayers = 99)
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

        _parameters = new ModelParams(modelPath)
        {
            ContextSize = (uint)contextSize,
            GpuLayerCount = gpuLayers
        };

        _weights = LLamaWeights.LoadFromFile(_parameters);
        _executor = new StatelessExecutor(_weights, _parameters);

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<BatchInferenceResult?> PredictAsync(
        string filePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var (result, _) = await PredictWithRawAsync(filePath, content, cancellationToken);
        return result;
    }

    private const string SystemPrompt =
        "You are an expert Enterprise Software Architect and Static Code Analyzer.\n" +
        "Analyze the provided code module skeleton (namespace, classes, interfaces, dependencies, method signatures, attributes, and internal calls).\n" +
        "Determine its architectural role, bounded context (business domain), mutations, capabilities, and intent for a Code Knowledge Graph.\n\n" +
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
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var lang = DetectLanguage(filePath);
        var truncated = TruncateContent(content, 4000);
        var fileName = Path.GetFileName(filePath);
        var userMsg = $"Language: {lang}\nFile: {fileName}\n\nCode Skeleton:\n```\n{truncated}\n```";

        var prompt = $"<|im_start|>system\n{SystemPrompt}<|im_end|>\n"
                   + $"<|im_start|>user\n{userMsg}<|im_end|>\n"
                   + "<|im_start|>assistant\n";

        var inferenceParams = new InferenceParams
        {
            MaxTokens = 1024,
            AntiPrompts = ["<|im_end|>", "<|endoftext|>"],
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = 0.1f,
                TopP = 0.95f
            }
        };

        var sb = new StringBuilder();
        await foreach (var token in _executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            sb.Append(token);
        }

        var raw = sb.ToString();
        var parsed = ParseJsonResult(raw, filePath);
        return (parsed, raw);
    }

    private BatchInferenceResult? ParseJsonResult(string rawOutput, string filePath)
    {
        if (string.IsNullOrWhiteSpace(rawOutput)) return null;

        var firstBrace = rawOutput.IndexOf('{');
        if (firstBrace < 0) return null;

        var lastBrace = rawOutput.LastIndexOf('}');
        string? jsonStr = null;

        if (lastBrace > firstBrace)
        {
            jsonStr = rawOutput.Substring(firstBrace, lastBrace - firstBrace + 1);
            try
            {
                var parsed = JsonSerializer.Deserialize<BatchInferenceResult>(jsonStr, _jsonOptions);
                if (parsed != null)
                {
                    return parsed with { FilePath = filePath };
                }
            }
            catch
            {
                // Fall through to attempt healing
            }
        }

        // Attempt healing if truncated (e.g. missing closing braces)
        try
        {
            var candidate = rawOutput[firstBrace..].Trim();
            // Remove markdown closing fence if present
            if (candidate.EndsWith("```"))
            {
                candidate = candidate[..^3].TrimEnd();
            }

            var openBraces = 0;
            var openBrackets = 0;
            var inString = false;
            var escape = false;

            foreach (var ch in candidate)
            {
                if (escape) { escape = false; continue; }
                if (ch == '\\') { escape = true; continue; }
                if (ch == '"') { inString = !inString; continue; }
                if (inString) continue;

                if (ch == '{') openBraces++;
                else if (ch == '}') openBraces--;
                else if (ch == '[') openBrackets++;
                else if (ch == ']') openBrackets--;
            }

            var sb = new StringBuilder(candidate);
            if (inString) sb.Append('"');
            while (openBrackets > 0) { sb.Append(']'); openBrackets--; }
            while (openBraces > 0) { sb.Append('}'); openBraces--; }

            var healed = sb.ToString();
            var parsed = JsonSerializer.Deserialize<BatchInferenceResult>(healed, _jsonOptions);
            if (parsed != null)
            {
                return parsed with { FilePath = filePath };
            }
        }
        catch
        {
            // Failed to parse or heal JSON
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
            _weights.Dispose();
            _disposed = true;
        }
    }
}
