---
name: semantic-invariant-gates
description: Use this skill when authoring, reviewing, testing, or updating Semantic Invariant Gates (SIG), defining architectural rules in OpenCypher, configuring ratchet baselines, or inspecting technical debt violations in CodeExplorer.
---

# Semantic Invariant Gates (SIG) Authoring Guide

This skill guides developers and AI agents in authoring, verifying, and maintaining **Semantic Invariant Gates (SIG)** in CodeExplorer.

---

## When to Use This Skill

Use this skill when:
- Defining new architectural invariants (e.g. boundary rules, layer purity, interface abstractions).
- Writing and testing OpenCypher queries against the codebase property graph.
- Creating gate manifests (`.codeexplorer/gates/*.yaml`) or adding built-in gates to `BuiltInGateCatalog.cs`.
- Setting up baseline snapshots for legacy technical debt (`ce gate ratchet --update`).
- Inspecting, filtering, or grouping gate violations (`ce gate violations`).
- Reviewing code changes against architectural standards before merging.

---

## Core Foundations

### 1. Fractal Orthogonal Invariant Matrix (FOIM)

Every invariant lives at a coordinate `[Scope, Dimension]` in the FOIM space:

| Code | Scope | Granularity | Example Entities |
| :--- | :--- | :--- | :--- |
| **A** | **System** | Entire distributed topology | Cross-service flows, external ingress/egress, cloud resources |
| **S** | **Service** | Executable / deployment unit | `Service`, `App`, `Worker`, `CliTool`, message topics, DBs |
| **P** | **Module** | Compilation unit / Project | `Project`, `Library`, package dependencies, layers |
| **C** | **Class** | Type / Interface / Struct | `Type`, inheritance trees, class dependencies, visitor purity |
| **M** | **Method** | Function / Procedure / Endpoint | `Function`, `Endpoint`, `CallLiteral`, parameter rules |

| Code | Dimension | Architectural Focus | Question Answered |
| :--- | :--- | :--- | :--- |
| **B** | **Boundaries** | Encapsulation & isolation | Who may talk to whom? (No direct DB calls from UI) |
| **T** | **Topology** | Graph shape & connectivity | Are there cycles, orphan nodes, or dead endpoints? |
| **C** | **Contract** | Typing & interfaces | Are contracts explicit, valid, and bijectional? |
| **L** | **Lifecycle** | State, resource, & async | Are resources disposed? Are transactions scoped? |
| **I** | **Rules** | Implementation constraints | Are constants hardcoded? Are visitors IO-pure? |

**Identifier Convention**:
`SIG-<ScopeCode>-<DimensionCode>-<Index3Digits>`
Examples:
- `SIG-P-B-001`: Module Scope $\times$ Boundaries Dimension (Layer hierarchy)
- `SIG-M-I-001`: Method Scope $\times$ Rules Dimension (Disallow hardcoded call arguments)
- `SIG-C-I-001`: Class Scope $\times$ Rules Dimension (In-Memory AST Visitor IO Purity)

---

### 2. The Zero-Match Contract

A Semantic Invariant Gate operates on a strict **Zero-Match Contract**:
- **0 rows returned** $\implies$ **PASS** (Invariant holds across the codebase).
- **$\ge 1$ rows returned** $\implies$ **VIOLATION** (Each row returned is a violation).

#### Standard Cypher Output Columns:
Your Cypher `RETURN` clause **must** yield these columns:
1. `file`: Path to the violating source file (string).
2. `line`: 1-indexed line / row number (integer, or 0 if symbol-level).
3. `column`: 1-indexed column number (integer, or 0 if symbol-level).
4. `symbol`: Offending caller or symbol name (e.g. class, method, or project name).
5. `target`: Target being violated (e.g. prohibited dependency, callee, or package).
6. `message`: Human-readable explanation of why this violation occurred.

> [!NOTE]
> Locations are displayed in standard 2D format (`file:line:column`, e.g. `src/MyFile.cs:42:15`), enabling direct clickable navigation in IDEs and terminals.

```cypher
RETURN 
  caller.file_path AS file,
  caller.line AS line,
  caller.column AS column,
  caller.name AS symbol,
  target.name AS target,
  'Prohibited direct dependency from ' + caller.name + ' to ' + target.name AS message
```

---

### 3. Line-Shift Immune Fingerprinting

CodeExplorer calculates violation fingerprints without depending on volatile line numbers:
$$\text{Fingerprint} = \text{SHA256}(\text{GateId} + \text{FilePath} + \text{Symbol} + \text{Target})$$

* **Why**: Adding comments, reformatting, or inserting code above a violation shifts line numbers but must **never** invalidate the baseline snapshot.
* **Best Practice**: Ensure `target` contains differentiating detail when a single method can have multiple violations (e.g. for `SIG-M-I-001`, `target` is `callee(argValue)`).

---

## Step-by-Step Gate Creation Workflow

```
1. Formulate Rule ──> 2. Test in Cypher ──> 3. Create Manifest ──> 4. Run Check ──> 5. Ratchet Baseline
   (FOIM Coordinates)   (ce query "...")      (.codeexplorer/gates)   (ce gate check)   (ce gate ratchet)
```

### Step 1: Formulate the Rule
1. Clearly state what is forbidden.
2. Select the FOIM `Scope` and `Dimension`.
3. Pick the next available ID: `SIG-<Scope>-<Dimension>-NNN`.

### Step 2: Prototype and Test the Cypher Query
Run your query interactively using `ce query` to test against the current workspace:
```bash
ce query "MATCH (l:Library)-[:DEPENDS_ON]->(p:Package) WHERE p.name = 'Microsoft.Data.Sqlite' RETURN l.path AS file, 0 AS line, l.name AS symbol, p.name AS target, 'Direct SQLite reference' AS message"
```
Verify that:
- Clean code returns **0 rows**.
- Real violations are caught.
- Test projects/files are appropriately excluded if not subject to the rule:
  ```cypher
  WHERE NOT (file CONTAINS 'test' OR file CONTAINS 'Test')
  ```

### Step 3: Create Gate Manifest YAML
Create `.codeexplorer/gates/<gate-id-in-kebab-case>.yaml`:

```yaml
id: SIG-P-B-004
scope: Module
dimension: Boundaries
name: "Disallow Direct Database Packages in Domain Libraries"
severity: Error # Error | Warning
description: "Domain and Core libraries must remain storage-agnostic. Direct references to database drivers violate Clean Architecture."
remediation: "Move database drivers and EF Core dependencies to Infrastructure/Data layer projects."
query: |
  MATCH (l:Library)-[:DEPENDS_ON]->(pkg:Package)
  WHERE l.name CONTAINS 'Core' OR l.name CONTAINS 'Domain'
    AND pkg.name IN ['Microsoft.Data.Sqlite', 'Npgsql', 'System.Data.SqlClient', 'MySqlConnector']
  RETURN 
    l.path AS file,
    0 AS line,
    l.name AS symbol,
    pkg.name AS target,
    'Core library ' + l.name + ' must not depend on database driver ' + pkg.name AS message
ratchet:
  enabled: true
  baseline: baselines/SIG-P-B-004.json
```

### Step 4: Verify Gate via CLI
Run the gate evaluator:
```bash
# Check single gate
ce gate check -g SIG-P-B-004

# Run all gates
ce gate check
```

### Step 5: Freeze Existing Debt into Baseline
If the codebase already contains legacy violations that cannot be fixed immediately:
```bash
# Update baseline snapshot for this specific gate
ce gate ratchet --update -g SIG-P-B-004
```
This generates `.codeexplorer/gates/baselines/SIG-P-B-004.json`. From this point forward:
- Legacy violations are **tolerated** (`[TOLERATED]`).
- Any **new** violation introduced in a PR immediately fails the gate (`[FAIL]`).
- When debt is fixed, `ce gate ratchet --update` lowers the ceiling (monotonic ratchet).

### Step 6: Inspect and Group Violations
```bash
# Aggregated summary by file
ce gate violations -g SIG-P-B-004 --group-by file

# Filter by specific file
ce gate violations -g SIG-P-B-004 --file Core/Analysis

# Export to JSON
ce gate violations -g SIG-P-B-004 -f json
```

---

## Ontology Reference Cheatsheet

### 1. High-Level Architecture Nodes
| Label | Description | Key Properties |
| :--- | :--- | :--- |
| `Service` | Backend microservice or API | `name`, `path`, `project_type` |
| `App` | Frontend or client application | `name`, `path`, `project_type` |
| `Worker` | Background queue consumer / daemon | `name`, `path` |
| `Library` | Shared package or class library | `name`, `path` |
| `CliTool` | Command-line tool executable | `name`, `path` |
| `Database` | Logical database or storage instance | `name`, `db_type` |
| `Topic` | Message queue or pub/sub topic | `name`, `broker_type` |

### 2. Code-Level Nodes
| Label | Description | Key Properties |
| :--- | :--- | :--- |
| `Type` | Class, interface, struct, enum | `name`, `symbol`, `file_path`, `line` |
| `Function` | Method, constructor, function | `name`, `symbol`, `file_path`, `line` |
| `CallLiteral` | Hardcoded literal argument in method call | `value`, `callee`, `arg_index`, `file_path`, `line` |
| `Endpoint` | HTTP route / REST handler | `path`, `method`, `route` |
| `Query` | SQL query extracted from AST | `raw_sql`, `operation_type` |
| `Package` | External dependency (NuGet, npm) | `name`, `version` |

### 3. Key Relationships
| Relationship | From $\to$ To | Meaning |
| :--- | :--- | :--- |
| `DEPENDS_ON` | `Project/Library` $\to$ `Package` | Project references external package |
| `SERVICE_CALL` | `Service` $\to$ `Service/Endpoint` | Network invocation (REST, gRPC) |
| `PUBLISHES_TO` | `Service` $\to$ `Topic` | Publishes events to message queue |
| `SUBSCRIBES_TO` | `Worker` $\to$ `Topic` | Consumes events from message queue |
| `USES_DB` | `Service` $\to$ `Database` | Service accesses database |
| `CALLS` | `Function` $\to$ `Function` | AST call graph invocation |
| `PASSES_LITERAL` | `Function/Type` $\to$ `CallLiteral` | Function passes literal string argument |
| `CONTAINS` | Parent $\to$ Child | Structural containment (Project $\to$ Type $\to$ Method) |
| `IMPLEMENTS` | `Type` $\to$ `Type` | Interface implementation |

---

## Catalog of Real-World Gate Patterns

### Pattern A: Clean Architecture Layer Purity (Boundaries)
Prohibit Domain/Application layer from referencing UI or Infrastructure packages:
```cypher
MATCH (p:Project)-[:DEPENDS_ON]->(pkg:Package)
WHERE p.path CONTAINS 'src/Domain'
  AND (pkg.name STARTS WITH 'Microsoft.AspNetCore' OR pkg.name STARTS WITH 'EntityFramework')
RETURN 
  p.path AS file,
  0 AS line,
  p.name AS symbol,
  pkg.name AS target,
  'Domain layer project ' + p.name + ' cannot reference infrastructure package ' + pkg.name AS message
```

### Pattern B: Circular Package Dependencies (Topology)
Detect circular references between workspace projects:
```cypher
MATCH (a:Project)-[:DEPENDS_ON]->(b:Project)-[:DEPENDS_ON]->(a)
WHERE a.name < b.name
RETURN 
  a.path AS file,
  0 AS line,
  a.name AS symbol,
  b.name AS target,
  'Circular dependency detected between projects ' + a.name + ' and ' + b.name AS message
```

### Pattern C: In-Memory Visitor IO Purity (Implementation Rules)
Ensure AST visitors and in-memory processors do not invoke filesystem or network IO:
```cypher
MATCH (cls:Type)-[:CONTAINS]->(fn:Function)-[:CALLS]->(ioTarget:Function)
WHERE cls.name CONTAINS 'Visitor'
  AND (ioTarget.name STARTS WITH 'File.' OR ioTarget.name STARTS WITH 'Directory.' OR ioTarget.name STARTS WITH 'HttpClient.')
RETURN 
  cls.file_path AS file,
  fn.line AS line,
  cls.name + '.' + fn.name AS symbol,
  ioTarget.name AS target,
  'AST Visitor method ' + fn.name + ' performs prohibited IO call ' + ioTarget.name AS message
```

### Pattern D: Disallow Hardcoded Call Arguments (Implementation Rules)
Flag raw string literals passed into business methods:
```cypher
MATCH (caller)-[:PASSES_LITERAL]->(lit:CallLiteral)
WHERE NOT (coalesce(caller.symbol, '') CONTAINS 'Test' 
       OR coalesce(caller.file_path, '') CONTAINS 'Test' 
       OR coalesce(caller.file_path, '') CONTAINS 'test')
RETURN 
  lit.file_path AS file,
  lit.line AS line,
  coalesce(caller.name, 'unknown') AS symbol,
  lit.callee + '(' + lit.value + ')' AS target,
  'Hardcoded string literal "' + lit.value + '" passed to ' + lit.callee AS message
```

---

## Best Practices & Pitfalls

1. **Avoid Volatile Symbols in `target`**:
   - Don't put line numbers or timestamps into `target`.
   - Use stable identifiers like method names, package names, or typed constants so the SHA-256 fingerprint remains deterministic.
2. **Exclude Test Code**:
   - Always add `WHERE NOT (file CONTAINS 'test' OR file CONTAINS 'Test')` unless the gate specifically targets testing practices.
3. **Always Provide Remediation**:
   - Every gate should include actionable instructions in the `remediation` field explaining *how* the developer or AI agent should fix the issue.
4. **Use Baselines to Avoid Blocking Existing Work**:
   - When introducing a new gate to an established repository, *never* break the build for existing debt.
   - Always run `ce gate ratchet --update -g <id>` to record existing violations in the baseline.
