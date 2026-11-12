---
name: code-explorer
description: Deterministic polyglot architecture graph, cross-service call tracing, SQL/ORM data lineage, blast-radius impact analysis, and Semantic Invariant Gates via the CodeExplorer MCP server. Use before grep/ripgrep whenever exploring repository architecture, finding callers or implementations, tracing HTTP/gRPC/Kafka/SQL flows, estimating refactoring blast radius, or verifying architectural gates.
---

# CodeExplorer Architecture Graph Skill

CodeExplorer indexes C#, TypeScript/JavaScript, Go, Python, Java, Kotlin, Rust, and SQL into a unified 5-layer SQLite ontology graph queried in milliseconds via MCP and OpenCypher.

## First-Step Bootstrap

1. Call `get_workspace_status` first.
2. If the repository is not indexed yet, call `init_workspace` followed by `scan_workspace` (incremental, typically a few seconds).

## Tool Selection Rules (Prefer Graph Over Text Search)

| Task | CodeExplorer MCP Tool | Why |
| :--- | :--- | :--- |
| Understand system map, services, DBs, queues, entry points | `get_architecture_map` | Returns full polyglot topology in one compact call instead of scanning dozens of manifests |
| Check project/module blast radius & package dependencies | `get_project_dependencies` | Exact inbound/outbound project links and external packages |
| Locate a class, interface, function, or endpoint | `find_symbol` | Resolved AST symbol lookup; fall back to grep only for dynamic strings |
| Find who calls a function, endpoint, table, or topic | `get_callers` / `get_call_chain` | Follows resolved calls, DI interfaces, HTTP clients, and message consumers |
| Estimate refactoring risk & affected tests | `analyze_code_impact` / `get_affected_tests` | Computes transitive callers and test suites reached by an edit |
| Trace cross-service API / event flows | `trace_cross_service_flow` | Connects frontend/API callers across HTTP, gRPC, Kafka/RabbitMQ, and DB tables |
| Inspect SQL / ORM table reads & writes | `inspect_data_lineage` | Links repositories, EF Core/Dapper/Prisma/SQL queries, and database tables |
| Enforce architectural invariants & zero regressions | `check_semantic_gates` | Evaluates OpenCypher Semantic Invariant Gates (`SIG-*`) with ratchet baselines |
| Custom graph queries | `execute_custom_read_cypher` | Runs read-only OpenCypher queries directly against the 5-layer code ontology |

## Post-Edit Verification

After modifying code in a repository with `.codeexplorer` initialized:
1. Run `scan_workspace` to update changed files incrementally.
2. Run `check_semantic_gates` and fix any newly introduced `[FAIL]` violations before finishing.
