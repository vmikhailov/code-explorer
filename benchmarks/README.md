# CodeExplorer Benchmarks: Architecture Graph vs. Ripgrep Navigation

This directory contains reproducible, telemetry-backed benchmarks measuring the impact of **CodeExplorer's OpenCypher Architecture Graph** on autonomous AI coding agents (`Gemini 3.8 Flash`) compared to traditional CLI exploration (`ripgrep` (`rg`), `git grep`, custom Python scripts, and `view_file`).

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

| Case Study & Codebase Scale | Speed & Turn Reduction (`Ripgrep` → `Graph`) | Token & Cost Savings w/ 90% Cache (`Ripgrep` → `Graph`) | Blast-Radius Completeness (`Ripgrep` → `Graph`) |
| :--- | :--- | :--- | :--- |
| **[01: Polyglot Architecture & Blast Radius](./01-polyglot-architecture-and-blast-radius/README.md)**<br>`16` projects, C# .NET 10 + TypeScript | **`1.89x` faster** (`5m 08s` → `2m 43s`)<br>**`1.64x` fewer turns** (`59` → `36`) | **`1.92x` fewer tokens** (`1.59M` → `831K`)<br>**`1.67x` cheaper** (`$0.142` → `$0.085`) | **`Complete` → `Complete`**<br>(`100%` — `0` missed callers) |
| **[02: Enterprise Microservices Deep Audit](./02-enterprise-microservices-deep-audit/README.md)**<br>`450` modules (`324` services, `13,782` files, TS + Go + SQL) | **`2.84x` faster** (`29m 52s` → `10m 30s`)<br>**`3.20x` fewer turns** (`285` → `89`) | **`3.73x` fewer tokens** (`17.19M` → `4.61M`)<br>**`3.41x` cheaper** (`$1.154` → `$0.338`) | **`Incomplete` → `Complete`**<br>(`9` services / `15+` callers missed (`~82%`) → **`0` missed (`100%`)**) |

> **Why `82%` Recall in Impact Analysis Is Like Jumping 82% Across a Chasm**: In architectural refactoring and contract migration, blast-radius completeness is binary. If an agent audits a shared service contract or message topic and finds `82%` of callers—missing **9 production RTB microservices and 15+ caller functions** because their target URLs are injected via environment constants—deploying that change breaks 9 services in production. As repository scale grows from **16 projects** to **324 microservices**, CodeExplorer not only cuts time by **2.84x** and tokens by **3.73x**, but turns an unsafe, incomplete grep heuristic into a **100% complete architectural guarantee (`0` blind spots)**.

---

## Evaluation Methodology

Every benchmark in this suite is executed under strict **paired A/B isolation** (identical model `Gemini 3.8 Flash`, identical prompt, simultaneous execution on the same commit) and evaluated directly from the runtime's `transcript_full.jsonl` logs:

1. **Time to Answer (`send_message`)**: Exact elapsed time from initial prompt dispatch to the `send_message` tool call delivering the final report.
2. **LLM Reasoning Turns & Tool Calls**: Number of model round-trips (`PLANNER_RESPONSE`) and tool invocations (`rg`, Python scripts, Cypher queries, and `view_file` reads).
3. **Compaction-Aware Cumulative Input Tokens**: Sum of active context tokens processed across all LLM turns ($\sum_{t=1}^T \text{ActiveContext}_t$), accounting for `CHECKPOINT` context resets and 90% prompt caching discounts.
4. **Source-Verified Recall & Precision (`%`)**: Manual verification of reported classes, methods, database tables, topics, and cross-service calls directly against the repository source code.
