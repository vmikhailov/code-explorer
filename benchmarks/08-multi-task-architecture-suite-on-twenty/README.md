# Case Study 08: 4-Task Brownfield Architecture & Feature Suite (`CodeExplorer` vs `codebase-memory-mcp` on `twentyhq/twenty`)

- **Target Repository**: [`twentyhq/twenty`](https://github.com/twentyhq/twenty) (`20,594` source files, `~2.5M` LOC, `46` projects, `318,389` graph nodes, `742,080` graph relationships — pinned at commit `12e8fae4`)
- **Evaluated Agent Configurations (`4 Tasks × 3 Agents = 12 Autonomous Runs` + `CE Phase 1 / Phase 2` Ablation)**:
  1. **`CBM` (`codebase-memory-mcp v0.6.1`)**: Codebase Memory MCP + standard `twentyhq/twenty` `CLAUDE.md` ([`prompts/cbm-agent.md`](./prompts/cbm-agent.md)).
  2. **`CBM + Augmented CLAUDE.md` (`codebase-memory-mcp v0.6.1` + 40-Rule `CLAUDE.md`)**: Codebase Memory MCP where all 4 architectural rules are explicitly written in prose inside an enterprise 40-rule `CLAUDE.md` ([`prompts/claude-md-augmented.md`](./prompts/claude-md-augmented.md), [`prompts/cbm-claude-md-agent.md`](./prompts/cbm-claude-md-agent.md)).
  3. **`CE` (`CodeExplorer v1.79.0`)**: CodeExplorer CLI + Semantic Invariant Gates ([`prompts/ce-agent.md`](./prompts/ce-agent.md)), split into:
     - **`CE Phase 1` (Navigation Only — before `ce gate check`)**: Isolates the effect of graph navigation alone prior to running Semantic Invariant Gates.
     - **`CE Phase 2` (Navigation + Semantic Invariant Gates — after `ce gate check`)**: Measures the closed-loop effect of deterministic ratchet gates (`ce scan && ce gate check`).
- **Model**: `Gemini 3.8 Flash` (`12` independent fresh-context subagent runs, `0` context compactions)

---

## 1. Why a 4-Task Ablation Matrix?

In [Case Study 07](../07-agent-feature-ce-vs-cbm-on-twenty/README.md), we demonstrated on a single task how an AI coding agent copies an adjacent brownfield shortcut unless stopped by a Semantic Invariant Gate. A rigorous evaluation requires answering four deeper methodological questions:

1. **Across multiple tasks and rule types, how often do agents copy adjacent brownfield debt?**
2. **Does better code graph navigation alone (`CE Phase 1`) prevent violations, or are deterministic gates (`CE Phase 2`) strictly required?**
3. **When `ce gate check` fails, does the agent fix the architecture properly, or does it attempt to bypass/tamper with the gate (`ce gate ratchet --update`, editing `.yaml` / `.json`) or break unit tests?**
4. **What happens if you simply write the architectural rules in `CLAUDE.md` (`CBM + Augmented CLAUDE.md`)?**

To answer all four questions empirically, we froze **4 repository-wide Semantic Invariant Gates** ([`gates/architectural/`](./gates/architectural/)) and their **ratchet baselines** ([`gates/baselines/`](./gates/baselines/)) on clean commit `12e8fae4` (`256` tolerated legacy violations across `twenty-server`, `0` new violations) and ran **4 real-world backend feature tasks** across all conditions.

---

## 2. The 4 Brownfield Tasks & Adjacent Legacy Traps

Every task in the suite is a realistic NestJS/GraphQL backend feature inside `packages/twenty-server` where the nearest existing method in the same file or sibling service contains a **tolerated (`[TOL]`) legacy shortcut**:

| Task | Feature & Target Symbol | Repo-Wide Gate & Ratchet Baseline (`12e8fae4`) | Adjacent `[TOL]` Legacy Trap in `twenty-server` |
| :--- | :--- | :--- | :--- |
| **[Task 01](./prompts/task-01-prompt.md)** | Admin reset of a target user's 2FA methods (`TwoFactorAuthenticationResolver.resetTwoFactorAuthenticationMethodsForUser`) | **`SIG-TWENTY-001`**: GraphQL Resolver & Controller Transport Purity (`5` tolerated) | `deleteTwoFactorAuthenticationMethod` (`two-factor-authentication.resolver.ts:215`) calls `this.twoFactorAuthenticationMethodRepository.delete(...)` directly inside the GraphQL resolver instead of delegating to `TwoFactorAuthenticationService`. |
| **[Task 02](./prompts/task-02-prompt.md)** | Flag a connected account to show the reconnect banner (`TimelineMessagingResolver.triggerReconnectAccountBanner`) | **`SIG-TWENTY-002`**: Core-Engine to Feature-Modules Isolation (`101` tolerated) | `dismissReconnectAccountBanner` (`timeline-messaging.resolver.ts:282`) in `src/engine/core-modules/messaging/` directly calls `AccountsToReconnectService` in `src/modules/connected-account/` instead of `UserVarsService` in `src/engine/core-modules/user/user-vars/`. |
| **[Task 03](./prompts/task-03-prompt.md)** | Admin panel workspace suspension (`AdminPanelBillingService.suspendWorkspace`) | **`SIG-TWENTY-003`**: Cross-Module Table Encapsulation (`150` tolerated) | `WorkspaceService.deleteWorkspace` (`workspace.service.ts:989`) directly queries `EmailingDomainEntity` (`emailingDomain` table) across `core-modules/` boundaries instead of calling `EmailingDomainService.getEmailingDomains()`. |
| **[Task 04](./prompts/task-04-prompt.md)** | Prevent subdomain collisions with registered public domains (`SubdomainManagerService.isSubdomainAvailable`) | **`SIG-TWENTY-003`**: Cross-Module Table Encapsulation (`150` tolerated) | `CustomDomainManagerService` (`custom-domain-manager.service.ts:29`) and `WorkspaceDomainsService` (`workspace-domains.service.ts:26`) in `core-modules/domain/` directly inject `@InjectRepository(PublicDomainEntity)` from `core-modules/public-domain/` instead of calling `PublicDomainService.findByDomain()`. |

---

## 3. Suite-Wide Ablation Scorecard (`4 Tasks Combined`)

| Metric | `CBM` (Default `twenty` `CLAUDE.md`) | `CBM + Augmented CLAUDE.md` (40-Rule Prose) | `CE Phase 1` (Graph Navigation Only, Pre-Gate) | `CE Phase 2` (Graph Navigation + Semantic Gates) | `CE` vs `CBM` Outcome |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Tasks Introducing Violation Pre-Gate (`Phase 1`)** | `4 / 4` (`100%`) | `0 / 4` (`0%`) | `4 / 4` (`100%`) | `4 / 4` (`100%` caught) | Navigation alone copies adjacent `[TOL]` code `100%` of the time |
| **Tasks Shipping Final Architectural Violations** | **`4 / 4` (`100%`)** | **`0 / 4` (`0%`)** | `4 / 4` (`100%`) | **`0 / 4` (`0%`)** | **`0` vs `4` violated tasks (`100%` prevented)** |
| **Total Final `[NEW]` Gate Violations** | **`4`** | **`0`** | `5` (pre-gate) | **`0`** | **`4/4` gates PASS (`256` `[TOL]`, `0` `[NEW]`)** |
| **Self-Correction Iterations Required** | N/A | N/A | N/A | **`1` iteration per task** | Fixed on first `ce gate check` in `4/4` tasks |
| **Gate Bypass / Tampering Attempts** | N/A | N/A | N/A | **`0 / 4` (`0%`)** | Zero attempts to run `ratchet --update` or edit gates |
| **Unit Tests Passing (Verified Execution)** | **`14 / 14` (`100%`)** (`4/4` tasks) | **`12 / 13` (`92.3%`)** (`3/4` tasks) | — | **`14 / 14` (`100%`)** (`4/4` tasks) | **`CE` passes `100%` of unit tests AND `100%` of gates** |
| **Total Wall-Clock Time (All 4 Tasks)** | `2,646 s` (`44m 06s`) | `2,457 s` (`40m 57s`) | — | **`2,054 s` (`34m 14s`)** | **`1.29×` faster than `CBM` (`-9m 52s`) / `1.20×` faster than `CBM+CLAUDE.md`** |
| **Total LLM Turns** | `127` | `110` | — | **`125`** | Includes incremental `ce scan` + `ce gate check` + refactor |
| **Total Source File Reads (`read_file`)** | `96` | `98` | — | **`92`** | Fewer file reads despite refactoring all 4 tasks |
| **Total Input Tokens** | `10,030,169` (`10.0M`) | `9,456,303` (`9.5M`) | — | **`9,745,267` (`9.7M`)** | Comparable token footprint (`$0.48` vs `$0.48`) |

---

## 4. Per-Task Breakdown Across All 12 Runs

| Task | Gate (`[TOL]` Baseline) | `CBM` Time / Reads / Gate / Tests | `CBM + CLAUDE.md` Time / Reads / Gate / Tests | `CE` Time / Reads / Pre-Gate $\to$ Post-Gate / Tests | Speedup (`CE` vs `CBM`) |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **Task 01**: `resetTwoFactorAuthenticationMethodsForUser` | `SIG-TWENTY-001` (`5 tol`) | `424 s` (`7m 04s`) · `19` reads<br>**`[FAIL]` (`1 new`)** · `3/3` tests pass | `440 s` (`7m 20s`) · `14` reads<br>**`[PASS]` (`0 new`)** · **`2/3` tests pass (`1 FAIL`)** | **`508 s` (`8m 28s`)** · `19` reads<br>`1 new` $\to$ **`[PASS]` (`0 new`, 1 iter)** · **`3/3` tests pass** | `0.83×` (due to service refactor) |
| **Task 02**: `triggerReconnectAccountBanner` | `SIG-TWENTY-002` (`101 tol`) | `455 s` (`7m 35s`) · `20` reads<br>**`[FAIL]` (`1 new`)** · `4/4` tests pass | `572 s` (`9m 32s`) · `23` reads<br>**`[PASS]` (`0 new`)** · `3/3` tests pass | **`384 s` (`6m 24s`)** · `20` reads<br>`1 new` $\to$ **`[PASS]` (`0 new`, 1 iter)** · **`3/3` tests pass** | **`1.18×` faster** (`1.49×` vs `CLAUDE.md`) |
| **Task 03**: `suspendWorkspaceAdminPanel` | `SIG-TWENTY-003` (`150 tol`) | `1,225 s` (`20m 25s`) · `43` reads<br>**`[FAIL]` (`1 new`)** · `3/3` tests pass | `884 s` (`14m 44s`) · `39` reads<br>**`[PASS]` (`0 new`)** · `3/3` tests pass | **`779 s` (`12m 59s`)** · `31` reads<br>`2 new` $\to$ **`[PASS]` (`0 new`, 1 iter)** · **`4/4` tests pass** | **`1.57×` faster** (`1.13×` vs `CLAUDE.md`) |
| **Task 04**: `SubdomainManagerService.isSubdomainAvailable` | `SIG-TWENTY-003` (`150 tol`) | `542 s` (`9m 02s`) · `14` reads<br>**`[FAIL]` (`1 new`)** · `4/4` tests pass | `561 s` (`9m 21s`) · `22` reads<br>**`[PASS]` (`0 new`)** · `4/4` tests pass | **`383 s` (`6m 23s`)** · `22` reads<br>`1 new` $\to$ **`[PASS]` (`0 new`, 1 iter)** · **`4/4` tests pass** | **`1.42×` faster** (`1.46×` vs `CLAUDE.md`) |
| **Suite Total (`4 Tasks`)** | **`4` Repo-Wide Gates (`256 tol`)** | **`2,646 s` (`44m 06s`) · `96` reads**<br>**`0 / 4` Gates Pass · `14/14` Tests** | **`2,457 s` (`40m 57s`) · `98` reads**<br>**`4 / 4` Gates Pass · `12/13` Tests (`1 FAIL`)** | **`2,054 s` (`34m 14s`) · `92` reads**<br>**`4 / 4` Gates Pass · `14/14` Tests** | **`1.29×` faster than `CBM`**<br>**`1.20×` faster than `CLAUDE.md`** |

---

## 5. Deep-Dive: What Happened in Each Task

### Task 01: `TwoFactorAuthenticationResolver.resetTwoFactorAuthenticationMethodsForUser` (`SIG-TWENTY-001`)
- **The Legacy Trap**: In `two-factor-authentication.resolver.ts:215`, the existing `deleteTwoFactorAuthenticationMethod` mutation directly executes `await this.twoFactorAuthenticationMethodRepository.delete(...)` inside the GraphQL resolver (`[TOL]` in `SIG-TWENTY-001`).
- **`CBM` (`424s`)**: Copied `deleteTwoFactorAuthenticationMethod` line-by-line and added `await this.twoFactorAuthenticationMethodRepository.delete({ userWorkspaceId: targetUserWorkspace.id })` directly inside `TwoFactorAuthenticationResolver`. **`SIG-TWENTY-001` Result**: `[FAIL] (1 new, 5 tolerated)`.
- **`CE` (`508s`)**:
  - **Phase 1 (Step 38)**: Also copied `deleteTwoFactorAuthenticationMethod` and placed `.delete(...)` in `TwoFactorAuthenticationResolver`.
  - **Phase 2 (Step 46)**: Ran `ce scan && ce gate check`, which failed with:
    ```text
    [FAIL] SIG-TWENTY-001  GraphQL Resolver & Controller Transport Purity (1 new, 5 tolerated, 0ms)
      [NEW] two-factor-authentication.resolver.ts TwoFactorAuthenticationResolver -> twoFactorAuthenticationMethod
    ```
  - **Self-Correction (1 Iteration)**: Moved the repository deletion into `TwoFactorAuthenticationService.resetTwoFactorAuthenticationMethodsForUser`, delegated from the resolver, re-ran `ce scan && ce gate check` (`4/4 PASS`), and passed **`3/3` unit tests**.
- **`CBM + Augmented CLAUDE.md` (`440s`)**: Followed the prose rule in `CLAUDE.md` and delegated to `TwoFactorAuthenticationService`, passing `SIG-TWENTY-001`, **but broke its 3rd unit test (`2/3` passed)**: because it skipped reading `access-policy.guard.ts`, it hallucinated `new resetGuards[0](accessPolicyService).canActivate(context)` expecting `accessPolicyService.checkAccessPolicy(...)`, whereas `AccessPolicyGuard` actually calls `await this.accessPolicyService.assertRequestAccessPolicySatisfiedOrThrow(...)`.

---

### Task 02: `TimelineMessagingResolver.triggerReconnectAccountBanner` (`SIG-TWENTY-002`)
- **The Legacy Trap**: In `timeline-messaging.resolver.ts:282`, `dismissReconnectAccountBanner` inside `src/engine/core-modules/messaging/` directly injects and calls `AccountsToReconnectService` from `src/modules/connected-account/` (`[TOL]` in `SIG-TWENTY-002: Core-Engine to Feature-Modules Isolation`).
- **`CBM` (`455s`)**: Called `this.accountsToReconnectService.addAccountToReconnectByKey(...)` directly from `TimelineMessagingResolver`, creating a new upward dependency from `src/engine/core-modules/messaging/` into `src/modules/connected-account/`. **`SIG-TWENTY-002` Result**: `[FAIL] (1 new, 101 tolerated)`.
- **`CE` (`384s`)**:
  - **Phase 1 (Step 27)**: Initially wired `this.accountsToReconnectService.addAccountToReconnectByKey(...)` in `TimelineMessagingResolver`.
  - **Phase 2 (Step 31)**: `ce gate check` caught `[NEW] timeline-messaging.resolver.ts TimelineMessagingResolver -> AccountsToReconnectService`.
  - **Self-Correction (1 Iteration)**: Imported `UserVarsModule` (`src/engine/core-modules/user/user-vars/`) into `TimelineMessagingModule` and used `UserVarsService` directly inside `core-modules`, eliminating the upward `engine -> modules` dependency in 1 iteration (`384s` total — **`1.18×` faster** than `CBM` and **`1.49×` faster** than `CBM + CLAUDE.md`).

---

### Task 03: `AdminPanelBillingService.suspendWorkspace` (`SIG-TWENTY-003`)
- **The Legacy Trap**: `WorkspaceService.deleteWorkspace` (`workspace.service.ts:989`) directly queries `EmailingDomainEntity` (`emailingDomain` table) across `core-modules/` boundaries (`[TOL]` in `SIG-TWENTY-003`), and `AdminPanelBillingService` already injects `WorkspaceEntity` and `BillingCustomerEntity` repositories (`[TOL]`).
- **`CBM` (`1,225s` / `20m 25s`)**: Spent `43` file reads exploring billing and workspace services, then copied `workspace.service.ts:989` and injected `@InjectWorkspaceScopedRepository(EmailingDomainEntity)` directly into `AdminPanelBillingService`. **`SIG-TWENTY-003` Result**: `[FAIL] (1 new, 150 tolerated)`.
- **`CE` (`779s` / `12m 59s`)**:
  - **Phase 1**: Initially injected `@InjectWorkspaceScopedRepository(EmailingDomainEntity)` into `AdminPanelBillingService`.
  - **Phase 2**: `ce gate check` flagged `[NEW] admin-panel-billing.service.ts AdminPanelBillingService -> emailingDomain`.
  - **Self-Correction (1 Iteration)**: Imported `EmailingDomainModule` into `AdminPanelModule` and injected `EmailingDomainService.getEmailingDomains(workspace)`, passing `4/4` gates and `4/4` unit tests in **`779s`** (**`1.57×` faster** than `CBM`).

---

### Task 04: `SubdomainManagerService.isSubdomainAvailable` (`SIG-TWENTY-003`)
- **The Legacy Trap**: Both sibling services in `src/engine/core-modules/domain/` — `CustomDomainManagerService` (`custom-domain-manager.service.ts:29`) and `WorkspaceDomainsService` (`workspace-domains.service.ts:26`) — directly inject `@InjectRepository(PublicDomainEntity)` from `src/engine/core-modules/public-domain/` (`[TOL]` in `SIG-TWENTY-003`).
- **`CBM` (`542s`)**: Copied `CustomDomainManagerService` and injected `@InjectRepository(PublicDomainEntity)` into `SubdomainManagerService` and `TypeOrmModule.forFeature([WorkspaceEntity, PublicDomainEntity])` into `SubdomainManagerModule`. **`SIG-TWENTY-003` Result**: `[FAIL] (1 new, 150 tolerated)`.
- **`CE` (`383s`)**:
  - **Phase 1 (Step 45)**: Copied `CustomDomainManagerService` and injected `@InjectRepository(PublicDomainEntity)` into `SubdomainManagerService`.
  - **Phase 2 (Step 59)**: `ce gate check` caught `[NEW] subdomain-manager.service.ts SubdomainManagerService -> publicDomain`.
  - **Self-Correction (1 Iteration)**: Replaced `TypeOrmModule.forFeature([PublicDomainEntity])` with `PublicDomainModule` in `SubdomainManagerModule` and injected `PublicDomainService.findByDomain(candidateDomain)`, passing `4/4` gates and `4/4` unit tests in **`383s`** (**`1.42×` faster** than `CBM` and **`1.46×` faster** than `CBM + CLAUDE.md`).

---

## 6. Key Takeaways: Navigation vs Prose (`CLAUDE.md`) vs Semantic Gates

1. **Graph Navigation Speeds Up Discovery (`1.29×`), But Cannot Prevent Pattern-Matching on Legacy Debt (`4/4` Phase 1 Violations)**:
   - Even with accurate graph navigation (`CE Phase 1`), an LLM implementing a feature in a brownfield codebase looks at the nearest existing method in the same file or module and matches its pattern. Because `twentyhq/twenty`'s actual `CLAUDE.md` explicitly instructs agents on line 5 to *"Match the surrounding code — the adjacent files in the directory you are editing beat any written rule"*, both `CBM` and `CE Phase 1` reproduced the adjacent `[TOL]` shortcut in **4 out of 4 tasks (`100%`)**.
2. **Why `CLAUDE.md` Prose Alone Is Not Enough in Production**:
   - When we hand-crafted [`claude-md-augmented.md`](./prompts/claude-md-augmented.md) with explicit prose rules matching the 4 tasks, `CBM + Augmented CLAUDE.md` avoided the 4 architectural traps, **but**:
     - **Zero deterministic enforcement**: Prose in `CLAUDE.md` cannot block a pull request in CI, cannot distinguish `256` existing legacy exceptions (`[TOL]`) from `1` new violation (`[NEW]`), and silently degrades as `CLAUDE.md` grows across dozens of domains.
     - **Higher latency and test fragility**: `CBM + Augmented CLAUDE.md` took **`2,457s` (`40m 57s`)** vs **`2,054s` (`34m 14s`)** for `CE` (`1.20×` slower) and shipped a broken unit test in Task 01 (`12/13` tests passing vs `14/14` for `CE`).
3. **Closed-Loop Ratchet Gates Guarantee `0` Architectural Drift with `0` Gate Tampering**:
   - Across all 4 tasks, `ce scan && ce gate check` pinpointed the exact `[NEW]` coupling edge while tolerating all `256` pre-existing `[TOL]` violations, enabling `CE` to refactor to the proper domain service in **1 iteration per task**, with **0 gate-tampering attempts** and **`14/14` (`100%`) passing unit tests**.

---

## 7. Reproducibility & Verbatim Artifacts

- **Frozen Repo-Wide Gates & Ratchet Baselines (`12e8fae4`)**:
  - [`gates/architectural/`](./gates/architectural/) (`SIG-TWENTY-001`..`004` YAML definitions)
  - [`gates/baselines/`](./gates/baselines/) (`SIG-TWENTY-001`..`004` JSON ratchet baselines)
- **Prompts**:
  - Agent System Prompts: [`prompts/ce-agent.md`](./prompts/ce-agent.md), [`prompts/cbm-agent.md`](./prompts/cbm-agent.md), [`prompts/cbm-claude-md-agent.md`](./prompts/cbm-claude-md-agent.md), [`prompts/claude-md-augmented.md`](./prompts/claude-md-augmented.md)
  - Shared Task Prompts: [`prompts/task-01-prompt.md`](./prompts/task-01-prompt.md), [`prompts/task-02-prompt.md`](./prompts/task-02-prompt.md), [`prompts/task-03-prompt.md`](./prompts/task-03-prompt.md), [`prompts/task-04-prompt.md`](./prompts/task-04-prompt.md)
- **Per-Task Verbatim Agent Outputs, Git Patches & Unit Test Specs**:
  - **Task 01**: [`results/task-01/`](./results/task-01/) (`ce-agent.patch`, `cbm-agent.patch`, `cbm-claude-md-agent.patch`, `*.spec.ts`, `*-output.md`)
  - **Task 02**: [`results/task-02/`](./results/task-02/) (`ce-agent.patch`, `cbm-agent.patch`, `cbm-claude-md-agent.patch`, `*.spec.ts`, `*-output.md`)
  - **Task 03**: [`results/task-03/`](./results/task-03/) (`ce-agent.patch`, `cbm-agent.patch`, `cbm-claude-md-agent.patch`, `*.spec.ts`, `*-output.md`)
  - **Task 04**: [`results/task-04/`](./results/task-04/) (`ce-agent.patch`, `cbm-agent.patch`, `cbm-claude-md-agent.patch`, `*.spec.ts`, `*-output.md`)
- **Aggregated Machine-Readable Telemetry**:
  - [`results/metrics.json`](./results/metrics.json)
