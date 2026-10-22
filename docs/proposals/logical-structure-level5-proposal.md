# 5-Layer Strict Unidirectional Ontology & Level 5 (Logical Structure) Specification

## 1. Overview & Architectural Vision
CodeExplorer's ontology is structured across five cohesive architectural layers, transitioning from physical files up to domain-driven business design.

### Core Ontology Law: Strict Unidirectional Layer Edges
1. **No Collapsing Across Layers**: Higher layers do not collapse lower-layer relationships into lower-layer nodes. Instead, each Layer $N$ **synthesizes horizontal relationships (`Layer N -> Layer N`)** between entities of Layer $N$ based on the connections established at Layer $N-1$.
2. **Edges Point Horizontally or Downward — NEVER Upward**:
   - Every edge belongs to the ontology layer of its source (`from_id`) node.
   - An entity at **Layer $N$** may only have outgoing edges to:
     - **Horizontal (Intra-Layer)**: Entities at **Layer $N$** (`Layer N -> Layer N`).
     - **Downward (Grounding)**: Entities at **Layer $< N$** (`Layer N -> Layer < N`) that implement, declare, or execute it.
   - **Zero Upward Edges**: No entity at Layer $K$ may ever have an outgoing edge to an entity at Layer $M$ where $M > K$ (e.g., `File -> Project`, `Project -> Service`, `Project -> Database`, `Function -> Database`, `Type -> Table`, `Function -> ExternalService` are strictly forbidden).
3. **`SyntaxEnricher` Elimination**:
   - `ISyntaxEnricher` / `SyntaxEnricher` is completely eliminated.
   - Layer 3 AST visitors and library parsers extract all syntactic symbols (`Type`, `Function`, `Member`, `CallLiteral`, `CatchClause`) and syntactic references (`CALLS`, `IMPLEMENTS`, `INHERITS_FROM`, `USES_TYPE`) in a single pass without disk re-reads or regexes in Layer 4.

---

## 2. The 5 Architectural Structures

| Level | Unified Display Title | Root Structure Node | Key Node Kinds | Horizontal Edges (`N -> N`) | Downward Grounding Edges (`N -> < N`) |
|---|---|---|---|---|---|
| **Level 1** | **File Structure** | `FilesStructureNode` (`FilesStructure`, `fs:`) | `Folder`, `File`, `GitSettings`, `Counter` | `Folder -[:CONTAINS]-> Folder \| File \| GitSettings` | *(None — base layer)* |
| **Level 2** | **Project Structure** | `ProjectsStructureNode` (`ProjectsStructure`, `ps:`) | `Project`, `Package` | `Project -[:DEPENDS_ON]-> Project \| Package`<br>`Package -[:IMPLEMENTED_BY]-> Project` | `Project -[:CONTAINS]-> File` (L1)<br>`Project -[:LOCATED_IN]-> Folder` (L1)<br>`Project -[:USES_GIT]-> GitSettings` (L1) |
| **Level 3** | **Syntax Structure** | `SyntaxStructureNode` (`SyntaxStructure`, `ss:`) | `ProjectSyntax`, `Type`, `Function`, `Member`, `CallLiteral`, `CatchClause` | `ProjectSyntax -[:CONTAINS]-> Type \| Function \| Member`<br>`Type -[:HAS_METHOD]-> Function`<br>`Type -[:HAS_MEMBER]-> Member`<br>`Type -[:INHERITS_FROM \| IMPLEMENTS \| USES_TYPE]-> Type`<br>`Function -[:CALLS]-> Function`<br>`Function -[:USES_TYPE]-> Type`<br>`Function -[:PASSES_LITERAL]-> CallLiteral`<br>`Member -[:OF_TYPE]-> Type` | `ProjectSyntax -[:BELONGS_TO]-> Project` (L2)<br>`Type \| Function \| Member \| CallLiteral \| CatchClause -[:DECLARED_IN]-> File` (L1) |
| **Level 4** | **Semantic Structure** | `SemanticStructureNode` (`SemanticStructure`, `sem:`) | `App`, `Service`, `Worker`, `Library`, `CliTool`, `TestSuite`, `Database`, `DataSet`, `Table`, `Procedure`, `Query`, `Topic`, `Endpoint`, `EntryPoint`, `ExternalService`, `CloudService`, `ApiInUse`, `PeerMesh` | `Workload -[:USES_DB]-> Database`<br>`Workload -[:SERVICE_CALL]-> Service \| ExternalService`<br>`Workload -[:PUBLISHES_TO \| SUBSCRIBES_TO]-> Topic`<br>`Workload -[:USES_CLOUD \| READS_FROM \| WRITES_TO]-> CloudService`<br>`Workload -[:DEPENDS_ON]-> Library`<br>`Workload -[:CONTAINS]-> Endpoint \| EntryPoint \| ApiInUse`<br>`TestSuite -[:TESTS]-> Workload`<br>`ExternalService -[:CALLS_ENDPOINT]-> Endpoint`<br>`Database -[:CONTAINS]-> Table \| Procedure`<br>`Query -[:READS_FROM \| WRITES_TO]-> Table` | `Workload -[:IMPLEMENTED_BY]-> Project` (L2)<br>`Workload -[:LOCATED_IN]-> Folder` (L1)<br>`Workload -[:DEPENDS_ON]-> Package` (L2)<br>`Database \| Table \| Query \| CloudService \| ApiInUse -[:ACCESSED_BY]-> Function \| Type` (L3)<br>`Endpoint \| EntryPoint -[:EXPOSED_BY]-> Function \| Type` (L3)<br>`ExternalService -[:CALLED_BY]-> Function` (L3)<br>`Topic -[:PUBLISHED_BY \| SUBSCRIBED_BY]-> Function` (L3) |
| **Level 5** | **Logical Structure** | `LogicalStructureNode` (`LogicalStructure`, `ls:`) | `Domain`, `BoundedContext` | `LogicalStructure -[:CONTAINS]-> Domain`<br>`Domain -[:CONTAINS]-> BoundedContext`<br>`BoundedContext -[:DEPENDS_ON]-> BoundedContext` | `BoundedContext -[:CONTAINS]-> Workload \| Database \| Topic` (L4)<br>`BoundedContext -[:DEFINES]-> Type` (L3) |

---

## 3. Pipeline Stage Self-Containment

Each pipeline stage completes its own node synthesis, downward grounding edges, and horizontal intra-layer edges before handing off to the next stage:

### 3.1 Layer 1: File Structure (`Layer1PhysicalParser`)
- Discovers `Folder`, `File`, `GitSettings`.
- Emits `L1 -> L1` `CONTAINS` edges.

### 3.2 Layer 2: Project Structure (`Layer2ProjectParser`)
- Discovers `Project` and `Package` boundaries.
- Emits downward `L2 -> L1` edges: `Project -[:CONTAINS]-> File`, `Project -[:LOCATED_IN]-> Folder`, `Project -[:USES_GIT]-> GitSettings` (no upward `File -[:BELONGS_TO]-> Project` edge).
- Emits horizontal `L2 -> L2` edges: `Project -[:DEPENDS_ON]-> Project | Package`, `Package -[:IMPLEMENTED_BY]-> Project`.

### 3.3 Layer 3: Syntax Structure (`Layer3SyntacticParser`)
- Parses ASTs and extracts all syntactic symbols (`Type`, `Function`, `Member`, `CallLiteral`, `CatchClause`) including variables/fields/properties directly from ASTs.
- Emits downward `L3 -> L2` (`ProjectSyntax -[:BELONGS_TO]-> Project`) and `L3 -> L1` (`Symbol -[:DECLARED_IN]-> File`) edges.
- Resolves all cross-file syntactic references in Layer 3 (`Function -[:CALLS]-> Function`, `Type -[:IMPLEMENTS | INHERITS_FROM | USES_TYPE]-> Type`, `Function -[:USES_TYPE]-> Type`).

### 3.4 Layer 4: Semantic Structure (`Layer4SemanticParser`)
- Synthesizes Layer 4 workloads (`Service`, `App`, `Worker`, `CliTool`, `Library`, `TestSuite`) and semantic resources (`Database`, `Table`, `Query`, `Topic`, `Endpoint`, `EntryPoint`, `ExternalService`, `CloudService`, `ApiInUse`) from Layer 3 AST symbols + configuration files.
- Emits downward `L4 -> L2` grounding edges:
  - `Workload -[:IMPLEMENTED_BY]-> Project`
- Emits downward `L4 -> L3` grounding edges:
  - `Database | Table | Query | CloudService | ApiInUse -[:ACCESSED_BY { operation: 'read' | 'write' | 'orm_entity' }]-> Function | Type`
  - `Endpoint | EntryPoint -[:EXPOSED_BY]-> Function | Type`
  - `ExternalService -[:CALLED_BY]-> Function`
  - `Topic -[:PUBLISHED_BY | SUBSCRIBED_BY]-> Function`
- Derives horizontal `L4 -> L4` edges from Layer 3 & Layer 2 connections:
  - **Direct Library & Call-Graph Propagation**:
    - A `Library` gets horizontal edges (e.g., `Library -[:USES_DB]-> Database`) for resources accessed by its own symbols.
    - Executable workloads (`Service`, `App`, `Worker`, `CliTool`) get horizontal edges (`Workload -[:USES_DB]-> Database`, `Workload -[:SERVICE_CALL]-> Service | ExternalService`, `Workload -[:PUBLISHES_TO | SUBSCRIBES_TO]-> Topic`) when their own symbols or their Layer 3 call/type reachability graph reaches the resource-accessing Layer 3 symbols (recording provenance on the edge).
  - Late-binds `ExternalService -[:CALLS_ENDPOINT]-> Endpoint` and synthesizes `Workload -[:SERVICE_CALL]-> TargetService`.

### 3.5 Layer 5: Logical Structure (`Layer5LogicalParser`)
- Synthesizes `LogicalStructureNode` (`{workspaceId}:ls`), `DomainNode` (`{workspaceId}:dom:{domainKey}`), and `BoundedContextNode` (`{workspaceId}:bc:{contextKey}`).
- **Hybrid Materialization**:
  1. User configurations from `.codeexplorer/domains.json` and `conventions.json`.
  2. DDD heuristic auto-discovery (project name tokens, namespaces, folder boundaries, domain aggregates).
  3. Fallback generic/shared handling (`Domain: Shared` / `dom:shared` and `BoundedContext: SharedKernel` / `bc:shared.kernel`).
- Emits downward `L5 -> L4` (`BoundedContext -[:CONTAINS]-> Workload | Database | Topic`) and `L5 -> L3` (`BoundedContext -[:DEFINES]-> Type`) edges.
- Derives horizontal `L5 -> L5` inter-context dependencies by aggregating Layer 4 interactions (`SERVICE_CALL`, `PUBLISHES_TO`/`SUBSCRIBES_TO`, `USES_DB`, `DEPENDS_ON`) between workloads in different Bounded Contexts into consolidated `BoundedContext -[:DEPENDS_ON]-> BoundedContext` edges:
  ```json
  {
    "interactions": ["rpc", "messaging"],
    "rpcCallsCount": 4,
    "eventsPublishedCount": 2,
    "channels": ["orders.created", "payments.processed"]
  }
  ```

---

## 4. Built-In Semantic Invariant Gate: `no-upward-layer-edges`
To permanently guarantee ontology hygiene, CodeExplorer enforces a built-in Semantic Invariant Gate that verifies zero edges point from a lower layer to a higher layer:
- Layer 1 (`File`, `Folder`, `GitSettings`) $\to$ cannot have edges to Layer 2, 3, 4, 5.
- Layer 2 (`Project`, `Package`) $\to$ cannot have edges to Layer 3, 4, 5.
- Layer 3 (`ProjectSyntax`, `Type`, `Function`, `Member`, `CallLiteral`, `CatchClause`) $\to$ cannot have edges to Layer 4, 5.
- Layer 4 (`Service`, `App`, `Worker`, `CliTool`, `Library`, `TestSuite`, `Database`, `Table`, `Query`, `Procedure`, `DataSet`, `Topic`, `Endpoint`, `EntryPoint`, `ExternalService`, `CloudService`, `ApiInUse`, `PeerMesh`) $\to$ cannot have edges to Layer 5.
