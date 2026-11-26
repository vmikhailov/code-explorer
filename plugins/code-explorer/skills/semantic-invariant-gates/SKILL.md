---
name: semantic-invariant-gates
description: Author, verify, and ratchet deterministic Semantic Invariant Gates (SIG) in Cypher to enforce architectural boundaries, CQRS purity, and authorization policies in CodeExplorer.
---

# Semantic Invariant Gates (SIG) Workflow

Use this skill when defining architectural or security rules in `.codeexplorer/gates/*.yaml`, checking gate violations, or baselining legacy technical debt with ratchets.

## 1. Zero-Match Gate Contract

Every Semantic Invariant Gate (`SIG-*`) is a declarative YAML manifest backed by a Cypher query against the CodeExplorer graph:
- **0 rows returned** -> **PASS** (the invariant holds across the codebase).
- **1 or more rows returned** -> **VIOLATION** (evaluated against optional `thresholds` and `ratchet` baselines).

## 2. Authoring a Gate Manifest (`.codeexplorer/gates/*.yaml`)

1. Call `get_taxonomy` first so every node label (`Endpoint`, `Policy`, `Function`, `Type`, `Table`, `Message`, `CallArgument`) and relationship kind (`EXPOSED_BY`, `GUARDED_BY`, `CALLS`, `READS_FROM`, `WRITES_TO`, `PASSES_ARGUMENT`) matches the ontology schema.
2. Prototype and test your Cypher query interactively with `execute_custom_read_cypher` (`LIMIT 25`), excluding test code (`WHERE NOT coalesce(fn.is_test, false)`).
3. Write the gate manifest under `.codeexplorer/gates/` using node-first `RETURN` projections:

```yaml
id: SIG-SEC-001
category: Architectural
platform: agnostic
scope: Method
dimension: Boundaries
name: Mutating Endpoints Must Declare an Authorization Policy
severity: Error
description: State-mutating HTTP endpoints (POST, PUT, PATCH, DELETE) must be protected by a Policy or explicit anonymous marker.
remediation: Attach [Authorize], @UseGuards, @PreAuthorize, or an explicit [AllowAnonymous] / @Public marker.
target: "{ep.name} -> {tbl.name}"
message: "Mutating endpoint '{ep.name}' writes to '{tbl.name}' via '{w.name}' without a GUARDED_BY policy"
query: |
  MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)
        -[:CALLS*0..4]->(w:Function)-[:WRITES_TO]->(tbl:Table)
  WHERE ep.http_method IN ['POST', 'PUT', 'PATCH', 'DELETE']
    AND NOT fn.is_test AND NOT w.is_test
    AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy))
  RETURN DISTINCT fn, ep, w, tbl
ratchet:
  enabled: true
  baseline: baselines/SIG-SEC-001.json
```

## 3. Verification & Ratcheting

- Re-index modified code with `scan_workspace` (`clear: false`), then run `check_semantic_gates` to verify **`failed: 0`** and **`newViolations: 0`**.
- Starter templates shipped in `.codeexplorer/gates/*.yaml.example` (layer hierarchy, CQRS read/write purity, DDD table ownership, and AuthZ/PII security checks) can be activated by renaming `.yaml.example` to `.yaml`.
