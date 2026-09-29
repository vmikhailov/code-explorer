# 🧭 CodeExplorer Benchmark: Top 50 Compact Projects (Compact & Focused)

> **Registry Purpose:** A curated pool of lightweight, clean, and architecturally verified open-source libraries and micro-tools (typically 1,000 to 25,000 lines of code).
> Unlike giant monoliths, these projects are ideal for **fast automated traversal, instantaneous indexing, and end-to-end testing of CodeExplorer** via LLM agents. The agent can clone a project in seconds, run AST parsers, test symbol resolution, and discover new dependency patterns without overloading context or memory.

---

## 📊 Selection Parameters Summary

| Language | Projects | Star Range | Codebase Size | Architectural Focus |
| :--- | :---: | :---: | :---: | :--- |
| **C#** | 10 | 3.6k – 18k ⭐ | ~2k – 20k LOC | Micro-ORM, Resilience pipelines, TUI, State machines, Expression trees |
| **TypeScript** | 10 | 9.7k – 61k ⭐ | ~1k – 15k LOC | Pure state containers, Type-inference schemas, Virtual DOM, Micro-routers |
| **Go** | 10 | 5.0k – 45k ⭐ | ~2k – 25k LOC | Embedded B+ Tree DB, Radix HTTP router, Elm TUI, Zero-alloc logging |
| **Java** | 10 | 2.0k – 47k ⭐ | ~3k – 30k LOC | Interceptor chains, Dynamic proxies, Annotation processors, AST visitors |
| **Python** | 10 | 2.5k – 57k ⭐ | ~1k – 15k LOC | Transport adapters, Decorator CLI, Compact ORM, Single-file micro-framework |

---

## 🔹 C# (10 Compact Projects)

| Repository | Stars | Last Commit | Architectural Focus | Link |
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

### Architecture Breakdown and Test Cases for CodeExplorer:

#### 📦 [DapperLib/Dapper](https://github.com/DapperLib/Dapper) — ⭐ 18,385 (2026-09-12)
* **Architectural Pattern:** `Extension Methods / Dynamic IL / Fast Object Mapping`
* **Why Test in CodeExplorer:** Компактный micro-ORM: генерация IL кода на лету, расширения для IDbConnection, замер точности маппинга типов.
* **Clone Command:** `git clone --depth 1 https://github.com/DapperLib/Dapper.git`

#### 📦 [App-vNext/Polly](https://github.com/App-vNext/Polly) — ⭐ 14,236 (2026-09-14)
* **Architectural Pattern:** `Fluent Policy Pipeline / Strategy Pattern`
* **Why Test in CodeExplorer:** Пайплайны стратегий (Retry, Circuit Breaker, Timeout, Fallback), богатый generic интерфейс, чистый асинхронный код.
* **Clone Command:** `git clone --depth 1 https://github.com/App-vNext/Polly.git`

#### 📦 [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console) — ⭐ 11,621 (2026-09-15)
* **Architectural Pattern:** `ANSI Parsing / Widget Hierarchy / Fluent API`
* **Why Test in CodeExplorer:** Консольные виджеты, деревья, таблицы, разбор ANSI разметки, отличная модульная объектная модель.
* **Clone Command:** `git clone --depth 1 https://github.com/spectreconsole/spectre.console.git`

#### 📦 [AutoMapper/AutoMapper](https://github.com/AutoMapper/AutoMapper) — ⭐ 10,189 (2026-09-09)
* **Architectural Pattern:** `Expression Tree Compiler / Reflection Engine`
* **Why Test in CodeExplorer:** Компиляция деревьев выражений (Expression Trees), вывод типов, сопоставление свойств объектов.
* **Clone Command:** `git clone --depth 1 https://github.com/AutoMapper/AutoMapper.git`

#### 📦 [Humanizr/Humanizer](https://github.com/Humanizr/Humanizer) — ⭐ 9,881 (2026-09-11)
* **Architectural Pattern:** `Extension-First Utility Architecture`
* **Why Test in CodeExplorer:** Сотни расширений для базовых типов (string, DateTime, TimeSpan, enum), локализационные ресурсы.
* **Clone Command:** `git clone --depth 1 https://github.com/Humanizr/Humanizer.git`

#### 📦 [MessagePack-CSharp/MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp) — ⭐ 6,780 (2026-09-15)
* **Architectural Pattern:** `High-Perf Zero-Alloc Buffers / Dynamic CodeGen`
* **Why Test in CodeExplorer:** Работа с Span<T>, Memory<T>, динамическая генерация сериализаторов, оптимизация под нулевые аллокации.
* **Clone Command:** `git clone --depth 1 https://github.com/MessagePack-CSharp/MessagePack-CSharp.git`

#### 📦 [dotnet-state-machine/stateless](https://github.com/dotnet-state-machine/stateless) — ⭐ 6,260 (2026-04-04)
* **Architectural Pattern:** `Hierarchical State Pattern / Fluent Builder`
* **Why Test in CodeExplorer:** Иерархические конечные автоматы, типизированные триггеры, лямбда-переходы, минимум зависимостей (~3k LOC).
* **Clone Command:** `git clone --depth 1 https://github.com/dotnet-state-machine/stateless.git`

#### 📦 [morelinq/MoreLINQ](https://github.com/morelinq/MoreLINQ) — ⭐ 3,836 (2025-11-25)
* **Architectural Pattern:** `Iterator Functions / Lazy Evaluation Pipelines`
* **Why Test in CodeExplorer:** Десятки операторов над IEnumerable<T>: отложенное вычисление (yield return), обработка граничных условий.
* **Clone Command:** `git clone --depth 1 https://github.com/morelinq/MoreLINQ.git`

#### 📦 [fluentassertions/fluentassertions](https://github.com/fluentassertions/fluentassertions) — ⭐ 3,815 (2026-09-15)
* **Architectural Pattern:** `Fluent Chaining / Recursive Equivalence`
* **Why Test in CodeExplorer:** Рекурсивное сравнение графов объектов, цепочки методов Fluent API, перегрузка операторов.
* **Clone Command:** `git clone --depth 1 https://github.com/fluentassertions/fluentassertions.git`

#### 📦 [dotnet/command-line-api](https://github.com/dotnet/command-line-api) — ⭐ 3,675 (2026-09-15)
* **Architectural Pattern:** `Hierarchical Command Tree / Tokenizer`
* **Why Test in CodeExplorer:** Официальная библиотека парсинга CLI: дерево команд, связывание аргументов с моделями, middleware парсинга.
* **Clone Command:** `git clone --depth 1 https://github.com/dotnet/command-line-api.git`

## 🔹 TypeScript (10 Compact Projects)

| Repository | Stars | Last Commit | Architectural Focus | Link |
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

### Architecture Breakdown and Test Cases for CodeExplorer:

#### 📦 [reduxjs/redux](https://github.com/reduxjs/redux) — ⭐ 61,486 (2026-09-08)
* **Architectural Pattern:** `Functional / Pure Reducers / Middleware Pipeline`
* **Why Test in CodeExplorer:** Канонический образец функциональной архитектуры: чистые функции, композиция middleware, ~2k строк чистейшего кода.
* **Clone Command:** `git clone --depth 1 https://github.com/reduxjs/redux.git`

#### 📦 [colinhacks/zod](https://github.com/colinhacks/zod) — ⭐ 43,953 (2026-09-14)
* **Architectural Pattern:** `Type Inference Engine / Composability`
* **Why Test in CodeExplorer:** Вершина системы типов TypeScript: вывод сложных типов (`z.infer<T>`), парсинг и валидация без внешних зависимостей.
* **Clone Command:** `git clone --depth 1 https://github.com/colinhacks/zod.git`

#### 📦 [preactjs/preact](https://github.com/preactjs/preact) — ⭐ 38,868 (2026-09-15)
* **Architectural Pattern:** `Micro Virtual DOM / Diffing Algorithm`
* **Why Test in CodeExplorer:** Полноценный React-совместимый Virtual DOM всего в 3 кБ: алгоритмы дифф-сравнения деревьев, хуки, компонентный жизненный цикл.
* **Clone Command:** `git clone --depth 1 https://github.com/preactjs/preact.git`

#### 📦 [fastify/fastify](https://github.com/fastify/fastify) — ⭐ 37,145 (2026-09-15)
* **Architectural Pattern:** `Plugin Tree / JSON Schema Compilation`
* **Why Test in CodeExplorer:** Дерево плагинов (Avvio), прекомпиляция схем валидации (fast-json-stringify), быстрый роутер.
* **Clone Command:** `git clone --depth 1 https://github.com/fastify/fastify.git`

#### 📦 [drizzle-team/drizzle-orm](https://github.com/drizzle-team/drizzle-orm) — ⭐ 35,777 (2026-09-15)
* **Architectural Pattern:** `SQL AST Builder / Schema Compiler`
* **Why Test in CodeExplorer:** Построение SQL-запросов на уровне типов TS: строгая проверка колонок, отношений, генерация миграций.
* **Clone Command:** `git clone --depth 1 https://github.com/drizzle-team/drizzle-orm.git`

#### 📦 [honojs/hono](https://github.com/honojs/hono) — ⭐ 32,197 (2026-09-15)
* **Architectural Pattern:** `RegExp Router / Multi-Target Runtime`
* **Why Test in CodeExplorer:** Легковесный современный веб-фреймворк под Node, Deno, Bun и Cloudflare: сверхбыстрый роутер на регексах, контекст.
* **Clone Command:** `git clone --depth 1 https://github.com/honojs/hono.git`

#### 📦 [lucide-icons/lucide](https://github.com/lucide-icons/lucide) — ⭐ 24,529 (2026-09-14)
* **Architectural Pattern:** `Multi-Package Component Generator`
* **Why Test in CodeExplorer:** Пайплайн кодогенерации: SVG узлы транслируются в типизированные React/Vue/Svelte компоненты.
* **Clone Command:** `git clone --depth 1 https://github.com/lucide-icons/lucide.git`

#### 📦 [sindresorhus/ky](https://github.com/sindresorhus/ky) — ⭐ 17,071 (2026-09-14)
* **Architectural Pattern:** `Fetch Wrapper / Hook Chain Architecture`
* **Why Test in CodeExplorer:** Элегантная обертка над native Fetch: хуки `beforeRequest`/`afterResponse`, таймауты, retry логика.
* **Clone Command:** `git clone --depth 1 https://github.com/sindresorhus/ky.git`

#### 📦 [paulmillr/chokidar](https://github.com/paulmillr/chokidar) — ⭐ 12,239 (2026-08-16)
* **Architectural Pattern:** `OS Event Normalization / Debouncing`
* **Why Test in CodeExplorer:** Нормализация файловых событий ОС (fsevents/inotify/win32), управление таймерами, устранение дубликатов.
* **Clone Command:** `git clone --depth 1 https://github.com/paulmillr/chokidar.git`

#### 📦 [sindresorhus/ora](https://github.com/sindresorhus/ora) — ⭐ 9,747 (2026-06-22)
* **Architectural Pattern:** `ANSI Stream Engine / Event Loop TTY`
* **Why Test in CodeExplorer:** Компактная TUI утилита: управление потоком stdout, ANSI escape последовательности, очистка терминала.
* **Clone Command:** `git clone --depth 1 https://github.com/sindresorhus/ora.git`

## 🔹 Go (10 Compact Projects)

| Repository | Stars | Last Commit | Architectural Focus | Link |
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

### Architecture Breakdown and Test Cases for CodeExplorer:

#### 📦 [charmbracelet/bubbletea](https://github.com/charmbracelet/bubbletea) — ⭐ 44,972 (2026-09-09)
* **Architectural Pattern:** `Elm Architecture (Model-Update-View)`
* **Why Test in CodeExplorer:** Идеальная архитектура: строгий цикл Init -> Update -> View, асинхронные команды `tea.Cmd`, чистые структуры данных.
* **Clone Command:** `git clone --depth 1 https://github.com/charmbracelet/bubbletea.git`

#### 📦 [spf13/cobra](https://github.com/spf13/cobra) — ⭐ 44,604 (2026-07-11)
* **Architectural Pattern:** `Hierarchical Command Tree / POSIX Flags`
* **Why Test in CodeExplorer:** Стандарт индустрии для CLI на Go: дерево команд `*cobra.Command`, наследование флагов, хуки исполнения.
* **Clone Command:** `git clone --depth 1 https://github.com/spf13/cobra.git`

#### 📦 [stretchr/testify](https://github.com/stretchr/testify) — ⭐ 26,204 (2026-09-02)
* **Architectural Pattern:** `Assertion Engine / Dynamic Mock Objects`
* **Why Test in CodeExplorer:** Проверка вызовов через рефлексию (reflect), генерация mock-объектов, сравнение структур.
* **Clone Command:** `git clone --depth 1 https://github.com/stretchr/testify.git`

#### 📦 [gorilla/websocket](https://github.com/gorilla/websocket) — ⭐ 24,869 (2025-03-19)
* **Architectural Pattern:** `Framing & Masking / Connection Loop`
* **Why Test in CodeExplorer:** Низкоуровневая реализация протокола RFC 6455: фрейминг, маскирование байт, потоковое чтение/запись.
* **Clone Command:** `git clone --depth 1 https://github.com/gorilla/websocket.git`

#### 📦 [uber-go/zap](https://github.com/uber-go/zap) — ⭐ 24,652 (2026-08-31)
* **Architectural Pattern:** `Zero-Allocation Memory Pooling / Core Encoder`
* **Why Test in CodeExplorer:** Экстремальная оптимизация памяти: `sync.Pool`, строгая типизация полей (Field), буферизация байт.
* **Clone Command:** `git clone --depth 1 https://github.com/uber-go/zap.git`

#### 📦 [go-chi/chi](https://github.com/go-chi/chi) — ⭐ 22,831 (2026-09-08)
* **Architectural Pattern:** `Radix Tree / Context Propagation`
* **Why Test in CodeExplorer:** Эталон чистого Go: роутер на префиксном дереве, стандартный `http.Handler`, сквозная передача контекста.
* **Clone Command:** `git clone --depth 1 https://github.com/go-chi/chi.git`

#### 📦 [pion/webrtc](https://github.com/pion/webrtc) — ⭐ 16,780 (2026-09-15)
* **Architectural Pattern:** `PeerConnection / RTP/RTCP Networking`
* **Why Test in CodeExplorer:** Чистая Go реализация WebRTC без C-зависимостей: сетевые сокеты, шифрование DTLS/SRTP, медиа-треки.
* **Clone Command:** `git clone --depth 1 https://github.com/pion/webrtc.git`

#### 📦 [tidwall/gjson](https://github.com/tidwall/gjson) — ⭐ 15,557 (2026-08-28)
* **Architectural Pattern:** `Zero-Alloc String Scanning / Path Syntax`
* **Why Test in CodeExplorer:** Поиск значений в JSON строке без парсинга всего документа и без аллокаций: токенайзер, синтаксис путей a.b.c.
* **Clone Command:** `git clone --depth 1 https://github.com/tidwall/gjson.git`

#### 📦 [etcd-io/bbolt](https://github.com/etcd-io/bbolt) — ⭐ 9,744 (2026-09-15)
* **Architectural Pattern:** `B+ Tree Storage Engine / Memory Mapped File`
* **Why Test in CodeExplorer:** Гениальная архитектура БД на ~4k строк: структура B+ дерева, ACID транзакции через `mmap()`, бакеты.
* **Clone Command:** `git clone --depth 1 https://github.com/etcd-io/bbolt.git`

#### 📦 [alecthomas/chroma](https://github.com/alecthomas/chroma) — ⭐ 5,034 (2026-09-15)
* **Architectural Pattern:** `Lexer Pipeline / Token Formatter Engine`
* **Why Test in CodeExplorer:** Парсинг исходного кода: лексеры на регулярных выражениях, токенизация, цветовое форматирование в HTML/ANSI.
* **Clone Command:** `git clone --depth 1 https://github.com/alecthomas/chroma.git`

## 🔹 Java (10 Compact Projects)

| Repository | Stars | Last Commit | Architectural Focus | Link |
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

### Architecture Breakdown and Test Cases for CodeExplorer:

#### 📦 [square/okhttp](https://github.com/square/okhttp) — ⭐ 47,059 (2026-09-15)
* **Architectural Pattern:** `Interceptor Chain / Connection Pooling`
* **Why Test in CodeExplorer:** Классический паттерн Chain of Responsibility: перехватчики запросов, пулинг сокетов, кэширование.
* **Clone Command:** `git clone --depth 1 https://github.com/square/okhttp.git`

#### 📦 [square/retrofit](https://github.com/square/retrofit) — ⭐ 43,938 (2026-09-09)
* **Architectural Pattern:** `Dynamic Proxy / Annotation Processing`
* **Why Test in CodeExplorer:** Динамические прокси Java (`Proxy.newProxyInstance`), преобразование аннотаций интерфейса в HTTP вызовы.
* **Clone Command:** `git clone --depth 1 https://github.com/square/retrofit.git`

#### 📦 [google/gson](https://github.com/google/gson) — ⭐ 24,236 (2026-09-14)
* **Architectural Pattern:** `TypeAdapter Pipeline / Reflection Factory`
* **Why Test in CodeExplorer:** Конвейер `TypeAdapter`, обход полей через рефлексию, типобезопасные фабрики сериализации.
* **Clone Command:** `git clone --depth 1 https://github.com/google/gson.git`

#### 📦 [resilience4j/resilience4j](https://github.com/resilience4j/resilience4j) — ⭐ 10,760 (2026-08-31)
* **Architectural Pattern:** `Functional Composition / Event-Driven State`
* **Why Test in CodeExplorer:** Функциональный подход в Java (Vavr): композиция функций декораторов, конечные автоматы Circuit Breaker.
* **Clone Command:** `git clone --depth 1 https://github.com/resilience4j/resilience4j.git`

#### 📦 [mapstruct/mapstruct](https://github.com/mapstruct/mapstruct) — ⭐ 7,692 (2026-08-07)
* **Architectural Pattern:** `Annotation Processor (JSR 269)`
* **Why Test in CodeExplorer:** Кодогенерация на этапе компиляции: генерация реализаций мапперов без применения рефлексии в рантайме.
* **Clone Command:** `git clone --depth 1 https://github.com/mapstruct/mapstruct.git`

#### 📦 [auth0/java-jwt](https://github.com/auth0/java-jwt) — ⭐ 6,235 (2026-09-14)
* **Architectural Pattern:** `Token Encoder & Verifier / Crypto Algorithms`
* **Why Test in CodeExplorer:** Криптографические алгоритмы HMAC/RSA/ECDSA, парсинг JSON payload, верификация клеймов.
* **Clone Command:** `git clone --depth 1 https://github.com/auth0/java-jwt.git`

#### 📦 [javaparser/javaparser](https://github.com/javaparser/javaparser) — ⭐ 6,149 (2026-09-15)
* **Architectural Pattern:** `Visitor Pattern / AST Manipulation`
* **Why Test in CodeExplorer:** Парсер синтаксиса Java на самой Java: паттерн Visitor, обход узлов AST, разрешение типов (SymbolSolver).
* **Clone Command:** `git clone --depth 1 https://github.com/javaparser/javaparser.git`

#### 📦 [uber/NullAway](https://github.com/uber/NullAway) — ⭐ 4,104 (2026-09-16)
* **Architectural Pattern:** `Compiler Plugin / Dataflow Analysis`
* **Why Test in CodeExplorer:** Плагин компилятора javac: статический анализ потока данных (dataflow analysis), обнаружение null dereferences.
* **Clone Command:** `git clone --depth 1 https://github.com/uber/NullAway.git`

#### 📦 [RoaringBitmap/RoaringBitmap](https://github.com/RoaringBitmap/RoaringBitmap) — ⭐ 3,930 (2026-09-10)
* **Architectural Pattern:** `Hybrid Run-Length / Array / Bitset Engine`
* **Why Test in CodeExplorer:** Высокопроизводительные битовые массивы: сжатие данных, побитовые булевы операции (AND, OR, XOR), SIMD.
* **Clone Command:** `git clone --depth 1 https://github.com/RoaringBitmap/RoaringBitmap.git`

#### 📦 [cbeust/jcommander](https://github.com/cbeust/jcommander) — ⭐ 2,024 (2026-04-15)
* **Architectural Pattern:** `Annotation-Driven Parameter Binding`
* **Why Test in CodeExplorer:** Декларативное связывание аргументов командной строки через аннотации `@Parameter`, конвертеры типов.
* **Clone Command:** `git clone --depth 1 https://github.com/cbeust/jcommander.git`

## 🔹 Python (10 Compact Projects)

| Repository | Stars | Last Commit | Architectural Focus | Link |
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

### Architecture Breakdown and Test Cases for CodeExplorer:

#### 📦 [Textualize/rich](https://github.com/Textualize/rich) — ⭐ 57,368 (2026-06-23)
* **Architectural Pattern:** `Renderable Protocol / Console Layout Grid`
* **Why Test in CodeExplorer:** Протокол `__rich_console__`, раскладка таблиц и панелей в терминале, парсинг ANSI и стилей.
* **Clone Command:** `git clone --depth 1 https://github.com/Textualize/rich.git`

#### 📦 [psf/requests](https://github.com/psf/requests) — ⭐ 54,304 (2026-09-07)
* **Architectural Pattern:** `Transport Adapter Pattern / Session Pooling`
* **Why Test in CodeExplorer:** Классика чистого Python: адаптеры транспорта (`HTTPAdapter`), сессии, пулы соединений urllib3 (~4k строк).
* **Clone Command:** `git clone --depth 1 https://github.com/psf/requests.git`

#### 📦 [tqdm/tqdm](https://github.com/tqdm/tqdm) — ⭐ 31,339 (2026-09-11)
* **Architectural Pattern:** `Iterator Decorator / Terminal Control`
* **Why Test in CodeExplorer:** Умный генератор-обертка: переопределение `__iter__`, измерение скорости, динамический вывод в stderr.
* **Clone Command:** `git clone --depth 1 https://github.com/tqdm/tqdm.git`

#### 📦 [pallets/click](https://github.com/pallets/click) — ⭐ 17,667 (2026-09-12)
* **Architectural Pattern:** `Decorator-Driven Command Tree / Context`
* **Why Test in CodeExplorer:** Дерево команд на декораторах (`@click.command`), управление контекстом (`click.Context`), типы параметров.
* **Clone Command:** `git clone --depth 1 https://github.com/pallets/click.git`

#### 📦 [encode/starlette](https://github.com/encode/starlette) — ⭐ 12,618 (2026-09-12)
* **Architectural Pattern:** `ASGI Protocol / Middleware Stack`
* **Why Test in CodeExplorer:** Основа FastAPI: асинхронный интерфейс ASGI, цепочки middleware, HTTP и WebSocket роутинг.
* **Clone Command:** `git clone --depth 1 https://github.com/encode/starlette.git`

#### 📦 [coleifer/peewee](https://github.com/coleifer/peewee) — ⭐ 11,986 (2026-09-14)
* **Architectural Pattern:** `Metaclass Model Registry / SQL Node Tree`
* **Why Test in CodeExplorer:** Компактный ORM (~7k строк): метаклассы для моделей, дерево узлов SQL выражений (Node), контекст транзакций.
* **Clone Command:** `git clone --depth 1 https://github.com/coleifer/peewee.git`

#### 📦 [pallets/jinja](https://github.com/pallets/jinja) — ⭐ 11,779 (2025-06-14)
* **Architectural Pattern:** `Lexer -> Parser -> Python Bytecode Compiler`
* **Why Test in CodeExplorer:** Компиляция шаблонов в байткод Python: лексер, парсер грамматики шаблонов, среда песочницы (Sandbox).
* **Clone Command:** `git clone --depth 1 https://github.com/pallets/jinja.git`

#### 📦 [bottlepy/bottle](https://github.com/bottlepy/bottle) — ⭐ 8,789 (2026-09-15)
* **Architectural Pattern:** `Single-File Web Framework / Router Engine`
* **Why Test in CodeExplorer:** Полный веб-фреймворк в одном файле: собственный роутер, шаблонизатор, адаптеры серверов (~4k строк).
* **Clone Command:** `git clone --depth 1 https://github.com/bottlepy/bottle.git`

#### 📦 [marshmallow-code/marshmallow](https://github.com/marshmallow-code/marshmallow) — ⭐ 7,238 (2026-09-15)
* **Architectural Pattern:** `Schema Field Declaration / Validation`
* **Why Test in CodeExplorer:** Декларативные схемы, валидация данных, вложенные поля (Nested), трансформация данных.
* **Clone Command:** `git clone --depth 1 https://github.com/marshmallow-code/marshmallow.git`

#### 📦 [samuelcolvin/watchfiles](https://github.com/samuelcolvin/watchfiles) — ⭐ 2,533 (2026-09-03)
* **Architectural Pattern:** `Rust Extension (Notify) / Async Iterator`
* **Why Test in CodeExplorer:** Интеграция Python с Rust через PyO3, асинхронный генератор событий изменения файлов.
* **Clone Command:** `git clone --depth 1 https://github.com/samuelcolvin/watchfiles.git`

---

## ⚡ Преимущества компактного бенчмарка для AI-Агента

1. **Мгновенное клонирование и индексация (1–5 секунд):**
   Каждый проект занимает от 1 до 15 МБ, клонируется с `--depth 1` моментально и парсится без риска исчерпания RAM.

2. **Высокая концентрация идиоматического кода:**
   В небольших библиотеках авторы уделяют максимум внимания чистоте API, строгой типизации, дженерикам и паттернам (отсутствует лишний legacy-код и гигантские сгенерированные файлы).

3. **Идеально для итеративной отладки парсеров (Feedback Loop):**
   Модель может за один диалоговый шаг прогнать индексацию, получить компактный отчёт об ошибках парсинга, исправить грамматику Tree-sitter / Roslyn в CodeExplorer и сразу же перезапустить тесты.