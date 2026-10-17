using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Options;
using CodeExplorer.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
public class ApiServerTests
{
    private string _tempWorkspace = null!;
    private string _dbPath = null!;
    private SqliteGraphClient _client = null!;
    private WebApplication _app = null!;
    private HttpClient _httpClient = null!;
    private int _port;
    private string _httpBaseUrl = null!;
    private string _wsBaseUrl = null!;
    private WebSocketServerHandler _wsHandler = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ce_api_tests_" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
        var projDir = Path.Combine(_tempWorkspace, "SampleProject").Replace('\\', '/');
        Directory.CreateDirectory(projDir);
        await File.WriteAllTextAsync(Path.Combine(projDir, "package.json"), "{\"name\":\"SampleProject\",\"version\":\"1.0.0\"}");
        var fileCode = @"
        import { Controller, Get, Post } from '@nestjs/common';
        @Controller('orders')
        export class OrdersController {
            @Get('list')
            async getOrders() { return []; }
            @Post('charge')
            async chargeOrder() { return true; }
        }";
        await File.WriteAllTextAsync(Path.Combine(projDir, "orders.controller.ts"), fileCode);

        _dbPath = Path.Combine(_tempWorkspace, "test_api_graph.db").Replace('\\', '/');
        _client = new SqliteGraphClient(_dbPath);

        WorkspaceIndexer.AutoRegisterParsers();
        var indexer = new WorkspaceIndexer(_client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        var serveOpts = new ServeOptions
        {
            Host = "127.0.0.1",
            Port = 0,
            IdleTimeoutSeconds = 0,
            Quiet = true
        };

        _app = Program.CreateWebApplication(serveOpts, _tempWorkspace, _client);
        await _app.StartAsync();

        var serverAddresses = _app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();
        var boundAddress = serverAddresses!.Addresses.First();
        var boundUri = new Uri(boundAddress);
        _port = boundUri.Port;
        _httpBaseUrl = $"http://127.0.0.1:{_port}";
        _wsBaseUrl = $"ws://127.0.0.1:{_port}/ws";

        _wsHandler = _app.Services.GetRequiredService<WebSocketServerHandler>();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        try
        {
            _httpClient.Dispose();
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
            }
            if (_client != null)
            {
                await _client.DisposeAsync();
            }
            if (Directory.Exists(_tempWorkspace))
            {
                Directory.Delete(_tempWorkspace, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    // ==========================================
    // REST API ENDPOINTS TESTS
    // ==========================================

    [Test]
    public async Task RestApi_Root_ReturnsAppMetadata()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        root.GetProperty("service").GetString().Should().Be("CodeExplorer API Server");

        (root.TryGetProperty("version", out var version) && !string.IsNullOrEmpty(version.GetString())).Should().BeTrue();
        root.GetProperty("wsEndpoint").GetString().Should().Be("/ws");
        root.GetProperty("workspace").GetString().Should().Be(_tempWorkspace);
    }

    [Test]
    public async Task RestApi_Status_ReturnsNodeAndEdgeCounts()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/status");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("ok");
        root.GetProperty("workspace").GetString().Should().Be(_tempWorkspace);
        root.TryGetProperty("nodes", out _).Should().BeTrue();
        root.TryGetProperty("edges", out _).Should().BeTrue();
    }

    [Test]
    public async Task RestApi_Architecture_ReturnsValidGraph()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/architecture");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        (root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array).Should().BeTrue();
        (root.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Array).Should().BeTrue();
        nodes.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task RestApi_Architecture_WithProjectFilter_ReturnsFilteredGraph()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/architecture?project=SampleProject");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        (root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array).Should().BeTrue();
        nodes.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task RestApi_Projects_ReturnsProjectList()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/projects");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().BeGreaterThan(0);

        var first = doc.RootElement[0];
        first.GetString().Should().Be("SampleProject");
    }

    [Test]
    public async Task RestApi_Dependencies_ReturnsNeighborhoodGraph()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/dependencies?project=SampleProject");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;

        (root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array).Should().BeTrue();
        (root.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Array).Should().BeTrue();
        root.TryGetProperty("metadata", out _).Should().BeTrue();
    }

    [Test]
    public async Task RestApi_InvalidRoute_Returns404NotFound()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/non_existent_route");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task RestApi_WsEndpointHttp_Returns400BadRequest()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/ws");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().Contain("WebSocket connection expected");
    }

    [Test]
    public async Task RestApi_GatesSaveAndTest_Succeeds()
    {
        // 1. Test Cypher query via POST /api/gates/test
        var testReq = new Core.Protocol.TestGateRequestDto
        {
            Query = "MATCH (p:Project) RETURN p.name AS symbol, '' AS target, 'test project' AS message, 'Sample.csproj' AS file, 1 AS line"
        };
        var testContent = new StringContent(JsonSerializer.Serialize(testReq), Encoding.UTF8, "application/json");
        var testRes = await _httpClient.PostAsync($"{_httpBaseUrl}/api/gates/test", testContent);
        testRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var testJson = await testRes.Content.ReadAsStringAsync();
        var testResult = JsonSerializer.Deserialize<Core.Protocol.TestGateResponseDto>(testJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        testResult.Should().NotBeNull();
        testResult!.Success.Should().BeTrue();
        testResult.ProjectedColumns.Should().Contain("symbol");
        testResult.ProjectedColumns.Should().Contain("file");
        testResult.MissingColumns.Should().BeEmpty();

        // 2. Save gate via POST /api/gates/save
        var saveReq = new Core.Protocol.SaveGateRequestDto
        {
            Gate = new Core.Protocol.GateItemDto
            {
                Id = "SIG-TEST-API-001",
                Name = "API Test Gate",
                Category = "Architectural",
                Platform = "agnostic",
                Scope = "Project",
                Dimension = "Rules",
                Severity = "Warning",
                Description = "Testing gate save API endpoint.",
                Remediation = "None",
                Query = testReq.Query,
                RatchetEnabled = false
            }
        };
        var saveContent = new StringContent(JsonSerializer.Serialize(saveReq), Encoding.UTF8, "application/json");
        var saveRes = await _httpClient.PostAsync($"{_httpBaseUrl}/api/gates/save", saveContent);
        saveRes.StatusCode.Should().Be(HttpStatusCode.OK);

        var saveJson = await saveRes.Content.ReadAsStringAsync();
        var saveResult = JsonSerializer.Deserialize<Core.Protocol.SaveGateResponseDto>(saveJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        saveResult.Should().NotBeNull();
        saveResult!.Success.Should().BeTrue();
        saveResult.FilePath.Should().NotBeNullOrEmpty();
        saveResult.Gate?.Id.Should().Be("SIG-TEST-API-001");
    }

    // ==========================================
    // WEBSOCKET PROTOCOL & VALIDATION TESTS
    // ==========================================

    [Test]
    public async Task WebSocket_HandshakeAndPing_Succeeds()
    {
        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);
        ws.State.Should().Be(WebSocketState.Open);

        var hsReq = new WsEnvelope<HandshakeRequestDto>
        {
            Type = WsMessageTypes.HandshakeRequest,
            RequestId = "hs-1",
            Payload = new HandshakeRequestDto { ClientName = "test-suite", ClientVersion = "1.0.0" }
        };
        await SendJsonAsync(ws, hsReq);
        var hsResp = await ReceiveJsonAsync<HandshakeResponseDto>(ws);

        hsResp.Should().NotBeNull();
        hsResp.Type.Should().Be(WsMessageTypes.HandshakeResponse);
        hsResp.RequestId.Should().Be("hs-1");
        hsResp.Payload?.Capabilities.Should().Contain("architecture");
        hsResp.Payload?.Capabilities.Should().Contain("cypher");
        hsResp.Payload?.TotalNodes.Should().BeGreaterThan(0);

        var pingReq = new WsEnvelope<PingRequestDto>
        {
            Type = WsMessageTypes.PingRequest,
            RequestId = "ping-1",
            Payload = new PingRequestDto { Timestamp = 999888777 }
        };
        await SendJsonAsync(ws, pingReq);
        var pingResp = await ReceiveJsonAsync<PongResponseDto>(ws);

        pingResp.Should().NotBeNull();
        pingResp.Type.Should().Be(WsMessageTypes.PongResponse);
        pingResp.Payload?.ClientTimestamp.Should().Be(999888777);
        pingResp.Payload?.ServerTimestamp.Should().BeGreaterThan(0);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_ExecuteCypher_ValidQuery_ReturnsResults()
    {
        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var req = new WsEnvelope<ExecuteCypherRequestDto>
        {
            Type = WsMessageTypes.ExecuteCypherRequest,
            RequestId = "cypher-1",
            Payload = new ExecuteCypherRequestDto
            {
                Query = "MATCH (p:Project) RETURN p.name AS name, p.language AS language"
            }
        };

        await SendJsonAsync(ws, req);
        var resp = await ReceiveJsonAsync<QueryResponseDto>(ws);

        resp.Should().NotBeNull();
        resp.Type.Should().Be(WsMessageTypes.QueryResponse);
        resp.Payload?.Success.Should().BeTrue();
        resp.Payload?.RawJson.Should().Contain("SampleProject");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_ExecuteCypher_EmptyQuery_ReturnsInvalidQueryError()
    {
        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var req = new WsEnvelope<ExecuteCypherRequestDto>
        {
            Type = WsMessageTypes.ExecuteCypherRequest,
            RequestId = "cypher-empty",
            Payload = new ExecuteCypherRequestDto { Query = "   " }
        };

        await SendJsonAsync(ws, req);
        var resp = await ReceiveJsonAsync<ErrorResponseDto>(ws);

        resp.Should().NotBeNull();
        resp.Type.Should().Be(WsMessageTypes.ErrorResponse);
        resp.Payload?.Code.Should().Be("INVALID_QUERY");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_ExecuteCypher_MalformedQuery_ReturnsExecutionError()
    {
        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var req = new WsEnvelope<ExecuteCypherRequestDto>
        {
            Type = WsMessageTypes.ExecuteCypherRequest,
            RequestId = "cypher-malformed",
            Payload = new ExecuteCypherRequestDto { Query = "THIS IS NOT VALID CYPHER SYNTAX !!!" }
        };

        await SendJsonAsync(ws, req);
        var resp = await ReceiveJsonAsync<ErrorResponseDto>(ws);

        resp.Should().NotBeNull();
        resp.Type.Should().Be(WsMessageTypes.ErrorResponse);
        resp.Payload?.Code.Should().Be("EXECUTION_ERROR");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_MalformedJson_ReturnsParseError()
    {
        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var badJsonBytes = Encoding.UTF8.GetBytes("{ this is not: valid json }");

        await ws.SendAsync(new ArraySegment<byte>(badJsonBytes), WebSocketMessageType.Text, true,
            CancellationToken.None);

        var resp = await ReceiveJsonAsync<ErrorResponseDto>(ws);
        resp.Should().NotBeNull();
        resp.Type.Should().Be(WsMessageTypes.ErrorResponse);
        resp.Payload?.Code.Should().Be("PARSE_ERROR");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_MissingEnvelopeType_ReturnsInvalidMessageError()
    {
        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var noTypeJson = Encoding.UTF8.GetBytes("{\"requestId\":\"no-type-1\"}");

        await ws.SendAsync(new ArraySegment<byte>(noTypeJson), WebSocketMessageType.Text, true,
            CancellationToken.None);

        var resp = await ReceiveJsonAsync<ErrorResponseDto>(ws);
        resp.Should().NotBeNull();
        resp.Type.Should().Be(WsMessageTypes.ErrorResponse);
        resp.Payload?.Code.Should().Be("INVALID_MESSAGE");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_UnknownMessageType_ReturnsUnknownTypeError()
    {
        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var unknownMsg = new WsEnvelope<object>
        {
            Type = "UNSUPPORTED_RANDOM_ACTION", RequestId = "unknown-1", Payload = new { foo = "bar" }
        };
        await SendJsonAsync(ws, unknownMsg);

        var resp = await ReceiveJsonAsync<ErrorResponseDto>(ws);
        resp.Should().NotBeNull();
        resp.Type.Should().Be(WsMessageTypes.ErrorResponse);
        resp.Payload?.Code.Should().Be("UNKNOWN_TYPE");

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_GetCallChainAndImpact_Succeeds()
    {
        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        // GET_CALL_CHAIN
        var callReq = new WsEnvelope<GetCallChainRequestDto>
        {
            Type = WsMessageTypes.GetCallChainRequest,
            RequestId = "call-1",
            Payload = new GetCallChainRequestDto
            {
                FromSymbol = "OrdersController", ToSymbol = "chargeOrder", MaxDepth = 3
            }
        };
        await SendJsonAsync(ws, callReq);
        var callResp = await ReceiveJsonAsync<QueryResponseDto>(ws);
        callResp.Should().NotBeNull();
        callResp.Type.Should().Be(WsMessageTypes.QueryResponse);
        callResp.Payload?.Success.Should().BeTrue();

        // GET_IMPACT
        var impactReq = new WsEnvelope<GetImpactRequestDto>
        {
            Type = WsMessageTypes.GetImpactRequest,
            RequestId = "impact-1",
            Payload = new GetImpactRequestDto { SymbolName = "OrdersController" }
        };
        await SendJsonAsync(ws, impactReq);
        var impactResp = await ReceiveJsonAsync<QueryResponseDto>(ws);
        impactResp.Should().NotBeNull();
        impactResp.Type.Should().Be(WsMessageTypes.QueryResponse);
        impactResp.Payload?.Success.Should().BeTrue();

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    // ==========================================
    // CONCURRENCY & ROBUSTNESS TESTS
    // ==========================================

    [Test]
    public async Task WebSocket_ConcurrentSendAsync_NoCollisionOrCrash()
    {
        // This test ensures that the server can handle multiple concurrent requests
        // over the same connection without throwing:
        // "System.InvalidOperationException: There is already one outstanding 'SendAsync' call for this WebSocket instance"

        using var ws = new ClientWebSocket();

        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        const int requestCount = 20;
        var receiveLock = new SemaphoreSlim(1, 1);
        var receivedResponses = new List<string>();

        // Start background receiver
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var receiveTask = Task.Run(async () =>
        {
            var buffer = new byte[1024 * 32];

            using var ms = new MemoryStream();

            while (!cts.Token.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;

                try
                {
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close) return;

                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException)
                {
                    break;
                }

                if (ms.Length > 0)
                {
                    var text = Encoding.UTF8.GetString(ms.ToArray());
                    await receiveLock.WaitAsync();

                    try
                    {
                        receivedResponses.Add(text);

                        if (receivedResponses.Count >= requestCount)
                        {
                            break;
                        }
                    }
                    finally
                    {
                        receiveLock.Release();
                    }
                }
            }
        }, cts.Token);

        // Send 20 requests concurrently over the SAME socket instance
        var clientSendLock = new SemaphoreSlim(1, 1);

        var sendTasks = Enumerable.Range(0, requestCount).Select(async i =>
        {
            var reqId = $"concurrent-{i}";

            var envelope = new WsEnvelope<PingRequestDto>
            {
                Type = WsMessageTypes.PingRequest,
                RequestId = reqId,
                Payload = new PingRequestDto { Timestamp = i }
            };

            var json = JsonSerializer.Serialize(envelope,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var bytes = Encoding.UTF8.GetBytes(json);

            // Client-side send also uses a lock to avoid client-side concurrency exception
            await clientSendLock.WaitAsync();

            try
            {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true,
                    CancellationToken.None);
            }
            finally
            {
                clientSendLock.Release();
            }
        });

        await Task.WhenAll(sendTasks);

        // Wait for all responses to arrive
        await receiveTask;

        receivedResponses.Count.Should().Be(requestCount, $"Expected {requestCount} responses without server send collision, received {receivedResponses.Count}");

        foreach (var respJson in receivedResponses)
        {
            using var doc = JsonDocument.Parse(respJson);

            doc.RootElement.GetProperty("type").GetString().Should().Be(WsMessageTypes.PongResponse);
        }

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_AbruptClientDisconnect_DoesNotCrashServer()
    {
        // Connect a client and abruptly abort it without clean close handshake

        using var clientAbrupt = new ClientWebSocket();

        await clientAbrupt.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var prePing = new WsEnvelope<PingRequestDto>
        {
            Type = WsMessageTypes.PingRequest,
            RequestId = "pre-abort-ping",
            Payload = new PingRequestDto { Timestamp = 1 }
        };
        await SendJsonAsync(clientAbrupt, prePing);
        var preResp = await ReceiveJsonAsync<PongResponseDto>(clientAbrupt);
        preResp.Payload?.ClientTimestamp.Should().Be(1);
        _wsHandler.ActiveSessionsCount.Should().BeGreaterThan(0);

        // Abruptly abort connection
        clientAbrupt.Abort();

        // Allow handler loop to process disconnect
        await Task.Delay(200);

        // Now verify that a NEW client can connect and operate normally

        using var clientHealthy = new ClientWebSocket();

        await clientHealthy.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);
        clientHealthy.State.Should().Be(WebSocketState.Open);

        var ping = new WsEnvelope<PingRequestDto>
        {
            Type = WsMessageTypes.PingRequest,
            RequestId = "post-abort-ping",
            Payload = new PingRequestDto { Timestamp = 42 }
        };
        await SendJsonAsync(clientHealthy, ping);
        var resp = await ReceiveJsonAsync<PongResponseDto>(clientHealthy);

        resp.Should().NotBeNull();
        resp.Payload?.ClientTimestamp.Should().Be(42);

        await clientHealthy.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_MultipleSimultaneousClients_TracksActiveSessionCount()
    {
        using var client1 = new ClientWebSocket();

        using var client2 = new ClientWebSocket();

        using var client3 = new ClientWebSocket();

        await client1.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);
        await client2.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);
        await client3.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        for (int i = 0; i < 20 && _wsHandler.ActiveSessionsCount < 3; i++)
        {
            await Task.Delay(25);
        }

        _wsHandler.ActiveSessionsCount.Should().BeGreaterThanOrEqualTo(3);

        // Ping from all three
        var tasks = new[] { client1, client2, client3 }.Select(async (ws, idx) =>
        {
            var req = new WsEnvelope<PingRequestDto>
            {
                Type = WsMessageTypes.PingRequest,
                RequestId = $"multi-{idx}",
                Payload = new PingRequestDto { Timestamp = idx }
            };
            await SendJsonAsync(ws, req);
            var resp = await ReceiveJsonAsync<PongResponseDto>(ws);
            resp.Payload?.ClientTimestamp.Should().Be(idx);
        });

        await Task.WhenAll(tasks);

        // Broadcast test
        await _wsHandler.BroadcastAsync("TEST_BROADCAST", new { testMessage = "broadcast-all" });

        // Read broadcast from client1
        var broadcastEnvelope = await ReceiveJsonAsync<JsonElement>(client1);
        broadcastEnvelope.Type.Should().Be("TEST_BROADCAST");

        // Close client 3
        await client3.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
        await Task.Delay(150);

        // Close remaining
        await client1.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
        await client2.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    // ==========================================
    // HELPER METHODS
    // ==========================================

    private static async Task SendJsonAsync<T>(ClientWebSocket ws, T data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<WsEnvelope<T>> ReceiveJsonAsync<T>(ClientWebSocket ws)
    {
        var buffer = new byte[1024 * 32];

        using var ms = new MemoryStream();

        WebSocketReceiveResult result;

        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(ms.ToArray());

        return JsonSerializer.Deserialize<WsEnvelope<T>>(json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true
            })!;
    }
}
