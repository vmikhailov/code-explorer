# CodeExplorer Backlog & Roadmap Ideas

### Completed
- [x] **Line numbers in symbol search**: `find_symbol` returns `file_path`, `start_line`, and `end_line` for direct source navigation.
- [x] **Graph schema discovery for Cypher**: `get_taxonomy` and `get_node_definition` tools expose complete ontology labels, relationship types, and node properties.
- [x] **Single database per workspace & `ce` CLI**: Streamlined embedded architecture with SQLite and self-contained single-file binary.
- [x] **Cypher query depth bounding & timeout resilience**: Eliminated unbounded graph traversal hangs, added 15s `CommandTimeout` and `CancellationToken` support.

---

### Active (In Progress)
- [ ] **Ingress & Egress Parsers (C#)**:
  - Full ASP.NET Core route composition: Controller-level `[Route("api/[controller]")]` + Action-level `[HttpGet("{id}")]` with token substitutions (`[controller]`, `[action]`).
  - Minimal API `app.MapGroup(...)` prefix concatenation.
  - Target URL/path resolution for `HttpClient`, `RestSharp`, and `Refit` declarative interfaces.
  - Message-driven ingress and egress detection (MassTransit consumers/publishers, MediatR handlers/requests, Kafka, RabbitMQ).

---

### Backlog & Planned Ideas
- [ ] **Mermaid Diagram Formatting**: Add `--format mermaid` (or parameter `format: "mermaid"`) to `get_project_dependencies` and `get_call_chain` for direct copy-pasting into Markdown and Confluence.
- [ ] **Static DI Analysis for TypeScript/JS (NestJS)**: Process `@Injectable()`, `@Inject()`, and `@Module()` declarations to resolve interfaces to implementations in `get_call_chain`.
- [ ] **C# Constructor Dependency Injection Resolution**: Automatically map constructor parameters `(IOrderService orderService)` and primary constructor parameters to interface method calls.
- [ ] **EF Core & Dapper Data Lineage**: Extract table names from `DbSet<T>` and Fluent API `ToTable("...")` to link C# code directly to database tables in `inspect_data_lineage`.
- [ ] **VS Code / Antigravity IDE Extension**: Interactive Cytoscape.js webview cockpit, bi-directional code navigation, and live LSP bridge using `vscode.executeDefinitionProvider`.
