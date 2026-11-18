# Zero-Spoiler Constructive Coding Benchmark: One-Shot Graph Context Compiler (`ce context`) vs Bare Agent (`ripgrep` + `read_file`)

Most benchmarks of code-indexing and graph tools either test **read-only symbol search** (*"list all callers of X"*) or suffer from methodological flaws that hide what actually happens when an autonomous coding agent writes production code.

This standalone study answers a direct, skeptical engineering question:

> **When a coding agent is given a realistic, zero-spoiler bug or feature ticket on a `36,543`-file monorepo (`twentyhq/twenty`), and both the Graph Agent and the Bare Agent (`ripgrep` + `read_file`) receive the exact same architectural rules in their system prompt, does a code graph actually reduce tokens, turns, and wall-clock time while producing working, architecturally compliant code — and under what interaction architecture?**

---

## 1. Why Most Code-Graph Coding Benchmarks Give Misleading Results

Before designing this benchmark, we audited why earlier constructive coding runs showed a graph-equipped agent taking `2.2×` *more* tokens and `2.5×` *more* time than a bare `ripgrep` agent. We identified three structural flaws common across agent benchmarks:

### Flaw 1: The Spoiler-Prompt Trap
When a benchmark task prompt names the exact target files (`api-key.service.ts`), method signatures (`revokeApiKey`), cache keys (`['usageLimits']`), or queue names, a 36,000-file monorepo collapses into a 2-file `read_file` task. The `Bare Agent` does not need to search at all; any indexing or graph tool called on top of a spoiler prompt is pure overhead.

**Our Rule**: Every task prompt in this benchmark is a **100% zero-spoiler symptom/behavior specification** — zero file paths, zero class names, zero method signatures, and zero module names.

### Flaw 2: The Asymmetric-Rules Trap
If one agent is secretly told about repository architectural constraints (or given a custom linter) while the baseline agent is not, the benchmark measures prompt secrecy rather than tool capability.

**Our Rule**: Both `CodeExplorer` and `Bare Agent` receive the **exact same 7 repository architectural invariants** verbatim in their system prompts (thin transport handlers `<= 45 LOC` with zero direct DB access, strict cross-module table ownership without circular `@Module` imports, and async queue/exception hygiene).

### Flaw 3: The Chatty Multi-Call MCP Trap
When an LLM uses a code graph like an interactive search engine — making 15 to 20 sequential MCP tool calls (`find_symbol` $\to$ `get_file_outline` $\to$ `get_callers` $\to$ `inspect_data_lineage` $\to$ `scan_workspace`) — every turn re-sends the growing conversation history (`30K–60K` tokens per step). Ten extra MCP turns cost `400K–600K` cumulative input tokens, completely wiping out the tokens saved by reading fewer source files.

**Our Architectural Shift**: Instead of exposing 15 granular navigation calls, we built a **One-Shot Task Context Pack Compiler** (`ce context "<keywords>"` / `GET /api/context?q=...` / `get_task_context`) that compiles the entire task subgraph in a **single call on Turn 1**:
1. **Diversified Anchors & Service Method Outlines** (`anchors`, `serviceMethods`): resolves camelCase/snake_case domain concepts across `Endpoint`, `Table`, `Type`, and `Function` nodes.
2. **Canonical Table Ownership** (`tablesAndCanonicalOwners`): ranks every `READS_FROM` and `WRITES_TO` accessor of touched database tables to identify the single canonical owning domain service (e.g., `UsageLimitService` for `usageLimit`, `PublicDomainService` for `publicDomain`) versus secondary consumers.
3. **Cross-Service Outbound Side-Effects** (`outboundSideEffects`): surfaces downstream cache invalidations (`WorkspaceCacheService.invalidateAndRecompute`), quota counter drops, and external DNS teardowns (`DnsManagerService.deleteHostnameSilently`) triggered by the owning service's methods.
4. **Module Wiring & Transitive Cycle Safety (`moduleWiringAndCycleSafety`, added in Iteration 2)**: extracts NestJS `@Module({ imports, providers, controllers, exports })` dependency edges (`DEPENDS_ON` and `USES_TYPE`) and runs an in-memory shortest-path BFS across all `460` NestJS modules in `<1 ms` to tell the agent upfront whether importing `TargetModule` into `ConsumerModule` is `SAFE_DIRECT_IMPORT` (`0` reverse paths) or `CYCLE_WARNING` (with the exact cycle path).
5. **Scoped Ratcheted Legacy Traps (`guardrails.legacyTrapsInSubgraphFiles`)**: warns the agent about existing `[TOLERATED]` baseline violations inside the exact files it is about to read so it does not imitate legacy anti-patterns.

---

## 2. Benchmark Tasks & Verification Protocol

All 12 runs (`6` in Iteration 1, `6` in Iteration 2) were executed on an isolated worktree of `twentyhq/twenty @ 12e8fae4` (`46` packages, `36,543` indexed files, `465,090` graph nodes, `1,068,128` graph relationships).

| Task ID | Zero-Spoiler Ticket Summary | Hidden Cross-Module & Side-Effect Complexity | Held-Out Verification |
| :--- | :--- | :--- | :--- |
| **[Task 1 (Feature 1)](./prompts/task-01-api-key-revoke.md)**<br>API Key Revocation Lifecycle | Revoking a workspace API key (`id`, `workspaceId`) must return `null` if missing, preserve `revokedAt` if already revoked, and remove all usage limits scoped to that key (`spenderType === 'apiKey'`, both workspace-admin and operator overrides) through Twenty's full removal lifecycle. | `ApiKeyService` (`api-key` module) must not delete from the `usageLimit` table directly (`SIG-TWENTY-003`). It must import `UsageLimitModule` and delegate to `UsageLimitService.delete(..., { isOperator: true })` so `WorkspaceCacheService.invalidateAndRecompute(['usageLimits'])` and active Redis quota/stock counters are cleaned up. | `5/5` Jest behavioral specs + `tsgo --noEmit` + `7/7` SIG gates (`0` new violations) |
| **[Task 2 (Feature 3)](./prompts/task-02-app-delete-public-domains.md)**<br>Application Deletion & Public Domain DNS Teardown | Deleting a custom application (`universalIdentifier`, `workspaceId`) must throw `APPLICATION_NOT_FOUND` if missing, and otherwise remove all `publicDomain` rows owned by that application in `workspaceId` through Twenty's full public-domain teardown lifecycle. | `ApplicationService` (`application` module) must not delete `PublicDomainEntity` rows directly (`SIG-TWENTY-003`). It must import `PublicDomainModule` and delegate to `PublicDomainService` so each domain's external custom hostname is torn down via `DnsManagerService.deleteHostnameSilently(domain, { isPublicDomain: true })` before cache recomputation. | `5/5` Jest behavioral specs + `tsgo --noEmit` + `7/7` SIG gates (`0` new violations) |
| **[Task 3 (Feature 4)](./prompts/task-03-subdomain-collision.md)**<br>Subdomain Validation & Suggestions vs Public Domains | When `PUBLIC_DOMAIN_URL` is a valid URL with a non-empty hostname, workspace subdomain validation, availability check (`checkWorkspaceSubdomainAvailability`), and suggestion generation (`-2`, `-3`, ...) must treat any `<subdomain>.<publicBaseHostname>` colliding with a registered `PublicDomain` as taken. | `SubdomainManagerService` (`domain/subdomain-manager`) must normalize `PUBLIC_DOMAIN_URL` (handling ports, casing, and invalid/empty-host URLs like `file:///`), import `PublicDomainModule` without introducing a circular dependency with `WorkspaceModule` / `AuthModule`, and query `PublicDomainService` in both single-check and batch candidate filtering. | `5/5` Jest behavioral specs + `tsgo --noEmit` + `7/7` SIG gates (`0` new violations) |

---

## 3. Where Agent Tokens Actually Go (The Iteration 1 Discovery)

In **Iteration 1** ([`results/round1-carbon/`](./results/round1-carbon/)), we tested the initial One-Shot Context Pack (`CE v1`, before indexing `@Module` imports) against `Bare Agent` on `Gemini 4 Carbon (High)`:
- `CE v1` beat `Bare Agent` on all 3 tasks: **`9.46M` vs `17.50M` total input tokens (`1.85×` fewer tokens)**, **`488.3K` vs `773.8K` uncached input tokens (`1.58×` fewer)**, **`100` vs `140` turns**, and **`2,516s` vs `2,946s`**.
- **However, inspecting the `CE v1` transcripts revealed a major bottleneck**:
  In Task 1 (`t1-ce`), `/api/context` delivered the exact 2 target service files (`api-key.service.ts` and `usage-limit.service.ts`) by **Turn 2 (`Step 3`)**. Yet the agent spent **24 additional turns (`Steps 10..76`)** running `rg` and reading `*.module.ts` files (`PermissionsModule`, `BillingModule`, `UsageModule`, `ThrottlerModule`, `EventLogEmitterModule`, etc.) to manually verify whether importing `UsageLimitModule` into `ApiKeyModule` would create a circular NestJS `@Module` dependency!
  And in Tasks 2 and 3, out of fear that `PublicDomainModule -> PermissionsModule` might transitively reach `ApplicationModule` or `SubdomainManagerModule`, the agent created brand-new `PublicDomainLookupModule` sub-modules (+3 extra files) when `PublicDomainModule` actually had **zero** reverse paths to either module!

### The Fix in Iteration 2 (`NestJsLibraryParser` `@Module` Graph + `moduleWiringAndCycleSafety`)
We extended `NestJsLibraryParser` to extract `@Module({ imports, providers, controllers, exports })` edges (`(Module:Type)-[:DEPENDS_ON]->(ImportedModule:Type)` and `(Module:Type)-[:USES_TYPE]->(Service:Type)`), adding `5,237` module dependency relationships across `460` `*.module.ts` files in `twenty`. Inside `McpContextPackBuilder`, we added an automatic `<1 ms` BFS pass (`moduleWiringAndCycleSafety`) that outputs exact pairwise cycle verdicts directly inside the Turn 1 context pack:

```yaml
moduleWiringAndCycleSafety:
  owningModules:
    - module: ApiKeyModule
      filePath: packages/twenty-server/src/engine/core-modules/api-key/api-key.module.ts
      provides: [ApiKeyService]
      directModuleImports: [EventLogEmitterModule, JwtModule, PermissionsModule, RoleTargetModule, ThrottlerModule, WorkspaceCacheModule, WorkspaceDataSourceModule]
    - module: UsageLimitModule
      filePath: packages/twenty-server/src/engine/core-modules/usage-limit/usage-limit.module.ts
      provides: [UsageLimitService]
      directModuleImports: [BillingModule, MetricsModule, PermissionsModule, ThrottlerModule, UsageAnalyticsModule, UsageModule, WorkspaceCacheModule]
  importSafetyVerdicts:
    - "SAFE_DIRECT_IMPORT: ApiKeyModule can import UsageLimitModule directly (0 reverse paths in module graph; no need to inspect other *.module.ts files)."
    - "SAFE_DIRECT_IMPORT: UsageLimitModule can import ApiKeyModule directly (0 reverse paths in module graph; no need to inspect other *.module.ts files)."
```

---

## 4. Complete Results: Iteration 2 (`Context-Pack v2 + @Module Graph`) & Iteration 1 Comparison

### Table A: 3-Task Suite Totals Across Both Iterations (12 Runs Total)

| Metric (Sum of Tasks 1, 2 & 3) | Iteration 1 `CE v1`<br>(No `@Module` Graph) | Iteration 1 `Bare`<br>(`rg` + `read_file`) | Iteration 2 `CE v2`<br>(With `@Module` Safety) | Iteration 2 `Bare`<br>(`rg` + `read_file`) | `CE v2` vs `Bare`<br>(Same Model, Iter 2) | Impact of `@Module` Graph<br>(`CE v1` $\to$ `CE v2`) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Held-Out Jest Behavioral Specs** | `15 / 15` | `15 / 15` | **`15 / 15`** | `15 / 15` | **`100%` Pass** | **`100%` Pass** |
| **Architectural Gates (`SIG-TWENTY-001..007`)** | `21 / 21` (`0` new) | `21 / 21` (`0` new) | **`21 / 21` (`0` new)** | `21 / 21` (`0` new) | **`0` New Violations** | **`0` New Violations** |
| **LLM Turns** | `100` | `140` | **`59`** | `84` | **`1.42×` fewer turns** (`-30%`) | **`1.69×` fewer turns** (`100` $\to$ `59`) |
| **Total Tool Calls** | `189` | `283` | **`95`** | `165` | **`1.74×` fewer calls** (`-42%`) | **`1.99×` fewer calls** (`189` $\to$ `95`) |
| **File Reads (`read_file`)** | `121` | `175` | **`55`** | `104` | **`1.89×` fewer reads** (`-47%`) | **`2.20×` fewer reads** (`121` $\to$ `55`) |
| **Shell Commands (`run_command`)** | `45` | `82` | **`20`** | `41` | **`2.05×` fewer cmds** (`-51%`) | **`2.25×` fewer cmds** (`45` $\to$ `20`) |
| **Wall-Clock Time** | `2,516 s` (`41m 56s`) | `2,946 s` (`49m 06s`) | **`1,178 s` (`19m 38s`)** | `1,682 s` (`28m 02s`) | **`1.43×` faster** (`-8m 24s`) | **`2.14×` faster** (`41.9m` $\to$ `19.6m`) |
| **Uncached Input Tokens** | `488,327` | `773,756` | **`316,255`** | `491,682` | **`1.55×` fewer** (`-36%`) | **`1.54×` fewer** (`488K` $\to$ `316K`) |
| **Total Input Tokens (Uncached + Cached)** | `9,457,637` | `17,502,629` | **`3,080,276`** | `6,897,615` | **`2.24×` fewer** (`-55%`) | **`3.07×` fewer** (`9.46M` $\to$ `3.08M`) |
| **Output Tokens** | `98,913` | `127,093` | **`41,472`** | `61,150` | **`1.47×` fewer** (`-32%`) | **`2.38×` fewer** (`98.9K` $\to$ `41.5K`) |

---

### Table B: Per-Task Breakdown — Iteration 2 (`Context-Pack v2 + @Module Safety` vs `Bare Agent`, `Gemini 4 Argon Medium`)

| Task | Agent | Jest Specs | SIG Gates | LLM Turns | Tool Calls (`cmd` / `read` / `edit`) | Wall-Clock Time | Uncached Input Tokens | Total Input Tokens | Output Tokens |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Task 1**: API Key Revoke $\to$ Usage Limits, Cache & Counters | **`CE v2`** ([diff](./results/round2-argon-module-graph/t1-ce/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | **`16`** | **`25`** (`7` / `12` / `5`) | **`322 s`** (`5m 22s`) | **`77,390`** | **`772,814`** | **`11,782`** |
| **Task 1**: API Key Revoke $\to$ Usage Limits, Cache & Counters | **`Bare`** ([diff](./results/round2-argon-module-graph/t1-bare/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | `26` (`1.63×`) | `55` (`11` / `39` / `4`) | `587 s` (`9m 47s`, `1.82×`) | `142,325` (`1.84×`) | `2,203,949` (`2.85×`) | `23,368` (`1.98×`) |
| **Task 2**: App Delete $\to$ Public Domains & External DNS | **`CE v2`** ([diff](./results/round2-argon-module-graph/t2-ce/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | **`16`** | **`28`** (`2` / `19` / `6`) | **`252 s`** (`4m 12s`) | **`85,483`** | **`881,006`** | **`8,811`** |
| **Task 2**: App Delete $\to$ Public Domains & External DNS | **`Bare`** ([diff](./results/round2-argon-module-graph/t2-bare/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | `26` (`1.63×`) | `48` (`15` / `26` / `6`) | `425 s` (`7m 05s`, `1.69×`) | `127,830` (`1.50×`) | `2,062,682` (`2.34×`) | `13,997` (`1.59×`) |
| **Task 3**: Subdomain Validation & Suggestions vs Public Domains | **`CE v2`** ([diff](./results/round2-argon-module-graph/t3-ce/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | **`27`** | **`42`** (`11` / `24` / `6`) | **`604 s`** (`10m 04s`) | **`153,382`** | **`1,426,456`** | **`20,879`** |
| **Task 3**: Subdomain Validation & Suggestions vs Public Domains | **`Bare`** ([diff](./results/round2-argon-module-graph/t3-bare/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | `32` (`1.19×`) | `62` (`15` / `39` / `7`) | `670 s` (`11m 10s`, `1.11×`) | `221,527` (`1.44×`) | `2,630,984` (`1.84×`) | `23,785` (`1.14×`) |

---

### Table C: Per-Task Breakdown — Iteration 1 Baseline Archive (`Context-Pack v1` without `@Module` Graph vs `Bare Agent`, `Gemini 4 Carbon High`)

| Task | Agent | Jest Specs | SIG Gates | LLM Turns | Tool Calls (`cmd` / `read` / `edit`) | Wall-Clock Time | Uncached Input Tokens | Total Input Tokens | Output Tokens |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Task 1**: API Key Revoke $\to$ Usage Limits, Cache & Counters | **`CE v1`** ([diff](./results/round1-carbon/t1-ce/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | **`31`** | **`60`** (`12` / `43` / `4`) | **`509 s`** (`8m 29s`) | **`151,694`** | **`2,858,456`** | **`26,197`** |
| **Task 1**: API Key Revoke $\to$ Usage Limits, Cache & Counters | **`Bare`** ([diff](./results/round1-carbon/t1-bare/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | `40` (`1.29×`) | `92` (`19` / `67` / `5`) | `676 s` (`11m 16s`, `1.33×`) | `212,272` (`1.40×`) | `5,033,482` (`1.76×`) | `31,381` (`1.20×`) |
| **Task 2**: App Delete $\to$ Public Domains & External DNS | **`CE v1`** ([diff](./results/round1-carbon/t2-ce/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | **`38`** | **`69`** (`20` / `40` / `6`) | **`1,039 s`** (`17m 19s`) | **`188,875`** | **`4,166,906`** | **`37,142`** |
| **Task 2**: App Delete $\to$ Public Domains & External DNS | **`Bare`** ([diff](./results/round1-carbon/t2-bare/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | `55` (`1.45×`) | `109` (`39` / `60` / `9`) | `1,216 s` (`20m 16s`, `1.17×`) | `273,548` (`1.45×`) | `8,204,360` (`1.97×`) | `51,125` (`1.38×`) |
| **Task 3**: Subdomain Validation & Suggestions vs Public Domains | **`CE v1`** ([diff](./results/round1-carbon/t3-ce/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | **`31`** | **`60`** (`13` / `38` / `6`) | **`968 s`** (`16m 08s`) | **`147,758`** | **`2,432,275`** | **`35,574`** |
| **Task 3**: Subdomain Validation & Suggestions vs Public Domains | **`Bare`** ([diff](./results/round1-carbon/t3-bare/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | `45` (`1.45×`) | `82` (`24` / `48` / `9`) | `1,054 s` (`17m 34s`, `1.09×`) | `287,936` (`1.95×`) | `4,264,787` (`1.75×`) | `44,587` (`1.25×`) |

---

## 5. Repository Structure & Verbatim Artifacts

- **Prompts (`./prompts/`)**:
  - [`task-01-api-key-revoke.md`](./prompts/task-01-api-key-revoke.md)
  - [`task-02-app-delete-public-domains.md`](./prompts/task-02-app-delete-public-domains.md)
  - [`task-03-subdomain-collision.md`](./prompts/task-03-subdomain-collision.md)
  - [`ce-agent-v2.md`](./prompts/ce-agent-v2.md) & [`bare-agent.md`](./prompts/bare-agent.md)
- **Compiled Turn-1 Graph Context Packs (`./context-packs/`)**:
  - [`task-01-context.yaml`](./context-packs/task-01-context.yaml)
  - [`task-02-context.yaml`](./context-packs/task-02-context.yaml)
  - [`task-03-context.yaml`](./context-packs/task-03-context.yaml)
- **Evaluation Harness & Held-Out Jest Suite (`./scripts/`)**:
  - [`eval-ctx-exp.mjs`](./scripts/eval-ctx-exp.mjs)
  - [`acceptance-spec.template.ts`](./scripts/acceptance-spec.template.ts)
- **Raw Run Scorecards, Git Diffs & Gate Evaluations (`./results/`)**:
  - [`metrics.json`](./results/metrics.json)
  - [`round2-argon-module-graph/`](./results/round2-argon-module-graph/) (`t1-ce`, `t1-bare`, `t2-ce`, `t2-bare`, `t3-ce`, `t3-bare`)
  - [`round1-carbon/`](./results/round1-carbon/) (`t1-ce`, `t1-bare`, `t2-ce`, `t2-bare`, `t3-ce`, `t3-bare`)
