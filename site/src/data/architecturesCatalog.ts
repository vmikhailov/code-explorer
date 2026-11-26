export type StackKey = 'agnostic' | 'dotnet' | 'typescript' | 'go' | 'python' | 'java';

export interface StackPaths {
  label: string;
  ext: string;
  domain: string;
  app: string;
  infra: string;
  ui: string;
  modPrefix: string;
  slicePrefix: string;
}

export const STACK_CONFIGS: Record<StackKey, StackPaths> = {
  agnostic: {
    label: 'Agnostic',
    ext: '.cs / .ts / .go',
    domain: '/Domain/',
    app: '/Application/',
    infra: '/Infrastructure/',
    ui: '/Api/',
    modPrefix: 'Modules/',
    slicePrefix: 'Features/',
  },
  dotnet: {
    label: 'C# / .NET',
    ext: '.cs',
    domain: '.Domain/',
    app: '.Application/',
    infra: '.Infrastructure/',
    ui: '.Api/',
    modPrefix: 'src/Modules/',
    slicePrefix: 'Features/',
  },
  typescript: {
    label: 'TypeScript',
    ext: '.ts',
    domain: 'src/domain/',
    app: 'src/application/',
    infra: 'src/infrastructure/',
    ui: 'src/controllers/',
    modPrefix: 'packages/',
    slicePrefix: 'src/features/',
  },
  go: {
    label: 'Go',
    ext: '.go',
    domain: 'internal/domain/',
    app: 'internal/usecase/',
    infra: 'internal/adapter/repository/',
    ui: 'internal/adapter/http/',
    modPrefix: 'internal/modules/',
    slicePrefix: 'internal/features/',
  },
  python: {
    label: 'Python',
    ext: '.py',
    domain: '/domain/',
    app: '/use_cases/',
    infra: '/adapters/orm/',
    ui: '/routers/',
    modPrefix: 'apps/',
    slicePrefix: 'features/',
  },
  java: {
    label: 'Java / Kotlin',
    ext: '.java',
    domain: '/domain/',
    app: '/application/',
    infra: '/infrastructure/',
    ui: '/web/',
    modPrefix: 'modules/',
    slicePrefix: 'features/',
  },
};

export interface ArchLayerSpec {
  name: string;
  role: string;
  paths: string;
  core?: boolean;
}

export interface ArchEdgeSpec {
  from: string;
  to: string;
  allow: boolean;
  code: string;
  gateId?: string;
}

export interface ArchGateSpec {
  id: string;
  name: string;
  coords: string;
  pattern: string;
  severity: 'Error' | 'Warning';
  description: string;
  remediation: string;
  target: string;
  message: string;
  query: string;
  platform: string;
  sampleViolation: {
    file: string;
    line: number;
    symbol: string;
    target: string;
    detail: string;
  };
}

export interface ArchPackSpec {
  slug: string;
  category: 'structural' | 'runtime';
  shortName: string;
  fileName: string;
  title: string;
  tagline: string;
  coreInvariant: string;
  whyItMatters: string[];
  howItErodes: {
    title: string;
    scenario: string;
    codeExample: string;
  }[];
  whyGraphBeatsLinters: string;
  bestFit: string[];
  avoidWhen: string[];
  graphLayersUsed: string[];
  layers: (s: StackPaths) => ArchLayerSpec[];
  edges: ArchEdgeSpec[];
  gates: (s: StackPaths, stack: StackKey) => ArchGateSpec[];
}

export const ARCHITECTURE_PACKS: ArchPackSpec[] = [
  {
    slug: 'clean-architecture',
    category: 'structural',
    shortName: 'Clean Architecture',
    fileName: 'clean-architecture.arch',
    title: 'Clean Architecture (Onion Dependency Rule)',
    tagline: 'All source code dependencies point strictly inward toward pure Domain entities and policies.',
    coreInvariant:
      'Inner layers (Domain Core and Application Use Cases) define business rules and abstract ports. They must never know which database engine, ORM, HTTP framework, or message broker sits in the outer rings.',
    whyItMatters: [
      'When Domain entities and Application use cases stay free of ORM annotations, DbContext references, and HTTP client SDKs, business rules can be tested in milliseconds in memory and evolved independently of database migrations or transport upgrades.',
      'As soon as a single Domain service imports an Infrastructure class (or an API controller bypasses Application use cases to run raw SQL directly), the separation collapses: domain unit tests now require a live database container, and transaction boundaries scatter across controllers.',
    ],
    howItErodes: [
      {
        title: 'AI Agent Copying a Convenience Helper into Domain',
        scenario:
          'When asked to calculate regional tax or validate a subscription limit inside a Domain aggregate, an AI agent searches for existing examples in the repo and imports TaxRateHttpClient or OrderDbContext straight into the Domain layer.',
        codeExample: `// [VIOLATION: SIG-CA-B-001] Inside src/Orders.Domain/Aggregates/Order.cs
public async Task RecalculateTotalsAsync(OrderDbContext db, TaxRateClient taxClient)
{
    // Domain aggregate couples directly to EF Core DbContext and outbound HTTP client
    var rate = await taxClient.GetRateAsync(this.BillingCountry);
    await db.SaveChangesAsync();
}`,
      },
      {
        title: 'Controller Bypassing Application Use Cases into SQL / ORM',
        scenario:
          'Under deadline pressure, an endpoint handler injects a repository or raw SQL connection directly into a controller action, skipping Application command validation, authorization policies, and domain events.',
        codeExample: `// [VIOLATION: SIG-CA-B-003] Inside src/Orders.Api/Controllers/OrdersController.cs
[HttpPost("{id}/cancel")]
public async Task<IActionResult> Cancel(Guid id, [FromServices] SqlConnection conn)
{
    // Bypasses CancelOrderCommandHandler and domain invariant checks
    await conn.ExecuteAsync("UPDATE orders SET status = 'cancelled' WHERE id = @id", new { id });
    return Ok();
}`,
      },
    ],
    whyGraphBeatsLinters:
      'Project-reference linters only see .csproj or package.json boundaries. In many repositories, layers live in folders within the same project, or leaks occur through transitive method calls and raw SQL queries (Layer 4 :Query and :Table nodes). CodeExplorer traces both AST call edges (-[:CALLS]->) and SQL dataflow (-[:ACCESSED_BY]->) across all five layers.',
    bestFit: [
      'Long-lived enterprise backends with rich domain invariants and multiple workflows',
      'Systems where business rules must be verified independently of persistence and transport',
      'Brownfield monoliths being untangled with a SHA-256 baseline ratchet',
    ],
    avoidWhen: [
      'Thin CRUD microservices that only forward JSON payloads to a single database table',
      'Short-lived prototypes or internal admin scripts with no domain logic',
    ],
    graphLayersUsed: ['L2 :Project', 'L3 :Type & :Function', 'L4 :Query & :Table'],
    layers: (s) => [
      {
        name: '1. Presentation & Delivery (API / gRPC / CLI)',
        role: 'HTTP controllers, GraphQL resolvers, gRPC services, consumer entrypoints',
        paths: `${s.ui}**`,
      },
      {
        name: '2. Infrastructure & Persistence (ORM / SQL / SDKs)',
        role: 'Database repositories, DbContext, external HTTP/RPC clients, broker adapters',
        paths: `${s.infra}**`,
      },
      {
        name: '3. Application Use Cases (Commands / Queries / Ports)',
        role: 'Use-case orchestration, command/query handlers, inbound/outbound port interfaces',
        paths: `${s.app}**`,
      },
      {
        name: '4. Domain Core (Aggregates / Entities / Value Objects)',
        role: 'Pure business invariants, domain events, and specifications (0 outward dependencies)',
        paths: `${s.domain}**`,
        core: true,
      },
    ],
    edges: [
      { from: 'Presentation', to: 'Application Use Cases', allow: true, code: 'ALLOWED' },
      { from: 'Infrastructure', to: 'Application & Domain', allow: true, code: 'ALLOWED' },
      { from: 'Domain Core', to: 'Infrastructure or Presentation', allow: false, code: 'SIG-CA-B-001', gateId: 'SIG-CA-B-001' },
      { from: 'Application', to: 'Concrete Infrastructure Adapter', allow: false, code: 'SIG-CA-B-002', gateId: 'SIG-CA-B-002' },
      { from: 'Presentation Controller', to: 'Direct SQL / ORM Query', allow: false, code: 'SIG-CA-B-003', gateId: 'SIG-CA-B-003' },
    ],
    gates: (s, stack) => [
      {
        id: 'SIG-CA-B-001',
        name: 'Domain Core Purity',
        coords: 'Module x Boundaries',
        pattern: '(Domain)-[:DEPENDS_ON|CALLS]->(Application|Infrastructure|Presentation)',
        severity: 'Error',
        description: 'Domain types and functions must never call or depend on Application, Infrastructure, or Presentation layers.',
        remediation: 'Extract an interface (Port) in Domain or Application and inject the implementation from Infrastructure.',
        target: '{dst.name}',
        message: "Domain symbol '{src.name}' calls outer-layer symbol '{dst.name}'",
        query: `MATCH (src:Function)-[:CALLS]->(dst:Function)\n  WHERE src.file_path CONTAINS '${s.domain}'\n    AND (dst.file_path CONTAINS '${s.infra}' OR dst.file_path CONTAINS '${s.app}' OR dst.file_path CONTAINS '${s.ui}')\n  RETURN src, dst`,
        platform: stack,
        sampleViolation: {
          file: `src/Orders${s.domain}Aggregates/Order${s.ext.split(' ')[0]}`,
          line: 84,
          symbol: 'Order.RecalculateTotalsAsync',
          target: 'TaxRateHttpClient.GetRateAsync',
          detail: 'Domain aggregate invokes outbound infrastructure HTTP client directly.',
        },
      },
      {
        id: 'SIG-CA-B-002',
        name: 'Application Layer Isolation',
        coords: 'Module x Boundaries',
        pattern: '(Application)-[:DEPENDS_ON|CALLS]->(Infrastructure|Presentation)',
        severity: 'Error',
        description: 'Application use cases orchestrate Domain logic through abstract ports without importing concrete Infrastructure or Delivery adapters.',
        remediation: 'Depend on an Application port interface (e.g. IOrderRepository) rather than the concrete SqlOrderRepository.',
        target: '{dst.name}',
        message: "Application use case '{src.name}' depends directly on adapter '{dst.name}'",
        query: `MATCH (src:Function)-[:CALLS]->(dst:Function)\n  WHERE src.file_path CONTAINS '${s.app}'\n    AND (dst.file_path CONTAINS '${s.infra}' OR dst.file_path CONTAINS '${s.ui}')\n  RETURN src, dst`,
        platform: stack,
        sampleViolation: {
          file: `src/Billing${s.app}Commands/CaptureInvoiceHandler${s.ext.split(' ')[0]}`,
          line: 47,
          symbol: 'CaptureInvoiceHandler.Handle',
          target: 'StripeRestAdapter.ChargeCardAsync',
          detail: 'Use-case handler calls concrete Stripe adapter instead of IPaymentGateway port.',
        },
      },
      {
        id: 'SIG-CA-B-003',
        name: 'No Delivery-to-Database Bypass',
        coords: 'Method x Boundaries',
        pattern: '(Endpoint|Controller)-[:CALLS|READS_FROM|WRITES_TO]->(Query|Table)',
        severity: 'Error',
        description: 'Delivery handlers must dispatch through Application use cases rather than issuing SQL or ORM queries directly.',
        remediation: 'Move the database query into an Application query/command handler or repository.',
        target: '{q.name}',
        message: "Delivery handler '{fn.name}' executes database query '{q.name}' directly, bypassing Application layer",
        query: `MATCH (q:Query)-[:ACCESSED_BY]->(fn:Function)\n  WHERE fn.file_path CONTAINS '${s.ui}'\n  RETURN fn, q`,
        platform: stack,
        sampleViolation: {
          file: `src/Orders${s.ui}OrdersController${s.ext.split(' ')[0]}`,
          line: 118,
          symbol: 'OrdersController.CancelOrder',
          target: 'UPDATE orders SET status = ...',
          detail: 'HTTP controller executes SQL UPDATE directly without dispatching CancelOrderCommand.',
        },
      },
      {
        id: 'SIG-CA-C-004',
        name: 'Persistence-Ignorant Domain Entities',
        coords: 'Class x Contract',
        pattern: "(Type {layer:'Domain'}) with ORM or HTTP transport annotations",
        severity: 'Warning',
        description: 'Domain entities should stay free of database table or HTTP serialization decorators.',
        remediation: 'Use separate persistence configurations (e.g. IEntityTypeConfiguration) or DTOs in outer layers.',
        target: '{t.attributes}',
        message: "Domain type '{t.name}' carries outer framework annotation '{t.attributes}'",
        query: `MATCH (t:Type)\n  WHERE t.file_path CONTAINS '${s.domain}'\n    AND (coalesce(t.attributes, '') CONTAINS 'Table' OR coalesce(t.attributes, '') CONTAINS 'Entity' OR coalesce(t.attributes, '') CONTAINS 'JsonProperty')\n  RETURN t`,
        platform: stack,
        sampleViolation: {
          file: `src/Catalog${s.domain}Entities/Product${s.ext.split(' ')[0]}`,
          line: 12,
          symbol: 'Product',
          target: '[Table("catalog_products"), JsonProperty]',
          detail: 'Domain entity is polluted with relational schema and JSON transport attributes.',
        },
      },
      {
        id: 'SIG-CA-B-005',
        name: 'Acyclic Project Dependency Graph',
        coords: 'Module x Boundaries',
        pattern: '(p1:Project)-[:DEPENDS_ON]->(p2:Project)-[:DEPENDS_ON]->(p1)',
        severity: 'Error',
        description: 'Projects must form a strict directed acyclic graph without mutual dependencies.',
        remediation: 'Extract shared contracts or domain events into an inward contracts project to break the cycle.',
        target: '{p2.name}',
        message: "Circular project dependency between '{p1.name}' and '{p2.name}'",
        query: `MATCH (p1:Project)-[:DEPENDS_ON]->(p2:Project)-[:DEPENDS_ON]->(p1)\n  WHERE p1.id < p2.id\n  RETURN p1, p2`,
        platform: stack,
        sampleViolation: {
          file: 'src/Orders.Application/Orders.Application.csproj',
          line: 1,
          symbol: 'Orders.Application',
          target: 'Fulfillment.Shared',
          detail: 'Mutual project reference cycle between Orders.Application and Fulfillment.Shared.',
        },
      },
    ],
  },
  {
    slug: 'hexagonal',
    category: 'structural',
    shortName: 'Hexagonal (Ports & Adapters)',
    fileName: 'hexagonal-ports-adapters.arch',
    title: 'Hexagonal Architecture (Ports & Adapters)',
    tagline: 'Driving inbound adapters and driven outbound adapters meet exclusively through explicit Core Port interfaces.',
    coreInvariant:
      'A symmetrical boundary separates the Inside (Application & Domain Hexagon + Inbound/Outbound Ports) from the Outside (Driving HTTP/gRPC/Queue adapters and Driven SQL/Redis/SDK adapters). No adapter may ever call another adapter directly.',
    whyItMatters: [
      'A service often starts with one REST API and one PostgreSQL database, then grows to support gRPC, Kafka consumers, CLI jobs, and third-party webhook callbacks. Hexagonal architecture ensures every entry trigger drives the exact same Inbound Port contract.',
      'By forcing all external I/O behind Outbound Ports implemented by Driven Adapters, you can swap payment gateways, mock external APIs at the port boundary, and prevent vendor SDK types (AWS, Stripe, Twilio) from leaking into core domain models.',
    ],
    howItErodes: [
      {
        title: 'Driving Adapter Short-Circuiting Directly into a Driven Adapter',
        scenario:
          'An HTTP webhook controller or Kafka consumer imports a Postgres repository or S3 client adapter directly instead of invoking an Inbound Port use case, splitting business validation across transport adapters.',
        codeExample: `// [VIOLATION: SIG-HEX-B-001] Driving HTTP adapter calls Driven Postgres adapter directly
public class StripeWebhookController : ControllerBase
{
    public async Task<IActionResult> Handle(PostgresSubscriptionAdapter repo)
    {
        // Bypasses IHandleSubscriptionRenewalUseCase inbound port
        await repo.MarkRenewedDirectlyAsync(Request.Body);
        return Ok();
    }
}`,
      },
      {
        title: 'Outbound HTTP / SDK Call Leaking Inside the Hexagon Core',
        scenario:
          'Instead of defining an outbound port interface (e.g. FraudScoringPort), a developer instantiates an HttpClient or external SDK call straight inside the application core.',
        codeExample: `// [VIOLATION: SIG-HEX-B-004] Outbound HTTP call declared inside Hexagon Core
public class CheckoutCoreService
{
    public async Task ApproveAsync(HttpClient http, Order order)
    {
        // Creates Layer 4 :ExternalService node inside /domain/ instead of /adapter/
        var score = await http.PostAsJsonAsync("https://fraud.internal/v1/score", order);
    }
}`,
      },
    ],
    whyGraphBeatsLinters:
      'CodeExplorer extracts Layer 3 interface realization edges (-[:IMPLEMENTS]->) and Layer 4 outbound calls (:ExternalService-[:DECLARED_IN]->:File). That means it can verify not just folder imports, but whether every concrete driven repository actually implements a Port interface and whether any outbound network call originates outside the adapter ring.',
    bestFit: [
      'Services driven by multiple protocols (REST + gRPC + message queue consumers + CLI)',
      'Domains integrating with volatile external APIs (payments, KYC, shipping, LLM providers)',
    ],
    avoidWhen: [
      'Simple single-protocol services where extra port/adapter indirection adds boilerplate without value',
    ],
    graphLayersUsed: ['L3 :Type-[:IMPLEMENTS]->:Type', 'L3 :Function-[:CALLS]->:Function', 'L4 :ExternalService'],
    layers: (s) => [
      {
        name: '1. Driving Inbound Adapters (HTTP / gRPC / Consumers)',
        role: 'Translates external requests/messages into Inbound Port calls',
        paths: `${s.ui}**`,
      },
      {
        name: '2. Inbound & Outbound Ports (Hexagon Boundary)',
        role: 'Use-case interfaces (Inbound) and SPI contracts for persistence/messaging (Outbound)',
        paths: `${s.app}ports/**`,
        core: true,
      },
      {
        name: '3. Hexagon Core (Domain & Application Logic)',
        role: 'Implements Inbound Ports and orchestrates domain models via Outbound Ports',
        paths: `${s.domain}**`,
        core: true,
      },
      {
        name: '4. Driven Outbound Adapters (SQL / Redis / External SDKs)',
        role: 'Concrete implementations of Outbound Ports talking to databases, brokers, and APIs',
        paths: `${s.infra}**`,
      },
    ],
    edges: [
      { from: 'Driving Adapter', to: 'Inbound Port Interface', allow: true, code: 'ALLOWED' },
      { from: 'Driven Adapter', to: 'Implements Outbound Port', allow: true, code: 'ALLOWED' },
      { from: 'Driving Adapter', to: 'Driven Adapter (Short-Circuit)', allow: false, code: 'SIG-HEX-B-001', gateId: 'SIG-HEX-B-001' },
      { from: 'Hexagon Core', to: 'Concrete Driven Adapter', allow: false, code: 'SIG-HEX-B-002', gateId: 'SIG-HEX-B-002' },
    ],
    gates: (s, stack) => [
      {
        id: 'SIG-HEX-B-001',
        name: 'No Adapter-to-Adapter Short-Circuit',
        coords: 'Method x Boundaries',
        pattern: '(DrivingAdapter)-[:CALLS]->(DrivenAdapter) bypassing Core Port',
        severity: 'Error',
        description: 'Inbound HTTP/gRPC/CLI adapters must never invoke outbound persistence or external SDK adapters directly.',
        remediation: 'Invoke an Inbound Port use case in the Hexagon Core, which then calls the Outbound Port interface.',
        target: '{outbound.name}',
        message: "Inbound adapter '{inbound.name}' bypasses Core Port and calls outbound adapter '{outbound.name}'",
        query: `MATCH (inbound:Function)-[:CALLS]->(outbound:Function)\n  WHERE inbound.file_path CONTAINS '${s.ui}' AND outbound.file_path CONTAINS '${s.infra}'\n  RETURN inbound, outbound`,
        platform: stack,
        sampleViolation: {
          file: `src/Payments${s.ui}StripeWebhookController${s.ext.split(' ')[0]}`,
          line: 53,
          symbol: 'StripeWebhookController.HandleWebhook',
          target: 'PostgresSubscriptionAdapter.UpdateStatusAsync',
          detail: 'Driving webhook controller bypasses Hexagon Core and calls driven Postgres adapter directly.',
        },
      },
      {
        id: 'SIG-HEX-B-002',
        name: 'Hexagon Core Infrastructure Isolation',
        coords: 'Module x Boundaries',
        pattern: '(Core)-[:CALLS]->(DrivenAdapter|ExternalService)',
        severity: 'Error',
        description: 'Domain and Port definitions inside the hexagon must never call concrete adapter implementations.',
        remediation: 'Define an Outbound Port interface inside the hexagon and invert the dependency.',
        target: '{adapter.name}',
        message: "Hexagon core function '{core.name}' calls concrete driven adapter '{adapter.name}'",
        query: `MATCH (core:Function)-[:CALLS]->(adapter:Function)\n  WHERE (core.file_path CONTAINS '${s.domain}' OR core.file_path CONTAINS '${s.app}')\n    AND adapter.file_path CONTAINS '${s.infra}'\n  RETURN core, adapter`,
        platform: stack,
        sampleViolation: {
          file: `src/Checkout${s.domain}CheckoutOrchestrator${s.ext.split(' ')[0]}`,
          line: 79,
          symbol: 'CheckoutOrchestrator.PlaceOrderAsync',
          target: 'KafkaProducerAdapter.PublishRawAsync',
          detail: 'Core orchestrator calls concrete KafkaProducerAdapter instead of OrderEventOutputPort.',
        },
      },
      {
        id: 'SIG-HEX-C-003',
        name: 'Driven Repository Implements Port Interface',
        coords: 'Class x Contract',
        pattern: 'Repository or Adapter class without IMPLEMENTS -> Port interface',
        severity: 'Warning',
        description: 'Every concrete outbound repository or client adapter must implement an explicit Port interface.',
        remediation: 'Extract an Outbound Port interface in the Application layer and have the adapter implement it.',
        target: 'PortInterface',
        message: "Driven adapter '{repo.name}' does not implement an outbound Port interface",
        query: `MATCH (repo:Type)\n  WHERE repo.file_path CONTAINS '${s.infra}' AND repo.name ENDS WITH 'Repository'\n    AND coalesce(repo.implements, '') = ''\n  RETURN repo`,
        platform: stack,
        sampleViolation: {
          file: `src/Inventory${s.infra}WarehouseSqlRepository${s.ext.split(' ')[0]}`,
          line: 14,
          symbol: 'WarehouseSqlRepository',
          target: 'PortInterface',
          detail: 'Concrete repository class does not implement any outbound Port interface.',
        },
      },
      {
        id: 'SIG-HEX-B-004',
        name: 'External HTTP Calls Confined to Driven Adapters',
        coords: 'Service x Boundaries',
        pattern: '(ExternalService)-[:DECLARED_IN]->(File outside Driven Adapters)',
        severity: 'Error',
        description: 'Outbound HTTP/RPC calls must originate strictly from driven adapter modules.',
        remediation: 'Move the HTTP client invocation into a Driven Adapter implementing an Outbound Port.',
        target: '{es.url}',
        message: "External service call '{es.name}' is declared outside driven adapters in '{f.path}'",
        query: `MATCH (es:ExternalService)-[:DECLARED_IN]->(f:File)\n  WHERE NOT f.path CONTAINS '${s.infra}'\n  RETURN es, f`,
        platform: stack,
        sampleViolation: {
          file: `src/Checkout${s.domain}FraudCheckService${s.ext.split(' ')[0]}`,
          line: 38,
          symbol: 'POST https://fraud.internal/v1/score',
          target: 'https://fraud.internal/v1/score',
          detail: 'Outbound HTTP call detected inside Domain Core outside driven adapters.',
        },
      },
    ],
  },
  {
    slug: 'modular-monolith',
    category: 'structural',
    shortName: 'Modular Monolith',
    fileName: 'modular-monolith.arch',
    title: 'Modular Monolith (Strict Module & SQL Table Encapsulation)',
    tagline: 'One deployable unit split into autonomous business modules that share neither internal classes nor database tables.',
    coreInvariant:
      'Every business module (Orders, Billing, Inventory, Identity) exposes only a narrow Public Contract (Facades, Commands, Integration Events) and owns its SQL tables exclusively. Cross-module calls into /Internal/ types or direct SQL queries against another module’s tables are forbidden.',
    whyItMatters: [
      'A Modular Monolith gives teams the operational simplicity of a single deployment (no distributed transactions, network serialization, or Kubernetes service mesh overhead) while preserving clear domain boundaries.',
      'However, 90% of Modular Monoliths degenerate into a "Big Ball of Mud" because nothing stops Module A from querying or updating Module B’s SQL tables directly. Once two modules share mutable tables, neither can change its schema or scale out independently.',
    ],
    howItErodes: [
      {
        title: 'Cross-Module Direct Database Table Write or Join',
        scenario:
          'In Case Study 07 and 08 on twentyhq/twenty, when asked to implement an admin or billing feature, the AI agent saw a legacy shortcut in an adjacent service and injected the foreign module’s TypeORM repository directly to query/mutate its table instead of calling the owning module’s public service.',
        codeExample: `// [VIOLATION: SIG-MM-B-002] Billing module mutates Orders module table directly
public class InvoiceSettlementHandler
{
    public async Task SettleAsync(DbConnection db, Guid orderId)
    {
        // Table 'purchase_orders' is owned by the Orders module!
        await db.ExecuteAsync("UPDATE purchase_orders SET billing_settled = true WHERE id = @orderId");
    }
}`,
      },
      {
        title: 'Calling Another Module’s Internal Domain Entities or Repositories',
        scenario:
          'When modules live in the same repository, IDE auto-import happily imports Orders.Internal.OrderPricingEngine inside Billing, bypassing Orders.Contracts.',
        codeExample: `// [VIOLATION: SIG-MM-B-001] Cross-module call into /Internal/ namespace
using Modules.Orders.Internal.Domain; // Forbidden: must use Modules.Orders.Contracts

public class BillingDiscountCalculator
{
    public Money Apply(OrderInternalAggregate order) => order.ComputeRawMargin();
}`,
      },
    ],
    whyGraphBeatsLinters:
      'Import linters (eslint, NetArchTest, ArchUnit) cannot parse SQL strings or trace which module owns which database table. CodeExplorer builds Layer 4 :Table and :Query nodes with -[:READS_FROM]->, -[:WRITES_TO]->, and -[:OWNS]-> edges, catching cross-module table access even when done via raw SQL or Dapper/TypeORM repositories.',
    bestFit: [
      'Product engineering teams that want strict Bounded Context isolation without microservice ops',
      'Monoliths preparing to extract 1 or 2 high-load modules into standalone services later',
    ],
    avoidWhen: [
      'Subsystems require completely different runtimes or independent scaling / blast-radius isolation',
    ],
    graphLayersUsed: ['L2 :Project', 'L3 :Function-[:CALLS]->:Function', 'L4 :Query-[:READS_FROM|WRITES_TO]->:Table'],
    layers: (s) => [
      {
        name: '1. Module Public Contracts (Facades / Commands / Events)',
        role: 'The ONLY legal entry surface for other modules in the monolith',
        paths: `${s.modPrefix}*/Contracts/**`,
        core: true,
      },
      {
        name: '2. Module A Internals (Orders: Handlers, Domain, Persistence)',
        role: 'Encapsulated domain model and repositories private to Orders',
        paths: `${s.modPrefix}Orders/Internal/**`,
      },
      {
        name: '3. Module B Internals (Billing: Handlers, Domain, Persistence)',
        role: 'Encapsulated domain model and repositories private to Billing',
        paths: `${s.modPrefix}Billing/Internal/**`,
      },
      {
        name: '4. Module-Owned SQL Tables (orders_*, billing_*)',
        role: 'Each SQL table has a single owning module for writes and reads',
        paths: 'Layer 4 :Table ownership boundary',
      },
    ],
    edges: [
      { from: 'Module A', to: 'Module B Public Contracts', allow: true, code: 'ALLOWED' },
      { from: 'Module A', to: 'Module A Owned SQL Tables', allow: true, code: 'ALLOWED' },
      { from: 'Module A', to: 'Module B Internal Types', allow: false, code: 'SIG-MM-B-001', gateId: 'SIG-MM-B-001' },
      { from: 'Module A', to: 'Module B Owned SQL Tables', allow: false, code: 'SIG-MM-B-002', gateId: 'SIG-MM-B-002' },
    ],
    gates: (s, stack) => [
      {
        id: 'SIG-MM-B-001',
        name: 'No Cross-Module Internal Symbol Calls',
        coords: 'Module x Boundaries',
        pattern: '(ModuleA)-[:CALLS]->(ModuleB/Internal) bypassing Contracts',
        severity: 'Error',
        description: 'Modules may only invoke another module through its Contracts or Public API namespace.',
        remediation: 'Expose the required operation on the target module’s Public Contract facade or publish an integration event.',
        target: '{callee.name}',
        message: "Cross-module encapsulation breach: '{caller.project_name}' calls internal symbol '{callee.name}'",
        query: `MATCH (caller:Function)-[:CALLS]->(callee:Function)\n  WHERE caller.project_name <> callee.project_name\n    AND callee.file_path CONTAINS '${s.modPrefix}'\n    AND (callee.file_path CONTAINS '/Internal/' OR callee.file_path CONTAINS '/Domain/' OR callee.file_path CONTAINS '/Persistence/')\n  RETURN caller, callee`,
        platform: stack,
        sampleViolation: {
          file: `${s.modPrefix}Billing/Internal/InvoiceService${s.ext.split(' ')[0]}`,
          line: 64,
          symbol: 'InvoiceService.GenerateAsync',
          target: 'Orders.Internal.OrderPricingRepository.GetCostBreakdown',
          detail: 'Billing module bypasses Orders.Contracts and calls internal OrderPricingRepository.',
        },
      },
      {
        id: 'SIG-MM-B-002',
        name: 'Cross-Module Database Table Write Shield',
        coords: 'Service x Boundaries',
        pattern: '(Two distinct Modules)<-[:DECLARED_IN]-(Query)-[:WRITES_TO]->(Shared Table)',
        severity: 'Error',
        description: 'Each database table must have a single owning module responsible for mutations.',
        remediation: 'Send a Command or publish an Integration Event to the module that owns the table.',
        target: '{t.name}',
        message: "Table '{t.name}' is mutated by multiple modules: '{f1.project_name}' and '{f2.project_name}'",
        query: `MATCH (q1:Query)-[:WRITES_TO]->(t:Table)<-[:WRITES_TO]-(q2:Query)\n  MATCH (q1)-[:ACCESSED_BY]->(f1:Function), (q2)-[:ACCESSED_BY]->(f2:Function)\n  WHERE f1.project_name < f2.project_name\n  RETURN f1, t, f2`,
        platform: stack,
        sampleViolation: {
          file: `${s.modPrefix}Billing/Internal/SettlementWriter${s.ext.split(' ')[0]}`,
          line: 92,
          symbol: 'Modules.Billing',
          target: 'purchase_orders (also written by Modules.Orders)',
          detail: 'Table purchase_orders is written directly by both Modules.Orders and Modules.Billing.',
        },
      },
      {
        id: 'SIG-MM-B-003',
        name: 'No Cross-Module Direct SQL Table Reads',
        coords: 'Service x Boundaries',
        pattern: '(ModuleA Query)-[:READS_FROM]->(Table owned/written by ModuleB)',
        severity: 'Warning',
        description: 'Modules should read foreign module data via query facades or local read projections instead of direct SQL joins.',
        remediation: 'Query the owning module’s public contract facade or maintain an event-driven local projection.',
        target: '{t.name}',
        message: "Module '{fr.project_name}' reads table '{t.name}' owned by module '{fw.project_name}'",
        query: `MATCH (qw:Query)-[:WRITES_TO]->(t:Table)<-[:READS_FROM]-(qr:Query)\n  MATCH (qw)-[:ACCESSED_BY]->(fw:Function), (qr)-[:ACCESSED_BY]->(fr:Function)\n  WHERE fw.project_name <> fr.project_name\n  RETURN fr, t, fw`,
        platform: stack,
        sampleViolation: {
          file: `${s.modPrefix}Shipping/Internal/ShipmentPlanner${s.ext.split(' ')[0]}`,
          line: 41,
          symbol: 'ShipmentPlanner.LoadCustomerTier',
          target: 'customer_subscriptions (owned by Modules.Billing)',
          detail: 'Shipping module executes SELECT against customer_subscriptions table owned by Billing.',
        },
      },
      {
        id: 'SIG-MM-B-004',
        name: 'Shared Kernel Purity',
        coords: 'Module x Boundaries',
        pattern: '(SharedKernel)-[:DEPENDS_ON|CALLS]->(BusinessModule)',
        severity: 'Error',
        description: 'Shared building blocks must never depend back on any concrete business module.',
        remediation: 'Keep SharedKernel strictly generic; move domain-specific types into the owning business module.',
        target: '{pMod.name}',
        message: "Shared kernel '{pShared.name}' depends on business module '{pMod.name}'",
        query: `MATCH (pShared:Project)-[:DEPENDS_ON]->(pMod:Project)\n  WHERE (pShared.name CONTAINS 'Shared' OR pShared.name CONTAINS 'Common' OR pShared.name CONTAINS 'BuildingBlocks')\n    AND pMod.path CONTAINS '${s.modPrefix}'\n  RETURN pShared, pMod`,
        platform: stack,
        sampleViolation: {
          file: 'src/BuildingBlocks.Common/BuildingBlocks.Common.csproj',
          line: 1,
          symbol: 'BuildingBlocks.Common',
          target: 'Modules.Identity',
          detail: 'Shared kernel project references concrete business module Modules.Identity.',
        },
      },
      {
        id: 'SIG-MM-B-005',
        name: 'Acyclic Inter-Module Contracts',
        coords: 'Module x Boundaries',
        pattern: '(ModA)-[:DEPENDS_ON]->(ModB)-[:DEPENDS_ON]->(ModA)',
        severity: 'Error',
        description: 'Module contract dependencies must remain strictly acyclic.',
        remediation: 'Decouple one direction of the interaction using an asynchronous Integration Event.',
        target: '{m2.name}',
        message: "Bidirectional coupling between modules '{m1.name}' and '{m2.name}'",
        query: `MATCH (m1:Project)-[:DEPENDS_ON]->(m2:Project)-[:DEPENDS_ON]->(m1)\n  WHERE m1.id < m2.id\n  RETURN m1, m2`,
        platform: stack,
        sampleViolation: {
          file: `${s.modPrefix}Orders/Orders.Contracts.csproj`,
          line: 1,
          symbol: 'Modules.Orders.Contracts',
          target: 'Modules.Inventory.Contracts',
          detail: 'Bidirectional contract dependency cycle between Orders and Inventory.',
        },
      },
    ],
  },
  {
    slug: 'vertical-slice',
    category: 'structural',
    shortName: 'Vertical Slice',
    fileName: 'vertical-slice.arch',
    title: 'Vertical Slice Architecture (Zero Lateral Feature Coupling)',
    tagline: 'Organize code around self-contained feature slices (Request -> Handler -> Storage) that never couple laterally.',
    coreInvariant:
      'Minimize coupling between features and maximize cohesion within a single feature. Each slice (e.g. Features/Orders/CreateOrder, Features/Orders/CancelOrder) owns its request contract, validation, handler, and data query, and must never import DTOs or handlers from a sibling slice.',
    whyItMatters: [
      'In traditional layered architectures, adding or changing one endpoint requires editing 6 horizontal folders (Controller, Service, Interface, Repository, DTO, Mapper) where shared abstractions create unintended blast radius across unrelated features.',
      'In Vertical Slice Architecture, changing CreateOrder touches only Features/Orders/CreateOrder. New engineers and AI coding agents can understand and modify a feature by reading a single folder—provided sibling slices stay 100% decoupled.',
    ],
    howItErodes: [
      {
        title: 'AI Agent Reusing a Sibling Slice’s Request Validator or Helper',
        scenario:
          'When implementing Features/Orders/CloneOrder, an AI agent finds a convenient helper or response DTO inside Features/Orders/CreateOrder and imports it directly, silently coupling the two slices.',
        codeExample: `// [VIOLATION: SIG-VSA-B-001] Lateral call from CloneOrder slice into CreateOrder slice
namespace Features.Orders.CloneOrder;
using Features.Orders.CreateOrder; // Forbidden lateral slice coupling!

public class CloneOrderHandler
{
    public Task Handle(CloneOrderCommand cmd) => CreateOrderTaxHelper.ApplyAsync(cmd);
}`,
      },
    ],
    whyGraphBeatsLinters:
      'Because all slices live inside the same compilation project (e.g. Features/Orders/CreateOrder and Features/Orders/CancelOrder), compiler project boundaries cannot isolate them. CodeExplorer compares path segments on every Layer 3 -[:CALLS]-> edge to block lateral slice-to-slice dependencies automatically.',
    bestFit: [
      'Feature-velocity product applications and MediatR / CQRS request-response APIs',
      'Teams where each ticket maps to an isolated user story or API endpoint',
    ],
    avoidWhen: [
      'Deeply algorithmic engines (compilers, physics, trading matchers) with minimal endpoint surface',
    ],
    graphLayersUsed: ['L1 :Folder Slice', 'L3 :Function-[:CALLS]->:Function', 'L3 :Type'],
    layers: (s) => [
      {
        name: 'Slice A: CreateOrder (Endpoint + Command + Validator + SQL)',
        role: 'Self-contained vertical feature slice for order creation',
        paths: `${s.slicePrefix}Orders/CreateOrder/**`,
      },
      {
        name: 'Slice B: CancelOrder (Endpoint + Command + Validator + SQL)',
        role: 'Self-contained vertical feature slice for order cancellation',
        paths: `${s.slicePrefix}Orders/CancelOrder/**`,
      },
      {
        name: 'Slice C: IssueInvoice (Endpoint + Command + Validator + SQL)',
        role: 'Self-contained vertical feature slice for invoicing',
        paths: `${s.slicePrefix}Billing/IssueInvoice/**`,
      },
      {
        name: 'Shared Domain & Infrastructure Primitives (Inward Only)',
        role: 'Pure domain entities, auth context, and low-level DB session factory',
        paths: 'Shared/**, Domain/**',
        core: true,
      },
    ],
    edges: [
      { from: 'Feature Slice A', to: 'Shared Domain Primitives', allow: true, code: 'ALLOWED' },
      { from: 'Feature Slice A', to: 'Feature Slice B (Lateral Call)', allow: false, code: 'SIG-VSA-B-001', gateId: 'SIG-VSA-B-001' },
      { from: 'Shared Primitives', to: 'Any Feature Slice', allow: false, code: 'SIG-VSA-B-002', gateId: 'SIG-VSA-B-002' },
    ],
    gates: (s, stack) => [
      {
        id: 'SIG-VSA-B-001',
        name: 'Zero Lateral Feature-to-Feature Calls',
        coords: 'Module x Boundaries',
        pattern: '(Features/SliceA)-[:CALLS]->(Features/SliceB)',
        severity: 'Error',
        description: 'Feature slices must remain independent and never directly call handlers, DTOs, or helpers in sibling slices.',
        remediation: 'If logic is genuinely domain-wide, promote it to Domain/ or Shared/; otherwise keep slice-specific copies.',
        target: '{b.name}',
        message: "Feature slice function '{a.name}' couples laterally to sibling slice symbol '{b.name}'",
        query: `MATCH (a:Function)-[:CALLS]->(b:Function)\n  WHERE a.file_path CONTAINS '${s.slicePrefix}' AND b.file_path CONTAINS '${s.slicePrefix}'\n    AND split(a.file_path, '/')[2] <> split(b.file_path, '/')[2]\n  RETURN a, b`,
        platform: stack,
        sampleViolation: {
          file: `${s.slicePrefix}Orders/CloneOrder/CloneOrderHandler${s.ext.split(' ')[0]}`,
          line: 34,
          symbol: 'CloneOrderHandler.Handle',
          target: 'CreateOrderValidator.ValidateLineItems',
          detail: 'CloneOrder slice calls CreateOrderValidator declared in sibling CreateOrder slice.',
        },
      },
      {
        id: 'SIG-VSA-B-002',
        name: 'Shared Primitives Never Call Feature Slices',
        coords: 'Module x Boundaries',
        pattern: '(Shared|Common|Domain)-[:CALLS]->(Features/*)',
        severity: 'Error',
        description: 'Shared infrastructure and domain primitives must never depend upward on specific feature slices.',
        remediation: 'Invert the dependency or move the feature-specific logic back into its owning slice.',
        target: '{feat.name}',
        message: "Shared symbol '{sh.name}' depends upward on feature slice '{feat.name}'",
        query: `MATCH (sh:Function)-[:CALLS]->(feat:Function)\n  WHERE NOT sh.file_path CONTAINS '${s.slicePrefix}' AND feat.file_path CONTAINS '${s.slicePrefix}'\n    AND (sh.file_path CONTAINS 'Shared' OR sh.file_path CONTAINS 'Common' OR sh.file_path CONTAINS 'Domain')\n  RETURN sh, feat`,
        platform: stack,
        sampleViolation: {
          file: `src/Shared/Pipeline/AuditBehavior${s.ext.split(' ')[0]}`,
          line: 51,
          symbol: 'AuditBehavior.FormatPayload',
          target: 'CreateOrderResponse.ToAuditSummary',
          detail: 'Shared pipeline behavior imports type from Features/Orders/CreateOrder.',
        },
      },
      {
        id: 'SIG-VSA-R-003',
        name: 'Single-Responsibility Slice Handler Size',
        coords: 'Method x Resources',
        pattern: 'Slice Handler method exceeding 150 LOC',
        severity: 'Warning',
        description: 'Keep vertical slice handlers concise by pushing complex business invariants into pure domain methods.',
        remediation: 'Move domain state transitions onto the Domain Entity and keep the slice handler focused on I/O orchestration.',
        target: '{fn.loc} LOC',
        message: "Slice handler '{fn.name}' exceeds 150 LOC ceiling ({fn.loc} LOC)",
        query: `MATCH (fn:Function)\n  WHERE fn.file_path CONTAINS '${s.slicePrefix}' AND coalesce(fn.loc, 0) > 150\n  RETURN fn`,
        platform: stack,
        sampleViolation: {
          file: `${s.slicePrefix}Checkout/SubmitOrder/SubmitOrderHandler${s.ext.split(' ')[0]}`,
          line: 22,
          symbol: 'SubmitOrderHandler.Handle',
          target: '214 LOC',
          detail: 'Slice handler grew to 214 lines by inlining pricing and inventory allocation rules.',
        },
      },
      {
        id: 'SIG-VSA-C-004',
        name: 'No Bloated Generic Repository in Vertical Slices',
        coords: 'Class x Contract',
        pattern: 'Generic Repository class (>15 methods) shared across slices',
        severity: 'Warning',
        description: 'Vertical slices favor direct query/command access over giant shared repository classes that couple all slices.',
        remediation: 'Execute focused queries directly inside each slice or split the god-repository by aggregate.',
        target: '{methods} methods',
        message: "Shared repository '{t.name}' accumulates {methods} methods across slices",
        query: `MATCH (t:Type)-[:HAS_METHOD]->(m:Function)\n  WHERE t.name ENDS WITH 'Repository'\n  WITH t, count(m) AS methods WHERE methods > 15\n  RETURN t, methods`,
        platform: stack,
        sampleViolation: {
          file: `src/Shared/Persistence/OrderRepository${s.ext.split(' ')[0]}`,
          line: 10,
          symbol: 'OrderRepository',
          target: '28 methods',
          detail: 'OrderRepository has accumulated 28 slice-specific query methods, becoming a central coupling bottleneck.',
        },
      },
    ],
  },
  {
    slug: 'feature-sliced-design',
    category: 'structural',
    shortName: 'Feature-Sliced Design',
    fileName: 'feature-sliced-design.arch',
    title: 'Feature-Sliced Design (FSD Frontend Hierarchy)',
    tagline: 'Strict unidirectional frontend hierarchy: app -> pages -> widgets -> features -> entities -> shared.',
    coreInvariant:
      'A module on layer N in a frontend codebase may only import from layers strictly below it (N-1 .. shared), and two slices on the same layer (e.g. features/auth and features/checkout) may never cross-import each other.',
    whyItMatters: [
      'Large React, Next.js, and Vue codebases notoriously rot into circular component graphs where a "reusable" card in entities/user imports a modal from features/edit-profile, which imports a layout from widgets/header.',
      'Enforcing the FSD hierarchy guarantees that entities/ and shared/ remain 100% portable and side-effect free, while composition happens strictly upward in widgets/ and pages/.',
    ],
    howItErodes: [
      {
        title: 'Entity Component Importing a Feature Action Button',
        scenario:
          'To render an "Add to Cart" button inside a ProductCard in src/entities/product, an engineer or AI agent imports AddToCartButton from src/features/cart—violating the downward FSD rule.',
        codeExample: `// [VIOLATION: SIG-FSD-B-001] Upward import from entities/ into features/
// File: src/entities/product/ui/ProductCard.tsx
import { AddToCartButton } from '@/features/cart/ui/AddToCartButton'; // Forbidden!
// Fix: Accept an action slot (renderAction / children) and compose in widgets/ProductCatalog`,
      },
    ],
    whyGraphBeatsLinters:
      'CodeExplorer resolves TypeScript path aliases (@/features/*, ~/*), barrel re-exports (index.ts), and actual function/component invocation edges (-[:CALLS]->) alongside Layer 4 outbound API calls (:ExternalService) so domain API endpoints cannot leak into src/shared.',
    bestFit: [
      'Medium-to-large React, Next.js, Vue, or Angular applications with multiple product squads',
    ],
    avoidWhen: [
      'Small marketing sites or single-purpose micro-frontends with <20 components',
    ],
    graphLayersUsed: ['L1 :File & :Folder', 'L3 :Function-[:CALLS]->:Function', 'L4 :ExternalService'],
    layers: () => [
      {
        name: '1. app & pages (Routing, Providers, Full Route Views)',
        role: 'Top-level application shell and route pages composing widgets and features',
        paths: 'src/app/**, src/pages/**',
      },
      {
        name: '2. widgets (Composite Page Blocks)',
        role: 'Self-contained UI blocks assembling entities and interactive features',
        paths: 'src/widgets/**',
      },
      {
        name: '3. features (User Interactions & Business Actions)',
        role: 'User-facing actions with business value (AddToCart, AuthLogin, FilterOrders)',
        paths: 'src/features/**',
      },
      {
        name: '4. entities & shared (Domain Models & Reusable UI Kit)',
        role: 'Business data entities (User, Order) and domain-agnostic design system / utilities',
        paths: 'src/entities/**, src/shared/**',
        core: true,
      },
    ],
    edges: [
      { from: 'pages & widgets', to: 'features, entities, shared', allow: true, code: 'ALLOWED' },
      { from: 'entities or shared', to: 'features / widgets / pages', allow: false, code: 'SIG-FSD-B-001', gateId: 'SIG-FSD-B-001' },
      { from: 'features/SliceA', to: 'features/SliceB (Lateral)', allow: false, code: 'SIG-FSD-B-002', gateId: 'SIG-FSD-B-002' },
    ],
    gates: (_s, stack) => {
      const effStack = stack === 'agnostic' ? 'typescript' : stack;
      return [
        {
          id: 'SIG-FSD-B-001',
          name: 'No Upward Layer Dependencies in FSD',
          coords: 'Module x Boundaries',
          pattern: '(shared|entities)-[:CALLS]->(features|widgets|pages|app)',
          severity: 'Error',
          description: 'Lower FSD layers (shared, entities) must never reference higher layers (features, widgets, pages, app).',
          remediation: 'Pass higher-layer components down via React slots/children or render props from a widget or page.',
          target: '{high.file_path}',
          message: "Upward FSD layer violation: '{low.file_path}' calls '{high.file_path}'",
          query: `MATCH (low:Function)-[:CALLS]->(high:Function)\n  WHERE (low.file_path CONTAINS 'src/shared/' OR low.file_path CONTAINS 'src/entities/')\n    AND (high.file_path CONTAINS 'src/features/' OR high.file_path CONTAINS 'src/widgets/' OR high.file_path CONTAINS 'src/pages/' OR high.file_path CONTAINS 'src/app/')\n  RETURN low, high`,
          platform: effStack,
          sampleViolation: {
            file: 'src/entities/product/ui/ProductCard.tsx',
            line: 27,
            symbol: 'ProductCard',
            target: 'src/features/cart/ui/AddToCartButton.tsx',
            detail: 'Entity component ProductCard imports and renders feature component AddToCartButton.',
          },
        },
        {
          id: 'SIG-FSD-B-002',
          name: 'No Cross-Imports Within Same FSD Layer',
          coords: 'Module x Boundaries',
          pattern: '(src/features/Auth)-[:CALLS]->(src/features/Checkout)',
          severity: 'Error',
          description: 'Slices on the same FSD layer (features or entities) must not couple horizontally; compose them in widgets or pages.',
          remediation: 'Lift the shared state or composition up to a widget in src/widgets/ or down to src/entities/.',
          target: '{b.file_path}',
          message: "Horizontal FSD slice coupling between '{a.file_path}' and '{b.file_path}'",
          query: `MATCH (a:Function)-[:CALLS]->(b:Function)\n  WHERE a.file_path CONTAINS 'src/features/' AND b.file_path CONTAINS 'src/features/'\n    AND split(a.file_path, '/')[2] <> split(b.file_path, '/')[2]\n  RETURN a, b`,
          platform: effStack,
          sampleViolation: {
            file: 'src/features/checkout/model/useCheckoutSubmit.ts',
            line: 19,
            symbol: 'useCheckoutSubmit',
            target: 'src/features/promo-code/model/validatePromo.ts',
            detail: 'Feature slice features/checkout directly calls sibling feature slice features/promo-code.',
          },
        },
        {
          id: 'SIG-FSD-B-003',
          name: 'Features Must Not Depend on Widgets or Pages',
          coords: 'Module x Boundaries',
          pattern: '(src/features/*)-[:CALLS]->(src/widgets/* | src/pages/*)',
          severity: 'Error',
          description: 'Features sit below widgets and pages and must never call upward into composite page blocks.',
          remediation: 'Extract the reusable primitive into src/entities/ or src/shared/ui/.',
          target: '{up.file_path}',
          message: "Feature '{f.name}' calls upward FSD layer '{up.file_path}'",
          query: `MATCH (f:Function)-[:CALLS]->(up:Function)\n  WHERE f.file_path CONTAINS 'src/features/'\n    AND (up.file_path CONTAINS 'src/widgets/' OR up.file_path CONTAINS 'src/pages/')\n  RETURN f, up`,
          platform: effStack,
          sampleViolation: {
            file: 'src/features/search/ui/QuickSearchModal.tsx',
            line: 42,
            symbol: 'QuickSearchModal',
            target: 'src/widgets/header/model/useHeaderState.ts',
            detail: 'Feature component reaches upward into widget state hook useHeaderState.',
          },
        },
        {
          id: 'SIG-FSD-C-004',
          name: 'Shared Layer Business-Entity Agnosticism',
          coords: 'Module x Contract',
          pattern: 'src/shared/** referencing domain API endpoints directly',
          severity: 'Warning',
          description: 'src/shared should provide generic UI/lib primitives rather than domain-specific API calls.',
          remediation: 'Move domain-specific endpoint requests to src/entities/<domain>/api/ or src/features/<feature>/api/.',
          target: '{es.url}',
          message: "Domain network call '{es.name}' leaked into generic src/shared layer ('{f.path}')",
          query: `MATCH (es:ExternalService)-[:DECLARED_IN]->(f:File)\n  WHERE f.path CONTAINS 'src/shared/' AND NOT f.path CONTAINS 'src/shared/api/'\n  RETURN es, f`,
          platform: effStack,
          sampleViolation: {
            file: 'src/shared/ui/DataTable/DataTable.tsx',
            line: 88,
            symbol: 'GET /api/v1/billing/invoices',
            target: '/api/v1/billing/invoices',
            detail: 'Generic UI component in src/shared/ui fetches a domain-specific billing endpoint directly.',
          },
        },
      ];
    },
  },
  {
    slug: 'microservices-isolation',
    category: 'structural',
    shortName: 'Shared-Nothing Microservices',
    fileName: 'microservices-isolation.arch',
    title: 'Shared-Nothing Microservices (Database & Sync Cycle Guard)',
    tagline: 'Every microservice owns its database tables exclusively and avoids synchronous distributed call cycles.',
    coreInvariant:
      'Two distinct deployable services (:Service / :App) must never read or write the same database :Table, must never import each other’s source projects directly, and must never form a synchronous HTTP/gRPC call cycle.',
    whyItMatters: [
      'A "distributed monolith" is the worst of both worlds: you pay the full latency, operational complexity, and partial-failure cost of microservices, but you cannot deploy Service A without breaking Service B because both services query and mutate the same PostgreSQL tables.',
      'Furthermore, synchronous HTTP/gRPC cycles (Service A -> Service B -> Service A) or sequential outbound RPC calls inside a loop (:Scope {kind: "loop"}) cause cascading thread-pool exhaustion during traffic spikes.',
    ],
    howItErodes: [
      {
        title: 'Two Polyglot Microservices Accessing the Same SQL Table',
        scenario:
          'A Go worker and a TypeScript NestJS API sit in the same monorepo (or multi-repo workspace). Instead of calling the API or consuming a Kafka topic, the Go worker runs a direct SELECT/UPDATE against the users_ledger table.',
        codeExample: `// [VIOLATION: SIG-MS-B-001] Go worker and TS service both access table 'users_ledger'
// Go worker (services/settlement-worker/store.go):
db.ExecContext(ctx, "UPDATE users_ledger SET balance = balance - $1 WHERE user_id = $2", amt, uid)
// TS service (services/ledger-api/src/ledger.repository.ts):
await this.repo.save(UsersLedgerEntity); // Shared database anti-pattern!`,
      },
      {
        title: 'N+1 Outbound HTTP / gRPC Call Inside a Loop Scope',
        scenario:
          'To enrich a list of 100 orders with customer profiles, an endpoint iterates over the orders in a loop and awaits an outbound HTTP call on each iteration instead of calling a batch endpoint.',
        codeExample: `// [VIOLATION: SIG-MS-R-004] Outbound RPC inside a Layer 3 Loop Scope
foreach (var order in orders) // Layer 3 :Scope { kind: 'loop', loop_depth: 1 }
{
    // Sequential O(N) network waterfall across services
    order.Customer = await _customerHttpClient.GetProfileAsync(order.CustomerId);
}`,
      },
    ],
    whyGraphBeatsLinters:
      'No single-language linter can see across a Go service, a .NET service, a Python FastAPI service, and a PostgreSQL schema at the same time. CodeExplorer indexes all 12 languages into one graph, linking polyglot services to shared :Table nodes, :Topic brokers, and Layer 3 :Scope loop blocks.',
    bestFit: [
      'Polyglot microservice monorepos and multi-repo enterprise workspaces',
      'Distributed systems enforcing strict database-per-service ownership',
    ],
    avoidWhen: [
      'Single-service monoliths intentionally sharing a unified relational schema',
    ],
    graphLayersUsed: ['L3 :Scope {kind:"loop"}', 'L4 :Service, :Table & :ExternalService', 'L5 :SERVICE_CALL'],
    layers: () => [
      {
        name: '1. Edge API Gateway / BFF',
        role: 'Routes external traffic and aggregates read views via non-blocking fan-out',
        paths: 'services/gateway/**, apps/bff/**',
      },
      {
        name: '2. Autonomous Microservices (Orders, Payments, Inventory)',
        role: 'Independently deployable services with zero cross-service code imports',
        paths: 'services/*/**',
        core: true,
      },
      {
        name: '3. Async Event Broker (Kafka / RabbitMQ / PubSub Topics)',
        role: 'Decoupled event choreography and eventual consistency contracts',
        paths: 'Layer 4 :Topic contracts',
      },
      {
        name: '4. Isolated Per-Service Databases',
        role: 'Strict 1-to-1 ownership between a microservice and its SQL tables',
        paths: 'Layer 4 :Table & :Database',
      },
    ],
    edges: [
      { from: 'Service A', to: 'Broker Topic -> Service B', allow: true, code: 'ALLOWED' },
      { from: 'Service A', to: 'Service A Dedicated Tables', allow: true, code: 'ALLOWED' },
      { from: 'Service A & Service B', to: 'Same SQL Table (Shared DB)', allow: false, code: 'SIG-MS-B-001', gateId: 'SIG-MS-B-001' },
      { from: 'Service A -> Service B', to: 'Service A (Sync RPC Cycle)', allow: false, code: 'SIG-MS-B-002', gateId: 'SIG-MS-B-002' },
    ],
    gates: (_s, stack) => [
      {
        id: 'SIG-MS-B-001',
        name: 'Shared-Database Integration Anti-Pattern',
        coords: 'System x Boundaries',
        pattern: '(ServiceA) and (ServiceB) both READS_FROM or WRITES_TO (Table)',
        severity: 'Error',
        description: 'No database table may be accessed directly by more than one microservice.',
        remediation: 'Route cross-service data access through an API contract or replicate state via broker events.',
        target: '{t.name}',
        message: "Shared database coupling on table '{t.name}' between '{f1.project_name}' and '{f2.project_name}'",
        query: `MATCH (q1:Query)-[:READS_FROM|WRITES_TO]->(t:Table)<-[:READS_FROM|WRITES_TO]-(q2:Query)\n  MATCH (q1)-[:ACCESSED_BY]->(f1:Function), (q2)-[:ACCESSED_BY]->(f2:Function)\n  WHERE f1.project_name < f2.project_name\n  RETURN f1, t, f2`,
        platform: stack,
        sampleViolation: {
          file: 'services/settlement-worker/internal/store/ledger.go',
          line: 74,
          symbol: 'settlement-worker & ledger-api',
          target: 'users_ledger',
          detail: 'Table users_ledger is directly queried/mutated by both settlement-worker (Go) and ledger-api (TS).',
        },
      },
      {
        id: 'SIG-MS-B-002',
        name: 'Distributed Synchronous Call Cycle Shield',
        coords: 'System x Boundaries',
        pattern: '(SvcA)-[:SERVICE_CALL]->(SvcB)-[:SERVICE_CALL]->(SvcA)',
        severity: 'Error',
        description: 'Synchronous HTTP/gRPC service-to-service calls must never form distributed cycles.',
        remediation: 'Break the synchronous cycle by converting one direction into an asynchronous event publication.',
        target: '{b.name}',
        message: "Distributed synchronous call cycle between '{a.name}' and '{b.name}'",
        query: `MATCH (a:App)-[:SERVICE_CALL]->(b:App)-[:SERVICE_CALL]->(a)\n  WHERE a.id < b.id\n  RETURN a, b`,
        platform: stack,
        sampleViolation: {
          file: 'services/orders-api/Clients/InventoryGrpcClient.cs',
          line: 42,
          symbol: 'orders-api',
          target: 'inventory-api',
          detail: 'Synchronous RPC cycle: orders-api -> inventory-api -> orders-api.',
        },
      },
      {
        id: 'SIG-MS-B-003',
        name: 'No Direct Cross-Service Source Code Imports',
        coords: 'Service x Boundaries',
        pattern: '(ServiceA:App)-[:DEPENDS_ON]->(ServiceB:App)',
        severity: 'Error',
        description: 'Deployable microservices may depend on shared libraries, never directly on another deployable service.',
        remediation: 'Extract shared DTOs or proto contracts into a dedicated contracts library.',
        target: '{p2.name}',
        message: "Deployable service '{p1.name}' directly imports deployable service '{p2.name}'",
        query: `MATCH (s1:App)-[:IMPLEMENTED_BY]->(p1:Project)-[:DEPENDS_ON]->(p2:Project)<-[:IMPLEMENTED_BY]-(s2:App)\n  WHERE p1.id <> p2.id\n  RETURN p1, p2`,
        platform: stack,
        sampleViolation: {
          file: 'services/billing-worker/billing-worker.csproj',
          line: 1,
          symbol: 'billing-worker',
          target: 'orders-api',
          detail: 'Deployable worker billing-worker directly references executable service orders-api.',
        },
      },
      {
        id: 'SIG-MS-R-004',
        name: 'N+1 Synchronous HTTP Call Inside Loop Scope',
        coords: 'Method x Resources',
        pattern: '(Function)-[:DECLARES]->(Scope {kind:"loop"}) with outbound ExternalService call',
        severity: 'Warning',
        description: 'Avoid sequential cross-service HTTP/RPC calls inside loops; prefer batch endpoints or bounded concurrency.',
        remediation: 'Replace the per-item RPC call inside the loop with a batch endpoint request outside the loop.',
        target: '{es.name}',
        message: "Outbound RPC call '{es.name}' in '{fn.name}' executes inside a loop scope",
        query: `MATCH (fn:Function)-[:DECLARES]->(s:Scope {kind: 'loop'}),\n        (fn)-[:CALLS]->(es:ExternalService)\n  RETURN fn, s, es`,
        platform: stack,
        sampleViolation: {
          file: 'services/orders-bff/Handlers/OrderFeedHandler.cs',
          line: 68,
          symbol: 'OrderFeedHandler.EnrichOrdersAsync',
          target: 'GET http://customer-service/api/v1/profiles/{id}',
          detail: 'Sequential outbound HTTP call executed inside foreach loop (loop_depth=1, await_count=1).',
        },
      },
    ],
  },
  {
    slug: 'cqrs-read-write',
    category: 'runtime',
    shortName: 'CQRS Read/Write Separation',
    fileName: 'cqrs-read-write.arch',
    title: 'CQRS Read/Write Separation (Zero Hidden Read-Path Mutations)',
    tagline: 'Queries and HTTP GET endpoints return data without mutating SQL tables, publishing events, or invoking commands.',
    coreInvariant:
      'Every function handling a CQRS Query (-[:HANDLES_QUERY]->:Message) or serving an HTTP GET (:Endpoint {http_method: "GET"}) must be 100% side-effect free across its entire transitive call chain (*0..5 hops): zero -[:WRITES_TO]->(:Table), zero -[:PUBLISHES]->(:Topic), and zero calls to command handlers.',
    whyItMatters: [
      'CQRS promises that read paths can be safely retried, cached, routed to read replicas, or called in parallel without altering system state.',
      'In Case Study 10 across open-source CQRS repositories, CodeExplorer uncovered real production bugs where a QueryHandler (FindPublicInvoiceQuery) secretly updated a viewed timestamp in SQL, and an HTTP GET endpoint (GET /wallet/me) lazily inserted a new Wallet row—breaking read-replica routing and idempotency.',
    ],
    howItErodes: [
      {
        title: 'Lazy Auto-Creation or Audit Counter Update Inside a QueryHandler',
        scenario:
          'A developer wants to record that an invoice was viewed or auto-initialize a missing user wallet during a GET request, adding a database write 3 hops deep inside a helper called by a QueryHandler.',
        codeExample: `// [VIOLATION: SIG-CQRS-001 & SIG-CQRS-003] Found in real OSS benchmarks (Case Study 10)
public class FindPublicInvoiceQueryHandler : IQueryHandler<FindPublicInvoiceQuery, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(FindPublicInvoiceQuery query)
    {
        var invoice = await _repo.GetByTokenAsync(query.Token);
        await _repo.MarkViewedAsync(invoice.Id); // Transitive WRITES_TO table 'invoices'!
        return invoice.ToDto();
    }
}`,
      },
    ],
    whyGraphBeatsLinters:
      'ArchUnit and NetArchTest can check whether a class named *QueryHandler* resides in the Queries namespace, but they cannot trace 4 hops through DI interfaces to see if a helper method executes an EF Core SaveChanges, TypeORM save(), or SQL INSERT/UPDATE/DELETE. CodeExplorer’s transitive graph query catches it in <5 ms.',
    bestFit: [
      'MediatR (.NET), @nestjs/cqrs (TypeScript), Spring CQRS, and Axon systems',
      'APIs routing GET queries to PostgreSQL/MySQL read replicas or HTTP caches',
    ],
    avoidWhen: [
      'Legacy RPC APIs where GET endpoints intentionally mutate state and cannot be refactored yet (use ratchet baseline first)',
    ],
    graphLayersUsed: ['L3 :Function-[:CALLS*0..5]->:Function', 'L4 :Endpoint, :Message & :Table', 'L5 :HANDLES_QUERY & :HANDLES_COMMAND'],
    layers: () => [
      {
        name: '1. Query Ingress (HTTP GET Endpoints & CQRS Query Messages)',
        role: 'Safe, idempotent read contracts (:Endpoint {http_method:"GET"}, :Message {sub_kind:"query"})',
        paths: 'Layer 4 :Endpoint & :Message',
        core: true,
      },
      {
        name: '2. Query Handlers (-[:HANDLES_QUERY]->)',
        role: 'Read-model projections and Dapper/SQL/ORM SELECT queries',
        paths: '**/Queries/**, **/*QueryHandler*',
        core: true,
      },
      {
        name: '3. Command Handlers (-[:HANDLES_COMMAND]->)',
        role: 'Domain state mutations, transactional writes, and event publications',
        paths: '**/Commands/**, **/*CommandHandler*',
      },
      {
        name: '4. Storage & Broker Sinks (:Table & :Topic)',
        role: 'READS_FROM allowed on query path; WRITES_TO and PUBLISHES strictly blocked',
        paths: 'Layer 4 :Table & :Topic',
      },
    ],
    edges: [
      { from: 'QueryHandler / GET', to: '-[:READS_FROM]->(:Table)', allow: true, code: 'ALLOWED' },
      { from: 'CommandHandler / POST', to: '-[:WRITES_TO]->(:Table)', allow: true, code: 'ALLOWED' },
      { from: 'QueryHandler', to: '-[:CALLS*0..4]->()-[:WRITES_TO]->(:Table)', allow: false, code: 'SIG-CQRS-001', gateId: 'SIG-CQRS-001' },
      { from: 'QueryHandler', to: '-[:CALLS*1..3]->(CommandHandler)', allow: false, code: 'SIG-CQRS-002', gateId: 'SIG-CQRS-002' },
      { from: 'HTTP GET Endpoint', to: '-[:CALLS*0..5]->()-[:WRITES_TO]->(:Table)', allow: false, code: 'SIG-CQRS-003', gateId: 'SIG-CQRS-003' },
    ],
    gates: (_s, stack) => [
      {
        id: 'SIG-CQRS-001',
        name: 'CQRS - Query Handlers Must Be Side-Effect Free',
        coords: 'Method x Responsibility',
        pattern: '(q:Function)-[:HANDLES_QUERY]->(msg), (q)-[:CALLS*0..4]->(w)-[:WRITES_TO|PUBLISHES]->(target)',
        severity: 'Error',
        description: 'In CQRS, query handlers must only return data and never mutate database tables or publish events.',
        remediation: 'Move database writes and event publishing out of the query path into an explicit Command Handler.',
        target: '{w.name} -> {target.name}',
        message: "Query handler '{q.name}' ({msg.name}) triggers state mutation on '{target.name}' via '{w.name}'",
        query: `MATCH (q:Function)-[:HANDLES_QUERY]->(msg:Message),\n        (q)-[:CALLS*0..4]->(w:Function)-[r:WRITES_TO|PUBLISHES]->(target)\n  WHERE NOT q.is_test AND NOT w.is_test\n  RETURN DISTINCT q, msg, w, r, target`,
        platform: stack,
        sampleViolation: {
          file: 'packages/core/src/invoice/commands/handlers/find-public-invoice.handler.ts',
          line: 46,
          symbol: 'FindPublicInvoiceHandler.execute (FindPublicInvoiceQuery)',
          target: 'InvoiceService.markViewed -> table "invoice"',
          detail: 'QueryHandler mutates table "invoice" 2 hops deep via InvoiceService.markViewed.',
        },
      },
      {
        id: 'SIG-CQRS-002',
        name: 'CQRS - Queries Must Not Invoke Command Handlers',
        coords: 'Method x Boundaries',
        pattern: '(q:Function)-[:HANDLES_QUERY]->(), (q)-[:CALLS*1..3]->(c:Function)-[:HANDLES_COMMAND]->()',
        severity: 'Error',
        description: 'Query handlers must remain strictly separated from command execution paths and never dispatch or invoke command handlers.',
        remediation: 'Decouple the read model from command execution; dispatch commands explicitly from the workflow caller.',
        target: '{q.name} -> {c.name}',
        message: "Query handler '{q.name}' ({qmsg.name}) invokes command handler '{c.name}' ({cmd.name})",
        query: `MATCH (q:Function)-[:HANDLES_QUERY]->(qmsg:Message),\n        (q)-[:CALLS*1..3]->(c:Function)-[:HANDLES_COMMAND]->(cmd:Message)\n  WHERE NOT q.is_test AND NOT c.is_test\n  RETURN DISTINCT q, qmsg, c, cmd`,
        platform: stack,
        sampleViolation: {
          file: 'src/Modules/Wallet/Queries/GetWalletQueryHandler.cs',
          line: 39,
          symbol: 'GetWalletQueryHandler.Handle',
          target: 'EnsureDefaultWalletCommandHandler.Handle',
          detail: 'Query handler dispatches EnsureDefaultWalletCommand when wallet record is missing.',
        },
      },
      {
        id: 'SIG-CQRS-003',
        name: 'CQRS - HTTP GET Endpoints Must Be Read-Only',
        coords: 'Method x Boundaries',
        pattern: '(ep:Endpoint {http_method:"GET"})-[:TRIGGERS]->(fn)-[:CALLS*0..5]->(w)-[:WRITES_TO]->(target)',
        severity: 'Error',
        description: 'HTTP GET endpoints represent queries and must be safe and idempotent without mutating database tables or publishing events.',
        remediation: 'Route state-changing operations through POST/PUT/PATCH/DELETE command endpoints.',
        target: '{ep.name} -> {w.name} -> {target.name}',
        message: "HTTP GET endpoint '{ep.name}' handled by '{fn.name}' mutates '{target.name}' via '{w.name}'",
        query: `MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)\n        -[:CALLS*0..5]->(w:Function)-[r:WRITES_TO|PUBLISHES]->(target)\n  WHERE ep.http_method = 'GET' AND NOT fn.is_test AND NOT w.is_test\n  RETURN DISTINCT fn, ep, w, r, target`,
        platform: stack,
        sampleViolation: {
          file: 'src/Api/Endpoints/WalletEndpoints.cs',
          line: 28,
          symbol: 'GET:/api/v1/wallet/me',
          target: 'WalletService.GetOrCreateAsync -> Wallets.Add',
          detail: 'HTTP GET endpoint triggers SQL INSERT on table "Wallets" 3 hops downstream.',
        },
      },
    ],
  },
  {
    slug: 'scope-and-async-cost',
    category: 'runtime',
    shortName: 'Control Scopes & Async Cost',
    fileName: 'scope-and-async-cost.arch',
    title: 'Control Scopes, Async-in-Loop Waterfalls & Resource Safety',
    tagline: 'Use Layer 3 Scope trees to block N+1 sequential awaits in loops, defer/using leaks in loops, and await inside locks.',
    coreInvariant:
      'Every function’s nested control-flow and resource blocks are indexed as Layer 3 :Scope nodes (kind: try, catch, finally, loop, branch, resource, lock, unsafe, task_spawn) with depth, loop_depth, await_count, and cognitive_complexity.',
    whyItMatters: [
      'A function can pass every typechecker and unit test on a 3-row fixture, then bring down production on 5,000 items because an await expression or Go defer resource cleanup sits inside a nested loop scope (loop_depth >= 1).',
      'Similarly, awaiting an asynchronous operation inside a synchronization lock (:Scope {kind: "lock", await_count: > 0}) or accumulating a 6-level nested pyramid of loops and branches (cognitive_complexity > 25) creates latent deadlocks and unmaintainable control flow.',
    ],
    howItErodes: [
      {
        title: 'Sequential Await Waterfall Inside a Loop (O(N) Latency)',
        scenario:
          'An AI agent writes a clean-looking for/foreach loop that awaits a database query or HTTP call on every iteration instead of batching ids or using Task.WhenAll / Promise.all.',
        codeExample: `// [VIOLATION: SIG-SCOPE-001] Layer 3 :Scope { kind: 'loop', loop_depth: 1, await_count: 2 }
public async Task SyncInvoicesAsync(IEnumerable<Guid> ids)
{
    foreach (var id in ids) // :Scope (loop)
    {
        var inv = await _db.LoadInvoiceAsync(id);   // await #1 inside loop
        await _erpClient.PushInvoiceAsync(inv);     // await #2 inside loop
    }
}`,
      },
      {
        title: 'Deferred Resource Cleanup Inside a Loop or Await Inside a Lock',
        scenario:
          'In Go, placing defer file.Close() or defer rows.Close() inside a for loop delays cleanup until the outer function exits, exhausting file descriptors or DB connections.',
        codeExample: `// [VIOLATION: SIG-SCOPE-003] :Scope { kind: 'resource', target_type: 'defer', loop_depth: 1 }
for _, path := range files {
    f, err := os.Open(path)
    if err != nil { continue }
    defer f.Close() // Descriptor leak until function exit!
}`,
      },
    ],
    whyGraphBeatsLinters:
      'CodeExplorer’s Layer 3 Scope Tree Enricher builds the full hierarchy (Function)-[:DECLARES]->(Scope)-[:CONTAINS*]->(Scope) across all 12 languages and computes exact loop_depth, await_count, cyclomatic_complexity, and cognitive_complexity on every function and scope block.',
    bestFit: [
      'High-throughput C#, TypeScript, Go, Python, Rust, and Java backend services',
      'Preventing AI-generated N+1 async waterfalls and resource exhaustion in PR checks',
    ],
    avoidWhen: [
      'CLI migration scripts where sequential 1-by-1 throttling inside a loop is intentional',
    ],
    graphLayersUsed: ['L3 :Function (cognitive_complexity, loop_await_count)', 'L3 :Scope (kind, loop_depth, await_count)'],
    layers: () => [
      {
        name: '1. Function Entry (:Function)',
        role: 'Aggregates cyclomatic_complexity, cognitive_complexity, max_nesting_depth, loop_await_count',
        paths: 'Layer 3 :Function',
        core: true,
      },
      {
        name: '2. Loop & Branch Scopes (:Scope {kind: "loop" | "branch"})',
        role: 'Tracks nesting depth, loop_depth, and sequential await_count inside iteration bodies',
        paths: 'for, foreach, while, .map(async ...)',
      },
      {
        name: '3. Resource & Lock Scopes (:Scope {kind: "resource" | "lock"})',
        role: 'Tracks using, await using, Go defer, Python with, lock, and synchronized blocks',
        paths: 'using, defer, with, lock',
      },
      {
        name: '4. Try / Catch / Finally Scopes (:Scope {kind: "try" | "catch" | "finally"})',
        role: 'Tracks exception target_type, is_empty, has_comment, and statement_count',
        paths: 'try, catch, except, finally',
      },
    ],
    edges: [
      { from: 'Function', to: '-[:DECLARES]->(:Scope {loop_depth: 0})', allow: true, code: 'ALLOWED' },
      { from: 'Loop Scope (loop_depth > 0)', to: 'Sequential await (await_count > 0)', allow: false, code: 'SIG-SCOPE-001', gateId: 'SIG-SCOPE-001' },
      { from: 'Function', to: 'cognitive_complexity > 25', allow: false, code: 'SIG-SCOPE-002', gateId: 'SIG-SCOPE-002' },
      { from: 'Loop / Lock Scope', to: 'defer/using in loop OR await in lock', allow: false, code: 'SIG-SCOPE-003', gateId: 'SIG-SCOPE-003' },
    ],
    gates: (_s, stack) => [
      {
        id: 'SIG-SCOPE-001',
        name: 'Performance - No Sequential Await Inside Loops',
        coords: 'Method x Resources',
        pattern: '(fn:Function)-[:DECLARES|CONTAINS*1..6]->(s:Scope) WHERE s.loop_depth > 0 AND s.await_count > 0',
        severity: 'Warning',
        description: 'Sequential await expressions inside a loop execute I/O round-trips one-by-one (O(N) latency waterfall) instead of batching.',
        remediation: 'Batch the query/request outside the loop or use bounded concurrency (Task.WhenAll / Promise.all).',
        target: 'loop@{s.line} (depth={s.loop_depth}, awaits={s.await_count})',
        message: "Function '{fn.name}' executes {s.await_count} sequential await(s) inside a loop at line {s.line}",
        query: `MATCH (fn:Function)-[:DECLARES|CONTAINS*1..6]->(s:Scope)\n  WHERE NOT coalesce(fn.is_test, false)\n    AND s.loop_depth > 0\n    AND s.await_count > 0\n  RETURN DISTINCT fn, s`,
        platform: stack,
        sampleViolation: {
          file: 'src/Billing/Services/InvoiceSyncService.cs',
          line: 88,
          symbol: 'InvoiceSyncService.SyncBatchAsync',
          target: 'loop@88 (depth=1, awaits=2)',
          detail: 'Function executes 2 sequential awaits per iteration inside foreach loop (loop_await_count=2).',
        },
      },
      {
        id: 'SIG-SCOPE-002',
        name: 'Maintainability - Cognitive & Cyclomatic Complexity Ceiling',
        coords: 'Method x Resources',
        pattern: '(fn:Function) WHERE fn.cognitive_complexity > 25 OR fn.cyclomatic_complexity > 15',
        severity: 'Warning',
        description: 'Functions with high depth-weighted cognitive complexity (> 25) or cyclomatic complexity (> 15) are error-prone and hard to test.',
        remediation: 'Extract nested loop/branch/try-catch scopes into focused helper methods or use early guard clauses.',
        target: 'cognitive={fn.cognitive_complexity}, cyclomatic={fn.cyclomatic_complexity}, depth={fn.max_nesting_depth}',
        message: "Function '{fn.name}' exceeds complexity ceiling (cognitive={fn.cognitive_complexity}, cyclomatic={fn.cyclomatic_complexity})",
        query: `MATCH (fn:Function)\n  WHERE NOT coalesce(fn.is_test, false)\n    AND fn.name <> '<module_init>'\n    AND (coalesce(fn.cognitive_complexity, 0) > 25 OR coalesce(fn.cyclomatic_complexity, 0) > 15)\n  RETURN fn`,
        platform: stack,
        sampleViolation: {
          file: 'src/Orders/Domain/DiscountRuleEvaluator.cs',
          line: 114,
          symbol: 'DiscountRuleEvaluator.EvaluateCampaignTree',
          target: 'cognitive=34, cyclomatic=18, depth=5',
          detail: 'Deeply nested 5-level loop/branch pyramid exceeds cognitive complexity ceiling (34 > 25).',
        },
      },
      {
        id: 'SIG-SCOPE-003',
        name: 'Reliability - No Resource Leak in Loop or Await in Lock',
        coords: 'Method x Resources',
        pattern: '(s:Scope) WHERE (s.kind = "resource" AND s.loop_depth > 0) OR (s.kind = "lock" AND s.await_count > 0)',
        severity: 'Error',
        description: 'Allocating deferred/disposable resources inside a loop or awaiting inside a lock scope causes descriptor exhaustion or deadlocks.',
        remediation: 'Move resource acquisition outside the loop (or extract a helper function for Go defer) and never await inside a lock.',
        target: '{s.kind}@{s.line} ({s.target_type})',
        message: "Function '{fn.name}' declares '{s.kind}' scope ({s.target_type}) at line {s.line} with loop_depth={s.loop_depth}, await_count={s.await_count}",
        query: `MATCH (fn:Function)-[:DECLARES|CONTAINS*1..6]->(s:Scope)\n  WHERE NOT coalesce(fn.is_test, false)\n    AND ((s.kind = 'resource' AND s.loop_depth > 0)\n      OR (s.kind = 'lock' AND s.await_count > 0))\n  RETURN DISTINCT fn, s`,
        platform: stack,
        sampleViolation: {
          file: 'internal/importer/archive_reader.go',
          line: 63,
          symbol: 'ArchiveReader.ImportAll',
          target: 'resource@63 (defer)',
          detail: 'Go defer statement declared inside for loop (loop_depth=1) delays file descriptor close until function exit.',
        },
      },
    ],
  },
  {
    slug: 'transactions-outbox',
    category: 'runtime',
    shortName: 'Transactions & Outbox',
    fileName: 'transactions-outbox.arch',
    title: 'Database Transactions & Transactional Outbox Safety',
    tagline: 'Prevent external HTTP calls inside open DB transactions, multi-table partial writes, and dual-write broker races.',
    coreInvariant:
      'Database transactions (:Transaction) must never span outbound network I/O (:ExternalService), commands mutating 2+ distinct tables must execute inside a transaction boundary, and functions mutating a SQL table must not publish directly to a message broker (:Topic) without an Outbox table.',
    whyItMatters: [
      'Calling an external HTTP API (Stripe, SendGrid, internal microservice) while holding an open SQL transaction keeps row/table locks open across 5–30 second network timeouts, exhausting the database connection pool in seconds.',
      'Conversely, writing to a database table and publishing directly to Kafka/RabbitMQ in the same method (Dual Write) guarantees data inconsistency whenever the broker publish fails after commit or the DB commit fails after publish.',
    ],
    howItErodes: [
      {
        title: 'Outbound HTTP Call Inside an Open Database Transaction',
        scenario:
          'Inside a BeginTransactionAsync scope (or [Transactional] method), a handler updates an order row and then calls an external notification or payment webhook before committing.',
        codeExample: `// [VIOLATION: SIG-TX-001] Function executes in :Transaction AND calls :ExternalService
await using var tx = await _db.Database.BeginTransactionAsync();
await _db.Orders.Where(o => o.Id == id).ExecuteUpdateAsync(...);
// Holds PostgreSQL row locks while waiting on external HTTP response!
await _httpClient.PostAsJsonAsync("https://hooks.partner.io/notify", payload);
await tx.CommitAsync();`,
      },
    ],
    whyGraphBeatsLinters:
      'CodeExplorer models Layer 4 :Transaction boundaries (-[:EXECUTES_IN_TX]->), :Table mutations (-[:WRITES_TO]->), :Topic publications (-[:PUBLISHES_TO]->), and :ExternalService calls in the same graph, making cross-cutting transaction hazards trivial to query.',
    bestFit: [
      'Financial, e-commerce, billing, and event-driven systems requiring atomic state transitions',
    ],
    avoidWhen: [
      'Read-only analytics services or stateless proxy gateways with no transactional storage',
    ],
    graphLayersUsed: ['L4 :Transaction', 'L4 :Table & :Topic', 'L4 :ExternalService'],
    layers: () => [
      {
        name: '1. Command Handler / Use Case',
        role: 'Opens transaction boundary and coordinates domain aggregate updates',
        paths: 'Layer 3 :Function-[:HANDLES_COMMAND]->:Message',
        core: true,
      },
      {
        name: '2. Active Database Transaction (:Transaction)',
        role: 'Wraps multi-table SQL mutations + Outbox table insert atomically',
        paths: 'Layer 4 :Transaction (-[:EXECUTES_IN_TX]->)',
        core: true,
      },
      {
        name: '3. Transactional Outbox Relay Worker',
        role: 'Polls or streams outbox_messages table after commit and publishes to :Topic',
        paths: 'Layer 4 :Job / :Worker',
      },
      {
        name: '4. External HTTP Services & Message Brokers (:ExternalService, :Topic)',
        role: 'Must be invoked outside the DB lock window or via Outbox relay',
        paths: 'Layer 4 :ExternalService & :Topic',
      },
    ],
    edges: [
      { from: 'Transaction Scope', to: 'Multi-Table SQL Writes + Outbox Insert', allow: true, code: 'ALLOWED' },
      { from: 'Transaction Scope', to: 'Outbound HTTP / gRPC Call', allow: false, code: 'SIG-TX-001', gateId: 'SIG-TX-001' },
      { from: 'Command Handler (2+ Tables)', to: 'No Transaction Boundary', allow: false, code: 'SIG-TX-002', gateId: 'SIG-TX-002' },
      { from: 'DB Table Write', to: 'Direct Broker Publish (No Outbox)', allow: false, code: 'SIG-TX-003', gateId: 'SIG-TX-003' },
    ],
    gates: (_s, stack) => [
      {
        id: 'SIG-TX-001',
        name: 'Transactions - No External Network IO Inside DB Transaction',
        coords: 'Method x Boundaries',
        pattern: '(fn:Function)-[:EXECUTES_IN_TX]->(tx:Transaction), (fn)-[:CALLS]->(es:ExternalService)',
        severity: 'Error',
        description: 'Invoking external HTTP/gRPC services inside an open database transaction holds row locks across network timeouts.',
        remediation: 'Move outbound network calls outside the transaction scope or decouple them via a transactional Outbox event.',
        target: '{tx.name} -> {es.name}',
        message: "Function '{fn.name}' inside transaction '{tx.name}' calls external service '{es.name}'",
        query: `MATCH (fn:Function)-[:EXECUTES_IN_TX]->(tx:Transaction),\n        (fn)-[:CALLS|CALLED_BY|ACCESSED_BY]-(es:ExternalService)\n  WHERE NOT fn.is_test\n  RETURN DISTINCT fn, tx, es`,
        platform: stack,
        sampleViolation: {
          file: 'src/Payments/Handlers/CapturePaymentCommandHandler.cs',
          line: 72,
          symbol: 'CapturePaymentCommandHandler.Handle',
          target: 'tx:BeginTransactionAsync -> POST https://api.stripe.com/v1/charges',
          detail: 'Outbound Stripe HTTP call executes while database transaction is open.',
        },
      },
      {
        id: 'SIG-TX-002',
        name: 'Transactions - Multi-Table Write Without Transaction Boundary',
        coords: 'Method x Responsibility',
        pattern: '(cmd:Function)-[:HANDLES_COMMAND]->(), count(DISTINCT tbl written) >= 2 without :Transaction',
        severity: 'Warning',
        description: 'Command handlers writing to multiple distinct tables without a transaction boundary leave partial state on mid-flight failures.',
        remediation: 'Wrap multi-table command execution in an explicit database transaction or transactional pipeline behavior.',
        target: '{cmd.name} ({tableCount} tables)',
        message: "Command handler '{cmd.name}' ({m.name}) writes to {tableCount} distinct tables outside an active transaction",
        query: `MATCH (cmd:Function)-[:HANDLES_COMMAND]->(m:Message),\n        (cmd)-[:CALLS*0..4]->(w:Function)-[:WRITES_TO]->(tbl:Table)\n  WHERE NOT cmd.is_test AND NOT w.is_test\n    AND NOT EXISTS((cmd)-[:EXECUTES_IN_TX]->(:Transaction))\n  WITH cmd, m, count(DISTINCT tbl) AS tableCount\n  WHERE tableCount >= 2\n  RETURN cmd, m, tableCount`,
        platform: stack,
        sampleViolation: {
          file: 'src/Orders/Commands/TransferSubscriptionHandler.cs',
          line: 44,
          symbol: 'TransferSubscriptionHandler.Handle',
          target: 'TransferSubscriptionHandler (3 tables)',
          detail: 'Command mutates subscriptions,member_seats,audit_entries without a transaction boundary.',
        },
      },
      {
        id: 'SIG-TX-003',
        name: 'Transactions - Dual Write to DB and Broker Without Outbox',
        coords: 'Method x Responsibility',
        pattern: '(fn:Function)-[:WRITES_TO]->(tbl:Table) AND (fn)-[:PUBLISHES_TO]->(top:Topic) without outbox',
        severity: 'Warning',
        description: 'Writing to a database table and publishing directly to a message broker risks lost or phantom messages.',
        remediation: 'Persist outgoing integration events to a transactional Outbox table in the same DB transaction.',
        target: '{tbl.name} + {top.name}',
        message: "Function '{fn.name}' performs dual write to table '{tbl.name}' and broker topic '{top.name}' without Outbox",
        query: `MATCH (fn:Function)-[:WRITES_TO]->(tbl:Table),\n        (fn)-[:PUBLISHES|PUBLISHES_TO|PUBLISHED_BY]-(top:Topic)\n  WHERE NOT fn.is_test\n    AND NOT toLower(tbl.name) CONTAINS 'outbox'\n  RETURN DISTINCT fn, tbl, top`,
        platform: stack,
        sampleViolation: {
          file: 'src/Orders/Services/OrderFulfillmentService.cs',
          line: 105,
          symbol: 'OrderFulfillmentService.CompleteOrderAsync',
          target: 'orders + topic:order.completed',
          detail: 'Function writes to SQL table "orders" and publishes directly to Kafka topic "order.completed".',
        },
      },
    ],
  },
  {
    slug: 'endpoint-security',
    category: 'runtime',
    shortName: 'Zero-Trust Endpoint Security',
    fileName: 'endpoint-security.arch',
    title: 'Zero-Trust Endpoint & Data Lineage Security Gates',
    tagline: 'Trace every HTTP/GraphQL/gRPC route to the SQL tables it touches and verify authorization policies and fail-closed guards.',
    coreInvariant:
      'Every state-mutating endpoint (POST/PUT/PATCH/DELETE) and every endpoint reaching sensitive tables (:Table {sensitivity: "pii"|"pci"|"secret"}) must be guarded by an explicit Layer 4 :Policy, privileged routes (/admin, /internal, /billing) must require role/permission policies, and authorization guards must never contain empty catch blocks.',
    whyItMatters: [
      'Most authorization bugs happen when a new endpoint or GraphQL mutation is added and the developer (or AI agent) forgets [Authorize] / @UseGuards, or uses a generic logged-in guard on an /admin or /billing endpoint that requires an explicit role.',
      'By combining Layer 4 :Endpoint, :Policy, transitive call chains (-[:CALLS*0..4]->), :Table sensitivity, and Layer 3 :Scope {kind: "catch"}, CodeExplorer audits authorization across the full request-to-database path.',
    ],
    howItErodes: [
      {
        title: 'Mutating Endpoint Reaching a Database Table Without a Policy Guard',
        scenario:
          'A new controller or FastAPI/NestJS route is wired up to a command handler that writes to a SQL table, but no authentication or role guard decorator is attached.',
        codeExample: `// [VIOLATION: SIG-SEC-001] POST endpoint mutates 'user_api_keys' table with no :Policy node
[HttpPost("api/v1/keys/rotate")] // Missing [Authorize] policy!
public async Task<IActionResult> RotateKey(RotateKeyCommand cmd)
    => Ok(await _mediator.Send(cmd)); // Transitively WRITES_TO table 'user_api_keys'`,
      },
      {
        title: 'Silent Exception Swallowing Inside an Auth Guard (:Scope {kind: "catch", is_empty: true})',
        scenario:
          'An authorization handler catches a token-parsing or permission-lookup exception with an empty catch block, inadvertently allowing execution to continue.',
        codeExample: `// [VIOLATION: SIG-SEC-005] Empty catch Scope inside Policy enforcement function
try {
    await _permissionVerifier.EnsureTenantAdminAsync(ctx.User);
} catch (Exception) {
    // Empty catch inside auth guard risks fail-open bypass!
}`,
      },
    ],
    whyGraphBeatsLinters:
      'A syntactic linter looking at an endpoint cannot tell whether that endpoint is a harmless healthcheck or whether 3 hops down the call chain it mutates a PII/billing database table. CodeExplorer joins :Endpoint, :Policy, transitive :Function calls, :Table sensitivity, and :Scope catch blocks in one query.',
    bestFit: [
      'Multi-tenant SaaS backends, fintech/healthcare APIs, and public REST/GraphQL gateways',
    ],
    avoidWhen: [
      'Internal air-gapped CLI utilities with no HTTP/gRPC endpoints',
    ],
    graphLayersUsed: ['L3 :Scope {kind:"catch"}', 'L4 :Endpoint & :Policy', 'L4 :Table (sensitivity)'],
    layers: () => [
      {
        name: '1. Exposed API Endpoints (:Endpoint)',
        role: 'REST, GraphQL, gRPC, and WebSocket routes with http_method and route_template',
        paths: 'Layer 4 :Endpoint',
      },
      {
        name: '2. Authorization Policies & Guards (:Policy)',
        role: 'Role, permission, authenticated, and custom_guard policies (-[:GUARDED_BY]->, -[:ENFORCED_BY]->)',
        paths: 'Layer 4 :Policy',
        core: true,
      },
      {
        name: '3. Guard Enforcement Functions & Catch Scopes (:Function -> :Scope)',
        role: 'Must fail closed on exceptions (zero empty catch scopes)',
        paths: 'Layer 3 :Function & :Scope {kind:"catch"}',
        core: true,
      },
      {
        name: '4. Sensitive & Mutable SQL Tables (:Table)',
        role: 'Classified tables (pii, pci, secret) and write sinks (-[:WRITES_TO]->)',
        paths: 'Layer 4 :Table',
      },
    ],
    edges: [
      { from: 'Mutating Endpoint', to: '-[:GUARDED_BY]->(:Policy)', allow: true, code: 'ALLOWED' },
      { from: 'Mutating Endpoint (No Policy)', to: '-[:CALLS*0..4]->()-[:WRITES_TO]->(:Table)', allow: false, code: 'SIG-SEC-001', gateId: 'SIG-SEC-001' },
      { from: 'Anonymous Endpoint', to: 'Sensitive PII/PCI/Secret Table', allow: false, code: 'SIG-SEC-002', gateId: 'SIG-SEC-002' },
      { from: 'Privileged /admin or /billing Route', to: 'Missing Role/Permission Policy', allow: false, code: 'SIG-SEC-003', gateId: 'SIG-SEC-003' },
      { from: 'Policy Guard Function', to: 'Empty Catch Scope (is_empty: true)', allow: false, code: 'SIG-SEC-005', gateId: 'SIG-SEC-005' },
    ],
    gates: (_s, stack) => [
      {
        id: 'SIG-SEC-001',
        name: 'Security - Unguarded Mutating Endpoint',
        coords: 'Method x Boundaries',
        pattern: '(ep:Endpoint {POST/PUT/PATCH/DELETE})-[:CALLS*0..4]->(w)-[:WRITES_TO]->(tbl) without :Policy',
        severity: 'Error',
        description: 'State-mutating HTTP endpoints (POST, PUT, PATCH, DELETE) must declare an authorization Policy or explicit public marker.',
        remediation: 'Attach [Authorize], @UseGuards, @PreAuthorize, or an explicit [AllowAnonymous] / @Public policy.',
        target: '{ep.name} -> {tbl.name}',
        message: "Mutating endpoint '{ep.name}' writes to '{tbl.name}' via '{w.name}' without a GUARDED_BY authorization policy",
        query: `MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)\n        -[:CALLS*0..4]->(w:Function)-[:WRITES_TO]->(tbl:Table)\n  WHERE ep.http_method IN ['POST', 'PUT', 'PATCH', 'DELETE']\n    AND NOT fn.is_test AND NOT w.is_test\n    AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy))\n  RETURN DISTINCT fn, ep, w, tbl`,
        platform: stack,
        sampleViolation: {
          file: 'src/Api/Controllers/ApiKeysController.cs',
          line: 49,
          symbol: 'POST:/api/v1/keys/rotate',
          target: 'POST:/api/v1/keys/rotate -> user_api_keys',
          detail: 'Mutating POST endpoint writes to table "user_api_keys" without a GUARDED_BY Policy.',
        },
      },
      {
        id: 'SIG-SEC-002',
        name: 'Security - Unprotected Access to Sensitive (PII/PCI/Secret) Tables',
        coords: 'Method x Boundaries',
        pattern: '(ep:Endpoint)-[:CALLS*0..4]->(acc)-[:READS_FROM|WRITES_TO]->(tbl {sensitivity: pii|pci|secret})',
        severity: 'Warning',
        description: 'Endpoints accessing tables classified as pii, pci, or secret must be protected by a non-anonymous authorization Policy.',
        remediation: 'Protect the endpoint with an authenticated, role, permission, or custom_guard Policy.',
        target: '{ep.name} -> {tbl.name} ({tbl.sensitivity})',
        message: "Unprotected or anonymous endpoint '{ep.name}' accesses sensitive table '{tbl.name}' ({tbl.sensitivity})",
        query: `MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)\n        -[:CALLS*0..4]->(acc:Function)-[:READS_FROM|WRITES_TO]->(tbl:Table)\n  WHERE tbl.sensitivity IN ['pii', 'pci', 'secret']\n    AND NOT fn.is_test AND NOT acc.is_test\n    AND (NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy))\n         OR EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'anonymous'})))\n  RETURN DISTINCT fn, ep, acc, tbl`,
        platform: stack,
        sampleViolation: {
          file: 'src/Users/Controllers/ProfileExportController.cs',
          line: 33,
          symbol: 'GET:/api/v1/users/export',
          target: 'GET:/api/v1/users/export -> customer_kyc_documents (pii)',
          detail: 'Unguarded endpoint reads PII-classified table "customer_kyc_documents".',
        },
      },
      {
        id: 'SIG-SEC-003',
        name: 'Security - Privileged Route Without Role or Permission Policy',
        coords: 'Method x Boundaries',
        pattern: '(ep:Endpoint {/admin|/internal|/billing}) without :Policy {sub_kind: role|permission|custom_guard}',
        severity: 'Error',
        description: 'Privileged routes (/admin, /internal, /billing, /roles, /permissions) must enforce role, permission, or custom_guard authorization.',
        remediation: 'Add a role or permission policy (e.g. [Authorize(Roles = "Admin")], @Roles, @PreAuthorize).',
        target: '{ep.name}',
        message: "Privileged endpoint '{ep.name}' in '{ep.file_path}' lacks a role, permission, or custom_guard Policy",
        query: `MATCH (ep:Endpoint)\n  WHERE (toLower(ep.route_template) CONTAINS '/admin'\n      OR toLower(ep.route_template) CONTAINS '/internal'\n      OR toLower(ep.route_template) CONTAINS '/billing'\n      OR toLower(ep.route_template) CONTAINS '/roles'\n      OR toLower(ep.route_template) CONTAINS '/permissions')\n    AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'role'}))\n    AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'permission'}))\n    AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'custom_guard'}))\n  RETURN DISTINCT ep`,
        platform: stack,
        sampleViolation: {
          file: 'src/Billing/Controllers/AdminBillingController.cs',
          line: 61,
          symbol: 'POST:/api/v1/admin/billing/credit',
          target: 'POST:/api/v1/admin/billing/credit',
          detail: 'Privileged /admin/billing route only requires basic login instead of a role or permission policy.',
        },
      },
      {
        id: 'SIG-SEC-005',
        name: 'Security - Silent Exception Swallowing in Auth Guard',
        coords: 'Method x Implementation',
        pattern: '(pol:Policy)-[:ENFORCED_BY]->(fn:Function)-[:DECLARES|CONTAINS*1..4]->(c:Scope {kind:"catch", is_empty:true})',
        severity: 'Error',
        description: 'Empty catch blocks inside authorization guard or requirement handler functions can silently fail open and bypass access control.',
        remediation: 'Explicitly deny access (return false / throw Unauthorized) and log the failure inside the catch block.',
        target: '{pol.name} -> {fn.name}',
        message: "Authorization guard '{fn.name}' enforcing policy '{pol.name}' contains an empty catch block at line {c.line}",
        query: `MATCH (pol:Policy)-[:ENFORCED_BY]->(fn:Function)-[:DECLARES|CONTAINS*1..4]->(c:Scope)\n  WHERE c.kind = 'catch' AND c.is_empty = true AND NOT fn.is_test\n  RETURN DISTINCT fn, pol, c`,
        platform: stack,
        sampleViolation: {
          file: 'src/Auth/Guards/TenantAdminGuard.cs',
          line: 44,
          symbol: 'TenantAdminGuard.CanActivateAsync',
          target: 'Policy:TenantAdmin -> TenantAdminGuard.CanActivateAsync',
          detail: 'Empty catch Scope at line 44 inside authorization guard can silently fail open.',
        },
      },
    ],
  },
];
