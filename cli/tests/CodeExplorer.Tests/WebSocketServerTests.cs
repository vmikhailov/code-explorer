using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Parser.TypeScript;
using NUnit.Framework;

namespace CodeExplorer.Tests;

[TestFixture]
[Category("Integration")]
public class WebSocketServerTests
{
    private const int TestPort = 8189;
    private static Task? _serverTask;
    private static HttpClient? _httpClient;
    private static string? _tempWorkspace;
    private static string? _dbPath;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "codeexplorer_ws_test_" + Guid.NewGuid()).Replace('\\', '/');
        var projDir = Path.Combine(_tempWorkspace, "SampleProject").Replace('\\', '/');
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

        _dbPath = Path.Combine(_tempWorkspace, "test_graph.db");
        await using var client = new SqliteGraphClient(_dbPath);
        WorkspaceIndexer.Register(new TypeScriptParser());
        var indexer = new WorkspaceIndexer(client);
        await indexer.IndexAsync(_tempWorkspace, _tempWorkspace, clear: true);

        // Start WebSocket server in background
        _serverTask = Task.Run(() => Program.Main([
            "serve",
            "--port", TestPort.ToString(),
            "--db-path", _dbPath,
            "--root", _tempWorkspace,
            "--idle-timeout", "0",
            "--quiet"
        ]));

        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        var available = false;
        for (var i = 0; i < 40; i++)
        {
            try
            {
                var response = await _httpClient.GetAsync($"http://127.0.0.1:{TestPort}/");
                if (response.IsSuccessStatusCode)
                {
                    available = true;
                    break;
                }
            }
            catch
            {
                await Task.Delay(250);
            }
        }

        Assert.That(available, Is.True, $"WebSocket server failed to start within timeout on port {TestPort}");
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        try
        {
            if (Program.App != null)
            {
                await Program.App.StopAsync();
            }
            _httpClient?.Dispose();

            if (_serverTask != null)
            {
                try
                {
                    await _serverTask;
                    _serverTask.Dispose();
                }
                catch
                {
                }
            }

            if (_tempWorkspace != null && Directory.Exists(_tempWorkspace))
            {
                Directory.Delete(_tempWorkspace, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Test]
    public async Task Test_HttpEndpoints_ReturnValidStatusAndArchitecture()
    {
        var statusRes = await _httpClient!.GetAsync($"http://127.0.0.1:{TestPort}/api/status");
        Assert.That(statusRes.IsSuccessStatusCode, Is.True);
        var statusJson = await statusRes.Content.ReadAsStringAsync();
        Assert.That(statusJson, Does.Contain("ok"));

        var archRes = await _httpClient!.GetAsync($"http://127.0.0.1:{TestPort}/api/architecture");
        Assert.That(archRes.IsSuccessStatusCode, Is.True);
        var archJson = await archRes.Content.ReadAsStringAsync();
        Assert.That(archJson, Does.Contain("nodes"));
    }

    [Test]
    public async Task Test_WebSocket_Handshake_ReturnsCapabilities()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{TestPort}/ws"), CancellationToken.None);
        Assert.That(ws.State, Is.EqualTo(WebSocketState.Open));

        var req = new WsEnvelope<HandshakeRequestDto>
        {
            Type = WsMessageTypes.HandshakeRequest,
            RequestId = "test-handshake-1",
            Payload = new HandshakeRequestDto
            {
                ClientName = "test-client",
                ClientVersion = "1.0.0"
            }
        };

        await SendJsonAsync(ws, req);
        var response = await ReceiveJsonAsync<HandshakeResponseDto>(ws);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Type, Is.EqualTo(WsMessageTypes.HandshakeResponse));
        Assert.That(response.RequestId, Is.EqualTo("test-handshake-1"));
        Assert.That(response.Payload?.Capabilities, Does.Contain("architecture"));
        Assert.That(response.Payload?.Capabilities, Does.Contain("cypher"));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task Test_WebSocket_PingPong()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{TestPort}/ws"), CancellationToken.None);

        var req = new WsEnvelope<PingRequestDto>
        {
            Type = WsMessageTypes.PingRequest,
            RequestId = "ping-1",
            Payload = new PingRequestDto
            {
                Timestamp = 123456789
            }
        };

        await SendJsonAsync(ws, req);
        var response = await ReceiveJsonAsync<PongResponseDto>(ws);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Type, Is.EqualTo(WsMessageTypes.PongResponse));
        Assert.That(response.Payload?.ClientTimestamp, Is.EqualTo(123456789));
        Assert.That(response.Payload?.ServerTimestamp, Is.GreaterThan(0));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task Test_WebSocket_GetArchitecture_ReturnsGraphData()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{TestPort}/ws"), CancellationToken.None);

        var req = new WsEnvelope<GetArchitectureRequestDto>
        {
            Type = WsMessageTypes.GetArchitectureRequest,
            RequestId = "arch-req-1",
            Payload = new GetArchitectureRequestDto()
        };

        await SendJsonAsync(ws, req);
        var response = await ReceiveJsonAsync<QueryResponseDto>(ws);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Type, Is.EqualTo(WsMessageTypes.QueryResponse));
        Assert.That(response.Payload?.Success, Is.True);
        Assert.That(response.Payload?.Graph, Is.Not.Null);
        Assert.That(response.Payload!.Graph!.Nodes, Is.Not.Empty);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task Test_WebSocket_ExecuteCypher_ReturnsResults()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{TestPort}/ws"), CancellationToken.None);

        var req = new WsEnvelope<ExecuteCypherRequestDto>
        {
            Type = WsMessageTypes.ExecuteCypherRequest,
            RequestId = "cypher-req-1",
            Payload = new ExecuteCypherRequestDto
            {
                Query = "MATCH (p:Project) RETURN p.name AS name"
            }
        };

        await SendJsonAsync(ws, req);
        var response = await ReceiveJsonAsync<QueryResponseDto>(ws);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Type, Is.EqualTo(WsMessageTypes.QueryResponse));
        Assert.That(response.Payload?.Success, Is.True);
        Assert.That(response.Payload?.RawJson, Does.Contain("SampleProject"));

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task Test_WebSocket_GetDependencies_ReturnsNeighborhoodGraph()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{TestPort}/ws"), CancellationToken.None);

        var req = new WsEnvelope<GetDependenciesRequestDto>
        {
            Type = WsMessageTypes.GetDependenciesRequest,
            RequestId = "dep-req-1",
            Payload = new GetDependenciesRequestDto
            {
                ProjectName = "SampleProject"
            }
        };

        await SendJsonAsync(ws, req);
        var response = await ReceiveJsonAsync<QueryResponseDto>(ws);

        Assert.That(response, Is.Not.Null);
        Assert.That(response.Type, Is.EqualTo(WsMessageTypes.QueryResponse));
        Assert.That(response.Payload?.Success, Is.True);
        Assert.That(response.Payload?.Graph, Is.Not.Null);
        Assert.That(response.Payload!.Graph!.Nodes.Any(n => n.Properties != null && n.Properties.ContainsKey("column")), Is.True);
        Assert.That(response.Payload.Graph.Metadata != null && response.Payload.Graph.Metadata.ContainsKey("allProjects"), Is.True);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

    [Test]
    public async Task Test_WebSocket_TriggerScanRequest_ReturnsSuccessAndBroadcastsProgress()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{TestPort}/ws"), CancellationToken.None);

        // 1. Handshake
        var hsReq = new WsEnvelope<HandshakeRequestDto>
        {
            Type = WsMessageTypes.HandshakeRequest,
            RequestId = "hs-scan-1",
            Payload = new HandshakeRequestDto { ClientVersion = "1.0.0" }
        };
        await SendJsonAsync(ws, hsReq);
        var hsResp = await ReceiveJsonAsync<HandshakeResponseDto>(ws);
        Assert.That(hsResp.Type, Is.EqualTo(WsMessageTypes.HandshakeResponse));

        // 2. Trigger Scan Request
        var scanReq = new WsEnvelope<TriggerScanRequestDto>
        {
            Type = WsMessageTypes.TriggerScanRequest,
            RequestId = "scan-req-1",
            Payload = new TriggerScanRequestDto
            {
                TargetPath = _tempWorkspace,
                Clear = false
            }
        };
        await SendJsonAsync(ws, scanReq);

        // 3. Receive QueryResponse acknowledging scan started
        var ackResp = await ReceiveJsonAsync<QueryResponseDto>(ws);
        Assert.That(ackResp.Type, Is.EqualTo(WsMessageTypes.QueryResponse));
        Assert.That(ackResp.Payload?.Success, Is.True);
        Assert.That(ackResp.Payload?.RawJson, Does.Contain("scanning_started"));

        // 4. Receive at least one ScanProgressEvent broadcast
        var progressEnvelope = await ReceiveJsonAsync<ScanProgressEventDto>(ws);
        Assert.That(progressEnvelope.Type, Is.EqualTo(WsMessageTypes.ScanProgressEvent));
        Assert.That(progressEnvelope.Payload, Is.Not.Null);
        Assert.That(progressEnvelope.Payload!.Phase, Is.Not.Empty);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
    }

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
