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

    this.outputChannel.appendLine(`[ProcessManager] Preparing CodeExplorer server for workspace: ${workspaceRoot}`);
    const executable = this.findExecutable(workspaceRoot, customPath);
    if (!executable) {
      this.outputChannel.appendLine('[ProcessManager] Error: No valid CodeExplorer executable could be found.');
      throw new Error(
        'CodeExplorer (ce) executable not found. Please specify "codeExplorer.executablePath" in settings, build the CLI, or install "ce" to PATH.'
      );
    }

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

    this.outputChannel.appendLine(`[ProcessManager] Spawning: ${executable.command} ${serverArgs.join(' ')} (CWD: ${workspaceRoot})`);

    return new Promise<ServerInfo>((resolve, reject) => {
      let isReady = false;
      const child = cp.spawn(executable.command, serverArgs, {
        cwd: workspaceRoot,
        env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production' },
        windowsHide: true,
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
          const detailedError = new Error(`${report.title}\n${report.summary}\n\nSuggested Action:\n${report.suggestion}\n\n${report.fullReport}`);
          (detailedError as any).diagnosticReport = report;
          reject(detailedError);
        }
      });

      child.on('exit', (code, signal) => {
        this.outputChannel.appendLine(`[Server] Process exited with code ${code}, signal ${signal}`);
        this.serverProcess = null;
        this.serverInfo = null;
        if (!isReady) {
          clearTimeout(timeout);
          const rawStderr = stderrChunks.join('');
          const report = diagnoseProcessExit(code, signal, rawStderr, executable.command, serverArgs, workspaceRoot);
          this.outputChannel.appendLine('\n' + report.fullReport + '\n');
          const detailedError = new Error(`${report.title}\n${report.summary}\n\nSuggested Action:\n${report.suggestion}\n\n${report.fullReport}`);
          (detailedError as any).diagnosticReport = report;
          reject(detailedError);
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
        const dllFallback = resolved.replace(/\.exe$/i, '.dll');
        if (resolved.endsWith('.exe') && fs.existsSync(dllFallback)) {
          return { command: 'dotnet', args: [dllFallback] };
        }
        return resolved.endsWith('.dll')
          ? { command: 'dotnet', args: [resolved] }
          : { command: resolved, args: [] };
      }
    }

    // 1. Check bundled platform-specific or universal binaries
    const isWindows = process.platform === 'win32';
    const isMac = process.platform === 'darwin';
    const isLinux = process.platform === 'linux';
    const arch = process.arch;
    const binName = isWindows ? 'ce.exe' : 'ce';

    const hostRids: string[] = [];
    if (isWindows) {
      if (arch === 'arm64') hostRids.push('win-arm64');
      hostRids.push('win-x64');
    } else if (isMac) {
      if (arch === 'arm64') hostRids.push('osx-arm64');
      hostRids.push('osx-x64');
    } else if (isLinux) {
      if (arch === 'arm64') hostRids.push('linux-arm64');
      hostRids.push('linux-x64');
    }

    const extensionRoot = path.resolve(__dirname, '..');
    const bundledCandidates: string[] = [
      // Platform-specific package layout: bin/ce[.exe]
      path.resolve(extensionRoot, 'bin', binName),
    ];

    // Multi-target / universal package layout: bin/<rid>/ce[.exe]
    for (const rid of hostRids) {
      bundledCandidates.push(path.resolve(extensionRoot, 'bin', rid, binName));
    }

    for (const binPath of bundledCandidates) {
      if (fs.existsSync(binPath)) {
        if (!isWindows) {
          try {
            fs.chmodSync(binPath, 0o755);
          } catch (e: any) {
            this.outputChannel.appendLine(`[ProcessManager] Warning: failed to chmod +x on ${binPath}: ${e.message}`);
          }
        }
        this.outputChannel.appendLine(`[ProcessManager] Using bundled CodeExplorer binary: ${binPath}`);
        return { command: binPath, args: [] };
      }
    }

    // 2. Check monorepo standard build locations (relative to workspace or extension directory)
    // Always prefer .dll over .exe in local build output directories because running app-host .exe
    // directly from build folders where hostfxr.dll is present causes .NET to search for shared runtimes locally,
    // failing with exit code 2147516566 (0x80008096). Invoking via 'dotnet <path>.dll' uses the host correctly.
    const candidatePaths = [
      path.resolve(workspaceRoot, 'cli', '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.dll'),
      path.resolve(workspaceRoot, 'cli', '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.exe'),
      path.resolve(workspaceRoot, 'cli', '.Build', 'bin_Debug_AnyCPU', 'CodeExplorer', 'ce.dll'),
      path.resolve(workspaceRoot, 'cli', '.Build', 'bin_Debug_AnyCPU', 'CodeExplorer', 'ce.exe'),
      path.resolve(workspaceRoot, '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.dll'),
      path.resolve(workspaceRoot, '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.exe'),
      path.resolve(__dirname, '..', '..', 'cli', '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.dll'),
      path.resolve(__dirname, '..', '..', 'cli', '.Build', 'bin_Release_AnyCPU', 'CodeExplorer', 'ce.exe'),
      path.resolve(__dirname, '..', '..', 'cli', '.Build', 'bin_Debug_AnyCPU', 'CodeExplorer', 'ce.dll'),
      path.resolve(__dirname, '..', '..', 'cli', '.Build', 'bin_Debug_AnyCPU', 'CodeExplorer', 'ce.exe'),
    ];

    for (const candidate of candidatePaths) {
      if (fs.existsSync(candidate)) {
        const dllCandidate = candidate.endsWith('.exe')
          ? candidate.replace(/\.exe$/i, '.dll')
          : candidate;
        if (fs.existsSync(dllCandidate)) {
          this.outputChannel.appendLine(`[ProcessManager] Found local binary candidate: dotnet ${dllCandidate}`);
          return { command: 'dotnet', args: [dllCandidate] };
        }
        this.outputChannel.appendLine(`[ProcessManager] Found local binary candidate: ${candidate}`);
        return candidate.endsWith('.dll')
          ? { command: 'dotnet', args: [candidate] }
          : { command: candidate, args: [] };
      }
    }

    // Check system PATH
    this.outputChannel.appendLine('[ProcessManager] No local monorepo binaries found. Using system PATH "ce".');
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
    suggestion = 'Install the .NET 10 Runtime or SDK (x64) from https://dotnet.microsoft.com/download/dotnet/10.0 or check installed runtimes via `dotnet --list-runtimes`.';
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
    `Exit Code: ${code} (hex: ${code !== null ? '0x' + (code >>> 0).toString(16).toUpperCase() : 'N/A'}), Signal: ${signal ?? 'none'}`,
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

