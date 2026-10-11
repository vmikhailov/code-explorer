# CodeExplorer Documentation Portal 📚

Welcome to the **CodeExplorer (`ce`)** documentation suite. This portal organizes architectural specifications, system designs, benchmark registries, roadmap tracking, and technical references.

---

## 🗺️ Documentation Map

```
site/                                         # Interactive Web Documentation Portal (code-explorer.mikhailov.cc)
├── index.html                                # Web Documentation & Interactive Landing Page
├── logo.svg, favicon.svg                     # Vector Branding Assets
├── all_services.png                          # Macro Architecture & Concentric Orbits
├── selected_services.png                     # Sub-Graph Neighborhood & Orbit Legend Reordering
├── project_flow.png                          # Project Flow, Service Cards & C1 System Context
├── deploy.sh                                 # One-Click Production VPS Deployment Script
└── README.md                                 # Site Development & Operations Guide

docs/                                         # Technical Architecture & Reference Specifications
├── README.md                                 # This Documentation Portal & Index
├── ontology.md                               # Live Auto-Generated Ontology Reference (32 Nodes)
│
├── architecture/                             # Core Technical Architecture & Foundations
│   ├── ontology-model.md                     # 5-Layer Decoupled Graph Ontology Specification
│   ├── ingestion-pipeline.md                 # 5-Stage Ingestion Pipeline & SQLite Persistence
│   ├── query-engine.md                       # SQLite Graph Database & Cypher-to-SQL Compiler
│   ├── fractal-orthogonal-invariant-matrix.md# Fractal Orthogonal Invariant Matrix (FOIM 5x4) Specification
│   └── vscode-extension.md                   # Interactive VS Code & Antigravity Extension Architecture
│
├── proposals/                                # Active Technical Proposals & Design Specs
│   └── semantic-invariant-gates-proposal.md  # Comprehensive Semantic Invariant Gates Catalog & Proposal
│
├── roadmap/                                  # Engineering Milestones & Task Tracking
│   ├── README.md                             # Unified Engineering Roadmap, Specs & Backlog
│   ├── todo.md                               # Active Engineering Task Checklist & Epics
│   └── semantic-invariant-gates-plan.md      # Semantic Invariant Gates (SIG) Guardrail Specification
│
├── benchmarks/                               # Polyglot Repositories for Autonomous Testing
│   ├── README.md                             # Benchmark Methodology & Evaluation Guide
│   ├── flagship-projects.md                  # Top 50 Flagship Projects (Monorepos, Full Stacks)
│   └── compact-projects.md                   # Top 50 Compact Idiomatic Libraries (~1k–25k LOC)
│
└── archive/                                  # Completed Historical Plans & Task Specs
    ├── cross-service-detection.md            # Cross-Service & Messaging Channel Detection
    ├── typed-cypher-queries-plan.md          # Strongly-Typed Cypher Queries & LINQ Provider (delivered v1.19.0)
    ├── domain-cleanup-and-generic-architecture-plan.md # Generic Architecture & De-Hack Plan (v1.27.0)
    ├── semantic-resolution-and-hacks-cleanup-plan.md   # Universal Value Resolution & UAST (v1.11.10)
    ├── library-parser-extraction.md          # Extraction of 5 Library Parsers
    └── self-descriptive-ontology-plan.md     # OntologyGen Build Integration Plan
```

---

## 📑 Document Catalog & Status

| Document | Category | Status | Target Audience | Summary |
| :--- | :--- | :--- | :--- | :--- |
| [**`ontology.md`**](ontology.md) | Reference | **Live (Generated)** | All / AI Agents | Auto-generated dictionary of all 32 node types, relationships, properties, and ID schemes. |
| [**`architecture/ontology-model.md`**](architecture/ontology-model.md) | Architecture | **Active Standard** | Engine Developers / Architects | 5-layer decoupled graph architecture (Physical, Project, Syntactic, Semantic, Late-bound). |
| [**`architecture/ingestion-pipeline.md`**](architecture/ingestion-pipeline.md) | Architecture | **Active Standard** | Core Developers | 5-stage sequential pipeline (`WorkspaceIndexer`), Tree-sitter visitors, and async channel persistence. |
| [**`architecture/query-engine.md`**](architecture/query-engine.md) | Architecture | **Active Standard** | Graph Developers | SQLite schema (`nodes`, `edges`), B-Tree indexing, Cypher-to-SQL compiler, and timeout bounds. |
| [**`architecture/fractal-orthogonal-invariant-matrix.md`**](architecture/fractal-orthogonal-invariant-matrix.md) | Architecture | **Active Standard** | Architects / QA / AI Agents | Formal FOIM 5x4 matrix taxonomy governing semantic invariant rules across all scopes and dimensions. |
| [**`architecture/vscode-extension.md`**](architecture/vscode-extension.md) | Architecture | **Active Standard** | Extension Developers | Architecture for interactive Cytoscape.js webview extension, host messaging RPC, and native binaries. |
| [**`proposals/semantic-invariant-gates-proposal.md`**](proposals/semantic-invariant-gates-proposal.md) | Proposals | **Active Proposal** | Architects / QA / AI Agents | Comprehensive theoretical catalog of 34 Architectural and Polyglot Platform gates ("Gates on Growth"). |
| [**`roadmap/README.md`**](roadmap/README.md) | Roadmap | **Active Master** | All Contributors / AI Agents | Unified roadmap: delivered milestones, active MCP & CLI semantic architecture spec, and backlog. |
| [**`roadmap/todo.md`**](roadmap/todo.md) | Roadmap | **Active Checklist** | Contributors / AI Agents | Granular engineering task list, active epics, and release checklists. |
| [**`roadmap/semantic-invariant-gates-plan.md`**](roadmap/semantic-invariant-gates-plan.md) | Roadmap | **Active Milestone** | Architects / QA | Declarative Cypher-based architectural guardrails and monotonic debt ratchets. |
| [**`benchmarks/README.md`**](benchmarks/README.md) | Benchmarks | **Active Standard** | AI Agents / Testers | Overview of the 5-language benchmark suites and autonomous testing workflow. |
| [**`benchmarks/flagship-projects.md`**](benchmarks/flagship-projects.md) | Benchmarks | **Active Reference** | AI Agents / Testers | Top 50 large-scale, high-star open source projects for stress-testing scale and memory. |
| [**`benchmarks/compact-projects.md`**](benchmarks/compact-projects.md) | Benchmarks | **Active Reference** | AI Agents / Testers | Top 50 compact, idiomatic libraries (~1k–25k LOC) for sub-second parser iteration loops. |
| [**`archive/cross-service-detection.md`**](archive/cross-service-detection.md) | Archive | **Completed Historical** | Historical Reference | Suffix matching, GCP Pub/Sub, RabbitMQ, and WebSocket event tracing (delivered v1.12.0). |
| [**`archive/typed-cypher-queries-plan.md`**](archive/typed-cypher-queries-plan.md) | Archive | **Completed Historical** | Historical Reference | Strongly-typed Cypher query execution and LINQ provider (`GraphContext`, delivered v1.19.0). |
| [**`archive/domain-cleanup-and-generic-architecture-plan.md`**](archive/domain-cleanup-and-generic-architecture-plan.md) | Archive | **Completed Historical** | Historical Reference | Plan for eliminating project-specific keywords and making domain clustering generic (v1.27.0). |
| [**`archive/semantic-resolution-and-hacks-cleanup-plan.md`**](archive/semantic-resolution-and-hacks-cleanup-plan.md) | Archive | **Completed Historical** | Historical Reference | Plan for universal AST value resolution, constant propagation, and ConfigStore (v1.11.10). |
| [**`archive/library-parser-extraction.md`**](archive/library-parser-extraction.md) | Archive | **Completed Historical** | Historical Reference | Step-by-step extraction plan for NestJS, Express, Fetch, AspNetCore, and HttpClient parsers. |
| [**`archive/self-descriptive-ontology-plan.md`**](archive/self-descriptive-ontology-plan.md) | Archive | **Completed Historical** | Historical Reference | Initial design plan for the Roslyn-based `OntologyGen` build tool. |

---

## 🧭 Where to Start

- **New to CodeExplorer's internals?** Start with [5-Layer Graph Ontology](architecture/ontology-model.md) and the [Ingestion Pipeline](architecture/ingestion-pipeline.md).
- **Writing or debugging Cypher queries?** Read the [Query Engine Guide](architecture/query-engine.md) and check available nodes in the [Live Ontology Reference](ontology.md).
- **Contributing a new language or framework parser?** Consult [Ingestion Pipeline: Stage 3 & 4](architecture/ingestion-pipeline.md).
- **Tracking progress or picking up tasks?** Review the [Engineering Roadmap & Backlog](roadmap/README.md) and [Active Todo Checklist](roadmap/todo.md).
- **Testing or benchmarking parser coverage?** Check out the [Benchmark Evaluation Suites](benchmarks/README.md).

---

## ✍️ Documentation Guidelines

To keep documentation clean, reliable, and uniform across the global codebase:

1. **Strict English**: All documentation, comments, and commit messages must be written in English.
2. **Never Edit `ontology.md` Manually**: It is regenerated during the build by `OntologyGen` from code attributes.
3. **Keep Architecture Aligned with Code**: When modifying core classes (e.g. `WorkspaceIndexer`, `SqliteGraphClient`), ensure corresponding documents in `architecture/` are kept in sync.
4. **Archive Completed Plans**: Once an architectural task or migration is merged, move its design document into `archive/` with an archival status header.
