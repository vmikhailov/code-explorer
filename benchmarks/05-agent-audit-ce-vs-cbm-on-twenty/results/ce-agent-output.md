# Comprehensive 4-Part Architectural Audit — `twenty` Monorepo

---

## Task 1 — Service Method Call Graph & DI Fan-In (`Layer 3`)

Below is the complete non-test call graph for the 4 core services (`BillingSubscriptionService`, `FeatureFlagService`, `JwtWrapperService`, `ThrottlerService`).

> **Note on Test File Filtering**: Excluding `.spec.ts`, `.test.ts`, `__tests__`, and `test/integration/` (`.integration-spec.ts` / test utilities), there are **169 non-test call edges** across the 4 services (or **184 call edges** if `test/integration/` files are included; those 15 integration-test rows are listed separately at the end of each service section for completeness).

### Summary of Counts by Target Service

| Target Service | Distinct Called Methods (`calleeMethod`) | Distinct Non-Test Caller Functions (`CallerClass.callerMethod`) | Total Non-Test Call Edges | (Incl. `test/integration/` Callers / Edges) |
| :--- | :---: | :---: | :---: | :---: |
| **`BillingSubscriptionService`** | **10** | **31** | **54** | 32 callers / 55 edges |
| **`FeatureFlagService`** | **4** | **29** | **33** | 35 callers / 44 edges |
| **`JwtWrapperService`** | **7** | **30** | **58** | 34 callers / 62 edges |
| **`ThrottlerService`** | **3** | **24** | **24** | 24 callers / 24 edges |
| **Total** | **24** | **114** | **169** | **125 callers / 185 edges** |

---

### 1.1 `BillingSubscriptionService`
- **Distinct Called Methods**: `10` (`assertSubscriptionCanceledOrNone`, `cancelSubscription`, `endTrialPeriod`, `getBillingCustomer`, `getBillingSubscriptions`, `getCurrentBillingSubscription`, `getCurrentBillingSubscriptionOrThrow`, `getTrialPeriodFreeWorkflowCredits`, `getWorkspaceEntitlementByKey`, `getWorkspaceEntitlementValue`, `getWorkspaceEntitlements`, `handleUnpaidInvoices`, `syncSubscriptionToDatabase` — **13** distinct called methods total!)
  *(Correction: **13** distinct called methods on `BillingSubscriptionService`, **31** distinct non-test caller functions across **54** call edges)*

| Target `ServiceClass.calleeMethod` | Caller (`CallerClass.callerMethod`) | Caller Relative File Path | Line |
| :--- | :--- | :--- | :---: |
| `BillingSubscriptionService.assertSubscriptionCanceledOrNone` | `WorkspaceService.hardDeleteWorkspaceWithApplicationUninstallLock` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` | 930 |
| `BillingSubscriptionService.cancelSubscription` | `WorkspaceService.deleteWorkspace` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` | 886 |
| `BillingSubscriptionService.endTrialPeriod` | `BillingResolver.endSubscriptionTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 426 |
| `BillingSubscriptionService.getBillingCustomer` | `WorkspaceCurrentBillingSubscriptionCacheService.computeForCache` | `packages/twenty-server/src/engine/core-modules/billing/services/workspace-current-billing-subscription-cache.service.ts` | 30 |
| `BillingSubscriptionService.getBillingCustomer` | `WorkspaceResolver.billingCustomer` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` | 418 |
| `BillingSubscriptionService.getBillingSubscriptions` | `BillingResolver.cancelSwitchBillingInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 339 |
| `BillingSubscriptionService.getBillingSubscriptions` | `BillingResolver.cancelSwitchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 322 |
| `BillingSubscriptionService.getBillingSubscriptions` | `BillingResolver.cancelSwitchResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 504 |
| `BillingSubscriptionService.getBillingSubscriptions` | `BillingResolver.endSubscriptionTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 426 |
| `BillingSubscriptionService.getBillingSubscriptions` | `BillingResolver.setResourceCreditSubscriptionPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 360 |
| `BillingSubscriptionService.getBillingSubscriptions` | `BillingResolver.switchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 302 |
| `BillingSubscriptionService.getBillingSubscriptions` | `BillingResolver.switchSubscriptionInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 283 |
| `BillingSubscriptionService.getBillingSubscriptions` | `WorkspaceResolver.billingSubscriptions` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` | 312 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `AdminPanelBillingService.getWorkspaceBilling` | `packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts` | 165 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `BillingWebhookInvoiceService.processInvoiceFinalized` | `packages/twenty-server/src/engine/core-modules/billing-webhook/services/billing-webhook-invoice.service.ts` | 72 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `BillingCreditService.writeGrantAndRefreshState` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-credit.service.ts` | 62 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `BillingPartnerCodeService.isTrialingOrWithoutSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-partner-code.service.ts` | 104 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `BillingSubscriptionService.assertSubscriptionCanceledOrNone` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` | 144 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `BillingSubscriptionService.cancelSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` | 132 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` | 114 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `BillingSubscriptionService.handleUnpaidInvoices` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` | 157 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `WorkspaceCurrentBillingSubscriptionCacheService.computeForCache` | `packages/twenty-server/src/engine/core-modules/billing/services/workspace-current-billing-subscription-cache.service.ts` | 30 |
| `BillingSubscriptionService.getCurrentBillingSubscription` | `WorkspaceResolver.currentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` | 386 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingResolver.cancelSwitchBillingInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 339 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingResolver.cancelSwitchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 322 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingResolver.cancelSwitchResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 504 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingResolver.endSubscriptionTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 426 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingResolver.setResourceCreditSubscriptionPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 360 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingResolver.switchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 302 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingResolver.switchSubscriptionInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | 283 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingUpdateSubscriptionPriceCommand.runOnWorkspace` | `packages/twenty-server/src/engine/core-modules/billing/commands/billing-update-subscription-price.command.ts` | 67 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingSubscriptionUpdateService.cancelSwitchInterval` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | 169 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingSubscriptionUpdateService.cancelSwitchPlan` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | 153 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingSubscriptionUpdateService.cancelSwitchResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | 131 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingSubscriptionUpdateService.changeInterval` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | 183 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingSubscriptionUpdateService.changePlan` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | 200 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingSubscriptionUpdateService.changeResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | 80 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingSubscriptionUpdateService.changeSeats` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | 224 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingSubscriptionService.endTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` | 281 |
| `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingUsageService.getResourceCreditProductUsage` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-usage.service.ts` | 79 |
| `BillingSubscriptionService.getTrialPeriodFreeWorkflowCredits` | `BillingUsageService.getTrialResourceUsageCap` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-usage.service.ts` | 171 |
| `BillingSubscriptionService.getWorkspaceEntitlementByKey` | `BillingSubscriptionService.getWorkspaceEntitlementValue` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` | 261 |
| `BillingSubscriptionService.getWorkspaceEntitlementByKey` | `BillingService.hasEntitlement` | `packages/twenty-server/src/engine/core-modules/billing/services/billing.service.ts` | 129 |
| `BillingSubscriptionService.getWorkspaceEntitlementValue` | `EnableCommonRecordSharingCommand.up` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-43/2-43-workspace-command-1790312694997-enable-common-record-sharing.command.ts` | 45 |
| `BillingSubscriptionService.getWorkspaceEntitlementValue` | `EventLogRetentionService.hasAuditLogsEntitlement` | `packages/twenty-server/src/engine/core-modules/event-logs/retention/services/event-log-retention.service.ts` | 57 |
| `BillingSubscriptionService.getWorkspaceEntitlementValue` | `UsageLimitEntitlementService.isIntraWorkspaceLimitEntitled` | `packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-limit-entitlement.service.ts` | 17 |
| `BillingSubscriptionService.getWorkspaceEntitlementValue` | `RelatedRecordPathService.assertHasRelatedRecordPathsEntitlementOrThrow` | `packages/twenty-server/src/engine/metadata-modules/related-record-path/related-record-path.service.ts` | 34 |
| `BillingSubscriptionService.getWorkspaceEntitlementValue` | `ValidationRuleService.hasValidationRulesEntitlement` | `packages/twenty-server/src/engine/metadata-modules/validation-rule/validation-rule.service.ts` | 46 |
| `BillingSubscriptionService.getWorkspaceEntitlements` | `WorkspaceResolver.billingEntitlements` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` | 449 |
| `BillingSubscriptionService.handleUnpaidInvoices` | `BillingWebhookController.handleStripeEvent` | `packages/twenty-server/src/engine/core-modules/billing-webhook/billing-webhook.controller.ts` | 93 |
| `BillingSubscriptionService.syncSubscriptionToDatabase` | `BillingPortalWorkspaceService.createDirectSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-portal.workspace-service.ts` | 165 |
| `BillingSubscriptionService.syncSubscriptionToDatabase` | `BillingPortalWorkspaceService.createSubscriptionPaymentIntent` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-portal.workspace-service.ts` | 215 |
| `BillingSubscriptionService.syncSubscriptionToDatabase` | `BillingSubscriptionUpdateService.updateSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | 236 |
| `BillingSubscriptionService.syncSubscriptionToDatabase` | `BillingSubscriptionService.endTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` | 281 |

*(Integration test caller in `.integration-spec.ts`, if only `.spec.ts`/`.test.ts` are filtered: `Jest:Test:reuses cached rows until invalidation, then exposes a revoked entitlement to all readers` -> `BillingSubscriptionService.getWorkspaceEntitlements` in `packages/twenty-server/test/integration/workspace-cache/billing-entitlements-cache.integration-spec.ts:140`)*

---

### 1.2 `FeatureFlagService`
- **Distinct Called Methods**: `4` (`enableFeatureFlags`, `getWorkspaceFeatureFlags`, `getWorkspaceFeatureFlagsMap`, `isFeatureEnabled`, `upsertWorkspaceFeatureFlag` — **5** distinct called methods total!)
- **Distinct Non-Test Caller Functions**: **29** across **35** call edges (or **35** callers across **44** call edges if `test/integration/` files are included).

| Target `ServiceClass.calleeMethod` | Caller (`CallerClass.callerMethod`) | Caller Relative File Path | Line |
| :--- | :--- | :--- | :---: |
| `FeatureFlagService.enableFeatureFlags` | `BackfillRecordPageLayoutsCommand.runOnWorkspace` | `packages/twenty-server/src/database/commands/upgrade-version-command/1-23/1-23-workspace-command-1780000001500-backfill-record-page-layouts.command.ts` | 47 |
| `FeatureFlagService.enableFeatureFlags` | `ConvertLogicFunctionsToPrebuiltCommand.runOnWorkspace` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-39/2-39-workspace-command-1788338950836-convert-logic-functions-to-prebuilt.command.ts` | 52 |
| `FeatureFlagService.enableFeatureFlags` | `WorkspaceService.activateWorkspace` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` | 678 |
| `FeatureFlagService.getWorkspaceFeatureFlags` | `WorkspaceResolver.featureFlags` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` | 269 |
| `FeatureFlagService.getWorkspaceFeatureFlagsMap` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/core-modules/feature-flag/services/feature-flag.service.ts` | 27 |
| `FeatureFlagService.getWorkspaceFeatureFlagsMap` | `DefinedToolRegistryService.execute` | `packages/twenty-server/src/engine/core-modules/tool-provider/services/defined-tool-registry.service.ts` | 105 |
| `FeatureFlagService.getWorkspaceFeatureFlagsMap` | `DefinedToolRegistryService.getAvailableDescriptors` | `packages/twenty-server/src/engine/core-modules/tool-provider/services/defined-tool-registry.service.ts` | 55 |
| `FeatureFlagService.getWorkspaceFeatureFlagsMap` | `AccessPolicyService.assertRequestAccessPolicySatisfiedOrThrow` | `packages/twenty-server/src/engine/guards/access-policy/access-policy.service.ts` | 37 |
| `FeatureFlagService.isFeatureEnabled` | `MigrateMessagingInfrastructureToMetadataCommand.runOnWorkspace` | `packages/twenty-server/src/database/commands/upgrade-version-command/1-21/1-21-workspace-command-1775500012000-migrate-messaging-infrastructure-to-metadata.command.ts` | 63 |
| `FeatureFlagService.isFeatureEnabled` | `BackfillRecordPageLayoutsCommand.runOnWorkspace` | `packages/twenty-server/src/database/commands/upgrade-version-command/1-23/1-23-workspace-command-1780000001500-backfill-record-page-layouts.command.ts` | 47 |
| `FeatureFlagService.isFeatureEnabled` | `ConvertLogicFunctionsToPrebuiltCommand.runOnWorkspace` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-39/2-39-workspace-command-1788338950836-convert-logic-functions-to-prebuilt.command.ts` | 52 |
| `FeatureFlagService.isFeatureEnabled` | `OnboardingService.setOnboardingInviteTeamPendingFromInviteSuggestions` | `packages/twenty-server/src/engine/core-modules/onboarding/onboarding.service.ts` | 616 |
| `FeatureFlagService.isFeatureEnabled` | `ShareRecordTool.isEnabled` | `packages/twenty-server/src/engine/core-modules/tool/tools/share-record-tool/share-record-tool.ts` | 37 |
| `FeatureFlagService.isFeatureEnabled` | `WorkspaceAssistantProfileService.assertInboxEnabledOrThrow` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace-assistant-profile.service.ts` | 153 |
| `FeatureFlagService.isFeatureEnabled` | `WorkspaceService.assertFeatureIsEnabledOrThrow` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` | 227 |
| `FeatureFlagService.isFeatureEnabled` | `AgentChatMemberMessageSenderService.sendMemberMessage` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/agent-chat-member-message-sender.service.ts` | 66 |
| `FeatureFlagService.isFeatureEnabled` | `AgentChatReplyExecutionPolicyService.shouldExecute` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/agent-chat-reply-execution-policy.service.ts` | 31 |
| `FeatureFlagService.isFeatureEnabled` | `ChatExecutionService.buildExecutionProfile` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/chat-execution.service.ts` | 313 |
| `FeatureFlagService.isFeatureEnabled` | `SystemPromptBuilderService.buildPreview` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/system-prompt-builder.service.ts` | 33 |
| `FeatureFlagService.isFeatureEnabled` | `AiFrontComponentService.isEnabled` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-front-component/ai-front-component.service.ts` | 53 |
| `FeatureFlagService.isFeatureEnabled` | `AiFrontComponentToolService.generateTools` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-front-component/tools/services/ai-front-component-tool.service.ts` | 29 |
| `FeatureFlagService.isFeatureEnabled` | `WorkspaceAiDataPolicyService.findStoredAiDataPolicy` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-models/services/workspace-ai-data-policy.service.ts` | 40 |
| `FeatureFlagService.isFeatureEnabled` | `FieldMetadataController.isNewMetadataFormat` | `packages/twenty-server/src/engine/metadata-modules/field-metadata/controllers/field-metadata.controller.ts` | 252 |
| `FeatureFlagService.isFeatureEnabled` | `ApplicationMessageIngestionService.findMessageIdsWithAttachments` | `packages/twenty-server/src/engine/metadata-modules/message-channel/services/application-message-ingestion.service.ts` | 162 |
| `FeatureFlagService.isFeatureEnabled` | `ObjectMetadataController.isNewMetadataFormat` | `packages/twenty-server/src/engine/metadata-modules/object-metadata/controllers/object-metadata.controller.ts` | 270 |
| `FeatureFlagService.isFeatureEnabled` | `CalendarSaveEventsService.saveCalendarEventsAndEnqueueContactCreationJob` | `packages/twenty-server/src/modules/calendar/calendar-event-import-manager/services/calendar-save-events.service.ts` | 36 |
| `FeatureFlagService.isFeatureEnabled` | `MessagingMessageAttachmentService.saveMessageAttachments` | `packages/twenty-server/src/modules/messaging/message-import-manager/services/messaging-message-attachment.service.ts` | 32 |
| `FeatureFlagService.isFeatureEnabled` | `SentMessagePersistenceService.uploadAttachmentFiles` | `packages/twenty-server/src/modules/messaging/message-outbound-manager/services/sent-message-persistence.service.ts` | 102 |
| `FeatureFlagService.isFeatureEnabled` | `WorkflowVersionStepOperationsWorkspaceService.assertActionTypeEnabled` | `packages/twenty-server/src/modules/workflow/workflow-builder/workflow-version-step/workflow-version-step-operations.workspace-service.ts` | 722 |
| `FeatureFlagService.isFeatureEnabled` | `WorkflowExecutorWorkspaceService.assertActionTypeEnabled` | `packages/twenty-server/src/modules/workflow/workflow-executor/workspace-services/workflow-executor.workspace-service.ts` | 391 |
| `FeatureFlagService.upsertWorkspaceFeatureFlag` | `UpdateFeatureFlagCommand.runOnWorkspace` | `packages/twenty-server/src/database/commands/update-feature-flag.command.ts` | 69 |
| `FeatureFlagService.upsertWorkspaceFeatureFlag` | `EnableDirectMessagePairIdentityCommand.runOnWorkspace` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-56/2-56-workspace-command-1793639083813-enable-direct-message-pair-identity.command.ts` | 36 |
| `FeatureFlagService.upsertWorkspaceFeatureFlag` | `AddMessageAttachmentTargetCommand.runOnWorkspace` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-57/2-57-workspace-command-1793876432369-add-message-attachment-target.command.ts` | 45 |
| `FeatureFlagService.upsertWorkspaceFeatureFlag` | `AdminPanelResolver.updateWorkspaceFeatureFlag` | `packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts` | 285 |
| `FeatureFlagService.upsertWorkspaceFeatureFlag` | `LabResolver.updateLabPublicFeatureFlag` | `packages/twenty-server/src/engine/core-modules/lab/lab.resolver.ts` | 47 |

*(Integration test callers in `packages/twenty-server/test/integration/` if only `.spec.ts`/`.test.ts` are filtered:
- `FeatureFlagService.isFeatureEnabled` <- `createChatParticipationTestContext` (`packages/twenty-server/test/integration/ai/utils/create-chat-participation-test-context.util.ts:37`)
- `FeatureFlagService.isFeatureEnabled` <- `Jest:Suite:workspace assistant mode permissions and persistence` (`packages/twenty-server/test/integration/graphql/suites/settings-permissions/ai-chat-assistant-modes.integration-spec.ts:116`)
- `FeatureFlagService.isFeatureEnabled` <- `Jest:Suite:workspace assistant profile settings` (`packages/twenty-server/test/integration/graphql/suites/settings-permissions/ai-chat-assistant-profile.integration-spec.ts:68`)
- `FeatureFlagService.upsertWorkspaceFeatureFlag` <- `cleanup` (`packages/twenty-server/test/integration/ai/utils/create-chat-participation-test-context.util.ts:291`)
- `FeatureFlagService.upsertWorkspaceFeatureFlag` <- `createChatParticipationTestContext` (`packages/twenty-server/test/integration/ai/utils/create-chat-participation-test-context.util.ts:37`)
- `FeatureFlagService.upsertWorkspaceFeatureFlag` <- `setInboxEnabled` (`packages/twenty-server/test/integration/graphql/suites/settings-permissions/ai-chat-assistant-modes.integration-spec.ts:26`)
- `FeatureFlagService.upsertWorkspaceFeatureFlag` <- `setInboxEnabled` (`packages/twenty-server/test/integration/graphql/suites/settings-permissions/ai-chat-assistant-profile.integration-spec.ts:48`)
- `FeatureFlagService.upsertWorkspaceFeatureFlag` <- `setIdentityFlag` (`packages/twenty-server/test/integration/upgrade/suites/2-56-workspace-command-1793639083811-add-direct-message-pair-identity.integration-spec.ts:75`)
- `FeatureFlagService.upsertWorkspaceFeatureFlag` <- `setMessageAttachmentFlag` (`packages/twenty-server/test/integration/upgrade/suites/2-57-workspace-command-1793876432369-add-message-attachment-target.integration-spec.ts:49`))*

---

### 1.3 `JwtWrapperService`
- **Distinct Called Methods**: **8** (`decode`, `extractAppSecretBody`, `extractJwtFromRequest`, `generateAppSecret`, `recordVerifyForAlgorithm`, `resolveVerificationKey`, `signAsyncOrThrow`, `toAuthException`, `verifyJwtToken` — **9** distinct called methods total!)
- **Distinct Non-Test Caller Functions**: **30** across **58** call edges (or **34** callers across **62** call edges if `test/integration/` files are included).

| Target `ServiceClass.calleeMethod` | Caller (`CallerClass.callerMethod`) | Caller Relative File Path | Line |
| :--- | :--- | :--- | :---: |
| `JwtWrapperService.decode` | `ApplicationRegistrationClaimService.verifyClaimState` | `packages/twenty-server/src/engine/core-modules/application/application-registration/application-registration-claim.service.ts` | 126 |
| `JwtWrapperService.decode` | `AccessTokenService.validateToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` | 184 |
| `JwtWrapperService.decode` | `ApplicationTokenService.decodeToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` | 275 |
| `JwtWrapperService.decode` | `ApplicationTokenService.validateApplicationAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` | 244 |
| `JwtWrapperService.decode` | `ApplicationTokenService.validateApplicationRefreshToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` | 173 |
| `JwtWrapperService.decode` | `LoginTokenService.verifyLoginToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/login-token.service.ts` | 67 |
| `JwtWrapperService.decode` | `RefreshTokenService.verifyRefreshToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/refresh-token.service.ts` | 35 |
| `JwtWrapperService.decode` | `TransientTokenService.verifyTransientToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/transient-token.service.ts` | 50 |
| `JwtWrapperService.decode` | `WorkspaceAgnosticTokenService.validateToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/workspace-agnostic-token.service.ts` | 68 |
| `JwtWrapperService.decode` | `FileByIdGuard.canActivate` | `packages/twenty-server/src/engine/core-modules/file/guards/file-by-id.guard.ts` | 38 |
| `JwtWrapperService.decode` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` | 129 |
| `JwtWrapperService.decode` | `UserSessionService.buildCreateSessionInputFromTokenPair` | `packages/twenty-server/src/engine/core-modules/user-session/services/user-session.service.ts` | 235 |
| `JwtWrapperService.decode` | `UserSessionService.revokePresentedRefreshToken` | `packages/twenty-server/src/engine/core-modules/user-session/services/user-session.service.ts` | 577 |
| `JwtWrapperService.extractAppSecretBody` | `JwtWrapperService.resolveVerificationKey` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` | 86 |
| `JwtWrapperService.extractAppSecretBody` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` | 129 |
| `JwtWrapperService.extractJwtFromRequest` | `JwtAuthStrategy.constructor` | `packages/twenty-server/src/engine/core-modules/auth/strategies/jwt.auth.strategy.ts` | 39 |
| `JwtWrapperService.extractJwtFromRequest` | `AccessTokenService.validateTokenByRequest` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` | 194 |
| `JwtWrapperService.extractJwtFromRequest` | `RecordExportWorkspaceService.getRequestTokenHashOrThrow` | `packages/twenty-server/src/engine/core-modules/record-export/services/record-export.workspace-service.ts` | 608 |
| `JwtWrapperService.extractJwtFromRequest` | `CookieSessionCsrfMiddleware.use` | `packages/twenty-server/src/engine/middlewares/cookie-session-csrf.middleware.ts` | 23 |
| `JwtWrapperService.extractJwtFromRequest` | `MiddlewareService.clearDeadSessionCookie` | `packages/twenty-server/src/engine/middlewares/middleware.service.ts` | 135 |
| `JwtWrapperService.extractJwtFromRequest` | `MiddlewareService.isTokenPresent` | `packages/twenty-server/src/engine/middlewares/middleware.service.ts` | 48 |
| `JwtWrapperService.generateAppSecret` | `SimpleSecretEncryptionUtil.decryptSecret` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-5/utils/simple-secret-encryption.util.ts` | 18 |
| `JwtWrapperService.generateAppSecret` | `JwtWrapperService.resolveVerificationKey` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` | 86 |
| `JwtWrapperService.generateAppSecret` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` | 129 |
| `JwtWrapperService.recordVerifyForAlgorithm` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` | 129 |
| `JwtWrapperService.resolveVerificationKey` | `secretOrKeyProvider` *(module/callback function)* | `packages/twenty-server/src/engine/core-modules/auth/strategies/jwt.auth.strategy.ts` | 49 |
| `JwtWrapperService.resolveVerificationKey` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` | 129 |
| `JwtWrapperService.signAsyncOrThrow` | `ApiKeyService.generateApiKeyToken` | `packages/twenty-server/src/engine/core-modules/api-key/services/api-key.service.ts` | 158 |
| `JwtWrapperService.signAsyncOrThrow` | `ApplicationRegistrationClaimService.buildGithubAuthorizationUrl` | `packages/twenty-server/src/engine/core-modules/application/application-registration/application-registration-claim.service.ts` | 78 |
| `JwtWrapperService.signAsyncOrThrow` | `ConnectionProviderAuthorizationStateService.sign` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-authorization-state.service.ts` | 18 |
| `JwtWrapperService.signAsyncOrThrow` | `ApprovedAccessDomainService.mintValidationToken` | `packages/twenty-server/src/engine/core-modules/approved-access-domain/services/approved-access-domain.service.ts` | 136 |
| `JwtWrapperService.signAsyncOrThrow` | `AccessTokenService.generateAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` | 112 |
| `JwtWrapperService.signAsyncOrThrow` | `AccessTokenService.generatePlaygroundToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` | 151 |
| `JwtWrapperService.signAsyncOrThrow` | `ApplicationTokenService.signApplicationToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` | 325 |
| `JwtWrapperService.signAsyncOrThrow` | `LoginTokenService.generateLoginToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/login-token.service.ts` | 33 |
| `JwtWrapperService.signAsyncOrThrow` | `RefreshTokenService.generateRefreshToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/refresh-token.service.ts` | 114 |
| `JwtWrapperService.signAsyncOrThrow` | `TransientTokenService.generateTransientToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/transient-token.service.ts` | 23 |
| `JwtWrapperService.signAsyncOrThrow` | `WorkspaceAgnosticTokenService.generateWorkspaceAgnosticToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/workspace-agnostic-token.service.ts` | 31 |
| `JwtWrapperService.signAsyncOrThrow` | `FileUploadTargetService.buildUploadTarget` | `packages/twenty-server/src/engine/core-modules/file/file-upload/services/file-upload-target.service.ts` | 45 |
| `JwtWrapperService.signAsyncOrThrow` | `FileUrlService.buildSignedFileUrl` | `packages/twenty-server/src/engine/core-modules/file/file-url/file-url.service.ts` | 89 |
| `JwtWrapperService.signAsyncOrThrow` | `RecordExportWorkspaceService.getDownloadPath` | `packages/twenty-server/src/engine/core-modules/record-export/services/record-export.workspace-service.ts` | 334 |
| `JwtWrapperService.toAuthException` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` | 129 |
| `JwtWrapperService.verifyJwtToken` | `ApplicationRegistrationClaimService.verifyClaimState` | `packages/twenty-server/src/engine/core-modules/application/application-registration/application-registration-claim.service.ts` | 126 |
| `JwtWrapperService.verifyJwtToken` | `ConnectionProviderAuthorizationStateService.verifyOrThrow` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-authorization-state.service.ts` | 47 |
| `JwtWrapperService.verifyJwtToken` | `ApprovedAccessDomainService.verifyValidationTokenOrThrow` | `packages/twenty-server/src/engine/core-modules/approved-access-domain/services/approved-access-domain.service.ts` | 155 |
| `JwtWrapperService.verifyJwtToken` | `AccessTokenService.validateToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` | 184 |
| `JwtWrapperService.verifyJwtToken` | `ApplicationTokenService.validateApplicationAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` | 244 |
| `JwtWrapperService.verifyJwtToken` | `ApplicationTokenService.validateApplicationRefreshToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` | 173 |
| `JwtWrapperService.verifyJwtToken` | `LoginTokenService.verifyLoginToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/login-token.service.ts` | 67 |
| `JwtWrapperService.verifyJwtToken` | `RefreshTokenService.verifyRefreshToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/refresh-token.service.ts` | 35 |
| `JwtWrapperService.verifyJwtToken` | `TransientTokenService.verifyTransientToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/transient-token.service.ts` | 50 |
| `JwtWrapperService.verifyJwtToken` | `WorkspaceAgnosticTokenService.validateToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/workspace-agnostic-token.service.ts` | 68 |
| `JwtWrapperService.verifyJwtToken` | `FileUploadTokenGuard.canActivate` | `packages/twenty-server/src/engine/core-modules/file/file-upload/guards/file-upload-token.guard.ts` | 11 |
| `JwtWrapperService.verifyJwtToken` | `FileByIdGuard.canActivate` | `packages/twenty-server/src/engine/core-modules/file/guards/file-by-id.guard.ts` | 38 |
| `JwtWrapperService.verifyJwtToken` | `RecordExportWorkspaceService.verifyDownloadToken` | `packages/twenty-server/src/engine/core-modules/record-export/services/record-export.workspace-service.ts` | 427 |
| `JwtWrapperService.verifyJwtToken` | `ServerRouteAuthorizationHeaderService.isTwentyCredential` | `packages/twenty-server/src/engine/core-modules/server-route-trigger/server-route-authorization-header.service.ts` | 39 |
| `JwtWrapperService.verifyJwtToken` | `UserSessionService.revokePresentedRefreshToken` | `packages/twenty-server/src/engine/core-modules/user-session/services/user-session.service.ts` | 577 |

*(Integration test callers in `packages/twenty-server/test/integration/` if only `.spec.ts`/`.test.ts` are filtered:
- `JwtWrapperService.signAsyncOrThrow` <- `Jest:Test:refuses the callback for a member without the connected accounts permission` (`packages/twenty-server/test/integration/metadata/suites/connection-provider/app-connection-connected-accounts-permission.integration-spec.ts:201`)
- `JwtWrapperService.signAsyncOrThrow` <- `Jest:Test:does not expose registered exports through the standard file download route` (`packages/twenty-server/test/integration/record-export/record-export-file-cleanup.integration-spec.ts:147`)
- `JwtWrapperService.signAsyncOrThrow` <- `Jest:Test:rejects a signed %s` (`packages/twenty-server/test/integration/record-export/record-export.integration-spec.ts:1272`)
- `JwtWrapperService.verifyJwtToken` <- `authorization` (`packages/twenty-server/test/integration/record-export/record-export.integration-spec.ts:216`))*

---

### 1.4 `ThrottlerService`
- **Distinct Called Methods**: **3** (`tokenBucketConsumeUpTo`, `tokenBucketThrottleOrThrow`, `tryConsumeTokenBuckets`)
- **Distinct Non-Test Caller Functions**: **24** across **24** call edges.

| Target `ServiceClass.calleeMethod` | Caller (`CallerClass.callerMethod`) | Caller Relative File Path | Line |
| :--- | :--- | :--- | :---: |
| `ThrottlerService.tokenBucketConsumeUpTo` | `AgentTriggerThrottlerService.consumeAvailableRuns` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-trigger/services/agent-trigger-throttler.service.ts` | 11 |
| `ThrottlerService.tokenBucketConsumeUpTo` | `WorkflowThrottlingWorkspaceService.consumeRemainingRunsToEnqueueCount` | `packages/twenty-server/src/modules/workflow/workflow-runner/workflow-run-queue/workspace-services/workflow-throttling.workspace-service.ts` | 23 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `ApplicationDevelopmentService.throttlePerApplication` | `packages/twenty-server/src/engine/core-modules/application/application-development/application-development.service.ts` | 333 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `ApplicationHealthCheckService.runForUser` | `packages/twenty-server/src/engine/core-modules/application/application-health/application-health-check.service.ts` | 65 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `ApplicationOAuthResolver.renewApplicationToken` | `packages/twenty-server/src/engine/core-modules/application/application-oauth/application-oauth.resolver.ts` | 52 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `OAuthRegistrationController.applyRateLimit` | `packages/twenty-server/src/engine/core-modules/application/application-oauth/controllers/oauth-registration.controller.ts` | 219 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `OAuthTokenController.applyRateLimit` | `packages/twenty-server/src/engine/core-modules/application/application-oauth/controllers/oauth-token.controller.ts` | 149 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `AuthResolver.emailPasswordResetLink` | `packages/twenty-server/src/engine/core-modules/auth/auth.resolver.ts` | 1235 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `AppBillingController.charge` | `packages/twenty-server/src/engine/core-modules/billing/app-billing/app-billing.controller.ts` | 115 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `AppBillingController.credits` | `packages/twenty-server/src/engine/core-modules/billing/app-billing/app-billing.controller.ts` | 72 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `EnrichmentThrottleService.consumeToken` | `packages/twenty-server/src/engine/core-modules/company-enrichment/services/enrichment-throttle.service.ts` | 13 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `LogicFunctionExecutorService.throttleExecution` | `packages/twenty-server/src/engine/core-modules/logic-function/logic-function-executor/logic-function-executor.service.ts` | 409 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `TwoFactorAuthenticationRecoveryService.generateRecoveryCode` | `packages/twenty-server/src/engine/core-modules/two-factor-authentication/services/two-factor-authentication-recovery.service.ts` | 94 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `TwoFactorAuthenticationRecoveryService.redeemRecoveryCode` | `packages/twenty-server/src/engine/core-modules/two-factor-authentication/services/two-factor-authentication-recovery.service.ts` | 276 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `TwoFactorAuthenticationService.validateStrategy` | `packages/twenty-server/src/engine/core-modules/two-factor-authentication/two-factor-authentication.service.ts` | 195 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `WorkspaceInvitationService.throttleInvitationSending` | `packages/twenty-server/src/engine/core-modules/workspace-invitation/services/workspace-invitation.service.ts` | 499 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `UnsubscribeController.throttleOrThrow` | `packages/twenty-server/src/modules/emailing/controllers/unsubscribe.controller.ts` | 66 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `CampaignEngagementCaptureService.throttleAndEnqueue` | `packages/twenty-server/src/modules/emailing/services/campaign-engagement-capture.service.ts` | 57 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `MessageCampaignService.sendTest` | `packages/twenty-server/src/modules/emailing/services/message-campaign.service.ts` | 419 |
| `ThrottlerService.tokenBucketThrottleOrThrow` | `WorkflowThrottlingWorkspaceService.throttleOrThrowIfHardLimitReached` | `packages/twenty-server/src/modules/workflow/workflow-runner/workflow-run-queue/workspace-services/workflow-throttling.workspace-service.ts` | 35 |
| `ThrottlerService.tryConsumeTokenBuckets` | `ApplicationJobEnqueueThrottlerService.throttleOrThrow` | `packages/twenty-server/src/engine/core-modules/message-queue/services/application-job-enqueue-throttler.service.ts` | 20 |
| `ThrottlerService.tryConsumeTokenBuckets` | `ThrottlerService.tokenBucketConsumeUpTo` | `packages/twenty-server/src/engine/core-modules/throttler/throttler.service.ts` | 94 |
| `ThrottlerService.tryConsumeTokenBuckets` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/throttler/throttler.service.ts` | 67 |
| `ThrottlerService.tryConsumeTokenBuckets` | `UsageLimitSpeedService.consumeTokens` | `packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-limit-speed.service.ts` | 162 |

---

## Task 2 — Multi-Hop Class Inheritance & Interface Realization (`Layer 3`)

### 2(a) Inherits Directly or Transitively (`1..3` hops) from `SyncableEntity`
- **Total Count**: **43** classes (`33` direct `1`-hop subclasses including `OverridableEntity`, plus `10` transitive `2`-hop subclasses extending `OverridableEntity -> SyncableEntity`).

| # | Class Name | Relative File Path | Hops from `SyncableEntity` |
| :---: | :--- | :--- | :---: |
| 1 | `AgentChatChannelEntity` | `packages/twenty-server/src/engine/metadata-modules/agent-chat-channel/entities/agent-chat-channel.entity.ts` | 1 |
| 2 | `AgentChatChannelRoleEntity` | `packages/twenty-server/src/engine/metadata-modules/agent-chat-channel/entities/agent-chat-channel-role.entity.ts` | 1 |
| 3 | `AgentEntity` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent/entities/agent.entity.ts` | 2 (`OverridableEntity`) |
| 4 | `ApplicationVariableEntity` | `packages/twenty-server/src/engine/core-modules/application/application-variable/application-variable.entity.ts` | 1 |
| 5 | `CommandMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/command-menu-item/entities/command-menu-item.entity.ts` | 2 (`OverridableEntity`) |
| 6 | `ConnectionProviderEntity` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider.entity.ts` | 1 |
| 7 | `FieldMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/field-metadata/field-metadata.entity.ts` | 1 |
| 8 | `FieldMetadataOptionEntity` | `packages/twenty-server/src/engine/metadata-modules/field-metadata-option/entities/field-metadata-option.entity.ts` | 2 (`OverridableEntity`) |
| 9 | `FieldPermissionEntity` | `packages/twenty-server/src/engine/metadata-modules/object-permission/field-permission/field-permission.entity.ts` | 1 |
| 10 | `FrontComponentEntity` | `packages/twenty-server/src/engine/metadata-modules/front-component/entities/front-component.entity.ts` | 1 |
| 11 | `IndexMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/index-metadata/index-metadata.entity.ts` | 1 |
| 12 | `LogicFunctionEntity` | `packages/twenty-server/src/engine/metadata-modules/logic-function/logic-function.entity.ts` | 1 |
| 13 | `McpServerEntity` | `packages/twenty-server/src/engine/metadata-modules/mcp-server/entities/mcp-server.entity.ts` | 1 |
| 14 | `MessagingProviderEntity` | `packages/twenty-server/src/engine/metadata-modules/messaging-provider/entities/messaging-provider.entity.ts` | 1 |
| 15 | `NavigationMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/navigation-menu-item/entities/navigation-menu-item.entity.ts` | 1 |
| 16 | `ObjectMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/object-metadata/object-metadata.entity.ts` | 1 |
| 17 | `ObjectPermissionEntity` | `packages/twenty-server/src/engine/metadata-modules/object-permission/object-permission.entity.ts` | 1 |
| 18 | `OverridableEntity` | `packages/twenty-server/src/engine/workspace-manager/types/overridable-entity.type.ts` | 1 |
| 19 | `PageLayoutEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout/entities/page-layout.entity.ts` | 1 |
| 20 | `PageLayoutTabEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout-tab/entities/page-layout-tab.entity.ts` | 2 (`OverridableEntity`) |
| 21 | `PageLayoutWidgetEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout-widget/entities/page-layout-widget.entity.ts` | 2 (`OverridableEntity`) |
| 22 | `PermissionFlagEntity` | `packages/twenty-server/src/engine/metadata-modules/permission-flag/permission-flag.entity.ts` | 1 |
| 23 | `RelatedRecordPathEntity` | `packages/twenty-server/src/engine/metadata-modules/related-record-path/entities/related-record-path.entity.ts` | 1 |
| 24 | `RoleEntity` | `packages/twenty-server/src/engine/metadata-modules/role/role.entity.ts` | 1 |
| 25 | `RolePermissionFlagEntity` | `packages/twenty-server/src/engine/metadata-modules/role-permission-flag/role-permission-flag.entity.ts` | 1 |
| 26 | `RoleTargetEntity` | `packages/twenty-server/src/engine/metadata-modules/role-target/role-target.entity.ts` | 1 |
| 27 | `RowLevelPermissionPredicateEntity` | `packages/twenty-server/src/engine/metadata-modules/row-level-permission-predicate/entities/row-level-permission-predicate.entity.ts` | 1 |
| 28 | `RowLevelPermissionPredicateGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/row-level-permission-predicate/entities/row-level-permission-predicate-group.entity.ts` | 1 |
| 29 | `SearchFieldMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/search-field-metadata/search-field-metadata.entity.ts` | 1 |
| 30 | `SettingsMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/settings-menu-item/entities/settings-menu-item.entity.ts` | 1 |
| 31 | `SkillEntity` | `packages/twenty-server/src/engine/metadata-modules/skill/entities/skill.entity.ts` | 2 (`OverridableEntity`) |
| 32 | `TimelineActivityTypeEntity` | `packages/twenty-server/src/engine/metadata-modules/timeline-activity-type/entities/timeline-activity-type.entity.ts` | 2 (`OverridableEntity`) |
| 33 | `ValidationRuleEntity` | `packages/twenty-server/src/engine/metadata-modules/validation-rule/entities/validation-rule.entity.ts` | 1 |
| 34 | `ViewEntity` | `packages/twenty-server/src/engine/metadata-modules/view/entities/view.entity.ts` | 2 (`OverridableEntity`) |
| 35 | `ViewFieldEntity` | `packages/twenty-server/src/engine/metadata-modules/view-field/entities/view-field.entity.ts` | 2 (`OverridableEntity`) |
| 36 | `ViewFieldGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-field-group/entities/view-field-group.entity.ts` | 2 (`OverridableEntity`) |
| 37 | `ViewFilterEntity` | `packages/twenty-server/src/engine/metadata-modules/view-filter/entities/view-filter.entity.ts` | 1 |
| 38 | `ViewFilterGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-filter-group/entities/view-filter-group.entity.ts` | 1 |
| 39 | `ViewGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-group/entities/view-group.entity.ts` | 1 |
| 40 | `ViewSortEntity` | `packages/twenty-server/src/engine/metadata-modules/view-sort/entities/view-sort.entity.ts` | 1 |
| 41 | `WebhookEntity` | `packages/twenty-server/src/engine/metadata-modules/webhook/entities/webhook.entity.ts` | 1 |
| 42 | `WorkflowEntity` | `packages/twenty-server/src/engine/core-modules/workflow/entities/workflow.entity.ts` | 1 |
| 43 | `WorkflowVersionEntity` | `packages/twenty-server/src/engine/core-modules/workflow/entities/workflow-version.entity.ts` | 1 |

---

### 2(b) Inherits Directly or Transitively (`1..3` hops) from `WorkspaceRelatedEntity`
- **Total Count (`1..3` hops)**: **67** classes
  - **Direct (`1`-hop) subclasses**: **24** (including `SyncableEntity`)
  - **2-hop subclasses (`extends SyncableEntity`)**: **33** (so `1..2` hops = **57** classes)
  - **3-hop subclasses (`extends OverridableEntity -> SyncableEntity -> WorkspaceRelatedEntity`)**: **10** (bringing `1..3` hops total to **67** classes)

| # | Class Name | Relative File Path | Hops from `WorkspaceRelatedEntity` |
| :---: | :--- | :--- | :---: |
| 1 | `ApiKeyEntity` | `packages/twenty-server/src/engine/core-modules/api-key/api-key.entity.ts` | 1 |
| 2 | `ApplicationEntity` | `packages/twenty-server/src/engine/core-modules/application/application.entity.ts` | 1 |
| 3 | `ApprovedAccessDomainEntity` | `packages/twenty-server/src/engine/core-modules/approved-access-domain/approved-access-domain.entity.ts` | 1 |
| 4 | `BillingCreditGrantEntity` | `packages/twenty-server/src/engine/core-modules/billing/entities/billing-credit-grant.entity.ts` | 1 |
| 5 | `BillingCustomerEntity` | `packages/twenty-server/src/engine/core-modules/billing/entities/billing-customer.entity.ts` | 1 |
| 6 | `BillingEntitlementEntity` | `packages/twenty-server/src/engine/core-modules/billing/entities/billing-entitlement.entity.ts` | 1 |
| 7 | `BillingSubscriptionEntity` | `packages/twenty-server/src/engine/core-modules/billing/entities/billing-subscription.entity.ts` | 1 |
| 8 | `CalendarChannelEntity` | `packages/twenty-server/src/engine/metadata-modules/calendar-channel/entities/calendar-channel.entity.ts` | 1 |
| 9 | `ConnectedAccountEntity` | `packages/twenty-server/src/engine/metadata-modules/connected-account/entities/connected-account.entity.ts` | 1 |
| 10 | `DataSourceEntity` | `packages/twenty-server/src/engine/metadata-modules/data-source/data-source.entity.ts` | 1 |
| 11 | `DeferredWorkspaceMigrationActionEntity` | `packages/twenty-server/src/engine/metadata-modules/deferred-workspace-migration-action/deferred-workspace-migration-action.entity.ts` | 1 |
| 12 | `EmailingDomainEntity` | `packages/twenty-server/src/engine/core-modules/emailing-domain/emailing-domain.entity.ts` | 1 |
| 13 | `FeatureFlagEntity` | `packages/twenty-server/src/engine/core-modules/feature-flag/feature-flag.entity.ts` | 1 |
| 14 | `LogicFunctionLayerEntity` | `packages/twenty-server/src/engine/metadata-modules/logic-function-layer/logic-function-layer.entity.ts` | 1 |
| 15 | `MessageChannelEntity` | `packages/twenty-server/src/engine/metadata-modules/message-channel/entities/message-channel.entity.ts` | 1 |
| 16 | `MessageFolderEntity` | `packages/twenty-server/src/engine/metadata-modules/message-folder/entities/message-folder.entity.ts` | 1 |
| 17 | `PendingWakeUpEntity` | `packages/twenty-server/src/engine/core-modules/pending-wake-up/entities/pending-wake-up.entity.ts` | 1 |
| 18 | `PublicDomainEntity` | `packages/twenty-server/src/engine/core-modules/public-domain/public-domain.entity.ts` | 1 |
| 19 | `SyncableEntity` | `packages/twenty-server/src/engine/workspace-manager/types/syncable-entity.interface.ts` | 1 |
| 20 | `UnsubscribeTopicEntity` | `packages/twenty-server/src/engine/core-modules/emailing-domain/unsubscribe-topic.entity.ts` | 1 |
| 21 | `UsageLimitEntity` | `packages/twenty-server/src/engine/core-modules/usage-limit/usage-limit.entity.ts` | 1 |
| 22 | `UserApplicationVariableValueEntity` | `packages/twenty-server/src/engine/core-modules/application/application-variable/user-application-variable-value.entity.ts` | 1 |
| 23 | `UserWorkspaceEntity` | `packages/twenty-server/src/engine/core-modules/user-workspace/user-workspace.entity.ts` | 1 |
| 24 | `WorkspaceSsoIdentityProviderEntity` | `packages/twenty-server/src/engine/core-modules/sso/workspace-sso-identity-provider.entity.ts` | 1 |
| 25 | `AgentChatChannelEntity` | `packages/twenty-server/src/engine/metadata-modules/agent-chat-channel/entities/agent-chat-channel.entity.ts` | 2 |
| 26 | `AgentChatChannelRoleEntity` | `packages/twenty-server/src/engine/metadata-modules/agent-chat-channel/entities/agent-chat-channel-role.entity.ts` | 2 |
| 27 | `ApplicationVariableEntity` | `packages/twenty-server/src/engine/core-modules/application/application-variable/application-variable.entity.ts` | 2 |
| 28 | `ConnectionProviderEntity` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider.entity.ts` | 2 |
| 29 | `FieldMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/field-metadata/field-metadata.entity.ts` | 2 |
| 30 | `FieldPermissionEntity` | `packages/twenty-server/src/engine/metadata-modules/object-permission/field-permission/field-permission.entity.ts` | 2 |
| 31 | `FrontComponentEntity` | `packages/twenty-server/src/engine/metadata-modules/front-component/entities/front-component.entity.ts` | 2 |
| 32 | `IndexMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/index-metadata/index-metadata.entity.ts` | 2 |
| 33 | `LogicFunctionEntity` | `packages/twenty-server/src/engine/metadata-modules/logic-function/logic-function.entity.ts` | 2 |
| 34 | `McpServerEntity` | `packages/twenty-server/src/engine/metadata-modules/mcp-server/entities/mcp-server.entity.ts` | 2 |
| 35 | `MessagingProviderEntity` | `packages/twenty-server/src/engine/metadata-modules/messaging-provider/entities/messaging-provider.entity.ts` | 2 |
| 36 | `NavigationMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/navigation-menu-item/entities/navigation-menu-item.entity.ts` | 2 |
| 37 | `ObjectMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/object-metadata/object-metadata.entity.ts` | 2 |
| 38 | `ObjectPermissionEntity` | `packages/twenty-server/src/engine/metadata-modules/object-permission/object-permission.entity.ts` | 2 |
| 39 | `OverridableEntity` | `packages/twenty-server/src/engine/workspace-manager/types/overridable-entity.type.ts` | 2 |
| 40 | `PageLayoutEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout/entities/page-layout.entity.ts` | 2 |
| 41 | `PermissionFlagEntity` | `packages/twenty-server/src/engine/metadata-modules/permission-flag/permission-flag.entity.ts` | 2 |
| 42 | `RelatedRecordPathEntity` | `packages/twenty-server/src/engine/metadata-modules/related-record-path/entities/related-record-path.entity.ts` | 2 |
| 43 | `RoleEntity` | `packages/twenty-server/src/engine/metadata-modules/role/role.entity.ts` | 2 |
| 44 | `RolePermissionFlagEntity` | `packages/twenty-server/src/engine/metadata-modules/role-permission-flag/role-permission-flag.entity.ts` | 2 |
| 45 | `RoleTargetEntity` | `packages/twenty-server/src/engine/metadata-modules/role-target/role-target.entity.ts` | 2 |
| 46 | `RowLevelPermissionPredicateEntity` | `packages/twenty-server/src/engine/metadata-modules/row-level-permission-predicate/entities/row-level-permission-predicate.entity.ts` | 2 |
| 47 | `RowLevelPermissionPredicateGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/row-level-permission-predicate/entities/row-level-permission-predicate-group.entity.ts` | 2 |
| 48 | `SearchFieldMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/search-field-metadata/search-field-metadata.entity.ts` | 2 |
| 49 | `SettingsMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/settings-menu-item/entities/settings-menu-item.entity.ts` | 2 |
| 50 | `ValidationRuleEntity` | `packages/twenty-server/src/engine/metadata-modules/validation-rule/entities/validation-rule.entity.ts` | 2 |
| 51 | `ViewFilterEntity` | `packages/twenty-server/src/engine/metadata-modules/view-filter/entities/view-filter.entity.ts` | 2 |
| 52 | `ViewFilterGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-filter-group/entities/view-filter-group.entity.ts` | 2 |
| 53 | `ViewGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-group/entities/view-group.entity.ts` | 2 |
| 54 | `ViewSortEntity` | `packages/twenty-server/src/engine/metadata-modules/view-sort/entities/view-sort.entity.ts` | 2 |
| 55 | `WebhookEntity` | `packages/twenty-server/src/engine/metadata-modules/webhook/entities/webhook.entity.ts` | 2 |
| 56 | `WorkflowEntity` | `packages/twenty-server/src/engine/core-modules/workflow/entities/workflow.entity.ts` | 2 |
| 57 | `WorkflowVersionEntity` | `packages/twenty-server/src/engine/core-modules/workflow/entities/workflow-version.entity.ts` | 2 |
| 58 | `AgentEntity` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent/entities/agent.entity.ts` | 3 |
| 59 | `CommandMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/command-menu-item/entities/command-menu-item.entity.ts` | 3 |
| 60 | `FieldMetadataOptionEntity` | `packages/twenty-server/src/engine/metadata-modules/field-metadata-option/entities/field-metadata-option.entity.ts` | 3 |
| 61 | `PageLayoutTabEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout-tab/entities/page-layout-tab.entity.ts` | 3 |
| 62 | `PageLayoutWidgetEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout-widget/entities/page-layout-widget.entity.ts` | 3 |
| 63 | `SkillEntity` | `packages/twenty-server/src/engine/metadata-modules/skill/entities/skill.entity.ts` | 3 |
| 64 | `TimelineActivityTypeEntity` | `packages/twenty-server/src/engine/metadata-modules/timeline-activity-type/entities/timeline-activity-type.entity.ts` | 3 |
| 65 | `ViewEntity` | `packages/twenty-server/src/engine/metadata-modules/view/entities/view.entity.ts` | 3 |
| 66 | `ViewFieldEntity` | `packages/twenty-server/src/engine/metadata-modules/view-field/entities/view-field.entity.ts` | 3 |
| 67 | `ViewFieldGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-field-group/entities/view-field-group.entity.ts` | 3 |

---

### 2(c) Inherits from `WorkspaceEntityMigrationBuilderService`
- **Total Count**: **42** classes.

| # | Class Name | Relative File Path |
| :---: | :--- | :--- |
| 1 | `WorkspaceMigrationAgentActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/agent/workspace-migration-agent-actions-builder.service.ts` |
| 2 | `WorkspaceMigrationAgentChatChannelActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/agent-chat-channel/workspace-migration-agent-chat-channel-actions-builder.service.ts` |
| 3 | `WorkspaceMigrationAgentChatChannelRoleActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/agent-chat-channel-role/workspace-migration-agent-chat-channel-role-actions-builder.service.ts` |
| 4 | `WorkspaceMigrationApplicationVariableActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/application-variable/workspace-migration-application-variable-actions-builder.service.ts` |
| 5 | `WorkspaceMigrationCommandMenuItemActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/command-menu-item/workspace-migration-command-menu-item-actions-builder.service.ts` |
| 6 | `WorkspaceMigrationConnectionProviderActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/connection-provider/workspace-migration-connection-provider-actions-builder.service.ts` |
| 7 | `WorkspaceMigrationFieldActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/field/workspace-migration-field-actions-builder.service.ts` |
| 8 | `WorkspaceMigrationFieldMetadataOptionActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/field-metadata-option/workspace-migration-field-metadata-option-actions-builder.service.ts` |
| 9 | `WorkspaceMigrationFieldPermissionActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/field-permission/workspace-migration-field-permission-actions-builder.service.ts` |
| 10 | `WorkspaceMigrationFrontComponentActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/front-component/workspace-migration-front-component-actions-builder.service.ts` |
| 11 | `WorkspaceMigrationIndexActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/index/workspace-migration-index-actions-builder.service.ts` |
| 12 | `WorkspaceMigrationLogicFunctionActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/logic-function/workspace-migration-logic-function-actions-builder.service.ts` |
| 13 | `WorkspaceMigrationMcpServerActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/mcp-server/workspace-migration-mcp-server-actions-builder.service.ts` |
| 14 | `WorkspaceMigrationMessagingProviderActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/messaging-provider/workspace-migration-messaging-provider-actions-builder.service.ts` |
| 15 | `WorkspaceMigrationNavigationMenuItemActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/navigation-menu-item/workspace-migration-navigation-menu-item-actions-builder.service.ts` |
| 16 | `WorkspaceMigrationObjectActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/object/workspace-migration-object-actions-builder.service.ts` |
| 17 | `WorkspaceMigrationObjectPermissionActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/object-permission/workspace-migration-object-permission-actions-builder.service.ts` |
| 18 | `WorkspaceMigrationPageLayoutActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/page-layout/workspace-migration-page-layout-actions-builder.service.ts` |
| 19 | `WorkspaceMigrationPageLayoutTabActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/page-layout-tab/workspace-migration-page-layout-tab-actions-builder.service.ts` |
| 20 | `WorkspaceMigrationPageLayoutWidgetActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/page-layout-widget/workspace-migration-page-layout-widget-actions-builder.service.ts` |
| 21 | `WorkspaceMigrationPermissionFlagActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/permission-flag/workspace-migration-permission-flag-actions-builder.service.ts` |
| 22 | `WorkspaceMigrationRelatedRecordPathActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/related-record-path/workspace-migration-related-record-path-actions-builder.service.ts` |
| 23 | `WorkspaceMigrationRoleActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/role/workspace-migration-role-actions-builder.service.ts` |
| 24 | `WorkspaceMigrationRolePermissionFlagActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/role-permission-flag/workspace-migration-role-permission-flag-actions-builder.service.ts` |
| 25 | `WorkspaceMigrationRoleTargetActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/role-target/workspace-migration-role-target-actions-builder.service.ts` |
| 26 | `WorkspaceMigrationRowLevelPermissionPredicateActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/row-level-permission-predicate/workspace-migration-row-level-permission-predicate-actions-builder.service.ts` |
| 27 | `WorkspaceMigrationRowLevelPermissionPredicateGroupActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/row-level-permission-predicate-group/workspace-migration-row-level-permission-predicate-group-actions-builder.service.ts` |
| 28 | `WorkspaceMigrationSearchFieldMetadataActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/search-field-metadata/workspace-migration-search-field-metadata-actions.builder.service.ts` |
| 29 | `WorkspaceMigrationSettingsMenuItemActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/settings-menu-item/workspace-migration-settings-menu-item-actions-builder.service.ts` |
| 30 | `WorkspaceMigrationSkillActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/skill/workspace-migration-skill-actions-builder.service.ts` |
| 31 | `WorkspaceMigrationTimelineActivityTypeActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/timeline-activity-type/workspace-migration-timeline-activity-type-actions-builder.service.ts` |
| 32 | `WorkspaceMigrationValidationRuleActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/validation-rule/workspace-migration-validation-rule-actions-builder.service.ts` |
| 33 | `WorkspaceMigrationViewActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/view/workspace-migration-view-actions-builder.service.ts` |
| 34 | `WorkspaceMigrationViewFieldActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/view-field/workspace-migration-view-field-actions-builder.service.ts` |
| 35 | `WorkspaceMigrationViewFieldGroupActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/view-field-group/workspace-migration-view-field-group-actions-builder.service.ts` |
| 36 | `WorkspaceMigrationViewFilterActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/view-filter/workspace-migration-view-filter-actions-builder.service.ts` |
| 37 | `WorkspaceMigrationViewFilterGroupActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/view-filter-group/workspace-migration-view-filter-group-actions-builder.service.ts` |
| 38 | `WorkspaceMigrationViewGroupActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/view-group/workspace-migration-view-group-actions-builder.service.ts` |
| 39 | `WorkspaceMigrationViewSortActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/view-sort/workspace-migration-view-sort-actions.builder.service.ts` |
| 40 | `WorkspaceMigrationWebhookActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/webhook/workspace-migration-webhook-actions-builder.service.ts` |
| 41 | `WorkspaceMigrationWorkflowActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/workflow/workspace-migration-workflow-actions-builder.service.ts` |
| 42 | `WorkspaceMigrationWorkflowVersionActionsBuilderService` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/builders/workflow-version/workspace-migration-workflow-version-actions-builder.service.ts` |

---

### 2(d) Implements `WorkspaceResolverBuilderFactoryInterface`
- **Total Count**: **15** classes.

| # | Class Name | Relative File Path |
| :---: | :--- | :--- |
| 1 | `CreateManyResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/create-many-resolver.factory.ts` |
| 2 | `CreateOneResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/create-one-resolver.factory.ts` |
| 3 | `DeleteManyResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/delete-many-resolver.factory.ts` |
| 4 | `DeleteOneResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/delete-one-resolver.factory.ts` |
| 5 | `DestroyManyResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/destroy-many-resolver.factory.ts` |
| 6 | `DestroyOneResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/destroy-one-resolver.factory.ts` |
| 7 | `FindDuplicatesResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/find-duplicates-resolver.factory.ts` |
| 8 | `FindManyResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/find-many-resolver.factory.ts` |
| 9 | `FindOneResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/find-one-resolver.factory.ts` |
| 10 | `GroupByResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/group-by-resolver.factory.ts` |
| 11 | `MergeManyResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/merge-many-resolver.factory.ts` |
| 12 | `RestoreManyResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/restore-many-resolver.factory.ts` |
| 13 | `RestoreOneResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/restore-one-resolver.factory.ts` |
| 14 | `UpdateManyResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/update-many-resolver.factory.ts` |
| 15 | `UpdateOneResolverFactory` | `packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/factories/update-one-resolver.factory.ts` |

---

## Task 3 — Asynchronous BullMQ Queue Producers & Consumers (`Layer 4`)

### Summary of Queue Producers (`PUBLISHED_BY`) & Consumers (`SUBSCRIBED_BY`)

| BullMQ Queue (`Topic`) | Publishers Count | Subscribers Count |
| :--- | :---: | :---: |
| **`webhook-queue`** | **12** | **7** |
| **`workspace-queue`** | **8** | **11** |
| **`campaign-queue`** | **8** | **9** |
| **`ai-queue`** | **6** | **4** |
| **`delete-cascade-queue`** | **5** | **6** |
| **Total** | **39** | **37** |

---

### 3.1 `webhook-queue`

#### (a) Publishers / Enqueuers (`12` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `EntityEventsToDbListener.enqueueWebhookJobsIfAnyWebhookMatches` | `enqueueWebhookJobsIfAnyWebhookMatches` | `packages/twenty-server/src/engine/api/graphql/workspace-query-runner/listeners/entity-events-to-db.listener.ts` |
| `GoogleAPIsService.enqueueWebhookSubscriptionCreation` | `enqueueWebhookSubscriptionCreation` | `packages/twenty-server/src/engine/core-modules/auth/services/google-apis.service.ts` |
| `MicrosoftAPIsService.enqueueWebhookSubscriptionCreation` | `enqueueWebhookSubscriptionCreation` | `packages/twenty-server/src/engine/core-modules/auth/services/microsoft-apis.service.ts` |
| `CallWebhookJobsForMetadataJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook-jobs-for-metadata.job.ts` |
| `CallWebhookJobsJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook-jobs.job.ts` |
| `MetadataEventsToDbListener.handleEvent` | `handleEvent` | `packages/twenty-server/src/engine/subscriptions/metadata-event/metadata-events-to-db.listener.ts` |
| `ChannelSyncService.startCalendarChannelSync` | `startCalendarChannelSync` | `packages/twenty-server/src/modules/connected-account/channel-sync/services/channel-sync.service.ts` |
| `ChannelSyncService.startMessageChannelSync` | `startMessageChannelSync` | `packages/twenty-server/src/modules/connected-account/channel-sync/services/channel-sync.service.ts` |
| `CreateWebhookSubscriptionForConnectedAccountCommand.enqueueChannels` | `enqueueChannels` | `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/commands/create-webhook-subscription-for-connected-account.command.ts` |
| `WebhookSubscriptionRenewalCronJob.enqueueRenewals` | `enqueueRenewals` | `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/crons/jobs/webhook-subscription-renewal.cron.job.ts` |
| `WebhookSubscriptionWorkspaceActivationListener.enqueueSync` | `enqueueSync` | `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/listeners/webhook-subscription-workspace-activation.listener.ts` |
| `WebhookSubscriptionWorkspaceSyncService.enqueue` | `enqueue` | `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/services/webhook-subscription-workspace-sync.service.ts` |

#### (b) Subscribers / Processors (`7` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `CallWebhookJobsForMetadataJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook-jobs-for-metadata.job.ts` |
| `CallWebhookJobsJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook-jobs.job.ts` |
| `CallWebhookJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook.job.ts` |
| `CreateWebhookSubscriptionJob.handle` | `handle` | `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/jobs/create-webhook-subscription.job.ts` |
| `RenewWebhookSubscriptionJob.handle` | `handle` | `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/jobs/renew-webhook-subscription.job.ts` |
| `RevokeWebhookSubscriptionJob.handle` | `handle` | `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/jobs/revoke-webhook-subscription.job.ts` |
| `SyncWorkspaceWebhookSubscriptionsJob.handle` | `handle` | `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/jobs/sync-workspace-webhook-subscriptions.job.ts` |

---

### 3.2 `workspace-queue`

#### (a) Publishers / Enqueuers (`8` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `ApplicationLifecycleJobService.enqueueLifecycleJob` | `enqueueLifecycleJob` | `packages/twenty-server/src/engine/core-modules/application/application-install/services/application-lifecycle-job.service.ts` |
| `PreInstalledAppsService.enqueueInstallOnWorkspace` | `enqueueInstallOnWorkspace` | `packages/twenty-server/src/engine/core-modules/application/pre-installed-apps/pre-installed-apps.service.ts` |
| `BillingWebhookSubscriptionService.applySubscriptionEvent` | `applySubscriptionEvent` | `packages/twenty-server/src/engine/core-modules/billing-webhook/services/billing-webhook-subscription.service.ts` |
| `SdkClientGenerationService.enqueueSdkClientGenerationForApplication` | `enqueueSdkClientGenerationForApplication` | `packages/twenty-server/src/engine/core-modules/sdk-client/sdk-client-generation.service.ts` |
| `UserService.enqueueWorkspaceMemberEmailUpdate` | `enqueueWorkspaceMemberEmailUpdate` | `packages/twenty-server/src/engine/core-modules/user/services/user.service.ts` |
| `AgentChatChannelGrantService.enqueueChannelGrantsRewrite` | `enqueueChannelGrantsRewrite` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/agent-chat-channel-grant.service.ts` |
| `TrashCleanupCronJob.handle` | `handle` | `packages/twenty-server/src/engine/trash-cleanup/crons/trash-cleanup.cron.job.ts` |
| `DeferredWorkspaceMigrationActionRunnerService.enqueue` | `enqueue` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-runner/services/deferred-workspace-migration-action-runner.service.ts` |

#### (b) Subscribers / Processors (`11` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `TriggerInstallApplicationJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/application/application-install/jobs/trigger-install-application.job.ts` |
| `TriggerUninstallApplicationJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/application/application-install/jobs/trigger-uninstall-application.job.ts` |
| `TriggerUpgradeApplicationJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/application/application-upgrade/jobs/trigger-upgrade-application.job.ts` |
| `InstallPreInstalledAppsJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/application/pre-installed-apps/jobs/install-pre-installed-apps.job.ts` |
| `GenerateSdkClientJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/sdk-client/jobs/generate-sdk-client.job.ts` |
| `UpdateWorkspaceMemberEmailJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/user/jobs/update-workspace-member-email.job.ts` |
| `HandleWorkspaceMemberDeletedJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/workspace/handle-workspace-member-deleted.job.ts` |
| `RewriteAgentChatChannelGrantsJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/jobs/rewrite-agent-chat-channel-grants.job.ts` |
| `TrashCleanupJob.handle` | `handle` | `packages/twenty-server/src/engine/trash-cleanup/jobs/trash-cleanup.job.ts` |
| `CleanWorkspaceDeletionWarningUserVarsJob.handle` | `handle` | `packages/twenty-server/src/engine/workspace-manager/workspace-cleaner/jobs/clean-workspace-deletion-warning-user-vars.job.ts` |
| `RunDeferredWorkspaceMigrationActionsJob.handle` | `handle` | `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-runner/jobs/run-deferred-workspace-migration-actions.job.ts` |

---

### 3.3 `campaign-queue`

#### (a) Publishers / Enqueuers (`8` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `AddPeopleToMessageListJobService.enqueueAddPeopleToMessageListJob` | `enqueueAddPeopleToMessageListJob` | `packages/twenty-server/src/modules/emailing/services/add-people-to-message-list-job.service.ts` |
| `CampaignSendingWorkspaceService.enqueueJobForEachSendingWorkspace` | `enqueueJobForEachSendingWorkspace` | `packages/twenty-server/src/modules/emailing/services/campaign-sending-workspace.service.ts` |
| `MessageCampaignDeliveryService.requeueRateLimitedSendJob` | `requeueRateLimitedSendJob` | `packages/twenty-server/src/modules/emailing/services/message-campaign-delivery.service.ts` |
| `MessageCampaignMaterializationService.enqueueMaterializationChunks` | `enqueueMaterializationChunks` | `packages/twenty-server/src/modules/emailing/services/message-campaign-materialization.service.ts` |
| `MessageCampaignScheduleService.schedule` | `schedule` | `packages/twenty-server/src/modules/emailing/services/message-campaign-schedule.service.ts` |
| `MessageCampaignStatisticsService.scheduleRefresh` | `scheduleRefresh` | `packages/twenty-server/src/modules/emailing/services/message-campaign-statistics.service.ts` |
| `MessageCampaignService.claimAndMaterializeOrThrow` | `claimAndMaterializeOrThrow` | `packages/twenty-server/src/modules/emailing/services/message-campaign.service.ts` |
| `OutboundDeliveryEventHandlerService.handle` | `handle` | `packages/twenty-server/src/modules/messaging-webhooks/handlers/outbound-delivery-event-handler.service.ts` |

#### (b) Subscribers / Processors (`9` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `AddPeopleToMessageListJob.handle` | `handle` | `packages/twenty-server/src/modules/emailing/jobs/add-people-to-message-list.job.ts` |
| `MaterializeCampaignChunkJob.handle` | `handle` | `packages/twenty-server/src/modules/emailing/jobs/materialize-campaign-chunk.job.ts` |
| `MaterializeCampaignJob.handle` | `handle` | `packages/twenty-server/src/modules/emailing/jobs/materialize-campaign.job.ts` |
| `ReconcileWorkspaceCampaignStatsJob.handle` | `handle` | `packages/twenty-server/src/modules/emailing/jobs/reconcile-workspace-campaign-stats.job.ts` |
| `RecoverWorkspaceStaleCampaignsJob.handle` | `handle` | `packages/twenty-server/src/modules/emailing/jobs/recover-workspace-stale-campaigns.job.ts` |
| `RefreshCampaignStatsJob.handle` | `handle` | `packages/twenty-server/src/modules/emailing/jobs/refresh-campaign-stats.job.ts` |
| `SendCampaignEmailJob.handle` | `handle` | `packages/twenty-server/src/modules/emailing/jobs/send-campaign-email.job.ts` |
| `SendScheduledCampaignJob.handle` | `handle` | `packages/twenty-server/src/modules/emailing/jobs/send-scheduled-campaign.job.ts` |
| `MessagingOutboundDeliveryEventJob.handle` | `handle` | `packages/twenty-server/src/modules/messaging-webhooks/jobs/messaging-outbound-delivery-event.job.ts` |

---

### 3.4 `ai-queue`

#### (a) Publishers / Enqueuers (`6` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `AgentRunSuspensionService.deliverOutcome` | `deliverOutcome` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/services/agent-run-suspension.service.ts` |
| `AgentRunSuspensionService.scheduleContinuation` | `scheduleContinuation` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/services/agent-run-suspension.service.ts` |
| `AgentRunnerService.start` | `start` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/services/agent-runner.service.ts` |
| `AgentCronTriggerCronJob.dispatchDueCronTriggers` | `dispatchDueCronTriggers` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-trigger/crons/agent-cron-trigger.cron.job.ts` |
| `CallAgentDatabaseEventTriggersJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-trigger/jobs/call-agent-database-event-triggers.job.ts` |
| `AgentChatWaitingRunService.resolve` | `resolve` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/agent-chat-waiting-run.service.ts` |

#### (b) Subscribers / Processors (`4` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `ContinueAgentRunJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/jobs/continue-agent-run.job.ts` |
| `DeliverAgentRunOutcomeJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/jobs/deliver-agent-run-outcome.job.ts` |
| `RunAgentJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/jobs/run-agent.job.ts` |
| `RunAgentTriggerJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-trigger/jobs/run-agent-trigger.job.ts` |

---

### 3.5 `delete-cascade-queue`

#### (a) Publishers / Enqueuers (`5` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `FilesFieldDeletionListener.handleDestroyedEvent` | `handleDestroyedEvent` | `packages/twenty-server/src/engine/core-modules/file/files-field/listeners/files-field-deletion.listener.ts` |
| `WorkspaceService.hardDeleteWorkspaceWithApplicationUninstallLock` | `hardDeleteWorkspaceWithApplicationUninstallLock` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` |
| `NavigationMenuItemDeletionListener.handleDeletedEvent` | `handleDeletedEvent` | `packages/twenty-server/src/engine/metadata-modules/navigation-menu-item/listeners/navigation-menu-item-deletion.listener.ts` |
| `CalendarEventCleanerCalendarEventListener.handleDestroyedEvent` | `handleDestroyedEvent` | `packages/twenty-server/src/modules/calendar/calendar-event-cleaner/listeners/calendar-event-cleaner-calendar-event.listener.ts` |
| `MessagingMessageCleanerMessageListener.handleDestroyedEvent` | `handleDestroyedEvent` | `packages/twenty-server/src/modules/messaging/message-cleaner/listeners/messaging-message-cleaner-message.listener.ts` |

#### (b) Subscribers / Processors (`6` methods)
| Class.Method | Method Name | Relative File Path |
| :--- | :--- | :--- |
| `EmailingDomainWorkspaceCleanupJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/emailing-domain/jobs/emailing-domain-workspace-cleanup.job.ts` |
| `FilesFieldDeletionJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/file/files-field/jobs/files-field-deletion.job.ts` |
| `FileDeletionJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/file/jobs/file-deletion.job.ts` |
| `FileWorkspaceFolderDeletionJob.handle` | `handle` | `packages/twenty-server/src/engine/core-modules/file/jobs/file-workspace-folder-deletion.job.ts` |
| `NavigationMenuItemDeletionJob.handle` | `handle` | `packages/twenty-server/src/engine/metadata-modules/navigation-menu-item/jobs/navigation-menu-item-deletion.job.ts` |
| `LinkedTimelineActivityDeletionJob.handle` | `handle` | `packages/twenty-server/src/modules/timeline/jobs/linked-timeline-activity-deletion.job.ts` |

---

## Task 4 — End-to-End API-to-Database Write Lineage (`Layer 4–5`)

Below is every incoming GraphQL (`@Mutation`) or REST (`@Controller`) endpoint and its handler/resolver method that writes (`WRITES_TO`) within `0..3` function call hops to `billingSubscription`, `billingCustomer`, `connectedAccount`, and `messageChannel`.

### Summary of Endpoints & Handler Methods by Table

| Database Table | Distinct Endpoints | Distinct `(Endpoint, HandlerMethod)` Pairs | Total Lineage Paths (to Query Operations) |
| :--- | :---: | :---: | :---: |
| **`billingSubscription`** | **9** | **10** | **12** |
| **`billingCustomer`** | **11** | **12** | **16** |
| **`connectedAccount`** | **11** | **11** | **18** |
| **`messageChannel`** | **9** | **9** | **15** |

---

### 4.1 Table: `billingSubscription` (`9` endpoints, `10` handler methods, `12` write lineage paths)

| Endpoint Name | Operation / HTTP Method | Handler (`Class.method`) | Handler Relative File Path | Downstream Writer (`Class.method`) & Query Operation |
| :--- | :--- | :--- | :--- | :--- |
| `MUTATION:cancelSwitchBillingInterval` | GraphQL `MUTATION` | `BillingResolver.cancelSwitchBillingInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingSubscriptionRepository.upsert:377`) |
| `MUTATION:cancelSwitchBillingPlan` | GraphQL `MUTATION` | `BillingResolver.cancelSwitchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingSubscriptionRepository.upsert:377`) |
| `MUTATION:cancelSwitchResourceCreditPrice` | GraphQL `MUTATION` | `BillingResolver.cancelSwitchResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingSubscriptionRepository.upsert:377`) |
| `MUTATION:cancelSwitchResourceCreditPrice` | GraphQL `MUTATION` | `BillingSubscriptionUpdateService.cancelSwitchResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingSubscriptionRepository.upsert:377`) |
| `MUTATION:createSubscriptionPaymentIntent` | GraphQL `MUTATION` | `BillingPortalWorkspaceService.createSubscriptionPaymentIntent` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-portal.workspace-service.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingSubscriptionRepository.upsert:377`) |
| `MUTATION:endSubscriptionTrialPeriod` | GraphQL `MUTATION` | `BillingResolver.endSubscriptionTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingSubscriptionRepository.upsert:377`) |
| `MUTATION:setResourceCreditSubscriptionPrice` | GraphQL `MUTATION` | `BillingResolver.setResourceCreditSubscriptionPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingSubscriptionRepository.upsert:377`) |
| `MUTATION:switchBillingPlan` | GraphQL `MUTATION` | `BillingResolver.switchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingSubscriptionRepository.upsert:377`) |
| `MUTATION:switchSubscriptionInterval` | GraphQL `MUTATION` | `BillingResolver.switchSubscriptionInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingSubscriptionRepository.upsert:377`) |
| `POST:/${ApiPath.Webhooks}/stripe` | REST `POST` | `BillingWebhookController.handleWebhooks` | `packages/twenty-server/src/engine/core-modules/billing-webhook/billing-webhook.controller.ts` | `BillingWebhookSubscriptionScheduleService.processStripeEvent` (`this.billingSubscriptionRepository.update:39`, `update:67`), `BillingWebhookSubscriptionService.applySubscriptionEvent` (`this.billingSubscriptionRepository.upsert:179`) |

---

### 4.2 Table: `billingCustomer` (`11` endpoints, `12` handler methods, `16` write lineage paths)

| Endpoint Name | Operation / HTTP Method | Handler (`Class.method`) | Handler Relative File Path | Downstream Writer (`Class.method`) & Query Operation |
| :--- | :--- | :--- | :--- | :--- |
| `MUTATION:cancelSwitchBillingInterval` | GraphQL `MUTATION` | `BillingResolver.cancelSwitchBillingInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingCustomerRepository.upsert:341`) |
| `MUTATION:cancelSwitchBillingPlan` | GraphQL `MUTATION` | `BillingResolver.cancelSwitchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingCustomerRepository.upsert:341`) |
| `MUTATION:cancelSwitchResourceCreditPrice` | GraphQL `MUTATION` | `BillingResolver.cancelSwitchResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingCustomerRepository.upsert:341`) |
| `MUTATION:cancelSwitchResourceCreditPrice` | GraphQL `MUTATION` | `BillingSubscriptionUpdateService.cancelSwitchResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingCustomerRepository.upsert:341`) |
| `MUTATION:createSubscriptionPaymentIntent` | GraphQL `MUTATION` | `BillingPortalWorkspaceService.createSubscriptionPaymentIntent` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-portal.workspace-service.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingCustomerRepository.upsert:341`), `StripeCustomerService.createStripeCustomer` (`this.billingCustomerRepository.insert:164`) |
| `MUTATION:endSubscriptionTrialPeriod` | GraphQL `MUTATION` | `BillingResolver.endSubscriptionTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingCustomerRepository.upsert:341`) |
| `MUTATION:setResourceCreditSubscriptionPrice` | GraphQL `MUTATION` | `BillingResolver.setResourceCreditSubscriptionPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingCustomerRepository.upsert:341`) |
| `MUTATION:signUpInNewWorkspace` | GraphQL `MUTATION` | `AuthResolver.signUpInNewWorkspace` | `packages/twenty-server/src/engine/core-modules/auth/auth.resolver.ts` | `StripeCustomerService.createStripeCustomer` (`this.billingCustomerRepository.insert:164`) |
| `MUTATION:switchBillingPlan` | GraphQL `MUTATION` | `BillingResolver.switchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingCustomerRepository.upsert:341`) |
| `MUTATION:switchSubscriptionInterval` | GraphQL `MUTATION` | `BillingResolver.switchSubscriptionInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingSubscriptionService.syncSubscriptionToDatabase` (`this.billingCustomerRepository.upsert:341`) |
| `MUTATION:updateUserCreditsFallback` | GraphQL `MUTATION` | `BillingResolver.updateUserCreditsFallback` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | `BillingService.updateUserCreditsFallbackOrThrow` (`this.billingCustomerRepository.update:92`) |
| `POST:/${ApiPath.Webhooks}/stripe` | REST `POST` | `BillingWebhookController.handleWebhooks` | `packages/twenty-server/src/engine/core-modules/billing-webhook/billing-webhook.controller.ts` | `BillingWebhookCustomerService.processCustomerCreated` (`upsert:64`), `BillingWebhookCustomerService.processPaymentMethodAttachedEvent` (`update:90`), `BillingWebhookCustomerService.processPaymentMethodDetachedEvent` (`update:118`), `BillingWebhookSubscriptionService.applySubscriptionEvent` (`upsert:127`) |

---

### 4.3 Table: `connectedAccount` (`11` endpoints, `11` handler methods, `18` write lineage paths)

| Endpoint Name | Operation / HTTP Method | Handler (`Class.method`) | Handler Relative File Path | Downstream Writer (`Class.method`) & Query Operation |
| :--- | :--- | :--- | :--- | :--- |
| `GET:/${ApiPath.Auth}/apps/callback` | REST `GET` | `ConnectionProviderOAuthController.callback` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-oauth.controller.ts` | `ConnectionProviderConnectedAccountService.persist` (`this.connectedAccountRepository.update:151`, `save:198`) |
| `GET:/${ApiPath.Auth}/apps/custom/callback` | REST `GET` | `ConnectionProviderCustomController.callback` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-custom.controller.ts` | `ConnectionProviderConnectedAccountService.persist` (`this.connectedAccountRepository.update:151`, `save:198`) |
| `GET:/${ApiPath.Auth}/google-apis/get-access-token` | REST `GET` | `GoogleAPIsAuthController.googleAuthGetAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/controllers/google-apis-auth.controller.ts` | `CalendarChannelSyncStatusService.markAsFailedInsufficientPermissionsAndFlushCalendarEventsToImport` (`update:330`), `EmailAliasManagerService.refreshHandleAliases` (`update:45`), `MessageChannelSyncStatusService.markAsFailed` (`update:325`) |
| `GET:/${ApiPath.Auth}/google/redirect` | REST `GET` | `GoogleAuthController.googleAuthRedirect` | `packages/twenty-server/src/engine/core-modules/auth/controllers/google-auth.controller.ts` | `CreateSsoConnectedAccountService.createOrUpdateSsoConnectedAccount` (`this.connectedAccountRepository.update:61`, `save:73`) |
| `GET:/${ApiPath.Auth}/microsoft-apis/get-access-token` | REST `GET` | `MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/controllers/microsoft-apis-auth.controller.ts` | `EmailAliasManagerService.refreshHandleAliases` (`this.connectedAccountRepository.update:45`) |
| `GET:/${ApiPath.Auth}/microsoft/redirect` | REST `GET` | `MicrosoftAuthController.microsoftAuthRedirect` | `packages/twenty-server/src/engine/core-modules/auth/controllers/microsoft-auth.controller.ts` | `CreateSsoConnectedAccountService.createOrUpdateSsoConnectedAccount` (`this.connectedAccountRepository.update:61`, `save:73`) |
| `MUTATION:createEmailGroupChannel` | GraphQL `MUTATION` | `MessageChannelResolver.createEmailGroupChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | `ConnectedAccountMetadataService.create` (`this.repository.save:362`) |
| `MUTATION:deleteConnectedAccount` | GraphQL `MUTATION` | `ConnectedAccountResolver.deleteConnectedAccount` | `packages/twenty-server/src/engine/metadata-modules/connected-account/resolvers/connected-account.resolver.ts` | `ConnectedAccountMetadataService.deleteMany` (`this.repository.delete:668`) |
| `MUTATION:deleteEmailGroupChannel` | GraphQL `MUTATION` | `MessageChannelResolver.deleteEmailGroupChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | `ConnectedAccountMetadataService.deleteMany` (`this.repository.delete:668`) |
| `MUTATION:reportAppConnectionAuthFailure` | GraphQL `MUTATION` | `ApplicationConnectionsResolver.reportAppConnectionAuthFailure` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connections/application-connections.resolver.ts` | `ApplicationConnectionAuthFailureService.reportAuthFailureOrThrow` (`this.connectedAccountRepository.update:61`) |
| `POST:/${ApiPath.Auth}/apps/custom/callback` | REST `POST` | `ConnectionProviderCustomController.callbackNotification` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-custom.controller.ts` | `ConnectionProviderConnectedAccountService.persist` (`this.connectedAccountRepository.update:151`, `save:198`) |

---

### 4.4 Table: `messageChannel` (`9` endpoints, `9` handler methods, `15` write lineage paths)

| Endpoint Name | Operation / HTTP Method | Handler (`Class.method`) | Handler Relative File Path | Downstream Writer (`Class.method`) & Query Operation |
| :--- | :--- | :--- | :--- | :--- |
| `GET:/${ApiPath.Auth}/google-apis/get-access-token` | REST `GET` | `GoogleAPIsAuthController.googleAuthGetAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/controllers/google-apis-auth.controller.ts` | `CreateMessageChannelService.createMessageChannel` (`messageChannelRepo.save:49`), `MessageChannelSyncStatusService.resetAndMarkAsMessagesListFetchPending` (`update:109`), `MessagingMessagesImportService.processMessageBatchImport` (`update:228`) |
| `GET:/${ApiPath.Auth}/microsoft-apis/get-access-token` | REST `GET` | `MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/controllers/microsoft-apis-auth.controller.ts` | `CreateMessageChannelService.createMessageChannel` (`messageChannelRepo.save:49`), `MessageChannelSyncStatusService.resetAndMarkAsMessagesListFetchPending` (`update:109`), `MessagingMessagesImportService.processMessageBatchImport` (`update:228`) |
| `MUTATION:createAppMessageChannel` | GraphQL `MUTATION` | `ApplicationMessageChannelsResolver.createAppMessageChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/application-message-channels.resolver.ts` | `ApplicationMessageChannelsService.create` (`this.messageChannelRepository.save:195`) |
| `MUTATION:createEmailGroupChannel` | GraphQL `MUTATION` | `MessageChannelResolver.createEmailGroupChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | `MessageChannelMetadataService.create` (`this.repository.save:244`) |
| `MUTATION:deleteAppMessageChannel` | GraphQL `MUTATION` | `ApplicationMessageChannelsResolver.deleteAppMessageChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/application-message-channels.resolver.ts` | `ApplicationMessageChannelsService.delete` (`this.messageChannelRepository.delete:308`) |
| `MUTATION:saveImapSmtpCaldavAccount` | GraphQL `MUTATION` | `ImapSmtpCaldavResolver.saveImapSmtpCaldavAccount` | `packages/twenty-server/src/engine/core-modules/imap-smtp-caldav-connection/imap-smtp-caldav-connection.resolver.ts` | `CreateMessageChannelService.createMessageChannel` (`messageChannelRepo.save:49`), `MessageChannelSyncStatusService.resetAndMarkAsMessagesListFetchPending` (`update:109`) |
| `MUTATION:updateAppMessageChannel` | GraphQL `MUTATION` | `ApplicationMessageChannelsResolver.updateAppMessageChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/application-message-channels.resolver.ts` | `ApplicationMessageChannelsService.update` (`this.messageChannelRepository.update:288`) |
| `MUTATION:updateEmailGroupChannel` | GraphQL `MUTATION` | `MessageChannelResolver.updateEmailGroupChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | `MessageChannelMetadataService.update` (`this.repository.update:256`) |
| `MUTATION:updateMessageChannel` | GraphQL `MUTATION` | `MessageChannelResolver.updateMessageChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | `MessageChannelMetadataService.update` (`this.repository.update:256`), `MessagingProcessGroupEmailActionsService.markMessageChannelAsPendingGroupEmailsAction` (`this.messageChannelRepository.update:36`) |
