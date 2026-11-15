using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
public class McpIntegrationTests
{
    private const int TestPort = 8185;
    private static Task? _serverTask;
    private static HttpClient? _httpClient;
    private static string? _tempWorkspace;
    private static string? _sessionId;
    private static Stream? _sseStream;
    private static StreamReader? _sseReader;

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

        var dbPath = Path.Combine(_tempWorkspace, "test_graph.db");

        await using var client = new SqliteGraphClient(dbPath);

        var gatesDir = Path.Combine(_tempWorkspace, ".codeexplorer", "gates");
        Directory.CreateDirectory(gatesDir);
        await File.WriteAllTextAsync(Path.Combine(gatesDir, "sig-test-001.yaml"), """
            id: SIG-TEST-001
            category: Architectural
            platform: agnostic
            scope: Module
            dimension: Boundaries
            name: Test Gate
            query: "MATCH (ep:Endpoint) RETURN ep.path AS file, 1 AS line, ep.name AS symbol, 'target' AS target, 'msg' AS message"
            """);

        WorkspaceIndexer.AutoRegisterParsers();
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        // Start the server in a background thread
        _serverTask = Task.Run(() =>
            Program.Main(["mcp", "--port", TestPort.ToString(), "--db-path", dbPath, "--quiet"]));

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
            Assert.Fail(
                $"MCP Server failed to start on port {TestPort}. Last exception: {lastEx?.Message}\n{lastEx?.StackTrace}");
        }

        // Establish the SSE GET stream connection
        var sseUrl = $"http://127.0.0.1:{TestPort}/mcp/sse";

        if (!string.IsNullOrEmpty(_tempWorkspace))
        {
            sseUrl += $"?ws={Uri.EscapeDataString(_tempWorkspace)}";
        }

        var sseRequest = new HttpRequestMessage(HttpMethod.Get, sseUrl);
        var sseResponse = await _httpClient.SendAsync(sseRequest, HttpCompletionOption.ResponseHeadersRead);
        sseResponse.IsSuccessStatusCode.Should().BeTrue($"SSE GET returned {sseResponse.StatusCode}");

        _sseStream = await sseResponse.Content.ReadAsStreamAsync();
        _sseReader = new StreamReader(_sseStream);

        // Read the first event to get the session ID
        var eventLine = await _sseReader.ReadLineAsync();
        var dataLine = await _sseReader.ReadLineAsync();

        eventLine.Should().StartWith("event:");
        dataLine.Should().StartWith("data:");

        var relativeEndpoint = dataLine["data:".Length..].Trim();
        var queryIndex = relativeEndpoint.IndexOf('?');
        queryIndex.Should().BeGreaterThan(0);
        var queryString = relativeEndpoint[(queryIndex + 1)..];
        var queryParams = System.Web.HttpUtility.ParseQueryString(queryString);
        _sessionId = queryParams["sessionId"];
        _sessionId.Should().NotBeNullOrEmpty();

        // Console.WriteLine($"Established SSE session: {_sessionId}");

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

        responsePost.IsSuccessStatusCode.Should().BeTrue($"Initialize POST returned {responsePost.StatusCode}");

        var initResponseStr = await ReadNextSseResponseAsync();
        var initRoot = CypherValue.TryParseJsonText(initResponseStr);
        initRoot.GetIntProp("id").Should().Be(100);

        // Console.WriteLine("Initialization handshake completed successfully!");

        // Send initialized notification (notification has no response)
        var initializedJson = @"{
            ""jsonrpc"": ""2.0"",
            ""method"": ""notifications/initialized"",
            ""params"": {}
        }";
        responsePost = await PostMessageAsync(initializedJson);
        responsePost.IsSuccessStatusCode.Should().BeTrue();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _sseReader?.Dispose();
        _sseStream?.Dispose();
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
            catch (Exception)
            {
                // Console.WriteLine($"Server task finished with exception: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(_tempWorkspace))
        {

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
        var url = $"http://127.0.0.1:{TestPort}/mcp/message?sessionId={_sessionId}";
        if (!string.IsNullOrEmpty(_tempWorkspace))
        {
            url += $"&ws={Uri.EscapeDataString(_tempWorkspace)}";
        }
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await _httpClient!.SendAsync(request);
    }

    private async Task<string> ReadNextSseResponseAsync()
    {
        string? line;
        string? dataJson = null;
        while ((line = await _sseReader!.ReadLineAsync()) != null)
        {
            if (line.StartsWith("data:"))
            {
                dataJson = line["data:".Length..].Trim();
            }
            if (string.IsNullOrWhiteSpace(line) && dataJson != null)
            {
                return dataJson;
            }
        }
        throw new Exception("SSE stream closed before reading response.");
    }

    private async Task<string> CallToolAndAssertSuccessAsync(string toolName, string argumentsJson, int responseId)
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
        responsePost.IsSuccessStatusCode.Should().BeTrue($"Calling {toolName} returned POST status {responsePost.StatusCode}");

        var responseStr = await ReadNextSseResponseAsync();
        responseStr.Should().NotBeNull();

        var root = CypherValue.TryParseJsonText(responseStr);

        root.GetIntProp("id").Should().Be(responseId);

        // Assert no error field on jsonrpc response
        if (root.TryGetProperty("error", out var errorElement))
        {
            Assert.Fail($"Tool {toolName} returned error: {errorElement.ToJsonString()}");
        }

        // Verify result exists
        root.TryGetProperty("result", out var resultElement).Should().BeTrue();

        // Verify isError is false in result content
        if (resultElement.TryGetProperty("isError", out var isErrorElement))
        {
            if (isErrorElement.AsBool())
            {
                var errorText = "No details";

                if (resultElement.TryGetProperty("content", out var contentElement) &&
                    contentElement.Kind == CypherValueKind.Array && contentElement.AsArray().Count > 0)
                {
                    errorText = contentElement.AsArray()[0].GetStringProp("text");
                }

                Assert.Fail($"Tool {toolName} result marked with isError: true. Error details: {errorText}");
            }
        }

        if (resultElement.TryGetProperty("content", out var okContentElement) &&
            okContentElement.Kind == CypherValueKind.Array &&
            okContentElement.AsArray().Count > 0)
        {
            return okContentElement.AsArray()[0].GetStringProp("text") ?? string.Empty;
        }

        return string.Empty;
    }

    [Test]
    public async Task Test_GetTaxonomy()
    {
        await CallToolAndAssertSuccessAsync("get_taxonomy", "{}", 1);
        await CallToolAndAssertSuccessAsync("get_taxonomy", "{\"kind\": \"Workspace\"}", 7);
        await CallToolAndAssertSuccessAsync("get_taxonomy", "{\"kind\": \"Class\"}", 8);
        await CallToolAndAssertSuccessAsync("get_taxonomy", "{\"statusOnly\": true}", 18);
    }

    [Test]
    public async Task Test_GetArchitectureMap()
    {
        await CallToolAndAssertSuccessAsync("get_architecture_map", "{}", 2);
        await CallToolAndAssertSuccessAsync("get_architecture_map", "{\"view\": \"c1\"}", 9);
        var mermaid = await CallToolAndAssertSuccessAsync("get_architecture_map", "{\"format\": \"mermaid\", \"view\": \"c1\"}", 27);
        mermaid.Should().NotBeNullOrWhiteSpace();
    }

    [Test]
    public async Task Test_ExecuteCustomReadCypher()
    {
        await CallToolAndAssertSuccessAsync("execute_custom_read_cypher", "{\"query\": \"MATCH (n) RETURN count(n) AS nodeCount\"}", 3);
        await CallToolAndAssertSuccessAsync("execute_custom_read_cypher", "{\"listSaved\": true}", 28);
    }

    [Test]
    public async Task Test_ExecuteCustomReadCypher_WithDatabaseTypeAlias_Succeeds()
    {
        await CallToolAndAssertSuccessAsync("execute_custom_read_cypher", "{\"query\": \"MATCH (n) RETURN n.kind AS databaseType LIMIT 1\"}", 16);
    }

    [Test]
    public async Task Test_GetServiceContracts_AllModes()
    {
        await CallToolAndAssertSuccessAsync("get_service_contracts", "{\"serviceName\": \"CodeExplorer\"}", 4);
        await CallToolAndAssertSuccessAsync("get_service_contracts", "{\"serviceName\": \"CodeExplorer\", \"direction\": \"entry_points\"}", 29);
        await CallToolAndAssertSuccessAsync("get_service_contracts", "{\"serviceName\": \"CodeExplorer\", \"direction\": \"flow\"}", 30);
    }

    [Test]
    public async Task Test_GetProjectDependencies()
    {
        await CallToolAndAssertSuccessAsync("get_project_dependencies", "{}", 6);
    }

    [Test]
    public async Task Test_FindSymbol_SubstringAndHybrid()
    {
        var subRes = await CallToolAndAssertSuccessAsync("find_symbol", "{\"name\": \"OrdersController\", \"symbolType\": \"Class\"}", 31);
        subRes.Should().Contain("OrdersController");
        await CallToolAndAssertSuccessAsync("find_symbol", "{\"name\": \"orders\", \"matchMode\": \"hybrid\"}", 32);
    }

    [Test]
    public async Task Test_GetCallers_AndResolveImplementations()
    {
        await CallToolAndAssertSuccessAsync("get_callers", "{\"target\": \"OrdersController\"}", 33);
        await CallToolAndAssertSuccessAsync("get_callers", "{\"target\": \"OrdersController\", \"methodName\": \"chargeOrder\"}", 34);
    }

    [Test]
    public async Task Test_GetAffectedTests_ImpactAndCoverage()
    {
        await CallToolAndAssertSuccessAsync("get_affected_tests", "{\"filePath\": \"CodeExplorer/server.ts\"}", 35);
        await CallToolAndAssertSuccessAsync("get_affected_tests", "{\"coverageStatus\": \"all\", \"projectName\": \"CodeExplorer\"}", 36);
    }

    [Test]
    public async Task Test_GetFileOutline()
    {
        await CallToolAndAssertSuccessAsync("get_file_outline", "{\"filePath\": \"CodeExplorer/server.ts\"}", 10);
        var dirOutline = await CallToolAndAssertSuccessAsync("get_file_outline", "{\"filePath\": \"CodeExplorer\"}", 26);
        dirOutline.Should().Contain("CodeExplorer/server.ts");
        dirOutline.Should().Contain("OrdersController");
    }

    [Test]
    public async Task Test_InspectDataLineage()
    {
        await CallToolAndAssertSuccessAsync("inspect_data_lineage", "{\"tableName\": \"orders\"}", 17);
    }

    [Test]
    public async Task Test_ScanWorkspace()
    {
        await CallToolAndAssertSuccessAsync("scan_workspace", "{\"clear\": false}", 19);
    }

    [Test]
    public async Task Test_CheckSemanticGates()
    {
        await CallToolAndAssertSuccessAsync("check_semantic_gates", "{}", 21);
        await CallToolAndAssertSuccessAsync("check_semantic_gates", "{\"gateId\": \"SIG-TEST-001\"}", 22);
        var listed = await CallToolAndAssertSuccessAsync("check_semantic_gates", "{\"listOnly\": true}", 23);
        listed.Should().Contain("SIG-TEST-001");
        await CallToolAndAssertSuccessAsync("check_semantic_gates", "{\"anomalies\": \"all\", \"projectName\": \"CodeExplorer\"}", 5);
    }

    [Test]
    public async Task Test_ToolsList_OmitsDomainAndBoundedContextTools()
    {
        var payload = """
        {
            "jsonrpc": "2.0",
            "id": 24,
            "method": "tools/list",
            "params": {}
        }
        """;

        var responsePost = await PostMessageAsync(payload);
        responsePost.IsSuccessStatusCode.Should().BeTrue();

        var responseStr = await ReadNextSseResponseAsync();
        var root = CypherValue.TryParseJsonText(responseStr);
        root.GetIntProp("id").Should().Be(24);
        root.TryGetProperty("result", out var resultElement).Should().BeTrue();
        resultElement.TryGetProperty("tools", out var toolsElement).Should().BeTrue();

        var tools = toolsElement.AsArray();
        var toolNames = tools.Select(t => t.GetStringProp("name")).ToList();

        var expectedTools = new[]
        {
            "scan_workspace",
            "get_taxonomy",
            "execute_custom_read_cypher",
            "get_architecture_map",
            "get_project_dependencies",
            "get_service_contracts",
            "find_symbol",
            "get_file_outline",
            "fetch_code_snippets",
            "get_callers",
            "get_call_chain",
            "analyze_code_impact",
            "inspect_data_lineage",
            "get_affected_tests",
            "check_semantic_gates"
        };

        var removedTools = new[]
        {
            "get_architecture_overview",
            "get_bounded_contexts",
            "manage_domain",
            "assign_service_domain",
            "get_architecture_view",
            "export_architecture_diagram",
            "get_project_entry_points",
            "trace_cross_service_flow",
            "search_identifiers",
            "resolve_call_target",
            "get_test_coverage",
            "list_semantic_gates",
            "find_refactoring_opportunities",
            "get_workspace_status",
            "get_node_definition",
            "init_workspace",
            "clear_workspace_index",
            "ingest_graph_data",
            "list_project_queries",
            "execute_project_query",
            "save_project_query"
        };

        toolNames.Count.Should().Be(15, "consolidated MCP surface must stay within Glama TDQS 3-15 optimal window");
        toolNames.Should().BeEquivalentTo(expectedTools);
        foreach (var removed in removedTools)
        {
            toolNames.Should().NotContain(removed);
        }

        var archMapTool = tools.First(t => t.GetStringProp("name") == "get_architecture_map");
        archMapTool.ToJsonString().Should().NotContain("'domain'");

        using var doc = JsonDocument.Parse(responseStr);
        var jsonTools = doc.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().ToList();
        jsonTools.Count.Should().Be(15);

        var archMapJson = jsonTools.First(t => t.GetProperty("name").GetString() == "get_architecture_map");
        var viewProp = archMapJson.GetProperty("inputSchema").GetProperty("properties").GetProperty("view");
        viewProp.TryGetProperty("enum", out var viewEnum).Should().BeTrue("constrained parameters should emit JSON Schema enum for TDQS paramsWithEnums");
        viewEnum.GetArrayLength().Should().Be(8);

        foreach (var tool in jsonTools)
        {
            var name = tool.GetProperty("name").GetString()!;
            var description = tool.GetProperty("description").GetString()!;

            description.Should().NotBeNullOrWhiteSpace($"tool '{name}' must have a non-empty description");
            description.ToLowerInvariant().Trim().Should().NotBe(name.ToLowerInvariant(), $"tool '{name}' must not have a tautological description");
            description.Length.Should().BeGreaterThan(150, $"tool '{name}' must provide purpose, usage guidelines, and behavioral transparency");
            description.Should().ContainEquivalentOf("use ", $"tool '{name}' must include explicit usage guidelines and sibling differentiation");

            foreach (var removed in removedTools)
            {
                description.Should().NotContain(removed, $"tool '{name}' description must not cross-reference removed tool '{removed}'");
            }

            tool.TryGetProperty("annotations", out var annotations).Should().BeTrue($"tool '{name}' must declare MCP annotations for TDQS");
            var title = tool.TryGetProperty("title", out var topTitle) ? topTitle.GetString() : null;
            if (string.IsNullOrEmpty(title) && annotations.TryGetProperty("title", out var annTitle))
            {
                title = annTitle.GetString();
            }
            title.Should().NotBeNullOrWhiteSpace($"tool '{name}' must declare a human-readable title");
            title.Should().NotBe(name, $"tool '{name}' title must differ from its programmatic name");
            title!.Length.Should().BeGreaterThan(name.Length, $"tool '{name}' title must satisfy TDQS titleIsMeaningful (longer than name)");

            annotations.TryGetProperty("readOnlyHint", out var readOnlyProp).Should().BeTrue($"tool '{name}' must declare readOnlyHint");
            annotations.TryGetProperty("destructiveHint", out var destructiveProp).Should().BeTrue($"tool '{name}' must declare destructiveHint");
            annotations.TryGetProperty("idempotentHint", out var idempotentProp).Should().BeTrue($"tool '{name}' must declare idempotentHint");
            annotations.TryGetProperty("openWorldHint", out var openWorldProp).Should().BeTrue($"tool '{name}' must declare openWorldHint");

            var isWrite = name == "scan_workspace";
            readOnlyProp.GetBoolean().Should().Be(!isWrite, $"tool '{name}' readOnlyHint must match its mutation behavior");
            destructiveProp.GetBoolean().Should().BeFalse($"tool '{name}' destructiveHint must be false across all 15 non-destructive tools");
            idempotentProp.GetBoolean().Should().BeTrue($"tool '{name}' idempotentHint must be true");
            openWorldProp.GetBoolean().Should().BeFalse($"tool '{name}' openWorldHint must be false (closed local workspace domain)");

            if (tool.TryGetProperty("inputSchema", out var inputSchema) &&
                inputSchema.TryGetProperty("properties", out var properties) &&
                properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in properties.EnumerateObject())
                {
                    prop.Value.TryGetProperty("description", out var paramDescProp).Should().BeTrue($"parameter '{prop.Name}' on tool '{name}' must declare a description");
                    paramDescProp.GetString().Should().NotBeNullOrWhiteSpace($"parameter '{prop.Name}' on tool '{name}' must have a non-empty schema description (100% coverage)");
                    foreach (var removed in removedTools)
                    {
                        paramDescProp.GetString()!.Should().NotContain(removed, $"parameter '{prop.Name}' on tool '{name}' must not reference removed tool '{removed}'");
                    }
                }
            }
        }
    }

    [Test]
    public async Task Test_InspectDataLineage_AtsProject_Performance()
    {
        var atsDbPath = Environment.GetEnvironmentVariable("ATS_GRAPH_DB_PATH");
        if (string.IsNullOrWhiteSpace(atsDbPath) || !File.Exists(atsDbPath))
        {
            Assert.Ignore("ATS_GRAPH_DB_PATH environment variable not set or file not found.");
            return;
        }

        await using var client = new SqliteGraphClient(atsDbPath);

        var repo = new Core.Mcp.CodeExplorerRepository(client);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var resultJson = await repo.InspectDataLineageAsync("campaigns");
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeLessThan(2000, $"Query took too long: {sw.ElapsedMilliseconds}ms (expected < 2000ms)");
        resultJson.Should().Contain("campaigns");
    }
}
