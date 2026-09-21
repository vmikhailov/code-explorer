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
            GraphPanel.createOrShow(context.extensionUri, serverInfo.wsUrl, workspaceRoot, outputChannel);
          } catch (err: any) {
            outputChannel.appendLine(`\n[CodeExplorer Server Activation Error]\n${err.message}\n`);
            const firstLine = err.message.split('\n')[0] || 'Server process failed';
            const action = await vscode.window.showErrorMessage(
              `CodeExplorer Error: ${firstLine}`,
              'Show Logs',
              'Copy Error Details'
            );
            if (action === 'Show Logs') {
              outputChannel.show(true);
            } else if (action === 'Copy Error Details') {
              const fullDetails = (err as any).diagnosticReport?.fullReport || err.stack || err.message;
              await vscode.env.clipboard.writeText(fullDetails);
              vscode.window.showInformationMessage('CodeExplorer diagnostic details copied to clipboard.');
            }
          }
        }
      );
    }
  );

  const triggerScan = async (clear: boolean) => {
    const workspaceFolders = vscode.workspace.workspaceFolders;
    if (!workspaceFolders || workspaceFolders.length === 0) {
      vscode.window.showWarningMessage('Please open a workspace folder to reindex.');
      return;
    }

    if (!GraphPanel.currentPanel) {
      await vscode.commands.executeCommand('codeExplorer.showGraph');
      setTimeout(() => {
        if (GraphPanel.currentPanel) {
          GraphPanel.currentPanel.postMessage({ type: 'TRIGGER_SCAN', clear });
        }
      }, 1000);
    } else {
      GraphPanel.currentPanel.postMessage({ type: 'TRIGGER_SCAN', clear });
    }
  };

  // Command: Rescan Workspace (Incremental)
  const reindexCommand = vscode.commands.registerCommand(
    'codeExplorer.reindex',
    async () => {
      await triggerScan(false);
    }
  );

  // Command: Full Re-index (Clear & Rescan)
  const reindexFullCommand = vscode.commands.registerCommand(
    'codeExplorer.reindexFull',
    async () => {
      const confirm = await vscode.window.showWarningMessage(
        'Are you sure you want to clear the graph database and run a full re-index?',
        { modal: true },
        'Clear & Rebuild'
      );
      if (confirm === 'Clear & Rebuild') {
        await triggerScan(true);
      }
    }
  );

  // Command: Show Logs
  const showLogsCommand = vscode.commands.registerCommand(
    'codeExplorer.showLogs',
    () => {
      outputChannel.show(true);
    }
  );

  context.subscriptions.push(showGraphCommand, reindexCommand, reindexFullCommand, showLogsCommand);
  outputChannel.appendLine('CodeExplorer extension activated.');
}

export function deactivate() {
  if (processManager) {
    processManager.dispose();
  }
}
