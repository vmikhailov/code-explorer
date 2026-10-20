using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Mcp;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Server;

public class ClientSession : IDisposable
{
    public string Id { get; }
    public WebSocket Socket { get; }
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public ClientSession(string id, WebSocket socket)
    {
        Id = id;
        Socket = socket;
    }

    public async Task SendAsync(byte[] bytes, WebSocketMessageType messageType = WebSocketMessageType.Text, bool endOfMessage = true, CancellationToken cancellationToken = default)
    {
        if (Socket.State != WebSocketState.Open)
        {
            return;
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (Socket.State == WebSocketState.Open)
            {
                await Socket.SendAsync(new ArraySegment<byte>(bytes), messageType, endOfMessage, cancellationToken);
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public void Dispose()
    {
        _sendLock.Dispose();
    }
}

public partial class WebSocketServerHandler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();
    public int ActiveSessionsCount => _sessions.Count;

    private readonly IGraphClient _graphClient;
    private readonly Core.Analysis.IArchitectureQueryService _archQueryService;
    private readonly CodeExplorerRepository _repository;
    private readonly WorkspaceIndexer _indexer;
    private readonly IHostApplicationLifetime? _appLifetime;
    private readonly ILogger<WebSocketServerHandler> _logger;
    private readonly int _idleTimeoutSeconds;
    private readonly string _workspaceRoot;
    private readonly string _serverVersion;

    private CancellationTokenSource? _idleCts;
    private readonly object _idleLock = new();
    private int _isScanning = 0;
    private volatile ScanProgressEventDto? _lastProgress;
    private GateSuiteSummaryDto? _cachedGateSuite;
    private readonly SemaphoreSlim _gateEvaluationLock = new(1, 1);

    public WebSocketServerHandler(
        IGraphClient graphClient,
        CodeExplorerRepository repository,
        WorkspaceIndexer indexer,
        ILogger<WebSocketServerHandler> logger,
        IHostApplicationLifetime? appLifetime = null,
        int idleTimeoutSeconds = 30,
        string? workspaceRoot = null,
        string? serverVersion = null,
        Core.Analysis.IArchitectureQueryService? archQueryService = null)
    {
        _graphClient = graphClient;
        _archQueryService = archQueryService ?? new Core.Analysis.ArchitectureQueryService(graphClient);
        _repository = repository;
        _indexer = indexer;
        _logger = logger;
        _appLifetime = appLifetime;
        _idleTimeoutSeconds = idleTimeoutSeconds;
        _workspaceRoot = workspaceRoot ?? WorkspaceLocator.Find()?.RootDirectory ?? Directory.GetCurrentDirectory();
        _serverVersion = serverVersion 
            ?? typeof(WebSocketServerHandler).Assembly.GetName().Version?.ToString(3) 
            ?? "1.4.1";

        if (_idleTimeoutSeconds > 0)
        {
            ResetIdleTimer();
        }
    }

    public async Task HandleConnectionAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid().ToString("N");

        using var session = new ClientSession(connectionId, socket);

        _sessions[connectionId] = session;

        _logger.LogInformation("[WS] Client connected: {ConnectionId} (total active: {Count})", connectionId,
            _sessions.Count);

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
                    if (socket.State == WebSocketState.CloseReceived)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing",
                            cancellationToken);
                    }

                    break;
                }

                if (ms.Length == 0) continue;

                var messageJson = Encoding.UTF8.GetString(ms.ToArray());
                await ProcessMessageAsync(session, messageJson, cancellationToken);
            }
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely ||
                                            ex.WebSocketErrorCode == WebSocketError.InvalidState)
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
            _sessions.TryRemove(connectionId, out _);

            _logger.LogInformation("[WS] Client disconnected: {ConnectionId} (remaining active: {Count})",
                connectionId, _sessions.Count);

            if (_sessions.IsEmpty && _idleTimeoutSeconds > 0)
            {
                ResetIdleTimer();
            }
        }
    }

    public async Task ProcessMessageAsync(ClientSession session, string json, CancellationToken cancellationToken)
    {
        WsEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<WsEnvelope>(json, JsonOpts);
            if (envelope == null || string.IsNullOrEmpty(envelope.Type))
            {
                await SendErrorAsync(session, "", "INVALID_MESSAGE", "Message envelope must include 'type'.", cancellationToken);
                return;
            }
        }
        catch (Exception ex)
        {
            await SendErrorAsync(session, "", "PARSE_ERROR", $"Invalid JSON payload: {ex.Message}", cancellationToken);
            return;
        }

        var reqId = envelope.RequestId ?? "";
        _logger.LogInformation("[WS] Received '{Type}' (reqId: {ReqId}, client: {ClientId})", envelope.Type, reqId, session.Id);

        try
        {
            switch (envelope.Type.ToUpperInvariant())
            {
                case WsMessageTypes.HandshakeRequest:
                case "HANDSHAKE":
                    await HandleHandshakeAsync(session, reqId, envelope.Payload, cancellationToken);
                    break;

                case WsMessageTypes.PingRequest:
                case "PING":
                    var ping = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<PingRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    await SendResponseAsync(session, WsMessageTypes.PongResponse, reqId, new PongResponseDto
                    {
                        ClientTimestamp = ping?.Timestamp ?? 0,
                        ServerTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    }, cancellationToken);
                    break;

                case WsMessageTypes.GetViewRequest:
                case "GET_VIEW":
                    var viewReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<GetViewRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var viewType = (viewReq?.View?.ToLowerInvariant()) switch
                    {
                        "serviceflow" or "flow" or "c2" => Core.Analysis.ArchitectureViewType.ServiceFlow,
                        "component" or "c3" => Core.Analysis.ArchitectureViewType.Component,
                        "domain" or "domainmap" or "domain-map" or "semantic" => Core.Analysis.ArchitectureViewType.DomainMap,
                        "contexts" or "boundedcontexts" or "contextmap" or "context-map" or "boundedcontext" or "bounded-context" => Core.Analysis.ArchitectureViewType.BoundedContexts,
                        "tiers" or "tiered" or "layers" or "layered" => Core.Analysis.ArchitectureViewType.Tiers,
                        _ => Core.Analysis.ArchitectureViewType.SystemContext
                    };
                    var viewGraph = await _archQueryService.GetViewAsync(new Core.Analysis.ArchitectureViewRequest
                    {
                        ViewType = viewType,
                        Scope = viewReq?.Scope,
                        IncludeLibraries = viewReq?.IncludeLibraries ?? (viewType == Core.Analysis.ArchitectureViewType.Tiers)
                    }, cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.GetViewResponse, reqId, new QueryResponseDto
                    {
                        Success = true,
                        Graph = viewGraph
                    }, cancellationToken);
                    break;

                case WsMessageTypes.GetBoundedContextsRequest:
                case "GET_BOUNDED_CONTEXTS":
                    var bcMap = await _archQueryService.GetBoundedContextMapAsync(ct: cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.GetBoundedContextsResponse, reqId, bcMap, cancellationToken);
                    break;

                case WsMessageTypes.GetDomainsRequest:
                case "GET_DOMAINS":
                    var domainConfig = DomainManagementService.LoadConfig(_workspaceRoot);
                    await SendResponseAsync(session, WsMessageTypes.GetDomainsResponse, reqId, domainConfig, cancellationToken);
                    break;

                case WsMessageTypes.SetDomainOverrideRequest:
                case "SET_DOMAIN_OVERRIDE":
                    var overrideReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<SetDomainOverrideRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    if (overrideReq == null || string.IsNullOrWhiteSpace(overrideReq.ServiceName))
                    {
                        await SendErrorAsync(session, reqId, "INVALID_REQUEST", "'serviceName' is required.", cancellationToken);
                        break;
                    }
                    if (overrideReq.RemoveOverride)
                    {
                        var removed = DomainManagementService.RemoveServiceOverride(_workspaceRoot, overrideReq.ServiceName);
                        _repository.InvalidateCache();
                        await SendResponseAsync(session, WsMessageTypes.DomainMutationResponse, reqId, new DomainMutationResponseDto
                        {
                            Success = removed,
                            Message = removed ? $"Override removed for service '{overrideReq.ServiceName}'." : $"No override found for '{overrideReq.ServiceName}'."
                        }, cancellationToken);
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(overrideReq.Domain))
                        {
                            await SendErrorAsync(session, reqId, "INVALID_REQUEST", "'domain' is required when setting override.", cancellationToken);
                            break;
                        }
                        DomainManagementService.AssignServiceDomain(_workspaceRoot, overrideReq.ServiceName, overrideReq.Domain, overrideReq.Context, _graphClient, cancellationToken);
                        _repository.InvalidateCache();
                        await SendResponseAsync(session, WsMessageTypes.DomainMutationResponse, reqId, new DomainMutationResponseDto
                        {
                            Success = true,
                            Message = $"Service '{overrideReq.ServiceName}' assigned to domain '{overrideReq.Domain}'."
                        }, cancellationToken);
                    }
                    break;

                case WsMessageTypes.AddDomainRequest:
                case "ADD_DOMAIN":
                    var addReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<AddDomainRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    if (addReq == null || string.IsNullOrWhiteSpace(addReq.Name))
                    {
                        await SendErrorAsync(session, reqId, "INVALID_REQUEST", "Domain 'name' is required.", cancellationToken);
                        break;
                    }
                    DomainManagementService.AddOrUpdateDomain(_workspaceRoot, new DomainDefinitionDto
                    {
                        Name = addReq.Name.Trim(),
                        DisplayName = addReq.DisplayName,
                        Description = addReq.Description,
                        Icon = addReq.Icon,
                        Color = addReq.Color
                    });
                    _repository.InvalidateCache();
                    await SendResponseAsync(session, WsMessageTypes.DomainMutationResponse, reqId, new DomainMutationResponseDto
                    {
                        Success = true,
                        Message = $"Domain '{addReq.Name}' added/updated."
                    }, cancellationToken);
                    break;

                case WsMessageTypes.DeleteDomainRequest:
                case "DELETE_DOMAIN":
                    var delReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<DeleteDomainRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    if (delReq == null || string.IsNullOrWhiteSpace(delReq.Name))
                    {
                        await SendErrorAsync(session, reqId, "INVALID_REQUEST", "Domain 'name' is required.", cancellationToken);
                        break;
                    }
                    var delSuccess = DomainManagementService.RemoveDomain(_workspaceRoot, delReq.Name.Trim(), delReq.ReassignTo);
                    _repository.InvalidateCache();
                    await SendResponseAsync(session, WsMessageTypes.DomainMutationResponse, reqId, new DomainMutationResponseDto
                    {
                        Success = delSuccess,
                        Message = delSuccess ? $"Domain '{delReq.Name}' removed." : $"Domain '{delReq.Name}' not found."
                    }, cancellationToken);
                    break;

                case WsMessageTypes.GetOntologyLayersRequest:
                case "GET_ONTOLOGY_LAYERS":
                    var ontologyLayers = await _archQueryService.GetOntologyLayersAsync(cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.GetOntologyLayersResponse, reqId, ontologyLayers, cancellationToken);
                    break;

                case WsMessageTypes.GetArchitectureRequest:
                case "GET_ARCHITECTURE":
                    var archReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<GetArchitectureRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var archGraph = await _archQueryService.GetArchitectureGraphAsync(archReq?.ProjectFilter, archReq?.IncludeLibraries ?? false, cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
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
                    var depGraph = await _archQueryService.GetProjectNeighborhoodAsync(depReq?.ProjectName, cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
                    {
                        Success = true,
                        Graph = depGraph
                    }, cancellationToken);
                    break;

                case WsMessageTypes.GetCallChainRequest:
                case "GET_CALL_CHAIN":
                    var callReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<GetCallChainRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var callJson = await _repository.GetCallChainAsync(callReq?.FromSymbol ?? "", callReq?.ToSymbol ?? "", callReq?.MaxDepth ?? 5, "json", _workspaceRoot, cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
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
                    var impactJson = await _repository.AnalyzeCodeImpactAsync(impactReq?.SymbolName ?? "", format: "json", workspacePath: _workspaceRoot, cancellationToken: cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
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
                        await SendErrorAsync(session, reqId, "INVALID_QUERY", "Cypher query must not be empty.", cancellationToken);
                        return;
                    }

                    var cypherResult = await _graphClient.ExecuteQueryAsync(cypherReq.Query, cypherReq.Parameters, cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
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

                    if (!TriggerScan(scanReq?.TargetPath, scanReq?.Clear ?? false))
                    {
                        await SendErrorAsync(session, reqId, "SCAN_IN_PROGRESS", "A workspace scan is already in progress.", cancellationToken);
                        break;
                    }

                    await SendResponseAsync(session, WsMessageTypes.QueryResponse, reqId, new QueryResponseDto
                    {
                        Success = true,
                        RawJson = "{\"status\":\"scanning_started\"}"
                    }, cancellationToken);
                    break;

                case WsMessageTypes.GetGatesRequest:
                case "GET_GATES":
                case WsMessageTypes.EvaluateGatesRequest:
                case "EVALUATE_GATES":
                    var evalReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<EvaluateGatesRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var suiteSummary = await EvaluateGatesSuiteAsync(evalReq?.GateId, cancellationToken);
                    var respType = envelope.Type.Contains("EVALUATE", StringComparison.OrdinalIgnoreCase)
                        ? WsMessageTypes.EvaluateGatesResponse
                        : WsMessageTypes.GetGatesResponse;
                    await SendResponseAsync(session, respType, reqId, suiteSummary, cancellationToken);
                    break;

                case WsMessageTypes.RatchetGatesRequest:
                case "RATCHET_GATES":
                    var ratchetReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<RatchetGatesRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var ratchetResult = await RatchetGatesSuiteAsync(ratchetReq?.GateId, ratchetReq?.UpdateAll ?? false, cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.RatchetGatesResponse, reqId, ratchetResult, cancellationToken);
                    break;

                case WsMessageTypes.InitGatesRequest:
                case "INIT_GATES":
                    var initReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<InitGatesRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    var initResult = await InitStarterGatesAsync(initReq?.Overwrite ?? false, cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.InitGatesResponse, reqId, initResult, cancellationToken);
                    break;

                case WsMessageTypes.SaveGateRequest:
                case "SAVE_GATE":
                    var saveReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<SaveGateRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    if (saveReq?.Gate == null)
                    {
                        await SendErrorAsync(session, reqId, "INVALID_PAYLOAD", "Payload must contain 'gate'.", cancellationToken);
                        break;
                    }
                    var saveResult = await SaveGateAsync(saveReq.Gate, saveReq.TargetPath, cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.SaveGateResponse, reqId, saveResult, cancellationToken);
                    break;

                case WsMessageTypes.TestGateRequest:
                case "TEST_GATE":
                    var testReq = envelope.Payload.ValueKind == JsonValueKind.Object
                        ? JsonSerializer.Deserialize<TestGateRequestDto>(envelope.Payload.GetRawText(), JsonOpts)
                        : null;
                    if (string.IsNullOrWhiteSpace(testReq?.Query))
                    {
                        await SendErrorAsync(session, reqId, "INVALID_PAYLOAD", "Payload must contain 'query'.", cancellationToken);
                        break;
                    }
                    var testResult = await TestGateQueryAsync(testReq.Query, testReq.GateId, testReq.Parameters, cancellationToken);
                    await SendResponseAsync(session, WsMessageTypes.TestGateResponse, reqId, testResult, cancellationToken);
                    break;

                default:
                    await SendErrorAsync(session, reqId, "UNKNOWN_TYPE", $"Unknown message type: '{envelope.Type}'", cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WS] Failed processing message {Type} (reqId: {ReqId})", envelope.Type, reqId);
            await SendErrorAsync(session, reqId, "EXECUTION_ERROR", ex.Message, cancellationToken, ex.ToString());
        }
    }

    public async Task BroadcastAsync<T>(string type, T payload, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[WS] Broadcasting '{Type}' to {Count} active clients", type, _sessions.Count);
        var envelope = new WsEnvelope<T>
        {
            Type = type,
            RequestId = Guid.NewGuid().ToString("N"),
            Payload = payload
        };

        var json = JsonSerializer.Serialize(envelope, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);

        foreach (var (id, session) in _sessions)
        {
            try
            {
                await session.SendAsync(bytes, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[WS] Failed broadcasting to client {Id}", id);
            }
        }
    }

    private async Task SendResponseAsync<T>(ClientSession session, string type, string reqId, T payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[WS] Responding '{Type}' (reqId: {ReqId}, client: {ClientId})", type, reqId, session.Id);
        var envelope = new WsEnvelope<T>
        {
            Type = type,
            RequestId = reqId,
            Payload = payload
        };
        var json = JsonSerializer.Serialize(envelope, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        await session.SendAsync(bytes, cancellationToken: cancellationToken);
    }

    private async Task SendErrorAsync(ClientSession session, string reqId, string code, string message, CancellationToken cancellationToken, string? details = null)
    {
        _logger.LogWarning("[WS] Sending error [{Code}] (reqId: {ReqId}, client: {ClientId}): {Message} | Details: {Details}", code, reqId, session.Id, message, details ?? "none");
        var envelope = new WsEnvelope<ErrorResponseDto>
        {
            Type = WsMessageTypes.ErrorResponse,
            RequestId = reqId,
            Payload = new ErrorResponseDto
            {
                Code = code,
                Message = message,
                Details = details
            }
        };
        var json = JsonSerializer.Serialize(envelope, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);
        await session.SendAsync(bytes, cancellationToken: cancellationToken);
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
                    if (!token.IsCancellationRequested && _sessions.IsEmpty)
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
