using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Analysis;

public sealed class OpenAiIntentPredictor : IIntentPredictor
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _model;
    private readonly int _concurrency;
    private readonly SemaphoreSlim _semaphore;

    public int Concurrency => _concurrency;
    public string ConcurrencyReason => $"configured for external API endpoint ({_concurrency}x)";
    public string ExecutionDevice => $"OpenAI-Compatible Endpoint ({_endpoint}, model: {_model})";

    public OpenAiIntentPredictor(
        string endpoint,
        string? model = null,
        string? apiKey = null,
        int concurrency = 4,
        HttpClient? httpClient = null)
    {
        _endpoint = endpoint.TrimEnd('/');
        _model = string.IsNullOrWhiteSpace(model) ? "default" : model.Trim();
        _concurrency = Math.Max(1, concurrency);
        _semaphore = new SemaphoreSlim(_concurrency, _concurrency);
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(3) };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }
    }

    public async Task<string> SendChatCompletionAsync(
        string userPrompt,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var url = $"{_endpoint}/chat/completions";
            var messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }
            messages.Add(new { role = "user", content = userPrompt });

            var payload = new
            {
                model = _model,
                messages,
                temperature = 0.1
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, jsonContent, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return content ?? string.Empty;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<SystemDomainsResult?> PredictSystemDomainsAsync(
        IReadOnlyList<ProjectSignature> signatures,
        CancellationToken cancellationToken = default)
    {
        var prompt = NativeIntentPredictor.BuildSystemDomainsUserPrompt(signatures);
        var raw = await SendChatCompletionAsync(prompt, NativeIntentPredictor.SystemDomainsPrompt, cancellationToken);
        RobustJsonParser.TryDeserialize<SystemDomainsResult>(raw, out var parsed);
        return parsed;
    }

    public async Task<ProjectBoundedContextResult?> PredictProjectBoundedContextAsync(
        ProjectSignature signature,
        CancellationToken cancellationToken = default)
    {
        var prompt = NativeIntentPredictor.BuildProjectBoundedContextUserPrompt(signature);
        var raw = await SendChatCompletionAsync(prompt, NativeIntentPredictor.ProjectBoundedContextPrompt, cancellationToken);
        RobustJsonParser.TryDeserialize<ProjectBoundedContextResult>(raw, out var parsed);
        return parsed;
    }

    public async Task<SystemDomainsResult?> PredictMacroDomainsFromContextsAsync(
        IReadOnlyList<ProjectBoundedContextResult> contexts,
        CancellationToken cancellationToken = default)
    {
        var prompt = NativeIntentPredictor.BuildMacroDomainsUserPrompt(contexts);
        var raw = await SendChatCompletionAsync(prompt, NativeIntentPredictor.SystemDomainsPrompt, cancellationToken);
        RobustJsonParser.TryDeserialize<SystemDomainsResult>(raw, out var parsed);
        return parsed;
    }

    public async Task<ProjectIntentResult?> PredictProjectIntentAsync(
        ProjectSignature signature,
        CancellationToken cancellationToken = default)
    {
        var prompt = NativeIntentPredictor.BuildProjectUserPrompt(signature);
        var raw = await SendChatCompletionAsync(prompt, NativeIntentPredictor.ProjectSystemPrompt, cancellationToken);
        RobustJsonParser.TryDeserialize<ProjectIntentResult>(raw, out var parsed);
        return parsed;
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
        var prompt = NativeIntentPredictor.BuildFileUserPrompt(filePath, content, projectName, knownDomains, projectDomain, projectRole);
        var raw = await SendChatCompletionAsync(prompt, NativeIntentPredictor.SystemPrompt, cancellationToken);
        if (RobustJsonParser.TryDeserialize<BatchInferenceResult>(raw, out var parsed) && parsed != null)
        {
            return parsed with { FilePath = filePath };
        }
        return null;
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
