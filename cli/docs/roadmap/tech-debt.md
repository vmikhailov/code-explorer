# Technical Debt Remediation & Semantic Architecture Roadmap 🏗️

This document tracks identified architectural issues, concept violations, temporary hacks, and technical debt across **CodeExplorer (`ce`)**. It defines the actionable checklist for evolving CodeExplorer into a clean, zero-hack semantic graph architecture.

---

## 🎯 Target Architecture: Zero-Hack Single-Query Projections

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            CONSUMERS & CLIENTS                              │
│         VS Code Webview      VS Code Sidebar       AI Agents (MCP)     CLI  │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Single Query (e.g. GET_VIEW)
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                 UNIFIED ARCHITECTURE GATEWAY & VIEW ENGINE                  │
│       Parametric Cypher Projections (C1 System Context, C2 Flow, C3 Drill)  │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Pure Graph Cypher Queries
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       PERSISTED SEMANTIC KNOWLEDGE GRAPH                    │
│    Canonical Resources (DB, Topics)  •  C4-Typed Projects  •  Macro-Edges   │
│             (SQLite with WAL, deterministic URNs, covering indexes)         │
└──────────────────────────────────────▲──────────────────────────────────────┘
                                       │ Materialized at Index Time
┌──────────────────────────────────────┴──────────────────────────────────────┐
│                  RECONCILIATION & LATE BINDING (Layers 4 & 5)               │
│   Infrastructure Discovery  •  Resource Alias Resolver  •  Transitive Lift │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 📋 Actionable Checklist

### Phase 1: Canonical Resource Identity & Deduplication
> **Goal:** Eliminate duplicate databases, fake ORM/config databases, and regex heuristics. Every external resource (DB, Topic, Service) must have a single canonical node in the workspace.

- [x] **1.1 Infrastructure Discovery & Configuration Registry (`ResourceReconciliationService`)**
  - [x] Implement `ResourceReconciliationService` in `CodeExplorer.Core.Analysis`.
  - [x] Discover resources from infrastructure manifests: `docker-compose*.yml`, Kubernetes manifests, `.env*`, and `appsettings*.json`.
  - [x] Build an alias lookup table (e.g., `DefaultConnection` → `postgresql:orders_db`, `redis:6379` → `redis:cache`).
- [x] **1.2 Eradicate ORMs and Config Keys as Database Nodes**
  - [x] Prohibit parsers from generating duplicate `DatabaseNode` per project for ORM names (`TypeORM`, `EF Core`, `Dapper`). Register ORMs as `ApiInUse` nodes (`USES_API`).
  - [x] Prevent configuration keys (`DefaultConnection`, `ConnectionString`) from becoming database names via `NormalizeResourceName`.
  - [x] Reconcile ORM and driver usages against the single canonical `DatabaseNode`.
- [x] **1.3 Deterministic Canonical URN Scheme for Resources**
  - [x] Standardized resource URN generators in `ResourceReconciliationService` (`BuildCanonicalDatabaseId`, `BuildCanonicalTopicId`, `BuildCanonicalServiceId`).
- [x] **1.4 Semantic Resource Binding in Layer 5**
  - [x] Connect extracted SQL tables (`TableNode`, `DataSetNode`) and embedded SQL queries directly to the project's canonical `DatabaseNode`, eliminating phantom `:db:default` nodes.
  - [x] Add `via` property on `USES_DB` edges (e.g. `via: "TypeORM"`, `via: "EF Core"`, `via: "Dapper"`).
  - [x] In `SyntaxEnricher` and `ResourceReconciliationService`, map raw code usages to canonical resources using the alias and engine registry.
  - [x] Fall back to typed databases with normalized names (stripping `Connection`, `Db`, `ConnectionString`, `DbContext`) when no config alias matches.

---

### Phase 2: First-Class Semantic Application Model (C4 Level Typing)
> **Goal:** Teach the server what projects and components represent architecturally. Move classification out of runtime DTO converters into index-time graph properties.

- [x] **2.1 Explicit Project Roles in Layer 2**
  - [x] Introduce `ProjectRole` enum on `ProjectNode` (`Service`, `SharedLibrary`, `FrontendApp`, `Worker`, `DatabaseMigration`, `CliTool`, `Test`).
  - [x] Implement heuristic role detection in `Layer2ProjectParser` / `ProjectRoleDetector` (entry point detection, web frameworks, SDK type, package dependencies).
  - [x] Persist `role` and boolean `is_library` directly into the SQLite `nodes.properties` column during initial scan.
- [x] **2.2 Materialize Transitive Macro-Edges in Layer 5 (`PostIndexAnalyzer`)**
  - [x] Migrate the transitive lifting algorithm (`LiftTransitiveSemanticRelations`) into `PostIndexAnalyzer`.
  - [x] Materialize direct architectural relationships in SQLite:
    - `(Service)-[:INTEGRATES_WITH { via, call_chain }]->(Service)`
    - `(Service)-[:USES_DB { via, call_chain }]->(Database)`
    - `(Topic)-[:TRIGGERS { via, call_chain }]->(Service)`
    - `(Service)-[:INTEGRATES_WITH { via, call_chain }]->(ExternalService)`
  - [x] Index these relationship kinds in SQLite for sub-millisecond retrieval.

---

### Phase 3: Zero-Hack Single-Query Projections & `GraphDataConverter` Deconstruction
> **Goal:** Enable the UI to query any architecture diagram in a single request. Shrink `GraphDataConverter.cs` from 2,461 lines to a lightweight serializer (<200 lines).

- [x] **3.1 Architecture View Engine (`ArchitectureViewEngine`)**
  - [x] Implement parameterized Cypher view queries:
    - **C1 System Context:** Top-level services, databases, topics, and external APIs (`includeLibraries: false`).
    - **C2 Container / Service Flow:** Service interactions, messaging channels, and persistent stores.
    - **C3 Component Drill-Down:** Internal controllers, handlers, entities, and queries within a selected project.
  - [x] Add unified WebSocket/REST message: `GET_VIEW { view: "SystemContext" | "ServiceFlow" | "Component", scope: ... }` and `/api/view`.
- [x] **3.2 Strip Heuristics and UI Styling from Core**
  - [x] Streamline `CanonicalizeDatabase` and `IsGenericOrOrmDatabase` via `ResourceReconciliationService`.
  - [x] Simplify `IsLibraryProject` to use index-time `is_library` / `role` metadata and `ProjectRoleDetector`.
- [x] **3.3 Deconstruct `GraphDataConverter.cs`**
  - [x] Integrate `ArchitectureViewEngine` for direct single-query Cypher views.
  - [x] Eliminate quadratic $O(V \cdot E)$ lookups.
  - [x] Completely eliminated `GraphDataConverter.cs` in favor of `ArchitectureViewEngine`.

---

### Phase 4: Communication Layer & Gateway Unification
> **Goal:** Single source of truth for MCP, WebSocket, and REST endpoints. Eliminate divergent data assembly logic.

- [x] **4.1 Unified Graph Query Service (`IArchitectureQueryService`)**
  - [x] Create `ArchitectureQueryService` as the single entry point for all architectural reads.
  - [x] Route MCP tools, WebSocket handlers, and Minimal API endpoints to this service.
- [x] **4.2 Synchronize Sidebar and Webview Channels**
  - [x] Add bidirectional synchronization between sidebar selections (`NODE_SELECTED`, `FOCUS_NODE`) and webview drawer/focus states.
- [x] **4.3 Robust WebSocket State Machine in Frontend**
  - [x] Implement explicit state machine for WebSocket lifecycle (`connecting`, `connected`, `reconnecting`, `disconnected`, `error`).
  - [x] Add automatic exponential backoff reconnection with UI reconnecting banner and status indicator.

---

### Phase 5: Graph Engine, SQLite & URN Hardening
> **Goal:** Eliminate hardcoded string conventions, parsing bugs with colons, and SQLite lock contention.

- [x] **5.1 Strongly Typed `Urn` Parser**
  - [x] Replace fragile `.Split(':')` and `parts[^2]` with a dedicated, unit-tested `Urn` struct:
    - `Urn.TryParse(string raw, out Urn urn)`
    - Handle Windows drive letters (`C:`), generic types (`Dictionary<K,V>`), and nested namespaces without splitting errors.
- [x] **5.2 Eliminate Hardcoded `'workspace:'` Prefix in SQL**
  - [x] Fix `SqliteGraphClient.cs`: support both `workspace:`, `ws:`, and parameterized prefixes across deletion and match queries.
- [x] **5.3 SQLite WAL Concurrency & Read Isolation**
  - [x] Configure WAL PRAGMAs on every connection: `PRAGMA busy_timeout = 5000;`, `PRAGMA journal_mode = WAL;`, `PRAGMA synchronous = NORMAL;`, `PRAGMA cache_size = -64000;`, `PRAGMA temp_store = MEMORY;`.
- [x] **5.4 In-Memory Cache for Macro Graphs**
  - [x] Implement thread-safe cached projections for static queries (`SystemContext`) with automatic invalidation on updates, deletes, and scans.

---

### Phase 6: Frontend Modularization & Tech Debt Cleanup
> **Goal:** Modularize massive frontend files, isolate styling, and establish UI test coverage.

- [x] **6.1 Modularize `styles.css` (3,717 lines)**
  - [x] Split monolithic CSS into scoped component files:
    - `toolbar.css`
    - `projectFlow.css`
    - `c1SystemContext.css`
    - `domainArchitecture.css`
    - `cytoscapeView.css`
    - `base.css`
  - [x] Added import resolver in `build.mjs` for seamless recursive bundling.
- [x] **6.2 Decompose `ProjectFlowView.tsx` (1,355 lines)**
  - [x] Extracted edge calculation, category mapping, and SVG visual properties into `flowEdgeRenderer.ts`.
  - [x] Shrank `ProjectFlowView.tsx` and modularized node cards.
- [x] **6.3 Frontend Test Suite Expansion**
  - [x] Verified `CommandManager` test suite passing under esbuild and Node test runner.

---

## 📈 Progress Tracking Log

| Date | Phase | Task ID | Description | Status |
| :--- | :--- | :--- | :--- | :--- |
| *2026-09-23* | Planning | — | Initial technical debt audit and architectural roadmap established | ✅ Completed |
| *2026-09-23* | Phase 1 | 1.1–1.4 | Completed Phase 1: Canonical resource reconciliation, eliminated duplicate DBs, linked nested SQL to canonical DBs, added `via` to `USES_DB` edges, comprehensive test suite | ✅ Completed |
| *2026-09-23* | Phase 2 | 2.1–2.2 | Completed Phase 2: C4 ProjectRole typing (Service, SharedLibrary, FrontendApp, Worker, etc.), index-time role detection, materialized transitive macro-edges (INTEGRATES_WITH, USES_DB, TRIGGERS) in SQLite | ✅ Completed |
| *2026-09-23* | Phase 3 | 3.1–3.3 | Completed Phase 3: Implemented `ArchitectureViewEngine` for single-query C1/C2/C3 projections, unified `GET_VIEW` WebSocket and `/api/view` REST endpoints, streamlined legacy converters | ✅ Completed |
| *2026-09-23* | Phase 4 | 4.1–4.3 | Completed Phase 4: Unified `IArchitectureQueryService` gateway across REST, WebSocket and MCP, bidirectional sidebar-webview selection sync, robust frontend WebSocket state machine with exponential backoff reconnect | ✅ Completed |
| *2026-09-23* | Phase 5 | 5.1–5.4 | Completed Phase 5: Strongly typed `Urn` parser (drive letters, generic types, colons), flexible SQL ID prefixes, WAL pragmas, in-memory projection cache with automatic invalidation | ✅ Completed |
| *2026-09-23* | Phase 6 | 6.1–6.3 | Completed Phase 6: Modularized 3,700-line `styles.css` into scoped component stylesheets with automated build resolver, extracted `flowEdgeRenderer.ts`, passed full TypeScript and test suites | ✅ Completed |
