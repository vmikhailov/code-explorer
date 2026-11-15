# Task 02 Prompt — Flag Connected Account Reconnect Banner (`twentyhq/twenty`)

Target Repository: `C:\Work\bench-pilot\repos\twenty`

## Feature Request: `triggerReconnectAccountBanner`

Implement a GraphQL mutation `triggerReconnectAccountBanner` in the `messaging` module (`packages/twenty-server/src/engine/core-modules/messaging/`) alongside `dismissReconnectAccountBanner` on `TimelineMessagingResolver` (`packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.resolver.ts`) so the client or a permission error handler can mark a `connectedAccountId` as requiring reconnection due to insufficient permissions for the authenticated user and workspace.

### Functional Requirements

1. **GraphQL Mutation (`TimelineMessagingResolver`)**:
   - Add a `@Mutation(() => Boolean)` named `triggerReconnectAccountBanner` on `TimelineMessagingResolver` (`packages/twenty-server/src/engine/core-modules/messaging/timeline-messaging.resolver.ts`).
   - Guard it with the exact same `AccessPolicyGuard` (`PermissionFlagType.CONNECTED_ACCOUNTS`) and `CustomPermissionGuard` as `dismissReconnectAccountBanner`.
   - Accept `@AuthUser() user: AuthContextUser`, `@AuthWorkspace() workspace: WorkspaceEntity`, and `@Args() { connectedAccountId }: DismissReconnectAccountBannerInput`.
2. **Reconnect Banner State Update**:
   - Record `connectedAccountId` under the `AccountsToReconnectKeys.ACCOUNTS_TO_RECONNECT_INSUFFICIENT_PERMISSIONS` user-var key for `(user.id, workspace.id)` if `connectedAccountId` is not already present in that array.
   - Return `true`.
3. **Unit Tests**:
   - Add unit tests in `packages/twenty-server/src/engine/core-modules/messaging/__tests__/timeline-messaging.resolver.spec.ts` verifying `triggerReconnectAccountBanner` (adding a new `connectedAccountId` and idempotently skipping when already present).

### Constraints
- Do NOT run `yarn`, `nx`, `jest`, or `tsc` via `run_command` (no database/Redis containers are running in the benchmark sandbox).
- When finished, send your final summary report via `send_message` listing all modified/created files and explaining how `triggerReconnectAccountBanner` is wired.
