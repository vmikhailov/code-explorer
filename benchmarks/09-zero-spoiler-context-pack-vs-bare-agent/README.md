# Case Study 09: Zero-Spoiler Coding Benchmark — One-Shot Graph Context Pack (`ce`) vs Bare Agent (`ripgrep` + `read_file`)

Most benchmarks of code-graph tools either evaluate **read-only symbol search** (*"list all callers of X"*) or inadvertently give away target file paths and method names inside the task prompt.

This study answers a direct engineering question:

> **When an AI coding agent is given a realistic, zero-spoiler bug or feature ticket on a `36,543`-file monorepo (`twentyhq/twenty`), and both agents receive the exact same architectural rules in their system prompt, how much faster and leaner is a One-Shot Task Context Pack (`CodeExplorer`) compared to a Bare Agent (`ripgrep` + `read_file`)?**

---

## 1. Eliminating the Three Classic Agent Benchmark Traps

### 1. Zero-Spoiler Symptom Prompts
When a prompt names the target files (`api-key.service.ts`), method signatures (`revokeApiKey`), or cache keys (`['usageLimits']`), a `36,543`-file monorepo collapses into a 2-file `read_file` task where search is unnecessary. Every task prompt in this benchmark is a **100% zero-spoiler behavior specification** — zero file paths, zero class names, zero method signatures, and zero module names.

### 2. Symmetric Architectural Instructions
If one agent is told about repository architectural boundaries while the baseline agent is not, the benchmark measures prompt secrecy rather than tool capability. Both `CodeExplorer` and `Bare Agent` receive the **exact same workspace architectural invariants** verbatim in their system prompts (thin transport handlers `<= 45 LOC` with zero direct DB access, strict cross-module table ownership without circular module imports, and async queue/exception hygiene).

### 3. One-Shot Context Compilation Over Chatty MCP
When an LLM calls 15 to 20 sequential navigation tools (`find_symbol` $\to$ `get_file_outline` $\to$ `get_callers` $\to$ `inspect_data_lineage`), every turn re-sends the growing conversation history (`30K–60K` tokens per step), wiping out the token savings of reading fewer files. Instead, `CodeExplorer` compiles the task subgraph in **one call on Turn 1** (`get_task_context` / `/api/context`):
- **Anchors & Service Outlines** (`anchors`, `serviceMethods`): resolves domain concepts across `Endpoint`, `Table`, `Type`, and `Function` nodes.
- **Canonical Table Ownership** (`tablesAndCanonicalOwners`): ranks `READS_FROM` and `WRITES_TO` edges to pinpoint the canonical owning service (`UsageLimitService` for `usageLimit`, `PublicDomainService` for `publicDomain`) versus secondary consumers.
- **Downstream Side-Effects** (`outboundSideEffects`): surfaces cache invalidations (`WorkspaceCacheService.invalidateAndRecompute`), quota counter drops, and external DNS teardowns (`DnsManagerService.deleteHostnameSilently`) triggered by owning methods.
- **Module Wiring & Legacy Debt Warnings** (`moduleWiringAndCycleSafety`, `guardrails.legacyTrapsInSubgraphFiles`): verifies module import acyclicity across the dependency graph and flags tolerated legacy violations inside the exact files the agent is about to read so it does not imitate legacy shortcuts.

---

## 2. Benchmark Tasks & Held-Out Verification Protocol

All 6 runs (`3` tasks $\times$ `2` agents on `Gemini 4 Argon (Medium)`) were executed in isolated worktrees of `twentyhq/twenty @ 12e8fae4` (`46` packages, `36,543` indexed files, `465,090` graph nodes, `1,068,128` graph relationships).

| Task ID | Zero-Spoiler Ticket Summary | Cross-Module & Side-Effect Requirements | Held-Out Verification |
| :--- | :--- | :--- | :--- |
| **[Task 1](./prompts/task-01-api-key-revoke.md)**<br>API Key Revocation Lifecycle | Revoking a workspace API key (`id`, `workspaceId`) must return `null` if missing, preserve `revokedAt` if already revoked, and remove all usage limits scoped to that key (`spenderType === 'apiKey'`, both workspace-admin and operator overrides) through Twenty's full removal lifecycle. | `ApiKeyService` must not delete from the `usageLimit` table directly (`SIG-TWENTY-003`). It must delegate through `UsageLimitService.delete(..., { isOperator: true })` so `WorkspaceCacheService.invalidateAndRecompute(['usageLimits'])` and active Redis quota/stock counters are cleaned up. | `5/5` Jest behavioral specs + `tsgo --noEmit` + `7/7` SIG gates (`0` new violations) |
| **[Task 2](./prompts/task-02-app-delete-public-domains.md)**<br>Application Deletion & Public Domain DNS Teardown | Deleting a custom application (`universalIdentifier`, `workspaceId`) must throw `APPLICATION_NOT_FOUND` if missing, and otherwise remove all `publicDomain` rows owned by that application in `workspaceId` through Twenty's full public-domain teardown lifecycle. | `ApplicationService` must not delete `PublicDomainEntity` rows directly (`SIG-TWENTY-003`). It must delegate to `PublicDomainService` so each domain's external custom hostname is torn down via `DnsManagerService.deleteHostnameSilently(domain, { isPublicDomain: true })` before cache recomputation. | `5/5` Jest behavioral specs + `tsgo --noEmit` + `7/7` SIG gates (`0` new violations) |
| **[Task 3](./prompts/task-03-subdomain-collision.md)**<br>Subdomain Validation & Suggestions vs Public Domains | When `PUBLIC_DOMAIN_URL` is a valid URL with a non-empty hostname, workspace subdomain validation, availability check (`checkWorkspaceSubdomainAvailability`), and suggestion generation (`-2`, `-3`, ...) must treat any `<subdomain>.<publicBaseHostname>` colliding with a registered `PublicDomain` as taken. | `SubdomainManagerService` must normalize `PUBLIC_DOMAIN_URL` (handling ports, casing, and invalid/empty-host URLs like `file:///`), wire `PublicDomainService` cleanly without circular dependencies, and check collisions in both single-check and batch candidate filtering. | `5/5` Jest behavioral specs + `tsgo --noEmit` + `7/7` SIG gates (`0` new violations) |

---

## 3. Results

### Table A: 3-Task Suite Totals (`CodeExplorer` vs `Bare Agent`, 6 Paired Runs)

| Metric (Sum of Tasks 1, 2 & 3) | `Bare Agent`<br>(`rg` + `read_file`) | `CodeExplorer`<br>(One-Shot Context Pack + Gates) | `CodeExplorer` Advantage |
| :--- | :---: | :---: | :---: |
| **Held-Out Jest Behavioral Specs** | `15 / 15` (`100%`) | **`15 / 15` (`100%`)** | **Parity (`15/15` pass)** |
| **Architectural Gates (`SIG-TWENTY-001..007`)** | `21 / 21` (`0` new) | **`21 / 21` (`0` new)** | **`0` New Violations** |
| **LLM Turns** | `84` | **`59`** | **`1.42×` fewer turns** (`-30%`) |
| **Total Tool Calls** | `165` | **`95`** | **`1.74×` fewer calls** (`-42%`) |
| **File Reads (`read_file`)** | `104` | **`55`** | **`1.89×` fewer reads** (`-47%`) |
| **Shell Commands (`run_command`)** | `41` | **`20`** | **`2.05×` fewer commands** (`-51%`) |
| **Wall-Clock Time** | `1,682 s` (`28m 02s`) | **`1,178 s` (`19m 38s`)** | **`1.43×` faster** (`-8m 24s`) |
| **Uncached Input Tokens** | `491,682` | **`316,255`** | **`1.55×` fewer** (`-36%`) |
| **Total Input Tokens (Uncached + Cached)** | `6,897,615` | **`3,080,276`** | **`2.24×` fewer** (`-55%`) |
| **Output Tokens** | `61,150` | **`41,472`** | **`1.47×` fewer** (`-32%`) |

---

### Table B: Per-Task Breakdown (`Gemini 4 Argon (Medium)`)

| Task | Agent | Jest Specs | SIG Gates | LLM Turns | Tool Calls (`cmd` / `read` / `edit`) | Wall-Clock Time | Uncached Input Tokens | Total Input Tokens | Output Tokens |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Task 1**: API Key Revoke $\to$ Usage Limits, Cache & Counters | **`CodeExplorer`** ([diff](./results/t1-ce/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | **`16`** | **`25`** (`7` / `12` / `5`) | **`322 s`** (`5m 22s`) | **`77,390`** | **`772,814`** | **`11,782`** |
| **Task 1**: API Key Revoke $\to$ Usage Limits, Cache & Counters | **`Bare`** ([diff](./results/t1-bare/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | `26` (`1.63×`) | `55` (`11` / `39` / `4`) | `587 s` (`9m 47s`, `1.82×`) | `142,325` (`1.84×`) | `2,203,949` (`2.85×`) | `23,368` (`1.98×`) |
| **Task 2**: App Delete $\to$ Public Domains & External DNS | **`CodeExplorer`** ([diff](./results/t2-ce/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | **`16`** | **`28`** (`2` / `19` / `6`) | **`252 s`** (`4m 12s`) | **`85,483`** | **`881,006`** | **`8,811`** |
| **Task 2**: App Delete $\to$ Public Domains & External DNS | **`Bare`** ([diff](./results/t2-bare/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | `26` (`1.63×`) | `48` (`15` / `26` / `6`) | `425 s` (`7m 05s`, `1.69×`) | `127,830` (`1.50×`) | `2,062,682` (`2.34×`) | `13,997` (`1.59×`) |
| **Task 3**: Subdomain Validation & Suggestions vs Public Domains | **`CodeExplorer`** ([diff](./results/t3-ce/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | **`27`** | **`42`** (`11` / `24` / `6`) | **`604 s`** (`10m 04s`) | **`153,382`** | **`1,426,456`** | **`20,879`** |
| **Task 3**: Subdomain Validation & Suggestions vs Public Domains | **`Bare`** ([diff](./results/t3-bare/git.diff)) | `5 / 5` | `7 / 7` (`0` new) | `32` (`1.19×`) | `62` (`15` / `39` / `7`) | `670 s` (`11m 10s`, `1.11×`) | `221,527` (`1.44×`) | `2,630,984` (`1.84×`) | `23,785` (`1.14×`) |

---

## 4. Verbatim Artifacts

- **Prompts (`./prompts/`)**:
  - [`task-01-api-key-revoke.md`](./prompts/task-01-api-key-revoke.md)
  - [`task-02-app-delete-public-domains.md`](./prompts/task-02-app-delete-public-domains.md)
  - [`task-03-subdomain-collision.md`](./prompts/task-03-subdomain-collision.md)
  - [`ce-agent.md`](./prompts/ce-agent.md) & [`bare-agent.md`](./prompts/bare-agent.md)
- **Compiled Turn-1 Graph Context Packs (`./context-packs/`)**:
  - [`task-01-context.yaml`](./context-packs/task-01-context.yaml)
  - [`task-02-context.yaml`](./context-packs/task-02-context.yaml)
  - [`task-03-context.yaml`](./context-packs/task-03-context.yaml)
- **Evaluation Harness & Held-Out Jest Suite (`./scripts/`)**:
  - [`eval-ctx-exp.mjs`](./scripts/eval-ctx-exp.mjs)
  - [`acceptance-spec.template.ts`](./scripts/acceptance-spec.template.ts)
- **Raw Run Scorecards, Git Diffs & Gate Evaluations (`./results/`)**:
  - [`metrics.json`](./results/metrics.json)
  - [`t1-ce/`](./results/t1-ce/), [`t1-bare/`](./results/t1-bare/), [`t2-ce/`](./results/t2-ce/), [`t2-bare/`](./results/t2-bare/), [`t3-ce/`](./results/t3-ce/), [`t3-bare/`](./results/t3-bare/)
