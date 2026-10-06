import * as vscode from 'vscode';
import * as path from 'path';
import { ProcessManager } from './processManager';
import { GraphPanel } from './graphPanel';
import { CodeExplorerTreeDataProvider } from './codeExplorerTreeProvider';
import { getModelStatus } from './modelManager';
import { LlmBridgeService } from './services/llmBridgeService';
import { isProjectKind } from '../../proto/types';
import { compareSemver } from './binaryManager';

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
    async (kindOrItem: any, layerName?: string, service?: string) => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a project workspace folder first.');
        return;
      }

      const kind: string =
        typeof kindOrItem === 'string'
          ? kindOrItem
          : kindOrItem?.data?.kind || kindOrItem?.data?.rel || '';
      const targetLayer: string | undefined =
        typeof kindOrItem === 'string'
          ? layerName
          : kindOrItem?.data?.layerTitle || layerName;
      const targetService: string | undefined =
        typeof kindOrItem === 'string'
          ? service
          : kindOrItem?.data?.serviceName || service;

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
          'grid',
          undefined,
          { kind, layerTitle: targetLayer, service: targetService }
        );
        panel.postMessage({
          type: 'OPEN_NODE_GRID',
          kind,
          layerName: targetLayer,
          service: targetService,
        });
      } catch (err: any) {
        outputChannel.appendLine(`[openNodeGrid Error] ${err.message}`);
      }
    }
  );

  // Command: Open Specific View (e.g. 'c1', 'flow', 'layers', 'semantic', 'full')
  const openViewCommand = vscode.commands.registerCommand(
    'codeExplorer.openView',
    async (viewModeOrItem: any, project?: string) => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a project workspace folder first.');
        return;
      }

      const viewMode: string =
        typeof viewModeOrItem === 'string'
          ? viewModeOrItem
          : viewModeOrItem?.data?.viewMode || (viewModeOrItem?.itemType === 'layer-group' ? 'layers' : 'layers');

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
        if (viewMode === 'contexts') {
          panel.postMessage({ type: 'RELOAD_CONTEXTS' });
        }
        if (project) {
          panel.postMessage({ type: 'SELECT_PROJECT', project });
        }
      } catch (err: any) {
        outputChannel.appendLine(`[openView Error] ${err.message}`);
      }
    }
  );

  // Command: Focus Bounded Context or Domain
  const focusBoundedContextCommand = vscode.commands.registerCommand(
    'codeExplorer.focusBoundedContext',
    async (contextName?: string, contextId?: string, domainId?: string) => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) return;

      try {
        const serverInfo = await processManager!.ensureServerStarted(workspaceRoot);
        const panel = GraphPanel.createOrShow(
          context.extensionUri,
          serverInfo.wsUrl,
          workspaceRoot,
          outputChannel,
          'contexts'
        );
        panel.postMessage({ type: 'SET_VIEW_MODE', viewMode: 'contexts' });
        panel.postMessage({
          type: 'FOCUS_CONTEXT',
          contextName,
          contextId,
          domainId,
        });
      } catch (err: any) {
        outputChannel.appendLine(`[focusBoundedContext Error] ${err.message}`);
      }
    }
  );

  // Command: Focus Node in Graph
  const focusNodeCommand = vscode.commands.registerCommand(
    'codeExplorer.focusNode',
    async (nodeIdOrItem: any, kind?: string) => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) return;

      const targetId = typeof nodeIdOrItem === 'string'
        ? nodeIdOrItem
        : nodeIdOrItem?.data?.serviceId || nodeIdOrItem?.data?.serviceName || nodeIdOrItem?.data?.id || nodeIdOrItem?.data?.item?.name;
      const targetKind = typeof nodeIdOrItem === 'string'
        ? kind
        : nodeIdOrItem?.data?.kind || nodeIdOrItem?.data?.item?.kind || (nodeIdOrItem?.itemType === 'ontology-service' ? 'Service' : undefined);
      if (!targetId) return;

      try {
        const serverInfo = await processManager!.ensureServerStarted(workspaceRoot);
        const targetMode = isProjectKind(targetKind) ? 'flow' : 'semantic';
        const targetProject = isProjectKind(targetKind)
          ? (nodeIdOrItem?.data?.serviceName || (typeof nodeIdOrItem === 'string' ? nodeIdOrItem : targetId))
          : undefined;
        const panel = GraphPanel.createOrShow(
          context.extensionUri,
          serverInfo.wsUrl,
          workspaceRoot,
          outputChannel,
          targetMode,
          targetProject
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
        targetLine = filePathOrItem.data.lineStart || filePathOrItem.data.line;
      } else if (filePathOrItem?.data?.item?.filePath) {
        targetPath = filePathOrItem.data.item.filePath;
        targetLine = filePathOrItem.data.item.line || filePathOrItem.data.item.lineStart;
      }

      if (!targetPath) return;

      const workspaceRoot = getWorkspaceRoot() || '';
      const absolutePath = path.isAbsolute(targetPath) ? targetPath : path.join(workspaceRoot, targetPath);

      try {
        const doc = await vscode.workspace.openTextDocument(vscode.Uri.file(absolutePath));
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

  // Command: Check / Update CodeExplorer Engine
  const updateEngineCommand = vscode.commands.registerCommand(
    'codeExplorer.updateEngine',
    async () => {
      const bm = processManager?.getBinaryManager();
      if (!bm) {
        vscode.window.showErrorMessage('CodeExplorer binary manager is not initialized.');
        return;
      }
      try {
        const workspaceRoot = getWorkspaceRoot();
        const result = await bm.checkAndUpdate(false, workspaceRoot);
        const serverVer = processManager?.getServerInfo()?.version;
        const currentEngVer = result.currentVersion;
        const needsRestart =
          result.updated || (serverVer && currentEngVer && compareSemver(currentEngVer, serverVer) > 0);
        if (needsRestart) {
          if (workspaceRoot && processManager?.getServerInfo()) {
            outputChannel.appendLine('[ProcessManager] Restarting server with preferred/updated engine...');
            processManager.stopServer();
            await processManager.ensureServerStarted(workspaceRoot);
          }
          treeDataProvider.refresh();
        }
      } catch (err: any) {
        vscode.window.showErrorMessage(`Failed to check/update CodeExplorer engine: ${err?.message || err}`);
      }
    }
  );

  // Command: What's New
  const showWhatsNew = async () => {
    const changelogPath = path.join(context.extensionPath, 'CHANGELOG.md');
    const changelogUri = vscode.Uri.file(changelogPath);
    try {
      await vscode.commands.executeCommand('markdown.showPreview', changelogUri);
    } catch {
      await vscode.commands.executeCommand('vscode.open', changelogUri);
    }
  };

  const whatsNewCommand = vscode.commands.registerCommand('codeExplorer.whatsNew', showWhatsNew);

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

  // Command: Distill Architectural Intents
  const distillIntentsCommand = vscode.commands.registerCommand(
    'codeExplorer.distillIntents',
    async () => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a workspace folder to run architectural intent distillation.');
        return;
      }

      const pm = processManager;
      if (!pm) return;

      const aiSettings = LlmBridgeService.getInstance().getSettings();
      if (aiSettings.provider === 'gguf') {
        const modelStatus = getModelStatus(workspaceRoot);
        if (!modelStatus.exists) {
          const choice = await vscode.window.showInformationMessage(
            'CodeExplorer AI model (~940 MB) is required for local GGUF intent distillation. Would you like to download it now or switch to Built-in IDE model (0 MB)?',
            'Download Model',
            'AI Settings',
            'Cancel'
          );
          if (choice === 'Download Model') {
            await vscode.commands.executeCommand('codeExplorer.downloadModel', false);
          } else if (choice === 'AI Settings') {
            await vscode.commands.executeCommand('codeExplorer.openSettings');
            return;
          } else {
            return;
          }
        }
      }

      const cliArgs = ['intent'];
      if (aiSettings.provider === 'openai' && aiSettings.endpoint) {
        cliArgs.push('--endpoint', aiSettings.endpoint);
        if (aiSettings.model) {
          cliArgs.push('--model', aiSettings.model);
        }
        if (aiSettings.apiKey) {
          cliArgs.push('--api-key', aiSettings.apiKey);
        }
      } else if (aiSettings.provider === 'builtin') {
        cliArgs.push('--domains-only');
      }

      await vscode.window.withProgress(
        {
          location: vscode.ProgressLocation.Notification,
          title: 'CodeExplorer: Distilling Architectural Intents...',
          cancellable: true,
        },
        async (progress, token) => {
          token.onCancellationRequested(async () => {
            outputChannel.appendLine('[DistillIntents] Stop requested by user. Terminating process...');
            try {
              await pm.runCliCommand(workspaceRoot, ['intent', '--stop'], (l) => outputChannel.appendLine(`[StopIntent] ${l}`));
            } catch (stopErr: any) {
              outputChannel.appendLine(`[DistillIntents Stop Error] ${stopErr.message}`);
            }
          });

          try {
            progress.report({ message: `Running intent distillation (${aiSettings.provider})...` });
            await pm.runCliCommand(workspaceRoot, cliArgs, (line) => {
              if (line.includes('[CodeIntent]') || line.includes('Enriched') || line.includes('candidate') || line.includes('Compute Device')) {
                progress.report({ message: line.replace(/^\[.*?\]\s*/, '') });
              }
            });
            treeDataProvider.refresh();
            GraphPanel.broadcast({ type: 'RELOAD_CONTEXTS' });
            if (!token.isCancellationRequested) {
              vscode.window
                .showInformationMessage('CodeExplorer: Intent distillation completed! Knowledge graph enriched.', 'Show Bounded Contexts')
                .then((choice) => {
                  if (choice === 'Show Bounded Contexts') {
                    vscode.commands.executeCommand('codeExplorer.openView', 'contexts');
                  }
                });
            }
          } catch (err: any) {
            outputChannel.appendLine(`[DistillIntents Error] ${err.message}`);
            if (!token.isCancellationRequested) {
              vscode.window
                .showErrorMessage(`Intent distillation failed: ${err.message}`, 'Show Logs')
                .then((c) => {
                  if (c === 'Show Logs') outputChannel.show(true);
                });
            }
          }
        }
      );
    }
  );

  // Command: Stop Intent Distillation
  const stopIntentCommand = vscode.commands.registerCommand(
    'codeExplorer.stopIntent',
    async () => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a workspace folder first.');
        return;
      }
      const pm = processManager;
      if (!pm) return;

      try {
        let resultMsg = '';
        await pm.runCliCommand(workspaceRoot, ['intent', '--stop'], (line) => {
          outputChannel.appendLine(`[StopIntent] ${line}`);
          if (line.includes('✓') || line.includes('ℹ') || line.includes('Stopped')) {
            resultMsg = line;
          }
        });
        vscode.window.showInformationMessage(resultMsg || 'CodeExplorer: Intent distillation process stopped.');
      } catch (err: any) {
        vscode.window.showErrorMessage(`Failed to stop intent distillation: ${err.message}`);
      }
    }
  );

  // Command: Download AI Model
  const downloadModelCommand = vscode.commands.registerCommand(
    'codeExplorer.downloadModel',
    async (force = false) => {
      const workspaceRoot = getWorkspaceRoot() || process.cwd();
      const pm = processManager;
      if (!pm) return;

      await vscode.window.withProgress(
        {
          location: vscode.ProgressLocation.Notification,
          title: 'CodeExplorer: Downloading AI Intent Model (~940 MB)...',
          cancellable: false,
        },
        async (progress) => {
          try {
            progress.report({ message: 'Starting model download...' });
            const cliArgs = ['model', 'download'];
            if (force) {
              cliArgs.push('--force');
            }
            await pm.runCliCommand(workspaceRoot, cliArgs, (line) => {
              if (line.includes('Downloading:') || line.includes('MB /') || line.includes('successfully')) {
                progress.report({ message: line.trim() });
              }
            });
            treeDataProvider.refresh();
            vscode.window
              .showInformationMessage('CodeExplorer: AI model downloaded successfully! Ready for intent distillation.', 'Run Distillation')
              .then((choice) => {
                if (choice === 'Run Distillation') {
                  vscode.commands.executeCommand('codeExplorer.distillIntents');
                }
              });
          } catch (err: any) {
            outputChannel.appendLine(`[DownloadModel Error] ${err.message}`);
            vscode.window
              .showErrorMessage(`Model download failed: ${err.message}`, 'Show Logs')
              .then((c) => {
                if (c === 'Show Logs') outputChannel.show(true);
              });
          }
        }
      );
    }
  );

  // Command: AI Model Management & Status
  const modelStatusCommand = vscode.commands.registerCommand(
    'codeExplorer.modelStatus',
    async () => {
      const workspaceRoot = getWorkspaceRoot();
      const status = getModelStatus(workspaceRoot);

      if (status.exists) {
        const selection = await vscode.window.showQuickPick(
          [
            {
              label: '$(check) Model Status: Ready',
              description: `${status.sizeMb} MB`,
              detail: status.modelPath,
            },
            {
              label: '$(sparkle) Distill Architectural Intents',
              description: 'Run SLM intent distillation pass',
            },
            {
              label: '$(debug-stop) Stop Intent Distillation',
              description: 'Stop any running background intent distillation process',
            },
            {
              label: '$(cloud-download) Force Re-download Model',
              description: 'Re-fetch model from repository (~940 MB)',
            },
            {
              label: '$(trash) Clear Intent Cache',
              description: 'Clear all cached intent records for this workspace',
            },
            {
              label: '$(debug-restart) Reset Intent Errors',
              description: 'Reset error counter for files that failed distillation',
            },
          ],
          {
            placeHolder: 'CodeExplorer AI Model Management',
          }
        );

        if (!selection) return;

        if (selection.label.includes('Distill Architectural Intents')) {
          vscode.commands.executeCommand('codeExplorer.distillIntents');
        } else if (selection.label.includes('Stop Intent Distillation')) {
          vscode.commands.executeCommand('codeExplorer.stopIntent');
        } else if (selection.label.includes('Force Re-download Model')) {
          vscode.commands.executeCommand('codeExplorer.downloadModel', true);
        } else if (selection.label.includes('Clear Intent Cache')) {
          vscode.commands.executeCommand('codeExplorer.clearIntents');
        } else if (selection.label.includes('Reset Intent Errors')) {
          if (workspaceRoot && processManager) {
            await processManager.runCliCommand(workspaceRoot, ['intent', '--reset-errors']);
            vscode.window.showInformationMessage('CodeExplorer: Intent error counters reset.');
          }
        }
      } else {
        const selection = await vscode.window.showQuickPick(
          [
            {
              label: '$(cloud-download) Download AI Model Now',
              description: 'Download ce-intent-v2-q4_k_m.gguf (~940 MB)',
              detail: `Target destination: ${status.modelPath}`,
            },
            {
              label: '$(info) Learn More About Architectural Intents',
              description: 'Classifies files into DDD Bounded Contexts, CQRS roles, and domain events',
            },
          ],
          {
            placeHolder: 'CodeExplorer AI Model is not downloaded',
          }
        );

        if (selection?.label.includes('Download AI Model Now')) {
          vscode.commands.executeCommand('codeExplorer.downloadModel', false);
        }
      }
    }
  );

  // Command: Clear Intent Cache
  const clearIntentsCommand = vscode.commands.registerCommand(
    'codeExplorer.clearIntents',
    async () => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a workspace folder first.');
        return;
      }
      const confirm = await vscode.window.showWarningMessage(
        'Clear all cached intent records for this workspace?',
        { modal: true },
        'Clear Intents'
      );
      if (confirm === 'Clear Intents' && processManager) {
        try {
          await processManager.runCliCommand(workspaceRoot, ['intent', '--clear']);
          vscode.window.showInformationMessage('CodeExplorer: Intent cache cleared.');
          treeDataProvider.refresh();
        } catch (err: any) {
          vscode.window.showErrorMessage(`Failed to clear intent cache: ${err.message}`);
        }
      }
    }
  );

  // Command: Full Re-distill Intents (Clear & Distill)
  const reindexIntentsFullCommand = vscode.commands.registerCommand(
    'codeExplorer.reindexIntentsFull',
    async () => {
      const workspaceRoot = getWorkspaceRoot();
      if (!workspaceRoot) {
        vscode.window.showWarningMessage('Please open a workspace folder first.');
        return;
      }
      const confirm = await vscode.window.showWarningMessage(
        'Clear & re-distill all architectural intents for this workspace?',
        { modal: true },
        'Clear & Re-distill'
      );
      if (confirm === 'Clear & Re-distill' && processManager) {
        try {
          await processManager.runCliCommand(workspaceRoot, ['intent', '--clear']);
          await vscode.commands.executeCommand('codeExplorer.distillIntents');
        } catch (err: any) {
          vscode.window.showErrorMessage(`Failed to re-distill intents: ${err.message}`);
        }
      }
    }
  );

  // Command: Open Settings
  const openSettingsCommand = vscode.commands.registerCommand(
    'codeExplorer.openSettings',
    async () => {
      await vscode.commands.executeCommand('codeExplorer.openView', 'settings');
    }
  );

  context.subscriptions.push(
    initAndScanCommand,
    showGraphCommand,
    refreshTreeCommand,
    openNodeGridCommand,
    openViewCommand,
    openSettingsCommand,
    focusBoundedContextCommand,
    focusNodeCommand,
    openSourceCommand,
    reindexCommand,
    reindexFullCommand,
    showLogsCommand,
    updateEngineCommand,
    whatsNewCommand,
    distillIntentsCommand,
    stopIntentCommand,
    downloadModelCommand,
    modelStatusCommand,
    clearIntentsCommand,
    reindexIntentsFullCommand
  );

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
