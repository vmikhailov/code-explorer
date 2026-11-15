# Implementation Summary: `resetTwoFactorAuthenticationMethodsForUser`

## Modified & Created Files
1. **[two-factor-authentication.resolver.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/two-factor-authentication/two-factor-authentication.resolver.ts#L317-L329)** (modified):
   - Added the `@Mutation(() => DeleteTwoFactorAuthenticationMethodDTO)` mutation `resetTwoFactorAuthenticationMethodsForUser` on `TwoFactorAuthenticationResolver`.
   - Guarded with `@UseGuards(AccessPolicyGuard(TWO_FACTOR_AUTHENTICATION_RECOVERY_ACCESS_POLICY))` requiring a standard `userSession` principal and `PermissionFlagType.SECURITY` (matching `twoFactorAuthenticationRecoveryStatus` and `revokeTwoFactorAuthenticationRecoveryCode`).
   - Accepts `@Args() { userId }: TwoFactorAuthenticationRecoveryTargetInput` and `@AuthWorkspace() workspace: WorkspaceEntity`, and delegates persistence mutations to `TwoFactorAuthenticationService.resetTwoFactorAuthenticationMethodsForUser(userId, workspace.id)` per `CLAUDE.md` backend resolver conventions.
2. **[two-factor-authentication.service.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/two-factor-authentication/two-factor-authentication.service.ts#L389-L410)** (modified):
   - Added `resetTwoFactorAuthenticationMethodsForUser(userId, workspaceId)` on `TwoFactorAuthenticationService`:
     - Resolves the target user's membership in the current workspace via `this.userWorkspaceService.getUserWorkspaceForUserOrThrow({ userId, workspaceId })`.
     - Deletes all `TwoFactorAuthenticationMethodEntity` rows for `{ userWorkspaceId: userWorkspace.id }` in `workspaceId` via `this.twoFactorAuthenticationMethodRepository.delete(workspaceId, { userWorkspaceId: userWorkspace.id })`.
     - Revokes all two-factor authentication recovery codes for `(workspaceId, userId)` including redeemed codes via `this.revokeRecoveryCodes({ workspaceId, userId, includeRedeemed: true })`.
     - Returns `{ success: true }`.
3. **[two-factor-authentication.resolver.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/two-factor-authentication/__tests__/two-factor-authentication.resolver.spec.ts#L1-L216)** (created):
   - Added unit tests covering `resetTwoFactorAuthenticationMethodsForUser`:
     - Verifies `userWorkspace` lookup, deletion of all 2FA methods for `userWorkspace.id` in `workspace.id`, revocation of recovery codes with `includeRedeemed: true`, and `{ success: true }` response.
     - Verifies error propagation without deleting 2FA methods or revoking recovery codes when `getUserWorkspaceForUserOrThrow` rejects.
     - Verifies the `AccessPolicyGuard(TWO_FACTOR_AUTHENTICATION_RECOVERY_ACCESS_POLICY)` metadata and `PermissionFlagType.SECURITY` enforcement.

## Architecture & Call-Flow Verification
- Re-indexed the repository with `codebase-memory-mcp.exe cli index_repository` and verified with `get_architecture` and `query_graph` that `TwoFactorAuthenticationResolver.resetTwoFactorAuthenticationMethodsForUser` calls `TwoFactorAuthenticationService.resetTwoFactorAuthenticationMethodsForUser`, which calls `revokeRecoveryCodes` and performs the repository mutations inside the domain service.
