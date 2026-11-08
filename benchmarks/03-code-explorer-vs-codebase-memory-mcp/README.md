# Case Study 03: Comparative Evaluation of `CodeExplorer` (`ce`) vs. `codebase-memory-mcp` (`CBM`)

> **Scope & Impartiality Disclosure**: This benchmark evaluates two open-source Model Context Protocol (MCP) codebase graph engines—**`CodeExplorer` (`ce` `v1.75.0`)** and **`codebase-memory-mcp` (`CBM` `v0.12.1`)**—across two open-source TypeScript/polyglot monorepos (`twentyhq/twenty` at `24,527` tracked files / `~2.49M` LOC and `immich-app/immich` at `3,205` tracked files / `~461k` LOC).
>
> Because the two tools target different abstraction depths—**`CBM`** is a fast, lightweight **Layer 1–3 structural AST & call-graph indexer**, whereas **`ce`** runs a **Layer 1–5 semantic and global architectural ontology pipeline** (including ORM tables, SQL/repository read/write lineage, message queue topology, and monorepo sub-project boundaries)—a single flat score across all categories would be methodologically unfair to `CBM`. We therefore separate the evaluation into three distinct parts:
> 1. **Part 1 — Head-to-Head on Common Ground (`C1`, `C2`, `C6`, `C7` + Cold Indexing Speed & Footprint):** Direct competition strictly on capabilities supported by both tools (`130` deterministically sampled ground-truth items).
> 2. **Part 2 — Extended L4/L5 Architectural Capabilities (`C3`, `C4`, `C5`, `C8`, `C9`, `C10`, `C11`):** Standalone evaluation of `ce` on architectural and data-lineage categories (`235` ground-truth items) that `CBM` does not model by design.
> 3. **Part 3 — MCP Server Tool Surface, Warm Stdio Latency & Agent Ergonomics:** Comparison of the MCP tools exposed to AI agents (`36` in `ce` vs. `17` in `CBM`), warm JSON-RPC round-trip latency, and response payload ergonomics.
>
> **Transparency Note on Framework Extractors:** During the preparation of this benchmark, several TypeScript framework extractors in `ce` (covering NestJS GraphQL `@Resolver`, BullMQ `@Process` decorators, TypeORM `@Entity` / `@Column`, Kysely `Updateable<T>`, and single-pass topological Layer 3 enrichment) were added or refined (`v1.72.0` → `v1.75.0`). `CBM` `v0.12.1` was evaluated out-of-the-box using its official `win32-x64` release binary.

---

## 1. Experimental Setup & Methodology

### 1.1 Tools Under Test
- **`ce` (`codeexplorer.cli`)**: `v1.75.0` (.NET 10, 5-layer AST & architectural ontology pipeline, SQLite graph backend, `36` MCP tools).
- **`CBM` (`codebase-memory-mcp`)**: `v0.12.1` (`win32-x64` native Go/CGo binary, Tree-sitter + SQLite graph backend, `17` MCP tools).

### 1.2 Target Repositories (Pinned Commits)
| Repository | Pinned Commit SHA | Scale | Primary Stack & Architecture |
| :--- | :--- | :--- | :--- |
| **`twenty`** (`twentyhq/twenty`) | `b9ef71072c06b5fa1853dd7120c7f3b58da0bc50` | `24,527` files (`~2.49M` LOC) | Enterprise TypeScript monorepo (`46` packages): NestJS, GraphQL (`@Resolver`), REST (`@Controller`), TypeORM (`WorkspaceScopedRepository`), BullMQ, React |
| **`immich`** (`immich-app/immich`) | `152accc692fdbe7ed0c1d18a49c007ba0769386a` | `3,205` files (`~461k` LOC) | Polyglot monorepo (`21` sub-projects): TypeScript/NestJS, Kysely ORM, BullMQ (`JobOf<QueueName>`), Python/FastAPI ML service, Dart/Flutter mobile app, Svelte web UI, Rust |

### 1.3 Ground Truth & Scoring Rules
- **100% Automated Ground Truth:** All ground-truth files ([`groundtruth/*.csv`](./groundtruth/)) are generated deterministically from repository source files by `build_groundtruth.py` using fixed random seed `42` (`random.Random(42)`) and zero manual CSV curation.
- **Integer `0–100` Recall Scale:** All Recall scores in [`results/scores.csv`](./results/scores.csv) and this report are integers on a `0–100` scale (`round(100 * Matched / GT)`), reported alongside exact `Matched / GT` ratios and `Total Extracted` counts.
- **Why Precision Is Not Reported as a Percentage:** Ground-truth files (`12–20` items per category) are **stratified random samples** of the codebase, whereas both tools extract the **entire population** of endpoints, calls, and inheritance edges across the repository (often hundreds or thousands of rows). Dividing sample matches by full-repo extraction count is mathematically invalid; instead, we report `Total Extracted` alongside sample `Recall (0–100)`.

---

## 2. Executive Summary

| Benchmark Dimension | **`ce` (`v1.75.0`)** | **`CBM` (`v0.12.1`)** | Objective Assessment |
| :--- | :---: | :---: | :--- |
| **Cold Indexing Speed (`twenty` + `immich`)** | `164.9 s` (2.75 min) | **`117.5 s` (1.96 min)** | **`ce` is faster on `immich` (`17.6 s` vs. `20.6 s`)**; **`CBM` is 1.52× faster** on `twenty` (`96.9 s` vs. `147.3 s`) |
| **Peak RAM / DB Size (`twenty`)** | `4,661 MB` / **`784 MB`** | **`2,076 MB`** / `843 MB` | **`ce` produces a smaller SQLite DB on both repos** (**`784 MB` vs. `843 MB` on `twenty`**, **`55.1 MB` vs. `282.0 MB` on `immich` — 5.1× smaller**); `CBM` uses less RAM |
| **Part 1: Head-to-Head Matched (`C1, C2, C6, C7`)** | **102 / 130** | 49 / 130 | Direct competition on Endpoints, Outbound HTTP, Call Graph, and Inheritance |
| **Part 1: Head-to-Head Micro-Recall (`0–100`)** | **78** | 38 | **`immich`: `73` vs. `50`** (`48/66` vs. `33/66`); **`twenty`: `84` vs. `25`** (`54/64` vs. `16/64`) |
| **Part 1: Head-to-Head Macro-Recall (`0–100`)** | **79** | 36 | Unweighted average across the 8 head-to-head evaluations |
| **Part 1: Category Wins / Ties / Losses (of 8)** | **7 W / 1 T / 0 L** | 0 W / 1 T / 7 L | Ties on `immich C7` (`15` vs. `15`); `CBM` scores `90` on `immich C1` and `75` on `twenty C6` |
| **Part 2: `ce` L4/L5 Ontology Recall (`C3–C5, C8–C11`)** | **97** (`227 / 235`) | *Not modeled (L1–L3 only)* | `ce` achieves **97** micro-recall (**98** macro-recall) across queues, ORM tables, data lineage & services |
| **Part 3: MCP Tool Surface & Warm Query Latency** | **36 tools** (`1.9–530 ms`) | **17 tools** (`6.5–287 ms`) | `ce` is faster on `get_taxonomy` (`22 ms` / `1.9 ms` cached), Cypher (`4.3 ms`), `immich` outline (`13.4 ms`) & 1-turn callers; `CBM` is faster on large-repo outline & symbol search |

---

## 3. Part 1: Head-to-Head Comparison on Common Ground (`L1–L3`)

### 3.1 Cold Indexing Speed, Resource Footprint & Parse Health

Both tools were executed from a cold state (`Remove-Item .codeexplorer/graph.db*` and a fresh `CBM_CACHE_DIR`) on the same Windows workstation:

| Metric | `twenty` — **`ce`** | `twenty` — **`CBM`** | `immich` — **`ce`** | `immich` — **`CBM`** |
| :--- | :---: | :---: | :---: | :---: |
| **Repository Scale** | 24,527 tracked (`36,644` physical files) | 24,527 files (~2.49M LOC) | 3,205 tracked (`1,485` code files) | 3,205 files (~461k LOC) |
| **Cold Index Wall Time** | `147.33 s` (2.45 min) | **`96.9 s` (1.6 min)** | **`17.60 s`** | `20.6 s` |
| **Peak Working Set (RAM)** | `4,661 MB` | **`2,076 MB`** | `781 MB` | **`595 MB`** |
| **SQLite DB Size on Disk** | **`784.0 MB`** | `842.5 MB` | **`55.1 MB`** | `282.0 MB` |
| **Graph Nodes Extracted** | `456,711` | `251,750` | `54,978` | `164,125` (incl. 141k `Variable` nodes) |
| **Graph Edges Extracted** | `960,660` | `914,479` | `117,070` | `246,561` |
| **Parse / Analysis Warnings** | **0** (`0` `ScanIssue` nodes) | 209 `parse_partial`, 1 `unusable` | **0** | 4 `parse_partial` |

**Analysis of Indexing Performance (`ce` Phase Breakdown):**
- On medium-sized repositories (`immich`), **`ce` indexes faster than `CBM` (`17.60 s` vs. `20.6 s`)** and produces a **5.1× smaller database** (`55.1 MB` vs. `282.0 MB`, partly because `CBM` indexes 141,286 local `Variable` nodes).
- On large monorepos (`twenty`), **`CBM` is 1.52× faster (`96.9 s` vs. `147.33 s`)** and uses less RAM, while **`ce` produces a smaller SQLite database (`784.0 MB` vs. `842.5 MB`) despite storing 1.81× more nodes (`456,711` vs. `251,750`) and 46k more edges (`960,660` vs. `914,479`)**. Breaking down `ce`'s `147.33 s` cold run on `twenty` shows where the time is spent:
  - **Layer 1 & 2 (File tree & 46 project boundaries across 36,644 files):** `14 s`
  - **Layer 3 (AST parsing & framework visitors across 46 packages):** `51 s`
  - **Layer 4 (Endpoints, queues, and ORM table extraction):** `4 s`
  - **Layer 5 (Cross-project symbol resolution & service call linking):** `19 s`
  - **SQLite write, indexing, and post-analysis:** `58 s`

---

### 3.2 Semantic Recall on Common Ground (`C1`, `C2`, `C6`, `C7`)

These four categories test core code-graph capabilities supported by both tools:
- **`C1` (Incoming HTTP / GraphQL Endpoints):** `(:Endpoint)-[:EXPOSED_BY]->(:Function)` in `ce` vs. `(:Method)-[:HANDLES]->(:Route)` in `CBM`.
- **`C2` (Outbound HTTP Calls):** `(:ExternalService)` and `:CALLS_ENDPOINT` in `ce` vs. `(:Function)-[:HTTP_CALLS]->(:Route)` in `CBM`.
- **`C6` (Call Graph — Intra-Class + Cross-Class DI Service Calls):** Automatically sampled by [`scripts/build_groundtruth.py`](./scripts/build_groundtruth.py) from both `public` service methods (called across files via NestJS constructor DI `this.<service>.<method>()`) and `private` helper methods (called intra-class via `this.<method>()`).
- **`C7` (Class Inheritance & Interface Implementation):** `(:Type)-[:INHERITS_FROM|IMPLEMENTS]->(:Type)` in `ce` vs. `(:Class)-[:INHERITS|IMPLEMENTS]->(:Class|Interface)` in `CBM`.

| Category | Description | Repo | GT | **`ce` Matched** | **`ce` Recall (`0–100`)** | **`ce` Total Extracted** | **`CBM` Matched** | **`CBM` Recall (`0–100`)** | **`CBM` Total Extracted** | Winner |
| :--- | :--- | :--- | :-: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **C1** | Incoming HTTP / GraphQL Endpoints | `twenty` | 20 | **20 / 20** | **100** | 895 | 0 / 20 | 0 | 4 | **`ce`** |
| **C1** | Incoming HTTP / FastAPI Endpoints | `immich` | 20 | **20 / 20** | **100** | 321 | 18 / 20 | 90 | 271 | **`ce`** (close) |
| **C2** | Outbound HTTP Calls (`fetch` / clients) | `twenty` | 12 | **6 / 12** | **50** | 164 | 3 / 12 | 25 | 196 | **`ce`** |
| **C2** | Outbound HTTP Calls (`fetch` / clients) | `immich` | 6 | **5 / 6** | **83** | 34 | 0 / 6 | 0 | 48 | **`ce`** |
| **C6** | Call Graph (Intra-Class + DI Service Calls) | `twenty` | 12 | **12 / 12** | **100** | 18 | 9 / 12 | 75 | 10 | **`ce`** |
| **C6** | Call Graph (Intra-Class + DI Service Calls) | `immich` | 20 | **20 / 20** | **100** | 119 | 12 / 20 | 60 | 97 | **`ce`** |
| **C7** | Class Inheritance & `implements` | `twenty` | 20 | **16 / 20** | **80** | 1,553 | 4 / 20 | 20 | 250 | **`ce`** |
| **C7** | Class Inheritance & `implements` | `immich` | 20 | **3 / 20** | **15** | 140 | **3 / 20** | **15** | 564 | **Tie** |

#### Part 1 Aggregate Score Summary (`0–100` Scale)

| Subset | **`ce` Matched / GT** | **`ce` Micro-Recall** | **`ce` Macro-Recall** | **`CBM` Matched / GT** | **`CBM` Micro-Recall** | **`CBM` Macro-Recall** |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **`twenty` (Common Ground)** | **54 / 64** | **84** | **83** | 16 / 64 | 25 | 30 |
| **`immich` (Common Ground)** | **48 / 66** | **73** | **75** | 33 / 66 | 50 | 41 |
| **Part 1 Combined Total** | **102 / 130** | **78** | **79** | **49 / 130** | **38** | **36** |

#### Detailed Analysis of Part 1 Differences
1. **Call Graph (`C6` — `100` vs. `75` on `twenty`, `100` vs. `60` on `immich`):**
   - Both tools accurately resolve **intra-class helper calls** (`this.helperMethod()`), where `CBM` matches **9/9** intra-class items on `twenty` (Recall `75` overall) and **12/12** intra-class items on `immich` (Recall `60` overall). Note that `ce` models nested object-literal arrow functions (`hooks.configureImmichDatabaseDump`, `handlers.[SyncRequestType.AssetsV1]`, `started.startStream`) as distinct child `:Function` nodes linked to their enclosing method via `[:DECLARES]`, matching **100%** (`12/12` on `twenty` and `20/20` on `immich`) when traversing `OPTIONAL MATCH (enclosing:Function)-[:DECLARES]->(caller)`.
   - The gap on the remaining items comes from **NestJS Dependency Injection calls** (`this.publicDomainService.findByDomain()`, `this.service.updateAll()`). In TypeScript, NestJS services declare injected dependencies as constructor parameter properties (`constructor(private readonly service: SomeService)`). `ce` resolves constructor parameter property types to their target classes across files, whereas `CBM` does not resolve method calls on constructor-injected fields.
2. **Incoming Endpoints (`C1` — `100` vs. `90` on `immich`, `100` vs. `0` on `twenty`):**
   - On `immich` (standard `@Controller('albums')` + Python FastAPI `@app.get('/ping')`), `CBM` achieves **90** (`18/20`), missing only 2 controllers whose route prefixes use TypeScript enum constants (`@Controller(RouteKey.Asset)`).
   - On `twenty`, REST controllers use template-literal route prefixes (`` @Controller(`${ApiPath.Rest}/metadata/fields`) ``) and the primary API surface uses NestJS GraphQL (`@Resolver`, `@Query`, `@Mutation`). `CBM` misclassifies template-literal `@Controller` decorators as outbound `HTTP_CALLS` rather than `HANDLES -> Route` and does not index GraphQL `@Resolver` operations (`0/20`).
3. **Outbound HTTP (`C2` — `50` vs. `25` on `twenty`, `83` vs. `0` on `immich`):**
   - `ce` scores **50** (`6/12`) on `twenty` and **83** (`5/6`) on `immich` via its `:ExternalService` and `:CALLS_ENDPOINT` extractors, while `CBM` matches **25** (`3/12`) on `twenty` by extracting `HTTP_CALLS` edges from functions invoking `fetch` with relative path templates (`/-/v1/search?text=...`, `/{}/latest`).
4. **Class Inheritance & Interfaces (`C7` — Tie `15` vs. `15` on `immich`, `ce` leads `80` vs. `20` on `twenty`):**
   - On `immich`, **both tools tie at `15` (`3/20`)** because 17 of the 20 sampled classes use functional mixin base classes (`export class AlbumResponseDto extends createZodDto(AlbumResponseSchema) {}`), which neither tool's AST visitor resolves as a base class.
   - On `twenty`, `ce` scores **80** (`16/20`) vs. `CBM`'s **20** (`4/20`) because `ce` extracts TypeScript `implements` relationships (`implements MigrationInterface`, `implements FastInstanceCommand`) into `:IMPLEMENTS` edges and resolves cross-package base classes (`twenty-shared` $\to$ `twenty-server`) via topological project ordering.

---

## 4. Part 2: Extended L4/L5 Architectural Capabilities (`ce` Standalone)

In addition to Layer 1–3 AST and call graphs, `ce` executes Layer 4 (Semantic) and Layer 5 (Global) passes to construct an architectural ontology covering asynchronous queues, ORM database tables, code-to-table read/write lineage, and monorepo sub-project boundaries. Because `CBM` does not model these entities, Part 2 evaluates `ce` standalone against the `235` ground-truth items across `twenty` and `immich`:

| Category | Capability Tested | Repo | GT | **`ce` Matched / GT** | **`ce` Recall (`0–100`)** | **`ce` Total Extracted** |
| :--- | :--- | :--- | :-: | :---: | :---: | :--- |
| **C3-producers** | Queue / Job Producers (`PUBLISHED_BY`) | `twenty` | 19 | **19 / 19** | **100** | 165 `PUBLISHED_BY` edges |
| **C3-producers** | Queue / Job Producers (`PUBLISHED_BY`) | `immich` | 20 | **19 / 20** | **95** | 101 `PUBLISHED_BY` edges |
| **C3-consumers** | Queue / Job Consumers (`SUBSCRIBED_BY`) | `twenty` | 19 | **19 / 19** | **100** | 192 `SUBSCRIBED_BY` links |
| **C3-consumers** | Queue / Job Consumers (`SUBSCRIBED_BY`) | `immich` | 17 | **17 / 17** | **100** | 135 `SUBSCRIBED_BY` links |
| **C8-topics** | Symbolic / Enum Queue Topic Resolution | `twenty` | 20 | **20 / 20** | **100** | 357 producer + consumer links |
| **C8-topics** | Symbolic / Enum Queue Topic Resolution | `immich` | 19 | **19 / 19** | **100** | 236 producer + consumer links |
| **C4** | Database Tables (`TypeORM` / `Kysely`) | `twenty` | 17 | **17 / 17** | **100** | 164 `:Table` nodes |
| **C4** | Database Tables (`TypeORM` / `Kysely`) | `immich` | 18 | **18 / 18** | **100** | 100 `:Table` nodes |
| **C5** | Code-to-Table Read/Write Lineage | `twenty` | 18 | **18 / 18** | **100** | 17,382 `READS_FROM` / `WRITES_TO` edges |
| **C5** | Code-to-Table Read/Write Lineage | `immich` | 10 | **10 / 10** | **100** | 1,206 `READS_FROM` / `WRITES_TO` edges |
| **C10-schema** | Table Columns & Entity Members (`:Member`) | `twenty` | 17 | **13 / 17** | **76** | 1,481 `Table -> Entity -> Member` links |
| **C10-schema** | Table Columns & Entity Members (`:Member`) | `immich` | 20 | **19 / 20** | **95** | 608 `Table -> Entity -> Member` links |
| **C9-services** | Monorepo Sub-Projects (`:Project`) | `twenty` | 8 | **8 / 8** | **100** | 46 `:Project` nodes |
| **C9-services** | Monorepo Sub-Projects (`:Project`) | `immich` | 7 | **7 / 7** | **100** | 21 `:Project` nodes |
| **C11-queries** | 6 End-to-End Architectural Cypher Queries | `twenty` | 6 | **6 / 6** | **100** | 76 rows across 6 queries |
| **C11-queries** | 6 End-to-End Architectural Cypher Queries | `immich` | 6 | **6 / 6** | **100** | 82 rows across 6 queries |
| **Part 2 Total** | **All L4/L5 Architectural Categories** | **Both** | **235** | **227 / 235** | **97** (Micro) / **98** (Macro) | **Full L4/L5 Ontology** |

**Notes on Part 2 Limitations (`C10-schema` on `twenty`, Recall `76`):**
- On `twenty`, `ce` matched `13/17` sampled table columns because several TypeORM entities define composite relation columns without explicit `@Column` decorators on the child class.

---

## 5. Part 3: MCP Server Tool Surface, Latency & Agent Ergonomics

To evaluate how AI coding agents interact with both tools over the Model Context Protocol, `bench_mcp.py` spawned both MCP servers over persistent JSON-RPC stdio sessions (`ce mcp` vs. `codebase-memory-mcp.exe`), inspected their `tools/list` schemas, and measured warm round-trip latency and response payload sizes on both repositories (raw telemetry saved in [`results/mcp-benchmark.json`](./results/mcp-benchmark.json)).

### 5.1 Functional Comparison of MCP Tool Surfaces (`36` Tools in `ce` vs. `17` Tools in `CBM`)

Comparing only the total tool count (`36` vs. `17`) does not tell the whole story: `CBM` packs many filtering parameters into a smaller set of multipurpose tools (such as `search_graph` and `detect_changes`), whereas `ce` exposes specialized, intent-driven tools across Layer 1–5 architecture, data lineage, test impact, and governance gates.

#### A. Category-by-Category Capability Matrix

| # | Functional Category | **`ce` (`36` tools)** | **`CBM` (`17` tools)** | Richer Surface & Why |
| :-: | :--- | :--- | :--- | :--- |
| **1** | **Workspace & Index Lifecycle** | **5 tools:** `init_workspace`, `scan_workspace`, `get_workspace_status`, `clear_workspace_index`, `ingest_graph_data` | **6 tools:** `index_repository`, `index_status`, `list_projects`, `delete_project`, `check_index_coverage`, `detect_changes` | **Parity (different focus):** `CBM` manages multi-project global caches & path coverage checks; `ce` supports incremental subfolder scanning & custom JSON node/edge ingestion (`ingest_graph_data`). |
| **2** | **Architecture & C4 / DDD Topology** | **5 tools:** `get_architecture_map`, `get_architecture_overview`, `get_architecture_view` (`c1`/`c2`/`c3`/`project-map`/`domain`/`tiers`), `get_bounded_contexts`, `export_architecture_diagram` (`mermaid`/`c4`) | **1 tool:** `get_architecture` (`aspects`, `include_behavior_evidence`) | **`ce` (much richer):** `ce` generates multi-level C4 views (`c1`–`c3`), internal module cycle maps (`project-map`), DDD bounded contexts, and Mermaid/C4 diagrams (`architecture`, `domain`, `lineage`, `cqrs`). |
| **3** | **Microservice Contracts & Distributed Flows** | **6 tools:** `get_project_dependencies` (`runtime`/`build`), `get_service_contracts` (`ingress`/`egress`), `get_project_entry_points`, `trace_cross_service_flow`, `manage_domain`, `assign_service_domain` | *None (0 dedicated tools)* | **`ce` (exclusive L4/L5 domain):** `ce` explicitly models inter-project dependencies, service API/queue contracts, cross-service multi-hop flows, and domain boundary overrides. |
| **4** | **Symbol & Text Search** | **2 tools:** `find_symbol` (`Class`/`Interface`/`Function`/`Variable`), `search_identifiers` (hybrid vector + FTS5 trigram across symbols, routes, tables, topics) | **2 tools:** `search_graph` (BM25 + regex + `min_degree`/`max_degree` + `semantic_query`), `search_code` (graph-ranked regex/text grep) | **`CBM` for structural filters & text grep; `ce` for L4/L5 entity search:** `CBM`'s `search_graph` supports node degree filtering (`min_degree`/`max_degree`) and built-in regex `search_code`; `ce`'s `search_identifiers` searches routes, DB tables, and queues via trigram + vector ranking. |
| **5** | **Call Graph, DI Resolution & Blast Radius** | **4 tools:** `get_callers` (functions, services, tables, topics), `get_call_chain`, `analyze_code_impact`, `resolve_call_target` (DI interface $\to$ concrete method) | **2 tools:** `trace_path` (`inbound`/`outbound`/`both`, `mode=calls\|dataflow\|cross-service`), `detect_changes` (git diff $\to$ graph impact) | **`ce` for DI & polymorphism; `CBM` for Git diff paging:** `ce`'s `get_callers` works across functions, DB tables, queues, and services in 1 call and resolves DI interfaces (`resolve_call_target`); `CBM` has cursor-paged `detect_changes`. |
| **6** | **File Outline & Source Snippets** | **2 tools:** `get_file_outline`, `fetch_code_snippets` (batch JSON node slice reader) | **2 tools:** `get_file_outline` (with label filtering & paging), `get_code_snippet` (by qualified name + `include_neighbors`) | **Parity:** `ce` fetches snippets for batches of graph nodes in one call; `CBM` fetches by symbol QN with neighbor context. |
| **7** | **Database & ORM Data Lineage** | **1 tool:** `inspect_data_lineage` (traces SQL/ORM queries, reader/writer functions, and source files per table) | *None (0 tools)* | **`ce` (exclusive):** First-class `READS_FROM` / `WRITES_TO` / `PERSISTED_IN` lineage across TypeORM, Kysely, EF Core, Dapper, SQLAlchemy, GORM, Prisma. |
| **8** | **Schema & Custom Cypher Queries** | **6 tools:** `get_taxonomy`, `get_node_definition`, `execute_custom_read_cypher`, `list_project_queries`, `save_project_query`, `execute_project_query` | **2 tools:** `get_graph_schema`, `query_graph` (with `cursor`/`offset` pagination) | **`ce` for reusable query library; `CBM` for cursor pagination:** `ce` lets agents save and parameterize reusable project queries in `.codeexplorer/queries/`; `CBM` adds built-in row cursor paging on `query_graph`. |
| **9** | **Test Impact Analysis & Code Health** | **3 tools:** `get_affected_tests` (by file, symbol, or `gitDiff`), `get_test_coverage` (`covered`/`uncovered`), `find_refactoring_opportunities` (`dead_code`, `god_objects`) | *None (0 dedicated tools; `detect_changes` maps git diff to general symbols)* | **`ce` (exclusive):** Static test reachability coverage (`covered`/`uncovered`), Test Impact Analysis (`get_affected_tests`), and automated God-class / dead-code detection. |
| **10** | **Architectural Governance, Snapshots & Runtime Traces** | **2 tools:** `check_semantic_gates`, `list_semantic_gates` (32+ built-in & custom OpenCypher gates with baseline ratchets) | **3 tools:** `compare_graphs` (snapshot diffing), `manage_adr` (ADR CRUD), `ingest_traces` (runtime call trace overlay) | **Complementary strengths:** `ce` enforces architectural invariants via Semantic Invariant Gates (`SIG`); `CBM` diffs two graph snapshots (`compare_graphs`), manages ADRs, and overlays runtime traces (`ingest_traces`). |

#### B. Summary: Whose Method Set Is Richer?
- **Breadth of Architectural & Engineering Workflows (`ce` leads: `36` vs. `17` tools, `18` exclusive capabilities):**
  - `ce` covers **5 architectural layers (`L1–L5`)**, giving agents dedicated methods that have no equivalent in `CBM`: **database table lineage** (`inspect_data_lineage`), **service ingress/egress contracts** (`get_service_contracts`), **cross-service flow tracing** (`trace_cross_service_flow`), **C1/C2/C3/DDD/Mermaid views** (`get_architecture_view`, `get_bounded_contexts`, `export_architecture_diagram`), **DI interface resolution** (`resolve_call_target`), **Test Impact Analysis & static test coverage** (`get_affected_tests`, `get_test_coverage`), **God-object/dead-code detection** (`find_refactoring_opportunities`), **saved parameterized project queries** (`save_project_query` / `execute_project_query`), and **Semantic Invariant Gates** (`check_semantic_gates`).
  - Moreover, `ce` tools support compact token-saving output formats (`markdown`, `toon`, `json`, `mermaid`, `yaml`).
- **Parameter Depth on Core Graph Primitives & Unique Utilities (`CBM` strengths):**
  - Although `CBM` has fewer tools (`17`), each tool has rich pagination and filtering parameters (`max_output_tokens`, `cursor`, `offset`, `min_degree`/`max_degree` on `search_graph`).
  - `CBM` also has **4 capabilities that `ce` does not have**: graph snapshot diffing (`compare_graphs`), runtime trace ingestion (`ingest_traces`), ADR document management (`manage_adr`), and a built-in regex text search (`search_code`).

---

### 5.2 Warm MCP Stdio Latency & Payload Comparison

| MCP Operation | Repo | **`ce` Tool** | **`ce` Latency** | **`ce` Payload** | **`CBM` Tool** | **`CBM` Latency** | **`CBM` Payload** | Ergonomics & Output Notes |
| :--- | :--- | :--- | :---: | :---: | :--- | :---: | :---: | :--- |
| **Architecture Overview** | `immich` | `get_architecture_map` | `268 ms` | `4.4 KB` | `get_architecture` | **`118 ms`** | `3.4 KB` | `ce` lists 21 sub-projects & DBs; `CBM` summarizes languages & top packages |
| **Architecture Overview** | `twenty` | `get_architecture_map` | `372 ms` | `11.0 KB` | `get_architecture` | **`287 ms`** | `6.1 KB` | `ce` lists 46 sub-projects; `CBM` summarizes root project structure |
| **Graph Schema (1st call)** | `immich` | `get_taxonomy` | **`22.3 ms`** | `51.5 KB` | `get_graph_schema` | `35.6 ms` | `1.0 KB` | **`ce` is 1.6× faster (`22.3 ms` vs. `35.6 ms`)** via incremental in-memory/metadata taxonomy |
| **Graph Schema (1st call)** | `twenty` | `get_taxonomy` | **`23.2 ms`** | `54.7 KB` | `get_graph_schema` | `72.0 ms` | `1.0 KB` | **`ce` is 3.1× faster (`23.2 ms` vs. `72.0 ms`)** and returns full property/edge schemas |
| **Graph Schema (Cached)** | `immich` | `get_taxonomy` | **`1.9 ms`** | `51.5 KB` | `get_graph_schema` | `40.9 ms` | `1.0 KB` | **`ce` is 21× faster (`1.9 ms`)** on subsequent calls in the same MCP session |
| **Graph Schema (Cached)** | `twenty` | `get_taxonomy` | **`2.7 ms`** | `54.7 KB` | `get_graph_schema` | `75.3 ms` | `1.0 KB` | **`ce` is 28× faster (`2.7 ms`)** on subsequent calls in the same MCP session |
| **Symbol Search (`AuthService`)** | `immich` | `find_symbol` | `80.8 ms` | `0.4 KB` | `search_graph` | **`55.6 ms`** | `1.1 KB` | Near parity (~56–81 ms); `ce` also offers fuzzy `search_identifiers` |
| **Symbol Search (`AuthService`)** | `twenty` | `find_symbol` | `367 ms` | `1.1 KB` | `search_graph` | **`97.5 ms`** | `0.8 KB` | **`CBM` is 3.8× faster** on 24.5k-file symbol lookup |
| **Inbound Callers (`validate`)** | `immich` | `get_callers` | `76.5 ms` | **`1.5 KB`** | `trace_path` | `7.5 ms` | `18.6 KB` (`ambiguous`) | **`ce` resolves callers in 1 call**; `CBM` returns `ambiguous` (62 candidates) requiring a 2nd call |
| **Inbound Callers (`validate`)** | `twenty` | `get_callers` | `530 ms` | **`2.5 KB`** | `trace_path` | `6.5 ms` | `45.6 KB` (`ambiguous`) | **`ce` resolves 12 callers in 1 call**; `CBM` dumps 45.6 KB of 67 candidate QNs without callers |
| **File Outline (`auth.*`)** | `immich` | `get_file_outline` | **`13.4 ms`** | `0.4 KB` | `get_file_outline` | `17.3 ms` | `4.8 KB` | **`ce` is faster (`13.4 ms` vs. `17.3 ms`)** and returns a compact class/method outline |
| **File Outline (`auth.*`)** | `twenty` | `get_file_outline` | `92.7 ms` | `0.9 KB` | `get_file_outline` | **`40.5 ms`** | `7.8 KB` | **`CBM` is 2.3× faster**; `ce` returns a more compact class/method outline |
| **Cypher Query (`LIMIT 10`)** | `immich` | `execute_custom_read_cypher` | **`4.3 ms`** | `1.0 KB` | `query_graph` | `38.1 ms` | `0.3 KB` | **`ce` is 8.9× faster** (`4.3 ms` vs. `38.1 ms`) |
| **Cypher Query (`LIMIT 10`)** | `twenty` | `execute_custom_read_cypher` | **`4.7 ms`** | `1.1 KB` | `query_graph` | `71.2 ms` | `0.3 KB` | **`ce` is 15× faster** (`4.7 ms` vs. `71.2 ms`) |
| **Data Lineage (`user` table)** | `immich` | `inspect_data_lineage` | **`10.9 ms`** | `2.6 KB` | *N/A* | — | — | `ce`-exclusive: traces SQL/ORM readers & writers of `user` in 10.9 ms |
| **Data Lineage (`user` table)** | `twenty` | `inspect_data_lineage` | **`16.2 ms`** | `5.2 KB` | *N/A* | — | — | `ce`-exclusive: traces repository & query accessors in 16.2 ms |
| **Project Dependencies** | `twenty` | `get_project_dependencies` | **`9.5 ms`** | `1.0 KB` | *N/A* | — | — | `ce`-exclusive: instant inter-package dependency graph |
| **Semantic Invariant Gates** | `twenty` | `check_semantic_gates` | **`9.1 ms`** | `0.4 KB` | *N/A* | — | — | `ce`-exclusive: evaluates architectural rules & baseline ratchets |

---

## 6. Impartial Summary of Trade-Offs & Engineering Backlog

### 6.1 Where `codebase-memory-mcp` (`CBM`) Excels
1. **High-Speed, Low-Memory Indexing at Scale:** On a 24,527-file monorepo (`twenty`), `CBM` indexes from scratch in **96.9 seconds** (1.52× faster than `ce`'s `147.33 s`) and uses less RAM (`2.08 GB` vs. `4.66 GB`), whereas `ce` is faster on `immich` (`17.60 s` vs. `20.6 s`) and produces a smaller SQLite database on both repositories (`784.0 MB` vs. `842.5 MB` on `twenty`, `55.1 MB` vs. `282.0 MB` on `immich`).
2. **Low-Latency File Outline & Symbol Search on Large Graphs:** `CBM`'s `get_file_outline` (`17.3–40.5 ms`) and `search_graph` (`55.6–97.5 ms`) remain very fast on 250k+ nodes, while `ce` is faster on `immich` `get_file_outline` (`13.4 ms` vs. `17.3 ms`), schema queries (`1.9–23.2 ms`), and Cypher queries (`4.3–4.7 ms`).
3. **Unique Workflow Tools (`compare_graphs`, `manage_adr`, `ingest_traces`, `search_code`):** `CBM` provides built-in graph snapshot diffing across git branches, Architecture Decision Record (ADR) management, OpenTelemetry runtime trace ingestion, and regex code search inside the same MCP binary.

### 6.2 Where `CodeExplorer` (`ce`) Excels
1. **Higher Recall on Framework-Driven TypeScript (`78` vs. `38` Micro-Recall in Part 1):** `ce` resolves cross-file NestJS constructor-injected service calls and nested object-literal callbacks (`C6`: `100` vs. `60–75`), GraphQL `@Resolver` and template-literal REST controllers (`C1`: `100` vs. `0–90`), outbound HTTP calls (`C2`: `50–83` vs. `0–25`), and TypeScript `implements` + cross-package base classes (`C7`: `80` vs. `20` on `twenty`).
2. **Layer 4 & Layer 5 Architectural Ontology (`97` Micro-Recall in Part 2):** `ce` models ORM database tables (`TypeORM`, `Kysely`), SQL/repository read/write lineage (`17,382` edges on `twenty`), BullMQ producer/consumer queues (including enum/constant topic resolution), and monorepo sub-project boundaries (`46` packages in `twenty`, `21` in `immich`).
3. **Incremental In-Memory Taxonomy & Sub-5ms Cypher Execution:** Because `ce` maintains its graph taxonomy incrementally in memory as nodes and relationships are uploaded and persists the snapshot in `metadata`, `get_taxonomy` executes in **`22.3–23.2 ms`** on the first call in a fresh MCP process (**3.1× faster** than `CBM`'s `get_graph_schema` on `twenty`) and **`1.9–2.7 ms`** on cached calls, while read-only Cypher queries (`execute_custom_read_cypher`) execute in **`4.3–4.7 ms`** (8.9–15× faster than `CBM`'s `query_graph`).
4. **Single-Round-Trip Caller Resolution & 18 Architectural MCP Tools:** `ce`'s `get_callers` returns aggregated callers directly in a compact `1.5–2.5 KB` Markdown table in a single tool call, whereas `CBM`'s `trace_path` returns an `18.6–45.6 KB` `{"status": "ambiguous"}` candidate dump whenever a method name (`validate`, `execute`, `create`) appears in multiple classes. Furthermore, `ce` exposes dedicated tools for data lineage (`inspect_data_lineage` in `10.9–16.2 ms`), service contracts (`get_service_contracts`), test impact (`get_affected_tests`), and Semantic Invariant Gates (`check_semantic_gates`).

### 6.3 Actionable Engineering Backlog for `ce` Identified by This Benchmark
1. **Resolve Functional Mixin Base Classes (`C7` on `immich`, Recall `15`):** Support call-expression base classes (`extends createZodDto(Schema)` imported from external npm packages) in TypeScript inheritance extraction.
2. **Propagate Composite Relation Columns (`C10-schema` on `twenty`, Recall `76`):** Link composite relation columns defined without explicit `@Column` decorators to child `:Table` nodes.

---

## 7. Repository Artifacts in This Folder

- [`results/scores.csv`](./results/scores.csv) — Verified evaluation scorecard across all 32 Part 1 and Part 2 evaluations (`0–100` integer Recall scale).
- [`results/mcp-benchmark.json`](./results/mcp-benchmark.json) — Raw JSON-RPC stdio latency, payload size, and `tools/list` telemetry for `ce` (`36` tools) and `CBM` (`17` tools).
- [`results/indexing-telemetry.txt`](./results/indexing-telemetry.txt) — Cold indexing wall-clock time, peak working set (RAM), and SQLite database size measurements.
- [`results/pinned-shas.txt`](./results/pinned-shas.txt) — Exact commit SHAs tested for `twenty` and `immich`.
- [`groundtruth/`](./groundtruth/) — Deterministically sampled ground-truth CSVs (`twenty-c*.csv`, `immich-c*.csv`), Cypher query specs (`*-c11-queries.md`), and SHA256 freeze manifest (`FREEZE.txt`).
- [`scripts/`](./scripts/) — Reproducible ground-truth generator ([`build_groundtruth.py`](./scripts/build_groundtruth.py)), automated query/recall evaluator ([`evaluate_ce_vs_cbm.py`](./scripts/evaluate_ce_vs_cbm.py)), and MCP stdio latency benchmark ([`bench_mcp.py`](./scripts/bench_mcp.py)).
