import * as vscode from 'vscode';
import * as cp from 'child_process';
import * as path from 'path';
import * as fs from 'fs';
import * as readline from 'readline';
import {
  BinaryManager,
  matchesEnginePattern,
  getEngineConfigFromExtensionVersion,
  parseSemver,
  cleanSemver,
  compareSemver,
  probeDotnetTool,
  selectEngineExecutable,
  DotnetToolInfo,
  getExecutableName,
  ensureExecutablePermissions,
} from './binaryManager';

export interface ServerInfo {
  status: string;
  port: number;
  wsUrl: string;
  httpUrl: string;
  workspace: string;
  version?: string;
}

export class ProcessManager implements vscode.Disposable {
  private serverProcess: cp.ChildProcess | null = null;
  private serverInfo: ServerInfo | null = null;
  private startPromise: Promise<ServerInfo> | null = null;
  private outputChannel: vscode.OutputChannel;
  private binaryManager?: BinaryManager;

  private _onDidServerStart = new vscode.EventEmitter<ServerInfo>();
  public readonly onDidServerStart = this._onDidServerStart.event;

  private _onDidServerStop = new vscode.EventEmitter<void>();
  public readonly onDidServerStop = this._onDidServerStop.event;

  constructor(outputChannel: vscode.OutputChannel, context?: vscode.ExtensionContext) {
    this.outputChannel = outputChannel;
    if (context) {
      this.binaryManager = new BinaryManager(outputChannel, context);
    }
  }

  setContext(context: vscode.ExtensionContext) {
    if (!this.binaryManager) {
      this.binaryManager = new BinaryManager(this.outputChannel, context);
    } else {
      this.binaryManager.setContext(context);
    }
  }

  getBinaryManager(): BinaryManager | undefined {
    return this.binaryManager;
  }

  private cachedDotnetTool: DotnetToolInfo | null = null;
  private cachedExecutable: { command: string; args: string[] } | null = null;

  public getCachedDotnetTool(): DotnetToolInfo | null {
    return this.cachedDotnetTool;
  }

  public getServerInfo(): ServerInfo | null {
    if (this.serverInfo && this.serverProcess && !this.serverProcess.killed) {
      return this.serverInfo;
    }
    return null;
  }

  public isStarting(): boolean {
    return this.startPromise !== null;
  }

  /**
   * Checks if .codeexplorer directory and database exists in the workspace.
   */
  hasWorkspace(workspaceRoot?: string): boolean {
    if (!workspaceRoot) return false;
    try {
      const ceDir = path.join(workspaceRoot, '.codeexplorer');
      return fs.existsSync(ceDir);
    } catch {
      return false;
    }
  }

  /**
   * Executes a one-off CLI command like `ce init` or `ce index`.
   */
  async runCliCommand(
    workspaceRoot: string,
    cliArgs: string[],
    onLine?: (line: string) => void
  ): Promise<void> {
    if (!workspaceRoot) {
      throw new Error('No workspace folder is currently open.');
    }
    const config = vscode.workspace.getConfiguration('codeExplorer');
    const customPath = config.get<string>('executablePath', '');
    const executable = await this.findExecutable(workspaceRoot, customPath);
    if (!executable) {
      throw new Error(
        'CodeExplorer (ce) executable not found. Please build the CLI or install "ce" to PATH.'
      );
    }

    const fullArgs = [...executable.args, ...cliArgs];
    this.outputChannel.appendLine(`[CLI] Running: ${executable.command} ${fullArgs.join(' ')}`);

    return new Promise<void>((resolve, reject) => {
      const isBatch =
        process.platform === 'win32' &&
        (executable.command.toLowerCase().endsWith('.cmd') || executable.command.toLowerCase().endsWith('.bat'));
      const child = cp.spawn(executable.command, fullArgs, {
        cwd: workspaceRoot,
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production' },
        windowsHide: true,
        shell: isBatch,
      });

      if (child.stdout) {
        const rl = readline.createInterface({ input: child.stdout });
        rl.on('line', (line) => {
          this.outputChannel.appendLine(`[ce stdout] ${line}`);
          onLine?.(line);
        });
      }

      if (child.stderr) {
        const rl = readline.createInterface({ input: child.stderr });
        rl.on('line', (line) => {
          this.outputChannel.appendLine(`[ce stderr] ${line}`);
        });
      }

      child.on('error', (err) => {
        this.outputChannel.appendLine(`[CLI Error] ${err.message}`);
        reject(err);
      });

      child.on('exit', (code) => {
        if (code === 0) {
          resolve();
        } else {
          reject(new Error(`Command exited with code ${code}`));
        }
      });
    });
  }

  /**
   * Returns existing or newly started server info for the given workspace.
   */
  async ensureServerStarted(workspaceRoot?: string): Promise<ServerInfo> {
    if (!workspaceRoot) {
      throw new Error('No workspace folder is currently open.');
    }

    const extVersion = this.binaryManager?.getExtensionVersion() || '1.18.2';
    const config = vscode.workspace.getConfiguration('codeExplorer');
    const userSetting = config?.get<string>('engineVersion', '')?.trim() || undefined;
    const { pattern } = getEngineConfigFromExtensionVersion(extVersion, userSetting);

    if (this.serverInfo && this.serverProcess && !this.serverProcess.killed) {
      if (this.serverInfo.version) {
        // Check if a higher preferred engine (e.g. local dotnet tool) is available
        const bestInstalled = this.binaryManager?.getBestInstalledEngine(workspaceRoot);
        if (bestInstalled && compareSemver(bestInstalled.version, this.serverInfo.version) > 0) {
          this.outputChannel.appendLine(
            `[ProcessManager] Active server version (v${this.serverInfo.version}) is lower than available engine (v${bestInstalled.version}, ${bestInstalled.source}). Restarting server with higher engine...`
          );
          this.stopServer();
        } else if (matchesEnginePattern(this.serverInfo.version, pattern)) {
          return this.serverInfo;
        } else {
          this.outputChannel.appendLine(
            `[ProcessManager] Active server version (v${this.serverInfo.version}) ` +
            `does not match required pattern '${pattern}'. Restarting server...`
          );
          this.stopServer();
        }
      } else {
        return this.serverInfo;
      }
    }

    if (this.startPromise) {
      return this.startPromise;
    }

    this.startPromise = this.startServer(workspaceRoot);
    try {
      this.serverInfo = await this.startPromise;
      return this.serverInfo;
    } finally {
      this.startPromise = null;
    }
  }

  /**
   * Spawns `ce serve` with an ephemeral port and resolves when the ready handshake is received.
   */
  private async startServer(workspaceRoot: string): Promise<ServerInfo> {
    const config = vscode.workspace.getConfiguration('codeExplorer');
    const customPath = config.get<string>('executablePath', '');
    const configuredPort = config.get<number>('serverPort') ?? 0;
    const idleTimeout = config.get<number>('idleTimeout') ?? 60;

    this.outputChannel.appendLine(`[ProcessManager] Preparing CodeExplorer server for workspace: ${workspaceRoot}`);
    const executable = await this.findExecutable(workspaceRoot, customPath);
    if (!executable) {
      this.outputChannel.appendLine('[ProcessManager] Error: No valid CodeExplorer executable could be found.');
      throw new Error(
        'CodeExplorer (ce) executable not found. Please specify "codeExplorer.executablePath" in settings, ' +
        'build the CLI, or install "ce" to PATH.'
      );
    }

    const serverArgs = [
      ...executable.args,
      'serve',
      '--root',
      workspaceRoot,
      '--port',
      (configuredPort ?? 0).toString(),
      '--idle-timeout',
      (idleTimeout ?? 60).toString(),
    ];

    this.outputChannel.appendLine(
      `[ProcessManager] Spawning: ${executable.command} ${serverArgs.join(' ')} (CWD: ${workspaceRoot})`
    );

    return new Promise<ServerInfo>((resolve, reject) => {
      let isReady = false;
      const isBatch =
        process.platform === 'win32' &&
        (executable.command.toLowerCase().endsWith('.cmd') || executable.command.toLowerCase().endsWith('.bat'));
      const child = cp.spawn(executable.command, serverArgs, {
        cwd: workspaceRoot,
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production' },
        windowsHide: true,
        shell: isBatch,
      });

      this.serverProcess = child;
      this.outputChannel.appendLine(`[ProcessManager] Server process spawned with PID: ${child.pid}`);

      const timeout = setTimeout(() => {
        if (!isReady) {
          child.kill();
          reject(new Error('Timed out waiting for CodeExplorer server ready handshake.'));
        }
      }, 20000);

      // Read stdout line-by-line to capture the ready JSON handshake
      if (child.stdout) {
        const rl = readline.createInterface({ input: child.stdout });
        rl.on('line', (line) => {
          this.outputChannel.appendLine(`[ce stdout] ${line}`);
          if (!isReady && line.trim().startsWith('{')) {
            try {
              const data = JSON.parse(line.trim());
              if (data.status === 'ready' && data.wsUrl) {
                isReady = true;
                clearTimeout(timeout);
                this.serverInfo = data as ServerInfo;
                this.outputChannel.appendLine(`[Server] Ready at ${data.wsUrl} (HTTP ${data.httpUrl})`);
                this._onDidServerStart.fire(this.serverInfo);
                resolve(this.serverInfo);
              }
            } catch {
              // Not a JSON line, continue waiting
            }
          }
        });
      }

      const stderrChunks: string[] = [];
      if (child.stderr) {
        child.stderr.on('data', (chunk) => {
          const text = chunk.toString();
          stderrChunks.push(text);
          this.outputChannel.appendLine(`[ce stderr] ${text}`);
        });
      }

      child.on('error', (err) => {
        this.outputChannel.appendLine(`[Server Error] Failed to spawn process: ${err.message}`);
        clearTimeout(timeout);
        if (!isReady) {
          const report = diagnoseProcessExit(null, null, err.message, executable.command, serverArgs, workspaceRoot);
          this.outputChannel.appendLine('\n' + report.fullReport + '\n');
          const detailedError = new Error(
            `${report.title}\n${report.summary}\n\nSuggested Action:\n${report.suggestion}\n\n${report.fullReport}`
          );
          (detailedError as any).diagnosticReport = report;
          reject(detailedError);
        }
      });

      child.on('exit', (code, signal) => {
        this.outputChannel.appendLine(`[Server] Process exited with code ${code}, signal ${signal}`);
        this.serverProcess = null;
        this.serverInfo = null;
        this._onDidServerStop.fire();
        if (!isReady) {
          clearTimeout(timeout);
          const rawStderr = stderrChunks.join('');
          const report = diagnoseProcessExit(code, signal, rawStderr, executable.command, serverArgs, workspaceRoot);
          this.outputChannel.appendLine('\n' + report.fullReport + '\n');
          const detailedError = new Error(
            `${report.title}\n${report.summary}\n\nSuggested Action:\n${report.suggestion}\n\n${report.fullReport}`
          );
          (detailedError as any).diagnosticReport = report;
          reject(detailedError);
        }
      });
    });
  }

  /**
   * Resolves the executable command and arguments to launch CodeExplorer.
   * Runs once at server start and reuses the result until the next restart.
   */
  private async findExecutable(
    workspaceRoot?: string,
    customPath?: string
  ): Promise<{ command: string; args: string[] } | null> {
    if (this.cachedExecutable) {
      return this.cachedExecutable;
    }

    const setAndReturn = (res: { command: string; args: string[] } | null) => {
      if (res) {
        this.cachedExecutable = res;
      }
      return res;
    };

    // 1. Explicit path from user settings (codeExplorer.executablePath) or CE_EXECUTABLE
    const targetPath = customPath && customPath.trim().length > 0 ? customPath.trim() : process.env.CE_EXECUTABLE?.trim();

    if (targetPath) {
      const resolved = path.isAbsolute(targetPath)
        ? targetPath
        : (workspaceRoot ? path.resolve(workspaceRoot, targetPath) : path.resolve(targetPath));

      if (fs.existsSync(resolved)) {
        const isDll = resolved.endsWith('.dll');
        this.outputChannel.appendLine(
          `[ProcessManager] Using configured executable (${customPath ? 'settings' : 'CE_EXECUTABLE'}): ${resolved}`
        );
        const customExecutable = isDll ? { command: 'dotnet', args: [resolved] } : { command: resolved, args: [] };
        return setAndReturn(customExecutable);
      }
    }

    // 2. ALWAYS PROBE INSTALLED ENGINES FIRST BEFORE DOWNLOADING
    const bestInstalled = this.binaryManager?.getBestInstalledEngine(workspaceRoot) || null;
    if (bestInstalled?.source === 'dotnet-tool') {
      this.cachedDotnetTool = {
        command: bestInstalled.command,
        args: bestInstalled.args,
        version: bestInstalled.version,
      };
    } else {
      this.cachedDotnetTool = probeDotnetTool(workspaceRoot);
    }

    if (bestInstalled) {
      this.outputChannel.appendLine(
        `[ProcessManager] Best installed engine candidate: v${bestInstalled.version} (${bestInstalled.source}) at ${bestInstalled.command}`
      );
      const extVersion = this.binaryManager?.getExtensionVersion() || '1.18.2';
      const config = vscode.workspace.getConfiguration('codeExplorer');
      const userSetting = config?.get<string>('engineVersion', '')?.trim() || undefined;
      const { pattern } = getEngineConfigFromExtensionVersion(extVersion, userSetting);

      if (matchesEnginePattern(bestInstalled.version, pattern) || compareSemver(bestInstalled.version, extVersion) >= 0) {
        this.outputChannel.appendLine(
          `[ProcessManager] Using installed engine matching pattern '${pattern}' (or >= extension version): ${bestInstalled.command}`
        );
        return setAndReturn({ command: bestInstalled.command, args: bestInstalled.args });
      }
    }

    // 3. Neither present or needs download: use BinaryManager to ensure binary (which probes against remote release)
    if (this.binaryManager) {
      try {
        const managed = await this.binaryManager.ensureBinary(false, workspaceRoot);
        if (managed && (fs.existsSync(managed.command) || managed.command === 'dotnet')) {
          this.outputChannel.appendLine(`[ProcessManager] Using managed CodeExplorer engine: ${managed.command}`);
          return setAndReturn(managed);
        }
      } catch (err: any) {
        this.outputChannel.appendLine(
          `[ProcessManager] BinaryManager engine download/resolution failed: ${err?.message || err}`
        );
      }
    }

    // 4. Fallback: if any installed engine exists (even if lower version), use it rather than failing
    if (bestInstalled) {
      this.outputChannel.appendLine(
        `[ProcessManager] Falling back to installed engine: ${bestInstalled.command} (v${bestInstalled.version}, ${bestInstalled.source})`
      );
      return setAndReturn({ command: bestInstalled.command, args: bestInstalled.args });
    }

    // 5. Bundled platform-specific binary in extension (legacy/offline fallback)
    const binName = getExecutableName('ce');
    const extensionRoot = path.resolve(__dirname, '..');
    const bundledCandidate = path.resolve(extensionRoot, 'bin', binName);

    if (fs.existsSync(bundledCandidate)) {
      ensureExecutablePermissions(bundledCandidate);
      this.outputChannel.appendLine(`[ProcessManager] Using bundled CodeExplorer binary: ${bundledCandidate}`);
      return setAndReturn({ command: bundledCandidate, args: [] });
    }

    // 6. Fallback to system PATH command
    this.outputChannel.appendLine('[ProcessManager] Using system PATH "ce".');
    return setAndReturn({ command: 'ce', args: [] });
  }

  stopServer(): void {
    if (this.serverProcess && !this.serverProcess.killed) {
      this.outputChannel.appendLine('[Server] Stopping CodeExplorer server process...');
      const pid = this.serverProcess.pid;
      if (process.platform === 'win32' && pid) {
        try {
          // On Windows, terminate the entire process tree to prevent orphaned dotnet/ce processes
          cp.spawnSync('taskkill', ['/F', '/T', '/PID', pid.toString()], {
            windowsHide: true,
            stdio: 'ignore',
          });
        } catch {
          // Best effort
        }
      }
      try {
        this.serverProcess.kill();
      } catch {
        // Best effort
      }
      this.serverProcess = null;
      this.serverInfo = null;
      this._onDidServerStop.fire();
    }
    this.cachedExecutable = null;
  }

  dispose() {
    this.stopServer();
    this._onDidServerStart.dispose();
    this._onDidServerStop.dispose();
  }
}

export interface ProcessDiagnosticReport {
  title: string;
  category: string;
  summary: string;
  suggestion: string;
  fullReport: string;
}

export function diagnoseProcessExit(
  code: number | null,
  signal: string | null,
  stderr: string,
  command: string,
  args: string[],
  workspaceRoot: string
): ProcessDiagnosticReport {
  const isNetHostFailure =
    code === 2147516566 ||
    code === -2147450730 ||
    stderr.includes('FrameworkMissingFailure') ||
    stderr.includes('No frameworks were found') ||
    stderr.includes('app-launch-failed') ||
    stderr.includes('80008096');

  const isPortConflict =
    stderr.includes('already in use') ||
    stderr.includes('EADDRINUSE') ||
    stderr.includes('Failed to bind to address');

  const isDbMissing =
    stderr.includes('Graph database not found') ||
    (stderr.includes('graph.db') && stderr.includes('not found'));

  const isOom = code === 137 || signal === 'SIGKILL';

  let title = 'CodeExplorer Server Process Failed';
  let category = 'Process Exit';
  let summary = `Process exited with code ${code ?? 'unknown'}${signal ? ` (signal: ${signal})` : ''}.`;
  let suggestion = 'Check Output Channel (CodeExplorer) for complete server logs.';

  if (isNetHostFailure) {
    title = '.NET 10 Runtime Missing (0x80008096)';
    category = '.NET Host Runtime Failure';
    summary = 'The .NET runtime host could not find Microsoft.NETCore.App 10.0 runtime.';
    suggestion =
      'Install the .NET 10 Runtime or SDK (x64) from https://dotnet.microsoft.com/download/dotnet/10.0 ' +
      'or check installed runtimes via `dotnet --list-runtimes`.';
  } else if (isPortConflict) {
    title = 'Server Port Already In Use';
    category = 'Network / Port Conflict';
    summary = 'The configured listening port is already occupied by another application or instance.';
    suggestion = 'Set `codeExplorer.serverPort` to 0 in VS Code Settings to allocate an automatic free ephemeral port.';
  } else if (isDbMissing) {
    title = 'Graph Database Not Initialized';
    category = 'Database Missing';
    summary = 'No indexed graph database (.codeexplorer/graph.db) exists in this workspace.';
    suggestion = 'Run `ce scan` in the workspace root or trigger index from CodeExplorer command palette.';
  } else if (isOom) {
    title = 'Process Terminated by System (Out Of Memory)';
    category = 'Out Of Memory';
    summary = 'The server process was killed by the operating system due to memory constraints.';
    suggestion = 'Free up system memory or increase virtual memory allocation.';
  }

  const lines = [
    `[CodeExplorer Diagnostics]`,
    `Title: ${title}`,
    `Category: ${category}`,
    `Summary: ${summary}`,
    `Suggested Action: ${suggestion}`,
    `--------------------------------------------------`,
    `Command: ${command} ${args.join(' ')}`,
    `Workspace: ${workspaceRoot}`,
    `Exit Code: ${code} (hex: ${code !== null ? '0x' + (code >>> 0).toString(16).toUpperCase() : 'N/A'}), ` +
    `Signal: ${signal ?? 'none'}`,
    `--------------------------------------------------`,
    `Console stderr output:`,
    stderr.trim() ? stderr.trim() : '(no stderr output recorded)'
  ];

  return {
    title,
    category,
    summary,
    suggestion,
    fullReport: lines.join('\n'),
  };
}

