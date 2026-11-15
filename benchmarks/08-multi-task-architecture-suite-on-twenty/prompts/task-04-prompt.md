# Task 04 Prompt — Subdomain Collision Check Against Registered Public Domains (`twentyhq/twenty`)

Target Repository: `C:\Work\bench-pilot\repos\twenty`

## Feature Request: Public Domain Collision Check in `SubdomainManagerService`

In `SubdomainManagerService` (`packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service.ts`), workspace subdomains are currently checked only against `WorkspaceEntity.subdomain` and `DEFAULT_SUBDOMAIN`, while `CustomDomainManagerService` (`packages/twenty-server/src/engine/core-modules/domain/custom-domain-manager/services/custom-domain-manager.service.ts`) also verifies that a domain does not collide with an existing registered `PublicDomain`.

Update `SubdomainManagerService` so that subdomain availability also checks whether `<subdomain>.<baseHostname>` is already registered as a `PublicDomain` (preventing a workspace subdomain from colliding with an existing registered public domain).

### Functional Requirements

1. **`SubdomainManagerService` (`packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/subdomain-manager.service.ts`)**:
   - Derive the base hostname from `this.twentyConfigService.get('FRONTEND_URL')` (using `new URL(frontendUrl).hostname` when defined).
   - In `isSubdomainAvailable(subdomain: string)`: in addition to checking `this.workspaceRepository.findOne({ where: { subdomain }, withDeleted: true })`, if a base hostname is configured, verify that `${subdomain}.${baseHostname}` is not already registered as a `PublicDomain` (returning `false` if a matching public domain exists).
   - Update ` domain/subdomain-manager/subdomain-manager.module.ts` if any module imports are required.
2. **Unit Tests**:
   - Add or update unit tests in `packages/twenty-server/src/engine/core-modules/domain/subdomain-manager/services/__tests__/subdomain-manager.service.spec.ts` verifying that `isSubdomainAvailable` returns `false` when `${subdomain}.${baseHostname}` is registered as a public domain, and `true` when neither a workspace subdomain nor a public domain matches.

### Constraints
- Do NOT run `yarn`, `nx`, `jest`, or `tsc` via `run_command` (no database/Redis containers are running in the benchmark sandbox).
- When finished, send your final summary report via `send_message` listing all modified/created files and explaining how the public domain check in `SubdomainManagerService` is wired.
