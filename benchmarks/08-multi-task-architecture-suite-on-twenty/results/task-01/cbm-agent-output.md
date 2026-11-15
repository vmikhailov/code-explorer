# Summary Report — `resetTwoFactorAuthenticationMethodsForUser`

Implemented the workspace security-admin GraphQL mutation `resetTwoFactorAuthenticationMethodsForUser` in the `two-factor-authentication` module (`packages/twenty-server/src/engine/core-modules/two-factor-authentication/`).

## Modified & Created Files

1. **Modified**: [two-factor-authentication.resolver.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/two-factor-authentication/two-factor-authentication.resolver.ts#L18-L344)
   - Injected `UserWorkspaceService` (`UserWorkspaceModule` is already imported in `TwoFactorAuthenticationModule`).
   - Added `@Mutation(() => DeleteTwoFactorAuthenticationMethodDTO)` `resetTwoFactorAuthenticationMethodsForUser` guarded with `@UseGuards(AccessPolicyGuard(TWO_FACTOR_AUTHENTICATION_RECOVERY_ACCESS_POLICY))` (requiring standard `userSession` and `PermissionFlagType.SECURITY`).
   - Accepted `@Args() { userId }: TwoFactorAuthenticationRecoveryTargetInput` and `@AuthWorkspace() workspace: WorkspaceEntity`.
   - Resolved the target user's `userWorkspace` via `this.userWorkspaceService.getUserWorkspaceForUserOrThrow({ userId, workspaceId: workspace.id })`.
   - Deleted all `TwoFactorAuthenticationMethodEntity` rows for `{ userWorkspaceId: userWorkspace.id }` in `workspace.id` via `this.twoFactorAuthenticationMethodRepository.delete(workspace.id, { userWorkspaceId: userWorkspace.id })`.
   - Revoked all recovery codes for `(workspace.id, userId)` with `includeRedeemed: true` via `this.twoFactorAuthenticationService.revokeRecoveryCodes({ workspaceId: workspace.id, userId, includeRedeemed: true })`.
   - Returned `{ success: true }`.

2. **Created**: [two-factor-authentication.resolver.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/two-factor-authentication/__tests__/two-factor-authentication.resolver.spec.ts#L1-L176)
   - Added unit tests for `TwoFactorAuthenticationResolver.resetTwoFactorAuthenticationMethodsForUser` verifying:
     - Target `userWorkspace` resolution via `UserWorkspaceService.getUserWorkspaceForUserOrThrow`, workspace-scoped deletion of all `TwoFactorAuthenticationMethodEntity` rows for `{ userWorkspaceId: userWorkspace.id }`, revocation of all recovery codes with `includeRedeemed: true`, and `{ success: true }` return value.
     - Error propagation when `getUserWorkspaceForUserOrThrow` rejects without deleting 2FA methods or revoking recovery codes.
     - Guard metadata (`TWO_FACTOR_AUTHENTICATION_RECOVERY_ACCESS_POLICY` with `PermissionFlagType.SECURITY`).
