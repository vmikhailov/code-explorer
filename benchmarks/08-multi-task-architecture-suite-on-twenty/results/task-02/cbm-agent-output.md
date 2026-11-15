## Summary Report — `triggerReconnectAccountBanner` Implementation

### Modified & Created Files
1. **[timeline-messaging.resolver.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.resolver.ts#L292-L323)** (Modified):
   - Imported `AccountsToReconnectKeys` from `src/modules/connected-account/types/accounts-to-reconnect-key-value.type`.
   - Added `@Mutation(() => Boolean) async triggerReconnectAccountBanner(...)` alongside `dismissReconnectAccountBanner` on `TimelineMessagingResolver`, guarded by the same `AccessPolicyGuard` (`PermissionFlagType.CONNECTED_ACCOUNTS`) and `CustomPermissionGuard`.
   - Accepts `@AuthUser() user: AuthContextUser`, `@AuthWorkspace() workspace: WorkspaceEntity`, and `@Args() { connectedAccountId }: DismissReconnectAccountBannerInput`.
   - Delegates to `this.accountsToReconnectService.addAccountToReconnectByKey(AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS, user.id, workspace.id, connectedAccountId)` and returns `true`.

2. **[timeline-messaging.resolver.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/messaging/__tests__/timeline-messaging.resolver.spec.ts#L1-L157)** (Created):
   - Added unit tests for `TimelineMessagingResolver.triggerReconnectAccountBanner` (and `dismissReconnectAccountBanner`) wiring `TimelineMessagingResolver` with `AccountsToReconnectService` and a mocked `UserVarsService`:
     - Verifies adding a new `connectedAccountId` under `AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS` for `(user.id, workspace.id)` when no accounts are stored yet or when other accounts are already stored, and returning `true`.
     - Verifies idempotently skipping `userVarsService.set` when `connectedAccountId` is already present in the array and returning `true`.

### Architecture & Wiring Explanation
- **`TimelineMessagingModule`** (`packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.module.ts`) imports **`ConnectedAccountModule`** (`packages/twenty-server/src/modules/connected-account/connected-account.module.ts`), which exports **`AccountsToReconnectService`** (`packages/twenty-server/src/modules/connected-account/services/accounts-to-reconnect.service.ts`).
- `TimelineMessagingResolver` already injects `AccountsToReconnectService` in its constructor.
- Calling `triggerReconnectAccountBanner` invokes `AccountsToReconnectService.addAccountToReconnectByKey(AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS, user.id, workspace.id, connectedAccountId)`:
  1. Reads the current `string[]` for `(userId, workspaceId, key: AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS)` via `UserVarsService.get` (defaulting to `[]` if unset).
  2. Checks `accountsToReconnect.includes(connectedAccountId)` and returns early if already present.
  3. Otherwise appends `connectedAccountId` and persists the updated array via `UserVarsService.set`.
  4. Returns `true` from the GraphQL mutation.
