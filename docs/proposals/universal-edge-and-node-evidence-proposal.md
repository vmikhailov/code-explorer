# Universal Edge & Node Evidence Architecture Proposal 🔍
## Creation-Time Provenance for Layer 4 Nodes and Rolled-Up Edges

- **Document Version:** 2.0.0
- **Status:** Proposal (remaining work only)
- **Target Audience:** Core Maintainers, Systems Architects, Frontend/Extension Engineers
- **Target Subsystems:** `Analysis/Layers/Layer4_Semantic`, `Analysis/Layers/Layer5_Global`, `Analysis/Services/ArchitectureViewEngine.*Evidence*`, `Protocol`, `vscode-extension`

> **Already shipped (v1.52.0 – v1.53.5), not covered here:** the database evidence fixes (`items` JSON contract, manifest-path → directory scoping via `EvidenceScope`, two-hop `Table`/`Procedure` traversal), `GET /api/architecture/edge-evidence` (incl. grouped edges), `GET /api/architecture/node-evidence`, the unified `ArchitectureEvidenceItemDto`, and the `EdgeEvidenceSection` / `NodeEvidenceSection` inspector UI.

---

## 1. Problem

Evidence is currently **reconstructed at query time** by heuristics in
[ArchitectureViewEngine.EdgeEvidence.cs](../../cli/src/Core/CodeExplorer.Core/Analysis/Services/ArchitectureViewEngine.EdgeEvidence.cs) and
[ArchitectureViewEngine.DatabaseEvidence.cs](../../cli/src/Core/CodeExplorer.Core/Analysis/Services/ArchitectureViewEngine.DatabaseEvidence.cs).
The analyzers that decide an edge exists know exactly *which* symbol caused it, but discard that knowledge. The view engine then guesses it back, which produces both false positives and false negatives:

| Heuristic | Location | Failure mode |
| :--- | :--- | :--- |
| Query ↔ database matched by schema substring in SQL text, or engine name in query id/name | `GetDatabaseEvidenceAsync` (step 3) | Schema `order` matches `orders.`; engine `Postgres` matches every query whose id contains it. |
| ORM classification by path substring (`Context`, `Model`, `Entity`, any `.cs`/`.ts`) | `GetDatabaseEvidenceAsync` (step 4) | Every non-config source file is labelled `ORM`. |
| Fixed operation per relationship (`WRITES_TO` → `INSERT`, `READS_FROM` → `SELECT`) | Step 4b | `UPDATE`/`DELETE`/`MERGE` shown as `INSERT`. |
| Service ownership by directory prefix plus `Contains("/" + dir + "/")` fallback | `EvidenceScope.Matches` | Nested projects: the parent project claims the child's evidence; same-named folders collide. |
| Node resolution by `name` / `ENDS WITH` | `ResolveEvidenceNodesAsync`, `ResolveServiceScopeAsync` | Similarly named nodes resolve to the wrong entity. |
| Reverse-direction retry when a pair has no evidence | `GetEdgeEvidenceAsync` | Evidence for `B → A` displayed on an `A → B` arrow. |
| `LineStart = 1` when no line is known | All collectors | Claims an exact location that does not exist. |
| Empty `catch { }` blocks (~10) | All collectors | Query failures render as "no evidence", the original trust problem. |

Performance is also unbounded: `GetDatabaseEvidenceAsync` scans **all** `:Query` nodes and all `Function-[:DECLARES]->Query` edges per call, and a grouped edge such as `Services (6) → Topics (14)` fans out into 84 pair resolutions, each re-running `ResolveServiceScopeAsync` (uncached) plus 2–4 queries, doubled when the reverse retry fires.

---

## 2. Target Architecture: Law of Semantic Provenance

> Every Layer 4 node and every rolled-up Layer 4/5 relationship is a projection of one or more concrete Layer 1 (`File`, `ConfigEntry`) or Layer 3 (`Type`, `Function`, `Member`, `CallLiteral`) constructs. The analyzer that creates it **must record that provenance at creation time**. Evidence lookup becomes a deterministic read, never an inference.

This fits the pipeline split in the architecture guidelines: Layer 4 records **symbol-level** provenance as it discovers semantic constructs; Layer 5 **aggregates** it when rolling symbol edges up to service-level edges.

### 2.1 Decision: reuse semantic edges, no new `EVIDENCED_BY` relationship

The v1 draft proposed both a new `EVIDENCED_BY` edge type and reusing existing semantic edges. We standardise on **reusing existing semantic edges** because:

1. Symbol-level edges (`PUBLISHES_TO`, `SUBSCRIBES_TO`, `CALLS_ENDPOINT`, `PERSISTED_IN`, `DECLARES`, `READS_FROM`, `WRITES_TO`, `CONFIGURES`) already *are* the evidence; an `EVIDENCED_BY` edge would duplicate each one.
2. The property graph cannot attach a node to an **edge**, so `EVIDENCED_BY` cannot express provenance of rolled-up edges such as `SERVICE_CALL` or `USES_DB` anyway.

### 2.2 Storage model

**(a) Symbol-level edges (Layer 4)** — every semantic edge emitted from a code construct carries, in its relationship `Extensions`:

| Key | Meaning |
| :--- | :--- |
| `file_path` | File of the originating construct (call site, annotation, config entry). |
| `line`, `line_end` | Exact span of the construct; **omitted** when unknown (never defaulted to `1`). |
| `operation` | Real operation detected by the analyzer: `SELECT`/`INSERT`/`UPDATE`/`DELETE`/`EXEC`, `PUBLISH`/`SUBSCRIBE`, HTTP verb, `ORM`, `CONFIG`, `MIGRATION`. |
| `evidence_kind` | One of the shared kind constants (§2.3). |

The source node of the edge is always the concrete Layer 1/3 node, so `CallerSymbol` comes from the graph, not from path guessing.

**(b) Rolled-up edges (Layer 5)** — when `Layer5GlobalAnalyzer` / `PostIndexAnalyzer` collapse symbol-level edges into `SERVICE_CALL`, `USES_DB`, `PUBLISHES_TO` (service → topic), etc., they store in `Extensions`:

| Key | Meaning |
| :--- | :--- |
| `evidence_ids` | JSON array of the contributing symbol-level edge identities (`fromId|kind|toId`), capped at a configurable limit (default 200). |
| `evidence_count` | Total number of contributing edges (may exceed the cap). |
| `evidence_truncated` | `true` when the cap was hit. |

Relationship records already expose `Extensions` (see [ServiceCallRelationship.cs](../../cli/src/Core/CodeExplorer.Core/Common/Relationships/ServiceCallRelationship.cs)), so no schema migration is needed.

**(c) Layer 4 nodes** — `Endpoint`, `Topic`, `Query`, `Table`, `Procedure`, `Database`, `ExternalService` keep `file_path`/`line`/`line_end` of their primary discovery site. Nodes discovered only from config are linked `(cfg:File|ConfigEntry)-[:CONFIGURES]->(n)`.

### 2.3 Evidence kind vocabulary

Replace free-form `kind` strings with constants in `OntologyConstants.EvidenceKinds`, mirrored in `proto/types`:

`endpoint`, `entrypoint`, `http_call`, `grpc_call`, `wcf_call`, `publish`, `subscribe`, `query`, `procedure`, `orm`, `table`, `config`, `migration`, `library_call`, `test_call`, `type_reference`.

Also promote the `HANDLES` literal used in the edge-evidence code to `OntologyConstants.Relationships`.

### 2.4 Service ownership by structure, not by path

Replace directory-prefix scoping in `EvidenceScope` with structural containment, which is exact for nested projects:

```cypher
MATCH (s)-[:CONTAINS*1..3]->(sym) WHERE s.id IN $serviceIds RETURN s.id, sym.id
```

Path matching remains only as a fallback for nodes without a `CONTAINS` chain, and such items are flagged (see §3.2).

---

## 3. Resolver Changes

### 3.1 Evidence resolution becomes a lookup

| Selected element | New resolution |
| :--- | :--- |
| Rolled-up edge `A → B` | Read `evidence_ids` from the edge(s) between the resolved ids **in the rendered direction only**; load those symbol edges and their source nodes in one batched query. |
| Grouped edge `Group A → Group B` | Same, with `IN $sourceIds` / `IN $targetIds` in a single query instead of N×M pair calls. |
| Layer 4 node | Incoming symbol-level semantic edges + `CONFIGURES` + the node's own discovery span. |
| `Service → Database` | Union of symbol edges to the `Database` and to its `Table`/`Procedure` children, filtered by §2.4 containment; operation read from the edge, not inferred. |

### 3.2 Legacy fallback with explicit confidence

Until every analyzer records provenance, the existing heuristic collectors remain as a fallback, but:

- Each `ArchitectureEvidenceItemDto` gains `confidence: "recorded" | "inferred"`; heuristic results are always `inferred` and the UI renders an *Inferred* badge.
- The reverse-direction retry is removed; bidirectional edges are resolved by querying both directions explicitly and labelling each item with its direction.
- `lineStart`/`lineEnd` become nullable; the UI opens the file without a cursor position when absent.
- Empty `catch { }` blocks log through `ILogger` and add a `warnings` array to the response so the inspector can distinguish "no evidence" from "lookup failed".

### 3.3 Performance

- Resolve service scopes and node ids **once per request** (batched `IN` queries, request-local cache).
- Filter `:Query` scans by the resolved database/table ids instead of scanning all queries.
- Target: any single edge or group-edge evidence request completes in < 200 ms on a 200k-node graph.

---

## 4. Implementation Plan

### Step 1: Shared vocabulary & DTO hardening (½ day)
1. Add `OntologyConstants.EvidenceKinds` and the `HANDLES` relationship constant; mirror in `proto/types`.
2. Add `confidence` and `warnings`; make `lineStart`/`lineEnd` nullable in `ArchitectureEvidenceItemDto` and the TS types.
3. Replace empty `catch { }` blocks in the evidence collectors with logged warnings.

### Step 2: Record symbol-level provenance in Layer 4 (2–3 days)
1. Endpoint, messaging, HTTP/gRPC/WCF client, ORM, query, and configuration analyzers under `Layer4_Semantic` write `file_path`, `line`, `line_end`, `operation`, `evidence_kind` into the `Extensions` of every semantic edge they emit.
2. Query analyzers record the real SQL verb per statement.

### Step 3: Aggregate provenance on rolled-up edges in Layer 5 (1–2 days)
1. `Layer5GlobalAnalyzer` (messaging, databases) and `PostIndexAnalyzer` (call graph, edge normalisation) write `evidence_ids` / `evidence_count` / `evidence_truncated` when creating or merging service-level edges.

### Step 4: Switch resolvers to lookups (1–2 days)
1. Implement the §3.1 lookup paths with batched queries; keep heuristics only behind the §3.2 `inferred` fallback.
2. Replace `EvidenceScope` path matching with §2.4 containment.
3. Remove the reverse-direction retry.

### Step 5: Tests (alongside each step)
Extend [EdgeEvidenceTests.cs](../../cli/tests/CodeExplorer.Tests/EdgeEvidenceTests.cs) with:
- Nested projects: a parent project does not claim the child project's evidence.
- Directional edges: `A → B` never shows `B → A` evidence.
- `UPDATE`/`DELETE` queries report their real operation.
- Schema-substring collision (`order` vs `orders`) yields no false match.
- Grouped edge resolution issues a bounded number of queries independent of group size.
- Rolled-up edges carry `evidence_ids` matching their contributing symbol edges, and truncation is flagged.

### Step 6: Retire heuristics
Once analyzers in every supported language record provenance, delete the heuristic collectors and the `inferred` fallback path.
