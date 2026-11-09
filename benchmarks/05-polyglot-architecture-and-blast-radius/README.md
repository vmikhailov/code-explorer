# Case Study 05: `OrchardCMS/OrchardCore` Modular .NET Monolith Audit (`CodeExplorer` vs `ripgrep`)

> **Benchmark Status (`[TBD]`)**: Paired A/B agent benchmark on the open-source [`OrchardCMS/OrchardCore`](https://github.com/OrchardCMS/OrchardCore) modular .NET monolith (`[TBD ~180]` C# projects, `[TBD ~6,000]` files). Placeholder cells marked `[TBD]` will be populated from `results/metrics.json` once the paired run completes.

---

## 1. Experimental Setup & Tested Versions

- **Target Repository**: `OrchardCMS/OrchardCore @ [TBD]` (`[TBD ~180]` `.csproj` modules, `[TBD ~6,000]` C# files, ASP.NET Core, YesSql).
- **Compared Tools**:
  - **`CodeExplorer` (`ce`)**: `v1.75.x` (.NET 10, SQLite + OpenCypher graph).
  - **Lexical Baseline**: `ripgrep` (`rg` `v14.1.1`) + `view_file`.
- **LLM Model**: `Gemini 3.8 Flash` (simultaneous execution with 100% symmetric system prompts).

---

## 2. What the Agents Must Solve on `OrchardCore`

In a modular .NET monolith like `OrchardCore`, classes rarely implement root interfaces directly: they inherit through generic base classes (such as `ContentPartDisplayDriver<TPart>` or `ContentHandlerBase`) across `[TBD ~180]` separate `.csproj` modules.

1. **Task 1 — Transitive Display Drivers & Content Handlers**:
   Find all concrete classes implementing `IDisplayDriver` and `IContentHandler` (including indirect subclasses via generic base classes) across key content modules (`[:INHERITS_FROM|IMPLEMENTS*1..5]`).
2. **Task 2 — Cross-Module Upstream Callers & Endpoints**:
   Trace production callers of core content management services across module boundaries up to the triggering Razor/MVC controller endpoints and background tasks (`[:CALLS]`).
3. **Task 3 — Project Fan-In & YesSql Document Indexes**:
   Identify which modules depend on core abstractions (`[:DEPENDS_ON]`) and map document index providers to their underlying storage tables.

---

## 3. Execution Telemetry Scorecard

Measured from paired A/B agent runs on the same `OrchardCMS/OrchardCore` commit with identical symmetric system prompts (`results/metrics.json`):

| Metric | `ripgrep` + File Reads | `CodeExplorer` | Delta |
| :--- | :---: | :---: | :---: |
| **Time to Answer (`send_message`)** | `[TBD s]` (`[TBD min]`) | `[TBD s]` (`[TBD min]`) | **`[TBD ×]` faster** |
| **LLM Reasoning Turns** | `[TBD]` turns | `[TBD]` turns | **`[TBD ×]` fewer turns** |
| **Total Tool Calls** | `[TBD]` (`rg` + `view_file`) | `[TBD]` (`ce` + `view_file`) | **`[TBD ×]` fewer tool calls** |
| **Raw Tool Output Ingested** | `[TBD]` chars (`~[TBD]` tokens) | `[TBD]` chars (`~[TBD]` tokens) | **`[TBD ×]` less tool payload** |
| **Cumulative Input Tokens** | `[TBD]` tokens | `[TBD]` tokens | **`[TBD ×]` fewer input tokens** |
| **API Cost w/ 90% Cache Discount (`Gemini 3.8 Flash`)** | `$[TBD]` | `$[TBD]` | **`[TBD ×]` cheaper** |
| **Audit Completeness (Tasks 1 to 3)** | `[TBD %]` | `[TBD %]` | **`[TBD]`** |
