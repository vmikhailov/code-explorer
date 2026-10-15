# Case Study 01: Polyglot Architecture Discovery & Blast Radius Analysis (`Ripgrep` vs. `CodeExplorer Graph`)

> **Headline Result**: On a 16-project polyglot C# .NET 10 + TypeScript repository (`code-explorer`), even when the Baseline Agent has full shell access to **`ripgrep` (`rg`)**, `git grep`, and directory traversal (`28` CLI search commands + `23` `view_file` reads), equipping **Gemini 3.8 Flash** with **CodeExplorer's OpenCypher Architecture Graph** completed the 3-part architectural audit **2.35x faster in time-to-answer** (`5m 08s` → `2m 11s`), required **1.97x fewer LLM turns** (`59` → `30`), made **1.97x fewer tool calls** (`57` → `29`), reduced cumulative LLM input tokens by **2.52x** (`1,594,003` → `633,469` tokens), and cut API cost by **1.87x** (`$0.140` → `$0.075` with 90% prompt caching), while achieving **100% ground-truth accuracy**.

---

## 1. Executive Summary & Comparative Scorecard

When AI coding agents tackle non-trivial architectural tasks—such as finding all implementations of a multi-level interface hierarchy across polyglot modules, or tracing the upstream blast radius of an engine method up to HTTP endpoints, WebSocket handlers, CLI verbs, and MCP tools—traditional lexical tools (`ripgrep`, `git grep`, file tree traversal, and file reads) face two structural bottlenecks:

1. **Multi-Hop Indirection Defeats Single-Pass Regex**: In a clean architecture, classes rarely implement a root marker interface directly in their declaration line. For example, in `code-explorer`, almost no parser writes `: ISemanticExtension`; instead, classes implement `IComponentLibraryParser`, `ILibraryParser`, `IDataFlowLibraryParser`, or inherit from `CSharpLibraryParserBase`. A `ripgrep` agent must iteratively discover each intermediate type and launch dozens of follow-up `rg` searches across subdirectories.
2. **Cumulative Context Growth Across Turns**: Because every LLM turn re-sends the active conversation history, every additional `rg` output listing and `view_file` verification slice compounds across all remaining turns.

By contrast, a **CodeExplorer Graph Agent** executes targeted **OpenCypher** queries against the pre-indexed semantic graph (`graph.db`), resolving transitive relationships (`[:IMPLEMENTS*1..3]`, `[:CALLS]`, `[:HAS_METHOD]`, `[:DECLARED_IN]`) in a single query and opening only specific line ranges (`view_file`) for final verification.

| Metric | Ripgrep Baseline Agent (`rg` + `git grep` + `view_file`) | CodeExplorer Graph Agent (`ce query` + Surgical Read) | Improvement / Delta |
| :--- | :---: | :---: | :---: |
| **Time to Answer (`send_message`)** | `308.0 s` (**5 min 08 sec**) | `131.0 s` (**2 min 11 sec**) | **2.35x faster** (`-57.5%` time) |
| **Total Session Wall-Clock** | `389.0 s` (`6 min 29 sec`) | `180.0 s` (`3 min 00 sec`) | **2.16x faster** (`-53.7%` time) |
| **Total Trajectory Steps** | `119` | `60` | **1.98x fewer steps** |
| **LLM Reasoning Turns** | `59` | `30` | **1.97x fewer turns** |
| **Total Tool Calls** | `57` (`28` `run_command` [`rg`], `23` `view_file`, `4` `manage_task`, `1` `schedule`, `1` `send_message`) | `29` (`13` `run_command` [`ce`], `15` `view_file`, `1` `send_message`) | **1.97x fewer tool calls** |
| **Raw Tool Output Ingested** | `189,668 chars` (**~47,417 tokens**) | `121,485 chars` (**~30,371 tokens**) | **1.56x less context payload** (`-35.9%`) |
| **Internal Reasoning (`thinking`)** | `12,862 chars` (`~3,215 tokens`) | `6,073 chars` (`~1,518 tokens`) | **2.12x less reasoning overhead** |
| **Context Window Compactions** | `0` | `0` | — |
| **Cumulative Actual Input Tokens** | `6,376,014 chars` (**~1,594,003 tokens**) | `2,533,878 chars` (**~633,469 tokens**) | **2.52x lower cumulative input load** (`-60.3%`) |
| **Ground-Truth Accuracy** | **100%** (all 3 tasks exact) | **100%** (all 3 tasks exact) | **Identical 100% precision & recall** |

---

## 2. Methodology & Controlled Experimental Setup

The experiment was executed as a **simultaneous, paired A/B test** inside Google Antigravity on the `code-explorer` repository (a polyglot C# .NET 10 + TypeScript/React codebase with 16 production/test projects).

### 2.1. Strict Tool & Environment Isolation
Both subagents were spawned in parallel at the exact same second (`2026-10-15T10:30:35Z`) on **Gemini 3.8 Flash** (`Model: "flash"`) with identical user prompts:

1. **Ripgrep Baseline Agent ([`prompts/baseline-agent.md`](./prompts/baseline-agent.md))**:
   - **Allowed Tools**: `run_command` (for `rg` / `ripgrep`, `git grep`, `Get-ChildItem`, `Select-String`) + `view_file` + `send_message`.
   - **Prohibited Actions**: Strictly forbidden from invoking `ce.exe` or reading `.codeexplorer` (verified via `transcript_full.jsonl`: **0 calls** to `ce.exe` and **0 accesses** to `.codeexplorer`).
2. **CodeExplorer Graph Agent ([`prompts/graph-agent.md`](./prompts/graph-agent.md))**:
   - **Allowed Tools**: `run_command` (to execute `ce.exe query "<CYPHER>"`) + `view_file` (for surgical line-range inspection) + `send_message`.

```mermaid
flowchart LR
    subgraph Harness["Parallel A/B Benchmark Harness (Gemini 3.8 Flash)"]
        Prompt["Identical 3-Part Architectural Prompt"]
        Prompt --> RgBase["Ripgrep Baseline Agent\n(rg + git grep + view_file)"]
        Prompt --> Graph["CodeExplorer Graph Agent\n(OpenCypher CLI + Surgical Read)"]
    end

    RgBase -->|"59 LLM turns / 28 rg commands + 23 file reads\n189.7 KB raw output"| BaseResult["5m 08s Time-to-Answer\n1.59M Cumulative Input Tokens"]
    Graph -->|"30 LLM turns / 13 Cypher queries + 15 surgical reads\n121.5 KB raw output"| GraphResult["2m 11s (2.35x Faster)\n633K Cumulative Input Tokens (2.52x Less)"]
```

### 2.2. Telemetry Collection
All metrics in [`results/metrics.json`](./results/metrics.json) are generated programmatically from the raw `transcript_full.jsonl` files:
- **Time to Answer (`time_to_answer_seconds`)**: Elapsed time from step 1 (`USER_INPUT`) to the exact step where the agent calls `send_message` with its final report.
- **Compaction-Aware Active Context (`active_context_est_tokens`)**: Sum of active context tokens processed on each LLM turn ($\sum_{t=1}^T \text{ActiveContext}_t$).

---

## 3. The Benchmark Prompt (3-Part Architectural Challenge)

The full verbatim prompt ([`prompts/task-prompt.md`](./prompts/task-prompt.md)) tests three core capabilities:

```markdown
Perform a precise architectural and impact analysis on the `code-explorer` repository and answer all three questions below with 100% completeness (do not guess or omit items; provide exact class names, method names, and relative file paths):

1. **Interface Implementations across the Codebase**:
   List every class that implements:
   - `IComponentLibraryParser`
   - `IMcpTargetAdapter`
   - `ILibraryConfigurationDescriptor`
   - `ISyntaxEnricher`
   For each class, provide its exact class name and the relative file path where it is declared.

2. **Blast Radius & Call Chain of `GateExecutionEngine`**:
   Identify all **production (non-test)** classes and methods that directly call `CheckGateAsync` or `CheckSuiteAsync` on `GateExecutionEngine`. For each caller, also identify one hop further upstream (which HTTP endpoint, WebSocket message type, CLI command, or MCP tool triggers that caller).

3. **Polyglot `ISemanticExtension` Breakdown**:
   Count the exact number of classes implementing `ISemanticExtension` in each of the following parser projects:
   - `CodeExplorer.Parser.CSharp`
   - `CodeExplorer.Parser.TypeScript`
   - `CodeExplorer.Parser.Python`
   - `CodeExplorer.Parser.Go`
   - `CodeExplorer.Parser.Java`
   - `CodeExplorer.Parser.Android`
   And list the exact class names of all `ISemanticExtension` implementations in `CodeExplorer.Parser.Java` and `CodeExplorer.Parser.Android`.
```

---

## 4. Detailed Comparison of Execution Trajectories

### 4.1. How `rg_baseline_agent` Solved It (`59` Turns, `5m 08s` Time-to-Answer)
- **Conversation ID**: `dc323d20-e714-4d4f-88d5-0a772d365a63`
- **Tool Breakdown**: `28` `run_command` (`rg` / `Get-ChildItem`), `23` `view_file`, `4` `manage_task`, `1` `schedule`, `1` `send_message`.
- **Trajectory Highlights**:
  1. **Turns 1–12**: Executed `rg "IComponentLibraryParser|IMcpTargetAdapter|ILibraryConfigurationDescriptor|ISyntaxEnricher" cli/src` and inspected `StandardConfigurationDescriptors.cs` and interface declarations via `view_file`.
  2. **Turns 13–24**: Searched for `CheckGateAsync|CheckSuiteAsync` via `rg`, then ran follow-up `rg` searches for `CheckSemanticGatesAsync`, `EvaluateGatesSuiteAsync`, `RatchetGatesSuiteAsync`, `InitGatesAsync`, and `GateCommandHandler`, followed by 11 `view_file` calls across `GateExecutionEngine.cs`, `CodeExplorerRepository.cs`, `GateCommandHandler.cs`, `WebSocketServerHandler.cs`, `ServerRouteBuilderExtensions.cs`, and `McpGraphHandler.cs`.
  3. **Turns 25–58 (Multi-Hop Interface Discovery via Repeated `rg` + PowerShell)**: To count `ISemanticExtension` implementations across the 6 parser projects, the agent discovered that `ISemanticExtension` is inherited transitively via `ILibraryParser`, `IComponentLibraryParser`, `IDataFlowLibraryParser`, and `CSharpLibraryParserBase`. It had to run **16 separate `rg` and PowerShell regex commands** across the 6 parser folders, checking for multi-class files and abstract base classes before submitting its answer at Turn 59 (`308.0s`).

### 4.2. How `ce_graph_agent` Solved It (`30` Turns, `2m 11s` Time-to-Answer)
- **Conversation ID**: `194018bb-888f-4f9d-baa0-5f08be4a97fd`
- **Tool Breakdown**: `13` `run_command` (`ce.exe query`), `15` `view_file`, `1` `send_message`.
- **Trajectory Highlights**:
  1. **Turns 1–6**: Queried `MATCH (c:Type)-[:IMPLEMENTS*1..3]->(i:Type)` for the 4 target interfaces in Task 1, immediately retrieving every concrete class and its `DECLARED_IN` file path (including all 3 classes inside `StandardConfigurationDescriptors.cs`).
  2. **Turns 7–18**: Queried `MATCH (caller:Function)-[:CALLS]->(callee:Function)` for `CheckGateAsync` and `CheckSuiteAsync` and their upstream callers, inspecting exact HTTP route declarations (`GET /api/gates`, `POST /api/gates/check`, etc.) and WebSocket handlers.
  3. **Turns 19–29**: Queried `MATCH (c:Type)-[:IMPLEMENTS*1..5]->(i:Type {name: 'ISemanticExtension'})` grouped by parser project path, getting the exact counts (`25`, `51`, `18`, `19`, `9`, `0`) without scanning source files by hand, and submitted its final report at Turn 29 (`131.0s`).

---

## 5. Turn-by-Turn Context Growth Curve (Exact Values from `metrics.json`)

Every number in the table below matches [`results/metrics.json`](./results/metrics.json) to the exact token:

```
Cumulative Input Tokens Across Agent Turns (Gemini 3.8 Flash)
=============================================================

Ripgrep Baseline (59 turns) : ████████████████████████████████████████ 1,594,003 tokens (6.38 MB chars)
Graph Agent      (30 turns) : ████████████████                         633,469 tokens (2.53 MB chars)
                              ^ 2.52x reduction in cumulative input tokens!
```

| Milestone | Ripgrep Baseline Active Context (`actual cumulative`) | Graph Agent Active Context (`actual cumulative`) |
| :--- | :---: | :---: |
| **Turn 5** | `1,413 tokens` (`5,243` cum.) | `5,038 tokens` (`15,737` cum.) |
| **Turn 10** | `2,231 tokens` (`14,828` cum.) | `17,751 tokens` (`85,123` cum.) |
| **Turn 15** | `12,303 tokens` (`52,972` cum.) | `22,212 tokens` (`185,772` cum.) |
| **Turn 20** | `15,773 tokens` (`122,820` cum.) | `26,661 tokens` (`310,444` cum.) |
| **Turn 25** | `21,509 tokens` (`217,448` cum.) | `31,385 tokens` (`458,696` cum.) |
| **Turn 30 (Graph Agent Done @ 2m 11s)** | `28,476 tokens` (`345,448` cum.) | **`40,193 tokens` (`633,469` cum. — 100% Done)** |
| **Turn 51** | `49,013 tokens` (`1,167,170` cum.) | — |
| **Turn 59 (Ripgrep Baseline Done @ 5m 08s)** | **`60,994 tokens` (`1,594,003` cum. — 100% Done)** | — |

---

## 6. Cost & Economic Evaluation (With 90% Prompt Caching Discount)

Using standard **Gemini 3.8 Flash** API pricing with a **90% prompt cache discount** on repeated history reads (\$0.50 / 1M uncached input, \$0.05 / 1M cached input, \$3.00 / 1M output & thinking tokens):

| Cost Component | Ripgrep Baseline Agent (`59` turns) | CodeExplorer Graph Agent (`30` turns) | Savings Factor |
| :--- | :---: | :---: | :---: |
| **Uncached Input Tokens** (fresh tool outputs) | `47,417` tokens (**\$0.024**) | `30,371` tokens (**\$0.015**) | **1.56x cheaper** |
| **Cached Input Tokens** (`cum_actual - uncached`) | `1,546,586` tokens (**\$0.077**) | `603,098` tokens (**\$0.030**) | **2.56x cheaper** |
| **Output & Thinking Tokens** | `12,928` tokens (**\$0.039**) | `9,786` tokens (**\$0.029**) | **1.32x cheaper** |
| **Total Flash API Cost per Run** | **\$0.140** | **\$0.075** | **1.87x cheaper (`-46.4%`)** |
| **Time to Answer** | `5m 08s` (`308.0s`) | `2m 11s` (`131.0s`) | **2.35x faster (`-2m 57s` saved)** |

---

## 7. Repository Artifacts in This Folder

- [`prompts/task-prompt.md`](./prompts/task-prompt.md) — Exact 3-part architectural benchmark prompt.
- [`prompts/baseline-agent.md`](./prompts/baseline-agent.md) — Declarative agent configuration for the Ripgrep Baseline Agent (`rg_baseline_agent`).
- [`prompts/graph-agent.md`](./prompts/graph-agent.md) — Declarative agent configuration for the CodeExplorer Graph Agent (`ce.exe query`).
- [`results/metrics.json`](./results/metrics.json) — Complete machine-readable telemetry, including turn-by-turn context growth curves and tool call breakdowns.
- [`results/baseline-agent-output.md`](./results/baseline-agent-output.md) — Verbatim report produced by the Ripgrep Baseline Agent (`5m 08s`).
- [`results/graph-agent-output.md`](./results/graph-agent-output.md) — Verbatim report produced by the CodeExplorer Graph Agent (`2m 11s`, `30` turns).
