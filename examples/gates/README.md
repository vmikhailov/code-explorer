# Semantic Invariant Gates (SIG) Examples & Catalog

This directory contains production-ready Semantic Invariant Gate definitions organized across the 4 canonical FOIM dimensions:

- **B (Boundaries)**: Layer boundaries, database access purity, component isolation, and call acyclicity.
- **C (Contract)**: Environment configuration, host names, endpoint contracts, and caller provenance.
- **R (Resources)**: Cognitive complexity, method length ceilings, and resource limits.
- **I (Rules)**: Implementation constraints, forbidden APIs, raw SQL execution, and dependency DAG invariants.

---

## How to Activate a Gate in Your Workspace

1. Copy the desired `.yaml` file into your repository's `.codeexplorer/gates/` directory:
   ```bash
   mkdir -p .codeexplorer/gates
   cp examples/gates/rules/sig-m-i-003-no-raw-sql.yaml .codeexplorer/gates/
   ```

2. Run evaluation:
   ```bash
   ce gate check
   ```

3. If legacy violations exist that you want to tolerate and freeze, update the baseline:
   ```bash
   ce gate ratchet --update
   ```

4. Add CI verification:
   ```bash
   ce gate check --new
   ```
   This ensures existing debt is frozen while blocking any **new** violations on Pull Requests.

---

## Gate Catalog Directory Structure

### 1. Boundaries (`boundaries/`)
- [`sig-c-b-001-controller-database-purity.yaml`](./boundaries/sig-c-b-001-controller-database-purity.yaml): Controllers must not directly execute database queries or access ORM repositories.
- [`sig-a-b-002-database-ownership.yaml`](./boundaries/sig-a-b-002-database-ownership.yaml): A database table must not be queried or mutated directly by multiple distinct microservices.
- [`sig-s-b-002-no-circular-http.yaml`](./boundaries/sig-s-b-002-no-circular-http.yaml): Prohibits synchronous circular HTTP service calls (`A -> B -> A`).

### 2. Contract (`contract/`)
- [`sig-m-c-001-no-localhost.yaml`](./contract/sig-m-c-001-no-localhost.yaml): Forbids hardcoded `localhost` and `127.0.0.1` addresses in non-test code.
- [`sig-a-b-001-call-lineage-provenance.yaml`](./contract/sig-a-b-001-call-lineage-provenance.yaml): Every external service egress must be provably rooted in an atomic calling function.
- [`sig-a-c-001-dead-internal-egress.yaml`](./contract/sig-a-c-001-dead-internal-egress.yaml): Internal egress calls must resolve to known endpoints across the workspace.

### 3. Resources (`resources/`)
- [`sig-m-r-001-method-length.yaml`](./resources/sig-m-r-001-method-length.yaml): Functions and methods must not exceed 500 lines of code.

### 4. Rules (`rules/`)
- [`sig-m-i-001-no-hardcoded-literals.yaml`](./rules/sig-m-i-001-no-hardcoded-literals.yaml): Forbids hardcoded business literals while permitting classified roles (Routing, Config, Logging, DI, etc.).
- [`sig-m-i-003-no-raw-sql.yaml`](./rules/sig-m-i-003-no-raw-sql.yaml): Prohibits raw SQL query execution via `queryRunner.query(...)` or `dataSource.query(...)`.
- [`sig-m-i-004-no-process-exit.yaml`](./rules/sig-m-i-004-no-process-exit.yaml): Prohibits `process.exit()` in web services to prevent ungraceful container termination.
- [`sig-m-i-005-no-empty-catch.yaml`](./rules/sig-m-i-005-no-empty-catch.yaml): Prohibits empty catch blocks without an explanatory comment.
- [`sig-p-i-001-circular-dependency.yaml`](./rules/sig-p-i-001-circular-dependency.yaml): Inter-package and inter-project dependencies must form an acyclic directed graph (DAG).

### 5. Compiler / Tool Dogfooding (`code-explorer/`)
Internal gates used by CodeExplorer itself to enforce AST parser purity and pipeline stage order:
- [`sig-p-b-001-pipeline-layer-hierarchy.yaml`](./code-explorer/sig-p-b-001-pipeline-layer-hierarchy.yaml)
- [`sig-p-b-002-database-driver-isolation.yaml`](./code-explorer/sig-p-b-002-database-driver-isolation.yaml)
- [`sig-s-b-001-storage-driver-abstraction.yaml`](./code-explorer/sig-s-b-001-storage-driver-abstraction.yaml)
- [`sig-c-i-001-visitor-io-purity.yaml`](./code-explorer/sig-c-i-001-visitor-io-purity.yaml)
- [`sig-m-r-001-method-length.yaml`](./code-explorer/sig-m-r-001-method-length.yaml)
