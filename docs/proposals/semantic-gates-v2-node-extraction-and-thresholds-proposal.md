# Proposal: Semantic Invariant Gates v2 — Node-Aware Extraction, Parameterized Templates, Call-Site Coordinates & Threshold Governance

## 1. Problem Statement & Motivation

Semantic Invariant Gates (SIG) enforce architectural, security, ontology, and evolution invariants using OpenCypher queries following the **Zero-Match Contract** ($R(Q) = \emptyset \implies \text{PASS}$).

While the core CLI engine (`GateExecutionEngine`, `RatchetService`, `SemanticGate`) recently introduced initial support for node-return parsing, parameterized `message` templates, and `warning`/`error` thresholds, several critical architectural gaps remain across the end-to-end pipeline:

1. **Query Boilerplate in the 31 Built-In Gates (`BuiltInGateCatalog`)**:
   All 31 built-in gates still use legacy scalar projections (`RETURN coalesce(fn.symbol, fn.file_path) AS file, coalesce(fn.start_line, 0) AS line, ... 'Hardcoded string' AS message`). This couples graph pattern matching with presentation formatting, omits column numbers (`start_col`), and discards graph node IDs (`NodeId`, `TargetNodeId`) needed for IDE/graph drill-down.
2. **Function Declaration Line vs. Exact Call-Site Line (`CALLS` Edge Coordinates)**:
   When a gate matches `(fn:Function)-[r:CALLS]->(target)`, returning `fn` resolves `line` and `column` to the **enclosing function declaration** (`fn.start_line:fn.start_col`) rather than the exact line where the prohibited call occurs inside the function body. Currently, `Reference` in [`BaseParserVisitor.cs`](../../cli/src/Core/CodeExplorer.Core/Parser/TreeSitter/BaseParserVisitor.cs) does not record `Line` and `Column` from the Tree-sitter call expression node onto the `CALLS` edge properties.
3. **Server Protocol, MCP & VS Code `GateEditorView` Disconnection**:
   - [`WebSocketServerHandler.Endpoints.cs`](../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.Endpoints.cs) (`TestGateQueryAsync`, `SaveGateAsync`, `MapGateSuiteToDto`) still enforces the legacy 5-column contract (`requiredContract = ["file", "line", "symbol", "target", "message"]`), flagging valid node-returning queries (`RETURN fn, target`) as having missing columns.
   - [`proto/types.ts`](../../proto/types.ts) and [`GateEditorView.tsx`](../../vscode-extension/src/webview/components/GateEditorView.tsx) lack fields and UI controls for `message` templates, `thresholds: { warning, error }`, `Warning` gate status badges, and `nodeId` / `targetNodeId` graph inspection links.
4. **CI/CD Annotations & SARIF Export**:
   Threshold-classified `Warning` vs `Failed` violations are not yet emitted as GitHub Actions workflow annotations (`::warning file=...,line=...,col=...::...` / `::error ...`) or SARIF v2.1.0 diagnostic levels (`warning` vs `error`).

---

## 2. Architecture Overview

```mermaid
flowchart LR
  subgraph YAML["1. Gate Definition (.yaml / Catalog)"]
    Q["Cypher Query\nRETURN fn, r, target"]
    M["Message Template\n'{fn.name} calls {target.name}'"]
    T["Thresholds\nwarning: 1, error: 30"]
  end

  subgraph Engine["2. GateExecutionEngine"]
    NE["Node & Edge Extractor\n(srcNode, callEdge, tgtNode)"]
    LOC["Exact Location Resolver\ncoalesce(r.line, src.start_line)"]
    INT["Template Interpolator\n{file}, {symbol}, {fn.name}"]
    FP["Line-Shift Immune Fingerprint\nSHA256(gateId + path + sym + tgt + #occ)"]
  end

  subgraph Ratchet["3. RatchetService & Thresholds"]
    BL["Baseline Filter\nActive = Total - Tolerated"]
    TH["Threshold State Machine\nPassed | Tolerated | Warning | Failed"]
  end

  subgraph Consumers["4. Consumers (CLI, MCP, VS Code, CI)"]
    CLI["CLI: --fail-on-warning\n--max-warnings <N>\nGitHub Annotations"]
    VSC["VS Code GateEditorView\nNode Drill-Down & Threshold UI"]
  end

  YAML --> Engine --> Ratchet --> Consumers
```

---

## 3. Core Design Pillars

### Pillar 1: Node-First & Edge-Aware Query Contract

Instead of projecting flattened strings, gate queries return matched graph entities directly:

```yaml
id: "SIG-R-C-001"
name: "Rust Async Tokio Anti-Blocking Invariant Gate"
category: "Rules"
platform: "rust"
scope: "Method"
dimension: "Rules"
severity: "Error"
description: "Async functions running on Tokio runtime must not invoke synchronous blocking calls."
remediation: "Use tokio::time::sleep or spawn_blocking instead of synchronous blocking calls."
message: "Async function '{fn.name}' invokes potential blocking call: '{target.name}'"
thresholds:
  warning: 1
  error: 30
query: |
  MATCH (fn:Function)-[r:CALLS]->(target)
  WHERE (fn.is_async = 'true' OR fn.is_async = true)
    AND target.name IN ['sleep', 'read', 'write', 'read_to_string']
    AND NOT coalesce(fn.is_test, false)
  RETURN fn, r, target
ratchet:
  enabled: true
  baseline: "baselines/SIG-R-C-001.json"
```

#### Automatic Entity & Location Extraction Precedence (`GateExecutionEngine`)
When `ParseViolations` inspects each JSON result row:
1. **Strict Node vs. Relationship Disambiguation**:
   - **Graph Node objects**: Identified by `"id"` and `"kind"` (without `"from"`/`"to"` or `"source"`/`"target"`).
   - **Graph Relationship (Edge) objects**: Identified by `"from"`/`"to"` (or `"source"`/`"target"`) or explicit relationship aliases (`r`, `rel`, `edge`, `call`). Relationship objects are **never** placed into `graphNodes`, preventing `r` in `RETURN fn, r, target` from being misclassified as `tgtNode`.
2. **Source Node (`srcNode`)**: Resolved from aliases `fn`, `func`, `function`, `src`, `source`, `caller`, `cls`, `class`, `type`, `node`, `from`, `p`, `project`, or the first returned graph node with `file_path` / `start_line`.
   - Extracts `File`, `Symbol`, `NodeId`, and fallback `start_line` / `start_col`.
3. **Relationship / Call-Site Edge (`relEdge`) & Multi-Call-Site Unrolling**:
   - If the row contains a relationship object (`r`, `rel`, `edge`, `call`) or a `CallLiteral` node (`lit`):
   - Overrides `Line` and `Column` with the **exact call-site coordinates** (`coalesce(r.line, lit.line, srcNode.start_line)` and `coalesce(r.column, lit.column, srcNode.start_col)`).
   - If `r.properties.call_sites` contains multiple `"line:col"` entries (e.g., when `fn` calls `target` multiple times in the same function body), `ParseViolations` automatically unrolls each call site into its own `GateViolation` at that exact `line:col`, incrementing `Occurrence` (`1, 2, ... k`) so each call site has a distinct **Line-Shift Immune Fingerprint** (`SHA256(GateId + File + Symbol + Target + Occurrence)`).
4. **Target Node (`tgtNode`) & Parameterized `target` Template**:
   - Resolved from aliases `target`, `tgt`, `callee`, `destination`, `to`, `pkg`, `package`, `dep`, `dependency`, `contract`, `tbl`, `table`, or the second distinct graph node.
   - Extracts `Target` (`name ?? full_name ?? id`) and `TargetNodeId`.
   - For **single-node or aggregate gates** (`RETURN fn` or `RETURN cls, method_count`), `SemanticGate` supports an optional `target` template field (e.g. `target: "GodClass"` or `target: "{cnt} dependencies"`). This ensures 100% deterministic `Target` resolution and preserves existing `SHA256` baseline hashes during v1 $\to$ v2 migration.
5. **100% Backward Compatibility**: Queries projecting explicit scalar columns (`file`, `line`, `column`, `symbol`, `target`, `message`) continue to work unchanged; explicit scalar columns take precedence over inferred node properties.

---

### Pillar 2: Multi-Call-Site Evidence Provenance on `CALLS` and `USES_TYPE` Edges

In SQLite (`edges` table), relationships are keyed by `PRIMARY KEY (from_id, to_id, kind)`. If function `A` calls `B` three times (at lines 15, 42, and 88), a single `(A)-[:CALLS]->(B)` edge exists in the database. Storing only scalar `line` and `column` would lose 2 of the 3 call sites.

To support both **Semantic Invariant Gates v2** and **Universal Edge Evidence** in the Architecture/Project Maps:

1. **AST Reference Coordinates (`Reference` record in `CodeExplorer.Core`)**:
   Extend `Reference` in [`ParseResult.cs`](../../cli/src/Core/CodeExplorer.Core/Models/ParseResult.cs) with optional `int Line = 0` and `int Column = 0`:
   ```csharp
   public record Reference(
       string SourceSymbol,
       string TargetSymbol,
       string Kind,
       int Line = 0,
       int Column = 0,
       string? ReceiverType = null);
   ```
2. **Tree-Sitter Call Extraction (`BaseParserVisitor.cs` & Language Visitors)**:
   When recording `new Reference(..., "CALLS")` or `"USES_TYPE"`, populate `Line = (int)node.StartPosition.Row + 1` and `Column = (int)node.StartPosition.Column + 1`.
3. **Edge Evidence Aggregation (`EvidenceProvenanceHelper` & `SqliteGraphClient`)**:
   When multiple `Reference` instances or duplicate `(From, To, Kind)` relationships are merged:
   - `line` and `column`: Store the first call-site coordinates (`int`) for fast scalar Cypher filtering/sorting (`r.line`).
   - `call_sites` (`ev_sites`): Compact JSON array of `"line:col"` strings (e.g. `["15:9", "42:13", "88:5"]`, capped by `EvidenceProvenanceHelper.DefaultEvidenceCap`) when more than one call site exists (or uniformly for all call edges).
   - `call_count`: Total number of call sites (`int`) between `From` and `To`.
   - Roll-up edges (Layer 2 `PROJECT_DEPENDS_ON`, Layer 5 `TRANSITIVELY_CALLS`) continue to merge provenance via `EvidenceProvenanceHelper.MergeProvenance`.

---

### Pillar 3: Parameterized Message & Target Templates

Decoupling the violation message and target label from the Cypher query keeps queries focused purely on graph predicates:

- **Properties**:
  - `message` (alias `message_template`) on `SemanticGate`.
  - `target` (optional target template/label for single-node or metric gates) on `SemanticGate`.
- **Supported Interpolation Tokens**:
  - **Standard Resolved Tokens**: `{file}`, `{line}`, `{column}` (or `{col}`), `{symbol}`, `{target}`, `{location}` (formatted as `file:line:col`), `{gate.id}`, `{gate.name}`.
  - **Dot-Notation Node/Edge Properties**: `{fn.name}`, `{target.name}`, `{r.line}`, `{r.call_count}`, `{pkg.version}`, `{cls.cyclomatic_complexity}`, etc., looked up against returned graph nodes/edges in the row.
  - **Scalar & Collection Row Columns**: Any custom scalar projection alias in `RETURN` (e.g., `{cycle_length}`, `{fan_out}`) as well as `JsonValueKind.Array` projections (e.g., `collect(target.name) AS targets` $\to$ formatted as comma-separated `"a, b, c"`).

---

### Pillar 4: Threshold-Based Classification (`Warning` vs `Error`) & Ratchet Integration

Not every architectural smell should immediately block CI on the first occurrence, while exceeding a ceiling must fail the build.

#### 1. YAML Configuration
```yaml
severity: "Warning" # Default base severity if thresholds are omitted
thresholds:
  warning: 1        # >= 1 active (un-baselined) violation -> GateCheckStatus.Warning
  error: 10         # >= 10 active (un-baselined) violations -> GateCheckStatus.Failed
```

#### 2. Interaction with Monotonic Ratchet Baselines & Implicit Warning Guard
- **Ratcheted Mode (`ratchet.enabled: true`)**:
  Tolerated baseline violations represent acknowledged legacy debt and **do not count** toward active warning/error thresholds:
  $$\text{ActiveViolations} = |\text{Violations} \setminus \text{Baseline}|$$
- **Global Budget Mode (`ratchet.enabled: false`)**:
  $$\text{ActiveViolations} = |\text{Violations}|$$
- **State Machine Rules**:
  - If $\text{ActiveViolations} == 0$:
    - Status is `PassedTolerated` (if $|\text{Tolerated}| > 0$) or `Passed`.
    - If $|\text{Violations}| < \text{Baseline.AllowedViolations}$, `CanRatchetDown = true` (ratchet opportunity to tighten the baseline).
  - If $\text{ActiveViolations} > 0$:
    - If `thresholds.error` is set and $\text{ActiveViolations} \ge \text{thresholds.error} \implies$ `Failed`.
    - Else if `thresholds.warning` is set and $\text{ActiveViolations} \ge \text{thresholds.warning} \implies$ `Warning`.
    - **Implicit Warning Guard**: If `thresholds.error` is set (e.g. `error: 5`), `thresholds.warning` is omitted, and $0 < \text{ActiveViolations} < \text{thresholds.error}$, the gate defaults to `Warning` (never silently `Passed` when active violations exist!).

#### 3. Status Taxonomy (`GateCheckStatus`)
| Status | CLI Badge | Default Exit Code | Meaning |
| :--- | :--- | :---: | :--- |
| `Passed` | `[PASS]` | `0` | 0 violations. |
| `PassedTolerated` | `[TOLERATED]` | `0` | 0 new violations; all existing matches are in the ratchet baseline. |
| `Warning` | `[WARN]` | `0` (`1` with `--strict`) | Active violations $\ge \text{warning}$ (or $> 0$ when below `error`). |
| `Failed` | `[FAIL]` | `1` | Active violations $\ge \text{error}$ (or `Severity == Error` without thresholds). |
| `Error` | `[ERR]` | `1` | Query syntax or execution failure. |

#### 4. CLI Flags & CI Pipeline Controls
- `ce gate check --fail-on-warning` (alias `--strict`): Elevates any `Warning` gate to exit code `1`.
- `ce gate check --max-warnings <N>`: Fails the suite with exit code `1` if total new warnings across the suite exceed $N$.
- `ce gate check --format github` (or auto-detected when `GITHUB_ACTIONS=true`): Emits `::warning file={file},line={line},col={col},title={gateId}::{message}` and `::error ...` annotations.

---

### Pillar 5: Server Protocol, MCP, VS Code `GateEditorView` & Native Inspector Integration

1. **Protocol DTOs ([`proto/types.ts`](../../proto/types.ts) & [`WebSocketDtos.cs`](../../cli/src/UI/CodeExplorer/Server/WebSocketDtos.cs))**:
   - `GateViolationDto`: Add `nodeId?: string` and `targetNodeId?: string`.
   - `GateItemDto`: Add `message?: string`, `target?: string`, `thresholds?: { warning?: number; error?: number }`, and include `'Warning'` in `status`.
   - `GateSuiteSummaryDto`: Add `warningCount: number`.
   - `TestGateRequestDto`: Add `message?: string` and `target?: string` so live query testing in `GateEditorView` previews interpolated messages and targets.
2. **Contract Validation in `TestGateQueryAsync` ([`WebSocketServerHandler.Endpoints.cs`](../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.Endpoints.cs))**:
   - Update `missingColumns` validation: if `ParseViolations` successfully extracts `File` and `Symbol` from a returned graph node (e.g. `RETURN fn, target`), mark `"file"`, `"line"`, `"symbol"`, `"target"` as satisfied by node extraction, and mark `"message"` as satisfied if `message` template is provided on the gate.
3. **VS Code `GateEditorView` & Native Sidebar Inspector (`codeExplorerInspectorView`)**:
   - Add **Message Template** and optional **Target Template** inputs with token autocomplete hints (`{file}`, `{symbol}`, `{target}`, `{fn.name}`).
   - Add **Thresholds (`Warning` / `Error`)** numeric inputs in the gate metadata form.
   - Update `STARTER_TEMPLATE` to use the clean node-return pattern (`RETURN caller, r, callee` + `message` template).
   - Render `[WARN]` badges in amber and enable one-click navigation using `file:line:column` as well as graph node/edge focus (`nodeId` / `targetNodeId`) in the Architecture/Project Map and `codeExplorerInspectorView`.

---

## 4. Phased Implementation Plan

### Phase 1: Core Engine & CLI Thresholds *(Completed in CLI/Core)*
- [x] `GateThresholdSettings` (`Warning`, `Error`) and `Message` template on [`SemanticGate`](../../cli/src/Core/CodeExplorer.Core/Gates/Models/SemanticGate.cs).
- [x] `NodeId` and `TargetNodeId` on [`GateViolation`](../../cli/src/Core/CodeExplorer.Core/Gates/Models/GateResult.cs).
- [x] Graph node auto-extraction and `{token}` / `{node.prop}` interpolation in [`GateExecutionEngine`](../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs).
- [x] `GateCheckStatus.Warning` and threshold evaluation in [`RatchetService`](../../cli/src/Core/CodeExplorer.Core/Gates/Engine/RatchetService.cs).
- [x] `--fail-on-warning` (`--strict`) and `--max-warnings` in [`GateOptions`](../../cli/src/UI/CodeExplorer/Options/GateOptions.cs) and [`GateCommandHandler`](../../cli/src/UI/CodeExplorer/Commands/GateCommandHandler.cs).
- [x] Unit test coverage in [`SemanticInvariantGateTests.cs`](../../cli/tests/CodeExplorer.Tests/SemanticInvariantGateTests.cs).

### Phase 2: Edge Evidence Call-Site Coordinates & Engine Refinement
- [ ] Add `Line` and `Column` to `Reference` in [`ParseResult.cs`](../../cli/src/Core/CodeExplorer.Core/Models/ParseResult.cs) and populate them across Tree-sitter visitors ([`BaseParserVisitor.cs`](../../cli/src/Core/CodeExplorer.Core/Parser/TreeSitter/BaseParserVisitor.cs) and language visitors) when creating `CALLS` and `USES_TYPE` references.
- [ ] Persist `line`, `column`, `call_sites` (`["line:col", ...]`), and `call_count` onto `CALLS` / `USES_TYPE` edge properties during Layer 3/5 edge construction and deduplication in [`SqliteGraphClient.cs`](../../cli/src/Core/CodeExplorer.Core/Database/SqliteGraphClient.cs) / [`EvidenceProvenanceHelper.cs`](../../cli/src/Core/CodeExplorer.Core/Analysis/EvidenceProvenanceHelper.cs).
- [ ] Refine [`GateExecutionEngine.ParseViolations`](../../cli/src/Core/CodeExplorer.Core/Gates/Engine/GateExecutionEngine.cs):
  - Strictly separate **Node** vs. **Relationship** objects so `r` in `RETURN fn, r, target` is never treated as `tgtNode`.
  - Unroll `r.properties.call_sites` into individual call-site violations with accurate `Occurrence` counters.
  - Support optional `gate.Target` template interpolation and `JsonValueKind.Array` formatting in `InterpolateMessage`.
  - Enforce the Implicit Warning Guard when `thresholds.error` is specified without `thresholds.warning`.

### Phase 3: Migrate 31 Built-In Gates Catalog to Node-First + Message Templates
- [ ] Refactor [`BuiltInGateCatalog.cs`](../../cli/src/Core/CodeExplorer.Core/Gates/Storage/BuiltInGateCatalog.cs), [`BuiltInGateCatalog.EvolutionGates.cs`](../../cli/src/Core/CodeExplorer.Core/Gates/Storage/BuiltInGateCatalog.EvolutionGates.cs), and [`BuiltInGateCatalog.PlatformAndResourceGates.cs`](../../cli/src/Core/CodeExplorer.Core/Gates/Storage/BuiltInGateCatalog.PlatformAndResourceGates.cs) to set `Message = "..."` (and `Target = "..."` where needed for single-node gates) and return graph nodes/edges (`RETURN src, r, target`), verifying 100% fingerprint stability against `.codeexplorer/gates/baselines/*.json`.
- [ ] Update `.codeexplorer/gates/*.yaml` in the CodeExplorer repository and [`.agents/skills/semantic-invariant-gates/SKILL.md`](../../.agents/skills/semantic-invariant-gates/SKILL.md).

### Phase 4: Server DTOs, WebSocket Endpoints & VS Code `GateEditorView`
- [ ] Update [`WebSocketDtos.cs`](../../cli/src/UI/CodeExplorer/Server/WebSocketDtos.cs), [`WebSocketServerHandler.Endpoints.cs`](../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.Endpoints.cs), and [`proto/types.ts`](../../proto/types.ts) with `message`, `target`, `thresholds`, `warningCount`, `nodeId`, and `targetNodeId`.
- [ ] Fix `TestGateQueryAsync` column contract check to recognize node-returning queries and `message` templates without false-positive `missingColumns` warnings.
- [ ] Update [`GateEditorView.tsx`](../../vscode-extension/src/webview/components/GateEditorView.tsx) and `GatesTreeProvider` to edit `message`, `target`, and `thresholds`, display `Warning` statuses, and jump to exact `file:line:column` locations.


