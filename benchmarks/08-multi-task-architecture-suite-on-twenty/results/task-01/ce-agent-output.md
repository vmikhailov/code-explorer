# Implementation Summary: `resetTwoFactorAuthenticationMethodsForUser`

Implemented the workspace security-admin GraphQL mutation `resetTwoFactorAuthenticationMethodsForUser` in the `two-factor-authentication` module and verified that all architectural semantic invariant gates (`SIG-TWENTY-001`..`SIG-TWENTY-004`) pass with zero violations.

## Modified & Created Files

1. **[two-factor-authentication.resolver.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/two-factor-authentication/two-factor-authentication.resolver.ts#L317-L330)** *(modified)*
   - Added `@Mutation(() => DeleteTwoFactorAuthenticationMethodDTO)` `resetTwoFactorAuthenticationMethodsForUser`.
   - Guarded with `@UseGuards(AccessPolicyGuard(TWO_FACTOR_AUTHENTICATION_RECOVERY_ACCESS_POLICY))` requiring a standard user session with `PermissionFlagType.SECURITY`.
   - Accepts `@Args() { userId }: TwoFactorAuthenticationRecoveryTargetInput` and `@AuthWorkspace() workspace: WorkspaceEntity`, delegating the reset operation to `TwoFactorAuthenticationService.resetTwoFactorAuthenticationMethodsForUser({ userId, workspaceId: workspace.id })`.

2. **[two-factor-authentication.service.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/two-factor-authentication/two-factor-authentication.service.ts#L389-L412)** *(modified)*
   - Added `resetTwoFactorAuthenticationMethodsForUser({ userId, workspaceId })` on `TwoFactorAuthenticationService` (keeping database mutations inside the domain service in compliance with `SIG-TWENTY-001` GraphQL Resolver & Controller Transport Purity):
     - Resolves the target user's membership via `this.userWorkspaceService.getUserWorkspaceForUserOrThrow({ userId, workspaceId })`.
     - Deletes all `TwoFactorAuthenticationMethodEntity` records for `{ userWorkspaceId: userWorkspace.id }` in `workspaceId` via `this.twoFactorAuthenticationMethodRepository.delete(workspaceId, { userWorkspaceId: userWorkspace.id })`.
     - Revokes all two-factor authentication recovery codes for `(workspaceId, userId)` including redeemed ones via `this.revokeRecoveryCodes({ workspaceId, userId, includeRedeemed: true })`.
     - Returns `{ success: true }`.

3. **[two-factor-authentication.resolver.spec.ts](file:///C:/Work/bench-pilot/repos/twenty/packages/twenty-server/src/engine/core-modules/two-factor-authentication/__tests__/two-factor-authentication.resolver.spec.ts#L1-L236)** *(created)*
   - Added unit tests for `TwoFactorAuthenticationResolver.resetTwoFactorAuthenticationMethodsForUser`:
     - Verifies target `userWorkspace` lookup via `UserWorkspaceService.getUserWorkspaceForUserOrThrow`, deletion of all `TwoFactorAuthenticationMethodEntity` records for `{ userWorkspaceId }` in `workspace.id`, revocation of recovery codes with `includeRedeemed: true`, and `{ success: true }` return value.
     - Verifies error propagation when `getUserWorkspaceForUserOrThrow` rejects without deleting methods or revoking recovery codes.
     - Verifies `AccessPolicyGuard` metadata on `resetTwoFactorAuthenticationMethodsForUser` (`PermissionFlagType.SECURITY`, standard user session only).

## Architectural Verification (`ce scan` & `ce gate check`)

- Incremental index (`ce scan`) verified the `MUTATION:resetTwoFactorAuthenticationMethodsForUser` endpoint and call chain (`TwoFactorAuthenticationResolver.resetTwoFactorAuthenticationMethodsForUser` -> `TwoFactorAuthenticationService.resetTwoFactorAuthenticationMethodsForUser` -> `UserWorkspaceService.getUserWorkspaceForUserOrThrow`, `WorkspaceScopedRepository.delete`, `TwoFactorAuthenticationService.revokeRecoveryCodes`).
- `ce gate check` passed all 4 gates (`Failed: 0`).
