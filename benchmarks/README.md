# CodeExplorer Benchmarks: Architecture Graph vs. File/Grep Navigation

This directory contains reproducible, telemetry-backed benchmarks measuring the impact of **CodeExplorer's OpenCypher Architecture Graph** on autonomous AI coding agents compared to traditional lexical exploration (`grep` / `ripgrep` / `list_dir` / `view_file`).

---

## Why Benchmark Graph Intelligence for AI Agents?

A recurring question in AI-assisted software engineering is:
> *"Why does an AI agent need a semantic codebase graph if it already has fast `ripgrep` and file reading tools?"*

While lexical search (`grep`) works well for single-hop exact string matches, real-world software architecture tasks—such as **transitive type hierarchy resolution**, **multi-hop blast radius analysis**, **cross-project dependency auditing**, and **entrypoint attribution**—require traversing structural relationships across dozens or hundreds of files.

When an agent relies purely on file navigation and text search:
1. **Context Window Saturation**: The agent must open and read dozens or hundreds of source files (`view_file`) to verify inheritance chains, method signatures, and call sites.
2. **Quadratic Input Token Multiplier**: Because every LLM turn is stateless, every kilobyte of raw source code read in Turn $t$ is re-sent as input tokens on Turn $t+1, t+2, \dots, T$.
3. **Lossy Context Compactions**: Once the context window fills with raw file dumps, the agent harness triggers mid-task context compaction—adding latency and risking the loss of earlier structural facts.

With **CodeExplorer**, the agent queries a high-level, function-and-type granularity projection of the codebase via **OpenCypher** (`ce query` / MCP tools) to locate exact structural facts and line numbers first, and only opens specific file slices when line-level implementation details are needed.

---

## Benchmark Suite Index

| Case Study | Task Domain | Duration (`Baseline` → `Graph`) | Tool Calls (`Baseline` → `Graph`) | Raw Context Payload (`Baseline` → `Graph`) | Compactions (`Baseline` → `Graph`) | Cumulative Input Tokens (`Baseline` → `Graph`) | Accuracy |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **[01: Polyglot Architecture Discovery & Blast Radius Analysis](./01-polyglot-architecture-and-blast-radius/README.md)** | Transitive interface census across 6 language parsers + 2-hop upstream call-chain & endpoint blast radius | `16m 23s` → **`3m 31s`** (**4.7x faster**) | `248` → **`28`** (**8.9x fewer**) | `1.81 MB` → **`168.8 KB`** (**10.7x smaller**) | `2` → **`0`** | `~15.32M` → **`~379.1K`** (**40.4x fewer**) | **100%** / **100%** |

---

## Evaluation Dimensions

Every benchmark in this suite is executed under strict **paired A/B isolation** (identical model, identical prompt, simultaneous execution on the same commit, with tool access controlled via declarative agent manifests) and evaluated across six objective dimensions parsed directly from the runtime's `transcript_full.jsonl` logs:

1. **Wall-Clock Completion Time (`seconds`)**: End-to-end duration from initial prompt dispatch to final answer delivery.
2. **LLM Reasoning Turns & Tool Calls**: Number of model round-trips (`PLANNER_RESPONSE`) and tool invocations required to reach 100% certainty.
3. **Raw Tool Output Ingested (`chars` / `tokens`)**: Total volume of tool output payloads injected into the agent's context window.
4. **Context Compaction Events**: Number of times the agent exceeded its active context threshold and required emergency summarization/compaction.
5. **Cumulative Input Payload (`tokens`)**: Sum of active context tokens processed across all LLM turns ($\sum_{t=1}^T \text{Context}_t$), which directly governs API cost and time-to-first-token (TTFT) latency.
6. **Ground-Truth Completeness & Precision (`%`)**: Verification of every reported class name, method name, line number, and file path against compiler/AST ground truth.
