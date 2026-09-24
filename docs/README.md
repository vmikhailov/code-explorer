# CodeExplorer Documentation Portal 📚

Welcome to the **CodeExplorer (`ce`)** documentation suite. This portal organizes architectural specifications, system designs, roadmap tracking, and technical references.

---

## 🗺️ Documentation Map

```
docs/
├── README.md                                 # This Documentation Portal & Index
├── ontology.md                               # Live Auto-Generated Ontology Reference
├── graph-example.png                         # Knowledge Graph Visual Diagram
│
├── architecture/                             # Core Technical Architecture & Foundations
│   ├── ontology-model.md                     # 5-Layer Decoupled Graph Ontology Specification
│   ├── ingestion-pipeline.md                 # 5-Stage Ingestion Pipeline & SQLite Persistence
│   └── query-engine.md                       # SQLite Graph Database & Cypher-to-SQL Compiler
│
├── proposals/                                # Active Technical Proposals & Design Specs
│   ├── cross-service-detection.md            # Cross-Service & Messaging Channel Detection
│   └── vscode-extension-design.md            # Interactive VS Code & Antigravity Cockpit
│
├── roadmap/                                  # Engineering Milestones & Task Tracking
│   └── README.md                             # Unified Engineering Roadmap, Specs & Backlog
│
└── archive/                                  # Completed Historical Plans & Task Specs
    ├── library-parser-extraction.md          # Completed: Extraction of 5 Library Parsers
    └── self-descriptive-ontology-plan.md     # Completed: OntologyGen Build Integration Plan
```

---

## 📑 Document Catalog & Status

| Document | Category | Status | Target Audience | Summary |
| :--- | :--- | :--- | :--- | :--- |
| [**`ontology.md`**](ontology.md) | Reference | **Live (Generated)** | All / AI Agents | Auto-generated dictionary of all 27 node types, 30 relationships, properties, and ID schemes. |
| [**`architecture/ontology-model.md`**](architecture/ontology-model.md) | Architecture | **Active Standard** | Engine Developers / Architects | 5-layer decoupled graph architecture (Physical, Project, Syntactic, Semantic, Late-bound). |
| [**`architecture/ingestion-pipeline.md`**](architecture/ingestion-pipeline.md) | Architecture | **Active Standard** | Core Developers | 5-stage sequential pipeline (`WorkspaceIndexer`), Tree-sitter visitors, and async channel persistence. |
| [**`architecture/query-engine.md`**](architecture/query-engine.md) | Architecture | **Active Standard** | Graph Developers | SQLite schema (`nodes`, `edges`), B-Tree indexing, Cypher-to-SQL compiler, and timeout bounds. |
| [**`proposals/cross-service-detection.md`**](proposals/cross-service-detection.md) | Proposals | **Active Design** | Parser Contributors | Design for dynamic HTTP templates, suffix matching, GCP Pub/Sub, and WebSocket event tracing. |
| [**`proposals/vscode-extension-design.md`**](proposals/vscode-extension-design.md) | Proposals | **Active Design** | Extension Developers | Architecture for interactive Cytoscape.js webview extension and live LSP bridge. |
| [**`roadmap/README.md`**](roadmap/README.md) | Roadmap | **Active Master** | All Contributors / AI Agents | Unified roadmap: delivered milestones, active MCP & CLI semantic architecture spec, and engineering backlog. |
| [**`archive/library-parser-extraction.md`**](archive/library-parser-extraction.md) | Archive | **Completed Historical** | Historical Reference | Step-by-step extraction plan for NestJS, Express, Fetch, AspNetCore, and HttpClient parsers. |
| [**`archive/self-descriptive-ontology-plan.md`**](archive/self-descriptive-ontology-plan.md) | Archive | **Completed Historical** | Historical Reference | Initial design plan for the Roslyn-based `OntologyGen` build tool. |

---

## 🧭 Where to Start

- **New to CodeExplorer's internals?** Start with [5-Layer Graph Ontology](architecture/ontology-model.md) and the [Ingestion Pipeline](architecture/ingestion-pipeline.md).
- **Writing or debugging Cypher queries?** Read the [Query Engine Guide](architecture/query-engine.md) and check available nodes in the [Live Ontology Reference](ontology.md).
- **Contributing a new language or framework parser?** Consult [Ingestion Pipeline: Stage 3 & 4](architecture/ingestion-pipeline.md) and [Cross-Service Detection](proposals/cross-service-detection.md).
- **Tracking progress or picking up tasks?** Review the [Engineering Roadmap & Backlog](roadmap/README.md).

---

## ✍️ Documentation Guidelines

To keep documentation clean, reliable, and uniform across the global codebase:

1. **Strict English**: All documentation, comments, and commit messages must be written in English.
2. **Never Edit `ontology.md` Manually**: It is regenerated during the build by `OntologyGen` from code attributes.
3. **Keep Architecture Aligned with Code**: When modifying core classes (e.g. `WorkspaceIndexer`, `SqliteGraphClient`), ensure corresponding documents in `architecture/` are kept in sync.
4. **Archive Completed Plans**: Once an architectural task or migration is merged, move its design document into `archive/` with an archival status header.
