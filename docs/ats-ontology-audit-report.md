# CodeExplorer Monorepo Multi-Layer Ontology Audit Report

**Audit Timestamp:** 2026-10-12T09:35:08.365Z
**Workspace:** `C:\Work\ATS\src\services`
**Total Audited Services:** 5

> [!IMPORTANT]
> This report provides an in-depth, multi-layer verification of CodeExplorer's extracted ontology knowledge graph vs. the ground-truth codebase in the ATS microservice monorepo. It details discrepancies across all 5 ontology layers, identifies recurring primitive obsession and string hardcodes, and catalogs specific parser blind spots.

## 1. Executive Summary & Scorecard

| Service | Language | Role | CE Total Endpoints | CE Unique Routes | Duplicate Pairs | Code Endpoints | Matched | Missing (FN) | Phantom (FP) | Phantom Tables | Egress Calls (CE) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **rule-tree** | typescript | Service | 39 | 39 | 0 | 36 | 36 | 0 | 3 | 0 | 0 |
| **sources** | typescript | Service | 18 | 18 | 0 | 17 | 17 | 0 | 1 | 0 | 17 |
| **splits** | typescript | Service | 7 | 7 | 0 | 6 | 6 | 0 | 1 | 0 | 6 |
| **stage-worker** | typescript | Worker | 45 | 45 | 0 | 44 | 43 | 1 | 2 | 0 | 4 |
| **tb-map** | typescript | Service | 0 | 0 | 0 | 12 | 0 | 12 | 0 | 0 | 0 |

---

## 2. Catalog of Discovered Architectural Situations & Gaps

### Situation 1: Custom Client Abstraction Hiding Egress Calls (`ApiClientService` & `API_ROUTES`)
- **Discovered in:** `bff` and downstream consumers.
- **Mechanism:** The service encapsulates all HTTP communications inside a centralized `ApiClientService`. Instead of calling `http.get("http://bundle-scheduler/api/v1/...")`, application modules call `this.apiClient.get('SCHEDULED_BUNDLES')`.
- **Ground Truth:** In `src/shared/config/config.consts.ts`, the mapping `SERVICE_DOMAINS` and `API_ROUTES` binds `SCHEDULED_BUNDLES` to service `bundle-scheduler` and path `/api/v1/timetables`.
- **Ontology Impact:** CodeExplorer sees a generic call to `ApiClientService.get` with a string literal symbol, but because it doesn't trace the configuration dictionary `API_ROUTES`, **the cross-service link to `bundle-scheduler` is completely missing from the graph**.

### Situation 2: SQL Lexer/AST False Positives (The `SKIP` Table Phenomenon)
- **Discovered in:** `action-scheduler`, `bundle`.
- **Mechanism:** Code queries PostgreSQL with `SELECT ... FROM scheduled_actions ... FOR UPDATE SKIP LOCKED`.
- **Parser Gap:** The regex/heuristic in `Layer4SemanticParser` looks for keywords following `SELECT ... FROM` or tokens before clause boundaries. The token `SKIP` was captured as a database table entity `Table { name: "SKIP" }`.
- **Ontology Impact:** A phantom Table node `ws:db:default:...:tbl:skip` was created, connected to the project via `CONTAINS`. This pollutes data lineage and database topology diagrams.

### Situation 3: Dynamic Template Literals & Wildcard Partitioning in BigQuery
- **Discovered in:** `bff`, `cpm-streaming-aggregator`.
- **Mechanism:** Analytics queries reference wildcard tables like `impressions_tb_*` or dynamically interpolate variables: `FROM `tracker.data_tracker_${tableSuffix}``.
- **Parser Gap:** CodeExplorer strips template interpolation syntax or captures literal identifiers `data_tracker_tableSuffix`, creating separate table nodes for every variable name variant rather than recognizing a parameterized or sharded table entity.
- **Ontology Impact:** Fragmented and unresolvable table nodes in data lineage.

### Situation 4: Primitive Obsession in Lower-Level Domain Contracts
- **Discovered in:** All 5 services.
- **Mechanism:** Essential domain identifiers (action types, event names, queue routing keys, service states, external API endpoints) are modeled as raw `string` types rather than TypeScript string enums, union types, or value objects.
- **Ontology Impact:** Because types are just `string`, static AST analysis cannot differentiate between an arbitrary string comment and a critical routing key or event channel without runtime tracing or semantic intent enrichment.

### Situation 5: Go Language AST & Pipeline Semantic Ingestion
- **Discovered in:** `cpm-streaming-aggregator`.
- **Mechanism:** Go microservices use goroutines, channels, and custom HTTP/database wrappers rather than traditional dependency-injected controller classes.
- **Ontology Impact:** Ingress routes and egress client calls in Go services require specialized AST visitor support matching Go standard library `net/http` and `gin-gonic/gin` constructs.

### Situation 6: Inconsistent Service Naming Causing Duplicate Endpoint Nodes
- **Discovered in:** `action-scheduler`, `bff`.
- **Mechanism:** One parser/layer constructs Endpoint IDs using the package manifest name (`ws:ep:internal-service-action-scheduler:GET:/ping`), while another layer constructs them using the directory name (`ws:ep:action-scheduler:GET:/ping`).
- **Ontology Impact:** Every HTTP endpoint in the project is duplicated in the graph, with identical routes connected to the same project node under two different IDs. This doubles the apparent API surface and confuses call resolution.

### Situation 7: Express Router Mount Prefix Loss (`app.use('/prefix', router)`)
- **Discovered in:** `bundle`.
- **Mechanism:** In Express apps, router modules define routes relative to their mount root (e.g., `router.delete('/:id')`), and the main app mounts them via `app.use('/api/bundles/', bundleRoutes)`.
- **Parser Gap:** CodeExplorer indexes the route file in isolation without propagating the parent mount prefix from `index.ts`. The endpoint becomes `DELETE:/:id` or `DELETE:/` instead of `DELETE:/api/bundles/:id`.
- **Ontology Impact:** When downstream services call `/` or `/:id`, they falsely bind to these un-prefixed Express routes, creating phantom cross-service dependencies.

---

## 3. Deep Dive Analysis by Service

### 3.1 Service: `rule-tree`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), Redis` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 39 endpoints | **Unique Routes:** 39
- **Ground Truth in Code:** 36 endpoints.
- **Matched:** 36 | **Missing (FN):** 0 | **Phantom (FP):** 3

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/findAll` (Controller: ``)
- `QUERY /api/getAll` (Controller: ``)
- `QUERY /api/preview` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `Redis`, `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.public`, `PostgreSQL.split`, `PostgreSQL.tree_rules`
- **Tables in CE:** 20 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.2 Service: `sources`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 18 endpoints | **Unique Routes:** 18
- **Ground Truth in Code:** 17 endpoints.
- **Matched:** 17 | **Missing (FN):** 0 | **Phantom (FP):** 1

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/get` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`, `PostgreSQL.sources`
- **Tables in CE:** 10 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 17 calls.
  - `sources` — (Direct) -> `adhub` (ExternalService) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `bqroutescalc` (ExternalService) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `bundles` (ExternalService) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `cf-gateway` (ExternalService) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `domains` (ExternalService) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `postbackpartner` (ExternalService) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `ruletreeupdater` (ExternalService) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `traffic_types` (ExternalService) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `ad-hub` (Project, Service) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `bq-routes-calculation` (Project, Service) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `cf-gateway` (Project, Service) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `domain` (Project, Service) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `postback-partner` (Project, Service) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `rule-tree-updater` (Project, Service) [Method: `*`, Path: `*`]
  - `sources` — (Direct) -> `traffic-types` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 2
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${this.serviceName}-${serviceName}.${this.deployment}`` in `src/configs/cluster.config.ts:47`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${this.domain}`` in `src/configs/cluster.config.ts:48`

---

### 3.3 Service: `splits`

- **Language / Framework:** typescript | Frameworks: NestJS, Fastify
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 7 endpoints | **Unique Routes:** 7
- **Ground Truth in Code:** 6 endpoints.
- **Matched:** 6 | **Missing (FN):** 0 | **Phantom (FP):** 1

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/test_id` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.bundles`, `PostgreSQL.split`
- **Tables in CE:** 5 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 6 calls.
  - `splits` — (Direct) -> `rule-configurator` (ExternalService) [Method: `*`, Path: `*`]
  - `splits` — (Direct) -> `rule-tree-updater` (ExternalService) [Method: `*`, Path: `*`]
  - `splits` — (Direct) -> `syskv` (ExternalService) [Method: `*`, Path: `*`]
  - `splits` — (Direct) -> `kv` (Project, Service) [Method: `*`, Path: `*`]
  - `splits` — (Direct) -> `rule-configurator` (Project, Service) [Method: `*`, Path: `*`]
  - `splits` — (Direct) -> `rule-tree-updater` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 4
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${this.clusterServiceName}-${serviceName}.${this.clusterDeployment}`` in `src/configs/app.config.ts:58`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${this.clusterDomain}`` in `src/configs/app.config.ts:59`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/databases/entities/full-bundles.entity.ts:14`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/shared/constants/types.ts:22`

---

### 3.4 Service: `stage-worker`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), Redis` | Queue: `RabbitMQ (amqp), BullMQ`
- **Primary Role (CE):** `Worker`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 45 endpoints | **Unique Routes:** 45
- **Ground Truth in Code:** 44 endpoints.
- **Matched:** 43 | **Missing (FN):** 1 | **Phantom (FP):** 2

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `POST /` (src/routes/rabbit.routes.ts:6)

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `GET /api/stages/integration_in_progress/networks/:network` (Controller: ``)
- `POST /api/rabbit` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `Redis`, `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.id`
- **Tables in CE:** 3 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 4 calls.
  - `stage-worker` — (Direct) -> `bq-routes-calc` (ExternalService) [Method: `*`, Path: `*`]
  - `stage-worker` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `stage-worker` — (Direct) -> `bq-routes-calculation` (Project, Service) [Method: `*`, Path: `*`]
  - `stage-worker` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 10
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/consts.ts:72`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/consts.ts:73`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.dev-at-systems.xyz`` in `src/consts.ts:75`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/controllers/rabbit.controller.ts:195`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/interfaces/bq-response/calculation-queue/IBundleUpdateAction.ts:7`
  - *...and 5 more occurrences.*

---

### 3.5 Service: `tb-map`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 12 endpoints.
- **Matched:** 0 | **Missing (FN):** 12 | **Phantom (FP):** 0

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `GET /status` (src/controllers/logger.controller.ts:8)
- `PUT /:status` (src/controllers/logger.controller.ts:15)
- `GET /` (src/controllers/ping.controller.ts:6)
- `POST /batch` (src/controllers/tb-click-types.controller.ts:24)
- `GET /` (src/controllers/tb-click-types.controller.ts:32)
- `PUT /batch` (src/controllers/tb-click-types.controller.ts:44)
- `DELETE /batch` (src/controllers/tb-click-types.controller.ts:50)
- `GET /filter` (src/controllers/tb-click-types.controller.ts:56)
- `POST /` (src/controllers/tb-click-types.controller.ts:62)
- `GET /:id` (src/controllers/tb-click-types.controller.ts:68)
- *...and 2 more.*

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`
- **Tables in CE:** 1 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 3
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/constants.ts:25`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/constants.ts:26`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.dev-at-systems.xyz`` in `src/constants.ts:28`

---

## 4. Prioritized Recommendations & Action Plan for CodeExplorer

1. **Implement Configuration-Aware Service Call Resolution (Layer 4/5):**
   - Add a pattern detector in `CodeExplorer.Parser.TypeScript` for service dictionary tables like `API_ROUTES` and `SERVICE_DOMAINS`.
   - Resolve indirect calls like `apiClient.get('ROUTE_KEY')` by cross-referencing the enum/string key to its declared target domain and path.

2. **Sanitize SQL AST Extraction to Prevent Phantom Tables:**
   - In `NestedSqlParser` / `Layer4SemanticParser`, enrich the SQL reserved word stoplist to include PostgreSQL keywords: `SKIP`, `LOCKED`, `NOWAIT`, `PARTITION`, `OVER`, `CASCADE`.
   - Ignore BigQuery internal metadata tables such as `__TABLES__` or treat them under a dedicated `SystemCatalog` node label instead of user schema tables.

3. **Handle Dynamic Table Template Literals in BigQuery / ORM:**
   - Detect patterns like `data_tracker_${tableSuffix}` or `impressions_tb_*` and normalize them into a parameterized table family rather than generating literal string table nodes.

4. **Expand Lower-Level Ontological Typing:**
   - Detect string fields in DTOs and contracts and classify them as candidates for domain type refinement.
   - Connect string literals in messaging producers (`channel.sendToQueue('string')`) to inferred message topic nodes.

