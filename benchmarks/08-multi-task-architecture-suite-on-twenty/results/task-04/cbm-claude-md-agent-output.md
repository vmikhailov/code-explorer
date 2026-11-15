# Task 04 Summary — Public Domain Collision Check in `SubdomainManagerService`

## Modified & Created Files

1. **[subdomain-manager.module.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/subdomain-manager.module.ts)** (Modified)
   - Imported `PublicDomainModule` into `SubdomainManagerModule` so `SubdomainManagerService` can consume the exported `PublicDomainService` rather than directly injecting `Repository<PublicDomainEntity>` across `core-modules` module boundaries.

2. **[subdomain-manager.service.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service.ts)** (Modified)
   - Injected `PublicDomainService` into `SubdomainManagerService`.
   - Added private helper `getBaseHostname()` that reads `this.twentyConfigService.get('FRONTEND_URL')` and returns `new URL(frontendUrl).hostname` when defined.
   - Updated `isSubdomainAvailable(subdomain: string)` so that after verifying no `WorkspaceEntity` (including soft-deleted rows via `withDeleted: true`) uses `subdomain`, it checks whether a `baseHostname` is configured and queries `this.publicDomainService.findByDomain(\`${subdomain}.${baseHostname}\`)`, returning `false` if a matching `PublicDomain` is registered and `true` otherwise.

3. **[subdomain-manager.service.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/__tests__/subdomain-manager.service.spec.ts)** (Created)
   - Added unit tests using `@nestjs/testing`'s `Test.createTestingModule` with `jest.fn()` mocks for `Repository<WorkspaceEntity>`, `PublicDomainService`, and `TwentyConfigService`.
   - Verified that `isSubdomainAvailable` returns `true` when neither a workspace subdomain nor a public domain matches, `false` when an existing workspace matches `subdomain`, `false` when `${subdomain}.${baseHostname}` is registered as a public domain, and `true` (skipping the public domain lookup) when `FRONTEND_URL` is not configured.

## Architectural & Convention Verification

- **Cross-Module Encapsulation (`CLAUDE.md`)**: Instead of directly injecting `@InjectRepository(PublicDomainEntity)` inside `core-modules/domain/subdomain-manager/` (which would cross module table-ownership boundaries into `core-modules/public-domain/` and require suppressing `twenty/prefer-workspace-scoped-repository`), `SubdomainManagerModule` imports `PublicDomainModule` and `SubdomainManagerService` delegates the unscoped domain lookup to `PublicDomainService.findByDomain(...)` (matching the pattern used by `CloudflareModule` / `DnsCloudflareService`).
- **Graph Re-indexing**: Incrementally re-indexed `C:\Work\bench-pilot\repos\twenty` via `codebase-memory-mcp.exe cli index_repository` (`status: "indexed"`).
