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
import { GraphInspectorViewProvider } from './graphInspectorViewProvider';
import { ServerTransport } from './serverTransport';
import { initializeMcpAutoRegistration } from './mcpRegistration';

let processManager: ProcessManager | null = null;
let serverTransport: ServerTransport | null = null;

const VIEW_ID_CODE_EXPLORER = 'codeExplorerView';
const CMD_FOCUS_INSPECTOR = 'codeExplorer.focusInspector';
const CMD_INIT_AND_SCAN = 'codeExplorer.initAndScan';
const CMD_MARKDOWN_PREVIEW = 'markdown.showPreview';
const CMD_VSCODE_OPEN = 'vscode.open';
const STATE_KEY_LAST_VERSION = 'codeExplorer.lastVersion';
const STATE_KEY_INIT_DISMISSED = 'codeExplorer.initPromptDismissed';
const OUTPUT_CHANNEL_MAIN = 'CodeExplorer';
const OUTPUT_CHANNEL_PROTOCOL = 'CodeExplorer (Protocol)';
const PROMPT_NO_GRAPH = 'CodeExplorer: No architecture graph found for this workspace. Initialize and scan now?';
const ACTION_INIT_AND_SCAN = 'Initialize & Scan';
const ACTION_LATER = 'Later';
const ACTION_WHATS_NEW = "What's New";
const ACTION_DISMISS = 'Dismiss';
const FILE_CHANGELOG = 'CHANGELOG.md';

export function activate(context: vscode.ExtensionContext) {
  const outputChannel = vscode.window.createOutputChannel(OUTPUT_CHANNEL_MAIN);
  const protocolChannel = vscode.window.createOutputChannel(OUTPUT_CHANNEL_PROTOCOL);
  context.subscriptions.push(outputChannel, protocolChannel);

  processManager = new ProcessManager(outputChannel, context);
  context.subscriptions.push(processManager);

  const getWorkspaceRoot = (): string | undefined => {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    return workspaceFolders && workspaceFolders.length > 0 ? workspaceFolders[0].uri.fsPath : undefined;
  };

  // Unified Transport Layer (sticky port allocation, liveness watchdog, pub/sub event bus)
  serverTransport = ServerTransport.initialize(processManager, getWorkspaceRoot, protocolChannel);
  context.subscriptions.push(serverTransport);

  // Automatically update all active WebviewPanels whenever server starts or restarts on a port
  serverTransport.onDidServerStart((info) => {
    outputChannel.appendLine(`[CodeExplorer] Server started on port ${info.port} (${info.wsUrl}). Synchronizing open webviews...`);
    protocolChannel.appendLine(`[Protocol] Server started on port ${info.port} (${info.wsUrl}). Synchronizing open webviews...`);
    GraphPanel.updateAllWsUrls(info.wsUrl);
  });

  // Central Gate State Manager (single source of truth for all gates evaluation)
  const gateStateManager = GateStateManager.initialize(serverTransport, getWorkspaceRoot, protocolChannel);
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
    vscode.window.registerTreeDataProvider(VIEW_ID_CODE_EXPLORER, treeDataProvider)
  );

  // Register Graph Inspector & Controls WebviewViewProvider for VS Code Sidebar / Secondary Sidebar
  const inspectorViewProvider = new GraphInspectorViewProvider(
    context.extensionUri,
    serverTransport,
    getWorkspaceRoot,
    protocolChannel
  );
  context.subscriptions.push(
    inspectorViewProvider,
    vscode.window.registerWebviewViewProvider(
      GraphInspectorViewProvider.viewType,
      inspectorViewProvider,
      { webviewOptions: { retainContextWhenHidden: true } }
    ),
    vscode.commands.registerCommand(CMD_FOCUS_INSPECTOR, async () => {
      await inspectorViewProvider.reveal(false);
    })
  );

  // Register WebviewPanelSerializers so surviving/restored graph panels reconnect automatically after Extension Host restarts
  if (typeof vscode.window.registerWebviewPanelSerializer === 'function') {
    for (const mode of GraphPanel.VIEW_MODES) {
      context.subscriptions.push(
        vscode.window.registerWebviewPanelSerializer(`codeExplorerGraph.${mode}`, {
          async deserializeWebviewPanel(webviewPanel: vscode.WebviewPanel, _state: any) {
            const wsRoot = getWorkspaceRoot() || '';
            const currentTransport = serverTransport!;
            let initialWsUrl = currentTransport.getEndpoints()?.wsUrl || '';
            if (!initialWsUrl && wsRoot) {
              try {
                await currentTransport.ensurePortAllocated(false, wsRoot);
                initialWsUrl = currentTransport.getEndpoints()?.wsUrl || '';
              } catch (err) {
                outputChannel.appendLine(`[CodeExplorer] Port allocation notice during panel revival: ${err}`);
              }
            }
            outputChannel.appendLine(`[CodeExplorer] Reviving webview panel codeExplorerGraph.${mode} (${initialWsUrl})`);
            GraphPanel.revive(
              webviewPanel,
              context.extensionUri,
              currentTransport,
              initialWsUrl,
              wsRoot,
              protocolChannel,
              mode
            );
            if (wsRoot && currentTransport.hasWorkspace(wsRoot)) {
              currentTransport
                .ensureConnected(wsRoot)
                .then((info) => {
                  GraphPanel.updateAllWsUrls(info.wsUrl);
                })
                .catch((err) => {
                  outputChannel.appendLine(`[CodeExplorer] Revived panel server connect error: ${err}`);
                });
            }
          },
        })
      );
    }
  }

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
      if (context.workspaceState.get<boolean>(STATE_KEY_INIT_DISMISSED)) {
        return;
      }
      vscode.window
        .showInformationMessage(
          PROMPT_NO_GRAPH,
          ACTION_INIT_AND_SCAN,
          ACTION_LATER
        )
        .then((choice) => {
          if (choice === ACTION_INIT_AND_SCAN) {
            vscode.commands.executeCommand(CMD_INIT_AND_SCAN);
          } else if (choice === ACTION_LATER) {
            void context.workspaceState.update(STATE_KEY_INIT_DISMISSED, true);
          }
        });
    } else {
      outputChannel.appendLine(`[CodeExplorer] Auto-starting graph server on load for: ${workspaceRoot}`);
      serverTransport
        .ensureConnected(workspaceRoot)
        .then(async (info) => {
          outputChannel.appendLine(`[CodeExplorer] Server ready at ${info.wsUrl}`);
          try {
            const metadataEndpoint = '/api/metadata';
            const convertSchemaOption = 'Convert Schema';
            const rebuildGraphOption = 'Rebuild Graph';
            const laterOption = 'Later';
            const upgradeSchemaCommandId = 'codeExplorer.upgradeSchema';
            const reindexFullCommandId = 'codeExplorer.reindexFull';
            const meta = await serverTransport?.get<{
              isSchemaOutdated?: boolean;
              schemaVersion?: number;
              currentSchemaVersion?: number;
            }>(metadataEndpoint, { autoStart: false });
            if (meta?.isSchemaOutdated) {
              const choice = await vscode.window.showWarningMessage(
                `CodeExplorer: Database schema version is outdated (v${meta.schemaVersion ?? 1} < v${meta.currentSchemaVersion ?? 6}). Convert database schema now?`,
                convertSchemaOption,
                rebuildGraphOption,
                laterOption
              );
              if (choice === convertSchemaOption) {
                vscode.commands.executeCommand(upgradeSchemaCommandId);
              } else if (choice === rebuildGraphOption) {
                vscode.commands.executeCommand(reindexFullCommandId);
              }
            }
          } catch {
            // Ignore metadata check errors on initial startup
          }
        })
        .catch((err) => {
          outputChannel.appendLine(`[CodeExplorer] Auto-start notice: ${err.message}`);
        });
    }
  };

  const initialWorkspace = getWorkspaceRoot();
  if (initialWorkspace) {
    checkAndInitWorkspace(initialWorkspace);
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
    const changelogPath = path.join(context.extensionPath, FILE_CHANGELOG);
    const changelogUri = vscode.Uri.file(changelogPath);
    try {
      await vscode.commands.executeCommand(CMD_MARKDOWN_PREVIEW, changelogUri);
    } catch {
      await vscode.commands.executeCommand(CMD_VSCODE_OPEN, changelogUri);
    }
  };

  registerGraphCommands(context, processManager, treeDataProvider, outputChannel, getWorkspaceRoot, serverTransport, protocolChannel);
  registerWorkspaceCommands(context, processManager, treeDataProvider, outputChannel, getWorkspaceRoot, showWhatsNew, protocolChannel);
  registerIntentCommands(context, processManager, treeDataProvider, outputChannel, getWorkspaceRoot);
  initializeMcpAutoRegistration(context, processManager, getWorkspaceRoot, outputChannel);

  // Check for extension update and show What's New prompt
  const previousVersion = context.globalState.get<string>(STATE_KEY_LAST_VERSION);
  const currentVersion = context.extension?.packageJSON?.version;
  if (currentVersion) {
    if (previousVersion && previousVersion !== currentVersion) {
      vscode.window
        .showInformationMessage(
          `CodeExplorer updated to v${currentVersion}!`,
          ACTION_WHATS_NEW,
          ACTION_DISMISS
        )
        .then((selection) => {
          if (selection === ACTION_WHATS_NEW) {
            showWhatsNew();
          }
        });
    }
    context.globalState.update(STATE_KEY_LAST_VERSION, currentVersion);
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
