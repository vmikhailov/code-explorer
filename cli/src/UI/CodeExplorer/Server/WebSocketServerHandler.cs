using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Server;

public class WebSocketServerHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly IGraphClient _graphClient;
    private readonly CodeExplorerRepository _repository;
    private readonly WorkspaceIndexer _indexer;
    private readonly IHostApplicationLifetime? _appLifetime;
    private readonly ILogger<WebSocketServerHandler> _logger;
    private readonly int _idleTimeoutSeconds;
    private readonly string _workspaceRoot;
    private readonly string _serverVersion;

    private CancellationTokenSource? _idleCts;
    private readonly object _idleLock = new();

    public WebSocketServerHandler(
        IGraphClient graphClient,
        CodeExplorerRepository repository,
        WorkspaceIndexer indexer,
        ILogger<WebSocketServerHandler> logger,
        IHostApplicationLifetime? appLifetime = null,
        int idleTimeoutSeconds = 30,
        string? workspaceRoot = null,
        string serverVersion = "1.3.4")
    {
        _graphClient = graphClient;
        _repository = repository;
        _indexer = indexer;
        _logger = logger;
        _appLifetime = appLifetime;
        _idleTimeoutSeconds = idleTimeoutSeconds;
        _workspaceRoot = workspaceRoot ?? WorkspaceLocator.Find()?.RootDirectory ?? Directory.GetCurrentDirectory();
        _serverVersion = serverVersion;

        if (_idleTimeoutSeconds > 0)
        {
            ResetIdleTimer();
        }
    }

    public async Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        _sockets[connectionId] = socket;
        _logger.LogInformation("[WS] Client connected: {ConnectionId} (total active: {Count})", connectionId, _sockets.Count);

        CancelIdleTimer();

        var buffer = new byte[1024 * 64];
        var ms = new MemoryStream();

        try
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                ms.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    break;
                }

                if (ms.Length == 0) continue;

                var messageJson = Encoding.UTF8.GetString(ms.ToArray());
                await ProcessMessageAsync(socket, messageJson, cancellationToken);
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug("[WS] Client disconnected prematurely: {ConnectionId}", connectionId);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WS] Error handling socket connection {ConnectionId}", connectionId);
        }
        finally
        {
            _sockets.TryRemove(connectionId, out _);
            _logger.LogInformation("[WS] Client disconnected: {ConnectionId} (remaining active: {Count})", connectionId, _sockets.Count);

            if (_sockets.IsEmpty && _idleTimeoutSeconds > 0)
            {
                ResetIdleTimer();
            }
        }
    }

    private async Task ProcessMessageAsync(WebSocket socket, string json, CancellationToken cancellationToken)
    {
        WsEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<WsEnvelope>(json, JsonOpts);
            if (envelope == null || string.IsNullOrEmpty(envelope.Type))
            {
                await SendErrorAsync(socket, "", "INVALID_MESSAGE", "Message envelope must include 'type'.", cancellationToken);
                return;
            }
        }
        catch (Exception ex)
        {
            await SendErrorAsync(socket, "", "PARSE_ERROR", $"Invalid JSON payload: {ex.Message}", cancellationToken);
            return;
        }

        var reqId = envelope.RequestId ?? "";

        try
        {
            switch (envelope.Type.ToUpperInvariant())
            {
                case WsMessageTypes.HandshakeRequest:
                case "HANDSHAKE":
                    await HandleHandshakeAsync(socket, reqId, envelope.Payload, cancellationToken);
                    break;

                case WsMessageTypes.PingRequest:
                case "PING":
                    var ping = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<PingRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    await SendResponseAsync(socket, WsMessageTypes.PongResponse, reqId, new PongResponseDto
                    {
                        ClientTimestamp = ping?.Timestamp ?? 0,
                        ServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    }, cancellationToken);
                    break;

                case WsMessageTypes.GetArchitectureRequest:
                case "GET_ARCHITECTURE":
                    var archReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<GetArchitectureRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var archGraph = await GraphDataConverter.GetArchitectureGraphAsync(_graphClient, archReq?.ProjectFilter, cancellationToken);
                    await SendResponseAsync(socket, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
                    {
                        Success = true,
                        Graph = archGraph
                    }, cancellationToken);
                    break;

                case WsMessageTypes.GetDependenciesRequest:
                case "GET_DEPENDENCIES":
                    var depReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<GetDependenciesRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var depJson = await _repository.GetProjectDependenciesAsync(depReq?.ProjectName, "json", 100, _workspaceRoot, cancellationToken);
                    await SendResponseAsync(socket, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
                    {
                        Success = true,
                        RawJson = depJson
                    }, cancellationToken);
                    break;

                case WsMessageTypes.GetCallChainRequest:
                case "GET_CALL_CHAIN":
                    var callReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<GetCallChainRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var callJson = await _repository.GetCallChainAsync(callReq?.FromSymbol ?? "", callReq?.ToSymbol ?? "", callReq?.MaxDepth ?? 5, "json", _workspaceRoot, cancellationToken);
                    await SendResponseAsync(socket, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
                    {
                        Success = true,
                        RawJson = callJson
                    }, cancellationToken);
                    break;

                case WsMessageTypes.GetImpactRequest:
                case "GET_IMPACT":
                    var impactReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<GetImpactRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var impactJson = await _repository.AnalyzeCodeImpactAsync(impactReq?.SymbolName ?? "", _workspaceRoot, cancellationToken);
                    await SendResponseAsync(socket, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
                    {
                        Success = true,
                        RawJson = impactJson
                    }, cancellationToken);
                    break;

                case WsMessageTypes.ExecuteCypherRequest:
                case "EXECUTE_CYPHER":
                    var cypherReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<ExecuteCypherRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    if (string.IsNullOrWhiteSpace(cypherReq?.Query))
                    {
                        await SendErrorAsync(socket, reqId, "INVALID_QUERY", "Cypher query must not be empty.", cancellationToken);
                        return;
                    }

                    var cypherResult = await _graphClient.ExecuteQueryAsync(cypherReq.Query, cypherReq.Parameters, cancellationToken);
                    await SendResponseAsync(socket, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
                    {
                        Success = true,
                        RawJson = cypherResult
                    }, cancellationToken);
                    break;

                case WsMessageTypes.TriggerScanRequest:
                case "TRIGGER_SCAN":
                    var scanReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<TriggerScanRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var target = string.IsNullOrWhiteSpace(scanReq?.TargetPath) ? _workspaceRoot : scanReq.TargetPath;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await BroadcastAsync(WsMessageTypes.ScanProgressEvent, new ScanProgressEventDto
                            {
                                Phase = "Starting",
                                Percentage = 0,
                                CurrentFile = target
                            }, CancellationToken.None);

                            var (nodesCount, relsCount, _) = await _indexer.IndexAsync(target, _workspaceRoot, scanReq?.Clear ?? false);

                            await BroadcastAsync(WsMessageTypes.ScanProgressEvent, new ScanProgressEventDto
                            {
                                Phase = "Completed",
                                Percentage = 100,
                                TotalFiles = (int)nodesCount
                            }, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[WS] Background scan failed");
                        }
                    }, cancellationToken);

                    await SendResponseAsync(socket, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
                    {
                        Success = true,
                        RawJson = "{\"status\":\"scanning_started\"}"
                    }, cancellationToken);
                    break;

                default:
                    await SendErrorAsync(socket, reqId, "UNKNOWN_TYPE", $"Unknown message type: '{envelope.Type}'", cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WS] Failed processing message {Type} (reqId: {ReqId})", envelope.Type, reqId);
            await SendErrorAsync(socket, reqId, "EXECUTION_ERROR", ex.Message, cancellationToken);
        }
    }

    private async Task HandleHandshakeAsync(WebSocket socket, string reqId, JsonElement payload, CancellationToken cancellationToken)
    {
        long nodesCount = 0;
        long edgesCount = 0;
        try
        {
            var nodeCountJson = await _graphClient.ExecuteQueryAsync("MATCH (n) RETURN count(n) AS cnt", null, cancellationToken);
            using var nodeDoc = JsonDocument.Parse(nodeCountJson);
            if (nodeDoc.RootElement.GetArrayLength() > 0)
            {
                nodesCount = nodeDoc.RootElement[0].GetProperty("cnt").GetInt64();
            }

            var edgeCountJson = await _graphClient.ExecuteQueryAsync("MATCH ()-[r]->() RETURN count(r) AS cnt", null, cancellationToken);
            using var edgeDoc = JsonDocument.Parse(edgeCountJson);
            if (edgeDoc.RootElement.GetArrayLength() > 0)
            {
                edgesCount = edgeDoc.RootElement[0].GetProperty("cnt").GetInt64();
            }
        }
        catch
        {
            // Ignore if empty/new database
        }

        var response = new HandshakeResponseDto
        {
            ServerVersion = _serverVersion,
            WorkspaceRoot = _workspaceRoot,
            DbPath = (_graphClient as SqliteGraphClient)?.DbPath ?? "",
            TotalNodes = nodesCount,
            TotalEdges = edgesCount,
            Capabilities = ["architecture", "dependencies", "call_chain", "impact", "cypher", "scan", "graph_patch"]
        };

        await SendResponseAsync(socket, WsMessageTypes.HandshakeResponse, reqId, response, cancellationToken);
    }

    public async Task BroadcastAsync<T>(string type, T payload, CancellationToken cancellationToken = default)
    {
        var envelope = new WsEnvelope<T>
        {
            Type = type,
            RequestId = Guid.NewGuid().ToString("N"),
            Payload = payload
        };

        var json = JsonSerializer.Serialize(envelope, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);

        foreach (var (id, socket) in _sockets)
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[WS] Failed broadcasting to client {Id}", id);
                }
            }
        }
    }

    private static async Task SendResponseAsync<T>(WebSocket socket, string type, string reqId, T payload, CancellationToken cancellationToken)
    {
        var envelope = new WsEnvelope<T>
        {
            Type = type,
            RequestId = reqId,
            Payload = payload
        };
        var json = JsonSerializer.Serialize(envelope, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task SendErrorAsync(WebSocket socket, string reqId, string code, string message, CancellationToken cancellationToken)
    {
        var envelope = new WsEnvelope<ErrorResponseDto>
        {
            Type = WsMessageTypes.ErrorResponse,
            RequestId = reqId,
            Payload = new ErrorResponseDto
            {
                Code = code,
                Message = message
            }
        };
        var json = JsonSerializer.Serialize(envelope, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private void CancelIdleTimer()
    {
        lock (_idleLock)
        {
            _idleCts?.Cancel();
            _idleCts?.Dispose();
            _idleCts = null;
        }
    }

    private void ResetIdleTimer()
    {
        lock (_idleLock)
        {
            CancelIdleTimer();
            if (_idleTimeoutSeconds <= 0) return;

            _idleCts = new CancellationTokenSource();
            var token = _idleCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_idleTimeoutSeconds), token);
                    if (!token.IsCancellationRequested && _sockets.IsEmpty)
                    {
                        _logger.LogInformation("[WS Server] Idle timeout of {Seconds}s reached with 0 connected clients. Shutting down...", _idleTimeoutSeconds);
                        _appLifetime?.StopApplication();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timer was reset or client connected
                }
            }, token);
        }
    }
}
