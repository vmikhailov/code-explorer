# Implementation Summary: `triggerReconnectAccountBanner`

## Overview
Implemented the `triggerReconnectAccountBanner` GraphQL mutation on `TimelineMessagingResolver` alongside `dismissReconnectAccountBanner`, wired to `UserVarsService<AccountsToReconnectKeyValueType>` within `src/engine/core-modules/` so that all 4 Semantic Invariant Gates (`SIG-TWENTY-001` through `SIG-TWENTY-004`, including `SIG-TWENTY-002` Core-Engine to Feature-Modules Isolation) pass with zero new violations.

## Modified & Created Files

1. **Modified**: [timeline-messaging.resolver.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.resolver.ts#L298-L341)
   - Injected `UserVarsService<AccountsToReconnectKeyValueType>` into `TimelineMessagingResolver`.
   - Added `@Mutation(() => Boolean)` `triggerReconnectAccountBanner` guarded by `AccessPolicyGuard` (`PermissionFlagType.CONNECTED_ACCOUNTS`) and `CustomPermissionGuard` matching `dismissReconnectAccountBanner`.
   - Accepts `@AuthUser() user: AuthContextUser`, `@AuthWorkspace() workspace: WorkspaceEntity`, and `@Args() { connectedAccountId }: DismissReconnectAccountBannerInput`.
   - Reads the current array under `AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS` for `(user.id, workspace.id)`, appends `connectedAccountId` via `userVarsService.set` only when not already present, and returns `true`.

2. **Modified**: [timeline-messaging.module.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.module.ts#L16-L25)
   - Imported `UserVarsModule` into `TimelineMessagingModule` so `UserVarsService` is available in the `TimelineMessagingResolver` DI context.

3. **Created**: [timeline-messaging.resolver.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/messaging/__tests__/timeline-messaging.resolver.spec.ts#L1-L133)
   - Added unit tests for `TimelineMessagingResolver.triggerReconnectAccountBanner` covering:
     - Adding a new `connectedAccountId` when no existing user-var entry is present.
     - Appending a new `connectedAccountId` when other account IDs are already present.
     - Idempotently skipping `userVarsService.set` when `connectedAccountId` is already present in the array.

## Architectural & Gate Verification (`ce scan` & `ce gate check`)
- Ran `ce scan "C:\Work\bench-pilot\repos\twenty"` and `ce gate check "C:\Work\bench-pilot\repos\twenty"`:
  - `SIG-TWENTY-001` (GraphQL Resolver & Controller Transport Purity): **PASSED** (`0 new`)
  - `SIG-TWENTY-002` (Core-Engine to Feature-Modules Isolation): **PASSED** (`0 new` — uses core-module `UserVarsService` rather than introducing a new cross-layer call into `src/modules/connected-account/`)
  - `SIG-TWENTY-003` (Cross-Module Table Encapsulation): **PASSED** (`0 new`)
  - `SIG-TWENTY-004` (Async Message Queue Boundary): **PASSED** (`0 new`)
