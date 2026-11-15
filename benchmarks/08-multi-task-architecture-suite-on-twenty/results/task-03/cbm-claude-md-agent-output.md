# Implementation Summary: Admin Panel Workspace Suspension (`suspendWorkspaceAdminPanel`)

## Modified & Created Files

1. **[admin-panel.module.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.module.ts)**
   - Imported `WorkspaceModule` and `EmailingDomainModule` in `AdminPanelModule` so `WorkspaceService` and `EmailingDomainService` can be injected into `AdminPanelBillingService`.

2. **[admin-panel.resolver.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts)**
   - Added the `suspendWorkspaceAdminPanel` GraphQL `@Mutation(() => Boolean)` guarded with `@UseGuards(AccessPolicyGuard(ADMIN_PANEL_ACCESS_POLICY), AdminPanelGuard)`.
   - Accepts `@Args('workspaceId', { type: () => UUIDScalarType }) workspaceId: string` and `@AuthUser() actor: AuthContextUser`, delegating to `this.adminBillingService.suspendWorkspace({ workspaceId, actorUserId: actor.id })`.

3. **[admin-panel-billing.service.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts)**
   - Injected `WorkspaceService`, `EmailingDomainService`, and `@InjectMessageQueue(MessageQueue.deleteCascadeQueue) deleteCascadeMessageQueueService: MessageQueueService`.
   - Implemented `suspendWorkspace({ workspaceId, actorUserId })`:
     - **Workspace Validation**: Queries `workspaceRepository.findOne({ where: { id: workspaceId }, withDeleted: true })` and throws `BillingException` with `BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND` when `!isDefined(workspace) || isDefined(workspace.deletedAt)`.
     - **Suspension & Webhook Revocation**: Calls `this.workspaceService.suspendWorkspace(workspaceId)`, which sets `activationStatus = WorkspaceActivationStatus.SUSPENDED` and `suspendedAt`, invalidates the `workspaceEntity` cache via `CoreEntityCacheService`, and emits `WORKSPACE_SUSPENDED_EVENT` (handled by `WebhookSubscriptionWorkspaceActivationListener` to enqueue `SyncWorkspaceWebhookSubscriptionsJob` with `action: 'REVOKE'` for all Google and Microsoft `MessageChannel` / `CalendarChannel` webhook subscriptions).
     - **Billing Subscription Cancellation & Active Credit Grant Revocation**: When `IS_BILLING_ENABLED` is truthy, cancels the active subscription via `this.billingSubscriptionService.cancelSubscription(workspaceId)`, fetches workspace credit grants via `this.getWorkspaceCreditGrants(workspaceId)`, filters by `grant.isActive`, and revokes each active grant via `this.billingCreditService.revokeGrant({ workspaceId, grantId: grant.id, revokedByUserId: actorUserId })`.
     - **Emailing Domain Cleanup**: Retrieves the workspace's emailing domains via `this.emailingDomainService.getEmailingDomains(workspace)` and enqueues `EmailingDomainWorkspaceCleanupJob.name` on `MessageQueue.deleteCascadeQueue` with `{ workspaceId, domains }`.

4. **[admin-panel-billing.service.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/services/__tests__/admin-panel-billing.service.spec.ts)**
   - Added unit tests using `@nestjs/testing`'s `Test.createTestingModule` covering:
     - Throwing `BillingException` (`BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND`) when the workspace does not exist or is soft-deleted (`deletedAt` is non-null).
     - Full suspension flow when `IS_BILLING_ENABLED` is `true` (workspace suspension, subscription cancellation, revoking only `isActive` credit grants on behalf of `actorUserId`, and enqueuing `EmailingDomainWorkspaceCleanupJob`).
     - Suspension flow when `IS_BILLING_ENABLED` is `false` (workspace suspension and enqueuing `EmailingDomainWorkspaceCleanupJob` without calling billing cancellation or credit grant services).
