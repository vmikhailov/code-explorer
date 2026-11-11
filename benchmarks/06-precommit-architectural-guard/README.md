# Case Study 06: Automated Pre-Commit Architectural Guard (Showcase)

Automated pre-commit / pull-request architectural quality guard showcase on the open-source Kubernetes continuous delivery platform [`argoproj/argo-cd`](https://github.com/argoproj/argo-cd) (`18` subsystems, `4,216` files, `45,616` graph nodes, `134,005` relationships) using **CodeExplorer Semantic Invariant Gates (`ce gate check` / `ce gate details`)** to enforce 6 production architectural invariant rules.

---

## 1. The Engineering Challenge: Architecture as Code

Engineering teams use architectural fitness functions ("Architecture as Code") to enforce structural constraints during local git pre-commit hooks and CI Pull-Request checks:
- No reverse layer inversions (`util/` directly calling higher-level service packages).
- No monolithic "God Types" (Go structs or TypeScript classes) exceeding complexity thresholds.
- Strict subsystem isolation for the embedded `gitops-engine/` module (zero upward calls into Argo CD host packages).
- Strict storage layer encapsulation (raw Kubernetes secret backends restricted to `util/db`).
- Decoupling of background notification utilities from direct database instantiation.
- Explicit audit and allowlisting of unauthenticated gRPC `AuthFuncOverride` endpoints.

### The CodeExplorer Solution (`ce gate check`)
CodeExplorer provides native **Semantic Invariant Gates (SIG)**:
- Declarative OpenCypher structural invariant rules with SHA-256 baseline ratchets.
- Evaluated deterministically via `ce gate check` in **422 milliseconds** (`330 ms` pure query execution) across the entire repository graph.
- Zero LLM tokens, `$0.00` cost, and deterministic binary exit codes (`0` for PASS, `1` for FAIL) suitable for pre-commit git hooks and GitHub Actions.

---

## 2. The 6 Pre-Commit Architectural Invariant Rules

| Gate ID | Invariant Rule | Scope & Dimension | Target Subsystems | Result | Findings |
| :--- | :--- | :--- | :--- | :---: | :---: |
| **SIG-P-B-101** | **Reverse Layer Inversion Shield** | Module $\times$ Boundaries | `util/**` calling package functions in `server/`, `controller/`, `reposerver/`, `cmpserver/` | **FAIL** | 3 call sites |
| **SIG-C-R-102** | **God Type Complexity Limit** | Class $\times$ Resources | Production structs & classes (>30 methods) | **FAIL** | 12 types |
| **SIG-P-B-103** | **GitOps Engine Layer Isolation** | Module $\times$ Boundaries | `gitops-engine/**` calling Argo CD host packages | **PASS** | 0 upward calls |
| **SIG-P-B-104** | **Storage Abstraction Boundary** | Module $\times$ Boundaries | Raw secret backend (`repository_secrets.go`) outside `util/db` | **PASS** | 0 external calls |
| **SIG-P-B-105** | **Notification Subsystem DB Decoupling** | Module $\times$ Boundaries | `util/notification` direct `db.NewDB` calls | **FAIL** | 2 call sites |
| **SIG-M-B-106** | **Authentication Bypass Audit** | Method $\times$ Boundaries | Server endpoints implementing `AuthFuncOverride` | **WARN** | 4 endpoints |

**Outcome**: Evaluated all 6 rules in **422 ms** (`330 ms` query sum) with **0 tokens** &rarr; Deterministic Exit Code **`1`** (Pull Request Blocked).

---

## 3. Evaluation Telemetry Scorecard

Measured on `argoproj/argo-cd @ 6d689fa3` ([`results/metrics.json`](./results/metrics.json)):

| Gate ID | Rule Name | Query Time | Result | Action |
| :--- | :--- | :---: | :---: | :--- |
| **SIG-P-B-101** | Reverse Layer Inversion Shield | 58 ms | ❌ FAIL | Block merge (3 direct cross-layer package calls) |
| **SIG-C-R-102** | God Type Complexity Limit | 206 ms | ❌ FAIL | Block merge (12 types exceed 30 methods) |
| **SIG-P-B-103** | GitOps Engine Layer Isolation | 27 ms | ✅ PASS | Compliant (0 upward calls into host services) |
| **SIG-P-B-104** | Storage Abstraction Boundary | 29 ms | ✅ PASS | Compliant (secrets access encapsulated in `util/db`) |
| **SIG-P-B-105** | Notification Subsystem DB Decoupling | 2 ms | ❌ FAIL | Block merge (2 direct `db.NewDB` instantiations) |
| **SIG-M-B-106** | Public Authentication Bypass Audit | 8 ms | ⚠️ WARN | Audit required (4 legitimate public bypasses) |
| **Total Suite** | **All 6 Invariant Gates (incl. SQLite/YAML init)** | **422 ms** (`330 ms` queries) | ❌ **FAIL** | **Exit Code 1 (`17` errors + `4` warnings)** |

---

## 4. Source-Verified Findings Breakdown

### Rule 1: Reverse Layer Inversion Shield (`SIG-P-B-101`) — FAIL (`3` violations)
Pinpoints the exact 3 direct package-level function calls where foundational `util/` packages import and call higher-level service layers (while cleanly ignoring packages like `util/git` and `util/helm` that properly invert dependencies through local interfaces):
1. `util/session/sessionmanager.go:288`: `Parse` &rarr; `server/rbacpolicy.GetProjectRoleFromSubject`
2. `util/app/discovery/discovery.go:171`: `cmpSupports` &rarr; `cmpserver/apiclient.NewConfigManagementPluginClientSet`
3. `util/webhook/webhook.go:500`: `storePreviouslyCachedManifests` &rarr; `reposerver/cache.NewManifestKey`

### Rule 2: God Type Complexity Limit (`SIG-C-R-102`) — FAIL (`12` violations)
Identifies 12 production types across Go and TypeScript (`11` Go structs + `1` Angular service class) exceeding the 30-method threshold:
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

### Rule 3: GitOps Engine Layer Isolation (`SIG-P-B-103`) — PASS (`0` violations)
Confirmed **0 upward package calls** from the embedded `gitops-engine/` module into Argo CD's `server/`, `controller/`, `reposerver/`, `cmpserver/`, `applicationset/`, or `util/` packages.

### Rule 4: Storage Abstraction Boundary (`SIG-P-B-104`) — PASS (`0` violations)
Confirmed **0 direct invocations** of `repository_secrets.go` outside `util/db`.

### Rule 5: Notification Subsystem DB Decoupling (`SIG-P-B-105`) — FAIL (`2` violations)
Identified the exact 2 violation sites where `util/notification` constructs standalone database connections instead of using injected `ArgoDB`:
1. `util/notification/argocd/service.go:66`: `GetCommitMetadata` &rarr; `db.NewDB`
2. `util/notification/argocd/service.go:89`: `GetAppDetails` &rarr; `db.NewDB`

### Rule 6: Public Authentication Bypass Audit (`SIG-M-B-106`) — WARN (`4` audited endpoints)
Identified all 4 public gRPC entry points implementing `AuthFuncOverride`:
1. `server/session/session.go:103`: `session.Server` (anonymous login / session creation)
2. `server/settings/settings.go:202`: `settings.Server` (public login UI configuration)
3. `server/version/version.go:76`: `version.Server` (anonymous CLI version query)
4. `server/server.go:969`: `healthServer` (Kubernetes liveness/readiness probes)

---

## 5. Declarative Gate Definitions & Artifacts

- **YAML Gate Suite**: [`gates/architectural-gates.yaml`](./gates/architectural-gates.yaml)
- **Verbatim CLI Output (`ce gate check` + `ce gate details`)**: [`results/ce-gate-output.txt`](./results/ce-gate-output.txt)
- **Telemetry Data**: [`results/metrics.json`](./results/metrics.json)
