# 🧭 CodeExplorer Benchmark: Топ-50 Компактных Проектов (Compact & Focused)

> **Назначение реестра:** Пул легких, чистых и архитектурно выверенных open-source библиотек и микро-инструментов (обычно от 1 000 до 25 000 строк кода).
> В отличие от монолитов-гигантов, эти проекты идеально подходят для **быстрого автоматического обхода, мгновенной индексации и сквозного тестирования CodeExplorer** с помощью LLM-агентов. Модель может за секунды склонировать проект, прогнать AST, протестировать разрешение символов и находить новые паттерны зависимостей без перегрузки контекста и памяти.

---

## 📊 Сводная таблица параметров отбора

| Язык | Проектов | Диапазон звёзд | Объем кодовой базы | Архитектурная специфика |
| :--- | :---: | :---: | :---: | :--- |
| **C#** | 10 | 3.6k – 18k ⭐ | ~2k – 20k LOC | Micro-ORM, Resilience pipelines, TUI, State machines, Expression trees |
| **TypeScript** | 10 | 9.7k – 61k ⭐ | ~1k – 15k LOC | Pure state containers, Type-inference schemas, Virtual DOM, Micro-routers |
| **Go** | 10 | 5.0k – 45k ⭐ | ~2k – 25k LOC | Embedded B+ Tree DB, Radix HTTP router, Elm TUI, Zero-alloc logging |
| **Java** | 10 | 2.0k – 47k ⭐ | ~3k – 30k LOC | Interceptor chains, Dynamic proxies, Annotation processors, AST visitors |
| **Python** | 10 | 2.5k – 57k ⭐ | ~1k – 15k LOC | Transport adapters, Decorator CLI, Compact ORM, Single-file micro-framework |

---

## 🔹 C# (10 компактных проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный фокус | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[DapperLib/Dapper](https://github.com/DapperLib/Dapper)** | ⭐ 18,385 | 📅 2026-09-12 | Micro-ORM & Data Access | [GitHub](https://github.com/DapperLib/Dapper.git) |
| **[App-vNext/Polly](https://github.com/App-vNext/Polly)** | ⭐ 14,236 | 📅 2026-09-14 | Resilience & Transient Faults | [GitHub](https://github.com/App-vNext/Polly.git) |
| **[spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console)** | ⭐ 11,621 | 📅 2026-09-15 | Terminal UI Toolkit | [GitHub](https://github.com/spectreconsole/spectre.console.git) |
| **[AutoMapper/AutoMapper](https://github.com/AutoMapper/AutoMapper)** | ⭐ 10,189 | 📅 2026-09-09 | Object-to-Object Mapping | [GitHub](https://github.com/AutoMapper/AutoMapper.git) |
| **[Humanizr/Humanizer](https://github.com/Humanizr/Humanizer)** | ⭐ 9,881 | 📅 2026-09-11 | String & Data Formatting | [GitHub](https://github.com/Humanizr/Humanizer.git) |
| **[MessagePack-CSharp/MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp)** | ⭐ 6,780 | 📅 2026-09-15 | Binary Serialization | [GitHub](https://github.com/MessagePack-CSharp/MessagePack-CSharp.git) |
| **[dotnet-state-machine/stateless](https://github.com/dotnet-state-machine/stateless)** | ⭐ 6,260 | 📅 2026-04-04 | State Machine Engine | [GitHub](https://github.com/dotnet-state-machine/stateless.git) |
| **[morelinq/MoreLINQ](https://github.com/morelinq/MoreLINQ)** | ⭐ 3,836 | 📅 2025-11-25 | LINQ Extended Operators | [GitHub](https://github.com/morelinq/MoreLINQ.git) |
| **[fluentassertions/fluentassertions](https://github.com/fluentassertions/fluentassertions)** | ⭐ 3,815 | 📅 2026-09-15 | Testing Assertions | [GitHub](https://github.com/fluentassertions/fluentassertions.git) |
| **[dotnet/command-line-api](https://github.com/dotnet/command-line-api)** | ⭐ 3,675 | 📅 2026-09-15 | CLI Parser & Binding | [GitHub](https://github.com/dotnet/command-line-api.git) |

### Разбор архитектуры и тест-кейсов для CodeExplorer:

#### 📦 [DapperLib/Dapper](https://github.com/DapperLib/Dapper) — ⭐ 18,385 (2026-09-12)
* **Архитектурный паттерн:** `Extension Methods / Dynamic IL / Fast Object Mapping`
* **Зачем тестировать в CodeExplorer:** Компактный micro-ORM: генерация IL кода на лету, расширения для IDbConnection, замер точности маппинга типов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/DapperLib/Dapper.git`

#### 📦 [App-vNext/Polly](https://github.com/App-vNext/Polly) — ⭐ 14,236 (2026-09-14)
* **Архитектурный паттерн:** `Fluent Policy Pipeline / Strategy Pattern`
* **Зачем тестировать в CodeExplorer:** Пайплайны стратегий (Retry, Circuit Breaker, Timeout, Fallback), богатый generic интерфейс, чистый асинхронный код.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/App-vNext/Polly.git`

#### 📦 [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console) — ⭐ 11,621 (2026-09-15)
* **Архитектурный паттерн:** `ANSI Parsing / Widget Hierarchy / Fluent API`
* **Зачем тестировать в CodeExplorer:** Консольные виджеты, деревья, таблицы, разбор ANSI разметки, отличная модульная объектная модель.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/spectreconsole/spectre.console.git`

#### 📦 [AutoMapper/AutoMapper](https://github.com/AutoMapper/AutoMapper) — ⭐ 10,189 (2026-09-09)
* **Архитектурный паттерн:** `Expression Tree Compiler / Reflection Engine`
* **Зачем тестировать в CodeExplorer:** Компиляция деревьев выражений (Expression Trees), вывод типов, сопоставление свойств объектов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/AutoMapper/AutoMapper.git`

#### 📦 [Humanizr/Humanizer](https://github.com/Humanizr/Humanizer) — ⭐ 9,881 (2026-09-11)
* **Архитектурный паттерн:** `Extension-First Utility Architecture`
* **Зачем тестировать в CodeExplorer:** Сотни расширений для базовых типов (string, DateTime, TimeSpan, enum), локализационные ресурсы.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/Humanizr/Humanizer.git`

#### 📦 [MessagePack-CSharp/MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp) — ⭐ 6,780 (2026-09-15)
* **Архитектурный паттерн:** `High-Perf Zero-Alloc Buffers / Dynamic CodeGen`
* **Зачем тестировать в CodeExplorer:** Работа с Span<T>, Memory<T>, динамическая генерация сериализаторов, оптимизация под нулевые аллокации.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/MessagePack-CSharp/MessagePack-CSharp.git`

#### 📦 [dotnet-state-machine/stateless](https://github.com/dotnet-state-machine/stateless) — ⭐ 6,260 (2026-04-04)
* **Архитектурный паттерн:** `Hierarchical State Pattern / Fluent Builder`
* **Зачем тестировать в CodeExplorer:** Иерархические конечные автоматы, типизированные триггеры, лямбда-переходы, минимум зависимостей (~3k LOC).
* **Команда для клонирования:** `git clone --depth 1 https://github.com/dotnet-state-machine/stateless.git`

#### 📦 [morelinq/MoreLINQ](https://github.com/morelinq/MoreLINQ) — ⭐ 3,836 (2025-11-25)
* **Архитектурный паттерн:** `Iterator Functions / Lazy Evaluation Pipelines`
* **Зачем тестировать в CodeExplorer:** Десятки операторов над IEnumerable<T>: отложенное вычисление (yield return), обработка граничных условий.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/morelinq/MoreLINQ.git`

#### 📦 [fluentassertions/fluentassertions](https://github.com/fluentassertions/fluentassertions) — ⭐ 3,815 (2026-09-15)
* **Архитектурный паттерн:** `Fluent Chaining / Recursive Equivalence`
* **Зачем тестировать в CodeExplorer:** Рекурсивное сравнение графов объектов, цепочки методов Fluent API, перегрузка операторов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/fluentassertions/fluentassertions.git`

#### 📦 [dotnet/command-line-api](https://github.com/dotnet/command-line-api) — ⭐ 3,675 (2026-09-15)
* **Архитектурный паттерн:** `Hierarchical Command Tree / Tokenizer`
* **Зачем тестировать в CodeExplorer:** Официальная библиотека парсинга CLI: дерево команд, связывание аргументов с моделями, middleware парсинга.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/dotnet/command-line-api.git`

## 🔹 TypeScript (10 компактных проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный фокус | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[reduxjs/redux](https://github.com/reduxjs/redux)** | ⭐ 61,486 | 📅 2026-09-08 | State Management | [GitHub](https://github.com/reduxjs/redux.git) |
| **[colinhacks/zod](https://github.com/colinhacks/zod)** | ⭐ 43,953 | 📅 2026-09-14 | Schema Declaration & Types | [GitHub](https://github.com/colinhacks/zod.git) |
| **[preactjs/preact](https://github.com/preactjs/preact)** | ⭐ 38,868 | 📅 2026-09-15 | Virtual DOM & UI Core | [GitHub](https://github.com/preactjs/preact.git) |
| **[fastify/fastify](https://github.com/fastify/fastify)** | ⭐ 37,145 | 📅 2026-09-15 | High-Speed Web Framework | [GitHub](https://github.com/fastify/fastify.git) |
| **[drizzle-team/drizzle-orm](https://github.com/drizzle-team/drizzle-orm)** | ⭐ 35,777 | 📅 2026-09-15 | Type-Safe SQL ORM | [GitHub](https://github.com/drizzle-team/drizzle-orm.git) |
| **[honojs/hono](https://github.com/honojs/hono)** | ⭐ 32,197 | 📅 2026-09-15 | Ultrafast Multi-Runtime Web | [GitHub](https://github.com/honojs/hono.git) |
| **[lucide-icons/lucide](https://github.com/lucide-icons/lucide)** | ⭐ 24,529 | 📅 2026-09-14 | Icon & Vector System | [GitHub](https://github.com/lucide-icons/lucide.git) |
| **[sindresorhus/ky](https://github.com/sindresorhus/ky)** | ⭐ 17,071 | 📅 2026-09-14 | Modern HTTP Client | [GitHub](https://github.com/sindresorhus/ky.git) |
| **[paulmillr/chokidar](https://github.com/paulmillr/chokidar)** | ⭐ 12,239 | 📅 2026-08-16 | Filesystem Watcher | [GitHub](https://github.com/paulmillr/chokidar.git) |
| **[sindresorhus/ora](https://github.com/sindresorhus/ora)** | ⭐ 9,747 | 📅 2026-06-22 | Terminal Spinner & UI | [GitHub](https://github.com/sindresorhus/ora.git) |

### Разбор архитектуры и тест-кейсов для CodeExplorer:

#### 📦 [reduxjs/redux](https://github.com/reduxjs/redux) — ⭐ 61,486 (2026-09-08)
* **Архитектурный паттерн:** `Functional / Pure Reducers / Middleware Pipeline`
* **Зачем тестировать в CodeExplorer:** Канонический образец функциональной архитектуры: чистые функции, композиция middleware, ~2k строк чистейшего кода.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/reduxjs/redux.git`

#### 📦 [colinhacks/zod](https://github.com/colinhacks/zod) — ⭐ 43,953 (2026-09-14)
* **Архитектурный паттерн:** `Type Inference Engine / Composability`
* **Зачем тестировать в CodeExplorer:** Вершина системы типов TypeScript: вывод сложных типов (`z.infer<T>`), парсинг и валидация без внешних зависимостей.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/colinhacks/zod.git`

#### 📦 [preactjs/preact](https://github.com/preactjs/preact) — ⭐ 38,868 (2026-09-15)
* **Архитектурный паттерн:** `Micro Virtual DOM / Diffing Algorithm`
* **Зачем тестировать в CodeExplorer:** Полноценный React-совместимый Virtual DOM всего в 3 кБ: алгоритмы дифф-сравнения деревьев, хуки, компонентный жизненный цикл.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/preactjs/preact.git`

#### 📦 [fastify/fastify](https://github.com/fastify/fastify) — ⭐ 37,145 (2026-09-15)
* **Архитектурный паттерн:** `Plugin Tree / JSON Schema Compilation`
* **Зачем тестировать в CodeExplorer:** Дерево плагинов (Avvio), прекомпиляция схем валидации (fast-json-stringify), быстрый роутер.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/fastify/fastify.git`

#### 📦 [drizzle-team/drizzle-orm](https://github.com/drizzle-team/drizzle-orm) — ⭐ 35,777 (2026-09-15)
* **Архитектурный паттерн:** `SQL AST Builder / Schema Compiler`
* **Зачем тестировать в CodeExplorer:** Построение SQL-запросов на уровне типов TS: строгая проверка колонок, отношений, генерация миграций.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/drizzle-team/drizzle-orm.git`

#### 📦 [honojs/hono](https://github.com/honojs/hono) — ⭐ 32,197 (2026-09-15)
* **Архитектурный паттерн:** `RegExp Router / Multi-Target Runtime`
* **Зачем тестировать в CodeExplorer:** Легковесный современный веб-фреймворк под Node, Deno, Bun и Cloudflare: сверхбыстрый роутер на регексах, контекст.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/honojs/hono.git`

#### 📦 [lucide-icons/lucide](https://github.com/lucide-icons/lucide) — ⭐ 24,529 (2026-09-14)
* **Архитектурный паттерн:** `Multi-Package Component Generator`
* **Зачем тестировать в CodeExplorer:** Пайплайн кодогенерации: SVG узлы транслируются в типизированные React/Vue/Svelte компоненты.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/lucide-icons/lucide.git`

#### 📦 [sindresorhus/ky](https://github.com/sindresorhus/ky) — ⭐ 17,071 (2026-09-14)
* **Архитектурный паттерн:** `Fetch Wrapper / Hook Chain Architecture`
* **Зачем тестировать в CodeExplorer:** Элегантная обертка над native Fetch: хуки `beforeRequest`/`afterResponse`, таймауты, retry логика.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/sindresorhus/ky.git`

#### 📦 [paulmillr/chokidar](https://github.com/paulmillr/chokidar) — ⭐ 12,239 (2026-08-16)
* **Архитектурный паттерн:** `OS Event Normalization / Debouncing`
* **Зачем тестировать в CodeExplorer:** Нормализация файловых событий ОС (fsevents/inotify/win32), управление таймерами, устранение дубликатов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/paulmillr/chokidar.git`

#### 📦 [sindresorhus/ora](https://github.com/sindresorhus/ora) — ⭐ 9,747 (2026-06-22)
* **Архитектурный паттерн:** `ANSI Stream Engine / Event Loop TTY`
* **Зачем тестировать в CodeExplorer:** Компактная TUI утилита: управление потоком stdout, ANSI escape последовательности, очистка терминала.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/sindresorhus/ora.git`

## 🔹 Go (10 компактных проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный фокус | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[charmbracelet/bubbletea](https://github.com/charmbracelet/bubbletea)** | ⭐ 44,972 | 📅 2026-09-09 | Terminal UI Framework | [GitHub](https://github.com/charmbracelet/bubbletea.git) |
| **[spf13/cobra](https://github.com/spf13/cobra)** | ⭐ 44,604 | 📅 2026-07-11 | CLI Application Engine | [GitHub](https://github.com/spf13/cobra.git) |
| **[stretchr/testify](https://github.com/stretchr/testify)** | ⭐ 26,204 | 📅 2026-09-02 | Test Toolkit & Mocking | [GitHub](https://github.com/stretchr/testify.git) |
| **[gorilla/websocket](https://github.com/gorilla/websocket)** | ⭐ 24,869 | 📅 2025-03-19 | WebSocket Protocol Engine | [GitHub](https://github.com/gorilla/websocket.git) |
| **[uber-go/zap](https://github.com/uber-go/zap)** | ⭐ 24,652 | 📅 2026-08-31 | Fast Structured Logging | [GitHub](https://github.com/uber-go/zap.git) |
| **[go-chi/chi](https://github.com/go-chi/chi)** | ⭐ 22,831 | 📅 2026-09-08 | Composable HTTP Router | [GitHub](https://github.com/go-chi/chi.git) |
| **[pion/webrtc](https://github.com/pion/webrtc)** | ⭐ 16,780 | 📅 2026-09-15 | Pure Go WebRTC Stack | [GitHub](https://github.com/pion/webrtc.git) |
| **[tidwall/gjson](https://github.com/tidwall/gjson)** | ⭐ 15,557 | 📅 2026-08-28 | Fast JSON Parser | [GitHub](https://github.com/tidwall/gjson.git) |
| **[etcd-io/bbolt](https://github.com/etcd-io/bbolt)** | ⭐ 9,744 | 📅 2026-09-15 | Embedded Key/Value DB | [GitHub](https://github.com/etcd-io/bbolt.git) |
| **[alecthomas/chroma](https://github.com/alecthomas/chroma)** | ⭐ 5,034 | 📅 2026-09-15 | Syntax Highlighter | [GitHub](https://github.com/alecthomas/chroma.git) |

### Разбор архитектуры и тест-кейсов для CodeExplorer:

#### 📦 [charmbracelet/bubbletea](https://github.com/charmbracelet/bubbletea) — ⭐ 44,972 (2026-09-09)
* **Архитектурный паттерн:** `Elm Architecture (Model-Update-View)`
* **Зачем тестировать в CodeExplorer:** Идеальная архитектура: строгий цикл Init -> Update -> View, асинхронные команды `tea.Cmd`, чистые структуры данных.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/charmbracelet/bubbletea.git`

#### 📦 [spf13/cobra](https://github.com/spf13/cobra) — ⭐ 44,604 (2026-07-11)
* **Архитектурный паттерн:** `Hierarchical Command Tree / POSIX Flags`
* **Зачем тестировать в CodeExplorer:** Стандарт индустрии для CLI на Go: дерево команд `*cobra.Command`, наследование флагов, хуки исполнения.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/spf13/cobra.git`

#### 📦 [stretchr/testify](https://github.com/stretchr/testify) — ⭐ 26,204 (2026-09-02)
* **Архитектурный паттерн:** `Assertion Engine / Dynamic Mock Objects`
* **Зачем тестировать в CodeExplorer:** Проверка вызовов через рефлексию (reflect), генерация mock-объектов, сравнение структур.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/stretchr/testify.git`

#### 📦 [gorilla/websocket](https://github.com/gorilla/websocket) — ⭐ 24,869 (2025-03-19)
* **Архитектурный паттерн:** `Framing & Masking / Connection Loop`
* **Зачем тестировать в CodeExplorer:** Низкоуровневая реализация протокола RFC 6455: фрейминг, маскирование байт, потоковое чтение/запись.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/gorilla/websocket.git`

#### 📦 [uber-go/zap](https://github.com/uber-go/zap) — ⭐ 24,652 (2026-08-31)
* **Архитектурный паттерн:** `Zero-Allocation Memory Pooling / Core Encoder`
* **Зачем тестировать в CodeExplorer:** Экстремальная оптимизация памяти: `sync.Pool`, строгая типизация полей (Field), буферизация байт.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/uber-go/zap.git`

#### 📦 [go-chi/chi](https://github.com/go-chi/chi) — ⭐ 22,831 (2026-09-08)
* **Архитектурный паттерн:** `Radix Tree / Context Propagation`
* **Зачем тестировать в CodeExplorer:** Эталон чистого Go: роутер на префиксном дереве, стандартный `http.Handler`, сквозная передача контекста.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/go-chi/chi.git`

#### 📦 [pion/webrtc](https://github.com/pion/webrtc) — ⭐ 16,780 (2026-09-15)
* **Архитектурный паттерн:** `PeerConnection / RTP/RTCP Networking`
* **Зачем тестировать в CodeExplorer:** Чистая Go реализация WebRTC без C-зависимостей: сетевые сокеты, шифрование DTLS/SRTP, медиа-треки.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/pion/webrtc.git`

#### 📦 [tidwall/gjson](https://github.com/tidwall/gjson) — ⭐ 15,557 (2026-08-28)
* **Архитектурный паттерн:** `Zero-Alloc String Scanning / Path Syntax`
* **Зачем тестировать в CodeExplorer:** Поиск значений в JSON строке без парсинга всего документа и без аллокаций: токенайзер, синтаксис путей a.b.c.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/tidwall/gjson.git`

#### 📦 [etcd-io/bbolt](https://github.com/etcd-io/bbolt) — ⭐ 9,744 (2026-09-15)
* **Архитектурный паттерн:** `B+ Tree Storage Engine / Memory Mapped File`
* **Зачем тестировать в CodeExplorer:** Гениальная архитектура БД на ~4k строк: структура B+ дерева, ACID транзакции через `mmap()`, бакеты.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/etcd-io/bbolt.git`

#### 📦 [alecthomas/chroma](https://github.com/alecthomas/chroma) — ⭐ 5,034 (2026-09-15)
* **Архитектурный паттерн:** `Lexer Pipeline / Token Formatter Engine`
* **Зачем тестировать в CodeExplorer:** Парсинг исходного кода: лексеры на регулярных выражениях, токенизация, цветовое форматирование в HTML/ANSI.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/alecthomas/chroma.git`

## 🔹 Java (10 компактных проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный фокус | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[square/okhttp](https://github.com/square/okhttp)** | ⭐ 47,059 | 📅 2026-09-15 | HTTP & HTTP/2 Engine | [GitHub](https://github.com/square/okhttp.git) |
| **[square/retrofit](https://github.com/square/retrofit)** | ⭐ 43,938 | 📅 2026-09-09 | Type-Safe HTTP Client | [GitHub](https://github.com/square/retrofit.git) |
| **[google/gson](https://github.com/google/gson)** | ⭐ 24,236 | 📅 2026-09-14 | JSON Serialization | [GitHub](https://github.com/google/gson.git) |
| **[resilience4j/resilience4j](https://github.com/resilience4j/resilience4j)** | ⭐ 10,760 | 📅 2026-08-31 | Fault Tolerance Library | [GitHub](https://github.com/resilience4j/resilience4j.git) |
| **[mapstruct/mapstruct](https://github.com/mapstruct/mapstruct)** | ⭐ 7,692 | 📅 2026-08-07 | Bean Mapping Generator | [GitHub](https://github.com/mapstruct/mapstruct.git) |
| **[auth0/java-jwt](https://github.com/auth0/java-jwt)** | ⭐ 6,235 | 📅 2026-09-14 | JWT Cryptography | [GitHub](https://github.com/auth0/java-jwt.git) |
| **[javaparser/javaparser](https://github.com/javaparser/javaparser)** | ⭐ 6,149 | 📅 2026-09-15 | Java AST Parser & Analysis | [GitHub](https://github.com/javaparser/javaparser.git) |
| **[uber/NullAway](https://github.com/uber/NullAway)** | ⭐ 4,104 | 📅 2026-09-16 | Static Bug Detector | [GitHub](https://github.com/uber/NullAway.git) |
| **[RoaringBitmap/RoaringBitmap](https://github.com/RoaringBitmap/RoaringBitmap)** | ⭐ 3,930 | 📅 2026-09-10 | Compressed Data Structures | [GitHub](https://github.com/RoaringBitmap/RoaringBitmap.git) |
| **[cbeust/jcommander](https://github.com/cbeust/jcommander)** | ⭐ 2,024 | 📅 2026-04-15 | CLI Argument Parser | [GitHub](https://github.com/cbeust/jcommander.git) |

### Разбор архитектуры и тест-кейсов для CodeExplorer:

#### 📦 [square/okhttp](https://github.com/square/okhttp) — ⭐ 47,059 (2026-09-15)
* **Архитектурный паттерн:** `Interceptor Chain / Connection Pooling`
* **Зачем тестировать в CodeExplorer:** Классический паттерн Chain of Responsibility: перехватчики запросов, пулинг сокетов, кэширование.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/square/okhttp.git`

#### 📦 [square/retrofit](https://github.com/square/retrofit) — ⭐ 43,938 (2026-09-09)
* **Архитектурный паттерн:** `Dynamic Proxy / Annotation Processing`
* **Зачем тестировать в CodeExplorer:** Динамические прокси Java (`Proxy.newProxyInstance`), преобразование аннотаций интерфейса в HTTP вызовы.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/square/retrofit.git`

#### 📦 [google/gson](https://github.com/google/gson) — ⭐ 24,236 (2026-09-14)
* **Архитектурный паттерн:** `TypeAdapter Pipeline / Reflection Factory`
* **Зачем тестировать в CodeExplorer:** Конвейер `TypeAdapter`, обход полей через рефлексию, типобезопасные фабрики сериализации.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/google/gson.git`

#### 📦 [resilience4j/resilience4j](https://github.com/resilience4j/resilience4j) — ⭐ 10,760 (2026-08-31)
* **Архитектурный паттерн:** `Functional Composition / Event-Driven State`
* **Зачем тестировать в CodeExplorer:** Функциональный подход в Java (Vavr): композиция функций декораторов, конечные автоматы Circuit Breaker.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/resilience4j/resilience4j.git`

#### 📦 [mapstruct/mapstruct](https://github.com/mapstruct/mapstruct) — ⭐ 7,692 (2026-08-07)
* **Архитектурный паттерн:** `Annotation Processor (JSR 269)`
* **Зачем тестировать в CodeExplorer:** Кодогенерация на этапе компиляции: генерация реализаций мапперов без применения рефлексии в рантайме.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/mapstruct/mapstruct.git`

#### 📦 [auth0/java-jwt](https://github.com/auth0/java-jwt) — ⭐ 6,235 (2026-09-14)
* **Архитектурный паттерн:** `Token Encoder & Verifier / Crypto Algorithms`
* **Зачем тестировать в CodeExplorer:** Криптографические алгоритмы HMAC/RSA/ECDSA, парсинг JSON payload, верификация клеймов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/auth0/java-jwt.git`

#### 📦 [javaparser/javaparser](https://github.com/javaparser/javaparser) — ⭐ 6,149 (2026-09-15)
* **Архитектурный паттерн:** `Visitor Pattern / AST Manipulation`
* **Зачем тестировать в CodeExplorer:** Парсер синтаксиса Java на самой Java: паттерн Visitor, обход узлов AST, разрешение типов (SymbolSolver).
* **Команда для клонирования:** `git clone --depth 1 https://github.com/javaparser/javaparser.git`

#### 📦 [uber/NullAway](https://github.com/uber/NullAway) — ⭐ 4,104 (2026-09-16)
* **Архитектурный паттерн:** `Compiler Plugin / Dataflow Analysis`
* **Зачем тестировать в CodeExplorer:** Плагин компилятора javac: статический анализ потока данных (dataflow analysis), обнаружение null dereferences.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/uber/NullAway.git`

#### 📦 [RoaringBitmap/RoaringBitmap](https://github.com/RoaringBitmap/RoaringBitmap) — ⭐ 3,930 (2026-09-10)
* **Архитектурный паттерн:** `Hybrid Run-Length / Array / Bitset Engine`
* **Зачем тестировать в CodeExplorer:** Высокопроизводительные битовые массивы: сжатие данных, побитовые булевы операции (AND, OR, XOR), SIMD.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/RoaringBitmap/RoaringBitmap.git`

#### 📦 [cbeust/jcommander](https://github.com/cbeust/jcommander) — ⭐ 2,024 (2026-04-15)
* **Архитектурный паттерн:** `Annotation-Driven Parameter Binding`
* **Зачем тестировать в CodeExplorer:** Декларативное связывание аргументов командной строки через аннотации `@Parameter`, конвертеры типов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/cbeust/jcommander.git`

## 🔹 Python (10 компактных проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный фокус | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[Textualize/rich](https://github.com/Textualize/rich)** | ⭐ 57,368 | 📅 2026-06-23 | Terminal Formatting & Layout | [GitHub](https://github.com/Textualize/rich.git) |
| **[psf/requests](https://github.com/psf/requests)** | ⭐ 54,304 | 📅 2026-09-07 | HTTP Library for Humans | [GitHub](https://github.com/psf/requests.git) |
| **[tqdm/tqdm](https://github.com/tqdm/tqdm)** | ⭐ 31,339 | 📅 2026-09-11 | Progress Bar Meter | [GitHub](https://github.com/tqdm/tqdm.git) |
| **[pallets/click](https://github.com/pallets/click)** | ⭐ 17,667 | 📅 2026-09-12 | Composable CLI Toolkit | [GitHub](https://github.com/pallets/click.git) |
| **[encode/starlette](https://github.com/encode/starlette)** | ⭐ 12,618 | 📅 2026-09-12 | Lightweight ASGI Toolkit | [GitHub](https://github.com/encode/starlette.git) |
| **[coleifer/peewee](https://github.com/coleifer/peewee)** | ⭐ 11,986 | 📅 2026-09-14 | Compact Expressive ORM | [GitHub](https://github.com/coleifer/peewee.git) |
| **[pallets/jinja](https://github.com/pallets/jinja)** | ⭐ 11,779 | 📅 2025-06-14 | Template Engine | [GitHub](https://github.com/pallets/jinja.git) |
| **[bottlepy/bottle](https://github.com/bottlepy/bottle)** | ⭐ 8,789 | 📅 2026-09-15 | Micro Web Framework | [GitHub](https://github.com/bottlepy/bottle.git) |
| **[marshmallow-code/marshmallow](https://github.com/marshmallow-code/marshmallow)** | ⭐ 7,238 | 📅 2026-09-15 | Object Serialization & Schemas | [GitHub](https://github.com/marshmallow-code/marshmallow.git) |
| **[samuelcolvin/watchfiles](https://github.com/samuelcolvin/watchfiles)** | ⭐ 2,533 | 📅 2026-09-03 | Filesystem Watcher | [GitHub](https://github.com/samuelcolvin/watchfiles.git) |

### Разбор архитектуры и тест-кейсов для CodeExplorer:

#### 📦 [Textualize/rich](https://github.com/Textualize/rich) — ⭐ 57,368 (2026-06-23)
* **Архитектурный паттерн:** `Renderable Protocol / Console Layout Grid`
* **Зачем тестировать в CodeExplorer:** Протокол `__rich_console__`, раскладка таблиц и панелей в терминале, парсинг ANSI и стилей.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/Textualize/rich.git`

#### 📦 [psf/requests](https://github.com/psf/requests) — ⭐ 54,304 (2026-09-07)
* **Архитектурный паттерн:** `Transport Adapter Pattern / Session Pooling`
* **Зачем тестировать в CodeExplorer:** Классика чистого Python: адаптеры транспорта (`HTTPAdapter`), сессии, пулы соединений urllib3 (~4k строк).
* **Команда для клонирования:** `git clone --depth 1 https://github.com/psf/requests.git`

#### 📦 [tqdm/tqdm](https://github.com/tqdm/tqdm) — ⭐ 31,339 (2026-09-11)
* **Архитектурный паттерн:** `Iterator Decorator / Terminal Control`
* **Зачем тестировать в CodeExplorer:** Умный генератор-обертка: переопределение `__iter__`, измерение скорости, динамический вывод в stderr.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/tqdm/tqdm.git`

#### 📦 [pallets/click](https://github.com/pallets/click) — ⭐ 17,667 (2026-09-12)
* **Архитектурный паттерн:** `Decorator-Driven Command Tree / Context`
* **Зачем тестировать в CodeExplorer:** Дерево команд на декораторах (`@click.command`), управление контекстом (`click.Context`), типы параметров.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/pallets/click.git`

#### 📦 [encode/starlette](https://github.com/encode/starlette) — ⭐ 12,618 (2026-09-12)
* **Архитектурный паттерн:** `ASGI Protocol / Middleware Stack`
* **Зачем тестировать в CodeExplorer:** Основа FastAPI: асинхронный интерфейс ASGI, цепочки middleware, HTTP и WebSocket роутинг.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/encode/starlette.git`

#### 📦 [coleifer/peewee](https://github.com/coleifer/peewee) — ⭐ 11,986 (2026-09-14)
* **Архитектурный паттерн:** `Metaclass Model Registry / SQL Node Tree`
* **Зачем тестировать в CodeExplorer:** Компактный ORM (~7k строк): метаклассы для моделей, дерево узлов SQL выражений (Node), контекст транзакций.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/coleifer/peewee.git`

#### 📦 [pallets/jinja](https://github.com/pallets/jinja) — ⭐ 11,779 (2025-06-14)
* **Архитектурный паттерн:** `Lexer -> Parser -> Python Bytecode Compiler`
* **Зачем тестировать в CodeExplorer:** Компиляция шаблонов в байткод Python: лексер, парсер грамматики шаблонов, среда песочницы (Sandbox).
* **Команда для клонирования:** `git clone --depth 1 https://github.com/pallets/jinja.git`

#### 📦 [bottlepy/bottle](https://github.com/bottlepy/bottle) — ⭐ 8,789 (2026-09-15)
* **Архитектурный паттерн:** `Single-File Web Framework / Router Engine`
* **Зачем тестировать в CodeExplorer:** Полный веб-фреймворк в одном файле: собственный роутер, шаблонизатор, адаптеры серверов (~4k строк).
* **Команда для клонирования:** `git clone --depth 1 https://github.com/bottlepy/bottle.git`

#### 📦 [marshmallow-code/marshmallow](https://github.com/marshmallow-code/marshmallow) — ⭐ 7,238 (2026-09-15)
* **Архитектурный паттерн:** `Schema Field Declaration / Validation`
* **Зачем тестировать в CodeExplorer:** Декларативные схемы, валидация данных, вложенные поля (Nested), трансформация данных.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/marshmallow-code/marshmallow.git`

#### 📦 [samuelcolvin/watchfiles](https://github.com/samuelcolvin/watchfiles) — ⭐ 2,533 (2026-09-03)
* **Архитектурный паттерн:** `Rust Extension (Notify) / Async Iterator`
* **Зачем тестировать в CodeExplorer:** Интеграция Python с Rust через PyO3, асинхронный генератор событий изменения файлов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/samuelcolvin/watchfiles.git`

---

## ⚡ Преимущества компактного бенчмарка для AI-Агента

1. **Мгновенное клонирование и индексация (1–5 секунд):**
   Каждый проект занимает от 1 до 15 МБ, клонируется с `--depth 1` моментально и парсится без риска исчерпания RAM.

2. **Высокая концентрация идиоматического кода:**
   В небольших библиотеках авторы уделяют максимум внимания чистоте API, строгой типизации, дженерикам и паттернам (отсутствует лишний legacy-код и гигантские сгенерированные файлы).

3. **Идеально для итеративной отладки парсеров (Feedback Loop):**
   Модель может за один диалоговый шаг прогнать индексацию, получить компактный отчёт об ошибках парсинга, исправить грамматику Tree-sitter / Roslyn в CodeExplorer и сразу же перезапустить тесты.