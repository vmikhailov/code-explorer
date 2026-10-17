import * as vscode from 'vscode';
import * as net from 'net';
import * as http from 'http';
import { ProcessManager, ServerInfo } from './processManager';

export type TransportState =
  | 'disconnected'
  | 'starting'
  | 'connected'
  | 'reconnecting'
  | 'error';

export interface TransportEndpoints {
  port: number;
  httpUrl: string;
  wsUrl: string;
}

export interface TransportStateSnapshot {
  state: TransportState;
  endpoints: TransportEndpoints | null;
  serverInfo: ServerInfo | null;
  reconnectAttempt: number;
  error: string | null;
}

export interface TransportRequestOptions {
  /**
   * Whether to automatically start/reconnect the server if not currently running.
   * Defaults to true.
   */
  autoStart?: boolean;
  /**
   * Request timeout in milliseconds. Defaults to 30000 (30s).
   */
  timeoutMs?: number;
  /**
   * Optional HTTP headers.
   */
  headers?: Record<string, string>;
  /**
   * Explicit workspace root override.
   */
  workspaceRoot?: string;
}

export type TopicListener<T = any> = (payload: T) => void;

/**
 * Allocates an available TCP port on 127.0.0.1 from the OS ephemeral range.
 */
export function allocateFreePort(host = '127.0.0.1'): Promise<number> {
  return new Promise((resolve, reject) => {
    const srv = net.createServer();
    srv.unref();
    srv.on('error', reject);
    srv.listen(0, host, () => {
      const addr = srv.address();
      if (addr && typeof addr === 'object' && addr.port > 0) {
        const port = addr.port;
        srv.close(() => resolve(port));
      } else {
        srv.close(() => reject(new Error('Failed to allocate an ephemeral TCP port.')));
      }
    });
  });
}

/**
 * Checks whether a specific TCP port on 127.0.0.1 is currently free to bind.
 */
export function isPortAvailable(port: number, host = '127.0.0.1'): Promise<boolean> {
  if (!port || port <= 0) {
    return Promise.resolve(false);
  }
  return new Promise((resolve) => {
    const srv = net.createServer();
    srv.unref();
    srv.once('error', () => resolve(false));
    srv.listen(port, host, () => {
      srv.close(() => resolve(true));
    });
  });
}

/**
 * Low-level HTTP JSON request helper with timeout support.
 */
export function executeHttpRequest<T>(
  urlStr: string,
  method: 'GET' | 'POST',
  body?: any,
  headers?: Record<string, string>,
  timeoutMs = 30000
): Promise<T> {
  const url = new URL(urlStr);
  const payloadStr =
    body !== undefined
      ? typeof body === 'string'
        ? body
        : JSON.stringify(body)
      : undefined;

  const reqHeaders: Record<string, string> = {
    Accept: 'application/json',
    ...headers,
  };
  if (payloadStr !== undefined && !reqHeaders['Content-Type']) {
    reqHeaders['Content-Type'] = 'application/json';
    reqHeaders['Content-Length'] = Buffer.byteLength(payloadStr).toString();
  }

  return new Promise<T>((resolve, reject) => {
    const req = http.request(
      {
        hostname: url.hostname,
        port: url.port,
        path: url.pathname + url.search,
        method,
        headers: reqHeaders,
        timeout: timeoutMs,
      },
      (res) => {
        let rawBody = '';
        res.on('data', (chunk) => {
          rawBody += chunk;
        });
        res.on('end', () => {
          if (res.statusCode && res.statusCode >= 400) {
            reject(new Error(`HTTP ${res.statusCode}: ${rawBody || res.statusMessage}`));
            return;
          }
          if (!rawBody || rawBody.trim().length === 0) {
            resolve({} as T);
            return;
          }
          try {
            resolve(JSON.parse(rawBody) as T);
          } catch (err) {
            reject(err);
          }
        });
      }
    );

    req.on('timeout', () => {
      req.destroy(new Error(`Request timed out after ${timeoutMs}ms (${method} ${url.pathname})`));
    });

    req.on('error', reject);

    if (payloadStr !== undefined) {
      req.write(payloadStr);
    }
    req.end();
  });
}

function isTransientNetworkError(err: any): boolean {
  const msg = String(err?.message || err || '').toLowerCase();
  const code = String(err?.code || '').toUpperCase();
  return (
    code === 'ECONNREFUSED' ||
    code === 'ECONNRESET' ||
    code === 'EPIPE' ||
    msg.includes('econnrefused') ||
    msg.includes('econnreset') ||
    msg.includes('socket hang up') ||
    msg.includes('fetch failed')
  );
}

/**
 * ServerTransport - Unified transport layer, sticky port allocator, pub/sub event bus,
 * and health watchdog between the VS Code Extension and the CodeExplorer server.
 *
 * Key Architectural Guarantees:
 * 1. Sticky Port Pinning: Allocates one TCP port for the lifetime of the extension window.
 *    Server restarts reuse the same port so Webviews and REST clients never lose their endpoint.
 * 2. Pub/Sub Event Bus: Centralizes state transitions and topic notifications across all UI panels.
 * 3. Watchdog & Auto-Heal: Actively monitors server health and automatically restarts crashed
 *    or terminated server processes with exponential backoff and circuit-breaker protection.
 * 4. Unified HTTP Client: All REST requests flow through `get`/`post` with single-flight auto-start
 *    and transparent retry on transient socket errors.
 */
export class ServerTransport implements vscode.Disposable {
  private static _instance: ServerTransport | null = null;

  private _state: TransportState = 'disconnected';
  private _allocatedPort: number | null = null;
  private _portPromise: Promise<number> | null = null;
  private _connectPromise: Promise<ServerInfo> | null = null;

  private _reconnectAttempt = 0;
  private _reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private _watchdogTimer: ReturnType<typeof setInterval> | null = null;
  private _consecutiveHealthFailures = 0;
  private _isHealthChecking = false;

  private _restartTimestamps: number[] = [];
  private _lastError: string | null = null;
  private _isDisposed = false;

  private readonly _topics = new Map<string, Set<TopicListener>>();
  private readonly _disposables: vscode.Disposable[] = [];

  private readonly _onDidChangeState = new vscode.EventEmitter<TransportStateSnapshot>();
  public readonly onDidChangeState = this._onDidChangeState.event;

  private readonly _onDidServerStart = new vscode.EventEmitter<ServerInfo>();
  public readonly onDidServerStart = this._onDidServerStart.event;

  private readonly _onDidServerStop = new vscode.EventEmitter<void>();
  public readonly onDidServerStop = this._onDidServerStop.event;

  private static readonly WATCHDOG_INTERVAL_MS = 15000;
  private static readonly HEALTH_CHECK_TIMEOUT_MS = 4000;
  private static readonly MAX_CONSECUTIVE_HEALTH_FAILURES = 2;
  private static readonly CIRCUIT_BREAKER_WINDOW_MS = 60000;
  private static readonly CIRCUIT_BREAKER_MAX_RESTARTS = 5;

  public static initialize(
    processManager: ProcessManager,
    getWorkspaceRoot: () => string | undefined,
    outputChannel: vscode.OutputChannel
  ): ServerTransport {
    if (!ServerTransport._instance || ServerTransport._instance._isDisposed) {
      ServerTransport._instance = new ServerTransport(processManager, getWorkspaceRoot, outputChannel);
    }
    return ServerTransport._instance;
  }

  public static getInstance(): ServerTransport {
    if (!ServerTransport._instance) {
      throw new Error('ServerTransport has not been initialized yet.');
    }
    return ServerTransport._instance;
  }

  public constructor(
    private readonly processManager: ProcessManager,
    private readonly getWorkspaceRoot: () => string | undefined,
    private readonly outputChannel: vscode.OutputChannel
  ) {
    this._disposables.push(
      this._onDidChangeState,
      this._onDidServerStart,
      this._onDidServerStop
    );

    // Subscribe to underlying process manager events
    this._disposables.push(
      this.processManager.onDidServerStart((info) => {
        if (info.port && info.port !== this._allocatedPort) {
          this._allocatedPort = info.port;
        }
        this._reconnectAttempt = 0;
        this._consecutiveHealthFailures = 0;
        this._lastError = null;
        this.clearReconnectTimer();
        this.setState('connected');
        this.startWatchdog();
        this._onDidServerStart.fire(info);
        this.publish('server:ready', info);
      }),
      this.processManager.onDidServerStop(() => {
        this._onDidServerStop.fire();
        this.publish('server:stopped', undefined);
      }),
      this.processManager.onDidUnexpectedExit(({ code, signal }) => {
        if (this._isDisposed) return;
        this.outputChannel.appendLine(
          `[Transport] Detected unexpected server exit (code: ${code}, signal: ${signal}). Initiating recovery...`
        );
        this.handleUnexpectedDisconnect(`Server process exited unexpectedly (code ${code})`);
      })
    );
  }

  public getProcessManager(): ProcessManager {
    return this.processManager;
  }

  // ---------------------------------------------------------------------------
  // 1. Pub/Sub Subscription Mechanism
  // ---------------------------------------------------------------------------

  /**
   * Subscribes to a named transport topic (e.g. 'server:ready', 'server:state', 'gates:updated', 'contexts:reload').
   */
  public subscribe<T = any>(topic: string, listener: TopicListener<T>): vscode.Disposable {
    let set = this._topics.get(topic);
    if (!set) {
      set = new Set();
      this._topics.set(topic, set);
    }
    set.add(listener);
    return new vscode.Disposable(() => {
      set?.delete(listener);
      if (set && set.size === 0) {
        this._topics.delete(topic);
      }
    });
  }

  /**
   * Publishes a payload to all subscribers of the given topic.
   */
  public publish<T = any>(topic: string, payload?: T): void {
    const set = this._topics.get(topic);
    if (!set || set.size === 0) return;
    for (const listener of Array.from(set)) {
      try {
        listener(payload);
      } catch (err: any) {
        this.outputChannel.appendLine(
          `[Transport] Error in subscriber for topic '${topic}': ${err?.message || err}`
        );
      }
    }
  }

  // ---------------------------------------------------------------------------
  // 2. Sticky Port Allocation (Lifetime Port Pinning)
  // ---------------------------------------------------------------------------

  /**
   * Returns the port pinned for the lifetime of this extension instance, or null if not yet allocated.
   */
  public getAllocatedPort(): number | null {
    return this._allocatedPort;
  }

  /**
   * Returns the HTTP and WebSocket endpoints for the pinned port.
   */
  public getEndpoints(): TransportEndpoints | null {
    const info = this.processManager.getServerInfo();
    if (info) {
      return {
        port: info.port,
        httpUrl: info.httpUrl,
        wsUrl: info.wsUrl,
      };
    }
    if (this._allocatedPort && this._allocatedPort > 0) {
      return {
        port: this._allocatedPort,
        httpUrl: `http://127.0.0.1:${this._allocatedPort}`,
        wsUrl: `ws://127.0.0.1:${this._allocatedPort}/ws`,
      };
    }
    return null;
  }

  /**
   * Allocates and pins a TCP port once for the entire lifetime of the extension.
   * Subsequent calls always return the same pinned port unless an external process
   * has occupied it while the server was stopped.
   */
  public async ensurePortAllocated(verifyAvailableForSpawn = false): Promise<number> {
    if (this._allocatedPort && this._allocatedPort > 0) {
      if (!verifyAvailableForSpawn || this.processManager.getServerInfo() !== null) {
        return this._allocatedPort;
      }
      // Server is not running; verify the sticky port hasn't been hijacked by another OS process
      const free = await isPortAvailable(this._allocatedPort);
      if (free) {
        return this._allocatedPort;
      }
      this.outputChannel.appendLine(
        `[Transport] Pinned port ${this._allocatedPort} was occupied by an external process while server was stopped. Re-allocating...`
      );
      this._allocatedPort = null;
    }

    if (this._portPromise) {
      return this._portPromise;
    }

    this._portPromise = (async () => {
      try {
        const config = vscode.workspace.getConfiguration('codeExplorer');
        const configuredPort = config?.get<number>('serverPort') ?? 0;
        if (configuredPort > 0) {
          const free = await isPortAvailable(configuredPort);
          if (free) {
            this._allocatedPort = configuredPort;
            this.outputChannel.appendLine(`[Transport] Pinned configured server port: ${configuredPort}`);
            return configuredPort;
          }
          this.outputChannel.appendLine(
            `[Transport] Configured port ${configuredPort} is already in use; allocating a free dynamic port instead.`
          );
        }

        const port = await allocateFreePort('127.0.0.1');
        this._allocatedPort = port;
        this.outputChannel.appendLine(
          `[Transport] Allocated lifetime sticky port: ${port} (http://127.0.0.1:${port})`
        );
        return port;
      } finally {
        this._portPromise = null;
      }
    })();

    return this._portPromise;
  }

  // ---------------------------------------------------------------------------
  // 3. State & Server Liveness Control
  // ---------------------------------------------------------------------------

  public getState(): TransportState {
    return this._state;
  }

  public getSnapshot(): TransportStateSnapshot {
    return {
      state: this._state,
      endpoints: this.getEndpoints(),
      serverInfo: this.processManager.getServerInfo(),
      reconnectAttempt: this._reconnectAttempt,
      error: this._lastError,
    };
  }

  public getServerInfo(): ServerInfo | null {
    return this.processManager.getServerInfo();
  }

  public isStarting(): boolean {
    return this._state === 'starting' || this._state === 'reconnecting' || this.processManager.isStarting();
  }

  public hasWorkspace(workspaceRoot?: string): boolean {
    return this.processManager.hasWorkspace(workspaceRoot ?? this.getWorkspaceRoot());
  }

  private setState(newState: TransportState, error?: string | null): void {
    if (error !== undefined) {
      this._lastError = error;
    }
    const changed = this._state !== newState || error !== undefined;
    this._state = newState;
    if (changed) {
      const snapshot = this.getSnapshot();
      this._onDidChangeState.fire(snapshot);
      this.publish('server:state', snapshot);
    }
  }

  /**
   * Ensures the server is started and responsive on the pinned lifetime port.
   * Deduplicates concurrent calls via a single-flight promise.
   */
  public async ensureConnected(
    workspaceRoot?: string,
    options?: { forceRestart?: boolean; resetCircuitBreaker?: boolean }
  ): Promise<ServerInfo> {
    const root = workspaceRoot ?? this.getWorkspaceRoot();
    if (!root) {
      throw new Error('No workspace folder is currently open.');
    }

    if (options?.resetCircuitBreaker !== false) {
      // Explicit demand resets the crash circuit breaker
      this._restartTimestamps = [];
    }

    if (options?.forceRestart) {
      this.stopWatchdog();
      this.clearReconnectTimer();
      this.processManager.stopServer();
    }

    const currentInfo = this.processManager.getServerInfo();
    if (currentInfo && !options?.forceRestart) {
      if (this._state !== 'connected') {
        this.setState('connected', null);
      }
      return currentInfo;
    }

    if (this._connectPromise) {
      return this._connectPromise;
    }

    this.clearReconnectTimer();
    this.setState(this._reconnectAttempt > 0 ? 'reconnecting' : 'starting', null);

    this._connectPromise = (async () => {
      try {
        const port = await this.ensurePortAllocated(true);
        // Disable self-termination idle timeout (0) because ServerTransport owns the server lifecycle
        const info = await this.processManager.ensureServerStarted(root, {
          port,
          idleTimeout: 0,
        });
        this._allocatedPort = info.port;
        this._reconnectAttempt = 0;
        this._consecutiveHealthFailures = 0;
        this.setState('connected', null);
        this.startWatchdog();
        return info;
      } catch (err: any) {
        const msg = err?.message || String(err);
        this.setState('error', msg);
        throw err;
      } finally {
        this._connectPromise = null;
      }
    })();

    return this._connectPromise;
  }

  /**
   * Verifies that the server is both running and responding to HTTP health probes;
   * if not, immediately restarts/connects on the sticky port.
   */
  public async verifyHealthyConnection(workspaceRoot?: string): Promise<ServerInfo> {
    const currentInfo = this.processManager.getServerInfo();
    if (currentInfo) {
      const healthy = await this.checkServerHealth();
      if (healthy) {
        return currentInfo;
      }
      this.outputChannel.appendLine(
        '[Transport] Server process failed active health verification on reconnect request. Restarting on sticky port...'
      );
      return this.ensureConnected(workspaceRoot, {
        forceRestart: true,
        resetCircuitBreaker: true,
      });
    }
    return this.ensureConnected(workspaceRoot, {
      forceRestart: false,
      resetCircuitBreaker: true,
    });
  }

  /**
   * Explicitly restarts the server on the same sticky port.
   */
  public async restart(workspaceRoot?: string): Promise<ServerInfo> {
    this.outputChannel.appendLine('[Transport] Restarting server on sticky port...');
    return this.ensureConnected(workspaceRoot, {
      forceRestart: true,
      resetCircuitBreaker: true,
    });
  }

  /**
   * Explicitly stops the server and halts watchdog monitoring.
   */
  public stop(): void {
    this.stopWatchdog();
    this.clearReconnectTimer();
    this._reconnectAttempt = 0;
    this.processManager.stopServer();
    this.setState('disconnected', null);
  }

  // ---------------------------------------------------------------------------
  // 4. Watchdog Health Checks & Auto-Restart with Circuit Breaker
  // ---------------------------------------------------------------------------

  private startWatchdog(): void {
    this.stopWatchdog();
    this._watchdogTimer = setInterval(() => {
      void this.performHealthCheck();
    }, ServerTransport.WATCHDOG_INTERVAL_MS);
  }

  private stopWatchdog(): void {
    if (this._watchdogTimer) {
      clearInterval(this._watchdogTimer);
      this._watchdogTimer = null;
    }
  }

  private clearReconnectTimer(): void {
    if (this._reconnectTimer) {
      clearTimeout(this._reconnectTimer);
      this._reconnectTimer = null;
    }
  }

  /**
   * Probes server liveness via HTTP GET / (lightweight metadata endpoint).
   */
  public async checkServerHealth(): Promise<boolean> {
    const info = this.processManager.getServerInfo();
    if (!info) {
      return false;
    }
    try {
      const res = await executeHttpRequest<{ service?: string }>(
        `${info.httpUrl}/`,
        'GET',
        undefined,
        undefined,
        ServerTransport.HEALTH_CHECK_TIMEOUT_MS
      );
      return Boolean(res && res.service);
    } catch {
      return false;
    }
  }

  private async performHealthCheck(): Promise<void> {
    if (this._isDisposed || this._isHealthChecking || this._state !== 'connected') {
      return;
    }

    const info = this.processManager.getServerInfo();
    if (!info) {
      this.handleUnexpectedDisconnect('Server process is no longer active');
      return;
    }

    this._isHealthChecking = true;
    try {
      const healthy = await this.checkServerHealth();
      if (healthy) {
        this._consecutiveHealthFailures = 0;
      } else {
        this._consecutiveHealthFailures++;
        this.outputChannel.appendLine(
          `[Transport] Watchdog health probe failed (${this._consecutiveHealthFailures}/${ServerTransport.MAX_CONSECUTIVE_HEALTH_FAILURES})`
        );
        if (this._consecutiveHealthFailures >= ServerTransport.MAX_CONSECUTIVE_HEALTH_FAILURES) {
          this.handleUnexpectedDisconnect('Server failed consecutive watchdog health probes');
        }
      }
    } finally {
      this._isHealthChecking = false;
    }
  }

  private handleUnexpectedDisconnect(reason: string): void {
    if (this._isDisposed) return;

    this.stopWatchdog();
    this.clearReconnectTimer();

    // Ensure any zombie process is cleaned up before respawning
    if (this.processManager.getServerInfo()) {
      this.processManager.stopServer();
    }

    const root = this.getWorkspaceRoot();
    if (!root || !this.processManager.hasWorkspace(root)) {
      this.setState('disconnected', reason);
      return;
    }

    // Sliding-window circuit breaker check
    const now = Date.now();
    this._restartTimestamps = this._restartTimestamps.filter(
      (ts) => now - ts < ServerTransport.CIRCUIT_BREAKER_WINDOW_MS
    );

    if (this._restartTimestamps.length >= ServerTransport.CIRCUIT_BREAKER_MAX_RESTARTS) {
      const msg = `Server crashed ${this._restartTimestamps.length} times within 60s. Auto-restart paused by circuit breaker.`;
      this.outputChannel.appendLine(`[Transport] ERROR: ${msg}`);
      this.setState('error', msg);
      return;
    }

    this._restartTimestamps.push(now);
    this._reconnectAttempt++;
    const delayMs = Math.min(500 * Math.pow(2, this._reconnectAttempt - 1), 8000);

    this.outputChannel.appendLine(
      `[Transport] Scheduling server auto-restart #${this._reconnectAttempt} on sticky port ${this._allocatedPort ?? 'auto'} in ${delayMs}ms (${reason})...`
    );
    this.setState('reconnecting', reason);

    this._reconnectTimer = setTimeout(async () => {
      this._reconnectTimer = null;
      if (this._isDisposed) return;
      try {
        await this.ensureConnected(root, { resetCircuitBreaker: false });
        this.outputChannel.appendLine(
          `[Transport] Server successfully recovered on sticky port ${this._allocatedPort}.`
        );
      } catch (err: any) {
        this.outputChannel.appendLine(
          `[Transport] Auto-restart attempt #${this._reconnectAttempt} failed: ${err?.message || err}`
        );
        this.handleUnexpectedDisconnect(err?.message || 'Restart failed');
      }
    }, delayMs);
  }

  // ---------------------------------------------------------------------------
  // 5. Unified HTTP Transport API (GET / POST with Auto-Heal Retry)
  // ---------------------------------------------------------------------------

  /**
   * Performs a typed HTTP GET request against the CodeExplorer server.
   * Automatically ensures the server is started (unless `autoStart: false`) and retries
   * once on transient socket disconnects.
   */
  public async get<T>(apiPath: string, options?: TransportRequestOptions): Promise<T> {
    return this.request<T>('GET', apiPath, undefined, options);
  }

  /**
   * Performs a typed HTTP POST request against the CodeExplorer server.
   * Automatically ensures the server is started (unless `autoStart: false`) and retries
   * once on transient socket disconnects.
   */
  public async post<T>(apiPath: string, body?: any, options?: TransportRequestOptions): Promise<T> {
    return this.request<T>('POST', apiPath, body, options);
  }

  private async request<T>(
    method: 'GET' | 'POST',
    apiPath: string,
    body?: any,
    options?: TransportRequestOptions
  ): Promise<T> {
    const autoStart = options?.autoStart ?? true;
    const timeoutMs = options?.timeoutMs ?? 30000;
    const normalizedPath = apiPath.startsWith('/') ? apiPath : `/${apiPath}`;

    let serverInfo = this.processManager.getServerInfo();
    if (!serverInfo) {
      if (!autoStart) {
        throw new Error('CodeExplorer server is not currently running.');
      }
      serverInfo = await this.ensureConnected(options?.workspaceRoot);
    }

    const url = `${serverInfo.httpUrl}${normalizedPath}`;
    try {
      return await executeHttpRequest<T>(url, method, body, options?.headers, timeoutMs);
    } catch (err: any) {
      if (autoStart && isTransientNetworkError(err)) {
        this.outputChannel.appendLine(
          `[Transport] Transient network error on ${method} ${normalizedPath} (${err.message}). Reconnecting and retrying once...`
        );
        const recoveredInfo = await this.ensureConnected(options?.workspaceRoot, {
          forceRestart: !this.processManager.getServerInfo(),
        });
        const retryUrl = `${recoveredInfo.httpUrl}${normalizedPath}`;
        return await executeHttpRequest<T>(retryUrl, method, body, options?.headers, timeoutMs);
      }
      throw err;
    }
  }

  public dispose(): void {
    this._isDisposed = true;
    this.stopWatchdog();
    this.clearReconnectTimer();
    this._topics.clear();
    for (const d of this._disposables) {
      d.dispose();
    }
    this._disposables.length = 0;
    if (ServerTransport._instance === this) {
      ServerTransport._instance = null;
    }
  }
}
