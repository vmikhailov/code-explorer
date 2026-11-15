# CodeExplorer Benchmarks: Real-Life Feature Coding, Agent Audits & Graph Engines

We run three complementary benchmark tracks across 7 reproducible case studies:
1. **Real-Life Feature Coding & Architectural Guardrails (`Cases 07 & 06`)** — measuring how fast an AI agent implements a new production feature + Jest unit tests on a `2.49M` LOC monorepo (`twentyhq/twenty`), whether it catches and self-heals a hidden domain-boundary violation copied from legacy code (`CodeExplorer` vs `codebase-memory-mcp`), and how deterministic `0`-token pre-commit gates (`ce gate check` & SHA-256 ratchet) enforce architecture on `argoproj/argo-cd`.
2. **Autonomous Multi-Hop Architectural Audits (`Cases 01, 02 & 05`)** — measuring how much time, tokens, and missed dependencies a semantic graph saves an LLM on read-only blast-radius and data-lineage audits compared to `codebase-memory-mcp` (`CBM`) or `ripgrep`.
3. **Graph Engine Head-to-Head (`Cases 03 & 04`)** — measuring cold scan speed, RAM footprint, MCP JSON-RPC query latency, and extraction recall across `371` source-verified items without an LLM in the loop.

Live interactive versions of all 7 reports are published at **[https://code-explorer.app/benchmarks](https://code-explorer.app/benchmarks)**.

---

## 1. Real-Life Feature Coding & Architectural Guardrails (`Cases 07 & 06`)

Everyday software engineering is not just read-only auditing: developers ask AI agents to implement new endpoints, service orchestration, and unit tests in brownfield repositories where existing code contains legacy shortcuts. We measure implementation speed, unit test passage, and whether the agent ships or self-heals architectural boundary leaks before completing the task.

<!-- BEGIN:md.catalog_reallife_table -->
<!-- END:md.catalog_reallife_table -->

---

## 2. Autonomous Multi-Hop Architectural Audits (`Cases 01, 02 & 05`)

Two identical AI agents receive the exact same multi-hop architectural audit task simultaneously on the same commit. We measure how many turns, files, tokens, and seconds each tool requires to answer, and whether any production dependencies are missed.

<!-- BEGIN:md.catalog_agent_table -->
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
6. **Pinned Target Commits**: `twentyhq/twenty @ b9ef7107` (Case 01), `twentyhq/twenty @ 12e8fae4` (Cases 03, 04 & 07), `immich-app/immich @ 2153d22f` (Cases 03 & 04), `OrchardCMS/OrchardCore @ 05ad2d9a` (Case 05), `argoproj/argo-cd @ 6d689fa3` (Case 06)
<!-- END:md.catalog_participants -->
