# Case Study 05: `OrchardCMS/OrchardCore` Modular .NET Monolith Audit (`CodeExplorer` vs `ripgrep`)

Paired A/B agent benchmark on the open-source [`OrchardCMS/OrchardCore`](https://github.com/OrchardCMS/OrchardCore) modular .NET monolith (`334` projects, `7,837` files, `134,067` graph nodes, `288,527` relationships) comparing a graph-assisted MCP agent (`CodeExplorer v1.78.1`) against a pure lexical CLI agent (`ripgrep v14.1.1` + `view_file`) under matched subagent system prompts.

---

## 1. Experimental Setup & Tested Versions

- **Target Repository**: `OrchardCMS/OrchardCore @ 05ad2d9a` (`334` projects, `7,837` files, ASP.NET Core, YesSql).
- **Compared Tools**:
  - **`CodeExplorer` (`ce`)**: `v1.78.1` (.NET 10, SQLite + OpenCypher MCP server, `134,067` nodes, `288,527` edges).
  - **Lexical Baseline**: `ripgrep` (`rg v14.1.1`) + `view_file` (custom AST/graph-walking script generation disallowed in both agents; complements Case 02 where custom Python scripts were allowed).
- **LLM Model**: `Gemini 3.8 Flash` (matched subagent prompts without `write_to_file`: [`prompts/graph-agent.md`](./prompts/graph-agent.md) and [`prompts/baseline-agent.md`](./prompts/baseline-agent.md)).

---

## 2. What the Agents Had to Solve on `OrchardCore`

1. **Task 1 — AppSec Authorization Audit (`AuthorizeAsync` Within `1..2` Hops)**:
   Across 5 security-sensitive modules (`OrchardCore.Users`, `OrchardCore.Roles`, `OrchardCore.Tenants`, `OrchardCore.ContentTypes`, `OrchardCore.Workflows`), identify all `88` non-anonymous public state-mutating controller actions (`[HttpPost]`, `[HttpPut]`, `[HttpDelete]`, excluding `[AllowAnonymous]`) and partition them into:
   - **(1a)** Actions calling `IAuthorizationService.AuthorizeAsync` within `1..2` hops (`67` actions: `56` direct `1`-hop + `11` via `2`-hop helpers `ProcessSaveAsync` and `CanManageAsync`), vs
   - **(1b)** Actions **not** calling `AuthorizeAsync` within `1..2` hops (`21` actions, highlighting `ExternalAuthenticationsController.LinkLogin`, `ExternalAuthenticationsController.RemoveLogin`, and `HttpWorkflowController.Invoke` which lack class-level `[Admin]` or `[Authorize]`).
2. **Task 2 — Architectural Boundary Violations (`Web & UI Layer Accessing YesSql.ISession Directly`)**:
   Across `src/OrchardCore.Modules/*`, identify all `25` non-test Controller and Display Driver classes across `13` modules whose methods bypass domain services to invoke operations (`Query`, `QueryIndex`, `SaveAsync`, `GetAsync`, `CancelAsync`, `CreateConnectionAsync`, `Delete`) directly on an injected `YesSql.ISession` member (`(t:Type)-[:HAS_MEMBER]->(mem:Member)-[r:ACCESSED_BY]->(m:Function)`).
3. **Task 3 — Event Lifecycle Side-Effect Fan-Out (`PublishingAsync` & `PublishedAsync`)**:
   Identify all `16` concrete `PublishingAsync` / `PublishedAsync` content handler methods across `15` classes (plus `6` base/adapter handler methods across `10` overloads in `OrchardCore.ContentManagement.Abstractions`) and trace their `1..2` hop downstream calls into database/index stores, `ITagCache.RemoveTagAsync`, `IWorkflowManager.TriggerEventAsync`, `ShellScope.AddDeferredTask`, and `ILiquidTemplateManager`.
4. **Task 4 — Breaking-Change Blast Radius & Over-Coupled Projects (`IContentManager`)**:
   For `IContentManager.GetAsync`, `CreateAsync`, and `PublishAsync`:
   - **(4a)** Identify all `19` non-test projects invoking `IContentManager` (`20` including internal self-calls inside `OrchardCore.ContentManagement` via `DefaultContentManager`) and all `118` `(CallerClass.CallerMethod, TargetMethod)` call edges (`94` `GetAsync` + `11` `CreateAsync` + `13` `PublishAsync`), including calls resolved via generic DI service locators (`sp.GetRequiredService<IContentManager>()`).
   - **(4b)** Identify all `38` over-coupled C# projects (`37` excluding `OrchardCore.ContentManagement`) that declare a direct `DEPENDS_ON` project reference on `OrchardCore.ContentManagement.Abstractions` yet have `0` calls to `GetAsync`, `CreateAsync`, or `PublishAsync`.

---

## 3. Execution Telemetry Scorecard

Measured from paired A/B agent runs on `OrchardCMS/OrchardCore @ 05ad2d9a` ([`results/metrics.json`](./results/metrics.json)):

| Metric | `ripgrep` + `view_file` (`b933d749`) | `CodeExplorer` MCP (`add5de69`) | Delta |
| :--- | :---: | :---: | :---: |
| **Time to Answer (`send_message`)** | `664 s` (`11 min 04 sec`) / `701 s` wall | **`265 s` (`4 min 25 sec`) / `299 s` wall** | **`2.51×` faster (`2.34×` wall)** |
| **LLM Turns, Steps & Compactions** | `193` turns / `389` steps (`0` compactions) | **`92` turns / `184` steps (`0` compactions)** | **`2.10×` fewer turns (`2.11×` fewer steps)** |
| **Total Tool Calls & File Reads** | `192` (`74` `rg` + `117` `view_file`, `1` `send_message`) | **`91` (`54` `mcp` + `36` `view_file`, `1` `send_message`)** | **`2.11×` fewer calls (`3.25×` fewer reads)** |
| **Raw Tool Output Ingested** | `573,355` chars (`~143,339` tokens) | **`294,355` chars (`~73,589` tokens)** | **`1.95×` less tool payload** |
| **Cumulative Input Tokens** | `16,098,025` est. (`25,878,074` API tokens) | **`3,910,897` est. (`6,000,146` API tokens)** | **`4.12×` fewer input tokens (`4.31×` API tokens)** |
| **API Cost w/ 90% Cache Discount** | `$1.91` Flash (`$7.51` Pro) | **`$0.65` Flash (`$2.52` Pro)** | **`2.94×` cheaper** |
| **Symbol Recall (`331` Verified Items)** | `92.7%` (`307 / 331`; `24` Task 4a caller errors) | **`100%` (`331 / 331` across Tasks 1 to 4)** | **`+24` caller edges resolved via AST** |

---

## 4. Methodological & Impartiality Notes

1. **Controlled Baseline Design (Pure `rg` + `view_file` vs Scripted AST Baselines)**:
   Unlike Case Study 01 and Case Study 02 — where baseline agents were granted `write_to_file` and wrote custom TypeScript/Python AST parsers mid-session — neither subagent in Case Study 05 was granted `write_to_file`, and `prompts/baseline-agent.md` explicitly disallowed custom AST/graph-walking scripts. This isolates the native interactive workflow (`rg` search + `view_file` reading) that most production coding agents execute when no pre-indexed code graph is attached.
2. **Why `CodeExplorer` Finished in `92` Turns (`184` Steps) vs `ripgrep`'s `193` Turns (`389` Steps)**:
   The `CodeExplorer` MCP agent executed `54` MCP tool calls (`51` `execute_custom_read_cypher` queries, `2` `get_taxonomy` calls, and `1` `get_workspace_status` call) plus `36` targeted `view_file` calls (`31` C# source files for attribute/edge verification, `1` MCP tool schema, and `4` spilled query outputs). By contrast, `ripgrep` issued `74` `rg` searches and `117` C# `view_file` calls, ingesting `573 KB` (`~143K` tokens) of raw source text and burning `4.12×` more cumulative input tokens (`16.10M` vs `3.91M`; `25.88M` vs `6.00M` API input tokens).
3. **Source-Verified Output Audit (`OrchardCore @ 05ad2d9a`, `331` Ground-Truth Items)**:
   - **`CodeExplorer` MCP (`add5de69`) — `331 / 331` (`100%`)**: Extracted `100%` of ground-truth classes and methods across Tasks 1–4 (`88` state-mutating actions = `67` calling vs `21` missing `AuthorizeAsync`; `72` `ISession` boundary pairs across `25` classes in `13` modules; `16` concrete + `6` base `PublishingAsync`/`PublishedAsync` handler methods; `118` `IContentManager` caller-method edges across `19` projects; and `37` over-coupled `DEPENDS_ON` projects, plus `OrchardCore.ContentManagement` self-implementation = `38`).
   - **`ripgrep` (`b933d749`) — `307 / 331` (`92.7%`)**: Matched `100%` of items on Tasks 1 (`88/88`), 2 (`72/72` pairs across `25` listed classes, writing `22` in its header sum), 3 (`16/16` concrete handlers), and 4b (`37/37`), but on **Task 4a (`94 / 118`)** suffered from a classic lexical `grep` blind spot: because `rg` prints call-site lines without enclosing AST method signatures, the agent guessed non-existent method names instead of reading every surrounding method header (`CreateEndpoint.AddCreateContentEndpoint` for `HandleAsync`, `SaveContentItemsStep.ExecuteAsync` for `SaveAsync`, `MenuShapes.BuildMenuShapeAsync` / `CreateSafeLinkContentItem` for `DiscoverAsync` / `ShouldCreateAsync`, `TermShapes.BuildTermShapeAsync` for `DiscoverAsync`, `ContainedQueryObjectType.ResolveAsync` and `TaxonomyFieldQueryObjectType.ResolveAsync` for constructors, and `MetaWeblogHandler.MetaWeblogGetUserBlogsAsync` in place of 5 distinct `MetaWeblog*` methods, plus missing `AdminController.EditInternalAsync` and `AdminController.CreateAndPublishPOST`).

---

## 5. Verbatim Artifacts

- **Task Prompt**: [`prompts/task-prompt.md`](./prompts/task-prompt.md)
- **System Prompts**: [`prompts/graph-agent.md`](./prompts/graph-agent.md) | [`prompts/baseline-agent.md`](./prompts/baseline-agent.md)
- **Agent Outputs**: [`results/graph-agent-output.md`](./results/graph-agent-output.md) | [`results/baseline-agent-output.md`](./results/baseline-agent-output.md)
- **Machine-Readable Telemetry**: [`results/metrics.json`](./results/metrics.json)
