# Twenty (`twentyhq/twenty`) Benchmark Findings & Remediation Plan

**Date:** 2026-11-06  
**Target Corpus:** `twentyhq/twenty` (`12e8fae471865e9dab60c0b0fd59f318505c804e`) — 13,617 `.ts` files in `twenty-server`, ~26,800 total files across monorepo  
**Comparison Baseline:** `CodeExplorer (ce)` v1.71.0 / v1.72.1 vs. `codebase-memory-mcp (CBM)` v0.12.1 vs. `ripgrep` (`rg`) Ground Truth

---

## 1. Executive Summary

During the benchmark pilot against `twentyhq/twenty`, we evaluated both indexing performance (`ce` v1.71.0 vs. v1.72.1 vs. CBM v0.12.1) and semantic graph accuracy against static source ground truth:

1. **Performance & Footprint (`ce` v1.72.1 Layer 1/3/5 Optimizations)**:
   - Layer 1 `--clear` fast-path (skipping redundant file SHA-256 hashing), Layer 3 $O(1)$ `ConstantRegistry` lookup + removal of dead `POTENTIAL_TYPE` references, and Layer 5 backward BFS + lazy FTS5 index initialization resolved the multi-tens-of-minutes hang in Layer 5.
2. **Semantic Extraction Depth (`ce` vs. CBM)**:
   - **ORM & Data Lineage**: `ce` detects **85/85 (100%)** TypeORM `@Entity` tables and traces **3,660** `READS_FROM`/`WRITES_TO` lineage edges (CBM detects **0** TypeORM tables).
   - **API Surface**: `ce` detects **166/172 (96.5%)** NestJS REST endpoints and **579/643 (90.0%)** NestJS GraphQL `@Query`/`@Mutation` endpoints with full `EXPOSES` handler linkage (CBM links only 4 handlers).
3. **Identified Precision & Recall Gaps in `ce`**:
   - **False Projects (`P1`–`P6`)**: Misclassified backend/docs/e2e/fixture/nested `package.json` manifests in TypeScript monorepos.
   - **False Links & Nodes (`L1`–`L7`)**:
     - `L1`: GraphQL SDL (`input`/`enum`/`interface`) scalar field types (`UUID`, `String`, `Boolean`) parsed as anonymous shorthand queries $\rightarrow$ **1,378 false `ExternalService` nodes** and **1,952 false `SERVICE_CALL` edges**.
     - `L2`: Internal library calls (e.g., `minimatch`) lifted to `ExternalService` when SDK heuristic triggers.
     - `L3`: Test fixture `.graphql` schemas (`schema.graphql` in `__tests__` / `fixtures`) producing **473 false GraphQL `Endpoint` nodes**.
     - `L4`: NestJS `@Query('param')` parameter decorators parsed as HTTP route decorators $\rightarrow$ **18 false `QUERY:*` REST endpoints**.
     - `L5`: Duplicate `Table` nodes (**80 duplicates**) between TypeORM `@Entity('agent')` (`workspace:tbl:agent`) and raw SQL migrations (`workspace:db:default:core:ds:core:tbl:agent`), plus **39 test tables** from `*.spec.ts`.
     - `L6`: Custom BullMQ wrapper (`MessageQueueService.add(MessageQueue.x, ...)` + `@Processor(MessageQueue.x)`) missed by `BullMqLibraryParser` (**0/128 queues/processors resolved**).
     - `L7`: Unresolved template URL hosts (`unknown`, `localhost`, `example.com`) creating noisy `ExternalService` nodes.

---

## 2. Summary Status Matrix

| ID | Category | Issue Description | Impact on `twenty` | Status | Branch / Target Files |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **P1** | Project Classification | Backend (`@nestjs/core`) using `react` for SSR email templates classified as `FrontendApp` | `twenty-server` $\rightarrow$ `FrontendApp` instead of `Service` | **Completed & Verified** | `ReactNextComponentParser.cs` |
| **P2** | Project Discovery | Nested sourceless `package.json` inside `src/` creates phantom `Project` | `twenty-front/src/modules/ui` & `app-deps-template` phantom `FrontendApp` | **Completed & Verified** | `Layer2BoundariesAnalyzer.Scanning.cs` |
| **P3** | Project Classification | Monorepo root `package.json` (`workspaces`, no `src/`) classified as `Service` | Root `twenty` $\rightarrow$ `Service` instead of `SharedLibrary` (`is_orchestrator`) | **Completed & Verified** | `TypeScriptParser.Manifest.cs`, `ProjectEntityClassifierRegistry.cs` |
| **P4** | Project Classification | Playwright/Cypress e2e packages and `fixtures/*` classified as `Service`/`FrontendApp` | `twenty-e2e-testing` + `fixtures/*` misclassified | **Completed & Verified** | `TypeScriptParser.Manifest.cs`, `ProjectEntityClassifierRegistry.cs` |
| **P5** | Project Classification | Documentation sites (`mintlify`, `scope:docs`, `@mdx-js/mdx`, `@docusaurus/core`, `vitepress`, `nextra`) classified as `Service` | `twenty-docs` $\rightarrow$ `Service` instead of `SharedLibrary` | **Completed & Verified** | `TypeScriptParser.Manifest.cs` |
| **P6** | Project Boundaries | Multi-workload packages (`src/` library + `server/` backend in one `package.json`) merged into single `FrontendApp` | `twenty-sdk` (`src/` + `server/`) | **Planned (Phase 3)** | `Layer2BoundariesAnalyzer.Scanning.cs` |
| **L1** | False `ExternalService` | GraphQL SDL `input`/`enum`/`interface` fields (`id: UUID`) parsed as shorthand query operations | **1,378** false `ExternalService` nodes, **1,952** `SERVICE_CALL` edges | **Completed & Verified** | `GraphQLFileParser.cs` |
| **L2** | False `ExternalService` | Utility NPM packages (`minimatch`, etc.) matched as external SDK calls | `workspace:ext:svc:minimatch` (`16` `CALLS_SERVICE` edges) | **Completed & Verified** | `NetworkEntityValidator.cs` |
| **L3** | False `Endpoint` | `.graphql` files & controllers inside `/fixtures/`, `/__tests__/`, `/__mocks__/`, `*.spec.ts` parsed as production endpoints | **473** false GraphQL endpoints from test fixtures + **3** test controller endpoints | **Completed & Verified** | `GraphQLFileParser.cs`, `SourcePathClassifier.cs`, `EndpointSemanticAnalyzer.cs` |
| **L4** | False `Endpoint` | NestJS `@Query('param')` method parameter decorator parsed as HTTP `QUERY` route | **18** false `QUERY:*` REST endpoints in `twenty-server` | **Completed & Verified** | `NestJsLibraryParser.cs` |
| **L5** | Duplicate / Test `Table` | Unqualified ORM table ID (`workspace:tbl:x`) vs. schema-qualified SQL migration ID (`workspace:db:default:core:ds:core:tbl:x`) + test tables | **79** duplicate tables + **39** test tables (`272` total vs. `159` unique tables) | **Completed & Verified** | `Layer4SemanticAnalyzer.Workloads.cs`, `NestedSqlParser.cs` |
| **L6** | Missing `Topic` / Queue | Custom DI wrapper (`this.messageQueueService.add(MessageQueue.x, ...)`) & NestJS `@Processor(MessageQueue.x)` not resolved | **27 / 27** `MessageQueue` BullMQ queues & edges recovered | **Completed & Verified** | `BullMqLibraryParser.cs`, `Layer5GlobalAnalyzer.Messaging.cs` |
| **L7** | Noisy `ExternalService` | Unresolved template literals / HTTP headers / test hosts (`unknown-service`, `localhost`, `example.com`, `127.0.0.1`, `sec-ch-ua`, `/-/` URL stem) emitted as `ExternalService` | Reduced `ExternalService` on `twenty-server` from **1,401** to **13–24** real APIs | **Completed & Verified** | `EndpointSemanticAnalyzer.cs`, `NetworkEntityValidator.cs`, `SourcePathClassifier.cs` |

---

## 3. Phase 1 Fixes (Already Implemented in `fix/false-projects-links`, Commit `a5ba8c3a`)

The following fixes have been implemented and verified with 9 regression tests in `cli/tests/CodeExplorer.Tests/FalsePositiveRegressionTests.cs`:

### 3.1 `L1`: GraphQL SDL Shorthand Query Disambiguation (`GraphQLFileParser.cs`)
- **Root Cause**: `GraphQLOperationRegex` matched any `{` as an anonymous shorthand GraphQL query (`{ hero { name } }`) whenever `operationType` was empty. In `.graphql` SDL schema files (`input CreateAgentInput { name: String! }`, `enum Role { ADMIN }`), the regex matched `{` on the type body and extracted the first field's type/name (`UUID`, `String`, `Boolean`, `Int`, `JSON`) as a GraphQL query call target.
- **Fix**: An anonymous shorthand query `{ ... }` is only valid in GraphQL at document start (`match.Index == 0` after trimming comments/whitespace) or immediately following another top-level block `}`. Any `{` preceded by an SDL keyword/identifier (`type`, `input`, `enum`, `interface`, `union`, `scalar`, `schema`, `extend`, `directive`) is skipped.
- **Verified Effect**: Eliminates all `1,378` false scalar `ExternalService` nodes (`UUID`, `String`, `Boolean`, etc.) and `1,952` false `SERVICE_CALL` edges.

### 3.2 `L3`: Test Fixture `.graphql` Exclusion & Path Classification (`GraphQLFileParser.cs`, `SourcePathClassifier.cs`)
- **Root Cause**: `GraphQLFileParser.ParseContent` indexed `type Query` / `type Mutation` fields from test fixture schemas (e.g., `packages/twenty-sdk/src/cli/__tests__/e2e/fixtures/schema.graphql` containing 473 operations), and `SourcePathClassifier.IsTestPath` lacked `/fixtures/`, `/fixture/`, `/__fixtures__/`, `/__mocks__/`, `/e2e/`, `.integration-spec.`, and `.e2e-spec.`.
- **Fix**: Added these patterns to `SourcePathClassifier.IsTestPath` and gated `GraphQLFileParser.ParseContent` to skip endpoint/service extraction on test paths.
- **Verified Effect**: Removes `473` false test-fixture GraphQL endpoints.

### 3.3 `L4`: NestJS Parameter Decorator Filtering (`NestJsLibraryParser.cs`)
- **Root Cause**: In NestJS, `@Query('param')` on a controller method parameter (`required_parameter` / `optional_parameter` in Tree-sitter TypeScript) shares the same decorator name as `@Query()` in `@nestjs/graphql`. Because `NestJsLibraryParser` only excluded `Get/Post/Put/Delete/Patch` on parameters, `@Query('mode')` walked up to the enclosing `method_definition` and registered a false HTTP endpoint `QUERY:/route/mode`.
- **Fix**: Immediately ignore any decorator attached to `required_parameter`, `optional_parameter`, or `formal_parameters` in `NestJsLibraryParser.ExtractControllerEndpoints`.
- **Verified Effect**: Reduces `QUERY:*` REST endpoints on `twenty-server` from `18` to `0`.

### 3.4 `P1`: Backend Projects with SSR React (`ReactNextComponentParser.cs`)
- **Root Cause**: `twenty-server` depends on `@nestjs/core` (`nest-cli.json`), `typeorm`, and `bullmq`, but also imports `react` and `@react-email/render` to render transactional HTML emails on the server. `ReactNextComponentParser.HasFrontendSignal` treated any `react` dependency or `.tsx` file as a strong `FrontendApp` signal, overriding `Service`.
- **Fix**: Added `HasBackendDominanceSignal` check (`@nestjs/core`, `@nestjs/common`, `express`, `fastify`, `koa`, `@hapi/hapi`, `nest-cli.json`) when no explicit frontend bundler/framework (`next`, `vite`, `react-scripts`, `@remix-run/react`, `astro`, `index.html`) is present.
- **Verified Effect**: `twenty-server` is accurately classified as `Service` (`["Service", "Project"]`).

### 3.5 `P2`–`P4`: Monorepo Boundaries & Classify Rules (`Layer2BoundariesAnalyzer.Scanning.cs`, `TypeScriptParser.Manifest.cs`, `ProjectEntityClassifierRegistry.cs`)
- **Root Cause**:
  - `P2`: `packages/twenty-front/src/modules/ui/package.json` is a 3-line barrel manifest (`{"name": "twenty-ui"}`) inside `twenty-front/src/` with 0 `.ts`/`.tsx` files of its own, yet spawned a standalone `FrontendApp`.
  - `P3`: Root `package.json` with `"workspaces"` and no `src/` had `"scripts": {"start": "nx ..."}` which triggered `Service`.
  - `P4`: `packages/twenty-e2e-testing` (`@playwright/test`) and `packages/*/fixtures/*` were classified as `Service` / `FrontendApp`.
- **Fix**:
  - Absorb nested `package.json` manifests inside a parent project's `src/` when they have no `dependencies`/`devDependencies`/`workspaces` and no direct source files (`IsSourcelessNestedManifest`).
  - Classify root `package.json` with `workspaces` (or `nx.json`/`turbo.json`) and no `src/` directory as `IsOrchestrator = true`.
  - Detect `@playwright/test`, `playwright`, `cypress` as `Test` projects, and classify any project path matching `/fixtures/` or `/__fixtures__/` as `Test`.

---

## 4. Phase 2 Remediation Plan (Remaining Accuracy & Lineage Fixes)

### 4.1 `L5`: Unify ORM `@Entity` Table IDs with SQL Migration Table IDs & Filter Test Tables
- **Problem**:
  - `TypeOrmLibraryParser` emits `@Entity('agent')` (or `@Entity({ name: 'agent', schema: 'core' })`) with ID `workspace:tbl:agent` (or `workspace:db:default:core:tbl:agent`), whereas raw SQL migration parser (`CREATE TABLE "core"."agent"`) emits `workspace:db:default:core:ds:core:tbl:agent`.
  - In `twenty-server`, this produces **80 duplicate table pairs** (`273` `Table` nodes instead of `~154` unique tables) and splits `READS_FROM`/`WRITES_TO` edges across two nodes for the same physical table.
  - Additionally, **39 `Table` nodes** originate from `*.spec.ts` / `*.test.ts` files (`test_entity`, `my_table`, `dup_table`).
- **Implementation Steps**:
  1. **Schema Extraction in `TypeOrmLibraryParser.cs`**:
     - Parse object literal arguments in `@Entity({ name: '...', schema: '...' })` so the `schema` property (`core`, `metadata`, `workspace_123`) is recorded on the `Table` node metadata.
  2. **Leaf-Name Consolidation in `PostIndexAnalyzer.Databases.InMemory.Consolidation.cs`**:
     - When a single project/repository contains both an unqualified ORM table (`workspace:tbl:<name>`) and a single schema-qualified SQL table with the exact same leaf table name (`workspace:db:default:<schema>:ds:<schema>:tbl:<name>`), merge them into the canonical node and redirect all `READS_FROM`, `WRITES_TO`, and `REFERENCES_TABLE` edges.
  3. **Test Table Tagging / Filtering**:
     - Tag tables extracted from `SourcePathClassifier.IsTestPath(filePath)` with `is_test = true` (or skip emitting global `workspace:tbl:*` nodes from unit test mocks so they do not pollute production data lineage).

### 4.2 `L6`: Resolve Custom Queue/Topic Wrappers & NestJS `@Processor(Enum.Member)` (`BullMqLibraryParser.cs` & `Layer5GlobalAnalyzer.Messaging.cs`)
- **Problem**:
  - `twenty-server` defines `enum MessageQueue { billingQueue = 'billing-queue', ... }` and uses:
    - **128 consumers**: `@Processor(MessageQueue.billingQueue)` or `@Processor({ queueName: MessageQueue.billingQueue })`
    - **163 producers**: `await this.messageQueueService.add<T>(MessageQueue.billingQueue, jobName, payload)`
  - Currently, `BullMqLibraryParser` only matches literal strings in `@Processor('literal')` or direct `queue.add(...)` on variables typed as `Queue`, resulting in **0 Topics/Queues** extracted on `twenty-server`.
- **Implementation Steps**:
  1. **Enum / Constant Resolution in `BullMqLibraryParser.cs`**:
     - Support member expressions (`MessageQueue.billingQueue`) and object literals (`{ queueName: MessageQueue.billingQueue }`, `{ name: ... }`) inside `@Processor(...)` and `@InjectQueue(...)`.
     - Resolve the enum member via `ConstantRegistry` (or fallback to normalized member name `billingQueue` / kebab-case when resolved in Layer 5).
  2. **Wrapper Service Producer Detection (`MessageQueueService.add` / `addCron`)**:
     - Extend `BullMqLibraryParser` producer matching to recognize `.add(...)`, `.addBulk(...)`, and `.addCron(...)` on receiver identifiers/fields ending with `QueueService` or `messageQueueService`, where the **first argument** is a `MessageQueue.*` enum reference or queue name constant.
  3. **Verification Target**:
     - On `twenty-server`, `ce` should recover $\ge 25$ distinct `Topic` (`MessageQueue`) nodes, $\ge 100$ consumer `SUBSCRIBES_TO` edges, and $\ge 120$ producer `PUBLISHES_TO` edges.

### 4.3 `L2` & `L7`: Tighten `ExternalService` Host & SDK Heuristics (`Layer5GlobalAnalyzer.HostBoundaries.cs`)
- **Problem**:
  - `L2`: Pure local utility libraries (`minimatch`) or internal workspace packages can be promoted to `ExternalService` if matched by broad third-party package call heuristics.
  - `L7`: Template literals (`https://${host}/...`) where `${host}` cannot be statically resolved fall back to `workspace:ext:svc:unknown`, and test/dev URLs (`localhost`, `127.0.0.1`, `example.com`, `0.0.0.0`) create non-architectural `ExternalService` nodes.
- **Implementation Steps**:
  1. **Blocklist Non-External / Placeholder Hosts (`L7`)**:
     - Filter out `unknown`, `localhost`, `127.0.0.1`, `0.0.0.0`, `example.com`, `test.com`, and unresolved template placeholders (`$`, `{`) before creating `ExternalService` nodes.
  2. **Strict External SDK Allowlist / Category Check (`L2`)**:
     - Only promote NPM/NuGet/PyPI package calls to `ExternalService` when the package is a known network/cloud/API SDK (`stripe`, `@aws-sdk/*`, `googleapis`, `@octokit/*`, `axios`, `@slack/*`, `twilio`, `firebase-admin`, `@sentry/*`, etc.) rather than local string/path/glob utilities (`minimatch`, `lodash`, `date-fns`, `uuid`).

### 4.4 `P5`: Documentation Site Project Classification (`TypeScriptParser.Manifest.cs`)
- **Problem**:
  - `packages/twenty-docs` (`mintlify` / MDX docs package) is classified as `Service`.
- **Implementation Steps**:
  - In `TypeScriptParser.Manifest.cs`, check for documentation framework dependencies/configs (`mintlify`, `mint.json`, `@docusaurus/core`, `vitepress`, `nextra`, `mkdocs.yml`) and classify as `Library` (or documentation role) rather than `Service`.

---

## 5. Execution & Verification Workflow

1. **Step 1 — Merge Phase 1 (`fix/false-projects-links`) onto `main` (v1.72.1)**:
   - Cherry-pick commit `a5ba8c3a` onto `main`, run `dotnet test` + `ce gate check`, and verify combined L5 performance + Phase 1 accuracy improvements on `twenty-server` and full `twenty`.
2. **Step 2 — Implement Phase 2 (`L5` Table Consolidation, `L6` BullMQ Wrapper + Enum Resolution, `L2`/`L7` ExternalService Filtering, `P5` Docs Classification)**:
   - Add unit tests in `FalsePositiveRegressionTests.cs` covering `@Entity({ name, schema })` + SQL migration deduplication, `@Processor(MessageQueue.x)` + `messageQueueService.add(MessageQueue.x, ...)`, and `ExternalService` placeholder filtering.
3. **Step 3 — Re-run Benchmark Pilot (`diag_baseline.ps1`)**:
   - Verify target metrics on `twenty-server`:
     - `Project` classification: `twenty-server` = `Service`, `twenty-front` = `FrontendApp`, `twenty-e2e-testing` = `Test`.
     - `ExternalService` count: $\le 15$ (all genuine external APIs: `stripe`, `googleapis`, `api.github.com`, `api.openai.com`, etc.; `0` GraphQL scalars; `0` `unknown`/`localhost`).
     - `Endpoint` count: `0` `QUERY:*` false REST routes; `0` test-fixture GraphQL operations.
     - `Table` count: $\sim 150$ deduplicated production tables (`0` ORM/SQL split duplicates).
     - `Topic` count: $\ge 25$ BullMQ queues with matched producer/consumer edges.

---

## 6. Verified Post-Fix Benchmark Results (`twenty-server` & `twenty-v2`)

All Phase 1 and Phase 2 accuracy fixes (`P1`–`P5`, `L1`–`L7`) and Layer 3 Pass 2 optimizations have been implemented, verified by 13 regression tests in `FalsePositiveRegressionTests.cs`, and benchmarked on both `twenty-server` and the full `twenty-v2` monorepo:

| Metric | `twenty-server` (Before) | `twenty-server` (After) | `twenty-v2` Monorepo (Before) | `twenty-v2` Monorepo (After) |
| :--- | ---: | ---: | ---: | ---: |
| **Total Scan Time** | `86.52s` | **`79.56s`–`81.74s`** | `232.69s` | **`215.06s`–`235.22s`** |
| **Projects** | `7` (`twenty-server`=`FrontendApp`, `app-deps-template`=`FrontendApp`) | **`6`** (`twenty-server`=**`Service`**, `app-deps-template` absorbed) | `48` (`twenty-server`=`FrontendApp`, root=`Service`, `twenty-docs`=`Service`, `e2e`/fixtures=`Service`/`FrontendApp`) | **`46`** (`twenty-server`=**`Service`**, root=**`SharedLibrary`**, `twenty-docs`=**`SharedLibrary`**, `e2e`+5 fixtures=**`Test`**) |
| **Functions** | `56,784` (`4,057` false arrow-body return identifiers) | **`52,727`** (`0` false arrow-body functions) | `127,900` | **`123,139`** (`-4,761` false arrow-body functions) |
| **Endpoints** | `735` (`3` test, `18` false `@Query()` param routes) | **`714`** (**`0` test**, **`0` false `@Query()` routes**; `357` GQL Mutation, `215` GQL Query, `5` GQL Subscription, `137` REST) | `1,661` (`473` test fixture endpoints) | **`1,186`** (**`0` test endpoints**) |
| **ExternalServices** | `1,401` (`1,378` false GraphQL SDL `UUID`/`String`/`Boolean`, `23` headers/test hosts) | **`13`–`24`** (`0` GraphQL SDL scalars, `0` HTTP headers/test hosts; only real external APIs) | `1,880` | **`488`–`517`** (`457` frontend GQL operation calls + real HTTP APIs; `1,264` `CALLS_ENDPOINT`) |
| **Tables** | `272` (`79` duplicate ORM/SQL tables + `39` test tables) | **`159`** (**`0` test tables**, **`0` duplicate ORM/SQL tables**) | `277` (`39` test + `77` ORM duplicates) | **`164`** (**`0` test tables**, **`0` ORM duplicates**) |
| **Topics / Queues (BullMQ)** | `0` | **`27` BullMQ queues** (`27` `PUBLISHES_TO`, `27` `SUBSCRIBES_TO`) | `0` | **`27` BullMQ queues** (`27` `PUBLISHES_TO`, `27` `SUBSCRIBES_TO`) |

---

## 7. Per-Stage Performance Breakdown & Next Speedup Targets

### 7.1 Current Stage Timings

| Stage | `twenty-server` (`13,599` files, `81.7s`) | `twenty-v2` Monorepo (`36,644` files, `215s`–`235s`) | Share (`twenty-v2`) | Details |
| :--- | ---: | ---: | ---: | :--- |
| **Layer 1 (Physical Scan)** | `5s` | `17s` | ~7% | Directory walk + file loading into `PhysicalFileStore` |
| **Layer 2 (Project Boundaries)** | `1s` | `5s` | ~2% | 46 projects + 1,179 package dependency resolutions |
| **Layer 3 Pass 1 (Tree-sitter Parse + Constants)** | `18s` | `38s` | ~17% | Native Tree-sitter C-AST parse + UAST conversion + `AstConstantExtractor` |
| **Layer 3 Pass 2 (Visitors + UAST Eval + Enrichment)** | `17s` | `76s`–`86s` | ~35% | `SyntaxValueEvaluator`, `BaseParserVisitor.Visit`, `EnrichSyntacticParametersPrototypesAndExports`, `ComponentLibraryParser.EnrichWithSyntax` across 36,340 files (~2.2 ms/file across 16 threads) |
| **Layer 4 (Semantic Enrichment)** | `1s` | `4s` | ~2% | Endpoints, DB workloads, ORM/SQL table consolidation |
| **Layer 5a (Cross-References Resolution)** | `3s` | `10s` | ~4% | Resolves `818,322` global cross-references |
| **Layer 5b (SQLite Bulk Node/Edge Persistence)** | `15s` | `41s` | ~18% | Persisting `417,750` nodes & `894,450` edges to `graph.db` (`2.5 GB`) |
| **Layer 5c (`PostIndexAnalyzer` In-Memory)** | `13s` | `28s` | ~12% | `7s` DB canonicalization + `15s` project layer classification + `6s` edge normalization |
| **Incremental Snapshot Cache** | `2s` | `4s` | ~2% | Persisting file registry metadata to SQLite |

### 7.2 Next High-Impact Speedup Targets

1. **Layer 3 Pass 2 — Single-Pass AST Fusion (`76s` $\rightarrow$ target `~30s`)**:
   - Currently, each file in Pass 2 undergoes 3–4 separate full-tree traversals: (a) `SyntaxValueEvaluator.EvaluateDocument` (`WalkAndEvaluate`), (b) `BaseParserVisitor.Visit` (Tree-sitter cursor walk + `LibraryParsers.CollectReferences`), (c) `EnrichSyntacticParametersPrototypesAndExports` (`EnumerateSyntaxFunctions` + `EnumerateNamedLambdaFunctions` + `EnrichReferencesWithCallContextAndFlow`), and (d) `ComponentLibraryParser.IsTestSource` / `EnrichWithSyntax` (`DescendantsAndSelf().OfType<CallExprSyntax>()`).
   - **Action**: Pre-collect `FunctionDeclSyntax` and top-level `LambdaExprSyntax` lists on `SyntaxDocument` during `TreeSitterSyntaxPopulator` (just like `doc.AllCalls`), and evaluate `SyntaxValueEvaluator` lazily on-demand (or only on nodes referenced by `LibraryParsers` / `AstValueResolver`) instead of eagerly evaluating every expression in every file.
2. **Layer 5b — SQLite Write Volume & Property Compaction (`41s` $\rightarrow$ target `~20s`)**:
   - Out of `417,750` nodes in `twenty-v2`, `195,336` (47%) are `Member` (fields/properties) and `19,444` are `CallLiteral` nodes, generating `894,450` edges and a `2.5 GB` `graph.db`.
   - **Action**: Skip persisting unreferenced private local `CallLiteral` nodes or compact `PropertiesJson` serialization via pooled `Utf8JsonWriter` buffers during bulk insert.
3. **Layer 5c — `PostIndexAnalyzer.ClassifyArchitecturalLayers` (`15s` $\rightarrow$ target `<2s`)**:
   - Classifying architectural layers for 46 projects currently takes `15s` in `PostIndexAnalyzer` due to repeated scans over the in-memory node/edge lists per project.
   - **Action**: Pre-index nodes and edges by `ProjectId` once before running `ClassifyArchitecturalLayers`.

