import * as vscode from 'vscode';
import * as path from 'path';
import { ProcessManager } from './processManager';
import { GraphPanel } from './graphPanel';
import { CodeExplorerTreeDataProvider } from './codeExplorerTreeProvider';
import { isProjectKind } from '../../proto/types';

let processManager: ProcessManager | null = null;

export function activate(context: vscode.ExtensionContext) {
  const outputChannel = vscode.window.createOutputChannel('CodeExplorer');
  context.subscriptions.push(outputChannel);

  processManager = new ProcessManager(outputChannel);
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

  // Command: Show Graph
  const showGraphCommand = vscode.commands.registerCommand(
    'codeExplorer.showGraph',
    async () => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a project workspace folder to visualize its architecture graph.');
        return;
      }

      if (!processManager!.hasWorkspace(workspaceRoot)) {
        const choice = await vscode.window.showInformationMessage(
          'CodeExplorer: No architecture graph found for this workspace. Initialize and scan now?',
          'Initialize & Scan',
          'Cancel'
        );
        if (choice === 'Initialize & Scan') {
          vscode.commands.executeCommand('codeExplorer.initAndScan');
        }
        return;
      }

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
            GraphPanel.createOrShow(context.extensionUri, serverInfo.wsUrl, workspaceRoot, outputChannel, 'semantic');
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

  // Command: Refresh Tree
  const refreshTreeCommand = vscode.commands.registerCommand(
    'codeExplorer.refreshTree',
    () => {
      treeDataProvider.refresh();
    }
  );

  // Command: Open Node Grid in Central Panel
  const openNodeGridCommand = vscode.commands.registerCommand(
    'codeExplorer.openNodeGrid',
    async (kind: string, layerName?: string) => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a project workspace folder first.');
        return;
      }

      if (!processManager!.hasWorkspace(workspaceRoot)) {
        const choice = await vscode.window.showInformationMessage(
          'CodeExplorer: No architecture graph found for this workspace. Initialize and scan now?',
          'Initialize & Scan',
          'Cancel'
        );
        if (choice === 'Initialize & Scan') {
          vscode.commands.executeCommand('codeExplorer.initAndScan');
        }
        return;
      }

      try {
        const serverInfo = await processManager!.ensureServerStarted(workspaceRoot);
        const panel = GraphPanel.createOrShow(context.extensionUri, serverInfo.wsUrl, workspaceRoot, outputChannel, 'grid');
        panel.postMessage({
          type: 'OPEN_NODE_GRID',
          kind,
          layerName,
        });
      } catch (err: any) {
        outputChannel.appendLine(`[openNodeGrid Error] ${err.message}`);
      }
    }
  );

  // Command: Open Specific View (e.g. 'c1', 'flow', 'layers', 'semantic', 'full')
  const openViewCommand = vscode.commands.registerCommand(
    'codeExplorer.openView',
    async (viewMode: string, project?: string) => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a project workspace folder first.');
        return;
      }

      if (!processManager!.hasWorkspace(workspaceRoot)) {
        const choice = await vscode.window.showInformationMessage(
          'CodeExplorer: No architecture graph found for this workspace. Initialize and scan now?',
          'Initialize & Scan',
          'Cancel'
        );
        if (choice === 'Initialize & Scan') {
          vscode.commands.executeCommand('codeExplorer.initAndScan');
        }
        return;
      }

      try {
        const serverInfo = await processManager!.ensureServerStarted(workspaceRoot);
        const panel = GraphPanel.createOrShow(
          context.extensionUri,
          serverInfo.wsUrl,
          workspaceRoot,
          outputChannel,
          viewMode,
          project
        );
        panel.postMessage({ type: 'SET_VIEW_MODE', viewMode });
        if (project) {
          panel.postMessage({ type: 'SELECT_PROJECT', project });
        }
      } catch (err: any) {
        outputChannel.appendLine(`[openView Error] ${err.message}`);
      }
    }
  );

  // Command: Focus Node in Graph
  const focusNodeCommand = vscode.commands.registerCommand(
    'codeExplorer.focusNode',
    async (nodeIdOrItem: any, kind?: string) => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) return;

      const targetId = typeof nodeIdOrItem === 'string' ? nodeIdOrItem : nodeIdOrItem?.data?.id;
      const targetKind = typeof nodeIdOrItem === 'string' ? kind : nodeIdOrItem?.data?.kind;
      if (!targetId) return;

      try {
        const serverInfo = await processManager!.ensureServerStarted(workspaceRoot);
        const targetMode = isProjectKind(targetKind) ? 'flow' : 'semantic';
        const panel = GraphPanel.createOrShow(
          context.extensionUri,
          serverInfo.wsUrl,
          workspaceRoot,
          outputChannel,
          targetMode
        );
        panel.postMessage({
          type: 'FOCUS_NODE',
          nodeId: targetId,
          kind: targetKind,
        });
      } catch (err: any) {
        outputChannel.appendLine(`[focusNode Error] ${err.message}`);
      }
    }
  );

  // Command: Go to Source File
  const openSourceCommand = vscode.commands.registerCommand(
    'codeExplorer.openSource',
    async (filePathOrItem: any, lineStart?: number) => {
      let targetPath = '';
      let targetLine = lineStart;

      if (typeof filePathOrItem === 'string') {
        targetPath = filePathOrItem;
      } else if (filePathOrItem?.data?.filePath) {
        targetPath = filePathOrItem.data.filePath;
        targetLine = filePathOrItem.data.lineStart;
      }

      if (!targetPath) return;

      const workspaceRoot = getWorkspaceRoot() || '';
      const absolutePath = path.isAbsolute(targetPath) ? targetPath : path.join(workspaceRoot, targetPath);

      try {
        const doc = await vscode.workspace.openTextDocument(absolutePath);
        const editor = await vscode.window.showTextDocument(doc);
        if (targetLine && targetLine > 0) {
          const pos = new vscode.Position(targetLine - 1, 0);
          editor.selection = new vscode.Selection(pos, pos);
          editor.revealRange(new vscode.Range(pos, pos), vscode.TextEditorRevealType.InCenter);
        }
      } catch (err: any) {
        vscode.window.showErrorMessage(`Failed to open source file: ${err.message}`);
      }
    }
  );

  const triggerScan = async (clear: boolean) => {
    const workspaceRoot = getWorkspaceRoot();
    if (!workspaceRoot) {
      vscode.window.showWarningMessage('Please open a workspace folder to reindex.');
      return;
    }

    if (!processManager!.hasWorkspace(workspaceRoot)) {
      const choice = await vscode.window.showInformationMessage(
        'CodeExplorer: No architecture graph found for this workspace. Initialize and scan now?',
        'Initialize & Scan',
        'Cancel'
      );
      if (choice === 'Initialize & Scan') {
        vscode.commands.executeCommand('codeExplorer.initAndScan');
      }
      return;
    }

    if (GraphPanel.panels.size === 0) {
      await vscode.commands.executeCommand('codeExplorer.showGraph');
      setTimeout(() => {
        for (const p of GraphPanel.panels.values()) {
          p.postMessage({ type: 'TRIGGER_SCAN', clear });
        }
      }, 1000);
    } else {
      for (const p of GraphPanel.panels.values()) {
        p.postMessage({ type: 'TRIGGER_SCAN', clear });
      }
    }
    // Also refresh tree view after short delay
    setTimeout(() => treeDataProvider.refresh(), 1500);
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

  // Command: Restart Server
  const restartServerCommand = vscode.commands.registerCommand(
    'codeExplorer.restartServer',
    async () => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a project workspace folder first.');
        return;
      }

      processManager!.stopServer();
      vscode.window.showInformationMessage('CodeExplorer: Restarting graph server...');
      try {
        await processManager!.ensureServerStarted(workspaceRoot);
        treeDataProvider.refresh();
        vscode.window.showInformationMessage('CodeExplorer: Server restarted successfully.');
      } catch (err: any) {
        vscode.window.showErrorMessage(`Failed to restart CodeExplorer server: ${err.message}`);
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

  // Command: Initialize & Scan Workspace
  const initAndScanCommand = vscode.commands.registerCommand(
    'codeExplorer.initAndScan',
    async () => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a workspace folder to initialize CodeExplorer.');
        return;
      }

      const pm = processManager;
      if (!pm) {
        vscode.window.showErrorMessage('CodeExplorer process manager is not ready.');
        return;
      }

      await vscode.window.withProgress(
        {
          location: vscode.ProgressLocation.Notification,
          title: 'CodeExplorer: Initializing & Indexing Workspace...',
          cancellable: false,
        },
        async (progress) => {
          try {
            // Step 1: Run ce init if .codeexplorer directory doesn't exist
            if (!pm.hasWorkspace(workspaceRoot)) {
              progress.report({ message: 'Initializing .codeexplorer workspace...' });
              await pm.runCliCommand(workspaceRoot, ['init']);
            }

            // Step 2: Run ce index
            progress.report({ message: 'Parsing ASTs and semantic graph (ce index)...' });
            await pm.runCliCommand(workspaceRoot, ['index'], (line) => {
              if (line.includes('[Layer') || line.includes('Successfully') || line.includes('Parsing project')) {
                progress.report({ message: line.replace(/^\[.*?\]\s*/, '') });
              }
            });

            // Step 3: Ensure server starts & refresh tree
            progress.report({ message: 'Starting graph server...' });
            await pm.ensureServerStarted(workspaceRoot);
            treeDataProvider.refresh();

            vscode.window
              .showInformationMessage('CodeExplorer workspace initialized and indexed successfully!', 'Show Graph')
              .then((choice) => {
                if (choice === 'Show Graph') {
                  vscode.commands.executeCommand('codeExplorer.showGraph');
                }
              });
          } catch (err: any) {
            outputChannel.appendLine(`[InitAndScan Error] ${err.message}`);
            vscode.window
              .showErrorMessage(`CodeExplorer initialization failed: ${err.message}`, 'Show Logs')
              .then((choice) => {
                if (choice === 'Show Logs') outputChannel.show(true);
              });
          }
        }
      );
    }
  );

  context.subscriptions.push(
    initAndScanCommand,
    showGraphCommand,
    refreshTreeCommand,
    openNodeGridCommand,
    openViewCommand,
    focusNodeCommand,
    openSourceCommand,
    reindexCommand,
    reindexFullCommand,
    showLogsCommand
  );
  outputChannel.appendLine('CodeExplorer extension activated.');
}

export function deactivate() {
  if (processManager) {
    processManager.dispose();
  }
}
