import { getVsCodeApi } from '../vscodeApi';
import type {
  DiagramFetchParams,
  RpcEventMessage,
  RpcRequestMessage,
  RpcResponseMessage,
} from '../../rpc/hostRpcBroker';

const RPC_METHOD_HTTP_GET = 'http.get';
const RPC_METHOD_HTTP_POST = 'http.post';
const RPC_METHOD_DIAGRAM_FETCH = 'diagram.fetch';

export interface WebviewRpcTransportAdapter {
  postMessage: (message: any) => void;
  onMessage: (listener: (message: any) => void) => () => void;
}

interface PendingRpcRequest {
  resolve: (value: any) => void;
  reject: (reason: Error) => void;
  timer: ReturnType<typeof setTimeout>;
}

/**
 * WebviewRpcClient — Typed RPC client running inside a VS Code Webview.
 *
 * All requests travel over `vscode.postMessage` to `HostRpcBroker` in the Extension Host.
 * Webviews never need to open direct browser WebSockets or wait for dynamic localhost ports:
 * requests sent while the engine is `Idle` or `Starting` are automatically queued by `HostRpcBroker`
 * behind `EngineSupervisor.ensureReady()` and resolved once `ce serve` is ready.
 */
export class WebviewRpcClient {
  private static _instance: WebviewRpcClient | null = null;

  private _seq = 0;
  private readonly _pending = new Map<string, PendingRpcRequest>();
  private readonly _eventListeners = new Map<string, Set<(payload: any) => void>>();
  private readonly _unsubMessage: () => void;
  private readonly _adapter: WebviewRpcTransportAdapter;

  public static getInstance(): WebviewRpcClient {
    if (!WebviewRpcClient._instance) {
      WebviewRpcClient._instance = new WebviewRpcClient();
    }
    return WebviewRpcClient._instance;
  }

  public constructor(adapter?: WebviewRpcTransportAdapter) {
    this._adapter = adapter ?? createDefaultBrowserAdapter();
    this._unsubMessage = this._adapter.onMessage((msg) => {
      this.handleHostMessage(msg);
    });
  }

  /**
   * Sends a typed `RPC_REQUEST` to `HostRpcBroker` and awaits the matching `RPC_RESPONSE`.
   */
  public request<TResult = any, TParams = any>(
    method: string,
    params?: TParams,
    timeoutMs = 60000
  ): Promise<TResult> {
    const id = `rpc_${++this._seq}_${Date.now()}`;
    const payload: RpcRequestMessage<TParams> = {
      type: 'RPC_REQUEST',
      id,
      method,
      params,
    };

    return new Promise<TResult>((resolve, reject) => {
      const timer = setTimeout(() => {
        this._pending.delete(id);
        reject(new Error(`RPC request '${method}' (${id}) timed out after ${timeoutMs}ms.`));
      }, timeoutMs);
      (timer as any)?.unref?.();

      this._pending.set(id, { resolve, reject, timer });

      try {
        this._adapter.postMessage(payload);
      } catch (err: any) {
        clearTimeout(timer);
        this._pending.delete(id);
        reject(new Error(`Failed to post RPC request '${method}': ${err?.message || err}`));
      }
    });
  }

  /**
   * Performs a typed HTTP GET through the Extension Host RPC broker.
   */
  public get<TResult = any>(path: string, timeoutMs?: number): Promise<TResult> {
    return this.request<TResult>(RPC_METHOD_HTTP_GET, { path, timeoutMs }, timeoutMs);
  }

  /**
   * Performs a typed HTTP POST through the Extension Host RPC broker.
   */
  public post<TResult = any>(path: string, body?: unknown, timeoutMs?: number): Promise<TResult> {
    return this.request<TResult>(RPC_METHOD_HTTP_POST, { path, body, timeoutMs }, timeoutMs);
  }

  /**
   * Fetches a diagram projection (`/api/view?...`) via `diagram.fetch` RPC.
   */
  public fetchDiagram<TResult = any>(
    params: DiagramFetchParams,
    timeoutMs?: number
  ): Promise<TResult> {
    return this.request<TResult>(RPC_METHOD_DIAGRAM_FETCH, params, timeoutMs);
  }

  /**
   * Subscribes to `RPC_EVENT` broadcasts emitted by `HostRpcBroker` (e.g. `'engine:status'`, `'scan:progress'`).
   */
  public onEvent<TPayload = any>(
    topic: string,
    listener: (payload: TPayload) => void
  ): () => void {
    let set = this._eventListeners.get(topic);
    if (!set) {
      set = new Set();
      this._eventListeners.set(topic, set);
    }
    set.add(listener);
    return () => {
      set?.delete(listener);
      if (set && set.size === 0) {
        this._eventListeners.delete(topic);
      }
    };
  }

  private handleHostMessage(msg: any): void {
    if (!msg || typeof msg !== 'object') {
      return;
    }

    if (msg.type === 'RPC_RESPONSE' && typeof msg.id === 'string') {
      const res = msg as RpcResponseMessage;
      const pending = this._pending.get(res.id);
      if (!pending) {
        return;
      }
      clearTimeout(pending.timer);
      this._pending.delete(res.id);

      if (res.ok) {
        pending.resolve(res.data);
      } else {
        const err = new Error(res.error?.message || 'RPC request failed');
        (err as any).code = res.error?.code;
        (err as any).details = res.error?.details;
        pending.reject(err);
      }
      return;
    }

    if (msg.type === 'RPC_EVENT' && typeof msg.topic === 'string') {
      const evt = msg as RpcEventMessage;
      const listeners = this._eventListeners.get(evt.topic);
      if (!listeners || listeners.size === 0) {
        return;
      }
      for (const listener of Array.from(listeners)) {
        try {
          listener(evt.payload);
        } catch {
          // Ignore listener error
        }
      }
    }
  }

  public dispose(): void {
    this._unsubMessage();
    for (const [, pending] of this._pending) {
      clearTimeout(pending.timer);
      pending.reject(new Error('WebviewRpcClient disposed.'));
    }
    this._pending.clear();
    this._eventListeners.clear();
  }
}

function createDefaultBrowserAdapter(): WebviewRpcTransportAdapter {
  return {
    postMessage: (message: any) => {
      const api = getVsCodeApi();
      if (!api) {
        throw new Error('VS Code Webview API is not available.');
      }
      api.postMessage(message);
    },
    onMessage: (listener: (message: any) => void) => {
      if (typeof window === 'undefined' || typeof window.addEventListener !== 'function') {
        return () => {};
      }
      const handler = (event: MessageEvent) => {
        listener(event.data);
      };
      window.addEventListener('message', handler);
      return () => window.removeEventListener('message', handler);
    },
  };
}

export const webviewRpcClient = WebviewRpcClient.getInstance();
