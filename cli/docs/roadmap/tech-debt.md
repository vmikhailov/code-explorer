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

- [ ] **2.1 Explicit Project Roles in Layer 2**
  - [ ] Introduce `ProjectRole` enum on `ProjectNode` (`Service`, `SharedLibrary`, `FrontendApp`, `Worker`, `DatabaseMigration`, `CliTool`, `Test`).
  - [ ] Implement heuristic role detection in `Layer2ProjectParser` (entry point detection, web frameworks, SDK type, package dependencies).
  - [ ] Persist `role` and boolean `is_library` directly into the SQLite `nodes.properties` column during initial scan.
- [ ] **2.2 Materialize Transitive Macro-Edges in Layer 5 (`PostIndexAnalyzer`)**
  - [ ] Migrate the transitive lifting algorithm (`LiftTransitiveSemanticRelations`) out of `GraphDataConverter.cs` into `PostIndexAnalyzer`.
  - [ ] Materialize direct architectural relationships in SQLite:
    - `(Service)-[:INTEGRATES_WITH { protocol, via }]->(Service)`
    - `(Service)-[:WRITES_TO | :READS_FROM { via }]->(Database)`
    - `(Service)-[:PUBLISHES | :SUBSCRIBES { via }]->(Topic)`
  - [ ] Index these relationship kinds in SQLite for sub-millisecond retrieval.

---

### Phase 3: Zero-Hack Single-Query Projections & `GraphDataConverter` Deconstruction
> **Goal:** Enable the UI to query any architecture diagram in a single request. Shrink `GraphDataConverter.cs` from 2,461 lines to a lightweight serializer (<200 lines).

- [ ] **3.1 Architecture View Engine (`ArchitectureViewEngine`)**
  - [ ] Implement parameterized Cypher view queries:
    - **C1 System Context:** Top-level services, databases, topics, and external APIs (`includeLibraries: false`).
    - **C2 Container / Service Flow:** Service interactions, messaging channels, and persistent stores.
    - **C3 Component Drill-Down:** Internal controllers, handlers, entities, and queries within a selected project.
  - [ ] Add unified WebSocket/REST message: `GET_VIEW { view: "SystemContext" | "ServiceFlow" | "Component", scope: ... }`.
- [ ] **3.2 Strip Heuristics and UI Styling from Core**
  - [ ] Delete `CanonicalizeDatabase` and `IsGenericOrOrmDatabase` from `GraphDataConverter.cs`.
  - [ ] Delete `IsLibraryProject` path/name regex matching from `GraphDataConverter.cs`.
  - [ ] Remove UI visual concerns (`layerColor`, `layerIcon`, `layerOrder`) from `Core`. Send semantic attributes; let the UI theme define styling.
- [ ] **3.3 Deconstruct `GraphDataConverter.cs`**
  - [ ] Break down the 2,461-line class into focused projection mappers:
    - `ArchitectureProjectionMapper.cs` (<200 lines)
    - `NeighborhoodProjectionMapper.cs` (<200 lines)
  - [ ] Eliminate quadratic $O(V \cdot E)$ lookups by utilizing indexed lookup dictionaries and graph queries.

---

### Phase 4: Communication Layer & Gateway Unification
> **Goal:** Single source of truth for MCP, WebSocket, and REST endpoints. Eliminate divergent data assembly logic.

- [ ] **4.1 Unified Graph Query Service (`IArchitectureQueryService`)**
  - [ ] Create `ArchitectureQueryService` as the single entry point for all architectural reads.
  - [ ] Route MCP tools, WebSocket handlers, and Minimal API endpoints to this service.
- [ ] **4.2 Synchronize Sidebar and Webview Channels**
  - [ ] Migrate `codeExplorerTreeProvider.ts` to utilize the existing WebSocket connection (or share a single communication client), deprecating parallel REST endpoints.
  - [ ] Ensure sidebar node selections and webview focus states stay bidirectionally synchronized.
- [ ] **4.3 Robust WebSocket State Machine in Frontend**
  - [ ] Implement an explicit state machine for WebSocket lifecycle (`Connecting`, `Connected`, `Reconnecting`, `Desynced`, `Error`).
  - [ ] Add automatic exponential backoff reconnection and UI reconnect banner.

---

### Phase 5: Graph Engine, SQLite & URN Hardening
> **Goal:** Eliminate hardcoded string conventions, parsing bugs with colons, and SQLite lock contention.

- [ ] **5.1 Strongly Typed `Urn` Parser**
  - [ ] Replace fragile `.Split(':')` and `parts[^2]` with a dedicated, unit-tested `Urn` struct:
    - `Urn.TryParse(string raw, out Urn urn)`
    - Handle Windows drive letters (`C:`), generic types (`Dictionary<K,V>`), and nested namespaces without splitting errors.
- [ ] **5.2 Eliminate Hardcoded `'workspace:'` Prefix in SQL**
  - [ ] Fix `SqliteGraphClient.cs` (lines 305–320): replace literal `'workspace:folder:'` and `'workspace:symbol:'` with parameterized `@wsPrefix`.
- [ ] **5.3 SQLite WAL Concurrency & Read Isolation**
  - [ ] Enforce `Mode=ReadOnly` on all tool/query connections.
  - [ ] Set PRAGMAs on every connection: `PRAGMA busy_timeout = 5000;`, `PRAGMA journal_mode = WAL;`, `PRAGMA synchronous = NORMAL;`, `PRAGMA cache_size = -64000;`.
- [ ] **5.4 In-Memory Cache for Macro Graphs**
  - [ ] Implement cached projections for static queries (`SystemContext`, `Taxonomy`) with automatic invalidation on `ce scan` / `ce clear`.

---

### Phase 6: Frontend Modularization & Tech Debt Cleanup
> **Goal:** Modularize massive frontend files, isolate styling, and establish UI test coverage.

- [ ] **6.1 Modularize `styles.css` (3,717 lines)**
  - [ ] Split monolithic CSS into scoped component files:
    - `toolbar.css`
    - `projectFlow.css`
    - `c1SystemContext.css`
    - `domainArchitecture.css`
    - `cytoscapeView.css`
- [ ] **6.2 Decompose `ProjectFlowView.tsx` (1,355 lines)**
  - [ ] Extract graph layout math and level grouping into a pure function module (`flowLayoutEngine.ts`).
  - [ ] Extract node cards (`ProjectCardNode`, `StoreCardNode`, `TopicCardNode`) into dedicated, memoized components.
  - [ ] Extract edge calculation and SVG bezier path generation into `flowEdgeRenderer.ts`.
- [ ] **6.3 Frontend Test Suite Expansion**
  - [ ] Add unit tests for WebSocket envelope serialization/deserialization.
  - [ ] Add unit tests for `CommandManager` view state transitions and layout computations.

---

## 📈 Progress Tracking Log

| Date | Phase | Task ID | Description | Status |
| :--- | :--- | :--- | :--- | :--- |
| *2026-09-23* | Planning | — | Initial technical debt audit and architectural roadmap established | ✅ Completed |
| *2026-09-23* | Phase 1 | 1.1–1.4 | Completed Phase 1: Canonical resource reconciliation, eliminated duplicate DBs, linked nested SQL to canonical DBs, added `via` to `USES_DB` edges, comprehensive test suite | ✅ Completed |
