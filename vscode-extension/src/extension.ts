import * as vscode from 'vscode';
import * as path from 'path';
import { ProcessManager } from './processManager';
import { CodeExplorerTreeDataProvider } from './codeExplorerTreeProvider';
import {
  registerGraphCommands,
  registerWorkspaceCommands,
  registerIntentCommands
} from './commands';

let processManager: ProcessManager | null = null;

export function activate(context: vscode.ExtensionContext) {
  const outputChannel = vscode.window.createOutputChannel('CodeExplorer');
  context.subscriptions.push(outputChannel);

  processManager = new ProcessManager(outputChannel, context);
  context.subscriptions.push(processManager);

  const getWorkspaceRoot = (): string | undefined => {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    return workspaceFolders && workspaceFolders.length > 0 ? workspaceFolders[0].uri.fsPath : undefined;
  };

  // Register TreeDataProvider for Sidebar
  const treeDataProvider = new CodeExplorerTreeDataProvider(processManager, getWorkspaceRoot, outputChannel);
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

  processManager.onDidServerStart((info) => {
    statusBarItem.text = '$(check) Code Graph';
    statusBarItem.tooltip = `CodeExplorer server running at ${info.wsUrl} (HTTP ${info.httpUrl})`;
  });

  processManager.onDidServerStop(() => {
    statusBarItem.text = '$(type-hierarchy-sub) Code Graph';
    statusBarItem.tooltip = 'Show CodeExplorer Architecture Graph';
  });

  // Check workspace on load or workspace folder changes
  const checkAndInitWorkspace = (workspaceRoot: string) => {
    if (!workspaceRoot) return;
    const hasCe = processManager!.hasWorkspace(workspaceRoot);
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
      processManager!
        .ensureServerStarted(workspaceRoot)
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

  registerGraphCommands(context, processManager, treeDataProvider, outputChannel, getWorkspaceRoot);
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
  if (processManager) {
    processManager.dispose();
  }
}
