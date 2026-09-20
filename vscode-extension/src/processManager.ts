import * as vscode from 'vscode';
import * as cp from 'child_process';
import * as path from 'path';
import * as fs from 'fs';
import * as readline from 'readline';

export interface ServerInfo {
  status: string;
  port: number;
  wsUrl: string;
  httpUrl: string;
  workspace: string;
}

export class ProcessManager implements vscode.Disposable {
  private serverProcess: cp.ChildProcess | null = null;
  private serverInfo: ServerInfo | null = null;
  private startPromise: Promise<ServerInfo> | null = null;
  private outputChannel: vscode.OutputChannel;

  constructor(outputChannel: vscode.OutputChannel) {
    this.outputChannel = outputChannel;
  }

  /**
   * Returns existing or newly started server info for the given workspace.
   */
  async ensureServerStarted(workspaceRoot: string): Promise<ServerInfo> {
    if (this.serverInfo && this.serverProcess && !this.serverProcess.killed) {
      return this.serverInfo;
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
    const configuredPort = config.get<number>('serverPort', 0);
    const idleTimeout = config.get<number>('idleTimeout', 60);

    const executable = this.findExecutable(workspaceRoot, customPath);
    if (!executable) {
      throw new Error(
        'CodeExplorer (ce) executable not found. Please specify "codeExplorer.executablePath" in settings, build the CLI, or install "ce" to PATH.'
      );
    }

    this.outputChannel.appendLine(`[Server] Starting CodeExplorer from: ${executable.command} ${executable.args.join(' ')}`);

    const serverArgs = [
      ...executable.args,
      'serve',
      '--root',
      workspaceRoot,
      '--port',
      configuredPort.toString(),
      '--idle-timeout',
      idleTimeout.toString(),
    ];

    return new Promise<ServerInfo>((resolve, reject) => {
      let isReady = false;
      const child = cp.spawn(executable.command, serverArgs, {
        cwd: workspaceRoot,
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production' },
        windowsHide: true,
      });

      this.serverProcess = child;

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
                resolve(this.serverInfo);
              }
            } catch {
              // Not a JSON line, continue waiting
            }
          }
        });
      }

      if (child.stderr) {
        child.stderr.on('data', (chunk) => {
          this.outputChannel.appendLine(`[ce stderr] ${chunk.toString()}`);
        });
      }

      child.on('error', (err) => {
        this.outputChannel.appendLine(`[Server Error] ${err.message}`);
        clearTimeout(timeout);
        if (!isReady) {
          reject(err);
        }
      });

      child.on('exit', (code, signal) => {
        this.outputChannel.appendLine(`[Server] Process exited with code ${code}, signal ${signal}`);
        this.serverProcess = null;
        this.serverInfo = null;
        if (!isReady) {
          clearTimeout(timeout);
          reject(new Error(`CodeExplorer process exited unexpectedly with code ${code}`));
        }
      });
    });
  }

  /**
   * Resolves the executable command and arguments to launch CodeExplorer.
   */
  private findExecutable(
    workspaceRoot: string,
    customPath?: string
  ): { command: string; args: string[] } | null {
    if (customPath && customPath.trim().length > 0) {
      const resolved = path.isAbsolute(customPath)
        ? customPath
        : path.resolve(workspaceRoot, customPath);
      if (fs.existsSync(resolved)) {
        return resolved.endsWith('.dll')
          ? { command: 'dotnet', args: [resolved] }
          : { command: resolved, args: [] };
      }
    }

    // Check monorepo standard build locations (relative to workspace or extension directory)
    const candidatePaths = [
      path.resolve(workspaceRoot, 'cli', '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.exe'),
      path.resolve(workspaceRoot, 'cli', '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.dll'),
      path.resolve(workspaceRoot, 'cli', '.Build', 'bin_Debug_AnyCPU', 'CodeExplorer', 'ce.exe'),
      path.resolve(workspaceRoot, 'cli', '.Build', 'bin_Debug_AnyCPU', 'CodeExplorer', 'ce.dll'),
      path.resolve(workspaceRoot, '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.exe'),
      path.resolve(workspaceRoot, '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.dll'),
      path.resolve(__dirname, '..', '..', 'cli', '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.exe'),
      path.resolve(__dirname, '..', '..', 'cli', '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.dll'),
      path.resolve(__dirname, '..', '..', 'cli', '.Build', 'bin_Debug_AnyCPU', 'CodeExplorer', 'ce.exe'),
      path.resolve(__dirname, '..', '..', 'cli', '.Build', 'bin_Debug_AnyCPU', 'CodeExplorer', 'ce.dll'),
    ];

    for (const candidate of candidatePaths) {
      if (fs.existsSync(candidate)) {
        return candidate.endsWith('.dll')
          ? { command: 'dotnet', args: [candidate] }
          : { command: candidate, args: [] };
      }
    }

    // Check system PATH
    return { command: 'ce', args: [] };
  }

  dispose() {
    if (this.serverProcess && !this.serverProcess.killed) {
      this.outputChannel.appendLine('[Server] Stopping CodeExplorer server process...');
      this.serverProcess.kill();
      this.serverProcess = null;
      this.serverInfo = null;
    }
  }
}
