# CodeExplorer Benchmarks: Real-Life Feature Coding, Agent Audits & Graph Engines

We run three complementary benchmark tracks across 8 reproducible case studies:
1. **Real-Life Feature Coding & Architectural Guardrails (`Cases 08, 07 & 06`)** — measuring how fast an AI agent implements new production features + Jest unit tests on a `2.49M` LOC monorepo (`twentyhq/twenty`), whether it catches and self-heals hidden domain-boundary violations copied from legacy code (`CodeExplorer` vs `codebase-memory-mcp` across a 4-task, 12-run ablation matrix), and how deterministic `0`-token pre-commit gates (`ce gate check` & SHA-256 ratchet) enforce architecture on `argoproj/argo-cd`.
2. **Autonomous Multi-Hop Architectural Audits (`Cases 01, 02 & 05`)** — measuring how much time, tokens, and missed dependencies a semantic graph saves an LLM on read-only blast-radius and data-lineage audits compared to `codebase-memory-mcp` (`CBM`) or `ripgrep`.
3. **Graph Engine Head-to-Head (`Cases 03 & 04`)** — measuring cold scan speed, RAM footprint, MCP JSON-RPC query latency, and extraction recall across `371` source-verified items without an LLM in the loop.

Live interactive versions of all 8 reports are published at **[https://code-explorer.app/benchmarks](https://code-explorer.app/benchmarks)**.

---

## 1. Real-Life Feature Coding & Architectural Guardrails (`Cases 08, 07 & 06`)

Everyday software engineering is not just read-only auditing: developers ask AI agents to implement new endpoints, service orchestration, and unit tests in brownfield repositories where existing code contains legacy shortcuts. We measure implementation speed, unit test passage, and whether the agent ships or self-heals architectural boundary leaks before completing the task.

<!-- BEGIN:md.catalog_reallife_table -->
| Case Study & Task | Repository Under Test | Compared Tools | Time & LLM Turns | Cumulative Input Tokens | Architectural Compliance & Outcome |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **[Case Study 09: Zero-Spoiler Coding Benchmark (One-Shot Context Pack vs Bare Agent)](./09-zero-spoiler-context-pack-vs-bare-agent/README.md)**<br>3 zero-spoiler bug & feature tickets across 12 runs (`apiKey` revocation lifecycle, `application` public-domain DNS teardown, `subdomain` collision guard) with 100% symmetric architectural rules, comparing **`Bare Agent` (`rg` + `read_file`)** vs **`CodeExplorer 1-Shot Context Pack`** (Iter 1: without `@Module` graph; Iter 2: with `@Module` cycle safety) | `twentyhq/twenty`<br>`46` packages, `36,543` indexed files (TS, NestJS, GraphQL, TypeORM) | `CodeExplorer`<br>vs<br>`ripgrep` | **`1.43×` faster (`2.14×` vs Iter 1)**<br>`19m 38s` vs `28m 02s`<br>`59` turns vs `84` turns | **`2.24×` fewer tokens (`3.07×` vs Iter 1)**<br>`3,080,276` vs `6,897,615`<br>(`55` vs `104` file reads, `20` vs `41` cmds) | **`15/15` Jest specs & `21/21` SIG gates (`0` new)**<br>Pre-computed `@Module` cycle verdicts (`SAFE_DIRECT_IMPORT`) eliminated 20+ turns of manual `*.module.ts` reads (`0` `rg` calls in Task 2) |
| **[Case Study 08: 4-Task Brownfield Architecture & Feature Suite](./08-multi-task-architecture-suite-on-twenty/README.md)**<br>4 backend features & unit test suites across 4 modules (`resetTwoFactorAuthenticationMethodsForUser`, `triggerReconnectAccountBanner`, `suspendWorkspaceAdminPanel`, `isSubdomainAvailable`) comparing **`CBM`** vs **`CodeExplorer Phase 1 (Navigation Only)`** vs **`Phase 2 (Navigation + Gates)`** | `twentyhq/twenty`<br>`46` packages, `24,527` files, `2.49M` LOC (TS, NestJS, GraphQL, TypeORM) | `CodeExplorer`<br>vs<br>`CBM` | **`1.29×` faster suite total**<br>`34m 14s` vs `44m 6s`<br>`125` turns vs `127` turns | `9,745,267` vs `10,030,169`<br>(`$0.48` vs `$0.48`, `92` vs `96` file reads) | **`0/4` vs `4/4` violated tasks (`100%` caught)**<br>Navigation alone copied adjacent `[TOL]` debt in `4/4` tasks; `ce gate check` fixed `4/4` in `1` iter (`0` tampering, `14/14` unit tests pass for both via `run-acceptance-tests.mjs`) |
| **[Case Study 07: Real-Life Feature Implementation & Self-Healing](./07-agent-feature-ce-vs-cbm-on-twenty/README.md)**<br>Implement `suspendWorkspaceAdminPanel` GraphQL mutation, service orchestration & unit tests on `twentyhq/twenty` (4 repo-wide architectural gates, zero rule hints) | `twentyhq/twenty`<br>`46` packages, `24,527` files, `2.49M` LOC (TS, NestJS, GraphQL, TypeORM, BullMQ) | `CodeExplorer`<br>vs<br>`CBM` | **`1.57×` faster end-to-end**<br>`12m 59s` vs `20m 25s` (`10m 3s` vs `15m 17s` initial draft, `1.52×` faster)<br>`43` turns vs `54` turns | **`1.25×` fewer est. tokens**<br>`3,387,918` vs `4,230,784`<br>(`31` vs `43` file reads) | **`4/4 PASS` vs `1 new` violation (`SIG-TWENTY-003`)**<br>Both passed unit tests (`4/4` vs `3/3`) & `oxlint`; pre-configured gate caught copied legacy coupling from `workspace.service.ts:989` (`[TOL]` -> `[NEW]`) & `ce` refactored in `1` turn |
| **[Case Study 06: Pre-Commit Architectural Guard (Showcase)](./06-precommit-architectural-guard/README.md)**<br>6 invariant gates + 4-phase brownfield SHA-256 ratchet lifecycle (Day 0 audit, line-shift-immune baseline freeze, PR regression block & monotonic debt tightening) | `argoproj/argo-cd`<br>`18` subsystems, `4,216` files (Go, TypeScript, K8s) | `CodeExplorer` (`ce gate check` & `ratchet`) | **`422 ms`** Day 0 check<br>**`183–356 ms`** ratcheted checks | **`0` tokens**<br>(**`$0.00`**) | **4-phase deterministic lifecycle**<br>Freezes `17` legacy errors + `4` warnings (`Exit 0`), blocks `1 new` PR leak (`Exit 1`), tightens ceiling |
<!-- END:md.catalog_reallife_table -->

---

## 2. Autonomous Multi-Hop Architectural Audits (`Cases 01, 02 & 05`)

Two identical AI agents receive the exact same multi-hop architectural audit task simultaneously on the same commit. We measure how many turns, files, tokens, and seconds each tool requires to answer, and whether any production dependencies are missed.

<!-- BEGIN:md.catalog_agent_table -->
| Case Study & Task | Repository Under Test | Compared Tools | Time & LLM Turns | Cumulative Input Tokens | Recall & Outcome |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **[Case Study 01: Public Monorepo Audit](./01-agent-audit-ce-vs-cbm-on-twenty/README.md)**<br>DI fan-in, inheritance, BullMQ queues & API-to-DB write lineage | `twentyhq/twenty`<br>`46` packages, `24,527` files, `2.49M` LOC (TS, NestJS, GraphQL, TypeORM, BullMQ) | `CodeExplorer`<br>vs<br>`CBM` | **`6.0×` faster**<br>`1m 33s` vs `9m 15s`<br>`7` turns vs `28` turns | **`7.0×` fewer tokens**<br>`241,528` vs `1,678,913`<br>(`$0.06` vs `$0.23`) | **Native Graph: `100%` vs `3.4%`**<br>`381/381` from `ce` (`0` file reads) vs `13/381` from `CBM` (wrote `3` AST scripts + `10` file reads) |
| **[Case Study 02: 324-Service Enterprise Audit](./02-enterprise-microservices-deep-audit/README.md)**<br>Shared ORM tables, outbound HTTP fan-in, Pub/Sub & RabbitMQ topology (zero schema hints) | Enterprise Multi-Repo Workspace<br>`450` independent service/library repos (`324` services, `13,782` files, TS, Go, SQL) | `CodeExplorer`<br>vs<br>`ripgrep` + Python | **`7.00×` faster**<br>`4m 16s` vs `29m 52s`<br>`18` turns vs `285` turns | **`25.07×` fewer tokens**<br>`685,738` vs `17,194,141`<br>(`$0.10` vs `$1.15`, `11.50×` cheaper) | **`100%` vs `~82%`**<br>`0` missed services (`16` file reads, `0` scripts) vs `9` RTB services missed (`114` file reads + `14` scripts) |
| **[Case Study 05: Modular .NET Monolith Audit](./05-polyglot-architecture-and-blast-radius/README.md)**<br>AppSec `AuthorizeAsync` audit, `YesSql.ISession` layer violations, `PublishedAsync` side-effects & `IContentManager` blast radius | `OrchardCMS/OrchardCore`<br>`334` projects, `7,837` files (.NET, ASP.NET Core, YesSql) | `CodeExplorer`<br>vs<br>`ripgrep` + `view_file` | **`2.51×` faster**<br>`4m 25s` vs `11m 04s`<br>`92` turns (`184` steps) vs `193` turns (`389` steps) | **`4.12×` fewer tokens** (`1.95×` less tool output)<br>`3,910,897` vs `16,098,025`<br>(`$0.65` vs `$1.91`, `2.94×` cheaper) | **`100%` vs `~93%`** (`331/331` vs `307/331`)<br>`36` file reads vs `117` file reads (`3.25×` fewer) |
<!-- END:md.catalog_agent_table -->

---

## 3. Deterministic Indexer & MCP Benchmarks (`Cases 03 & 04`)

No LLM in the loop. Both tools index the same commits of `twentyhq/twenty` (`24,527` files) and `immich-app/immich` (`3,205` files) from a cold state, and are evaluated by automated Python scripts against `371` source-verified items (`seed=42`).

| Case Study & Focus | Repositories Under Test | Compared Engines | Cold Indexing Speed & Footprint | Group A: Basic Code (`130` Items) | Group B: Queues, DB & SQL (`241` Items) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **[Case Study 03: `CodeExplorer` vs `codebase-memory-mcp`](./03-code-explorer-vs-codebase-memory-mcp/README.md)**<br>Cold scan speed, DB footprint, warm MCP stdio latency & recall | `twentyhq/twenty` (`24,527` files, `2.49M` LOC)<br>&<br>`immich-app/immich` (`3,205` files, `461k` LOC) | `CodeExplorer`<br>vs<br>`CBM` | **`immich`**: `ce` `17.60s` (`55.1 MB`) vs `CBM` `20.60s` (`282.0 MB`)<br>**`twenty`**: `CBM` `96.90s` (`842.5 MB`) vs `ce` `147.33s` (`784.0 MB`) | **`78%` vs `38%`**<br>`ce` `102/130` vs `CBM` `49/130`<br>(`7` wins, `1` tie, `0` losses) | **`97.5%` (`235/241`)**<br>vs<br>Not modeled in `CBM` (`0/241`) |
| **[Case Study 04: `CodeExplorer` vs `Graphify`](./04-code-explorer-vs-graphify/README.md)**<br>Monorepo indexing scalability, cold MCP init & recall | `twentyhq/twenty` (`24,527` files, `2.49M` LOC)<br>&<br>`immich-app/immich` (`3,205` files, `461k` LOC) | `CodeExplorer`<br>vs<br>`Graphify` | **`immich`**: `ce` `17.60s` vs `Graphify` `107.60s` (`6.11×` faster)<br>**`twenty`**: `ce` `147.33s` vs `Graphify` **DNF (`>45m` timeout)** | **`78%` vs `18%`**<br>`ce` `102/130` vs `Graphify` `23/130`<br>(On `immich`: `73%` vs `35%`, tie `20/20` on `C6`) | **`97.5%` (`235/241`)**<br>vs<br>Not modeled in `Graphify` (`0/241`) |

---

## Tested Participants & Versions

<!-- BEGIN:md.catalog_participants -->
1. **CodeExplorer (`ce`)**: `v1.75.0 to v1.87.2` (.NET 10 & .NET 11, SQLite + OpenCypher, `36` MCP tools)
2. **codebase-memory-mcp (`CBM`)**: `v0.12.1` (`win32-x64` Go + SQLite binary, `17` MCP tools)
3. **Graphify (`graphifyy`)**: `v0.9.104` (Python 3.10, Tree-sitter + NetworkX `--code-only`, `10` MCP tools)
4. **Lexical CLI Baseline**: `ripgrep` (`rg v14.1.1`) + Python `3.10` (Case 02) + `view_file`
5. **LLM Model (Tracks 1 & 2)**: `Gemini 3.8 Flash` (symmetric system prompts across paired runs)
6. **Pinned Target Commits**: `twentyhq/twenty @ b9ef7107` (Case 01), `twentyhq/twenty @ 12e8fae4` (Cases 03, 04, 07 & 08), `immich-app/immich @ 2153d22f` (Cases 03 & 04), `OrchardCMS/OrchardCore @ 05ad2d9a` (Case 05), `argoproj/argo-cd @ 6d689fa3` (Case 06)
<!-- END:md.catalog_participants -->
