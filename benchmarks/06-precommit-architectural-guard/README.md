# Case Study 06: Automated Pre-Commit Architectural Guard Audit (`CodeExplorer` vs `Codebase Memory`)

Exhaustive pre-commit / pull-request architectural quality guard benchmark on the open-source Kubernetes continuous delivery platform [`argoproj/argo-cd`](https://github.com/argoproj/argo-cd) (`18` projects, `4,216` files, `45,616` graph nodes, `134,005` relationships) comparing a guard agent equipped with **CodeExplorer Semantic Invariant Gates (`ce gate check` / `ce gate details`)** against an agent equipped with **Codebase Memory CLI (`codebase-memory-mcp v0.12.1`)** evaluating 6 core architectural invariant rules.

---

## 1. The Real-World Engineering Problem

In modern software development, engineering teams strive for continuous architectural governance ("Architecture as Code" / "Fitness Functions"). Teams want to enforce hard structural rules during pre-commit hooks and CI Pull-Request checks:
- No reverse layer inversions (`util/` calling higher-level business services).
- No monolithic "God Structs" exceeding complexity thresholds.
- Strict package dependency DAG compliance (zero cyclic imports).
- Strict storage layer encapsulation (raw secret manipulation restricted to data access layers).
- Explicit audit of unauthenticated authentication overrides.

### The Problem with Agentic LLM-Driven Governance
Tools like `Codebase Memory` (CBM) or generic coding agents have no native architectural gate engine. To audit a commit or PR, an agent must formulate complex Cypher queries, write external Python scripts, read files, and burn through iterative reasoning loops.
- **Latency**: 4 to 7 minutes per commit.
- **Cost**: ~$0.15 to $0.50 per commit.
- **Flakiness**: Stochastic LLM reasoning loops cannot reliably block CI pipelines.

### The CodeExplorer Solution (`ce gate check`)
CodeExplorer provides native **Semantic Invariant Gates (SIG)**:
- Pre-compiled OpenCypher structural invariant rules with threshold ratchets.
- Evaluated deterministically via `ce gate check` in **398 milliseconds**.
- Zero LLM tokens, $0.00 cost, and deterministic binary exit codes (`0` for PASS, `1` for FAIL) suitable for pre-commit git hooks and GitHub Actions.

---

## 2. The 6 Pre-Commit Architectural Invariant Rules

| Gate ID | Invariant Rule | Scope & Dimension | Pass / Fail Condition |
| :--- | :--- | :--- | :--- |
| **SIG-P-B-101** | **Reverse Layer Inversion Shield** | Module $\times$ Boundaries | `util/**` must never call functions in `server/**`, `controller/**`, or `reposerver/**` |
| **SIG-C-R-102** | **God Struct Complexity Limit** | Class $\times$ Resources | Production structs must declare $\le$ 30 methods |
| **SIG-P-I-103** | **Package Dependency DAG Integrity** | Module $\times$ Rules | Package import relationships must form an acyclic directed graph (zero cycles) |
| **SIG-P-B-104** | **Storage Abstraction Boundary** | Module $\times$ Boundaries | Raw secret backend (`repository_secrets.go`) must only be called from within `util/db` |
| **SIG-P-B-105** | **Notification Subsystem DB Decoupling** | Module $\times$ Boundaries | `util/notification` must not instantiate standalone `db.NewDB` (dependency injection required) |
| **SIG-M-B-106** | **Authentication Bypass Audit** | Method $\times$ Boundaries | All server endpoints implementing `AuthFuncOverride` must be explicitly audited |

---

## 3. Execution Telemetry Scorecard

Measured from the paired head-to-head run on `argoproj/argo-cd @ 6d689fa3` ([`results/metrics.json`](./results/metrics.json)):

### Agent-Assisted Comparison (LLM Mode: Gemini 3.8 Flash)
| Metric | `Codebase Memory` (`86aa7d48`) | `CodeExplorer` (`5e260b11`) | Delta |
| :--- | :---: | :---: | :---: |
| **Time to Answer (`send_message`)** | `264.0 s` (`4 min 24 sec`) | **`83.0 s` (`1 min 23 sec`)** | **`3.18×` faster** |
| **LLM Turns & Steps** | `62` turns / `124` steps | **`28` turns / `56` steps** | **`2.21×` fewer turns** |
| **Total Tool Calls** | `61` tool calls | **`27` tool calls** | **`2.26×` fewer calls** |
| **Tool Breakdown** | `37` run_command + `18` view_file + `5` write_file + `1` send_message | `14` run_command + `12` view_file + `1` send_message | Native gate inspection vs multi-step manual querying |
| **Raw Tool Output Ingested** | `105,431` chars (`~26,357` tokens) | `86,120` chars (`~21,530` tokens) | Clean structured gate summaries |
| **API Total Input Tokens** | `1,885,020` | **`812,410`** | **`2.32×` less tokens** |
| **API Cost (Gemini 3.8 Flash)** | `$0.141` | **`$0.061`** | **`2.31×` cheaper** |

---

### Direct CLI / CI-Pipeline Comparison (Deterministic Pre-Commit Hook Mode)
When integrated directly into a `pre-commit` hook or CI workflow without an LLM agent:

| Metric | `Codebase Memory` (Requires Agent) | `CodeExplorer` (`ce gate check`) | Delta |
| :--- | :---: | :---: | :---: |
| **Execution Time** | `264,000 ms` (`264 s`) | **`398 ms` (`0.398 s`)** | **`663×` faster** |
| **LLM Tokens Consumed** | `1,885,020 tokens` | **`0 tokens`** | **Infinite token savings** |
| **Cost per Commit / PR** | `$0.141` | **`$0.000`** | **Free vs ongoing API cost** |
| **Determinism & CI Exit Code** | Non-deterministic (stochastic prompt) | **Deterministic binary exit code (`0` / `1`)** | Enterprise CI/CD ready |

---

## 4. Source-Verified Findings Breakdown

Both evaluators converged on the core violations, with CodeExplorer providing exact line-and-column unrolled call-site matrices:

### Rule 1: Reverse Layer Leaks (SIG-P-B-101) &mdash; FAIL
- Both identified all cross-layer call boundaries where foundational utility code reaches into higher-level business services:
  - `util/webhook/webhook.go` &rarr; `reposerver/cache` and `server/cache`
  - `util/git/client.go` &rarr; `reposerver/cache.GetOrLockGitReferences`, `SetGitReferences`
  - `util/helm/client.go` &rarr; `reposerver/cache.GetHelmIndex`, `SetHelmIndex`
  - `util/session/sessionmanager.go` &rarr; `server/rbacpolicy.GetProjectRoleFromSubject`
  - `util/notification/argocd/service.go` &rarr; `reposerver/apiclient`
  - `util/manifeststream/stream.go` &rarr; `reposerver/apiclient`
  - `util/app/discovery/discovery.go` &rarr; `cmpserver/plugin`, `cmpserver/apiclient`

### Rule 2: God Structs & Complexity Limit (SIG-C-R-102) &mdash; FAIL
- Identical top-complexity structs exceeding the 30-method threshold:
  1. `SettingsManager` (`util/settings/settings.go`): **70–74 methods**
  2. `ApplicationController` (`controller/appcontroller.go`): **64–83 methods**
  3. `client` (`pkg/apiclient/apiclient.go`): **61–64 methods**
  4. `db` (`util/db/db.go`): **35–60 methods**
  5. `Server` (`server/application/application.go`): **59 methods**
  6. `nativeGitClient` (`util/git/client.go`): **50 methods**
  7. `store` (`gitops-engine/pkg/cache/cluster.go`): **44 methods**
  8. `ArgoCDSettings` (`util/settings/settings.go`): **38 methods**
  9. `Service` (`reposerver/repository/repository.go`): **33–35 methods**
  10. `Cache` (`reposerver/cache/cache.go`): **34 methods**
  11. `syncContext` (`gitops-engine/pkg/sync/sync_context.go`): **35 methods**

### Rule 3: Package Dependency DAG Integrity (SIG-P-I-103) &mdash; PASS
- Confirmed **0 circular package dependencies** in production Go code.

### Rule 4: Storage Abstraction Boundary (SIG-P-B-104) &mdash; PASS
- Confirmed **0 direct invocations** of `repository_secrets.go` outside `util/db`.

### Rule 5: Notification Subsystem DB Decoupling (SIG-P-B-105) &mdash; FAIL
- Both identified the exact 2 violation sites where `util/notification` constructs standalone database connections:
  - `util/notification/argocd/service.go:66`: `GetCommitMetadata` calling `db.NewDB`
  - `util/notification/argocd/service.go:89`: `GetAppDetails` calling `db.NewDB`

### Rule 6: Public Authentication Bypass Audit (SIG-M-B-106) &mdash; AUDIT / WARNING
- Both identified the exact 4 legitimate public entry points implementing `AuthFuncOverride`:
  1. `server/session/session.go:103`: `session.Server` (anonymous login / session creation)
  2. `server/settings/settings.go:202`: `settings.Server` (public login UI config)
  3. `server/version/version.go:76`: `version.Server` (anonymous CLI version query)
  4. `server/server.go:969`: `healthServer` (Kubernetes liveness/readiness probes)

---

## 5. Verbatim Artifacts

- **Task Prompt**: [`prompts/task-prompt.md`](./prompts/task-prompt.md)
- **System Prompts**: [`prompts/ce-agent.md`](./prompts/ce-agent.md) | [`prompts/cbm-agent.md`](./prompts/cbm-agent.md)
- **Verbatim Agent Outputs**:
  - [`results/ce-agent-output.md`](./results/ce-agent-output.md)
  - [`results/cbm-agent-output.md`](./results/cbm-agent-output.md)
- **Machine Telemetry**: [`results/metrics.json`](./results/metrics.json)
