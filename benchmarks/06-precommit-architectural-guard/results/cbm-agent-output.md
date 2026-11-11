# Pre-Commit / CI Pull-Request Architectural Guard Report
**Repository**: `argoproj/argo-cd`  
**Guard Status**: **FAIL (3 Violations Detected across 3 Rules)**

---

## Executive Summary

| Invariant Rule | Target Domain | Result | Violations |
| :--- | :--- | :---: | :---: |
| **Rule 1: Reverse Layer Leaks** | `util/**` calls into `server/**`, `controller/**`, `reposerver/**` | **FAIL** | 9 |
| **Rule 2: God Structs & Complexity Limit** | Production structs with > 30 declared methods | **FAIL** | 11 |
| **Rule 3: Package Dependency DAG** | Cyclic package import detection | **PASS** | 0 |
| **Rule 4: Storage Abstraction Boundary** | Encapsulation of `util/db/repository_secrets.go` | **PASS** | 0 |
| **Rule 5: Notification Subsystem Decoupling** | No standalone `db.NewDB` instantiations in `util/notification/**` | **FAIL** | 2 |
| **Rule 6: Public Authentication Bypass Audit** | Server endpoints implementing `AuthFuncOverride` | **AUDIT / PASS** | 4 endpoints |

---

## Detailed Rule Evaluations

### Rule 1: Reverse Layer Leaks
- **Status**: `FAIL`
- **Violation Count**: 9 production files (spanning 15 distinct reverse package imports)
- **Description**: Foundational utility packages in `util/**` must remain self-contained and never depend on or call higher-level service implementations (`server/**`, `controller/**`, `reposerver/**`).

#### Concrete Violations:
1. [file:///C:/Work/bench-pilot/repos/argo-cd/util/webhook/webhook.go#L36](file:///C:/Work/bench-pilot/repos/argo-cd/util/webhook/webhook.go#L36-L37)
   - **Caller Symbol**: `util/webhook` (`ArgoCDWebhookHandler`)
   - **Target Symbols**:
     - `reposerver/cache` (`*cache.Cache`)
     - `server/cache` (`*servercache.Cache`)
2. [file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go#L41](file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go#L41)
   - **Caller Symbol**: `util/settings` (`SettingsManager`)
   - **Target Symbol**: `server/settings/oidc`
3. [file:///C:/Work/bench-pilot/repos/argo-cd/util/session/sessionmanager.go#L29](file:///C:/Work/bench-pilot/repos/argo-cd/util/session/sessionmanager.go#L29)
   - **Caller Symbol**: `util/session` (`SessionManager`)
   - **Target Symbol**: `server/rbacpolicy`
4. [file:///C:/Work/bench-pilot/repos/argo-cd/util/oidc/oidc.go#L34](file:///C:/Work/bench-pilot/repos/argo-cd/util/oidc/oidc.go#L34)
   - **Caller Symbol**: `util/oidc` (`Client`)
   - **Target Symbol**: `server/settings/oidc` (`OIDCConfig`, `Claims`)
5. [file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/expression/shared/appdetail.go#L8](file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/expression/shared/appdetail.go#L8)
   - **Caller Symbol**: `util/notification/expression/shared`
   - **Target Symbol**: `reposerver/apiclient`
6. [file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L18](file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L18)
   - **Caller Symbol**: `util/notification/argocd` (`NewArgoCDService`)
   - **Target Symbol**: `reposerver/apiclient` (`Clientset`, `RepoServerRevisionMetadataRequest`, `RepoServerAppDetailsQuery`)
7. [file:///C:/Work/bench-pilot/repos/argo-cd/util/manifeststream/stream.go#L16](file:///C:/Work/bench-pilot/repos/argo-cd/util/manifeststream/stream.go#L16)
   - **Caller Symbol**: `util/manifeststream`
   - **Target Symbol**: `reposerver/apiclient`
8. [file:///C:/Work/bench-pilot/repos/argo-cd/util/argo/argo.go#L29](file:///C:/Work/bench-pilot/repos/argo-cd/util/argo/argo.go#L29)
   - **Caller Symbol**: `util/argo` (`ValidateRepo` at line 394)
   - **Target Symbol**: `reposerver/apiclient` (`repoClientset.NewRepoServerClient()`)

#### Remediation Guidance:
- Move shared DTOs/Protobuf types (`server/settings/oidc`, `server/rbacpolicy`) into common contracts under `pkg/` (e.g. `pkg/apis/` or `pkg/oidc/`).
- Invert dependencies for cache and repo-server clients via Go interfaces defined in the caller utility packages rather than importing concrete server implementations.

---

### Rule 2: God Structs & Complexity Limit
- **Status**: `FAIL`
- **Violation Count**: 11 production structs
- **Description**: Production structs (excluding test code, generated protobuf files, and mocks) must not exceed 30 declared methods.

#### Concrete Violations:
1. [`ApplicationController`](file:///C:/Work/bench-pilot/repos/argo-cd/controller/appcontroller.go#L120) — **83 methods** (in [controller/appcontroller.go](file:///C:/Work/bench-pilot/repos/argo-cd/controller/appcontroller.go))
2. [`SettingsManager`](file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go#L632) — **74 methods** (in [util/settings/settings.go](file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go))
3. [`client`](file:///C:/Work/bench-pilot/repos/argo-cd/pkg/apiclient/apiclient.go#L164) — **64 methods** (in [pkg/apiclient/apiclient.go](file:///C:/Work/bench-pilot/repos/argo-cd/pkg/apiclient/apiclient.go))
4. [`db`](file:///C:/Work/bench-pilot/repos/argo-cd/util/db/db.go#L127) — **60 methods** (in [util/db/db.go](file:///C:/Work/bench-pilot/repos/argo-cd/util/db/db.go))
5. [`Server`](file:///C:/Work/bench-pilot/repos/argo-cd/server/application/application.go#L103) — **59 methods** (in [server/application/application.go](file:///C:/Work/bench-pilot/repos/argo-cd/server/application/application.go))
6. [`nativeGitClient`](file:///C:/Work/bench-pilot/repos/argo-cd/util/git/client.go#L202) — **50 methods** (in [util/git/client.go](file:///C:/Work/bench-pilot/repos/argo-cd/util/git/client.go))
7. [`Application`](file:///C:/Work/bench-pilot/repos/argo-cd/pkg/apis/application/v1alpha1/types.go#L70) — **39 methods** (in [pkg/apis/application/v1alpha1/types.go](file:///C:/Work/bench-pilot/repos/argo-cd/pkg/apis/application/v1alpha1/types.go))
8. [`AppProject`](file:///C:/Work/bench-pilot/repos/argo-cd/pkg/apis/application/v1alpha1/app_project_types.go#L46) — **39 methods** (in [pkg/apis/application/v1alpha1/app_project_types.go](file:///C:/Work/bench-pilot/repos/argo-cd/pkg/apis/application/v1alpha1/app_project_types.go))
9. [`ArgoCDSettings`](file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go#L87) — **38 methods** (in [util/settings/settings.go](file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go))
10. [`Cache`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L32) — **34 methods** (in [reposerver/cache/cache.go](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go))
11. [`Service`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/repository/repository.go#L93) — **33 methods** (in [reposerver/repository/repository.go](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/repository/repository.go))

#### Remediation Guidance:
- Decompose monolithic structs into focused delegates using composition:
  - `ApplicationController`: split reconciliation, status updating, event handling, and diffing logic into dedicated sub-controllers/handlers.
  - `SettingsManager`: segregate settings retrieval into domain-specific managers (`AuthSettingsManager`, `PluginSettingsManager`, `TLSSettingsManager`).
  - `db`: decompose repository, cluster, and certificate operations into separate storage repositories adhering to Interface Segregation Principle.

---

### Rule 3: Package Dependency DAG
- **Status**: `PASS`
- **Violation Count**: 0
- **Description**: Package imports across production code form a strict directed acyclic graph. Go compiler enforcement guarantees zero import cycles at build time, and graph validation confirms no mutual package dependencies exist across modules.

---

### Rule 4: Storage Abstraction Boundary
- **Status**: `PASS`
- **Violation Count**: 0
- **Description**: Raw Kubernetes secret repository storage backend ([util/db/repository_secrets.go](file:///C:/Work/bench-pilot/repos/argo-cd/util/db/repository_secrets.go)) is strictly unexported and encapsulated within `util/db`. No outside packages directly reference or invoke methods from `repository_secrets.go`.

---

### Rule 5: Notification Subsystem Decoupling
- **Status**: `FAIL`
- **Violation Count**: 2
- **Description**: `util/notification/**` must not directly call `db.NewDB` or construct standalone DB instances; database dependencies must be injected.

#### Concrete Violations:
1. [file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L66](file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L66)
   - **Caller Symbol**: [`GetCommitMetadata`](file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L65-L84)
   - **Target Symbol**: `db.NewDB`
   - **Code**: `argocdDB := db.NewDB(svc.namespace, svc.settingsMgr, svc.clientset)`
2. [file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L89](file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L89)
   - **Caller Symbol**: [`GetAppDetails`](file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L86-L115)
   - **Target Symbol**: `db.NewDB`
   - **Code**: `argocdDB := db.NewDB(svc.namespace, svc.settingsMgr, svc.clientset)`

#### Remediation Guidance:
- Extend `argoCDService` struct in `util/notification/argocd/service.go` to accept `db.ArgoDB` as a constructor parameter in `NewArgoCDService(..., db db.ArgoDB)` instead of creating an ephemeral `db.NewDB` instance per invocation.

---

### Rule 6: Public Authentication Bypass Audit
- **Status**: `AUDIT / PASS`
- **Endpoint Count**: 4 endpoints identified
- **Description**: Identification and audit of all server endpoints implementing `AuthFuncOverride` to bypass standard gRPC token authentication.

#### Identified Endpoints:
1. [`session.Server.AuthFuncOverride`](file:///C:/Work/bench-pilot/repos/argo-cd/server/session/session.go#L103-L107)
   - **Location**: [server/session/session.go:103](file:///C:/Work/bench-pilot/repos/argo-cd/server/session/session.go#L103)
   - **Scope**: Entire session service (allows unauthenticated login and initial token exchange; attempts authentication but suppresses errors).
2. [`settings.Server.AuthFuncOverride`](file:///C:/Work/bench-pilot/repos/argo-cd/server/settings/settings.go#L202-L210)
   - **Location**: [server/settings/settings.go:202](file:///C:/Work/bench-pilot/repos/argo-cd/server/settings/settings.go#L202)
   - **Scope**: Whitelisted endpoint `/cluster.SettingsService/Get` (serves public auth provider and login configuration to unauthenticated clients).
3. [`version.Server.AuthFuncOverride`](file:///C:/Work/bench-pilot/repos/argo-cd/server/version/version.go#L76-L82)
   - **Location**: [server/version/version.go:76](file:///C:/Work/bench-pilot/repos/argo-cd/server/version/version.go#L76)
   - **Scope**: Entire version service (allows public querying of server/build version).
4. [`healthServer.AuthFuncOverride`](file:///C:/Work/bench-pilot/repos/argo-cd/server/server.go#L969-L971)
   - **Location**: [server/server.go:969](file:///C:/Work/bench-pilot/repos/argo-cd/server/server.go#L969)
   - **Scope**: Standard gRPC health checking protocol (`/grpc.health.v1.Health/Check`) for load balancer probes without credentials.

---

### Pre-Commit Gate Recommendation
**MERGE BLOCKED**: PR cannot be merged until violations in **Rule 1** (Reverse Layer Leaks), **Rule 2** (God Struct Limits), and **Rule 5** (Notification Subsystem DB Injection) are resolved.