# CodeExplorer Active Backlog & Engineering Roadmap (`roadmap/todo`) 📋

This document tracks active epics, immediate architectural refactoring plans, and the master engineering checklist for **CodeExplorer (`ce`)**.

---

## 🚨 IMMEDIATE PRIORITY: First-Class Semantic Entities (`Service`, `App`, `Library`, `Worker`, `CliTool`)

### 1. Архитектурная цель
Заменить абстрактный `Project` с полем `role` в JSON на специализированные первоклассные типы узлов в SQLite (`kind = 'Service'`, `kind = 'App'`, `kind = 'Library'`, `kind = 'Worker'`, `kind = 'CliTool'`).
При этом обеспечить прозрачный полиморфизм в Cypher-компиляторе: запрос `MATCH (p:Project)` находит любые из них.

```
                  ┌──────────────────────┐
                  │     ProjectNode      │ (базовый тип / неклассифицированный)
                  └──────────┬───────────┘
         ┌─────────────┬─────┴───────┬─────────────┬─────────────┐
         ▼             ▼             ▼             ▼             ▼
   ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐
   │ServiceNode│ │  AppNode  │ │LibraryNode│ │WorkerNode │ │CliToolNode│
   └───────────┘ └───────────┘ └───────────┘ └───────────┘ └───────────┘
   kind:Service  kind:App      kind:Library  kind:Worker   kind:CliTool
```

### 2. Пошаговый план реализации

#### Этап 1. Модель данных и C#-классы нод (`CodeExplorer.Core`)
- [ ] **1.1 Базовый `ProjectNode`**:
  - Сохранить общие поля: `Id`, `Name`, `Path`, `ProjectType` (язык: csharp, typescript и т.д.), `Extensions`.
- [ ] **1.2 Новые классы узлов в `Common/Nodes/Layer2_Boundaries/`**:
  - `ServiceNode.cs` (`[OntologyNode(label: "Service")]`):
    - Связи: `SERVICE_CALL`, `PUBLISHES_TO`, `SUBSCRIBES_TO`, `USES_DB`, `CONTAINS (Endpoint, EntryPoint, ApiInUse, CloudService)`.
  - `AppNode.cs` (`[OntologyNode(label: "App")]`):
    - Связи: `SERVICE_CALL`, `CONTAINS (Endpoint, EntryPoint, ApiInUse)`.
  - `LibraryNode.cs` (`[OntologyNode(label: "Library")]`):
    - Связи: `DEPENDS_ON`, `CONTAINS (Type, Function)`.
  - `WorkerNode.cs` (`[OntologyNode(label: "Worker")]`):
    - Связи: `SUBSCRIBES_TO`, `PUBLISHES_TO`, `USES_DB`.
  - `CliToolNode.cs` (`[OntologyNode(label: "CliTool")]`):
    - Связи: `USES_DB`, `DEPENDS_ON`.
- [ ] **1.3 Константы**:
  - Зафиксировать имена в `OntologyConstants.NodeLabels`.

#### Этап 2. Фабрика и классификация при сканировании (`Layer2` & `PostIndexAnalyzer`)
- [ ] **2.1 Фабрика проектов `ProjectNodeFactory`**:
  - В `Layer2ProjectParser` при обнаружении проекта определять роль (через `ProjectRoleDetector`) и сразу инстанциировать конкретный тип (`ServiceNode`, `AppNode`, `LibraryNode`, `WorkerNode`, `CliToolNode`).
- [ ] **2.2 Запись в SQLite (`kind`)**:
  - В таблицу `nodes` в колонку `kind` физически писать реальный тип (`Service`, `App`, `Library`, `Worker`, `CliTool`).
  - Поиск по типу становится нативным по B-Tree индексу SQLite (`CREATE INDEX idx_nodes_kind ON nodes(kind)`).

#### Этап 3. Полиморфизм в Cypher-компиляторе (`CodeExplorer.Cypher`)
- [ ] **3.1 Прямой быстрый поиск**:
  - Запрос `MATCH (s:Service)` транслировать в чистый SQL: `WHERE s.kind = 'Service'` (без вызовов `json_extract(properties, '$.role')`).
- [ ] **3.2 Иерархический полиморфизм для `Project`**:
  - Запрос `MATCH (p:Project)` транслировать в:
    `WHERE p.kind IN ('Project', 'Service', 'App', 'Library', 'Worker', 'CliTool')`.
- [ ] **3.3 Функция `labels(n)`**:
  - Если `n.kind IN ('Service', 'App', 'Library', 'Worker', 'CliTool')`, возвращать `json_array(n.kind, 'Project')`.

#### Этап 4. Движок проекций и запросы (`ArchitectureViewEngine`)
- [ ] **4.1 Упрощение C1 / C2 / C3 запросов**:
  - Заменить составные проверки `WHERE p.kind = 'Project' AND json_extract(...)` на прямое сопоставление по типам: `MATCH (s:Service)`, `MATCH (l:Library)`.
  - В C1 System Context отображать только `Service`, `App`, `Worker`, `Database`, `Topic` (исключая `Library` без костылей).

#### Этап 5. Авто-генерация онтологии (`OntologyGen`) и тесты
- [ ] **5.1 Регенерация `ontology.md`**:
  - Запустить `OntologyGen`. Он автоматически сгенерирует для `Service`, `App`, `Library`, `Worker`, `CliTool` отдельные разделы с диаграммами и свойствами.
- [ ] **5.2 Тесты**:
  - Набор тестов в `CodeExplorer.Cypher.Tests`:
    - `MATCH (s:Service)` выбирает только сервисы.
    - `MATCH (p:Project)` полиморфно выбирает все проекты.
  - Регрессионные тесты в `CodeExplorer.Tests`.

---

## 🚀 EPIC 1: Agent Semantic & Project Architecture (MCP & CLI Parity)

- [ ] **1.1 `get_architecture_view` MCP Tool**:
  - Подключить MCP напрямую к `ArchitectureViewEngine` для C1 (System Context), C2 (Service Flow), C3 (Component Drill-Down).
  - Поддержка параметров `viewType`, `scope`, `includeLibraries`, `format`.
- [ ] **1.2 Фильтрация зависимостей в `get_project_dependencies`**:
  - Добавить параметр `--type runtime|build|all`:
    - `runtime` / `semantic`: только сетевые вызовы (`SERVICE_CALL`), очереди (`PUBLISHES_TO`, `SUBSCRIBES_TO`), базы (`USES_DB`).
    - `build` / `structural`: только ссылки на проекты (`PROJECT_REFERENCE`) и пакеты (`DEPENDS_ON`).
- [ ] **1.3 `get_service_contracts` MCP Tool**:
  - Извлечение контрактов сервиса: ingress (эндпоинты, консьюмеры) и egress (HTTP-клиенты, паблишеры, БД).
- [ ] **1.4 `trace_cross_service_flow` MCP Tool**:
  - Сквозная трассировка вызовов через границы сервисов (Контроллер -> Очередь -> Консьюмер -> База).
- [ ] **1.5 Паритет команд CLI**:
  - `ce view architecture`, `ce dependencies --type`, `ce contracts`, `ce trace flow`.
- [ ] **1.6 Токен-эффективные сериализаторы**:
  - Сериализаторы в TOON (Token-Oriented Object Notation), Mermaid (`flowchart LR`) и компактный Markdown для минимального расхода контекста LLM.

---

## 🔍 EPIC 2: Parser Intelligence & High-Fidelity Lineage

- [ ] **2.1 ASP.NET Core иерархическая композиция маршрутов**:
  - Склейка `[Route("api/[controller]")]` с методами действий `[HttpGet("{id}")]`, подстановка токенов (`[controller]`, `[action]`).
  - Поддержка цепочек Minimal API `app.MapGroup("/api/v1")`.
- [ ] **2.2 C# Constructor Dependency Injection Mapping**:
  - Привязка параметров конструкторов к приватным полям (`_orderService`).
  - Резолвинг вызовов интерфейсов через `[:IMPLEMENTS]` в конкретные классы реализаций в Layer 5.
- [ ] **2.3 Декларативные HTTP-клиенты и Egress**:
  - Извлечение URL и маршрутов из `HttpClient`, `RestSharp`, `Refit`, `RestEase`.
- [ ] **2.4 ORM Data Lineage Mapping (EF Core & Dapper)**:
  - Извлечение таблиц из `DbSet<T>` и Fluent API `ToTable("...")`.
  - Привязка сырых SQL-запросов напрямую к нодам таблиц БД в `inspect_data_lineage`.

---

## ⚡ EPIC 3: Cypher Query Engine & Transpiler Expansion

- [ ] **3.1 Функции связей OpenCypher**:
  - Поддержка функций связей: `type(r)`, `properties(r)`, `startNode(r)`, `endNode(r)` в `WITH` и агрегациях.
  - Прямой доступ к атрибутам ребер (`r.via`, `r.call_chain`).
- [ ] **3.2 Декомпозиция декартова произведения в `OPTIONAL MATCH`**:
  - Разделение независимых веток `OPTIONAL MATCH` на изолированные коррелированные подзапросы/CTE, устраняющее промежуточный взрыв строк $O(N \cdot M \cdot K)$.
- [ ] **3.3 Предикаты путей и списков**:
  - Транспиляция `WHERE EXISTS((n)-[:REL]->(m))` в SQL `EXISTS`.
  - Квантификаторы `any()`, `all()`, `none()` над JSON-массивами.

---

## 🛡️ EPIC 4: Concurrency & Incremental Watcher

- [ ] **4.1 Инкрементальный вотчер файлов (`ce watch`)**:
  - Отслеживание изменений файлов с дебаунсом 500мс.
  - Точечный запуск `ParsingContext.IsSubtreeScan` на измененных файлах.
- [ ] **4.2 Read-Only пул соединений для MCP-тулов**:
  - Принудительный режим `Mode=ReadOnly;Cache=Shared;` для устранения блокировок читателей/писателей в SQLite.
- [ ] **4.3 Кэширование макро-структур**:
  - Потокобезопасный in-memory кэш для `SystemContext` и `Taxonomy` с авто-сбросом при изменениях.

---

## 🧪 EPIC 5: CI/CD Benchmark Fixtures

- [ ] **5.1 Синтетический фикстурный граф на 100k узлов**:
  - Детерминированный генератор графа в памяти для CI (10k файлов, 25k классов, 70k методов, 300k связей).
  - Автоматические проверки SLA в тестах (`find_symbol` < 25ms, `get_call_chain` < 100ms, `inspect_data_lineage` < 50ms).

---

## 📋 Сводный чеклист задач

| ID | Область | Задача | Приоритет | Статус |
| :--- | :--- | :--- | :--- | :--- |
| **0.1** | Core/Entities | Создать `ServiceNode`, `AppNode`, `LibraryNode`, `WorkerNode`, `CliToolNode` | 🚨 Urgent | ⏳ Pending |
| **0.2** | Scanner | Фабрика `ProjectNodeFactory` и запись реального `kind` в SQLite | 🚨 Urgent | ⏳ Pending |
| **0.3** | Cypher | Полиморфизм: `MATCH (p:Project)` -> `kind IN (...)`, `MATCH (s:Service)` -> `kind = 'Service'` | 🚨 Urgent | ⏳ Pending |
| **0.4** | ViewEngine | Очистка Cypher-запросов C1/C2/C3 под новые типы нод | 🚨 Urgent | ⏳ Pending |
| **0.5** | OntologyGen | Регенерация `ontology.md` с отдельными секциями для Service/App/Library | 🚨 Urgent | ⏳ Pending |
| **1.1** | Agent/MCP | Реализовать MCP-тул `get_architecture_view` через `ArchitectureViewEngine` | High | ⏳ Pending |
| **1.2** | Agent/MCP | Добавить фильтр `--type runtime\|build\|all` в `get_project_dependencies` | High | ⏳ Pending |
| **1.3** | Agent/MCP | Реализовать MCP-тул `get_service_contracts` (ingress / egress) | High | ⏳ Pending |
| **1.4** | Agent/MCP | Реализовать MCP-тул `trace_cross_service_flow` | High | ⏳ Pending |
| **1.5** | CLI | Добавить команды `ce view architecture`, `ce dependencies --type`, `ce contracts` | High | ⏳ Pending |
| **1.6** | Formats | Добавить TOON, Mermaid и Markdown сериализаторы в `ArchitectureViewEngine` | High | ⏳ Pending |
| **2.1** | Parsers | Композиция маршрутов ASP.NET Core и Minimal API `MapGroup` | Medium | ⏳ Pending |
| **2.2** | Parsers | C# constructor DI mapping и привязка к реализациям через `[:IMPLEMENTS]` | Medium | ⏳ Pending |
| **2.3** | Parsers | Декларативные HTTP-клиенты (Refit/RestEase) и URI-резолвинг | Medium | ⏳ Pending |
| **2.4** | Parsers | EF Core `DbSet<T>` / `ToTable` и Dapper SQL lineage | Medium | ⏳ Pending |
| **3.1** | Cypher | Функции связей (`type(r)`, `properties(r)`, `startNode`, `endNode`) | Medium | ⏳ Pending |
| **3.2** | Cypher | Декомпозиция декартова произведения в `OPTIONAL MATCH` | Medium | ⏳ Pending |
| **3.3** | Cypher | Предикаты путей (`EXISTS((a)->(b))`) и списковые квантификаторы | Low | ⏳ Pending |
| **4.1** | Concurrency | Инкрементальный вотчер `ce watch` с дебаунсом | Medium | ⏳ Pending |
| **4.2** | Concurrency | Read-only пул соединений для MCP-тулов | Medium | ⏳ Pending |
| **5.1** | Testing | Синтетический бенчмарк-граф на 100k узлов для CI | Low | ⏳ Pending |
