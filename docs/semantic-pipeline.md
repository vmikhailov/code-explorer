# Semantic Analysis Pipeline

This document describes the two-layer semantic analysis architecture used by CodeExplorer to derive meaning from parsed source code.

---

## Overview

Extracting *meaning* from code requires two fundamentally different kinds of work:

1. **What the code *is*** — syntactic structure, extracted faithfully from each source file, language by language.
2. **What the code *means*** — derived relationships that require cross-file, cross-project reasoning.

CodeExplorer separates these into two explicit layers. The boundary between them is Memgraph: Layer 1 writes into it; Layer 2 reads from and writes back into it.

---

## Layer 1 — Structural Graph

**Goal:** Produce a complete, faithful, structural snapshot of the codebase.

**Inputs:** Source files (`.ts`, `.cs`, `.py`, `.go`, `.sql`)

**Outputs:** Nodes and edges in Memgraph representing the raw ontology

**Rule:** *No inference.* Only facts directly observable in the source code of a single file. Language-specific parsers own this layer entirely.

### What gets written

| Node type | Source |
| --- | --- |
| `Workspace`, `Project`, `File` | Directory structure |
| `Class`, `Interface`, `Function` | AST declarations |
| `EntryPoint` | Route decorators / HTTP attributes |
| `ExternalService` | HTTP client calls (`axios.get`, `HttpClient.GetAsync`) |
| `DB` | ORM/driver imports, SQL strings |
| `Query` | Inline SQL string literals |
| `Package` | `package.json`, `.csproj`, `go.mod`, `requirements.txt` |

| Edge type | Meaning |
| --- | --- |
| `CONTAINS` | Parent–child structural ownership |
| `CALLS` | Direct invocation within a single file or resolved globally |
| `IMPLEMENTS` | Function implements an EntryPoint (route handler) |
| `INHERITS_FROM` / `IMPLEMENTS` (class) | Type hierarchy |
| `DEPENDS_ON` | Project depends on package |
| `EXPOSES` | Project exposes an EntryPoint |

### How it works

```text
Source file
    │
    ▼
TreeSitter AST
    │
    ├─ MapNodeType()       ← maps AST node type to ontology kind
    ├─ ExtractIdentifier() ← extracts name
    ├─ CollectReferences() ← emits CALLS, IMPLEMENTS, INHERITS_FROM
    └─ CollectSemanticData() ← emits RawImport, RawVariable
    │
    ▼
FileNode tree (in-memory)
    │
    ▼
OntologyUploader → Memgraph (structural nodes + edges)
    │
    ▼
WorkspaceParser (global reference resolution pass)
    │
    └─ Resolves CALLS edges across files using GlobalSymbols dict
```

### Language-specific parsers

Each language parser implements `IFileParser`:

| Parser | EntryPoint detection | ExternalService detection |
| --- | --- | --- |
| C# | `[HttpGet]`, `[Route]` attributes | `HttpClient.GetAsync` etc. |
| TypeScript | NestJS decorators, Express routes | `axios`, `fetch`, `got` |
| Python | Flask/FastAPI decorators *(planned)* | `requests`, `httpx` *(planned)* |
| Go | Gin/Echo route registrations *(planned)* | `http.Get` *(planned)* |
| SQL | — | — |

### Current gap: local type bindings

The one known incompleteness in Layer 1 is **calls through local variables**:

```typescript
// This CALLS edge IS captured:
paymentService.charge(amount);

// This CALLS edge is NOT captured — variable type unknown:
const svc = new PaymentService();
svc.charge(amount);

// This CALLS edge is NOT captured — injected dependency:
constructor(private readonly httpService: HttpService) {}
async process() { this.httpService.post(url, body); }
```

Fixing this requires a `LocalTypeBindings` pass during `CollectSemanticData`: record `(variable_name → ClassName)` when a `new X()` or typed constructor parameter is seen, then use it when resolving `member_expression` call targets.

---

## Layer 2 — Semantic Graph

**Goal:** Derive higher-order relationships that require graph traversal across the structural graph.

**Inputs:** Structural graph in Memgraph (written by Layer 1)

**Outputs:** Derived edges and property annotations written back into Memgraph

**Rule:** *Entirely Cypher queries.* No language-specific knowledge. Runs once after all files are indexed. Can be re-run at any time without re-parsing.

### Derived relationships

#### `TRANSITIVELY_CALLS`

Shortcut edge from any function to any sink it can reach through the call graph.

```cypher
MATCH path = (caller:Function)-[:CALLS*1..15]->(sink)
WHERE sink:ExternalService OR sink:DB OR sink:Query
WITH caller, sink, min(length(path)) AS hops
MERGE (caller)-[r:TRANSITIVELY_CALLS]->(sink)
SET r.hops = hops
```

#### `ATTRIBUTED_TO`

Links an `EntryPoint` directly to every `ExternalService` or `DB` reachable from it through its implementing function chain. This is the primary "what does this API do?" edge.

```cypher
MATCH path = (ep:EntryPoint)<-[:IMPLEMENTS]-(fn:Function)-[:CALLS*0..15]->(sink)
WHERE sink:ExternalService OR sink:DB OR sink:Query
WITH ep, sink, min(length(path)) AS hops, labels(sink)[0] AS sinkKind
MERGE (ep)-[r:ATTRIBUTED_TO]->(sink)
SET r.hops = hops, r.sink_kind = sinkKind
```

#### `EXPOSES_API` (on Project)

Annotates the Project with a summary of what external APIs it calls, aggregated from all its EntryPoints.

```cypher
MATCH (p:Project)-[:EXPOSES]->(ep:EntryPoint)-[:ATTRIBUTED_TO]->(es:ExternalService)
WITH p, collect(DISTINCT es.domain) AS domains
SET p.external_apis = domains
```

### What questions Layer 2 enables

| Query | Cypher pattern |
| --- | --- |
| What external services does EntryPoint X call? | `(ep)-[:ATTRIBUTED_TO]->(es:ExternalService)` |
| What DB tables does route `/orders/charge` depend on? | `(ep {route: 'charge'})-[:ATTRIBUTED_TO]->(q:Query)-[:DEPENDS_ON]->(t:Table)` |
| Which EntryPoints are affected if ExternalService Y goes down? | `(ep)-[:ATTRIBUTED_TO]->(es {domain: 'stripe.com'})` |
| What is the full call chain from endpoint to HTTP call? | `shortestPath((ep)<-[:IMPLEMENTS]-(fn)-[:CALLS*]->(es))` |
| Which projects call the same downstream service? | `(p1)-[:EXPOSES]->(ep1)-[:ATTRIBUTED_TO]->(es)<-[:ATTRIBUTED_TO]-(ep2)<-[:EXPOSES]-(p2)` |

---

## Execution order

```text
1. For each file in the workspace:
   └─ Layer 1: Parse → FileNode tree → upload to Memgraph

2. After all files parsed:
   └─ Layer 1 (cont.): WorkspaceParser global reference resolution
      └─ Resolves cross-file CALLS, IMPLEMENTS, INHERITS_FROM edges

3. After reference resolution:
   └─ Layer 2: PostIndexAnalyzer
      ├─ Write TRANSITIVELY_CALLS
      ├─ Write ATTRIBUTED_TO
      └─ Write EXPOSES_API annotations
```

### PostIndexAnalyzer (planned)

A new `PostIndexAnalyzer` class in `CodeExplorer.Core` will own Layer 2. It is:

- Instantiated once after `WorkspaceParser.IndexAsync()` completes
- Passed only a `MemgraphClient` — no language knowledge
- Fires Cypher queries in dependency order
- Idempotent: uses `MERGE` throughout so it can be re-run safely

```csharp
public class PostIndexAnalyzer
{
    private readonly MemgraphClient _db;

    public PostIndexAnalyzer(MemgraphClient db) => _db = db;

    public async Task RunAsync(string workspaceId)
    {
        await WriteTransitivelyCallsAsync(workspaceId);
        await WriteAttributedToAsync(workspaceId);
        await WriteProjectApiAnnotationsAsync(workspaceId);
    }
}
```

---

## Relationship to existing code

| Component | Layer | Status |
| --- | --- | --- |
| `TreeSitterFileParser` | 1 | Complete |
| `CSharpParser`, `TypeScriptParser`, etc. | 1 | Complete (local type bindings gap) |
| `WorkspaceParser` (reference resolution) | 1 | Complete |
| `CSharpSemanticAnalyzer`, `TypeScriptSemanticAnalyzer` | 1 | Complete (DB/API detection from imports) |
| `PostIndexAnalyzer` | 2 | **Planned** |
| `TRANSITIVELY_CALLS` edges | 2 | **Planned** |
| `ATTRIBUTED_TO` edges | 2 | **Planned** |

---

## Design principles

- **Layer 1 is stateless per file.** Each file is parsed independently. No file depends on another being parsed first (cross-file resolution is deferred to the global pass).
- **Layer 2 is pure Cypher.** It has no knowledge of programming languages. Adding a new language only requires implementing Layer 1 for it.
- **The graph is the interface.** All downstream consumers (MCP tools, RAG, UI queries) operate on the graph, never on raw AST data.
- **Re-indexing is incremental.** Layer 2 uses `MERGE`, so running `PostIndexAnalyzer` again after a partial re-index is safe and cheap.

---

## Refactoring Plan

### Current state

`ProjectProcessor.ProcessAsync()` today runs parse and enrichment in a single pass, tightly coupled:

```text
ProjectProcessor.ProcessAsync()
  ├─ Step 1: ScanDirectory → SyntaxTree[] per file         ← Layer 1 ✓
  ├─ Step 2: ParseDependencies → PackageNode[]             ← Layer 1 ✓
  ├─ Step 3: SemanticModel.AnalyzeAndEnrichAsync (per file) ← Layer 1+2 mixed ⚠
  │    ├─ Detects framework from packages          (node creation — Layer 1)
  │    ├─ Creates DbNode/ApiInUseNode from imports (node creation — Layer 1)
  │    └─ Creates VariableNode from RawVariables   (node creation — Layer 1)
  └─ OntologyUploader.UploadNodeTreeAsync                  ← Layer 1 ✓

WorkspaceParser (after all projects)
  └─ Global reference resolution (CALLS, IMPLEMENTS…)      ← Layer 1 ✓

PostIndexAnalyzer                                          ← Layer 2 ✗ missing
```

The problems with the current state:

1. `ISemanticModel` / `BaseSemanticModel` are misnamed — they enrich the *syntax* graph, not the semantic graph.
2. Enrichment runs per-file inside the parse loop; it should run per-project after all files are parsed.
3. There is no Layer 2 at all yet.

---

### Phase A — Extract library detection from language parsers into `ILibraryParser`

**Goal:** Complete the architectural evolution from monolithic parsers toward library-specific parsers that own all knowledge about their framework.

#### The evolution

The system started as a single parser per language. It then grew `ILibraryParser` — an interface with `MapNodeType`, `ExtractIdentifier`, and `CollectReferences` — so that libraries could own their own AST detection. `TreeSitterFileParser` already dispatches to library parsers *first*, before the language parser, during tree traversal. This is the right design.

The problem is that the migration is incomplete. Several critical detectors were written inline in the language parser and never moved:

| Detection logic | Lives in | Should live in |
| --- | --- | --- |
| NestJS `@Get`, `@Post` decorator → `EntryPoint` | `TypeScriptParser.IsTsDecoratorEntryPoint` | `NestJsLibraryParser` |
| Express `app.get()` → `EntryPoint` | `TypeScriptParser.IsExpressRoute` | `ExpressLibraryParser` |
| `fetch`/`got`/`superagent` → `ExternalService` | `TypeScriptParser.IsTsHttpClientCall` | `FetchLibraryParser` (built-in) |
| ASP.NET Core `[HttpGet]`/`[Route]` → `EntryPoint` | `CSharpParser` attribute detection | `AspNetCoreLibraryParser` |
| `HttpClient.GetAsync` → `ExternalService` | `CSharpParser.IsHttpClientCall` | `HttpClientLibraryParser` (built-in) |

These inline methods fire based on **AST shape alone** — no import match required — which means they trigger even in files that don't use those frameworks. A particularly bad case: `IsHttpClientCall` in `CSharpParser` tags any `GetAsync`/`PostAsync` call as `ExternalService` regardless of whether `System.Net.Http` is imported.

#### Current library parser status

Several `ILibraryParser` implementations already exist and are fully working — they prove the pattern:

| Parser | Language | Status | What it detects |
| --- | --- | --- | --- |
| `AxiosLibraryParser` | TypeScript | ✓ Implemented | `axios.*()` → `ExternalService` |
| `MongooseLibraryParser` | TypeScript | ✓ Implemented | Mongoose query calls → `Query` |
| `RedisLibraryParser` | TypeScript | ✓ Implemented | Redis commands → `Query` |
| `DapperLibraryParser` | C# | ✓ Implemented | Dapper SQL calls → `Query` with SQL deps |
| `FlurlLibraryParser` | C# | ✓ Implemented | Flurl chain calls → `ExternalService` |
| ~40 others | All | ✗ Stubs | `IsImplemented = false`, throw `NotImplementedException` |

#### Actions

1. Create `NestJsLibraryParser` in `Parsers/CodeExplorer.Parser.TypeScript/Libraries/`:
   - Move `IsTsDecoratorEntryPoint` + `ExtractTsDecoratorRoute` logic into `MapNodeType`/`ExtractIdentifier`.
   - `IsImplemented = true`, `IsBuiltIn = false` (only fires when `@nestjs/common` etc. is imported).

2. Create `ExpressLibraryParser` in `Parsers/CodeExplorer.Parser.TypeScript/Libraries/`:
   - Move `IsExpressRoute` + `ExtractExpressRoute` logic.
   - `IsImplemented = true`, `IsBuiltIn = false`.

3. Create `FetchLibraryParser` in `Parsers/CodeExplorer.Parser.TypeScript/Libraries/`:
   - Move `IsTsHttpClientCall` / `ExtractTsHttpClientTarget` for `fetch`, `node-fetch`, `got`, `superagent`.
   - `IsImplemented = true`, `IsBuiltIn = true` (fetch is a browser/Node built-in).

4. Create `AspNetCoreLibraryParser` in `Parsers/CodeExplorer.Parser.CSharp/Libraries/`:
   - Move ASP.NET Core attribute detection (`[HttpGet]`, `[Route]`, etc.).
   - `IsImplemented = true`, `IsBuiltIn = false`, pattern `["Microsoft.AspNetCore"]`.

5. Create `HttpClientLibraryParser` in `Parsers/CodeExplorer.Parser.CSharp/Libraries/`:
   - Move `IsHttpClientCall` / `ExtractHttpClientTarget`.
   - `IsImplemented = true`, `IsBuiltIn = true` (`System.Net.Http` is part of .NET runtime).

6. After each new library parser is wired into `LibraryParsers`, **delete** the corresponding inline detection from `TypeScriptParser` and `CSharpParser`.

7. Also delete the parameterless default constructors from all `SyntaxEnricher` classes (e.g. `new TypeScriptParser().LibraryParsers`) — enrichers must only be created via `parser.GetSyntaxEnricher(syntaxTree)`.

**Risk:** Low per parser — each is an isolated extract-and-delete with a clear test via `ParserValidationTests`. Do one at a time and build between each.

---

### Phase B — ~~Split `ProjectProcessor` into two passes~~ (dropped)

`ProjectProcessor.ProcessAsync()` already has the right structure. The `ISyntaxEnricher` is **still Layer 1** — it creates `DbNode`, `ApiInUseNode` nodes from import statements visible in source files. That is observable structural fact, not derived reasoning.

`LibraryParsers` being registered on `IFileParser` confirms the coupling is intentional: library detection is part of file parsing, and enrichment is its direct consumer at the project level. Separating them across two passes would add orchestration complexity for no benefit.

The correct conceptual split is:

```text
IFileParser            ← reads file, emits AST nodes + RawImports (LibraryParsers)
ISyntaxEnricher        ← turns RawImports into graph nodes (DbNode, ApiInUseNode)
PostIndexAnalyzer      ← derives edges from the complete graph   ← Layer 2
```

`IFileParser` + `ISyntaxEnricher` together are Layer 1. `PostIndexAnalyzer` alone is Layer 2. Phase B is dropped.

---

### Phase C — Add `PostIndexAnalyzer` (Layer 2)

**Goal:** Derive semantic edges after all structural + enrichment data is in Memgraph.

**New file:** `Core/CodeExplorer.Core/Parser/PostIndexAnalyzer.cs`

```csharp
public class PostIndexAnalyzer(MemgraphClient db)
{
    public async Task RunAsync(string workspaceId)
    {
        await WriteTransitivelyCallsAsync(workspaceId);
        await WriteAttributedToAsync(workspaceId);
        await WriteProjectApiAnnotationsAsync(workspaceId);
    }

    private Task WriteTransitivelyCallsAsync(string workspaceId) => db.ExecuteAsync("""
        MATCH path = (caller:Function {workspace_id: $wid})-[:CALLS*1..15]->(sink)
        WHERE sink:ExternalService OR sink:DB OR sink:Query
        WITH caller, sink, min(length(path)) AS hops
        MERGE (caller)-[r:TRANSITIVELY_CALLS]->(sink)
        SET r.hops = hops
        """, new { wid = workspaceId });

    private Task WriteAttributedToAsync(string workspaceId) => db.ExecuteAsync("""
        MATCH path = (ep:EntryPoint {workspace_id: $wid})<-[:IMPLEMENTS]-(fn:Function)-[:CALLS*0..15]->(sink)
        WHERE sink:ExternalService OR sink:DB OR sink:Query
        WITH ep, sink, min(length(path)) AS hops, labels(sink)[0] AS sinkKind
        MERGE (ep)-[r:ATTRIBUTED_TO]->(sink)
        SET r.hops = hops, r.sink_kind = sinkKind
        """, new { wid = workspaceId });

    private Task WriteProjectApiAnnotationsAsync(string workspaceId) => db.ExecuteAsync("""
        MATCH (p:Project {workspace_id: $wid})-[:EXPOSES]->(ep:EntryPoint)-[:ATTRIBUTED_TO]->(es:ExternalService)
        WITH p, collect(DISTINCT es.domain) AS domains
        SET p.external_apis = domains
        """, new { wid = workspaceId });
}
```

**Actions:**

1. Create `PostIndexAnalyzer.cs` in `Core/CodeExplorer.Core/Parser/`.
2. Verify `MemgraphClient` has a parameterised `ExecuteAsync(string cypher, object params)` method; add it if missing.
3. Call `await new PostIndexAnalyzer(ctx.DbClient).RunAsync(ctx.WorkspaceId)` at the end of `WorkspaceParser.IndexAsync()`, after global reference resolution.
4. Add `TRANSITIVELY_CALLS` and `ATTRIBUTED_TO` to `OntologyConstants.Relationships`.

**Risk:** None — purely additive. Existing graph is unchanged; new edges are added on top.

---

### Phase D — Local type bindings (call graph completeness)

**Goal:** Capture `CALLS` edges through local variables and injected constructor parameters.

**Problem:**

```typescript
// Not captured today:
const svc = new PaymentService();   // svc → PaymentService
svc.charge(amount);                  // CALLS PaymentService.charge — missed

constructor(private readonly http: HttpService) {}
async run() { this.http.post(url); } // CALLS HttpService.post — missed
```

**Solution:** During `CollectSemanticData`, build a `LocalTypeBindings` dictionary per function scope:

- When seeing `new X()` assigned to variable `v` → record `v → X`
- When seeing constructor parameter `v: X` → record `this.v → X`
- When resolving a `member_expression` `v.method()` → look up `v` in bindings → emit `CALLS(scope, X.method)`

**Actions:**

1. Add `RawTypeBinding(string VariableName, string TypeName, string FilePath, string ScopeId)` to `RawSemanticData.cs`.
2. Add `List<RawTypeBinding> RawTypeBindings` to `SyntaxTree` and `ParsingContext`.
3. In `TypeScriptParser.CollectSemanticData`:
   - Detect `new_expression` → record binding.
   - Detect constructor `parameter` with type annotation → record `this.name → type`.
4. In `CSharpParser.CollectSemanticData`:
   - Detect `object_creation_expression` → record binding.
   - Detect constructor `parameter` → record `this.name → type`.
5. In `WorkspaceParser` global resolution, after `CALLS` resolution: for each `CALLS` reference whose target resolves to `v.method`, look up `v` in `RawTypeBindings` and re-resolve to `TypeName.method`.

**Risk:** Medium — touches the reference resolution hot path. Should be guarded by the existing test suite and a new targeted test.

---

### Delivery order

| Phase | Effort | Risk | Value |
| --- | --- | --- | --- |
| A — Extract library parsers | 1–2h per parser | Low (isolated) | Correct detection, removes false positives |
| C — PostIndexAnalyzer | 2h | None | Layer 2 queries immediately usable |
| D — Local type bindings | 4–5h | Medium | Call graph completeness, improves all Layer 2 results |

Recommended order: **A (one parser at a time) → C → D**.
