# Semantic Invariant Gates (SIG) Implementation Plan 🛡️

## 1. Context & Motivation
Large projects (such as enterprise microservices, AI-assisted workflows, and compiler pipelines) suffer from architectural drift and regression during continuous iteration.
In projects using manual AST scripts, rules are fragile, slow, single-language, and expensive to maintain.

**CodeExplorer** already maintains a high-fidelity semantic graph (`graph.db`) with polyglot support (C#, Python, Java, Kotlin, TypeScript, Go, SQL, ColdFusion), sub-millisecond OpenCypher querying, and native MCP support.

By introducing **Semantic Invariant Gates (SIG)**, CodeExplorer becomes an active architectural guardrail:
- **Declarative Zero-Match Invariants**: Express forbidden architectural topology in OpenCypher.
- **Monotonic Technical Debt Ratchets**: Legacy violations are baselined; debt can only strictly decrease ($N_{t+1} \le N_t$).
- **Sub-Second Local Pre-Commit / CI Checks**: Fast execution (< 50ms) directly over SQLite.
- **Autonomous AI-Agent Guardrails**: Native MCP tool `check_semantic_gates` allowing agents to self-verify code changes before commit.

> **Theoretical Taxonomy & Architectural Model:** See [Fractal Orthogonal Invariant Matrix (FOIM)](../architecture/fractal-orthogonal-invariant-matrix.md) for the complete $5 \times 4$ taxonomy across Method, Class, Module, Service, and System scopes.
> **Full Proposal & Polyglot Catalog ("Gates on Growth"):** See [docs/proposals/semantic-invariant-gates-proposal.md](../proposals/semantic-invariant-gates-proposal.md) for 34 detailed gates across all supported platforms.

---

## 2. Directory Layout & Architecture

```
.codeexplorer/
├── gates/                                # Workspace gate definitions (YAML)
│   ├── architectural/                    # Platform-agnostic architectural gates
│   │   ├── sig-p-b-001-pipeline-layer-strict-hierarchy.yaml
│   │   ├── sig-p-b-003-clean-architecture-transport-purity.yaml
│   │   ├── sig-c-i-001-in-memory-ast-visitor-io-purity.yaml
│   │   └── sig-m-i-001-disallow-hardcoded-call-arguments.yaml
│   └── platform/                         # Language/framework-specific gates
│       └── dotnet/
│           └── sig-s-b-001-storage-interface-abstraction-invariance.yaml
└── baselines/                            # Monotonic debt baseline snapshots (JSON)
    ├── SIG-P-B-001.json
    ├── SIG-P-B-002.json
    └── SIG-S-B-001.json
```

---

## 3. Phased Implementation Roadmap & Status

### Phase 1: Core Domain & Data Model (`CodeExplorer.Core`) ✅ DELIVERED
- [x] **1.1 Gate Models (`CodeExplorer.Core.Gates.Models`)**:
  - `SemanticGate`: Id, Name, Category (`Architectural`, `Platform`), Platform (`agnostic`, `dotnet`, `typescript`, etc.), Severity (`Error`, `Warning`), Description, Remediation, CypherQuery, RatchetSettings.
  - `GateViolation`: File, Line, Column, Symbol, Target, Message, Fingerprint.
  - `GateCheckResult`: Gate, Status (`Passed`, `FailedNew`, `FailedStaleRatchet`), Violations, Duration.
  - `GateBaseline`: GateId, AllowedCount, Violations list with fingerprints and rationale.
- [x] **1.2 Stable Fingerprint Generator (`GateFingerprintGenerator`)**:
  - Line-shift immune: `SHA256(GateId + NormalizedFilePath + SymbolName + Target)`.
- [x] **1.3 YAML/JSON Deserializer & Serializer (`GateSerializer`)**:
  - Load and save `.codeexplorer/gates/**/*.yaml` definitions with full backwards compatibility.

### Phase 2: Execution Engine & Monotonic Ratchet (`CodeExplorer.Core`) ✅ DELIVERED
- [x] **2.1 Semantic Gate Manager (`FileGateRepository`, `BuiltInGateCatalog`)**:
  - Discover gates in `.codeexplorer/gates/` (recursive search in `architectural/` and `platform/<platform>/`) and system built-ins.
  - Category and Platform filtering support.
- [x] **2.2 Execution Engine (`GateExecutionEngine`)**:
  - Run Cypher queries against `graph.db` using `CodeExplorerRepository`.
  - Zero-Match Contract evaluation: 0 rows = PASS, >0 rows = FAIL.
- [x] **2.3 Ratchet Service (`RatchetService`)**:
  - Compare live violations against baseline JSON.
  - Identify `NewViolations` (block build) vs `ToleratedViolations` (legacy debt).
  - Identify `StaleBaselineEntries` (debt resolved but not yet ratcheted down).

### Phase 3: CLI Commands (`UI/CodeExplorer`) ✅ DELIVERED
- [x] **3.1 Command Options (`GateOptions`)**:
  - `ce gate [check]` `[--gate <id>]` `[--category <cat>]` `[--platform <plat>]` `[--format table|json|sarif]`.
  - `ce gate list`: List all gates and tolerated debt.
  - `ce gate ratchet [--update]`: Automatically ratchet down baselines when debt is fixed.
  - `ce gate init`: Scaffold default starter gates organized into `architectural/` and `platform/`.
- [x] **3.2 Command Handler (`GateCommandHandler`)**:
  - Spectre.Console rendering with dynamically calculated column widths, clean separators, colored violations, and remediation guidance.
  - Return exit code `0` (clean or tolerated) or `1` (blocking violations).

### Phase 4: AI-Agent MCP Tool (`ce mcp`) ✅ DELIVERED
- [x] **4.1 Register `check_semantic_gates` Tool**:
  - Input: `gateId` (optional), `category` (optional), `platform` (optional), `changedFiles` (optional filter).
  - Output: Structured report with remediation steps tailored for LLM refactoring loops.
- [x] **4.2 Agent Prompt & Guidance Integration**:
  - Updated `.agents/skills/semantic-invariant-gates/SKILL.md` for AI coding agent guardrail verification.

### Phase 5: Standard Built-in Gate Library ✅ DELIVERED
- [x] **5.1 Clean Architecture / Transport Purity (`SIG-P-B-003`)**: Transport endpoints cannot directly invoke database queries.
- [x] **5.2 Storage Driver Abstraction (`SIG-S-B-001`)**: High-level domain logic couples to storage abstractions, not concrete drivers.
- [x] **5.3 Pipeline Layer Hierarchy (`SIG-P-B-001`)**: Parser stage $N$ must not call higher-order stages.
- [x] **5.4 Circular Package Shield (`SIG-P-I-001`)**: Inter-package dependencies form a strict DAG.
- [x] **5.5 In-Memory Visitor IO Purity (`SIG-C-I-001`)**: AST visitors must not perform ambient disk I/O.
- [x] **5.6 Hardcoded Arguments Gate (`SIG-M-I-001`)**: Production methods must not receive raw string literals.

---

### Phase 6: Structural Graph Invariants & Stage Ordering ✅ DELIVERED
- [x] **6.1 Structural Project-to-File Links (`Project` $\leftrightarrow$ `File`)**:
  - Emit `(p:Project)-[:CONTAINS]->(f:File)` and `(f:File)-[:BELONGS_TO]->(p:Project)` during Layer 3 indexing using existing `filesByProjectId` mapping.
  - Emit `(lit:CallLiteral)-[:DECLARED_IN]->(f:File)` to connect literals to files.
- [x] **6.2 Semantic Test & Project Kind Flag Propagation**:
  - Propagate `is_test: bool`, `project_kind: string`, and `project_name: string` onto `FileNode`, `CallLiteralNode`, `TypeNode`, `FunctionNode`.
  - Eliminate brittle string heuristics (`WHERE NOT file_path CONTAINS 'Test'`) across all gate Cypher queries.
- [x] **6.3 Ordered Layer & Pipeline Stage Relationships**:
  - Introduce `stage_order: int` via `WorkspaceConventions.GetPipelineStageOrder` on nodes for clean, non-combinatorial hierarchy checks:
    ```cypher
    MATCH (src)-[:CALLS|USES_TYPE]->(tgt)
    WHERE NOT coalesce(src.is_test, false)
      AND src.stage_order IS NOT NULL
      AND tgt.stage_order IS NOT NULL
      AND src.stage_order < tgt.stage_order
    ```
- [ ] **6.4 Polyglot Gate Library Expansion**:
  - Progressively implement the 24 platform gates defined in [docs/proposals/semantic-invariant-gates-proposal.md](../proposals/semantic-invariant-gates-proposal.md) (.NET, TS, Go, Python, Java, Kotlin/Android, SQL, ColdFusion).
