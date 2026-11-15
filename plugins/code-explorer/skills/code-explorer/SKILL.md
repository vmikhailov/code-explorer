---
name: code-explorer
description: Deterministic polyglot architecture graph, cross-service call tracing, SQL/ORM data lineage, blast-radius impact analysis, and Semantic Invariant Gates via the CodeExplorer MCP server. Use before grep/ripgrep whenever exploring repository architecture, finding callers or implementations, tracing HTTP/gRPC/Kafka/SQL flows, estimating refactoring blast radius, or verifying architectural gates.
---

# CodeExplorer Architecture Graph Skill

CodeExplorer indexes C#, TypeScript/JavaScript, Go, Python, Java, Kotlin, Rust, and SQL into a unified 5-layer SQLite ontology graph queried in milliseconds via MCP and OpenCypher.

## First-Step Bootstrap

1. Call `get_taxonomy` with `statusOnly: true` first to inspect index health.
2. If the repository is not indexed yet, call `scan_workspace` (auto-initializes `.codeexplorer/graph.db` and indexes incrementally in a few seconds).

## Tool Selection Rules (Prefer Graph Over Text Search)

| Task | CodeExplorer MCP Tool | Why |
| :--- | :--- | :--- |
| Understand system map, C1/C2/C3 views, or export diagrams | `get_architecture_map` | Returns polyglot inventory, module cycles (`view: "c3"`), or Mermaid/C4 diagrams (`format: "mermaid"`) |
| Check project/module blast radius & package dependencies | `get_project_dependencies` | Exact inbound/outbound project links and external packages |
| Locate code symbols, endpoints, tables, or fuzzy keywords | `find_symbol` | Substring AST lookup (`matchMode: "substring"`) or hybrid trigram+vector search (`matchMode: "hybrid"`) |
| Find inbound callers or concrete interface implementations | `get_callers` / `get_call_chain` | Follows resolved calls, DI implementations (`methodName`), HTTP clients, and message consumers |
| Estimate refactoring risk, affected tests & test coverage | `analyze_code_impact` / `get_affected_tests` | Computes transitive blast radius, impacted test suites, and static test coverage (`coverageStatus`) |
| Inspect service contracts, entry points & distributed flows | `get_service_contracts` | Static ingress/egress contracts, handler coordinates (`direction: "entry_points"`), and cross-service traces (`direction: "flow"`) |
| Inspect SQL / ORM table reads & writes | `inspect_data_lineage` | Links repositories, EF Core/Dapper/Prisma/SQL queries, and database tables |
| Enforce architectural invariants & detect anomalies | `check_semantic_gates` | Evaluates Semantic Invariant Gates (`SIG-*`), lists rules (`listOnly: true`), or scans dead code / god objects (`anomalies`) |
| Ad-hoc or saved OpenCypher queries | `execute_custom_read_cypher` | Runs read-only Cypher (`query`), saved `.codeexplorer/queries/` templates (`savedQuery`), or lists saved queries (`listSaved: true`) |

## Post-Edit Verification

After modifying code in a repository with `.codeexplorer` initialized:
1. Run `scan_workspace` to update changed files incrementally.
2. Run `check_semantic_gates` and fix any newly introduced `[FAIL]` violations before finishing.
