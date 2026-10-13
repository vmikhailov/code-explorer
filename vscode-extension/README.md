# CodeExplorer

**A visual knowledge graph and codebase intelligence tool for developers and AI agents.**

CodeExplorer maps complex polyglot repositories, microservices, and monorepos into an **interactive visual knowledge graph** directly inside Visual Studio Code. It is powered by an **embedded SQLite graph database with native Cypher query compilation** and requires no external dependencies, Docker containers, or complex configuration.

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/all_services.png" alt="CodeExplorer Macro Architecture Concentric Orbits" width="100%" />
</p>
<p align="center">
  <em>Full-system microservice topology projected across 5 Concentric Architectural Orbits with live transitive dependency routing.</em>
</p>

---

## ⚡ Extension Features

CodeExplorer provides an interactive visual architecture studio inside VS Code (`Code Graph`). Designed for high-density architectures, it clarifies distributed systems and monoliths:

*   **Unified Studio Cockpit**: A single resizable right-hand docked panel with a top segmented tab switcher:
    *   `[ℹ️ Info]`: Detailed microservice, database, topic, and call relationship inspector.
    *   `[🪐 Orbits]`: Concentric orbit hierarchy, drag-and-drop orbit tier reordering, and tier visibility toggles.
    *   `[👁️ Hidden]`: Active hidden entities list with instant count badge, search filtering, and one-click restoration.
*   **Context-Aware Auto-Switch**: Clicking any microservice, database, topic, or edge on the canvas or matrix/grid immediately opens the panel (if closed) and focuses the `Info` inspector.
*   **Edge-to-Edge Canvas**: Visual workspace starts from `x: 0`, maximizing diagram space in VS Code without cramped dual-sidebar clutter.
*   **Three Presentation Modes**:
    *   `🕸️ Graph`: Interactive 2D canvas with 8 specialized layouts (Concentric Orbits, Equispaced, Polar Force, Domain Sectors, Architectural Swimlanes, Domain Islands, Hive Plots, and COSE Force).
    *   `⊞ Grid`: Unified Domain & Services Grid grouping components by DDD bounded contexts.
    *   `▦ Matrix`: High-density cross-service interaction matrix displaying caller-callee communication volumes.
*   **Project Flow & C1 Diagram Controls**:
    *   Dedicated `⛶ Fit` button to center and fit diagrams on demand.
    *   Zoom-invariant box and connection expansion preserving your manual zoom factor and pan coordinates.
    *   Interactive service cards with inbound callers, outbound dependencies, registered databases, and framework metadata.
*   **Advanced Edge Routing & Obstacle Avoidance**:
    *   `〰️ Bezier Curves`: Adaptive fluid curves based on chord length and angular span.
    *   `🛡️ Bypass Inner Orbits`: Automatically bends cross-orbit communication arcs around inner microservices to eliminate core clutter.
    *   `📏 Straight Lines`: Direct chords.
    *   On-hover edge label reveal mode.
*   **Smart Transitive Contraction**: Automatically synthesizes clean, dashed transitive links when intermediate brokers (Kafka, RabbitMQ, SQS) are hidden or filtered, preserving architectural fidelity without visual noise.
*   **Click-to-Code Navigation**: Double-click any node or click "Jump to Code" in the details drawer to immediately open the source file at the exact declaration line in your editor.
*   **Batteries-Included & Zero Setup**: Pre-compiled, self-contained `ce` native binaries are bundled for each platform (Windows x64/ARM64, macOS Apple Silicon/Intel, Linux x64/ARM64). No manual .NET installation or global CLI setup required!
*   **Ad-Hoc Cypher Querying**: Run custom openCypher queries directly from the top search bar (e.g. `MATCH (n:Project)-[r]->(m) RETURN n,r,m`) to explore arbitrary subgraphs and blast radius.
*   **Real-Time WebSocket Engine**: Communicates over low-latency WebSockets with the local `ce` engine, supporting live graph patching and scan progress reporting.

<p align="center">
  <img src="https://raw.githubusercontent.com/vmikhailov/code-explorer/main/docs/selected_services.png" alt="Focused Service Sub-Graph & Orbit Customization" width="100%" />
</p>
<p align="center">
  <em>Focused sub-graph inspection with live HUD metrics, unified right-hand sidebar, orbit reordering, and edge curvature sliders.</em>
</p>

---

## 🚀 Quick Start

1. Open any project workspace in VS Code.
2. Click the **`Code Graph`** button in the status bar or run `CodeExplorer: Show Architecture Graph` from the Command Palette (`Ctrl+Shift+P`).
3. Click the `Initialize & Scan` button in the UI (or run `ce scan` via CLI) to parse your repository and build the local SQLite knowledge graph.
4. Explore the graph, use the top search bar to filter nodes, or double-click nodes to jump straight to their source code.

---

## 🧠 Local Architectural Inference & MCP

CodeExplorer goes beyond just visualization:

*   **Model Context Protocol (MCP)**: The extension bundles a standard MCP server, giving AI agents (Cursor, Claude, Copilot, Antigravity) access to architectural maps, call chains, downstream blast-radius analysis, and arbitrary Cypher queries.
*   **Semantic Invariant Gates (SIG)**: Automated architectural governance using OpenCypher queries and ratchet baselines to prevent architectural erosion in CI/CD.
*   **Local Intent Distillation**: Automatically enriches graph nodes with business domains, architectural patterns, and capability tags using an embedded GGUF model via native `llama.cpp`.
*   **Deep Polyglot AST & Framework Ecosystem**: Bridges C# (.NET/EF Core), Java (Spring Boot/Kafka/RabbitMQ), TypeScript/JavaScript (React, Vue, Angular, Svelte, Vite, Jest, Vitest, Playwright, Cypress, Stripe, AWS, Azure, ClickHouse, BigQuery), Go (Gin, Fiber, Echo, pgx), Python (Flask, Django, requests, httpx, aiohttp), ColdFusion, and SQL into a single unified semantic graph.

---

## 🛠 Supported Platforms

The extension bundles native `ce` binaries for:

*   **Windows**: `x64`, `ARM64`
*   **macOS**: `Apple Silicon (M1/M2/M3/M4)`, `Intel x64`
*   **Linux**: `x64`, `ARM64`

*Note: Zero orphaned processes — automatically allocates ephemeral ports and auto-shuts down background server instances when all windows are closed.*

---

## ⚙️ Configuration Settings

| Setting | Default | Description |
|---|---|---|
| `codeExplorer.executablePath` | `""` | Custom path to the `ce` or `ce.exe` binary. If empty, automatically discovers bundled binaries or searches system `PATH`. |
| `codeExplorer.serverPort` | `0` | Port for the CodeExplorer WebSocket server (`0` for automatically assigned ephemeral port). |
| `codeExplorer.idleTimeout` | `60` | Inactivity timeout in seconds before background server process terminates when no clients are connected. |
