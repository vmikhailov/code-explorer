using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Options;
using CodeExplorer.Parser.TypeScript;
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

        WorkspaceIndexer.Register(new TypeScriptParser());
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.That(root.GetProperty("service").GetString(), Is.EqualTo("CodeExplorer API Server"));
        Assert.That(root.TryGetProperty("version", out var version) && !string.IsNullOrEmpty(version.GetString()), Is.True);
        Assert.That(root.GetProperty("wsEndpoint").GetString(), Is.EqualTo("/ws"));
        Assert.That(root.GetProperty("workspace").GetString(), Is.EqualTo(_tempWorkspace));
    }

    [Test]
    public async Task RestApi_Status_ReturnsNodeAndEdgeCounts()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/status");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("ok"));
        Assert.That(root.GetProperty("workspace").GetString(), Is.EqualTo(_tempWorkspace));
        Assert.That(root.TryGetProperty("nodes", out _), Is.True);
        Assert.That(root.TryGetProperty("edges", out _), Is.True);
    }

    [Test]
    public async Task RestApi_Architecture_ReturnsValidGraph()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/architecture");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.That(root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array, Is.True);
        Assert.That(root.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Array, Is.True);
        Assert.That(nodes.GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    public async Task RestApi_Architecture_WithProjectFilter_ReturnsFilteredGraph()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/architecture?project=SampleProject");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.That(root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array, Is.True);
        Assert.That(nodes.GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    public async Task RestApi_Projects_ReturnsProjectList()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/projects");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(doc.RootElement.GetArrayLength(), Is.GreaterThan(0));

        var first = doc.RootElement[0];
        Assert.That(first.GetString(), Is.EqualTo("SampleProject"));
    }

    [Test]
    public async Task RestApi_Dependencies_ReturnsNeighborhoodGraph()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/dependencies?project=SampleProject");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.That(root.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array, Is.True);
        Assert.That(root.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Array, Is.True);
        Assert.That(root.TryGetProperty("metadata", out _), Is.True);
    }

    [Test]
    public async Task RestApi_InvalidRoute_Returns404NotFound()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/api/non_existent_route");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task RestApi_WsEndpointHttp_Returns400BadRequest()
    {
        var response = await _httpClient.GetAsync($"{_httpBaseUrl}/ws");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var text = await response.Content.ReadAsStringAsync();
        Assert.That(text, Does.Contain("WebSocket connection expected"));
    }

    // ==========================================
    // WEBSOCKET PROTOCOL & VALIDATION TESTS
    // ==========================================

    [Test]
    public async Task WebSocket_HandshakeAndPing_Succeeds()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);
        Assert.That(ws.State, Is.EqualTo(WebSocketState.Open));

        var hsReq = new WsEnvelope<HandshakeRequestDto>
        {
            Type = WsMessageTypes.HandshakeRequest,
            RequestId = "hs-1",
            Payload = new HandshakeRequestDto { ClientName = "test-suite", ClientVersion = "1.0.0" }
        };
        await SendJsonAsync(ws, hsReq);
        var hsResp = await ReceiveJsonAsync<HandshakeResponseDto>(ws);

        Assert.That(hsResp, Is.Not.Null);
        Assert.That(hsResp.Type, Is.EqualTo(WsMessageTypes.HandshakeResponse));
        Assert.That(hsResp.RequestId, Is.EqualTo("hs-1"));
        Assert.That(hsResp.Payload?.Capabilities, Does.Contain("architecture"));
        Assert.That(hsResp.Payload?.Capabilities, Does.Contain("cypher"));
        Assert.That(hsResp.Payload?.TotalNodes, Is.GreaterThan(0));

        var pingReq = new WsEnvelope<PingRequestDto>
        {
            Type = WsMessageTypes.PingRequest,
            RequestId = "ping-1",
            Payload = new PingRequestDto { Timestamp = 999888777 }
        };
        await SendJsonAsync(ws, pingReq);
        var pingResp = await ReceiveJsonAsync<PongResponseDto>(ws);

        Assert.That(pingResp, Is.Not.Null);
        Assert.That(pingResp.Type, Is.EqualTo(WsMessageTypes.PongResponse));
        Assert.That(pingResp.Payload?.ClientTimestamp, Is.EqualTo(999888777));
        Assert.That(pingResp.Payload?.ServerTimestamp, Is.GreaterThan(0));

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

        Assert.That(resp, Is.Not.Null);
        Assert.That(resp.Type, Is.EqualTo(WsMessageTypes.QueryResponse));
        Assert.That(resp.Payload?.Success, Is.True);
        Assert.That(resp.Payload?.RawJson, Does.Contain("SampleProject"));

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

        Assert.That(resp, Is.Not.Null);
        Assert.That(resp.Type, Is.EqualTo(WsMessageTypes.ErrorResponse));
        Assert.That(resp.Payload?.Code, Is.EqualTo("INVALID_QUERY"));

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

        Assert.That(resp, Is.Not.Null);
        Assert.That(resp.Type, Is.EqualTo(WsMessageTypes.ErrorResponse));
        Assert.That(resp.Payload?.Code, Is.EqualTo("EXECUTION_ERROR"));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_MalformedJson_ReturnsParseError()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var badJsonBytes = Encoding.UTF8.GetBytes("{ this is not: valid json }");
        await ws.SendAsync(new ArraySegment<byte>(badJsonBytes), WebSocketMessageType.Text, true, CancellationToken.None);

        var resp = await ReceiveJsonAsync<ErrorResponseDto>(ws);
        Assert.That(resp, Is.Not.Null);
        Assert.That(resp.Type, Is.EqualTo(WsMessageTypes.ErrorResponse));
        Assert.That(resp.Payload?.Code, Is.EqualTo("PARSE_ERROR"));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_MissingEnvelopeType_ReturnsInvalidMessageError()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var noTypeJson = Encoding.UTF8.GetBytes("{\"requestId\":\"no-type-1\"}");
        await ws.SendAsync(new ArraySegment<byte>(noTypeJson), WebSocketMessageType.Text, true, CancellationToken.None);

        var resp = await ReceiveJsonAsync<ErrorResponseDto>(ws);
        Assert.That(resp, Is.Not.Null);
        Assert.That(resp.Type, Is.EqualTo(WsMessageTypes.ErrorResponse));
        Assert.That(resp.Payload?.Code, Is.EqualTo("INVALID_MESSAGE"));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task WebSocket_UnknownMessageType_ReturnsUnknownTypeError()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);

        var unknownMsg = new WsEnvelope<object>
        {
            Type = "UNSUPPORTED_RANDOM_ACTION",
            RequestId = "unknown-1",
            Payload = new { foo = "bar" }
        };
        await SendJsonAsync(ws, unknownMsg);

        var resp = await ReceiveJsonAsync<ErrorResponseDto>(ws);
        Assert.That(resp, Is.Not.Null);
        Assert.That(resp.Type, Is.EqualTo(WsMessageTypes.ErrorResponse));
        Assert.That(resp.Payload?.Code, Is.EqualTo("UNKNOWN_TYPE"));

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
                FromSymbol = "OrdersController",
                ToSymbol = "chargeOrder",
                MaxDepth = 3
            }
        };
        await SendJsonAsync(ws, callReq);
        var callResp = await ReceiveJsonAsync<QueryResponseDto>(ws);
        Assert.That(callResp, Is.Not.Null);
        Assert.That(callResp.Type, Is.EqualTo(WsMessageTypes.QueryResponse));
        Assert.That(callResp.Payload?.Success, Is.True);

        // GET_IMPACT
        var impactReq = new WsEnvelope<GetImpactRequestDto>
        {
            Type = WsMessageTypes.GetImpactRequest,
            RequestId = "impact-1",
            Payload = new GetImpactRequestDto
            {
                SymbolName = "OrdersController"
            }
        };
        await SendJsonAsync(ws, impactReq);
        var impactResp = await ReceiveJsonAsync<QueryResponseDto>(ws);
        Assert.That(impactResp, Is.Not.Null);
        Assert.That(impactResp.Type, Is.EqualTo(WsMessageTypes.QueryResponse));
        Assert.That(impactResp.Payload?.Success, Is.True);

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
            var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var bytes = Encoding.UTF8.GetBytes(json);

            // Client-side send also uses a lock to avoid client-side concurrency exception
            await clientSendLock.WaitAsync();
            try
            {
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            finally
            {
                clientSendLock.Release();
            }
        });

        await Task.WhenAll(sendTasks);

        // Wait for all responses to arrive
        await receiveTask;

        Assert.That(receivedResponses.Count, Is.EqualTo(requestCount),
            $"Expected {requestCount} responses without server send collision, received {receivedResponses.Count}");

        foreach (var respJson in receivedResponses)
        {
            using var doc = JsonDocument.Parse(respJson);
            Assert.That(doc.RootElement.GetProperty("type").GetString(), Is.EqualTo(WsMessageTypes.PongResponse));
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
        Assert.That(preResp.Payload?.ClientTimestamp, Is.EqualTo(1));
        Assert.That(_wsHandler.ActiveSessionsCount, Is.GreaterThan(0));

        // Abruptly abort connection
        clientAbrupt.Abort();

        // Allow handler loop to process disconnect
        await Task.Delay(200);

        // Now verify that a NEW client can connect and operate normally
        using var clientHealthy = new ClientWebSocket();
        await clientHealthy.ConnectAsync(new Uri(_wsBaseUrl), CancellationToken.None);
        Assert.That(clientHealthy.State, Is.EqualTo(WebSocketState.Open));

        var ping = new WsEnvelope<PingRequestDto>
        {
            Type = WsMessageTypes.PingRequest,
            RequestId = "post-abort-ping",
            Payload = new PingRequestDto { Timestamp = 42 }
        };
        await SendJsonAsync(clientHealthy, ping);
        var resp = await ReceiveJsonAsync<PongResponseDto>(clientHealthy);

        Assert.That(resp, Is.Not.Null);
        Assert.That(resp.Payload?.ClientTimestamp, Is.EqualTo(42));

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

        Assert.That(_wsHandler.ActiveSessionsCount, Is.GreaterThanOrEqualTo(3));

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
            Assert.That(resp.Payload?.ClientTimestamp, Is.EqualTo(idx));
        });

        await Task.WhenAll(tasks);

        // Broadcast test
        await _wsHandler.BroadcastAsync("TEST_BROADCAST", new { testMessage = "broadcast-all" });

        // Read broadcast from client1
        var broadcastEnvelope = await ReceiveJsonAsync<JsonElement>(client1);
        Assert.That(broadcastEnvelope.Type, Is.EqualTo("TEST_BROADCAST"));

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
        return JsonSerializer.Deserialize<WsEnvelope<T>>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        })!;
    }
}
