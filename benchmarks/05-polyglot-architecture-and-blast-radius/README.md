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
   Identify all `16` concrete `PublishingAsync` / `PublishedAsync` content handler methods across `15` classes (plus `10` abstract base/adapter methods in `OrchardCore.ContentManagement.Abstractions`) and trace their `1..2` hop downstream calls into database/index stores, `ITagCache.RemoveTagAsync`, `IWorkflowManager.TriggerEventAsync`, `ShellScope.AddDeferredTask`, and `ILiquidTemplateManager`.
4. **Task 4 — Breaking-Change Blast Radius & Over-Coupled Projects (`IContentManager`)**:
   For `IContentManager.GetAsync`, `CreateAsync`, and `PublishAsync`:
   - **(4a)** Identify all `20` non-test projects (`19` via `IContentManager` interface + `OrchardCore.ContentManagement` via `DefaultContentManager`) and every caller method (`CallerClass.CallerMethod`), including calls resolved via generic DI service locator calls (`sp.GetRequiredService<IContentManager>()`).
   - **(4b)** Identify all `38` over-coupled C# projects (`37` excluding `OrchardCore.ContentManagement`) that declare a direct `DEPENDS_ON` project reference on `OrchardCore.ContentManagement.Abstractions` yet have `0` calls to `GetAsync`, `CreateAsync`, or `PublishAsync`.

---

## 3. Execution Telemetry Scorecard

Measured from paired A/B agent runs on `OrchardCMS/OrchardCore @ 05ad2d9a` ([`results/metrics.json`](./results/metrics.json)):

| Metric | `ripgrep` + `view_file` (`af28679a`) | `CodeExplorer` MCP (`3882e500`) | Delta |
| :--- | :---: | :---: | :---: |
| **Time to Answer (`send_message`)** | `889 s` (`14 min 49 sec`) / `1,091 s` wall | **`384 s` (`6 min 24 sec`) / `531 s` wall** | **`2.32×` faster (`2.05×` wall)** |
| **LLM Turns, Steps & Compactions** | `38` turns / `227` steps (`2` compactions) | **`15` turns / `61` steps (`0` compactions)** | **`2.53×` fewer turns (`3.72×` fewer steps)** |
| **Total Tool Calls & File Reads** | `186` (`22` `rg` + `163` `view_file`, `1` `send_message`) | **`45` (`29` `mcp` + `15` `view_file`, `1` `send_message`)** | **`4.13×` fewer calls (`10.87×` fewer reads)** |
| **Raw Tool Output Ingested** | `1,625,077` chars (`~406,269` tokens) | **`270,470` chars (`~67,618` tokens)** | **`6.01×` less tool payload** |
| **Cumulative Input Tokens** | `3,197,058` est. (`4,089,567` API tokens) | **`651,686` est. (`884,342` API tokens)** | **`4.91×` fewer input tokens (`4.34×` uncached)** |
| **API Cost w/ 90% Cache Discount** | `$0.65` Flash (`$2.47` Pro) | **`$0.23` Flash (`$0.83` Pro)** | **`2.83×` cheaper** |
| **Symbol Recall & Context Health** | `100%` symbol recall (`2` context compactions) | **`100%` symbol recall (single uncompacted window)** | **`0` compactions vs `2` compactions** |

---

## 4. Methodological & Impartiality Notes

1. **Controlled Baseline Design (Pure `rg` + `view_file` vs Scripted AST Baselines)**:
   Unlike Case Study 01 and Case Study 02 — where baseline agents were granted `write_to_file` and wrote custom TypeScript/Python AST parsers mid-session — neither subagent in Case Study 05 was granted `write_to_file`, and `prompts/baseline-agent.md` explicitly disallowed custom AST/graph-walking scripts. This isolates the native interactive workflow (`rg` search + `view_file` reading) that most production coding agents execute when no pre-indexed code graph is attached.
2. **Why `CodeExplorer` Finished in `15` Turns (`61` Steps, `0` Compactions) vs `ripgrep`'s `38` Turns (`227` Steps, `2` Compactions)**:
   The `CodeExplorer` MCP agent executed `22` parallelized OpenCypher queries (`execute_custom_read_cypher`), `1` `get_taxonomy` call, `1` `list_resources` call, and `5` targeted `fetch_code_snippets` calls — with **`0` direct repository source file reads via `view_file`** (its `15` `view_file` calls read only `4` lazy MCP tool schemas and `11` spilled query outputs). By contrast, `ripgrep` issued `22` `rg` searches and `163` `view_file` calls (`151` repository C# source files + `12` spilled `rg` outputs), ingesting `1.63 MB` (`~406K` tokens) of raw source text, overflowing the active context window twice (`Turn 20` and `Turn 31`), and burning `4.62×` more API input tokens (`4,089,567` vs `884,342`).
3. **Source-Verified Output Audit (`OrchardCore @ 05ad2d9a`)**:
   - **`CodeExplorer` MCP (`3882e500`)**: Extracted `100%` of ground-truth classes and methods across Tasks 1–4 in a single uncompacted context window (`88` state-mutating actions = `67` calling vs `21` missing `AuthorizeAsync`; `25` `ISession`-violating classes across `13` modules; `16` concrete + `6` base `PublishingAsync`/`PublishedAsync` handler methods across `11` projects; `19` `IContentManager` caller projects + `38` over-coupled `DEPENDS_ON` projects).
   - **`ripgrep` (`af28679a`)**: Also reached `100%` ground-truth symbol recall across Tasks 1–4 by manually reading `151` C# source files across `38` turns, isolating pure execution speed (`2.32×` faster answer), context efficiency (`4.91×` fewer cumulative input tokens, `6.01×` less tool output), and API cost (`2.83×` cheaper).

---

## 5. Verbatim Artifacts

- **Task Prompt**: [`prompts/task-prompt.md`](./prompts/task-prompt.md)
- **System Prompts**: [`prompts/graph-agent.md`](./prompts/graph-agent.md) | [`prompts/baseline-agent.md`](./prompts/baseline-agent.md)
- **Agent Outputs**: [`results/graph-agent-output.md`](./results/graph-agent-output.md) | [`results/baseline-agent-output.md`](./results/baseline-agent-output.md)
- **Machine-Readable Telemetry**: [`results/metrics.json`](./results/metrics.json)
