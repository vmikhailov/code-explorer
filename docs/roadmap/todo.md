# CodeExplorer Active Backlog & Engineering Roadmap 📋

This document tracks active epics, planned technical initiatives, and the master checklist for **CodeExplorer (`ce`)**.

For completed architectural debt remediation phases (Canonical Resources, C4 Typing, ArchitectureViewEngine, SQLite WAL), see [**`tech-debt.md`**](./tech-debt.md).  
For the detailed specification on exposing semantic vs. project views to AI agents, see [**`agent-mcp-and-cli-architecture.md`**](./agent-mcp-and-cli-architecture.md).

---

## 1. High-Level Summary of Delivered Milestones

The following capabilities have been fully implemented, tested, and shipped:

- [x] **Zero-Hack Architecture Engine & Ontology Cleanup (Phases 1–6)**:
  - Eliminated `GraphDataConverter.cs` (~2,400 lines) in favor of `ArchitectureViewEngine`.
  - Abolished `ProjectSemanticNode`; promoted Services, Apps, and Libraries to first-class entities.
  - Materialized macro-edges (`SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`) in `PostIndexAnalyzer`.
  - Added `ResourceReconciliationService` for canonical database deduplication and infra discovery (`docker-compose`, `.env`, `appsettings`).
  - Modularized frontend webview stylesheets (3,700-line `styles.css` split into scoped component files).
- [x] **Embedded SQLite & Cypher Compiler**:
  - Embedded SQLite backend with WAL mode, parameterized recursive CTEs, and covering indexes.
  - Self-contained single-file cross-platform binary (`ce` / `ce.exe`).
- [x] **MCP Client Ergonomics & Standby Resilience**:
  - Multi-source workspace locator (`WorkspaceLocator.FindWithFallbacks`) supporting environment variables, CLI root, and current working directory.
  - Graceful standby mode (process boots without crashing when spawned in empty or foreign directories).
  - Multi-format tool outputs: compact Markdown tables, native Mermaid diagrams (`graph TD`, flowchart), compact JSON (`WriteIndented = false`), YAML, and TOON.
- [x] **Multi-Language Library AST Parsers**:
  - Full TypeScript & Node.js framework parsers: NestJS (`@Module`, `@Controller`, `@Injectable`), Express, Fastify, Next.js App Router, BullMQ, KafkaJS, Elasticsearch, Sequelize, TypeORM, Prisma, Drizzle.
  - Hierarchical `.gitignore` loading in monorepos and safe defaults (`node_modules`, `dist`, `bin`, `obj`).
- [x] **Token-Based Mutating Query Security Validator**: Safe execution blocking on `CREATE`, `SET`, `DELETE` with word boundaries.
- [x] **Real-World Multi-Project Verification**: Verified on 140+ project enterprise repositories (150,000+ nodes, 650,000+ edges) with sub-second queries.

---

## 2. Active Engineering Epics

### Epic 1: Agent Semantic & Project Architecture (MCP & CLI Parity) 🚀
> **Spec**: [Detailed Specification (`agent-mcp-and-cli-architecture.md`)](./agent-mcp-and-cli-architecture.md)

Enable AI coding agents and CLI scripts to query clean architectural topology and differentiate runtime vs compile-time relationships:
- [ ] **1.1 `get_architecture_view` MCP Tool**: Connect MCP directly to `ArchitectureViewEngine` for C1 (System Context), C2 (Service Flow), and C3 (Component Drill-Down).
- [ ] **1.2 Runtime vs Build Filtering in `get_project_dependencies`**: Add `--type runtime|build|all` parameter to isolate IPC/network/DB communication from project/package references.
- [ ] **1.3 `get_service_contracts` MCP Tool**: Query ingress (endpoints/consumers) and egress (HTTP clients/publishers/DBs) contracts per service.
- [ ] **1.4 `trace_cross_service_flow` MCP Tool**: Trace end-to-end execution paths across service boundaries (Controller -> HTTP/Event -> Consumer -> Database).
- [ ] **1.5 CLI Command Parity**: Expose `ce view architecture`, `ce dependencies --type`, and `ce contracts` in the CLI.
- [ ] **1.6 Token-Efficient Serializers**: Add compact Markdown, TOON, and Mermaid serializers for all architectural projections.

---

### Epic 2: Parser Intelligence & High-Fidelity Lineage 🔍

Deepen C# and multi-language parser resolution for complex enterprise patterns:
- [ ] **2.1 ASP.NET Core Hierarchical Route Composition**:
  - Combine Controller-level `[Route("api/[controller]")]` or `[Route("api/v{version}/[controller]")]` with Action-level attributes (`[HttpGet("{id}")]`), resolving route tokens into canonical routes (`GET:/api/Orders/{id}`).
  - Track Minimal API `app.MapGroup("/api/v1")` prefixes and hierarchical group chaining.
- [ ] **2.2 C# Constructor Dependency Injection Mapping**:
  - Map constructor and primary constructor parameters (`(IOrderService orderService)`) to private fields (`_orderService`).
  - Resolve invocations (`_orderService.Create()`) through `[:IMPLEMENTS]` edges to concrete implementation classes during Layer 5 Late Binding.
- [ ] **2.3 Declarative HTTP Clients & Egress**:
  - Extract base URLs and relative paths from `HttpClient`, `RestSharp`, and declarative interface contracts (`Refit`, `RestEase`).
- [ ] **2.4 ORM Data Lineage Mapping (EF Core & Dapper)**:
  - Extract database table names from EF Core `DbSet<T>` properties and Fluent API `ToTable("...")` configurations.
  - Link SQL queries directly to database table nodes in `inspect_data_lineage`.

---

### Epic 3: Cypher Query Engine & Transpiler Expansion ⚡

Enhance OpenCypher compatibility and execution performance in `CodeExplorer.Cypher`:
- [ ] **3.1 OpenCypher Relationship Functions & Edge Bindings**:
  - Expose relationship variables `r` in the symbol table with bindings to `edges.kind` (`type(r)`), `edges.properties_json` (`properties(r)`), `edges.from_id` (`startNode(r)`), and `edges.to_id` (`endNode(r)`).
  - Support attribute extraction directly on relationship variables (`r.via`, `r.call_chain`).
  - Support edge variables across `WITH` clauses and aggregations.
- [ ] **3.2 Multi-Branch `OPTIONAL MATCH` Cartesian Product Elimination**:
  - Transpile independent `OPTIONAL MATCH` branches into discrete correlated subqueries or separate CTEs aggregated by root node ID, avoiding $O(N \cdot M \cdot K)$ intermediate row explosion.
- [ ] **3.3 Path & List Predicates in Transpiler**:
  - Transpile `WHERE EXISTS((n)-[:REL]->(m))` into SQL `EXISTS` subqueries.
  - Transpile list predicates (`all()`, `any()`, `none()`) over JSON arrays via `json_each()`.

---

### Epic 4: Concurrency, Incremental Watcher & Performance 🛡️

Ensure instant response times and lock-free execution during active development:
- [ ] **4.1 Incremental File Watcher (`ce watch`)**:
  - Implement file system watcher with a 500ms debounce buffer.
  - Trigger targeted `ParsingContext.IsSubtreeScan` on modified files, updating the SQLite graph in sub-second intervals.
- [ ] **4.2 Read-Only Connection Pooling for Tools**:
  - Enforce `Mode=ReadOnly;Cache=Shared;` for all analytical MCP tool connections to eliminate reader/writer lock contention.
- [ ] **4.3 In-Memory Projection Caching**:
  - Cache static macro-structures (`SystemContext`, `Taxonomy`) with automated invalidation hooks on `ce scan` / `ce clear` / `ce watch`.

---

### Epic 5: Test Automation & Benchmark Fixtures 🧪

Prevent performance regressions on enterprise-scale codebases:
- [ ] **5.1 Portable Synthetic 100k-Node CI Benchmark Fixture**:
  - Implement an in-memory synthetic graph generator seeding 10,000 files, 25,000 classes, 70,000 methods, and 300,000 edges.
  - Establish automated CI assertions:
    - `find_symbol`: < 25ms
    - `get_call_chain` (3 hops): < 100ms
    - `inspect_data_lineage`: < 50ms
    - Cypher multi-branch projections: < 150ms

---

## 3. Master Implementation Checklist

| ID | Epic | Description | Status |
| :--- | :--- | :--- | :--- |
| **1.1** | Agent/MCP | Implement `get_architecture_view` MCP tool backed by `ArchitectureViewEngine` | ⏳ Pending |
| **1.2** | Agent/MCP | Add `--type runtime\|build\|all` filtering to `get_project_dependencies` | ⏳ Pending |
| **1.3** | Agent/MCP | Implement `get_service_contracts` MCP tool (ingress / egress) | ⏳ Pending |
| **1.4** | Agent/MCP | Implement `trace_cross_service_flow` MCP tool | ⏳ Pending |
| **1.5** | CLI | Expose `ce view architecture`, `ce dependencies --type`, `ce contracts` in CLI | ⏳ Pending |
| **1.6** | Formats | Implement compact Markdown, TOON, and Mermaid serializers for architecture views | ⏳ Pending |
| **2.1** | Parsers | ASP.NET Core hierarchical route composition and Minimal API `MapGroup` | ⏳ Pending |
| **2.2** | Parsers | C# constructor DI mapping and interface call late binding in Layer 5 | ⏳ Pending |
| **2.3** | Parsers | Declarative HTTP clients (Refit/RestEase) and HttpClient URI resolution | ⏳ Pending |
| **2.4** | Parsers | EF Core `DbSet<T>` / `ToTable` and Dapper SQL lineage | ⏳ Pending |
| **3.1** | Cypher | Relationship functions (`type(r)`, `properties(r)`, `startNode`, `endNode`) | ⏳ Pending |
| **3.2** | Cypher | Multi-branch `OPTIONAL MATCH` Cartesian product elimination | ⏳ Pending |
| **3.3** | Cypher | Path predicates (`EXISTS((a)->(b))`) and list quantifiers (`any`, `all`, `none`) | ⏳ Pending |
| **4.1** | Concurrency | Incremental file watcher (`ce watch`) with debounced subtree updates | ⏳ Pending |
| **4.2** | Concurrency | Enforce `Mode=ReadOnly` connection pooling for MCP read tools | ⏳ Pending |
| **5.1** | Testing | Portable synthetic 100k-node graph benchmark fixture for CI/CD | ⏳ Pending |
