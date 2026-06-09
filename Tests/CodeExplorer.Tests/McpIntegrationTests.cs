using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
[Explicit("Runs integration tests against a real Memgraph database.")]
public class McpIntegrationTests
{
    private const int TestPort = 8185;
    private static Task? _serverTask;
    private static HttpClient? _httpClient;
    private static string? _tempWorkspace;
    private static string? _resolvedBoltUrl;

    public static string GetBoltUrl()
    {
        if (_resolvedBoltUrl != null) return _resolvedBoltUrl;

        var envUrl = Environment.GetEnvironmentVariable("BOLT_URL");
        if (!string.IsNullOrEmpty(envUrl))
        {
            _resolvedBoltUrl = envUrl;
            return envUrl;
        }

        // Check if localhost:7687 is listening
        try
        {
            using var tcp = new System.Net.Sockets.TcpClient();
            var result = tcp.BeginConnect("127.0.0.1", 7687, null, null);
            var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
            if (success)
            {
                tcp.EndConnect(result);
                _resolvedBoltUrl = "bolt://127.0.0.1:7687";
                return _resolvedBoltUrl;
            }
        }
        catch {}

        // If not, try to find WSL IP and check if 7687 is listening there
        try
        {
            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = "-d docker-desktop -e sh -c \"ip address || ifconfig || cat /proc/net/fib_trie\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            var match = System.Text.RegularExpressions.Regex.Match(output, @"inet\s+(172\.\d+\.\d+\.\d+)");
            if (match.Success)
            {
                var wslIp = match.Groups[1].Value;
                using var tcp = new System.Net.Sockets.TcpClient();
                var result = tcp.BeginConnect(wslIp, 7687, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                if (success)
                {
                    tcp.EndConnect(result);
                    _resolvedBoltUrl = $"bolt://{wslIp}:7687";
                    return _resolvedBoltUrl;
                }
            }
        }
        catch {}

        _resolvedBoltUrl = "bolt://127.0.0.1:7687";
        return _resolvedBoltUrl;
    }

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

        var boltUrl = GetBoltUrl();
        await using var client = new MemgraphClient(boltUrl, "", "");
        WorkspaceIndexer.Register(new TypeScriptParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        // Start the server in a background thread
        _serverTask = Task.Run(() => Program.Main(["mcp", "--port", TestPort.ToString(), "--bolt-url", boltUrl]));

        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Wait for the server to become available
        var available = false;
        Exception? lastEx = null;
        for (var i = 0; i < 40; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://127.0.0.1:{TestPort}/");
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
            Assert.Fail($"MCP Server failed to start on port {TestPort}. Last exception: {lastEx?.Message}\n{lastEx?.StackTrace}");
        }

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

        var initResponseStr = await responsePost.Content.ReadAsStringAsync();
        Console.WriteLine($"[SSE DEBUG] Response body: {initResponseStr}");
        var json = ExtractJsonFromSse(initResponseStr);
        using var doc = JsonDocument.Parse(json);
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

        if (!string.IsNullOrEmpty(_tempWorkspace))
        {
            try
            {
                var boltUrl = GetBoltUrl();
                await using var client = new MemgraphClient(boltUrl, "", "");
                await client.ClearWorkspaceAsync(_tempWorkspace);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cleaning up Memgraph workspace in TearDown: {ex.Message}");
            }

            if (Directory.Exists(_tempWorkspace))
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
    }

    private async Task<HttpResponseMessage> PostMessageAsync(string jsonPayload)
    {
        var url = $"http://127.0.0.1:{TestPort}/mcp";
        if (!string.IsNullOrEmpty(_tempWorkspace))
        {
            url += $"?ws={Uri.EscapeDataString(_tempWorkspace)}";
        }
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json-seq"));
        return await _httpClient!.SendAsync(request);
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

        var responseStr = await responsePost.Content.ReadAsStringAsync();
        Assert.That(responseStr, Is.Not.Null);
        var json = ExtractJsonFromSse(responseStr);
        using var doc = JsonDocument.Parse(json);
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

    private static string ExtractJsonFromSse(string responseStr)
    {
        if (string.IsNullOrEmpty(responseStr))
        {
            throw new Exception("Response is empty.");
        }
        if (responseStr.TrimStart().StartsWith("{"))
        {
            return responseStr;
        }
        using var reader = new StringReader(responseStr);
        string? line;
        string? currentEvent = null;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.StartsWith("event:"))
            {
                currentEvent = line["event:".Length..].Trim();
            }
            else if (line.StartsWith("data:"))
            {
                var data = line["data:".Length..].Trim();
                if (currentEvent == "message" || currentEvent == null)
                {
                    return data;
                }
            }
        }
        throw new Exception($"Could not find data event in response: {responseStr}");
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

    [Test]
    public async Task Test_GetNodeDefinition()
    {
        await CallToolAndAssertSuccessAsync("get_node_definition", "{\"kind\": \"Workspace\"}", 7);
        await CallToolAndAssertSuccessAsync("get_node_definition", "{\"kind\": \"Class\"}", 8);
    }

    [Test]
    public async Task Test_RestIndexingBackground_Lifecycle()
    {
        // 1. Create a dedicated temporary workspace for this test
        var localTempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_bg_test_" + Guid.NewGuid()).Replace('\\', '/');
        var projDir = Path.Combine(localTempWorkspace, "CodeExplorer").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "package.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(projDir, "dummy.ts"), "export class Dummy {}");

        try
        {
            // 2. Get initial status (should be Idle or previous run's final state)
            var response = await _httpClient!.GetAsync($"http://127.0.0.1:{TestPort}/api/workspaces/index/status");
            Assert.That(response.IsSuccessStatusCode, Is.True);
            var statusStr = await response.Content.ReadAsStringAsync();
            using (var doc = JsonDocument.Parse(statusStr))
            {
                var state = doc.RootElement.GetProperty("state").GetString();
                Assert.That(state, Is.Not.Null);
            }

            // 3. Start indexing in background
            var startRequest = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{TestPort}/api/workspaces/index");
            var startPayload = JsonSerializer.Serialize(new { dir = localTempWorkspace, clear = true });
            startRequest.Content = new StringContent(startPayload, Encoding.UTF8, "application/json");
            var startResponse = await _httpClient.SendAsync(startRequest);
            Assert.That(startResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.Accepted));

            // 4. Poll status until it is Completed
            string finalState = "Running";
            for (int i = 0; i < 20; i++)
            {
                await Task.Delay(200);
                var statusResp = await _httpClient.GetAsync($"http://127.0.0.1:{TestPort}/api/workspaces/index/status");
                Assert.That(statusResp.IsSuccessStatusCode, Is.True);
                var currentStatus = await statusResp.Content.ReadAsStringAsync();
                using var currentDoc = JsonDocument.Parse(currentStatus);
                finalState = currentDoc.RootElement.GetProperty("state").GetString() ?? "Running";
                if (finalState != "Running")
                {
                    break;
                }
            }
            Assert.That(finalState, Is.EqualTo("Completed"));

            // 5. Test Stop on a fresh run
            startRequest = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{TestPort}/api/workspaces/index/start");
            startRequest.Content = new StringContent(startPayload, Encoding.UTF8, "application/json");
            startResponse = await _httpClient.SendAsync(startRequest);
            
            if (startResponse.StatusCode == System.Net.HttpStatusCode.Accepted)
            {
                // Stop it immediately
                var stopResponse = await _httpClient.PostAsync($"http://127.0.0.1:{TestPort}/api/workspaces/index/stop", null);
                Assert.That(stopResponse.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK).Or.EqualTo(System.Net.HttpStatusCode.BadRequest));
            }
        }
        finally
        {
            if (Directory.Exists(localTempWorkspace))
            {
                try { Directory.Delete(localTempWorkspace, true); } catch {}
            }
        }
    }

    [Test]
    public async Task Test_RestNodeDefinition_ReturnsSuccess()
    {
        var response = await _httpClient!.GetAsync($"http://127.0.0.1:{TestPort}/api/workspaces/node-definition?kind=Workspace");
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(response.IsSuccessStatusCode, Is.True, $"Failed with status {response.StatusCode} and body: {content}");
        using var doc = JsonDocument.Parse(content);
        var definition = doc.RootElement.GetProperty("definition").GetString();
        Assert.That(definition, Does.Contain("### Kind: Workspace"));
    }

    [Test]
    public async Task Test_RestTaxonomy_ReturnsSuccess()
    {
        var response = await _httpClient!.GetAsync($"http://127.0.0.1:{TestPort}/api/workspaces/taxonomy?workspacePath={Uri.EscapeDataString(_tempWorkspace!)}");
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(response.IsSuccessStatusCode, Is.True, $"Taxonomy request failed with status {response.StatusCode} and body: {content}");
    }

    [Test]
    public async Task Test_RestNodeDefinition_ReturnsBadRequestForEmpty()
    {
        var response = await _httpClient!.GetAsync($"http://127.0.0.1:{TestPort}/api/workspaces/node-definition?kind=");
        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Test_RestNodeDefinition_ReturnsNotFoundForUnknown()
    {
        var response = await _httpClient!.GetAsync($"http://127.0.0.1:{TestPort}/api/workspaces/node-definition?kind=UnknownKindXYZ");
        Assert.That(response.StatusCode, Is.EqualTo(System.Net.HttpStatusCode.NotFound));
    }
}
