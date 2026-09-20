import * as vscode from 'vscode';
import { ProcessManager } from './processManager';
import { GraphPanel } from './graphPanel';

let processManager: ProcessManager | null = null;

export function activate(context: vscode.ExtensionContext) {
  const outputChannel = vscode.window.createOutputChannel('CodeExplorer');
  context.subscriptions.push(outputChannel);

  processManager = new ProcessManager(outputChannel);
  context.subscriptions.push(processManager);

  // Status Bar Item
  const statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
  statusBarItem.command = 'codeExplorer.showGraph';
  statusBarItem.text = '$(type-hierarchy-sub) Code Graph';
  statusBarItem.tooltip = 'Show CodeExplorer Architecture Graph';
  statusBarItem.show();
  context.subscriptions.push(statusBarItem);

  // Command: Show Graph
  const showGraphCommand = vscode.commands.registerCommand(
    'codeExplorer.showGraph',
    async () => {
      const workspaceFolders = vscode.workspace.workspaceFolders;
      if (!workspaceFolders || workspaceFolders.length === 0) {
        vscode.window.showWarningMessage('Please open a project workspace folder to visualize its architecture graph.');
        return;
      }

      const workspaceRoot = workspaceFolders[0].uri.fsPath;

      await vscode.window.withProgress(
        {
          location: vscode.ProgressLocation.Notification,
          title: 'CodeExplorer: Starting graph server...',
          cancellable: false,
        },
        async (progress) => {
          try {
            progress.report({ message: 'Connecting to ce serve...' });
            const serverInfo = await processManager!.ensureServerStarted(workspaceRoot);
            GraphPanel.createOrShow(context.extensionUri, serverInfo.wsUrl, workspaceRoot);
          } catch (err: any) {
            outputChannel.appendLine(`[Activation Error] ${err.message}`);
            vscode.window.showErrorMessage(`CodeExplorer Server Error: ${err.message}`);
          }
        }
      );
    }
  );

  // Command: Reindex Workspace
  const reindexCommand = vscode.commands.registerCommand(
    'codeExplorer.reindex',
    async () => {
      const workspaceFolders = vscode.workspace.workspaceFolders;
      if (!workspaceFolders || workspaceFolders.length === 0) {
        vscode.window.showWarningMessage('Please open a workspace folder to reindex.');
        return;
      }

      vscode.window.showInformationMessage('Triggering CodeExplorer workspace reindexing...');
      // If GraphPanel is open, we can send trigger scan request over WS
      if (GraphPanel.currentPanel) {
        GraphPanel.currentPanel.postMessage({ type: 'TRIGGER_SCAN' });
      }
    }
  );

  context.subscriptions.push(showGraphCommand, reindexCommand);
  outputChannel.appendLine('CodeExplorer extension activated.');
}

export function deactivate() {
  if (processManager) {
    processManager.dispose();
  }
}
