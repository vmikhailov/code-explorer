# Architectural Governance, Security, and Blast-Radius Audit Report: OrchardCore

## Executive Summary
This report presents the findings of a 4-part architectural governance, AppSec authorization, reactive pipeline fan-out, and breaking-change blast radius audit conducted across the `OrchardCMS/OrchardCore` codebase (`src/OrchardCore/*` and `src/OrchardCore.Modules/*`, excluding `test/` projects, `src/Templates/`, and test files).

---

## Task 1: AppSec Authorization Audit — State-Mutating Controller Actions

Audited all public action methods on MVC/API controllers decorated with `[HttpPost]`, `[HttpPut]`, or `[HttpDelete]` (excluding `[AllowAnonymous]`) across 5 security-sensitive modules.

- **Total (1a) Authorized Actions**: **67**
- **Total (1b) Actions Missing `AuthorizeAsync` within 1–2 Hops**: **21**

### 1. OrchardCore.Roles
- **(1a) Authorized Actions (4)**:
  - `AdminController.Create` (`AuthorizeAsync(User, RolesPermissions.ManageRoles)`)
  - `AdminController.EditPost` (`AuthorizeAsync(User, RolesPermissions.ManageRoles)`)
  - `AdminController.Delete` (`AuthorizeAsync(User, RolesPermissions.ManageRoles)`)
  - `AdminController.ClonePost` (`AuthorizeAsync(User, RolesPermissions.ManageRoles)`)
- **(1b) Missing `AuthorizeAsync` (0)**: None

### 2. OrchardCore.ContentTypes
- **(1a) Authorized Actions (13)**:
  - `AdminController.CreatePOST` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.EditPost` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.Delete` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.AddPartsToPOST` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.AddReusablePartToPOST` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.RemovePart` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.CreatePartPOST` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.EditPartPOST` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.DeletePart` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.AddFieldToPOST` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.EditFieldPOST` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.RemoveFieldFromPOST` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
  - `AdminController.EditTypePartPOST` (`AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)`)
- **(1b) Missing `AuthorizeAsync` (0)**: None

### 3. OrchardCore.Tenants
- **(1a) Authorized Actions (17)**:
  - `AdminController.Index` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `AdminController.Create` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `AdminController.Edit` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `AdminController.Disable` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `AdminController.Enable` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `AdminController.Reload` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `AdminController.Remove` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `TenantApiController.Create` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `TenantApiController.Edit` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `TenantApiController.Disable` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `TenantApiController.Enable` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `TenantApiController.Remove` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `TenantApiController.Setup` (`AuthorizeAsync(User, Permissions.ManageTenants)`)
  - `FeatureProfilesController.CreatePost` (`ProcessSaveAsync` -> `AuthorizeAsync`)
  - `FeatureProfilesController.EditPost` (`ProcessSaveAsync` -> `AuthorizeAsync`)
  - `FeatureProfilesController.Delete` (`AuthorizeAsync(User, Permissions.ManageTenantFeatureProfiles)`)
  - `FeatureProfilesController.IndexPost` (`AuthorizeAsync(User, Permissions.ManageTenantFeatureProfiles)`)
- **(1b) Missing `AuthorizeAsync` (2)**:
  - `AdminController.IndexFilterPOST` (Controller decorated with `[Admin]`)
  - `FeatureProfilesController.IndexFilterPOST` (Controller decorated with `[Admin]`)

### 4. OrchardCore.Workflows
- **(1a) Authorized Actions (21)**:
  - `WorkflowTypeController.BulkEdit` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
  - `WorkflowTypeController.Export` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
  - `WorkflowTypeController.Create` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
  - `WorkflowTypeController.Clone` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
  - `WorkflowTypeController.Delete` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
  - `WorkflowDesignerController.Run` (`AuthorizeAsync(User, WorkflowsPermissions.ExecuteWorkflows)`)
  - `WorkflowDesignerController.Retry` (`AuthorizeAsync(User, WorkflowsPermissions.ExecuteWorkflows)`)
  - `WorkflowDesignerController.Save` (`CanManageAsync` -> `AuthorizeAsync`)
  - `WorkflowDesignerController.AddActivity` (`CanManageAsync` -> `AuthorizeAsync`)
  - `WorkflowDesignerController.EditorPost` (`CanManageAsync` -> `AuthorizeAsync`)
  - `WorkflowDesignerController.Variables` (`CanManageAsync` -> `AuthorizeAsync`)
  - `WorkflowDesignerController.OutputBindings` (`CanManageAsync` -> `AuthorizeAsync`)
  - `WorkflowDesignerController.SettingsPost` (`CanManageAsync` -> `AuthorizeAsync`)
  - `WorkflowDesignerController.Publish` (`CanManageAsync` -> `AuthorizeAsync`)
  - `WorkflowDesignerController.Restore` (`CanManageAsync` -> `AuthorizeAsync`)
  - `WorkflowDesignerController.Discard` (`CanManageAsync` -> `AuthorizeAsync`)
  - `WorkflowController.Delete` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
  - `WorkflowController.Restart` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
  - `WorkflowController.BulkEdit` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
  - `WorkflowController.AllBulkEdit` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
  - `HttpWorkflowController.GenerateUrl` (`AuthorizeAsync(User, WorkflowsPermissions.ManageWorkflows)`)
- **(1b) Missing `AuthorizeAsync` (4)**:
  - `WorkflowTypeController.IndexFilterPOST` (Controller decorated with `[Admin]`)
  - `WorkflowController.IndexFilterPOST` (Controller decorated with `[Admin]`)
  - `WorkflowController.AllFilterPOST` (Controller decorated with `[Admin]`)
  - `HttpWorkflowController.Invoke` (No class-level authorization attributes; secured via SAS token decrypt validation)

### 5. OrchardCore.Users
- **(1a) Authorized Actions (12)**:
  - `AdminController.IndexPOST` (`AuthorizeAsync(User, UsersPermissions.ListUsers)` & `EditUsers`)
  - `AdminController.CreatePost` (`AuthorizeAsync(User, UsersPermissions.EditUsers)`)
  - `AdminController.EditPost` (`AuthorizeAsync(User, UsersPermissions.EditUsers)` / `EditOwnUser`)
  - `AdminController.ConfirmEmail` (`AuthorizeAsync(User, UsersPermissions.EditUsers)`)
  - `AdminController.Delete` (`AuthorizeAsync(User, UsersPermissions.DeleteUsers)`)
  - `AdminController.EditPassword` (`AuthorizeAsync(User, UsersPermissions.EditUsers)`)
  - `AdminController.Enable` (`AuthorizeAsync(User, UsersPermissions.EditUsers)`)
  - `AdminController.Disable` (`AuthorizeAsync(User, UsersPermissions.EditUsers)`)
  - `AdminController.Unlock` (`AuthorizeAsync(User, UsersPermissions.EditUsers)`)
  - `AdminTwoFactorAuthenticationController.Disable` (`AuthorizeAsync(User, UsersPermissions.DisableTwoFactorAuthenticationForUsers)`)
  - `AuditTrailAdminController.IndexFilterPOST` (`AuthorizeAsync(User, Permissions.ManageUserAuditTrailSettings)`)
  - `EmailConfirmationController.SendVerificationEmail` (`AuthorizeAsync(User, UsersPermissions.ManageUsers)`)
- **(1b) Missing `AuthorizeAsync` (15)**:
  - `AdminController.IndexFilterPOST` (Controller decorated with `[Admin]`)
  - `ChangeEmailController.Index` (Controller decorated with `[Authorize]`)
  - `TwoFactorAuthenticationController.Index` (Controller decorated with `[Authorize]`)
  - `TwoFactorAuthenticationController.ForgetTwoFactorClient` (Controller decorated with `[Authorize]`)
  - `TwoFactorAuthenticationController.GenerateRecoveryCodesPost` (Controller decorated with `[Authorize]`)
  - `TwoFactorAuthenticationController.EnableTwoFactorAuthentication` (Controller decorated with `[Authorize]`)
  - `TwoFactorAuthenticationController.DisableTwoFactorAuthenticationPost` (Controller decorated with `[Authorize]`)
  - `AuthenticatorAppController.Index` (Controller decorated with `[Authorize]`)
  - `AuthenticatorAppController.ResetPost` (Controller decorated with `[Authorize]`)
  - `SmsAuthenticatorController.IndexPost` (Controller decorated with `[Authorize]`)
  - `SmsAuthenticatorController.ValidateCode` (Controller decorated with `[Authorize]`)
  - `ExternalAuthenticationsController.LinkLogin` (No class-level authorization attribute; challenges external provider)
  - `ExternalAuthenticationsController.RemoveLogin` (No class-level authorization attribute; acts on authenticated user from HttpContext)
  - `AccountController.LogOff` (Controller decorated with `[Authorize]`)
  - `AccountController.ChangePassword` (Controller decorated with `[Authorize]`)

---

## Task 2: Architectural Boundary Violations — Direct `YesSql.ISession` Access in Web & UI Layers

Identified all non-test **Controllers** and **Display Drivers** across `src/OrchardCore.Modules/*` directly invoking operations on an injected `YesSql.ISession` member.

- **Total Violating Modules**: **13**
- **Total Violating Classes**: **22**
- **Total Method-to-Session Operation Pairs**: **72**

### 1. OrchardCore.AdminDashboard (1 class, 1 pair)
- `DashboardController.Update -> ISession.SaveAsync`

### 2. OrchardCore.Autoroute (1 class, 1 pair)
- `AutoroutePartDisplayDriver.UpdateAsync -> ISession.QueryIndex`

### 3. OrchardCore.ContentFields (2 classes, 3 pairs)
- `LocalizationSetContentPickerAdminController.SearchLocalizationSets -> ISession.Query`
- `UserPickerFieldDisplayDriver.Edit -> ISession.Query`
- `UserPickerFieldDisplayDriver.UpdateAsync -> ISession.Query`

### 4. OrchardCore.Contents (4 classes, 13 pairs)
- `AuditTrailContentController.Display -> ISession.Query`
- `AuditTrailContentController.Restore -> ISession.Query`
- `AuditTrailContentEventDisplayDriver.DisplayAsync -> ISession.QueryIndex`
- `AuditTrailContentEventDisplayDriver.DisplayAsync -> ISession.Query`
- `AdminController.ListPOST -> ISession.Query`
- `AdminController.ListPOST -> ISession.CancelAsync`
- `AdminController.CreateInternalAsync -> ISession.CancelAsync`
- `AdminController.EditInternalAsync -> ISession.CancelAsync`
- `AddToDeploymentPlanController.AddContentItem -> ISession.GetAsync`
- `AddToDeploymentPlanController.AddContentItem -> ISession.SaveAsync`
- `AddToDeploymentPlanController.AddContentItems -> ISession.GetAsync`
- `AddToDeploymentPlanController.AddContentItems -> ISession.Query`
- `AddToDeploymentPlanController.AddContentItems -> ISession.SaveAsync`

### 5. OrchardCore.Demo (2 classes, 8 pairs)
- `ContentController.EditPost -> ISession.CancelAsync`
- `ContentController.EditPost -> ISession.SaveAsync`
- `TodoController.Index -> ISession.Query`
- `TodoController.Edit -> ISession.Query`
- `TodoController.Update -> ISession.Query`
- `TodoController.Update -> ISession.SaveAsync`
- `TodoController.Delete -> ISession.Query`
- `TodoController.Delete -> ISession.Delete`

### 6. OrchardCore.Deployment (3 classes, 20 pairs)
- `DeploymentPlanController.Index -> ISession.Query`
- `DeploymentPlanController.IndexBulkActionPOST -> ISession.Query`
- `DeploymentPlanController.IndexBulkActionPOST -> ISession.Delete`
- `DeploymentPlanController.Display -> ISession.GetAsync`
- `DeploymentPlanController.Create -> ISession.QueryIndex`
- `DeploymentPlanController.Create -> ISession.SaveAsync`
- `DeploymentPlanController.Edit -> ISession.GetAsync`
- `DeploymentPlanController.Edit -> ISession.QueryIndex`
- `DeploymentPlanController.Edit -> ISession.SaveAsync`
- `DeploymentPlanController.Delete -> ISession.GetAsync`
- `DeploymentPlanController.Delete -> ISession.Delete`
- `ExportFileController.Execute -> ISession.GetAsync`
- `StepController.Create -> ISession.GetAsync`
- `StepController.Create -> ISession.SaveAsync`
- `StepController.Edit -> ISession.GetAsync`
- `StepController.Edit -> ISession.SaveAsync`
- `StepController.Delete -> ISession.GetAsync`
- `StepController.Delete -> ISession.SaveAsync`
- `StepController.UpdateOrder -> ISession.GetAsync`
- `StepController.UpdateOrder -> ISession.SaveAsync`

### 7. OrchardCore.Deployment.Remote (1 class, 1 pair)
- `ExportRemoteInstanceController.Execute -> ISession.GetAsync`

### 8. OrchardCore.Layers (1 class, 1 pair)
- `AdminController.UpdatePosition -> ISession.SaveAsync`

### 9. OrchardCore.Notifications (2 classes, 4 pairs)
- `AdminController.ListPOST -> ISession.Query`
- `NotificationNavbarDisplayDriver.DisplayAsync -> ISession.Query`
- `NotificationNavbarDisplayDriver.DisplayAsync -> ISession.QueryIndex`
- `NotificationNavbarDisplayDriver.PopulateVisibleAsync -> ISession.Query`

### 10. OrchardCore.Search (1 class, 1 pair)
- `SearchController.Search -> ISession.Query`

### 11. OrchardCore.Taxonomies (3 classes, 6 pairs)
- `AdminController.CreatePost -> ISession.SaveAsync`
- `AdminController.EditPost -> ISession.SaveAsync`
- `AdminController.Delete -> ISession.SaveAsync`
- `TagController.CreatePost -> ISession.CancelAsync`
- `TagController.CreatePost -> ISession.SaveAsync`
- `TaxonomyContentsAdminListSettingsDisplayDriver.EditAsync -> ISession.Query`

### 12. OrchardCore.Users (1 class, 3 pairs)
- `AdminController.IndexPOST -> ISession.Query`
- `AdminController.Delete -> ISession.CancelAsync`
- `AdminController.Unlock -> ISession.CancelAsync`

### 13. OrchardCore.Workflows (3 classes, 10 pairs)
- `WorkflowController.BuildListAsync -> ISession.QueryIndex`
- `WorkflowController.BuildListAsync -> ISession.Query`
- `WorkflowDesignerController.Definition -> ISession.QueryIndex`
- `WorkflowDesignerController.LatestInstance -> ISession.Query`
- `WorkflowDesignerController.Instance -> ISession.QueryIndex`
- `WorkflowDesignerController.CountInstancesByVersionAsync -> ISession.CreateConnectionAsync`
- `WorkflowTypeController.Index -> ISession.Query`
- `WorkflowTypeController.Index -> ISession.CreateConnectionAsync`
- `WorkflowTypeController.BulkEdit -> ISession.Query`
- `WorkflowTypeController.Clone -> ISession.GetAsync`

---

## Task 3: Cross-Module Reactive Pipeline — Publishing Event Fan-Out

Identified all non-test classes implementing or overriding `PublishingAsync` or `PublishedAsync` and mapped their 1–2 hop downstream reactive side effects.

- **Total Handler Methods**: **16** (across 15 classes in 9 modules)

### 1. OrchardCore.ContentManagement
- `UpdateContentsHandler.PublishingAsync`:
  - **Database / Content Mutation**: In-memory `ContentItem.PublishedUtc` and `ContentItem.CreatedUtc` timestamp updates.
- `ContentPartHandlerCoordinator.PublishingAsync`:
  - **Database / Pipeline Dispatch**: Dispatches to `IContentPartHandler.PublishingAsync` and `IContentFieldHandler.PublishingAsync`.
- `ContentPartHandlerCoordinator.PublishedAsync`:
  - **Database / Pipeline Dispatch**: Dispatches to `IContentPartHandler.PublishedAsync` and `IContentFieldHandler.PublishedAsync`.

### 2. OrchardCore.Autoroute.Core
- `AutoroutePartIndexProvider.PublishedAsync`:
  - **Database / Store Mutation**: Invokes `IContentDefinitionManager.GetTypeDefinitionAsync` and triggers `IAutorouteEntries.UpdateEntriesAsync`.

### 3. OrchardCore.Indexing.Core
- `IndexingContentHandler.PublishedAsync`:
  - **Workflow / Deferred Task**: Enqueues deferred indexing via `ShellScope.AddDeferredTask`.
  - **Database / Index Store Mutation**: Triggers `IIndexProfileStore.GetByTypeAsync` and `IContentManager.GetAsync` / indexing in background scope.

### 4. OrchardCore.Alias
- `AliasPartHandler.PublishedAsync`:
  - **Cache Invalidation**: Invokes `ITagCache.RemoveTagAsync` (`alias:{alias}`).

### 5. OrchardCore.Autoroute
- `AutoroutePartHandler.PublishedAsync`:
  - **Database / Store Mutation**: Calls `IContentManager.PopulateAspectAsync`, `IAutorouteEntries.UpdateEntriesAsync`, and `ISiteService.LoadSiteSettingsAsync` / `UpdateSiteSettingsAsync`.
  - **Cache Invalidation**: Calls `ITagCache.RemoveTagAsync` via `RemoveTagAsync(part)`.

### 6. OrchardCore.ContentLocalization
- `LocalizedContentItemIndexProvider.PublishedAsync`:
  - **Database / Definition Mutation**: Calls `IContentDefinitionManager.GetTypeDefinitionAsync` and strips removed `LocalizationPart`.
- `LocalizationPartHandler.PublishedAsync`:
  - **Database / Store Mutation**: Invokes `ILocalizationEntries.UpdateEntriesAsync`.

### 7. OrchardCore.Contents
- `AuditTrailContentHandler.PublishedAsync`:
  - **Database / Audit Store Mutation**: Queries `ISession.QueryIndex<ContentItemIndex>`, calls `ISiteService.GetSettingsAsync`, and records audit event via `IAuditTrailManager.RecordEventAsync`.
- `ContentsHandler.PublishedAsync` (Cache):
  - **Cache Invalidation**: Invokes `ITagCache.RemoveTagAsync` (`contentitemid:{id}`).
- `ContentTypesSitemapUpdateHandler.PublishedAsync`:
  - **Database / Store Mutation**: Triggers `ISitemapUpdateHandler.UpdateSitemapAsync`.
- `ContentsHandler.PublishedAsync` (Workflows):
  - **Workflow Trigger**: Fires `IWorkflowManager.TriggerEventAsync` (`ContentPublishedEvent`).

### 8. OrchardCore.Title
- `TitlePartHandler.PublishingAsync`:
  - **Database / Definition Mutation**: Calls `IContentDefinitionManager.GetTypeDefinitionAsync` and updates `ContentItem.DisplayText`.
  - **Template / Scripting Trigger**: Executes liquid rendering via `ILiquidTemplateManager.RenderStringAsync`.

### 9. OrchardCore.Layers
- `LayerMetadataHandler.PublishedAsync`:
  - **Database / Store Mutation**: Triggers volatile state update via `IVolatileDocumentManager<LayerState>.UpdateAsync`.

### 10. OrchardCore.Indexing
- `CreateIndexingTaskContentHandler.PublishedAsync`:
  - **Database / Task Store Mutation**: Records indexing task via `IIndexingTaskManager.CreateTaskAsync`.

---

## Task 4: Breaking-Change Blast Radius & Unused Project Dependencies

Contract methods audited: `IContentManager.GetAsync`, `IContentManager.CreateAsync`, `IContentManager.PublishAsync`.

- **(4a) Direct Call Blast Radius Projects**: **20**
- **(4b) Over-Coupled Projects (`DEPENDS_ON` with 0 calls)**: **37** (out of 48 projects declaring direct dependency)

### (4a) Direct Call Blast Radius per Project

1. **OrchardCore.AdminDashboard** (1 distinct caller):
   - `GetAsync` (1): `DashboardController.Update`
   - `CreateAsync` (0)
   - `PublishAsync` (0)
2. **OrchardCore.Alias** (1 distinct caller):
   - `GetAsync` (1): `AliasPartRazorHelperExtensions.GetContentItemByAliasAsync`
   - `CreateAsync` (0)
   - `PublishAsync` (0)
3. **OrchardCore.ArchiveLater** (1 distinct caller):
   - `GetAsync` (1): `ScheduledArchivingBackgroundTask.DoWorkAsync`
   - `CreateAsync` (0)
   - `PublishAsync` (0)
4. **OrchardCore.Autoroute** (2 distinct callers):
   - `GetAsync` (2): `Startup.ConfigureServices`, `AutoroutePartRazorHelperExtensions.GetContentItemBySlugAsync`
   - `CreateAsync` (0)
   - `PublishAsync` (0)
5. **OrchardCore.ContentFields** (3 distinct callers):
   - `GetAsync` (3): `ContentPickerAdminController.SearchContentItems`, `ContentPickerFieldDisplayDriver.Edit`, `LocalizationSetContentPickerFieldDisplayDriver.Edit`
   - `CreateAsync` (0)
   - `PublishAsync` (0)
6. **OrchardCore.ContentLocalization** (1 distinct caller):
   - `GetAsync` (1): `AdminController.Localize`
   - `CreateAsync` (0)
   - `PublishAsync` (0)
7. **OrchardCore.ContentManagement.Abstractions** (3 distinct callers):
   - `GetAsync` (2): `ContentManagerExtensions.HasPublishedVersionAsync`, `ContentManagerExtensions.GetAsync`
   - `CreateAsync` (1): `ContentManagerExtensions.UpdateValidateAndCreateAsync`
   - `PublishAsync` (0)
8. **OrchardCore.ContentManagement** (3 distinct callers):
   - `GetAsync` (2): `DefaultContentManager.PublishAsync`, `DefaultContentManager.CloneAsync`
   - `CreateAsync` (2): `DefaultContentManager.ImportAsync`, `DefaultContentManager.CloneAsync`
   - `PublishAsync` (0)
9. **OrchardCore.ContentManagement.GraphQL** (1 distinct caller):
   - `GetAsync` (1): `ContentItemQuery.ResolveAsync`
   - `CreateAsync` (0)
   - `PublishAsync` (0)
10. **OrchardCore.Contents** (36 distinct callers):
    - `GetAsync` (32): `AdminController.Clone`, `AdminController.Delete`, `AdminController.DiscardDraft`, `AdminController.Display`, `AdminController.Edit`, `AdminController.Publish`, `AdminController.PublishOrUnpublishAsync`, `AdminController.Remove`, `AdminController.Unpublish`, `AddToDeploymentPlanController.AddContentItem`, `ContentActivity.OnWorkflowRestartingAsync`, `ContentItemDeploymentSource.ProcessAsync`, `ContentItemDeploymentStepDriver.UpdateAsync`, `ContentItemFilter.ProcessAsync`, `ContentItemSerializer.DeserializeValueAsync`, `CreateEndpoint.AddCreateContentEndpoint`, `DeleteEndpoint.AddDeleteContentEndpoint`, `DeleteContentTask.ExecuteAsync`, `DisplayContentItemViewComponent.InvokeAsync`, `DownloadController.Display`, `DownloadController.Download`, `ExportContentToDeploymentTargetDeploymentSource.ProcessAsync`, `GetEndpoint.AddGetContentEndpoint`, `ItemController.Display`, `ItemController.Preview`, `PublishContentTask.ExecuteAsync`, `RetrieveContentTask.ExecuteAsync`, `Shapes.AddShapeTableAsync`, `Startup.ConfigureServices`, `Startup.GetContentByHandleAsync`, `UnpublishContentTask.ExecuteAsync`, `UpdateContentTask.ExecuteAsync`
    - `CreateAsync` (4): `AdminController.CreateInternalAsync`, `ContentMethodsProvider.CreateContentItemAsync`, `CreateContentTask.ExecuteAsync`, `CreateEndpoint.AddCreateContentEndpoint`
    - `PublishAsync` (7): `AdminController.ListPOST`, `AdminController.Publish`, `AdminController.PublishOrUnpublishAsync`, `CreateContentTask.ExecuteAsync`, `CreateEndpoint.AddCreateContentEndpoint`, `PublishContentTask.ExecuteAsync`, `UpdateContentTask.ExecuteAsync`
11. **OrchardCore.Contents.Core** (3 distinct callers):
    - `GetAsync` (3): `ContentOrchardHelperExtensions.GetContentItemByHandleAsync`, `ContentOrchardHelperExtensions.GetContentItemByIdAsync`, `ContentOrchardHelperExtensions.GetContentItemsByIdAsync`
    - `CreateAsync` (0)
    - `PublishAsync` (0)
12. **OrchardCore.DataPipelines** (1 distinct caller):
    - `GetAsync` (1): `SaveContentItemsStep.ExecuteAsync`
    - `CreateAsync` (1): `SaveContentItemsStep.ExecuteAsync`
    - `PublishAsync` (1): `SaveContentItemsStep.ExecuteAsync`
13. **OrchardCore.Demo** (12 distinct callers):
    - `GetAsync` (9): `ContentApiController.GetAuthorizedById`, `ContentController.Display`, `ContentController.Edit`, `ContentController.EditPost`, `EditModel.OnGetAsync`, `EditModel.OnPostDelete`, `EditModel.OnPostUpdate`, `HomeController.Display`, `HomeController.DisplayShape`
    - `CreateAsync` (3): `ContentApiController.AddContent`, `HomeController.Index`, `ListModel.OnPostAsync`
    - `PublishAsync` (0)
14. **OrchardCore.Layers** (1 distinct caller):
    - `GetAsync` (1): `AdminController.UpdatePosition`
    - `CreateAsync` (0)
    - `PublishAsync` (0)
15. **OrchardCore.Lists** (9 distinct callers):
    - `GetAsync` (9): `ContainedPartDisplayDriver.GetContainerAsync`, `ContainedPartHandler.CloningAsync`, `ContainedQueryObjectType.ResolveAsync`, `ContainerFilter.ProcessAsync`, `ContainerService.UpdateContentItemOrdersAsync`, `ListFeedQuery.ExecuteAsync`, `ListFeedQuery.MatchAsync`, `MetaWeblogHandler.MetaWeblogGetUserBlogsAsync`, `RemotePublishingController.Rsd`
    - `CreateAsync` (1): `MetaWeblogHandler.MetaWeblogGetUserBlogsAsync`
    - `PublishAsync` (1): `MetaWeblogHandler.MetaWeblogGetUserBlogsAsync`
16. **OrchardCore.Media** (1 distinct caller):
    - `GetAsync` (1): `ViewMediaFolderAuthorizationHandler.AuthorizeAttachedMediaFieldsFolderAsync`
    - `CreateAsync` (0)
    - `PublishAsync` (0)
17. **OrchardCore.Menu** (6 distinct callers):
    - `GetAsync` (6): `AdminController.CreatePost`, `AdminController.Delete`, `AdminController.Edit`, `AdminController.EditPost`, `MenuShapes.BuildMenuShapeAsync`, `MenuShapes.CreateSafeLinkContentItem`
    - `CreateAsync` (0)
    - `PublishAsync` (0)
18. **OrchardCore.PublishLater** (1 distinct caller):
    - `GetAsync` (1): `ScheduledPublishingBackgroundTask.DoWorkAsync`
    - `CreateAsync` (0)
    - `PublishAsync` (1): `ScheduledPublishingBackgroundTask.DoWorkAsync`
19. **OrchardCore.Taxonomies** (17 distinct callers):
    - `GetAsync` (17): `AdminController.CreatePost`, `AdminController.Delete`, `AdminController.Edit`, `AdminController.EditPost`, `InheritedTermsFilter.ProcessAsync`, `TagController.CreatePost`, `TaxonomyContentsAdminListDisplayDriver.EditAsync`, `TaxonomyFieldIndexHandler.BuildIndexAsync`, `TaxonomyFieldDisplayDriver.Edit`, `TaxonomyFieldQueryObjectType.ResolveAsync`, `TaxonomyFieldTagsDisplayDriver.Edit`, `TaxonomyFieldTagsDisplayDriver.UpdateAsync`, `TaxonomyOrchardHelperExtensions.GetInheritedTermsAsync`, `TaxonomyOrchardHelperExtensions.GetTaxonomyTermAsync`, `TaxonomyTermsFilter.ProcessAsync`, `TermShapes.BuildTermShapeAsync`, `TermShapes.CreateSafeLinkContentItem`
    - `CreateAsync` (1): `TagController.CreatePost`
    - `PublishAsync` (1): `TagController.CreatePost`
20. **OrchardCore.Templates** (1 distinct caller):
    - `GetAsync` (1): `PreviewController.Render`
    - `CreateAsync` (0)
    - `PublishAsync` (0)

---

### (4b) Over-Coupled Projects (`DEPENDS_ON` Without Call Usage)

Total non-test projects declaring a direct `<ProjectReference Include="...OrchardCore.ContentManagement.Abstractions.csproj" />`: **48**
- **Calling Projects**: **11** (`OrchardCore.Alias`, `OrchardCore.Autoroute`, `OrchardCore.ContentFields`, `OrchardCore.ContentLocalization`, `OrchardCore.ContentManagement`, `OrchardCore.ContentManagement.GraphQL`, `OrchardCore.Layers`, `OrchardCore.Media`, `OrchardCore.Menu`, `OrchardCore.Taxonomies`, `OrchardCore.Templates`)
- **Over-Coupled Projects (`0` call sites)**: **37**

List of 37 over-coupled projects:
1. `OrchardCore.AuditTrail.Abstractions`
2. `OrchardCore.Autoroute.Core`
3. `OrchardCore.AzureAI.Core`
4. `OrchardCore.ContentFields.Core`
5. `OrchardCore.ContentLocalization.Abstractions`
6. `OrchardCore.ContentManagement.Display`
7. `OrchardCore.ContentPreview`
8. `OrchardCore.Contents.TagHelpers`
9. `OrchardCore.ContentTypes`
10. `OrchardCore.ContentTypes.Abstractions`
11. `OrchardCore.CustomSettings`
12. `OrchardCore.Facebook`
13. `OrchardCore.Features`
14. `OrchardCore.Flows`
15. `OrchardCore.Flows.Core`
16. `OrchardCore.GitHub`
17. `OrchardCore.Html`
18. `OrchardCore.Html.Core`
19. `OrchardCore.Indexing.Abstractions`
20. `OrchardCore.Markdown`
21. `OrchardCore.Media.Abstractions`
22. `OrchardCore.MetaWeblog.Abstractions`
23. `OrchardCore.Microsoft.Authentication`
24. `OrchardCore.Notifications.Core`
25. `OrchardCore.OpenId`
26. `OrchardCore.Placements`
27. `OrchardCore.Queries`
28. `OrchardCore.ReCaptcha`
29. `OrchardCore.Seo`
30. `OrchardCore.Sitemaps`
31. `OrchardCore.Sitemaps.Abstractions`
32. `OrchardCore.Spatial`
33. `OrchardCore.Taxonomies.Core`
34. `OrchardCore.Title`
35. `OrchardCore.Twitter`
36. `OrchardCore.Widgets`
37. `OrchardCore.Workflows`
