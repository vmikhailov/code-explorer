# Pre-Commit / CI Pull-Request Architectural Guard Report
**Repository**: `argoproj/argo-cd` (`C:\Work\bench-pilot\repos\argo-cd`)  
**Evaluator**: CodeExplorer Architectural Guard (`ce gate check` / `ce gate details`)  
**Overall CI Status**: ❌ **BLOCKED (3 FAILED, 1 WARNING, 2 PASSED)**

---

## Executive Summary

| Gate ID | Architectural Invariant Rule | Status | Violations | Severity |
| :--- | :--- | :---: | :---: | :---: |
| **SIG-P-B-101** | **Rule 1**: Reverse Layer Inversion Shield (`util/**` calling higher services) | ❌ **FAIL** | 104 | Error |
| **SIG-C-R-102** | **Rule 2**: God Struct Complexity Limit (&le; 30 methods) | ❌ **FAIL** | 12 | Error |
| **SIG-P-I-103** | **Rule 3**: Package Dependency DAG Integrity (Zero Import Cycles) | ✅ **PASS** | 0 | Error |
| **SIG-P-B-104** | **Rule 4**: Storage Abstraction Boundary (Raw Secrets Access Shield) | ✅ **PASS** | 0 | Error |
| **SIG-P-B-105** | **Rule 5**: Notification Subsystem DB Decoupling (`db.NewDB` injection) | ❌ **FAIL** | 2 | Error |
| **SIG-M-B-106** | **Rule 6**: Public Authentication Bypass Audit (`AuthFuncOverride`) | ⚠️ **WARNING** | 4 | Warning |

---

## Detailed Rule Evaluations

### Rule 1: Reverse Layer Leaks (Reverse Layer Inversion Shield)
- **Gate ID**: `SIG-P-B-101`
- **Rule Description**: Foundational utility packages (`util/**`, excluding `*_test.go`) must remain self-contained and must never call code in higher-level business services (`server/**`, `controller/**`, `reposerver/**`).
- **Status**: ❌ **FAIL**
- **Violation Count**: **104 violations** (across 90 call graph edges)
- **Key Violation Call Sites**:
  1. [`util/webhook/webhook.go:500:20`](file:///C:/Work/bench-pilot/repos/argo-cd/util/webhook/webhook.go#L500) | Caller: `storePreviouslyCachedManifests` &rarr; Target: [`reposerver/cache.NewManifestKey`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L348)
  2. [`util/webhook/webhook.go:521:12`](file:///C:/Work/bench-pilot/repos/argo-cd/util/webhook/webhook.go#L521) | Caller: `storePreviouslyCachedManifests` &rarr; Target: [`reposerver/cache.SetNewRevisionManifests`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L393)
  3. [`util/git/client.go:862:24`](file:///C:/Work/bench-pilot/repos/argo-cd/util/git/client.go#L862) | Caller: `getRefsFromCacheOrFetch` &rarr; Target: [`reposerver/cache.GetOrLockGitReferences`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L183)
  4. [`util/git/client.go:876:13`](file:///C:/Work/bench-pilot/repos/argo-cd/util/git/client.go#L876) | Caller: `getRefsFromCacheOrFetch` &rarr; Target: [`reposerver/cache.UnlockGitReferences`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L210)
  5. [`util/git/client.go:887:13`](file:///C:/Work/bench-pilot/repos/argo-cd/util/git/client.go#L887) | Caller: `getRefsFromCacheOrFetch` &rarr; Target: [`reposerver/cache.SetGitReferences`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L225)
  6. [`util/helm/client.go:322:13`](file:///C:/Work/bench-pilot/repos/argo-cd/util/helm/client.go#L322) | Caller: `GetIndex` &rarr; Target: [`reposerver/cache.GetHelmIndex`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L254)
  7. [`util/helm/client.go:337:14`](file:///C:/Work/bench-pilot/repos/argo-cd/util/helm/client.go#L337) | Caller: `GetIndex` &rarr; Target: [`reposerver/cache.SetHelmIndex`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L274)
  8. [`util/helm/client.go:516:13`](file:///C:/Work/bench-pilot/repos/argo-cd/util/helm/client.go#L516) | Caller: `GetTags` &rarr; Target: [`reposerver/cache.GetHelmIndex`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L254)
  9. [`util/helm/client.go:600:14`](file:///C:/Work/bench-pilot/repos/argo-cd/util/helm/client.go#L600) | Caller: `GetTags` &rarr; Target: [`reposerver/cache.SetHelmIndex`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L274)
  10. [`util/oci/client.go:437:13`](file:///C:/Work/bench-pilot/repos/argo-cd/util/oci/client.go#L437) | Caller: `getTags` &rarr; Target: [`reposerver/cache.GetOCITags`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L294)
  11. [`util/oci/client.go:465:14`](file:///C:/Work/bench-pilot/repos/argo-cd/util/oci/client.go#L465) | Caller: `getTags` &rarr; Target: [`reposerver/cache.SetOCITags`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L314)
  12. [`util/session/sessionmanager.go:288:27`](file:///C:/Work/bench-pilot/repos/argo-cd/util/session/sessionmanager.go#L288) | Caller: `Parse` &rarr; Target: [`server/rbacpolicy.GetProjectRoleFromSubject`](file:///C:/Work/bench-pilot/repos/argo-cd/server/rbacpolicy/rbacpolicy.go#L115)
  13. [`util/tls/tls.go:372:20`](file:///C:/Work/bench-pilot/repos/argo-cd/util/tls/tls.go#L372) | Caller: `generate` &rarr; Target: [`server/certificate.CreateCertificate`](file:///C:/Work/bench-pilot/repos/argo-cd/server/certificate/certificate.go#L38)
  14. [`util/app/discovery/discovery.go:139:26`](file:///C:/Work/bench-pilot/repos/argo-cd/util/app/discovery/discovery.go#L139) | Caller: `matchRepositoryCMP` &rarr; Target: [`cmpserver/plugin.MatchRepository`](file:///C:/Work/bench-pilot/repos/argo-cd/cmpserver/plugin/plugin.go#L305)
  15. [`util/app/discovery/discovery.go:171:18`](file:///C:/Work/bench-pilot/repos/argo-cd/util/app/discovery/discovery.go#L171) | Caller: `cmpSupports` &rarr; Target: [`cmpserver/apiclient.NewConfigManagementPluginClientSet`](file:///C:/Work/bench-pilot/repos/argo-cd/cmpserver/apiclient/clientset.go#L64)
  16. [`util/app/discovery/discovery.go:173:26`](file:///C:/Work/bench-pilot/repos/argo-cd/util/app/discovery/discovery.go#L173) | Caller: `cmpSupports` &rarr; Target: [`cmpserver/apiclient.NewConfigManagementPluginClient`](file:///C:/Work/bench-pilot/repos/argo-cd/cmpserver/apiclient/clientset.go#L33)
  17. [`util/app/discovery/discovery.go:182:14`](file:///C:/Work/bench-pilot/repos/argo-cd/util/app/discovery/discovery.go#L182) | Caller: `cmpSupports` &rarr; Target: [`cmpserver/plugin.CheckPluginConfiguration`](file:///C:/Work/bench-pilot/repos/argo-cd/cmpserver/plugin/plugin.go#L311)
  *(and 87 additional calls resolving across lower-to-higher package boundaries).*
- **Remediation**:
  - Invert dependencies by defining cache, policy, and client interfaces inside `util/` or a shared leaf package (e.g. `pkg/cache`), implemented by `reposerver/cache`.
  - Move service-specific webhook manifest caching and CMP discovery logic up into their respective service domains (`reposerver/` or `server/`).

---

### Rule 2: God Structs & Complexity Limit
- **Gate ID**: `SIG-C-R-102`
- **Rule Description**: Structs must declare at most 30 methods in production code. Monolithic God Structs degrade maintainability and violate the Single Responsibility Principle.
- **Status**: ❌ **FAIL**
- **Violation Count**: **12 structs**
- **Violations List**:
  1. [`util/settings/settings.go:632:6`](file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go#L632) | [`SettingsManager`](file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go#L632) &mdash; **70 methods**
  2. [`controller/appcontroller.go:120:6`](file:///C:/Work/bench-pilot/repos/argo-cd/controller/appcontroller.go#L120) | [`ApplicationController`](file:///C:/Work/bench-pilot/repos/argo-cd/controller/appcontroller.go#L120) &mdash; **64 methods**
  3. [`pkg/apiclient/apiclient.go:164:6`](file:///C:/Work/bench-pilot/repos/argo-cd/pkg/apiclient/apiclient.go#L164) | [`client`](file:///C:/Work/bench-pilot/repos/argo-cd/pkg/apiclient/apiclient.go#L164) &mdash; **61 methods**
  4. [`server/application/application.go:103:6`](file:///C:/Work/bench-pilot/repos/argo-cd/server/application/application.go#L103) | [`Server`](file:///C:/Work/bench-pilot/repos/argo-cd/server/application/application.go#L103) &mdash; **59 methods**
  5. [`util/git/client.go:202:6`](file:///C:/Work/bench-pilot/repos/argo-cd/util/git/client.go#L202) | [`nativeGitClient`](file:///C:/Work/bench-pilot/repos/argo-cd/util/git/client.go#L202) &mdash; **50 methods**
  6. [`gitops-engine/pkg/cache/cluster.go:243:6`](file:///C:/Work/bench-pilot/repos/argo-cd/gitops-engine/pkg/cache/cluster.go#L243) | [`store`](file:///C:/Work/bench-pilot/repos/argo-cd/gitops-engine/pkg/cache/cluster.go#L243) &mdash; **44 methods**
  7. [`util/settings/settings.go:87:6`](file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go#L87) | [`ArgoCDSettings`](file:///C:/Work/bench-pilot/repos/argo-cd/util/settings/settings.go#L87) &mdash; **38 methods**
  8. [`ui/src/app/shared/services/applications-service.ts:57:8`](file:///C:/Work/bench-pilot/repos/argo-cd/ui/src/app/shared/services/applications-service.ts#L57) | [`ApplicationsService`](file:///C:/Work/bench-pilot/repos/argo-cd/ui/src/app/shared/services/applications-service.ts#L57) &mdash; **36 methods**
  9. [`util/db/repository.go:58:1`](file:///C:/Work/bench-pilot/repos/argo-cd/util/db/repository.go#L58) | [`db`](file:///C:/Work/bench-pilot/repos/argo-cd/util/db/repository.go#L58) &mdash; **35 methods**
  10. [`reposerver/repository/repository.go:93:6`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/repository/repository.go#L93) | [`Service`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/repository/repository.go#L93) &mdash; **35 methods**
  11. [`gitops-engine/pkg/sync/sync_context.go:364:1`](file:///C:/Work/bench-pilot/repos/argo-cd/gitops-engine/pkg/sync/sync_context.go#L364) | [`syncContext`](file:///C:/Work/bench-pilot/repos/argo-cd/gitops-engine/pkg/sync/sync_context.go#L364) &mdash; **35 methods**
  12. [`reposerver/cache/cache.go:32:6`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L32) | [`Cache`](file:///C:/Work/bench-pilot/repos/argo-cd/reposerver/cache/cache.go#L32) &mdash; **34 methods**
- **Remediation**:
  - Decompose monolithic structs into cohesive domain delegates using composition:
    - Split `SettingsManager` (70) and `ArgoCDSettings` (38) into specialized managers (e.g. `OidcSettingsManager`, `TlsSettingsManager`, `InformerManager`).
    - Decompose `ApplicationController` (64) and `Server` (59) by factoring sub-handlers (reconciliation, RBAC policy enforcement, event streaming) into discrete delegates.
    - Segregate `client` (61) across domain interfaces (`ApplicationClient`, `ClusterClient`, `RepoClient`).

---

### Rule 3: Package Dependency DAG Integrity
- **Gate ID**: `SIG-P-I-103`
- **Rule Description**: Package import relationships across the repository must form a strictly acyclic directed graph (DAG).
- **Status**: ✅ **PASS**
- **Violation Count**: **0 violations**
- **Findings**: OpenCypher cyclic path search `MATCH (p1:Package)-[:DEPENDS_ON*1..3]->(p2:Package) WHERE (p2)-[:DEPENDS_ON*1..3]->(p1) AND p1.id <> p2.id` confirmed zero package dependency cycles.
- **Remediation**: N/A.

---

### Rule 4: Storage Abstraction Boundary
- **Gate ID**: `SIG-P-B-104`
- **Rule Description**: Raw Kubernetes secret repository storage backends (`repository_secrets.go`) must be encapsulated strictly within `util/db` and never called from other packages.
- **Status**: ✅ **PASS**
- **Violation Count**: **0 violations**
- **Findings**: OpenCypher query `MATCH (caller:Function)-[r:CALLS]->(callee:Function) WHERE callee.path CONTAINS 'repository_secrets.go' AND NOT caller.path CONTAINS 'util/db'` returned 0 calls from external packages. All repository secret storage access is properly mediated through `util/db`.
- **Remediation**: N/A.

---

### Rule 5: Notification Subsystem DB Decoupling
- **Gate ID**: `SIG-P-B-105`
- **Rule Description**: Notification utilities in `util/notification` must not instantiate standalone database instances or call `db.NewDB` directly (DB instances must be injected).
- **Status**: ❌ **FAIL**
- **Violation Count**: **2 violations**
- **Violations List**:
  1. [`util/notification/argocd/service.go:66:14`](file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L66) | Caller: `GetCommitMetadata` &rarr; Target: [`db.NewDB`](file:///C:/Work/bench-pilot/repos/argo-cd/util/db/db.go#L42)
  2. [`util/notification/argocd/service.go:89:14`](file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L89) | Caller: `GetAppDetails` &rarr; Target: [`db.NewDB`](file:///C:/Work/bench-pilot/repos/argo-cd/util/db/db.go#L42)
- **Remediation**:
  - Add an injected `db db.ArgoDB` field to the [`argoCDService`](file:///C:/Work/bench-pilot/repos/argo-cd/util/notification/argocd/service.go#L50) struct.
  - Pass `db.ArgoDB` as a constructor argument in `NewArgoCDService` rather than instantiating transient `db.NewDB(svc.namespace, svc.settingsMgr, svc.clientset)` objects inside query methods.

---

### Rule 6: Public Authentication Bypass Audit
- **Gate ID**: `SIG-M-B-106`
- **Rule Description**: Identify all server endpoints that implement `AuthFuncOverride` to bypass standard authentication.
- **Status**: ⚠️ **WARNING** (Audit & Documentation Verification)
- **Violation Count**: **4 endpoints detected**
- **Audited Endpoints**:
  1. [`server/session/session.go:103:1`](file:///C:/Work/bench-pilot/repos/argo-cd/server/session/session.go#L103) | `(s *Server) AuthFuncOverride`  
     *Purpose*: Authenticates login credentials without requiring pre-existing tokens; prevents chicken-and-egg authentication deadlocks.
  2. [`server/settings/settings.go:202:1`](file:///C:/Work/bench-pilot/repos/argo-cd/server/settings/settings.go#L202) | `(s *Server) AuthFuncOverride`  
     *Purpose*: Selectively bypasses authentication for `/cluster.SettingsService/Get` so unauthenticated users can load login page settings (OIDC providers, logo, login flags).
  3. [`server/version/version.go:76:1`](file:///C:/Work/bench-pilot/repos/argo-cd/server/version/version.go#L76) | `(s *Server) AuthFuncOverride`  
     *Purpose*: Allows CLI and monitoring agents to query server version information anonymously.
  4. [`server/server.go:969:1`](file:///C:/Work/bench-pilot/repos/argo-cd/server/server.go#L969) | `(*healthServer) AuthFuncOverride`  
     *Purpose*: Implements standard gRPC health checks (`/grpc.health.v1.Health/Check`) for load balancers and Kubernetes kubelet probes without credentials.
- **Remediation**:
  - All 4 overrides correspond to known, legitimate public entry points.
  - Document and maintain these endpoints in the architectural security baseline (`sig-m-b-106-auth-override-audit.yaml` ratchet/allowlist) so any new unauthorized `AuthFuncOverride` fails CI automatically.

---

## Action Items Prior to Merge
1. **Fix SIG-P-B-105**: Update `util/notification/argocd/service.go` to inject `db.ArgoDB`.
2. **Refactor or Ratchet SIG-C-R-102**: Register baseline ratchets for existing God Structs or plan decomposition into sub-delegates.
3. **Decouple SIG-P-B-101**: Introduce caching and policy interfaces to break reverse dependencies from `util/**` into `server/`, `controller/`, and `reposerver/`.
4. **Approve SIG-M-B-106**: Confirm the 4 documented `AuthFuncOverride` bypass endpoints remain within acceptable security posture.