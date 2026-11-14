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
import { EngineSupervisor } from './engine/engineSupervisor';
import { HostRpcBroker } from './rpc/hostRpcBroker';
import { StatusBarController } from './ui/statusBarController';
import { initializeMcpAutoRegistration } from './mcpRegistration';

let processManager: ProcessManager | null = null;
let serverTransport: ServerTransport | null = null;
let engineSupervisor: EngineSupervisor | null = null;
let hostRpcBroker: HostRpcBroker | null = null;

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
const TOPIC_SERVER_READY = 'server:ready';
const CFG_EXECUTABLE_PATH = 'codeExplorer.executablePath';
const CFG_ENGINE_VERSION = 'codeExplorer.engineVersion';

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

  // Deterministic EngineSupervisor & Single-Broker Host RPC
  engineSupervisor = EngineSupervisor.initialize(
    processManager,
    serverTransport,
    getWorkspaceRoot,
    outputChannel,
    protocolChannel
  );
  hostRpcBroker = HostRpcBroker.initialize(engineSupervisor, protocolChannel);
  context.subscriptions.push(engineSupervisor, { dispose: () => hostRpcBroker?.dispose() });

  // Automatically update all active WebviewPanels whenever server starts or restarts on a port
  serverTransport.onDidServerStart((info) => {
    outputChannel.appendLine(`[CodeExplorer] Server started on port ${info.port} (${info.wsUrl}). Synchronizing open webviews...`);
    protocolChannel.appendLine(`[Protocol] Server started on port ${info.port} (${info.wsUrl}). Synchronizing open webviews...`);
    GraphPanel.updateAllWsUrls(info.wsUrl);
    hostRpcBroker?.broadcastEvent(TOPIC_SERVER_READY, info);
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
            if (wsRoot && currentTransport.hasWorkspace(wsRoot) && engineSupervisor) {
              engineSupervisor
                .ensureReady(wsRoot)
                .then((info) => {
                  GraphPanel.updateAllWsUrls(info.wsUrl);
                })
                .catch((err) => {
                  outputChannel.appendLine(`[CodeExplorer] Revived panel server connect notice: ${err.message || err}`);
                });
            }
          },
        })
      );
    }
  }

  // Status Bar Controller (Idle, Starting…, Ready, Scanning…, Failed + Quick Actions)
  const statusBarController = new StatusBarController(
    engineSupervisor,
    serverTransport,
    getWorkspaceRoot
  );
  context.subscriptions.push(
    statusBarController,
    vscode.workspace.onDidChangeConfiguration((e) => {
      if (
        e.affectsConfiguration(CFG_EXECUTABLE_PATH) ||
        e.affectsConfiguration(CFG_ENGINE_VERSION)
      ) {
        void engineSupervisor?.onExecutableSettingChanged(getWorkspaceRoot());
      }
    })
  );

  // Check workspace on load or workspace folder changes
  const checkAndInitWorkspace = (workspaceRoot: string) => {
    if (!workspaceRoot || !serverTransport || !engineSupervisor) return;
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
      engineSupervisor
        .ensureReady(workspaceRoot)
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
      statusBarController.updateVisibility();
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
  if (hostRpcBroker) {
    hostRpcBroker.dispose();
  }
  if (engineSupervisor) {
    engineSupervisor.dispose();
  }
  if (serverTransport) {
    serverTransport.dispose();
  }
  if (processManager) {
    processManager.dispose();
  }
}
