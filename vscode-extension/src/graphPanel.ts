import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export class GraphPanel {
  public static currentPanel: GraphPanel | undefined;
  private readonly panel: vscode.WebviewPanel;
  private readonly extensionUri: vscode.Uri;
  private disposables: vscode.Disposable[] = [];

  public static createOrShow(
    extensionUri: vscode.Uri,
    wsUrl: string,
    workspaceRoot: string,
    outputChannel?: vscode.OutputChannel
  ) {
    const column = vscode.window.activeTextEditor
      ? vscode.ViewColumn.Beside
      : vscode.ViewColumn.One;

    if (GraphPanel.currentPanel) {
      GraphPanel.currentPanel.panel.reveal(column);
      GraphPanel.currentPanel.postMessage({
        type: 'SERVER_CONFIG',
        wsUrl,
        workspaceRoot,
      });
      return;
    }

    const panel = vscode.window.createWebviewPanel(
      'codeExplorerGraph',
      'CodeExplorer: Graph',
      column,
      {
        enableScripts: true,
        retainContextWhenHidden: true,
        localResourceRoots: [vscode.Uri.joinPath(extensionUri, 'dist')],
      }
    );

    GraphPanel.currentPanel = new GraphPanel(panel, extensionUri, wsUrl, workspaceRoot, outputChannel);
  }

  private constructor(
    panel: vscode.WebviewPanel,
    extensionUri: vscode.Uri,
    private wsUrl: string,
    private workspaceRoot: string,
    private outputChannel?: vscode.OutputChannel
  ) {
    this.panel = panel;
    this.extensionUri = extensionUri;

    // Set webview HTML
    this.updateHtml();

    // Listen for webview disposal
    this.panel.onDidDispose(() => this.dispose(), null, this.disposables);

    // Handle messages from Webview
    this.panel.webview.onDidReceiveMessage(
      async (message) => {
        switch (message.type) {
          case 'WEBVIEW_READY':
            this.outputChannel?.appendLine('[GraphPanel] Webview ready received. Sending SERVER_CONFIG.');
            this.postMessage({
              type: 'SERVER_CONFIG',
              wsUrl: this.wsUrl,
              workspaceRoot: this.workspaceRoot,
            });
            break;

          case 'LOG':
            this.outputChannel?.appendLine(`[Webview ${message.level || 'INFO'}] ${message.message}`);
            break;

          case 'OPEN_FILE':
            this.outputChannel?.appendLine(`[GraphPanel] Open file requested: ${message.filePath}:${message.lineStart || 1}`);
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
</head>
<body>
  <div id="root"></div>
  <script nonce="${nonce}" src="${scriptUri}"></script>
</body>
</html>`;
  }

  public dispose() {
    GraphPanel.currentPanel = undefined;
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
