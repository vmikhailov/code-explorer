# Feature Specification: Admin Panel Workspace Suspension (`suspendWorkspaceAdminPanel`)

Implement a new `suspendWorkspaceAdminPanel` GraphQL mutation in the Admin Panel of `twenty-server` (`packages/twenty-server/src/engine/core-modules/admin-panel/`) so a server admin can suspend an active workspace for abuse or non-payment and clean up its active billing, webhook, and emailing domain resources.

## Requirements

1. **GraphQL Mutation (`AdminPanelResolver`)**:
   - Add a `suspendWorkspaceAdminPanel` mutation returning `Boolean` in `packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts`, guarded with `@UseGuards(AccessPolicyGuard(ADMIN_PANEL_ACCESS_POLICY), AdminPanelGuard)`.
   - Accept `@Args('workspaceId', { type: () => UUIDScalarType }) workspaceId: string` and `@AuthUser() actor: AuthContextUser`.
   - Delegate the operation to `AdminPanelBillingService.suspendWorkspace({ workspaceId, actorUserId: actor.id })` in `packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts` (updating `AdminPanelModule` imports/providers in `admin-panel.module.ts` if needed).

2. **Workspace Validation, Suspension & Connected-Account Webhook Revocation**:
   - Verify the target workspace exists and is not soft-deleted (`deletedAt` is null); if not found or deleted, throw `BillingException` with `BillingExceptionCode.BILLING_SUBSCRIPTION_EVENT_WORKSPACE_NOT_FOUND`.
   - Suspend the workspace (`activationStatus = WorkspaceActivationStatus.SUSPENDED`, `suspendedAt` timestamp, and workspace entity cache invalidation) and ensure all connected-account webhook subscriptions (`MessageChannel` / `CalendarChannel` for Google and Microsoft) for that workspace are revoked.

3. **Billing Subscription Cancellation & Credit Grant Revocation**:
   - When billing is enabled (`IS_BILLING_ENABLED`), cancel the workspace's active billing subscription and revoke all currently active credit grants (`isActive`) for the workspace on behalf of `actorUserId`.

4. **Emailing Domain Cleanup**:
   - Trigger cleanup of the workspace's configured emailing domains via `EmailingDomainWorkspaceCleanupJob`.

5. **Unit Tests**:
   - Create unit tests in `packages/twenty-server/src/engine/core-modules/admin-panel/services/__tests__/admin-panel-billing.service.spec.ts` covering `suspendWorkspace` (workspace not found error, billing enabled flow, and billing disabled flow).

6. **Completion Report**:
   - Once the code changes and unit test file are written and verified with your assigned graph CLI, send a concise summary of all modified/created files and how each requirement was satisfied via `send_message` to parent conversation `517d38de-0260-4402-81a4-a016ed53ecb9`.
