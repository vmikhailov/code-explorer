# Visual Code Explorer — VS Code & Antigravity Extension Design

This document details the architectural design, frontend stack, user experience, and distribution strategy for the **CodeExplorer VS Code / Antigravity Extension**.

---

## 1. Executive Summary

**CodeExplorer** already provides a high-performance semantic graph engine in SQLite (indexing 200k+ nodes, Cypher querying, and MCP integration). 

The goal of this extension is to transform `code-explorer` from a background CLI / AI-agent tool into a **first-class interactive visual cockpit** for developers inside VS Code and Antigravity IDE, enabling real-time architectural exploration, live code-sync, and visual blast-radius analysis.

---

## 2. High-Level Architecture

```
┌───────────────────────────────────────────────────────────────────────────┐
│                          VS Code / Antigravity                            │
│                                                                           │
│  ┌──────────────────────┐                     ┌────────────────────────┐  │
│  │   Text Editor        │                     │   Webview Panel        │  │
│  │   (C#, TS, Go, etc.) │                     │   (Cytoscape.js)       │  │
│  └──────────┬───────────┘                     └───────────▲────────────┘  │
│             │ onDidChangeCursor / onSave                  │               │
│             ▼                                             │ Typed RPC     │
│  ┌────────────────────────────────────────────────────────┴────────────┐  │
│  │                 Extension Host (TypeScript)                         │  │
│  │  - Workspace / Document Event Listeners                             │  │
│  │  - Process Manager (ce.exe life-cycle & stdio / HTTP bridge)        │  │
│  │  - Bi-directional Code Navigation Coordinator                       │  │
│  └──────────────────────────────┬──────────────────────────────────────┘  │
└─────────────────────────────────┼─────────────────────────────────────────┘
                                  │ JSON-RPC / MCP (stdio or http)
                                  ▼
                   ┌──────────────────────────────┐
                   │    ce.exe (Embedded Engine)   │
                   │  - Model Context Protocol    │
                   │  - Cypher Query Engine       │
                   │  - Incremental Indexer       │
                   └──────────────┬───────────────┘
                                  │ PRAGMA journal_mode = WAL (Read-Only)
                                  ▼
                   ┌──────────────────────────────┐
                   │   .codeexplorer/graph.db     │
                   │   (SQLite Graph Database)    │
                   └──────────────────────────────┘
```

### Core Architectural Principles:
1. **Zero Editor Lag**: Heavy graph operations run out-of-process in `ce.exe`. All queries execute against SQLite in non-blocking WAL mode.
2. **Context-Driven Viewport (Ego-Graph)**: Never render 200k+ nodes simultaneously. The UI operates on bounded subgraphs (1–2 hops) or macro-level cluster aggregations.
3. **Bi-Directional Synchrony**: Moving in code navigates the graph; interacting with the graph navigates the code.

---

## 3. Frontend & Graph Rendering Stack

### 3.1 Graph Engine: Cytoscape.js

* **Technology**: HTML5 Canvas.
* **Why Cytoscape.js is the optimal choice**:
  * **Compound Nodes (Visual Nesting)**: Natively supports nesting `Project ──> Folder ──> File ──> Class/Function`. Nodes can be visually grouped and collapsed/expanded.
  * **Dual Layout Engine**:
    * **`cytoscape-dagre` / `cytoscape-elk`**: Hierarchical directed acyclic graph (top-to-bottom or left-to-right) for architectural layers, pipeline flows, and function call chains (`get_call_chain`).
    * **`cytoscape-fcose`**: High-performance physics force-directed layout for dependency cluster discovery.
  * **Performance**: Handles 2,000–5,000 elements smoothly at 60 FPS on canvas without DOM bloat.

### 3.2 UI Shell & HUD: Vite + Svelte 5 (or React 19)

The UI framework only manages the control overlay (HUD), toolbars, search box, and node inspector cards:
* **Svelte 5 (Runes)** / **React 19 + Vite**: Instantaneous HMR during development, sub-millisecond bundle initialization in Electron iframe.
* **Design System**: `@vscode/webview-ui-toolkit` or Tailwind CSS using native CSS variables (`var(--vscode-editor-background)`, `var(--vscode-button-background)`, etc.) ensuring 100% theme compatibility (Dark, Light, High Contrast).

---

## 4. Key User Scenarios & Capabilities

### 4.1 Dual-Mode Visual Paradigm

#### Mode A: Macro Architecture Map
* **Trigger**: Extension startup, clicking "Architecture Overview" or switching to Macro tab.
* **Data Source**: `get_architecture_map_workspace` / `get_project_dependencies`.
* **Visuals**:
  * Clean hierarchical DAG of solution projects (e.g., 145 projects in `pow3`).
  * Color-coded by language (C#, TypeScript, Python, Go, SQL).
  * Outbound/inbound dependency edges with package tags.
  * Attached database badges (`Dapper`, `SQL Server`, `Redis`, `EF Core`).
  * Ingress (Controllers, Endpoints) and Egress (External APIs, Message Queues).

#### Mode B: Micro Focus (Cursor Ego-Graph)
* **Trigger**: Automatic on editor cursor movement (`onDidChangeTextEditorSelection`), or right-click "Explore Symbol in Graph".
* **Data Source**: Subgraph query bounded to $N \le 2$ hops around the current symbol.
* **Visuals**:
  * Central focused node (e.g., method `SubmitOrder`).
  * Inbound edges: All callers (`<-[:CALLS]-`).
  * Outbound edges: Downstream functions called (`-[:CALLS]->`).
  * Structural edges: Implemented interfaces (`-[:IMPLEMENTS]->`) and affected DB tables (`-[:USES_DB]->`).

### 4.2 Interactive Blast Radius (Refactoring Preview)
* **Feature**: Right-click symbol in editor $\rightarrow$ *"CodeExplorer: Show Impact Graph"*.
* **Behavior**:
  * Traverses incoming `CALLS` and `USES_TYPE` dependencies.
  * Highlights broken downstream callers in **Red / Amber**.
  * Shows associated Unit / Integration tests that cover this code path in **Green**.

### 4.3 Bi-directional Code Navigation
* **Graph $\rightarrow$ Code**: Double-clicking any `Type`, `Function`, `File`, or `Query` node sends an IPC message to the Extension Host, opening the exact file and selecting the start line/column.
* **Code $\rightarrow$ Graph**: Placing the cursor in a method animates a smooth camera pan and glow effect on the corresponding node in the graph webview.

---

## 5. IPC & Communication Protocol

The Webview and Extension Host communicate via typed request/response messages:

```typescript
// Shared Types (types/rpc.ts)
export type WebviewMessage =
  | { type: 'ready' }
  | { type: 'request_architecture_map' }
  | { type: 'request_neighborhood'; symbol: string; depth?: number }
  | { type: 'navigate_to_source'; filePath: string; line: number; column?: number }
  | { type: 'run_custom_cypher'; query: string };

export type HostMessage =
  | { type: 'set_architecture_map'; data: ArchitectureMapDto }
  | { type: 'set_neighborhood'; data: SubgraphDto }
  | { type: 'highlight_symbol'; symbolName: string }
  | { type: 'incremental_update'; updatedNodes: NodeDto[]; removedNodeIds: string[] }
  | { type: 'error'; message: string };
```

---

## 6. Distribution & Packaging Strategy

### 6.1 "Batteries-Included" Single-File Native Packaging
The extension bundles the self-contained native `ce` binary directly inside the `.vsix` package:

```text
code-explorer-vscode/
├── bin/
│   ├── win32-x64/ce.exe
│   ├── linux-x64/ce
│   └── darwin-arm64/ce
├── dist/
│   ├── extension.js
│   └── webview/
│       ├── index.html
│       ├── assets/
└── package.json
```

### 6.2 Target Platforms via `vsce package --target`
To keep download sizes minimal (~35 MB instead of 100+ MB), packages are built per target platform:
* `vsce package --target win32-x64`
* `vsce package --target linux-x64`
* `vsce package --target darwin-arm64`

### 6.3 Flexible Executable Resolution
```typescript
export function resolveCeBinary(context: vscode.ExtensionContext): string {
    const customPath = vscode.workspace.getConfiguration('codeExplorer').get<string>('executablePath');
    if (customPath && fs.existsSync(customPath)) return customPath;

    const bundled = path.join(context.extensionPath, 'bin', `${process.platform}-${process.arch}`, process.platform === 'win32' ? 'ce.exe' : 'ce');
    if (fs.existsSync(bundled)) return bundled;

    return 'ce'; // Fallback to system PATH
}
```

---

## 7. Implementation Roadmap

| Phase | Milestone | Deliverables |
| :--- | :--- | :--- |
| **Phase 1** | **Extension Scaffold & Macro View** | Extension project setup, Webview panel with Cytoscape.js, `get_architecture_map_workspace` rendering, Dagre layout. |
| **Phase 2** | **Navigation & Symbol Focus** | Editor selection listener, focused Ego-graph rendering ($N=1..2$), node click-to-code jump. |
| **Phase 3** | **Interactive Blast Radius** | "Show Impact Graph" context menu command, color-coded caller traversal, test file highlighting. |
| **Phase 4** | **Live Incremental Sync** | Editor save listener (`onDidSaveTextDocument`), surgical single-file indexer trigger, animated Cytoscape node/edge patching. |
