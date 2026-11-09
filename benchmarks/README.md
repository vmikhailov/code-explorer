# CodeExplorer Benchmarks: Making AI Coding Agents Production-Safe (`Graph` vs. `ripgrep`)

**AI agents see files, not architecture.** This directory contains reproducible, telemetry-backed benchmarks measuring how **CodeExplorer (`ce`)** gives autonomous AI coding agents (`Gemini Flash`) a live cross-service map of APIs, queues, and databases via MCP—compared to traditional CLI exploration (`ripgrep` (`rg`), `git grep`, custom Python scripts, and `view_file`).

---

## Why Benchmark Graph Intelligence Against `ripgrep`?

A recurring question in AI-assisted software engineering is:
> *"Why does an AI agent need a semantic codebase graph if it already has fast `ripgrep` and file reading tools?"*

While `ripgrep` works well for single-hop literal string matches in small projects, real-world software architecture tasks—such as **transitive type hierarchy resolution**, **multi-hop blast radius analysis**, **cross-service synchronous `SERVICE_CALL` fan-in**, **shared ORM database table coupling**, and **polyglot asynchronous messaging topology**—require traversing structural relationships across dozens or hundreds of services and files.

When an agent relies purely on `ripgrep` and file reading:
1. **Multi-Hop Indirection Requires Dozens of Iterative Searches**: Base interfaces, helper wrappers, and URL builders (`linkGenerator`) force the agent to run `30–140+` iterative `rg` commands and write ad-hoc scripts.
2. **Quadratic Input Token Multiplier**: Because every LLM turn is stateless, every kilobyte of `rg` output and `view_file` slices accumulated in Turn $t$ is re-sent as input tokens on Turn $t+1, t+2, \dots, T$.
3. **Incomplete Recall on Large Monorepos**: In a 324-service monorepo, string-based `rg` heuristics miss indirect callers (such as environment-injected host constants), whereas a pre-indexed semantic graph resolves 100% of relationships in a single query.

---

## Benchmark Summary

| Case Study & Codebase Scale | Speed & Turn Reduction (`Competitor` → `CodeExplorer`) | Token & Cost Savings w/ 90% Cache (`Competitor` → `CodeExplorer`) | Blast-Radius Completeness (`Competitor` → `CodeExplorer`) |
| :--- | :--- | :--- | :--- |
| **[01: Agent Audit `ce` vs. `CBM` on `twentyhq/twenty`](./01-agent-audit-ce-vs-cbm-on-twenty/README.md)**<br>`twenty` (`46` packages, `24,527` files, `2.49M` LOC — 100% open-source) | **`13.24x` faster** (`16m 46s` → `1m 16s`)<br>**`8.00x` fewer turns** (`56` → `7`; `CBM` agent abandoned `CBM` after Turn 5 & wrote `11` Python scripts) | **`16.46x` fewer tokens** (`4.41M` → `268K`)<br>**`3.52x` cheaper** (`$0.45` → `$0.13`; **`7.11x` cheaper input**) | **Native Graph:** `CBM` **`3.96%`** (`18/454`, `0` cross-file DI calls) vs `ce` **`100%`** (`454/454`)<br>**Final Audit:** `CBM` + Python missed `2` `JwtWrapperService` callers (`99.56%`) vs `ce` **`100%`** |
| **[02: Enterprise Microservices Deep Audit](./02-enterprise-microservices-deep-audit/README.md)**<br>`450` modules (`324` services, `13,782` files, TS + Go + SQL) | **`7.59x` faster** (`29m 52s` → `3m 56s`)<br>**`12.39x` fewer turns** (`285` → `23`) | **`43.70x` fewer tokens** (`17.19M` → `393K`)<br>**`14.07x` cheaper** (`$1.15` → `$0.08`) | **`Incomplete` → `Complete`**<br>(`9` services / `15+` callers missed (`~82%`) → **`0` missed (`100%`)**) |
| **[03: `CodeExplorer` (`ce`) vs. `codebase-memory-mcp` (`CBM`)](./03-code-explorer-vs-codebase-memory-mcp/README.md)**<br>`twenty` (`24,527` files, `2.49M` LOC) + `immich` (`3,205` files, `461k` LOC) | **Cold Indexing:** **`ce` faster on `immich`** (`17.6s` vs `20.6s`, `5.1x` smaller DB); **`CBM` `1.52x` faster on `twenty`** (`96.9s` vs `147.3s`, `ce` smaller DB `784 MB` vs `843 MB`) | **MCP Surface:** `ce` `36` tools vs `CBM` `17` tools (`ce` faster on taxonomy `1.9ms`, Cypher `4.3ms` & 1-turn callers; `CBM` faster on `twenty` outline/search) | **Common Ground Recall (`L1–L3`):** `ce` **`78`** (`102/130`) vs `CBM` **`38`** (`49/130`)<br>**`ce` Extended `L4–L5` Ontology:** **`97.5%`** (`235/241`) |
| **[04: `CodeExplorer` (`ce`) vs. `graphify` (`graphifyy`)](./04-code-explorer-vs-graphify/README.md)**<br>`twenty` (`24,527` files, `2.49M` LOC) + `immich` (`3,205` files, `461k` LOC) | **Cold Indexing:** **`ce` `6.1x` faster on `immich`** (`17.60s` vs `107.60s`) and **`>18.6x` faster on `twenty`** (`147.33s` vs **`DNF >45m`**) | **MCP Startup & Surface:** `ce` `6.1x` faster cold init (`599ms` / `64 MB` vs `3,683ms` / `163 MB`), faster schema & FTS5 search | **Common Ground Recall (`L1–L3`):** `ce` **`78`** (`102/130`) vs `graphify` **`18`** (`23/130`; `35` on `immich`)<br>**`ce` Extended `L4–L5` Ontology:** **`97.5%`** (`235/241`, not modeled in `graphify`) |
| **[05: Polyglot Architecture & Blast Radius](./05-polyglot-architecture-and-blast-radius/README.md)**<br>`16` projects, C# .NET 10 + TypeScript | **`2.35x` faster** (`5m 08s` → `2m 11s`)<br>**`1.97x` fewer turns** (`59` → `30`) | **`2.52x` fewer tokens** (`1.59M` → `633K`)<br>**`1.87x` cheaper** (`$0.14` → `$0.08`) | **`Complete` → `Complete`**<br>(`100%` — `0` missed callers) |

> **Why `82%` Recall in Impact Analysis Is Like Jumping 82% Across a Chasm**: In architectural refactoring and contract migration, blast-radius completeness is binary. If an agent audits a shared service contract or message topic and finds `82%` of callers—missing **9 production RTB microservices and 15+ caller functions** because their target URLs are injected via environment constants—deploying that change breaks 9 services in production. As repository scale grows from **16 projects** to **324 microservices**, CodeExplorer not only cuts time by **7.59x** (`29m 52s` → `3m 56s`) and cumulative input tokens by **43.70x** (`17.19M` → `393K`), but turns an unsafe, incomplete grep heuristic into a **100% complete architectural guarantee (`0` blind spots)**.

---

## Evaluation Methodology

Every benchmark in this suite is executed under strict **paired A/B isolation** (identical model `Gemini Flash`, identical prompt, simultaneous execution on the same commit) and evaluated directly from the runtime's `transcript_full.jsonl` logs:

1. **Time to Answer (`send_message`)**: Exact elapsed time from initial prompt dispatch to the `send_message` tool call delivering the final report.
2. **LLM Reasoning Turns & Tool Calls**: Number of model round-trips (`PLANNER_RESPONSE`) and tool invocations (`rg`, Python scripts, Cypher queries, and `view_file` reads).
3. **Compaction-Aware Cumulative Input Tokens**: Sum of active context tokens processed across all LLM turns ($\sum_{t=1}^T \text{ActiveContext}_t$), accounting for `CHECKPOINT` context resets and 90% prompt caching discounts.
4. **Source-Verified Recall & Precision (`%`)**: Manual verification of reported classes, methods, database tables, topics, and cross-service calls directly against the repository source code.
