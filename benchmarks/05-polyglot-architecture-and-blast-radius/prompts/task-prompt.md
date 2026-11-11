Perform a comprehensive, 4-part architectural governance, security, and blast-radius audit on the `OrchardCMS/OrchardCore` modular .NET monolith located at `./repos/OrchardCore` (`src/OrchardCore/OrchardCore.*` and `src/OrchardCore.Modules/OrchardCore.*`, excluding `test/` projects, `src/Templates/`, and `*Tests*` files). Answer all four engineering questions below with 100% completeness (do not guess, sample, or truncate items). To keep both query outputs and your final report concise, **do not include file path columns** and **use compact grouped bullet lists** rather than wide multi-line Markdown tables:

1. **Task 1 — AppSec Authorization Audit: State-Mutating Controller Actions Missing `AuthorizeAsync` (`Negative Graph Query / NOT EXISTS`)**:
   Across the following 5 security-sensitive modules:
   - `OrchardCore.Users`
   - `OrchardCore.Roles`
   - `OrchardCore.Tenants`
   - `OrchardCore.ContentTypes`
   - `OrchardCore.Workflows`
   Identify every public action method (`ControllerClass.ActionMethod`) on an MVC/API controller class (inheriting from `Controller` or `ControllerBase`, or decorated with `[Controller]` / `[ApiController]`) that is decorated with `[HttpPost]`, `[HttpPut]`, or `[HttpDelete]` (excluding `[AllowAnonymous]` actions) and:
   - **(1a)** Calls `IAuthorizationService.AuthorizeAsync` directly (`1` hop) or via a helper method within `2` hops (`1..2` hops), vs
   - **(1b)** **Does NOT call `AuthorizeAsync` within `1..2` hops** (indicating whether class-level attributes like `[Admin]` or `[Authorize]` are present on the controller).
   Group by module and report the exact counts and compact list of `ControllerClass.ActionMethod` for both **(1a)** and **(1b)**.

2. **Task 2 — Architectural Boundary Violations: Web & UI Layer Bypassing Services to Access `YesSql.ISession` Directly (`Semantic Invariant Audit`)**:
   In a clean modular architecture, MVC controllers and UI display drivers should interact with domain services (`IContentManager`, `ISiteService`, stores) rather than querying or mutating the low-level `YesSql.ISession` document session directly.
   Across all modules under `src/OrchardCore.Modules/*`, identify every non-test **Controller** class (inheriting from `Controller` or `ControllerBase`, or decorated with `[Controller]` / `[ApiController]`) and **Display Driver** class (inheriting from or implementing any `*DisplayDriver*` contract such as `DisplayDriver`, `ContentPartDisplayDriver`, `ContentFieldDisplayDriver`, `SectionDisplayDriver`, or `IDisplayDriver`) whose methods **directly invoke methods on an injected `YesSql.ISession` member** (such as `Query`, `QueryIndex`, `SaveAsync`, `GetAsync`, `CancelAsync`, `CreateConnectionAsync`).
   Group by module (`.csproj` project name), report the exact count of violating classes and method-to-session operation pairs per module, and list `ClassName.MethodName -> ISession.Operation`.

3. **Task 3 — Event Lifecycle Side-Effect Fan-Out: What Happens Across Modules When Content Is Published? (`Cross-Module Reactive Pipeline`)**:
   When content is published in `OrchardCore`, the pipeline broadcasts `PublishingAsync` and `PublishedAsync` across content handlers (`IContentHandler`, `ContentHandlerBase`, `ContentPartHandler`, `ContentFieldHandler`, or `IScopedIndexProvider` implementations).
   Identify every non-test class across `src/OrchardCore/` and `src/OrchardCore.Modules/` that implements or overrides `PublishingAsync` or `PublishedAsync`, and trace its **downstream calls (`1 to 2` hops)** to classify what side-effects each handler triggers:
   - **Database / Session / Index / Content / Store mutation** (`ISession`, `*Entries`, `*IndexProvider`, `*TaskManager`, `*Store`, `IContentManager`, `IContentDefinitionManager`, `ISiteService`, `IAuditTrailManager`, `ISitemapUpdateHandler`)
   - **Cache invalidation** (`ITagCache`, `RemoveTagAsync`, `IMemoryCache`, `IDistributedCache`)
   - **Workflow / Deferred Task / Template trigger** (`IWorkflowManager`, `TriggerEventAsync`, `AddDeferredTask`, `ILiquidTemplateManager`)
   Report the total count of handler methods (`ClassName.PublishingAsync` / `ClassName.PublishedAsync`) and list `Module -> HandlerClass.MethodName -> [Triggered Downstream Services/Methods]`.

4. **Task 4 — Breaking-Change Blast Radius & Unused Project Dependencies (`Layer 1–3 Cross-Layer Coupling`)**:
   Suppose we introduce a breaking signature change to these 3 core contract methods on `IContentManager` in `OrchardCore.ContentManagement.Abstractions`:
   - `IContentManager.GetAsync`
   - `IContentManager.CreateAsync`
   - `IContentManager.PublishAsync`
   Determine across all non-test projects (`!is_test`, excluding `test/` and `src/Templates/`):
   - **(4a) Direct Call Blast Radius**: Which projects contain classes/methods that invoke at least one of these 3 `IContentManager` methods directly (`1` hop), and how many distinct caller methods (`CallerClass.CallerMethod`) invoke each of `GetAsync`, `CreateAsync`, and `PublishAsync` per project?
   - **(4b) Over-Coupled Projects (`DEPENDS_ON` Without Call Usage)**: Which non-test projects declare a direct project dependency (`ProjectReference` / `DEPENDS_ON`) on `OrchardCore.ContentManagement.Abstractions`, yet contain **zero calls (`0` call sites)** to any of `GetAsync`, `CreateAsync`, or `PublishAsync`?
   Report exact project counts and compact lists for both **(4a)** and **(4b)**.
