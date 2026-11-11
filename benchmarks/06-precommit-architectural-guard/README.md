# Case Study 06: Automated Pre-Commit Architectural Guard (Showcase)

Automated pre-commit / pull-request architectural quality guard evaluation on the open-source Kubernetes continuous delivery platform [`argoproj/argo-cd`](https://github.com/argoproj/argo-cd) (`18` subsystems, `4,216` files, `45,616` graph nodes, `134,005` relationships) using **CodeExplorer Semantic Invariant Gates (`ce gate check` / `ce gate details`)** to enforce 6 core architectural invariant rules.

---

## 1. The Engineering Challenge: Architecture as Code

In modern software development, engineering teams strive for continuous architectural governance ("Fitness Functions" / "Architecture as Code"). Teams want to enforce hard structural constraints during local git pre-commit hooks and CI Pull-Request checks:
- No reverse layer inversions (`util/` calling higher-level business services).
- No monolithic "God Structs" exceeding complexity thresholds.
- Strict package dependency DAG compliance (zero cyclic imports).
- Strict storage layer encapsulation (raw secret manipulation restricted to data access layers).
- Decoupling of background notification utilities from direct database instantiation.
- Explicit audit and allowlisting of unauthenticated authentication overrides.

### The CodeExplorer Solution (`ce gate check`)
CodeExplorer provides native **Semantic Invariant Gates (SIG)**:
- Pre-compiled OpenCypher structural invariant rules with threshold ratchets.
- Evaluated deterministically via `ce gate check` in **398 milliseconds** across the entire repository graph.
- Zero LLM tokens, $0.00 cost, and deterministic binary exit codes (`0` for PASS, `1` for FAIL) suitable for pre-commit git hooks and GitHub Actions.

---

## 2. The 6 Pre-Commit Architectural Invariant Rules

| Gate ID | Invariant Rule | Scope & Dimension | Target Subsystems | Result | Violations |
| :--- | :--- | :--- | :--- | :---: | :---: |
| **SIG-P-B-101** | **Reverse Layer Inversion Shield** | Module $\times$ Boundaries | `util/**` calling `server/`, `controller/`, `reposerver/` | **FAIL** | 104 call sites |
| **SIG-C-R-102** | **God Struct Complexity Limit** | Class $\times$ Resources | Production structs (>30 methods) | **FAIL** | 12 structs |
| **SIG-P-I-103** | **Package Dependency DAG Integrity** | Module $\times$ Rules | Package import relationships (zero cycles) | **PASS** | 0 cycles |
| **SIG-P-B-104** | **Storage Abstraction Boundary** | Module $\times$ Boundaries | Raw secret backend (`repository_secrets.go`) | **PASS** | 0 external calls |
| **SIG-P-B-105** | **Notification Subsystem DB Decoupling** | Module $\times$ Boundaries | `util/notification` direct `db.NewDB` calls | **FAIL** | 2 call sites |
| **SIG-M-B-106** | **Authentication Bypass Audit** | Method $\times$ Boundaries | Server endpoints implementing `AuthFuncOverride` | **WARN** | 4 endpoints |

**Outcome**: Evaluated all 6 rules in **398 ms** with **0 tokens** &rarr; Deterministic Exit Code **`1`** (Pull Request Blocked).

---

## 3. Evaluation Telemetry Scorecard

Measured on `argoproj/argo-cd @ 6d689fa3` ([`results/metrics.json`](./results/metrics.json)):

| Gate ID | Rule Name | Latency | Result | Action |
| :--- | :--- | :---: | :---: | :--- |
| **SIG-P-B-101** | Reverse Layer Inversion Shield | 74 ms | ❌ FAIL | Block merge (104 illegal cross-layer calls) |
| **SIG-C-R-102** | God Struct Complexity Limit | 62 ms | ❌ FAIL | Block merge (12 structs exceed 30 methods) |
| **SIG-P-I-103** | Package Dependency DAG Integrity | 68 ms | ✅ PASS | Compliant (acyclic directed graph) |
| **SIG-P-B-104** | Storage Abstraction Boundary | 48 ms | ✅ PASS | Compliant (secrets access fully encapsulated) |
| **SIG-P-B-105** | Notification Subsystem DB Decoupling | 51 ms | ❌ FAIL | Block merge (2 direct `db.NewDB` instantiations) |
| **SIG-M-B-106** | Public Authentication Bypass Audit | 55 ms | ⚠️ WARN | Audit required (4 legitimate public bypasses) |
| **Total** | **All 6 Invariant Gates (Parallel Engine)** | **398 ms** | ❌ **FAIL** | **Exit Code 1 (PR Blocked)** |

---

## 4. Source-Verified Findings Breakdown

### Rule 1: Reverse Layer Leaks (SIG-P-B-101) &mdash; FAIL
Identified 104 cross-layer call boundaries where foundational utility code reaches into higher-level business services:
- `util/webhook/webhook.go` &rarr; `reposerver/cache` and `server/cache`
- `util/git/client.go` &rarr; `reposerver/cache.GetOrLockGitReferences`, `SetGitReferences`
- `util/helm/client.go` &rarr; `reposerver/cache.GetHelmIndex`, `SetHelmIndex`
- `util/session/sessionmanager.go` &rarr; `server/rbacpolicy.GetProjectRoleFromSubject`
- `util/notification/argocd/service.go` &rarr; `reposerver/apiclient`
- `util/manifeststream/stream.go` &rarr; `reposerver/apiclient`
- `util/app/discovery/discovery.go` &rarr; `cmpserver/plugin`, `cmpserver/apiclient`

### Rule 2: God Structs & Complexity Limit (SIG-C-R-102) &mdash; FAIL
12 production structs exceed the 30-method threshold:
1. `SettingsManager` (`util/settings/settings.go`): **70 methods**
2. `ApplicationController` (`controller/appcontroller.go`): **64 methods**
3. `client` (`pkg/apiclient/apiclient.go`): **61 methods**
4. `Server` (`server/application/application.go`): **59 methods**
5. `nativeGitClient` (`util/git/client.go`): **50 methods**
6. `store` (`gitops-engine/pkg/cache/cluster.go`): **44 methods**
7. `ArgoCDSettings` (`util/settings/settings.go`): **38 methods**
8. `ApplicationsService` (`ui/src/app/shared/services/applications-service.ts`): **36 methods**
9. `db` (`util/db/repository.go`): **35 methods**
10. `Service` (`reposerver/repository/repository.go`): **35 methods**
11. `syncContext` (`gitops-engine/pkg/sync/sync_context.go`): **35 methods**
12. `Cache` (`reposerver/cache/cache.go`): **34 methods**

### Rule 3: Package Dependency DAG Integrity (SIG-P-I-103) &mdash; PASS
Confirmed **0 circular package dependencies** in production Go code.

### Rule 4: Storage Abstraction Boundary (SIG-P-B-104) &mdash; PASS
Confirmed **0 direct invocations** of `repository_secrets.go` outside `util/db`.

### Rule 5: Notification Subsystem DB Decoupling (SIG-P-B-105) &mdash; FAIL
Identified the exact 2 violation sites where `util/notification` constructs standalone database connections:
- `util/notification/argocd/service.go:66`: `GetCommitMetadata` calling `db.NewDB`
- `util/notification/argocd/service.go:89`: `GetAppDetails` calling `db.NewDB`

### Rule 6: Public Authentication Bypass Audit (SIG-M-B-106) &mdash; AUDIT / WARN
Identified the exact 4 legitimate public entry points implementing `AuthFuncOverride`:
1. `server/session/session.go:103`: `session.Server` (anonymous login / session creation)
2. `server/settings/settings.go:202`: `settings.Server` (public login UI config)
3. `server/version/version.go:76`: `version.Server` (anonymous CLI version query)
4. `server/server.go:969`: `healthServer` (Kubernetes liveness/readiness probes)

---

## 5. Declarative Gate Definitions & Artifacts

- **YAML Gate Suite**: [`gates/architectural-gates.yaml`](./gates/architectural-gates.yaml)
- **Shared Task Prompt**: [`prompts/task-prompt.md`](./prompts/task-prompt.md)
- **Detailed Evaluator Report**: [`results/ce-agent-output.md`](./results/ce-agent-output.md)
- **Telemetry Data**: [`results/metrics.json`](./results/metrics.json)
