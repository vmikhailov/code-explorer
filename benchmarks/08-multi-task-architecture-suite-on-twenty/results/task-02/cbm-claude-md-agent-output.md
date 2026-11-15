# Implementation Summary: `triggerReconnectAccountBanner`

## Modified & Created Files

1. **[timeline-messaging.module.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.module.ts)** *(modified)*
   - Imported `UserVarsModule` (`src/engine/core-modules/user/user-vars/user-vars.module`) in `TimelineMessagingModule` so `UserVarsService` is available for injection in `TimelineMessagingResolver`.

2. **[timeline-messaging.resolver.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.resolver.ts)** *(modified)*
   - Injected `UserVarsService<AccountsToReconnectKeyValueType>` into `TimelineMessagingResolver` (adhering to the `CLAUDE.md` architectural boundary rule that `src/engine/core-modules/` must not introduce new direct method calls to workspace feature modules under `src/modules/`, decoupling state updates via `UserVarsService`).
   - Added `@Mutation(() => Boolean)` method `triggerReconnectAccountBanner` guarded with `AccessPolicyGuard` (`requiredPermissionFlags: [PermissionFlagType.CONNECTED_ACCOUNTS]`, identical `authPrincipals` to `dismissReconnectAccountBanner`) and `CustomPermissionGuard`.
   - Accepted `@AuthUser() user: AuthContextUser`, `@AuthWorkspace() workspace: WorkspaceEntity`, and `@Args() { connectedAccountId }: DismissReconnectAccountBannerInput`.
   - Read the existing array under `AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS` for `(user.id, workspace.id)` via `userVarsService.get(...)`, appended `connectedAccountId` via `userVarsService.set(...)` only if not already present, and returned `true`.

3. **[timeline-messaging.resolver.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/messaging/__tests__/timeline-messaging.resolver.spec.ts)** *(created)*
   - Added unit tests for `TimelineMessagingResolver.triggerReconnectAccountBanner` using `@nestjs/testing`'s `Test.createTestingModule`:
     - Verifies adding `connectedAccountId` when the user-var key is not yet set (`undefined`).
     - Verifies appending `connectedAccountId` when other account IDs are already present.
     - Verifies idempotently skipping `userVarsService.set` when `connectedAccountId` is already present in the array.

## Verification
- Re-indexed the repository with `codebase-memory-mcp.exe cli index_repository` and verified via `search_graph` that `TimelineMessagingResolver.triggerReconnectAccountBanner` (`lines 315-340`) and its unit test suite are cleanly indexed and wired.
