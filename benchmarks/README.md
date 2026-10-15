# CodeExplorer Benchmarks: Architecture Graph vs. Ripgrep / File Navigation

This directory contains reproducible, telemetry-backed benchmarks measuring the impact of **CodeExplorer's OpenCypher Architecture Graph** on autonomous AI coding agents compared to traditional lexical exploration (`ripgrep` (`rg`), `git grep`, custom Python regex scripts, and `view_file`).

---

## Why Benchmark Graph Intelligence for AI Agents?

A recurring question in AI-assisted software engineering is:
> *"Why does an AI agent need a semantic codebase graph if it already has fast `ripgrep` and file reading tools?"*

While lexical search (`rg`) works well for single-hop exact string matches in small projects, real-world software architecture tasks—such as **transitive type hierarchy resolution**, **multi-hop blast radius analysis**, **cross-service synchronous `SERVICE_CALL` fan-in**, **shared ORM database table coupling**, and **polyglot asynchronous messaging topology**—require traversing structural relationships across dozens or hundreds of services and files.

When an agent relies purely on `ripgrep` and file reading:
1. **Multi-Hop Indirection Requires Dozens of Iterative Searches**: Base interfaces, helper wrappers, and URL builders (`linkGenerator`) force the agent to run `50–100+` iterative `rg` commands and write ad-hoc regex scripts.
2. **Quadratic Input Token Multiplier**: Because every LLM turn is stateless, every kilobyte of `rg` output and `view_file` slices accumulated in Turn $t$ is re-sent as input tokens on Turn $t+1, t+2, \dots, T$.
3. **Incomplete Recall on Large Monorepos**: In a 324-service monorepo, string-based `rg` heuristics miss indirect callers (such as inherited client wrappers or frontend stream resources), whereas a pre-indexed semantic graph returns 100% of relationships in a single query.

---

## Benchmark Suite Index

| Case Study | Target Codebase & Scale | Baseline Configuration & Model | Time to Answer (`Baseline` → `Graph`) | LLM Turns (`Baseline` → `Graph`) | Tool Calls (`Baseline` → `Graph`) | Cumulative Input Tokens (`Baseline` → `Graph`) | API Cost w/ 90% Cache (`Baseline` → `Graph`) | Architectural Recall (`Baseline` → `Graph`) |
| :--- | :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **[01 (Run 1B): Polyglot Architecture & Blast Radius](./01-polyglot-architecture-and-blast-radius/README.md)** | `code-explorer` (`16` projects, C# .NET 10 + TypeScript) | `ripgrep` (`rg`) + `git grep` + `view_file` (`Gemini 3.8 Flash`) | `5m 08s` → **`2m 43s`** (**1.89x faster**) | `59` → **`36`** (**1.64x fewer**) | `57` → **`35`** (**1.63x fewer**) | `1.59M` → **`831K`** (**1.92x fewer**) | `$0.142` → **`$0.085`** (**1.67x cheaper**) | **100%** → **100%** |
| **[01 (Run 1A): Polyglot Architecture & Blast Radius](./01-polyglot-architecture-and-blast-radius/README.md)** | `code-explorer` (`16` projects, C# .NET 10 + TypeScript) | `view_file`-only navigation, no shell (`Gemini 4 Argon`) | `15m 01s` → **`2m 37s`** (**5.74x faster**) | `51` → **`16`** (**3.19x fewer**) | `248` → **`28`** (**8.86x fewer**) | `3.47M` actual (`15.32M` uncomp.) → **`379K`** (**9.16x / 40.4x fewer**) | `$1.726` → **`$0.220`** (**7.85x cheaper**) | **100%** → **100%** |
| **[02: Enterprise Microservices Deep Audit](./02-enterprise-microservices-deep-audit/README.md)** | **Enterprise Distributed Platform** (`450` modules: `324` services, `93` libs, `13,782` files, TS/NestJS + Go + SQL) | `ripgrep` (`rg`) + **14 custom Python scripts** + `view_file` (`Gemini 3.8 Flash`) | `29m 52s` → **`10m 30s`** (**2.84x faster**, `-19m 22s`) | `285` → **`89`** (**3.20x fewer**) | `284` → **`88`** (**3.23x fewer**) | `17.19M` actual (`37.09M` uncomp.) → **`4.61M`** (**3.73x / 8.04x fewer**) | `$1.154` → **`$0.338`** (**3.41x cheaper**) | **`~82%` (after `1` compaction)** → **100% Complete** |

---

## Evaluation Dimensions

Every benchmark in this suite is executed under strict **paired A/B isolation** (identical model, identical prompt, simultaneous execution on the same commit) and evaluated across six objective dimensions parsed directly from the runtime's `transcript_full.jsonl` logs:

1. **Time to Answer (`send_message`) & Total Wall-Clock Duration (`seconds`)**: Exact elapsed time from initial prompt dispatch to the `send_message` tool call delivering the final report, plus total session teardown time.
2. **LLM Reasoning Turns & Tool Calls**: Number of model round-trips (`PLANNER_RESPONSE`) and tool invocations required.
3. **Raw Tool Output Ingested (`chars` / `tokens`)**: Total volume of tool output payloads injected into the agent's context window.
4. **Compaction-Aware Active Context (`CHECKPOINT` handling)**: Exact tracking of active context size on every turn, resetting whenever a `CHECKPOINT` compaction event occurs.
5. **Cumulative Input Payload (`tokens`)**: Sum of active context tokens processed across all LLM turns ($\sum_{t=1}^T \text{ActiveContext}_t$), which governs API cost and time-to-first-token (TTFT) latency.
6. **Ground-Truth Completeness & Precision (`%`)**: Verification of every reported class name, method name, database table, topic, and cross-service call against AST/graph ground truth.
