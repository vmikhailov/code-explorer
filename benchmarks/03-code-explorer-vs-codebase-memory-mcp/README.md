# Case Study 03: Comparative Evaluation of `CodeExplorer` (`ce`) vs. `codebase-memory-mcp` (`CBM`)

> **Scope & Impartiality Disclosure**: This benchmark evaluates two open-source Model Context Protocol (MCP) codebase graph engines—**`CodeExplorer` (`ce` `v1.74.0`)** and **`codebase-memory-mcp` (`CBM` `v0.12.1`)**—across two open-source TypeScript/polyglot monorepos (`twentyhq/twenty` at `24,527` tracked files / `~2.49M` LOC and `immich-app/immich` at `3,205` tracked files / `~461k` LOC).
>
> Because the two tools target different abstraction depths—**`CBM`** is a fast, lightweight **Layer 1–3 structural AST & call-graph indexer**, whereas **`ce`** runs a **Layer 1–5 semantic and global architectural ontology pipeline** (including ORM tables, SQL/repository read/write lineage, message queue topology, and monorepo sub-project boundaries)—a single flat score across all categories would be methodologically unfair to `CBM`. We therefore separate the evaluation into three distinct parts:
> 1. **Part 1 — Head-to-Head on Common Ground (`C1`, `C2`, `C6`, `C7` + Cold Indexing Speed & Footprint):** Direct competition strictly on capabilities supported by both tools (`130` deterministically sampled ground-truth items).
> 2. **Part 2 — Extended L4/L5 Architectural Capabilities (`C3`, `C4`, `C5`, `C8`, `C9`, `C10`, `C11`):** Standalone evaluation of `ce` on architectural and data-lineage categories (`235` ground-truth items) that `CBM` does not model by design.
> 3. **Part 3 — MCP Server Tool Surface, Warm Stdio Latency & Agent Ergonomics:** Comparison of the MCP tools exposed to AI agents (`36` in `ce` vs. `17` in `CBM`), warm JSON-RPC round-trip latency, and response payload ergonomics.
>
> **Transparency Note on Framework Extractors:** During the preparation of this benchmark, several TypeScript framework extractors in `ce` (covering NestJS GraphQL `@Resolver`, BullMQ `@Process` decorators, TypeORM `@Entity` / `@Column`, and Kysely `Updateable<T>`) were added or refined (`v1.72.0` → `v1.74.0`). `CBM` `v0.12.1` was evaluated out-of-the-box using its official `win32-x64` release binary.

---

## 1. Experimental Setup & Methodology

### 1.1 Tools Under Test
- **`ce` (`codeexplorer.cli`)**: `v1.74.0` (.NET 10, Tree-sitter + 5-Layer Semantic & Global Ontology Pipeline, SQLite graph backend, `36` MCP tools).
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

| Benchmark Dimension | **`ce` (`v1.74.0`)** | **`CBM` (`v0.12.1`)** | Objective Assessment |
| :--- | :---: | :---: | :--- |
| **Cold Indexing Speed (`twenty` + `immich`)** | `232.2 s` (~3.9 min) | **`117.5 s` (~2.0 min)** | **`ce` is faster on `immich` (`19.6 s` vs. `20.6 s`)**; **`CBM` is 2.2× faster** on `twenty` (`96.9 s` vs. `212.6 s`) |
| **Peak RAM / DB Size (`twenty`)** | `4,589 MB` / **`805 MB`** | **`2,076 MB`** / `843 MB` | **`ce` produces a smaller SQLite DB on both repos** (**`805 MB` vs. `843 MB` on `twenty`**, **`55.1 MB` vs. `282.0 MB` on `immich` — 5.1× smaller**); `CBM` uses less RAM |
| **Part 1: Head-to-Head Matched (`C1, C2, C6, C7`)** | **86 / 130** | 49 / 130 | Direct competition on Endpoints, Outbound HTTP, Call Graph, and Inheritance |
| **Part 1: Head-to-Head Micro-Recall (`0–100`)** | **66** | 38 | **`immich`: `62` vs. `50`** (`41/66` vs. `33/66`); **`twenty`: `70` vs. `25`** (`45/64` vs. `16/64`) |
| **Part 1: Head-to-Head Macro-Recall (`0–100`)** | **60** | 36 | Unweighted average across the 8 head-to-head evaluations |
| **Part 1: Category Wins / Ties / Losses (of 8)** | **6 W / 1 T / 1 L** | 1 W / 1 T / 6 L | `CBM` wins `twenty C2` (`25` vs. `8`), ties `immich C7` (`15` vs. `15`), and scores `90` on `immich C1` |
| **Part 2: `ce` L4/L5 Ontology Recall (`C3–C5, C8–C11`)** | **95** (`223 / 235`) | *Not modeled (L1–L3 only)* | `ce` achieves **95** micro-recall (**96** macro-recall) across queues, ORM tables, data lineage & services |
| **Part 3: MCP Tool Surface & Warm Query Latency** | **36 tools** (`1.9–530 ms`) | **17 tools** (`6.5–287 ms`) | `ce` is faster on `get_taxonomy` (`22 ms` / `1.9 ms` cached), Cypher (`4.3 ms`), `immich` outline (`13.4 ms`) & 1-turn callers; `CBM` is faster on large-repo outline & symbol search |

---

## 3. Part 1: Head-to-Head Comparison on Common Ground (`L1–L3`)

### 3.1 Cold Indexing Speed, Resource Footprint & Parse Health

Both tools were executed from a cold state (`Remove-Item .codeexplorer/graph.db*` and a fresh `CBM_CACHE_DIR`) on the same Windows workstation:

| Metric | `twenty` — **`ce`** | `twenty` — **`CBM`** | `immich` — **`ce`** | `immich` — **`CBM`** |
| :--- | :---: | :---: | :---: | :---: |
| **Repository Scale** | 24,527 tracked (`36,644` physical files) | 24,527 files (~2.49M LOC) | 3,205 tracked (`1,485` code files) | 3,205 files (~461k LOC) |
| **Cold Index Wall Time** | `212.6 s` (3.5 min) | **`96.9 s` (1.6 min)** | **`19.6 s`** | `20.6 s` |
| **Peak Working Set (RAM)** | `4,589 MB` | **`2,076 MB`** | `777 MB` | **`595 MB`** |
| **SQLite DB Size on Disk** | **`805.0 MB`** | `842.5 MB` | **`55.1 MB`** | `282.0 MB` |
| **Graph Nodes Extracted** | `459,130` | `251,750` | `54,274` | `164,125` (incl. 141k `Variable` nodes) |
| **Graph Edges Extracted** | `965,886` | `914,479` | `115,775` | `246,561` |
| **Parse / Analysis Warnings** | **0** (`0` `ScanIssue` nodes) | 209 `parse_partial`, 1 `unusable` | **0** | 4 `parse_partial` |

**Analysis of Indexing Performance (`ce` Phase Breakdown):**
- On medium-sized repositories (`immich`), **`ce` indexes faster than `CBM` (`19.6 s` vs. `20.6 s`)** and produces a **5.1× smaller database** (`55.1 MB` vs. `282.0 MB` because `CBM` indexes 141,286 local `Variable` nodes and `ce` Schema v6 encodes `kind` and `from_id`/`to_id` as compact integers (`rowid`) and builds partial/unique B-Tree indices bottom-up after bulk load).
- On large monorepos (`twenty`), **`CBM` is 2.2× faster (`96.9 s` vs. `212.6 s`)** and uses less RAM, while **`ce` produces a smaller SQLite database (`805.0 MB` vs. `842.5 MB`) despite storing 1.82× more nodes (`459,130` vs. `251,750`) and 56k more edges (`965,886` vs. `914,479`)**. Breaking down `ce`'s `212.6 s` cold run on `twenty` shows where the time is spent:
  - **Layer 1 & 2 (Physical topology & 46 project boundaries across 36,644 files):** `21 s`
  - **Layer 3 Pass 1 (Tree-sitter C-API parse + UAST construction for 36,644 files):** `42 s`
  - **Layer 3 Pass 2 (AST visitors + declarative semantic extensions + in-memory bulk buffering):** `82 s`
  - **Layer 4 (Semantic enrichment across 46 projects / 36,340 syntax trees):** **`5 s`**
  - **Layer 5 (Resolving 578,143 global cross-references + late-binding 5,092 `ExternalService` calls):** `19 s`
  - **Single-Transaction Unindexed Bulk Flush + Bottom-Up B-Tree Index Build + `PostIndexAnalyzer` & Incremental AST Snapshots:** `48 s` (**`17.9 s`** for bulk flush + index creation of 459.1k nodes and 1.04M integer-keyed edges)

---

### 3.2 Semantic Recall on Common Ground (`C1`, `C2`, `C6`, `C7`)

These four categories test core code-graph capabilities supported by both tools:
- **`C1` (Incoming HTTP / GraphQL Endpoints):** `(:Endpoint)-[:EXPOSED_BY]->(:Function)` in `ce` vs. `(:Method)-[:HANDLES]->(:Route)` in `CBM`.
- **`C2` (Outbound HTTP Calls):** `(:ExternalService)` in `ce` vs. `(:Function)-[:HTTP_CALLS]->(:Route)` in `CBM`.
- **`C6` (Call Graph — Intra-Class + Cross-Class DI Service Calls):** Automatically sampled by `build_groundtruth.py` from both `public` service methods (called across files via NestJS constructor DI `this.<service>.<method>()`) and `private` helper methods (called intra-class via `this.<method>()`).
- **`C7` (Class Inheritance & Interface Implementation):** `(:Type)-[:INHERITS_FROM|IMPLEMENTS]->(:Type)` in `ce` vs. `(:Class)-[:INHERITS|IMPLEMENTS]->(:Class|Interface)` in `CBM`.

| Category | Description | Repo | GT | **`ce` Matched** | **`ce` Recall (`0–100`)** | **`ce` Total Extracted** | **`CBM` Matched** | **`CBM` Recall (`0–100`)** | **`CBM` Total Extracted** | Winner |
| :--- | :--- | :--- | :-: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **C1** | Incoming HTTP / GraphQL Endpoints | `twenty` | 20 | **20 / 20** | **100** | 896 | 0 / 20 | 0 | 4 | **`ce`** |
| **C1** | Incoming HTTP / FastAPI Endpoints | `immich` | 20 | **20 / 20** | **100** | 321 | 18 / 20 | 90 | 271 | **`ce`** (close) |
| **C2** | Outbound HTTP Calls (`fetch` / clients) | `twenty` | 12 | 1 / 12 | 8 | 963 | **3 / 12** | **25** | 196 | **`CBM`** |
| **C2** | Outbound HTTP Calls (`fetch` / clients) | `immich` | 6 | **1 / 6** | **17** | 13 | 0 / 6 | 0 | 48 | **`ce`** |
| **C6** | Call Graph (Intra-Class + DI Service Calls) | `twenty` | 12 | **11 / 12** | **92** | 18 | 9 / 12 | 75 | 10 | **`ce`** |
| **C6** | Call Graph (Intra-Class + DI Service Calls) | `immich` | 20 | **17 / 20** | **85** | 128 | 12 / 20 | 60 | 97 | **`ce`** |
| **C7** | Class Inheritance & `implements` | `twenty` | 20 | **13 / 20** | **65** | 1,424 | 4 / 20 | 20 | 250 | **`ce`** |
| **C7** | Class Inheritance & `implements` | `immich` | 20 | **3 / 20** | **15** | 139 | **3 / 20** | **15** | 564 | **Tie** |

#### Part 1 Aggregate Score Summary (`0–100` Scale)

| Subset | **`ce` Matched / GT** | **`ce` Micro-Recall** | **`ce` Macro-Recall** | **`CBM` Matched / GT** | **`CBM` Micro-Recall** | **`CBM` Macro-Recall** |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **`twenty` (Common Ground)** | **45 / 64** | **70** | **66** | 16 / 64 | 25 | 30 |
| **`immich` (Common Ground)** | **41 / 66** | **62** | **54** | 33 / 66 | 50 | 41 |
| **Part 1 Combined Total** | **86 / 130** | **66** | **60** | **49 / 130** | **38** | **36** |

#### Detailed Analysis of Part 1 Differences
1. **Call Graph (`C6` — `92` vs. `75` on `twenty`, `85` vs. `60` on `immich`):**
   - Both tools accurately resolve **intra-class helper calls** (`this.helperMethod()`), where `CBM` matches **9/9** intra-class items on `twenty` (Recall `75` overall) and **12/12** intra-class items on `immich` (Recall `60` overall).
   - The gap on the remaining items comes from **NestJS Dependency Injection calls** (`this.publicDomainService.findByDomain()`, `this.service.updateAll()`). In TypeScript, NestJS services declare injected dependencies as constructor parameter properties (`constructor(private readonly service: SomeService)`). `ce` resolves constructor parameter property types to their target classes across files, whereas `CBM` does not resolve method calls on constructor-injected fields.
2. **Incoming Endpoints (`C1` — `100` vs. `90` on `immich`, `100` vs. `0` on `twenty`):**
   - On `immich` (standard `@Controller('albums')` + Python FastAPI `@app.get('/ping')`), `CBM` achieves **90** (`18/20`), missing only 2 controllers whose route prefixes use TypeScript enum constants (`@Controller(RouteKey.Asset)`).
   - On `twenty`, REST controllers use template-literal route prefixes (`` @Controller(`${ApiPath.Rest}/metadata/fields`) ``) and the primary API surface uses NestJS GraphQL (`@Resolver`, `@Query`, `@Mutation`). `CBM` misclassifies template-literal `@Controller` decorators as outbound `HTTP_CALLS` rather than `HANDLES -> Route` and does not index GraphQL `@Resolver` operations (`0/20`).
3. **Outbound HTTP (`C2` — `CBM` wins `25` vs. `8` on `twenty`):**
   - Both tools score low on generic TypeScript `fetch()` / variable-URL HTTP calls (`8–25`), making `C2` a shared weakness.
   - **`CBM` wins `twenty C2` (`3/12` vs. `1/12`)** because `CBM` extracts `HTTP_CALLS` edges from functions invoking `fetch` with relative path templates (`/-/v1/search?text=...`, `/{}/latest`), whereas `ce`'s `:ExternalService` extractor only captures literals containing full scheme prefixes (`https://...`) or GraphQL client operations.
4. **Class Inheritance & Interfaces (`C7` — Tie `15` vs. `15` on `immich`, `ce` leads `65` vs. `20` on `twenty`):**
   - On `immich`, **both tools tie at `15` (`3/20`)** because 17 of the 20 sampled classes use functional mixin base classes (`export class AlbumResponseDto extends createZodDto(AlbumResponseSchema) {}`), which neither tool's AST visitor resolves as a base class.
   - On `twenty`, `ce` scores **65** (`13/20`) vs. `CBM`'s **20** (`4/20`) because `ce` extracts TypeScript `implements` relationships (`implements MigrationInterface`, `implements FastInstanceCommand`) into `:IMPLEMENTS` edges.

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
| **C4** | Database Tables (`TypeORM` / `Kysely`) | `twenty` | 17 | **17 / 17** | **100** | 169 `:Table` nodes |
| **C4** | Database Tables (`TypeORM` / `Kysely`) | `immich` | 18 | **18 / 18** | **100** | 99 `:Table` nodes |
| **C5** | Code-to-Table Read/Write Lineage | `twenty` | 18 | **18 / 18** | **100** | 17,372 `READS_FROM` / `WRITES_TO` edges |
| **C5** | Code-to-Table Read/Write Lineage | `immich` | 10 | **10 / 10** | **100** | 1,155 `READS_FROM` / `WRITES_TO` edges |
| **C10-schema** | Table Columns & Entity Members (`:Member`) | `twenty` | 17 | **9 / 17** | **53** | 1,486 `Table -> Entity -> Member` links |
| **C10-schema** | Table Columns & Entity Members (`:Member`) | `immich` | 20 | **19 / 20** | **95** | 595 `Table -> Entity -> Member` links |
| **C9-services** | Monorepo Sub-Projects (`:Project`) | `twenty` | 8 | **8 / 8** | **100** | 46 `:Project` nodes |
| **C9-services** | Monorepo Sub-Projects (`:Project`) | `immich` | 7 | **7 / 7** | **100** | 21 `:Project` nodes |
| **C11-queries** | 6 End-to-End Architectural Cypher Queries | `twenty` | 6 | **6 / 6** | **100** | 76 rows across 6 queries |
| **C11-queries** | 6 End-to-End Architectural Cypher Queries | `immich` | 6 | **6 / 6** | **100** | 82 rows across 6 queries |
| **Part 2 Total** | **All L4/L5 Architectural Categories** | **Both** | **235** | **223 / 235** | **95** (Micro) / **96** (Macro) | **Full L4/L5 Ontology** |

**Notes on Part 2 Limitations (`C10-schema` on `twenty`, Recall `53`):**
- On `twenty`, `ce` matched `9/17` sampled table columns because several TypeORM entities inherit standard columns (`id`, `createdAt`, `updatedAt`, `deletedAt`) from base classes (`WorkspaceCustomEntity`, `BaseWorkspaceEntity`) or define composite relation columns without explicit `@Column` decorators on the child class. Propagating inherited `:Member` nodes along `[:INHERITS_FROM]` to the mapped `:Table` is an open enhancement for `ce`.

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
1. **High-Speed, Low-Memory Indexing at Scale:** On a 24,527-file monorepo (`twenty`), `CBM` indexes from scratch in **96.9 seconds** (2.2× faster than `ce`'s `212.6 s`) and uses less RAM (`2.08 GB` vs. `4.59 GB`), whereas `ce` produces a smaller SQLite database on both repositories (`805.0 MB` vs. `842.5 MB` on `twenty`, `55.1 MB` vs. `282.0 MB` on `immich`).
2. **Low-Latency File Outline & Symbol Search on Large Graphs:** `CBM`'s `get_file_outline` (`17.3–40.5 ms`) and `search_graph` (`55.6–97.5 ms`) remain very fast on 250k+ nodes (2.3–3.8× faster than `ce`'s `get_file_outline` (`92.7 ms`) and `find_symbol` (`367 ms`) on `twenty`, while `ce` is faster on `immich` `get_file_outline` at `13.4 ms` vs. `17.3 ms`).
3. **Unique Workflow Tools (`compare_graphs`, `manage_adr`, `ingest_traces`, `search_code`):** `CBM` provides built-in graph snapshot diffing across git branches, Architecture Decision Record (ADR) management, OpenTelemetry runtime trace ingestion, and regex code search inside the same MCP binary.

### 6.2 Where `CodeExplorer` (`ce`) Excels
1. **Higher Recall on Framework-Driven TypeScript (`66` vs. `38` Micro-Recall in Part 1):** `ce` resolves cross-file NestJS constructor-injected service calls (`C6`: `85–92` vs. `60–75`), GraphQL `@Resolver` and template-literal REST controllers (`C1`: `100` vs. `0–90`), and TypeScript `implements` relationships (`C7`: `65` vs. `20` on `twenty`).
2. **Layer 4 & Layer 5 Architectural Ontology (`95` Micro-Recall in Part 2):** `ce` models ORM database tables (`TypeORM`, `Kysely`), SQL/repository read/write lineage (`17,386` edges on `twenty`), BullMQ producer/consumer queues (including enum/constant topic resolution), and monorepo sub-project boundaries (`46` packages in `twenty`, `21` in `immich`).
3. **Incremental In-Memory Taxonomy & Sub-5ms Cypher Execution:** Because `ce` maintains its graph taxonomy incrementally in memory as nodes and relationships are uploaded and persists the snapshot in `metadata`, `get_taxonomy` executes in **`25.5–28.1 ms`** on the first call in a fresh MCP process (**2.9× faster** than `CBM`'s `get_graph_schema` on `twenty`) and **`2.4–3.3 ms`** on cached calls, while read-only Cypher queries (`execute_custom_read_cypher`) execute in **`3.0–4.9 ms`** (9.7–25× faster than `CBM`'s `query_graph`).
4. **Single-Round-Trip Caller Resolution & 16 Architectural MCP Tools:** `ce`'s `get_callers` returns aggregated callers directly in a compact `1.2–2.5 KB` Markdown table in a single tool call, whereas `CBM`'s `trace_path` returns an `18.6–45.6 KB` `{"status": "ambiguous"}` candidate dump whenever a method name (`validate`, `execute`, `create`) appears in multiple classes. Furthermore, `ce` exposes dedicated tools for data lineage (`inspect_data_lineage` in `15.7–29.2 ms`), service contracts (`get_service_contracts`), test impact (`get_affected_tests`), and Semantic Invariant Gates (`check_semantic_gates`).

### 6.3 Actionable Engineering Backlog for `ce` Identified by This Benchmark
1. **Optimize `find_symbol` on 450k-Node Graphs:** Add composite indexes / fast-path lookups so `find_symbol` (`367 ms`) on `twenty` drops under `100 ms` (`get_file_outline` already reached `92.7 ms` with Schema v6).
2. **Improve Generic Outbound HTTP Call Extraction (`C2`, Recall `8–17`):** Extend `TypeScriptFileVisitor` to extract `fetch()`, `axios`, and NestJS `HttpService` calls that use relative path templates or variable URLs.
3. **Resolve Functional Mixin Base Classes (`C7` on `immich`, Recall `15`):** Support call-expression base classes (`extends createZodDto(Schema)`) in TypeScript inheritance extraction.
4. **Propagate Inherited Entity Columns (`C10-schema` on `twenty`, Recall `53`):** Link `:Member` columns inherited from base entity classes (`BaseWorkspaceEntity`) to child `:Table` nodes.

---

## 7. Repository Artifacts in This Folder

- [`results/scores.csv`](./results/scores.csv) — Verified evaluation scorecard across all 32 Part 1 and Part 2 evaluations (`0–100` integer Recall scale).
- [`results/mcp-benchmark.json`](./results/mcp-benchmark.json) — Raw JSON-RPC stdio latency, payload size, and `tools/list` telemetry for `ce` (`36` tools) and `CBM` (`17` tools).
- [`results/indexing-telemetry.txt`](./results/indexing-telemetry.txt) — Cold indexing wall-clock time, peak working set (RAM), and SQLite database size measurements.
- [`results/pinned-shas.txt`](./results/pinned-shas.txt) — Exact commit SHAs tested for `twenty` and `immich`.
- [`groundtruth/`](./groundtruth/) — Deterministically sampled ground-truth CSVs (`twenty-c*.csv`, `immich-c*.csv`), Cypher query specs (`*-c11-queries.md`), and SHA256 freeze manifest (`FREEZE.txt`).
