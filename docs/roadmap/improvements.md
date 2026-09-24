# Architectural Evolution & Strategic Roadmap 🗺️

This document captures the architectural evolution of **CodeExplorer (`ce`)**, tracks implemented milestones, and outlines the active strategic roadmap.

For the actionable task checklist and backlog, see [**`todo.md`**](./todo.md).  
For the detailed specification on exposing semantic vs. project views to AI agents, see [**`agent-mcp-and-cli-architecture.md`**](./agent-mcp-and-cli-architecture.md).  
For the technical debt remediation log, see [**`tech-debt.md`**](./tech-debt.md).

---

## 1. Implemented Milestones & Delivered Solutions

### ✓ Milestone 1: Embedded SQLite & Cypher Compiler (Completed)
* **Problem**: Heavy dependency on external Memgraph Docker containers, high setup friction.
* **Delivered Solution**:
  * Migrated 100% to an embedded, in-process **SQLite** database with WAL mode and B-Tree indexes.
  * Implemented an in-house AST-based **Cypher-to-SQL Compiler** (`CodeExplorer.Cypher`) translating MATCH, OPTIONAL MATCH, WITH, WHERE, pattern comprehensions, and subqueries into high-performance SQLite Recursive CTEs.
  * Packaged into a self-contained, single-file cross-platform executable (`ce` / `ce.exe`, ~58 MB) for Windows, Linux, and macOS.

### ✓ Milestone 2: Targeted Subtree Ingestion & Clearing (Completed)
* **Problem**: Scanning even small changes required full workspace re-indexing.
* **Delivered Solution**:
  * Added targeted subtree scanning: `ce scan <folder>` (and `ce clear <folder>`).
  * `ParsingContext.IsSubtreeScan` preserves workspace-relative URN paths, generates intermediate `FolderNode` hierarchies up to the root, and isolates updates to the target folder.
  * Enforced unique indexes on `edges(from_id, to_id, kind)` with `ON CONFLICT DO UPDATE` for safe idempotent re-scans.

### ✓ Milestone 3: Query Traversal Bounding & Timeout Resilience (Completed)
* **Problem**: Unbounded Cypher paths (`[:CONTAINS*0..]`) caused exponential recursive CTE explosion (>10-15s timeouts) on large graphs (200k+ nodes).
* **Delivered Solution**:
  * Bounded all recursive Cypher patterns (`[:CONTAINS*0..3]`, `[:CONTAINS*1..5]`, `[:DEFINES|DECLARES*1..4]`).
  * Replaced full-table scan in `get_file_outline` with normalized index lookups.
  * Implemented 15-second `CommandTimeout` in `SqliteGraphClient` and end-to-end `CancellationToken` propagation across MCP handlers, throwing descriptive `TimeoutException` and returning actionable error messages instead of deadlocks.

### ✓ Milestone 4: Project-Specific Query Extensibility & Multi-Source MCP (Completed)
* **Delivered Solution**:
  * Dynamic query discovery and persistence in `<workspaceRoot>/.codeexplorer/queries/*.cypher` and `*.json`.
  * MCP tools `list_project_queries`, `save_project_query`, `execute_project_query`.
  * Multi-source fallback strategy for workspace detection (`WORKSPACE_ROOT`, CLI flags, current working directory) with graceful standby mode when booted in unbound folders.
  * Token-efficient multi-format serializers: compact Markdown tables, native Mermaid diagrams, compact JSON (`WriteIndented = false`), YAML, and TOON.

### ✓ Milestone 5: Zero-Hack Semantic Architecture & Materialized Macro-Edges (Completed)
* **Problem**: `GraphDataConverter.cs` (~2,400 lines) was an unmaintainable "god class" performing expensive ad-hoc heuristics and BFS lifting at render time. Duplicate databases were created per project.
* **Delivered Solution**:
  * **Eliminated `GraphDataConverter.cs` entirely** in favor of `ArchitectureViewEngine`.
  * **Abolished `ProjectSemanticNode`**: Promoted `Service`, `App`, and `Library` to first-class semantic entities directly in the ontology.
  * **Materialized Macro-Edges**: Shifted graph reconciliation from read time to index time in `PostIndexAnalyzer`. Macro-edges (`SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`) are now pre-computed and stored in SQLite.
  * **`ResourceReconciliationService`**: Discovers external resources and connection strings from `docker-compose`, `.env`, and `appsettings.json`, canonicalizing databases and message topics across projects.

### ✓ Milestone 6: Multi-Language AST Parsers & Framework Parity (Completed)
* **Delivered Solution**:
  * Comprehensive TypeScript/Node.js library parsers: NestJS (`@Module`, `@Controller`, `@Injectable`), Express, Fastify, Next.js App Router, BullMQ, KafkaJS, Elasticsearch, Sequelize, TypeORM, Prisma, Drizzle.
  * Hierarchical `.gitignore` resolution in monorepos and automatic pruning of minified bundles and build artifacts (`node_modules`, `dist`, `bin`, `obj`).

### ✓ Milestone 7: VS Code Interactive Cockpit (Completed)
* **Delivered Solution**:
  * Dedicated VS Code webview extension with Cytoscape.js and React-based C1/C2/C3 interactive projections (`DomainArchitectureView`, `ProjectFlowView`).
  * Real-time WebSocket bridge with auto-reconnection state machine and bidirectional selection synchronization.
  * Modularized CSS components and decoupled rendering pipeline (`flowEdgeRenderer.ts`).

---

## 2. Active Strategic Roadmap

```
┌────────────────────────────────────────────────────────────────────────┐
│                        PHASE 1: AGENT & MCP PARITY                     │
│  [ArchitectureViewEngine in MCP] ──> [Semantic vs Project Dependencies]│
│  [Service Contracts] ──> [Cross-Service Flow Tracing]                  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                   PHASE 2: DEEP C# INGRESS, EGRESS & DI                │
│  [ASP.NET Hierarchical Routes] ──> [C# Constructor DI] ──> [EF Core]  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                 PHASE 3: CYPHER ENGINE & TRANSPILER EXPANSION          │
│  [OpenCypher Edge Variables] ──> [Cartesian Decomposition] ──> [EXISTS]│
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│               PHASE 4: INCREMENTAL WATCHER & MULTI-LANGUAGE            │
│  [ce watch Subtree Sync] ──> [Go Gin/Chi/GORM] ──> [Python FastAPI]   │
└────────────────────────────────────────────────────────────────────────┘
```

### Phase 1: AI Agent & MCP Architecture Projections (Current Focus)
* Connect MCP directly to `ArchitectureViewEngine` via `get_architecture_view`.
* Differentiate between **Runtime / Semantic** dependencies (`SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`) and **Build / Project** dependencies (`PROJECT_REFERENCE`, `DEPENDS_ON`).
* Implement `get_service_contracts` and `trace_cross_service_flow`.
* Provide full parity in the CLI (`ce view architecture`, `ce dependencies --type`, `ce contracts`).
* *(Detailed spec: [agent-mcp-and-cli-architecture.md](./agent-mcp-and-cli-architecture.md))*

### Phase 2: Deep C# Ingress, Egress & Dependency Injection
* Hierarchical ASP.NET Core route composition (`[Route("api/[controller]")]` + `[HttpGet]`).
* Minimal API `MapGroup` chain extraction.
* Constructor and primary constructor DI mapping linked to concrete implementations via `[:IMPLEMENTS]`.
* Direct ORM data lineage extraction from EF Core `DbSet<T>` and Dapper SQL strings.

### Phase 3: OpenCypher Engine & Transpiler Expansion
* Support relationship variables (`r`) and functions: `type(r)`, `properties(r)`, `startNode(r)`, `endNode(r)` across `WITH` and aggregations.
* Multi-branch `OPTIONAL MATCH` decomposition into correlated subqueries/CTEs to eliminate Cartesian product explosion.
* Pattern predicates (`WHERE EXISTS((a)-[:REL]->(b))`) and list quantifiers (`any()`, `all()`, `none()`).

### Phase 4: Incremental File Watcher & Concurrency
* Real-time file watcher (`ce watch`) with 500ms debounce buffer and incremental subtree scanning.
* Read-only connection pooling (`Mode=ReadOnly;Cache=Shared;`) for MCP tools to eliminate SQLite reader/writer lock contention.
* In-memory projection caching for macro-structures.

### Phase 5: Multi-Language Parity Expansion
* Go: Gin, Chi, Fiber routing; `database/sql` and GORM query lineage.
* Python: FastAPI (`APIRouter`), Flask Blueprints, Django URLs, and SQLAlchemy models.
