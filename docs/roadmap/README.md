# CodeExplorer Engineering Roadmap 🗺️🤖

This directory tracks the engineering roadmap, active epic specifications, and actionable task backlog for **CodeExplorer (`ce`)**.

---

## 📌 Roadmap Overview & Quick Links

| Document | Purpose | Status |
| :--- | :--- | :--- |
| [**`todo.md`**](./todo.md) | Granular Master Task Checklist across all Epics | 📋 Single Source of Truth |
| [**`semantic-invariant-gates-plan.md`**](./semantic-invariant-gates-plan.md) | Active Epic Specification: Semantic Invariant Gates & Ratchets | 🚀 In Progress (v1.32.0) |
| [**`../architecture/fractal-orthogonal-invariant-matrix.md`**](../architecture/fractal-orthogonal-invariant-matrix.md) | Formal FOIM $5 \times 4$ Architecture Specification | 🏛️ Reference Specification |
| [**`../proposals/semantic-invariant-gates-proposal.md`**](../proposals/semantic-invariant-gates-proposal.md) | Polyglot Gates Proposal & 34-Gate Catalog ("Gates on Growth") | 💡 Active Proposal |

---

## 🚨 Current Active Milestone: v1.32.0 (Structural Graph Invariants)

Following the delivery of **Semantic Invariant Gates (v1.31.0)**, the active focus is hardening graph topology and simplifying gate queries:

1. **Structural Project $\leftrightarrow$ File Edges**:
   * Emit bidirectional relationships during Layer 3 parsing: `(p:Project)-[:CONTAINS]->(f:File)` and `(f:File)-[:BELONGS_TO]->(p:Project)`.
   * Emit `(lit:CallLiteral)-[:DECLARED_IN]->(f:File)` linking call literals to their declaring files.
2. **First-Class Test and Project Metadata Flags**:
   * Propagate `is_test: bool`, `project_kind: string`, and `project_name: string` onto `FileNode`, `CallLiteralNode`, `TypeNode`, and `FunctionNode`.
   * Eliminate brittle string heuristics (`WHERE NOT file_path CONTAINS 'Test'`) across all gate Cypher queries.
3. **Pipeline Stage & Layer Ordering Invariants (`stage_order`)**:
   * Introduce a scalar `stage_order: int` property on pipeline stage types and files.
   * Replace combinatorial $O(N^2)$ `OR` clauses in `SIG-P-B-001` with clean scalar comparison:
     ```cypher
     MATCH (src:Type)-[:CALLS|USES_TYPE]->(tgt:Type)
     WHERE src.stage_order IS NOT NULL 
       AND tgt.stage_order IS NOT NULL 
       AND src.stage_order < tgt.stage_order
     RETURN ...
     ```

---

## 🧭 Strategic Epics Roadmap

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  EPIC 0: Universal Semantic Resolution & Zero-Hack Core            ✅ v1.10 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 1: Agent Semantic Architecture (C1/C2/C3 Projections)        ✅ v1.12 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 2: Parser Intelligence & High-Fidelity Lineage               ✅ v1.15 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 3: Typed Cypher & Transpiler Expansion                       ✅ v1.19 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 4: Incremental Diffing Watcher (`ce watch`)                  ✅ v1.25 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 5: CI/CD Synthetic Benchmark Fixtures (100k nodes)           ✅ v1.30 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 6: Semantic Invariant Gates (SIG) & Ratchets        🔄 In Progress │
│          • Phase 1–5: Engine, Models, CLI, MCP, Built-ins          ✅ v1.31 │
│          • Phase 6: Structural Graph Links & Order Invariants       ⏳ v1.32 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 7: Polyglot Gate Library & Static Analysis Expansion       ⏳ Planned │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 📊 Epics & Delivery Status Summary

### Epic 0: Universal Semantic Value Resolution & Zero-Hack Core ✅ Completed (v1.10.0)
* Unified `ConfigStore` in Layer 2/3 for cross-file configuration discovery.
* Universal AST Symbol & Constant Graph across all files with Expression Trees and topological evaluation.
* Universal `AstValueResolver` for multi-language AST expressions, environment configs, and reaching definitions.
* Evidence-based entity classification (`Service`, `App`, `Worker`, `Library`, `CliTool`) replacing ad-hoc string heuristics.

### Epic 1: Agent Semantic & Project Architecture ✅ Completed (v1.12.0)
* C1 System Context, C2 Service Flow, and C3 Component drill-down projections via `ArchitectureViewEngine`.
* MCP tools: `get_architecture_view`, `get_project_dependencies --type runtime|build|all`, `get_service_contracts`, `trace_cross_service_flow`.
* CLI commands: `ce view architecture`, `ce dependencies --type`, `ce contracts`, `ce trace flow`.
* Token-efficient serializers: TOON, Mermaid (`flowchart LR`), compact Markdown.

### Epic 2: Parser Intelligence & High-Fidelity Lineage ✅ Completed (v1.15.0)
* ASP.NET Core hierarchical route composition and Minimal API `MapGroup` chaining.
* C# constructor dependency injection mapping and interface resolution via `[:IMPLEMENTS]`.
* Declarative HTTP client URL resolution (`HttpClient`, `RestSharp`, `Refit`, `RestEase`).
* ORM data lineage mapping (EF Core `DbSet<T>` / `ToTable` and Dapper SQL extraction).

### Epic 3: Cypher Query Engine & Transpiler Expansion ✅ Completed (v1.19.0)
* Typed Cypher Queries: `GraphContext`, `CypherQueryable<T>`, LINQ Expression compiler, strongly-typed graph entities.
* OpenCypher relationship functions: `type(r)`, `properties(r)`, `startNode(r)`, `endNode(r)` in `WITH` and aggregations.
* Cartesian product elimination: decomposed independent `OPTIONAL MATCH` branches into isolated CTEs.
* Path predicates (`WHERE EXISTS((a)->(b))`) and list quantifiers (`any()`, `all()`, `none()`, `single()`).

### Epic 4: Concurrency & Incremental Watcher 🔄 Mostly Complete (v1.25.0)
* **4.1 Completed:** Fine-grained semantic AST diffing (`SemanticGraphDiffer`, `SymbolFootprint`, `FileGraphSnapshot`), debounced `ce index --watch`, two-tier changeset detection (timestamps + SHA-256), and SQLite auto-vacuum.
* **4.2 Pending:** Read-only connection pooling (`Mode=ReadOnly;Cache=Shared;`) for MCP tools and thread-safe in-memory cache for `SystemContext`.

### Epic 5: CI/CD Benchmark Fixtures ✅ Completed (v1.30.0)
* Synthetic deterministic 100k-node graph fixture for CI (10k files, 25k classes, 70k methods, 300k relationships).
* Automated SLA performance assertions: `find_symbol` < 25ms, `get_call_chain` < 100ms, `inspect_data_lineage` < 50ms.

### Epic 6: Semantic Invariant Gates (SIG) & Architectural Ratchets 🔄 Active Priority (v1.31.0 - v1.32.0)
* **6.1–6.6 Completed (v1.31.0):**
  - Gate models, YAML definitions, JSON baselines, line-shift immune fingerprinting (`SHA256(gate_id + path + symbol + target)`).
  - `GateExecutionEngine` (Zero-Match contract) and `RatchetService` (monotonic baseline enforcement).
  - CLI `ce gate check/list/ratchet/init` commands with ANSI reporting and SARIF export.
  - MCP tool `check_semantic_gates` with actionable remediation guidance.
  - Gate taxonomy: `Category` (`Architectural` vs `Platform`) and `Platform` filtering.
* **6.7–6.8 In Progress (v1.32.0):**
  - Structural links `Project` $\leftrightarrow$ `File` and `CallLiteral` $\to$ `File`.
  - Propagation of `is_test`, `project_kind`, and `project_name` to eliminate string heuristics.
  - Pipeline stage ordering (`stage_order`) to simplify hierarchical pipeline gate queries.

### Epic 7: Polyglot Gate Library & Static Analysis Expansion ⏳ Planned (v1.33.0+)
* Implementation of the 24 platform-specific gates specified in [`semantic-invariant-gates-proposal.md`](../proposals/semantic-invariant-gates-proposal.md) across .NET, TypeScript, Go, Python, Java, Kotlin/Android, SQL, and ColdFusion.
* Pre-commit and GitHub Actions workflow integrations.
