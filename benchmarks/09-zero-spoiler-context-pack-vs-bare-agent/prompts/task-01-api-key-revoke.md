# Bug Report: API Key Revocation Leaves Scoped Usage Limits, Cache & Counters Behind

Implement the fix in `C:/Work/bench-pilot/repos/twenty-ctx-exp` (`packages/twenty-server`).

## Problem Description
Workspaces can configure intra-workspace usage limits scoped to a specific API key (`spenderType === 'apiKey'` and `spenderId === <apiKeyId>`), including both workspace-admin limits (`isInstanceOverride: false`) and operator instance overrides (`isInstanceOverride: true`).

Currently, when a workspace API key is revoked in `twenty-server`, the API key record is marked as revoked (`revokedAt`), but all usage limits scoped to that API key remain persisted in the database, cached in the workspace cache, and active in usage counters.

## Required Behavior
Update the workspace API key revocation flow (taking an API key `id` and `workspaceId`) so that:
1. If no API key with `id` exists in `workspaceId`, return `null` without mutating any usage limits.
2. If the API key in `workspaceId` was already revoked prior to the call (`revokedAt` is already non-null), return the existing API key as-is without overwriting its original `revokedAt` timestamp or mutating usage limits.
3. When revoking a previously active (`revokedAt == null`) API key in `workspaceId`, mark it as revoked (`revokedAt = new Date()`) and remove all usage limits in `workspaceId` scoped to that API key (`spenderType === 'apiKey'` and `spenderId === id`, including both `isInstanceOverride: false` and `isInstanceOverride: true`) through Twenty's full usage-limit removal lifecycle (including workspace cache recomputation and dropping active usage-limit counters), while leaving usage limits belonging to other API keys, other spender types, or other workspaces untouched. Return the updated revoked API key.
