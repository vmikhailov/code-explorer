import { useState, useEffect } from 'react';
import { WebSocketMessage, HandshakeRequest } from '../../../../proto/types';
import { getVsCodeApi } from '../vscodeApi';

export type WebviewConnectionState =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'
  | 'error';

export interface TransportUrls {
  wsUrl: string;
  httpUrl: string;
}

/**
 * Normalizes a WebSocket URL into an HTTP base URL.
 * e.g. ws://127.0.0.1:54321/ws -> http://127.0.0.1:54321
 */
export function deriveHttpUrl(wsUrl?: string): string {
  if (!wsUrl) return '';
  return wsUrl
    .replace(/^ws:\/\//i, 'http://')
    .replace(/^wss:\/\//i, 'https://')
    .replace(/\/ws$/i, '');
}

export interface WebviewServerTransportOptions {
  wsFactory?: (url: string) => WebSocket;
}

/**
 * WebviewServerTransport
 * Single shared transport layer for all Webview components (App, DomainGatesPanel, GateEditorView).
 * Encapsulates WebSocket connection lifecycle, keepalive pings, reconnect backoff,
 * URL synchronization, and unified JSON REST fetcher.
 */
export class WebviewServerTransport {
  public static readonly MAX_RECONNECT_ATTEMPTS = 8;
  private static _instance: WebviewServerTransport | null = null;

  private _wsUrl = '';
  private _httpUrl = '';
  private _workspaceRoot = '';
  private _connectionState: WebviewConnectionState = 'connecting';
  private _ws: WebSocket | null = null;
  private _reconnectAttempt = 0;
  private _reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private _keepAliveTimer: ReturnType<typeof setInterval> | null = null;
  private _isManuallyClosed = false;
  private _wsFactory?: (url: string) => WebSocket;

  private readonly _urlListeners = new Set<(urls: TransportUrls) => void>();
  private readonly _stateListeners = new Set<(state: WebviewConnectionState) => void>();
  private readonly _messageListeners = new Set<(msg: WebSocketMessage) => void>();
  private readonly _errorListeners = new Set<(error: any) => void>();

  public static getInstance(): WebviewServerTransport {
    if (!WebviewServerTransport._instance) {
      WebviewServerTransport._instance = new WebviewServerTransport();
    }
    return WebviewServerTransport._instance;
  }

  public constructor(options?: WebviewServerTransportOptions) {
    if (options?.wsFactory) {
      this._wsFactory = options.wsFactory;
    }
    if (typeof window !== 'undefined') {
      const config = window.__CE_CONFIG__;
      if (config?.wsUrl) {
        this._wsUrl = config.wsUrl;
        this._httpUrl = deriveHttpUrl(config.wsUrl);
      }
      if (config?.workspaceRoot) {
        this._workspaceRoot = config.workspaceRoot;
      }
      this.setupWindowListener();
    }
  }

  public setWsFactory(factory?: (url: string) => WebSocket): void {
    this._wsFactory = factory;
  }

  public getWsUrl(): string {
    return this._wsUrl;
  }

  public getHttpUrl(): string {
    return this._httpUrl || deriveHttpUrl(this._wsUrl);
  }

  public getConnectionState(): WebviewConnectionState {
    return this._connectionState;
  }

  public getWorkspaceRoot(): string {
    return this._workspaceRoot;
  }

  public setWorkspaceRoot(root: string): void {
    this._workspaceRoot = root;
  }

  public updateUrls(wsUrl: string): void {
    if (!wsUrl) return;
    const changed = this._wsUrl !== wsUrl;
    this._wsUrl = wsUrl;
    this._httpUrl = deriveHttpUrl(wsUrl);

    if (typeof window !== 'undefined') {
      if (!window.__CE_CONFIG__) {
        window.__CE_CONFIG__ = {};
      }
      window.__CE_CONFIG__.wsUrl = wsUrl;
    }

    if (changed) {
      const urls: TransportUrls = { wsUrl: this._wsUrl, httpUrl: this._httpUrl };
      for (const listener of Array.from(this._urlListeners)) {
        try {
          listener(urls);
        } catch (err) {
          console.error('[WebviewServerTransport] Error in url listener:', err);
        }
      }
    }
  }

  private setConnectionState(state: WebviewConnectionState): void {
    if (this._connectionState === state) return;
    this._connectionState = state;
    for (const listener of Array.from(this._stateListeners)) {
      try {
        listener(state);
      } catch (err) {
        console.error('[WebviewServerTransport] Error in state listener:', err);
      }
    }
  }

  private setupWindowListener(): void {
    if (typeof window === 'undefined') return;

    window.addEventListener('message', (event: MessageEvent) => {
      const msg = event.data;
      if (!msg || typeof msg !== 'object') return;

      if (msg.type === 'UPDATE_WS_URL' || msg.type === 'SERVER_CONFIG') {
        if (msg.workspaceRoot) {
          this._workspaceRoot = msg.workspaceRoot;
        }
        if (msg.wsUrl) {
          const urlChanged = this._wsUrl !== msg.wsUrl;
          this.updateUrls(msg.wsUrl);
          if (
            urlChanged ||
            !this._ws ||
            this._ws.readyState === WebSocket.CLOSED ||
            this._ws.readyState === WebSocket.CLOSING
          ) {
            this.connect(msg.wsUrl);
          }
        }
      }
    });
  }

  public connect(url?: string): void {
    const targetUrl = url || this._wsUrl;
    if (!targetUrl) return;

    this.clearReconnectTimer();

    if (this._ws) {
      this._isManuallyClosed = true;
      try {
        this._ws.close();
      } catch {}
      this._ws = null;
    }
    this._isManuallyClosed = false;

    this.updateUrls(targetUrl);
    this.setConnectionState(this._reconnectAttempt > 0 ? 'reconnecting' : 'connecting');

    try {
      let ws: WebSocket | null = null;
      if (this._wsFactory) {
        ws = this._wsFactory(targetUrl);
      } else if (typeof window !== 'undefined' && typeof WebSocket !== 'undefined') {
        ws = new WebSocket(targetUrl);
      } else {
        // In Node.js environment (e.g. unit tests without a mock wsFactory), do not instantiate native WebSocket
        return;
      }
      this._ws = ws;

      ws.onopen = () => {
        if (this._ws !== ws) return;
        this._reconnectAttempt = 0;
        this.setConnectionState('connected');
        this.startKeepAlive();

        // Send initial handshake request
        const handshake: WebSocketMessage<HandshakeRequest> = {
          type: 'HANDSHAKE_REQUEST',
          requestId: `req_handshake_${Date.now()}`,
          payload: {
            clientVersion: '0.1.0',
            clientName: 'vscode-extension',
            workspacePath: this._workspaceRoot,
          },
        };
        this.send(handshake);
      };

      ws.onmessage = (event: MessageEvent) => {
        if (this._ws !== ws) return;
        try {
          const parsed = JSON.parse(event.data) as WebSocketMessage;
          for (const listener of Array.from(this._messageListeners)) {
            try {
              listener(parsed);
            } catch (err) {
              console.error('[WebviewServerTransport] Error in message listener:', err);
            }
          }
        } catch (err) {
          console.error('[WebviewServerTransport] Failed to parse message JSON:', err);
        }
      };

      ws.onerror = (err) => {
        if (this._ws !== ws) return;
        this.setConnectionState('error');
        for (const listener of Array.from(this._errorListeners)) {
          try {
            listener(err);
          } catch {}
        }
        this.requestReconnectFromHost();
      };

      ws.onclose = (ev: CloseEvent) => {
        if (this._ws !== ws) return;
        this.stopKeepAlive();
        this._ws = null;

        if (this._isManuallyClosed || ev.code === 1000) {
          this.setConnectionState('disconnected');
          return;
        }

        const attempt = this._reconnectAttempt + 1;
        this._reconnectAttempt = attempt;
        if (this._reconnectAttempt >= WebviewServerTransport.MAX_RECONNECT_ATTEMPTS) {
          this.setConnectionState('error');
          return;
        }

        const delay = Math.min(1000 * Math.pow(1.5, Math.min(attempt, 8)), 10000);

        this.setConnectionState('reconnecting');
        this.requestReconnectFromHost();

        this._reconnectTimer = setTimeout(() => {
          if (this._wsUrl) {
            this.connect(this._wsUrl);
          }
        }, delay);
        (this._reconnectTimer as any)?.unref?.();
      };
    } catch {
      this.setConnectionState('error');
      this.requestReconnectFromHost();
    }
  }

  public reconnectNow(): void {
    this.clearReconnectTimer();
    if (this._ws) {
      this._isManuallyClosed = true;
      try {
        this._ws.close();
      } catch {}
      this._ws = null;
    }
    this._isManuallyClosed = false;
    this._reconnectAttempt = 0;
    this.setConnectionState('reconnecting');
    this.requestReconnectFromHost();
    if (this._wsUrl) {
      this.connect(this._wsUrl);
    }
  }

  public send(msg: WebSocketMessage): boolean {
    if (this._ws && this._ws.readyState === WebSocket.OPEN) {
      try {
        this._ws.send(JSON.stringify(msg));
        return true;
      } catch (err) {
        console.error('[WebviewServerTransport] Error sending WS message:', err);
        return false;
      }
    }
    return false;
  }

  public async fetchJson<T>(relativePath: string, init?: RequestInit): Promise<T> {
    const baseUrl = this.getHttpUrl();
    if (!baseUrl) {
      throw new Error('CodeExplorer server HTTP endpoint is not available.');
    }
    const cleanPath = relativePath.startsWith('/') ? relativePath : `/${relativePath}`;
    const url = `${baseUrl}${cleanPath}`;
    const headers: Record<string, string> = {
      Accept: 'application/json',
      ...((init?.headers as Record<string, string>) || {}),
    };
    const res = await fetch(url, {
      ...init,
      headers,
    });
    if (!res.ok) {
      const errorText = await res.text().catch(() => '');
      throw new Error(`HTTP ${res.status}: ${res.statusText}${errorText ? ` - ${errorText}` : ''}`);
    }
    return (await res.json()) as T;
  }

  public onUrlChange(listener: (urls: TransportUrls) => void): () => void {
    this._urlListeners.add(listener);
    return () => this._urlListeners.delete(listener);
  }

  public onConnectionStateChange(listener: (state: WebviewConnectionState) => void): () => void {
    this._stateListeners.add(listener);
    return () => this._stateListeners.delete(listener);
  }

  public onMessage(listener: (msg: WebSocketMessage) => void): () => void {
    this._messageListeners.add(listener);
    return () => this._messageListeners.delete(listener);
  }

  public onError(listener: (error: any) => void): () => void {
    this._errorListeners.add(listener);
    return () => this._errorListeners.delete(listener);
  }

  public close(): void {
    this.clearReconnectTimer();
    this.stopKeepAlive();
    this._isManuallyClosed = true;
    if (this._ws) {
      try {
        this._ws.close(1000);
      } catch {}
      this._ws = null;
    }
    this.setConnectionState('disconnected');
  }

  public disconnect(): void {
    this.close();
  }

  public dispose(): void {
    this.close();
  }

  private clearReconnectTimer(): void {
    if (this._reconnectTimer) {
      clearTimeout(this._reconnectTimer);
      this._reconnectTimer = null;
    }
  }

  private startKeepAlive(): void {
    this.stopKeepAlive();
    this._keepAliveTimer = setInterval(() => {
      if (this._ws && this._ws.readyState === WebSocket.OPEN) {
        try {
          this._ws.send(
            JSON.stringify({
              type: 'PING_REQUEST',
              requestId: `ping_${Date.now()}`,
              payload: { timestamp: Date.now() },
            })
          );
        } catch {}
      }
    }, 25000);
    (this._keepAliveTimer as any)?.unref?.();
  }

  private stopKeepAlive(): void {
    if (this._keepAliveTimer) {
      clearInterval(this._keepAliveTimer);
      this._keepAliveTimer = null;
    }
  }

  private requestReconnectFromHost(): void {
    const api = getVsCodeApi();
    if (api) {
      try {
        api.postMessage({ type: 'REQUEST_RECONNECT' });
        api.postMessage({ type: 'REQUEST_SERVER_RECONNECT' });
      } catch {}
    }
  }
}

export const webviewTransport = WebviewServerTransport.getInstance();

export function useServerTransport(): {
  wsUrl: string;
  httpUrl: string;
  connectionState: WebviewConnectionState;
  transport: WebviewServerTransport;
} {
  const [urls, setUrls] = useState<TransportUrls>(() => ({
    wsUrl: webviewTransport.getWsUrl(),
    httpUrl: webviewTransport.getHttpUrl(),
  }));
  const [connectionState, setConnectionState] = useState<WebviewConnectionState>(() =>
    webviewTransport.getConnectionState()
  );

  useEffect(() => {
    setUrls({
      wsUrl: webviewTransport.getWsUrl(),
      httpUrl: webviewTransport.getHttpUrl(),
    });
    setConnectionState(webviewTransport.getConnectionState());

    const unsubUrl = webviewTransport.onUrlChange(setUrls);
    const unsubState = webviewTransport.onConnectionStateChange(setConnectionState);

    return () => {
      unsubUrl();
      unsubState();
    };
  }, []);

  return {
    wsUrl: urls.wsUrl,
    httpUrl: urls.httpUrl,
    connectionState,
    transport: webviewTransport,
  };
}
