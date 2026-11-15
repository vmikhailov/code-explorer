# Case Study 08: Multi-Task Brownfield Architecture Suite — CodeExplorer vs CBM (`twentyhq/twenty`, 4 Feature Tasks)

- **Repository**: [`twentyhq/twenty`](https://github.com/twentyhq/twenty) (`24,527` source files, `2.49M` LOC, `46` workspace packages, `464,794` graph nodes, `1,113,891` graph edges)
- **Commit**: `12e8fae4` (with unmodified repository [`CLAUDE.md`](https://github.com/twentyhq/twenty/blob/12e8fae4/CLAUDE.md))
- **Model**: `Gemini 3.8 Flash` (`inherit`)
- **Compared Tools**:
  1. **CodeExplorer (`v1.87.2`)** — recorded at two milestones inside each run:
     - **Phase 1: CE Navigation Only (Pre-Gate)** — the agent's initial feature implementation before running `ce gate check`.
     - **Phase 2: CE Navigation + Semantic Invariant Gates (Post-Gate)** — the final implementation after running `ce scan --incremental` + `ce gate check` and refactoring any `[NEW]` architectural violation.
  2. **codebase-memory-mcp (`v0.12.1`, CBM)** — structural call-graph MCP server (`search_graph`, `trace_path`, `query_graph`, `get_code_snippet`).

---

## Executive Summary

When an AI coding agent implements a feature inside a 2.5M-LOC brownfield monorepo, **how often does it copy an adjacent legacy shortcut that violates module boundaries — and does a structural code graph alone prevent that drift?**

To move beyond a single data point, we evaluated `CodeExplorer` and `codebase-memory-mcp (CBM)` across **4 brownfield feature tasks** in `twentyhq/twenty` (combining 3 new tasks in `auth`, `messaging`, and `domain` with the `admin-panel` workspace suspension run analyzed in detail in [Case Study 07](../07-agent-feature-ce-vs-cbm-on-twenty/README.md)). In every task, the target file or module sits next to an existing legacy shortcut (`[TOL]` in the baseline) that bypasses clean architectural boundaries:

| Metric (4-Task Aggregate) | CBM (`v0.12.1`) | CodeExplorer Phase 1 (Navigation Only, Pre-Gate) | CodeExplorer Phase 2 (Navigation + Gates, Final) |
| :--- | :---: | :---: | :---: |
| **Tasks with Initial Architectural Violations** | **4 / 4** (`100%`) | **4 / 4** (`100%`) | — |
| **Tasks Passing All Architectural Gates (Final)** | **0 / 4** (`0%`) | **0 / 4** (`0%`) | **4 / 4** (`100%`) |
| **Total `[NEW]` Architectural Violations Shipped** | **`4` `[NEW]`** (`1` per task) | `4` `[NEW]` (caught before completion) | **`0` `[NEW]`** (`256` `[TOL]` untouched) |
| **Gate Tampering (`ratchet --update` / rule edits)** | — | — | **`0 / 4`** (`100%` genuine code refactors) |
| **Self-Correction Iterations Needed** | — | — | **`1` iteration per task** (`4/4` fixed on first check) |
| **Unit Tests Passing (`.spec.ts` via `run-acceptance-tests.mjs`)** | **4 / 4 tasks** (`14/14` tests) | — | **4 / 4 tasks** (`14/14` tests) |
| **Total Wall-Clock Time (Sum of 4 Tasks)** | **2,646 s** (`44m 06s`) | — | **2,054 s** (`34m 14s`, **1.29× faster**) |
| **Total LLM Turns / Tool Calls** | `127` turns / `176` calls | — | `125` turns / `199` calls |
| **Total API Cost (`Gemini 3.8 Flash`)** | **\$0.48** (`10.03M` input tokens) | — | **\$0.48** (`9.75M` input tokens) |

---

## Key Findings

### 1. Graph Navigation Alone Does Not Stop "Monkey-See, Monkey-Do" Drift (`4/4` Pre-Gate Violations)
Across all 4 tasks, **both `CBM` (`4/4`) and `CodeExplorer Phase 1` (`4/4`, prior to running `ce gate check`) copied the nearest legacy shortcut in the file they were editing**:
- Neither agent needed to read `CLAUDE.md` to fall into this pattern (conversation logs confirm neither agent opened `CLAUDE.md`). When an LLM edits a service or resolver, it reads the surrounding methods in that file and naturally mimics their dependency injection and repository calls.
- Because a structural graph shows what existing code *does* rather than what it *is allowed to do*, navigation alone makes legacy shortcuts even easier to discover and replicate.

### 2. Why Not Just Put All Architectural Rules in `CLAUDE.md`?
A natural question is: *why not write these architectural rules into `CLAUDE.md` instead of using executable graph gates?* In real engineering teams, relying on `CLAUDE.md` for architectural invariants fails for four practical reasons:
1. **Nobody writes or maintains hundreds of boundary rules in `CLAUDE.md`**: In `twentyhq/twenty`'s actual [`CLAUDE.md`](https://github.com/twentyhq/twenty/blob/12e8fae4/CLAUDE.md), none of these module-encapsulation or transport-purity rules are documented — and `256` historical violations (`5` in `SIG-TWENTY-001`, `101` in `SIG-TWENTY-002`, `150` in `SIG-TWENTY-003`) have already accumulated in the codebase.
2. **You cannot predict which legacy file an agent will copy next**: A 2.5M-LOC monorepo has dozens of modules and hundreds of TypeORM entities. Maintainers only discover an agent copied a bad neighbor after the PR is opened (or worse, in production). Writing a prose paragraph in `CLAUDE.md` *after* you already know which 4 files you are benchmarking is hindsight; in real life, nobody keeps a prose encyclopedia of every forbidden cross-module edge inside the prompt context window.
3. **Prose cannot distinguish `[TOL]` legacy debt from `[NEW]` regressions**: Even if `CLAUDE.md` says *"do not inject foreign repositories across `core-modules/*`"*, the file the agent is editing *already* imports and injects them (`[TOL]`). Without a deterministic baseline ratchet (`.codeexplorer/gates-baseline.json`), neither the developer nor the CI pipeline can tell whether a violation was already there or newly introduced.
4. **Advisory instructions have no `Exit Code 1`**: Prompts are best-effort advice. Semantic Invariant Gates (`ce gate check`) are executable Cypher queries over the AST/DI/SQL graph that run both inside the agent's self-verification loop and in CI/CD, blocking regressions deterministically regardless of whether an agent remembered a prompt instruction.

### 3. Deterministic Self-Correction Without Gate Tampering (`4/4` Fixed on First Iteration)
When the `CodeExplorer` agent ran `ce scan --incremental` and `ce gate check` at the end of each task:
- Each gate pinpointed the exact `[NEW]` caller-to-target edge while ignoring the `256` tolerated (`[TOL]`) legacy shortcuts in the baseline.
- In **`4 / 4` tasks**, the agent refactored its code on the **first self-correction iteration** — delegating to the owning domain service and updating its `.spec.ts` mocks — without ever attempting to run `ce gate ratchet --update` or weaken `.codeexplorer/gates.yaml` (`0` tampering incidents).
- Even including the extra `ce scan --incremental`, `ce gate check`, and refactoring steps, `CodeExplorer` finished the 4-task suite **22% faster** overall (`2,054 s` vs `2,646 s`) and at the exact same API cost (**\$0.48** vs **\$0.48**).

---

## Frozen Repository-Wide Baseline (`SIG-TWENTY-001`..`004`)

All four gates in [`prompts/gates.yaml`](./prompts/gates.yaml) are generic, repository-wide architectural invariants ratified against unmodified `twentyhq/twenty @ 12e8fae4` (`256` total `[TOL]` legacy exceptions frozen in `.codeexplorer/gates-baseline.json`):

| Gate ID | Architectural Invariant | Scope | Frozen `[TOL]` Baseline on `12e8fae4` |
| :--- | :--- | :--- | :---: |
| **`SIG-TWENTY-001`** | **GraphQL Resolver & Controller Transport Purity**: `@Resolver` / `@Controller` classes in `core-modules/` must not directly read/write TypeORM tables; delegate to domain services. | All `core-modules/*` | `5` `[TOL]` |
| **`SIG-TWENTY-002`** | **Core-Engine to Feature-Modules Isolation**: `src/engine/core-modules/*` must not call application-layer services in `src/modules/*`. | `core-modules/*` $\to$ `modules/*` | `101` `[TOL]` |
| **`SIG-TWENTY-003`** | **Cross-Module Table Encapsulation**: A service in `core-modules/<moduleA>/` must not read/write a TypeORM table whose entity belongs to `core-modules/<moduleB>/`. | All `core-modules/*` pairs | `150` `[TOL]` |
| **`SIG-TWENTY-004`** | **No Direct Environment Variable Reads in Services**: Domain services must use `TwentyConfigService` instead of raw `process.env`. | All `packages/twenty-server` | `0` `[TOL]` |

> **Provenance Note on Task 03 (`suspendWorkspaceAdminPanel`)**: Tasks 01, 02, and 04 were executed specifically for this multi-task suite against the frozen 4-gate configuration above. Task 03 incorporates the clean `12e8fae4` run from [Case Study 07](../07-agent-feature-ce-vs-cbm-on-twenty/README.md) (`385986ab` for `CodeExplorer`, `775b7008` for `CBM`, both run on the same `CodeExplorer v1.87.2`, `CBM v0.12.1`, and `Gemini 3.8 Flash` stack), where `SIG-TWENTY-003` caught the cross-module `AdminPanelBillingService` $\to$ `emailingDomain` violation (`1` `[NEW]` violation pre-gate $\to$ `0` `[NEW]` post-gate for `CE`, vs `1` `[NEW]` violation shipped by `CBM`).

---

## Per-Task Breakdown

### Task 01: Admin Reset of Target User 2FA Methods (`auth`)
- **Prompt**: [`prompts/task-01-prompt.md`](./prompts/task-01-prompt.md)
- **Feature**: Add `resetTwoFactorAuthenticationMethodsForUser(userId: string)` to `TwoFactorAuthenticationResolver` so an admin can wipe a target user's 2FA methods.
- **Adjacent Legacy Trap**: `TwoFactorAuthenticationResolver.deleteTwoFactorAuthenticationMethod` (`two-factor-authentication.resolver.ts:215`, one of the `5` `[TOL]` entries in `SIG-TWENTY-001`) injects `@InjectRepository(TwoFactorAuthenticationMethodEntity)` directly inside the GraphQL resolver and calls `this.twoFactorAuthenticationMethodRepository.delete(...)` instead of delegating to `TwoFactorAuthenticationService`.

| Metric | CBM (`52f2cb0b`) | CodeExplorer (`3635a90f`) |
| :--- | :---: | :---: |
| **Phase 1 (Pre-Gate) Violations** | `1` `[NEW]` (`SIG-TWENTY-001`) | `1` `[NEW]` (`SIG-TWENTY-001`) |
| **Final Violations (`ce gate check`)** | **[FAIL] `1` `[NEW]`** | **[PASS] `0` `[NEW]`** |
| **How Implemented (Final)** | Called `twoFactorAuthenticationMethodRepository` directly inside `TwoFactorAuthenticationResolver` | Caught by `SIG-TWENTY-001`; extracted `resetTwoFactorAuthenticationMethodsForUser` into `TwoFactorAuthenticationService` and removed `@InjectRepository` from resolver |
| **Wall-Clock Time / LLM Turns** | `424 s` / `22` turns | `508 s` / `30` turns |
| **Total Input / Output Tokens** | `1,307,487` in / `11,757` out | `1,861,020` in / `20,356` out |
| **API Cost (`Gemini 3.8 Flash`)** | **\$0.07** | **\$0.10** |
| **Unit Tests (`run-acceptance-tests.mjs`)** | `3 / 3` passed | `3 / 3` passed |
| **Artifacts** | [Output](./results/task-01/cbm-agent-output.md) · [Patch](./results/task-01/cbm-agent.patch) · [Spec](./results/task-01/cbm-two-factor-authentication.resolver.spec.ts) | [Output](./results/task-01/ce-agent-output.md) · [Patch](./results/task-01/ce-agent.patch) · [Spec](./results/task-01/ce-two-factor-authentication.resolver.spec.ts) |

---

### Task 02: Flag Connected Account Reconnect Banner (`messaging`)
- **Prompt**: [`prompts/task-02-prompt.md`](./prompts/task-02-prompt.md)
- **Feature**: Add `triggerReconnectAccountBanner(userId: string, connectedAccountId: string)` to `TimelineMessagingResolver` in `src/engine/core-modules/messaging/`.
- **Adjacent Legacy Trap**: `TimelineMessagingResolver.dismissReconnectAccountBanner` (`timeline-messaging.resolver.ts:282`, one of the `101` `[TOL]` entries in `SIG-TWENTY-002`) reaches upward from `src/engine/core-modules/messaging/` into `src/modules/connected-account/services/accounts-to-reconnect.service.ts`.

| Metric | CBM (`7f906a4a`) | CodeExplorer (`d3ee7b01`) |
| :--- | :---: | :---: |
| **Phase 1 (Pre-Gate) Violations** | `1` `[NEW]` (`SIG-TWENTY-002`) | `1` `[NEW]` (`SIG-TWENTY-002`) |
| **Final Violations (`ce gate check`)** | **[FAIL] `1` `[NEW]`** | **[PASS] `0` `[NEW]`** |
| **How Implemented (Final)** | Called `this.accountsToReconnectService.addAccountToReconnectKey(...)` (`src/modules/connected-account/`) directly from `TimelineMessagingResolver` (`src/engine/core-modules/messaging/`) | Caught by `SIG-TWENTY-002`; delegated banner state update to `TimelineMessagingService` inside `core-modules/messaging/` using `UserVarsService` |
| **Wall-Clock Time / LLM Turns** | `455 s` / `27` turns | **`384 s`** / `22` turns (**1.18× faster**) |
| **Total Input / Output Tokens** | `1,148,620` in / `10,657` out | **`1,024,125`** in / `14,745` out |
| **API Cost (`Gemini 3.8 Flash`)** | **\$0.06** | **\$0.06** |
| **Unit Tests (`run-acceptance-tests.mjs`)** | `4 / 4` passed | `3 / 3` passed |
| **Artifacts** | [Output](./results/task-02/cbm-agent-output.md) · [Patch](./results/task-02/cbm-agent.patch) · [Spec](./results/task-02/cbm-timeline-messaging.resolver.spec.ts) | [Output](./results/task-02/ce-agent-output.md) · [Patch](./results/task-02/ce-agent.patch) · [Spec](./results/task-02/ce-timeline-messaging.resolver.spec.ts) |

---

### Task 03: Admin Panel Workspace Suspension (`admin-panel` — from [Case Study 07](../07-agent-feature-ce-vs-cbm-on-twenty/README.md))
- **Prompt**: [`prompts/task-03-prompt.md`](./prompts/task-03-prompt.md)
- **Feature**: Add `suspendWorkspace(workspaceId: string, reason?: string)` to `AdminPanelBillingService` in `src/engine/core-modules/admin-panel/` to suspend an active workspace, deactivate its billing entitlements, and revoke active `EmailingDomainEntity` records.
- **Adjacent Legacy Trap**: `WorkspaceService.deleteWorkspace` (`workspace.service.ts:989`) directly queries `@InjectRepository(EmailingDomainEntity)` across module boundaries instead of calling `EmailingDomainService` (`150` `[TOL]` baseline in `SIG-TWENTY-003`).

| Metric | CBM (`775b7008`) | CodeExplorer (`385986ab`) |
| :--- | :---: | :---: |
| **Phase 1 (Pre-Gate) Violations** | `1` `[NEW]` (`SIG-TWENTY-003`) | `1` `[NEW]` (`SIG-TWENTY-003`) |
| **Final Violations (`ce gate check`)** | **[FAIL] `1` `[NEW]`** | **[PASS] `0` `[NEW]`** |
| **How Implemented (Final)** | Injected `@InjectRepository(EmailingDomainEntity)` into `AdminPanelBillingService` and mutated `emailingDomain` directly (`+` missed `AdminPanelModule` DI wiring) | Caught by `SIG-TWENTY-003`; removed foreign repository from `AdminPanelBillingService`, delegated to `EmailingDomainService.suspendEmailingDomainsByWorkspaceId`, and wired `EmailingDomainModule` in `AdminPanelModule` |
| **Wall-Clock Time / LLM Turns** | `1,225 s` / `54` turns | **`779 s`** / `43` turns (**1.57× faster**) |
| **Total Input / Output Tokens** | `6,358,186` in / `39,092` out | **`5,135,656`** in / `38,441` out |
| **API Cost (`Gemini 3.8 Flash`)** | **\$0.29** | **\$0.24** |
| **Unit Tests (`run-acceptance-tests.mjs`)** | `3 / 3` passed | `4 / 4` passed |
| **Artifacts** | [Output](./results/task-03/cbm-agent-output.md) · [Patch](./results/task-03/cbm-agent.patch) · [Spec](./results/task-03/cbm-admin-panel-billing.service.spec.ts) | [Output](./results/task-03/ce-agent-output.md) · [Patch](./results/task-03/ce-agent.patch) · [Spec](./results/task-03/ce-admin-panel-billing.service.spec.ts) |

---

### Task 04: Subdomain Collision Check Against Public Domains (`domain`)
- **Prompt**: [`prompts/task-04-prompt.md`](./prompts/task-04-prompt.md)
- **Feature**: Update `SubdomainManagerService.isSubdomainAvailable(subdomain: string)` in `src/engine/core-modules/domain/subdomain-manager/` so a subdomain is also rejected if it collides with a registered `PublicDomainEntity` whose domain starts with `<subdomain>.`.
- **Adjacent Legacy Trap**: Neighboring files in `src/engine/core-modules/domain/` — `CustomDomainManagerService` (`custom-domain-manager.service.ts:29`) and `WorkspaceDomainsService` (`workspace-domains.service.ts:26`) — directly inject `@InjectRepository(PublicDomainEntity)` from `src/engine/core-modules/public-domain/` (`[TOL]` in `SIG-TWENTY-003`).

| Metric | CBM (`c10be936`) | CodeExplorer (`714b63d4`) |
| :--- | :---: | :---: |
| **Phase 1 (Pre-Gate) Violations** | `1` `[NEW]` (`SIG-TWENTY-003`) | `1` `[NEW]` (`SIG-TWENTY-003`) |
| **Final Violations (`ce gate check`)** | **[FAIL] `1` `[NEW]`** | **[PASS] `0` `[NEW]`** |
| **How Implemented (Final)** | Injected `@InjectRepository(PublicDomainEntity)` directly into `SubdomainManagerService` (`core-modules/domain/`) and queried `publicDomainRepository.findOne(...)` | Caught by `SIG-TWENTY-003`; added `isSubdomainTakenByPublicDomain(subdomain)` to `PublicDomainService` (`core-modules/public-domain/`), exported `PublicDomainService` from `PublicDomainModule`, and called it from `SubdomainManagerService` |
| **Wall-Clock Time / LLM Turns** | `542 s` / `24` turns | **`383 s`** / `30` turns (**1.42× faster**) |
| **Total Input / Output Tokens** | `1,215,876` in / `16,432` out | `1,724,466` in / `16,239` out |
| **API Cost (`Gemini 3.8 Flash`)** | **\$0.07** | **\$0.09** |
| **Unit Tests (`run-acceptance-tests.mjs`)** | `4 / 4` passed | `4 / 4` passed |
| **Artifacts** | [Output](./results/task-04/cbm-agent-output.md) · [Patch](./results/task-04/cbm-agent.patch) · [Spec](./results/task-04/cbm-subdomain-manager.service.spec.ts) | [Output](./results/task-04/ce-agent-output.md) · [Patch](./results/task-04/ce-agent.patch) · [Spec](./results/task-04/ce-subdomain-manager.service.spec.ts) |

---

## Unit Test Verification (`scripts/run-acceptance-tests.mjs`)

To prevent `node.exe` OOM crashes on Windows when spawning full-monorepo Jest workers across 46 packages, both agent system prompts (`ce-agent.md` and `cbm-agent.md`) instruct the agents to write `.spec.ts` unit tests without invoking Jest directly.

After each run, every agent's patch (`results/task-01..04/{cbm,ce}-agent.patch`) was applied to a clean `twentyhq/twenty @ 12e8fae4` worktree and verified using [`scripts/run-acceptance-tests.mjs`](./scripts/run-acceptance-tests.mjs), which bundles the modified service/resolver and the agent's `.spec.ts` suite with `esbuild` and executes the real NestJS `Test.createTestingModule` test cases:

```powershell
node benchmarks/08-multi-task-architecture-suite-on-twenty/scripts/run-acceptance-tests.mjs
```

All `8 / 8` patches (`4` `CBM` + `4` `CodeExplorer`) compiled cleanly and passed `100%` of their unit test suites (`14/14` unit tests for `CBM`, `14/14` unit tests for `CodeExplorer`).

---

## Reproducibility & Artifacts

- **System Prompts**:
  - [`prompts/ce-agent.md`](./prompts/ce-agent.md) (CodeExplorer Agent)
  - [`prompts/cbm-agent.md`](./prompts/cbm-agent.md) (CBM Baseline Agent)
- **Task Prompts**:
  - [`prompts/task-01-prompt.md`](./prompts/task-01-prompt.md)
  - [`prompts/task-02-prompt.md`](./prompts/task-02-prompt.md)
  - [`prompts/task-03-prompt.md`](./prompts/task-03-prompt.md)
  - [`prompts/task-04-prompt.md`](./prompts/task-04-prompt.md)
- **Frozen Gate Definitions**: [`prompts/gates.yaml`](./prompts/gates.yaml)
- **Acceptance Test Runner**: [`scripts/run-acceptance-tests.mjs`](./scripts/run-acceptance-tests.mjs)
- **Machine-Readable Telemetry**: [`results/metrics.json`](./results/metrics.json)
