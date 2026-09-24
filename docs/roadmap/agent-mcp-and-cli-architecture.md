# Architectural Specification: Unified Semantic & Project Architecture for MCP & CLI 🤖🌐

## 1. Executive Summary & Problem Statement

Following the recent architectural overhaul of **CodeExplorer (`ce`)**:
1. **`GraphDataConverter.cs` (~2,400 lines) was completely eliminated.**
2. **`ArchitectureViewEngine`** is now the single source of truth for architecture projections (C1 System Context, C2 Service Flow, C3 Component Drill-Down).
3. **`ProjectSemanticNode` was abolished.** Applications, Services, and Libraries are now first-class semantic entities, and macro-edges (`SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`) are canonicalized and materialized during index time via `PostIndexAnalyzer`.

### The Problem
While the VS Code Webview leverages `ArchitectureViewEngine` via `ArchitectureQueryService`, the **Model Context Protocol (MCP) layer (`McpGraphHandler`, `CodeExplorerRepository`)** and the **CLI (`codeexplorer`)** still suffer from functional gaps:
- MCP graph tools (`get_architecture_overview`, `get_architecture_map`) still run legacy, un-materialized raw Cypher queries that bypass `ArchitectureViewEngine`.
- AI coding agents have no clean way to distinguish between **Runtime / Semantic interactions** (network calls, queues, databases) and **Build / Project dependencies** (csproj/package references).
- Output payloads are either too granular (raw AST/node arrays) or lack token-efficient formats tailored for LLM reasoning windows.

---

## 2. Core Concepts: Semantic vs. Project Views

When an AI agent explores a repository, it reasons across two distinct dimensions:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       DIMENSION 1: RUNTIME / SEMANTIC                       │
│  "What happens when the system runs?"                                        │
│  • Services / Apps communicating over HTTP/gRPC (`SERVICE_CALL`)             │
│  • Message pub/sub topics (`PUBLISHES_TO`, `SUBSCRIBES_TO`)                 │
│  • Data persistence access (`USES_DB`, `WRITES_TO`, `READS_FROM`)            │
│  • External third-party integrations (`CALLS_EXTERNAL`)                     │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                       DIMENSION 2: BUILD / STRUCTURAL                       │
│  "How is the codebase compiled and packaged?"                               │
│  • Project-to-project references (`PROJECT_REFERENCE`)                       │
│  • NuGet / NPM / Pip package dependencies (`DEPENDS_ON`)                     │
│  • Solution and monorepo workspace boundaries                               │
│  • Shared utility and infrastructure libraries (`is_library: true`)          │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 3. MCP Tool Specifications

### 3.1 `get_architecture_view` (New Primary Architecture Tool)
Replaces ad-hoc overview queries with direct calls to `ArchitectureViewEngine`.

* **Signature:**
  ```csharp
  [McpServerTool]
  [Description("Retrieves the high-level architecture topology at C4 abstraction levels (System Context, Service Flow, or Component Drill-down). Powered by ArchitectureViewEngine with materialized macro-edges.")]
  public async Task<CallToolResult> GetArchitectureViewAsync(
      [Description("View level: 'system_context' (C1: top-level services, databases, message topics), 'service_flow' (C2: service communication and queues), 'component' (C3: internal components of a project).")] string viewType = "system_context",
      [Description("Optional project or service name to scope the view (required for 'component' view).")] string? scope = null,
      [Description("Whether to include shared utility and infrastructure libraries (default: false).")] bool includeLibraries = false,
      [Description("Output format: 'markdown' (default), 'toon', 'mermaid', or 'json'.")] string format = "markdown",
      [Description("Optional workspace root path.")] string? workspacePath = null,
      CancellationToken cancellationToken = default);
  ```

* **Format Behaviors:**
  - **`markdown`** (Default): Compact Markdown bullet hierarchy + connection summary table.
  - **`toon`**: Token-Oriented Object Notation (indented key-value tuples without JSON punctuation, saving 40-50% tokens).
  - **`mermaid`**: Clean `flowchart LR` diagram ready to render or reason over.
  - **`json`**: Compact JSON representation of `GraphDataDto` (`WriteIndented = false`).

---

### 3.2 `get_project_dependencies` (Enhanced)
Extends the existing tool to allow AI agents to isolate runtime vs build dependencies.

* **Added Parameter:**
  - `type` (string, default: `"all"`):
    - `"all"`: Returns both runtime semantic connections and compile-time package/project references.
    - `"runtime"` / `"semantic"`: Only returns active communication paths (`SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`).
    - `"build"` / `"structural"`: Only returns project references (`PROJECT_REFERENCE`) and external package dependencies (`DEPENDS_ON`).

* **Example Markdown Output (`--type runtime`):**
  ```markdown
  ### Runtime Dependencies for `OrderService`
  - **Downstream Services (Calls)**:
    - `PaymentGateway` via HTTP `POST /api/v1/charge`
    - `InventoryService` via gRPC `CheckStock`
  - **Messaging**:
    - Publishes to: `topic:orders.created`, `topic:orders.cancelled`
    - Subscribes to: `topic:payments.processed`
  - **Databases**:
    - `postgres:orders_db` (via EF Core)
  ```

---

### 3.3 `get_service_contracts` (New)
Allows an agent to inspect the public surface and consumption requirements of a specific service without reading file ASTs.

* **Signature:**
  ```csharp
  [McpServerTool]
  [Description("Extracts the ingress (endpoints/subscribers) and egress (HTTP calls/publishers/DBs) contracts for a specified service.")]
  public async Task<CallToolResult> GetServiceContractsAsync(
      [Description("Name of the service or project.")] string serviceName,
      [Description("Direction: 'ingress' (exposed endpoints/consumers), 'egress' (external calls/publishers), or 'all' (default).")] string direction = "all",
      [Description("Output format: 'markdown', 'toon', or 'json'.")] string format = "markdown",
      [Description("Optional workspace root path.")] string? workspacePath = null,
      CancellationToken cancellationToken = default);
  ```

---

### 3.4 `trace_cross_service_flow` (New)
Enables agents to trace end-to-end flows spanning across service boundaries (e.g., from an API Controller in Service A through an event bus to a Consumer in Service B that updates a database).

* **Signature:**
  ```csharp
  [McpServerTool]
  [Description("Traces an end-to-end execution flow across service boundaries starting from an entry point or service to downstream queues, services, and databases.")]
  public async Task<CallToolResult> TraceCrossServiceFlowAsync(
      [Description("Originating service or project name.")] string startService,
      [Description("Optional starting entry point name (e.g. 'CheckoutController.SubmitOrder').")] string? entryPoint = null,
      [Description("Maximum cross-service traversal depth (1-5, default: 3).")] int maxDepth = 3,
      [Description("Output format: 'mermaid' (default), 'markdown', or 'json'.")] string format = "mermaid",
      [Description("Optional workspace root path.")] string? workspacePath = null,
      CancellationToken cancellationToken = default);
  ```

---

## 4. CLI Parity Commands

The CLI (`codeexplorer` / `ce`) must provide 1:1 parity with MCP tools for scripting and terminal workflows:

```bash
# 1. Architecture Views
ce view architecture --level system-context --format markdown
ce view architecture --level service-flow --scope OrderService --format mermaid
ce view architecture --level component --scope PaymentGateway --format toon

# 2. Filtered Dependencies
ce dependencies --project OrderService --type runtime --format markdown
ce dependencies --project OrderService --type build --format toon

# 3. Service Contracts
ce contracts --service OrderService --direction all --format markdown

# 4. Cross-Service Flow Tracing
ce trace flow --from OrderService --entry "SubmitOrder" --format mermaid
```

---

## 5. Token-Efficient Serializer Specifications

For AI agent consumption, output density is paramount. All architectural tools must route their `GraphDataDto` or graph records through formatters:

### 5.1 TOON (Token-Oriented Object Notation)
```yaml
system_context:
  services:
    - id: OrderService, type: Service, framework: ASP.NET Core
    - id: PaymentService, type: Service, framework: NestJS
  databases:
    - id: orders_db, engine: postgresql, used_by: [OrderService]
  topics:
    - id: orders.created, publishers: [OrderService], subscribers: [PaymentService]
  service_calls:
    - from: OrderService, to: PaymentService, protocol: HTTP, via: HttpClient
```

### 5.2 Mermaid Flowchart
```mermaid
flowchart LR
  OrderService[OrderService] -->|HTTP POST /charge| PaymentService[PaymentService]
  OrderService -->|Publish| TopicOrders[topic:orders.created]
  TopicOrders -->|Subscribe| PaymentService
  OrderService -->|USES_DB (EF Core)| DbOrders[(postgres:orders_db)]
```

---

## 6. Implementation Guide for Developer / AI Agent

### Step 1: Update `ArchitectureViewEngine.cs`
- Add projection formatting extensions:
  - `ToMarkdown(this GraphDataDto dto)`
  - `ToToon(this GraphDataDto dto)`
  - `ToMermaid(this GraphDataDto dto)`
- Add helper method for `trace_cross_service_flow`:
  - Run traversal query starting from `(p:Project { name: $startService })` following `SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, and `USES_DB` up to `maxDepth`.

### Step 2: Implement Repository Layer (`CodeExplorerRepository.cs`)
- Wire up `ArchitectureViewEngine` directly in `CodeExplorerRepository`:
  - `GetArchitectureViewAsync(...)`
  - `GetServiceContractsAsync(...)`
  - `TraceCrossServiceFlowAsync(...)`
- Update `GetProjectDependenciesAsync(...)` to support the new `type` parameter (`runtime`, `build`, `all`).
- Redirect `GetArchitectureOverviewAsync` and `GetArchitectureMapAsync` to delegate to `ArchitectureViewEngine.GetViewAsync`.

### Step 3: Expose Tools in `McpGraphHandler.cs`
- Decorate new methods with `[McpServerTool]` and rich `[Description]` attributes.
- Ensure all calls accept `workspacePath` and propagate `CancellationToken`.

### Step 4: Add CLI Commands in `UI/CodeExplorer`
- Create or update command handlers under `Commands/`:
  - `ViewArchitectureCommandHandler.cs`
  - `ContractsCommandHandler.cs`
  - `TraceFlowCommandHandler.cs`

### Step 5: Verification & Tests
- Add unit and integration tests in `cli/tests/CodeExplorer.Tests/`:
  - `ArchitectureViewEngineFormatTests.cs`: Verify Markdown, TOON, and Mermaid generators produce valid syntax.
  - `McpArchitectureToolsTests.cs`: Verify `get_architecture_view`, `get_service_contracts`, and `get_project_dependencies` against mock SQLite database.
  - Test on real monorepo graphs.
