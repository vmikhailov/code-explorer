# Stratified Ontology Visualization, Schema Audit & OpenCypher IntelliSense (Proposal)

## 1. Executive Summary & Motivation

CodeExplorer's graph database is governed by a formal 5-layer ontology (**38 Node Types** and **42 Relationship Types**) defined via C# source attributes (`[OntologyNode]`, `[OntologyEdge<TTo>]`, `[OntologyProperty]`, and `[OntologyRelationship]`). Currently, [`CodeExplorer.OntologyGen`](../../cli/src/Tools/CodeExplorer.OntologyGen/Program.cs) extracts this metadata at build time solely to generate a static Markdown document ([`docs/ontology.md`](../ontology.md)).

While `docs/ontology.md` serves as a reference manual, two critical workflow gaps remain for both platform developers and end users authoring queries and **Semantic Invariant Gates (SIG v2)**:

1. **No Interactive Way to Validate the Ontology or Discover Queryable Paths**:
   - A flat Mermaid diagram of 38 node types and 180+ allowed `[OntologyEdge<TTo>]` transitions turns into an unreadable "hairball".
   - When evolving language parsers (C#, Rust, TypeScript, Python, Go, Java, Kotlin, SQL), parsers occasionally emit relationships `(srcKind)-[relKind]->(tgtKind)` into SQLite that were never declared in the C# `[OntologyEdge]` attributes (**Schema Drift**), or conversely, declared ontology edges remain unpopulated (`0` instances).
   - Users constructing multi-hop OpenCypher queries struggle to see **which node types connect across Layers 1–5**, in **which direction** arrows point (`Endpoint -[:HANDLED_BY]-> Function` vs. `Function -[:CALLS]-> ExternalService`), and **which properties** exist on each entity.

2. **No Ontology-Aware IntelliSense When Authoring SIG Rules & Cypher Queries**:
   - In [`CypherEditor.tsx`](../../vscode-extension/src/webview/domain/components/CypherEditor.tsx) and [`GateEditorView.tsx`](../../vscode-extension/src/webview/components/GateEditorView.tsx), users write Cypher queries and `{alias.property}` message templates from memory.
   - Typos in node labels, inverted relationship directions, or guessing property names (`path` vs. `file_path`, `route_template` vs. `url`, `has_comment` vs. `is_commented`) only surface at runtime or produce silent zero-match results.

This proposal introduces a unified architecture powered by a **Single Source of Truth (`ontology-schema.json`)** generated at build time and enriched at runtime:
- **Feature 1: Stratified Ontology Explorer & Schema Audit View** — an interactive 5-layer visualization for validating ontology integrity, inspecting live workspace coverage, and generating Cypher `MATCH` patterns in one click.
- **Feature 2: Context-Aware OpenCypher & SIG Template IntelliSense** — zero-latency autocomplete and semantic validation inside `CypherEditor` and `GateEditorView`.

---

## 2. Pillar 1: Single Source of Truth (`ontology-schema.json` & `/api/ontology`)

Instead of maintaining separate schema definitions in C# and TypeScript, [`CodeExplorer.OntologyGen`](../../cli/src/Tools/CodeExplorer.OntologyGen/Program.cs) will emit a machine-readable JSON schema alongside `docs/ontology.md` during every build.

### 2.1. Build-Time Schema Generation (`OntologyGen`)

Extend `OntologyGen` with a `JsonSchemaRenderer` that outputs `proto/ontology-schema.json` (and embeds it into `CodeExplorer.Core.dll`):

```json
{
  "version": "2.0",
  "layers": [
    { "id": 1, "name": "Layer 1: File Structure", "shortName": "Physical" },
    { "id": 2, "name": "Layer 2: Project Structure", "shortName": "Boundaries" },
    { "id": 3, "name": "Layer 3: Syntax Structure", "shortName": "Syntax" },
    { "id": 4, "name": "Layer 4: Semantic Structure", "shortName": "Semantic" },
    { "id": 5, "name": "Layer 5: Logical Structure", "shortName": "Logical" }
  ],
  "nodes": [
    {
      "label": "Function",
      "className": "FunctionNode",
      "layer": 3,
      "idScheme": "{ws}:sym:{relFilePath}:Function:{Name}:{StartLine}",
      "purpose": "Represents a callable function, method, constructor, or closure.",
      "properties": [
        { "name": "name", "type": "string", "description": "Simple function or method name" },
        { "name": "full_name", "type": "string", "description": "Fully-qualified name including enclosing type/module" },
        { "name": "file_path", "type": "string", "description": "Workspace-relative file path" },
        { "name": "start_line", "type": "int", "description": "1-indexed declaration start line" },
        { "name": "end_line", "type": "int", "description": "1-indexed declaration end line" },
        { "name": "line_count", "type": "int", "description": "Total lines of code in function body" },
        { "name": "is_async", "type": "bool", "description": "True if declared async" },
        { "name": "is_test", "type": "bool", "description": "True if test method or inside test project" }
      ],
      "outEdges": [
        { "rel": "CALLS", "toLabel": "Function", "category": "ControlFlow" },
        { "rel": "CALLS", "toLabel": "ExternalService", "category": "ControlFlow" },
        { "rel": "READS_FROM", "toLabel": "Table", "category": "DataFlow" },
        { "rel": "WRITES_TO", "toLabel": "Table", "category": "DataFlow" },
        { "rel": "DECLARED_IN", "toLabel": "File", "category": "Structural" },
        { "rel": "DECLARED_IN", "toLabel": "Type", "category": "Structural" }
      ]
    }
  ],
  "relationships": [
    {
      "label": "CALLS",
      "description": "Direct or resolved function/method invocation.",
      "category": "ControlFlow",
      "properties": [
        { "name": "call_sites", "type": "string[]", "description": "1-indexed 'line:col' call-site coordinates" },
        { "name": "call_count", "type": "int", "description": "Number of distinct call sites between caller and callee" },
        { "name": "ev_ids", "type": "string[]", "description": "Associated CallLiteral evidence node IDs" }
      ]
    }
  ]
}
```

### 2.2. Runtime Workspace Ontology Audit Endpoint (`/api/ontology`)

Add a fast endpoint in [`WebSocketServerHandler.Endpoints.cs`](../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.Endpoints.cs) that merges the static ontology schema with live statistics from the active SQLite graph database:

1. **Node Instance Counts**:
   ```sql
   SELECT kind, COUNT(*) AS cnt FROM nodes GROUP BY kind;
   ```
2. **Relationship Triples & Instance Counts**:
   ```sql
   SELECT s.kind AS from_kind, r.kind AS rel_kind, t.kind AS to_kind, COUNT(*) AS cnt
   FROM relationships r
   JOIN nodes s ON s.id = r.from_id
   JOIN nodes t ON t.id = r.to_id
   GROUP BY s.kind, r.kind, t.kind;
   ```
3. **Schema Drift Detection**:
   - Compares every `(from_kind, rel_kind, to_kind)` triple observed in SQLite against the declared `outEdges` in `ontology-schema.json`.
   - Any triple present in SQLite but missing from `[OntologyEdge<TTo>]` is flagged with `isUndeclaredInSchema: true` (`SchemaDriftWarning`), allowing immediate visual and automated auditing of parser output vs. ontology contracts.

---

## 3. Pillar 2: Stratified Ontology Visualization & Validation View

### 3.1. 5-Layer Stratified Swimlane Layout

Unlike a free-form force layout, the **Ontology View** renders meta-nodes inside **5 fixed horizontal swimlanes** corresponding to the architectural layers of CodeExplorer:

```
┌────────────────────────────────────────────────────────────────────────────┐
│ LAYER 5: LOGICAL       [ Domain ] ──CONTAINS──> [ BoundedContext ]         │
├────────────────────────────────────────────────────────────────────────────┤
│ LAYER 4: SEMANTIC      [ Service ]   [ App ]   [ Endpoint ]   [ Table ]    │
│                        [ Worker ]    [ Topic ] [ Database ]   [ Query ]    │
├────────────────────────────────────────────────────────────────────────────┤
│ LAYER 3: SYNTAX        [ Type ]   [ Function ]   [ Member ]   [CallLiteral]│
├────────────────────────────────────────────────────────────────────────────┤
│ LAYER 2: BOUNDARIES    [ Project ] ──DEPENDS_ON──> [ Package ]             │
├────────────────────────────────────────────────────────────────────────────┤
│ LAYER 1: PHYSICAL      [ Workspace ] ──> [ Folder ] ──> [ File ]           │
└────────────────────────────────────────────────────────────────────────────┘
```

### 3.2. Anti-Hairball Progressive Disclosure

Because 38 node types have 180+ valid relationship transitions, rendering all edges simultaneously creates visual clutter. The Ontology View prevents this via three mechanisms:

1. **Edge Category Toggles (in the panel gear menu ⚙️)**:
   - **Control & Call Flow** (*on by default*): `CALLS`, `HANDLED_BY`, `SERVICE_CALL`, `CALLS_ENDPOINT`, `ROUTES_TO`
   - **Data & Messaging Flow** (*on by default*): `READS_FROM`, `WRITES_TO`, `USES_DB`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `QUERIES`
   - **Type & Dependency Graph** (*optional*): `INHERITS`, `IMPLEMENTS`, `USES_TYPE`, `DEPENDS_ON`, `REFERENCES`
   - **Structural Containment** (*off by default — shown on node focus*): `CONTAINS`, `DECLARED_IN`, `LOCATED_IN`, `IMPLEMENTED_BY`, `DEFINED_IN`

2. **Live Workspace Filter**:
   - Toggle **"Active Workspace Types Only"**: hides or dims (`opacity: 0.35`, dashed border) ontology node types that have `0` instances in the currently open repository, showing instance count badges (`Function · 1,420`) on active types.

3. **1-Hop Ego Focus & 2-Node Path Finder**:
   - **Single Click on a Meta-Node** (e.g., `Endpoint`): dims unrelated nodes and highlights only incoming and outgoing relationship types connected to `Endpoint`, with clear directional arrows and edge labels.
   - **Shift+Click on a Second Meta-Node** (e.g., `Endpoint` $\to$ `Table`): computes and highlights all valid multi-hop meta-paths between them in the ontology (e.g., `Endpoint -[:HANDLED_BY]-> Function -[:READS_FROM]-> Table`).

### 3.3. Ontology Validation & Audit Mode

A dedicated **"Audit Overlay"** toggle highlights structural anomalies in the ontology itself:

| Audit Check | Visual Indicator | Purpose |
| :--- | :--- | :--- |
| **Schema Drift (Undeclared DB Edge)** | Bright red dashed edge (`#ef4444`) + warning badge | Detects when a parser writes a relationship `(A)-[:REL]->(B)` into SQLite that is not documented with `[OntologyEdge<B>("REL")]` in C#. |
| **Upward Cross-Layer Violation** | Amber edge (`#f59e0b`) | Highlights edges where a lower ontology layer references a higher layer contrary to the intended layer hierarchy. |
| **Orphan / Unpopulated Meta-Type** | Muted grey node | Identifies ontology classes with `0` declared edges or `0` indexed instances in the workspace. |

### 3.4. Interactive Query & Gate Generator (`Click-to-Query`)

When a user selects a meta-node, meta-edge, or 2-node path in the Ontology View, the right-hand **Declarative Inspector** displays:
1. **Layer, ID Scheme & Purpose**: e.g., `{ws}:sym:{relFilePath}:Function:{Name}:{StartLine}`.
2. **Ontology Properties Table**: Name, Type (`string`, `int`, `bool`), Description, and whether it is indexed.
3. **Inbound & Outbound Schema Edges**: Grouped by relationship type (`-[:CALLS]-> Function | ExternalService`).
4. **Ready-to-Use Cypher & SIG v2 Snippets**:
   - Clicking an edge `Function -[:READS_FROM]-> Table` generates:
     ```cypher
     MATCH (fn:Function)-[r:READS_FROM]->(tbl:Table)
     RETURN fn, r, tbl
     ```
   - Action buttons:
     - **Copy Cypher MATCH**
     - **Run in Cypher Console** (previews actual matching instances from the workspace)
     - **Create Invariant Gate** (opens `GateEditorView` pre-filled with the `MATCH` query and a default `Message Template` `{fn.name} -> {tbl.name}`).

---

## 4. Pillar 3: Ontology-Driven IntelliSense for OpenCypher & SIG v2

Using `ontology-schema.json`, we equip [`CypherEditor.tsx`](../../vscode-extension/src/webview/domain/components/CypherEditor.tsx) and [`GateEditorView.tsx`](../../vscode-extension/src/webview/components/GateEditorView.tsx) with a fast, deterministic autocomplete popup and inline schema validator.

### 4.1. Lightweight Query Context Parser (`cypherCompletionEngine.ts`)

As the user types in `CypherEditor`, a lightweight lexer scans the query text to build an **Alias Symbol Table** mapping variable names to ontology types:
- `(caller:Function)` $\implies$ `aliases["caller"] = { kind: "node", label: "Function" }`
- `[r:CALLS]` $\implies$ `aliases["r"] = { kind: "edge", label: "CALLS", fromLabel: "Function" }`
- `(caller:Function)-[r:CALLS]->(callee)` $\implies$ if `callee` has no explicit label yet, infers candidate target labels from `Function`'s `CALLS` edges (`Function | ExternalService`).

### 4.2. Four Context-Aware Completion Modes

#### Mode 1: Node Label Completion — triggered after `(` or `(alias:`
- **Trigger**: Cursor is inside node parentheses after `:`, e.g., `MATCH (fn:|)` or `MATCH (:|`.
- **Suggestions**: All 38 ontology node labels, filterable by prefix, showing:
  - Label name (`Function`)
  - Layer badge (`L3 · Syntax`)
  - Short description (`Represents a callable function, method, constructor...`)
  - Instance count in current workspace (`1,420 in workspace`).

#### Mode 2: Source-Constrained Relationship Completion — triggered after `-[:` or `-[alias:`
- **Trigger**: Cursor is inside relationship brackets after `:`, e.g., `MATCH (fn:Function)-[r:|`.
- **Smart Filtering**:
  - Because the left node `(fn:Function)` is known to be `Function`, the completion list places **valid outbound/inbound relationships of `Function` at the top** (`CALLS`, `READS_FROM`, `WRITES_TO`, `DECLARED_IN`, `HANDLED_BY` [incoming]).
  - **Pattern Expansion Snippets**: Selecting `CALLS → Function` can complete either just the relationship `CALLS` or the full typed hop `CALLS]->(target:Function)`.
  - If the user selects an incoming relationship (e.g., `HANDLED_BY ← Endpoint` when starting from `Function`), it automatically adjusts the arrow direction: `<-[r:HANDLED_BY]-(ep:Endpoint)`!

#### Mode 3: Property Completion — triggered after `alias.`
- **Trigger**: Cursor follows a bound variable and a dot (`fn.|` or `r.|`) in `WHERE`, `RETURN`, or `WITH`.
- **Suggestions**:
  - For a node alias (`fn:Function`): suggests all `[OntologyProperty]` fields of `Function` (`name`, `full_name`, `file_path`, `start_line`, `end_line`, `line_count`, `is_async`, `is_test`, `visibility`, `return_type`) with their type badge (`string`, `int`, `bool`) and description.
  - For a relationship alias (`r:CALLS`): suggests relationship properties (`call_sites`, `call_count`, `ev_ids`, `line`, `column`).

#### Mode 4: SIG v2 Template Token Completion — triggered after `{` in `GateEditorView`
- **Trigger**: User types `{` inside the **Message Template** or **Target Template** input fields in [`GateEditorView.tsx`](../../vscode-extension/src/webview/components/GateEditorView.tsx).
- **Suggestions**:
  - Inspects the `RETURN` clause of the current query (e.g., `RETURN caller, r, callee`).
  - Suggests bound node/edge property tokens:
    - `{caller.name}`, `{caller.full_name}`, `{caller.file_path}`, `{caller.label}`
    - `{callee.name}`, `{callee.full_name}`, `{callee.label}`
    - `{r.call_count}`
    - Built-in location tokens: `{line}`, `{column}`, `{file}`, `{symbol}`, `{target}`
    - Fallback snippet: `{callee.name|unknown}`.

### 4.3. Real-Time Static Schema Diagnostics in `CypherEditor`

Below the editor (next to the existing Return Contract status bar), `cypherCompletionEngine` provides non-blocking static hints before running the query:
- **Unknown Node Label**: e.g., `MATCH (f:Method)` $\implies$ *"Unknown label `:Method`. Did you mean `:Function` or `:Member`?"*
- **Invalid Edge Between Types**: e.g., `MATCH (f:File)-[:CALLS]->(t:Table)` $\implies$ *"Ontology warning: `:File` has no declared `[:CALLS]` edge to `:Table`."*
- **Inverted Arrow Hint**: e.g., `MATCH (fn:Function)-[:HANDLED_BY]->(ep:Endpoint)` $\implies$ *"Direction hint: `HANDLED_BY` is declared from `:Endpoint` to `:Function` (`(ep:Endpoint)-[:HANDLED_BY]->(fn:Function)`)."*
- **Unknown Property**: e.g., `WHERE fn.lines > 500` $\implies$ *"Property `lines` is not declared on `:Function`. Did you mean `line_count` or `start_line`?"*

---

## 5. Phased Implementation Plan

### Phase 1: Build-Time `ontology-schema.json` & `/api/ontology` Endpoint
1. **[`cli/src/Tools/CodeExplorer.OntologyGen/`](../../cli/src/Tools/CodeExplorer.OntologyGen/Program.cs)**:
   - Add `JsonSchemaRenderer.cs` to generate `proto/ontology-schema.json` alongside `docs/ontology.md` on build.
   - Include standard edge properties (`call_sites`, `call_count`, `ev_ids`) and relationship category classification (`Structural`, `ControlFlow`, `DataFlow`, `TypeHierarchy`).
2. **[`cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.Endpoints.cs`](../../cli/src/UI/CodeExplorer/Server/WebSocketServerHandler.Endpoints.cs)**:
   - Add `GET /api/ontology` returning the static schema + live workspace counts (`nodeCounts`, `edgeTriples`) + `schemaDriftWarnings` (undeclared edges found in SQLite).
3. **[`proto/types.ts`](../../proto/types.ts)**:
   - Add TypeScript interfaces `OntologySchemaDto`, `OntologyNodeSchemaDto`, `OntologyEdgeSchemaDto`, `OntologyPropertySchemaDto`, and `OntologyAuditSummaryDto`.

### Phase 2: Context-Aware IntelliSense in `CypherEditor` & `GateEditorView`
1. **[`vscode-extension/src/webview/domain/cypherCompletionEngine.ts`](../../vscode-extension/src/webview/domain/components/CypherEditor.tsx)**:
   - Implement pure, unit-tested functions:
     - `extractQueryAliases(query: string, schema: OntologySchemaDto)`
     - `getCypherCompletions(query: string, cursorOffset: number, schema: OntologySchemaDto)`
     - `getTemplateCompletions(query: string, templateText: string, cursorOffset: number, schema: OntologySchemaDto)`
     - `validateQueryAgainstOntology(query: string, schema: OntologySchemaDto)` (detecting unknown labels, inverted arrows, and typo'd properties).
2. **[`vscode-extension/src/webview/domain/components/CypherEditor.tsx`](../../vscode-extension/src/webview/domain/components/CypherEditor.tsx)**:
   - Add keyboard-navigable (`ArrowUp`, `ArrowDown`, `Enter`/`Tab`, `Escape`, `Ctrl+Space`) autocomplete popover and inline ontology diagnostic hints.
3. **[`vscode-extension/src/webview/components/GateEditorView.tsx`](../../vscode-extension/src/webview/components/GateEditorView.tsx)**:
   - Connect `{alias.prop}` autocomplete to the `Message Template` and `Target Template` inputs.

### Phase 3: Stratified Ontology Explorer & Validation View
1. **`OntologyExplorerView` / Graph Mode (`viewMode: 'ontology'`)**:
   - Render the 5 horizontal layer bands (`Layer 1` $\to$ `Layer 5`) on the Cytoscape canvas using deterministic stratified coordinates.
   - Add gear-menu controls (⚙️) for Edge Category toggles (`ControlFlow`, `DataFlow`, `TypeHierarchy`, `Structural`), **"Active Workspace Only"**, and **"Schema Drift Audit Overlay"**.
2. **Inspector Integration & Click-to-Query**:
   - Display `[OntologyProperty]` schema table, allowed incoming/outgoing relationships, and one-click **"Create Gate"** / **"Copy MATCH"** actions in the sidebar when a meta-node or meta-edge is selected.
