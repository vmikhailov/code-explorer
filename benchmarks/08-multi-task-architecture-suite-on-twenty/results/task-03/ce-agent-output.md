# Implementation Summary: `suspendWorkspaceAdminPanel`

All requirements have been implemented and verified against the CodeExplorer semantic invariant gates (`SIG-TWENTY-001` through `SIG-TWENTY-004` — all **4/4 PASS**).

## Modified & Created Files

1. **[admin-panel.resolver.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts#L742-L752)**
   - Added `@Mutation(() => Boolean) async suspendWorkspaceAdminPanel(@Args('workspaceId', { type: () => UUIDScalarType }) workspaceId: string, @AuthUser() actor: AuthContextUser): Promise<boolean>` guarded with `@UseGuards(AccessPolicyGuard(ADMIN_PANEL_ACCESS_POLICY), AdminPanelGuard)`.
   - Delegated execution to `this.adminBillingService.suspendWorkspace({ workspaceId, actorUserId: actor.id })`.

2. **[admin-panel-billing.service.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts#L148-L198)**
   - Injected `WorkspaceService`, `EmailingDomainService`, and `@InjectMessageQueue(MessageQueue.deleteCascadeQueue) deleteCascadeMessageQueueService: MessageQueueService`.
   - Implemented `suspendWorkspace({ workspaceId, actorUserId })`:
     - **Workspace Validation**: Looks up the workspace via `workspaceRepository.findOne({ where: { id: workspaceId } })` and throws `BillingException` with `BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND` if missing or soft-deleted (`deletedAt` is defined).
     - **Workspace Suspension & Webhook Revocation**: Calls `this.workspaceService.suspendWorkspace(workspaceId)`, which sets `activationStatus = WorkspaceActivationStatus.SUSPENDED` and `suspendedAt`, invalidates `'workspaceEntity'` in `CoreEntityCacheService`, and emits `WORKSPACE_SUSPENDED_EVENT` (triggering `WebhookSubscriptionWorkspaceActivationListener` -> `SyncWorkspaceWebhookSubscriptionsJob` with `'REVOKE'` to revoke all Google/Microsoft `MessageChannel` and `CalendarChannel` webhook subscriptions).
     - **Billing Subscription Cancellation & Credit Grant Revocation**: When `IS_BILLING_ENABLED` is true, cancels the active subscription via `this.billingSubscriptionService.cancelSubscription(workspaceId)` and revokes all currently active (`isActive`) credit grants via `this.billingCreditService.revokeGrant({ workspaceId, grantId: grant.id, revokedByUserId: actorUserId })`.
     - **Emailing Domain Cleanup**: Fetches workspace emailing domains via `this.emailingDomainService.getEmailingDomains(workspace)` (preserving domain table encapsulation per `SIG-TWENTY-003`) and enqueues `EmailingDomainWorkspaceCleanupJob` on `MessageQueue.deleteCascadeQueue` (preserving the async queue boundary per `SIG-TWENTY-004`).

3. **[admin-panel.module.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.module.ts#L34-L90)**
   - Imported `WorkspaceModule` and `EmailingDomainModule` into `AdminPanelModule` so `WorkspaceService` and `EmailingDomainService` are available to `AdminPanelBillingService`.

4. **[admin-panel-billing.service.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/admin-panel/services/__tests__/admin-panel-billing.service.spec.ts#L1-L216)**
   - Created unit tests for `AdminPanelBillingService.suspendWorkspace` covering:
     - Workspace not found (`BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND`)
     - Soft-deleted workspace (`BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND`)
     - Billing enabled flow (workspace suspension, subscription cancellation, revoking only `isActive` credit grants on behalf of `actorUserId`, and enqueuing `EmailingDomainWorkspaceCleanupJob`)
     - Billing disabled flow (workspace suspension and emailing domain cleanup job enqueuing without subscription cancellation or credit grant revocation)
