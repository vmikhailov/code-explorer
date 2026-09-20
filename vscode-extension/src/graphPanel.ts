import * as vscode from 'vscode';
import * as path from 'path';

export class GraphPanel {
  public static currentPanel: GraphPanel | undefined;
  private readonly panel: vscode.WebviewPanel;
  private readonly extensionUri: vscode.Uri;
  private disposables: vscode.Disposable[] = [];

  public static createOrShow(extensionUri: vscode.Uri, wsUrl: string, workspaceRoot: string) {
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

    GraphPanel.currentPanel = new GraphPanel(panel, extensionUri, wsUrl, workspaceRoot);
  }

  private constructor(
    panel: vscode.WebviewPanel,
    extensionUri: vscode.Uri,
    private wsUrl: string,
    private workspaceRoot: string
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
            this.postMessage({
              type: 'SERVER_CONFIG',
              wsUrl: this.wsUrl,
              workspaceRoot: this.workspaceRoot,
            });
            break;

          case 'OPEN_FILE':
            await this.handleOpenFile(message.filePath, message.lineStart, message.lineEnd);
            break;

          case 'SHOW_INFO':
            vscode.window.showInformationMessage(message.message);
            break;

          case 'SHOW_ERROR':
            vscode.window.showErrorMessage(message.message);
            break;
        }
      },
      null,
      this.disposables
    );
  }

  private async handleOpenFile(filePath: string, lineStart?: number, lineEnd?: number) {
    try {
      if (!filePath) return;

      const fullPath = path.isAbsolute(filePath)
        ? filePath
        : path.resolve(this.workspaceRoot, filePath);

      const uri = vscode.Uri.file(fullPath);
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
      vscode.window.showErrorMessage(`Failed to open file "${filePath}": ${err.message}`);
    }
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
