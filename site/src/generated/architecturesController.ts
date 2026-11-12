// Auto-generated Architectures Page interactive controller for React SPA
// @ts-nocheck
export function initArchitecturesController(): () => void {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return () => {};
  }

      var STACK_PATHS = {
        agnostic: {
          domain: "/Domain/",
          app: "/Application/",
          infra: "/Infrastructure/",
          ui: "/Api/",
          modPrefix: "Modules/",
          slicePrefix: "Features/"
        },
        dotnet: {
          domain: ".Domain/",
          app: ".Application/",
          infra: ".Infrastructure/",
          ui: ".Api/",
          modPrefix: "src/Modules/",
          slicePrefix: "Features/"
        },
        typescript: {
          domain: "src/domain/",
          app: "src/application/",
          infra: "src/infrastructure/",
          ui: "src/controllers/",
          modPrefix: "packages/",
          slicePrefix: "src/features/"
        },
        go: {
          domain: "internal/domain/",
          app: "internal/usecase/",
          infra: "internal/adapter/repository/",
          ui: "internal/adapter/http/",
          modPrefix: "internal/modules/",
          slicePrefix: "internal/features/"
        },
        python: {
          domain: "/domain/",
          app: "/use_cases/",
          infra: "/adapters/orm/",
          ui: "/routers/",
          modPrefix: "apps/",
          slicePrefix: "features/"
        },
        java: {
          domain: "/domain/",
          app: "/application/",
          infra: "/infrastructure/",
          ui: "/web/",
          modPrefix: "modules/",
          slicePrefix: "features/"
        }
      };

      var PACKS = {
        "clean-architecture": {
          title: "Clean Architecture (Onion Dependency Rule)",
          subtitle: "All source code dependencies point strictly inward toward Domain entities.",
          note: "Why AST + SQL graph beats import linters: import linters only check file imports. CodeExplorer catches transitive call chains, DI interface resolutions, and raw SQL or ORM table accesses crossing layer boundaries.",
          kv: [
            ["Best fit", "Complex domain rules, long-lived backends"],
            ["Avoid when", "Thin CRUD microservices or prototypes"],
            ["Graph layers used", "L2 Project + L3 Call/Type + L4 SQL/ORM"],
            ["Legacy repo onboarding", "SHA-256 ratchet (0 Day-1 rewrites)"]
          ],
          layers: function (s) {
            return [
              { name: "Presentation & Delivery (API / UI / CLI)", paths: s.ui + "**", core: false },
              { name: "Infrastructure & Persistence (ORM / HTTP / Queue)", paths: s.infra + "**", core: false },
              { name: "Application Use Cases (Commands / Queries / Ports)", paths: s.app + "**", core: false },
              { name: "Domain Core (Entities / Value Objects / Policies)", paths: s.domain + "** (0 outward deps)", core: true }
            ];
          },
          edges: [
            { label: "Presentation to Application", allow: true, code: "ALLOW" },
            { label: "Infrastructure to Application & Domain", allow: true, code: "ALLOW" },
            { label: "Domain to Infrastructure or Web", allow: false, code: "SIG-CA-B-001", drawer: "drawer-0" },
            { label: "Controller direct to DbContext / SQL", allow: false, code: "SIG-CA-B-003", drawer: "drawer-2" }
          ],
          gates: function (s, stack) {
            return [
              {
                id: "SIG-CA-B-001",
                name: "Domain Core Purity",
                coords: "Module x Boundaries",
                pattern: "(Domain)-[:DEPENDS_ON|CALLS]->(Application|Infrastructure|Presentation)",
                severity: "Error",
                description: "Domain types and functions must never call or depend on Application, Infrastructure, or Presentation layers.",
                query: "MATCH (src:Function)-[:CALLS]->(dst:Function)\n  WHERE src.file_path CONTAINS '" + s.domain + "'\n    AND (dst.file_path CONTAINS '" + s.infra + "' OR dst.file_path CONTAINS '" + s.app + "' OR dst.file_path CONTAINS '" + s.ui + "')\n  RETURN src.file_path AS file, src.start_line AS line, src.name AS symbol, dst.name AS target,\n         'Domain symbol ' + src.name + ' calls outer-layer symbol ' + dst.name AS message",
                platform: stack
              },
              {
                id: "SIG-CA-B-002",
                name: "Application Layer Isolation",
                coords: "Module x Boundaries",
                pattern: "(Application)-[:DEPENDS_ON|CALLS]->(Infrastructure|Presentation)",
                severity: "Error",
                description: "Application use cases orchestrate Domain logic without importing concrete Infrastructure or Delivery adapters.",
                query: "MATCH (src:Function)-[:CALLS]->(dst:Function)\n  WHERE src.file_path CONTAINS '" + s.app + "'\n    AND (dst.file_path CONTAINS '" + s.infra + "' OR dst.file_path CONTAINS '" + s.ui + "')\n  RETURN src.file_path AS file, src.start_line AS line, src.name AS symbol, dst.name AS target,\n         'Application use case ' + src.name + ' depends directly on adapter ' + dst.name AS message",
                platform: stack
              },
              {
                id: "SIG-CA-B-003",
                name: "No Delivery-to-Database Bypass",
                coords: "Method x Boundaries",
                pattern: "(Endpoint|Controller)-[:CALLS|READS_FROM|WRITES_TO]->(Query|Table)",
                severity: "Error",
                description: "Delivery handlers must dispatch through Application use cases rather than issuing SQL or ORM queries directly.",
                query: "MATCH (q:Query)-[:ACCESSED_BY]->(fn:Function)\n  WHERE fn.file_path CONTAINS '" + s.ui + "'\n  RETURN fn.file_path AS file, fn.start_line AS line, fn.name AS symbol, q.name AS target,\n         'Delivery handler ' + fn.name + ' executes database query directly, bypassing Application layer' AS message",
                platform: stack
              },
              {
                id: "SIG-CA-C-004",
                name: "Persistence-Ignorant Domain Entities",
                coords: "Class x Contract",
                pattern: "(Type {layer:'Domain'}) with ORM or HTTP transport annotations",
                severity: "Warning",
                description: "Domain entities should stay free of database table or HTTP serialization decorators.",
                query: "MATCH (t:Type)\n  WHERE t.file_path CONTAINS '" + s.domain + "'\n    AND (coalesce(t.attributes, '') CONTAINS 'Table' OR coalesce(t.attributes, '') CONTAINS 'Entity' OR coalesce(t.attributes, '') CONTAINS 'JsonProperty')\n  RETURN t.file_path AS file, t.start_line AS line, t.name AS symbol, t.attributes AS target,\n         'Domain type ' + t.name + ' carries outer framework annotation ' + t.attributes AS message",
                platform: stack
              },
              {
                id: "SIG-CA-B-005",
                name: "Acyclic Project Dependency Graph",
                coords: "Module x Boundaries",
                pattern: "(p1:Project)-[:DEPENDS_ON]->(p2:Project)-[:DEPENDS_ON]->(p1)",
                severity: "Error",
                description: "Projects must form a strict directed acyclic graph without mutual dependencies.",
                query: "MATCH (p1:Project)-[:DEPENDS_ON]->(p2:Project)-[:DEPENDS_ON]->(p1)\n  WHERE p1.id < p2.id\n  RETURN p1.path AS file, 1 AS line, p1.name AS symbol, p2.name AS target,\n         'Circular project dependency between ' + p1.name + ' and ' + p2.name AS message",
                platform: stack
              }
            ];
          }
        },
        "hexagonal": {
          title: "Hexagonal Architecture (Ports & Adapters)",
          subtitle: "Inbound driving adapters and outbound driven adapters meet exclusively through Core Port interfaces.",
          note: "Prevents HTTP controllers or CLI commands from short-circuiting directly into SQL repositories, Redis clients, or external SDKs.",
          kv: [
            ["Best fit", "Services with multiple protocols (HTTP, gRPC, CLI, Queue) and swappable infra"],
            ["Avoid when", "Single-adapter scripts with zero external integrations"],
            ["Graph layers used", "L3 Interface IMPLEMENTS + L4 Endpoint / ExternalService / DB"],
            ["Legacy repo onboarding", "SHA-256 ratchet (0 Day-1 rewrites)"]
          ],
          layers: function (s) {
            return [
              { name: "Driving Inbound Adapters (HTTP / gRPC / Queue Consumers)", paths: s.ui + "**", core: false },
              { name: "Application Ports (Inbound & Outbound Interfaces)", paths: s.app + "ports/**", core: true },
              { name: "Domain & Application Core", paths: s.domain + "**", core: true },
              { name: "Driven Outbound Adapters (Postgres / Kafka / HTTP SDKs)", paths: s.infra + "**", core: false }
            ];
          },
          edges: [
            { label: "Driving Adapter to Inbound Port", allow: true, code: "ALLOW" },
            { label: "Driven Adapter implements Outbound Port", allow: true, code: "ALLOW" },
            { label: "Driving Adapter direct to Driven Adapter", allow: false, code: "SIG-HEX-B-001", drawer: "drawer-0" },
            { label: "Core importing SDK or DB driver", allow: false, code: "SIG-HEX-B-002", drawer: "drawer-1" }
          ],
          gates: function (s, stack) {
            return [
              {
                id: "SIG-HEX-B-001",
                name: "No Adapter-to-Adapter Short-Circuit",
                coords: "Method x Boundaries",
                pattern: "(DrivingAdapter)-[:CALLS]->(DrivenAdapter) bypassing Core Port",
                severity: "Error",
                description: "Inbound HTTP/gRPC/CLI adapters must never invoke outbound persistence or external SDK adapters directly.",
                query: "MATCH (inbound:Function)-[:CALLS]->(outbound:Function)\n  WHERE inbound.file_path CONTAINS '" + s.ui + "' AND outbound.file_path CONTAINS '" + s.infra + "'\n  RETURN inbound.file_path AS file, inbound.start_line AS line, inbound.name AS symbol, outbound.name AS target,\n         'Inbound adapter ' + inbound.name + ' bypasses Core Port and calls outbound adapter ' + outbound.name AS message",
                platform: stack
              },
              {
                id: "SIG-HEX-B-002",
                name: "Hexagon Core Infrastructure Isolation",
                coords: "Module x Boundaries",
                pattern: "(Core)-[:CALLS]->(DrivenAdapter|ExternalService)",
                severity: "Error",
                description: "Domain and Port definitions inside the hexagon must never call concrete adapter implementations.",
                query: "MATCH (core:Function)-[:CALLS]->(adapter:Function)\n  WHERE (core.file_path CONTAINS '" + s.domain + "' OR core.file_path CONTAINS '" + s.app + "')\n    AND adapter.file_path CONTAINS '" + s.infra + "'\n  RETURN core.file_path AS file, core.start_line AS line, core.name AS symbol, adapter.name AS target,\n         'Hexagon core function ' + core.name + ' calls concrete driven adapter ' + adapter.name AS message",
                platform: stack
              },
              {
                id: "SIG-HEX-C-003",
                name: "Driven Repository Implements Port Interface",
                coords: "Class x Contract",
                pattern: "Repository or Adapter class without IMPLEMENTS -> Port interface",
                severity: "Warning",
                description: "Every concrete outbound repository or client adapter must implement an explicit Port interface.",
                query: "MATCH (repo:Type)\n  WHERE repo.file_path CONTAINS '" + s.infra + "' AND repo.name ENDS WITH 'Repository'\n    AND coalesce(repo.implements, '') = ''\n  RETURN repo.file_path AS file, repo.start_line AS line, repo.name AS symbol, 'PortInterface' AS target,\n         'Driven adapter ' + repo.name + ' does not implement an outbound Port interface' AS message",
                platform: stack
              },
              {
                id: "SIG-HEX-B-004",
                name: "External HTTP Calls Confined to Driven Adapters",
                coords: "Service x Boundaries",
                pattern: "(ExternalService)-[:DECLARED_IN]->(File outside Driven Adapters)",
                severity: "Error",
                description: "Outbound HTTP/RPC calls must originate strictly from driven adapter modules.",
                query: "MATCH (es:ExternalService)-[:DECLARED_IN]->(f:File)\n  WHERE NOT f.path CONTAINS '" + s.infra + "'\n  RETURN f.path AS file, coalesce(es.start_line, 1) AS line, es.name AS symbol, es.url AS target,\n         'External service call ' + es.name + ' is declared outside driven adapters' AS message",
                platform: stack
              }
            ];
          }
        },
        "modular-monolith": {
          title: "Modular Monolith (Strict Module & Table Encapsulation)",
          subtitle: "Single deployable binary split into isolated business modules that share neither internal types nor SQL tables.",
          note: "The #1 killer of Modular Monoliths is cross-module SQL table joins and writes. CodeExplorer's L4 SQL lineage blocks cross-module table access automatically.",
          kv: [
            ["Best fit", "Growing product teams wanting microservice boundaries without distributed ops"],
            ["Avoid when", "Modules require independent hardware scaling or polyglot runtimes"],
            ["Graph layers used", "L2 Module + L3 Public Facade + L4 SQL READS_FROM / WRITES_TO"],
            ["Legacy repo onboarding", "Freeze cross-module SQL debt via ratchet, block all new leaks"]
          ],
          layers: function (s) {
            return [
              { name: "Module Public Contracts (Contracts / Events / Facades)", paths: s.modPrefix + "*/Contracts/**", core: true },
              { name: "Module A Internals (Orders: Handlers, Domain, DB)", paths: s.modPrefix + "Orders/Internal/**", core: false },
              { name: "Module B Internals (Billing: Handlers, Domain, DB)", paths: s.modPrefix + "Billing/Internal/**", core: false },
              { name: "Module-Owned SQL Tables (orders_*, billing_*)", paths: "L4 Table ownership boundary", core: false }
            ];
          },
          edges: [
            { label: "Module A to Module B Public Contract", allow: true, code: "ALLOW" },
            { label: "Module A writing its own SQL tables", allow: true, code: "ALLOW" },
            { label: "Module A calling Module B Internal types", allow: false, code: "SIG-MM-B-001", drawer: "drawer-0" },
            { label: "Module A writing Module B SQL tables", allow: false, code: "SIG-MM-B-002", drawer: "drawer-1" }
          ],
          gates: function (s, stack) {
            return [
              {
                id: "SIG-MM-B-001",
                name: "No Cross-Module Internal Symbol Calls",
                coords: "Module x Boundaries",
                pattern: "(ModuleA)-[:CALLS]->(ModuleB/Internal) bypassing Contracts",
                severity: "Error",
                description: "Modules may only invoke another module through its Contracts or Public API namespace.",
                query: "MATCH (caller:Function)-[:CALLS]->(callee:Function)\n  WHERE caller.project_name <> callee.project_name\n    AND callee.file_path CONTAINS '" + s.modPrefix + "'\n    AND (callee.file_path CONTAINS '/Internal/' OR callee.file_path CONTAINS '/Domain/' OR callee.file_path CONTAINS '/Persistence/')\n  RETURN caller.file_path AS file, caller.start_line AS line, caller.name AS symbol, callee.name AS target,\n         'Cross-module encapsulation breach: ' + caller.project_name + ' calls internal symbol ' + callee.name AS message",
                platform: stack
              },
              {
                id: "SIG-MM-B-002",
                name: "Cross-Module Database Table Write Shield",
                coords: "Service x Boundaries",
                pattern: "(Two distinct Modules)<-[:DECLARED_IN]-(Query)-[:WRITES_TO]->(Shared Table)",
                severity: "Error",
                description: "Each database table must have a single owning module responsible for mutations.",
                query: "MATCH (q1:Query)-[:WRITES_TO]->(t:Table)<-[:WRITES_TO]-(q2:Query)\n  MATCH (q1)-[:ACCESSED_BY]->(f1:Function), (q2)-[:ACCESSED_BY]->(f2:Function)\n  WHERE f1.project_name < f2.project_name\n  RETURN f1.file_path AS file, f1.start_line AS line, f1.project_name AS symbol, t.name AS target,\n         'Table ' + t.name + ' is mutated by multiple modules: ' + f1.project_name + ' and ' + f2.project_name AS message",
                platform: stack
              },
              {
                id: "SIG-MM-B-003",
                name: "No Cross-Module Direct SQL Table Reads",
                coords: "Service x Boundaries",
                pattern: "(ModuleA Query)-[:READS_FROM]->(Table owned/written by ModuleB)",
                severity: "Warning",
                description: "Modules should read foreign module data via query facades or integration events instead of SQL joins.",
                query: "MATCH (qw:Query)-[:WRITES_TO]->(t:Table)<-[:READS_FROM]-(qr:Query)\n  MATCH (qw)-[:ACCESSED_BY]->(fw:Function), (qr)-[:ACCESSED_BY]->(fr:Function)\n  WHERE fw.project_name <> fr.project_name\n  RETURN fr.file_path AS file, fr.start_line AS line, fr.name AS symbol, t.name AS target,\n         'Module ' + fr.project_name + ' reads table ' + t.name + ' owned by module ' + fw.project_name AS message",
                platform: stack
              },
              {
                id: "SIG-MM-B-004",
                name: "Shared Kernel Purity",
                coords: "Module x Boundaries",
                pattern: "(SharedKernel)-[:DEPENDS_ON|CALLS]->(BusinessModule)",
                severity: "Error",
                description: "Shared building blocks must never depend back on any concrete business module.",
                query: "MATCH (pShared:Project)-[:DEPENDS_ON]->(pMod:Project)\n  WHERE (pShared.name CONTAINS 'Shared' OR pShared.name CONTAINS 'Common' OR pShared.name CONTAINS 'BuildingBlocks')\n    AND pMod.path CONTAINS '" + s.modPrefix + "'\n  RETURN pShared.path AS file, 1 AS line, pShared.name AS symbol, pMod.name AS target,\n         'Shared kernel ' + pShared.name + ' depends on business module ' + pMod.name AS message",
                platform: stack
              },
              {
                id: "SIG-MM-B-005",
                name: "Acyclic Inter-Module Contracts",
                coords: "Module x Boundaries",
                pattern: "(ModA)-[:DEPENDS_ON]->(ModB)-[:DEPENDS_ON]->(ModA)",
                severity: "Error",
                description: "Module contract dependencies must remain strictly acyclic.",
                query: "MATCH (m1:Project)-[:DEPENDS_ON]->(m2:Project)-[:DEPENDS_ON]->(m1)\n  WHERE m1.id < m2.id\n  RETURN m1.path AS file, 1 AS line, m1.name AS symbol, m2.name AS target,\n         'Bidirectional coupling between modules ' + m1.name + ' and ' + m2.name AS message",
                platform: stack
              }
            ];
          }
        },
        "vertical-slice": {
          title: "Vertical Slice Architecture (Zero Lateral Slice Coupling)",
          subtitle: "Organize code by self-contained feature slices rather than horizontal technical layers.",
          note: "AI coding agents love reusing helpers from a neighboring feature folder, silently tangling independent slices into a distributed spaghetti ball.",
          kv: [
            ["Best fit", "Feature-driven product apps, CQRS command/query pipelines"],
            ["Avoid when", "Heavy shared algorithmic engines with minimal feature endpoints"],
            ["Graph layers used", "L1 Folder Slice + L3 CALLS / INSTANTIATES"],
            ["Legacy repo onboarding", "Instant slice isolation check across Features/**"]
          ],
          layers: function (s) {
            return [
              { name: "Slice 1: CreateOrder (Endpoint + Command + Validator + SQL)", paths: s.slicePrefix + "Orders/CreateOrder/**", core: false },
              { name: "Slice 2: CancelOrder (Endpoint + Command + Validator + SQL)", paths: s.slicePrefix + "Orders/CancelOrder/**", core: false },
              { name: "Slice 3: IssueInvoice (Endpoint + Command + Validator + SQL)", paths: s.slicePrefix + "Billing/IssueInvoice/**", core: false },
              { name: "Shared Domain & Infrastructure Primitives", paths: "Shared/**, Domain/** (inward only)", core: true }
            ];
          },
          edges: [
            { label: "Feature Slice to Shared Domain", allow: true, code: "ALLOW" },
            { label: "Self-contained Handler to DB inside Slice", allow: true, code: "ALLOW" },
            { label: "Feature Slice A calling Feature Slice B", allow: false, code: "SIG-VSA-B-001", drawer: "drawer-0" },
            { label: "Shared folder calling into a Feature Slice", allow: false, code: "SIG-VSA-B-002", drawer: "drawer-1" }
          ],
          gates: function (s, stack) {
            return [
              {
                id: "SIG-VSA-B-001",
                name: "Zero Lateral Feature-to-Feature Calls",
                coords: "Module x Boundaries",
                pattern: "(Features/SliceA)-[:CALLS]->(Features/SliceB)",
                severity: "Error",
                description: "Feature slices must remain independent and never directly call handlers, DTOs, or helpers in sibling slices.",
                query: "MATCH (a:Function)-[:CALLS]->(b:Function)\n  WHERE a.file_path CONTAINS '" + s.slicePrefix + "' AND b.file_path CONTAINS '" + s.slicePrefix + "'\n    AND split(a.file_path, '/')[2] <> split(b.file_path, '/')[2]\n  RETURN a.file_path AS file, a.start_line AS line, a.name AS symbol, b.name AS target,\n         'Feature slice function ' + a.name + ' couples laterally to sibling slice symbol ' + b.name AS message",
                platform: stack
              },
              {
                id: "SIG-VSA-B-002",
                name: "Shared Primitives Never Call Feature Slices",
                coords: "Module x Boundaries",
                pattern: "(Shared|Common|Domain)-[:CALLS]->(Features/*)",
                severity: "Error",
                description: "Shared infrastructure and domain primitives must never depend upward on specific feature slices.",
                query: "MATCH (sh:Function)-[:CALLS]->(feat:Function)\n  WHERE NOT sh.file_path CONTAINS '" + s.slicePrefix + "' AND feat.file_path CONTAINS '" + s.slicePrefix + "'\n    AND (sh.file_path CONTAINS 'Shared' OR sh.file_path CONTAINS 'Common' OR sh.file_path CONTAINS 'Domain')\n  RETURN sh.file_path AS file, sh.start_line AS line, sh.name AS symbol, feat.name AS target,\n         'Shared symbol ' + sh.name + ' depends upward on feature slice ' + feat.name AS message",
                platform: stack
              },
              {
                id: "SIG-VSA-R-003",
                name: "Single-Responsibility Slice Handler Size",
                coords: "Method x Resources",
                pattern: "Slice Handler method exceeding 150 LOC",
                severity: "Warning",
                description: "Keep vertical slice handlers concise by pushing complex invariants into pure domain methods.",
                query: "MATCH (fn:Function)\n  WHERE fn.file_path CONTAINS '" + s.slicePrefix + "' AND coalesce(fn.loc, 0) > 150\n  RETURN fn.file_path AS file, fn.start_line AS line, fn.name AS symbol, toString(fn.loc) + ' LOC' AS target,\n         'Slice handler ' + fn.name + ' exceeds 150 LOC ceiling' AS message",
                platform: stack
              },
              {
                id: "SIG-VSA-C-004",
                name: "No Bloated Generic Repository in Vertical Slices",
                coords: "Class x Contract",
                pattern: "Generic Repository class (>15 methods) shared across slices",
                severity: "Warning",
                description: "Vertical slices favor direct query/command access over giant shared repository classes.",
                query: "MATCH (t:Type)-[:HAS_METHOD]->(m:Function)\n  WHERE t.name ENDS WITH 'Repository'\n  WITH t, count(m) AS methods WHERE methods > 15\n  RETURN t.file_path AS file, t.start_line AS line, t.name AS symbol, toString(methods) + ' methods' AS target,\n         'Shared repository ' + t.name + ' accumulates ' + toString(methods) + ' methods across slices' AS message",
                platform: stack
              }
            ];
          }
        },
        "feature-sliced-design": {
          title: "Feature-Sliced Design (FSD Frontend Hierarchy)",
          subtitle: "Strict downward layer hierarchy: app -> pages -> widgets -> features -> entities -> shared.",
          note: "Frontend repos suffer constant upward import drift when AI agents import a widget or feature inside entities or shared UI components.",
          kv: [
            ["Best fit", "Large React, Next.js, Vue, or Angular frontends"],
            ["Avoid when", "Small marketing sites or single-screen widgets"],
            ["Graph layers used", "L1 File/Folder + L3 Function/Type CALLS & references"],
            ["Legacy repo onboarding", "Ratchet existing FSD violations in 1 command"]
          ],
          layers: function () {
            return [
              { name: "1. app & pages (Routing, Providers, Full Route Views)", paths: "src/app/**, src/pages/**", core: false },
              { name: "2. widgets (Composite Page Blocks)", paths: "src/widgets/**", core: false },
              { name: "3. features (User Interactions & Business Actions)", paths: "src/features/**", core: false },
              { name: "4. entities & shared (Domain Models & Reusable UI Kit)", paths: "src/entities/**, src/shared/**", core: true }
            ];
          },
          edges: [
            { label: "pages -> widgets -> features -> entities -> shared", allow: true, code: "ALLOW" },
            { label: "Slice consumed via public index.ts", allow: true, code: "ALLOW" },
            { label: "entities or shared importing features/widgets", allow: false, code: "SIG-FSD-B-001", drawer: "drawer-0" },
            { label: "Sibling feature-to-feature cross-call", allow: false, code: "SIG-FSD-B-002", drawer: "drawer-1" }
          ],
          gates: function (_s, stack) {
            var effStack = stack === "agnostic" ? "typescript" : stack;
            return [
              {
                id: "SIG-FSD-B-001",
                name: "No Upward Layer Dependencies in FSD",
                coords: "Module x Boundaries",
                pattern: "(shared|entities)-[:CALLS]->(features|widgets|pages|app)",
                severity: "Error",
                description: "Lower FSD layers (shared, entities) must never reference higher layers (features, widgets, pages, app).",
                query: "MATCH (low:Function)-[:CALLS]->(high:Function)\n  WHERE (low.file_path CONTAINS 'src/shared/' OR low.file_path CONTAINS 'src/entities/')\n    AND (high.file_path CONTAINS 'src/features/' OR high.file_path CONTAINS 'src/widgets/' OR high.file_path CONTAINS 'src/pages/' OR high.file_path CONTAINS 'src/app/')\n  RETURN low.file_path AS file, low.start_line AS line, low.name AS symbol, high.name AS target,\n         'Upward FSD layer violation: ' + low.file_path + ' calls ' + high.file_path AS message",
                platform: effStack
              },
              {
                id: "SIG-FSD-B-002",
                name: "No Cross-Imports Within Same FSD Layer",
                coords: "Module x Boundaries",
                pattern: "(src/features/Auth)-[:CALLS]->(src/features/Checkout)",
                severity: "Error",
                description: "Slices on the same FSD layer (features or entities) must not couple horizontally; compose them in widgets or pages.",
                query: "MATCH (a:Function)-[:CALLS]->(b:Function)\n  WHERE a.file_path CONTAINS 'src/features/' AND b.file_path CONTAINS 'src/features/'\n    AND split(a.file_path, '/')[2] <> split(b.file_path, '/')[2]\n  RETURN a.file_path AS file, a.start_line AS line, a.name AS symbol, b.name AS target,\n         'Horizontal FSD slice coupling between features/' + split(a.file_path, '/')[2] + ' and features/' + split(b.file_path, '/')[2] AS message",
                platform: effStack
              },
              {
                id: "SIG-FSD-B-003",
                name: "Features Must Not Depend on Widgets or Pages",
                coords: "Module x Boundaries",
                pattern: "(src/features/*)-[:CALLS]->(src/widgets/* | src/pages/*)",
                severity: "Error",
                description: "Features sit below widgets and pages and must never call upward into composite page blocks.",
                query: "MATCH (f:Function)-[:CALLS]->(up:Function)\n  WHERE f.file_path CONTAINS 'src/features/'\n    AND (up.file_path CONTAINS 'src/widgets/' OR up.file_path CONTAINS 'src/pages/')\n  RETURN f.file_path AS file, f.start_line AS line, f.name AS symbol, up.name AS target,\n         'Feature ' + f.name + ' calls upward FSD layer ' + up.file_path AS message",
                platform: effStack
              },
              {
                id: "SIG-FSD-C-004",
                name: "Shared Layer Business-Entity Agnosticism",
                coords: "Module x Contract",
                pattern: "src/shared/** referencing domain API endpoints directly",
                severity: "Warning",
                description: "src/shared should provide generic UI/lib primitives rather than domain-specific API calls.",
                query: "MATCH (es:ExternalService)-[:DECLARED_IN]->(f:File)\n  WHERE f.path CONTAINS 'src/shared/' AND NOT f.path CONTAINS 'src/shared/api/'\n  RETURN f.path AS file, coalesce(es.start_line, 1) AS line, es.name AS symbol, es.url AS target,\n         'Domain network call ' + es.name + ' leaked into generic src/shared layer' AS message",
                platform: effStack
              }
            ];
          }
        },
        "microservices-isolation": {
          title: "Shared-Nothing Microservices (DB & Sync Fan-Out Guard)",
          subtitle: "Each microservice owns its database schema exclusively and avoids deep synchronous call cascades.",
          note: "In polyglot monorepos (Go + TS + .NET + Python), only a unified L4 graph sees when two services read or write the exact same SQL table.",
          kv: [
            ["Best fit", "Polyglot multi-service monorepos and distributed platforms"],
            ["Avoid when", "Single-database monoliths"],
            ["Graph layers used", "L2 Service + L4 Endpoint / ExternalService / Table / Topic"],
            ["Legacy repo onboarding", "Audit shared tables & freeze migration baseline"]
          ],
          layers: function () {
            return [
              { name: "API Gateway / Edge BFF", paths: "services/gateway/**, apps/bff/**", core: false },
              { name: "Autonomous Microservices (Order, Payment, Inventory)", paths: "services/*/**", core: true },
              { name: "Async Event Broker (Kafka / RabbitMQ / PubSub Topics)", paths: "L4 Topic contracts", core: false },
              { name: "Isolated Per-Service Databases", paths: "1 owner service per Table", core: false }
            ];
          },
          edges: [
            { label: "Service A -> Broker Topic -> Service B", allow: true, code: "ALLOW" },
            { label: "Service A -> Service A Dedicated DB", allow: true, code: "ALLOW" },
            { label: "Two Services accessing same SQL Table", allow: false, code: "SIG-MS-B-001", drawer: "drawer-0" },
            { label: "Cyclic synchronous HTTP/RPC call loop", allow: false, code: "SIG-MS-B-002", drawer: "drawer-1" }
          ],
          gates: function (_s, stack) {
            return [
              {
                id: "SIG-MS-B-001",
                name: "Shared-Database Integration Anti-Pattern",
                coords: "System x Boundaries",
                pattern: "(ServiceA) and (ServiceB) both READS_FROM or WRITES_TO (Table)",
                severity: "Error",
                description: "No database table may be accessed directly by more than one microservice.",
                query: "MATCH (q1:Query)-[:READS_FROM|WRITES_TO]->(t:Table)<-[:READS_FROM|WRITES_TO]-(q2:Query)\n  MATCH (q1)-[:ACCESSED_BY]->(f1:Function), (q2)-[:ACCESSED_BY]->(f2:Function)\n  WHERE f1.project_name < f2.project_name\n  RETURN f1.file_path AS file, f1.start_line AS line, f1.project_name AS symbol, t.name AS target,\n         'Shared database coupling on table ' + t.name + ' between ' + f1.project_name + ' and ' + f2.project_name AS message",
                platform: stack
              },
              {
                id: "SIG-MS-B-002",
                name: "Distributed Synchronous Call Cycle Shield",
                coords: "System x Boundaries",
                pattern: "(SvcA)-[:SERVICE_CALL]->(SvcB)-[:SERVICE_CALL]->(SvcA)",
                severity: "Error",
                description: "Synchronous HTTP/gRPC service-to-service calls must never form distributed cycles.",
                query: "MATCH (a:App)-[:SERVICE_CALL]->(b:App)-[:SERVICE_CALL]->(a)\n  WHERE a.id < b.id\n  RETURN a.path AS file, 1 AS line, a.name AS symbol, b.name AS target,\n         'Distributed synchronous call cycle between ' + a.name + ' and ' + b.name AS message",
                platform: stack
              },
              {
                id: "SIG-MS-B-003",
                name: "No Direct Cross-Service Source Code Imports",
                coords: "Service x Boundaries",
                pattern: "(ServiceA:App)-[:DEPENDS_ON]->(ServiceB:App)",
                severity: "Error",
                description: "Deployable microservices may depend on shared libraries, never directly on another deployable service.",
                query: "MATCH (s1:App)-[:IMPLEMENTED_BY]->(p1:Project)-[:DEPENDS_ON]->(p2:Project)<-[:IMPLEMENTED_BY]-(s2:App)\n  WHERE p1.id <> p2.id\n  RETURN p1.path AS file, 1 AS line, p1.name AS symbol, p2.name AS target,\n         'Deployable service ' + p1.name + ' directly imports deployable service ' + p2.name AS message",
                platform: stack
              },
              {
                id: "SIG-MS-R-004",
                name: "N+1 Synchronous HTTP Call Inside Loop",
                coords: "Method x Resources",
                pattern: "Outbound HTTP client call inside loop execution context",
                severity: "Warning",
                description: "Avoid sequential cross-service HTTP calls inside loops; prefer batch endpoints or async fan-out.",
                query: "MATCH (es:ExternalService)<-[:CALLED_BY]-(fn:Function)\n  WHERE fn.loc > 25\n  RETURN fn.file_path AS file, coalesce(es.start_line, fn.start_line) AS line, fn.name AS symbol, es.name AS target,\n         'Verify outbound RPC call ' + es.name + ' in ' + fn.name + ' uses batching and explicit timeout' AS message",
                platform: stack
              }
            ];
          }
        }
      };

      var currentPack = "clean-architecture";
      var currentStack = "agnostic";

      function esc(str) {
        return String(str)
          .replace(/&/g, "&amp;")
          .replace(/</g, "&lt;")
          .replace(/>/g, "&gt;")
          .replace(/"/g, "&quot;");
      }

      function buildGateYaml(g) {
        var parts = g.coords.split(" x ");
        return [
          "id: " + g.id,
          "category: Architectural",
          "platform: " + g.platform,
          "scope: " + parts[0],
          "dimension: " + parts[1],
          "severity: " + g.severity,
          "name: " + g.name,
          "description: " + g.description,
          "query: >-",
          "  " + g.query
        ].join("\n");
      }

      function buildPackBundleYaml() {
        var pack = PACKS[currentPack];
        var s = STACK_PATHS[currentStack];
        var gates = pack.gates(s, currentStack);
        var header = "# CodeExplorer Semantic Invariant Gate Pack\n# Pack: " + currentPack + " | Stack: " + currentStack + "\n# Place in .codeexplorer/gates/" + currentPack + ".yaml and run: ce gate check\n---\n";
        return header + gates.map(buildGateYaml).join("\n---\n") + "\n";
      }

      function render() {
        var pack = PACKS[currentPack];
        var s = STACK_PATHS[currentStack];
        var layers = pack.layers(s);
        var gates = pack.gates(s, currentStack);

        document.getElementById("bp-title").textContent = pack.title;
        document.getElementById("bp-subtitle").textContent = pack.subtitle;
        document.getElementById("bp-note").innerHTML = "<strong>Architectural guard note:</strong> " + esc(pack.note);
        document.getElementById("pack-slug-badge").textContent = "pack: " + currentPack;
        document.getElementById("cli-cmd").textContent = "ce gate install " + currentPack + " --stack " + currentStack;

        var kvHtml = pack.kv.map(function (row) {
          return '<li class="kv-row"><span class="kv-label">' + esc(row[0]) + '</span><span class="kv-val">' + esc(row[1]) + '</span></li>';
        }).join("");
        document.getElementById("pack-kv").innerHTML = kvHtml;

        var layersHtml = layers.map(function (l) {
          var cls = l.core ? "topo-layer topo-layer--core" : "topo-layer";
          return '<div class="' + cls + '"><span class="topo-layer-name">' + esc(l.name) + '</span><span class="topo-layer-paths">' + esc(l.paths) + '</span></div>';
        }).join("");

        var edgesHtml = '<div class="topo-connectors">' + pack.edges.map(function (e) {
          if (e.allow) {
            return '<div class="edge-rule edge-rule--allow"><span>' + esc(e.label) + '</span><code class="edge-rule-code">Allowed</code></div>';
          }
          return '<a href="#' + esc(e.drawer) + '" class="edge-rule edge-rule--block" data-open-drawer="' + esc(e.drawer) + '"><span>' + esc(e.label) + '</span><code class="edge-rule-code">' + esc(e.code.toLowerCase()) + '</code></a>';
        }).join("") + '</div>';

        document.getElementById("topology-stage").innerHTML = layersHtml + edgesHtml;

        document.getElementById("invariants-heading").textContent = "1. Included Semantic Invariant Gates: " + pack.title.split(" (")[0] + ".";
        document.getElementById("table-pack-caption").textContent = "Pack: " + currentPack + " (" + gates.length + " executable invariants)";
        document.getElementById("table-stack-caption").textContent = "Stack preset: " + currentStack;

        document.getElementById("gates-tbody").innerHTML = gates.map(function (g) {
          var badgeCls = g.severity === "Error" ? "badge badge--bad" : "badge badge--accent";
          return '<tr>' +
            '<td><code>' + esc(g.id) + '</code><br><strong>' + esc(g.name) + '</strong></td>' +
            '<td>' + esc(g.coords) + '</td>' +
            '<td><code>' + esc(g.pattern) + '</code></td>' +
            '<td class="cell-num"><code class="' + badgeCls + '">' + esc(g.severity) + '</code></td>' +
            '</tr>';
        }).join("");

        var drawersHead = '<div class="prompt-stack-head">' +
          '<h3 class="prompt-stack-title">Inspect executable YAML &amp; OpenCypher definitions</h3>' +
          '<span class="prompt-stack-note">Drop directly into <code>.codeexplorer/gates/</code> or adapt paths with your AI agent</span>' +
          '</div>';

        var drawersHtml = gates.map(function (g, idx) {
          var openAttr = idx === 0 ? " open" : "";
          var badgeCls = g.severity === "Error" ? "badge badge--bad" : "badge badge--accent";
          return '<details id="drawer-' + idx + '" class="prompt-drawer"' + openAttr + '>' +
            '<summary>' +
              '<span class="prompt-drawer-title"><code class="' + badgeCls + '">' + esc(g.id) + '</code><span>' + esc(g.name) + ' (OpenCypher YAML)</span></span>' +
              '<span class="prompt-drawer-toggle"></span>' +
            '</summary>' +
            '<div class="prompt-drawer-body"><pre class="code-block">' + esc(buildGateYaml(g)) + '</pre></div>' +
            '</details>';
        }).join("");

        document.getElementById("yaml-drawers").innerHTML = drawersHead + drawersHtml;

        document.querySelectorAll(".arch-tab").forEach(function (btn) {
          btn.setAttribute("aria-selected", btn.getAttribute("data-pack") === currentPack ? "true" : "false");
        });
        document.querySelectorAll(".stack-btn").forEach(function (btn) {
          btn.setAttribute("aria-pressed", btn.getAttribute("data-stack") === currentStack ? "true" : "false");
        });
      }

      function triggerDownload() {
        var yaml = buildPackBundleYaml();
        var blob = new Blob([yaml], { type: "text/yaml;charset=utf-8" });
        var url = URL.createObjectURL(blob);
        var a = document.createElement("a");
        a.href = url;
        a.download = currentPack + "-" + currentStack + ".gates.yaml";
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
      }

      function flashButton(btn, msg) {
        var orig = btn.textContent;
        btn.textContent = msg;
        setTimeout(function () {
          btn.textContent = orig;
        }, 1600);
      }

      var onArchClick = function (ev) {
        var tab = ev.target.closest(".arch-tab");
        if (tab) {
          currentPack = tab.getAttribute("data-pack");
          render();
          return;
        }

        var stackBtn = ev.target.closest(".stack-btn");
        if (stackBtn) {
          currentStack = stackBtn.getAttribute("data-stack");
          render();
          return;
        }

        var selectBtn = ev.target.closest("[data-select-pack]");
        if (selectBtn) {
          currentPack = selectBtn.getAttribute("data-select-pack");
          render();
          document.getElementById("workbench").scrollIntoView({ behavior: "smooth" });
          return;
        }

        var drawerLink = ev.target.closest("[data-open-drawer]");
        if (drawerLink) {
          var targetId = drawerLink.getAttribute("data-open-drawer");
          var drawer = document.getElementById(targetId);
          if (drawer) {
            ev.preventDefault();
            drawer.open = true;
            drawer.scrollIntoView({ behavior: "smooth", block: "center" });
          }
          return;
        }
      };
      document.addEventListener("click", onArchClick);

      document.getElementById("download-pack-btn").addEventListener("click", triggerDownload);
      document.getElementById("hero-download-btn").addEventListener("click", triggerDownload);

      document.getElementById("copy-cli-btn").addEventListener("click", function () {
        var btn = this;
        var text = document.getElementById("cli-cmd").textContent;
        if (navigator.clipboard) {
          navigator.clipboard.writeText(text).then(function () {
            flashButton(btn, "Copied");
          });
        }
      });

      document.getElementById("copy-prompt-btn").addEventListener("click", function () {
        var btn = this;
        var promptText = "Inspect my workspace using CodeExplorer get_architecture_map, adapt the following '" +
          currentPack + "' (" + currentStack + ") Semantic Invariant Gate pack to match our actual folder/project paths, save the rules to .codeexplorer/gates/, run check_semantic_gates, and freeze existing baseline debt via ce gate ratchet --update:\n\n" +
          buildPackBundleYaml();
        if (navigator.clipboard) {
          navigator.clipboard.writeText(promptText).then(function () {
            flashButton(btn, "Prompt Copied");
          });
        }
      });
    
  return () => {
    document.removeEventListener("click", onArchClick);
  };
}
