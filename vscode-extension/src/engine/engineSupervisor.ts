import type * as vscode from 'vscode';
import type { ProcessManager, ServerInfo } from '../processManager';
import type * as ServerTransportModule from '../serverTransport';
import type { ServerTransport, TransportRequestOptions } from '../serverTransport';
import {
  EngineFailureInfo,
  EngineFsmSnapshot,
  EngineLifecycleFsm,
  EngineLifecycleState,
  isFatalEngineExit,
} from './engineLifecycleFsm';
import { clearEngineDiscoveryCache } from './discovery';

const MODULE_SERVER_TRANSPORT = '../serverTransport';

class SimpleEmitter<T> {
  private readonly _listeners = new Set<(e: T) => any>();
  public readonly event: vscode.Event<T> = (listener: (e: T) => any) => {
    this._listeners.add(listener);
    return { dispose: () => this._listeners.delete(listener) };
  };
  public fire(data: T): void {
    for (const listener of Array.from(this._listeners)) listener(data);
  }
  public dispose(): void {
    this._listeners.clear();
  }
}

export interface EngineSupervisorStatus {
  state: EngineLifecycleState;
  retryCount: number;
  failure: EngineFailureInfo | null;
  serverInfo: ServerInfo | null;
  isScanning: boolean;
  scanLabel?: string;
  projectCount?: number;
}

export interface EngineSupervisorAdapter {
  discoverBinary: (workspaceRoot: string) => Promise<{ command: string; args: string[] } | null>;
  startServer: (workspaceRoot: string) => Promise<ServerInfo>;
  stopServer: () => Promise<void> | void;
  httpRequest?: <T>(
    method: 'GET' | 'POST',
    apiPath: string,
    body?: unknown,
    options?: TransportRequestOptions
  ) => Promise<T>;
}

export interface EngineSupervisorOptions {
  retryDelayMs?: number;
  adapter?: EngineSupervisorAdapter;
}

/**
 * EngineSupervisor encapsulates the deterministic `EngineLifecycleFsm`, single-flight
 * `ensureReady()` promise queue, binary discovery caching, and strict separation of logs
 * between `OutputChannel("CodeExplorer")` and `OutputChannel("CodeExplorer (Protocol)")`.
 */
export class EngineSupervisor implements vscode.Disposable {
  private static _instance: EngineSupervisor | null = null;

  private readonly _fsm = new EngineLifecycleFsm();
  private readonly _retryDelayMs: number;
  private readonly _adapter: EngineSupervisorAdapter;
  private _inFlightReadyPromise: Promise<ServerInfo> | null = null;
  private _retryTimer: ReturnType<typeof setTimeout> | null = null;
  private _serverInfo: ServerInfo | null = null;
  private _isScanning = false;
  private _scanLabel: string | undefined;
  private _projectCount: number | undefined;
  private _hasLoggedFailureDiagnostic = false;
  private _isDisposed = false;
  private readonly _disposables: Array<{ dispose(): void }> = [];

  private readonly _onDidChangeStatus = new SimpleEmitter<EngineSupervisorStatus>();
  public readonly onDidChangeStatus = this._onDidChangeStatus.event;

  public static initialize(
    processManager: ProcessManager,
    transport: ServerTransport,
    getWorkspaceRoot: () => string | undefined,
    processChannel: Pick<vscode.OutputChannel, 'appendLine' | 'show'>,
    protocolChannel: Pick<vscode.OutputChannel, 'appendLine' | 'show'>,
    options?: EngineSupervisorOptions
  ): EngineSupervisor {
    if (!EngineSupervisor._instance || EngineSupervisor._instance._isDisposed) {
      EngineSupervisor._instance = new EngineSupervisor(
        processManager,
        transport,
        getWorkspaceRoot,
        processChannel,
        protocolChannel,
        options
      );
    }
    return EngineSupervisor._instance;
  }

  public static getInstance(): EngineSupervisor | null {
    return EngineSupervisor._instance;
  }

  public constructor(
    private readonly processManager: ProcessManager,
    private readonly transport: ServerTransport,
    private readonly getWorkspaceRoot: () => string | undefined,
    private readonly processChannel: Pick<vscode.OutputChannel, 'appendLine' | 'show'>,
    private readonly protocolChannel: Pick<vscode.OutputChannel, 'appendLine' | 'show'>,
    options?: EngineSupervisorOptions
  ) {
    this._retryDelayMs = options?.retryDelayMs ?? 2000;
    this._adapter = options?.adapter ?? {
      discoverBinary: (wsRoot) => this.processManager.resolveExecutable(wsRoot),
      startServer: (wsRoot) =>
        this.transport.ensureConnected(wsRoot, { forceRestart: false, resetCircuitBreaker: true }),
      stopServer: () => this.processManager.stopServerAsync(),
    };

    const unsubFsm = this._fsm.onStateChange((snap, prev) => {
      this.processChannel.appendLine(`[EngineSupervisor] State: ${prev} -> ${snap.state}`);
      if (snap.state === 'Failed') {
        this.clearRetryTimer();
        if (!this._hasLoggedFailureDiagnostic && snap.failure) {
          this._hasLoggedFailureDiagnostic = true;
          this.processChannel.appendLine(
            `[EngineSupervisor] Engine entered Failed state (${snap.failure.kind}): ${snap.failure.reason}`
          );
        }
      }
      this._onDidChangeStatus.fire(this.getStatus());
    });
    this._disposables.push({ dispose: unsubFsm }, this._onDidChangeStatus);

    if (this.processManager?.onDidUnexpectedExit) {
      this._disposables.push(
        this.processManager.onDidUnexpectedExit(({ code, signal }) => {
          if (!this._isDisposed) void this.handleUnexpectedRuntimeExit(code, signal);
        })
      );
    }
  }

  public getFsmSnapshot(): EngineFsmSnapshot {
    return this._fsm.getSnapshot();
  }

  public getState(): EngineLifecycleState {
    return this._fsm.getState();
  }

  public getServerInfo(): ServerInfo | null {
    return this._serverInfo ?? this.processManager?.getServerInfo?.() ?? null;
  }

  public getStatus(): EngineSupervisorStatus {
    const snap = this._fsm.getSnapshot();
    return {
      state: snap.state,
      retryCount: snap.retryCount,
      failure: snap.failure,
      serverInfo: this.getServerInfo(),
      isScanning: this._isScanning,
      scanLabel: this._scanLabel,
      projectCount: this._projectCount,
    };
  }

  public setScanning(isScanning: boolean, scanLabel?: string): void {
    if (this._isScanning === isScanning && this._scanLabel === scanLabel) return;
    this._isScanning = isScanning;
    this._scanLabel = scanLabel;
    this._onDidChangeStatus.fire(this.getStatus());
  }

  public setProjectCount(count: number): void {
    if (this._projectCount === count) return;
    this._projectCount = count;
    this._onDidChangeStatus.fire(this.getStatus());
  }

  public showLaunchLogs(): void {
    this.processChannel.show?.(true);
  }

  public showProtocolLogs(): void {
    this.protocolChannel.show?.(true);
  }

  public ensureReady(workspaceRoot?: string): Promise<ServerInfo> {
    const state = this._fsm.getState();
    if (state === 'Ready' && this._serverInfo) {
      return Promise.resolve(this._serverInfo);
    }
    if (state === 'Failed') {
      const failure = this._fsm.getSnapshot().failure;
      return Promise.reject(
        new Error(failure?.reason || 'CodeExplorer engine is in Failed state. Use Retry Start to recover.')
      );
    }
    if (this._inFlightReadyPromise) {
      return this._inFlightReadyPromise;
    }
    const root = workspaceRoot ?? this.getWorkspaceRoot();
    if (!root) {
      return Promise.reject(new Error('No workspace folder is currently open.'));
    }
    this._inFlightReadyPromise = this.runStartupSequence(root).finally(() => {
      this._inFlightReadyPromise = null;
    });
    return this._inFlightReadyPromise;
  }

  public async retry(workspaceRoot?: string): Promise<ServerInfo> {
    this.clearRetryTimer();
    this._hasLoggedFailureDiagnostic = false;
    this._serverInfo = null;
    clearEngineDiscoveryCache();
    if (this._fsm.getState() !== 'Failed' && this._fsm.getState() !== 'Idle') {
      await this.stop();
    }
    this._fsm.retry();
    const root = workspaceRoot ?? this.getWorkspaceRoot();
    if (!root) throw new Error('No workspace folder is currently open.');
    this._inFlightReadyPromise = this.runStartupSequence(root, true).finally(() => {
      this._inFlightReadyPromise = null;
    });
    return this._inFlightReadyPromise;
  }

  public async onExecutableSettingChanged(workspaceRoot?: string): Promise<void> {
    this.clearRetryTimer();
    this._hasLoggedFailureDiagnostic = false;
    clearEngineDiscoveryCache();
    this.processManager?.getBinaryManager()?.invalidateEngineCache();
    const prevState = this._fsm.getState();
    if (prevState === 'Ready' || prevState === 'Starting' || prevState === 'RetryingOnce') {
      await this.stop();
    }
    if (this._fsm.onExecutableSettingChanged()) {
      const root = workspaceRoot ?? this.getWorkspaceRoot();
      if (root && this.processManager?.hasWorkspace(root)) {
        this._inFlightReadyPromise = this.runStartupSequence(root, true).finally(() => {
          this._inFlightReadyPromise = null;
        });
        await this._inFlightReadyPromise.catch(() => {});
      }
    }
  }

  public async request<T>(
    method: 'GET' | 'POST',
    apiPath: string,
    body?: unknown,
    options?: TransportRequestOptions
  ): Promise<T> {
    const autoStart = options?.autoStart ?? true;
    const normalizedPath = apiPath.startsWith('/') ? apiPath : `/${apiPath}`;
    let info = this.getServerInfo();
    if (!info || this._fsm.getState() !== 'Ready') {
      if (!autoStart) throw new Error('CodeExplorer engine is not Ready.');
      info = await this.ensureReady(options?.workspaceRoot);
    }

    const startMs = Date.now();
    const reqBytes =
      body !== undefined ? Buffer.byteLength(typeof body === 'string' ? body : JSON.stringify(body)) : 0;
    this.protocolChannel.appendLine(
      `[Protocol] -> ${method} ${normalizedPath}${reqBytes > 0 ? ` (${reqBytes}B)` : ''}`
    );

    try {
      const res = this._adapter.httpRequest
        ? await this._adapter.httpRequest<T>(method, normalizedPath, body, options)
        : await (require(MODULE_SERVER_TRANSPORT) as typeof ServerTransportModule).executeHttpRequest<T>(
            `${info.httpUrl}${normalizedPath}`,
            method,
            body,
            options?.headers,
            options?.timeoutMs ?? 30000
          );
      const durationMs = Date.now() - startMs;
      const resBytes = res !== undefined ? Buffer.byteLength(JSON.stringify(res)) : 0;
      this.protocolChannel.appendLine(
        `[Protocol] <- 200 OK (${method} ${normalizedPath}) [${durationMs}ms, ${resBytes}B]`
      );
      return res;
    } catch (err: any) {
      const durationMs = Date.now() - startMs;
      this.protocolChannel.appendLine(
        `[Protocol] <- ERROR (${method} ${normalizedPath}) [${durationMs}ms]: ${err?.message || err}`
      );
      throw err;
    }
  }

  public async stop(): Promise<void> {
    this.clearRetryTimer();
    if (!this._fsm.beginStop()) return;
    this._serverInfo = null;
    try {
      this.transport?.stop?.();
      await this._adapter.stopServer();
    } finally {
      this._fsm.onStopped();
    }
  }

  private async runStartupSequence(workspaceRoot: string, alreadyDiscovering = false): Promise<ServerInfo> {
    if (!alreadyDiscovering && this._fsm.getState() === 'Idle') {
      this._fsm.requestStart();
    }
    if (this._fsm.getState() === 'Discovering') {
      this.processChannel.appendLine(`[EngineSupervisor] Discovering CodeExplorer binary for workspace: ${workspaceRoot}`);
      const executable = await this._adapter.discoverBinary(workspaceRoot);
      if (!executable || !executable.command) {
        const msg = 'CodeExplorer (ce) executable was not found. Configure "codeExplorer.executablePath" or install "ce".';
        this._fsm.onBinaryNotFound(msg);
        throw new Error(msg);
      }
      this.processChannel.appendLine(
        `[EngineSupervisor] Discovered binary: ${executable.command} ${executable.args.join(' ')}`.trim()
      );
      this._fsm.onBinaryDiscovered(executable.command);
    }

    while (this._fsm.getState() === 'Starting' || this._fsm.getState() === 'RetryingOnce') {
      try {
        const info = await this._adapter.startServer(workspaceRoot);
        this._serverInfo = info;
        this._fsm.onServerReady(info.port);
        return info;
      } catch (err: any) {
        const reason = err?.message || String(err);
        const exitCode = typeof err?.exitCode === 'number' ? err.exitCode : null;
        const stderr = typeof err?.stderr === 'string' ? err.stderr : '';
        const isTimeout = /timed out/i.test(reason);
        const fatal = err?.isFatal === true || isFatalEngineExit(exitCode, stderr, reason);
        const nextState = this._fsm.onProcessFailure({ reason, exitCode, stderr, isTimeout, fatal });
        if (nextState === 'RetryingOnce') {
          this.processChannel.appendLine(
            `[EngineSupervisor] Transient startup error (${reason}). Retrying once in ${this._retryDelayMs}ms...`
          );
          await this.waitRetryDelay();
          continue;
        }
        throw err;
      }
    }
    throw new Error(this._fsm.getSnapshot().failure?.reason || 'Engine startup failed.');
  }

  private async handleUnexpectedRuntimeExit(code: number | null, signal: string | null): Promise<void> {
    if (this._fsm.getState() !== 'Ready') return;
    this._serverInfo = null;
    const reason = `Process exited unexpectedly (code: ${code}, signal: ${signal ?? 'none'})`;
    const nextState = this._fsm.onProcessFailure({ reason, exitCode: code, signal });
    if (nextState === 'RetryingOnce') {
      const root = this.getWorkspaceRoot();
      if (!root) return;
      this.processChannel.appendLine(
        `[EngineSupervisor] Unexpected runtime exit. Retrying once in ${this._retryDelayMs}ms...`
      );
      this._inFlightReadyPromise = (async () => {
        await this.waitRetryDelay();
        return this.runStartupSequence(root, true);
      })().finally(() => {
        this._inFlightReadyPromise = null;
      });
      await this._inFlightReadyPromise.catch(() => {});
    }
  }

  private waitRetryDelay(): Promise<void> {
    this.clearRetryTimer();
    return new Promise<void>((resolve) => {
      this._retryTimer = setTimeout(() => {
        this._retryTimer = null;
        resolve();
      }, this._retryDelayMs);
    });
  }

  private clearRetryTimer(): void {
    if (this._retryTimer) {
      clearTimeout(this._retryTimer);
      this._retryTimer = null;
    }
  }

  public dispose(): void {
    this._isDisposed = true;
    this.clearRetryTimer();
    while (this._disposables.length > 0) this._disposables.pop()?.dispose();
  }
}
