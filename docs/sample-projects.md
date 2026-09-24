# 🧭 CodeExplorer Benchmark: Топ-50 Проектов для Автономного Тестирования

> **Назначение реестра:** Эталонный пул разнообразных, активно развивающихся и высокозвездных open-source проектов на 5 языках (C#, TypeScript, Go, Java, Python).
> Модель-агент использует этот каталог для непрерывного обхода, парсинга, индексации и тестирования архитектурных запросов в **CodeExplorer**, выявляя узкие места парсеров (Roslyn, Tree-sitter), расширяя список поддерживаемых паттернов и обогащая встроенную базу библиотек.

---

## 📊 Сводная таблица параметров отбора

| Язык | Проектов | Диапазон звёзд | Архитектурные домены |
| :--- | :---: | :---: | :--- |
| **C#** | 10 | 19k – 57k ⭐ | Web Core, UI Engines (XAML/WinUI), Decompilers, CLI & AST, AI Agents, Game Engines |
| **TypeScript** | 10 | 109k – 204k ⭐ | IDE & Editor, Compiler Engine, Distributed BaaS, Workflow Graph, AI Coding Agents, Canvas 2D |
| **Go** | 10 | 64k – 181k ⭐ | Cloud Native, Compilers/SSA, Time-Series DB, Mod-Servers, P2P Sync, Terminal UI, AI Bridges |
| **Java** | 10 | 39k – 81k ⭐ | Search Engine, IoC / Enterprise, Binary Decompilers, OSGi Desktop, Reactive, Android Systems |
| **Python** | 10 | 14k – 191k ⭐ | Modern Async Web, Metaclass ORM, Machine Learning, IoT Monolith, AST Rewriting, Proxies |

---

## 🔹 C# (10 проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный домен | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[dotnet/aspnetcore](https://github.com/dotnet/aspnetcore)** | ⭐ 38,440 | 📅 2026-09-15 | Web & Cloud Framework | [GitHub](https://github.com/dotnet/aspnetcore.git) |
| **[jellyfin/jellyfin](https://github.com/jellyfin/jellyfin)** | ⭐ 57,173 | 📅 2026-09-15 | Media Server / Clean Backend | [GitHub](https://github.com/jellyfin/jellyfin.git) |
| **[AvaloniaUI/Avalonia](https://github.com/AvaloniaUI/Avalonia)** | ⭐ 31,509 | 📅 2026-09-15 | Cross-Platform UI Engine | [GitHub](https://github.com/AvaloniaUI/Avalonia.git) |
| **[PowerShell/PowerShell](https://github.com/PowerShell/PowerShell)** | ⭐ 55,413 | 📅 2026-09-15 | Scripting Engine / CLI Runtime | [GitHub](https://github.com/PowerShell/PowerShell.git) |
| **[files-community/Files](https://github.com/files-community/Files)** | ⭐ 45,442 | 📅 2026-09-15 | Modern Desktop File Manager | [GitHub](https://github.com/files-community/Files.git) |
| **[icsharpcode/ILSpy](https://github.com/icsharpcode/ILSpy)** | ⭐ 26,069 | 📅 2026-09-15 | Decompiler & Static Analysis | [GitHub](https://github.com/icsharpcode/ILSpy.git) |
| **[microsoft/semantic-kernel](https://github.com/microsoft/semantic-kernel)** | ⭐ 28,561 | 📅 2026-09-11 | Enterprise AI Agent Framework | [GitHub](https://github.com/microsoft/semantic-kernel.git) |
| **[DevToys-app/DevToys](https://github.com/DevToys-app/DevToys)** | ⭐ 31,999 | 📅 2026-02-25 | Developer Desktop Utilities | [GitHub](https://github.com/DevToys-app/DevToys.git) |
| **[ShareX/ShareX](https://github.com/ShareX/ShareX)** | ⭐ 39,585 | 📅 2026-09-15 | Desktop Utility & Media Pipeline | [GitHub](https://github.com/ShareX/ShareX.git) |
| **[ppy/osu](https://github.com/ppy/osu)** | ⭐ 19,062 | 📅 2026-09-15 | Game Engine & Audio Simulation | [GitHub](https://github.com/ppy/osu.git) |

### Детальный разбор тест-кейсов для CodeExplorer:

#### 📦 [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore) — ⭐ 38,440 (2026-09-15)
* **Описание:** ASP.NET Core is a cross-platform .NET framework for building modern cloud-based web applications on Windows, Mac, or Linux.
* **Архитектурный паттерн:** `Gigantic Monorepo / Layered Architecture`
* **Зачем тестировать в CodeExplorer:** Экстремальный тест для Roslyn/CodeExplorer: глубокая иерархия middleware, Source Generators, Dependency Injection, тысячи интерфейсов, generic host, SignalR сокеты.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/dotnet/aspnetcore.git`

#### 📦 [jellyfin/jellyfin](https://github.com/jellyfin/jellyfin) — ⭐ 57,173 (2026-09-15)
* **Описание:** The Free Software Media System - Server Backend & API
* **Архитектурный паттерн:** `Clean Architecture / EF Core / REST API`
* **Зачем тестировать в CodeExplorer:** Идеален для проверки Entity Framework связей, контроллеров API, сервисного слоя, плагинной архитектуры и асинхронных потоков задач.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/jellyfin/jellyfin.git`

#### 📦 [AvaloniaUI/Avalonia](https://github.com/AvaloniaUI/Avalonia) — ⭐ 31,509 (2026-09-15)
* **Описание:** Develop Desktop, Embedded, Mobile and WebAssembly apps with C# and XAML. The future of .NET UI
* **Архитектурный паттерн:** `Component-Based / Reactive / Compiler Engine`
* **Зачем тестировать в CodeExplorer:** Сложная система XAML компилятора, реактивные свойства (AvaloniaProperty), тяжелая иерархия контролов, кроссплатформенный рендеринг.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/AvaloniaUI/Avalonia.git`

#### 📦 [PowerShell/PowerShell](https://github.com/PowerShell/PowerShell) — ⭐ 55,413 (2026-09-15)
* **Описание:** PowerShell for every system!
* **Архитектурный паттерн:** `Runtime Interpreter & AST Engine`
* **Зачем тестировать в CodeExplorer:** Собственный парсер AST, DLR (Dynamic Language Runtime), пайплайны команд, богатые метаданные типов и reflection.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/PowerShell/PowerShell.git`

#### 📦 [files-community/Files](https://github.com/files-community/Files) — ⭐ 45,442 (2026-09-15)
* **Описание:** A modern file manager that helps users organize their files and folders.
* **Архитектурный паттерн:** `WinUI 3 / MVVM / Windows App SDK`
* **Зачем тестировать в CodeExplorer:** Тестирование паттерна MVVM, dependency injection, асинхронных операций с I/O, событийных шин и привязок данных XAML/C#.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/files-community/Files.git`

#### 📦 [icsharpcode/ILSpy](https://github.com/icsharpcode/ILSpy) — ⭐ 26,069 (2026-09-15)
* **Описание:** .NET Decompiler with support for PDB generation, ReadyToRun, Metadata (&more) - cross-platform!
* **Архитектурный паттерн:** `Static Analysis / AST Transformation / Cecil/SRM`
* **Зачем тестировать в CodeExplorer:** Прямой бенчмарк для AST: манипуляции с синтаксическими деревьями C#, чтение метаданных сборок (System.Reflection.Metadata), CFG графы потока управления.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/icsharpcode/ILSpy.git`

#### 📦 [microsoft/semantic-kernel](https://github.com/microsoft/semantic-kernel) — ⭐ 28,561 (2026-09-11)
* **Описание:** Integrate cutting-edge LLM technology quickly and easily into your apps
* **Архитектурный паттерн:** `Modular Plugin Architecture / Abstraction Layer`
* **Зачем тестировать в CodeExplorer:** Проверка вызовов плагинов через атрибуты (KernelFunction), динамического связывания интерфейсов LLM-провайдеров и цепочек фильтров.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/microsoft/semantic-kernel.git`

#### 📦 [DevToys-app/DevToys](https://github.com/DevToys-app/DevToys) — ⭐ 31,999 (2026-02-25)
* **Описание:** A Swiss Army knife for developers.
* **Архитектурный паттерн:** `Blazor Hybrid / Modular Micro-Tools`
* **Зачем тестировать в CodeExplorer:** Плагинная модульная архитектура: десятки независимых инструментов, динамическая загрузка расширений (MEF / dynamic loading).
* **Команда для клонирования:** `git clone --depth 1 https://github.com/DevToys-app/DevToys.git`

#### 📦 [ShareX/ShareX](https://github.com/ShareX/ShareX) — ⭐ 39,585 (2026-09-15)
* **Описание:** ShareX is a free and open-source application that enables users to capture or record any area of their screen with a single keystroke. It also supports uploading images, text, and various file types to a wide range of destinations.
* **Архитектурный паттерн:** `WinForms / Win32 P-Invoke / Pipeline`
* **Зачем тестировать в CodeExplorer:** Проверка P/Invoke interop вызовов к Win32 API, обработка графических конвейеров, сетевой стек загрузчиков.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/ShareX/ShareX.git`

#### 📦 [ppy/osu](https://github.com/ppy/osu) — ⭐ 19,062 (2026-09-15)
* **Описание:** rhythm is just a *click* away!
* **Архитектурный паттерн:** `Custom Game Framework (osu-framework) / ECS`
* **Зачем тестировать в CodeExplorer:** Огромная кодовая база игры: кастомный игровой движок, математические структуры данных, многопоточный игровой цикл, кастомные контейнеры компонентов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/ppy/osu.git`

## 🔹 TypeScript (10 проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный домен | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[microsoft/vscode](https://github.com/microsoft/vscode)** | ⭐ 192,568 | 📅 2026-09-16 | Desktop IDE / Extensible Editor | [GitHub](https://github.com/microsoft/vscode.git) |
| **[microsoft/TypeScript](https://github.com/microsoft/TypeScript)** | ⭐ 111,062 | 📅 2026-09-15 | Compiler / Type Checker Engine | [GitHub](https://github.com/microsoft/TypeScript.git) |
| **[supabase/supabase](https://github.com/supabase/supabase)** | ⭐ 109,329 | 📅 2026-09-15 | Backend-as-a-Service / Platform | [GitHub](https://github.com/supabase/supabase.git) |
| **[n8n-io/n8n](https://github.com/n8n-io/n8n)** | ⭐ 204,438 | 📅 2026-09-15 | Workflow Automation Engine | [GitHub](https://github.com/n8n-io/n8n.git) |
| **[vercel/next.js](https://github.com/vercel/next.js)** | ⭐ 142,333 | 📅 2026-09-16 | Full-Stack React Framework | [GitHub](https://github.com/vercel/next.js.git) |
| **[shadcn-ui/ui](https://github.com/shadcn-ui/ui)** | ⭐ 123,889 | 📅 2026-09-12 | Component System & CLI Generator | [GitHub](https://github.com/shadcn-ui/ui.git) |
| **[excalidraw/excalidraw](https://github.com/excalidraw/excalidraw)** | ⭐ 132,049 | 📅 2026-09-15 | Interactive Canvas & Collaborative Graphics | [GitHub](https://github.com/excalidraw/excalidraw.git) |
| **[immich-app/immich](https://github.com/immich-app/immich)** | ⭐ 114,312 | 📅 2026-09-15 | Self-Hosted Media Server | [GitHub](https://github.com/immich-app/immich.git) |
| **[firecrawl/firecrawl](https://github.com/firecrawl/firecrawl)** | ⭐ 180,867 | 📅 2026-09-15 | AI Web Scraping & Extraction Engine | [GitHub](https://github.com/firecrawl/firecrawl.git) |
| **[anthropics/claude-code](https://github.com/anthropics/claude-code)** | ⭐ 145,194 | 📅 2026-09-15 | Agentic CLI Coding Assistant | [GitHub](https://github.com/anthropics/claude-code.git) |

### Детальный разбор тест-кейсов для CodeExplorer:

#### 📦 [microsoft/vscode](https://github.com/microsoft/vscode) — ⭐ 192,568 (2026-09-16)
* **Описание:** Visual Studio Code
* **Архитектурный паттерн:** `Multi-Process / Electron / DI / Monorepo`
* **Зачем тестировать в CodeExplorer:** Эталонный тест для TS: миллионы строк, собственная DI-система (InstantiationService), изоляция Extension Host, IPC коммуникации.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/microsoft/vscode.git`

#### 📦 [microsoft/TypeScript](https://github.com/microsoft/TypeScript) — ⭐ 111,062 (2026-09-15)
* **Описание:** TypeScript is a superset of JavaScript that compiles to clean JavaScript output.
* **Архитектурный паттерн:** `Compiler Pipeline (Scanner -> Parser -> Binder -> Checker -> Emitter)`
* **Зачем тестировать в CodeExplorer:** Золотой стандарт для проверки AST: самохостящийся компилятор TS, колоссальные объединения типов (union/intersection), рекурсивный тайп-чекер.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/microsoft/TypeScript.git`

#### 📦 [supabase/supabase](https://github.com/supabase/supabase) — ⭐ 109,329 (2026-09-15)
* **Описание:** The Postgres development platform. Supabase gives you a dedicated Postgres database to build your web, mobile, and AI applications.
* **Архитектурный паттерн:** `Monorepo / Full-Stack / Studio + API`
* **Зачем тестировать в CodeExplorer:** Многопакетный монорепозиторий (Turborepo), генерация клиентских библиотек по схемам, React Studio фронтенд + серверные Edge функции.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/supabase/supabase.git`

#### 📦 [n8n-io/n8n](https://github.com/n8n-io/n8n) — ⭐ 204,438 (2026-09-15)
* **Описание:** Fair-code workflow automation platform with native AI capabilities. Combine visual building with custom code, self-host or cloud, 400+ integrations.
* **Архитектурный паттерн:** `Distributed Graph Execution Engine`
* **Зачем тестировать в CodeExplorer:** Оркестрация графов выполнения: сотни модульных узлов (node definitions), динамическое исполнение кода, асинхронные очереди (BullMQ).
* **Команда для клонирования:** `git clone --depth 1 https://github.com/n8n-io/n8n.git`

#### 📦 [vercel/next.js](https://github.com/vercel/next.js) — ⭐ 142,333 (2026-09-16)
* **Описание:** The React Framework
* **Архитектурный паттерн:** `Compiler + Server Runtime + Client Bundler`
* **Зачем тестировать в CodeExplorer:** Сложная гибридная архитектура: Server Components (RSC), App Router, интеграция со SWC/Turbopack, полифилы и оптимизация бандлов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/vercel/next.js.git`

#### 📦 [shadcn-ui/ui](https://github.com/shadcn-ui/ui) — ⭐ 123,889 (2026-09-12)
* **Описание:** Composable, accessible components with thoughtful defaults. Build your own component library with code you can customize, extend, and make your own.
* **Архитектурный паттерн:** `CLI AST Transformer / Headless UI`
* **Зачем тестировать в CodeExplorer:** Проверка парсинга CLI утилит, модификации AST кода пользователя при установке компонентов, Tailwind / Radix интеграция.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/shadcn-ui/ui.git`

#### 📦 [excalidraw/excalidraw](https://github.com/excalidraw/excalidraw) — ⭐ 132,049 (2026-09-15)
* **Описание:** Virtual whiteboard for sketching hand-drawn like diagrams
* **Архитектурный паттерн:** `State Machine / CRDT / Canvas 2D Engine`
* **Зачем тестировать в CodeExplorer:** Математическая геометрия, низкоуровневый рендеринг на Canvas, обработка жестов и сетевая синхронизация состояния.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/excalidraw/excalidraw.git`

#### 📦 [immich-app/immich](https://github.com/immich-app/immich) — ⭐ 114,312 (2026-09-15)
* **Описание:** High performance self-hosted photo and video management solution.
* **Архитектурный паттерн:** `NestJS Microservices / Clean Monorepo`
* **Зачем тестировать в CodeExplorer:** Отличный образец корпоративного NestJS: декораторы типов, TypeORM/Prisma, очереди задач, микросервисная архитектура машинного зрения.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/immich-app/immich.git`

#### 📦 [firecrawl/firecrawl](https://github.com/firecrawl/firecrawl) — ⭐ 180,867 (2026-09-15)
* **Описание:** The context API to search, scrape, and interact with the web at scale. 🔥
* **Архитектурный паттерн:** `Distributed Crawler / Playwright Orchestrator`
* **Зачем тестировать в CodeExplorer:** Масштабируемый распределенный краулинг, парсеры HTML в Markdown, пулы headless-браузеров, обработка очередей.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/firecrawl/firecrawl.git`

#### 📦 [anthropics/claude-code](https://github.com/anthropics/claude-code) — ⭐ 145,194 (2026-09-15)
* **Описание:** Claude Code is an agentic coding tool that lives in your terminal, understands your codebase, and helps you code faster by executing routine tasks, explaining complex code, and handling git workflows - all through natural language commands.
* **Архитектурный паттерн:** `CLI Agent Loop / Terminal UI / Tool Dispatcher`
* **Зачем тестировать в CodeExplorer:** Самый свежий образец агентной архитектуры: цикл рассуждений (agent loop), реактивный Ink TUI, диспатчинг локальных инструментов, bash execution.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/anthropics/claude-code.git`

## 🔹 Go (10 проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный домен | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[kubernetes/kubernetes](https://github.com/kubernetes/kubernetes)** | ⭐ 127,748 | 📅 2026-09-15 | Container Orchestration & Cloud Native | [GitHub](https://github.com/kubernetes/kubernetes.git) |
| **[golang/go](https://github.com/golang/go)** | ⭐ 138,837 | 📅 2026-09-15 | Compiler, Runtime & Standard Library | [GitHub](https://github.com/golang/go.git) |
| **[ollama/ollama](https://github.com/ollama/ollama)** | ⭐ 181,075 | 📅 2026-09-15 | Local LLM Inference Engine | [GitHub](https://github.com/ollama/ollama.git) |
| **[prometheus/prometheus](https://github.com/prometheus/prometheus)** | ⭐ 66,079 | 📅 2026-09-16 | Time Series Database & Monitoring | [GitHub](https://github.com/prometheus/prometheus.git) |
| **[caddyserver/caddy](https://github.com/caddyserver/caddy)** | ⭐ 75,766 | 📅 2026-09-14 | Extensible Web Server | [GitHub](https://github.com/caddyserver/caddy.git) |
| **[gohugoio/hugo](https://github.com/gohugoio/hugo)** | ⭐ 89,838 | 📅 2026-09-14 | Static Site Generator | [GitHub](https://github.com/gohugoio/hugo.git) |
| **[gin-gonic/gin](https://github.com/gin-gonic/gin)** | ⭐ 89,225 | 📅 2026-08-15 | Microservices & HTTP Router | [GitHub](https://github.com/gin-gonic/gin.git) |
| **[syncthing/syncthing](https://github.com/syncthing/syncthing)** | ⭐ 88,621 | 📅 2026-09-15 | P2P File Synchronization | [GitHub](https://github.com/syncthing/syncthing.git) |
| **[jesseduffield/lazygit](https://github.com/jesseduffield/lazygit)** | ⭐ 82,367 | 📅 2026-09-15 | Terminal UI Git Client | [GitHub](https://github.com/jesseduffield/lazygit.git) |
| **[traefik/traefik](https://github.com/traefik/traefik)** | ⭐ 64,851 | 📅 2026-09-15 | Cloud-Native Reverse Proxy | [GitHub](https://github.com/traefik/traefik.git) |

### Детальный разбор тест-кейсов для CodeExplorer:

#### 📦 [kubernetes/kubernetes](https://github.com/kubernetes/kubernetes) — ⭐ 127,748 (2026-09-15)
* **Описание:** Production-Grade Container Scheduling and Management
* **Архитектурный паттерн:** `Massive Distributed Monorepo / Declarative API`
* **Зачем тестировать в CodeExplorer:** Крупнейший в мире проект на Go: API machinery, контроллеры и реконсиляторы (informers/listers), etcd хранилище, сложные интерфейсы.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/kubernetes/kubernetes.git`

#### 📦 [golang/go](https://github.com/golang/go) — ⭐ 138,837 (2026-09-15)
* **Описание:** The Go programming language
* **Архитектурный паттерн:** `Compiler (cmd/compile) + Runtime + Stdlib`
* **Зачем тестировать в CodeExplorer:** Фундаментальный проект: SSA генерация кода, планировщик горутин, GC аллокатор, парсер синтаксиса Go (go/ast, go/types).
* **Команда для клонирования:** `git clone --depth 1 https://github.com/golang/go.git`

#### 📦 [ollama/ollama](https://github.com/ollama/ollama) — ⭐ 181,075 (2026-09-15)
* **Описание:** Get up and running with Kimi, GLM, MiniMax, DeepSeek, gpt-oss, Qwen, Gemma and other models.
* **Архитектурный паттерн:** `Go Orchestrator + CGO Bindings`
* **Зачем тестировать в CodeExplorer:** Проверка вызовов CGO (связка с llama.cpp), управление жизненным циклом GPU/VRAM процессов, высоконагруженный HTTP стриминг API.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/ollama/ollama.git`

#### 📦 [prometheus/prometheus](https://github.com/prometheus/prometheus) — ⭐ 66,079 (2026-09-16)
* **Описание:** The Prometheus monitoring system and time series database.
* **Архитектурный паттерн:** `Storage Engine (TSDB) + PromQL Engine`
* **Зачем тестировать в CodeExplorer:** Собственная встроенная БД: Write-Ahead Log (WAL), сжатие time-series данных, парсер языка запросов PromQL, скрапинг метрик.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/prometheus/prometheus.git`

#### 📦 [caddyserver/caddy](https://github.com/caddyserver/caddy) — ⭐ 75,766 (2026-09-14)
* **Описание:** Fast and extensible multi-platform HTTP/1-2-3 web server with automatic HTTPS
* **Архитектурный паттерн:** `Modular Plugin Architecture / Dynamic TLS`
* **Зачем тестировать в CodeExplorer:** Образцовая модульная экосистема Go: конфигурация через JSON, runtime загрузка плагинов, HTTP/3 QUIC стек, автоматические сертификаты.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/caddyserver/caddy.git`

#### 📦 [gohugoio/hugo](https://github.com/gohugoio/hugo) — ⭐ 89,838 (2026-09-14)
* **Описание:** The world’s fastest framework for building websites.
* **Архитектурный паттерн:** `Ultra-Fast Pipeline / Concurrency Engine`
* **Зачем тестировать в CodeExplorer:** Бенчмарк параллелизма: глубокое использование горутин и каналов, кастомный движок шаблонов, пайплайны процессинга медиа.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/gohugoio/hugo.git`

#### 📦 [gin-gonic/gin](https://github.com/gin-gonic/gin) — ⭐ 89,225 (2026-08-15)
* **Описание:** Gin is a high-performance HTTP web framework written in Go. It provides a Martini-like API but with significantly better performance—up to 40 times faster—thanks to httprouter. Gin is designed for building REST APIs, web applications, and microservices.
* **Архитектурный паттерн:** `Radix Tree Router / Zero-Allocation Middleware`
* **Зачем тестировать в CodeExplorer:** Проверка алгоритмов префиксного дерева (Radix tree), цепочек middleware, минимизации аллокаций памяти и валидации структур.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/gin-gonic/gin.git`

#### 📦 [syncthing/syncthing](https://github.com/syncthing/syncthing) — ⭐ 88,621 (2026-09-15)
* **Описание:** Open Source Continuous File Synchronization
* **Архитектурный паттерн:** `Distributed P2P / Cryptographic Protocols`
* **Зачем тестировать в CodeExplorer:** Сетевые протоколы (BEP), TLS аутентификация нод, блокировка файлов, обнаружение пиров в локальной сети и через релеи.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/syncthing/syncthing.git`

#### 📦 [jesseduffield/lazygit](https://github.com/jesseduffield/lazygit) — ⭐ 82,367 (2026-09-15)
* **Описание:** simple terminal UI for git commands
* **Архитектурный паттерн:** `Event-Driven TUI / Git Subprocess Engine`
* **Зачем тестировать в CodeExplorer:** Событийный цикл консоли (gocui), асинхронный парсинг вывода git команд, многопоточный менеджмент фоновых задач.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/jesseduffield/lazygit.git`

#### 📦 [traefik/traefik](https://github.com/traefik/traefik) — ⭐ 64,851 (2026-09-15)
* **Описание:** The Cloud Native Application Proxy
* **Архитектурный паттерн:** `Dynamic Configuration Provider / Ingress Controller`
* **Зачем тестировать в CodeExplorer:** Провайдеры динамической конфигурации (Docker, K8s, Consul), трансляция сетевых маршрутов, балансировка нагрузки.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/traefik/traefik.git`

## 🔹 Java (10 проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный домен | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[elastic/elasticsearch](https://github.com/elastic/elasticsearch)** | ⭐ 77,919 | 📅 2026-09-15 | Distributed Search Engine | [GitHub](https://github.com/elastic/elasticsearch.git) |
| **[spring-projects/spring-boot](https://github.com/spring-projects/spring-boot)** | ⭐ 81,438 | 📅 2026-09-15 | Enterprise Application Framework | [GitHub](https://github.com/spring-projects/spring-boot.git) |
| **[NationalSecurityAgency/ghidra](https://github.com/NationalSecurityAgency/ghidra)** | ⭐ 76,681 | 📅 2026-09-15 | Software Reverse Engineering (SRE) | [GitHub](https://github.com/NationalSecurityAgency/ghidra.git) |
| **[dbeaver/dbeaver](https://github.com/dbeaver/dbeaver)** | ⭐ 51,769 | 📅 2026-09-15 | Universal Database Client | [GitHub](https://github.com/dbeaver/dbeaver.git) |
| **[apache/dubbo](https://github.com/apache/dubbo)** | ⭐ 41,568 | 📅 2026-09-15 | High-Performance RPC & Microservices | [GitHub](https://github.com/apache/dubbo.git) |
| **[ReactiveX/RxJava](https://github.com/ReactiveX/RxJava)** | ⭐ 48,202 | 📅 2026-09-14 | Reactive Programming Library | [GitHub](https://github.com/ReactiveX/RxJava.git) |
| **[skylot/jadx](https://github.com/skylot/jadx)** | ⭐ 50,474 | 📅 2026-09-12 | Dex to Java Decompiler | [GitHub](https://github.com/skylot/jadx.git) |
| **[google/guava](https://github.com/google/guava)** | ⭐ 51,904 | 📅 2026-09-15 | Core Data Structures & Utilities | [GitHub](https://github.com/google/guava.git) |
| **[termux/termux-app](https://github.com/termux/termux-app)** | ⭐ 60,868 | 📅 2026-09-15 | Android Terminal & Linux Emulation | [GitHub](https://github.com/termux/termux-app.git) |
| **[halo-dev/halo](https://github.com/halo-dev/halo)** | ⭐ 39,747 | 📅 2026-09-15 | Modern Extensible CMS | [GitHub](https://github.com/halo-dev/halo.git) |

### Детальный разбор тест-кейсов для CodeExplorer:

#### 📦 [elastic/elasticsearch](https://github.com/elastic/elasticsearch) — ⭐ 77,919 (2026-09-15)
* **Описание:** Free and Open Source, Distributed, RESTful Search Engine
* **Архитектурный паттерн:** `Distributed Clustering / Lucene Engine`
* **Зачем тестировать в CodeExplorer:** Масштабная распределенная система: шардирование, консенсус кластера, управление памятью за пределами кучи (off-heap), Lucene интеграция.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/elastic/elasticsearch.git`

#### 📦 [spring-projects/spring-boot](https://github.com/spring-projects/spring-boot) — ⭐ 81,438 (2026-09-15)
* **Описание:** Spring Boot helps you to create Spring-powered, production-grade applications and services with absolute minimum fuss.
* **Архитектурный паттерн:** `IoC Container / Auto-Configuration Framework`
* **Зачем тестировать в CodeExplorer:** Главный бенчмарк для Java: инверсия управления (IoC/DI), conditional аннотации (@ConditionalOnClass), загрузчики классов, рефлексия.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/spring-projects/spring-boot.git`

#### 📦 [NationalSecurityAgency/ghidra](https://github.com/NationalSecurityAgency/ghidra) — ⭐ 76,681 (2026-09-15)
* **Описание:** Ghidra is a software reverse engineering (SRE) framework
* **Архитектурный паттерн:** `Decompiler / Architecture Emulator / Swing GUI`
* **Зачем тестировать в CodeExplorer:** Гигантский сложнейший проект: трансляция языков ассемблера (Sleigh), графы декомпиляции кода, плагины расширения, Swing GUI.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/NationalSecurityAgency/ghidra.git`

#### 📦 [dbeaver/dbeaver](https://github.com/dbeaver/dbeaver) — ⭐ 51,769 (2026-09-15)
* **Описание:** Free universal database tool and SQL client
* **Архитектурный паттерн:** `Eclipse RCP / OSGi Plugin Architecture`
* **Зачем тестировать в CodeExplorer:** Модульная архитектура OSGi: динамические бандлы плагинов, поддержка сотен драйверов JDBC, управление подключениями к БД.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/dbeaver/dbeaver.git`

#### 📦 [apache/dubbo](https://github.com/apache/dubbo) — ⭐ 41,568 (2026-09-15)
* **Описание:** The java implementation of Apache Dubbo. An RPC and microservice framework.
* **Архитектурный паттерн:** `Service Mesh / Dynamic Proxy / RPC Stack`
* **Зачем тестировать в CodeExplorer:** Сетевой стек Netty, динамическая генерация прокси (Javassist/ByteBuddy), балансировка нагрузки, сериализация протоколов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/apache/dubbo.git`

#### 📦 [ReactiveX/RxJava](https://github.com/ReactiveX/RxJava) — ⭐ 48,202 (2026-09-14)
* **Описание:** RxJava – Reactive Extensions for the JVM – a library for composing asynchronous and event-based programs using observable sequences for the Java VM.
* **Архитектурный паттерн:** `Observable Pattern / Concurrent Scheduler`
* **Зачем тестировать в CodeExplorer:** Экстремальные generics: сложные цепочки типов в сигнатурах, операторы преобразования потоков, планировщики потоков исполнения.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/ReactiveX/RxJava.git`

#### 📦 [skylot/jadx](https://github.com/skylot/jadx) — ⭐ 50,474 (2026-09-12)
* **Описание:** Dex to Java decompiler
* **Архитектурный паттерн:** `Bytecode Parser / Control Flow Graph / SSA`
* **Зачем тестировать в CodeExplorer:** Парсинг байткода Dalvik, построение графов потока управления (CFG), восстановление синтаксических конструкций Java из байткода.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/skylot/jadx.git`

#### 📦 [google/guava](https://github.com/google/guava) — ⭐ 51,904 (2026-09-15)
* **Описание:** Google core libraries for Java
* **Архитектурный паттерн:** `Immutable Collections / Graph Framework`
* **Зачем тестировать в CodeExplorer:** Академический образец чистого кода: immutable структуры, реализация графов (Graph, ValueGraph, Network), кэширование, функциональные интерфейсы.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/google/guava.git`

#### 📦 [termux/termux-app](https://github.com/termux/termux-app) — ⭐ 60,868 (2026-09-15)
* **Описание:** Termux - a terminal emulator application for Android OS extendible by variety of packages.
* **Архитектурный паттерн:** `Android Native Integration / JNI Bridge`
* **Зачем тестировать в CodeExplorer:** Взаимодействие Android UI с Linux подсистемой через JNI / C, эмуляция VT терминала, управление фоновыми сервисами Android.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/termux/termux-app.git`

#### 📦 [halo-dev/halo](https://github.com/halo-dev/halo) — ⭐ 39,747 (2026-09-15)
* **Описание:** Halo 是一款强大易用的开源建站工具，从个人博客、知识库，到企业官网、在线商城，Halo 都能助您轻松实现，一站式满足您的多样化建站需求。
* **Архитектурный паттерн:** `Spring Boot 3 / Plugin Runtime System`
* **Зачем тестировать в CodeExplorer:** Современный Spring Boot 3 на Java 17/21: виртуальные потоки (Project Loom), динамическая загрузка плагинов, реактивный стек WebFlux.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/halo-dev/halo.git`

## 🔹 Python (10 проектов)

| Репозиторий | Звёзды | Последний коммит | Архитектурный домен | Ссылка |
| :--- | :---: | :---: | :--- | :--- |
| **[tiangolo/fastapi](https://github.com/tiangolo/fastapi)** | ⭐ 102,353 | 📅 2026-09-14 | Modern Async Web Framework | [GitHub](https://github.com/tiangolo/fastapi.git) |
| **[django/django](https://github.com/django/django)** | ⭐ 91,065 | 📅 2026-09-15 | Classic Monolithic Web Framework | [GitHub](https://github.com/django/django.git) |
| **[huggingface/transformers](https://github.com/huggingface/transformers)** | ⭐ 166,197 | 📅 2026-09-15 | State-of-the-Art ML / AI Framework | [GitHub](https://github.com/huggingface/transformers.git) |
| **[home-assistant/core](https://github.com/home-assistant/core)** | ⭐ 90,526 | 📅 2026-09-15 | IoT & Automation Operating System | [GitHub](https://github.com/home-assistant/core.git) |
| **[yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp)** | ⭐ 191,434 | 📅 2026-08-30 | Media Downloader & Extractor | [GitHub](https://github.com/yt-dlp/yt-dlp.git) |
| **[langchain-ai/langchain](https://github.com/langchain-ai/langchain)** | ⭐ 146,397 | 📅 2026-09-15 | AI Agent & Pipeline Orchestration | [GitHub](https://github.com/langchain-ai/langchain.git) |
| **[Significant-Gravitas/AutoGPT](https://github.com/Significant-Gravitas/AutoGPT)** | ⭐ 187,369 | 📅 2026-09-16 | Autonomous AI Agent Architecture | [GitHub](https://github.com/Significant-Gravitas/AutoGPT.git) |
| **[pytest-dev/pytest](https://github.com/pytest-dev/pytest)** | ⭐ 14,506 | 📅 2026-09-15 | Testing Framework & AST Rewriter | [GitHub](https://github.com/pytest-dev/pytest.git) |
| **[streamlit/streamlit](https://github.com/streamlit/streamlit)** | ⭐ 45,763 | 📅 2026-09-15 | Interactive Data Web App Framework | [GitHub](https://github.com/streamlit/streamlit.git) |
| **[mitmproxy/mitmproxy](https://github.com/mitmproxy/mitmproxy)** | ⭐ 45,065 | 📅 2026-09-10 | Interactive Network Interception Proxy | [GitHub](https://github.com/mitmproxy/mitmproxy.git) |

### Детальный разбор тест-кейсов для CodeExplorer:

#### 📦 [tiangolo/fastapi](https://github.com/tiangolo/fastapi) — ⭐ 102,353 (2026-09-14)
* **Описание:** FastAPI framework, high performance, easy to learn, fast to code, ready for production
* **Архитектурный паттерн:** `Type-Driven API / Asyncio / Pydantic Engine`
* **Зачем тестировать в CodeExplorer:** Парсинг современных аннотаций типов Python 3.10+, валидация Pydantic, внедрение зависимостей через Depends, асинхронные обработчики.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/tiangolo/fastapi.git`

#### 📦 [django/django](https://github.com/django/django) — ⭐ 91,065 (2026-09-15)
* **Описание:** The Web framework for perfectionists with deadlines.
* **Архитектурный паттерн:** `MVC / Metaclass ORM / Signal Architecture`
* **Зачем тестировать в CodeExplorer:** Глубокая магия Python: метаклассы в моделях ORM, динамическое создание полей, система сигналов, кастомный шаблонизатор.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/django/django.git`

#### 📦 [huggingface/transformers](https://github.com/huggingface/transformers) — ⭐ 166,197 (2026-09-15)
* **Описание:** 🤗 Transformers: the model-definition framework for state-of-the-art machine learning models in text, vision, audio, and multimodal models, for both inference and training. 
* **Архитектурный паттерн:** `Unified Model Abstraction / PyTorch Bindings`
* **Зачем тестировать в CodeExplorer:** Колоссальное число классов моделей: полиморфизм конфигураций, динамическая подгрузка весов, токенизаторы, генерация тензорных вычислений.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/huggingface/transformers.git`

#### 📦 [home-assistant/core](https://github.com/home-assistant/core) — ⭐ 90,526 (2026-09-15)
* **Описание:** :house_with_garden: Open source home automation that puts local control and privacy first.
* **Архитектурный паттерн:** `Event-Driven Async Monolith / 1000+ Integrations`
* **Зачем тестировать в CodeExplorer:** Крупнейший чистый Python-проект: центральная шина событий, конечный автомат состояний, динамическая загрузка сотен сторонних библиотек.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/home-assistant/core.git`

#### 📦 [yt-dlp/yt-dlp](https://github.com/yt-dlp/yt-dlp) — ⭐ 191,434 (2026-08-30)
* **Описание:** A feature-rich command-line audio/video downloader
* **Архитектурный паттерн:** `Plugin-Based Extractor Architecture`
* **Зачем тестировать в CodeExplorer:** Сотни модульных экстракторов (InfoExtractor), сложнейшие регулярные выражения, динамический обход веб-защит, пост-процессинг.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/yt-dlp/yt-dlp.git`

#### 📦 [langchain-ai/langchain](https://github.com/langchain-ai/langchain) — ⭐ 146,397 (2026-09-15)
* **Описание:** The agent engineering platform.
* **Архитектурный паттерн:** `Abstraction Layer / Chain & Agent Framework`
* **Зачем тестировать в CodeExplorer:** Глубокие иерархии абстракций (BaseLanguageModel, Runnable, Tool), Pydantic v2 интеграции, графы выполнения LangGraph.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/langchain-ai/langchain.git`

#### 📦 [Significant-Gravitas/AutoGPT](https://github.com/Significant-Gravitas/AutoGPT) — ⭐ 187,369 (2026-09-16)
* **Описание:** AutoGPT is the vision of accessible AI for everyone, to use and to build on. Our mission is to provide the tools, so that you can focus on what matters.
* **Архитектурный паттерн:** `Autonomous Agent Loop / Execution Sandbox`
* **Зачем тестировать в CodeExplorer:** Агентный цикл выполнения команд, векторная память, планирование задач, безопасное исполнение внешних скриптов.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/Significant-Gravitas/AutoGPT.git`

#### 📦 [pytest-dev/pytest](https://github.com/pytest-dev/pytest) — ⭐ 14,506 (2026-09-15)
* **Описание:** The pytest framework makes it easy to write small tests, yet scales to support complex functional testing
* **Архитектурный паттерн:** `Pluggy Hook Architecture / AST Rewriting Engine`
* **Зачем тестировать в CodeExplorer:** Переписывание синтаксического дерева (AST rewrite) для операторов assert, система фикстур с инъекцией аргументов, плагин-архитектура.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/pytest-dev/pytest.git`

#### 📦 [streamlit/streamlit](https://github.com/streamlit/streamlit) — ⭐ 45,763 (2026-09-15)
* **Описание:** Streamlit — A faster way to build and share data apps.
* **Архитектурный паттерн:** `Script Re-Execution Model / Protobuf / WebSockets`
* **Зачем тестировать в CodeExplorer:** Уникальная модель выполнения: перезапуск скрипта при каждом событии ввода, кэширование состояния (@st.cache_data), Protobuf сериализация.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/streamlit/streamlit.git`

#### 📦 [mitmproxy/mitmproxy](https://github.com/mitmproxy/mitmproxy) — ⭐ 45,065 (2026-09-10)
* **Описание:** An interactive TLS-capable intercepting HTTP proxy for penetration testers and software developers.
* **Архитектурный паттерн:** `Network Event Loop / Flow Addon Architecture`
* **Зачем тестировать в CodeExplorer:** Сетевые протоколы HTTP/1, HTTP/2, HTTP/3 QUIC, WebSockets, плагинная система перехвата трафика (Addons), асинхронные потоки.
* **Команда для клонирования:** `git clone --depth 1 https://github.com/mitmproxy/mitmproxy.git`

---

## 🤖 Методология автоматического прогона (CodeExplorer Benchmark Workflow)

1. **Тест индексации (Ingestion & AST Construction):**
   * Клонирование репозитория с `--depth 1`.
   * Запуск парсера CodeExplorer (`index`): замер скорости разбора AST, пикового потребления RAM и отсутствия сбоев на экзотическом синтаксисе.

2. **Тест разрешения символов (Cross-File Symbol Resolution):**
   * Проверка точности связывания типов, интерфейсов, базовых классов и реализаций.
   * Проверка устойчивости к циклическим зависимостям и сложным generics/метатипам.

3. **Тест архитектурного анализатора (Architecture Queries):**
   * Выявление циклических зависимостей пакетов/модулей.
   * Расчет метрик зацепления (Coupling / Cohesion) и глубины дерева наследования.

4. **Автоматическое пополнение каталога библиотек:**
   * Модель сканирует файлы сборки (`*.csproj`, `package.json`, `go.mod`, `pom.xml`/`build.gradle`, `pyproject.toml`/`requirements.txt`).
   * Новые популярные сторонние зависимости автоматически классифицируются и добавляются во внутреннюю базу знаний CodeExplorer.