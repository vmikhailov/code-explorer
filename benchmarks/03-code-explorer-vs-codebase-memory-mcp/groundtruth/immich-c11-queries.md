# immich — Category C11 Ground Truth Architectural Queries (§5.8d)

1. **Топики, у которых есть производитель, но нет потребителя (и наоборот):**
   - Producer-only (`0`): `none`
   - Consumer-only (`0`): `none`
   - Both producer and consumer (`18`): `backgroundTask, backupDatabase, duplicateDetection, editor, faceDetection, facialRecognition, integrityCheck, library, metadataExtraction, migration, notifications, ocr, sidecar, smartSearch, storageTemplateMigration, thumbnailGeneration, videoConversion, workflow`

2. **Пары сервисов, связанные через общую таблицу (оба пишут/читают одну таблицу):**
   - В `immich` серверный процесс и фоновый воркер разделяют общую БД PostgreSQL (все таблицы `18+` разделяются между HTTP-сервером и воркером очередей).

3. **Таблицы, в которые пишет более одного сервиса:**
   - Таблицы с записью как из HTTP-обработчиков, так и из фоновых джобов воркера (в рамках общей схемы БД).

4. **Эндпоинты, из которых транзитивно (<= 4 хопа по вызовам) достижима запись в таблицу `activity`:**
   - Прямые методы записи в `activity` из C5: `ActivityRepository.create, ActivityRepository.delete` и вызывающие их контроллеры/резолверы из C1.

5. **Для каждого сервиса: число входящих эндпоинтов, число публикуемых и потребляемых топиков:**
   - `immich` сервер/воркер: входящие эндпоинты C1, публикуемые топики = `18`, потребляемые топики = `18`.

6. **Цепочки `эндпоинт -> (вызовы) -> публикация в топик -> потребитель в другом сервисе`:**
   - Цепочки от контроллеров/резолверов C1 через сервисы-продюсеры C3 (`18` очередей) к `@Processor`/`@OnJob` воркерам C3 (`18` очередей).
