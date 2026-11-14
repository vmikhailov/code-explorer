import * as vscode from 'vscode';
import { EngineSupervisor, EngineSupervisorStatus } from '../engine/engineSupervisor';
import { ServerTransport, TransportStateSnapshot } from '../serverTransport';

export const CMD_STATUS_BAR_MENU = 'codeExplorer.statusBarQuickActions';
export const CMD_RETRY_START = 'codeExplorer.retryEngineStart';

const STATUS_BAR_ID = 'CodeExplorerStatusBar';
const MSG_ENGINE_STARTED = 'CodeExplorer engine started.';
const ACTION_RETRY_START = 'Retry Start';
const ACTION_OPEN_LAUNCH_LOGS = 'Open Launch Logs';
const CMD_REINDEX = 'codeExplorer.reindex';
const CMD_SHOW_GRAPH = 'codeExplorer.showGraph';
const THEME_ERROR_BG = 'statusBarItem.errorBackground';

/**
 * StatusBarController (`CodeExplorerStatusBar`)
 *
 * Displays live engine lifecycle state (`Idle`, `Starting…`, `Ready`, `Scanning…`, `Failed`)
 * in the VS Code Status Bar and provides one-click quick actions:
 * - `Retry Start`
 * - `Rescan Workspace`
 * - `Open Launch Logs`
 * - `Open Protocol Logs`
 * - `Show Architecture Graph`
 */
export class StatusBarController implements vscode.Disposable {
  private readonly _statusBarItem: vscode.StatusBarItem;
  private readonly _disposables: vscode.Disposable[] = [];

  public constructor(
    private readonly supervisor: EngineSupervisor,
    private readonly transport: ServerTransport,
    private readonly getWorkspaceRoot: () => string | undefined
  ) {
    this._statusBarItem = vscode.window.createStatusBarItem(
      STATUS_BAR_ID,
      vscode.StatusBarAlignment.Right,
      100
    );
    this._statusBarItem.name = 'CodeExplorer Engine Status';
    this._statusBarItem.command = CMD_STATUS_BAR_MENU;
    this._disposables.push(this._statusBarItem);

    this.registerCommands();

    this._disposables.push(
      this.supervisor.onDidChangeStatus((status) => {
        this.renderFromSupervisor(status);
      }),
      this.transport.onDidChangeState((snapshot) => {
        this.syncFromTransport(snapshot);
      })
    );

    this.updateVisibility();
    this.renderFromSupervisor(this.supervisor.getStatus());
  }

  public updateVisibility(): void {
    if (this.getWorkspaceRoot()) {
      this._statusBarItem.show();
    } else {
      this._statusBarItem.hide();
    }
  }

  private registerCommands(): void {
    const retryCmd = vscode.commands.registerCommand(CMD_RETRY_START, async () => {
      try {
        await this.supervisor.retry(this.getWorkspaceRoot());
        vscode.window.showInformationMessage(MSG_ENGINE_STARTED);
      } catch (err: any) {
        const msg = err?.message?.split('\n')[0] || String(err);
        const choice = await vscode.window.showErrorMessage(
          `CodeExplorer engine failed to start: ${msg}`,
          ACTION_RETRY_START,
          ACTION_OPEN_LAUNCH_LOGS
        );
        if (choice === ACTION_RETRY_START) {
          await vscode.commands.executeCommand(CMD_RETRY_START);
        } else if (choice === ACTION_OPEN_LAUNCH_LOGS) {
          this.supervisor.showLaunchLogs();
        }
      }
    });

    const menuCmd = vscode.commands.registerCommand(CMD_STATUS_BAR_MENU, async () => {
      await this.showQuickActionsMenu();
    });

    this._disposables.push(retryCmd, menuCmd);
  }

  private async showQuickActionsMenu(): Promise<void> {
    const status = this.supervisor.getStatus();
    const items: Array<vscode.QuickPickItem & { action: string }> = [];

    if (status.state === 'Failed') {
      items.push(
        {
          label: '$(debug-restart) Retry Start',
          description: 'Reset failure state and retry launching ce serve',
          action: 'retry',
        },
        {
          label: '$(output) Open Launch Logs',
          description: 'Inspect CodeExplorer process discovery and stderr output',
          action: 'launchLogs',
        }
      );
    } else {
      items.push(
        {
          label: '$(type-hierarchy-sub) Show Architecture Graph',
          description: 'Open the CodeExplorer architecture diagram view',
          action: 'showGraph',
        },
        {
          label: '$(sync) Rescan Workspace',
          description: 'Incrementally index workspace source files',
          action: 'rescan',
        },
        {
          label: '$(debug-restart) Retry Start / Restart Engine',
          description: 'Restart the background ce serve engine process',
          action: 'retry',
        },
        {
          label: '$(output) Open Launch Logs',
          description: 'Open OutputChannel("CodeExplorer")',
          action: 'launchLogs',
        },
        {
          label: '$(radio-tower) Open Protocol Logs',
          description: 'Open OutputChannel("CodeExplorer (Protocol)")',
          action: 'protocolLogs',
        }
      );
    }

    const picked = await vscode.window.showQuickPick(items, {
      placeHolder: `CodeExplorer Engine: ${status.state}${status.failure ? ` (${status.failure.reason.split('\n')[0]})` : ''}`,
    });

    if (!picked) return;

    switch (picked.action) {
      case 'retry':
        await vscode.commands.executeCommand(CMD_RETRY_START);
        break;
      case 'rescan':
        await vscode.commands.executeCommand(CMD_REINDEX);
        break;
      case 'launchLogs':
        this.supervisor.showLaunchLogs();
        break;
      case 'protocolLogs':
        this.supervisor.showProtocolLogs();
        break;
      case 'showGraph':
        await vscode.commands.executeCommand(CMD_SHOW_GRAPH);
        break;
    }
  }

  private syncFromTransport(snapshot: TransportStateSnapshot): void {
    const supStatus = this.supervisor.getStatus();
    // If supervisor is Idle but transport connected/failed directly via legacy callers, reflect it
    if (supStatus.state === 'Idle') {
      if (snapshot.state === 'connected' && snapshot.serverInfo) {
        this._statusBarItem.backgroundColor = undefined;
        this._statusBarItem.text = '$(check) CodeExplorer: Ready';
        this._statusBarItem.tooltip = `CodeExplorer running at ${snapshot.serverInfo.httpUrl}. Click for quick actions.`;
      } else if (snapshot.state === 'starting' || snapshot.state === 'reconnecting') {
        this._statusBarItem.backgroundColor = undefined;
        this._statusBarItem.text = '$(sync~spin) CodeExplorer: Starting…';
        this._statusBarItem.tooltip = 'CodeExplorer engine is starting…';
      } else if (snapshot.state === 'error') {
        this._statusBarItem.backgroundColor = new vscode.ThemeColor(THEME_ERROR_BG);
        this._statusBarItem.text = '$(error) CodeExplorer: Failed';
        this._statusBarItem.tooltip = `CodeExplorer failed: ${snapshot.error?.split('\n')[0] || 'Unknown error'}. Click to Retry Start or Open Launch Logs.`;
      }
    }
  }

  private renderFromSupervisor(status: EngineSupervisorStatus): void {
    if (status.isScanning) {
      this._statusBarItem.backgroundColor = undefined;
      this._statusBarItem.text = `$(sync~spin) CodeExplorer: ${status.scanLabel || 'Scanning…'}`;
      this._statusBarItem.tooltip = 'CodeExplorer is scanning workspace files…';
      return;
    }

    switch (status.state) {
      case 'Idle':
      case 'Stopping': {
        this._statusBarItem.backgroundColor = undefined;
        this._statusBarItem.text = '$(type-hierarchy-sub) CodeExplorer: Idle';
        this._statusBarItem.tooltip = 'CodeExplorer Engine: Idle. Click for quick actions.';
        break;
      }
      case 'Discovering':
      case 'Starting':
      case 'RetryingOnce': {
        this._statusBarItem.backgroundColor = undefined;
        this._statusBarItem.text = '$(sync~spin) CodeExplorer: Starting…';
        this._statusBarItem.tooltip =
          status.state === 'RetryingOnce'
            ? 'CodeExplorer Engine: Retrying start (attempt 2/2)…'
            : 'CodeExplorer Engine: Starting…';
        break;
      }
      case 'Ready': {
        this._statusBarItem.backgroundColor = undefined;
        const projSuffix =
          typeof status.projectCount === 'number' && status.projectCount > 0
            ? ` · ${status.projectCount} projects`
            : '';
        this._statusBarItem.text = `$(check) CodeExplorer: Ready${projSuffix}`;
        const urlInfo = status.serverInfo ? ` (${status.serverInfo.httpUrl})` : '';
        this._statusBarItem.tooltip = `CodeExplorer Engine: Ready${urlInfo}. Click to Rescan or Open Logs.`;
        break;
      }
      case 'Failed': {
        this._statusBarItem.backgroundColor = new vscode.ThemeColor(THEME_ERROR_BG);
        this._statusBarItem.text = '$(error) CodeExplorer: Failed';
        const reason = status.failure?.reason?.split('\n')[0] || 'Engine failed to start';
        this._statusBarItem.tooltip = `CodeExplorer Engine Failed: ${reason}. Click for Retry Start or Open Launch Logs.`;
        break;
      }
    }
  }

  public dispose(): void {
    while (this._disposables.length > 0) {
      this._disposables.pop()?.dispose();
    }
  }
}
