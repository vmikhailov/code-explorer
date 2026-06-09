# Architectural Audit & Improvements Roadmap

This document captures the architectural review of **CodeExplorer**, highlights the critical scaling/operational challenges identified by the Principal Solution Architect, captures the core engineering responses, and outlines a concrete mitigation roadmap.

---

## 1. Core Architectural Critique & Mitigation Strategies

### Challenge 1: The "Big Bang" Ingestion Bottleneck (Full Re-scans)
*   **Architect's Critique**: Indexing requires a complete database wipe and full reload (`--clear`). This takes minutes on large solutions (e.g., 1.5M+ lines of code), making real-time IDE pair programming impossible.
*   **Developer Feedback**: **Incremental updating is our immediate near-term priority.**
*   **Mitigation Strategy**:
    *   Implement an event-driven **Workspace File Watcher** (using C# `FileSystemWatcher` or integration with the editor client file events).
    *   When a file is saved/created, extract only that file's syntax tree using Layer 1 parser in memory.
    *   Execute a target transaction to:
        1. Remove the old `File` node and prune its orphan child declarations (`[:DEFINES|DECLARES]`).
        2. Insert the new `File` node and its fresh child elements.
        3. Run a scoped Layer 2 late-binding update strictly for references originating from or targeting the modified file.

---

### Challenge 2: Inferred Type Blindness (Static AST vs. Type Checkers)
*   **Architect's Critique**: Tree-sitter parsers are purely syntactic. They cannot resolve the target type of abstract identifiers, constructor-injected interfaces, or dynamically returned values, leaving `resolve_call_target` with major blind spots.
*   **Developer Feedback**: **Integration with a real LSP (Language Server Protocol) is hard but highly feasible and will not change the overall mapping logic.**
*   **Mitigation Strategy**:
    *   Integrate with the native language's LSP or developer compilers (e.g., MSBuild Workspace/Roslyn for C#, TypeScript Language Service, `go/types`).
    *   During the AST enrichment phase, delegate name resolution and typing requests to the active LSP instance to resolve the fully qualified namespace/type of late-bound targets.
    *   Preserve the current generic `SyntaxTree` and `IOntologyNode` structures; the LSP simply acts as a high-fidelity enricher for node metadata and references.

---

### Challenge 3: Database Infrastructure Overhead (Memgraph/Docker Dependency)
*   **Architect's Critique**: Requiring a running Memgraph database in a Docker container creates high configuration friction for local developers and increases runtime dependencies.
*   **Developer Feedback**: **Moving to a lightweight embedded database is a great path forward. Rewriting queries will be necessary but is entirely doable.**
*   **Mitigation Strategy**:
    *   Evaluate embedded relational databases (such as **SQLite** with Recursive Common Table Expressions (CTEs) and JSON extensions, or **DuckDB**) or an embedded key-value graph library.
    *   Migrate the query layer in `Queries.cs` from Cypher format to standard SQL dialect leveraging Recursive CTEs for parent-child tree traversals (`[:CONTAINS*]`) and call chain pathfinding.
    *   Compile the database engine directly into the application DLL, reducing deployment to a single binary CLI.

---

### Challenge 4: Multi-Language Semantic Mismatch
*   **Architect's Critique**: Forcing diverse structures (Go receiver functions, C# partial classes, TS union types, SQL tables) into a flat schema (`Class`, `Interface`, `Function`, `Variable`) deletes language-specific nuances.
*   **Mitigation Strategy**:
    *   Evolve the schema to support a **poly-typed ontology**. Allow nodes to support language-specific labels and properties (e.g. `:GoReceiver` or `:TypeScriptUnion`).
    *   Enable polymorphic node builders that derive from base kinds but attach language-specific extension bags.

---

### Challenge 5: Library Parser Scalability
*   **Architect's Critique**: Writing hand-coded `ILibraryParser` classes for every third-party framework (Axios, NestJS, Express) is a maintenance dead-end given the size and churn of the package ecosystem.
*   **Mitigation Strategy**:
    *   Move from rigid hardcoded rules to **heuristic pattern metadata** defined in configuration files (e.g., JSON schemas mapping signature patterns to endpoint declarations).
    *   Leverage the LSP-enriched imports list to identify target package calls generically, flagging them as external dependency egress points without manual parser code.

---

### Challenge 6: Coordinate Shift & Stale Graph Data
*   **Architect's Critique**: Absolute source-line numbers stored in the graph database shift instantly when files are edited, causing the LLM to write code targeting shifted, out-of-bounds coordinates.
*   **Mitigation Strategy**:
    *   Introduce **Dynamic Line-Shift Tracking**. When a file is modified, calculate the line-number delta and offset all sibling/child nodes in the local session memory.
    *   Convert absolute line coordinates to **semantic symbols/ranges** during LLM retrieval, referencing target methods by name or signature rather than hard lines when generating code changes.

---

## 2. Decoupled 4-Bucket Ontology Model

To support fast, independent updates and optimize query complexity, the graph schema is organized into four horizontal buckets linked by reference pointers:

### A. FilesStructure (Physical Topology)
*   **Purpose:** Maps the layout of files on disk.
*   **Entities:**
    *   `Folder` (path, name)
    *   `File` (path, name, hash, language)
*   **Relationships:** `Folder -[CONTAINS_FILE]-> File`

### B. ClassStructure (Syntactic Code Models)
*   **Purpose:** Stores the static syntax structures parsed from code files.
*   **Entities:**
    *   `Project` (name, language, path)
    *   `Type` (name, kind [class/interface/struct/enum], start_line, end_line)
    *   `Function` (name, signature, return_type, start_line, end_line)
    *   `Member` (name, type_name, kind [field/property/parameter/variable])
*   **Relationships:**
    *   `Project -[DECLARES_TYPE]-> Type`
    *   `Type -[HAS_METHOD]-> Function`
    *   `Type -[HAS_MEMBER]-> Member`
    *   `Function -[DECLARED_IN]-> File` (Links back to FilesStructure)

### C. SemanticStructure (Runtime System Map)
*   **Purpose:** Exposes public interfaces, endpoints, databases, and brokers.
*   **Entities:**
    *   `Endpoint` (http_method, route_template)
    *   `Database` (name, db_type)
    *   `Topic` (name, broker_type)
    *   `EntryPoint` (entry_type)
*   **Relationships:**
    *   `Function -[EXPOSES_ENDPOINT]-> Endpoint`
    *   `Function -[QUERIES_DB]-> Database`

### D. SystemBindings (Cross-Project Integration)
*   **Purpose:** Links callers to providers across networks or project boundaries.
*   **Relationships:**
    *   `Function -[CALLS_ENDPOINT]-> Endpoint`
    *   `Function -[PUBLISHES_TO]-> Topic`
    *   `Function -[SUBSCRIBES_TO]-> Topic`

## 3. Ingestion & Runtime Architecture Evolution

```
[Local File Saves] ──> [File Watcher] ──> [Surgical Parse (Layer 1)]
                                                  │
                                                  ▼
[Embedded SQLite DB] <── [Scoped Relink] <── [LSP Semantic Resolve]
```


## 3. Immediate Roadmap

| Phase | Goal | Key Task |
| :--- | :--- | :--- |
| **Phase 1** | **Incremental Update Pipeline** | Implement `FileSystemWatcher` tracking + scoped database transaction pruning. |
| **Phase 2** | **LSP Integration** | Wire Roslyn and TypeScript Compiler APIs to resolve late-bound polymorphic method calls. |
| **Phase 3** | **Embedded Graph Transition** | Re-write Cypher queries to SQLite Recursive CTEs to remove Docker runtime dependency. |
