# Deterministic Gate Controls & Mutation Verification (`SIG-TWENTY-001` .. `SIG-TWENTY-004`)

This document records the 5 deterministic control runs verifying that `SIG-TWENTY-003` (**Cross-Module Table Encapsulation**) is a general, repository-wide architectural rule across `packages/twenty-server/src/engine/core-modules/` rather than a task-specific trap.

All 4 gates (`gates/architectural/*.yaml`) and their SHA-256 baseline snapshots (`gates/baselines/*.json`) contain **zero hardcoded module names and zero hardcoded table names**.

---

## Summary of the 5 Controls

| # | Control Target | `oxlint` Result | `ce gate check` Result | What It Proves |
| :- | :--- | :--- | :--- | :--- |
| **1** | **Clean baseline (`twenty @ 12e8fae4`)** | `0 errors` | **PASS** (`0 new`, `150 tolerated` in `SIG-TWENTY-003`) | Existing cross-module couplings across `core-modules/`—including the legacy shortcut `WorkspaceService -> emailingDomain` at `workspace.service.ts:141`—are frozen in the baseline ratchet as `[TOL]`. |
| **2** | **`ce-agent.patch` (Final CodeExplorer patch)** | `0 errors, 0 warnings` | **PASS** (`0 new`, `150 tolerated`) | Delegating to `EmailingDomainService.getEmailingDomains(workspace)` via `EmailingDomainModule` satisfies all 4 gates cleanly. |
| **3** | **`cbm-agent.patch` (Final CBM patch)** | `0 errors, 2 warnings` (Exit `0`) | **FAIL** (`1 new`: `AdminPanelBillingService -> emailingDomain`) | Copying the legacy shortcut via `@InjectWorkspaceScopedRepository(EmailingDomainEntity)` is missed by `oxlint` (`0` errors) and `CBM`, but caught by `SIG-TWENTY-003`. |
| **4** | **Mutation: `coreDataSource.getRepository(EmailingDomainEntity)` without module entity registration** | `0 errors, 0 warnings` (Exit `0`) | **FAIL** (`1 new`: `AdminPanelBillingService -> emailingDomain`) | Proves `SIG-TWENTY-003` checks semantic `READS_FROM\|WRITES_TO` edges to the `Table` node, **not** NestJS decorators or `AdminPanelModule` imports. Even when `coreDataSource.getRepository(...)` is used directly (where `oxlint` emits `0` errors and `0` warnings), `SIG-TWENTY-003` catches the table access. |
| **5** | **Control Feature: Intra-module table query in `BillingSubscriptionService`** | `0 errors, 0 warnings` (Exit `0`) | **PASS** (`0 new`, `150 tolerated`) | Adding a new method in `src/engine/core-modules/billing/` that queries `billingSubscription` (owned by `billing`) produces zero false positives because `split(c.path, '/')[5] = split(e.path, '/')[5] = 'billing'`. |

---

## Verbatim Output: Control 1 (Clean `12e8fae4` Baseline)

```text
$ ce gate check ./repos/twenty

--- Semantic Invariant Gates Evaluation ---

STATUS         ID              CATEGORY        PLATFORM   SCOPE     VIOLATIONS      NAME
---------------------------------------------------------------------------------------------------------
[TOLERATED]    SIG-TWENTY-001  Architectural   agnostic   Class     5 tol           GraphQL Resolver & Controller Transport Purity
[TOLERATED]    SIG-TWENTY-002  Architectural   agnostic   Module    101 tol         Core-Engine to Feature-Modules Isolation
[TOLERATED]    SIG-TWENTY-003  Architectural   agnostic   Module    150 tol         Cross-Module Table Encapsulation
[PASS]         SIG-TWENTY-004  Architectural   agnostic   Method    0               Async Message Queue Boundary (No Direct Job .handle() Invocation)
---------------------------------------------------------------------------------------------------------
Total: 4 gates | Passed: 4 | Warnings: 0 | Failed: 0 | Duration: 1359ms

All architectural semantic gates passed successfully.
```

---

## Verbatim Output: Control 3 (`cbm-agent.patch`)

Notice how `WorkspaceService -> emailingDomain` (the existing legacy shortcut in `workspace.service.ts`) appears as `[TOL]`, while the agent's newly copied coupling `AdminPanelBillingService -> emailingDomain` appears immediately below it as `[NEW]`:

```text
$ ce gate details -g SIG-TWENTY-003 ./repos/twenty

--- Semantic Invariant Gate Details ---
[FAIL] SIG-TWENTY-003 [Architectural/agnostic] - Cross-Module Table Encapsulation (402ms)
  Violations (1 new, 150 tolerated) | Showing 151 of 151:
  STATUS | LOCATION                                                                                                | SYMBOL                   | TARGET / CALL
  -------+---------------------------------------------------------------------------------------------------------+--------------------------+---------------
  [TOL]  | packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:37:8 | AdminPanelBillingService | billingCustomer
  [TOL]  | packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:37:8 | AdminPanelBillingService | billingPrice
  [TOL]  | packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts:141:8            | WorkspaceService         | emailingDomain
  [NEW]  | packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:46:8 | AdminPanelBillingService | emailingDomain
  [TOL]  | packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:37:8 | AdminPanelBillingService | workspace
```

---

## Verbatim Output: Control 4 (`coreDataSource.getRepository(EmailingDomainEntity)` Mutation)

When `AdminPanelBillingService` queries `this.coreDataSource.getRepository(EmailingDomainEntity).find({ where: { workspaceId } })` directly (the exact pattern in `workspace.service.ts:989`, without `@InjectWorkspaceScopedRepository` and without registering `EmailingDomainEntity` in `AdminPanelModule`), `SIG-TWENTY-003` still catches the `READS_FROM` edge to `emailingDomain`:

```text
$ ce gate details -g SIG-TWENTY-003 ./repos/twenty

--- Semantic Invariant Gate Details ---
[FAIL] SIG-TWENTY-003 [Architectural/agnostic] - Cross-Module Table Encapsulation (392ms)
  Violations (1 new, 150 tolerated) | Showing 151 of 151:
  STATUS | LOCATION                                                                                                | SYMBOL                   | TARGET / CALL
  -------+---------------------------------------------------------------------------------------------------------+--------------------------+---------------
  [TOL]  | packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts:141:8            | WorkspaceService         | emailingDomain
  [NEW]  | packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts:48:8 | AdminPanelBillingService | emailingDomain
```
