# VS Code Extension Webview & View Architecture Rules

1. **Dedicated Full-View Webview Panels (`GraphPanel` per `viewMode`)**:
   - Every major UI screen, diagram, or editor in the VS Code Extension (`semantic`, `contexts`, `layers`, `flow`, `c1`, `grid`, `mermaid`, `settings`, `gate-editor`) is a dedicated `viewMode` opened in its own VS Code `WebviewPanel` tab via `GraphPanel.createOrShow(..., viewMode)`.
   - Never add a global view-switcher `<select>` inside a view container that switches one tab into a different diagram/view.

2. **Full-View Forms Instead of Modal Dialogs**:
   - Configuration and editor screens (such as `SettingsView` for AI/Model settings and `GateEditorView` for Semantic Invariant Gates) MUST be implemented as clean, scrollable **full-view forms** (`width: 100%`, `height: 100%`, using native VS Code theme variables `--vscode-editor-background`, `--vscode-input-background`, etc.) rendered directly in their own `viewMode` tab.
   - **DO NOT** build complex forms or editors as modal popup dialogs (`position: fixed` overlays) on top of graph canvas views.

3. **Shared Server Transport**:
   - On the Extension Host, always inject and use the shared `ServerTransport` instance in `GraphPanel`.
   - Inside the Webview, always use the shared `webviewTransport` / `useServerTransport()` service (`src/webview/services/webviewTransport.ts`) for WebSocket and HTTP REST calls (`/api/...`) so dynamic server port changes propagate automatically across all views.
