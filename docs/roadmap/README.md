# CodeExplorer Engineering Roadmap 🗺️🤖

This directory tracks the engineering roadmap, active epic specifications, and actionable task backlog for **CodeExplorer (`ce`)**.

---

## 📌 Roadmap Overview & Quick Links

| Document | Purpose | Status |
| :--- | :--- | :--- |
| [**`todo.md`**](./todo.md) | Granular Master Task Checklist across all Epics | 📋 Single Source of Truth |
| [**`../architecture/fractal-orthogonal-invariant-matrix.md`**](../architecture/fractal-orthogonal-invariant-matrix.md) | Formal FOIM $5 \times 4$ Architecture Specification | 🏛️ Reference Specification |
| [**`../proposals/universal-parser-proposal.md`**](../proposals/universal-parser-proposal.md) | Universal AST Visitor & `ILanguageHooks` (v2.0.0 PoC) | 💡 Active Proposal (Next Priority) |
| [**`../proposals/semantic-archetypes-and-yaml-projections-proposal.md`**](../proposals/semantic-archetypes-and-yaml-projections-proposal.md) | Declarative YAML Semantic Projection Engine & 5 Archetypes | 💡 Active Proposal |
| [**`../proposals/universal-edge-and-node-evidence-proposal.md`**](../proposals/universal-edge-and-node-evidence-proposal.md) | Creation-Time Provenance (`evidence_ids`) for Layer 4/5 Edges | 💡 Active Proposal (Remaining v2.0.0 Work) |
| [**`../proposals/project-map-proposal.md`**](../proposals/project-map-proposal.md) | Project Map: C3 internal module view, call lifting, cycle detection | ✅ v1.56.0 (remaining: DDL table linking patch, module-cycle gate) |

---

## 🚨 Current Active Milestone: Universal Parser & Declarative Projections (v1.56.0 / v2.0.0 PoC)

Following the delivery of **Full 5-Layer Rust Support (`v1.47.0`–`v1.49.0`)**, **31 Polyglot & Evolution Semantic Invariant Gates (`v1.52.0`)**, **Universal Code Evidence Inspector (`v1.53.0`)**, and **Strict 5-Layer Ontology Hierarchy (`v1.55.0`)**, the active focus is:

1. **Universal AST Visitor & `ILanguageHooks` ([`universal-parser-proposal.md`](../proposals/universal-parser-proposal.md))**:
   * Introduce `UniversalAstVisitor` in `CodeExplorer.Parser.TreeSitter` driving ~80% of Layer 3 syntax extraction (`Types`, `Functions`, `Members`, `CallSites`, `ControlFlow`, `Variables`).
   * Keep a thin `ILanguageHooks` interface (~150–250 LOC per language) for language-specific edges (`Imports`, `Visibility`, `Annotations`, `TypeReferences`).
   * **Track A (Parity)**: Migrate Java (`JavaLanguageHooks`) and verify 100% node/edge parity against `JavaSyntaxExtractor`.
   * **Track B (Velocity)**: Deliver full 5-layer **Swift** support (`SwiftLanguageHooks` + `SwiftPackageParser`) in ~300 LOC.
2. **Declarative YAML Semantic Projections ([`semantic-archetypes-and-yaml-projections-proposal.md`](../proposals/semantic-archetypes-and-yaml-projections-proposal.md))**:
   * Introduce `YamlSemanticProjectionEngine` in Layer 4 mapping the 5 Universal Semantic Archetypes (`EntryPoint`, `OutboundCall`, `MessageFlow`, `DataAccess`, `DependencyBinding`) via declarative `.yaml` rule packs.
3. **Creation-Time Provenance ([`universal-edge-and-node-evidence-proposal.md`](../proposals/universal-edge-and-node-evidence-proposal.md))**:
   * Persist `evidence_ids` at edge creation time in Layer 4 and aggregate during Layer 5 rollup, replacing query-time evidence heuristics with deterministic $O(k)$ primary-key lookups.

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
│  EPIC 6: Semantic Invariant Gates (SIG) & 31-Gate Catalog          ✅ v1.52 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 7: Full 5-Layer Rust Support (`CodeExplorer.Parser.Rust`)    ✅ v1.49 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 8: Strict 5-Layer Ontology & Level 5 Logical Structure       ✅ v1.55 │
├─────────────────────────────────────────────────────────────────────────────┤
│  EPIC 9: Universal AST Visitor & Declarative YAML Projections      🚀 Next  │
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

### Epic 4: Concurrency & Incremental Watcher ✅ Completed (v1.25.0)
* Fine-grained semantic AST diffing (`SemanticGraphDiffer`, `SymbolFootprint`, `FileGraphSnapshot`), debounced `ce index --watch`, two-tier changeset detection (timestamps + SHA-256), and SQLite auto-vacuum.

### Epic 5: CI/CD Benchmark Fixtures ✅ Completed (v1.30.0)
* Synthetic deterministic 100k-node graph fixture for CI (10k files, 25k classes, 70k methods, 300k relationships).
* Automated SLA performance assertions: `find_symbol` < 25ms, `get_call_chain` < 100ms, `inspect_data_lineage` < 50ms.

### Epic 6: Semantic Invariant Gates (SIG) & 31-Gate Polyglot Catalog ✅ Completed (v1.31.0 – v1.52.0)
* Gate models, YAML definitions, JSON baselines, line-shift immune fingerprinting (`SHA256(gate_id + path + symbol + target)`).
* `GateExecutionEngine` (Zero-Match contract), `RatchetService` (monotonic baseline enforcement), and interactive `GateEditorView` in the VS Code extension (`v1.46.0`).
* 31 built-in gates across Architectural, Evolution (`SIG-P-B-007`..`009`), Ontology (`SIG-O-B-001`..`003`), Code Complexity (`SIG-C-R-001`), and Platform-specific suites (.NET, TypeScript, Go, Python, Java, Kotlin, Rust, SQL, ColdFusion).

### Epic 7: Full 5-Layer Rust Language Support ✅ Completed (v1.47.0 – v1.49.0)
* `CodeExplorer.Parser.Rust` with `RustSyntaxExtractor` (`tree-sitter-rust`), `CargoProjectParser` (including Cargo workspace resolution), `RustTypeResolver`, 12 ecosystem library parsers (`axum`, `actix-web`, `rocket`, `tonic`, `reqwest`, `sqlx`, `sea-orm`, `diesel`, `rdkafka`, `lapin`, `async-nats`, `aws-sdk-rust`), and 4 Rust SIG gates (`SIG-R-S-001`, `SIG-R-A-001`, `SIG-R-C-001`, `SIG-R-D-001`).

### Epic 8: Strict 5-Layer Ontology & Level 5 Logical Structure ✅ Completed (v1.53.0 – v1.55.0)
* Universal Edge & Node Code Evidence Inspector (`GET /api/architecture/edge-evidence`, `GET /api/architecture/node-evidence`, `v1.53.0`).
* Standardized 5-Layer Ontology Hierarchy (`File Structure`, `Project Structure`, `Syntax Structure`, `Semantic Structure`, `Logical Structure`), Zero Upward Edges invariant (`SIG-O-B-003`), removal of `ISyntaxEnricher`, and persisted `DomainNode` / `BoundedContextNode` entities (`v1.55.0`).
