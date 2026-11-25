# Proposal: Pre-Indexed Library Registry & Server-Side Graph Branching

**Date:** 2026-11-25  
**Status:** Concept / Strategic Product & Architecture Proposal  
**Scope:** `CodeExplorer` (`ce`), `CodePassport`, `CodeExplorer.Cypher` (.NET Postgres Dialect), Team / Enterprise Server

---

## 1. Executive Summary

Two complementary server-backed capabilities extend CodeExplorer from a single-repository local CLI into a complete team and enterprise platform ($20–$50 per active committer/month) without placing artificial restrictions on the open-source local tool:

1. **Pre-Indexed Library Registry & Behavioral Search** built directly on CodePassport's existing 70,000+ package scanning pipeline (`npm`, `PyPI`, `NuGet`, GitHub).
2. **Git-Synchronized Server Graph Branching** backed by PostgreSQL (porting the PostgreSQL dialect already validated in `cypher-sql-go` into `CodeExplorer.Cypher` on .NET) for shared team caching, sub-second CI, cross-repo in-flight contracts, and pre-merge semantic conflict detection.

Combined with recently shipped code-health metrics (cyclomatic complexity, method/type sizing, empty catch blocks, anomalies), graph-based test reachability coverage (`ce coverage`), affected test selection (`ce test impact`), SHA-256 baseline ratchets, and SARIF output, the platform can directly replace legacy SonarQube / SonarCloud budgets while adding multi-service architecture governance and MCP context for AI coding agents.

---

## 2. Initiative A: Pre-Indexed Library Registry & Search

### 2.1 Existing Infrastructure Leverage (`CodePassport`)
CodePassport (`passport.code-passport.app`) already operates the hardest parts of a universal package indexer in production:
- Downloads and safely unpacks release archives (`npm`, `PyPI`, `NuGet`) and GitHub repositories in isolated worker sandboxes (`PackageScanScheduler`).
- Builds the 5-layer ontology graph (`PassportGenerator`), computes `CapabilityFlags`, `PackageArchetype`, taint/provenance slices, and safety verdicts across **70,000+ indexed libraries** in `SqlitePassportStore`.
- Today, only the security/capability `PackagePassport` is persisted; the parsed API and symbol surface is discarded with the temporary extraction directory.

### 2.2 Public API & Effect Surface Snapshot
During package scanning, persist a compact GZip-compressed **Public Surface Snapshot** (`~3–15 KB` per `(ecosystem, package, version)`, `< 1 GB` per 70k packages) alongside the security passport:
- Exported types, interfaces, base classes, traits, and public methods/functions with parameter and return signatures.
- Framework extension points (middleware hooks, DI registrations, handler interfaces).
- Per-export transitive capability and side-effect summary (network/HTTP calls, filesystem access, SQL/ORM operations, process spawning, environment variable reads, caching, transactions).

### 2.3 Product & Agent Workflows
1. **Behavioral Library Search (Web + MCP)**:
   Developers and AI agents search libraries not merely by README keywords or download counts, but by verified runtime behavior and safety (e.g., *"CSV / PDF / JWT library in TypeScript or Python with zero outbound network calls, zero `child_process` usage, and `Safe` verdict"*).
2. **Version-Exact Code API Reference for Coding Agents**:
   Unlike documentation scrapers (such as Context7) that fail when markdown docs lag behind code or omit edge cases, agents query the exact exported signatures and extension contracts of `pkg@version` parsed directly from the shipped artifact.
3. **Cross-Boundary `:Package` Hydration in `CodeExplorer`**:
   In a user repository, `:Package` nodes no longer terminate analysis as opaque black boxes:
   - `ce` and Semantic Invariant Gates can inspect transitive side effects crossing third-party (or internal corporate SDK) boundaries without locally indexing `node_modules` or decompiling binaries.
   - In Enterprise deployments, the exact same pipeline indexes **private internal NuGet / npm / PyPI SDKs** so teams have a live API + effect catalog of internal shared libraries.

---

## 3. Initiative B: Server-Side Shared Graph & Branching

### 3.1 Storage Split: Local SQLite vs Enterprise PostgreSQL
- **Local Single-Repo CLI (`ce`)**: Keeps zero-dependency `.codeexplorer/graph.db` on SQLite for instant single-developer and local MCP usage.
- **Team & Enterprise Server**: Runs a centralized multi-repo graph service backed by **PostgreSQL**:
  - Port the PostgreSQL SQL-generation dialect from `cypher-sql-go` into `CodeExplorer.Cypher` (.NET), keeping a unified C#/.NET codebase across client and server.
  - Leverage PostgreSQL-native strengths: standard SQL `CYCLE` detection on recursive CTEs (`*1..N` paths), `JSONB` + GIN indexes on node/edge properties, and `Npgsql` binary `COPY` for bulk ingestion.
  - Zero operational friction in enterprise procurement and self-hosted VPC deployments: standard managed PostgreSQL (RDS, Cloud SQL, Patroni) requires no exotic graph DB licensing or DBA retraining.

### 3.2 Git-Synchronized Branch Delta Model
Full feature branches typically modify `5–30` files out of `10,000–50,000` (`< 0.2%`). Instead of duplicating entire repository graphs per branch, store:
1. **Base Graph (`main` / `merge-base`)**: Immutable canonical nodes and edges for the default branch.
2. **Branch Delta Overlay (`branch_id`, `base_commit`, `head_commit`)**:
   - `branch_tombstones`: set of modified or deleted `file_path` entries whose base-branch nodes and outbound edges are masked during reads.
   - `branch_nodes` & `branch_edges`: newly parsed nodes and recomputed cross-file Layer 4/5 edges for only the touched files.

Queries scoped to an active branch evaluate `(base_graph MINUS tombstones) UNION branch_delta` via indexed Postgres views/CTEs (with optional in-memory adjacency caching for hot branches).

### 3.3 Core Team & Multi-Agent Capabilities
1. **Shared Team Cache & Zero-Cost Agent Worktrees**:
   Switching branches (`git switch`) or spawning 3–10 parallel background coding agents in separate `git worktree` directories or cloud sandboxes requires zero full re-indexing: each worktree attaches to the cached `main` graph plus a kilobyte-scale branch delta.
2. **Fast CI Execution & Structural PR Diffs**:
   CI pipelines upload or evaluate only changed files against the server graph, running `ce gate check`, `ce coverage --threshold`, and `ce test impact` in sub-second to few-second turnarounds. Reviewers get an architectural diff (new endpoints, new `WRITES_TO` table edges, layer crossings, affected test suites) instead of raw file diffs alone.
3. **Early Visibility into In-Flight Contracts (Cross-Repo Branch Stitching)**:
   When a feature spans multiple microservices or an internal SDK (`feat/refunds` across `payments-service`, `billing-sdk`, and `api-gateway`), matching branch overlays compose into a virtual cross-repo graph (falling back to `main` for untouched repos). Engineers and agents can query and validate new contracts *before* the first PR merges.
4. **Pre-Merge Semantic Conflict Detection**:
   Git only detects line-level text collisions in the same file. By intersecting the blast radii of active branch overlays against `main`, the server flags semantic collisions early (e.g., Branch A alters an endpoint contract, message payload, or method behavior in File X while Branch B adds a new caller to the old contract in File Y).

---

## 4. Commercial Packaging & SonarQube Consolidation

### 4.1 Natural Open Core vs Paid Server Demarcation
No artificial license-key gates inside the local single-repo CLI:
- **Free / Open Core**: Local single-repo `ce` CLI, VS Code extension, local MCP server, and public lookup across the 70k+ OSS library registry.
- **Team ($20–$30 / active committer / month)**: Shared PostgreSQL graph cache, active branch overlays for developers and parallel agents, fast delta CI checks, and structural PR diff comments.
- **Enterprise ($40–$50 / active committer / month)**: Cross-repo microservice & branch federation, private pre-indexed internal SDK registry + supply-chain policy gates (CodePassport integration), org-wide Sonar-replacement quality/architecture dashboards, SSO/SCIM, and self-hosted VPC deployment on customer PostgreSQL.

### 4.2 Direct SonarQube / SonarCloud Budget Replacement
- **No Per-LOC Tax on AI-Generated Code**: Sonar commercial tiers charge by Lines of Code (penalizing teams as coding agents write more code while providing zero context back to those agents). Seat-based active-committer pricing aligns cost with team size.
- **Context-Aware Graph Rules Over Isolated File Lint**: Combining cyclomatic complexity, catch-block hygiene, and static test reachability coverage (`ce coverage`) with `CALLS` fan-in, `:Transaction` scopes, and `:Table` write lineage in Cypher eliminates noisy false positives on trivial DTO/mapper code and focuses enforcement on real architectural risk.
- **Immediate ROI**: Savings on LLM input tokens (`ce context` + MCP) and CI runner minutes (`ce test impact` + cached branch deltas) offset the $20–$50/seat subscription on infrastructure savings alone.
