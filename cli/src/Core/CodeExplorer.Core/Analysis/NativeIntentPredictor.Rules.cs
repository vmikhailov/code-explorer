using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Database;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace CodeExplorer.Core.Analysis;

public sealed partial class NativeIntentPredictor
{
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

    public static string SanitizePredictedDomainName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Core";

        var clean = name.Trim();
        if (clean.StartsWith("domain:", StringComparison.OrdinalIgnoreCase))
            clean = clean["domain:".Length..].Trim();
        if (clean.StartsWith("dom:", StringComparison.OrdinalIgnoreCase))
            clean = clean["dom:".Length..].Trim();

        clean = clean.Trim('"', '\'', '`', '.', ':', ';', ',', '!', '?');

        // Check for sentence patterns (e.g. starts with "This ", "The ", contains verbs like "is responsible for", "handles all", etc.)
        if (clean.EndsWith('.') ||
            clean.StartsWith("This ", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("The ", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("It ", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("A ", StringComparison.OrdinalIgnoreCase) ||
            clean.StartsWith("An ", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains(" is ", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains(" responsible ", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains(" handles ", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains(" provides ", StringComparison.OrdinalIgnoreCase))
        {
            return "Core";
        }

        // Split into words, stripping '&', separators
        var rawWords = clean.Split([' ', '_', '-', '/', '\\', '&', ',', ';', '(', ')'], StringSplitOptions.RemoveEmptyEntries);

        // Deduplicate consecutive or identical words (e.g. "Order Order" -> "Order")
        var words = new List<string>();
        foreach (var w in rawWords)
        {
            var trimmed = w.Trim('"', '\'', '.', ':', ';', ',');
            if (trimmed.Length == 0) continue;
            if (trimmed.Equals("and", StringComparison.OrdinalIgnoreCase)) continue;
            if (words.Count == 0 || !words[^1].Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                words.Add(trimmed);
            }
        }

        if (words.Count == 0) return "Core";

        // If the name exceeds 4 words or 40 chars, take at most the first 3 words
        if (words.Count > 4 || clean.Length > 40)
        {
            words = words.Take(3).ToList();
        }

        var sb = new StringBuilder();
        foreach (var w in words)
        {
            if (w.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(w[0]));
                if (w.Length > 1) sb.Append(w[1..]);
            }
        }

        var result = sb.ToString();
        return result.Length >= 2 ? result : "Core";
    }

    private SystemDomainsResult? ParseSystemDomainsJsonResult(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput)) return null;

        try
        {
            if (RobustJsonParser.TryDeserialize<SystemDomainsResult>(rawOutput, out var parsed, _jsonOptions) &&
                parsed?.Domains != null && parsed.Domains.Count > 0)
            {
                var sanitized = new List<SystemDomainAssignment>();
                foreach (var d in parsed.Domains)
                {
                    var cleanName = SanitizePredictedDomainName(d.Name);
                    if (!string.IsNullOrWhiteSpace(cleanName))
                    {
                        sanitized.Add(d with { Name = cleanName });
                    }
                }
                return sanitized.Count > 0 ? new SystemDomainsResult(sanitized) : null;
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


    private ProjectBoundedContextResult? ParseProjectBoundedContextJsonResult(string rawOutput)
    {
        if (string.IsNullOrWhiteSpace(rawOutput)) return null;

        try
        {
            if (RobustJsonParser.TryDeserialize<ProjectBoundedContextResult>(rawOutput, out var parsed, _jsonOptions) &&
                parsed != null && !string.IsNullOrWhiteSpace(parsed.BoundedContext))
            {
                var cleanBc = SanitizePredictedDomainName(parsed.BoundedContext);
                var cleanDom = SanitizePredictedDomainName(parsed.SuggestedDomain);
                return parsed with { BoundedContext = cleanBc, SuggestedDomain = cleanDom };
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
            var cleanDom = !string.IsNullOrWhiteSpace(parsed.Domain) ? SanitizePredictedDomainName(parsed.Domain) : parsed.Domain;
            return parsed with { FilePath = filePath, Domain = cleanDom };
        }
        return null;
    }

    private ProjectIntentResult? ParseProjectJsonResult(string rawOutput)
    {
        if (RobustJsonParser.TryDeserialize<ProjectIntentResult>(rawOutput, out var parsed, _jsonOptions) &&
            parsed != null && !string.IsNullOrWhiteSpace(parsed.Domain))
        {
            return parsed with { Domain = SanitizePredictedDomainName(parsed.Domain) };
        }
        return null;
    }

}
