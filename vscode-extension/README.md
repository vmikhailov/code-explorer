# CodeExplorer VS Code Extension

Interactive code architecture, dependency graph visualizer, and call-chain explorer powered by CodeExplorer (`ce`).

---

## Features

- **Interactive Architecture & Dependency Graph**: Visualizes projects, classes, database tables, and API endpoints using high-performance Cytoscape.js and Dagre hierarchical layouts.
- **Click-to-Code Navigation**: Double-click any node or click "Jump to Code" in the details drawer to immediately open the source file at the exact declaration line in the editor.
- **Real-Time WebSocket Engine**: Communicates over low-latency WebSockets with `ce serve`, supporting live graph patching (`GRAPH_PATCH_EVENT`) and scan progress reporting (`SCAN_PROGRESS_EVENT`).
- **Ad-Hoc Cypher Querying**: Run custom openCypher queries directly from the top search bar (e.g. `MATCH (n:Project)-[r]->(m) RETURN n,r,m`) to explore arbitrary subgraphs and blast radius.
- **Zero Orphaned Processes**: Automatically allocates ephemeral ports and auto-shuts down background `ce` server instances when all windows are closed via idle timeout.

---

## Documentation

- [Implementation Variants & Architecture Tradeoffs](docs/implementation-variants.md) — Comprehensive analysis of communication channels (WebSocket vs CLI vs SQLite WASM), graph visualization engines (Cytoscape vs React Flow), UI layout modes, and binary packaging strategies.
- [WebSocket Protocol Specification](../proto/README.md) — Protobuf schema and TypeScript types for client-server communication.

---

## Development & Building

### Prerequisites
- Node.js 18+ and npm
- .NET 9 SDK (to build the `ce` CLI)

### Setup & Build
```bash
# Navigate to the extension folder
cd vscode-extension

# Install dependencies
npm install

# Build extension and webview bundles
npm run build

# Or run in watch mode for development
npm run watch

# Run TypeScript type check
npm run typecheck
```

### Debugging in VS Code
1. Open the repository root in VS Code.
2. Ensure `ce` has been built (`dotnet build cli/CodeExplorer.sln -c Release`).
3. Press `F5` (or choose "Launch Extension" from the Run & Debug view).
4. In the Extension Development Host window, open any workspace and run `CodeExplorer: Show Architecture Graph` from the Command Palette or click `⚡ Code Graph` in the status bar.

---

## Configuration Settings

| Setting | Default | Description |
|---|---|---|
| `codeExplorer.executablePath` | `""` | Custom path to the `ce` or `ce.exe` binary. If empty, automatically discovers local monorepo builds or searches system `PATH`. |
| `codeExplorer.serverPort` | `0` | Port for the CodeExplorer WebSocket server (`0` for automatically assigned ephemeral port). |
| `codeExplorer.idleTimeout` | `60` | Inactivity timeout in seconds before background server process terminates when no clients are connected. |
