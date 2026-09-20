# CodeExplorer VS Code Extension — Implementation Variants & Architecture Tradeoffs

This document details and compares the architectural options, graph visualization rendering engines, backend communication channels, and distribution strategies for the **CodeExplorer** VS Code / Antigravity extension.

---

## 1. Extension $\leftrightarrow$ CodeExplorer Engine Communication

How the extension queries graph data, architecture topologies, and relationships from `.codeexplorer/graph.db`:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        VS Code Extension Host                          │
└──────────┬───────────────────┬───────────────────┬─────────────────────┘
           │ Option 1          │ Option 2          │ Option 3
           │ CLI Subprocess    │ MCP Daemon (RPC)  │ Direct SQLite WASM
           ▼                   ▼                   ▼
    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐
    │ ce query ... │    │  ce mcp      │    │ sql.js / WASM│
    │  (On Demand) │    │ (Persistent) │    │  (In-Process)│
    └──────┬───────┘    └──────┬───────┘    └──────┬───────┘
           │                   │                   │
           └───────────────────┼───────────────────┘
                               ▼
                ┌──────────────────────────────┐
                │    .codeexplorer/graph.db    │
                │        (SQLite WAL)          │
                └──────────────────────────────┘
```

### Option 1.1: CLI Subprocess (`ce query / ce export`)
The extension spawns the `ce` executable on demand as a child process:
- `ce query "MATCH ... RETURN ..." --json`
- `ce export --format json --type architecture`

| Pros | Cons |
| :--- | :--- |
| **Simple implementation**: The extension is completely stateless, requiring no session management or crash supervision. | **Process spawn latency**: 100–300 ms per `dotnet / ce.exe` invocation; too slow for real-time cursor tracking. |
| **Single source of truth**: Directly reuses the Cypher query compiler and domain logic. | Constant JSON serialization/deserialization overhead over stdout. |
| **Memory isolation**: Zero idle memory overhead; process terminates immediately after returning results. | No server-push capability (cannot notify the extension when background incremental scanning completes). |

---

### Option 1.2: Persistent MCP Daemon (stdio / JSON-RPC) — *(Recommended)*
On workspace activation, the extension launches `ce mcp` in the background and communicates over stdio using standard JSON-RPC / MCP protocols.

| Pros | Cons |
| :--- | :--- |
| **Sub-10ms response time**: Process is pre-warmed, JIT is compiled, and SQLite connection pool is active. | Requires lifecycle supervision (startup, graceful shutdown, restart on crash). |
| **Ready-to-use typed methods**: `get_architecture_map`, `get_call_chain`, `find_symbol`, `analyze_code_impact`. | Consumes steady background RAM (~60–100 MB). |
| **Bi-directional communication (Server Push)**: The engine can push notifications when the graph is updated after file edits. | Slightly more complex initial setup. |
| **Native compatibility**: The MCP protocol is already built, tested, and maintained in the core repository. | |

---

### Option 1.3: In-Process SQLite via WASM (Zero External Process)
The extension opens `.codeexplorer/graph.db` directly in read-only mode using `@sqlite.org/sqlite-wasm` or `sql.js` inside the Node.js extension host.

| Pros | Cons |
| :--- | :--- |
| **Ultra-fast read access (<2ms)**: Direct in-memory reads without IPC. | **Logic duplication**: The Cypher compiler (`CodeExplorer.Cypher`) does not exist in JS; queries must be written as raw SQL against `nodes` and `edges`. |
| **Web-ready**: Operates in browser-based VS Code environments (vscode.dev, github.dev) if the DB file is mounted. | Schema coupling: Any changes to the C# SQLite schema require synchronized updates to TypeScript SQL queries. |
| Does not require the `ce` binary for read-only visualization of an existing database. | Cannot perform workspace indexing (`ce scan`), which still requires the core engine. |

---

### Option 1.4: Local WebSocket API Server (`ce serve --port 0`) — *(Top Recommendation)*
The extension spawns `ce serve --port 0` (ephemeral port). The server exposes a bi-directional, full-duplex WebSocket endpoint (`ws://127.0.0.1:<port>/ws`):

```
┌────────────────────────────────────────────────────────────────────────┐
│                        VS Code Webview (iframe)                        │
│                                                                        │
│   const ws = new WebSocket("ws://127.0.0.1:<port>/ws");                │
└───────────────────────────────────▲────────────────────────────────────┘
                                    │ Direct WebSocket (ws://)
                                    │ (Zero Extension Host double-hop!)
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│               ce.exe (ASP.NET Core Minimal API + WebSockets)           │
│                                                                        │
│  - Endpoint: /ws (Full-duplex JSON messages)                           │
│  - Ephemeral port (--port 0) -> prevents port collisions               │
│  - Self-terminating heartbeat -> kills ce.exe when client disconnects  │
└───────────────────────────────────▲────────────────────────────────────┘
                                    │
                                    ▼
                     ┌──────────────────────────────┐
                     │    .codeexplorer/graph.db    │
                     │        (SQLite WAL)          │
                     └──────────────────────────────┘
```

| Pros | Cons |
| :--- | :--- |
| **Direct Webview connection**: Bypasses the Extension Host `postMessage` relay entirely. The Webview browser context communicates directly with `ce.exe` via native `new WebSocket(...)`. | Requires port management (solved by ephemeral `--port 0`). |
| **Sub-millisecond latency (<1ms)**: Zero HTTP handshake/header overhead per query. Cursor movements stream ego-graphs in real time without stutter. | Requires loopback binding (`127.0.0.1`) and handshake security token. |
| **Real-time server push**: Server pushes `graph_patch` (diffs from incremental file saves) and `scan_progress` (live indexing % and current file) without polling. | Small memory footprint (~60–90 MB RAM for ASP.NET Core process). |
| **Zero orphaned processes**: The server tracks active WebSocket connections. When VS Code closes or the Webview disconnects, the server automatically shuts down cleanly after a brief timeout. | |
| **External browser cockpit**: The exact same WebSocket endpoint can power a dedicated full-screen browser window on a second monitor (`http://localhost:<port>`). | |
| **Native in ASP.NET Core**: Built directly into `Microsoft.AspNetCore.App` via `app.UseWebSockets()` with zero third-party dependencies. | |

---

### Option 1.5: Hybrid Communication Architecture
- **Real-Time Interactive Graph & Navigation**: Local WebSocket Server (`ce serve --port 0`) connected directly to the Webview.
- **Background Heavy Jobs**: Spawning CLI commands (`ce scan <dir>`) with live output streamed over the WebSocket.
- **Fallback**: Stdio-based MCP Daemon (`ce mcp`) if local socket creation is restricted by OS firewall/security policy.

---

## 2. Graph Visualization & Rendering Engine (Webview Frontend)

Comparison of graph libraries evaluated for the VS Code Webview:

| Criterion | **Cytoscape.js** | **React Flow (@xyflow)** | **Sigma.js / Graphology** | **Mermaid.js (SVG)** |
| :--- | :--- | :--- | :--- | :--- |
| **Rendering** | HTML5 Canvas | DOM + React Components | WebGL / Canvas | SVG DOM |
| **Scalability** | Up to 5,000 – 10,000 nodes | Up to 500 – 1,000 nodes | 100,000+ nodes | Up to 100 nodes (DOM bottlenecks) |
| **Hierarchical Nesting** | **Native** (`compound nodes`: Project $\to$ File $\to$ Class) | Limited (requires custom sub-nodes) | Weak (flat graphs only) | Basic `subgraph` blocks |
| **Layout Algorithms** | **Extensive**: Dagre, fCoSE, Elk, Concentric, Cola | Basic (requires external Dagre/Elk) | ForceAtlas2, physics layouts | Fixed sequential Mermaid layouts |
| **Node Customization** | Canvas selectors & styles | **Maximum** (arbitrary HTML/JSX, buttons, tabs) | Shader/Canvas based | Minimal (basic CSS classes) |
| **Framework Agnostic** | Yes (plain JS/TS, works with or without React) | No (React-coupled, Svelte variant exists) | Yes | Yes |
| **Bundle Size** | ~300 KB | ~450 KB (+ React runtime) | ~250 KB | ~1.5 MB |

### Frontend Takeaways:
1. **Macro Architecture Maps & Call Trees**: **Cytoscape.js** with `cytoscape-dagre` and `cytoscape-fcose` is the optimal choice. It natively handles compound nested nodes (e.g. classes inside projects) and renders thousands of elements smoothly at 60 FPS on HTML5 Canvas.
2. **Detailed Node Inspectors**: React Flow excels at rich interactive node cards (e.g. lists of methods with clickable badges and ports), but suffers performance degradation on large solution graphs (100+ projects).
3. **Mermaid.js**: Suitable only for static Markdown exports, not for a fluid interactive explorer with smooth zooming, panning, and entity selection.

---

## 3. VS Code User Experience & Layout Modes

Where and how the graph is presented within the editor:

```
┌────────────────────────────────────────────────────────────────────────┐
│                              VS Code UI                                │
├─────────┬──────────────────────────────────┬───────────────────────────┤
│ Sidebar │ Editor Area (Active File)        │ Secondary Editor Column   │
│ (Mode 2)│                                  │ (Mode 1 - Full Canvas)    │
│ ┌─────┐ │ public class OrderService {      │ ┌───────────────────────┐ │
│ │Tree/│ │   [CodeLens: 3 Callers | Graph]  │ │   [Cytoscape Canvas]  │ │
│ │Mini │ │   public void ProcessOrder() {   │ │                       │ │
│ │Graph│ │       ...                        │ │  (A) ──> (B) ──> [DB] │ │
│ └─────┘ │   }                              │ │                       │ │
│         │ }                                │ └───────────────────────┘ │
└─────────┴──────────────────────────────────┴───────────────────────────┘
```

### Mode 3.1: Dedicated Editor Tab (Full Canvas)
- Opened via command `CodeExplorer: Open Architecture Map` or status-bar shortcut.
- Occupies the beside editor column (`ViewColumn.Beside`).
- Provides a full-screen canvas for panning, zooming, filtering node kinds, and running custom Cypher queries.

### Mode 3.2: Activity Bar Webview View (Sidebar Ego-Graph)
- Resides persistently in the secondary sidebar or activity bar (`WebviewViewProvider`).
- Automatically updates in real time to show an **"Ego-Graph"** (1–2 hops of inbound callers and outbound dependencies) centered on the symbol under the active editor cursor.

### Mode 3.3: CodeLens & Hover Integration
- Displays actionable CodeLens metrics above methods and classes:  
  `👁 4 callers | 2 tables | Impact Graph`
- Clicking focuses the graph canvas directly on that target symbol.

---

## 4. Binary Distribution & Packaging Strategies

How the `ce` engine is delivered to extension users:

### Strategy 4.1: "Batteries-Included" (Platform-Specific `.vsix` Packages) — *(Recommended)*
- Uses VS Code Marketplace targeting: `vsce package --target <platform>`.
- Generates platform-specific packages bundling self-contained `ce` binaries:
  - `code-explorer-win32-x64.vsix`
  - `code-explorer-linux-x64.vsix`
  - `code-explorer-linux-arm64.vsix`
  - `code-explorer-darwin-arm64.vsix` (Apple Silicon)
  - `code-explorer-darwin-x64.vsix` (Intel Mac)
- **Pros**: Frictionless out-of-the-box experience. Users click "Install" and it immediately works without requiring .NET installed on their system.
- **Cons**: Package size is ~35–45 MB per platform due to the self-contained native binary.

### Strategy 4.2: "Thin Client" (Prerequisite .NET Tool)
- The extension bundle is tiny (<1 MB), containing only TypeScript code.
- At runtime, it searches for `ce` in `$PATH` or the `codeExplorer.executablePath` setting.
- If missing, it displays a notification:  
  `"CodeExplorer CLI not found. Run 'dotnet tool install -g CodeExplorer.Cli'"`
- **Pros**: Minimal bundle size; simple single-package publishing.
- **Cons**: Requires the user to have .NET 10 SDK/Runtime installed globally.

### Strategy 4.3: On-Demand Auto-Download
- Extension is distributed as a thin package.
- On first activation, it detects the local OS/architecture and downloads the matching release archive from GitHub Releases (`https://github.com/vmikhailov/code-explorer/releases`).
- **Pros**: Small marketplace download; automatic installation without pre-installed .NET.
- **Cons**: Requires outbound internet access on first launch (can fail in restricted corporate environments).

---

## 5. Decision Matrix & Recommended MVP Roadmap

| Dimension | MVP (Phase 1) | Target Architecture (Phase 2+) |
| :--- | :--- | :--- |
| **Communication Channel** | **Local WebSocket API Server (`ce serve --port 0`)** with direct Webview connection | WebSocket Server with real-time `graph_patch` streaming and self-terminating heartbeat |
| **Graph Rendering** | **Cytoscape.js** + `cytoscape-dagre` (fast start, high performance) | Cytoscape.js for graph canvas + Svelte/React HUD for controls & node inspectors |
| **UI Placement** | Dedicated Editor Tab (`ViewColumn.Beside`) | Editor Tab + persistent Sidebar Ego-Graph + inline CodeLens + External Browser Cockpit |
| **Binary Resolution** | PATH lookup + monorepo local build auto-detection | Platform-specific `.vsix` releases bundled via GitHub Actions |
| **Interactivity** | Bi-directional Jump-to-Code (clicking node opens code line in editor) | Visual Blast Radius highlighting (amber/red impact paths) and Cypher playground |
