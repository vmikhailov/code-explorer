# Comprehensive 4-Part Architectural Audit of `twenty`

---

## Task 1 — Service Method Call Graph & DI Fan-In (`Layer 3`)

### 1. `BillingSubscriptionService` (`totalCallPairs`: **54**)
- **`AdminPanelBillingService`** (1): `getWorkspaceBilling -> getCurrentBillingSubscription`
- **`BillingCreditService`** (1): `writeGrantAndRefreshState -> getCurrentBillingSubscription`
- **`BillingPartnerCodeService`** (1): `isTrialingOrWithoutSubscription -> getCurrentBillingSubscription`
- **`BillingPortalWorkspaceService`** (2): `createDirectSubscription -> syncSubscriptionToDatabase`, `createSubscriptionPaymentIntent -> syncSubscriptionToDatabase`
- **`BillingResolver`** (15): `cancelSwitchBillingInterval -> getBillingSubscriptions`, `cancelSwitchBillingInterval -> getCurrentBillingSubscriptionOrThrow`, `cancelSwitchBillingPlan -> getBillingSubscriptions`, `cancelSwitchBillingPlan -> getCurrentBillingSubscriptionOrThrow`, `cancelSwitchResourceCreditPrice -> getBillingSubscriptions`, `cancelSwitchResourceCreditPrice -> getCurrentBillingSubscriptionOrThrow`, `endSubscriptionTrialPeriod -> endTrialPeriod`, `endSubscriptionTrialPeriod -> getBillingSubscriptions`, `endSubscriptionTrialPeriod -> getCurrentBillingSubscriptionOrThrow`, `setResourceCreditSubscriptionPrice -> getBillingSubscriptions`, `setResourceCreditSubscriptionPrice -> getCurrentBillingSubscriptionOrThrow`, `switchBillingPlan -> getBillingSubscriptions`, `switchBillingPlan -> getCurrentBillingSubscriptionOrThrow`, `switchSubscriptionInterval -> getBillingSubscriptions`, `switchSubscriptionInterval -> getCurrentBillingSubscriptionOrThrow`
- **`BillingService`** (1): `hasEntitlement -> getWorkspaceEntitlementByKey`
- **`BillingSubscriptionService`** (7): `assertSubscriptionCanceledOrNone -> getCurrentBillingSubscription`, `cancelSubscription -> getCurrentBillingSubscription`, `endTrialPeriod -> getCurrentBillingSubscriptionOrThrow`, `endTrialPeriod -> syncSubscriptionToDatabase`, `getCurrentBillingSubscriptionOrThrow -> getCurrentBillingSubscription`, `getWorkspaceEntitlementValue -> getWorkspaceEntitlementByKey`, `handleUnpaidInvoices -> getCurrentBillingSubscription`
- **`BillingSubscriptionUpdateService`** (8): `cancelSwitchInterval -> getCurrentBillingSubscriptionOrThrow`, `cancelSwitchPlan -> getCurrentBillingSubscriptionOrThrow`, `cancelSwitchResourceCreditPrice -> getCurrentBillingSubscriptionOrThrow`, `changeInterval -> getCurrentBillingSubscriptionOrThrow`, `changePlan -> getCurrentBillingSubscriptionOrThrow`, `changeResourceCreditPrice -> getCurrentBillingSubscriptionOrThrow`, `changeSeats -> getCurrentBillingSubscriptionOrThrow`, `updateSubscription -> syncSubscriptionToDatabase`
- **`BillingUpdateSubscriptionPriceCommand`** (1): `runOnWorkspace -> getCurrentBillingSubscriptionOrThrow`
- **`BillingUsageService`** (2): `getResourceCreditProductUsage -> getCurrentBillingSubscriptionOrThrow`, `getTrialResourceUsageCap -> getTrialPeriodFreeWorkflowCredits`
- **`BillingWebhookController`** (1): `handleStripeEvent -> handleUnpaidInvoices`
- **`BillingWebhookInvoiceService`** (1): `processInvoiceFinalized -> getCurrentBillingSubscription`
- **`EnableCommonRecordSharingCommand`** (1): `up -> getWorkspaceEntitlementValue`
- **`EventLogRetentionService`** (1): `hasAuditLogsEntitlement -> getWorkspaceEntitlementValue`
- **`RelatedRecordPathService`** (1): `assertHasRelatedRecordPathsEntitlementOrThrow -> getWorkspaceEntitlementValue`
- **`UsageLimitEntitlementService`** (1): `isIntraWorkspaceLimitEntitled -> getWorkspaceEntitlementValue`
- **`ValidationRuleService`** (1): `hasValidationRulesEntitlement -> getWorkspaceEntitlementValue`
- **`WorkspaceCurrentBillingSubscriptionCacheService`** (2): `computeForCache -> getBillingCustomer`, `computeForCache -> getCurrentBillingSubscription`
- **`WorkspaceResolver`** (4): `billingCustomer -> getBillingCustomer`, `billingEntitlements -> getWorkspaceEntitlements`, `billingSubscriptions -> getBillingSubscriptions`, `currentBillingSubscription -> getCurrentBillingSubscription`
- **`WorkspaceService`** (2): `deleteWorkspace -> cancelSubscription`, `hardDeleteWorkspaceWithApplicationUninstallLock -> assertSubscriptionCanceledOrNone`

### 2. `ThrottlerService` (`totalCallPairs`: **24**)
- **`AgentTriggerThrottlerService`** (1): `consumeAvailableRuns -> tokenBucketConsumeUpTo`
- **`AppBillingController`** (2): `charge -> tokenBucketThrottleOrThrow`, `credits -> tokenBucketThrottleOrThrow`
- **`ApplicationDevelopmentService`** (1): `throttlePerApplication -> tokenBucketThrottleOrThrow`
- **`ApplicationHealthCheckService`** (1): `runForUser -> tokenBucketThrottleOrThrow`
- **`ApplicationJobEnqueueThrottlerService`** (1): `throttleOrThrow -> tryConsumeTokenBuckets`
- **`ApplicationOAuthResolver`** (1): `renewApplicationToken -> tokenBucketThrottleOrThrow`
- **`AuthResolver`** (1): `emailPasswordResetLink -> tokenBucketThrottleOrThrow`
- **`CampaignEngagementCaptureService`** (1): `throttleAndEnqueue -> tokenBucketThrottleOrThrow`
- **`EnrichmentThrottleService`** (1): `consumeToken -> tokenBucketThrottleOrThrow`
- **`LogicFunctionExecutorService`** (1): `throttleExecution -> tokenBucketThrottleOrThrow`
- **`MessageCampaignService`** (1): `sendTest -> tokenBucketThrottleOrThrow`
- **`OAuthRegistrationController`** (1): `applyRateLimit -> tokenBucketThrottleOrThrow`
- **`OAuthTokenController`** (1): `applyRateLimit -> tokenBucketThrottleOrThrow`
- **`ThrottlerService`** (2): `tokenBucketConsumeUpTo -> tryConsumeTokenBuckets`, `tokenBucketThrottleOrThrow -> tryConsumeTokenBuckets`
- **`TwoFactorAuthenticationRecoveryService`** (2): `generateRecoveryCode -> tokenBucketThrottleOrThrow`, `redeemRecoveryCode -> tokenBucketThrottleOrThrow`
- **`TwoFactorAuthenticationService`** (1): `validateStrategy -> tokenBucketThrottleOrThrow`
- **`UnsubscribeController`** (1): `throttleOrThrow -> tokenBucketThrottleOrThrow`
- **`UsageLimitSpeedService`** (1): `consumeTokens -> tryConsumeTokenBuckets`
- **`WorkflowThrottlingWorkspaceService`** (2): `consumeRemainingRunsToEnqueueCount -> tokenBucketConsumeUpTo`, `throttleOrThrowIfHardLimitReached -> tokenBucketThrottleOrThrow`
- **`WorkspaceInvitationService`** (1): `throttleInvitationSending -> tokenBucketThrottleOrThrow`

### 3. `AccessTokenService` (`totalCallPairs`: **15**)
- **`AccessTokenService`** (4): `generateAccessToken -> resolveTokenSubject`, `generatePlaygroundToken -> resolveTokenSubject`, `validateTokenByRequest -> validateSessionToken`, `validateTokenByRequest -> validateToken`
- **`AuthResolver`** (1): `generatePlaygroundToken -> generatePlaygroundToken`
- **`AuthService`** (2): `generateImpersonationAccessTokenAndRefreshToken -> generateAccessToken`, `verify -> generateAccessToken`
- **`JwtAuthGuard`** (1): `canActivate -> validateTokenByRequest`
- **`MiddlewareService`** (2): `hydrateGraphqlRequest -> validateTokenByRequest`, `hydrateRestRequest -> validateTokenByRequest`
- **`OpenApiService`** (1): `getWorkspaceFromRequest -> validateTokenByRequest`
- **`RenewTokenService`** (1): `generateTokensFromRefreshToken -> generateAccessToken`
- **`RestApiBaseHandler`** (1): `getObjectMetadata -> validateTokenByRequest`
- **`RouteTriggerService`** (2): `handle -> validateTokenByRequest`, `resolveAuthenticationContextForWorkspaceFallback -> validateTokenByRequest`

---

## Task 2 — Multi-Hop Class Inheritance & Interface Realization (`Layer 3`)

### (a) Subclasses of `SyncableEntity` (1 to 3 hops) — **Total: 43**
- **1-Hop Direct Subclasses (33)**: `AgentChatChannelEntity`, `AgentChatChannelRoleEntity`, `ApplicationVariableEntity`, `ConnectionProviderEntity`, `FieldMetadataEntity`, `FieldPermissionEntity`, `FrontComponentEntity`, `IndexMetadataEntity`, `LogicFunctionEntity`, `McpServerEntity`, `MessagingProviderEntity`, `NavigationMenuItemEntity`, `ObjectMetadataEntity`, `ObjectPermissionEntity`, `OverridableEntity`, `PageLayoutEntity`, `PermissionFlagEntity`, `RelatedRecordPathEntity`, `RoleEntity`, `RolePermissionFlagEntity`, `RoleTargetEntity`, `RowLevelPermissionPredicateEntity`, `RowLevelPermissionPredicateGroupEntity`, `SearchFieldMetadataEntity`, `SettingsMenuItemEntity`, `ValidationRuleEntity`, `ViewFilterEntity`, `ViewFilterGroupEntity`, `ViewGroupEntity`, `ViewSortEntity`, `WebhookEntity`, `WorkflowEntity`, `WorkflowVersionEntity`
- **2-Hop Transitive Subclasses via `OverridableEntity` (10)**: `AgentEntity`, `CommandMenuItemEntity`, `FieldMetadataOptionEntity`, `PageLayoutTabEntity`, `PageLayoutWidgetEntity`, `SkillEntity`, `TimelineActivityTypeEntity`, `ViewEntity`, `ViewFieldEntity`, `ViewFieldGroupEntity`
- **3-Hop Transitive Subclasses (0)**: None

### (b) Subclasses of `WorkspaceRelatedEntity` (1 to 3 hops) — **Total: 67** (57 in 1–2 hops; 67 in 1–3 hops)
- **1-Hop Direct Subclasses (24)**: `ApiKeyEntity`, `ApplicationEntity`, `ApprovedAccessDomainEntity`, `BillingCreditGrantEntity`, `BillingCustomerEntity`, `BillingEntitlementEntity`, `BillingSubscriptionEntity`, `CalendarChannelEntity`, `ConnectedAccountEntity`, `DataSourceEntity`, `DeferredWorkspaceMigrationActionEntity`, `EmailingDomainEntity`, `FeatureFlagEntity`, `LogicFunctionLayerEntity`, `MessageChannelEntity`, `MessageFolderEntity`, `PendingWakeUpEntity`, `PublicDomainEntity`, `SyncableEntity`, `UnsubscribeTopicEntity`, `UsageLimitEntity`, `UserApplicationVariableValueEntity`, `UserWorkspaceEntity`, `WorkspaceSsoIdentityProviderEntity`
- **2-Hop Transitive Subclasses via `SyncableEntity` (33)**: `AgentChatChannelEntity`, `AgentChatChannelRoleEntity`, `ApplicationVariableEntity`, `ConnectionProviderEntity`, `FieldMetadataEntity`, `FieldPermissionEntity`, `FrontComponentEntity`, `IndexMetadataEntity`, `LogicFunctionEntity`, `McpServerEntity`, `MessagingProviderEntity`, `NavigationMenuItemEntity`, `ObjectMetadataEntity`, `ObjectPermissionEntity`, `OverridableEntity`, `PageLayoutEntity`, `PermissionFlagEntity`, `RelatedRecordPathEntity`, `RoleEntity`, `RolePermissionFlagEntity`, `RoleTargetEntity`, `RowLevelPermissionPredicateEntity`, `RowLevelPermissionPredicateGroupEntity`, `SearchFieldMetadataEntity`, `SettingsMenuItemEntity`, `ValidationRuleEntity`, `ViewFilterEntity`, `ViewFilterGroupEntity`, `ViewGroupEntity`, `ViewSortEntity`, `WebhookEntity`, `WorkflowEntity`, `WorkflowVersionEntity`
- **3-Hop Transitive Subclasses via `OverridableEntity -> SyncableEntity` (10)**: `AgentEntity`, `CommandMenuItemEntity`, `FieldMetadataOptionEntity`, `PageLayoutTabEntity`, `PageLayoutWidgetEntity`, `SkillEntity`, `TimelineActivityTypeEntity`, `ViewEntity`, `ViewFieldEntity`, `ViewFieldGroupEntity`

### (c) Subclasses of `WorkspaceEntityMigrationBuilderService` — **Total: 42**
- **1-Hop Direct Subclasses (42)**: `WorkspaceMigrationAgentActionsBuilderService`, `WorkspaceMigrationAgentChatChannelActionsBuilderService`, `WorkspaceMigrationAgentChatChannelRoleActionsBuilderService`, `WorkspaceMigrationApplicationVariableActionsBuilderService`, `WorkspaceMigrationCommandMenuItemActionsBuilderService`, `WorkspaceMigrationConnectionProviderActionsBuilderService`, `WorkspaceMigrationFieldActionsBuilderService`, `WorkspaceMigrationFieldMetadataOptionActionsBuilderService`, `WorkspaceMigrationFieldPermissionActionsBuilderService`, `WorkspaceMigrationFrontComponentActionsBuilderService`, `WorkspaceMigrationIndexActionsBuilderService`, `WorkspaceMigrationLogicFunctionActionsBuilderService`, `WorkspaceMigrationMcpServerActionsBuilderService`, `WorkspaceMigrationMessagingProviderActionsBuilderService`, `WorkspaceMigrationNavigationMenuItemActionsBuilderService`, `WorkspaceMigrationObjectActionsBuilderService`, `WorkspaceMigrationObjectPermissionActionsBuilderService`, `WorkspaceMigrationPageLayoutActionsBuilderService`, `WorkspaceMigrationPageLayoutTabActionsBuilderService`, `WorkspaceMigrationPageLayoutWidgetActionsBuilderService`, `WorkspaceMigrationPermissionFlagActionsBuilderService`, `WorkspaceMigrationRelatedRecordPathActionsBuilderService`, `WorkspaceMigrationRoleActionsBuilderService`, `WorkspaceMigrationRolePermissionFlagActionsBuilderService`, `WorkspaceMigrationRoleTargetActionsBuilderService`, `WorkspaceMigrationRowLevelPermissionPredicateActionsBuilderService`, `WorkspaceMigrationRowLevelPermissionPredicateGroupActionsBuilderService`, `WorkspaceMigrationSearchFieldMetadataActionsBuilderService`, `WorkspaceMigrationSettingsMenuItemActionsBuilderService`, `WorkspaceMigrationSkillActionsBuilderService`, `WorkspaceMigrationTimelineActivityTypeActionsBuilderService`, `WorkspaceMigrationValidationRuleActionsBuilderService`, `WorkspaceMigrationViewActionsBuilderService`, `WorkspaceMigrationViewFieldActionsBuilderService`, `WorkspaceMigrationViewFieldGroupActionsBuilderService`, `WorkspaceMigrationViewFilterActionsBuilderService`, `WorkspaceMigrationViewFilterGroupActionsBuilderService`, `WorkspaceMigrationViewGroupActionsBuilderService`, `WorkspaceMigrationViewSortActionsBuilderService`, `WorkspaceMigrationWebhookActionsBuilderService`, `WorkspaceMigrationWorkflowActionsBuilderService`, `WorkspaceMigrationWorkflowVersionActionsBuilderService`

### (d) Implementors of `WorkspaceResolverBuilderFactoryInterface` — **Total: 15**
- **Direct Implementors (15)**: `CreateManyResolverFactory`, `CreateOneResolverFactory`, `DeleteManyResolverFactory`, `DeleteOneResolverFactory`, `DestroyManyResolverFactory`, `DestroyOneResolverFactory`, `FindDuplicatesResolverFactory`, `FindManyResolverFactory`, `FindOneResolverFactory`, `GroupByResolverFactory`, `MergeManyResolverFactory`, `RestoreManyResolverFactory`, `RestoreOneResolverFactory`, `UpdateManyResolverFactory`, `UpdateOneResolverFactory`

---

## Task 3 — Asynchronous BullMQ Queue Producers & Consumers (`Layer 4`)

### 1. `webhook-queue`
- **(a) Publishers / Producers (12)**: `CallWebhookJobsForMetadataJob.handle`, `CallWebhookJobsJob.handle`, `ChannelSyncService.startCalendarChannelSync`, `ChannelSyncService.startMessageChannelSync`, `CreateWebhookSubscriptionForConnectedAccountCommand.enqueueChannels`, `EntityEventsToDbListener.enqueueWebhookJobsIfAnyWebhookMatches`, `GoogleAPIsService.enqueueWebhookSubscriptionCreation`, `MetadataEventsToDbListener.handleEvent`, `MicrosoftAPIsService.enqueueWebhookSubscriptionCreation`, `WebhookSubscriptionRenewalCronJob.enqueueRenewals`, `WebhookSubscriptionWorkspaceActivationListener.enqueueSync`, `WebhookSubscriptionWorkspaceSyncService.enqueue`
- **(b) Subscribers / Consumers (7)**: `CallWebhookJob.handle`, `CallWebhookJobsForMetadataJob.handle`, `CallWebhookJobsJob.handle`, `CreateWebhookSubscriptionJob.handle`, `RenewWebhookSubscriptionJob.handle`, `RevokeWebhookSubscriptionJob.handle`, `SyncWorkspaceWebhookSubscriptionsJob.handle`

### 2. `workspace-queue`
- **(a) Publishers / Producers (8)**: `AgentChatChannelGrantService.enqueueChannelGrantsRewrite`, `ApplicationLifecycleJobService.enqueueLifecycleJob`, `BillingWebhookSubscriptionService.applySubscriptionEvent`, `DeferredWorkspaceMigrationActionRunnerService.enqueue`, `PreInstalledAppsService.enqueueInstallOnWorkspace`, `SdkClientGenerationService.enqueueSdkClientGenerationForApplication`, `TrashCleanupCronJob.handle`, `UserService.enqueueWorkspaceMemberEmailUpdate`
- **(b) Subscribers / Consumers (11)**: `CleanWorkspaceDeletionWarningUserVarsJob.handle`, `GenerateSdkClientJob.handle`, `HandleWorkspaceMemberDeletedJob.handle`, `InstallPreInstalledAppsJob.handle`, `RewriteAgentChatChannelGrantsJob.handle`, `RunDeferredWorkspaceMigrationActionsJob.handle`, `TrashCleanupJob.handle`, `TriggerInstallApplicationJob.handle`, `TriggerUninstallApplicationJob.handle`, `TriggerUpgradeApplicationJob.handle`, `UpdateWorkspaceMemberEmailJob.handle`

### 3. `campaign-queue`
- **(a) Publishers / Producers (8)**: `AddPeopleToMessageListJobService.enqueueAddPeopleToMessageListJob`, `CampaignSendingWorkspaceService.enqueueJobForEachSendingWorkspace`, `MessageCampaignDeliveryService.requeueRateLimitedSendJob`, `MessageCampaignMaterializationService.enqueueMaterializationChunks`, `MessageCampaignScheduleService.schedule`, `MessageCampaignService.claimAndMaterializeOrThrow`, `MessageCampaignStatisticsService.scheduleRefresh`, `OutboundDeliveryEventHandlerService.handle`
- **(b) Subscribers / Consumers (9)**: `AddPeopleToMessageListJob.handle`, `MaterializeCampaignChunkJob.handle`, `MaterializeCampaignJob.handle`, `MessagingOutboundDeliveryEventJob.handle`, `ReconcileWorkspaceCampaignStatsJob.handle`, `RecoverWorkspaceStaleCampaignsJob.handle`, `RefreshCampaignStatsJob.handle`, `SendCampaignEmailJob.handle`, `SendScheduledCampaignJob.handle`

### 4. `ai-queue`
- **(a) Publishers / Producers (6)**: `AgentChatWaitingRunService.resolve`, `AgentCronTriggerCronJob.dispatchDueCronTriggers`, `AgentRunSuspensionService.deliverOutcome`, `AgentRunSuspensionService.scheduleContinuation`, `AgentRunnerService.start`, `CallAgentDatabaseEventTriggersJob.handle`
- **(b) Subscribers / Consumers (4)**: `ContinueAgentRunJob.handle`, `DeliverAgentRunOutcomeJob.handle`, `RunAgentJob.handle`, `RunAgentTriggerJob.handle`

### 5. `delete-cascade-queue`
- **(a) Publishers / Producers (5)**: `CalendarEventCleanerCalendarEventListener.handleDestroyedEvent`, `FilesFieldDeletionListener.handleDestroyedEvent`, `MessagingMessageCleanerMessageListener.handleDestroyedEvent`, `NavigationMenuItemDeletionListener.handleDeletedEvent`, `WorkspaceService.hardDeleteWorkspaceWithApplicationUninstallLock`
- **(b) Subscribers / Consumers (6)**: `EmailingDomainWorkspaceCleanupJob.handle`, `FileDeletionJob.handle`, `FileWorkspaceFolderDeletionJob.handle`, `FilesFieldDeletionJob.handle`, `LinkedTimelineActivityDeletionJob.handle`, `NavigationMenuItemDeletionJob.handle`

---

## Task 4 — End-to-End API-to-Database Write Lineage (`Layer 4–5`)

### 1. `billingSubscription` — **Distinct Endpoints: 10**
- `MUTATION:cancelSwitchBillingInterval (MUTATION) -> BillingResolver.cancelSwitchBillingInterval`
- `MUTATION:cancelSwitchBillingPlan (MUTATION) -> BillingResolver.cancelSwitchBillingPlan`
- `MUTATION:cancelSwitchResourceCreditPrice (MUTATION) -> BillingResolver.cancelSwitchResourceCreditPrice`
- `MUTATION:checkoutSession (MUTATION) -> BillingResolver.checkoutSession`
- `MUTATION:createSubscriptionPaymentIntent (MUTATION) -> BillingResolver.createSubscriptionPaymentIntent`
- `MUTATION:endSubscriptionTrialPeriod (MUTATION) -> BillingResolver.endSubscriptionTrialPeriod`
- `MUTATION:setResourceCreditSubscriptionPrice (MUTATION) -> BillingResolver.setResourceCreditSubscriptionPrice`
- `MUTATION:switchBillingPlan (MUTATION) -> BillingResolver.switchBillingPlan`
- `MUTATION:switchSubscriptionInterval (MUTATION) -> BillingResolver.switchSubscriptionInterval`
- `POST:/${ApiPath.Webhooks}/stripe (POST) -> BillingWebhookController.handleWebhooks`

### 2. `billingCustomer` — **Distinct Endpoints: 12**
- `MUTATION:cancelSwitchBillingInterval (MUTATION) -> BillingResolver.cancelSwitchBillingInterval`
- `MUTATION:cancelSwitchBillingPlan (MUTATION) -> BillingResolver.cancelSwitchBillingPlan`
- `MUTATION:cancelSwitchResourceCreditPrice (MUTATION) -> BillingResolver.cancelSwitchResourceCreditPrice`
- `MUTATION:checkoutSession (MUTATION) -> BillingResolver.checkoutSession`
- `MUTATION:createSubscriptionPaymentIntent (MUTATION) -> BillingResolver.createSubscriptionPaymentIntent`
- `MUTATION:endSubscriptionTrialPeriod (MUTATION) -> BillingResolver.endSubscriptionTrialPeriod`
- `MUTATION:setResourceCreditSubscriptionPrice (MUTATION) -> BillingResolver.setResourceCreditSubscriptionPrice`
- `MUTATION:signUpInNewWorkspace (MUTATION) -> AuthResolver.signUpInNewWorkspace`
- `MUTATION:switchBillingPlan (MUTATION) -> BillingResolver.switchBillingPlan`
- `MUTATION:switchSubscriptionInterval (MUTATION) -> BillingResolver.switchSubscriptionInterval`
- `MUTATION:updateUserCreditsFallback (MUTATION) -> BillingResolver.updateUserCreditsFallback`
- `POST:/${ApiPath.Webhooks}/stripe (POST) -> BillingWebhookController.handleWebhooks`

### 3. `connectedAccount` — **Distinct Endpoints: 13**
- `GET:/${ApiPath.Auth}/apps/callback (GET) -> ConnectionProviderOAuthController.callback`
- `GET:/${ApiPath.Auth}/apps/custom/callback (GET) -> ConnectionProviderCustomController.callback`
- `GET:/${ApiPath.Auth}/google-apis/get-access-token (GET) -> GoogleAPIsAuthController.googleAuthGetAccessToken`
- `GET:/${ApiPath.Auth}/google/redirect (GET) -> GoogleAuthController.googleAuthRedirect`
- `GET:/${ApiPath.Auth}/microsoft-apis/get-access-token (GET) -> MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken`
- `GET:/${ApiPath.Auth}/microsoft/redirect (GET) -> MicrosoftAuthController.microsoftAuthRedirect`
- `MUTATION:createEmailGroupChannel (MUTATION) -> MessageChannelResolver.createEmailGroupChannel`
- `MUTATION:deleteConnectedAccount (MUTATION) -> ConnectedAccountResolver.deleteConnectedAccount`
- `MUTATION:deleteEmailGroupChannel (MUTATION) -> MessageChannelResolver.deleteEmailGroupChannel`
- `MUTATION:disconnectConnectedAccount (MUTATION) -> ConnectedAccountResolver.disconnectConnectedAccount`
- `MUTATION:reportAppConnectionAuthFailure (MUTATION) -> ApplicationConnectionsResolver.reportAppConnectionAuthFailure`
- `MUTATION:saveImapSmtpCaldavAccount (MUTATION) -> ImapSmtpCaldavResolver.saveImapSmtpCaldavAccount`
- `POST:/${ApiPath.Auth}/apps/custom/callback (POST) -> ConnectionProviderCustomController.callbackNotification`

### 4. `messageChannel` — **Distinct Endpoints: 10**
- `GET:/${ApiPath.Auth}/google-apis/get-access-token (GET) -> GoogleAPIsAuthController.googleAuthGetAccessToken`
- `GET:/${ApiPath.Auth}/microsoft-apis/get-access-token (GET) -> MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken`
- `MUTATION:createAppMessageChannel (MUTATION) -> ApplicationMessageChannelsResolver.createAppMessageChannel`
- `MUTATION:createEmailGroupChannel (MUTATION) -> MessageChannelResolver.createEmailGroupChannel`
- `MUTATION:deleteAppMessageChannel (MUTATION) -> ApplicationMessageChannelsResolver.deleteAppMessageChannel`
- `MUTATION:disconnectConnectedAccount (MUTATION) -> ConnectedAccountResolver.disconnectConnectedAccount`
- `MUTATION:saveImapSmtpCaldavAccount (MUTATION) -> ImapSmtpCaldavResolver.saveImapSmtpCaldavAccount`
- `MUTATION:updateAppMessageChannel (MUTATION) -> ApplicationMessageChannelsResolver.updateAppMessageChannel`
- `MUTATION:updateEmailGroupChannel (MUTATION) -> MessageChannelResolver.updateEmailGroupChannel`
- `MUTATION:updateMessageChannel (MUTATION) -> MessageChannelResolver.updateMessageChannel`
