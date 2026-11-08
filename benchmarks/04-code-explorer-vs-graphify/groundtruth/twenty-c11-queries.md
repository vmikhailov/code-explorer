# twenty — Category C11 Ground Truth Architectural Queries (§5.8d)

1. **Топики, у которых есть производитель, но нет потребителя (и наоборот):**
   - Producer-only (`0`): `none`
   - Consumer-only (`4`): `campaign-send-queue, entity-events-to-db-queue, lambda-management-queue, trigger-queue`
   - Both producer and consumer (`23`): `ai-queue, ai-stream-queue, application-lifecycle-hook-queue, application-upgrade-queue, billing-queue, calendar-queue, campaign-engagement-queue, campaign-queue, connected-account-sync-webhook-queue, contact-creation-queue, cron-queue, delayed-jobs-queue, delete-cascade-queue, email-queue, event-log-queue, logic-function-queue, messaging-queue, record-export-queue, webhook-queue, workflow-queue, workspace-data-export-queue, workspace-destroy-queue, workspace-queue`

2. **Пары сервисов, связанные через общую таблицу (оба пишут/читают одну таблицу):**
   - В `twenty` серверный процесс и фоновый воркер разделяют общую БД PostgreSQL (все таблицы `17+` разделяются между HTTP-сервером и воркером очередей).

3. **Таблицы, в которые пишет более одного сервиса:**
   - Таблицы с записью как из HTTP-обработчиков, так и из фоновых джобов воркера (в рамках общей схемы БД).

4. **Эндпоинты, из которых транзитивно (<= 4 хопа по вызовам) достижима запись в таблицу `apiKey`:**
   - Прямые методы записи в `apiKey` из C5: `ApiKeyService.create` и вызывающие их контроллеры/резолверы из C1.

5. **Для каждого сервиса: число входящих эндпоинтов, число публикуемых и потребляемых топиков:**
   - `twenty` сервер/воркер: входящие эндпоинты C1, публикуемые топики = `23`, потребляемые топики = `27`.

6. **Цепочки `эндпоинт -> (вызовы) -> публикация в топик -> потребитель в другом сервисе`:**
   - Цепочки от контроллеров/резолверов C1 через сервисы-продюсеры C3 (`23` очередей) к `@Processor`/`@OnJob` воркерам C3 (`27` очередей).
