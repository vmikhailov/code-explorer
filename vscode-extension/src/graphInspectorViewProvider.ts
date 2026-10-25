import * as vscode from 'vscode';
import { ServerTransport } from './serverTransport';
import { GraphPanel } from './graphPanel';

export class GraphInspectorViewProvider implements vscode.WebviewViewProvider {
  public static readonly viewType = 'codeExplorerInspectorView';
  private static instance: GraphInspectorViewProvider | undefined;

  public static getInstance(): GraphInspectorViewProvider | undefined {
    return GraphInspectorViewProvider.instance;
  }

  private view?: vscode.WebviewView;
  private isWebviewReady = false;
  private lastSyncedState: any = null;
  private wsUrl = '';
  private disposables: vscode.Disposable[] = [];

  constructor(
    private readonly extensionUri: vscode.Uri,
    private readonly transport: ServerTransport,
    private readonly getWorkspaceRoot: () => string | undefined,
    private readonly outputChannel?: vscode.OutputChannel
  ) {
    GraphInspectorViewProvider.instance = this;
    this.wsUrl = this.transport?.getEndpoints()?.wsUrl || '';

    if (this.transport) {
      this.disposables.push(
        this.transport.onDidServerStart((info) => {
          this.updateWsUrl(info.wsUrl);
        }),
        this.transport.onDidChangeState((snapshot) => {
          if (snapshot.endpoints?.wsUrl && snapshot.endpoints.wsUrl !== this.wsUrl) {
            this.updateWsUrl(snapshot.endpoints.wsUrl);
          }
        })
      );
    }
  }

  public updateWsUrl(newWsUrl: string): void {
    this.wsUrl = newWsUrl;
    if (this.view && this.isWebviewReady) {
      this.view.webview.postMessage({
        type: 'UPDATE_WS_URL',
        wsUrl: this.wsUrl,
      });
      this.view.webview.postMessage({
        type: 'SERVER_CONFIG',
        wsUrl: this.wsUrl,
        workspaceRoot: this.getWorkspaceRoot() || '',
        initialViewMode: 'inspector',
      });
    }
  }

  public syncState(state: any): void {
    this.lastSyncedState = state;
    if (this.view && this.isWebviewReady) {
      this.view.webview.postMessage({
        type: 'INSPECTOR_STATE_SYNC',
        state,
      });
    }
  }

  public async reveal(preserveFocus = false): Promise<void> {
    try {
      if (preserveFocus) {
        // Subtle background reveal (e.g. canvas selection): keep user focus in editor/canvas
        try {
          await vscode.commands.executeCommand('codeExplorerInspectorView.open', { preserveFocus: true });
        } catch {
          this.view?.show(true);
        }
      } else {
        // Explicit user action (Inspector HUD button or command):
        // Execute focus command to guarantee VS Code opens the hosting container
        // (Secondary Side Bar / Auxiliary Bar, Primary Side Bar, or Bottom Panel) even if closed/collapsed,
        // brings this view container forward, and expands the view pane.
        await vscode.commands.executeCommand('codeExplorerInspectorView.focus');
      }
    } catch (err: any) {
      this.outputChannel?.appendLine(`[InspectorView] 'codeExplorerInspectorView.focus' failed: ${err?.message || err}`);
      try {
        await vscode.commands.executeCommand('workbench.view.extension.code-explorer-inspector');
      } catch {}
      try {
        await vscode.commands.executeCommand('workbench.action.focusAuxiliaryBar');
      } catch {}
    }

    if (this.view) {
      this.view.show(preserveFocus);
    }
  }

  public resolveWebviewView(
    webviewView: vscode.WebviewView,
    _context: vscode.WebviewViewResolveContext,
    _token: vscode.CancellationToken
  ): void {
    this.view = webviewView;
    this.isWebviewReady = false;

    webviewView.webview.options = {
      enableScripts: true,
      localResourceRoots: [vscode.Uri.joinPath(this.extensionUri, 'dist')],
    };

    const liveWsUrl = this.transport?.getEndpoints()?.wsUrl;
    if (liveWsUrl) {
      this.wsUrl = liveWsUrl;
    }

    webviewView.webview.html = this.getHtmlForWebview(webviewView.webview);

    webviewView.onDidDispose(() => {
      if (this.view === webviewView) {
        this.view = undefined;
        this.isWebviewReady = false;
      }
    });

    webviewView.onDidChangeVisibility(() => {
      if (webviewView.visible && this.isWebviewReady) {
        if (this.lastSyncedState) {
          webviewView.webview.postMessage({
            type: 'INSPECTOR_STATE_SYNC',
            state: this.lastSyncedState,
          });
        }
        GraphPanel.currentPanel?.postMessage({ type: 'REQUEST_INSPECTOR_SYNC' });
      }
    });

    webviewView.webview.onDidReceiveMessage(async (message) => {
      switch (message.type) {
        case 'WEBVIEW_READY': {
          this.isWebviewReady = true;
          const currentWs = this.transport?.getEndpoints()?.wsUrl || this.wsUrl;
          this.wsUrl = currentWs;
          webviewView.webview.postMessage({
            type: 'UPDATE_WS_URL',
            wsUrl: this.wsUrl,
          });
          webviewView.webview.postMessage({
            type: 'SERVER_CONFIG',
            wsUrl: this.wsUrl,
            workspaceRoot: this.getWorkspaceRoot() || '',
            initialViewMode: 'inspector',
          });
          if (this.lastSyncedState) {
            webviewView.webview.postMessage({
              type: 'INSPECTOR_STATE_SYNC',
              state: this.lastSyncedState,
            });
          }
          GraphPanel.currentPanel?.postMessage({ type: 'REQUEST_INSPECTOR_SYNC' });
          break;
        }

        case 'INSPECTOR_ACTION': {
          const activePanel = GraphPanel.currentPanel;
          if (activePanel) {
            activePanel.postMessage({
              type: 'INSPECTOR_ACTION',
              action: message.action,
            });
          }
          break;
        }

        case 'OPEN_FILE': {
          const activePanel = GraphPanel.currentPanel;
          if (activePanel) {
            await activePanel.openFileFromInspector(message.filePath, message.lineStart, message.lineEnd);
          }
          break;
        }

        case 'OPEN_VIEW': {
          await vscode.commands.executeCommand('codeExplorer.openView', message.viewMode, message.project);
          break;
        }

        case 'COPY_TO_CLIPBOARD': {
          if (message.text) {
            await vscode.env.clipboard.writeText(message.text);
            vscode.window.showInformationMessage('CodeExplorer details copied to clipboard.');
          }
          break;
        }

        case 'MOVE_INSPECTOR_VIEW': {
          await vscode.commands.executeCommand('workbench.action.moveView');
          break;
        }

        case 'LOG': {
          this.outputChannel?.appendLine(`[InspectorWebview ${message.level || 'INFO'}] ${message.message}`);
          break;
        }
      }
    });
  }

  private getHtmlForWebview(webview: vscode.Webview): string {
    const scriptUri = webview.asWebviewUri(vscode.Uri.joinPath(this.extensionUri, 'dist', 'webview.js'));
    const styleUri = webview.asWebviewUri(vscode.Uri.joinPath(this.extensionUri, 'dist', 'styles.css'));
    const nonce = getNonce();
    const configData = {
      viewMode: 'inspector',
      wsUrl: this.wsUrl,
      workspaceRoot: this.getWorkspaceRoot() || '',
    };
    const configScript = `window.__CE_CONFIG__ = ${JSON.stringify(configData).replace(/</g, '\\u003c')};`;

    return `<!DOCTYPE html>
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
  <title>CodeExplorer Graph Inspector</title>
  <link rel="stylesheet" href="${styleUri}">
  <script nonce="${nonce}">${configScript}</script>
</head>
<body class="ce-inspector-webview-body" style="margin: 0; padding: 0; overflow: hidden;">
  <div id="root"></div>
  <script nonce="${nonce}" src="${scriptUri}"></script>
</body>
</html>`;
  }

  public dispose(): void {
    while (this.disposables.length) {
      this.disposables.pop()?.dispose();
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
