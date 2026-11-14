export type EngineLifecycleState =
  | 'Idle'
  | 'Discovering'
  | 'Starting'
  | 'RetryingOnce'
  | 'Ready'
  | 'Failed'
  | 'Stopping';

const STATE_IDLE: EngineLifecycleState = 'Idle';
const STATE_DISCOVERING: EngineLifecycleState = 'Discovering';
const STATE_STARTING: EngineLifecycleState = 'Starting';
const STATE_RETRYING_ONCE: EngineLifecycleState = 'RetryingOnce';
const STATE_READY: EngineLifecycleState = 'Ready';
const STATE_FAILED: EngineLifecycleState = 'Failed';
const STATE_STOPPING: EngineLifecycleState = 'Stopping';

export type EngineFailureKind =
  | 'binary-not-found'
  | 'fatal-exit'
  | 'transient-exhausted'
  | 'startup-timeout';

export interface EngineFailureInfo {
  kind: EngineFailureKind;
  reason: string;
  exitCode?: number | null;
  signal?: string | null;
  stderr?: string;
}

export interface EngineFsmSnapshot {
  state: EngineLifecycleState;
  retryCount: number;
  maxRetries: 1;
  failure: EngineFailureInfo | null;
  binaryCommand: string | null;
  port: number | null;
}

export type EngineFsmListener = (
  snapshot: EngineFsmSnapshot,
  previousState: EngineLifecycleState
) => void;

const FATAL_EXIT_CODES = new Set<number>([
  2147516566, // 0x80008096 (.NET host FrameworkMissingFailure)
  -2147450730, // Signed 32-bit representation of 0x80008096
  150, // POSIX .NET host FrameworkMissingFailure (0x96)
  64, // EX_USAGE (invalid CLI flag / argument)
  126, // Command invoked cannot execute (permission problem / not executable)
  127, // Command not found
]);

const FATAL_STDERR_PATTERNS: RegExp[] = [
  /FrameworkMissingFailure/i,
  /No frameworks were found/i,
  /You must install or update \.NET to run this application/i,
  /app-launch-failed/i,
  /80008096/i,
  /hostfxr/i,
  /Unrecognized command or argument/i,
  /Unrecognized option/i,
  /Unknown option/i,
  /Required argument missing/i,
  /Invalid option/i,
  /ENOENT/i,
  /EACCES/i,
  /is not recognized as an internal or external command/i,
];

/**
 * Classifies whether a process exit code or stderr indicates a non-recoverable
 * fatal configuration/runtime error that must skip all retry attempts.
 */
export function isFatalEngineExit(
  exitCode?: number | null,
  stderr?: string | null,
  errorMessage?: string | null
): boolean {
  if (typeof exitCode === 'number' && FATAL_EXIT_CODES.has(exitCode)) {
    return true;
  }
  const combined = `${stderr || ''}\n${errorMessage || ''}`;
  if (!combined.trim()) {
    return false;
  }
  return FATAL_STDERR_PATTERNS.some((pattern) => pattern.test(combined));
}

/**
 * Pure deterministic Finite State Machine governing the `ce` engine process lifecycle.
 *
 * State transitions:
 * - Idle -> Discovering (on first demand: requestStart)
 * - Discovering -> Starting (onBinaryDiscovered)
 * - Discovering -> Failed (onBinaryNotFound, 0 retries)
 * - Starting -> Ready (onServerReady)
 * - Starting -> RetryingOnce (transient failure, retryCount = 1)
 * - Starting -> Failed (fatal exit code / missing .NET runtime / invalid flag, 0 retries)
 * - RetryingOnce -> Ready (onServerReady)
 * - RetryingOnce -> Failed (any failure while in RetryingOnce -> Circuit Open)
 * - Ready -> RetryingOnce (unexpected transient exit when retryCount < 1)
 * - Ready -> Failed (fatal exit OR transient exit when retryCount >= 1)
 * - Failed -> Discovering (ONLY via explicit user retry() or onExecutableSettingChanged())
 * - Discovering | Starting | RetryingOnce | Ready -> Stopping -> Idle (stop / onStopped)
 */
export class EngineLifecycleFsm {
  private _state: EngineLifecycleState = 'Idle';
  private _retryCount = 0;
  private _failure: EngineFailureInfo | null = null;
  private _binaryCommand: string | null = null;
  private _port: number | null = null;
  private readonly _listeners = new Set<EngineFsmListener>();

  public getState(): EngineLifecycleState {
    return this._state;
  }

  public getSnapshot(): EngineFsmSnapshot {
    return {
      state: this._state,
      retryCount: this._retryCount,
      maxRetries: 1,
      failure: this._failure ? { ...this._failure } : null,
      binaryCommand: this._binaryCommand,
      port: this._port,
    };
  }

  public onStateChange(listener: EngineFsmListener): () => void {
    this._listeners.add(listener);
    return () => {
      this._listeners.delete(listener);
    };
  }

  /**
   * Initiates discovery from `Idle`.
   * Never transitions out of `Failed` automatically; returns false if not in `Idle`.
   */
  public requestStart(): boolean {
    if (this._state !== 'Idle') {
      return false;
    }
    this._failure = null;
    this._retryCount = 0;
    this.transitionTo(STATE_DISCOVERING);
    return true;
  }

  /**
   * Transitions `Discovering -> Starting` when a valid `ce` binary is located.
   */
  public onBinaryDiscovered(command: string): boolean {
    if (this._state !== 'Discovering') {
      return false;
    }
    this._binaryCommand = command;
    this.transitionTo(STATE_STARTING);
    return true;
  }

  /**
   * Transitions `Discovering -> Failed` immediately with 0 retries when `ce` binary is missing.
   */
  public onBinaryNotFound(reason = 'CodeExplorer (ce) executable was not found.'): boolean {
    if (this._state !== 'Discovering') {
      return false;
    }
    this._retryCount = 0;
    this._failure = {
      kind: 'binary-not-found',
      reason,
    };
    this.transitionTo(STATE_FAILED);
    return true;
  }

  /**
   * Transitions `Starting | RetryingOnce -> Ready` when `/api/status` or ready handshake succeeds.
   */
  public onServerReady(port: number): boolean {
    if (this._state !== 'Starting' && this._state !== 'RetryingOnce') {
      return false;
    }
    this._port = port;
    this._failure = null;
    this.transitionTo(STATE_READY);
    return true;
  }

  /**
   * Handles a startup failure or unexpected runtime exit.
   * Enforces at most 1 transient retry and 0 retries on fatal errors.
   */
  public onProcessFailure(options: {
    reason: string;
    exitCode?: number | null;
    signal?: string | null;
    stderr?: string;
    isTimeout?: boolean;
    fatal?: boolean;
  }): EngineLifecycleState {
    if (
      this._state !== 'Starting' &&
      this._state !== 'RetryingOnce' &&
      this._state !== 'Ready'
    ) {
      return this._state;
    }

    const isFatal =
      options.fatal === true ||
      isFatalEngineExit(options.exitCode, options.stderr, options.reason);

    if (isFatal) {
      this._port = null;
      this._failure = {
        kind: 'fatal-exit',
        reason: options.reason,
        exitCode: options.exitCode,
        signal: options.signal,
        stderr: options.stderr,
      };
      this.transitionTo(STATE_FAILED);
      return this._state;
    }

    if (this._state === 'Starting' || this._state === 'Ready') {
      if (this._retryCount < 1) {
        this._retryCount += 1;
        this._port = null;
        this._failure = {
          kind: options.isTimeout ? 'startup-timeout' : 'transient-exhausted',
          reason: options.reason,
          exitCode: options.exitCode,
          signal: options.signal,
          stderr: options.stderr,
        };
        this.transitionTo(STATE_RETRYING_ONCE);
        return this._state;
      }
    }

    // Already in RetryingOnce or retry budget (1) exhausted -> Failed (Circuit Open)
    this._port = null;
    this._failure = {
      kind: options.isTimeout ? 'startup-timeout' : 'transient-exhausted',
      reason: options.reason,
      exitCode: options.exitCode,
      signal: options.signal,
      stderr: options.stderr,
    };
    this.transitionTo(STATE_FAILED);
    return this._state;
  }

  /**
   * Explicit user-initiated retry (`Retry Start` button / command).
   * This is one of the ONLY two ways to leave `Failed` state.
   */
  public retry(): boolean {
    if (this._state !== 'Failed' && this._state !== 'Idle') {
      return false;
    }
    this._retryCount = 0;
    this._failure = null;
    this._port = null;
    this.transitionTo(STATE_DISCOVERING);
    return true;
  }

  /**
   * Triggered when the user updates `codeExplorer.executablePath` or `codeExplorer.engineVersion` in settings.
   * Transitions `Failed | Idle -> Discovering`.
   */
  public onExecutableSettingChanged(): boolean {
    this._binaryCommand = null;
    if (this._state === 'Failed' || this._state === 'Idle') {
      this._retryCount = 0;
      this._failure = null;
      this._port = null;
      this.transitionTo(STATE_DISCOVERING);
      return true;
    }
    return false;
  }

  /**
   * Begins stopping the active engine process (`-> Stopping -> Idle`).
   */
  public beginStop(): boolean {
    if (this._state === 'Idle' || this._state === 'Stopping') {
      return false;
    }
    if (this._state === 'Failed') {
      this._port = null;
      this.transitionTo(STATE_IDLE);
      return true;
    }
    this._port = null;
    this.transitionTo(STATE_STOPPING);
    return true;
  }

  /**
   * Completes the stop sequence (`Stopping -> Idle`).
   */
  public onStopped(): boolean {
    if (this._state !== 'Stopping') {
      return false;
    }
    this._port = null;
    this.transitionTo(STATE_IDLE);
    return true;
  }

  private transitionTo(nextState: EngineLifecycleState): void {
    const prev = this._state;
    if (prev === nextState) {
      return;
    }
    this._state = nextState;
    const snap = this.getSnapshot();
    for (const listener of Array.from(this._listeners)) {
      listener(snap, prev);
    }
  }
}
