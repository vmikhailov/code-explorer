# Summary Report — Public Domain Collision Check in `SubdomainManagerService`

## Modified and Created Files

1. **Modified**: [subdomain-manager.module.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/subdomain-manager.module.ts)
   - Imported `PublicDomainModule` into `SubdomainManagerModule` so `SubdomainManagerService` can consume the exported `PublicDomainService` without directly coupling to `PublicDomainEntity`'s repository across core-module boundaries (`SIG-TWENTY-003`: Cross-Module Table Encapsulation).

2. **Modified**: [subdomain-manager.service.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service.ts)
   - Injected `PublicDomainService` into `SubdomainManagerService`.
   - Added private helper `getBaseHostname(): string | undefined` that reads `this.twentyConfigService.get('FRONTEND_URL')` and returns `new URL(frontendUrl).hostname` when defined.
   - Updated `isSubdomainAvailable(subdomain: string)` to:
     1. Check `this.workspaceRepository.findOne({ where: { subdomain }, withDeleted: true })` and return `false` if a workspace (including soft-deleted) exists.
     2. Resolve `baseHostname` via `this.getBaseHostname()`; when defined, query `this.publicDomainService.findByDomain(\`${subdomain}.${baseHostname}\`)` and return `false` if a matching `PublicDomain` is registered.
     3. Return `true` otherwise.

3. **Created**: [subdomain-manager.service.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/__tests__/subdomain-manager.service.spec.ts)
   - Added unit tests for `SubdomainManagerService.isSubdomainAvailable` verifying:
     - Returns `true` when neither a workspace subdomain nor a registered public domain matches `${subdomain}.${baseHostname}`.
     - Returns `false` when `${subdomain}.${baseHostname}` is registered as a public domain.
     - Returns `false` when a workspace with the subdomain already exists (without querying public domains).
     - Returns `true` without querying public domains when `FRONTEND_URL` is undefined.

## Architectural & Gate Verification (`ce`)
- Ran incremental `ce scan` and `ce gate check` on `C:\Work\bench-pilot\repos\twenty`:
  - `SIG-TWENTY-001` (GraphQL Resolver & Controller Transport Purity): **PASSED (Tolerated: 5, New: 0)**
  - `SIG-TWENTY-002` (Core-Engine to Feature-Modules Isolation): **PASSED (Tolerated: 101, New: 0)**
  - `SIG-TWENTY-003` (Cross-Module Table Encapsulation): **PASSED (Tolerated: 150, New: 0)**
  - `SIG-TWENTY-004` (Async Message Queue Boundary): **PASSED (Violations: 0)**
