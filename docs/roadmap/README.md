# CodeExplorer Engineering Roadmap & Technical Specifications 🗺️🤖

This directory tracks the engineering roadmap, active specifications, and actionable backlog for **CodeExplorer (`ce`)**.
* For the active checklist and immediate refactoring tasks, see [**`todo.md`**](./todo.md).

---

## 🚨 Immediate Focus: First-Class Semantic Entities (`Service`, `App`, `Library`, `Worker`, `CliTool`)

Transitioning from `Project` with JSON `properties.role` to dedicated first-class nodes in SQLite (`kind = 'Service'`, `kind = 'App'`, etc.) with Cypher polymorphic fallback `MATCH (p:Project)`. See detailed plan in [**`todo.md`**](./todo.md).

---

## 1. Executive Status & Delivered Milestones

CodeExplorer has transitioned from a proof-of-concept graph visualizer into a high-performance, embedded, zero-hack semantic knowledge graph engine.

### ✓ Completed Architectural Milestones
* **Zero-Hack Semantic Architecture & Materialized Macro-Edges**:
  * **Eliminated `GraphDataConverter.cs` entirely** (~2,400 lines of legacy runtime heuristics).
  * Consolidated all architectural projections (C1 System Context, C2 Service Flow, C3 Component Drill-Down) into **`ArchitectureViewEngine`**.
  * **Abolished `ProjectSemanticNode`**: Promoted `Service`, `App`, and `Library` to first-class semantic entities directly in the ontology.
  * **Shifted computation from read time to index time**: `PostIndexAnalyzer` pre-computes and materializes direct macro-edges (`SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`) with sub-millisecond query performance.
  * **`ResourceReconciliationService`**: Discovers external infrastructure from `docker-compose`, `.env`, and `appsettings.json`, eliminating duplicate database nodes and canonicalizing database/topic URNs.
* **Embedded SQLite & Cypher Compiler**:
  * In-process SQLite engine with WAL mode and covering indexes, eliminating external graph database containers.
  * AST-based Cypher compiler (`CodeExplorer.Cypher`) translating MATCH, WITH, WHERE, pattern comprehensions, and subqueries into SQLite Recursive CTEs.
  * Self-contained cross-platform single-file binary (`ce` / `ce.exe`, ~58 MB).
* **Robust MCP Server & Client Ergonomics**:
  * Multi-source workspace locator (`WorkspaceLocator.FindWithFallbacks`) supporting environment variables, CLI flags, and directory walking.
  * Standby graceful degradation (process boots without crashing when launched in unbound directories).
  * Multi-format outputs tailored for LLMs: compact Markdown tables, native Mermaid diagrams (`flowchart`, `graph TD`), compact JSON (`WriteIndented = false`), YAML, and TOON.
* **Multi-Language AST Parsers & Monorepo Support**:
  * Complete framework parsers: NestJS (`@Module`, `@Controller`, `@Injectable`), Express, Fastify, Next.js App Router, BullMQ, KafkaJS, Elasticsearch, Sequelize, TypeORM, Prisma, Drizzle.
  * Recursive monorepo `.gitignore` evaluation and automatic heuristic pruning of minified bundles and build artifacts (`node_modules`, `dist`, `bin`, `obj`).
* **VS Code Webview Cockpit**:
  * Interactive Cytoscape.js webview with C1/C2/C3 projections (`DomainArchitectureView`, `ProjectFlowView`).
  * Real-time WebSocket bridge with auto-reconnection state machine and bidirectional selection synchronization.
  * Modularized CSS stylesheets and decoupled rendering pipeline (`flowEdgeRenderer.ts`).
* **Scale Verification**:
  * Verified on enterprise codebases (140+ projects, 150,000+ nodes, 650,000+ edges) with clean test execution (0 warnings, 0 errors with `/warnaserror`).

---

## 2. Active Feature Specification: Unified Semantic & Project Architecture for MCP & CLI

### 2.1 Problem & Motivation
While the VS Code Webview leverages `ArchitectureViewEngine` via `ArchitectureQueryService`, the **MCP server (`McpGraphHandler`, `CodeExplorerRepository`)** and **CLI (`codeexplorer`)** still use legacy, un-materialized raw Cypher queries.

Furthermore, AI agents need to explicitly distinguish between:
1. **Runtime / Semantic interactions**: Network calls (`SERVICE_CALL`), messaging (`PUBLISHES_TO`, `SUBSCRIBES_TO`), database access (`USES_DB`), and external APIs.
2. **Build / Structural dependencies**: Project references (`PROJECT_REFERENCE`), package dependencies (`DEPENDS_ON`), and shared utility libraries (`is_library: true`).

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

### 2.2 New & Updated MCP Tools

#### 1. `get_architecture_view` (New Primary Architecture Tool)
Replaces ad-hoc overview queries with direct single-query projections from `ArchitectureViewEngine`.
* **Parameters**:
  - `viewType` (string, required): `"system_context"` (C1: top-level services, databases, topics), `"service_flow"` (C2: communication & macro-edges), or `"component"` (C3: internal components of a project).
  - `scope` (string, optional): Specific project/service name to filter (required for `"component"` view).
  - `includeLibraries` (bool, optional, default: false): Include shared utility/infra libraries.
  - `format` (string, optional, default: `"markdown"`): Output format: `"markdown"`, `"toon"`, `"mermaid"`, or `"json"`.
  - `workspacePath` (string, optional): Target workspace path.

#### 2. `get_project_dependencies` (Enhanced with Filtering)
Allows AI agents to isolate runtime vs build dependencies.
* **Added Parameter**:
  - `type` (string, default: `"all"`):
    - `"all"`: Returns both runtime communication and compile-time package/project references.
    - `"runtime"` / `"semantic"`: Only returns active communication paths (`SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`).
    - `"build"` / `"structural"`: Only returns project references (`PROJECT_REFERENCE`) and external package dependencies (`DEPENDS_ON`).

#### 3. `get_service_contracts` (New)
Extracts the ingress (endpoints/consumers) and egress (HTTP calls/publishers/DBs) contracts for a specified service.
* **Parameters**:
  - `serviceName` (string, required): Target service or project name.
  - `direction` (string, optional, default: `"all"`): `"ingress"`, `"egress"`, or `"all"`.
  - `format` (string, optional, default: `"markdown"`): `"markdown"`, `"toon"`, or `"json"`.

#### 4. `trace_cross_service_flow` (New)
Traces an end-to-end execution flow across service boundaries starting from an entry point or service to downstream queues, services, and databases.
* **Parameters**:
  - `startService` (string, required): Originating service or project name.
  - `entryPoint` (string, optional): Starting entry point or controller method name.
  - `maxDepth` (int, optional, default: 3): Maximum cross-service traversal depth (1–5).
  - `format` (string, optional, default: `"mermaid"`): `"mermaid"`, `"markdown"`, or `"json"`.

---

### 2.3 CLI Command Parity
Expose 1:1 parity in the CLI (`ce` / `codeexplorer`):
```bash
# Architecture Views
ce view architecture --level system-context --format markdown
ce view architecture --level service-flow --scope OrderService --format mermaid
ce view architecture --level component --scope PaymentGateway --format toon

# Dependencies Filtering
ce dependencies --project OrderService --type runtime --format markdown
ce dependencies --project OrderService --type build --format toon

# Service Contracts
ce contracts --service OrderService --direction all --format markdown

# Cross-Service Tracing
ce trace flow --from OrderService --entry "SubmitOrder" --format mermaid
```

---

### 2.4 Token-Efficient Output Serializers for LLMs

To minimize token usage in LLM reasoning contexts, all architectural projections support four output modes:
1. **TOON (Token-Oriented Object Notation)**:
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
2. **Mermaid Flowchart (`flowchart LR`)**: Directed, readable syntax for diagrams and quick structural comprehension.
3. **Compact Markdown**: Grouped bullet hierarchy and concise markdown summary tables.
4. **Compact JSON**: `GraphDataDto` with `WriteIndented = false`.

---

### 2.5 Implementation Instructions for Developers & Agents
1. **`ArchitectureViewEngine.cs`**:
   - Add projection serializers: `ToMarkdown(GraphDataDto dto)`, `ToToon(GraphDataDto dto)`, `ToMermaid(GraphDataDto dto)`.
   - Add `TraceCrossServiceFlowAsync(string startService, string? entryPoint, int maxDepth, CancellationToken ct)` executing bounded multi-hop traversal over macro-edges.
2. **`CodeExplorerRepository.cs`**:
   - Wire `ArchitectureViewEngine` directly to service calls.
   - Implement `GetArchitectureViewAsync`, `GetServiceContractsAsync`, and `TraceCrossServiceFlowAsync`.
   - Update `GetProjectDependenciesAsync` with the `type` parameter (`runtime`, `build`, `all`).
   - Deprecate old raw Cypher queries in `GetArchitectureOverviewAsync` / `GetArchitectureMapAsync` by delegating to `ArchitectureViewEngine`.
3. **`McpGraphHandler.cs`**:
   - Expose the new tools with rich `[Description]` annotations so AI reasoning agents correctly select tools.
4. **CLI (`Commands/`)**:
   - Add `ViewArchitectureCommandHandler`, `ContractsCommandHandler`, and `TraceFlowCommandHandler`.
5. **Testing**:
   - Add unit tests in `CodeExplorer.Tests` verifying serializer output integrity and MCP tool handlers against mock SQLite databases.

---

## 3. Active Engineering Backlog & Master Checklist

### Epic 1: Agent Semantic & Project Architecture (MCP & CLI Parity) 🚀
- [ ] **1.1 `get_architecture_view` MCP Tool**: Connect MCP to `ArchitectureViewEngine` for C1/C2/C3 projections.
- [ ] **1.2 Runtime vs Build Filtering**: Add `--type runtime|build|all` to `get_project_dependencies`.
- [ ] **1.3 `get_service_contracts` MCP Tool**: Ingress/egress surface extraction per service.
- [ ] **1.4 `trace_cross_service_flow` MCP Tool**: Cross-service macro-edge flow tracing.
- [ ] **1.5 CLI Parity Commands**: Implement `ce view architecture`, `ce dependencies --type`, and `ce contracts`.
- [ ] **1.6 Token Serializers**: Implement Markdown, TOON, and Mermaid formatters in `ArchitectureViewEngine`.

### Epic 2: Parser Intelligence & High-Fidelity Lineage 🔍
- [ ] **2.1 ASP.NET Core Hierarchical Route Composition**:
  - Combine Controller `[Route("api/[controller]")]` with Action `[HttpGet("{id}")]` and resolve tokens into canonical routes (`GET:/api/Orders/{id}`).
  - Support Minimal API `app.MapGroup("/api/v1")` hierarchical prefix chaining.
- [ ] **2.2 C# Constructor Dependency Injection Mapping**:
  - Map constructor and primary constructor parameters to private fields (`_orderService`).
  - Resolve invocations through `[:IMPLEMENTS]` edges to concrete implementation classes during Layer 5 Late Binding.
- [ ] **2.3 Declarative HTTP Clients & Egress**:
  - Resolve base URLs and relative endpoints from `HttpClient`, `RestSharp`, and `Refit` declarative interfaces.
- [ ] **2.4 ORM Data Lineage Mapping (EF Core & Dapper)**:
  - Extract database table names from EF Core `DbSet<T>` properties and Fluent API `ToTable("...")`.
  - Link SQL string queries directly to database table nodes in `inspect_data_lineage`.

### Epic 3: Cypher Query Engine & Transpiler Expansion ⚡
- [ ] **3.1 OpenCypher Relationship Functions & Edge Bindings**:
  - Expose relationship variables `r` in the symbol table with bindings to `edges.kind` (`type(r)`), `edges.properties_json` (`properties(r)`), `edges.from_id` (`startNode(r)`), and `edges.to_id` (`endNode(r)`).
  - Support attribute extraction directly on relationship variables (`r.via`, `r.call_chain`).
  - Support edge variables across `WITH` clauses and aggregations.
- [ ] **3.2 Multi-Branch `OPTIONAL MATCH` Cartesian Product Elimination**:
  - Transpile independent `OPTIONAL MATCH` branches into discrete correlated subqueries or separate CTEs aggregated by root node ID, preventing $O(N \cdot M \cdot K)$ intermediate row explosion.
- [ ] **3.3 Path & List Predicates in Transpiler**:
  - Transpile `WHERE EXISTS((n)-[:REL]->(m))` into SQL `EXISTS` subqueries.
  - Transpile list predicates (`all()`, `any()`, `none()`) over JSON arrays via `json_each()`.

### Epic 4: Concurrency, Incremental Watcher & Performance 🛡️
- [ ] **4.1 Incremental File Watcher (`ce watch`)**:
  - Implement file system watcher with a 500ms debounce buffer.
  - Trigger targeted `ParsingContext.IsSubtreeScan` on modified files, updating the SQLite graph in sub-second intervals.
- [ ] **4.2 Read-Only Connection Pooling for Tools**:
  - Enforce `Mode=ReadOnly;Cache=Shared;` for all analytical MCP tool connections to eliminate reader/writer lock contention.
- [ ] **4.3 In-Memory Projection Caching**:
  - Cache static macro-structures (`SystemContext`, `Taxonomy`) with automated invalidation hooks on `ce scan` / `ce clear` / `ce watch`.

### Epic 5: Test Automation & Benchmark Fixtures 🧪
- [ ] **5.1 Portable Synthetic 100k-Node CI Benchmark Fixture**:
  - Implement an in-memory synthetic graph generator seeding 10,000 files, 25,000 classes, 70,000 methods, and 300,000 edges.
  - Automated CI assertions: `find_symbol` < 25ms, `get_call_chain` (3 hops) < 100ms, `inspect_data_lineage` < 50ms, Cypher multi-branch projections < 150ms.

---

## 4. Master Implementation Checklist

| ID | Epic | Description | Priority | Status |
| :--- | :--- | :--- | :--- | :--- |
| **1.1** | Agent/MCP | Implement `get_architecture_view` MCP tool backed by `ArchitectureViewEngine` | High | ⏳ Pending |
| **1.2** | Agent/MCP | Add `--type runtime\|build\|all` filtering to `get_project_dependencies` | High | ⏳ Pending |
| **1.3** | Agent/MCP | Implement `get_service_contracts` MCP tool (ingress / egress) | High | ⏳ Pending |
| **1.4** | Agent/MCP | Implement `trace_cross_service_flow` MCP tool | High | ⏳ Pending |
| **1.5** | CLI | Expose `ce view architecture`, `ce dependencies --type`, `ce contracts` in CLI | High | ⏳ Pending |
| **1.6** | Formats | Implement compact Markdown, TOON, and Mermaid serializers for architecture views | High | ⏳ Pending |
| **2.1** | Parsers | ASP.NET Core hierarchical route composition and Minimal API `MapGroup` | Medium | ⏳ Pending |
| **2.2** | Parsers | C# constructor DI mapping and interface call late binding in Layer 5 | Medium | ⏳ Pending |
| **2.3** | Parsers | Declarative HTTP clients (Refit/RestEase) and HttpClient URI resolution | Medium | ⏳ Pending |
| **2.4** | Parsers | EF Core `DbSet<T>` / `ToTable` and Dapper SQL lineage | Medium | ⏳ Pending |
| **3.1** | Cypher | Relationship functions (`type(r)`, `properties(r)`, `startNode`, `endNode`) | Medium | ⏳ Pending |
| **3.2** | Cypher | Multi-branch `OPTIONAL MATCH` Cartesian product elimination | Medium | ⏳ Pending |
| **3.3** | Cypher | Path predicates (`EXISTS((a)->(b))`) and list quantifiers (`any`, `all`, `none`) | Low | ⏳ Pending |
| **4.1** | Concurrency | Incremental file watcher (`ce watch`) with debounced subtree updates | Medium | ⏳ Pending |
| **4.2** | Concurrency | Enforce `Mode=ReadOnly` connection pooling for MCP read tools | Medium | ⏳ Pending |
| **5.1** | Testing | Portable synthetic 100k-node graph benchmark fixture for CI/CD | Low | ⏳ Pending |
