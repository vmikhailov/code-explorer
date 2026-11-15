# Implementation Summary: Admin Panel Workspace Suspension (`suspendWorkspaceAdminPanel`)

## Modified & Created Files

1. **[admin-panel.resolver.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts#L742-L752)**
   - Added the `@Mutation(() => Boolean) suspendWorkspaceAdminPanel` mutation guarded with `@UseGuards(AccessPolicyGuard(ADMIN_PANEL_ACCESS_POLICY), AdminPanelGuard)`.
   - Accepts `@Args('workspaceId', { type: () => UUIDScalarType }) workspaceId: string` and `@AuthUser() actor: AuthContextUser`, delegating to `this.adminBillingService.suspendWorkspace({ workspaceId, actorUserId: actor.id })`.

2. **[admin-panel-billing.service.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts#L149-L202)**
   - Injected `WorkspaceService`, `@InjectWorkspaceScopedRepository(EmailingDomainEntity) emailingDomainRepository`, and `@InjectMessageQueue(MessageQueue.deleteCascadeQueue) deleteCascadeMessageQueueService`.
   - Implemented `suspendWorkspace({ workspaceId, actorUserId })`:
     - **Workspace Validation**: Queries `workspaceRepository.findOne({ where: { id: workspaceId, deletedAt: IsNull() } })` and throws `BillingException` with `BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND` if the workspace does not exist or is soft-deleted.
     - **Billing Subscription Cancellation & Credit Grant Revocation**: When `IS_BILLING_ENABLED` is `true`, cancels the active subscription via `billingSubscriptionService.cancelSubscription(workspaceId)`, retrieves credit grants via `getWorkspaceCreditGrants(workspaceId)`, filters by `grant.isActive`, and revokes each active grant via `billingCreditService.revokeGrant({ workspaceId, grantId: grant.id, revokedByUserId: actorUserId })`.
     - **Workspace Suspension & Connected-Account Webhook Revocation**: Calls `workspaceService.suspendWorkspace(workspaceId)`, which sets `activationStatus = WorkspaceActivationStatus.SUSPENDED` and `suspendedAt`, invalidates `workspaceEntity` in `CoreEntityCacheService`, and emits `WORKSPACE_SUSPENDED_EVENT` (triggering `WebhookSubscriptionWorkspaceActivationListener` -> `SyncWorkspaceWebhookSubscriptionsJob` -> `WebhookSubscriptionWorkspaceSyncService.enqueueRevocations` to revoke `MessageChannel` and `CalendarChannel` webhook subscriptions for Google and Microsoft).
     - **Emailing Domain Cleanup**: Queries `emailingDomainRepository.find(workspaceId)` and enqueues `EmailingDomainWorkspaceCleanupJob` onto `MessageQueue.deleteCascadeQueue` with `{ workspaceId, domains }`.

3. **[admin-panel.module.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.module.ts#L57-L120)**
   - Added `EmailingDomainEntity` to `TypeOrmModule.forFeature([...])`, `WorkspaceModule` to `imports`, and `provideWorkspaceScopedRepository(EmailingDomainEntity)` to `providers`.

4. **[admin-panel-billing.service.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/services/__tests__/admin-panel-billing.service.spec.ts)**
   - Added unit tests for `AdminPanelBillingService.suspendWorkspace` covering:
     - Throwing `BillingException` (`BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND`) when the target workspace is not found.
     - Full billing-enabled flow (canceling active subscription, revoking only `isActive` credit grants on behalf of `actorUserId`, suspending the workspace, and enqueuing `EmailingDomainWorkspaceCleanupJob`).
     - Billing-disabled flow (skipping subscription cancellation and credit grant revocation while still suspending the workspace and enqueuing `EmailingDomainWorkspaceCleanupJob`).

## Verification
- Re-indexed the repository with `codebase-memory-mcp.exe cli index_repository` and verified via `search_graph` that `AdminPanelResolver.suspendWorkspaceAdminPanel` (`admin-panel.resolver.ts:744-752`) and `AdminPanelBillingService.suspendWorkspace` (`admin-panel-billing.service.ts:149-202`) are indexed with all expected call edges.
