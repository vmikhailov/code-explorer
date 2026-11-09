# Comprehensive 4-Part Architectural Audit — `twenty` Monorepo

---

## Task 1 — Service Method Call Graph & DI Fan-In (`Layer 3`)

Below is the complete non-test call graph (`CallerClass.callerMethod` $\to$ `ServiceClass.calleeMethod`, with caller relative file path) across the `twenty` monorepo for the 4 target services, including both external DI callers and intra-class `this.<method>()` callers.

### 1. `BillingSubscriptionService`
* **Declared file**: `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts`
* **Total Call Edges**: `54`
* **Total Distinct Caller Functions/Methods**: `44` (38 external DI callers + 6 intra-class callers)
* **Total Distinct Called Methods**: `13` (`assertSubscriptionCanceledOrNone`, `cancelSubscription`, `endTrialPeriod`, `getBillingCustomer`, `getBillingSubscriptions`, `getCurrentBillingSubscription`, `getCurrentBillingSubscriptionOrThrow`, `getTrialPeriodFreeWorkflowCredits`, `getWorkspaceEntitlementByKey`, `getWorkspaceEntitlementValue`, `getWorkspaceEntitlements`, `handleUnpaidInvoices`, `syncSubscriptionToDatabase`)

| # | Caller (`CallerClass.callerMethod`) | Target (`ServiceClass.calleeMethod`) | Caller Relative File Path |
|---|---|---|---|
| 1 | `AdminPanelBillingService.getWorkspaceBilling` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/admin-panel/services/admin-panel-billing.service.ts` |
| 2 | `BillingCreditService.writeGrantAndRefreshState` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-credit.service.ts` |
| 3 | `BillingPartnerCodeService.isTrialingOrWithoutSubscription` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-partner-code.service.ts` |
| 4 | `BillingPortalWorkspaceService.createDirectSubscription` | `BillingSubscriptionService.syncSubscriptionToDatabase` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-portal.workspace-service.ts` |
| 5 | `BillingPortalWorkspaceService.createSubscriptionPaymentIntent` | `BillingSubscriptionService.syncSubscriptionToDatabase` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-portal.workspace-service.ts` |
| 6 | `BillingResolver.cancelSwitchBillingInterval` | `BillingSubscriptionService.getBillingSubscriptions` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 7 | `BillingResolver.cancelSwitchBillingInterval` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 8 | `BillingResolver.cancelSwitchBillingPlan` | `BillingSubscriptionService.getBillingSubscriptions` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 9 | `BillingResolver.cancelSwitchBillingPlan` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 10 | `BillingResolver.cancelSwitchResourceCreditPrice` | `BillingSubscriptionService.getBillingSubscriptions` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 11 | `BillingResolver.cancelSwitchResourceCreditPrice` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 12 | `BillingResolver.endSubscriptionTrialPeriod` | `BillingSubscriptionService.endTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 13 | `BillingResolver.endSubscriptionTrialPeriod` | `BillingSubscriptionService.getBillingSubscriptions` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 14 | `BillingResolver.endSubscriptionTrialPeriod` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 15 | `BillingResolver.setResourceCreditSubscriptionPrice` | `BillingSubscriptionService.getBillingSubscriptions` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 16 | `BillingResolver.setResourceCreditSubscriptionPrice` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 17 | `BillingResolver.switchBillingPlan` | `BillingSubscriptionService.getBillingSubscriptions` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 18 | `BillingResolver.switchBillingPlan` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 19 | `BillingResolver.switchSubscriptionInterval` | `BillingSubscriptionService.getBillingSubscriptions` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 20 | `BillingResolver.switchSubscriptionInterval` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` |
| 21 | `BillingService.hasEntitlement` | `BillingSubscriptionService.getWorkspaceEntitlementByKey` | `packages/twenty-server/src/engine/core-modules/billing/services/billing.service.ts` |
| 22 | `BillingSubscriptionService.assertSubscriptionCanceledOrNone` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` |
| 23 | `BillingSubscriptionService.cancelSubscription` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` |
| 24 | `BillingSubscriptionService.endTrialPeriod` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` |
| 25 | `BillingSubscriptionService.endTrialPeriod` | `BillingSubscriptionService.syncSubscriptionToDatabase` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` |
| 26 | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` |
| 27 | `BillingSubscriptionService.getWorkspaceEntitlementValue` | `BillingSubscriptionService.getWorkspaceEntitlementByKey` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` |
| 28 | `BillingSubscriptionService.handleUnpaidInvoices` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription.service.ts` |
| 29 | `BillingSubscriptionUpdateService.cancelSwitchInterval` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` |
| 30 | `BillingSubscriptionUpdateService.cancelSwitchPlan` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` |
| 31 | `BillingSubscriptionUpdateService.cancelSwitchResourceCreditPrice` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` |
| 32 | `BillingSubscriptionUpdateService.changeInterval` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` |
| 33 | `BillingSubscriptionUpdateService.changePlan` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` |
| 34 | `BillingSubscriptionUpdateService.changeResourceCreditPrice` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` |
| 35 | `BillingSubscriptionUpdateService.changeSeats` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` |
| 36 | `BillingSubscriptionUpdateService.updateSubscription` | `BillingSubscriptionService.syncSubscriptionToDatabase` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-subscription-update.service.ts` |
| 37 | `BillingUpdateSubscriptionPriceCommand.runOnWorkspace` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/commands/billing-update-subscription-price.command.ts` |
| 38 | `BillingUsageService.getResourceCreditProductUsage` | `BillingSubscriptionService.getCurrentBillingSubscriptionOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-usage.service.ts` |
| 39 | `BillingUsageService.getTrialResourceUsageCap` | `BillingSubscriptionService.getTrialPeriodFreeWorkflowCredits` | `packages/twenty-server/src/engine/core-modules/billing/services/billing-usage.service.ts` |
| 40 | `BillingWebhookController.handleStripeEvent` | `BillingSubscriptionService.handleUnpaidInvoices` | `packages/twenty-server/src/engine/core-modules/billing-webhook/billing-webhook.controller.ts` |
| 41 | `BillingWebhookInvoiceService.processInvoiceFinalized` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/billing-webhook/services/billing-webhook-invoice.service.ts` |
| 42 | `EnableCommonRecordSharingCommand.up` | `BillingSubscriptionService.getWorkspaceEntitlementValue` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-43/2-43-workspace-command-1790312694997-enable-common-record-sharing.command.ts` |
| 43 | `EventLogRetentionService.hasAuditLogsEntitlement` | `BillingSubscriptionService.getWorkspaceEntitlementValue` | `packages/twenty-server/src/engine/core-modules/event-logs/retention/services/event-log-retention.service.ts` |
| 44 | `RelatedRecordPathService.assertHasRelatedRecordPathsEntitlementOrThrow` | `BillingSubscriptionService.getWorkspaceEntitlementValue` | `packages/twenty-server/src/engine/metadata-modules/related-record-path/related-record-path.service.ts` |
| 45 | `UsageLimitEntitlementService.isIntraWorkspaceLimitEntitled` | `BillingSubscriptionService.getWorkspaceEntitlementValue` | `packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-limit-entitlement.service.ts` |
| 46 | `ValidationRuleService.hasValidationRulesEntitlement` | `BillingSubscriptionService.getWorkspaceEntitlementValue` | `packages/twenty-server/src/engine/metadata-modules/validation-rule/validation-rule.service.ts` |
| 47 | `WorkspaceCurrentBillingSubscriptionCacheService.computeForCache` | `BillingSubscriptionService.getBillingCustomer` | `packages/twenty-server/src/engine/core-modules/billing/services/workspace-current-billing-subscription-cache.service.ts` |
| 48 | `WorkspaceCurrentBillingSubscriptionCacheService.computeForCache` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/billing/services/workspace-current-billing-subscription-cache.service.ts` |
| 49 | `WorkspaceResolver.billingCustomer` | `BillingSubscriptionService.getBillingCustomer` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` |
| 50 | `WorkspaceResolver.billingEntitlements` | `BillingSubscriptionService.getWorkspaceEntitlements` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` |
| 51 | `WorkspaceResolver.billingSubscriptions` | `BillingSubscriptionService.getBillingSubscriptions` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` |
| 52 | `WorkspaceResolver.currentBillingSubscription` | `BillingSubscriptionService.getCurrentBillingSubscription` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` |
| 53 | `WorkspaceService.deleteWorkspace` | `BillingSubscriptionService.cancelSubscription` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` |
| 54 | `WorkspaceService.hardDeleteWorkspaceWithApplicationUninstallLock` | `BillingSubscriptionService.assertSubscriptionCanceledOrNone` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` |

---

### 2. `FeatureFlagService`
* **Declared file**: `packages/twenty-server/src/engine/core-modules/feature-flag/services/feature-flag.service.ts`
* **Total Call Edges**: `35`
* **Total Distinct Caller Functions/Methods**: `33` (32 external DI callers + 1 intra-class caller)
* **Total Distinct Called Methods**: `5` (`enableFeatureFlags`, `getWorkspaceFeatureFlags`, `getWorkspaceFeatureFlagsMap`, `isFeatureEnabled`, `upsertWorkspaceFeatureFlag`)

| # | Caller (`CallerClass.callerMethod`) | Target (`ServiceClass.calleeMethod`) | Caller Relative File Path |
|---|---|---|---|
| 1 | `AccessPolicyService.assertRequestAccessPolicySatisfiedOrThrow` | `FeatureFlagService.getWorkspaceFeatureFlagsMap` | `packages/twenty-server/src/engine/guards/access-policy/access-policy.service.ts` |
| 2 | `AddMessageAttachmentTargetCommand.runOnWorkspace` | `FeatureFlagService.upsertWorkspaceFeatureFlag` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-57/2-57-workspace-command-1793876432369-add-message-attachment-target.command.ts` |
| 3 | `AdminPanelResolver.updateWorkspaceFeatureFlag` | `FeatureFlagService.upsertWorkspaceFeatureFlag` | `packages/twenty-server/src/engine/core-modules/admin-panel/admin-panel.resolver.ts` |
| 4 | `AgentChatMemberMessageSenderService.sendMemberMessage` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/agent-chat-member-message-sender.service.ts` |
| 5 | `AgentChatReplyExecutionPolicyService.shouldExecute` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/agent-chat-reply-execution-policy.service.ts` |
| 6 | `AiFrontComponentService.isEnabled` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-front-component/ai-front-component.service.ts` |
| 7 | `AiFrontComponentToolService.generateTools` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-front-component/tools/services/ai-front-component-tool.service.ts` |
| 8 | `ApplicationMessageIngestionService.findMessageIdsWithAttachments` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/message-channel/services/application-message-ingestion.service.ts` |
| 9 | `BackfillRecordPageLayoutsCommand.runOnWorkspace` | `FeatureFlagService.enableFeatureFlags` | `packages/twenty-server/src/database/commands/upgrade-version-command/1-23/1-23-workspace-command-1780000001500-backfill-record-page-layouts.command.ts` |
| 10 | `BackfillRecordPageLayoutsCommand.runOnWorkspace` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/database/commands/upgrade-version-command/1-23/1-23-workspace-command-1780000001500-backfill-record-page-layouts.command.ts` |
| 11 | `CalendarSaveEventsService.saveCalendarEventsAndEnqueueContactCreationJob` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/modules/calendar/calendar-event-import-manager/services/calendar-save-events.service.ts` |
| 12 | `ChatExecutionService.buildExecutionProfile` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/chat-execution.service.ts` |
| 13 | `ConvertLogicFunctionsToPrebuiltCommand.runOnWorkspace` | `FeatureFlagService.enableFeatureFlags` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-39/2-39-workspace-command-1788338950836-convert-logic-functions-to-prebuilt.command.ts` |
| 14 | `ConvertLogicFunctionsToPrebuiltCommand.runOnWorkspace` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-39/2-39-workspace-command-1788338950836-convert-logic-functions-to-prebuilt.command.ts` |
| 15 | `DefinedToolRegistryService.execute` | `FeatureFlagService.getWorkspaceFeatureFlagsMap` | `packages/twenty-server/src/engine/core-modules/tool-provider/services/defined-tool-registry.service.ts` |
| 16 | `DefinedToolRegistryService.getAvailableDescriptors` | `FeatureFlagService.getWorkspaceFeatureFlagsMap` | `packages/twenty-server/src/engine/core-modules/tool-provider/services/defined-tool-registry.service.ts` |
| 17 | `EnableDirectMessagePairIdentityCommand.runOnWorkspace` | `FeatureFlagService.upsertWorkspaceFeatureFlag` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-56/2-56-workspace-command-1793639083813-enable-direct-message-pair-identity.command.ts` |
| 18 | `FeatureFlagService.isFeatureEnabled` | `FeatureFlagService.getWorkspaceFeatureFlagsMap` | `packages/twenty-server/src/engine/core-modules/feature-flag/services/feature-flag.service.ts` |
| 19 | `FieldMetadataController.isNewMetadataFormat` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/field-metadata/controllers/field-metadata.controller.ts` |
| 20 | `LabResolver.updateLabPublicFeatureFlag` | `FeatureFlagService.upsertWorkspaceFeatureFlag` | `packages/twenty-server/src/engine/core-modules/lab/lab.resolver.ts` |
| 21 | `MessagingMessageAttachmentService.saveMessageAttachments` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/modules/messaging/message-import-manager/services/messaging-message-attachment.service.ts` |
| 22 | `MigrateMessagingInfrastructureToMetadataCommand.runOnWorkspace` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/database/commands/upgrade-version-command/1-21/1-21-workspace-command-1775500012000-migrate-messaging-infrastructure-to-metadata.command.ts` |
| 23 | `ObjectMetadataController.isNewMetadataFormat` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/object-metadata/controllers/object-metadata.controller.ts` |
| 24 | `OnboardingService.setOnboardingInviteTeamPendingFromInviteSuggestions` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/core-modules/onboarding/onboarding.service.ts` |
| 25 | `SentMessagePersistenceService.uploadAttachmentFiles` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/modules/messaging/message-outbound-manager/services/sent-message-persistence.service.ts` |
| 26 | `ShareRecordTool.isEnabled` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/core-modules/tool/tools/share-record-tool/share-record-tool.ts` |
| 27 | `SystemPromptBuilderService.buildPreview` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/system-prompt-builder.service.ts` |
| 28 | `UpdateFeatureFlagCommand.runOnWorkspace` | `FeatureFlagService.upsertWorkspaceFeatureFlag` | `packages/twenty-server/src/database/commands/update-feature-flag.command.ts` |
| 29 | `WorkflowExecutorWorkspaceService.assertActionTypeEnabled` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/modules/workflow/workflow-executor/workspace-services/workflow-executor.workspace-service.ts` |
| 30 | `WorkflowVersionStepOperationsWorkspaceService.assertActionTypeEnabled` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/modules/workflow/workflow-builder/workflow-version-step/workflow-version-step-operations.workspace-service.ts` |
| 31 | `WorkspaceAiDataPolicyService.findStoredAiDataPolicy` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-models/services/workspace-ai-data-policy.service.ts` |
| 32 | `WorkspaceAssistantProfileService.assertInboxEnabledOrThrow` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace-assistant-profile.service.ts` |
| 33 | `WorkspaceResolver.featureFlags` | `FeatureFlagService.getWorkspaceFeatureFlags` | `packages/twenty-server/src/engine/core-modules/workspace/workspace.resolver.ts` |
| 34 | `WorkspaceService.activateWorkspace` | `FeatureFlagService.enableFeatureFlags` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` |
| 35 | `WorkspaceService.assertFeatureIsEnabledOrThrow` | `FeatureFlagService.isFeatureEnabled` | `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` |

---

### 3. `JwtWrapperService`
* **Declared file**: `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts`
* **Total Call Edges**: `55`
* **Total Distinct Caller Functions/Methods**: `38` (36 external DI callers + 2 intra-class callers)
* **Total Distinct Called Methods**: `9` (`decode`, `extractAppSecretBody`, `extractJwtFromRequest`, `generateAppSecret`, `recordVerifyForAlgorithm`, `resolveVerificationKey`, `signAsyncOrThrow`, `toAuthException`, `verifyJwtToken`)

| # | Caller (`CallerClass.callerMethod`) | Target (`ServiceClass.calleeMethod`) | Caller Relative File Path |
|---|---|---|---|
| 1 | `AccessTokenService.generateAccessToken` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` |
| 2 | `AccessTokenService.generatePlaygroundToken` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` |
| 3 | `AccessTokenService.validateToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` |
| 4 | `AccessTokenService.validateToken` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` |
| 5 | `AccessTokenService.validateTokenByRequest` | `JwtWrapperService.extractJwtFromRequest` | `packages/twenty-server/src/engine/core-modules/auth/token/services/access-token.service.ts` |
| 6 | `ApiKeyService.generateApiKeyToken` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/api-key/services/api-key.service.ts` |
| 7 | `ApplicationRegistrationClaimService.buildGithubAuthorizationUrl` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/application/application-registration/application-registration-claim.service.ts` |
| 8 | `ApplicationRegistrationClaimService.verifyClaimState` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/application/application-registration/application-registration-claim.service.ts` |
| 9 | `ApplicationRegistrationClaimService.verifyClaimState` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/application/application-registration/application-registration-claim.service.ts` |
| 10 | `ApplicationTokenService.decodeToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` |
| 11 | `ApplicationTokenService.signApplicationToken` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` |
| 12 | `ApplicationTokenService.validateApplicationAccessToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` |
| 13 | `ApplicationTokenService.validateApplicationAccessToken` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` |
| 14 | `ApplicationTokenService.validateApplicationRefreshToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` |
| 15 | `ApplicationTokenService.validateApplicationRefreshToken` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/application-token.service.ts` |
| 16 | `ApprovedAccessDomainService.mintValidationToken` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/approved-access-domain/services/approved-access-domain.service.ts` |
| 17 | `ApprovedAccessDomainService.verifyValidationTokenOrThrow` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/approved-access-domain/services/approved-access-domain.service.ts` |
| 18 | `ConnectionProviderAuthorizationStateService.sign` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-authorization-state.service.ts` |
| 19 | `ConnectionProviderAuthorizationStateService.verifyOrThrow` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-authorization-state.service.ts` |
| 20 | `CookieSessionCsrfMiddleware.use` | `JwtWrapperService.extractJwtFromRequest` | `packages/twenty-server/src/engine/middlewares/cookie-session-csrf.middleware.ts` |
| 21 | `FileByIdGuard.canActivate` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/file/guards/file-by-id.guard.ts` |
| 22 | `FileByIdGuard.canActivate` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/file/guards/file-by-id.guard.ts` |
| 23 | `FileUploadTargetService.buildUploadTarget` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/file/file-upload/services/file-upload-target.service.ts` |
| 24 | `FileUploadTokenGuard.canActivate` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/file/file-upload/guards/file-upload-token.guard.ts` |
| 25 | `FileUrlService.buildSignedFileUrl` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/file/file-url/file-url.service.ts` |
| 26 | `JwtAuthStrategy.constructor` | `JwtWrapperService.extractJwtFromRequest` | `packages/twenty-server/src/engine/core-modules/auth/strategies/jwt.auth.strategy.ts` |
| 27 | `JwtAuthStrategy.constructor` | `JwtWrapperService.resolveVerificationKey` | `packages/twenty-server/src/engine/core-modules/auth/strategies/jwt.auth.strategy.ts` |
| 28 | `JwtWrapperService.resolveVerificationKey` | `JwtWrapperService.extractAppSecretBody` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` |
| 29 | `JwtWrapperService.resolveVerificationKey` | `JwtWrapperService.generateAppSecret` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` |
| 30 | `JwtWrapperService.verifyJwtToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` |
| 31 | `JwtWrapperService.verifyJwtToken` | `JwtWrapperService.extractAppSecretBody` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` |
| 32 | `JwtWrapperService.verifyJwtToken` | `JwtWrapperService.generateAppSecret` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` |
| 33 | `JwtWrapperService.verifyJwtToken` | `JwtWrapperService.recordVerifyForAlgorithm` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` |
| 34 | `JwtWrapperService.verifyJwtToken` | `JwtWrapperService.resolveVerificationKey` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` |
| 35 | `JwtWrapperService.verifyJwtToken` | `JwtWrapperService.toAuthException` | `packages/twenty-server/src/engine/core-modules/jwt/services/jwt-wrapper.service.ts` |
| 36 | `LoginTokenService.generateLoginToken` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/auth/token/services/login-token.service.ts` |
| 37 | `LoginTokenService.verifyLoginToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/auth/token/services/login-token.service.ts` |
| 38 | `LoginTokenService.verifyLoginToken` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/login-token.service.ts` |
| 39 | `MiddlewareService.clearDeadSessionCookie` | `JwtWrapperService.extractJwtFromRequest` | `packages/twenty-server/src/engine/middlewares/middleware.service.ts` |
| 40 | `MiddlewareService.isTokenPresent` | `JwtWrapperService.extractJwtFromRequest` | `packages/twenty-server/src/engine/middlewares/middleware.service.ts` |
| 41 | `RecordExportWorkspaceService.getDownloadPath` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/record-export/services/record-export.workspace-service.ts` |
| 42 | `RefreshTokenService.generateRefreshToken` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/auth/token/services/refresh-token.service.ts` |
| 43 | `RefreshTokenService.verifyRefreshToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/auth/token/services/refresh-token.service.ts` |
| 44 | `RefreshTokenService.verifyRefreshToken` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/refresh-token.service.ts` |
| 45 | `ServerRouteAuthorizationHeaderService.isTwentyCredential` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/server-route-trigger/server-route-authorization-header.service.ts` |
| 46 | `SimpleSecretEncryptionUtil.decryptSecret` | `JwtWrapperService.generateAppSecret` | `packages/twenty-server/src/database/commands/upgrade-version-command/2-5/utils/simple-secret-encryption.util.ts` |
| 47 | `TransientTokenService.generateTransientToken` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/auth/token/services/transient-token.service.ts` |
| 48 | `TransientTokenService.verifyTransientToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/auth/token/services/transient-token.service.ts` |
| 49 | `TransientTokenService.verifyTransientToken` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/transient-token.service.ts` |
| 50 | `UserSessionService.buildCreateSessionInputFromTokenPair` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/user-session/services/user-session.service.ts` |
| 51 | `UserSessionService.revokePresentedRefreshToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/user-session/services/user-session.service.ts` |
| 52 | `UserSessionService.revokePresentedRefreshToken` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/user-session/services/user-session.service.ts` |
| 53 | `WorkspaceAgnosticTokenService.generateWorkspaceAgnosticToken` | `JwtWrapperService.signAsyncOrThrow` | `packages/twenty-server/src/engine/core-modules/auth/token/services/workspace-agnostic-token.service.ts` |
| 54 | `WorkspaceAgnosticTokenService.validateToken` | `JwtWrapperService.decode` | `packages/twenty-server/src/engine/core-modules/auth/token/services/workspace-agnostic-token.service.ts` |
| 55 | `WorkspaceAgnosticTokenService.validateToken` | `JwtWrapperService.verifyJwtToken` | `packages/twenty-server/src/engine/core-modules/auth/token/services/workspace-agnostic-token.service.ts` |

---

### 4. `ThrottlerService`
* **Declared file**: `packages/twenty-server/src/engine/core-modules/throttler/throttler.service.ts`
* **Total Call Edges**: `24`
* **Total Distinct Caller Functions/Methods**: `24` (22 external DI callers + 2 intra-class callers)
* **Total Distinct Called Methods**: `3` (`tokenBucketConsumeUpTo`, `tokenBucketThrottleOrThrow`, `tryConsumeTokenBuckets`)

| # | Caller (`CallerClass.callerMethod`) | Target (`ServiceClass.calleeMethod`) | Caller Relative File Path |
|---|---|---|---|
| 1 | `AgentTriggerThrottlerService.consumeAvailableRuns` | `ThrottlerService.tokenBucketConsumeUpTo` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-trigger/services/agent-trigger-throttler.service.ts` |
| 2 | `AppBillingController.charge` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/app-billing/app-billing.controller.ts` |
| 3 | `AppBillingController.credits` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/billing/app-billing/app-billing.controller.ts` |
| 4 | `ApplicationDevelopmentService.throttlePerApplication` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/application/application-development/application-development.service.ts` |
| 5 | `ApplicationHealthCheckService.runForUser` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/application/application-health/application-health-check.service.ts` |
| 6 | `ApplicationJobEnqueueThrottlerService.throttleOrThrow` | `ThrottlerService.tryConsumeTokenBuckets` | `packages/twenty-server/src/engine/core-modules/message-queue/services/application-job-enqueue-throttler.service.ts` |
| 7 | `ApplicationOAuthResolver.renewApplicationToken` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/application/application-oauth/application-oauth.resolver.ts` |
| 8 | `AuthResolver.emailPasswordResetLink` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/auth/auth.resolver.ts` |
| 9 | `CampaignEngagementCaptureService.throttleAndEnqueue` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/modules/emailing/services/campaign-engagement-capture.service.ts` |
| 10 | `EnrichmentThrottleService.consumeToken` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/company-enrichment/services/enrichment-throttle.service.ts` |
| 11 | `LogicFunctionExecutorService.throttleExecution` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/logic-function/logic-function-executor/logic-function-executor.service.ts` |
| 12 | `MessageCampaignService.sendTest` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/modules/emailing/services/message-campaign.service.ts` |
| 13 | `OAuthRegistrationController.applyRateLimit` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/application/application-oauth/controllers/oauth-registration.controller.ts` |
| 14 | `OAuthTokenController.applyRateLimit` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/application/application-oauth/controllers/oauth-token.controller.ts` |
| 15 | `ThrottlerService.tokenBucketConsumeUpTo` | `ThrottlerService.tryConsumeTokenBuckets` | `packages/twenty-server/src/engine/core-modules/throttler/throttler.service.ts` |
| 16 | `ThrottlerService.tokenBucketThrottleOrThrow` | `ThrottlerService.tryConsumeTokenBuckets` | `packages/twenty-server/src/engine/core-modules/throttler/throttler.service.ts` |
| 17 | `TwoFactorAuthenticationRecoveryService.generateRecoveryCode` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/two-factor-authentication/services/two-factor-authentication-recovery.service.ts` |
| 18 | `TwoFactorAuthenticationRecoveryService.redeemRecoveryCode` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/two-factor-authentication/services/two-factor-authentication-recovery.service.ts` |
| 19 | `TwoFactorAuthenticationService.validateStrategy` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/two-factor-authentication/two-factor-authentication.service.ts` |
| 20 | `UnsubscribeController.throttleOrThrow` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/modules/emailing/controllers/unsubscribe.controller.ts` |
| 21 | `UsageLimitSpeedService.consumeTokens` | `ThrottlerService.tryConsumeTokenBuckets` | `packages/twenty-server/src/engine/core-modules/usage-limit/services/usage-limit-speed.service.ts` |
| 22 | `WorkflowThrottlingWorkspaceService.consumeRemainingRunsToEnqueueCount` | `ThrottlerService.tokenBucketConsumeUpTo` | `packages/twenty-server/src/modules/workflow/workflow-runner/workflow-run-queue/workspace-services/workflow-throttling.workspace-service.ts` |
| 23 | `WorkflowThrottlingWorkspaceService.throttleOrThrowIfHardLimitReached` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/modules/workflow/workflow-runner/workflow-run-queue/workspace-services/workflow-throttling.workspace-service.ts` |
| 24 | `WorkspaceInvitationService.throttleInvitationSending` | `ThrottlerService.tokenBucketThrottleOrThrow` | `packages/twenty-server/src/engine/core-modules/workspace-invitation/services/workspace-invitation.service.ts` |

---

## Task 2 — Multi-Hop Class Inheritance & Interface Realization (`Layer 3`)

### (a) Inherits Directly or Transitively (1 to 3 hops) from `SyncableEntity`
* **Base Class**: `SyncableEntity` (`packages/twenty-server/src/engine/workspace-manager/types/syncable-entity.interface.ts`)
* **Exact Total Count**: `43` classes (`33` direct 1-hop subclasses + `10` transitive 2-hop subclasses via `OverridableEntity`)

| # | Hop | Class Name | Relative File Path | Parent Class |
|---|---|---|---|---|
| 1 | 1 | `AgentChatChannelEntity` | `packages/twenty-server/src/engine/metadata-modules/agent-chat-channel/entities/agent-chat-channel.entity.ts` | `SyncableEntity` |
| 2 | 1 | `AgentChatChannelRoleEntity` | `packages/twenty-server/src/engine/metadata-modules/agent-chat-channel/entities/agent-chat-channel-role.entity.ts` | `SyncableEntity` |
| 3 | 1 | `ApplicationVariableEntity` | `packages/twenty-server/src/engine/core-modules/application/application-variable/application-variable.entity.ts` | `SyncableEntity` |
| 4 | 1 | `ConnectionProviderEntity` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider.entity.ts` | `SyncableEntity` |
| 5 | 1 | `FieldMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/field-metadata/field-metadata.entity.ts` | `SyncableEntity` |
| 6 | 1 | `FieldPermissionEntity` | `packages/twenty-server/src/engine/metadata-modules/object-permission/field-permission/field-permission.entity.ts` | `SyncableEntity` |
| 7 | 1 | `FrontComponentEntity` | `packages/twenty-server/src/engine/metadata-modules/front-component/entities/front-component.entity.ts` | `SyncableEntity` |
| 8 | 1 | `IndexMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/index-metadata/index-metadata.entity.ts` | `SyncableEntity` |
| 9 | 1 | `LogicFunctionEntity` | `packages/twenty-server/src/engine/metadata-modules/logic-function/logic-function.entity.ts` | `SyncableEntity` |
| 10 | 1 | `McpServerEntity` | `packages/twenty-server/src/engine/metadata-modules/mcp-server/entities/mcp-server.entity.ts` | `SyncableEntity` |
| 11 | 1 | `MessagingProviderEntity` | `packages/twenty-server/src/engine/metadata-modules/messaging-provider/entities/messaging-provider.entity.ts` | `SyncableEntity` |
| 12 | 1 | `NavigationMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/navigation-menu-item/entities/navigation-menu-item.entity.ts` | `SyncableEntity` |
| 13 | 1 | `ObjectMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/object-metadata/object-metadata.entity.ts` | `SyncableEntity` |
| 14 | 1 | `ObjectPermissionEntity` | `packages/twenty-server/src/engine/metadata-modules/object-permission/object-permission.entity.ts` | `SyncableEntity` |
| 15 | 1 | `OverridableEntity` | `packages/twenty-server/src/engine/workspace-manager/types/overridable-entity.type.ts` | `SyncableEntity` |
| 16 | 1 | `PageLayoutEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout/entities/page-layout.entity.ts` | `SyncableEntity` |
| 17 | 1 | `PermissionFlagEntity` | `packages/twenty-server/src/engine/metadata-modules/permission-flag/permission-flag.entity.ts` | `SyncableEntity` |
| 18 | 1 | `RelatedRecordPathEntity` | `packages/twenty-server/src/engine/metadata-modules/related-record-path/entities/related-record-path.entity.ts` | `SyncableEntity` |
| 19 | 1 | `RoleEntity` | `packages/twenty-server/src/engine/metadata-modules/role/role.entity.ts` | `SyncableEntity` |
| 20 | 1 | `RolePermissionFlagEntity` | `packages/twenty-server/src/engine/metadata-modules/role-permission-flag/role-permission-flag.entity.ts` | `SyncableEntity` |
| 21 | 1 | `RoleTargetEntity` | `packages/twenty-server/src/engine/metadata-modules/role-target/role-target.entity.ts` | `SyncableEntity` |
| 22 | 1 | `RowLevelPermissionPredicateEntity` | `packages/twenty-server/src/engine/metadata-modules/row-level-permission-predicate/entities/row-level-permission-predicate.entity.ts` | `SyncableEntity` |
| 23 | 1 | `RowLevelPermissionPredicateGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/row-level-permission-predicate/entities/row-level-permission-predicate-group.entity.ts` | `SyncableEntity` |
| 24 | 1 | `SearchFieldMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/search-field-metadata/search-field-metadata.entity.ts` | `SyncableEntity` |
| 25 | 1 | `SettingsMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/settings-menu-item/entities/settings-menu-item.entity.ts` | `SyncableEntity` |
| 26 | 1 | `ValidationRuleEntity` | `packages/twenty-server/src/engine/metadata-modules/validation-rule/entities/validation-rule.entity.ts` | `SyncableEntity` |
| 27 | 1 | `ViewFilterEntity` | `packages/twenty-server/src/engine/metadata-modules/view-filter/entities/view-filter.entity.ts` | `SyncableEntity` |
| 28 | 1 | `ViewFilterGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-filter-group/entities/view-filter-group.entity.ts` | `SyncableEntity` |
| 29 | 1 | `ViewGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-group/entities/view-group.entity.ts` | `SyncableEntity` |
| 30 | 1 | `ViewSortEntity` | `packages/twenty-server/src/engine/metadata-modules/view-sort/entities/view-sort.entity.ts` | `SyncableEntity` |
| 31 | 1 | `WebhookEntity` | `packages/twenty-server/src/engine/metadata-modules/webhook/entities/webhook.entity.ts` | `SyncableEntity` |
| 32 | 1 | `WorkflowEntity` | `packages/twenty-server/src/engine/core-modules/workflow/entities/workflow.entity.ts` | `SyncableEntity` |
| 33 | 1 | `WorkflowVersionEntity` | `packages/twenty-server/src/engine/core-modules/workflow/entities/workflow-version.entity.ts` | `SyncableEntity` |
| 34 | 2 | `AgentEntity` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent/entities/agent.entity.ts` | `OverridableEntity` |
| 35 | 2 | `CommandMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/command-menu-item/entities/command-menu-item.entity.ts` | `OverridableEntity` |
| 36 | 2 | `FieldMetadataOptionEntity` | `packages/twenty-server/src/engine/metadata-modules/field-metadata-option/entities/field-metadata-option.entity.ts` | `OverridableEntity` |
| 37 | 2 | `PageLayoutTabEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout-tab/entities/page-layout-tab.entity.ts` | `OverridableEntity` |
| 38 | 2 | `PageLayoutWidgetEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout-widget/entities/page-layout-widget.entity.ts` | `OverridableEntity` |
| 39 | 2 | `SkillEntity` | `packages/twenty-server/src/engine/metadata-modules/skill/entities/skill.entity.ts` | `OverridableEntity` |
| 40 | 2 | `TimelineActivityTypeEntity` | `packages/twenty-server/src/engine/metadata-modules/timeline-activity-type/entities/timeline-activity-type.entity.ts` | `OverridableEntity` |
| 41 | 2 | `ViewEntity` | `packages/twenty-server/src/engine/metadata-modules/view/entities/view.entity.ts` | `OverridableEntity` |
| 42 | 2 | `ViewFieldEntity` | `packages/twenty-server/src/engine/metadata-modules/view-field/entities/view-field.entity.ts` | `OverridableEntity` |
| 43 | 2 | `ViewFieldGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-field-group/entities/view-field-group.entity.ts` | `OverridableEntity` |

---

### (b) Inherits Directly or Transitively (1 to 3 hops) from `WorkspaceRelatedEntity`
* **Base Class**: `WorkspaceRelatedEntity` (`packages/twenty-server/src/engine/workspace-manager/types/workspace-related-entity.ts`)
* **Exact Total Count**:
  * **1-hop (direct subclasses)**: `24` classes (including `SyncableEntity`)
  * **2-hop (transitive via `SyncableEntity`)**: `33` classes (`57` cumulative 1..2 hops)
  * **3-hop (transitive via `OverridableEntity` $\to$ `SyncableEntity`)**: `10` classes (`67` cumulative 1..3 hops)

| # | Hop | Class Name | Relative File Path | Parent Class |
|---|---|---|---|---|
| 1 | 1 | `ApiKeyEntity` | `packages/twenty-server/src/engine/core-modules/api-key/api-key.entity.ts` | `WorkspaceRelatedEntity` |
| 2 | 1 | `ApplicationEntity` | `packages/twenty-server/src/engine/core-modules/application/application.entity.ts` | `WorkspaceRelatedEntity` |
| 3 | 1 | `ApprovedAccessDomainEntity` | `packages/twenty-server/src/engine/core-modules/approved-access-domain/approved-access-domain.entity.ts` | `WorkspaceRelatedEntity` |
| 4 | 1 | `BillingCreditGrantEntity` | `packages/twenty-server/src/engine/core-modules/billing/entities/billing-credit-grant.entity.ts` | `WorkspaceRelatedEntity` |
| 5 | 1 | `BillingCustomerEntity` | `packages/twenty-server/src/engine/core-modules/billing/entities/billing-customer.entity.ts` | `WorkspaceRelatedEntity` |
| 6 | 1 | `BillingEntitlementEntity` | `packages/twenty-server/src/engine/core-modules/billing/entities/billing-entitlement.entity.ts` | `WorkspaceRelatedEntity` |
| 7 | 1 | `BillingSubscriptionEntity` | `packages/twenty-server/src/engine/core-modules/billing/entities/billing-subscription.entity.ts` | `WorkspaceRelatedEntity` |
| 8 | 1 | `CalendarChannelEntity` | `packages/twenty-server/src/engine/metadata-modules/calendar-channel/entities/calendar-channel.entity.ts` | `WorkspaceRelatedEntity` |
| 9 | 1 | `ConnectedAccountEntity` | `packages/twenty-server/src/engine/metadata-modules/connected-account/entities/connected-account.entity.ts` | `WorkspaceRelatedEntity` |
| 10 | 1 | `DataSourceEntity` | `packages/twenty-server/src/engine/metadata-modules/data-source/data-source.entity.ts` | `WorkspaceRelatedEntity` |
| 11 | 1 | `DeferredWorkspaceMigrationActionEntity` | `packages/twenty-server/src/engine/metadata-modules/deferred-workspace-migration-action/deferred-workspace-migration-action.entity.ts` | `WorkspaceRelatedEntity` |
| 12 | 1 | `EmailingDomainEntity` | `packages/twenty-server/src/engine/core-modules/emailing-domain/emailing-domain.entity.ts` | `WorkspaceRelatedEntity` |
| 13 | 1 | `FeatureFlagEntity` | `packages/twenty-server/src/engine/core-modules/feature-flag/feature-flag.entity.ts` | `WorkspaceRelatedEntity` |
| 14 | 1 | `LogicFunctionLayerEntity` | `packages/twenty-server/src/engine/metadata-modules/logic-function-layer/logic-function-layer.entity.ts` | `WorkspaceRelatedEntity` |
| 15 | 1 | `MessageChannelEntity` | `packages/twenty-server/src/engine/metadata-modules/message-channel/entities/message-channel.entity.ts` | `WorkspaceRelatedEntity` |
| 16 | 1 | `MessageFolderEntity` | `packages/twenty-server/src/engine/metadata-modules/message-folder/entities/message-folder.entity.ts` | `WorkspaceRelatedEntity` |
| 17 | 1 | `PendingWakeUpEntity` | `packages/twenty-server/src/engine/core-modules/pending-wake-up/entities/pending-wake-up.entity.ts` | `WorkspaceRelatedEntity` |
| 18 | 1 | `PublicDomainEntity` | `packages/twenty-server/src/engine/core-modules/public-domain/public-domain.entity.ts` | `WorkspaceRelatedEntity` |
| 19 | 1 | `SyncableEntity` | `packages/twenty-server/src/engine/workspace-manager/types/syncable-entity.interface.ts` | `WorkspaceRelatedEntity` |
| 20 | 1 | `UnsubscribeTopicEntity` | `packages/twenty-server/src/engine/core-modules/emailing-domain/unsubscribe-topic.entity.ts` | `WorkspaceRelatedEntity` |
| 21 | 1 | `UsageLimitEntity` | `packages/twenty-server/src/engine/core-modules/usage-limit/usage-limit.entity.ts` | `WorkspaceRelatedEntity` |
| 22 | 1 | `UserApplicationVariableValueEntity` | `packages/twenty-server/src/engine/core-modules/application/application-variable/user-application-variable-value.entity.ts` | `WorkspaceRelatedEntity` |
| 23 | 1 | `UserWorkspaceEntity` | `packages/twenty-server/src/engine/core-modules/user-workspace/user-workspace.entity.ts` | `WorkspaceRelatedEntity` |
| 24 | 1 | `WorkspaceSsoIdentityProviderEntity` | `packages/twenty-server/src/engine/core-modules/sso/workspace-sso-identity-provider.entity.ts` | `WorkspaceRelatedEntity` |
| 25 | 2 | `AgentChatChannelEntity` | `packages/twenty-server/src/engine/metadata-modules/agent-chat-channel/entities/agent-chat-channel.entity.ts` | `SyncableEntity` |
| 26 | 2 | `AgentChatChannelRoleEntity` | `packages/twenty-server/src/engine/metadata-modules/agent-chat-channel/entities/agent-chat-channel-role.entity.ts` | `SyncableEntity` |
| 27 | 2 | `ApplicationVariableEntity` | `packages/twenty-server/src/engine/core-modules/application/application-variable/application-variable.entity.ts` | `SyncableEntity` |
| 28 | 2 | `ConnectionProviderEntity` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider.entity.ts` | `SyncableEntity` |
| 29 | 2 | `FieldMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/field-metadata/field-metadata.entity.ts` | `SyncableEntity` |
| 30 | 2 | `FieldPermissionEntity` | `packages/twenty-server/src/engine/metadata-modules/object-permission/field-permission/field-permission.entity.ts` | `SyncableEntity` |
| 31 | 2 | `FrontComponentEntity` | `packages/twenty-server/src/engine/metadata-modules/front-component/entities/front-component.entity.ts` | `SyncableEntity` |
| 32 | 2 | `IndexMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/index-metadata/index-metadata.entity.ts` | `SyncableEntity` |
| 33 | 2 | `LogicFunctionEntity` | `packages/twenty-server/src/engine/metadata-modules/logic-function/logic-function.entity.ts` | `SyncableEntity` |
| 34 | 2 | `McpServerEntity` | `packages/twenty-server/src/engine/metadata-modules/mcp-server/entities/mcp-server.entity.ts` | `SyncableEntity` |
| 35 | 2 | `MessagingProviderEntity` | `packages/twenty-server/src/engine/metadata-modules/messaging-provider/entities/messaging-provider.entity.ts` | `SyncableEntity` |
| 36 | 2 | `NavigationMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/navigation-menu-item/entities/navigation-menu-item.entity.ts` | `SyncableEntity` |
| 37 | 2 | `ObjectMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/object-metadata/object-metadata.entity.ts` | `SyncableEntity` |
| 38 | 2 | `ObjectPermissionEntity` | `packages/twenty-server/src/engine/metadata-modules/object-permission/object-permission.entity.ts` | `SyncableEntity` |
| 39 | 2 | `OverridableEntity` | `packages/twenty-server/src/engine/workspace-manager/types/overridable-entity.type.ts` | `SyncableEntity` |
| 40 | 2 | `PageLayoutEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout/entities/page-layout.entity.ts` | `SyncableEntity` |
| 41 | 2 | `PermissionFlagEntity` | `packages/twenty-server/src/engine/metadata-modules/permission-flag/permission-flag.entity.ts` | `SyncableEntity` |
| 42 | 2 | `RelatedRecordPathEntity` | `packages/twenty-server/src/engine/metadata-modules/related-record-path/entities/related-record-path.entity.ts` | `SyncableEntity` |
| 43 | 2 | `RoleEntity` | `packages/twenty-server/src/engine/metadata-modules/role/role.entity.ts` | `SyncableEntity` |
| 44 | 2 | `RolePermissionFlagEntity` | `packages/twenty-server/src/engine/metadata-modules/role-permission-flag/role-permission-flag.entity.ts` | `SyncableEntity` |
| 45 | 2 | `RoleTargetEntity` | `packages/twenty-server/src/engine/metadata-modules/role-target/role-target.entity.ts` | `SyncableEntity` |
| 46 | 2 | `RowLevelPermissionPredicateEntity` | `packages/twenty-server/src/engine/metadata-modules/row-level-permission-predicate/entities/row-level-permission-predicate.entity.ts` | `SyncableEntity` |
| 47 | 2 | `RowLevelPermissionPredicateGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/row-level-permission-predicate/entities/row-level-permission-predicate-group.entity.ts` | `SyncableEntity` |
| 48 | 2 | `SearchFieldMetadataEntity` | `packages/twenty-server/src/engine/metadata-modules/search-field-metadata/search-field-metadata.entity.ts` | `SyncableEntity` |
| 49 | 2 | `SettingsMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/settings-menu-item/entities/settings-menu-item.entity.ts` | `SyncableEntity` |
| 50 | 2 | `ValidationRuleEntity` | `packages/twenty-server/src/engine/metadata-modules/validation-rule/entities/validation-rule.entity.ts` | `SyncableEntity` |
| 51 | 2 | `ViewFilterEntity` | `packages/twenty-server/src/engine/metadata-modules/view-filter/entities/view-filter.entity.ts` | `SyncableEntity` |
| 52 | 2 | `ViewFilterGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-filter-group/entities/view-filter-group.entity.ts` | `SyncableEntity` |
| 53 | 2 | `ViewGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-group/entities/view-group.entity.ts` | `SyncableEntity` |
| 54 | 2 | `ViewSortEntity` | `packages/twenty-server/src/engine/metadata-modules/view-sort/entities/view-sort.entity.ts` | `SyncableEntity` |
| 55 | 2 | `WebhookEntity` | `packages/twenty-server/src/engine/metadata-modules/webhook/entities/webhook.entity.ts` | `SyncableEntity` |
| 56 | 2 | `WorkflowEntity` | `packages/twenty-server/src/engine/core-modules/workflow/entities/workflow.entity.ts` | `SyncableEntity` |
| 57 | 2 | `WorkflowVersionEntity` | `packages/twenty-server/src/engine/core-modules/workflow/entities/workflow-version.entity.ts` | `SyncableEntity` |
| 58 | 3 | `AgentEntity` | `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent/entities/agent.entity.ts` | `OverridableEntity` |
| 59 | 3 | `CommandMenuItemEntity` | `packages/twenty-server/src/engine/metadata-modules/command-menu-item/entities/command-menu-item.entity.ts` | `OverridableEntity` |
| 60 | 3 | `FieldMetadataOptionEntity` | `packages/twenty-server/src/engine/metadata-modules/field-metadata-option/entities/field-metadata-option.entity.ts` | `OverridableEntity` |
| 61 | 3 | `PageLayoutTabEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout-tab/entities/page-layout-tab.entity.ts` | `OverridableEntity` |
| 62 | 3 | `PageLayoutWidgetEntity` | `packages/twenty-server/src/engine/metadata-modules/page-layout-widget/entities/page-layout-widget.entity.ts` | `OverridableEntity` |
| 63 | 3 | `SkillEntity` | `packages/twenty-server/src/engine/metadata-modules/skill/entities/skill.entity.ts` | `OverridableEntity` |
| 64 | 3 | `TimelineActivityTypeEntity` | `packages/twenty-server/src/engine/metadata-modules/timeline-activity-type/entities/timeline-activity-type.entity.ts` | `OverridableEntity` |
| 65 | 3 | `ViewEntity` | `packages/twenty-server/src/engine/metadata-modules/view/entities/view.entity.ts` | `OverridableEntity` |
| 66 | 3 | `ViewFieldEntity` | `packages/twenty-server/src/engine/metadata-modules/view-field/entities/view-field.entity.ts` | `OverridableEntity` |
| 67 | 3 | `ViewFieldGroupEntity` | `packages/twenty-server/src/engine/metadata-modules/view-field-group/entities/view-field-group.entity.ts` | `OverridableEntity` |

---

### (c) Inherits Directly or Transitively from `WorkspaceEntityMigrationBuilderService`
* **Base Class**: `WorkspaceEntityMigrationBuilderService` (`packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-builder/services/workspace-entity-migration-builder.service.ts`)
* **Exact Total Count**: `42` classes

| # | Class Name | Relative File Path |
|---|---|---|
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

### (d) Implements `WorkspaceResolverBuilderFactoryInterface`
* **Interface**: `WorkspaceResolverBuilderFactoryInterface` (`packages/twenty-server/src/engine/api/graphql/workspace-resolver-builder/interfaces/workspace-resolver-builder-factory.interface.ts`)
* **Exact Total Count**: `15` classes

| # | Class Name | Relative File Path |
|---|---|---|
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

### 1. `webhook-queue` (`MessageQueue.webhookQueue`)
* **(a) Producers / Enqueuers (`12` methods)**:
  1. `CallWebhookJobsForMetadataJob.handle` — `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook-jobs-for-metadata.job.ts` (`messageQueueService.add`)
  2. `CallWebhookJobsJob.handle` — `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook-jobs.job.ts` (`messageQueueService.add`)
  3. `ChannelSyncService.startCalendarChannelSync` — `packages/twenty-server/src/modules/connected-account/channel-sync/services/channel-sync.service.ts` (`webhookQueueService.add`)
  4. `ChannelSyncService.startMessageChannelSync` — `packages/twenty-server/src/modules/connected-account/channel-sync/services/channel-sync.service.ts` (`webhookQueueService.add`)
  5. `CreateWebhookSubscriptionForConnectedAccountCommand.enqueueChannels` — `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/commands/create-webhook-subscription-for-connected-account.command.ts` (`webhookQueueService.add`)
  6. `EntityEventsToDbListener.enqueueWebhookJobsIfAnyWebhookMatches` — `packages/twenty-server/src/engine/api/graphql/workspace-query-runner/listeners/entity-events-to-db.listener.ts` (`webhookQueueService.add`)
  7. `GoogleAPIsService.enqueueWebhookSubscriptionCreation` — `packages/twenty-server/src/engine/core-modules/auth/services/google-apis.service.ts` (`webhookQueueService.add`)
  8. `MetadataEventsToDbListener.handleEvent` — `packages/twenty-server/src/engine/subscriptions/metadata-event/metadata-events-to-db.listener.ts` (`webhookQueueService.add`)
  9. `MicrosoftAPIsService.enqueueWebhookSubscriptionCreation` — `packages/twenty-server/src/engine/core-modules/auth/services/microsoft-apis.service.ts` (`webhookQueueService.add`)
  10. `WebhookSubscriptionRenewalCronJob.enqueueRenewals` — `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/crons/jobs/webhook-subscription-renewal.cron.job.ts` (`webhookQueueService.add`)
  11. `WebhookSubscriptionWorkspaceActivationListener.enqueueSync` — `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/listeners/webhook-subscription-workspace-activation.listener.ts` (`webhookQueueService.add`)
  12. `WebhookSubscriptionWorkspaceSyncService.enqueue` — `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/services/webhook-subscription-workspace-sync.service.ts` (`webhookQueueService.bulkAdd`)
* **(b) Consumers / Processors (`7` methods)**:
  1. `CallWebhookJob.handle` — `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook.job.ts` (`@Process(CallWebhookJob.name)`)
  2. `CallWebhookJobsForMetadataJob.handle` — `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook-jobs-for-metadata.job.ts` (`@Process(CallWebhookJobsForMetadataJob.name)`)
  3. `CallWebhookJobsJob.handle` — `packages/twenty-server/src/engine/metadata-modules/webhook/jobs/call-webhook-jobs.job.ts` (`@Process(CallWebhookJobsJob.name)`)
  4. `CreateWebhookSubscriptionJob.handle` — `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/jobs/create-webhook-subscription.job.ts` (`@Process(CreateWebhookSubscriptionJob.name)`)
  5. `RenewWebhookSubscriptionJob.handle` — `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/jobs/renew-webhook-subscription.job.ts` (`@Process(RenewWebhookSubscriptionJob.name)`)
  6. `RevokeWebhookSubscriptionJob.handle` — `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/jobs/revoke-webhook-subscription.job.ts` (`@Process(RevokeWebhookSubscriptionJob.name)`)
  7. `SyncWorkspaceWebhookSubscriptionsJob.handle` — `packages/twenty-server/src/modules/connected-account/webhook-subscription-manager/jobs/sync-workspace-webhook-subscriptions.job.ts` (`@Process(SyncWorkspaceWebhookSubscriptionsJob.name)`)

---

### 2. `workspace-queue` (`MessageQueue.workspaceQueue`)
* **(a) Producers / Enqueuers (`8` job-publishing methods + `2` queue inspection methods = `10` total queue methods)**:
  1. `AgentChatChannelGrantService.enqueueChannelGrantsRewrite` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/agent-chat-channel-grant.service.ts` (`workspaceQueueService.add`)
  2. `ApplicationLifecycleJobService.enqueueLifecycleJob` — `packages/twenty-server/src/engine/core-modules/application/application-install/services/application-lifecycle-job.service.ts` (`workspaceQueueService.add`)
  3. `BillingWebhookSubscriptionService.applySubscriptionEvent` — `packages/twenty-server/src/engine/core-modules/billing-webhook/services/billing-webhook-subscription.service.ts` (`messageQueueService.add`)
  4. `DeferredWorkspaceMigrationActionRunnerService.enqueue` — `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-runner/services/deferred-workspace-migration-action-runner.service.ts` (`messageQueueService.add`)
  5. `PreInstalledAppsService.enqueueInstallOnWorkspace` — `packages/twenty-server/src/engine/core-modules/application/pre-installed-apps/pre-installed-apps.service.ts` (`messageQueueService.add`)
  6. `SdkClientGenerationService.enqueueSdkClientGenerationForApplication` — `packages/twenty-server/src/engine/core-modules/sdk-client/sdk-client-generation.service.ts` (`messageQueueService.add`)
  7. `TrashCleanupCronJob.handle` — `packages/twenty-server/src/engine/trash-cleanup/crons/trash-cleanup.cron.job.ts` (`messageQueueService.add`)
  8. `UserService.enqueueWorkspaceMemberEmailUpdate` — `packages/twenty-server/src/engine/core-modules/user/services/user.service.ts` (`workspaceQueueService.add`)
  *(Note: `ApplicationLifecycleJobService.findInFlightJobIds` and `ApplicationLifecycleJobService.findLifecycleJobStatus` in `packages/twenty-server/src/engine/core-modules/application/application-install/services/application-lifecycle-job.service.ts` also call `workspaceQueueService.getInFlightJobs` and `workspaceQueueService.getJobs` to inspect queue state.)*
* **(b) Consumers / Processors (`11` methods)**:
  1. `CleanWorkspaceDeletionWarningUserVarsJob.handle` — `packages/twenty-server/src/engine/workspace-manager/workspace-cleaner/jobs/clean-workspace-deletion-warning-user-vars.job.ts` (`@Process(CleanWorkspaceDeletionWarningUserVarsJob.name)`)
  2. `GenerateSdkClientJob.handle` — `packages/twenty-server/src/engine/core-modules/sdk-client/jobs/generate-sdk-client.job.ts` (`@Process(GenerateSdkClientJob.name)`)
  3. `HandleWorkspaceMemberDeletedJob.handle` — `packages/twenty-server/src/engine/core-modules/workspace/handle-workspace-member-deleted.job.ts` (`@Process(HandleWorkspaceMemberDeletedJob.name)`)
  4. `InstallPreInstalledAppsJob.handle` — `packages/twenty-server/src/engine/core-modules/application/pre-installed-apps/jobs/install-pre-installed-apps.job.ts` (`@Process(InstallPreInstalledAppsJob.name)`)
  5. `RewriteAgentChatChannelGrantsJob.handle` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/jobs/rewrite-agent-chat-channel-grants.job.ts` (`@Process(RewriteAgentChatChannelGrantsJob.name)`)
  6. `RunDeferredWorkspaceMigrationActionsJob.handle` — `packages/twenty-server/src/engine/workspace-manager/workspace-migration/workspace-migration-runner/jobs/run-deferred-workspace-migration-actions.job.ts` (`@Process(RunDeferredWorkspaceMigrationActionsJob.name)`)
  7. `TrashCleanupJob.handle` — `packages/twenty-server/src/engine/trash-cleanup/jobs/trash-cleanup.job.ts` (`@Process(TrashCleanupJob.name)`)
  8. `TriggerInstallApplicationJob.handle` — `packages/twenty-server/src/engine/core-modules/application/application-install/jobs/trigger-install-application.job.ts` (`@Process(TriggerInstallApplicationJob.name)`)
  9. `TriggerUninstallApplicationJob.handle` — `packages/twenty-server/src/engine/core-modules/application/application-install/jobs/trigger-uninstall-application.job.ts` (`@Process(TriggerUninstallApplicationJob.name)`)
  10. `TriggerUpgradeApplicationJob.handle` — `packages/twenty-server/src/engine/core-modules/application/application-upgrade/jobs/trigger-upgrade-application.job.ts` (`@Process(TriggerUpgradeApplicationJob.name)`)
  11. `UpdateWorkspaceMemberEmailJob.handle` — `packages/twenty-server/src/engine/core-modules/user/jobs/update-workspace-member-email.job.ts` (`@Process(UpdateWorkspaceMemberEmailJob.name)`)

---

### 3. `campaign-queue` (`MessageQueue.campaignQueue`)
* **(a) Producers / Enqueuers (`8` methods)**:
  1. `AddPeopleToMessageListJobService.enqueueAddPeopleToMessageListJob` — `packages/twenty-server/src/modules/emailing/services/add-people-to-message-list-job.service.ts` (`campaignQueueService.add` & `campaignQueueService.getInFlightJobs`)
  2. `CampaignSendingWorkspaceService.enqueueJobForEachSendingWorkspace` — `packages/twenty-server/src/modules/emailing/services/campaign-sending-workspace.service.ts` (`messageQueueService.bulkAdd`)
  3. `MessageCampaignDeliveryService.requeueRateLimitedSendJob` — `packages/twenty-server/src/modules/emailing/services/message-campaign-delivery.service.ts` (`messageQueueService.add`)
  4. `MessageCampaignMaterializationService.enqueueMaterializationChunks` — `packages/twenty-server/src/modules/emailing/services/message-campaign-materialization.service.ts` (`messageQueueService.bulkAdd`)
  5. `MessageCampaignScheduleService.schedule` — `packages/twenty-server/src/modules/emailing/services/message-campaign-schedule.service.ts` (`campaignMessageQueueService.add`)
  6. `MessageCampaignService.claimAndMaterializeOrThrow` — `packages/twenty-server/src/modules/emailing/services/message-campaign.service.ts` (`messageQueueService.add`)
  7. `MessageCampaignStatisticsService.scheduleRefresh` — `packages/twenty-server/src/modules/emailing/services/message-campaign-statistics.service.ts` (`messageQueueService.add`)
  8. `OutboundDeliveryEventHandlerService.handle` — `packages/twenty-server/src/modules/messaging-webhooks/handlers/outbound-delivery-event-handler.service.ts` (`messageQueueService.add`)
* **(b) Consumers / Processors (`9` methods)**:
  1. `AddPeopleToMessageListJob.handle` — `packages/twenty-server/src/modules/emailing/jobs/add-people-to-message-list.job.ts` (`@Process(AddPeopleToMessageListJob.name)`)
  2. `MaterializeCampaignChunkJob.handle` — `packages/twenty-server/src/modules/emailing/jobs/materialize-campaign-chunk.job.ts` (`@Process(MaterializeCampaignChunkJob.name)`)
  3. `MaterializeCampaignJob.handle` — `packages/twenty-server/src/modules/emailing/jobs/materialize-campaign.job.ts` (`@Process(MaterializeCampaignJob.name)`)
  4. `MessagingOutboundDeliveryEventJob.handle` — `packages/twenty-server/src/modules/messaging-webhooks/jobs/messaging-outbound-delivery-event.job.ts` (`@Process(MessagingOutboundDeliveryEventJob.name)`)
  5. `ReconcileWorkspaceCampaignStatsJob.handle` — `packages/twenty-server/src/modules/emailing/jobs/reconcile-workspace-campaign-stats.job.ts` (`@Process(ReconcileWorkspaceCampaignStatsJob.name)`)
  6. `RecoverWorkspaceStaleCampaignsJob.handle` — `packages/twenty-server/src/modules/emailing/jobs/recover-workspace-stale-campaigns.job.ts` (`@Process(RecoverWorkspaceStaleCampaignsJob.name)`)
  7. `RefreshCampaignStatsJob.handle` — `packages/twenty-server/src/modules/emailing/jobs/refresh-campaign-stats.job.ts` (`@Process(RefreshCampaignStatsJob.name)`)
  8. `SendCampaignEmailJob.handle` — `packages/twenty-server/src/modules/emailing/jobs/send-campaign-email.job.ts` (`@Process(SendCampaignEmailJob.name)`)
  9. `SendScheduledCampaignJob.handle` — `packages/twenty-server/src/modules/emailing/jobs/send-scheduled-campaign.job.ts` (`@Process(SendScheduledCampaignJob.name)`)

---

### 4. `ai-queue` (`MessageQueue.aiQueue`)
* **(a) Producers / Enqueuers (`6` methods)**:
  1. `AgentChatWaitingRunService.resolve` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-chat/services/agent-chat-waiting-run.service.ts` (`messageQueueService.add`)
  2. `AgentCronTriggerCronJob.dispatchDueCronTriggers` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-trigger/crons/agent-cron-trigger.cron.job.ts` (`messageQueueService.add`)
  3. `AgentRunSuspensionService.deliverOutcome` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/services/agent-run-suspension.service.ts` (`messageQueueService.add`)
  4. `AgentRunSuspensionService.scheduleContinuation` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/services/agent-run-suspension.service.ts` (`messageQueueService.add`)
  5. `AgentRunnerService.start` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/services/agent-runner.service.ts` (`messageQueueService.add`)
  6. `CallAgentDatabaseEventTriggersJob.handle` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-trigger/jobs/call-agent-database-event-triggers.job.ts` (`messageQueueService.bulkAdd`)
* **(b) Consumers / Processors (`4` methods)**:
  1. `ContinueAgentRunJob.handle` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/jobs/continue-agent-run.job.ts` (`@Process(ContinueAgentRunJob.name)`)
  2. `DeliverAgentRunOutcomeJob.handle` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/jobs/deliver-agent-run-outcome.job.ts` (`@Process(DeliverAgentRunOutcomeJob.name)`)
  3. `RunAgentJob.handle` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-execution/jobs/run-agent.job.ts` (`@Process(RunAgentJob.name)`)
  4. `RunAgentTriggerJob.handle` — `packages/twenty-server/src/engine/metadata-modules/ai/ai-agent-trigger/jobs/run-agent-trigger.job.ts` (`@Process(RunAgentTriggerJob.name)`)

---

### 5. `delete-cascade-queue` (`MessageQueue.deleteCascadeQueue`)
* **(a) Producers / Enqueuers (`5` methods)**:
  1. `CalendarEventCleanerCalendarEventListener.handleDestroyedEvent` — `packages/twenty-server/src/modules/calendar/calendar-event-cleaner/listeners/calendar-event-cleaner-calendar-event.listener.ts` (`messageQueueService.add`)
  2. `FilesFieldDeletionListener.handleDestroyedEvent` — `packages/twenty-server/src/engine/core-modules/file/files-field/listeners/files-field-deletion.listener.ts` (`messageQueueService.add`)
  3. `MessagingMessageCleanerMessageListener.handleDestroyedEvent` — `packages/twenty-server/src/modules/messaging/message-cleaner/listeners/messaging-message-cleaner-message.listener.ts` (`messageQueueService.add`)
  4. `NavigationMenuItemDeletionListener.handleDeletedEvent` — `packages/twenty-server/src/engine/metadata-modules/navigation-menu-item/listeners/navigation-menu-item-deletion.listener.ts` (`messageQueueService.add`)
  5. `WorkspaceService.hardDeleteWorkspaceWithApplicationUninstallLock` — `packages/twenty-server/src/engine/core-modules/workspace/services/workspace.service.ts` (`deleteCascadeMessageQueueService.add`)
* **(b) Consumers / Processors (`6` methods)**:
  1. `EmailingDomainWorkspaceCleanupJob.handle` — `packages/twenty-server/src/engine/core-modules/emailing-domain/jobs/emailing-domain-workspace-cleanup.job.ts` (`@Process(EmailingDomainWorkspaceCleanupJob.name)`)
  2. `FileDeletionJob.handle` — `packages/twenty-server/src/engine/core-modules/file/jobs/file-deletion.job.ts` (`@Process(FileDeletionJob.name)`)
  3. `FileWorkspaceFolderDeletionJob.handle` — `packages/twenty-server/src/engine/core-modules/file/jobs/file-workspace-folder-deletion.job.ts` (`@Process(FileWorkspaceFolderDeletionJob.name)`)
  4. `FilesFieldDeletionJob.handle` — `packages/twenty-server/src/engine/core-modules/file/files-field/jobs/files-field-deletion.job.ts` (`@Process(FilesFieldDeletionJob.name)`)
  5. `LinkedTimelineActivityDeletionJob.handle` — `packages/twenty-server/src/modules/timeline/jobs/linked-timeline-activity-deletion.job.ts` (`@Process(LinkedTimelineActivityDeletionJob.name)`)
  6. `NavigationMenuItemDeletionJob.handle` — `packages/twenty-server/src/engine/metadata-modules/navigation-menu-item/jobs/navigation-menu-item-deletion.job.ts` (`@Process(NavigationMenuItemDeletionJob.name)`)

---

## Task 4 — End-to-End API-to-Database Write Lineage (`Layer 4–5`)

### 1. Table: `billingSubscription` (`BillingSubscriptionEntity`)
* **Total Distinct Writing Endpoints (0..3 hops)**: `10` (`9` GraphQL `@Mutation` endpoints + `1` REST `POST` endpoint)

| # | Endpoint Name | Operation / HTTP Method | Handler Method (`Class.method`) | Handler Relative File Path | Hop Count & Write Call Chain |
|---|---|---|---|---|---|
| 1 | `cancelSwitchBillingInterval` | GraphQL `Mutation` | `BillingResolver.cancelSwitchBillingInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.cancelSwitchBillingInterval` $\to$ `BillingSubscriptionUpdateService.cancelSwitchInterval` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingSubscriptionRepository.upsert`) |
| 2 | `cancelSwitchBillingPlan` | GraphQL `Mutation` | `BillingResolver.cancelSwitchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.cancelSwitchBillingPlan` $\to$ `BillingSubscriptionUpdateService.cancelSwitchPlan` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingSubscriptionRepository.upsert`) |
| 3 | `cancelSwitchResourceCreditPrice` | GraphQL `Mutation` | `BillingResolver.cancelSwitchResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.cancelSwitchResourceCreditPrice` $\to$ `BillingSubscriptionUpdateService.cancelSwitchResourceCreditPrice` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingSubscriptionRepository.upsert`) |
| 4 | `checkoutSession` | GraphQL `Mutation` | `BillingResolver.checkoutSession` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **2 hops**: `BillingResolver.checkoutSession` $\to$ `BillingPortalWorkspaceService.createDirectSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingSubscriptionRepository.upsert`) |
| 5 | `createSubscriptionPaymentIntent` | GraphQL `Mutation` | `BillingResolver.createSubscriptionPaymentIntent` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **2 hops**: `BillingResolver.createSubscriptionPaymentIntent` $\to$ `BillingPortalWorkspaceService.createSubscriptionPaymentIntent` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingSubscriptionRepository.upsert`) |
| 6 | `endSubscriptionTrialPeriod` | GraphQL `Mutation` | `BillingResolver.endSubscriptionTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **2 hops**: `BillingResolver.endSubscriptionTrialPeriod` $\to$ `BillingSubscriptionService.endTrialPeriod` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingSubscriptionRepository.upsert`) |
| 7 | `setResourceCreditSubscriptionPrice` | GraphQL `Mutation` | `BillingResolver.setResourceCreditSubscriptionPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.setResourceCreditSubscriptionPrice` $\to$ `BillingSubscriptionUpdateService.changeResourceCreditPrice` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingSubscriptionRepository.upsert`) |
| 8 | `switchBillingPlan` | GraphQL `Mutation` | `BillingResolver.switchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.switchBillingPlan` $\to$ `BillingSubscriptionUpdateService.changePlan` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingSubscriptionRepository.upsert`) |
| 9 | `switchSubscriptionInterval` | GraphQL `Mutation` | `BillingResolver.switchSubscriptionInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.switchSubscriptionInterval` $\to$ `BillingSubscriptionUpdateService.changeInterval` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingSubscriptionRepository.upsert`) |
| 10 | `webhooks/stripe` (`${ApiPath.Webhooks}/stripe`) | REST `POST` | `BillingWebhookController.handleWebhooks` | `packages/twenty-server/src/engine/core-modules/billing-webhook/billing-webhook.controller.ts` | **2 hops**: `BillingWebhookController.handleWebhooks` $\to$ `BillingWebhookController.handleStripeEvent` $\to$ `BillingWebhookSubscriptionScheduleService.processStripeEvent` (`billingSubscriptionRepository.update`); **3 hops**: `BillingWebhookController.handleWebhooks` $\to$ `BillingWebhookController.handleStripeEvent` $\to$ `BillingWebhookSubscriptionService.processStripeEvent` $\to$ `BillingWebhookSubscriptionService.applySubscriptionEvent` (`billingSubscriptionRepository.upsert`) |

---

### 2. Table: `billingCustomer` (`BillingCustomerEntity`)
* **Total Distinct Writing Endpoints (0..3 hops)**: `12` (`11` GraphQL `@Mutation` endpoints + `1` REST `POST` endpoint)

| # | Endpoint Name | Operation / HTTP Method | Handler Method (`Class.method`) | Handler Relative File Path | Hop Count & Write Call Chain |
|---|---|---|---|---|---|
| 1 | `cancelSwitchBillingInterval` | GraphQL `Mutation` | `BillingResolver.cancelSwitchBillingInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.cancelSwitchBillingInterval` $\to$ `BillingSubscriptionUpdateService.cancelSwitchInterval` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingCustomerRepository.upsert`) |
| 2 | `cancelSwitchBillingPlan` | GraphQL `Mutation` | `BillingResolver.cancelSwitchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.cancelSwitchBillingPlan` $\to$ `BillingSubscriptionUpdateService.cancelSwitchPlan` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingCustomerRepository.upsert`) |
| 3 | `cancelSwitchResourceCreditPrice` | GraphQL `Mutation` | `BillingResolver.cancelSwitchResourceCreditPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.cancelSwitchResourceCreditPrice` $\to$ `BillingSubscriptionUpdateService.cancelSwitchResourceCreditPrice` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingCustomerRepository.upsert`) |
| 4 | `checkoutSession` | GraphQL `Mutation` | `BillingResolver.checkoutSession` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **2 hops**: `BillingResolver.checkoutSession` $\to$ `BillingPortalWorkspaceService.createDirectSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingCustomerRepository.upsert`) |
| 5 | `createSubscriptionPaymentIntent` | GraphQL `Mutation` | `BillingResolver.createSubscriptionPaymentIntent` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **2 hops**: `BillingResolver.createSubscriptionPaymentIntent` $\to$ `BillingPortalWorkspaceService.createSubscriptionPaymentIntent` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingCustomerRepository.upsert`) |
| 6 | `endSubscriptionTrialPeriod` | GraphQL `Mutation` | `BillingResolver.endSubscriptionTrialPeriod` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **2 hops**: `BillingResolver.endSubscriptionTrialPeriod` $\to$ `BillingSubscriptionService.endTrialPeriod` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingCustomerRepository.upsert`) |
| 7 | `setResourceCreditSubscriptionPrice` | GraphQL `Mutation` | `BillingResolver.setResourceCreditSubscriptionPrice` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.setResourceCreditSubscriptionPrice` $\to$ `BillingSubscriptionUpdateService.changeResourceCreditPrice` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingCustomerRepository.upsert`) |
| 8 | `signUpInNewWorkspace` | GraphQL `Mutation` | `AuthResolver.signUpInNewWorkspace` | `packages/twenty-server/src/engine/core-modules/auth/auth.resolver.ts` | **3 hops**: `AuthResolver.signUpInNewWorkspace` $\to$ `SignInUpService.signUpOnNewWorkspace` $\to$ `BillingService.ensureBillingCustomer` $\to$ `StripeCustomerService.createStripeCustomer` (`billingCustomerRepository.insert`) |
| 9 | `switchBillingPlan` | GraphQL `Mutation` | `BillingResolver.switchBillingPlan` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.switchBillingPlan` $\to$ `BillingSubscriptionUpdateService.changePlan` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingCustomerRepository.upsert`) |
| 10 | `switchSubscriptionInterval` | GraphQL `Mutation` | `BillingResolver.switchSubscriptionInterval` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **3 hops**: `BillingResolver.switchSubscriptionInterval` $\to$ `BillingSubscriptionUpdateService.changeInterval` $\to$ `BillingSubscriptionUpdateService.updateSubscription` $\to$ `BillingSubscriptionService.syncSubscriptionToDatabase` (`billingCustomerRepository.upsert`) |
| 11 | `updateUserCreditsFallback` | GraphQL `Mutation` | `BillingResolver.updateUserCreditsFallback` | `packages/twenty-server/src/engine/core-modules/billing/billing.resolver.ts` | **1 hop**: `BillingResolver.updateUserCreditsFallback` $\to$ `BillingService.updateUserCreditsFallbackOrThrow` (`billingCustomerRepository.update`) |
| 12 | `webhooks/stripe` (`${ApiPath.Webhooks}/stripe`) | REST `POST` | `BillingWebhookController.handleWebhooks` | `packages/twenty-server/src/engine/core-modules/billing-webhook/billing-webhook.controller.ts` | **3 hops**: `BillingWebhookController.handleWebhooks` $\to$ `BillingWebhookController.handleStripeEvent` $\to$ `BillingWebhookCustomerService.processStripeEvent` $\to$ `processCustomerCreated` / `processPaymentMethodAttachedEvent` / `processPaymentMethodDetachedEvent` (`billingCustomerRepository.upsert` / `update`), and via `BillingWebhookSubscriptionService.processStripeEvent` $\to$ `applySubscriptionEvent` (`billingCustomerRepository.upsert`) |

---

### 3. Table: `connectedAccount` (`ConnectedAccountEntity`)
* **Total Distinct Writing Endpoints (0..3 hops)**: `13` (`6` GraphQL `@Mutation` endpoints + `6` REST `GET` endpoints + `1` REST `POST` endpoint)

| # | Endpoint Name | Operation / HTTP Method | Handler Method (`Class.method`) | Handler Relative File Path | Hop Count & Write Call Chain |
|---|---|---|---|---|---|
| 1 | `createEmailGroupChannel` | GraphQL `Mutation` | `MessageChannelResolver.createEmailGroupChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | **2 hops**: `MessageChannelResolver.createEmailGroupChannel` $\to$ `MessageChannelMetadataService.createEmailGroupChannel` $\to$ `ConnectedAccountMetadataService.create` (`repository.save`) |
| 2 | `deleteConnectedAccount` | GraphQL `Mutation` | `ConnectedAccountResolver.deleteConnectedAccount` | `packages/twenty-server/src/engine/metadata-modules/connected-account/resolvers/connected-account.resolver.ts` | **2 hops**: `ConnectedAccountResolver.deleteConnectedAccount` $\to$ `ConnectedAccountMetadataService.delete` $\to$ `ConnectedAccountMetadataService.deleteMany` (`repository.delete`) |
| 3 | `deleteEmailGroupChannel` | GraphQL `Mutation` | `MessageChannelResolver.deleteEmailGroupChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | **3 hops**: `MessageChannelResolver.deleteEmailGroupChannel` $\to$ `MessageChannelMetadataService.deleteEmailGroupChannel` $\to$ `ConnectedAccountMetadataService.delete` $\to$ `ConnectedAccountMetadataService.deleteMany` (`repository.delete`) |
| 4 | `disconnectConnectedAccount` | GraphQL `Mutation` | `ConnectedAccountResolver.disconnectConnectedAccount` | `packages/twenty-server/src/engine/metadata-modules/connected-account/resolvers/connected-account.resolver.ts` | **1 hop**: `ConnectedAccountResolver.disconnectConnectedAccount` $\to$ `ConnectedAccountMetadataService.disconnect` (`manager.update(ConnectedAccountEntity)`) |
| 5 | `reportAppConnectionAuthFailure` | GraphQL `Mutation` | `ApplicationConnectionsResolver.reportAppConnectionAuthFailure` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connections/application-connections.resolver.ts` | **1 hop**: `ApplicationConnectionsResolver.reportAppConnectionAuthFailure` $\to$ `ApplicationConnectionAuthFailureService.reportAuthFailureOrThrow` (`connectedAccountRepository.update`) |
| 6 | `saveImapSmtpCaldavAccount` | GraphQL `Mutation` | `ImapSmtpCaldavResolver.saveImapSmtpCaldavAccount` | `packages/twenty-server/src/engine/core-modules/imap-smtp-caldav-connection/imap-smtp-caldav-connection.resolver.ts` | **1 hop**: `ImapSmtpCaldavResolver.saveImapSmtpCaldavAccount` $\to$ `ImapSmtpCalDavApiService.upsertConnectedAccount` (`getRepository(ConnectedAccountEntity).save` / `update`) |
| 7 | `auth/apps/callback` (`${ApiPath.Auth}/apps/callback`) | REST `GET` | `ConnectionProviderOAuthController.callback` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-oauth.controller.ts` | **2 hops**: `ConnectionProviderOAuthController.callback` $\to$ `ConnectionProviderOAuthFlowService.completeAuthorizationFlow` $\to$ `ConnectionProviderConnectedAccountService.persist` (`connectedAccountRepository.save` / `update`) |
| 8 | `auth/apps/custom/callback` (`${ApiPath.Auth}/apps/custom/callback`) | REST `GET` | `ConnectionProviderCustomController.callback` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-custom.controller.ts` | **2 hops**: `ConnectionProviderCustomController.callback` $\to$ `ConnectionProviderCustomFlowService.completeAuthorizationFlow` $\to$ `ConnectionProviderConnectedAccountService.persist` (`connectedAccountRepository.save` / `update`) |
| 9 | `auth/apps/custom/callback` (`${ApiPath.Auth}/apps/custom/callback`) | REST `POST` | `ConnectionProviderCustomController.callbackNotification` | `packages/twenty-server/src/engine/core-modules/application/connection-provider/connection-provider-custom.controller.ts` | **2 hops**: `ConnectionProviderCustomController.callbackNotification` $\to$ `ConnectionProviderCustomFlowService.completeAuthorizationFlow` $\to$ `ConnectionProviderConnectedAccountService.persist` (`connectedAccountRepository.save` / `update`) |
| 10 | `auth/google-apis/get-access-token` (`${ApiPath.Auth}/google-apis/get-access-token`) | REST `GET` | `GoogleAPIsAuthController.googleAuthGetAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/controllers/google-apis-auth.controller.ts` | **2 hops**: `GoogleAPIsAuthController.googleAuthGetAccessToken` $\to$ `GoogleAPIsService.refreshGoogleRefreshToken` $\to$ `CreateConnectedAccountService.createConnectedAccount` (`save`) / `UpdateConnectedAccountOnReconnectService.updateConnectedAccountOnReconnect` (`update`) / `EmailAliasManagerService.refreshHandleAliases` (`update`) / `CalendarChannelSyncStatusService.markAsFailedInsufficientPermissionsAndFlushCalendarEventsToImport` (`update`) / `MessageChannelSyncStatusService.markAsFailed` (`update`) |
| 11 | `auth/google/redirect` (`${ApiPath.Auth}/google/redirect`) | REST `GET` | `GoogleAuthController.googleAuthRedirect` | `packages/twenty-server/src/engine/core-modules/auth/controllers/google-auth.controller.ts` | **3 hops**: `GoogleAuthController.googleAuthRedirect` $\to$ `AuthService.signInUpWithSocialSso` $\to$ `AuthService.createSsoConnectedAccountIfFeatureFlagIsOn` $\to$ `CreateSsoConnectedAccountService.createOrUpdateSsoConnectedAccount` (`connectedAccountRepository.save` / `update`) |
| 12 | `auth/microsoft-apis/get-access-token` (`${ApiPath.Auth}/microsoft-apis/get-access-token`) | REST `GET` | `MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/controllers/microsoft-apis-auth.controller.ts` | **2 hops**: `MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken` $\to$ `MicrosoftAPIsService.refreshMicrosoftRefreshToken` $\to$ `CreateConnectedAccountService.createConnectedAccount` (`save`) / `UpdateConnectedAccountOnReconnectService.updateConnectedAccountOnReconnect` (`update`) / `EmailAliasManagerService.refreshHandleAliases` (`update`) |
| 13 | `auth/microsoft/redirect` (`${ApiPath.Auth}/microsoft/redirect`) | REST `GET` | `MicrosoftAuthController.microsoftAuthRedirect` | `packages/twenty-server/src/engine/core-modules/auth/controllers/microsoft-auth.controller.ts` | **3 hops**: `MicrosoftAuthController.microsoftAuthRedirect` $\to$ `AuthService.signInUpWithSocialSso` $\to$ `AuthService.createSsoConnectedAccountIfFeatureFlagIsOn` $\to$ `CreateSsoConnectedAccountService.createOrUpdateSsoConnectedAccount` (`connectedAccountRepository.save` / `update`) |

---

### 4. Table: `messageChannel` (`MessageChannelEntity`)
* **Total Distinct Writing Endpoints (0..3 hops)**: `10` (`8` GraphQL `@Mutation` endpoints + `2` REST `GET` endpoints)

| # | Endpoint Name | Operation / HTTP Method | Handler Method (`Class.method`) | Handler Relative File Path | Hop Count & Write Call Chain |
|---|---|---|---|---|---|
| 1 | `createAppMessageChannel` | GraphQL `Mutation` | `ApplicationMessageChannelsResolver.createAppMessageChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/application-message-channels.resolver.ts` | **1 hop**: `ApplicationMessageChannelsResolver.createAppMessageChannel` $\to$ `ApplicationMessageChannelsService.create` (`messageChannelRepository.save`) |
| 2 | `createEmailGroupChannel` | GraphQL `Mutation` | `MessageChannelResolver.createEmailGroupChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | **2 hops**: `MessageChannelResolver.createEmailGroupChannel` $\to$ `MessageChannelMetadataService.createEmailGroupChannel` $\to$ `MessageChannelMetadataService.create` (`repository.save`) |
| 3 | `deleteAppMessageChannel` | GraphQL `Mutation` | `ApplicationMessageChannelsResolver.deleteAppMessageChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/application-message-channels.resolver.ts` | **1 hop**: `ApplicationMessageChannelsResolver.deleteAppMessageChannel` $\to$ `ApplicationMessageChannelsService.delete` (`messageChannelRepository.delete`) |
| 4 | `disconnectConnectedAccount` | GraphQL `Mutation` | `ConnectedAccountResolver.disconnectConnectedAccount` | `packages/twenty-server/src/engine/metadata-modules/connected-account/resolvers/connected-account.resolver.ts` | **1 hop**: `ConnectedAccountResolver.disconnectConnectedAccount` $\to$ `ConnectedAccountMetadataService.disconnect` (`manager.update(MessageChannelEntity)`) |
| 5 | `saveImapSmtpCaldavAccount` | GraphQL `Mutation` | `ImapSmtpCaldavResolver.saveImapSmtpCaldavAccount` | `packages/twenty-server/src/engine/core-modules/imap-smtp-caldav-connection/imap-smtp-caldav-connection.resolver.ts` | **1 hop**: `ImapSmtpCaldavResolver.saveImapSmtpCaldavAccount` $\to$ `ImapSmtpCalDavApiService.upsertConnectedAccount` (`getRepository(MessageChannelEntity).save` / `update`); **2 hops**: via `CreateMessageChannelService.createMessageChannel` (`save`) and `MessageChannelSyncStatusService.resetAndMarkAsMessagesListFetchPending` (`update`) |
| 6 | `updateAppMessageChannel` | GraphQL `Mutation` | `ApplicationMessageChannelsResolver.updateAppMessageChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/application-message-channels.resolver.ts` | **1 hop**: `ApplicationMessageChannelsResolver.updateAppMessageChannel` $\to$ `ApplicationMessageChannelsService.update` (`messageChannelRepository.update`) |
| 7 | `updateEmailGroupChannel` | GraphQL `Mutation` | `MessageChannelResolver.updateEmailGroupChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | **2 hops**: `MessageChannelResolver.updateEmailGroupChannel` $\to$ `MessageChannelMetadataService.updateEmailGroupChannel` $\to$ `MessageChannelMetadataService.update` (`repository.update`) |
| 8 | `updateMessageChannel` | GraphQL `Mutation` | `MessageChannelResolver.updateMessageChannel` | `packages/twenty-server/src/engine/metadata-modules/message-channel/resolvers/message-channel.resolver.ts` | **1 hop**: `MessageChannelResolver.updateMessageChannel` $\to$ `MessageChannelMetadataService.update` (`repository.update`) and `MessagingProcessGroupEmailActionsService.markMessageChannelAsPendingGroupEmailsAction` (`messageChannelRepository.update`) |
| 9 | `auth/google-apis/get-access-token` (`${ApiPath.Auth}/google-apis/get-access-token`) | REST `GET` | `GoogleAPIsAuthController.googleAuthGetAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/controllers/google-apis-auth.controller.ts` | **1 hop**: `GoogleAPIsAuthController.googleAuthGetAccessToken` $\to$ `GoogleAPIsService.refreshGoogleRefreshToken` (`getRepository(MessageChannelEntity).update`); **2 hops**: via `CreateMessageChannelService.createMessageChannel` (`save`) and `MessageChannelSyncStatusService.resetAndMarkAsMessagesListFetchPending` (`update`); **3 hops**: via `MessageChannelSyncStatusService.markAsFailed` $\to$ `updateMessageChannels` (`createQueryBuilder`) and `OnboardingRecentMessagesImportService.importRecentMessages` $\to$ `MessagingMessagesImportService.processMessageBatchImport` (`update`) |
| 10 | `auth/microsoft-apis/get-access-token` (`${ApiPath.Auth}/microsoft-apis/get-access-token`) | REST `GET` | `MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken` | `packages/twenty-server/src/engine/core-modules/auth/controllers/microsoft-apis-auth.controller.ts` | **1 hop**: `MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken` $\to$ `MicrosoftAPIsService.refreshMicrosoftRefreshToken` (`getRepository(MessageChannelEntity).update`); **2 hops**: via `CreateMessageChannelService.createMessageChannel` (`save`) and `MessageChannelSyncStatusService.resetAndMarkAsMessagesListFetchPending` (`update`); **3 hops**: via `OnboardingRecentMessagesImportService.importRecentMessages` $\to$ `MessagingMessagesImportService.processMessageBatchImport` (`update`) |
