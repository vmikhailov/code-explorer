# Semantic Invariant Gates (SIG) Proposal & Catalog 🛡️
## Comprehensive Taxonomy of Architectural & Polyglot Platform Gates ("Gates on Growth")

- **Document Version:** 1.0.0
- **Status:** Architecture Reference & "Blueprint for Growth"
- **Target Audience:** Systems Architects, Tech Leads, Compiler Engineers, and Autonomous AI Coding Agents
- **Target Subsystems:** `CodeExplorer.Core.Gates`, `CodeExplorer.UI.Commands.GateCommandHandler`, `CodeExplorer.Core.Mcp`

---

## 1. Overview & Architectural Philosophy

The **Semantic Invariant Gate (SIG)** system in **CodeExplorer** replaces brittle, single-language, regex-based linters and slow integration test suites with **declarative, sub-millisecond OpenCypher invariants** executed directly against the unified SQLite semantic graph (`graph.db`).

### 1.1 The Zero-Match Invariant Contract
Every gate represents a **forbidden architectural topology**.
- **Healthy Codebase:** The Cypher query returns **0 rows** $\implies$ Gate **PASSED**.
- **Architectural Violation:** The Cypher query returns $\ge 1$ rows $\implies$ Gate **FAILED**. Each returned row provides the exact file, line, symbol, target, and actionable remediation message.

### 1.2 Two-Dimensional Taxonomy

Every gate is classified along two axes:

1. **Category**:
   - **`Architectural` (Platform-Agnostic):** Universal laws of modularity, boundaries, flow direction, Clean/Hexagonal architecture, and technical debt that apply to *any* language.
   - **`Platform` (Language/Framework-Specific):** Precision invariants enforcing idioms, concurrency safety, framework lifecycles, and driver abstractions for specific runtimes (.NET, TypeScript, Go, Python, Java, Kotlin/Android, SQL, ColdFusion).

2. **FOIM Coordinate (Scope $\times$ Dimension):**
   - **Scope:** Method (`M`), Class/Type (`C`), Module/Package (`P`), Service/Project (`S`), System/Workspace (`A`).
   - **Dimension:** External Contract (`C`), Implementation Rules (`I`), Resource & Complexity (`R`), Scope Boundaries (`B`).

```
                    FRACTAL ORTHOGONAL INVARIANT MATRIX (FOIM)
            ┌──────────┬──────────┬──────────┬──────────┬─────────────┐
            │  Method  │  Class   │  Module  │ Service  │   System    │
┌───────────┼──────────┼──────────┼──────────┼──────────┼─────────────┤
│ Contract  │ [M, C]   │ [C, C]   │ [P, C]   │ [S, C]   │   [A, C]    │
├───────────┼──────────┼──────────┼──────────┼──────────┼─────────────┤
│ Rules     │ [M, I]   │ [C, I]   │ [P, I]   │ [S, I]   │   [A, I]    │
├───────────┼──────────┼──────────┼──────────┼──────────┼─────────────┤
│ Resources │ [M, R]   │ [C, R]   │ [P, R]   │ [S, R]   │   [A, R]    │
├───────────┼──────────┼──────────┼──────────┼──────────┼─────────────┤
│ Boundary  │ [M, B]   │ [C, B]   │ [P, B]   │ [S, B]   │   [A, B]    │
└───────────┴──────────┴──────────┴──────────┴──────────┴─────────────┘
```

---

## 2. Cross-Platform Architectural Gates (`Architectural`)

These gates enforce clean architecture, strict layer boundaries, encapsulation, and system flow invariants regardless of programming language.

---

### `SIG-P-B-001`: Pipeline Stage Strict Hierarchy
- **FOIM Coordinate:** `[P, B]` (Module $\times$ Boundaries)
- **Scope:** Module
- **Dimension:** Boundaries
- **Severity:** `Error`
- **Description:** Enforces unidirectional flow in phased pipelines: Stage $N$ must never depend on or call higher-order stage $N+1$ or later.
- **Remediation:** Remove forward calls. Pass intermediate context down the pipeline, use decoupled event buses, or move shared data contracts to a common foundation module.
- **Cypher Query:**
```cypher
MATCH (src:Type)-[:CALLS|USES_TYPE]->(tgt:Type)
WHERE src.stage_order IS NOT NULL 
  AND tgt.stage_order IS NOT NULL 
  AND src.stage_order < tgt.stage_order
RETURN 
  src.file_path AS file,
  src.start_line AS line,
  src.name AS symbol,
  tgt.name AS target,
  'Pipeline stage violation: stage ' + src.stage_order + ' illegally depends on successor stage ' + tgt.stage_order AS message
```

---

### `SIG-P-B-003`: Clean Architecture Transport Purity
- **FOIM Coordinate:** `[P, B]` (Module $\times$ Boundaries)
- **Scope:** Module
- **Dimension:** Boundaries
- **Severity:** `Error`
- **Description:** Transport endpoints (HTTP Controllers, gRPC handlers, CLI commands) must not directly execute database queries or mutate ORM tables.
- **Remediation:** Inject domain services or repository interfaces into controllers. Move persistence calls into service or repository layers.
- **Cypher Query:**
```cypher
MATCH (ep:Endpoint)-[:CALLS]->(q:Query)
RETURN 
  ep.file_path AS file,
  coalesce(ep.start_line, 0) AS line,
  ep.name AS symbol,
  q.name AS target,
  'Transport endpoint directly executes raw database query bypassing domain services' AS message
```

---

### `SIG-P-B-004`: Domain Core Framework Independence
- **FOIM Coordinate:** `[P, B]` (Module $\times$ Boundaries)
- **Scope:** Module
- **Dimension:** Boundaries
- **Severity:** `Error`
- **Description:** Core business entities and domain services must be pure and free of external transport, serialization, or cloud framework dependencies.
- **Remediation:** Remove web/cloud imports from domain modules. Use domain-specific interfaces and implement adapters in the infrastructure layer.
- **Cypher Query:**
```cypher
MATCH (p:Project)-[:CONTAINS]->(f:File)<-[:DECLARED_IN]-(core:Type)-[:USES_TYPE]->(infra:Type)
WHERE (p.role = 'Domain' OR core.file_path CONTAINS 'Domain/' OR core.symbol CONTAINS 'Core/Domain')
  AND (infra.symbol CONTAINS 'AspNetCore' OR infra.symbol CONTAINS 'Express' OR infra.symbol CONTAINS 'FastAPI' OR infra.symbol CONTAINS 'Spring' OR infra.symbol CONTAINS 'AWS' OR infra.symbol CONTAINS 'Azure')
RETURN 
  core.file_path AS file,
  core.start_line AS line,
  core.name AS symbol,
  infra.name AS target,
  'Domain core entity leaks coupling to external transport/cloud framework' AS message
```

---

### `SIG-S-B-002`: Cross-Service Database Isolation
- **FOIM Coordinate:** `[S, B]` (Service $\times$ Boundaries)
- **Scope:** Service
- **Dimension:** Boundaries
- **Severity:** `Error`
- **Description:** Service $A$ must never directly query or mutate database tables owned by Service $B$. Data persistence must be private to each service boundary.
- **Remediation:** Expose an API endpoint, gRPC method, or domain event from Service $B$ instead of allowing Service $A$ to reach directly into Service $B$'s database.
- **Cypher Query:**
```cypher
MATCH (s1:Service)-[:USES_DB]->(t:Table)<-[:USES_DB]-(s2:Service)
WHERE s1.id <> s2.id 
  AND t.owner_service_id IS NOT NULL 
  AND s1.id <> t.owner_service_id
RETURN 
  s1.path AS file,
  1 AS line,
  s1.name AS symbol,
  t.name + ' (owned by ' + s2.name + ')' AS target,
  'Cross-service database leak: service directly accesses private tables of another service' AS message
```

---

### `SIG-S-B-003`: Outbound Egress Gateway Chokepoint
- **FOIM Coordinate:** `[S, B]` (Service $\times$ Boundaries)
- **Scope:** Service
- **Dimension:** Boundaries
- **Severity:** `Warning`
- **Description:** Outbound HTTP, gRPC, and third-party API calls must be concentrated in dedicated Gateway/Adapter classes, not scattered across arbitrary business services.
- **Remediation:** Encapsulate third-party API invocations inside an outbound adapter class implementing an interface.
- **Cypher Query:**
```cypher
MATCH (caller:Function)-[:CALLS]->(es:ExternalService)
WHERE NOT (caller.symbol CONTAINS 'Gateway' OR caller.symbol CONTAINS 'Adapter' OR caller.symbol CONTAINS 'Client' OR caller.file_path CONTAINS 'Egress' OR caller.file_path CONTAINS 'Infrastructure')
RETURN 
  caller.file_path AS file,
  caller.start_line AS line,
  caller.name AS symbol,
  es.name AS target,
  'Ungoverned external egress: third-party service called directly outside dedicated gateway/adapter' AS message
```

---

### `SIG-P-I-001`: Circular Package Dependency Shield
- **FOIM Coordinate:** `[P, I]` (Module $\times$ Implementation Rules)
- **Scope:** Module
- **Dimension:** Implementation Rules
- **Severity:** `Error`
- **Description:** Package and module dependencies must form a strict Directed Acyclic Graph (DAG) with no cyclic loops.
- **Remediation:** Break circular dependencies using interfaces (Dependency Inversion), domain events, or extracting shared structures to a common leaf module.
- **Cypher Query:**
```cypher
MATCH (p1:Package)-[:DEPENDS_ON*1..3]->(p2:Package)
WHERE (p2)-[:DEPENDS_ON*1..3]->(p1) AND p1.id <> p2.id
RETURN 
  p1.name AS file,
  1 AS line,
  p1.name AS symbol,
  p2.name AS target,
  'Circular package dependency cycle detected between modules' AS message
```

---

### `SIG-A-C-001`: Dead Public Endpoint Detection
- **FOIM Coordinate:** `[A, C]` (System $\times$ External Contract)
- **Scope:** System
- **Dimension:** External Contract
- **Severity:** `Warning`
- **Description:** Detects public HTTP endpoints that have zero inbound callers across all workspace services, client applications, and test suites.
- **Remediation:** Deprecate and remove dead endpoints, or implement missing client consumers and integration test coverage.
- **Cypher Query:**
```cypher
MATCH (ep:Endpoint)
WHERE ep.is_public = true 
  AND NOT EXISTS((:Function)-[:CALLS_ENDPOINT]->(ep))
  AND NOT EXISTS((:ExternalService)-[:CALLS_ENDPOINT]->(ep))
RETURN 
  ep.file_path AS file,
  coalesce(ep.start_line, 0) AS line,
  ep.name AS symbol,
  ep.http_method + ' ' + ep.route AS target,
  'Dead public endpoint: zero detected inbound callers from services, clients, or test suites' AS message
```

---

### `SIG-M-I-001`: Disallow Hardcoded Call Arguments
- **FOIM Coordinate:** `[M, I]` (Method $\times$ Implementation Rules)
- **Scope:** Method
- **Dimension:** Implementation Rules
- **Severity:** `Error`
- **Description:** Production methods must not receive raw, hardcoded string literals as arguments. Use typed enums, strongly-typed domain identifiers, named constants, or external configuration.
- **Remediation:** Replace string literals with strongly-typed enums, configuration settings, or constants.
- **Cypher Query:**
```cypher
MATCH (p:Project)-[:CONTAINS]->(f:File)<-[:DECLARED_IN]-(lit:CallLiteral)<-[:PASSES_LITERAL]-(caller)
WHERE p.kind <> 'Test' AND NOT coalesce(f.is_test, false)
RETURN 
  lit.file_path AS file,
  lit.line AS line,
  lit.column AS column,
  coalesce(caller.name, 'unknown') AS symbol,
  lit.callee + '(' + lit.value + ')' AS target,
  'Hardcoded string literal "' + lit.value + '" passed to ' + lit.callee AS message
```

---

### `SIG-C-I-001`: In-Memory AST Visitor IO Purity
- **FOIM Coordinate:** `[C, I]` (Class $\times$ Implementation Rules)
- **Scope:** Class
- **Dimension:** Implementation Rules
- **Severity:** `Error`
- **Description:** Syntax visitors, AST enrichers, and code analyzers must operate purely on in-memory syntax trees and avoid ambient disk I/O.
- **Remediation:** Pass file contents or pre-parsed trees via pipeline context instead of reading files during AST traversal.
- **Cypher Query:**
```cypher
MATCH (visitor:Type)-[:CALLS]->(io:Type)
WHERE (visitor.symbol CONTAINS 'SyntaxEnricher' OR visitor.symbol CONTAINS 'Visitor' OR visitor.file_path CONTAINS 'SyntaxEnricher')
  AND io.name IN [
    'File', 'Directory', 'FileStream', 'StreamReader', 'StreamWriter',
    'fs', 'promises', 'fs-extra',
    'os', 'ioutil',
    'pathlib', 'shutil',
    'Files', 'Paths', 'FileInputStream', 'FileOutputStream'
  ]
RETURN 
  coalesce(visitor.symbol, visitor.file_path) AS file,
  coalesce(visitor.start_line, 0) AS line,
  visitor.name AS symbol,
  io.name AS target,
  'AST enricher/visitor performs ambient disk I/O instead of operating on in-memory AST' AS message
```

---

### `SIG-A-I-001`: Dual-Write Messaging Transaction Invariance
- **FOIM Coordinate:** `[A, I]` (System $\times$ Implementation Rules)
- **Scope:** System
- **Dimension:** Implementation Rules
- **Severity:** `Error`
- **Description:** A single operation must not both mutate a database and publish to a message broker without an Outbox pattern or transactional coordinator (prevents dual-write consistency bugs).
- **Remediation:** Route message publishing through a transactional Outbox table committed atomically with database mutations.
- **Cypher Query:**
```cypher
MATCH (m:Function)-[:MUTATES]->(t:Table),
      (m)-[:PUBLISHES_TO]->(top:Topic)
WHERE NOT EXISTS((m)-[:USES_TRANSACTION]->(:TransactionManager))
  AND NOT EXISTS((:Table {name: 'Outbox'})<-[:MUTATES]-(m))
RETURN 
  m.file_path AS file,
  m.start_line AS line,
  m.name AS symbol,
  top.name AS target,
  'Uncoordinated dual-write: handler mutates database table and publishes message without Outbox' AS message
```

---

## 3. Platform-Specific Gates (`Platform`)

---

### 3.1 .NET / C# Platform Gates (`dotnet`)

#### `SIG-DOTNET-M-I-001`: Sync-Over-Async Deadlock Prevention
- **FOIM Coordinate:** `[M, I]` (Method $\times$ Implementation Rules)
- **Severity:** `Error`
- **Description:** Calling `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` synchronously blocks threads in async execution paths, causing thread pool starvation and deadlocks.
- **Remediation:** Use `await` throughout the call stack; never block synchronously on asynchronous tasks.
- **Cypher Query:**
```cypher
MATCH (caller:Function)-[:CALLS]->(blocker:Function)
WHERE blocker.name IN ['get_Result', 'Wait', 'GetResult']
  AND blocker.symbol CONTAINS 'System.Threading.Tasks'
RETURN 
  caller.file_path AS file,
  caller.start_line AS line,
  caller.name AS symbol,
  blocker.name AS target,
  'Sync-over-async anti-pattern: blocking call on Task threatens thread-pool starvation' AS message
```

#### `SIG-DOTNET-S-B-001`: Captive Dependency Lifetime Violation
- **FOIM Coordinate:** `[S, B]` (Service $\times$ Boundaries)
- **Severity:** `Error`
- **Description:** Scoped dependencies (such as EF Core `DbContext` or per-request units of work) must never be injected into Singleton services (causes state pollution and concurrency crashes).
- **Remediation:** Inject `IServiceScopeFactory` into the Singleton to create short-lived scopes, or re-register the consumer as Scoped.
- **Cypher Query:**
```cypher
MATCH (singleton:Type)-[:INJECTS]->(scoped:Type)
WHERE singleton.lifetime = 'Singleton' AND scoped.lifetime = 'Scoped'
RETURN 
  singleton.file_path AS file,
  singleton.start_line AS line,
  singleton.name AS symbol,
  scoped.name AS target,
  'Captive dependency: Singleton service holds reference to Scoped dependency' AS message
```

#### `SIG-DOTNET-S-B-002`: Storage Interface Abstraction Invariance
- **FOIM Coordinate:** `[S, B]` (Service $\times$ Boundaries)
- **Severity:** `Error`
- **Description:** Domain and pipeline layers must interact with databases via abstractions (`IGraphClient`, repositories) rather than concrete ADO.NET drivers (`SqliteConnection`, `SqlConnection`).
- **Remediation:** Inject `IGraphClient` or an abstraction interface; encapsulate concrete connection classes in infrastructure projects.
- **Cypher Query:**
```cypher
MATCH (parser:Type)-[:CALLS|USES_TYPE]->(concreteDb:Type)
WHERE (parser.symbol CONTAINS 'Parser/' OR parser.file_path CONTAINS 'Parser/')
  AND concreteDb.name IN ['SqliteGraphClient', 'SqliteConnection', 'SqliteCommand', 'SqlConnection', 'NpgsqlConnection']
  AND NOT (parser.symbol CONTAINS 'Test' OR parser.file_path CONTAINS 'Test')
RETURN 
  coalesce(parser.symbol, parser.file_path) AS file,
  coalesce(parser.start_line, 0) AS line,
  parser.name AS symbol,
  concreteDb.name AS target,
  'High-level parser couples to concrete ADO.NET database connection instead of abstraction' AS message
```

#### `SIG-DOTNET-C-R-001`: Unbounded Entity Framework Streaming
- **FOIM Coordinate:** `[C, R]` (Class $\times$ Resources)
- **Severity:** `Warning`
- **Description:** Executing `.ToList()` or `.ToArray()` directly on an `IQueryable` without pagination (`Take`), projection (`Select`), or `CancellationToken` risks massive memory allocation.
- **Remediation:** Apply `.Take(limit)`, `.Select(...)` DTO projections, or accept a `CancellationToken`.
- **Cypher Query:**
```cypher
MATCH (caller:Function)-[:CALLS]->(sink:Function)
WHERE sink.name IN ['ToListAsync', 'ToList', 'ToArrayAsync', 'ToArray']
  AND NOT EXISTS((caller)-[:CALLS]->(:Function {name: 'Take'}))
  AND caller.file_path CONTAINS 'Repository'
RETURN 
  caller.file_path AS file,
  caller.start_line AS line,
  caller.name AS symbol,
  sink.name AS target,
  'Unbounded query materialization: ToList called without prior Take/pagination' AS message
```

---

### 3.2 TypeScript / JavaScript Platform Gates (`typescript`)

#### `SIG-TS-M-C-001`: Any Type Escape Shield
- **FOIM Coordinate:** `[M, C]` (Method $\times$ External Contract)
- **Severity:** `Error`
- **Description:** Publicly exported functions, controllers, and domain interfaces must not accept or return `any` or untyped `unknown` without runtime type guards.
- **Remediation:** Replace `any` with strongly-typed interfaces, generics, or Zod schema validation types.
- **Cypher Query:**
```cypher
MATCH (fn:Function)
WHERE fn.is_exported = true 
  AND (fn.return_type = 'any' OR fn.parameters_types CONTAINS 'any')
  AND NOT fn.file_path CONTAINS '.test.'
RETURN 
  fn.file_path AS file,
  fn.start_line AS line,
  fn.name AS symbol,
  fn.return_type AS target,
  'Type safety erosion: exported function exposes untyped any at module boundary' AS message
```

#### `SIG-TS-P-B-001`: Next.js Server-Client Boundary Isolation
- **FOIM Coordinate:** `[P, B]` (Module $\times$ Boundaries)
- **Severity:** `Error`
- **Description:** Client components (`'use client'`) must never import server-only modules, database drivers (`prisma`, `drizzle`, `pg`), or backend utilities.
- **Remediation:** Move database and server operations to Server Actions (`'use server'`) or API route handlers.
- **Cypher Query:**
```cypher
MATCH (clientFile:File)-[:IMPORTS]->(serverPkg:Package)
WHERE clientFile.has_use_client_directive = true 
  AND serverPkg.name IN ['@prisma/client', 'drizzle-orm', 'pg', 'mysql2', 'fs', 'jsonwebtoken', 'bcrypt']
RETURN 
  clientFile.path AS file,
  1 AS line,
  clientFile.name AS symbol,
  serverPkg.name AS target,
  'Server-client boundary breach: Client component imports server-only database/crypto package' AS message
```

#### `SIG-TS-M-R-001`: Node.js Event Loop Blocking
- **FOIM Coordinate:** `[M, R]` (Method $\times$ Resources)
- **Severity:** `Error`
- **Description:** Synchronous file system or child process calls (`fs.readFileSync`, `execSync`) in HTTP handlers block the single Node.js event loop thread for all concurrent requests.
- **Remediation:** Replace synchronous calls with asynchronous counterparts (`fs.promises.readFile`, `execFile` with promises).
- **Cypher Query:**
```cypher
MATCH (ep:Endpoint)-[:CALLS*1..3]->(blocking:Function)
WHERE blocking.name IN ['readFileSync', 'writeFileSync', 'execSync', 'spawnSync']
RETURN 
  ep.file_path AS file,
  ep.start_line AS line,
  ep.name AS symbol,
  blocking.name AS target,
  'Event loop blocking: synchronous I/O call executed in HTTP request handling chain' AS message
```

---

### 3.3 Go Platform Gates (`go`)

#### `SIG-GO-M-I-001`: Unbounded Goroutine Lifecycle Protection
- **FOIM Coordinate:** `[M, I]` (Method $\times$ Implementation Rules)
- **Severity:** `Error`
- **Description:** Spawning goroutines with `go func()` without propagating a `context.Context` or registering with a `sync.WaitGroup` leads to silent goroutine leaks.
- **Remediation:** Pass `ctx context.Context` into the goroutine and monitor `<-ctx.Done()`, or use an `errgroup.Group`.
- **Cypher Query:**
```cypher
MATCH (caller:Function)-[:SPAWNS_GOROUTINE]->(routine:Function)
WHERE NOT EXISTS((routine)-[:USES_TYPE]->(:Type {name: 'Context'}))
  AND NOT EXISTS((caller)-[:USES_TYPE]->(:Type {name: 'WaitGroup'}))
RETURN 
  caller.file_path AS file,
  caller.start_line AS line,
  caller.name AS symbol,
  routine.name AS target,
  'Goroutine leak risk: background goroutine spawned without context cancellation or wait group' AS message
```

#### `SIG-GO-P-B-001`: Internal Package Encapsulation Guard
- **FOIM Coordinate:** `[P, B]` (Module $\times$ Boundaries)
- **Severity:** `Error`
- **Description:** Packages inside an `/internal/...` directory must never be imported by code outside their parent tree.
- **Remediation:** Move shared code to a public package (e.g. `/pkg/...`) or keep usage strictly within the internal module's root.
- **Cypher Query:**
```cypher
MATCH (caller:File)-[:IMPORTS]->(targetPkg:Package)
WHERE targetPkg.path CONTAINS '/internal/'
  AND NOT caller.path STARTS WITH substring(targetPkg.path, 0, indexOf(targetPkg.path, '/internal/'))
RETURN 
  caller.path AS file,
  1 AS line,
  caller.name AS symbol,
  targetPkg.name AS target,
  'Go internal boundary violation: module imports private /internal/ package outside parent tree' AS message
```

#### `SIG-GO-M-R-001`: Unclosed SQL Rows Resource Hygiene
- **FOIM Coordinate:** `[M, R]` (Method $\times$ Resources)
- **Severity:** `Error`
- **Description:** Calling `db.Query()` without deferring `rows.Close()` leaks database connection pool handles under load.
- **Remediation:** Always call `defer rows.Close()` immediately after checking the query error.
- **Cypher Query:**
```cypher
MATCH (fn:Function)-[:CALLS]->(q:Function {name: 'Query'})
WHERE NOT EXISTS((fn)-[:CALLS]->(:Function {name: 'Close'}))
RETURN 
  fn.file_path AS file,
  fn.start_line AS line,
  fn.name AS symbol,
  q.name AS target,
  'Database connection leak: sql.Rows queried without guaranteed rows.Close()' AS message
```

---

### 3.4 Python Platform Gates (`python`)

#### `SIG-PY-M-I-001`: Global Module State Mutation
- **FOIM Coordinate:** `[M, I]` (Method $\times$ Implementation Rules)
- **Severity:** `Error`
- **Description:** Modifying `global` variables from inside functions creates unpredictable side effects, concurrency races under ASGI/WSGI, and untestable code.
- **Remediation:** Pass state via dependency injection, classes, or explicit parameter passing.
- **Cypher Query:**
```cypher
MATCH (fn:Function)-[:WRITES_GLOBAL]->(v:Member)
WHERE NOT fn.file_path CONTAINS 'test'
RETURN 
  fn.file_path AS file,
  fn.start_line AS line,
  fn.name AS symbol,
  v.name AS target,
  'Runtime purity violation: function mutates global module variable' AS message
```

#### `SIG-PY-M-R-001`: Async Handler Blocking Call Shield
- **FOIM Coordinate:** `[M, R]` (Method $\times$ Resources)
- **Severity:** `Error`
- **Description:** Invoking synchronous blocking I/O (`requests.get`, `time.sleep`, synchronous database drivers) inside `async def` routes stalls the Python asyncio event loop.
- **Remediation:** Use async clients (`httpx.AsyncClient`, `asyncio.sleep`, `asyncpg`) or offload to `asyncio.to_thread()`.
- **Cypher Query:**
```cypher
MATCH (route:Function)-[:CALLS*1..2]->(blocking:Function)
WHERE route.is_async = true 
  AND blocking.name IN ['sleep', 'get', 'post', 'execute']
  AND blocking.symbol IN ['time.sleep', 'requests.get', 'requests.post', 'urllib.request.urlopen']
RETURN 
  route.file_path AS file,
  route.start_line AS line,
  route.name AS symbol,
  blocking.symbol AS target,
  'AsyncIO thread freeze: synchronous blocking call invoked inside async route handler' AS message
```

#### `SIG-PY-M-C-001`: Untyped Dict Return Elimination
- **FOIM Coordinate:** `[M, C]` (Method $\times$ External Contract)
- **Severity:** `Warning`
- **Description:** Returning raw, untyped `dict` from service or repository layers hides schema contracts and defeats static type analysis.
- **Remediation:** Return Pydantic models (`BaseModel`) or typed `dataclass` instances.
- **Cypher Query:**
```cypher
MATCH (fn:Function)
WHERE fn.file_path CONTAINS 'service' 
  AND fn.return_type IN ['dict', 'Dict', 'typing.Dict', 'Any']
RETURN 
  fn.file_path AS file,
  fn.start_line AS line,
  fn.name AS symbol,
  fn.return_type AS target,
  'Contract degradation: domain/service function returns raw untyped dict instead of model' AS message
```

---

### 3.5 Java / Spring Boot Platform Gates (`java`)

#### `SIG-JAVA-M-I-001`: Spring AOP Self-Invocation Bypass
- **FOIM Coordinate:** `[M, I]` (Method $\times$ Implementation Rules)
- **Severity:** `Error`
- **Description:** Invoking a `@Transactional` or `@Async` method from within the same class (`this.method()`) bypasses Spring's dynamic AOP proxy, silently disabling transactions or asynchronous execution.
- **Remediation:** Move the transactional method to a separate collaborator service, or inject the self-proxy bean.
- **Cypher Query:**
```cypher
MATCH (cls:Type)-[:DECLARES]->(m1:Function),
      (cls)-[:DECLARES]->(m2:Function)
WHERE (m2.has_annotation = 'Transactional' OR m2.has_annotation = 'Async')
  AND (m1)-[:CALLS]->(m2)
RETURN 
  cls.file_path AS file,
  m1.start_line AS line,
  m1.name + ' -> ' + m2.name AS symbol,
  m2.name AS target,
  'Spring AOP proxy bypass: internal call to @Transactional/@Async method disables interceptor' AS message
```

#### `SIG-JAVA-C-R-001`: JPA Eager Fetching N+1 Hazard
- **FOIM Coordinate:** `[C, R]` (Class $\times$ Resources)
- **Severity:** `Error`
- **Description:** Annotating collection relationships (`@OneToMany`, `@ManyToMany`) with `FetchType.EAGER` triggers massive query cascades and memory exhaustion.
- **Remediation:** Change relationship fetch mode to `FetchType.LAZY` and use `JOIN FETCH` or Entity Graphs for targeted loading.
- **Cypher Query:**
```cypher
MATCH (entity:Type)-[:HAS_RELATIONSHIP]->(prop:Member)
WHERE prop.annotation_name IN ['OneToMany', 'ManyToMany']
  AND prop.fetch_type = 'EAGER'
RETURN 
  entity.file_path AS file,
  prop.start_line AS line,
  entity.name + '.' + prop.name AS symbol,
  prop.fetch_type AS target,
  'JPA performance hazard: collection relationship marked EAGER causes query explosion' AS message
```

#### `SIG-JAVA-P-B-001`: Controller Direct Repository Access
- **FOIM Coordinate:** `[P, B]` (Module $\times$ Boundaries)
- **Severity:** `Error`
- **Description:** Spring `@RestController` classes must coordinate with `@Service` components and never directly inject `@Repository` interfaces or `EntityManager`.
- **Remediation:** Introduce a service layer method to encapsulate business logic and data access.
- **Cypher Query:**
```cypher
MATCH (ctrl:Type)-[:INJECTS]->(repo:Type)
WHERE ctrl.has_annotation IN ['RestController', 'Controller']
  AND repo.has_annotation IN ['Repository', 'JpaRepository']
RETURN 
  ctrl.file_path AS file,
  ctrl.start_line AS line,
  ctrl.name AS symbol,
  repo.name AS target,
  'Layer inversion: Spring Web Controller directly injects persistence Repository' AS message
```

---

### 3.6 Kotlin & Android Platform Gates (`kotlin` / `android`)

#### `SIG-KOTLIN-M-B-001`: Android Main Thread I/O Ban
- **FOIM Coordinate:** `[M, B]` (Method $\times$ Boundaries)
- **Severity:** `Error`
- **Description:** Executing Room queries, disk access, or network calls on `Dispatchers.Main` freezes the Android UI thread and causes ANR (Application Not Responding) dialogs.
- **Remediation:** Switch context to `Dispatchers.IO` using `withContext(Dispatchers.IO)`.
- **Cypher Query:**
```cypher
MATCH (fn:Function)-[:CALLS]->(ioOp:Function)
WHERE fn.coroutine_dispatcher = 'Dispatchers.Main'
  AND (ioOp.symbol CONTAINS 'Room' OR ioOp.symbol CONTAINS 'OkHttpClient' OR ioOp.symbol CONTAINS 'FileInputStream')
RETURN 
  fn.file_path AS file,
  fn.start_line AS line,
  fn.name AS symbol,
  ioOp.name AS target,
  'Android ANR risk: database/network I/O executed on Dispatchers.Main UI thread' AS message
```

#### `SIG-KOTLIN-C-R-001`: ViewModel Activity Context Leak
- **FOIM Coordinate:** `[C, R]` (Class $\times$ Resources)
- **Severity:** `Error`
- **Description:** Storing references to `Activity`, `Fragment`, or UI `View` inside an Android `ViewModel` causes severe memory leaks across configuration changes.
- **Remediation:** Pass Application Context or use reactive state (`StateFlow`, `LiveData`) instead of holding View references.
- **Cypher Query:**
```cypher
MATCH (vm:Type)-[:HAS_MEMBER]->(m:Member)
WHERE vm.base_class = 'ViewModel'
  AND m.type_name IN ['Activity', 'Fragment', 'View', 'Context']
RETURN 
  vm.file_path AS file,
  m.start_line AS line,
  vm.name AS symbol,
  m.type_name AS target,
  'Android memory leak: ViewModel retains strong reference to UI Context/Activity' AS message
```

#### `SIG-KOTLIN-P-B-001`: Domain Module Framework Purity
- **FOIM Coordinate:** `[P, B]` (Module $\times$ Boundaries)
- **Severity:** `Error`
- **Description:** Clean Architecture `:domain` modules must be pure Kotlin and must not import Android framework packages (`android.*`, `androidx.*`).
- **Remediation:** Move Android-specific implementations to `:app` or `:data` modules; keep `:domain` strictly pure Kotlin.
- **Cypher Query:**
```cypher
MATCH (p:Project {role: 'Domain'})-[:CONTAINS]->(f:File)<-[:DECLARED_IN]-(sym:Type)-[:USES_TYPE]->(fw:Type)
WHERE fw.symbol STARTS WITH 'android.' OR fw.symbol STARTS WITH 'androidx.'
RETURN 
  sym.file_path AS file,
  sym.start_line AS line,
  sym.name AS symbol,
  fw.name AS target,
  'Domain impurity: pure Kotlin domain module imports Android framework dependencies' AS message
```

---

### 3.7 SQL & Schema Gates (`sql`)

#### `SIG-SQL-C-R-001`: Unindexed Foreign Key Full-Scan Hazard
- **FOIM Coordinate:** `[C, R]` (Class $\times$ Resources)
- **Severity:** `Warning`
- **Description:** Foreign key constraint columns lacking a corresponding index force full table scans on parent key updates, cascading deletes, and joins.
- **Remediation:** Add an explicit index `CREATE INDEX idx_<table>_<fk> ON <table>(<fk>);`.
- **Cypher Query:**
```cypher
MATCH (t:Table)-[:HAS_FOREIGN_KEY]->(col:Column)
WHERE NOT EXISTS((t)-[:HAS_INDEX]->(:Index {column: col.name}))
RETURN 
  t.file_path AS file,
  coalesce(t.start_line, 1) AS line,
  t.name + '.' + col.name AS symbol,
  'Missing index on FK' AS target,
  'Unindexed foreign key: joins and cascading deletes will trigger full table scans' AS message
```

#### `SIG-SQL-P-B-001`: Destructive Migration Safety Guard
- **FOIM Coordinate:** `[P, B]` (Module $\times$ Boundaries)
- **Severity:** `Error`
- **Description:** Production database migrations must not perform destructive DDL operations (`DROP TABLE`, `ALTER TABLE DROP COLUMN`) in active migration paths.
- **Remediation:** Use expand-and-contract: deprecate the column/table in version $N$, stop writing in version $N+1$, and clean up in a dedicated archival script.
- **Cypher Query:**
```cypher
MATCH (mig:File)-[:CONTAINS]->(stmt:Query)
WHERE (stmt.query_type = 'DDL')
  AND (stmt.sql CONTAINS 'DROP TABLE' OR stmt.sql CONTAINS 'DROP COLUMN')
  AND mig.path CONTAINS 'migrations/'
RETURN 
  mig.path AS file,
  coalesce(stmt.line, 1) AS line,
  mig.name AS symbol,
  stmt.sql AS target,
  'Destructive DDL migration: DROP statement threatens irreversible data loss during rollout' AS message
```

#### `SIG-SQL-C-C-001`: Explicit Primary Key Enforcement
- **FOIM Coordinate:** `[C, C]` (Class $\times$ External Contract)
- **Severity:** `Error`
- **Description:** Every persistent relational table must declare an explicit Primary Key to ensure clustered index ordering and replication integrity.
- **Remediation:** Declare a primary key constraint on an identity, UUID, or natural key column.
- **Cypher Query:**
```cypher
MATCH (t:Table)
WHERE NOT EXISTS((t)-[:HAS_PRIMARY_KEY]->(:Column))
  AND NOT t.name STARTS WITH '__'
RETURN 
  t.file_path AS file,
  coalesce(t.start_line, 1) AS line,
  t.name AS symbol,
  'Missing PK' AS target,
  'Relational integrity violation: table lacks an explicit Primary Key' AS message
```

---

### 3.8 ColdFusion Platform Gates (`coldfusion`)

#### `SIG-CF-M-I-001`: CFQuery Dynamic SQL Injection Shield
- **FOIM Coordinate:** `[M, I]` (Method $\times$ Implementation Rules)
- **Severity:** `Error`
- **Description:** Dynamic variables concatenated into `<cfquery>` blocks without `<cfqueryparam>` expose the database to SQL injection attacks.
- **Remediation:** Wrap all dynamic variables in `<cfqueryparam value="#var#" cfsqltype="...">`.
- **Cypher Query:**
```cypher
MATCH (q:Query)-[:CONTAINS_PARAM]->(p:Member)
WHERE q.callee = 'cfquery' AND p.is_parameterized = false
RETURN 
  q.file_path AS file,
  q.line AS line,
  q.name AS symbol,
  p.name AS target,
  'SQL injection vulnerability: ColdFusion query interpolates variable without cfqueryparam' AS message
```

#### `SIG-CF-M-I-002`: Unsynchronized Shared Scope Mutation
- **FOIM Coordinate:** `[M, I]` (Method $\times$ Implementation Rules)
- **Severity:** `Warning`
- **Description:** Writing directly to shared `application` or `session` scopes without enclosing `<cflock>` blocks causes race conditions under concurrent requests.
- **Remediation:** Enclose shared state mutations inside `<cflock scope="Application" type="Exclusive" timeout="10">`.
- **Cypher Query:**
```cypher
MATCH (fn:Function)-[:WRITES_SCOPE]->(s:Member)
WHERE s.name IN ['application', 'session']
  AND NOT EXISTS((fn)-[:ENCLOSED_IN]->(:Block {type: 'cflock'}))
RETURN 
  fn.file_path AS file,
  fn.start_line AS line,
  fn.name AS symbol,
  s.name AS target,
  'Concurrency race hazard: shared scope written without surrounding cflock protection' AS message
```

---

## 4. Summary Matrix of Invariant Gates

| Gate ID | Scope | Dimension | Category | Platform | Severity | Primary Objective |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **`SIG-P-B-001`** | Module | Boundaries | Architectural | Agnostic | `Error` | Pipeline Layer Strict Hierarchy (Unidirectional flow) |
| **`SIG-P-B-003`** | Module | Boundaries | Architectural | Agnostic | `Error` | Clean Architecture Transport Purity (No DB in Endpoints) |
| **`SIG-P-B-004`** | Module | Boundaries | Architectural | Agnostic | `Error` | Domain Core Framework Independence |
| **`SIG-S-B-002`** | Service | Boundaries | Architectural | Agnostic | `Error` | Cross-Service Database Isolation |
| **`SIG-S-B-003`** | Service | Boundaries | Architectural | Agnostic | `Warning` | Outbound Egress Gateway Chokepoint |
| **`SIG-P-I-001`** | Module | Rules | Architectural | Agnostic | `Error` | Circular Package Dependency Shield |
| **`SIG-A-C-001`** | System | Contract | Architectural | Agnostic | `Warning` | Dead Public Endpoint Detection |
| **`SIG-M-I-001`** | Method | Rules | Architectural | Agnostic | `Error` | Disallow Hardcoded Call Arguments |
| **`SIG-C-I-001`** | Class | Rules | Architectural | Agnostic | `Error` | In-Memory AST Visitor IO Purity |
| **`SIG-A-I-001`** | System | Rules | Architectural | Agnostic | `Error` | Dual-Write Messaging Transaction Invariance |
| **`SIG-DOTNET-M-I-001`** | Method | Rules | Platform | `dotnet` | `Error` | Sync-Over-Async Deadlock Prevention (`.Result`/`.Wait()`) |
| **`SIG-DOTNET-S-B-001`** | Service | Boundaries | Platform | `dotnet` | `Error` | Captive Dependency DI Lifetime Violation |
| **`SIG-DOTNET-S-B-002`** | Service | Boundaries | Platform | `dotnet` | `Error` | Storage Driver Concrete Type Abstraction |
| **`SIG-DOTNET-C-R-001`** | Class | Resources | Platform | `dotnet` | `Warning` | Unbounded EF Core Query Streaming |
| **`SIG-TS-M-C-001`** | Method | Contract | Platform | `typescript` | `Error` | Any Type Escape Shield at Module Boundaries |
| **`SIG-TS-P-B-001`** | Module | Boundaries | Platform | `typescript` | `Error` | Next.js Server-Client Boundary Isolation |
| **`SIG-TS-M-R-001`** | Method | Resources | Platform | `typescript` | `Error` | Node.js Event Loop Synchronous I/O Blocking |
| **`SIG-GO-M-I-001`** | Method | Rules | Platform | `go` | `Error` | Unbounded Goroutine Lifecycle Protection |
| **`SIG-GO-P-B-001`** | Module | Boundaries | Platform | `go` | `Error` | Go Internal Package Encapsulation Guard |
| **`SIG-GO-M-R-001`** | Method | Resources | Platform | `go` | `Error` | Unclosed SQL Rows Resource Hygiene |
| **`SIG-PY-M-I-001`** | Method | Rules | Platform | `python` | `Error` | Global Module State Mutation Guard |
| **`SIG-PY-M-R-001`** | Method | Resources | Platform | `python` | `Error` | Async Route Handler Blocking Call Freeze Shield |
| **`SIG-PY-M-C-001`** | Method | Contract | Platform | `python` | `Warning` | Untyped Dict Return Elimination |
| **`SIG-JAVA-M-I-001`** | Method | Rules | Platform | `java` | `Error` | Spring AOP Self-Invocation Bypass (`@Transactional`) |
| **`SIG-JAVA-C-R-001`** | Class | Resources | Platform | `java` | `Error` | JPA Eager Fetching N+1 Collection Explosion |
| **`SIG-JAVA-P-B-001`** | Module | Boundaries | Platform | `java` | `Error` | Spring Web Controller Direct Repository Injection |
| **`SIG-KOTLIN-M-B-001`** | Method | Boundaries | Platform | `kotlin` | `Error` | Android Main Thread Disk/Network I/O Ban |
| **`SIG-KOTLIN-C-R-001`** | Class | Resources | Platform | `kotlin` | `Error` | Android ViewModel Activity Context Memory Leak |
| **`SIG-KOTLIN-P-B-001`** | Module | Boundaries | Platform | `kotlin` | `Error` | Clean Architecture Domain Module Framework Purity |
| **`SIG-SQL-C-R-001`** | Class | Resources | Platform | `sql` | `Warning` | Unindexed Foreign Key Full-Scan Prevention |
| **`SIG-SQL-P-B-001`** | Module | Boundaries | Platform | `sql` | `Error` | Destructive Migration DDL Safety Guard |
| **`SIG-SQL-C-C-001`** | Class | Contract | Platform | `sql` | `Error` | Explicit Primary Key Enforcement |
| **`SIG-CF-M-I-001`** | Method | Rules | Platform | `coldfusion` | `Error` | Dynamic CFQuery SQL Injection Protection |
| **`SIG-CF-M-I-002`** | Method | Rules | Platform | `coldfusion` | `Warning` | Unsynchronized Application Scope Mutation |

---

## 5. Implementation & Rollout Roadmap for "Gates on Growth"

1. **Step 1: Universal Taxonomy Schema Support (Delivered in v1.31.0)**:
   - Added `category` (`Architectural` vs `Platform`) and `platform` (`agnostic`, `dotnet`, `typescript`, `go`, etc.) to `SemanticGate.cs` and YAML parser.
   - Organized starter gates into `.codeexplorer/gates/architectural/` and `.codeexplorer/gates/platform/<platform>/`.

2. **Step 2: Structural Graph Topology Extension (In Progress)**:
   - Introduce `(p:Project)-[:CONTAINS]->(f:File)` and `(f:File)-[:BELONGS_TO]->(p:Project)`.
   - Propagate `is_test: bool` and `project_kind: string` to files and symbols to eliminate heuristic path/symbol `CONTAINS 'Test'` checks.
   - Introduce pipeline stage and layer order relations (`[:PRECEDES]` and `layer_order`) for clean $O(1)$ hierarchy queries.

3. **Step 3: Phased Gate Activation**:
   - Workspaces adopt architectural gates first (`SIG-P-B-001`, `SIG-P-B-003`, `SIG-M-I-001`).
   - Platform gates are enabled per project based on detected `project_type` or language configuration.
   - Monotonic baselines (`.codeexplorer/baselines/*.json`) prevent regression while allowing existing technical debt to be burned down iteratively.
