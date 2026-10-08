# Semantic Invariant Gates (SIG) Implementation Plan 🛡️

## 1. Context & Motivation
Large projects (such as `HearAI`, enterprise microservices, and AI-assisted workflows) suffer from architectural drift and regression during continuous iteration.
In projects using manual AST scripts (e.g. 179 Python `ast.walk` tests in HearAI), rules are fragile, slow, single-language, and expensive to maintain.

**CodeExplorer** already maintains a high-fidelity semantic graph (`graph.db`) with polyglot support (C#, Python, Java, Kotlin, TypeScript, Go, SQL), sub-millisecond OpenCypher querying, and native MCP support.

By introducing **Semantic Invariant Gates (SIG)**, CodeExplorer becomes an active architectural guardrail:
- **Declarative Zero-Match Invariants**: Express forbidden architectural topology in OpenCypher.
- **Monotonic Technical Debt Ratchets**: Legacy violations are baselined; debt can only strictly decrease ($N_{t+1} \le N_t$).
- **Sub-Second Local Pre-Commit / CI Checks**: Fast execution (< 50ms) directly over SQLite.
- **Autonomous AI-Agent Guardrails**: Native MCP tool `check_semantic_gates` allowing agents to self-verify code changes before commit.

---

## 2. Directory Layout & Architecture

```
.codeexplorer/
├── gates/                        # Workspace gate definitions (YAML)
│   ├── SIG-001-transport-purity.yaml
│   ├── SIG-002-chokepoint-isolation.yaml
│   └── SIG-003-dead-public-endpoints.yaml
└── baselines/                    # Monotonic debt baseline snapshots (JSON)
    ├── SIG-001-transport-purity.json
    └── SIG-002-chokepoint-isolation.json
```

---

## 3. Phased Implementation Roadmap

### Phase 1: Core Domain & Data Model (`CodeExplorer.Core`)
- [ ] **1.1 Gate Models (`CodeExplorer.Core.Gates.Models`)**:
  - `SemanticGate`: Id, Name, Category, Severity (`Error`, `Warning`), Description, Remediation, CypherQuery, RatchetSettings.
  - `GateViolation`: File, Line, Symbol, Target, Message, Fingerprint.
  - `GateCheckResult`: Gate, Status (`Passed`, `FailedNew`, `FailedStaleRatchet`), Violations, Duration.
  - `GateBaseline`: GateId, AllowedCount, Violations list with fingerprints and rationale.
- [ ] **1.2 Stable Fingerprint Generator (`GateFingerprintGenerator`)**:
  - Line-shift immune: `SHA256(GateId + NormalizedFilePath + SymbolName + Target)`.
- [ ] **1.3 YAML Deserializer / Serializer**:
  - Load and save `.codeexplorer/gates/*.yaml` definitions.

### Phase 2: Execution Engine & Monotonic Ratchet (`CodeExplorer.Core`)
- [ ] **2.1 Semantic Gate Manager (`SemanticGateManager`)**:
  - Discover gates in `.codeexplorer/gates/` and system built-ins.
  - Validate Cypher read-only safety via `CypherSecurityValidator`.
- [ ] **2.2 Execution Engine (`GateExecutionEngine`)**:
  - Run Cypher queries against `graph.db` using `CodeExplorerRepository`.
  - Zero-Match Contract evaluation: 0 rows = PASS, >0 rows = FAIL.
- [ ] **2.3 Ratchet Service (`RatchetService`)**:
  - Compare live violations against baseline JSON.
  - Identify `NewViolations` (block build) vs `ToleratedViolations` (legacy debt).
  - Identify `StaleBaselineEntries` (debt resolved but not yet ratcheted down).

### Phase 3: CLI Commands (`UI/CodeExplorer`)
- [ ] **3.1 Command Options (`GateOptions`)**:
  - `ce gate [check]` `[--gate <id>]` `[--category <cat>]` `[--format table|json|sarif]`.
  - `ce gate list`: List all gates and tolerated debt.
  - `ce gate ratchet [--update]`: Automatically ratchet down baselines when debt is fixed.
  - `ce gate init`: Scaffold default starter gates.
- [ ] **3.2 Command Handler (`GateCommandHandler`)**:
  - Ansi/Spectre console rendering with colored violations and remediation guidance.
  - Return exit code `0` (clean or tolerated) or `1` (blocking violations).

### Phase 4: AI-Agent MCP Tool (`ce mcp`)
- [ ] **4.1 Register `check_semantic_gates` Tool**:
  - Input: `gateId` (optional), `changedFiles` (optional filter).
  - Output: Structured report with remediation steps tailored for LLM refactoring loops.
- [ ] **4.2 Agent Prompt & Guidance Integration**:
  - Update agent documentation so AI coding agents run `check_semantic_gates` prior to creating git commits.

### Phase 5: Standard Built-in Gate Library
- [ ] **5.1 Clean Architecture / Layer Purity**: Transport handlers cannot directly invoke ORM mutations (`Add`, `Update`, `Delete`, `SaveChanges`).
- [ ] **5.2 Mutation Chokepoint Protection**: Sensitive entity state changes must pass through designated domain services.
- [ ] **5.3 Dead Endpoint Detection**: Public API endpoints with zero inbound references from frontend or tests.
- [ ] **5.4 Circular Dependency Shield**: Inter-service circular dependency detection.
