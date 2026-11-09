# CodeExplorer Benchmarks: Agent Workflows & Graph Engine Comparisons

We run two distinct types of benchmarks:
1. **End-to-End AI Agent Workflows (`Cases 01, 02 & 05`)** — measuring how much time, tokens, and missed dependencies a graph saves an LLM on real multi-hop architectural tasks compared to `codebase-memory-mcp` (`CBM`) or `ripgrep`.
2. **Graph Engine Head-to-Head (`Cases 03 & 04`)** — measuring cold scan speed, RAM footprint, MCP JSON-RPC query latency, and extraction recall across `371` source-verified items without an LLM in the loop.

Live interactive versions of all 5 reports are published at **[https://code-explorer.app/benchmarks.html](https://code-explorer.app/benchmarks.html)**.

---

## 1. End-to-End AI Agent Workflows (`Cases 01, 02 & 05`)

Two identical AI agents receive the exact same multi-hop architectural audit task simultaneously on the same commit. We measure how many turns, files, tokens, and seconds each tool requires to answer, and whether any production dependencies are missed.

| Case Study & Task | Repository Under Test | Compared Tools | Time & LLM Turns | Cumulative Input Tokens | Recall & Outcome |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **[Case Study 01: Public Monorepo Audit](./01-agent-audit-ce-vs-cbm-on-twenty/README.md)**<br>DI fan-in, inheritance, BullMQ queues & API-to-DB write lineage | `twentyhq/twenty`<br>`46` packages, `24,527` files, `2.49M` LOC (TS, NestJS, GraphQL, TypeORM, BullMQ) | `CodeExplorer`<br>vs<br>`CBM` | **`13.5×` faster**<br>`41s` vs `9m 15s`<br>`4` turns vs `28` turns | **`20.9×` fewer tokens**<br>`80,177` vs `1,678,913`<br>(`$0.04` vs `$0.23`) | **Native Graph: `99%` vs `3.4%`**<br>`378/381` from `ce` (`0` file reads) vs `13/381` from `CBM` (wrote `3` AST scripts + `10` file reads) |
| **[Case Study 02: 324-Service Enterprise Audit](./02-enterprise-microservices-deep-audit/README.md)**<br>Shared ORM tables, outbound HTTP fan-in, Pub/Sub & RabbitMQ topology | Enterprise Microservices Monorepo<br>`450` modules (`324` services, `13,782` files, TS, Go, SQL) | `CodeExplorer`<br>vs<br>`ripgrep` + Python | **`7.59×` faster**<br>`3m 56s` vs `29m 52s`<br>`23` turns vs `285` turns | **`43.70×` fewer tokens**<br>`393,466` vs `17,194,141`<br>(`$0.08` vs `$1.15`) | **`100%` vs `~82%`**<br>`0` missed services (`0` file reads) vs `9` RTB services missed (`114` file reads + `14` scripts) |
| **[Case Study 05: Modular .NET Monolith Audit](./05-polyglot-architecture-and-blast-radius/README.md)**<br>Cross-module C# interface hierarchies, DI call chains & YesSql indexes | `OrchardCMS/OrchardCore`<br>`[TBD ~180]` C# projects, `[TBD ~6,000]` files (.NET, ASP.NET Core, YesSql) | `CodeExplorer`<br>vs<br>`ripgrep` | **`[TBD ×]` faster**<br>`[TBD]` vs `[TBD]`<br>`[TBD]` turns vs `[TBD]` turns | **`[TBD ×]` fewer tokens**<br>`[TBD]` vs `[TBD]`<br>(`$[TBD]` vs `$[TBD]`) | **`[TBD %]` vs `[TBD %]`**<br>`[TBD]` file reads vs `[TBD]` file reads |

---

## 2. Deterministic Indexer & MCP Benchmarks (`Cases 03 & 04`)

No LLM in the loop. Both tools index the same commits of `twentyhq/twenty` (`24,527` files) and `immich-app/immich` (`3,205` files) from a cold state, and are evaluated by automated Python scripts against `371` source-verified items (`seed=42`).

| Case Study & Focus | Repositories Under Test | Compared Engines | Cold Indexing Speed & Footprint | Group A: Basic Code (`130` Items) | Group B: Queues, DB & SQL (`241` Items) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **[Case Study 03: `CodeExplorer` vs `codebase-memory-mcp`](./03-code-explorer-vs-codebase-memory-mcp/README.md)**<br>Cold scan speed, DB footprint, warm MCP stdio latency & recall | `twentyhq/twenty` (`24,527` files, `2.49M` LOC)<br>&<br>`immich-app/immich` (`3,205` files, `461k` LOC) | `CodeExplorer`<br>vs<br>`CBM` | **`immich`**: `ce` `17.60s` (`55.1 MB`) vs `CBM` `20.60s` (`282.0 MB`)<br>**`twenty`**: `CBM` `96.90s` (`842.5 MB`) vs `ce` `147.33s` (`784.0 MB`) | **`78%` vs `38%`**<br>`ce` `102/130` vs `CBM` `49/130`<br>(`7` wins, `1` tie, `0` losses) | **`97.5%` (`235/241`)**<br>vs<br>Not modeled in `CBM` (`0/241`) |
| **[Case Study 04: `CodeExplorer` vs `Graphify`](./04-code-explorer-vs-graphify/README.md)**<br>Monorepo indexing scalability, cold MCP init & recall | `twentyhq/twenty` (`24,527` files, `2.49M` LOC)<br>&<br>`immich-app/immich` (`3,205` files, `461k` LOC) | `CodeExplorer`<br>vs<br>`Graphify` | **`immich`**: `ce` `17.60s` vs `Graphify` `107.60s` (`6.11×` faster)<br>**`twenty`**: `ce` `147.33s` vs `Graphify` **DNF (`>45m` timeout)** | **`78%` vs `18%`**<br>`ce` `102/130` vs `Graphify` `23/130`<br>(On `immich`: `73%` vs `35%`, tie `20/20` on `C6`) | **`97.5%` (`235/241`)**<br>vs<br>Not modeled in `Graphify` (`0/241`) |

---

## Tested Participants & Versions

1. **CodeExplorer (`ce`)**: `v1.75.0` / `v1.75.1` (.NET 10, SQLite + OpenCypher, `36` MCP tools)
2. **codebase-memory-mcp (`CBM`)**: `v0.12.1` (`win32-x64` Go + SQLite binary, `17` MCP tools)
3. **Graphify (`graphifyy`)**: `v0.9.104` (Python 3.10, Tree-sitter + NetworkX `--code-only`, `10` MCP tools)
4. **Lexical CLI Baseline**: `ripgrep` (`rg v14.1.1`) + Python `3.10` + `view_file`
5. **LLM Model (Track 1)**: `Gemini 3.8 Flash` (`100%` symmetric system prompts)
6. **Pinned Target Commits**: `twentyhq/twenty @ b9ef7107` (Case 01), `twentyhq/twenty @ 12e8fae4` & `immich-app/immich @ 2153d22f` (Cases 03 & 04), `OrchardCMS/OrchardCore @ [TBD]` (Case 05)
