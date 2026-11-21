# Enterprise Architecture & Performance Audit Query Pack

Drop any of these `.cypher` files into `<workspace>/.codeexplorer/queries/` or execute them directly via `ce query --file <path>` / MCP `execute_custom_read_cypher`.

| File | Audit Deliverable | What It Surfaces |
| :--- | :--- | :--- |
| `01-repository-scorecard.cypher` | Keep / Merge / Repackage / Sunset | Per-project size (files, LOC), role, inbound/outbound coupling, endpoints, and DB touches |
| `02-shared-database-matrix.cypher` | Architecture Assessment & Merge | Tables/Databases shared across multiple services or projects (distributed monolith detector) |
| `03-circular-project-dependencies.cypher` | Keep / Merge / Repackage | Bidirectional project dependencies and chatty service pairs |
| `04-n-plus-one-in-loops.cypher` | Slow Journeys (Performance) | Calls executed inside loops (`r.in_loop = true`) targeting DB queries, tables, or remote services |
| `05-deep-sync-chains.cypher` | Slow Journeys (Performance) | Endpoints/EntryPoints with deep synchronous execution paths to data/egress |
| `06-orphan-projects-and-sunset-candidates.cypher` | Sunset / Repackage | Projects with zero inbound consumers and zero exposed entry points |
