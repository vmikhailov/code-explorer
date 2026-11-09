Perform a comprehensive, 4-part architectural audit on the `twenty` monorepo located at `./repos/twenty` (`24,527` tracked files across `46` packages). Answer all four architectural questions below with 100% completeness (do not guess, truncate, or omit items; exclude `.spec.ts` / `.test.ts` / `__tests__` test files). To keep both query outputs and your final report concise, **do not include file path columns** and **use compact grouped bullet lists** rather than wide multi-line Markdown tables:

1. **Task 1 — Service Method Call Graph & DI Fan-In (`Layer 3`)**:
   Identify every non-test caller function/method (`CallerClass.callerMethod` and target `calleeMethod`) across the monorepo that invokes any method on these 3 core services:
   - `BillingSubscriptionService`
   - `ThrottlerService`
   - `AccessTokenService`
   Group by target service, report the total call pair count (`totalCallPairs`) for each service, and list `callerMethod -> calleeMethod` pairs grouped compactly by `CallerClass`.

2. **Task 2 — Multi-Hop Class Inheritance & Interface Realization (`Layer 3`)**:
   List every non-test class/type name (`className`) across the monorepo that:
   (a) Inherits **directly or transitively (1 to 3 hops)** from `SyncableEntity`
   (b) Inherits **directly or transitively (1 to 3 hops)** from `WorkspaceRelatedEntity` (include both direct 1-hop subclasses and 2-hop transitive subclasses)
   (c) Inherits (directly or transitively) from `WorkspaceEntityMigrationBuilderService`
   (d) Implements the `WorkspaceResolverBuilderFactoryInterface` interface
   For each of (a), (b), (c), and (d), report the exact total count and a compact comma-separated list of `className` values (grouped by hop distance where applicable).

3. **Task 3 — Asynchronous BullMQ Queue Producers & Consumers (`Layer 4`)**:
   For each of the following 5 BullMQ queues in `twenty`:
   - `webhook-queue`
   - `workspace-queue`
   - `campaign-queue`
   - `ai-queue`
   - `delete-cascade-queue`
   Identify every non-test `ClassName.methodName` that:
   (a) **Publishes / enqueues** jobs to the queue, and
   (b) **Subscribes / processes** jobs from the queue.
   Report exact counts and a compact comma-separated list of `ClassName.methodName` per queue and role.

4. **Task 4 — End-to-End API-to-Database Write Lineage (`Layer 4–5`)**:
   For each of the following 4 database tables:
   - `billingSubscription`
   - `billingCustomer`
   - `connectedAccount`
   - `messageChannel`
   Identify every incoming GraphQL (`@Mutation` / `@Query`) or REST (`@Controller`) endpoint and its handler/resolver method (`endpointName`, `httpMethod`/operation, and `HandlerClass.handlerMethodName`) that **writes (`WRITES_TO` — inserts, updates, saves, upserts, or deletes)** to that table either directly (`0` hops) or within `1 to 3` function call hops (`Endpoint -> Handler/Resolver -> Service -> Repository/Query -> Table`). Provide the distinct count and compact list of `endpointName (httpMethod) -> HandlerClass.handlerMethodName` per table.
