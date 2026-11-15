## Summary: Public Domain Collision Check in `SubdomainManagerService`

### Modified & Created Files

1. **[subdomain-manager.service.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service.ts)** *(modified)*
   - Injected `@InjectRepository(PublicDomainEntity) private readonly publicDomainRepository: Repository<PublicDomainEntity>` (with the required `// eslint-disable-next-line twenty/prefer-workspace-scoped-repository` annotation for global cross-workspace uniqueness checks, matching `CustomDomainManagerService` and `WorkspaceDomainsService`).
   - Added `getBaseHostname(): string | undefined` to derive the base hostname from `this.twentyConfigService.get('FRONTEND_URL')` using `new URL(frontendUrl).hostname` when defined.
   - Updated `isSubdomainAvailable(subdomain: string)` to first check `this.workspaceRepository.findOne({ where: { subdomain }, withDeleted: true })`, returning `false` if a workspace already uses the subdomain; then, if `baseHostname` is defined, queries `this.publicDomainRepository.findOne({ where: { domain: `${subdomain}.${baseHostname}` } })` and returns `false` if a matching `PublicDomainEntity` is registered, or `true` otherwise.

2. **[subdomain-manager.module.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/subdomain-manager.module.ts)** *(modified)*
   - Registered `PublicDomainEntity` alongside `WorkspaceEntity` in `TypeOrmModule.forFeature([WorkspaceEntity, PublicDomainEntity])`.

3. **[subdomain-manager.service.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/__tests__/subdomain-manager.service.spec.ts)** *(created)*
   - Added unit tests for `SubdomainManagerService.isSubdomainAvailable` covering:
     - Returning `false` when an existing workspace already uses the subdomain.
     - Returning `false` when `${subdomain}.${baseHostname}` is registered as a `PublicDomainEntity`.
     - Returning `true` when neither a workspace subdomain nor a registered public domain matches.
     - Returning `true` without querying `publicDomainRepository` when `FRONTEND_URL` is undefined.

### Verification
- Incrementally re-indexed `C:\Work\bench-pilot\repos\twenty` via `codebase-memory-mcp.exe cli index_repository` and verified the outbound call graph of `SubdomainManagerService.isSubdomainAvailable` (`getBaseHostname`, `isDefined`).
