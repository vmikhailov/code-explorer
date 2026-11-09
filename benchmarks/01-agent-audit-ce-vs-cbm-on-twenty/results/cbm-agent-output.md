# Comprehensive 4-Part Architectural Audit — `twenty` Monorepo

---

## Task 1 — Service Method Call Graph & DI Fan-In (`Layer 3`)

> **Counting Note**: For each service below, both **external (cross-class DI) call pairs** and **all call pairs including intra-class (`this.*`) self-calls** are reported.

### 1. `BillingSubscriptionService`
- **`totalCallPairs`**: **`54`** total distinct `callerMethod -> calleeMethod` pairs (**`47`** cross-class DI call pairs across `19` external caller classes + **`7`** intra-class `this.*` call pairs within `BillingSubscriptionService`)
- **Callers grouped by `CallerClass`**:
  - **`AdminPanelBillingService`** (1): `getWorkspaceBilling -> getCurrentBillingSubscription`
  - **`BillingCreditService`** (1): `writeGrantAndRefreshState -> getCurrentBillingSubscription`
  - **`BillingPartnerCodeService`** (1): `isTrialingOrWithoutSubscription -> getCurrentBillingSubscription`
  - **`BillingPortalWorkspaceService`** (2): `createDirectSubscription -> syncSubscriptionToDatabase`, `createSubscriptionPaymentIntent -> syncSubscriptionToDatabase`
  - **`BillingResolver`** (15): `cancelSwitchBillingInterval -> getBillingSubscriptions`, `cancelSwitchBillingInterval -> getCurrentBillingSubscriptionOrThrow`, `cancelSwitchBillingPlan -> getBillingSubscriptions`, `cancelSwitchBillingPlan -> getCurrentBillingSubscriptionOrThrow`, `cancelSwitchResourceCreditPrice -> getBillingSubscriptions`, `cancelSwitchResourceCreditPrice -> getCurrentBillingSubscriptionOrThrow`, `endSubscriptionTrialPeriod -> endTrialPeriod`, `endSubscriptionTrialPeriod -> getBillingSubscriptions`, `endSubscriptionTrialPeriod -> getCurrentBillingSubscriptionOrThrow`, `setResourceCreditSubscriptionPrice -> getBillingSubscriptions`, `setResourceCreditSubscriptionPrice -> getCurrentBillingSubscriptionOrThrow`, `switchBillingPlan -> getBillingSubscriptions`, `switchBillingPlan -> getCurrentBillingSubscriptionOrThrow`, `switchSubscriptionInterval -> getBillingSubscriptions`, `switchSubscriptionInterval -> getCurrentBillingSubscriptionOrThrow`
  - **`BillingService`** (1): `hasEntitlement -> getWorkspaceEntitlementByKey`
  - **`BillingSubscriptionService`** *(intra-class self-calls, 7)*: `assertSubscriptionCanceledOrNone -> getCurrentBillingSubscription`, `cancelSubscription -> getCurrentBillingSubscription`, `endTrialPeriod -> getCurrentBillingSubscriptionOrThrow`, `endTrialPeriod -> syncSubscriptionToDatabase`, `getCurrentBillingSubscriptionOrThrow -> getCurrentBillingSubscription`, `getWorkspaceEntitlementValue -> getWorkspaceEntitlementByKey`, `handleUnpaidInvoices -> getCurrentBillingSubscription`
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

### 2. `ThrottlerService`
- **`totalCallPairs`**: **`24`** total distinct `callerMethod -> calleeMethod` pairs (**`22`** cross-class DI call pairs across `19` external caller classes + **`2`** intra-class `this.*` call pairs within `ThrottlerService`; `25` raw call-sites as `WorkspaceInvitationService.throttleInvitationSending` invokes `tokenBucketThrottleOrThrow` twice)
- **Callers grouped by `CallerClass`**:
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
  - **`ThrottlerService`** *(intra-class self-calls, 2)*: `tokenBucketConsumeUpTo -> tryConsumeTokenBuckets`, `tokenBucketThrottleOrThrow -> tryConsumeTokenBuckets`
  - **`TwoFactorAuthenticationRecoveryService`** (2): `generateRecoveryCode -> tokenBucketThrottleOrThrow`, `redeemRecoveryCode -> tokenBucketThrottleOrThrow`
  - **`TwoFactorAuthenticationService`** (1): `validateStrategy -> tokenBucketThrottleOrThrow`
  - **`UnsubscribeController`** (1): `throttleOrThrow -> tokenBucketThrottleOrThrow`
  - **`UsageLimitSpeedService`** (1): `consumeTokens -> tryConsumeTokenBuckets`
  - **`WorkflowThrottlingWorkspaceService`** (2): `consumeRemainingRunsToEnqueueCount -> tokenBucketConsumeUpTo`, `throttleOrThrowIfHardLimitReached -> tokenBucketThrottleOrThrow`
  - **`WorkspaceInvitationService`** (1): `throttleInvitationSending -> tokenBucketThrottleOrThrow`

### 3. `AccessTokenService`
- **`totalCallPairs`**: **`15`** total distinct `callerMethod -> calleeMethod` pairs (**`11`** cross-class DI call pairs across `8` external caller classes + **`4`** intra-class `this.*` call pairs within `AccessTokenService`)
- **Callers grouped by `CallerClass`**:
  - **`AccessTokenService`** *(intra-class self-calls, 4)*: `generateAccessToken -> resolveTokenSubject`, `generatePlaygroundToken -> resolveTokenSubject`, `validateTokenByRequest -> validateSessionToken`, `validateTokenByRequest -> validateToken`
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

### (a) Inherits from `SyncableEntity` (1 to 3 hops)
- **Exact Total Count**: **`43`** (`33` direct 1-hop subclasses + `10` transitive 2-hop subclasses via `OverridableEntity`; `0` at 3 hops)
- **Hop 1 (`33`)**: `AgentChatChannelEntity`, `AgentChatChannelRoleEntity`, `ApplicationVariableEntity`, `ConnectionProviderEntity`, `FieldMetadataEntity`, `FieldPermissionEntity`, `FrontComponentEntity`, `IndexMetadataEntity`, `LogicFunctionEntity`, `McpServerEntity`, `MessagingProviderEntity`, `NavigationMenuItemEntity`, `ObjectMetadataEntity`, `ObjectPermissionEntity`, `OverridableEntity`, `PageLayoutEntity`, `PermissionFlagEntity`, `RelatedRecordPathEntity`, `RoleEntity`, `RolePermissionFlagEntity`, `RoleTargetEntity`, `RowLevelPermissionPredicateEntity`, `RowLevelPermissionPredicateGroupEntity`, `SearchFieldMetadataEntity`, `SettingsMenuItemEntity`, `ValidationRuleEntity`, `ViewFilterEntity`, `ViewFilterGroupEntity`, `ViewGroupEntity`, `ViewSortEntity`, `WebhookEntity`, `WorkflowEntity`, `WorkflowVersionEntity`
- **Hop 2 (`10`, via `OverridableEntity`)**: `AgentEntity`, `CommandMenuItemEntity`, `FieldMetadataOptionEntity`, `PageLayoutTabEntity`, `PageLayoutWidgetEntity`, `SkillEntity`, `TimelineActivityTypeEntity`, `ViewEntity`, `ViewFieldEntity`, `ViewFieldGroupEntity`
- **Hop 3 (`0`)**: None

### (b) Inherits from `WorkspaceRelatedEntity` (1 to 3 hops)
- **Exact Total Count**: **`67`** across 1–3 hops (**`57`** across 1–2 hops: `24` direct 1-hop subclasses + `33` transitive 2-hop subclasses via `SyncableEntity` + `10` transitive 3-hop subclasses via `OverridableEntity`)
- **Hop 1 (`24`)**: `ApiKeyEntity`, `ApplicationEntity`, `ApprovedAccessDomainEntity`, `BillingCreditGrantEntity`, `BillingCustomerEntity`, `BillingEntitlementEntity`, `BillingSubscriptionEntity`, `CalendarChannelEntity`, `ConnectedAccountEntity`, `DataSourceEntity`, `DeferredWorkspaceMigrationActionEntity`, `EmailingDomainEntity`, `FeatureFlagEntity`, `LogicFunctionLayerEntity`, `MessageChannelEntity`, `MessageFolderEntity`, `PendingWakeUpEntity`, `PublicDomainEntity`, `SyncableEntity`, `UnsubscribeTopicEntity`, `UsageLimitEntity`, `UserApplicationVariableValueEntity`, `UserWorkspaceEntity`, `WorkspaceSsoIdentityProviderEntity`
- **Hop 2 (`33`, via `SyncableEntity`)**: `AgentChatChannelEntity`, `AgentChatChannelRoleEntity`, `ApplicationVariableEntity`, `ConnectionProviderEntity`, `FieldMetadataEntity`, `FieldPermissionEntity`, `FrontComponentEntity`, `IndexMetadataEntity`, `LogicFunctionEntity`, `McpServerEntity`, `MessagingProviderEntity`, `NavigationMenuItemEntity`, `ObjectMetadataEntity`, `ObjectPermissionEntity`, `OverridableEntity`, `PageLayoutEntity`, `PermissionFlagEntity`, `RelatedRecordPathEntity`, `RoleEntity`, `RolePermissionFlagEntity`, `RoleTargetEntity`, `RowLevelPermissionPredicateEntity`, `RowLevelPermissionPredicateGroupEntity`, `SearchFieldMetadataEntity`, `SettingsMenuItemEntity`, `ValidationRuleEntity`, `ViewFilterEntity`, `ViewFilterGroupEntity`, `ViewGroupEntity`, `ViewSortEntity`, `WebhookEntity`, `WorkflowEntity`, `WorkflowVersionEntity`
- **Hop 3 (`10`, via `OverridableEntity`)**: `AgentEntity`, `CommandMenuItemEntity`, `FieldMetadataOptionEntity`, `PageLayoutTabEntity`, `PageLayoutWidgetEntity`, `SkillEntity`, `TimelineActivityTypeEntity`, `ViewEntity`, `ViewFieldEntity`, `ViewFieldGroupEntity`

### (c) Inherits from `WorkspaceEntityMigrationBuilderService`
- **Exact Total Count**: **`42`** (all `42` are direct 1-hop subclasses; `0` at 2+ hops)
- **Hop 1 (`42`)**: `WorkspaceMigrationAgentActionsBuilderService`, `WorkspaceMigrationAgentChatChannelActionsBuilderService`, `WorkspaceMigrationAgentChatChannelRoleActionsBuilderService`, `WorkspaceMigrationApplicationVariableActionsBuilderService`, `WorkspaceMigrationCommandMenuItemActionsBuilderService`, `WorkspaceMigrationConnectionProviderActionsBuilderService`, `WorkspaceMigrationFieldActionsBuilderService`, `WorkspaceMigrationFieldMetadataOptionActionsBuilderService`, `WorkspaceMigrationFieldPermissionActionsBuilderService`, `WorkspaceMigrationFrontComponentActionsBuilderService`, `WorkspaceMigrationIndexActionsBuilderService`, `WorkspaceMigrationLogicFunctionActionsBuilderService`, `WorkspaceMigrationMcpServerActionsBuilderService`, `WorkspaceMigrationMessagingProviderActionsBuilderService`, `WorkspaceMigrationNavigationMenuItemActionsBuilderService`, `WorkspaceMigrationObjectActionsBuilderService`, `WorkspaceMigrationObjectPermissionActionsBuilderService`, `WorkspaceMigrationPageLayoutActionsBuilderService`, `WorkspaceMigrationPageLayoutTabActionsBuilderService`, `WorkspaceMigrationPageLayoutWidgetActionsBuilderService`, `WorkspaceMigrationPermissionFlagActionsBuilderService`, `WorkspaceMigrationRelatedRecordPathActionsBuilderService`, `WorkspaceMigrationRoleActionsBuilderService`, `WorkspaceMigrationRolePermissionFlagActionsBuilderService`, `WorkspaceMigrationRoleTargetActionsBuilderService`, `WorkspaceMigrationRowLevelPermissionPredicateActionsBuilderService`, `WorkspaceMigrationRowLevelPermissionPredicateGroupActionsBuilderService`, `WorkspaceMigrationSearchFieldMetadataActionsBuilderService`, `WorkspaceMigrationSettingsMenuItemActionsBuilderService`, `WorkspaceMigrationSkillActionsBuilderService`, `WorkspaceMigrationTimelineActivityTypeActionsBuilderService`, `WorkspaceMigrationValidationRuleActionsBuilderService`, `WorkspaceMigrationViewActionsBuilderService`, `WorkspaceMigrationViewFieldActionsBuilderService`, `WorkspaceMigrationViewFieldGroupActionsBuilderService`, `WorkspaceMigrationViewFilterActionsBuilderService`, `WorkspaceMigrationViewFilterGroupActionsBuilderService`, `WorkspaceMigrationViewGroupActionsBuilderService`, `WorkspaceMigrationViewSortActionsBuilderService`, `WorkspaceMigrationWebhookActionsBuilderService`, `WorkspaceMigrationWorkflowActionsBuilderService`, `WorkspaceMigrationWorkflowVersionActionsBuilderService`

### (d) Implements `WorkspaceResolverBuilderFactoryInterface`
- **Exact Total Count**: **`15`**
- **Classes (`15`)**: `CreateManyResolverFactory`, `CreateOneResolverFactory`, `DeleteManyResolverFactory`, `DeleteOneResolverFactory`, `DestroyManyResolverFactory`, `DestroyOneResolverFactory`, `FindDuplicatesResolverFactory`, `FindManyResolverFactory`, `FindOneResolverFactory`, `GroupByResolverFactory`, `MergeManyResolverFactory`, `RestoreManyResolverFactory`, `RestoreOneResolverFactory`, `UpdateManyResolverFactory`, `UpdateOneResolverFactory`

---

## Task 3 — Asynchronous BullMQ Queue Producers & Consumers (`Layer 4`)

### 1. `webhook-queue` (`MessageQueue.webhookQueue`)
- **(a) Publishes / Enqueues (`12`)**: `CallWebhookJobsForMetadataJob.handle`, `CallWebhookJobsJob.handle`, `ChannelSyncService.startCalendarChannelSync`, `ChannelSyncService.startMessageChannelSync`, `CreateWebhookSubscriptionForConnectedAccountCommand.enqueueChannels`, `EntityEventsToDbListener.enqueueWebhookJobsIfAnyWebhookMatches`, `GoogleAPIsService.enqueueWebhookSubscriptionCreation`, `MetadataEventsToDbListener.handleEvent`, `MicrosoftAPIsService.enqueueWebhookSubscriptionCreation`, `WebhookSubscriptionRenewalCronJob.enqueueRenewals`, `WebhookSubscriptionWorkspaceActivationListener.enqueueSync`, `WebhookSubscriptionWorkspaceSyncService.enqueue`
- **(b) Subscribes / Processes (`7`)**: `CallWebhookJob.handle`, `CallWebhookJobsForMetadataJob.handle`, `CallWebhookJobsJob.handle`, `CreateWebhookSubscriptionJob.handle`, `RenewWebhookSubscriptionJob.handle`, `RevokeWebhookSubscriptionJob.handle`, `SyncWorkspaceWebhookSubscriptionsJob.handle`

### 2. `workspace-queue` (`MessageQueue.workspaceQueue`)
- **(a) Publishes / Enqueues (`8` strict `.add()` producers; `10` total queue callers if including `2` read-only queue inspection methods)**:
  - **Enqueuers (`8`)**: `AgentChatChannelGrantService.enqueueChannelGrantsRewrite`, `ApplicationLifecycleJobService.enqueueLifecycleJob`, `BillingWebhookSubscriptionService.applySubscriptionEvent`, `DeferredWorkspaceMigrationActionRunnerService.enqueue`, `PreInstalledAppsService.enqueueInstallOnWorkspace`, `SdkClientGenerationService.enqueueSdkClientGenerationForApplication`, `TrashCleanupCronJob.handle`, `UserService.enqueueWorkspaceMemberEmailUpdate`
  - **Queue Status Readers (`2`, calling `getInFlightJobs`/`getJobs`)**: `ApplicationLifecycleJobService.findInFlightJobIds`, `ApplicationLifecycleJobService.findLifecycleJobStatus`
- **(b) Subscribes / Processes (`11`)**: `CleanWorkspaceDeletionWarningUserVarsJob.handle`, `GenerateSdkClientJob.handle`, `HandleWorkspaceMemberDeletedJob.handle`, `InstallPreInstalledAppsJob.handle`, `RewriteAgentChatChannelGrantsJob.handle`, `RunDeferredWorkspaceMigrationActionsJob.handle`, `TrashCleanupJob.handle`, `TriggerInstallApplicationJob.handle`, `TriggerUninstallApplicationJob.handle`, `TriggerUpgradeApplicationJob.handle`, `UpdateWorkspaceMemberEmailJob.handle`

### 3. `campaign-queue` (`MessageQueue.campaignQueue`)
- **(a) Publishes / Enqueues (`8`)**: `AddPeopleToMessageListJobService.enqueueAddPeopleToMessageListJob`, `CampaignSendingWorkspaceService.enqueueJobForEachSendingWorkspace`, `MessageCampaignDeliveryService.requeueRateLimitedSendJob`, `MessageCampaignMaterializationService.enqueueMaterializationChunks`, `MessageCampaignScheduleService.schedule`, `MessageCampaignService.claimAndMaterializeOrThrow`, `MessageCampaignStatisticsService.scheduleRefresh`, `OutboundDeliveryEventHandlerService.handle`
- **(b) Subscribes / Processes (`9`)**: `AddPeopleToMessageListJob.handle`, `MaterializeCampaignChunkJob.handle`, `MaterializeCampaignJob.handle`, `MessagingOutboundDeliveryEventJob.handle`, `ReconcileWorkspaceCampaignStatsJob.handle`, `RecoverWorkspaceStaleCampaignsJob.handle`, `RefreshCampaignStatsJob.handle`, `SendCampaignEmailJob.handle`, `SendScheduledCampaignJob.handle`

### 4. `ai-queue` (`MessageQueue.aiQueue`)
- **(a) Publishes / Enqueues (`6`)**: `AgentChatWaitingRunService.resolve`, `AgentCronTriggerCronJob.dispatchDueCronTriggers`, `AgentRunSuspensionService.deliverOutcome`, `AgentRunSuspensionService.scheduleContinuation`, `AgentRunnerService.start`, `CallAgentDatabaseEventTriggersJob.handle`
- **(b) Subscribes / Processes (`4`)**: `ContinueAgentRunJob.handle`, `DeliverAgentRunOutcomeJob.handle`, `RunAgentJob.handle`, `RunAgentTriggerJob.handle`

### 5. `delete-cascade-queue` (`MessageQueue.deleteCascadeQueue`)
- **(a) Publishes / Enqueues (`5`)**: `CalendarEventCleanerCalendarEventListener.handleDestroyedEvent`, `FilesFieldDeletionListener.handleDestroyedEvent`, `MessagingMessageCleanerMessageListener.handleDestroyedEvent`, `NavigationMenuItemDeletionListener.handleDeletedEvent`, `WorkspaceService.hardDeleteWorkspaceWithApplicationUninstallLock`
- **(b) Subscribes / Processes (`6`)**: `EmailingDomainWorkspaceCleanupJob.handle`, `FileDeletionJob.handle`, `FileWorkspaceFolderDeletionJob.handle`, `FilesFieldDeletionJob.handle`, `LinkedTimelineActivityDeletionJob.handle`, `NavigationMenuItemDeletionJob.handle`

---

## Task 4 — End-to-End API-to-Database Write Lineage (`Layer 4–5`, `0..3` Hops)

> **Route Prefix Note**: In `twenty`, REST controllers use `ApiPath.Webhooks = 'webhooks'` and `ApiPath.Auth = 'auth'`. Both the resolved path and template expression are shown where applicable.

### 1. Table: `billingSubscription` (`BillingSubscriptionEntity`)
- **Distinct Endpoint Count (`0..3` hops)**: **`10`** (`9` GraphQL `@Mutation` endpoints + `1` REST `POST` endpoint)
  - `cancelSwitchBillingInterval (Mutation) -> BillingResolver.cancelSwitchBillingInterval` *(3 hops: `-> BillingSubscriptionUpdateService.cancelSwitchInterval -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `cancelSwitchBillingPlan (Mutation) -> BillingResolver.cancelSwitchBillingPlan` *(3 hops: `-> BillingSubscriptionUpdateService.cancelSwitchPlan -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `cancelSwitchResourceCreditPrice (Mutation) -> BillingResolver.cancelSwitchResourceCreditPrice` *(3 hops: `-> BillingSubscriptionUpdateService.cancelSwitchResourceCreditPrice -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `checkoutSession (Mutation) -> BillingResolver.checkoutSession` *(2 hops: `-> BillingPortalWorkspaceService.createDirectSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `createSubscriptionPaymentIntent (Mutation) -> BillingResolver.createSubscriptionPaymentIntent` *(2 hops: `-> BillingPortalWorkspaceService.createSubscriptionPaymentIntent -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `endSubscriptionTrialPeriod (Mutation) -> BillingResolver.endSubscriptionTrialPeriod` *(2 hops: `-> BillingSubscriptionService.endTrialPeriod -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `setResourceCreditSubscriptionPrice (Mutation) -> BillingResolver.setResourceCreditSubscriptionPrice` *(3 hops: `-> BillingSubscriptionUpdateService.changeResourceCreditPrice -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `switchBillingPlan (Mutation) -> BillingResolver.switchBillingPlan` *(3 hops: `-> BillingSubscriptionUpdateService.changePlan -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `switchSubscriptionInterval (Mutation) -> BillingResolver.switchSubscriptionInterval` *(3 hops: `-> BillingSubscriptionUpdateService.changeInterval -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `/webhooks/stripe` [or `/${ApiPath.Webhooks}/stripe`] `(POST) -> BillingWebhookController.handleWebhooks` *(2 hops: `-> BillingWebhookController.handleStripeEvent -> BillingWebhookSubscriptionScheduleService.processStripeEvent`; also 3 hops via `BillingWebhookSubscriptionService.processStripeEvent -> BillingWebhookSubscriptionService.applySubscriptionEvent`)*

### 2. Table: `billingCustomer` (`BillingCustomerEntity`)
- **Distinct Endpoint Count (`0..3` hops)**: **`12`** (`11` GraphQL `@Mutation` endpoints + `1` REST `POST` endpoint)
  - `cancelSwitchBillingInterval (Mutation) -> BillingResolver.cancelSwitchBillingInterval` *(3 hops: `-> BillingSubscriptionUpdateService.cancelSwitchInterval -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `cancelSwitchBillingPlan (Mutation) -> BillingResolver.cancelSwitchBillingPlan` *(3 hops: `-> BillingSubscriptionUpdateService.cancelSwitchPlan -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `cancelSwitchResourceCreditPrice (Mutation) -> BillingResolver.cancelSwitchResourceCreditPrice` *(3 hops: `-> BillingSubscriptionUpdateService.cancelSwitchResourceCreditPrice -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `checkoutSession (Mutation) -> BillingResolver.checkoutSession` *(2 hops: `-> BillingPortalWorkspaceService.createDirectSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `createSubscriptionPaymentIntent (Mutation) -> BillingResolver.createSubscriptionPaymentIntent` *(2 hops: `-> BillingPortalWorkspaceService.createSubscriptionPaymentIntent -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `endSubscriptionTrialPeriod (Mutation) -> BillingResolver.endSubscriptionTrialPeriod` *(2 hops: `-> BillingSubscriptionService.endTrialPeriod -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `setResourceCreditSubscriptionPrice (Mutation) -> BillingResolver.setResourceCreditSubscriptionPrice` *(3 hops: `-> BillingSubscriptionUpdateService.changeResourceCreditPrice -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `signUpInNewWorkspace (Mutation) -> AuthResolver.signUpInNewWorkspace` *(3 hops: `-> SignInUpService.signUpOnNewWorkspace -> BillingService.ensureBillingCustomer -> StripeCustomerService.createStripeCustomer`)*
  - `switchBillingPlan (Mutation) -> BillingResolver.switchBillingPlan` *(3 hops: `-> BillingSubscriptionUpdateService.changePlan -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `switchSubscriptionInterval (Mutation) -> BillingResolver.switchSubscriptionInterval` *(3 hops: `-> BillingSubscriptionUpdateService.changeInterval -> BillingSubscriptionUpdateService.updateSubscription -> BillingSubscriptionService.syncSubscriptionToDatabase`)*
  - `updateUserCreditsFallback (Mutation) -> BillingResolver.updateUserCreditsFallback` *(1 hop: `-> BillingService.updateUserCreditsFallbackOrThrow`)*
  - `/webhooks/stripe` [or `/${ApiPath.Webhooks}/stripe`] `(POST) -> BillingWebhookController.handleWebhooks` *(3 hops: `-> BillingWebhookController.handleStripeEvent -> BillingWebhookCustomerService.processStripeEvent -> processCustomerCreated / processPaymentMethodAttachedEvent / processPaymentMethodDetachedEvent` and `BillingWebhookSubscriptionService.processStripeEvent -> applySubscriptionEvent`)*

### 3. Table: `connectedAccount` (`ConnectedAccountEntity`)
- **Distinct Endpoint Count (`0..3` hops)**: **`13`** (`6` GraphQL `@Mutation` endpoints + `7` REST endpoints)
  - `/auth/apps/callback` [or `/${ApiPath.Auth}/apps/callback`] `(GET) -> ConnectionProviderOAuthController.callback` *(2 hops: `-> ConnectionProviderOAuthFlowService.completeAuthorizationFlow -> ConnectionProviderConnectedAccountService.persist`)*
  - `/auth/apps/custom/callback` [or `/${ApiPath.Auth}/apps/custom/callback`] `(GET) -> ConnectionProviderCustomController.callback` *(2 hops: `-> ConnectionProviderCustomFlowService.completeAuthorizationFlow -> ConnectionProviderConnectedAccountService.persist`)*
  - `/auth/apps/custom/callback` [or `/${ApiPath.Auth}/apps/custom/callback`] `(POST) -> ConnectionProviderCustomController.callbackNotification` *(2 hops: `-> ConnectionProviderCustomFlowService.completeAuthorizationFlow -> ConnectionProviderConnectedAccountService.persist`)*
  - `/auth/google-apis/get-access-token` [or `/${ApiPath.Auth}/google-apis/get-access-token`] `(GET) -> GoogleAPIsAuthController.googleAuthGetAccessToken` *(2 hops: `-> GoogleAPIsService.refreshGoogleRefreshToken -> CreateConnectedAccountService.createConnectedAccount / UpdateConnectedAccountOnReconnectService.updateConnectedAccountOnReconnect / EmailAliasManagerService.refreshHandleAliases / MessageChannelSyncStatusService.markAsFailed / CalendarChannelSyncStatusService.markAsFailedInsufficientPermissionsAndFlushCalendarEventsToImport`)*
  - `/auth/google/redirect` [or `/${ApiPath.Auth}/google/redirect`] `(GET) -> GoogleAuthController.googleAuthRedirect` *(3 hops: `-> AuthService.signInUpWithSocialSso -> AuthService.createSsoConnectedAccountIfFeatureFlagIsOn -> CreateSsoConnectedAccountService.createOrUpdateSsoConnectedAccount`)*
  - `/auth/microsoft-apis/get-access-token` [or `/${ApiPath.Auth}/microsoft-apis/get-access-token`] `(GET) -> MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken` *(2 hops: `-> MicrosoftAPIsService.refreshMicrosoftRefreshToken -> CreateConnectedAccountService.createConnectedAccount / UpdateConnectedAccountOnReconnectService.updateConnectedAccountOnReconnect / EmailAliasManagerService.refreshHandleAliases`)*
  - `/auth/microsoft/redirect` [or `/${ApiPath.Auth}/microsoft/redirect`] `(GET) -> MicrosoftAuthController.microsoftAuthRedirect` *(3 hops: `-> AuthService.signInUpWithSocialSso -> AuthService.createSsoConnectedAccountIfFeatureFlagIsOn -> CreateSsoConnectedAccountService.createOrUpdateSsoConnectedAccount`)*
  - `createEmailGroupChannel (Mutation) -> MessageChannelResolver.createEmailGroupChannel` *(2 hops: `-> MessageChannelMetadataService.createEmailGroupChannel -> ConnectedAccountMetadataService.create`)*
  - `deleteConnectedAccount (Mutation) -> ConnectedAccountResolver.deleteConnectedAccount` *(2 hops: `-> ConnectedAccountMetadataService.delete -> ConnectedAccountMetadataService.deleteMany`)*
  - `deleteEmailGroupChannel (Mutation) -> MessageChannelResolver.deleteEmailGroupChannel` *(3 hops: `-> MessageChannelMetadataService.deleteEmailGroupChannel -> ConnectedAccountMetadataService.delete -> ConnectedAccountMetadataService.deleteMany`)*
  - `disconnectConnectedAccount (Mutation) -> ConnectedAccountResolver.disconnectConnectedAccount` *(1 hop: `-> ConnectedAccountMetadataService.disconnect`)*
  - `reportAppConnectionAuthFailure (Mutation) -> ApplicationConnectionsResolver.reportAppConnectionAuthFailure` *(1 hop: `-> ApplicationConnectionAuthFailureService.reportAuthFailureOrThrow`)*
  - `saveImapSmtpCaldavAccount (Mutation) -> ImapSmtpCaldavResolver.saveImapSmtpCaldavAccount` *(1 hop: `-> ImapSmtpCalDavApiService.upsertConnectedAccount`)*
  - *(Note at 4 hops: `/auth/oidc/callback (GET) -> SsoAuthController.oidcAuthCallback`, `/auth/saml/callback/:identityProviderId (POST) -> SsoAuthController.samlAuthCallback`, `deleteUser (Mutation) -> UserResolver.deleteUser`, and `deleteUserFromWorkspace (Mutation) -> UserResolver.deleteUserFromWorkspace` reach `connectedAccount` in 4 hops.)*

### 4. Table: `messageChannel` (`MessageChannelEntity`)
- **Distinct Endpoint Count (`0..3` hops)**: **`10`** (`8` GraphQL `@Mutation` endpoints + `2` REST `GET` endpoints)
  - `/auth/google-apis/get-access-token` [or `/${ApiPath.Auth}/google-apis/get-access-token`] `(GET) -> GoogleAPIsAuthController.googleAuthGetAccessToken` *(1 hop: `-> GoogleAPIsService.refreshGoogleRefreshToken`; 2 hops: `-> CreateMessageChannelService.createMessageChannel` & `MessageChannelSyncStatusService.resetAndMarkAsMessagesListFetchPending`; 3 hops: `-> OnboardingRecentMessagesImportService.importRecentMessages -> MessagingMessagesImportService.processMessageBatchImport`)*
  - `/auth/microsoft-apis/get-access-token` [or `/${ApiPath.Auth}/microsoft-apis/get-access-token`] `(GET) -> MicrosoftAPIsAuthController.MicrosoftAuthGetAccessToken` *(1 hop: `-> MicrosoftAPIsService.refreshMicrosoftRefreshToken`; 2 hops: `-> CreateMessageChannelService.createMessageChannel` & `MessageChannelSyncStatusService.resetAndMarkAsMessagesListFetchPending`; 3 hops: `-> OnboardingRecentMessagesImportService.importRecentMessages -> MessagingMessagesImportService.processMessageBatchImport`)*
  - `createAppMessageChannel (Mutation) -> ApplicationMessageChannelsResolver.createAppMessageChannel` *(1 hop: `-> ApplicationMessageChannelsService.create`)*
  - `createEmailGroupChannel (Mutation) -> MessageChannelResolver.createEmailGroupChannel` *(2 hops: `-> MessageChannelMetadataService.createEmailGroupChannel -> MessageChannelMetadataService.create`)*
  - `deleteAppMessageChannel (Mutation) -> ApplicationMessageChannelsResolver.deleteAppMessageChannel` *(1 hop: `-> ApplicationMessageChannelsService.delete`)*
  - `disconnectConnectedAccount (Mutation) -> ConnectedAccountResolver.disconnectConnectedAccount` *(1 hop: `-> ConnectedAccountMetadataService.disconnect`)*
  - `saveImapSmtpCaldavAccount (Mutation) -> ImapSmtpCaldavResolver.saveImapSmtpCaldavAccount` *(1 hop: `-> ImapSmtpCalDavApiService.upsertConnectedAccount`; 2 hops: `-> CreateMessageChannelService.createMessageChannel` & `MessageChannelSyncStatusService.resetAndMarkAsMessagesListFetchPending`)*
  - `updateAppMessageChannel (Mutation) -> ApplicationMessageChannelsResolver.updateAppMessageChannel` *(1 hop: `-> ApplicationMessageChannelsService.update`)*
  - `updateEmailGroupChannel (Mutation) -> MessageChannelResolver.updateEmailGroupChannel` *(2 hops: `-> MessageChannelMetadataService.updateEmailGroupChannel -> MessageChannelMetadataService.update`)*
  - `updateMessageChannel (Mutation) -> MessageChannelResolver.updateMessageChannel` *(1 hop: `-> MessageChannelMetadataService.update` & `MessagingProcessGroupEmailActionsService.markMessageChannelAsPendingGroupEmailsAction`)*
  - *(Note: `deleteEmailGroupChannel (Mutation) -> MessageChannelResolver.deleteEmailGroupChannel` deletes the parent `connectedAccount` row via `ConnectedAccountMetadataService.delete`, which cascades at the DB FK level; `deleteUser` and `deleteUserFromWorkspace` reach `messageChannel` via `ConnectedAccountMetadataService.transferOwnership` in 4 hops.)*
