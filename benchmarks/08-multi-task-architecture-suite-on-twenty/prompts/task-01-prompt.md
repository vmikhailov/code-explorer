# Task 01 Prompt — Admin Reset of Target User 2FA Methods (`twentyhq/twenty`)

Target Repository: `C:\Work\bench-pilot\repos\twenty`

## Feature Request: `resetTwoFactorAuthenticationMethodsForUser`

Implement a workspace security-admin GraphQL mutation `resetTwoFactorAuthenticationMethodsForUser` in the `two-factor-authentication` module (`packages/twenty-server/src/engine/core-modules/two-factor-authentication/`) so a workspace administrator with `PermissionFlagType.SECURITY` can reset all two-factor authentication methods and recovery codes for a target user in the current workspace.

### Functional Requirements

1. **GraphQL Mutation (`TwoFactorAuthenticationResolver`)**:
   - Add a `@Mutation(() => DeleteTwoFactorAuthenticationMethodDTO)` named `resetTwoFactorAuthenticationMethodsForUser` on `TwoFactorAuthenticationResolver` (`packages/twenty-server/src/engine/core-modules/two-factor-authentication/two-factor-authentication.resolver.ts`).
   - Guard it with `@UseGuards(AccessPolicyGuard(TWO_FACTOR_AUTHENTICATION_RECOVERY_ACCESS_POLICY))` (matching `twoFactorAuthenticationRecoveryStatus` and `revokeTwoFactorAuthenticationRecoveryCode` in the same resolver).
   - Accept `@Args() { userId }: TwoFactorAuthenticationRecoveryTargetInput` and `@AuthWorkspace() workspace: WorkspaceEntity`.
2. **Deletion & Recovery Code Revocation Logic**:
   - Resolve the target user's `userWorkspace` in `workspace.id` via `UserWorkspaceService.getUserWorkspaceForUserOrThrow({ userId, workspaceId: workspace.id })`.
   - Delete all `TwoFactorAuthenticationMethodEntity` records belonging to `{ userWorkspaceId: userWorkspace.id }` in `workspace.id`.
   - Revoke all two-factor authentication recovery codes for `(workspace.id, userId)` with `includeRedeemed: true`.
   - Return `{ success: true }`.
3. **Unit Tests**:
   - Add or update unit tests in `packages/twenty-server/src/engine/core-modules/two-factor-authentication/__tests__/two-factor-authentication.resolver.spec.ts` covering the new `resetTwoFactorAuthenticationMethodsForUser` flow.

### Constraints
- Do NOT run `yarn`, `nx`, `jest`, or `tsc` via `run_command` (no database/Redis containers are running in the benchmark sandbox).
- When finished, send your final summary report via `send_message` listing all modified/created files and explaining how `resetTwoFactorAuthenticationMethodsForUser` is wired.
