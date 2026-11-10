# OrchardCore Architectural Governance, Security & Blast-Radius Audit

Scope: `src/OrchardCore/OrchardCore.*` and `src/OrchardCore.Modules/OrchardCore.*` (excluding `test/`, `src/Templates/`, and `*Tests*` files).

---

## 1. AppSec Authorization Audit: State-Mutating Controller Actions (`[HttpPost]` / `[HttpPut]` / `[HttpDelete]`, non-`[AllowAnonymous]`)

**Totals across the 5 audited modules**: **88** public state-mutating controller actions (excluding 10 action-level `[AllowAnonymous]` POST endpoints in `OrchardCore.Users`):
- **(1a) Calls `IAuthorizationService.AuthorizeAsync` within `1..2` hops**: **67** actions (`54` direct 1-hop, `11` via 2-hop helper, `4` via both 1-hop and 2-hop — note: `61` if excluding the `6` actions on `TenantApiController`, whose class declaration carries `[Authorize(AuthenticationSchemes = "Api"), AllowAnonymous]`).
- **(1b) Does NOT call `AuthorizeAsync` within `1..2` hops**: **21** actions (`7` protected by class `[Admin]`, `11` protected by class `[Authorize]`, and **`3` with neither `[Admin]` nor `[Authorize]` on class or action**).

### (1a) Actions Calling `AuthorizeAsync` (`1..2` Hops) — `67` Total
- **`OrchardCore.ContentTypes` (`13`)**:
  - `AdminController` (`13`; no class `[Admin]`/`[Authorize]`): `AddFieldToPOST` (1-hop), `AddPartsToPOST` (1-hop & 2-hop via `AddPartsTo`), `AddReusablePartToPOST` (1-hop & 2-hop via `AddReusablePartTo`), `CreatePOST` (1-hop), `CreatePartPOST` (1-hop), `Delete` (1-hop), `DeletePart` (1-hop), `EditFieldPOST` (1-hop), `EditPartPOST` (1-hop), `EditPost` (1-hop), `EditTypePartPOST` (1-hop), `RemoveFieldFromPOST` (1-hop), `RemovePart` (1-hop; action `[Admin]`)
- **`OrchardCore.Roles` (`4`)**:
  - `AdminController` (`4`; class `[Admin]`): `ClonePost` (1-hop), `Create` (1-hop), `Delete` (1-hop), `EditPost` (1-hop)
- **`OrchardCore.Tenants` (`17`)**:
  - `AdminController` (`7`; class `[Admin]`): `Create` (1-hop), `Disable` (1-hop), `Edit` (1-hop), `Enable` (1-hop), `Index` (1-hop), `Reload` (1-hop), `Remove` (1-hop)
  - `FeatureProfilesController` (`4`; class `[Feature, Admin]`): `CreatePost` (2-hop via `ProcessSaveAsync`), `Delete` (1-hop), `EditPost` (2-hop via `ProcessSaveAsync`), `IndexPost` (1-hop)
  - `TenantApiController` (`6`; class `[Route, ApiController, Authorize, IgnoreAntiforgeryToken, AllowAnonymous]`): `Create` (1-hop), `Disable` (1-hop), `Edit` (1-hop), `Enable` (1-hop), `Remove` (1-hop), `Setup` (1-hop)
- **`OrchardCore.Users` (`12`)**:
  - `AdminController` (`9`; class `[Admin]`): `ConfirmEmail` (1-hop), `CreatePost` (1-hop), `Delete` (1-hop), `Disable` (1-hop), `EditPassword` (1-hop), `EditPost` (1-hop), `Enable` (1-hop), `IndexPOST` (1-hop), `Unlock` (1-hop)
  - `AdminTwoFactorAuthenticationController` (`1`; class `[Admin, Feature]`): `Disable` (1-hop)
  - `AuditTrailAdminController` (`1`; class `[Admin]`): `IndexFilterPOST` (1-hop)
  - `EmailConfirmationController` (`1`; action `[Authorize]`, no class auth attribute): `SendVerificationEmail` (1-hop)
- **`OrchardCore.Workflows` (`21`)**:
  - `HttpWorkflowController` (`1`; action `[Admin]`, no class auth attribute): `GenerateUrl` (1-hop)
  - `WorkflowController` (`4`; class `[Admin]`): `AllBulkEdit` (1-hop & 2-hop via `ApplyBulkActionAsync`), `BulkEdit` (1-hop & 2-hop via `ApplyBulkActionAsync`), `Delete` (1-hop), `Restart` (1-hop)
  - `WorkflowDesignerController` (`11`; class `[Admin]`): `Retry` (1-hop), `Run` (1-hop); 2-hop via `CanManageAsync`: `AddActivity`, `Discard`, `EditorPost`, `OutputBindings`, `Publish`, `Restore`, `Save`, `SettingsPost`, `Variables`
  - `WorkflowTypeController` (`5`; class `[Admin]`): `BulkEdit` (1-hop), `Clone` (1-hop), `Create` (1-hop), `Delete` (1-hop), `Export` (1-hop)

### (1b) Actions NOT Calling `AuthorizeAsync` (`1..2` Hops) — `21` Total
- **`OrchardCore.ContentTypes` (`0`)**: None.
- **`OrchardCore.Roles` (`0`)**: None.
- **`OrchardCore.Tenants` (`2`)**:
  - `AdminController.IndexFilterPOST` — class `[Admin]`
  - `FeatureProfilesController.IndexFilterPOST` — class `[Feature, Admin]`
- **`OrchardCore.Users` (`15`)**:
  - `AccountController` (`2`; class `[Authorize]`): `ChangePassword`, `LogOff`
  - `AdminController` (`1`; class `[Admin]`): `IndexFilterPOST`
  - `AuthenticatorAppController` (`2`; class `[Authorize, Feature]`): `Index`, `ResetPost`
  - `ChangeEmailController` (`1`; class `[Feature, Authorize]`): `Index`
  - `ExternalAuthenticationsController` (`2`; **NO `[Admin]` or `[Authorize]` on class or action** — class only has `[Feature]`): `LinkLogin`, `RemoveLogin`
  - `SmsAuthenticatorController` (`2`; class `[Authorize, Feature]`): `IndexPost`, `ValidateCode`
  - `TwoFactorAuthenticationController` (`5`; class `[Authorize, Feature]`): `DisableTwoFactorAuthenticationPost`, `EnableTwoFactorAuthentication`, `ForgetTwoFactorClient`, `GenerateRecoveryCodesPost`, `Index`
- **`OrchardCore.Workflows` (`4`)**:
  - `HttpWorkflowController.Invoke` (`[IgnoreAntiforgeryToken, HttpGet, HttpPost, HttpPut, HttpPatch]`) — **NO `[Admin]` or `[Authorize]` on class or action** (validates signed workflow token)
  - `WorkflowController.AllFilterPOST` — class `[Admin]`
  - `WorkflowController.IndexFilterPOST` — class `[Admin]`
  - `WorkflowTypeController.IndexFilterPOST` — class `[Admin]`

---

## 2. Architectural Boundary Violations: Controllers & Display Drivers Directly Accessing `YesSql.ISession`

**Totals across `src/OrchardCore.Modules/*`**: **13** modules, **25** violating classes (`20` Controllers + `5` Display Drivers), **51** distinct `ClassName.MethodName` methods (`55` method overloads), and **72** distinct `ClassName.MethodName -> ISession.Operation` pairs (**76** overload-level `(Function, ISession.Operation)` pairs where GET/POST overloads share a method name). *(Note: Razor Pages `PageModel` classes `EditModel` and `ListModel` in `OrchardCore.Demo` also access `ISession`, but are excluded as `PageModel` rather than MVC `Controller`/`ControllerBase`.)*

- **`OrchardCore.AdminDashboard`** — `1` class (`1` Controller), `1` pair:
  - `DashboardController.Update -> ISession.SaveAsync`
- **`OrchardCore.Autoroute`** — `1` class (`1` Display Driver), `1` pair:
  - `AutoroutePartDisplayDriver.UpdateAsync -> ISession.QueryIndex`
- **`OrchardCore.ContentFields`** — `2` classes (`1` Controller, `1` Display Driver), `3` pairs:
  - `LocalizationSetContentPickerAdminController.SearchLocalizationSets -> ISession.Query`
  - `UserPickerFieldDisplayDriver.Edit -> ISession.Query`
  - `UserPickerFieldDisplayDriver.UpdateAsync -> ISession.Query`
- **`OrchardCore.Contents`** — `4` classes (`3` Controllers, `1` Display Driver), `13` pairs:
  - `AddToDeploymentPlanController.AddContentItem -> ISession.GetAsync`, `ISession.SaveAsync`
  - `AddToDeploymentPlanController.AddContentItems -> ISession.GetAsync`, `ISession.Query`, `ISession.SaveAsync`
  - `AdminController.CreateInternalAsync -> ISession.CancelAsync`
  - `AdminController.EditInternalAsync -> ISession.CancelAsync`
  - `AdminController.ListPOST -> ISession.Query`, `ISession.CancelAsync`
  - `AuditTrailContentController.Display -> ISession.Query`
  - `AuditTrailContentController.Restore -> ISession.Query`
  - `AuditTrailContentEventDisplayDriver.DisplayAsync -> ISession.QueryIndex`, `ISession.Query`
- **`OrchardCore.Demo`** — `2` classes (`2` Controllers), `8` pairs:
  - `ContentController.EditPost -> ISession.CancelAsync`, `ISession.SaveAsync`
  - `TodoController.Delete -> ISession.Query`, `ISession.Delete`
  - `TodoController.Edit -> ISession.Query`
  - `TodoController.Index -> ISession.Query`
  - `TodoController.Update -> ISession.Query`, `ISession.SaveAsync`
- **`OrchardCore.Deployment`** — `3` classes (`3` Controllers), `20` distinct `ClassName.MethodName -> Operation` pairs (`23` overload-level pairs across `14` overloads):
  - `DeploymentPlanController.Create -> ISession.QueryIndex`, `ISession.SaveAsync`
  - `DeploymentPlanController.Delete -> ISession.GetAsync`, `ISession.Delete`
  - `DeploymentPlanController.Display -> ISession.GetAsync`
  - `DeploymentPlanController.Edit -> ISession.GetAsync` (in both GET & POST overloads), `ISession.QueryIndex` (POST), `ISession.SaveAsync` (POST)
  - `DeploymentPlanController.Index -> ISession.Query`
  - `DeploymentPlanController.IndexBulkActionPOST -> ISession.Query`, `ISession.Delete`
  - `ExportFileController.Execute -> ISession.GetAsync`
  - `StepController.Create -> ISession.GetAsync` (in both GET & POST overloads), `ISession.SaveAsync` (POST)
  - `StepController.Delete -> ISession.GetAsync`, `ISession.SaveAsync`
  - `StepController.Edit -> ISession.GetAsync` (in both GET & POST overloads), `ISession.SaveAsync` (POST)
  - `StepController.UpdateOrder -> ISession.GetAsync`, `ISession.SaveAsync`
- **`OrchardCore.Deployment.Remote`** — `1` class (`1` Controller), `1` pair:
  - `ExportRemoteInstanceController.Execute -> ISession.GetAsync`
- **`OrchardCore.Layers`** — `1` class (`1` Controller), `1` pair:
  - `AdminController.UpdatePosition -> ISession.SaveAsync`
- **`OrchardCore.Notifications`** — `2` classes (`1` Controller, `1` Display Driver), `4` pairs:
  - `AdminController.ListPOST -> ISession.Query`
  - `NotificationNavbarDisplayDriver.DisplayAsync -> ISession.Query`, `ISession.QueryIndex`
  - `NotificationNavbarDisplayDriver.PopulateVisibleAsync -> ISession.Query`
- **`OrchardCore.Search`** — `1` class (`1` Controller), `1` pair:
  - `SearchController.Search -> ISession.Query`
- **`OrchardCore.Taxonomies`** — `3` classes (`2` Controllers, `1` Display Driver), `6` pairs:
  - `AdminController.CreatePost -> ISession.SaveAsync`
  - `AdminController.Delete -> ISession.SaveAsync`
  - `AdminController.EditPost -> ISession.SaveAsync`
  - `TagController.CreatePost -> ISession.CancelAsync`, `ISession.SaveAsync`
  - `TaxonomyContentsAdminListSettingsDisplayDriver.EditAsync -> ISession.Query`
- **`OrchardCore.Users`** — `1` class (`1` Controller), `3` pairs:
  - `AdminController.Delete -> ISession.CancelAsync`
  - `AdminController.IndexPOST -> ISession.Query`
  - `AdminController.Unlock -> ISession.CancelAsync`
- **`OrchardCore.Workflows`** — `3` classes (`3` Controllers), `10` distinct `ClassName.MethodName -> Operation` pairs (`11` overload-level pairs across `9` overloads):
  - `WorkflowController.BuildListAsync -> ISession.Query`, `ISession.QueryIndex`
  - `WorkflowDesignerController.CountInstancesByVersionAsync -> ISession.CreateConnectionAsync`
  - `WorkflowDesignerController.Definition -> ISession.QueryIndex`
  - `WorkflowDesignerController.Instance -> ISession.QueryIndex`
  - `WorkflowDesignerController.LatestInstance -> ISession.Query`
  - `WorkflowTypeController.BulkEdit -> ISession.Query`
  - `WorkflowTypeController.Clone -> ISession.GetAsync` (in both GET & POST overloads)
  - `WorkflowTypeController.Index -> ISession.Query`, `ISession.CreateConnectionAsync`

---

## 3. Event Lifecycle Side-Effect Fan-Out: `PublishingAsync` & `PublishedAsync` (`1..2` Hops)

**Totals across `src/OrchardCore/` and `src/OrchardCore.Modules/`**: **18** non-test handler classes across **11** projects implementing/overriding **22** `ClassName.MethodName` handler methods (`6` `PublishingAsync` + `16` `PublishedAsync`, spanning **26** `Function` overloads because `ContentPartHandler<TPart>` and `ContentFieldHandler<TField>` each define an explicit interface method delegating to a typed virtual overload; **15** concrete handler classes / **16** concrete handler methods + **3** base classes / **6** base methods). *(Interfaces `IContentHandler`, `IContentPartHandler`, `IContentFieldHandler`, and unrelated `IDataPipelineHandler` are excluded as interfaces.)*

### Database / Session / Index / Content / Store Side-Effects
- `OrchardCore.Autoroute -> AutoroutePartHandler.PublishedAsync -> [IAutorouteEntries.UpdateEntriesAsync / AutorouteEntries.UpdateEntriesAsync -> AutorouteEntries.EnsureInitializedAsync & IVolatileDocumentManager<AutorouteStateDocument>.UpdateAsync; IContentManager.PopulateAspectAsync / DefaultContentManager.PopulateAspectAsync (direct & via CheckContainedHomeRouteAsync); ISiteService.LoadSiteSettingsAsync & ISiteService.UpdateSiteSettingsAsync (via SetHomeRouteAsync)]` *(also triggers Cache Invalidation below)*
- `OrchardCore.Autoroute.Core -> AutoroutePartIndexProvider.PublishedAsync -> [IAutorouteEntries.UpdateEntriesAsync / AutorouteEntries.UpdateEntriesAsync -> AutorouteEntries.EnsureInitializedAsync & IVolatileDocumentManager<AutorouteStateDocument>.UpdateAsync; IContentDefinitionManager.GetTypeDefinitionAsync -> IContentDefinitionStore.GetContentDefinitionAsync (DatabaseContentDefinitionStore / FileContentDefinitionStore)]`
- `OrchardCore.ContentLocalization -> LocalizationPartHandler.PublishedAsync -> [ILocalizationEntries.UpdateEntriesAsync / LocalizationEntries.UpdateEntriesAsync -> LocalizationEntries.EnsureInitializedAsync & IVolatileDocumentManager<LocalizationStateDocument>.UpdateAsync]`
- `OrchardCore.ContentLocalization -> LocalizedContentItemIndexProvider.PublishedAsync -> [IContentDefinitionManager.GetTypeDefinitionAsync / ContentDefinitionManager.GetTypeDefinitionAsync -> IContentDefinitionStore.GetContentDefinitionAsync (DatabaseContentDefinitionStore / FileContentDefinitionStore)]`
- `OrchardCore.ContentManagement -> ContentPartHandlerCoordinator.PublishingAsync -> [ContentPartHandlerCoordinator.InvokeHandlers -> IContentDefinitionManager.GetTypeDefinitionAsync, IContentPartHandlerResolver.GetHandlers, IContentFieldHandlerResolver.GetHandlers]`
- `OrchardCore.ContentManagement -> ContentPartHandlerCoordinator.PublishedAsync -> [ContentPartHandlerCoordinator.InvokeHandlers -> IContentDefinitionManager.GetTypeDefinitionAsync, IContentPartHandlerResolver.GetHandlers, IContentFieldHandlerResolver.GetHandlers]`
- `OrchardCore.Contents -> AuditTrailContentHandler.PublishedAsync -> [AuditTrailContentHandler.RecordAuditTrailEventAsync -> YesSql.ISession.QueryIndex, ISiteService.GetSettingsAsync / SiteServiceExtensions.GetSettingsAsync, IAuditTrailManager.RecordEventAsync / AuditTrailManager.RecordEventAsync]`
- `OrchardCore.Contents -> ContentTypesSitemapUpdateHandler.PublishedAsync -> [ContentTypesSitemapUpdateHandler.UpdateSitemapAsync -> ISitemapUpdateHandler.UpdateSitemapAsync / DefaultSitemapUpdateHandler.UpdateSitemapAsync]`
- `OrchardCore.Indexing -> CreateIndexingTaskContentHandler.PublishedAsync -> [CreateIndexingTaskContentHandler.AddUpdateTaskAsync -> IIndexingTaskManager.CreateTaskAsync / IndexingTaskManager.CreateTaskAsync]`
- `OrchardCore.Layers -> LayerMetadataHandler.PublishedAsync -> [LayerMetadataHandler.UpdateAsync -> ContentItemExtensions.TryGet, IVolatileDocumentManager<LayerState>.UpdateAsync]`

### Cache Invalidation Side-Effects
- `OrchardCore.Alias -> AliasPartHandler.PublishedAsync -> [ITagCache.RemoveTagAsync (DefaultTagCache.RemoveTagAsync, RedisTagCache.RemoveTagAsync -> IRedisService.ConnectAsync, IEnumerable<ITagRemovedEventHandler>.InvokeAsync)]`
- `OrchardCore.Autoroute -> AutoroutePartHandler.PublishedAsync -> [AutoroutePartHandler.RemoveTagAsync -> ITagCache.RemoveTagAsync (DefaultTagCache.RemoveTagAsync, RedisTagCache.RemoveTagAsync)]` *(combined with DB/Entries/Site mutations above)*
- `OrchardCore.Contents -> ContentsHandler.PublishedAsync (OrchardCore.Contents.Handlers.ContentsHandler) -> [ITagCache.RemoveTagAsync (DefaultTagCache.RemoveTagAsync, RedisTagCache.RemoveTagAsync -> IRedisService.ConnectAsync, IEnumerable<ITagRemovedEventHandler>.InvokeAsync)]`

### Workflow / Deferred Task / Template Trigger Side-Effects
- `OrchardCore.Contents -> ContentsHandler.PublishedAsync (OrchardCore.Contents.Workflows.Handlers.ContentsHandler) -> [ContentsHandler.TriggerWorkflowEventAsync -> IWorkflowManager.TriggerEventAsync / WorkflowManager.TriggerEventAsync]`
- `OrchardCore.Indexing.Core -> IndexingContentHandler.PublishedAsync -> [IndexingContentHandler.AddContentItemAsync -> IndexingContentHandler.AddDeferredTask (-> ShellScope.AddDeferredTask)]`
- `OrchardCore.Title -> TitlePartHandler.PublishingAsync -> [TitlePartHandler.SetTitleAsync -> ILiquidTemplateManager.RenderStringAsync / LiquidTemplateManager.RenderStringAsync, TitlePartHandler.GetSettingsAsync]`

### In-Memory Mutation Only / No-Op Base Implementations (No External Side-Effects)
- `OrchardCore.ContentManagement -> UpdateContentsHandler.PublishingAsync -> [In-memory ContentItem.CreatedUtc / PublishedUtc assignment via IClock.UtcNow; no downstream service call]`
- `OrchardCore.ContentManagement.Abstractions -> ContentHandlerBase.PublishingAsync -> [None (virtual no-op)]`
- `OrchardCore.ContentManagement.Abstractions -> ContentHandlerBase.PublishedAsync -> [None (virtual no-op)]`
- `OrchardCore.ContentManagement.Abstractions -> ContentPartHandler.PublishingAsync -> [Self-delegates explicit IContentPartHandler.PublishingAsync -> virtual ContentPartHandler.PublishingAsync]`
- `OrchardCore.ContentManagement.Abstractions -> ContentPartHandler.PublishedAsync -> [Self-delegates explicit IContentPartHandler.PublishedAsync -> virtual ContentPartHandler.PublishedAsync]`
- `OrchardCore.ContentManagement.Abstractions -> ContentFieldHandler.PublishingAsync -> [Self-delegates explicit IContentFieldHandler.PublishingAsync -> virtual ContentFieldHandler.PublishingAsync]`
- `OrchardCore.ContentManagement.Abstractions -> ContentFieldHandler.PublishedAsync -> [Self-delegates explicit IContentFieldHandler.PublishedAsync -> virtual ContentFieldHandler.PublishedAsync]`

---

## 4. Breaking-Change Blast Radius (`IContentManager.GetAsync`, `CreateAsync`, `PublishAsync`) & Unused Project Dependencies

### (4a) Direct Call Blast Radius (`1` Hop to `IContentManager.{GetAsync, CreateAsync, PublishAsync}`) — `19` Non-Test Projects
Across all non-test projects, **19** projects directly invoke `IContentManager.GetAsync` (**94** `CallerClass.CallerMethod` methods, or **95** `Function` nodes including local function `GetContentByHandleAsync` inside `OrchardCore.Contents.Startup.ConfigureServices`), `IContentManager.CreateAsync` (**11** caller methods), or `IContentManager.PublishAsync` (**13** caller methods):
- **`OrchardCore.AdminDashboard`**: `GetAsync: 1` (`DashboardController.Update`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.Alias`**: `GetAsync: 1` (`AliasPartRazorHelperExtensions.GetContentItemByAliasAsync`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.ArchiveLater`**: `GetAsync: 1` (`ScheduledArchivingBackgroundTask.DoWorkAsync`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.Autoroute`**: `GetAsync: 2` (`AutoroutePartRazorHelperExtensions.GetContentItemBySlugAsync`, `Startup.ConfigureServices`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.ContentFields`**: `GetAsync: 3` (`ContentPickerAdminController.SearchContentItems`, `ContentPickerFieldDisplayDriver.Edit`, `LocalizationSetContentPickerFieldDisplayDriver.Edit`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.ContentLocalization`**: `GetAsync: 1` (`AdminController.Localize`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.ContentManagement.Abstractions`**: `GetAsync: 0` *(note: `ContentManagerExtensions.GetAsync` and `HasPublishedVersionAsync` also invoke `contentManager.GetAsync` in source, whose graph `:CALLS` edges resolve to `ContentManagerExtensions.GetAsync` / `DefaultContentManager.GetAsync`, which would make `GetAsync: 2`)*, `CreateAsync: 1` (`ContentManagerExtensions.UpdateValidateAndCreateAsync`), `PublishAsync: 0`
- **`OrchardCore.ContentManagement.GraphQL`**: `GetAsync: 1` (`ContentItemQuery.ResolveAsync`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.Contents`**: `GetAsync: 32` class methods / `33` functions (+ local function `Startup.ConfigureServices$GetContentByHandleAsync`; `AddToDeploymentPlanController.AddContentItem`, `AdminController.{Clone, Delete, DiscardDraft, Display, Edit, EditInternalAsync, Publish, PublishOrUnpublishAsync, Remove, Unpublish}`, `ContentActivity.OnWorkflowRestartingAsync`, `ContentItemDeploymentSource.ProcessAsync`, `ContentItemDeploymentStepDriver.UpdateAsync`, `ContentItemFilter.ProcessAsync`, `ContentItemSerializer.DeserializeValueAsync`, `CreateEndpoint.HandleAsync`, `DeleteContentTask.ExecuteAsync`, `DeleteEndpoint.HandleAsync`, `DisplayContentItemViewComponent.InvokeAsync`, `DownloadController.{Display, Download}`, `ExportContentToDeploymentTargetDeploymentSource.ProcessAsync`, `GetEndpoint.HandleAsync`, `ItemController.{Display, Preview}`, `PublishContentTask.ExecuteAsync`, `RetrieveContentTask.ExecuteAsync`, `Shapes.DiscoverAsync`, `Startup.ConfigureServices`, `UnpublishContentTask.ExecuteAsync`, `UpdateContentTask.ExecuteAsync`), `CreateAsync: 4` (`AdminController.CreateInternalAsync`, `ContentMethodsProvider.CreateContentItemAsync`, `CreateContentTask.ExecuteAsync`, `CreateEndpoint.HandleAsync`), `PublishAsync: 8` (`AdminController.{CreateAndPublishPOST, ListPOST, Publish, PublishOrUnpublishAsync}`, `CreateContentTask.ExecuteAsync`, `CreateEndpoint.HandleAsync`, `PublishContentTask.ExecuteAsync`, `UpdateContentTask.ExecuteAsync`)
- **`OrchardCore.Contents.Core`**: `GetAsync: 3` (`ContentOrchardHelperExtensions.{GetContentItemByHandleAsync, GetContentItemByIdAsync, GetContentItemsByIdAsync}`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.DataPipelines`**: `GetAsync: 1` (`SaveContentItemsStep.SaveAsync`), `CreateAsync: 1` (`SaveContentItemsStep.SaveAsync`), `PublishAsync: 1` (`SaveContentItemsStep.SaveAsync`)
- **`OrchardCore.Demo`**: `GetAsync: 9` (`ContentApiController.GetAuthorizedById`, `ContentController.{Display, Edit, EditPost}`, `EditModel.{OnGetAsync, OnPostDelete, OnPostUpdate}`, `HomeController.{Display, DisplayShape}`), `CreateAsync: 3` (`ContentApiController.AddContent`, `HomeController.Index`, `ListModel.OnPostAsync`), `PublishAsync: 0`
- **`OrchardCore.Layers`**: `GetAsync: 1` (`AdminController.UpdatePosition`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.Lists`**: `GetAsync: 13` (`ContainedPartDisplayDriver.GetContainerAsync`, `ContainedPartHandler.CloningAsync`, `ContainedQueryObjectType.ContainedQueryObjectType`, `ContainerFilter.ProcessAsync`, `ContainerService.UpdateContentItemOrdersAsync`, `ListFeedQuery.{ExecuteAsync, MatchAsync}`, `MetaWeblogHandler.{MetaWeblogDeletePostAsync, MetaWeblogEditPostAsync, MetaWeblogGetPostAsync, MetaWeblogGetRecentPosts, MetaWeblogNewPostAsync}`, `RemotePublishingController.Rsd`), `CreateAsync: 1` (`MetaWeblogHandler.MetaWeblogNewPostAsync`), `PublishAsync: 2` (`MetaWeblogHandler.{MetaWeblogEditPostAsync, MetaWeblogNewPostAsync}`)
- **`OrchardCore.Media`**: `GetAsync: 1` (`ViewMediaFolderAuthorizationHandler.AuthorizeAttachedMediaFieldsFolderAsync`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.Menu`**: `GetAsync: 6` (`AdminController.{CreatePost, Delete, Edit, EditPost}`, `MenuShapes.{DiscoverAsync, ShouldCreateAsync}`), `CreateAsync: 0`, `PublishAsync: 0`
- **`OrchardCore.PublishLater`**: `GetAsync: 1` (`ScheduledPublishingBackgroundTask.DoWorkAsync`), `CreateAsync: 0`, `PublishAsync: 1` (`ScheduledPublishingBackgroundTask.DoWorkAsync`)
- **`OrchardCore.Taxonomies`**: `GetAsync: 16` (`AdminController.{CreatePost, Delete, Edit, EditPost}`, `InheritedTermsFilter.ProcessAsync`, `TagController.CreatePost`, `TaxonomyContentsAdminListDisplayDriver.EditAsync`, `TaxonomyFieldDisplayDriver.Edit`, `TaxonomyFieldIndexHandler.BuildIndexAsync`, `TaxonomyFieldQueryObjectType.TaxonomyFieldQueryObjectType`, `TaxonomyFieldTagsDisplayDriver.{Edit, UpdateAsync}`, `TaxonomyOrchardHelperExtensions.{GetInheritedTermsAsync, GetTaxonomyTermAsync}`, `TaxonomyTermsFilter.ProcessAsync`, `TermShapes.DiscoverAsync`), `CreateAsync: 1` (`TagController.CreatePost`), `PublishAsync: 1` (`TagController.CreatePost`)
- **`OrchardCore.Templates`**: `GetAsync: 1` (`PreviewController.Render`), `CreateAsync: 0`, `PublishAsync: 0`

*(Note: `OrchardCore.ContentManagement` implements `IContentManager` on `DefaultContentManager` and makes internal self-calls to `DefaultContentManager.CreateAsync` (`CloneAsync`, `RestoreAsync`) and `DefaultContentManager.GetAsync` (`DiscardDraftAsync`, `UnpublishAsync`), rather than calling via `IContentManager`.)*

### (4b) Direct `DEPENDS_ON` `OrchardCore.ContentManagement.Abstractions` With `0` Calls to `GetAsync` / `CreateAsync` / `PublishAsync` — `38` Non-Test Projects
Out of **48** non-test projects declaring a direct `DEPENDS_ON` (`ProjectReference`) on `OrchardCore.ContentManagement.Abstractions`, `10` invoke `IContentManager.{GetAsync, CreateAsync, PublishAsync}` and **38** have `0` call sites to those 3 methods (`37` if excluding `OrchardCore.ContentManagement`, which implements `IContentManager` on `DefaultContentManager` and calls its own concrete methods):
- **Core / Framework Projects (`15`)**: `OrchardCore.AuditTrail.Abstractions`, `OrchardCore.Autoroute.Core`, `OrchardCore.AzureAI.Core`, `OrchardCore.ContentFields.Core`, `OrchardCore.ContentLocalization.Abstractions`, `OrchardCore.ContentManagement`, `OrchardCore.ContentManagement.Display`, `OrchardCore.ContentTypes.Abstractions`, `OrchardCore.Contents.TagHelpers`, `OrchardCore.Flows.Core`, `OrchardCore.Html.Core`, `OrchardCore.Indexing.Abstractions`, `OrchardCore.Media.Abstractions`, `OrchardCore.MetaWeblog.Abstractions`, `OrchardCore.Notifications.Core`, `OrchardCore.Sitemaps.Abstractions`, `OrchardCore.Taxonomies.Core` *(wait: exact count = `17` under `src/OrchardCore/` + `21` under `src/OrchardCore.Modules/` = `38` total)*
- **Module Projects (`21`)**: `OrchardCore.ContentPreview`, `OrchardCore.ContentTypes`, `OrchardCore.CustomSettings`, `OrchardCore.Facebook`, `OrchardCore.Features`, `OrchardCore.Flows`, `OrchardCore.GitHub`, `OrchardCore.Html`, `OrchardCore.Markdown`, `OrchardCore.Microsoft.Authentication`, `OrchardCore.OpenId`, `OrchardCore.Placements`, `OrchardCore.Queries`, `OrchardCore.ReCaptcha`, `OrchardCore.Seo`, `OrchardCore.Sitemaps`, `OrchardCore.Spatial`, `OrchardCore.Title`, `OrchardCore.Twitter`, `OrchardCore.Widgets`, `OrchardCore.Workflows`
