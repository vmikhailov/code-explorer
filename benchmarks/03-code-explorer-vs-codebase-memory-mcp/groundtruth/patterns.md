# Ground Truth Patterns & Verification (§5.1)

All patterns were verified against at least 3 real source files in `twentyhq/twenty` (`12e8fae471865e9dab60c0b0fd59f318505c804e`) and `immich-app/immich` (`2153d22f34349de690cd98890ee4c635808da638`). Test files (`*.spec.ts`, `*.test.ts`, `__tests__`, `test/`, `e2e/`, `fixtures/`, `test_*.py`) are excluded across all categories.

## C1 — Incoming HTTP / GraphQL Endpoints
- **twenty (`packages/twenty-server/src`)**:
  - REST Controllers: classes with `@Controller(...)` and methods decorated with `@(Get|Post|Put|Patch|Delete)\(`. Note: `@Query()` inside `@Controller` is a NestJS query-param decorator and is excluded.
  - GraphQL Resolvers: classes with `@Resolver(...)` and methods decorated with `@(Query|Mutation|Subscription)\(`.
  - Verified examples: `ClientConfigController.getClientConfig`, `RestApiCoreController`, `BillingWebhookController`.
- **immich (`server/src` + `machine-learning/immich_ml`)**:
  - NestJS REST Controllers: classes with `@Controller(...)` and methods decorated with `@(Get|Post|Put|Patch|Delete)\(`.
  - Python FastAPI (`machine-learning/immich_ml/main.py`): functions decorated with `@(app|router)\.(get|post|put|delete|patch)\(`.
  - Verified examples: `AssetController`, `AuthController`, `main.py:root` (`GET /`), `main.py:ping` (`GET /ping`), `main.py:predict` (`POST /predict`).

## C2 — Outbound Inter-Service / External HTTP Calls
- **twenty (`packages/twenty-server/src`)**:
  - Calls matching `\b(fetch|axios\.(get|post|put|delete|patch|request)|this\.httpService\.(get|post|put|delete|patch|request))\(`. Excluded non-HTTP `imapClient.fetch`.
  - Verified examples: `TurnstileDriver.validate` (`this.httpService.post`), `ModelsDevCatalogService.getCachedData` (`fetch(MODELS_DEV_API_URL)`), `EnterprisePlanService`.
- **immich (`server/src`)**:
  - Calls matching `\bfetch\(`.
  - Verified examples: `MachineLearningRepository.check` (`fetch(new URL('ping', url))` -> `immich-machine-learning` `GET /ping`), `MachineLearningRepository.predict` (`fetch(new URL('predict', url))` -> `immich-machine-learning` `POST /predict`), `MaintenanceHealthRepository`.

## C3 & C8 — Message Queues & Async Jobs
- **twenty (`packages/twenty-server/src`)**:
  - Queue names defined in `export enum MessageQueue` (`message-queue.constants.ts`, 27 queues, `definition_kind = enum`).
  - Producers: services injecting `@InjectMessageQueue(MessageQueue.<queue>)` and calling `this.<queueService>.(add|addCron)(...)`.
  - Consumers: job classes decorated with `@Processor(MessageQueue.<queue>)` or `@Processor({ queueName: MessageQueue.<queue> })` and handler methods decorated with `@Process(...)`.
  - Verified examples: `WorkspaceDataExportStepService.enqueue` -> `MessageQueue.workspaceDataExportQueue` -> `WorkspaceDataExportStepJob.handle`.
- **immich (`server/src`)**:
  - Queue names defined in `export enum QueueName` (19 queues) and job names in `export enum JobName` (`server/src/enum.ts`, `definition_kind = enum`).
  - Producers: calls to `this.jobRepository.(queue|queueAll)({ name: JobName.<job>, ... })`.
  - Consumers: methods decorated with `@OnJob({ name: JobName.<job>, queue: QueueName.<queue> })`.
  - Verified examples: `AssetService` (`jobRepository.queue({ name: JobName.AssetExtractMetadata })`, `@OnJob({ name: JobName.AssetDeleteCheck, queue: QueueName.BackgroundTask })`).

## C4 & C10 — Database Tables & Schema
- **twenty (`packages/twenty-server/src`)**:
  - Classes decorated with `@Entity({ name: '<table_name>', schema: '...' })` or `@Entity('<table_name>')` (TypeORM).
  - Columns: `@PrimaryGeneratedColumn`, `@Column`, `@CreateDateColumn`, `@UpdateDateColumn`, `@DeleteDateColumn`.
  - Relations: `@ManyToOne`, `@OneToMany`, `@OneToOne`, `@ManyToMany`.
  - Verified examples: `UserEntity` (`@Entity({ name: 'user', schema: 'core' })`), `AppTokenEntity`, `WorkspaceEntity`.
- **immich (`server/src/schema/tables/*.table.ts`)**:
  - Classes decorated with `@Table('<table_name>')` or `@Table({ name: '<table_name>' })` (`@immich/sql-tools`).
  - Columns: `@PrimaryGeneratedColumn`, `@Column`, `@CreateDateColumn`, `@UpdateDateColumn`, `@UpdateIdColumn`.
  - Relations: `@ForeignKeyColumn(() => <TargetTable>)`.
  - Verified examples: `ActivityTable` (`@Table('activity')`), `AlbumTable` (`@Table({ name: 'album' })`), `AssetTable` (`@Table({ name: 'asset' })`).

## C5 — Code-to-Table Lineage
- **twenty**: Repository fields injected via `@InjectRepository(<Entity>)` or `@InjectWorkspaceScopedRepository(<Entity>)`, calling `.find*`, `.count`, `.createQueryBuilder` (`read`) or `.save`, `.insert`, `.update`, `.delete`, `.softDelete`, `.upsert`, `.remove` (`write`).
- **immich**: Kysely query builder calls in `server/src/repositories/*.repository.ts`: `.selectFrom('<table>')` (`read`) and `.insertInto('<table>')`, `.updateTable('<table>')`, `.deleteFrom('<table>')` (`write`).

## C6 — Call Graph
- Deterministic selection of 10 methods across `*Service` classes (`*.service.ts`), tracing direct non-test callers invoking `this.<serviceField>.<methodName>(` or `this.<methodName>(`.

## C7 — Inheritance & Implementation
- Top 5 base classes/interfaces with `>= 3` non-test classes matching `export (abstract )?class <Child> (extends|implements) <Parent>`.

## C9 — Services & Inter-Service Topology
- Derived from `docker-compose.yml`, `package.json`, and entrypoints (`main.ts`, `worker.ts`, `main.py`), plus C2 (HTTP) and C8 (Queue) edges.
