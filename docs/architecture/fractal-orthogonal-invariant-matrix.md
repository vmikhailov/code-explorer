# Fractal Orthogonal Invariant Matrix (FOIM) 🛡️
## Formal Architecture Specification for CodeExplorer Semantic Governance

- **Document Version:** 1.0.0
- **Target Audience:** Systems Architects, Compiler Engineers, and Autonomous AI Coding Agents
- **Target Subsystem:** `CodeExplorer.Core.Gates`, `CodeExplorer.UI.Commands`, `CodeExplorer.Core.Mcp`

---

## 1. Executive Summary & Problem Statement

### 1.1 The Failure of Ad-Hoc Testing & Syntax Gating
Historically, software quality assurance relies on two disparate extremes:
1. **Ad-Hoc Integration / Scenario Tests:** Complex, slow (minutes), and brittle tests (e.g. spinning up databases or browser swarms). They test a single execution path of a single function, leaving the rest of the codebase exposed.
2. **Syntactic AST / Regex Linters:** Flat collections of ad-hoc scripts (e.g. hundreds of regex scanners checking substrings, line counts, or brace matching). These fail due to a **combinatorial explosion**: there are not 200 bugs in a codebase, but hundreds of thousands of potential syntactic variations. They produce false positives, require manual baseline maintenance, and collapse under refactoring.

### 1.2 The Solution: The Fractal Orthogonal Invariant Matrix (FOIM)
Rather than enumerating thousands of specific bugs, the **Fractal Orthogonal Invariant Matrix (FOIM)** defines a formal, closed coordinate system for code architecture.

Architecture is decomposed into:
- **5 Nested Levels of Granularity (Scope)**: From a single function up to the distributed system.
- **4 Orthogonal Invariant Dimensions**: The four fundamental laws governing software entities.

Every architectural rule in modern software engineering maps to exactly **one cell** $(i, j)$ in this $5 \times 4$ matrix. In `CodeExplorer`, each cell is evaluated not by brittle regex scripts, but by **declarative, sub-millisecond OpenCypher queries** executed against the unified SQLite semantic graph (`graph.db`).

---

## 2. The Coordinate System

```
                         GRANULARITY ZOOM LEVELS (SCOPE)
                ┌──────────┬──────────┬──────────┬──────────┬─────────────┐
                │  Method  │  Class   │  Module  │ Service  │   System    │
┌───────────────┼──────────┼──────────┼──────────┼──────────┼─────────────┤
│ 1. Contract   │  [M, C]  │  [C, C]  │  [P, C]  │  [S, C]  │   [A, C]    │
├───────────────┼──────────┼──────────┼──────────┼──────────┼─────────────┤
│ 2. Rules/Impl │  [M, I]  │  [C, I]  │  [P, I]  │  [S, I]  │   [A, I]    │
├───────────────┼──────────┼──────────┼──────────┼──────────┼─────────────┤
│ 3. Resources  │  [M, R]  │  [C, R]  │  [P, R]  │  [S, R]  │   [A, R]    │
├───────────────┼──────────┼──────────┼──────────┼──────────┼─────────────┤
│ 4. Boundaries │  [M, B]  │  [C, B]  │  [P, B]  │  [S, B]  │   [A, B]    │
└───────────────┴──────────┴──────────┴──────────┴──────────┴─────────────┘
```

### 2.1 Granularity Levels (Scope)
1. **Method (`Method` / `Function`)**: Atomic callable block of instructions.
2. **Class (`Class` / `Type` / `Struct` / `Interface`)**: Data structure and encapsulated behaviors.
3. **Module (`Package` / `Folder` / `Namespace`)**: Cohesive grouping of types sharing an internal lifecycle.
4. **Service (`Service` / `App` / `Library` / `Worker` / `CliTool`)**: First-class boundary entity with dedicated deployment or dependency semantics (CodeExplorer Layer 2 entities).
5. **System (`Application Suite` / `Workspace`)**: The macro-topology representing all interconnected services, databases, and message brokers.

### 2.2 Orthogonal Dimensions
1. **Dimension 1: External Contract (C)** — Interface definition, input/output typing, public protocol stability, wire fidelity.
2. **Dimension 2: Implementation Rules (I)** — State mutations, transactional guarantees, exception hygiene, purity, concurrency.
3. **Dimension 3: Resource & Complexity Control (R)** — Cognitive/cyclomatic complexity, cohesion (LCOM), coupling, coupling metrics ($I$, $A$, $D$).
4. **Dimension 4: Scope Boundaries & Direction (B)** — Dependency Inversion (DAG direction), Layer isolation, data access restrictions, privacy lineage.

---

## 3. The 20 Invariant Cells: Complete Taxonomy & Cypher Models

### Level 1: Method Scope

#### [M, C] Method $\times$ External Contract
- **Invariant:** A public method must declare explicit parameter and return types (no untyped `object`, raw `dict`, or `any`). Method parameter count must not exceed $K$ (default: $\le 4$).
- **Anti-Pattern Prevented:** Untyped dictionary returns ("mock-green trap"), runtime `AttributeError` / `KeyError`, parameter-soup.
- **Cypher Invariant (Zero-Match Contract):**
```cypher
MATCH (m:Method)
WHERE m.is_public = true 
  AND (m.return_type IN ['dict', 'object', 'any', 'NoneType'] OR m.parameter_count > 4)
RETURN m.file, m.name, m.return_type, m.parameter_count
```

#### [M, I] Method $\times$ Implementation Rules
- **Invariant:** A method must not swallow critical exceptions silently (`catch (...) { /* empty */ }`). Write methods must guarantee execution determinism (no un-frozen wall-clock comparisons in test assertions).
- **Anti-Pattern Prevented:** Silent failure masking database crashes, flaky midnight-rollover test assertions.
- **Cypher Invariant:**
```cypher
MATCH (m:Method)-[:CONTAINS_HANDLER]->(h:CatchHandler)
WHERE h.is_empty = true AND NOT h.has_rethrow = true AND NOT h.has_log = true
RETURN m.file, m.name, h.exception_type
```

#### [M, R] Method $\times$ Resource & Complexity
- **Invariant:** Method Cognitive Complexity $\le 15$ and maximum control-flow nesting depth $\le 3$.
- **Anti-Pattern Prevented:** Spaghettified functions with unmaintainable nested branching.
- **Cypher Invariant:**
```cypher
MATCH (m:Method)
WHERE m.cyclomatic_complexity > 15 OR m.max_nesting_depth > 3
RETURN m.file, m.name, m.cyclomatic_complexity, m.max_nesting_depth
```

#### [M, B] Method $\times$ Scope Boundaries
- **Invariant:** A pure/calculation method must not mutate global or module-level variables. Local variable definitions must not shadow module symbols.
- **Anti-Pattern Prevented:** Hidden side-effects, variable shadowing bugs (`UnboundLocalError`).
- **Cypher Invariant:**
```cypher
MATCH (m:Method)-[:WRITES_GLOBAL]->(v:GlobalVariable)
WHERE m.is_pure_expected = true
RETURN m.file, m.name, v.name
```

---

### Level 2: Class Scope

#### [C, C] Class $\times$ External Contract
- **Invariant:** "Deep Module Interface" (Ousterhout): public method count must be compact relative to internal implementation complexity. Public interfaces must adhere to Interface Segregation (ISP).
- **Anti-Pattern Prevented:** Shallow classes that expose internal data structures; bloated interfaces forcing consumers to implement unused methods.
- **Cypher Invariant:**
```cypher
MATCH (c:Class)
WHERE c.public_methods_count > 20 AND c.is_facade = false
RETURN c.file, c.name, c.public_methods_count
```

#### [C, I] Class $\times$ Implementation Rules
- **Invariant:** Data Transfer Objects (DTOs) and Value Objects must be immutable. Mutable state must be private and mutated only via validating member methods.
- **Anti-Pattern Prevented:** Leaked mutable internal state, bypass of domain invariants through public property setters.
- **Cypher Invariant:**
```cypher
MATCH (c:Class {is_dto: true})-[:HAS_PROPERTY]->(p:Property {is_mutable: true})
RETURN c.file, c.name, p.name
```

#### [C, R] Class $\times$ Resource & Complexity
- **Invariant:** Classes must exhibit high cohesion: LCOM (Lack of Cohesion of Methods) metric must remain below threshold. Class field count must not exceed $K$ (default: $\le 10$).
- **Anti-Pattern Prevented:** "God Class" anti-pattern grouping unrelated responsibilities.
- **Cypher Invariant:**
```cypher
MATCH (c:Class)
WHERE c.lcom_score > 0.8 OR c.field_count > 12
RETURN c.file, c.name, c.lcom_score, c.field_count
```

#### [C, B] Class $\times$ Scope Boundaries
- **Invariant:** Law of Demeter: a method of class $A$ cannot invoke methods on objects returned by collaborating class $B$ (no deep call chaining `a.getB().getC().doAction()`).
- **Anti-Pattern Prevented:** Tight coupling to internal topologies of collaborating classes.
- **Cypher Invariant:**
```cypher
MATCH (c:Class)-[:CALLS_CHAIN_DEPTH]->(d:CallChain)
WHERE d.depth > 2 AND NOT d.is_fluent_builder = true
RETURN c.file, c.name, d.chain_expression
```

---

### Level 3: Module / Package Scope

#### [P, C] Module $\times$ External Contract
- **Invariant:** External consumers must interact with a module only through explicit public exported symbols or Facades/Barrel files (`index.ts`, `__init__.py`, `public` API). Direct imports of internal implementation details are forbidden.
- **Anti-Pattern Prevented:** Deep coupling to package internal file structures; inability to refactor module internals.
- **Cypher Invariant:**
```cypher
MATCH (consumer:Module)-[:IMPORTS]->(targetFile:File)
WHERE targetFile.is_internal = true 
  AND consumer.package_name <> targetFile.package_name
RETURN consumer.name, targetFile.path
```

#### [P, I] Module $\times$ Implementation Rules
- **Invariant:** Internal module dependencies must form an Acyclic Directed Graph (DAG). Circular package dependencies are forbidden.
- **Anti-Pattern Prevented:** Module initialization race conditions, memory leaks, and import deadlocks.
- **Cypher Invariant:**
```cypher
MATCH (p1:Package)-[:DEPENDS_ON*1..5]->(p2:Package)
WHERE (p2)-[:DEPENDS_ON*1..5]->(p1)
RETURN p1.name, p2.name
```

#### [P, R] Module $\times$ Resource & Complexity
- **Invariant:** Martin's Coupling Metrics: Distance from the Main Sequence $D = |A + I - 1|$ must satisfy $D \le 0.7$.
- **Anti-Pattern Prevented:** Modules stuck in the "Zone of Pain" (rigid, non-abstract, heavily depended on) or "Zone of Uselessness" (abstract with no dependents).
- **Cypher Invariant:**
```cypher
MATCH (m:Package)
WHERE abs(m.abstractness + m.instability - 1.0) > 0.7
RETURN m.name, m.abstractness, m.instability
```

#### [P, B] Module $\times$ Scope Boundaries
- **Invariant:** Clean Architecture Layer Invariant: Dependencies point strictly inwards: `Transport -> Service -> Domain -> Storage`. An inner layer must never depend on an outer layer.
- **Anti-Pattern Prevented:** Domain logic poisoned by HTTP frameworks or database libraries.
- **Cypher Invariant:**
```cypher
MATCH (inner:Package {layer: 'Domain'})-[:DEPENDS_ON]->(outer:Package)
WHERE outer.layer IN ['Transport', 'UI', 'Infrastructure']
RETURN inner.name, outer.name, outer.layer
```

---

### Level 4: Service Scope

#### [S, C] Service $\times$ External Contract
- **Invariant:** Wire Parity: All published Ingress Endpoints (HTTP/gRPC/WS) called by client apps must exist and match signatures. Deprecated endpoints must provide sunset dates.
- **Anti-Pattern Prevented:** Silent runtime 404s, client-server contract drift across releases.
- **Cypher Invariant:**
```cypher
MATCH (clientCall:EgressCall)
WHERE NOT EXISTS {
  MATCH (ep:IngressEndpoint)
  WHERE ep.route = clientCall.target_route 
    AND ep.http_method = clientCall.http_method
}
RETURN clientCall.source_file, clientCall.target_route, clientCall.http_method
```

#### [S, I] Service $\times$ Implementation Rules
- **Invariant:** Mutation Chokepoint Monopoly: Sensitive business state changes (balance, tier, permissions) must route through exactly one authorized service method equipped with row locking (`FOR UPDATE`).
- **Anti-Pattern Prevented:** Concurrent double-spends, state divergence, bypass of audit logs.
- **Cypher Invariant:**
```cypher
MATCH (writer)-[:WRITES_FIELD]->(f:Field {is_critical_state: true})
WHERE writer.qualified_name <> f.authorized_chokepoint
RETURN writer.file, writer.qualified_name, f.name
```

#### [S, R] Service $\times$ Resource & Complexity
- **Invariant:** Database Query Complexity & Lineage: Ingress service handlers must not execute unbounded N+1 database queries. Connection pools and query timeouts must be declared.
- **Anti-Pattern Prevented:** Cascading database exhaustion, unindexed join queries freezing production.
- **Cypher Invariant:**
```cypher
MATCH (h:EndpointHandler)-[:INVOKES_IN_LOOP]->(q:DatabaseQuery)
RETURN h.file, h.name, q.query_text
```

#### [S, B] Service $\times$ Scope Boundaries
- **Invariant:** Database Storage Isolation: A service must access only its designated database schemas or tables. Direct cross-service database querying is strictly forbidden.
- **Anti-Pattern Prevented:** Shared database monolith coupling, bypass of microservice APIs.
- **Cypher Invariant:**
```cypher
MATCH (s:Service)-[:USES_DB_TABLE]->(t:DatabaseTable)
WHERE t.owning_service <> s.name
RETURN s.name, t.name, t.owning_service
```

---

### Level 5: System Scope

#### [A, C] System $\times$ External Contract
- **Invariant:** Backward Compatibility: Public gateway endpoints must adhere to semantic versioning. Breaking contract changes without version bump are rejected.
- **Anti-Pattern Prevented:** Breaking deployed mobile or third-party client integrations.
- **Cypher Invariant:**
```cypher
MATCH (oldEp:IngressEndpoint {status: 'Active'})
WHERE NOT EXISTS { MATCH (newEp:IngressEndpoint {signature_hash: oldEp.signature_hash}) }
  AND oldEp.is_version_bumped = false
RETURN oldEp.route, oldEp.version
```

#### [A, I] System $\times$ Implementation Rules
- **Invariant:** Distributed State Consistency: Multi-service workflows must implement compensation logic (Saga Orchestration / Transactional Outbox). Synchronous distributed two-phase commits (2PC) are forbidden.
- **Anti-Pattern Prevented:** Distributed deadlocks, orphaned intermediate states during network partitions.
- **Cypher Invariant:**
```cypher
MATCH (s:Service)-[:CALLS_DISTRIBUTED_TRANSACTION {type: '2PC'}]->(target:Service)
RETURN s.name, target.name
```

#### [A, R] System $\times$ Resource & Complexity
- **Invariant:** Egress Tripwires & Quotas: All external network egress points (third-party payment gateways, LLM APIs) must pass through a Circuit Breaker and rate limiter.
- **Anti-Pattern Prevented:** Runaway API bill exhaustion, cascading outages from external vendor downtime.
- **Cypher Invariant:**
```cypher
MATCH (e:EgressPoint {is_external_vendor: true})
WHERE NOT EXISTS { MATCH (e)<-[:PROTECTED_BY]-(:CircuitBreaker) }
RETURN e.file, e.target_host
```

#### [A, B] System $\times$ Scope Boundaries
- **Invariant:** Lifecycle & Data Lineage Completeness: Every entity created in the system must have a verifiable deletion/purge path (Right to be Forgotten / Compliance). No orphan tables with unmanaged growth.
- **Anti-Pattern Prevented:** Regulatory non-compliance (GDPR/152-FZ), permanent storage leakage.
- **Cypher Invariant:**
```cypher
MATCH (t:DatabaseTable)
WHERE t.has_lifecycle_policy = false 
  AND NOT EXISTS { MATCH (:Service)-[:DELETES_RECORDS_FROM]->(t) }
RETURN t.name
```

---

## 4. Engineering Architecture in `CodeExplorer`

```
┌─────────────────────────────────────────────────────────────┐
│                    CodeExplorer Core Engine                 │
│                                                             │
│   Layer 1: Physical Files & Directories                     │
│   Layer 2: Semantic Boundaries (Service, App, Library)      │
│   Layer 3: AST Symbols (Class, Method, Interface, Field)    │
│   Layer 4: Relational Graph (CALLS, WRITES, USES_DB)        │
│   Layer 5: Architectural Projections & Domain Clusters      │
└──────────────────────────────┬──────────────────────────────┘
                               │
                Evaluates Matrix Invariants
                               ▼
┌─────────────────────────────────────────────────────────────┐
│             Semantic Invariant Gate Engine (SIG)            │
│                                                             │
│  .codeexplorer/gates/*.yaml                                 │
│  ├── [M, C] Method Contract Gates                           │
│  ├── [C, R] Class Cohesion Gates (LCOM)                     │
│  ├── [P, B] Package Clean Arch DAG Gates                    │
│  └── [S, I] Service Chokepoint & Lock Gates                 │
│                                                             │
│  .codeexplorer/baselines/*.json  (Monotonic Ratchets)       │
└──────────────────────────────┬──────────────────────────────┘
                               │
               Sub-second Result (< 50 ms)
                               ▼
         ┌─────────────────────┴─────────────────────┐
         ▼                                           ▼
┌──────────────────┐                       ┌──────────────────┐
│   CLI: ce gate   │                       │  MCP: ce mcp     │
│   Exit 0 / 1     │                       │  Agent Guardrail │
└──────────────────┘                       └──────────────────┘
```

### 4.1 Gate Manifest Format (`.codeexplorer/gates/[SCOPE]_[DIMENSION]_[NAME].yaml`)

```yaml
id: SIG-S-I-001
scope: Service       # Method | Class | Module | Service | System
dimension: Rules     # Contract | Rules | Resources | Boundaries
name: Service Mutation Chokepoint Monopoly
severity: Error
description: "Direct mutation of sensitive state fields is restricted to the authorized domain mutator."
remediation: "Do not write to this field directly. Route mutations through the designated service chokepoint."

query: |
  MATCH (writer)-[:WRITES_FIELD]->(f:Field {is_critical_state: true})
  WHERE writer.qualified_name <> f.authorized_chokepoint
  RETURN 
    writer.file AS file, 
    writer.line AS line, 
    writer.qualified_name AS symbol, 
    f.name AS target,
    'Illegal state mutation outside authorized chokepoint' AS message

ratchet:
  enabled: true
  mode: MonotonicDecrease
  baseline: baselines/SIG-S-I-001.json
```

---

## 5. Implementation Roadmap for Autonomous Agents

### Stage 1: Domain Enums & Model Binding (`CodeExplorer.Core`)
- [ ] Add `InvariantScope`: `Method`, `Class`, `Module`, `Service`, `System`.
- [ ] Add `InvariantDimension`: `Contract`, `Rules`, `Resources`, `Boundaries`.
- [ ] Add `SemanticGateDefinition`: parsing YAML with schema validation.
- [ ] Add `GateCoordinate`: helper struct `(Scope, Dimension)` mapping to the $5 \times 4$ matrix.

### Stage 2: Built-in Cypher Invariant Catalog (`CodeExplorer.Core.Gates.Library`)
- [ ] Implement out-of-the-box system templates for the canonical cells:
  - `[P, B] clean-architecture-dag.yaml`
  - `[S, I] mutation-chokepoint-monopoly.yaml`
  - `[S, C] wire-contract-parity.yaml`
  - `[M, I] exception-hygiene-no-swallow.yaml`
  - `[C, R] class-cohesion-lcom.yaml`
  - `[S, B] service-storage-isolation.yaml`

### Stage 3: Command & MCP Execution Layer
- [ ] Implement CLI command:
  ```bash
  ce gate check [--scope <Scope>] [--dimension <Dimension>] [--format table|json|sarif]
  ```
- [ ] Implement MCP Tool in `McpGraphHandler`:
  - Name: `check_semantic_invariants`
  - Arguments: `scope` (optional), `dimension` (optional), `changedFiles` (optional).
  - Returns: Structured violations formatted specifically for LLM agent remediation.
