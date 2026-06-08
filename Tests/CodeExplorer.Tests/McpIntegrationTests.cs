using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
public class McpIntegrationTests
{
    private static Task? _serverTask;
    private static HttpClient? _httpClient;
    private static SseSession? _sseSession;
    private static string? _postEndpoint;
    private static string? _tempWorkspace;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // 1. Create a temporary workspace and index it
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_mcp_test_" + Guid.NewGuid()).Replace('\\', '/');
        var projDir = Path.Combine(_tempWorkspace, "CodeExplorer").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "package.json"), "{}");
        var fileCode = @"
        import { Controller, Post } from '@nestjs/common';
        @Controller('orders')
        export class OrdersController {
            @Post('charge')
            async chargeOrder() {}
        }";
        await File.WriteAllTextAsync(Path.Combine(projDir, "server.ts"), fileCode);

        await using var client = new MemgraphClient("bolt://127.0.0.1:7687", "", "");
        WorkspaceIndexer.Register(new TypeScriptParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        // Start the server in a background thread
        _serverTask = Task.Run(() => Program.Main(["mcp", "--port", "8085"]));

        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Wait for the server to become available
        var available = false;
        Exception? lastEx = null;
        for (var i = 0; i < 40; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync("http://127.0.0.1:8085/");
                available = true;
                break;
            }
            catch (Exception ex)
            {
                lastEx = ex;
            }
            await Task.Delay(250);
        }

        if (!available)
        {
            Assert.Fail($"MCP Server failed to start on port 8085. Last exception: {lastEx?.Message}\n{lastEx?.StackTrace}");
        }

        // Initialize the SSE session
        _sseSession = new SseSession(_httpClient, _tempWorkspace);
        
        // Wait for the post endpoint to be received
        _postEndpoint = await _sseSession.EndpointPathTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(_postEndpoint, Is.Not.Null);
        Console.WriteLine($"Connected to SSE. Endpoint: {_postEndpoint}");

        // Perform the handshake (initialize request)
        var initJson = @"{
            ""jsonrpc"": ""2.0"",
            ""id"": 100,
            ""method"": ""initialize"",
            ""params"": {
                ""protocolVersion"": ""2024-11-05"",
                ""capabilities"": {},
                ""clientInfo"": {
                    ""name"": ""NUnitIntegrationTests"",
                    ""version"": ""1.0.0""
                }
            }
        }";

        var responsePost = await PostMessageAsync(initJson);
        Assert.That(responsePost.IsSuccessStatusCode, Is.True, $"Initialize POST returned {responsePost.StatusCode}");

        // Wait for initialize response
        var initResponseStr = await _sseSession.WaitForNextMessageAsync(100, TimeSpan.FromSeconds(10));
        Assert.That(initResponseStr, Is.Not.Null);
        
        using var doc = JsonDocument.Parse(initResponseStr);
        Assert.That(doc.RootElement.GetProperty("id").GetInt32(), Is.EqualTo(100));
        Console.WriteLine("Initialization handshake completed successfully!");

        // Send initialized notification
        var initializedJson = @"{
            ""jsonrpc"": ""2.0"",
            ""method"": ""notifications/initialized"",
            ""params"": {}
        }";
        responsePost = await PostMessageAsync(initializedJson);
        Assert.That(responsePost.IsSuccessStatusCode, Is.True);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_sseSession != null)
        {
            await _sseSession.DisposeAsync();
        }

        _httpClient?.Dispose();

        if (Program.App != null)
        {
            await Program.App.StopAsync();
        }

        if (_serverTask != null)
        {
            try
            {
                await _serverTask;
                _serverTask.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server task finished with exception: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(_tempWorkspace) && Directory.Exists(_tempWorkspace))
        {
            try
            {
                Directory.Delete(_tempWorkspace, true);
            }
            catch
            {
                // ignore
            }
        }
    }

    private async Task<HttpResponseMessage> PostMessageAsync(string jsonPayload)
    {
        var relPath = _postEndpoint!.StartsWith("/") ? _postEndpoint : $"/{_postEndpoint}";
        var url = $"http://127.0.0.1:8085{relPath}";
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        return await _httpClient!.PostAsync(url, content);
    }

    private async Task CallToolAndAssertSuccessAsync(string toolName, string argumentsJson, int responseId)
    {
        var payload = $$"""
        {
            "jsonrpc": "2.0",
            "id": {{responseId}},
            "method": "tools/call",
            "params": {
                "name": "{{toolName}}",
                "arguments": {{argumentsJson}}
            }
        }
        """;

        var responsePost = await PostMessageAsync(payload);
        Assert.That(responsePost.IsSuccessStatusCode, Is.True, $"Calling {toolName} returned POST status {responsePost.StatusCode}");

        var sseMessageStr = await _sseSession!.WaitForNextMessageAsync(responseId, TimeSpan.FromSeconds(15));
        Assert.That(sseMessageStr, Is.Not.Null);

        using var doc = JsonDocument.Parse(sseMessageStr);
        var root = doc.RootElement;
        
        Assert.That(root.GetProperty("id").GetInt32(), Is.EqualTo(responseId));
        
        // Assert no error field on jsonrpc response
        if (root.TryGetProperty("error", out var errorElement))
        {
            Assert.Fail($"Tool {toolName} returned error: {errorElement.GetRawText()}");
        }

        // Verify result exists
        Assert.That(root.TryGetProperty("result", out var resultElement), Is.True);
        
        // Verify isError is false in result content
        if (resultElement.TryGetProperty("isError", out var isErrorElement))
        {
            if (isErrorElement.GetBoolean())
            {
                var errorText = "No details";
                if (resultElement.TryGetProperty("content", out var contentElement) && 
                    contentElement.ValueKind == JsonValueKind.Array && 
                    contentElement.GetArrayLength() > 0)
                {
                    errorText = contentElement[0].GetProperty("text").GetString();
                }
                Assert.Fail($"Tool {toolName} result marked with isError: true. Error details: {errorText}");
            }
        }

        Console.WriteLine($"Tool {toolName} executed successfully.");
    }

    [Test]
    public async Task Test_GetTaxonomy()
    {
        await CallToolAndAssertSuccessAsync("get_taxonomy", "{}", 1);
    }

    [Test]
    public async Task Test_GetArchitectureMap()
    {
        await CallToolAndAssertSuccessAsync("get_architecture_map", "{}", 2);
    }

    [Test]
    public async Task Test_ExecuteCustomReadCypher()
    {
        await CallToolAndAssertSuccessAsync("execute_custom_read_cypher", "{\"query\": \"MATCH (n) WHERE toString(n.id) STARTS WITH $workspaceIdPrefix RETURN count(n) AS nodeCount\"}", 3);
    }

    [Test]
    public async Task Test_GetProjectEntryPoints()
    {
        await CallToolAndAssertSuccessAsync("get_project_entry_points", "{\"projectName\": \"CodeExplorer\"}", 4);
    }

    [Test]
    public async Task Test_FindRefactoringOpportunities()
    {
        await CallToolAndAssertSuccessAsync("find_refactoring_opportunities", "{\"projectName\": \"CodeExplorer\", \"metricType\": \"all\"}", 5);
    }

    [Test]
    public async Task Test_GetProjectDependencies()
    {
        await CallToolAndAssertSuccessAsync("get_project_dependencies", "{}", 6);
    }
}

public class SseSession : IAsyncDisposable
{
    private readonly HttpClient _client;
    private readonly Task _readTask;
    private readonly CancellationTokenSource _cts;
    private readonly TaskCompletionSource<string> _endpointTcs = new();
    private readonly Dictionary<int, TaskCompletionSource<string>> _messageWaiters = new();
    private readonly object _lock = new();

    public SseSession(HttpClient client, string? workspacePath = null)
    {
        _client = client;
        _cts = new CancellationTokenSource();
        _readTask = ReadStreamAsync(workspacePath);
    }

    public Task<string> EndpointPathTask => _endpointTcs.Task;

    private async Task ReadStreamAsync(string? workspacePath)
    {
        try
        {
            var url = "http://127.0.0.1:8085/sse";
            if (!string.IsNullOrEmpty(workspacePath))
            {
                url += $"?workspacePath={Uri.EscapeDataString(workspacePath)}";
            }
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _cts.Token);
            using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
            using var reader = new StreamReader(stream);

            string? currentEvent = null;
            while (!_cts.Token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(_cts.Token);
                if (line == null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.StartsWith("event:"))
                {
                    currentEvent = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("data:"))
                {
                    var data = line["data:".Length..].Trim();
                    if (currentEvent == "endpoint")
                    {
                        _endpointTcs.TrySetResult(data);
                    }
                    else if (currentEvent == "message")
                    {
                        ProcessMessage(data);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _endpointTcs.TrySetException(ex);
        }
    }

    private void ProcessMessage(string data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.TryGetProperty("id", out var idElement) && idElement.TryGetInt32(out var id))
            {
                lock (_lock)
                {
                    if (_messageWaiters.TryGetValue(id, out var tcs))
                    {
                        tcs.TrySetResult(data);
                        _messageWaiters.Remove(id);
                    }
                    else
                    {
                        _messageWaiters[id] = new TaskCompletionSource<string>();
                        _messageWaiters[id].TrySetResult(data);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing SSE message JSON: {ex.Message}");
        }
    }

    public Task<string> WaitForNextMessageAsync(int id, TimeSpan timeout)
    {
        TaskCompletionSource<string> tcs;
        lock (_lock)
        {
            if (!_messageWaiters.TryGetValue(id, out tcs!))
            {
                tcs = new TaskCompletionSource<string>();
                _messageWaiters[id] = tcs;
            }
            else if (tcs.Task.IsCompleted)
            {
                return tcs.Task;
            }
        }
        return tcs.Task.WaitAsync(timeout);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try
        {
            await _readTask;
        }
        catch { }
        _cts.Dispose();
    }
}
