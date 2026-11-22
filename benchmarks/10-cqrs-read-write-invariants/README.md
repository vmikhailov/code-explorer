# Case Study 10: Verifying CQRS Read/Write Separation Across Open-Source Codebases

Automated architectural verification study evaluating 3 built-in **CQRS Semantic Invariant Gates (`SIG-CQRS-001`, `SIG-CQRS-002`, `SIG-CQRS-003`)** across **13 open-source CQRS and DDD repositories** (.NET, TypeScript/NestJS, Go, Java, PHP) with **0 LLM tokens (`$0.00`)** using end-to-end call-graph and SQL/ORM data-lineage traversal.

---

## 1. The Question: Naming Conventions vs Behavioral Guarantees

In Command Query Responsibility Segregation (CQRS), teams routinely automate structural checks in CI:
- Every query handler class ends with `QueryHandler`, is `sealed`, and accepts a `CancellationToken`.
- Query classes reside under `Features/*/Queries` or `*/queries/handlers` folders.
- Application handlers depend only on domain interfaces rather than infrastructure assemblies (`ArchUnit`, `NetArchTest`).

Those checks verify **how types are named and packaged**, not **whether a read path actually mutates state**. A `QueryHandler` or `HTTP GET` endpoint can pass every naming and dependency rule while calling an injected domain/application service whose implementation inserts rows or updates statuses three call hops away.

### The 3 Behavioral CQRS Invariant Gates

CodeExplorer extracts CQRS contracts into Layer 4 **`Message`** nodes (`sub_kind`: `query`, `command`, `event`) and connects handler functions via typed **`HANDLES_QUERY`**, **`HANDLES_COMMAND`**, and **`HANDLES_EVENT`** edges across .NET (MediatR, `IQueryHandler`, Wolverine), TypeScript (`@nestjs/cqrs`), Java (Axon `@QueryHandler` / `@CommandHandler`, Spring), PHP (Symfony Messenger), Python, Go, and Rust:

| Gate ID | Invariant Rule | Graph Path Checked | Severity |
| :--- | :--- | :--- | :---: |
| **`SIG-CQRS-001`** | **Query Handlers Must Be Side-Effect Free** | `(q:Function)-[:HANDLES_QUERY]->(msg:Message), (q)-[:CALLS*0..4]->(w:Function)-[:WRITES_TO\|PUBLISHES]->(target)` | `Error` |
| **`SIG-CQRS-002`** | **Queries Must Not Invoke Command Handlers** | `(q:Function)-[:HANDLES_QUERY]->(:Message), (q)-[:CALLS*1..3]->(c:Function)-[:HANDLES_COMMAND]->(cmd:Message)` | `Error` |
| **`SIG-CQRS-003`** | **HTTP GET Endpoints Must Be Read-Only** | `(ep:Endpoint {http_method: 'GET'})-[:EXPOSED_BY\|TRIGGERS]->(fn)-[:CALLS*0..5]->(w)-[:WRITES_TO\|PUBLISHES]->(target)` | `Error` |

---

## 2. Corpus Results Across 13 Open-Source Repositories

Out of 13 open-source CQRS/DDD repositories scanned, **6 repositories** had end-to-end storage lineage connecting framework query handlers (`97` traced query handlers total) and `GET` endpoints down to SQL/ORM table operations:

| Repository & Commit | Stack | Scan Time | Query Handlers (Traced to DB) | `READS_FROM` / `WRITES_TO` | `SIG-CQRS-001` (Query Writes) | `SIG-CQRS-002` (Query->Cmd) | `SIG-CQRS-003` (GET Writes) |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| [`kgrzybek/modular-monolith-with-ddd`](https://github.com/kgrzybek/modular-monolith-with-ddd/tree/91c8ef2) (`91c8ef2`) | .NET, MediatR, Dapper, EF Core | `4.4 s` | `29` (`29` traced) | `228` / `302` | **`0`** | **`0`** | **`0`** |
| [`fullstackhero/dotnet-starter-kit`](https://github.com/fullstackhero/dotnet-starter-kit/tree/2f5989b243d702e0e2f089bd0258117da05f32e7) (`2f5989b`) | .NET, Mediator, EF Core | `12.0 s` | `71` (`59` traced) | `280` / `123` | **`1`** | **`0`** | **`1`** |
| [`ever-co/ever-gauzy`](https://github.com/ever-co/ever-gauzy/tree/0dc826c76e04d6c8803e5933b398aeb4fed8b8b6) (`0dc826c7`) | TS, NestJS `@nestjs/cqrs`, TypeORM | `331.4 s` | `101` (`5` traced) | `27,743` / `34,344` | **`1`** | **`0`** | **`7`** |
| [`jasontaylordev/CleanArchitecture`](https://github.com/jasontaylordev/CleanArchitecture/tree/9e1042b) (`9e1042b`) | .NET, MediatR, EF Core | `3.4 s` | `2` (`1` traced) | `9` / `5` | **`0`** | **`0`** | **`0`** |
| [`Sairyss/domain-driven-hexagon`](https://github.com/Sairyss/domain-driven-hexagon/tree/5c2d15a) (`5c2d15a`) | TS, NestJS `@nestjs/cqrs`, Slonik | `2.1 s` | `1` (`1` traced) | `4` / `2` | **`0`** | **`0`** | **`0`** |
| [`asc-lab/dotnetcore-microservices-poc`](https://github.com/asc-lab/dotnetcore-microservices-poc/tree/6500daa) (`6500daa`) | .NET, MediatR, EF Core, Marten | `3.8 s` | `8` (`2` traced) | `4` / `2` | **`0`** | **`0`** | **`0`** |

> **Transparency on coverage limits (remaining 7 repositories):** Across all 13 repositories, `ce v1.96.0` extracted `225` `HANDLES_QUERY`, `649` `HANDLES_COMMAND`, and `162` `HANDLES_EVENT` handler edges (`1,036` total). In `dotnet/eShop` (`ab10e4c`), `Squidex/squidex` (`1c8a367`), `simplcommerce/SimplCommerce` (`3472ba0`), and `BottegaIT/ddd-leaven-v2` (`5225ff7`, `27` `READS_FROM` / `27` `WRITES_TO`), reads bypass mediator `QueryHandler` contracts; in `AleksK1NG/Go-CQRS-Microservices` (`70cda28`, `5` `HANDLES_QUERY`), `ThreeDotsLabs/wild-workouts-go-ddd-example` (`8ecfcdf`, `4` `HANDLES_QUERY`), and `CodelyTV/php-ddd-example` (`9271c46`, `4` `HANDLES_QUERY`), query handlers are extracted natively while storage writes use drivers not yet modeled as `WRITES_TO` edges (Go `pgx`/Firestore, PHP Doctrine `EntityManager`). Vacuous `0`-violation runs are excluded from positive claims above.

---

## 3. Source-Verified Anatomy of Read-Path Writes

Neither finding below is sloppy code — both are intentional, pragmatic product shortcuts written by experienced authors. What call-graph + SQL lineage adds is making those tradeoffs **explicit and visible** to architects.

### Case A: Lazy Wallet Provisioning Inside `GET /wallet/me` (`fullstackhero/dotnet-starter-kit`)

1. **HTTP GET Endpoint** — [`GetMyWalletEndpoint.cs#L15-L17`](https://github.com/fullstackhero/dotnet-starter-kit/blob/2f5989b243d702e0e2f089bd0258117da05f32e7/src/Modules/Billing/Modules.Billing/Features/v1/Wallets/GetMyWallet/GetMyWalletEndpoint.cs#L15-L17):
   `endpoints.MapGet("/wallet/me", async (IMediator mediator, CancellationToken ct) => Results.Ok(await mediator.Send(new GetMyWalletQuery(), ct)))`
2. **Query Handler** — [`GetMyWalletQueryHandler.cs#L12-L25`](https://github.com/fullstackhero/dotnet-starter-kit/blob/2f5989b243d702e0e2f089bd0258117da05f32e7/src/Modules/Billing/Modules.Billing/Features/v1/Wallets/GetMyWallet/GetMyWalletQueryHandler.cs#L12-L25):
   `public sealed class GetMyWalletQueryHandler` implements `IQueryHandler<GetMyWalletQuery, WalletDto>` (passing all reflection rules in `HandlerArchitectureTests.cs`) and delegates to `IBillingService.GetOrCreateWalletAsync(...)`.
3. **Hidden Table Write** — [`BillingService.cs#L170-L181`](https://github.com/fullstackhero/dotnet-starter-kit/blob/2f5989b243d702e0e2f089bd0258117da05f32e7/src/Modules/Billing/Modules.Billing/Services/BillingService.cs#L170-L181):
   Queries `_db.Wallets`, and when missing, executes `_db.Wallets.Add(wallet)` + `await _db.SaveChangesAsync(cancellationToken)`.

**Why visibility matters:**
- Routing `IQueryHandler` pipelines to a **read-only database replica** will fail at runtime on a tenant's first visit.
- `WalletConfiguration.cs#L23` defines unique index `ux_wallets_tenantid`; two concurrent initial `GET /wallet/me` requests from a newly loaded SPA dashboard race in check-then-act and throw `DbUpdateException` on one of the reads.

### Case B: Marking Shared Invoices `VIEWED` on `QueryBus` (`ever-co/ever-gauzy`)

1. **HTTP GET Endpoints** — [`public-invoice.controller.ts#L46-L52`](https://github.com/ever-co/ever-gauzy/blob/0dc826c76e04d6c8803e5933b398aeb4fed8b8b6/packages/core/src/lib/public-share/invoice/public-invoice.controller.ts#L46-L52) and [`#L92`](https://github.com/ever-co/ever-gauzy/blob/0dc826c76e04d6c8803e5933b398aeb4fed8b8b6/packages/core/src/lib/public-share/invoice/public-invoice.controller.ts#L92) (plus GraphQL `@Query` in `public-invoice.resolver.ts#L14`):
   Execute `this.queryBus.execute(new FindPublicInvoiceQuery(...))`.
2. **`@QueryHandler`** — [`find-public-invoice.handler.ts#L6-L15`](https://github.com/ever-co/ever-gauzy/blob/0dc826c76e04d6c8803e5933b398aeb4fed8b8b6/packages/core/src/lib/public-share/invoice/queries/handlers/find-public-invoice.handler.ts#L6-L15):
   `@QueryHandler(FindPublicInvoiceQuery)` delegates to `publicInvoiceService.findOneByPublicLink(...)`.
3. **Hidden Status Update** — [`public-invoice.service.ts#L146-L170`](https://github.com/ever-co/ever-gauzy/blob/0dc826c76e04d6c8803e5933b398aeb4fed8b8b6/packages/core/src/lib/public-share/invoice/public-invoice.service.ts#L146-L170):
   Calls `this.markViewed(invoice)`, executing `this.typeOrmInvoiceRepository.update(..., { status: InvoiceStatusTypesEnum.VIEWED })` inside a `try/catch` that logs a warning on failure.

In addition, `SIG-CQRS-003` surfaced five `GET` endpoints in `ever-gauzy` performing lazy initialization (`GET /plugins/docs/inbound-addresses` and `.../verification` calling `mintPlatformAddress`, and three `GET /ever-stats/*` routes calling `everInstanceService.ensure()` -> `insertIgnore`).

---

## 4. Why Existing Static Checks Miss Multi-Hop CQRS Writes

| Tool Category | Representative Tools | What It Sees | Why It Stops Before the Write |
| :--- | :--- | :--- | :--- |
| **Type Dependency Tests** | `ArchUnit`, `NetArchTest` | Class/assembly references, interfaces, attributes, naming | `GetMyWalletQueryHandler` only references `IBillingService` in the same module. DI binds `BillingService` at runtime, and `IBillingService` legitimately contains read methods too. |
| **Single-File Linters** | `ESLint`, Roslyn Analyzers, `oxlint` | Per-file AST & local type symbols | The handler file is a single delegation line (`billingService.GetOrCreateWalletAsync` / `findOneByPublicLink`). The `DbSet.Add` or `repository.update` lives 2 to 3 files deeper. |
| **Dataflow / SAST Engines** | `CodeQL`, `Semgrep Pro` | Cross-file AST / call-graph taint paths | Capable in principle if you author custom per-framework queries modeling Mediator/`QueryBus` dispatch + EF Core / Dapper / TypeORM / embedded SQL write sinks across each language. |
| **Runtime Read-Only Tx** | EF Core / Postgres `READ ONLY` pipeline behavior | Live SQL statements executed during tests/prod | Catches writes **only when that code branch executes** (e.g., first-access `wallet is null` branch or `status == SENT` branch). Static graph gates catch every branch in `<1s` on every PR. |

---

## 5. Run the 3 CQRS Gates on Your Own Repository

1. Index your repository and generate starter gate templates:
   ```bash
   ce scan --init
   ```
2. Activate the three built-in CQRS gates in `.codeexplorer/gates/`:
   ```bash
   mv .codeexplorer/gates/cqrs-query-no-writes.yaml.example .codeexplorer/gates/cqrs-query-no-writes.yaml
   mv .codeexplorer/gates/cqrs-no-query-to-command-calls.yaml.example .codeexplorer/gates/cqrs-no-query-to-command-calls.yaml
   mv .codeexplorer/gates/cqrs-http-get-read-only.yaml.example .codeexplorer/gates/cqrs-http-get-read-only.yaml
   ```
3. Evaluate deterministically (or baseline intentional `GetOrCreate` exceptions via SHA-256 ratchets):
   ```bash
   ce gate check
   ce gate details
   # Optional: acknowledge deliberate exceptions while blocking any new read-side writes in future PRs
   ce gate ratchet --update
   ```
