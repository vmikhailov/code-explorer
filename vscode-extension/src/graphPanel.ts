import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export function getViewTitle(viewMode?: string): string {
  switch (viewMode) {
    case 'semantic':
      return 'CodeExplorer: Domain Microservices';
    case 'layers':
      return 'CodeExplorer: Architecture Tiers';
    case 'c1':
      return 'CodeExplorer: C1 System Context';
    case 'flow':
      return 'CodeExplorer: Project Flow';
    case 'full':
      return 'CodeExplorer: Physical Graph';
    case 'grid':
      return 'CodeExplorer: Node Grid';
    case 'mermaid':
      return 'CodeExplorer: Mermaid Diagram';
    default:
      return 'CodeExplorer: Architecture';
  }
}

export class GraphPanel {
  public static readonly panels = new Map<string, GraphPanel>();
  public static activePanel: GraphPanel | undefined;

  public static get currentPanel(): GraphPanel | undefined {
    return GraphPanel.activePanel || GraphPanel.panels.values().next().value;
  }

  private readonly panel: vscode.WebviewPanel;
  private readonly extensionUri: vscode.Uri;
  private readonly viewMode: string;
  private disposables: vscode.Disposable[] = [];
  private isWebviewReady = false;
  private pendingMessages: any[] = [];

  public static createOrShow(
    extensionUri: vscode.Uri,
    wsUrl: string,
    workspaceRoot: string,
    outputChannel?: vscode.OutputChannel,
    initialViewMode?: string,
    project?: string,
    initialGridCategory?: { kind: string; layerTitle?: string; service?: string }
  ): GraphPanel {
    const column = vscode.window.activeTextEditor?.viewColumn ?? vscode.ViewColumn.Active;
    const mode = initialViewMode || 'semantic';

    const existing = GraphPanel.panels.get(mode);
    if (existing) {
      existing.panel.title = getViewTitle(mode);
      existing.panel.reveal(existing.panel.viewColumn ?? column);
      existing.postMessage({
        type: 'SERVER_CONFIG',
        wsUrl,
        workspaceRoot,
        initialViewMode: mode,
      });
      existing.postMessage({ type: 'SET_VIEW_MODE', viewMode: mode });
      if (project) {
        existing.postMessage({ type: 'SELECT_PROJECT', project });
      }
      if (initialGridCategory) {
        existing.postMessage({
          type: 'OPEN_NODE_GRID',
          kind: initialGridCategory.kind,
          layerName: initialGridCategory.layerTitle,
          service: initialGridCategory.service,
        });
      }
      return existing;
    }

    const panel = vscode.window.createWebviewPanel(
      `codeExplorerGraph.${mode}`,
      getViewTitle(mode),
      column,
      {
        enableScripts: true,
        retainContextWhenHidden: true,
        localResourceRoots: [vscode.Uri.joinPath(extensionUri, 'dist')],
      }
    );

    const newPanel = new GraphPanel(panel, extensionUri, wsUrl, workspaceRoot, outputChannel, mode, project, initialGridCategory);
    GraphPanel.panels.set(mode, newPanel);
    GraphPanel.activePanel = newPanel;

    if (mode) {
      newPanel.postMessage({ type: 'SET_VIEW_MODE', viewMode: mode });
    }
    if (project) {
      newPanel.postMessage({ type: 'SELECT_PROJECT', project });
    }
    if (initialGridCategory) {
      newPanel.postMessage({
        type: 'OPEN_NODE_GRID',
        kind: initialGridCategory.kind,
        layerName: initialGridCategory.layerTitle,
        service: initialGridCategory.service,
      });
    }

    return newPanel;
  }

  private constructor(
    panel: vscode.WebviewPanel,
    extensionUri: vscode.Uri,
    private wsUrl: string,
    private workspaceRoot: string,
    private outputChannel?: vscode.OutputChannel,
    private initialViewMode?: string,
    private initialProject?: string,
    private initialGridCategory?: { kind: string; layerTitle?: string; service?: string }
  ) {
    this.viewMode = initialViewMode || 'semantic';
    this.panel = panel;
    this.extensionUri = extensionUri;

    // Track active state for currentPanel getter
    this.panel.onDidChangeViewState(
      (e) => {
        if (e.webviewPanel.active) {
          GraphPanel.activePanel = this;
        }
      },
      null,
      this.disposables
    );

    // Set webview HTML
    this.updateHtml();

    // Listen for webview disposal
    this.panel.onDidDispose(() => this.dispose(), null, this.disposables);

    // Handle messages from Webview
    this.panel.webview.onDidReceiveMessage(
      async (message) => {
        switch (message.type) {
          case 'WEBVIEW_READY':
            this.isWebviewReady = true;
            this.outputChannel?.appendLine(`[GraphPanel:${this.viewMode}] Webview ready. Sending SERVER_CONFIG.`);
            this.panel.webview.postMessage({
              type: 'SERVER_CONFIG',
              wsUrl: this.wsUrl,
              workspaceRoot: this.workspaceRoot,
              initialViewMode: this.initialViewMode,
            });
            while (this.pendingMessages.length > 0) {
              const queuedMsg = this.pendingMessages.shift();
              this.outputChannel?.appendLine(`[GraphPanel:${this.viewMode}] Delivering queued message: ${queuedMsg.type}`);
              this.panel.webview.postMessage(queuedMsg);
            }
            break;

          case 'LOG':
            this.outputChannel?.appendLine(`[Webview:${this.viewMode} ${message.level || 'INFO'}] ${message.message}`);
            break;

          case 'OPEN_VIEW':
            this.outputChannel?.appendLine(`[GraphPanel:${this.viewMode}] OPEN_VIEW requested: ${message.viewMode} (project=${message.project})`);
            await vscode.commands.executeCommand('codeExplorer.openView', message.viewMode, message.project);
            break;

          case 'OPEN_FILE':
            this.outputChannel?.appendLine(`[GraphPanel:${this.viewMode}] Open file requested: ${message.filePath}:${message.lineStart || 1}`);
            await this.handleOpenFile(message.filePath, message.lineStart, message.lineEnd);
            break;

          case 'SHOW_INFO':
            vscode.window.showInformationMessage(message.message);
            break;

          case 'SHOW_LOGS':
            this.outputChannel?.show(true);
            break;

          case 'COPY_TO_CLIPBOARD':
            if (message.text) {
              await vscode.env.clipboard.writeText(message.text);
              vscode.window.showInformationMessage('CodeExplorer details copied to clipboard.');
            }
            break;

          case 'SHOW_ERROR': {
            const detailText = message.details ? `\nDetails:\n${message.details}` : '';
            this.outputChannel?.appendLine(`[Webview ERROR] ${message.message}${detailText}`);
            const action = await vscode.window.showErrorMessage(
              `CodeExplorer: ${message.message}`,
              'Show Logs',
              'Copy Error Details'
            );
            if (action === 'Show Logs') {
              this.outputChannel?.show(true);
            } else if (action === 'Copy Error Details') {
              const fullDetails = message.details ? `${message.message}\n\nDetails:\n${message.details}` : message.message;
              await vscode.env.clipboard.writeText(fullDetails);
              vscode.window.showInformationMessage('CodeExplorer error details copied to clipboard.');
            }
            break;
          }
        }
      },
      null,
      this.disposables
    );
  }

  private async handleOpenFile(filePath: string, lineStart?: number, lineEnd?: number) {
    try {
      if (!filePath) return;

      const resolvedPath = await this.resolveTargetFile(filePath);
      if (!resolvedPath) {
        vscode.window.showWarningMessage(`Could not locate file or project for "${filePath}".`);
        return;
      }

      const stat = fs.statSync(resolvedPath);
      if (stat.isDirectory()) {
        // Reveal directory in file explorer if no text file was found
        const uri = vscode.Uri.file(resolvedPath);
        await vscode.commands.executeCommand('revealInExplorer', uri);
        return;
      }

      const uri = vscode.Uri.file(resolvedPath);
      const doc = await vscode.workspace.openTextDocument(uri);

      let selection: vscode.Range | undefined;
      if (lineStart && lineStart > 0) {
        const startLineIdx = Math.max(0, lineStart - 1);
        const endLineIdx = lineEnd && lineEnd > 0 ? Math.max(0, lineEnd - 1) : startLineIdx;
        selection = new vscode.Range(startLineIdx, 0, endLineIdx, 0);
      }

      await vscode.window.showTextDocument(doc, {
        viewColumn: vscode.ViewColumn.One,
        preserveFocus: false,
        selection,
      });
    } catch (err: any) {
      vscode.window.showErrorMessage(`Failed to open "${filePath}": ${err.message}`);
    }
  }

  private async resolveTargetFile(targetPath: string): Promise<string | null> {
    if (!targetPath) return null;

    // 1. Direct path check (if absolute or relative to workspaceRoot)
    const directPath = path.isAbsolute(targetPath)
      ? targetPath
      : path.resolve(this.workspaceRoot, targetPath);

    let candidate = this.checkPathOrDirectory(directPath);
    if (candidate) return candidate;

    // 2. Check under 'cli/' subfolder if workspace is monorepo root
    const cliPath = path.resolve(this.workspaceRoot, 'cli', targetPath);
    candidate = this.checkPathOrDirectory(cliPath);
    if (candidate) return candidate;

    // 3. If targetPath starts with 'cli/', try stripping it in case workspaceRoot is already inside cli/
    if (targetPath.startsWith('cli/') || targetPath.startsWith('cli\\')) {
      const stripped = targetPath.replace(/^cli[\\/]/, '');
      candidate = this.checkPathOrDirectory(path.resolve(this.workspaceRoot, stripped));
      if (candidate) return candidate;
    }

    // 4. Fallback: Search workspace for matching file or project
    const baseName = path.basename(targetPath);
    if (baseName) {
      // If it's a project without extension, search for <baseName>.*proj
      const searchPattern = baseName.includes('.') ? `**/${baseName}` : `**/${baseName}.*proj`;
      const matches = await vscode.workspace.findFiles(searchPattern, '**/node_modules/**', 2);
      if (matches.length > 0) {
        return matches[0].fsPath;
      }

      // If still not found and baseName has no extension, search for directory
      const folderMatches = await vscode.workspace.findFiles(`**/${baseName}/**`, '**/node_modules/**', 5);
      for (const m of folderMatches) {
        const dir = path.dirname(m.fsPath);
        const resolvedProj = this.findProjectFileInDir(dir);
        if (resolvedProj) return resolvedProj;
      }
    }

    return null;
  }

  private checkPathOrDirectory(p: string): string | null {
    if (!fs.existsSync(p)) return null;

    try {
      const stat = fs.statSync(p);
      if (stat.isFile()) {
        return p;
      }

      if (stat.isDirectory()) {
        const proj = this.findProjectFileInDir(p);
        return proj || p;
      }
    } catch {}

    return null;
  }

  private findProjectFileInDir(dir: string): string | null {
    if (!fs.existsSync(dir)) return null;
    try {
      const files = fs.readdirSync(dir);
      const projFile = files.find((f) =>
        f.endsWith('.csproj') ||
        f.endsWith('.fsproj') ||
        f.endsWith('.vbproj') ||
        f === 'package.json' ||
        f === 'go.mod' ||
        f === 'pom.xml' ||
        f === 'Cargo.toml'
      );
      if (projFile) {
        return path.join(dir, projFile);
      }
    } catch {}
    return null;
  }

  public postMessage(message: any) {
    if (!this.isWebviewReady) {
      this.pendingMessages.push(message);
      return;
    }
    this.panel.webview.postMessage(message);
  }

  private updateHtml() {
    const webview = this.panel.webview;
    const scriptUri = webview.asWebviewUri(
      vscode.Uri.joinPath(this.extensionUri, 'dist', 'webview.js')
    );
    const styleUri = webview.asWebviewUri(
      vscode.Uri.joinPath(this.extensionUri, 'dist', 'styles.css')
    );

    const nonce = getNonce();
    const configData = {
      viewMode: this.viewMode,
      wsUrl: this.wsUrl,
      workspaceRoot: this.workspaceRoot,
      project: this.initialProject,
      initialGridCategory: this.initialGridCategory,
    };
    const configScript = `window.__CE_CONFIG__ = ${JSON.stringify(configData).replace(/</g, '\\u003c')};`;

    this.panel.webview.html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta http-equiv="Content-Security-Policy" content="
    default-src 'none';
    img-src ${webview.cspSource} https: data:;
    style-src ${webview.cspSource} 'unsafe-inline';
    script-src 'nonce-${nonce}';
    connect-src 'self' ws://127.0.0.1:* ws://localhost:* http://127.0.0.1:* http://localhost:*;
  ">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>CodeExplorer Graph</title>
  <link rel="stylesheet" href="${styleUri}">
  <script nonce="${nonce}">${configScript}</script>
</head>
<body>
  <div id="root"></div>
  <script nonce="${nonce}" src="${scriptUri}"></script>
</body>
</html>`;
  }

  public dispose() {
    GraphPanel.panels.delete(this.viewMode);
    if (GraphPanel.activePanel === this) {
      GraphPanel.activePanel = GraphPanel.panels.values().next().value;
    }
    this.panel.dispose();
    while (this.disposables.length) {
      const d = this.disposables.pop();
      if (d) {
        d.dispose();
      }
    }
  }
}

function getNonce(): string {
  let text = '';
  const possible = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';
  for (let i = 0; i < 32; i++) {
    text += possible.charAt(Math.floor(Math.random() * possible.length));
  }
  return text;
}
