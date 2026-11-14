import type * as vscode from 'vscode';
import { EngineSupervisor, EngineSupervisorStatus } from '../engine/engineSupervisor';
import { ServerInfo } from '../processManager';

export interface RpcRequestMessage<TParams = any> {
  type: 'RPC_REQUEST';
  id: string;
  method: string;
  params?: TParams;
}

export interface RpcErrorPayload {
  code: string;
  message: string;
  details?: string;
}

export interface RpcResponseMessage<TResult = any> {
  type: 'RPC_RESPONSE';
  id: string;
  ok: boolean;
  data?: TResult;
  error?: RpcErrorPayload;
}

export interface RpcEventMessage<TPayload = any> {
  type: 'RPC_EVENT';
  topic: string;
  payload: TPayload;
}

export interface WebviewMessagePort {
  postMessage(message: any): Thenable<boolean> | Promise<boolean> | boolean | void;
  onDidReceiveMessage(
    listener: (e: any) => any,
    thisArgs?: any,
    disposables?: vscode.Disposable[]
  ): vscode.Disposable | { dispose(): void };
}

export interface RpcHandlerContext {
  supervisor: EngineSupervisor;
  serverInfo: ServerInfo | null;
  webview?: WebviewMessagePort;
}

export type RpcMethodHandler<TParams = any, TResult = any> = (
  params: TParams,
  context: RpcHandlerContext
) => Promise<TResult> | TResult;

interface RegisteredMethod {
  handler: RpcMethodHandler;
  requiresEngine: boolean;
}

export interface DiagramFetchParams {
  path?: string;
  viewType?: string;
  project?: string;
  service?: string;
  target?: string;
  query?: Record<string, string | number | boolean | undefined>;
  timeoutMs?: number;
}

/**
 * HostRpcBroker — Single-Broker Extension Host RPC Router.
 *
 * Bridges all Webview tabs and the Inspector to `EngineSupervisor` over `postMessage`:
 * - Queues incoming `RPC_REQUEST` messages behind `supervisor.ensureReady()` while the engine
 *   is `Idle`, `Discovering`, `Starting`, or `RetryingOnce`, guaranteeing zero dropped requests
 *   when multiple diagram tabs restore in parallel.
 * - Logs every RPC call with duration (ms) and payload size (bytes) to `CodeExplorer (Protocol)`.
 * - Broadcasts `RPC_EVENT` notifications (`engine:status`, `scan:progress`, `gates:updated`) to all attached webviews.
 */
export class HostRpcBroker {
  private static _instance: HostRpcBroker | null = null;
  private static readonly TOPIC_ENGINE_STATUS = 'engine:status';
  private static readonly RPC_HTTP_GET = 'http.get';
  private static readonly RPC_HTTP_POST = 'http.post';
  private static readonly RPC_DIAGRAM_FETCH = 'diagram.fetch';
  private static readonly RPC_ENGINE_STATUS = 'engine.status';
  private static readonly RPC_ENGINE_RETRY = 'engine.retry';
  private static readonly HTTP_GET: 'GET' = 'GET';
  private static readonly HTTP_POST: 'POST' = 'POST';

  private readonly _methods = new Map<string, RegisteredMethod>();
  private readonly _webviews = new Set<WebviewMessagePort>();
  private readonly _disposables: Array<{ dispose(): void }> = [];
  private _isDisposed = false;

  public static initialize(
    supervisor: EngineSupervisor,
    protocolChannel: Pick<vscode.OutputChannel, 'appendLine'>
  ): HostRpcBroker {
    if (!HostRpcBroker._instance || HostRpcBroker._instance._isDisposed) {
      HostRpcBroker._instance = new HostRpcBroker(supervisor, protocolChannel);
    }
    return HostRpcBroker._instance;
  }

  public static getInstance(): HostRpcBroker | null {
    return HostRpcBroker._instance;
  }

  public constructor(
    private readonly supervisor: EngineSupervisor,
    private readonly protocolChannel: Pick<vscode.OutputChannel, 'appendLine'>
  ) {
    this.registerBuiltInMethods();

    if (this.supervisor.onDidChangeStatus) {
      const sub = this.supervisor.onDidChangeStatus((status: EngineSupervisorStatus) => {
        this.broadcastEvent(HostRpcBroker.TOPIC_ENGINE_STATUS, status);
      });
      this._disposables.push(sub);
    }
  }

  public registerMethod<TParams = any, TResult = any>(
    method: string,
    handler: RpcMethodHandler<TParams, TResult>,
    options?: { requiresEngine?: boolean }
  ): void {
    this._methods.set(method, {
      handler,
      requiresEngine: options?.requiresEngine ?? true,
    });
  }

  /**
   * Attaches a VS Code Webview (`panel.webview` or `webviewView.webview`) to the broker.
   * Non-RPC messages are ignored so existing message handlers continue to work side-by-side.
   */
  public attachWebview(webview: WebviewMessagePort): { dispose(): void } {
    this._webviews.add(webview);
    const listenerSub = webview.onDidReceiveMessage((msg: any) => {
      if (msg && msg.type === 'RPC_REQUEST' && typeof msg.id === 'string' && typeof msg.method === 'string') {
        void this.handleIncomingMessage(msg as RpcRequestMessage, webview);
      }
    });

    return {
      dispose: () => {
        this._webviews.delete(webview);
        listenerSub.dispose();
      },
    };
  }

  /**
   * Broadcasts a typed `RPC_EVENT` to all currently attached Webviews.
   */
  public broadcastEvent<TPayload = any>(topic: string, payload: TPayload): void {
    if (this._isDisposed) return;
    const message: RpcEventMessage<TPayload> = {
      type: 'RPC_EVENT',
      topic,
      payload,
    };
    const bytes = payload !== undefined ? Buffer.byteLength(JSON.stringify(payload)) : 0;
    this.protocolChannel.appendLine(`[Protocol] [RPC_EVENT ${topic}] (${bytes}B) -> ${this._webviews.size} webview(s)`);
    for (const wv of Array.from(this._webviews)) {
      try {
        void wv.postMessage(message);
      } catch {
        // Ignore disposed webview
      }
    }
  }

  /**
   * Executes an `RPC_REQUEST` and returns its `RPC_RESPONSE`.
   * If `requiresEngine` is true and the engine is `Idle` or `Starting`, waits on `supervisor.ensureReady()`.
   */
  public async dispatch<TResult = any>(
    request: RpcRequestMessage,
    webview?: WebviewMessagePort
  ): Promise<RpcResponseMessage<TResult>> {
    const startMs = Date.now();
    const { id, method, params } = request;
    const entry = this._methods.get(method);

    if (!entry) {
      const errMsg = `Unknown RPC method: '${method}'`;
      this.protocolChannel.appendLine(`[Protocol] [RPC ${id}] ${method} -> ERR_METHOD_NOT_FOUND`);
      return {
        type: 'RPC_RESPONSE',
        id,
        ok: false,
        error: {
          code: 'ERR_METHOD_NOT_FOUND',
          message: errMsg,
        },
      };
    }

    const reqBytes = params !== undefined ? Buffer.byteLength(JSON.stringify(params)) : 0;
    this.protocolChannel.appendLine(
      `[Protocol] [RPC ${id}] -> ${method}${reqBytes > 0 ? ` (${reqBytes}B)` : ''}`
    );

    try {
      let serverInfo: ServerInfo | null = this.supervisor.getServerInfo();
      if (entry.requiresEngine) {
        serverInfo = await this.supervisor.ensureReady();
      }

      const data = (await entry.handler(params, {
        supervisor: this.supervisor,
        serverInfo,
        webview,
      })) as TResult;

      const durationMs = Date.now() - startMs;
      const resBytes = data !== undefined ? Buffer.byteLength(JSON.stringify(data)) : 0;
      this.protocolChannel.appendLine(
        `[Protocol] [RPC ${id}] <- ${method} OK [${durationMs}ms, ${resBytes}B]`
      );

      return {
        type: 'RPC_RESPONSE',
        id,
        ok: true,
        data,
      };
    } catch (err: any) {
      const durationMs = Date.now() - startMs;
      const message = err?.message || String(err);
      this.protocolChannel.appendLine(
        `[Protocol] [RPC ${id}] <- ${method} ERROR [${durationMs}ms]: ${message}`
      );
      return {
        type: 'RPC_RESPONSE',
        id,
        ok: false,
        error: {
          code: err?.code || 'ERR_RPC_EXECUTION',
          message,
          details: err?.stack || message,
        },
      };
    }
  }

  private async handleIncomingMessage(
    request: RpcRequestMessage,
    webview: WebviewMessagePort
  ): Promise<void> {
    const response = await this.dispatch(request, webview);
    try {
      await webview.postMessage(response);
    } catch {
      // Webview was disposed before response completed
    }
  }

  private registerBuiltInMethods(): void {
    // 1. Generic HTTP GET proxy through EngineSupervisor
    this.registerMethod<{ path: string; timeoutMs?: number }>(
      HostRpcBroker.RPC_HTTP_GET,
      async (params, { supervisor }) => {
        if (!params?.path) {
          throw new Error('RPC http.get requires a "path" parameter.');
        }
        return supervisor.request(HostRpcBroker.HTTP_GET, params.path, undefined, {
          timeoutMs: params.timeoutMs,
        });
      },
      { requiresEngine: true }
    );

    // 2. Generic HTTP POST proxy through EngineSupervisor
    this.registerMethod<{ path: string; body?: unknown; timeoutMs?: number }>(
      HostRpcBroker.RPC_HTTP_POST,
      async (params, { supervisor }) => {
        if (!params?.path) {
          throw new Error('RPC http.post requires a "path" parameter.');
        }
        return supervisor.request(HostRpcBroker.HTTP_POST, params.path, params.body, {
          timeoutMs: params.timeoutMs,
        });
      },
      { requiresEngine: true }
    );

    // 3. Diagram projection fetch (queued automatically via ensureReady)
    this.registerMethod<DiagramFetchParams>(
      HostRpcBroker.RPC_DIAGRAM_FETCH,
      async (params, { supervisor }) => {
        const apiPath = buildDiagramApiPath(params);
        return supervisor.request(HostRpcBroker.HTTP_GET, apiPath, undefined, {
          timeoutMs: params?.timeoutMs,
        });
      },
      { requiresEngine: true }
    );

    // 4. Engine status snapshot (does not force engine start unless autoStart: true)
    this.registerMethod<{ autoStart?: boolean } | undefined, EngineSupervisorStatus>(
      HostRpcBroker.RPC_ENGINE_STATUS,
      async (params, { supervisor }) => {
        if (params?.autoStart) {
          await supervisor.ensureReady();
        }
        return supervisor.getStatus();
      },
      { requiresEngine: false }
    );

    // 5. Explicit user retry from Failed state
    this.registerMethod<{ workspaceRoot?: string } | undefined, ServerInfo>(
      HostRpcBroker.RPC_ENGINE_RETRY,
      async (params, { supervisor }) => {
        return supervisor.retry(params?.workspaceRoot);
      },
      { requiresEngine: false }
    );
  }

  public dispose(): void {
    this._isDisposed = true;
    this._webviews.clear();
    while (this._disposables.length > 0) {
      this._disposables.pop()?.dispose();
    }
  }
}

const QUERY_PARAM_TYPE = 'type';
const QUERY_PARAM_PROJECT = 'project';
const QUERY_PARAM_SERVICE = 'service';
const QUERY_PARAM_TARGET = 'target';

export function buildDiagramApiPath(params?: DiagramFetchParams): string {
  if (params?.path) {
    return params.path.startsWith('/') ? params.path : `/${params.path}`;
  }
  const search = new URLSearchParams();
  if (params?.viewType) search.set(QUERY_PARAM_TYPE, params.viewType);
  if (params?.project) search.set(QUERY_PARAM_PROJECT, params.project);
  if (params?.service) search.set(QUERY_PARAM_SERVICE, params.service);
  if (params?.target) search.set(QUERY_PARAM_TARGET, params.target);
  if (params?.query) {
    for (const [k, v] of Object.entries(params.query)) {
      if (v !== undefined && v !== null) {
        search.set(k, String(v));
      }
    }
  }
  const qs = search.toString();
  return qs ? `/api/view?${qs}` : '/api/view';
}
