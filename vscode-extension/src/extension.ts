import * as vscode from 'vscode';
import * as path from 'path';
import { ProcessManager } from './processManager';
import { CodeExplorerTreeDataProvider } from './codeExplorerTreeProvider';
import {
  registerGraphCommands,
  registerWorkspaceCommands,
  registerIntentCommands
} from './commands';

import { GateStateManager } from './gateStateManager';
import { GraphPanel } from './graphPanel';
import { ServerTransport } from './serverTransport';

let processManager: ProcessManager | null = null;
let serverTransport: ServerTransport | null = null;

export function activate(context: vscode.ExtensionContext) {
  const outputChannel = vscode.window.createOutputChannel('CodeExplorer');
  context.subscriptions.push(outputChannel);

  processManager = new ProcessManager(outputChannel, context);
  context.subscriptions.push(processManager);

  const getWorkspaceRoot = (): string | undefined => {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    return workspaceFolders && workspaceFolders.length > 0 ? workspaceFolders[0].uri.fsPath : undefined;
  };

  // Unified Transport Layer (sticky port allocation, liveness watchdog, pub/sub event bus)
  serverTransport = ServerTransport.initialize(processManager, getWorkspaceRoot, outputChannel);
  context.subscriptions.push(serverTransport);

  // Automatically update all active WebviewPanels whenever server starts or restarts on a port
  serverTransport.onDidServerStart((info) => {
    outputChannel.appendLine(`[CodeExplorer] Server started on port ${info.port} (${info.wsUrl}). Synchronizing open webviews...`);
    GraphPanel.updateAllWsUrls(info.wsUrl);
  });

  // Central Gate State Manager (single source of truth for all gates evaluation)
  const gateStateManager = GateStateManager.initialize(serverTransport, getWorkspaceRoot, outputChannel);
  context.subscriptions.push(gateStateManager);

  // Broadcast gate updates in real-time to all open webviews
  gateStateManager.onDidChangeSuite((event) => {
    GraphPanel.broadcast({
      type: 'GATES_UPDATED',
      suite: event.suite,
      isEvaluating: event.isEvaluating,
      evaluatingGateId: event.evaluatingGateId,
      actionStatus: event.actionStatus,
      error: event.error,
    });
  });

  // Register TreeDataProvider for Sidebar
  const treeDataProvider = new CodeExplorerTreeDataProvider(
    processManager,
    getWorkspaceRoot,
    outputChannel,
    gateStateManager,
    serverTransport
  );
  context.subscriptions.push(
    vscode.window.registerTreeDataProvider('codeExplorerView', treeDataProvider)
  );

  // Status Bar Item
  const statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
  statusBarItem.command = 'codeExplorer.showGraph';
  statusBarItem.text = '$(type-hierarchy-sub) Code Graph';
  statusBarItem.tooltip = 'Show CodeExplorer Architecture Graph';
  context.subscriptions.push(statusBarItem);

  const updateStatusBarVisibility = () => {
    if (getWorkspaceRoot()) {
      statusBarItem.show();
    } else {
      statusBarItem.hide();
    }
  };
  updateStatusBarVisibility();

  serverTransport.onDidChangeState((event) => {
    if (event.state === 'connected' && event.serverInfo) {
      statusBarItem.text = '$(check) Code Graph';
      statusBarItem.tooltip = `CodeExplorer server running at ${event.serverInfo.wsUrl} (HTTP ${event.serverInfo.httpUrl})`;
    } else if (event.state === 'reconnecting' || event.state === 'starting') {
      statusBarItem.text = '$(sync~spin) Code Graph';
      statusBarItem.tooltip = `CodeExplorer server ${event.state} (port ${event.endpoints?.port ?? 'allocating'})...`;
    } else if (event.state === 'error') {
      statusBarItem.text = '$(warning) Code Graph';
      statusBarItem.tooltip = `CodeExplorer server error: ${event.error || 'Unknown error'}. Click to reopen or restart.`;
    } else {
      statusBarItem.text = '$(type-hierarchy-sub) Code Graph';
      statusBarItem.tooltip = 'Show CodeExplorer Architecture Graph';
    }
  });

  // Check workspace on load or workspace folder changes
  const checkAndInitWorkspace = (workspaceRoot: string) => {
    if (!workspaceRoot || !serverTransport) return;
    const hasCe = serverTransport.hasWorkspace(workspaceRoot);
    if (!hasCe) {
      outputChannel.appendLine(`[CodeExplorer] No .codeexplorer directory found in: ${workspaceRoot}`);
      vscode.window
        .showInformationMessage(
          'CodeExplorer: No architecture graph found for this workspace. Initialize and scan now?',
          'Initialize & Scan',
          'Later'
        )
        .then((choice) => {
          if (choice === 'Initialize & Scan') {
            vscode.commands.executeCommand('codeExplorer.initAndScan');
          }
        });
    } else {
      outputChannel.appendLine(`[CodeExplorer] Auto-starting graph server on load for: ${workspaceRoot}`);
      serverTransport
        .ensureConnected(workspaceRoot)
        .then((info) => {
          outputChannel.appendLine(`[CodeExplorer] Server ready at ${info.wsUrl}`);
        })
        .catch((err) => {
          outputChannel.appendLine(`[CodeExplorer] Auto-start notice: ${err.message}`);
        });
    }
  };

  const initialWorkspace = getWorkspaceRoot();
  if (initialWorkspace) {
    setTimeout(() => {
      checkAndInitWorkspace(initialWorkspace);
    }, 500);
  }

  context.subscriptions.push(
    vscode.workspace.onDidChangeWorkspaceFolders(() => {
      updateStatusBarVisibility();
      const root = getWorkspaceRoot();
      if (root) {
        checkAndInitWorkspace(root);
      }
      treeDataProvider.refresh();
    })
  );

  const showWhatsNew = async () => {
    const changelogPath = path.join(context.extensionPath, 'CHANGELOG.md');
    const changelogUri = vscode.Uri.file(changelogPath);
    try {
      await vscode.commands.executeCommand('markdown.showPreview', changelogUri);
    } catch {
      await vscode.commands.executeCommand('vscode.open', changelogUri);
    }
  };

  registerGraphCommands(context, processManager, treeDataProvider, outputChannel, getWorkspaceRoot, serverTransport);
  registerWorkspaceCommands(context, processManager, treeDataProvider, outputChannel, getWorkspaceRoot, showWhatsNew);
  registerIntentCommands(context, processManager, treeDataProvider, outputChannel, getWorkspaceRoot);

  // Check for extension update and show What's New prompt
  const previousVersion = context.globalState.get<string>('codeExplorer.lastVersion');
  const currentVersion = context.extension?.packageJSON?.version;
  if (currentVersion) {
    if (previousVersion && previousVersion !== currentVersion) {
      vscode.window
        .showInformationMessage(
          `CodeExplorer updated to v${currentVersion}!`,
          "What's New",
          'Dismiss'
        )
        .then((selection) => {
          if (selection === "What's New") {
            showWhatsNew();
          }
        });
    }
    context.globalState.update('codeExplorer.lastVersion', currentVersion);
  }

  outputChannel.appendLine('CodeExplorer extension activated.');
}

export function deactivate() {
  if (serverTransport) {
    serverTransport.dispose();
  }
  if (processManager) {
    processManager.dispose();
  }
}
