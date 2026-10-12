# CodeExplorer Monorepo Multi-Layer Ontology Audit Report

**Audit Timestamp:** 2026-10-12T10:47:57.883Z
**Workspace:** `C:\Work\ATS\src\services`
**Total Audited Services:** 55

> [!IMPORTANT]
> This report provides an in-depth, multi-layer verification of CodeExplorer's extracted ontology knowledge graph vs. the ground-truth codebase in the ATS microservice monorepo. It details discrepancies across all 5 ontology layers, identifies recurring primitive obsession and string hardcodes, and catalogs specific parser blind spots.

## 1. Executive Summary & Scorecard

| Service | Language | Role | CE Total Endpoints | CE Unique Routes | Duplicate Pairs | Code Endpoints | Matched | Missing (FN) | Phantom (FP) | Phantom Tables | Egress Calls (CE) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **action-scheduler** | typescript | Worker | 11 | 11 | 0 | 9 | 9 | 0 | 2 | 0 | 1 |
| **ad-hub** | typescript | Service | 41 | 41 | 0 | 33 | 33 | 0 | 8 | 0 | 2 |
| **adhub** | unknown | unknown | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **analytics** | typescript | Service | 5 | 5 | 0 | 5 | 5 | 0 | 0 | 0 | 1 |
| **approval** | typescript | Service | 5 | 5 | 0 | 5 | 5 | 0 | 0 | 0 | 3 |
| **bff** | typescript | Service | 131 | 131 | 0 | 112 | 112 | 0 | 19 | 0 | 13 |
| **bq-routes-calculation** | typescript | Service | 116 | 116 | 0 | 116 | 108 | 8 | 8 | 0 | 5 |
| **bundle** | typescript | Service | 166 | 166 | 0 | 166 | 127 | 39 | 35 | 0 | 27 |
| **bundle-cpm-controller** | typescript | Service | 6 | 6 | 0 | 6 | 6 | 0 | 0 | 0 | 1 |
| **bundle-priority** | typescript | Service | 6 | 6 | 0 | 6 | 6 | 0 | 0 | 0 | 3 |
| **bundle-scheduler** | typescript | Worker | 14 | 14 | 0 | 13 | 13 | 0 | 1 | 0 | 4 |
| **bundle-update-adapter** | typescript | Service | 14 | 14 | 0 | 11 | 11 | 0 | 4 | 0 | 1 |
| **calc-epm** | typescript | Service | 5 | 5 | 0 | 4 | 4 | 0 | 1 | 0 | 0 |
| **cf-gateway** | typescript | Service | 48 | 48 | 0 | 41 | 41 | 0 | 7 | 0 | 5 |
| **cf-landings** | typescript | Worker | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4 |
| **cf-worker** | typescript | Service | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 5 |
| **cf-worker-rule-tree** | typescript | Worker | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4 |
| **cf-worker-service-bindings** | typescript | Worker | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **cf-worker-ts** | unknown | unknown | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **cpm-rates-service** | go | Service | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **cpm-streaming-aggregator** | go | Service | 10 | 10 | 0 | 10 | 10 | 0 | 0 | 0 | 1 |
| **domain** | typescript | Service | 43 | 43 | 0 | 43 | 40 | 3 | 3 | 0 | 6 |
| **domain-checker** | typescript | Service | 8 | 8 | 0 | 6 | 6 | 0 | 2 | 0 | 3 |
| **domain-log** | typescript | Service | 5 | 5 | 0 | 4 | 4 | 0 | 1 | 0 | 0 |
| **domain-settler** | typescript | Service | 11 | 11 | 0 | 8 | 8 | 0 | 3 | 0 | 4 |
| **domain-v2** | typescript | Service | 54 | 54 | 0 | 48 | 48 | 0 | 7 | 0 | 12 |
| **edge-decision-engine** | typescript | Worker | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **fe** | typescript | FrontendApp | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 48 |
| **hub** | typescript | Service | 17 | 17 | 0 | 17 | 17 | 0 | 0 | 0 | 2 |
| **journal** | typescript | Service | 8 | 8 | 0 | 6 | 6 | 0 | 3 | 0 | 0 |
| **kv** | typescript | Service | 10 | 10 | 0 | 10 | 8 | 2 | 2 | 0 | 2 |
| **kv-v2** | typescript | Service | 3 | 3 | 0 | 3 | 3 | 0 | 0 | 0 | 2 |
| **landers** | typescript | Service | 25 | 25 | 0 | 20 | 20 | 0 | 5 | 0 | 0 |
| **landing** | typescript | FrontendApp | 1 | 1 | 0 | 2 | 1 | 1 | 0 | 0 | 0 |
| **netstatend** | typescript | Service | 7 | 7 | 0 | 7 | 7 | 0 | 0 | 0 | 5 |
| **networks** | typescript | Service | 23 | 23 | 0 | 21 | 21 | 0 | 2 | 0 | 3 |
| **notifier** | typescript | Service | 17 | 17 | 0 | 12 | 12 | 0 | 5 | 0 | 2 |
| **nrt** | typescript | Service | 8 | 8 | 0 | 6 | 6 | 0 | 2 | 0 | 1 |
| **partners-fe** | typescript | FrontendApp | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 2 |
| **partnerstat** | typescript | Service | 5 | 5 | 0 | 5 | 4 | 1 | 1 | 0 | 20 |
| **postback-partner** | typescript | Service | 22 | 22 | 0 | 16 | 16 | 0 | 6 | 0 | 3 |
| **prefetch** | typescript | Service | 3 | 3 | 0 | 3 | 3 | 0 | 0 | 0 | 6 |
| **rates** | typescript | Service | 8 | 8 | 0 | 8 | 8 | 0 | 0 | 0 | 4 |
| **redis-route** | typescript | Service | 77 | 77 | 0 | 77 | 77 | 0 | 0 | 0 | 0 |
| **rule-configurator** | typescript | Service | 12 | 12 | 0 | 10 | 10 | 0 | 2 | 0 | 0 |
| **rule-tree** | typescript | Service | 39 | 39 | 0 | 36 | 36 | 0 | 3 | 0 | 0 |
| **rule-tree-updater** | typescript | Service | 54 | 54 | 0 | 32 | 29 | 3 | 25 | 0 | 2 |
| **smart-cpa** | typescript | Service | 15 | 15 | 0 | 12 | 12 | 0 | 3 | 0 | 1 |
| **sources** | typescript | Service | 18 | 18 | 0 | 17 | 17 | 0 | 1 | 0 | 17 |
| **splits** | typescript | Service | 7 | 7 | 0 | 6 | 6 | 0 | 1 | 0 | 6 |
| **sql** | unknown | unknown | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **stage-worker** | typescript | Worker | 45 | 45 | 0 | 44 | 44 | 0 | 1 | 0 | 4 |
| **tb-map** | typescript | Service | 12 | 12 | 0 | 12 | 12 | 0 | 0 | 0 | 0 |
| **tracker** | typescript | Service | 3 | 3 | 0 | 3 | 3 | 0 | 0 | 0 | 1 |
| **traffic-types** | typescript | Service | 20 | 20 | 0 | 18 | 18 | 0 | 3 | 0 | 0 |

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

### 3.1 Service: `action-scheduler`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Worker`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 11 endpoints | **Unique Routes:** 11
- **Ground Truth in Code:** 9 endpoints.
- **Matched:** 9 | **Missing (FN):** 0 | **Phantom (FP):** 2

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY findAll` (Controller: ``)
- `QUERY findAllPaginated` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.action-scheduler`, `PostgreSQL.public`
- **Tables in CE:** 2 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 1 calls.
  - `action-scheduler` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 3
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.dev-at-systems.xyz`` in `src/common/config/config.service.ts:36`
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${config.clusterServiceName}-${serviceName}.${config.clusterDeployment}`` in `src/common/config/config.service.ts:39`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${config.clusterDomain}`` in `src/common/config/config.service.ts:40`

---

### 3.2 Service: `ad-hub`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 41 endpoints | **Unique Routes:** 41
- **Ground Truth in Code:** 33 endpoints.
- **Matched:** 33 | **Missing (FN):** 0 | **Phantom (FP):** 8

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/get` (Controller: ``)
- `QUERY /api/getAdFormats` (Controller: ``)
- `QUERY /api/getAdScenarios` (Controller: ``)
- `QUERY /api/getAdSetRules` (Controller: ``)
- `QUERY /api/getAdSets` (Controller: ``)
- `QUERY /api/getDspMaps` (Controller: ``)
- `QUERY /api/getDsps` (Controller: ``)
- `QUERY /api/remove` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.ads`, `PostgreSQL.defaults`, `PostgreSQL.public`
- **Tables in CE:** 9 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 2 calls.
  - `ad-hub` — (Direct) -> `traffic-types` (ExternalService) [Method: `*`, Path: `*`]
  - `ad-hub` — (Direct) -> `traffic-types` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 2
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/shared/constants/constants.ts:28`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/shared/constants/constants.ts:29`

---

### 3.3 Service: `adhub`

- **Language / Framework:** unknown | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `unknown`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** None
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 33
  - **Dynamic URL Host Interpolation:** ``https://${mirrorHost}/?state=${encodeURIComponent(nextToken)}${debugSuffix}`` in `adhub-cf-worker/src/core/fsm/fsmEngine.ts:107`
  - **Dynamic URL Host Interpolation:** ``https://${rotationHost}/?state=${encodeURIComponent(nextToken)}${debugSuffix}`` in `adhub-cf-worker/src/core/fsm/fsmEngine.ts:117`
  - **Dynamic URL Host Interpolation:** ``https://${options.trackerHost}${url}`` in `adhub-cf-worker/src/core/fsm/offerResolver.ts:103`
  - **Dynamic URL Host Interpolation:** ``https://${options.trackerHost}/click`` in `adhub-cf-worker/src/core/fsm/offerResolver.ts:115`
  - **Dynamic URL Host Interpolation:** ``https://${options.trackerHost}/click?cid=${encodeURIComponent(sid)}&type=${clickType}${countryParam}${sourceParam}${bundleParam}`` in `adhub-cf-worker/src/core/fsm/offerResolver.ts:183`
  - *...and 28 more occurrences.*

---

### 3.4 Service: `analytics`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), Redis, BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 5 endpoints | **Unique Routes:** 5
- **Ground Truth in Code:** 5 endpoints.
- **Matched:** 5 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.analytics`, `ClickHouse.cpm-streaming-aggregator`, `MySQL.analytics`, `PostgreSQL.analytics`, `PostgreSQL.defaults`
- **Tables in CE:** 1 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 1 calls.
  - `analytics` — (Direct) -> `api.telegram.org` (ExternalService) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 3
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/constants.ts:32`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/constants.ts:33`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.dev-at-systems.xyz`` in `src/constants.ts:35`

---

### 3.5 Service: `approval`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 5 endpoints | **Unique Routes:** 5
- **Ground Truth in Code:** 5 endpoints.
- **Matched:** 5 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`, `PostgreSQL.public`
- **Tables in CE:** 1 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 3 calls.
  - `approval` — (Direct) -> `bundles` (ExternalService) [Method: `*`, Path: `*`]
  - `approval` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `approval` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 2
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/common/constants.ts:23`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/common/constants.ts:24`

---

### 3.6 Service: `bff`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), Redis, BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 131 endpoints | **Unique Routes:** 131
- **Ground Truth in Code:** 112 endpoints.
- **Matched:** 112 | **Missing (FN):** 0 | **Phantom (FP):** 19

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY email` (Controller: ``)
- `QUERY forceUpdate` (Controller: ``)
- `QUERY getAllUsers` (Controller: ``)
- `QUERY getBalance` (Controller: ``)
- `QUERY getBalances` (Controller: ``)
- `QUERY getCalculationCost` (Controller: ``)
- `QUERY getCostOfWorkloads` (Controller: ``)
- `QUERY getDailyCost` (Controller: ``)
- `QUERY getData` (Controller: ``)
- `QUERY getDepthCurve` (Controller: ``)
- *...and 9 more.*

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.bff`, `PostgreSQL.bff`, `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.public`, `PostgreSQL.system_data`, `PostgreSQL.tracker`, `PostgreSQL.tracking`
- **Tables in CE:** 41 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 13 calls.
  - `bff` — (Direct) -> `ad-hub` (ExternalService) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `api.telegram.org` (ExternalService) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `auth` (ExternalService) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `bq-routes` (ExternalService) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `bq-routes-calc` (ExternalService) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `bundles` (ExternalService) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `domain-v2` (ExternalService) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `tb-map` (ExternalService) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `ad-hub` (Project, Service) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `bq-routes-calculation` (Project, Service) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]
  - `bff` — (Direct) -> `domain-v2` (Project, Service) [Method: `*`, Path: `*`]

##### Configured Target Services in Code (`API_ROUTES`):
  - Target `BQ_ROUTES`: 3 configured routes
  - Target `BUNDLE_SCHEDULER`: 2 configured routes
  - Target `BUNDLES`: 6 configured routes
  - Target `DOMAINS_V2`: 2 configured routes
  - Target `ID_HELPER_NEST`: 5 configured routes
  - Target `NETWORK_NEST`: 3 configured routes
  - Target `BQ_ROUTES_CALC`: 2 configured routes
  - Target `TB_MAP`: 1 configured routes
  - Target `AD_HUB`: 4 configured routes

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 28
  - **Dynamic SQL Template Interpolation:** `from ${projectId}` in `src/modules/cpm-stats/sql/cpm-stats.sql.ts:4`
  - **Dynamic SQL Template Interpolation:** `from ${projectId}` in `src/modules/cpm-stats/sql/cpm-stats.sql.ts:16`
  - **Primitive Obsession (Unmodeled string contract field):** `event: string;` in `src/modules/events/dto/events-pull.dto.ts:16`
  - **Primitive Obsession (Unmodeled string contract field):** `event: string;` in `src/modules/events/interfaces/event-payload.interface.ts:6`
  - **Primitive Obsession (Unmodeled string contract field):** `event: string;` in `src/modules/events/interfaces/event-payload.interface.ts:14`
  - *...and 23 more occurrences.*

---

### 3.7 Service: `bq-routes-calculation`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), Redis, BigQuery` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 116 endpoints | **Unique Routes:** 116
- **Ground Truth in Code:** 116 endpoints.
- **Matched:** 108 | **Missing (FN):** 8 | **Phantom (FP):** 8

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `GET /` (src/route/discrepancies.route.ts:6)
- `POST /` (src/route/discrepancy.route.ts:15)
- `GET /` (src/route/discrepancy.route.ts:16)
- `PUT /` (src/route/discrepancy.route.ts:17)
- `DELETE /` (src/route/discrepancy.route.ts:18)
- `POST /` (src/route/low-q-p.route.ts:6)
- `DELETE /` (src/route/low-q-p.route.ts:7)
- `GET /` (src/route/low-q-p.route.ts:8)

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `DELETE /api/discrepancy` (Controller: ``)
- `DELETE /api/low-quality-placements` (Controller: ``)
- `GET /api/discrepancies` (Controller: ``)
- `GET /api/discrepancy` (Controller: ``)
- `GET /api/low-quality-placements` (Controller: ``)
- `POST /api/discrepancy` (Controller: ``)
- `POST /api/low-quality-placements` (Controller: ``)
- `PUT /api/discrepancy` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.any`, `BigQuery.bq-routes-calculation`, `BigQuery.bundles`, `BigQuery.calc_rate`, `BigQuery.calculation_history`, `BigQuery.calculation_results`, `BigQuery.default`, `BigQuery.defaults`, `BigQuery.networks`, `BigQuery.srcmeta.schema`, `BigQuery.system_data`, `BigQuery.tb_rates`, `BigQuery.test`, `BigQuery.tracking`, `Redis`, `PostgreSQL.bq-routes-calculation`, `PostgreSQL.srcmeta.schema`
- **Tables in CE:** 81 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 5 calls.
  - `bq-routes-calculation` — (Direct) -> `bff` (ExternalService) [Method: `*`, Path: `*`]
  - `bq-routes-calculation` — (Direct) -> `notifier` (ExternalService) [Method: `*`, Path: `*`]
  - `bq-routes-calculation` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `bq-routes-calculation` — (Direct) -> `bff` (Project, Service) [Method: `*`, Path: `*`]
  - `bq-routes-calculation` — (Direct) -> `notifier` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 18
  - **Primitive Obsession (Unmodeled string contract field):** `state: string;` in `src/bq.utility.ts:72`
  - **Dynamic SQL Template Interpolation:** `from ${srcMeta.id}`` in `src/bq.utility.ts:193`
  - **Primitive Obsession (Unmodeled string contract field):** `type: string;` in `src/db.ts:100`
  - **Dynamic SQL Template Interpolation:** `FROM ${targetTable}` in `src/db.ts:154`
  - **Dynamic SQL Template Interpolation:** `FROM ${targetTable}` in `src/db.ts:155`
  - *...and 13 more occurrences.*

---

### 3.8 Service: `bundle`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), Redis` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 166 endpoints | **Unique Routes:** 166
- **Ground Truth in Code:** 166 endpoints.
- **Matched:** 127 | **Missing (FN):** 39 | **Phantom (FP):** 35

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `POST /` (src/routes/bundles-placements-black.route.ts:13)
- `GET /` (src/routes/bundles-placements-black.route.ts:15)
- `POST /` (src/routes/bundles.route.ts:65)
- `GET /` (src/routes/bundles.v2.route.ts:5)
- `GET /` (src/routes/country.route.ts:5)
- `GET /` (src/routes/custom-rate.route.ts:12)
- `DELETE /` (src/routes/custom-rate.route.ts:13)
- `POST /` (src/routes/custom-rate.route.ts:14)
- `GET /` (src/routes/killswitch.route.ts:14)
- `GET /` (src/routes/lander-images.route.ts:11)
- *...and 29 more.*

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `DELETE /api/custom-rates` (Controller: ``)
- `DELETE /api/rates-tb` (Controller: ``)
- `DELETE /api/tb-map` (Controller: ``)
- `DELETE /api/tb-template` (Controller: ``)
- `GET /api/countries` (Controller: ``)
- `GET /api/custom-rates` (Controller: ``)
- `GET /api/killswitch` (Controller: ``)
- `GET /api/lander-images` (Controller: ``)
- `GET /api/landers` (Controller: ``)
- `GET /api/networks` (Controller: ``)
- *...and 25 more.*

#### Layer 4: Databases & Tables
- **Databases:** `Redis`, `PostgreSQL.archive`, `PostgreSQL.bundle`, `PostgreSQL.bundle-scheduler`, `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.id`, `PostgreSQL.kv`, `PostgreSQL.landers`, `PostgreSQL.public`, `PostgreSQL.split`, `PostgreSQL.tree_rules`
- **Tables in CE:** 18 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 27 calls.
  - `bundle` — (Direct) -> `ad-hub` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `api.github.com` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `bq-routes-calc` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `bundle-scheduler` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `bundle-update-adapter` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `domains` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `id-helper-nest` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `journal` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `notifier` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `rates` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `redis-routes` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `rule-configurator` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `rule-tree` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `sources` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `ad-hub` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `bq-routes-calculation` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `bundle-scheduler` (Project, Worker) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `bundle-update-adapter` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `domain` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `journal` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `notifier` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `rates` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `redis-route` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `rule-configurator` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `rule-tree` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle` — (Direct) -> `sources` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 12
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/app/interfaces/id-helper.interface.ts:31`
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/consts.ts:38`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/consts.ts:39`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/controllers/bundles.controller.ts:254`
  - **Dynamic SQL Template Interpolation:** `from ${sources.length}`` in `src/controllers/rules.controller.ts:160`
  - *...and 7 more occurrences.*

---

### 3.9 Service: `bundle-cpm-controller`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 6 endpoints | **Unique Routes:** 6
- **Ground Truth in Code:** 6 endpoints.
- **Matched:** 6 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.id`
- **Tables in CE:** 6 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 1 calls.
  - `bundle-cpm-controller` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 2
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/database/entities/bundle.entity.ts:42`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/database/entities/full-bundles.entitiy.ts:38`

---

### 3.10 Service: `bundle-priority`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 6 endpoints | **Unique Routes:** 6
- **Ground Truth in Code:** 6 endpoints.
- **Matched:** 6 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.id`, `PostgreSQL.landers`, `PostgreSQL.tracking`
- **Tables in CE:** 11 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 3 calls.
  - `bundle-priority` — (Direct) -> `bundle-update-adapter` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle-priority` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle-priority` — (Direct) -> `bundle-update-adapter` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 5
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/db/entities/full-bundles.entitiy.ts:39`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/priority/interfaces/priority-row.ts:6`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/priority/interfaces/short-bundle.ts:6`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/priority/interfaces/status-effect.ts:3`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/priority/priority.service.ts:159`

---

### 3.11 Service: `bundle-scheduler`

- **Language / Framework:** typescript | Frameworks: NestJS, Fastify
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Worker`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 14 endpoints | **Unique Routes:** 14
- **Ground Truth in Code:** 13 endpoints.
- **Matched:** 13 | **Missing (FN):** 0 | **Phantom (FP):** 1

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/getStopped` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.bundle-scheduler`, `PostgreSQL.bundles`, `PostgreSQL.defaults`
- **Tables in CE:** 4 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 4 calls.
  - `bundle-scheduler` — (Direct) -> `bundles.at-systems.biz` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle-scheduler` — (Direct) -> `notifier.at-systems.biz` (ExternalService) [Method: `*`, Path: `*`]
  - `bundle-scheduler` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]
  - `bundle-scheduler` — (Direct) -> `notifier` (Project, Service) [Method: `*`, Path: `*`]

---

### 3.12 Service: `bundle-update-adapter`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), Redis, BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 14 endpoints | **Unique Routes:** 14
- **Ground Truth in Code:** 11 endpoints.
- **Matched:** 11 | **Missing (FN):** 0 | **Phantom (FP):** 4

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY getMultipleBundleUpdateData` (Controller: ``)
- `QUERY getMultipleBundleUpdateDataBySnapshot` (Controller: ``)
- `QUERY getMultipleBundleUpdateDataV2` (Controller: ``)
- `QUERY getSmartCpaCampaignsData` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `Redis`, `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.id`
- **Tables in CE:** 6 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 1 calls.
  - `bundle-update-adapter` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 10
  - **Primitive Obsession (Unmodeled string contract field):** `type: string;` in `src/db/entities/full-browser-version-types.entity.ts:7`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/db/entities/full-bundles.entitiy.ts:40`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/interfaces/responses/bundle-current-state-sc.response.ts:7`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/interfaces/responses/bundle-desired-state-sc-out.response.ts:7`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/interfaces/responses/bundle-desired-state-sc.response.ts:7`
  - *...and 5 more occurrences.*

---

### 3.13 Service: `calc-epm`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `none` | DB: `TypeORM, PostgreSQL (pg), BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 5 endpoints | **Unique Routes:** 5
- **Ground Truth in Code:** 4 endpoints.
- **Matched:** 4 | **Missing (FN):** 0 | **Phantom (FP):** 1

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY runEpmQuery` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.calc-epm`, `BigQuery.defaults`, `PostgreSQL.archive`, `PostgreSQL.defaults`, `PostgreSQL.public`
- **Tables in CE:** 1 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.14 Service: `cf-gateway`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), Redis` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 48 endpoints | **Unique Routes:** 48
- **Ground Truth in Code:** 41 endpoints.
- **Matched:** 41 | **Missing (FN):** 0 | **Phantom (FP):** 7

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/domain_id` (Controller: ``)
- `QUERY /api/domain_name` (Controller: ``)
- `QUERY /api/getCustomHostnames` (Controller: ``)
- `QUERY /api/getKeys` (Controller: ``)
- `QUERY /api/getValue` (Controller: ``)
- `QUERY /api/ruleset_id` (Controller: ``)
- `QUERY /api/tarif_name` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `Redis`, `PostgreSQL.defaults`
- **Tables in CE:** 2 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 5 calls.
  - `cf-gateway` — (Direct) -> `base_url_notifier` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-gateway` — (Direct) -> `bq-routes-calc-api` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-gateway` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-gateway` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-gateway` — (Direct) -> `notifier` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 18
  - **Primitive Obsession (Unmodeled string contract field):** `type: string;` in `src/interfaces/requests/cloudflare-worker-service.interface.ts:30`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/interfaces/requests/cloudflare-worker-service.interface.ts:88`
  - **Primitive Obsession (Unmodeled string contract field):** `type: string;` in `src/interfaces/responses/all-domains-res.interface.ts:19`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/interfaces/responses/all-domains-res.interface.ts:21`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/interfaces/responses/all-domains-res.interface.ts:34`
  - *...and 13 more occurrences.*

---

### 3.15 Service: `cf-landings`

- **Language / Framework:** typescript | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `Worker`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** None
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 4 calls.
  - `cf-landings` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-landings` — (Direct) -> `bundles.at-systems.pro` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-landings` — (Direct) -> `ipinfo.io` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-landings` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 19
  - **Dynamic URL Host Interpolation:** ``https://${hub_domain}/event/5?impression_id=${clickid}`` in `features/head/head3.js:43`
  - **Dynamic URL Host Interpolation:** ``https://${hub_domain}/event/1?impression_id=${clickid}`` in `features/head/main1.js:80`
  - **Dynamic URL Host Interpolation:** ``https://${mirrorNumber ? mirrorNumber + '.' : ''}${domain}${url.pathname}${url.search}`` in `features/head/redirect.js:158`
  - **Dynamic URL Host Interpolation:** ``https://${hub_domain}/event/5?impression_id=${clickid}`` in `features/head/userEvents.js:67`
  - **Dynamic URL Host Interpolation:** ``https://${hub_domain}/conversion/v1?impression_id=${newClickId}&domain=${domain}`` in `features/push/push.js:24`
  - *...and 14 more occurrences.*

---

### 3.16 Service: `cf-worker`

- **Language / Framework:** typescript | Frameworks: None detected
- **Libraries:** HTTP: `axios` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 5 calls.
  - `cf-worker` — (Direct) -> `bundles.at-systems.biz` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-worker` — (Direct) -> `htmlcompressor.com` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-worker` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-worker` — (Direct) -> `user-data-receiver.at-systems.co` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-worker` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 590
  - **Dynamic URL Host Interpolation:** ``https://${domain}/l/${partner}/sdk.js?z=${z}`` in `workers/tracker-worker/public_src/gulp_includes/partners/RA/partner.js:8`
  - **Dynamic URL Host Interpolation:** ``https://${domain}/l/${partner}/sdk.js?z=${z}`` in `workers/tracker-worker/public_src/gulp_includes/partners/RA/partner.js:16`
  - **Dynamic URL Host Interpolation:** ``https://${e}`` in `workers/tracker-worker/public_src/l/PA/nocache/micro.tag.min.js:1`
  - **Dynamic URL Host Interpolation:** ``https://${r}/${o()}`` in `workers/tracker-worker/public_src/l/PA/nocache/nn-sw-check-permissions-777.js:34`
  - **Dynamic URL Host Interpolation:** ``https://${r}/${o()}`` in `workers/tracker-worker/public_src/l/PA/nocache/sw.perm.check-AT-574200.nano.min.js:18`
  - *...and 585 more occurrences.*

---

### 3.17 Service: `cf-worker-rule-tree`

- **Language / Framework:** typescript | Frameworks: None detected
- **Libraries:** HTTP: `axios` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `Worker`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** None
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 4 calls.
  - `cf-worker-rule-tree` — (Direct) -> `feed.push.house` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-worker-rule-tree` — (Direct) -> `in.eu.adopexchange.com` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-worker-rule-tree` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `cf-worker-rule-tree` — (Direct) -> `user-data-receiver.at-systems.co` (ExternalService) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 17
  - **Dynamic URL Host Interpolation:** ``https://${e}`` in `workers/public/l/PA/nocache/micro.tag.min.js:1`
  - **Dynamic URL Host Interpolation:** ``https://${r}/${o()}`` in `workers/public/l/PA/nocache/nn-sw-check-permissions-777.js:34`
  - **Dynamic URL Host Interpolation:** ``https://${r}/${o()}`` in `workers/public/l/PA/nocache/sw.perm.check-AT-574200.nano.min.js:18`
  - **Dynamic URL Host Interpolation:** ``https://${n}/${o()}`` in `workers/public/l/PA/nocache/sw.perm.check-AT-574201.nano.min.js:19`
  - **Dynamic URL Host Interpolation:** ``https://${r}/${o()}`` in `workers/public/l/PA/nocache/sw.perm.check.min.js:18`
  - *...and 12 more occurrences.*

---

### 3.18 Service: `cf-worker-service-bindings`

- **Language / Framework:** typescript | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `Worker`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** None
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.19 Service: `cf-worker-ts`

- **Language / Framework:** unknown | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `unknown`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** None
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.20 Service: `cpm-rates-service`

- **Language / Framework:** go | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `ClickHouse.cpm-rates`
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.21 Service: `cpm-streaming-aggregator`

- **Language / Framework:** go | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `BigQuery (Go)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 10 endpoints | **Unique Routes:** 10
- **Ground Truth in Code:** 10 endpoints.
- **Matched:** 10 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.cpm-streaming-aggregator`, `ClickHouse.cpm-streaming-aggregator`
- **Tables in CE:** 4 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 1 calls.
  - `cpm-streaming-aggregator` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]

---

### 3.22 Service: `domain`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg)` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 43 endpoints | **Unique Routes:** 43
- **Ground Truth in Code:** 43 endpoints.
- **Matched:** 40 | **Missing (FN):** 3 | **Phantom (FP):** 3

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `POST /` (src/routes/domains-rotation.route.ts:23)
- `GET /` (src/routes/domains-rotation.route.ts:27)
- `POST /` (src/routes/domains.route.ts:36)

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `GET /api/v2/rotation` (Controller: ``)
- `POST /api/domains` (Controller: ``)
- `POST /api/v2/rotation` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.domain-log`, `PostgreSQL.defaults`, `PostgreSQL.domains_rotation`, `PostgreSQL.id`, `PostgreSQL.networks`
- **Tables in CE:** 12 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 6 calls.
  - `domain` — (Direct) -> `cf-gateway` (ExternalService) [Method: `*`, Path: `*`]
  - `domain` — (Direct) -> `notifier` (ExternalService) [Method: `*`, Path: `*`]
  - `domain` — (Direct) -> `safebrowsing.googleapis.com` (ExternalService) [Method: `*`, Path: `*`]
  - `domain` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `domain` — (Direct) -> `cf-gateway` (Project, Service) [Method: `*`, Path: `*`]
  - `domain` — (Direct) -> `notifier` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 3
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/cloudflare.ts:24`
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/consts.ts:35`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/consts.ts:36`

---

### 3.23 Service: `domain-checker`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 8 endpoints | **Unique Routes:** 8
- **Ground Truth in Code:** 6 endpoints.
- **Matched:** 6 | **Missing (FN):** 0 | **Phantom (FP):** 2

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/deleteProxy` (Controller: ``)
- `QUERY /api/getProxy` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`
- **Tables in CE:** 2 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 3 calls.
  - `domain-checker` — (Direct) -> `domains.at-systems.biz` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-checker` — (Direct) -> `transparencyreport.google.com` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-checker` — (Direct) -> `domain` (Project, Service) [Method: `*`, Path: `*`]

---

### 3.24 Service: `domain-log`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 5 endpoints | **Unique Routes:** 5
- **Ground Truth in Code:** 4 endpoints.
- **Matched:** 4 | **Missing (FN):** 0 | **Phantom (FP):** 1

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/getDomainLog` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.domain-log`, `PostgreSQL.public`
- **Tables in CE:** 2 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 3
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${this.serviceName}-${serviceName}.${this.deployment}`` in `src/configs/cluster.config.ts:33`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${this.domain}`` in `src/configs/cluster.config.ts:34`
  - **Primitive Obsession (Unmodeled string contract field):** `event: string;` in `src/interfaces/responces/domain-log-entry.interface.ts:3`

---

### 3.25 Service: `domain-settler`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 11 endpoints | **Unique Routes:** 11
- **Ground Truth in Code:** 8 endpoints.
- **Matched:** 8 | **Missing (FN):** 0 | **Phantom (FP):** 3

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/delete` (Controller: ``)
- `QUERY /api/get` (Controller: ``)
- `QUERY /api/getArchive` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`
- **Tables in CE:** 5 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 4 calls.
  - `domain-settler` — (Direct) -> `base_url_notifier` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-settler` — (Direct) -> `domains.at-systems.biz` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-settler` — (Direct) -> `domain` (Project, Service) [Method: `*`, Path: `*`]
  - `domain-settler` — (Direct) -> `notifier` (Project, Service) [Method: `*`, Path: `*`]

---

### 3.26 Service: `domain-v2`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 54 endpoints | **Unique Routes:** 54
- **Ground Truth in Code:** 48 endpoints.
- **Matched:** 48 | **Missing (FN):** 0 | **Phantom (FP):** 7

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/changeAutoRenewStatus` (Controller: ``)
- `QUERY /api/get` (Controller: ``)
- `QUERY /api/getAll` (Controller: ``)
- `QUERY /api/getAllUsageDomains` (Controller: ``)
- `QUERY /api/getChangeLinkCampaignState` (Controller: ``)
- `QUERY /api/getJournalEntries` (Controller: ``)
- `QUERY /api/getPaginatedDomains` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`, `PostgreSQL.domains_rotation`, `PostgreSQL.id`, `PostgreSQL.networks`, `PostgreSQL.public`
- **Tables in CE:** 20 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 12 calls.
  - `domain-v2` — (Direct) -> `bundles` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `cf-gateway` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `domain-settler.at-systems.biz` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `id-helper-nest` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `notifier` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `script.google.com` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `support-bot` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `cf-gateway` (Project, Service) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `domain-settler` (Project, Service) [Method: `*`, Path: `*`]
  - `domain-v2` — (Direct) -> `notifier` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 5
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/interfaces/requests/change-domain.request.ts:2`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/interfaces/requests/subscriber.request.ts:3`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/interfaces/responses/all-domains.responses.ts:27`
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/shared/constants/constants.ts:68`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/shared/constants/constants.ts:69`

---

### 3.27 Service: `edge-decision-engine`

- **Language / Framework:** typescript | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `Worker`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.public`
- **Tables in CE:** 12 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.28 Service: `fe`

- **Language / Framework:** typescript | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `FrontendApp`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.landers`, `PostgreSQL.prefetch`, `PostgreSQL.public`, `PostgreSQL.sources`
- **Tables in CE:** 4 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 48 calls.
  - `fe` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `ad-hub` (ExternalService) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `bff.at-systems.dev` (ExternalService) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `action-scheduler` (Project, Worker) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `ad-hub` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `analytics` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `approval` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `bff` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `bq-routes-calculation` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `bundle-cpm-controller` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `bundle-priority` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `bundle-scheduler` (Project, Worker) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `bundle-update-adapter` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `calc-epm` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `cf-gateway` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `domain-checker` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `domain-log` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `domain-settler` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `domain-v2` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `domain` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `hub` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `journal` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `kv-v2` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `kv` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `landers` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `landing` (Project, App) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `netstatend` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `networks` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `notifier` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `nrt` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `partnerstat` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `postback-partner` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `prefetch` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `rates` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `redis-route` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `rule-configurator` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `rule-tree-updater` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `rule-tree` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `smart-cpa` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `sources` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `splits` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `stage-worker` (Project, Worker) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `tracker` (Project, Service) [Method: `*`, Path: `*`]
  - `fe` — (Direct) -> `traffic-types` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 21
  - **Primitive Obsession (Unmodeled string contract field):** `event: string;` in `src/app/common/models/event.model.ts:2`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/app/common/models/event.model.ts:5`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/app/common/models/okr.model.ts:6`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/app/common/models/okr.model.ts:33`
  - **Primitive Obsession (Unmodeled string contract field):** `type: string;` in `src/app/pages/bundles-report/modules/bundle-chart/components/bundle-events-chart/utils/bundle-events-chart.utils.ts:7`
  - *...and 16 more occurrences.*

---

### 3.29 Service: `hub`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), Redis, BigQuery` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 17 endpoints | **Unique Routes:** 17
- **Ground Truth in Code:** 17 endpoints.
- **Matched:** 17 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.metadata.schema`, `Redis`, `PostgreSQL.defaults`, `PostgreSQL.metadata.schema`
- **Tables in CE:** 3 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 2 calls.
  - `hub` — (Direct) -> `dezoty.com` (ExternalService) [Method: `*`, Path: `*`]
  - `hub` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 5
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `consts.ts:37`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `consts.ts:38`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.at-systems.co`` in `consts.ts:40`
  - **Dynamic SQL Template Interpolation:** `FROM ${dataset}` in `src/BigQuery.ts:314`
  - **Dynamic SQL Template Interpolation:** `FROM ${dataset}` in `src/BigQuery.ts:362`

---

### 3.30 Service: `journal`

- **Language / Framework:** typescript | Frameworks: NestJS, Fastify
- **Libraries:** HTTP: `none` | DB: `TypeORM, PostgreSQL (pg), BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 8 endpoints | **Unique Routes:** 8
- **Ground Truth in Code:** 6 endpoints.
- **Matched:** 6 | **Missing (FN):** 0 | **Phantom (FP):** 3

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/deleteJournalEvents` (Controller: ``)
- `QUERY /api/getJournalEventRows` (Controller: ``)
- `QUERY /api/getJournalEvents` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.defaults`, `PostgreSQL.defaults`
- **Tables in CE:** 1 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.31 Service: `kv`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg)` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 10 endpoints | **Unique Routes:** 10
- **Ground Truth in Code:** 10 endpoints.
- **Matched:** 8 | **Missing (FN):** 2 | **Phantom (FP):** 2

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `GET /` (src/routes/namespace.route.ts:5)
- `POST /` (src/routes/namespace.route.ts:6)

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `GET /api/namespaces` (Controller: ``)
- `POST /api/namespaces` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.kv`
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 2 calls.
  - `kv` — (Direct) -> `bq-routes-calc-api` (ExternalService) [Method: `*`, Path: `*`]
  - `kv` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 5
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/consts.ts:28`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/consts.ts:29`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.dev-at-systems.xyz`` in `src/consts.ts:31`
  - **Primitive Obsession (Unmodeled string contract field):** `action: string;` in `src/handlers/task-handler.ts:16`
  - **Primitive Obsession (Unmodeled string contract field):** `type: string;` in `src/models/rabbit/RabbitQueueElement.ts:3`

---

### 3.32 Service: `kv-v2`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 3 endpoints | **Unique Routes:** 3
- **Ground Truth in Code:** 3 endpoints.
- **Matched:** 3 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** None
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 2 calls.
  - `kv-v2` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `kv-v2` — (Direct) -> `KV_BASE_URL` (ExternalService) [Method: `*`, Path: `*`]

---

### 3.33 Service: `landers`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 25 endpoints | **Unique Routes:** 25
- **Ground Truth in Code:** 20 endpoints.
- **Matched:** 20 | **Missing (FN):** 0 | **Phantom (FP):** 5

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/getFullLandsList` (Controller: ``)
- `QUERY /api/getLandBuildsList` (Controller: ``)
- `QUERY /api/getLandImages` (Controller: ``)
- `QUERY /api/getLandSkinsList` (Controller: ``)
- `QUERY /api/getLandsList` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`, `PostgreSQL.landers`, `PostgreSQL.public`
- **Tables in CE:** 6 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.34 Service: `landing`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `FrontendApp`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 1 endpoints | **Unique Routes:** 1
- **Ground Truth in Code:** 2 endpoints.
- **Matched:** 1 | **Missing (FN):** 1 | **Phantom (FP):** 0

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `GET /api/{*splat}` (src/server.ts:36)

#### Layer 4: Databases & Tables
- **Databases:** None
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.35 Service: `netstatend`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), Redis` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 7 endpoints | **Unique Routes:** 7
- **Ground Truth in Code:** 7 endpoints.
- **Matched:** 7 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 5 calls.
  - `netstatend` — (Direct) -> `api.telegram.org` (ExternalService) [Method: `*`, Path: `*`]
  - `netstatend` — (Direct) -> `bundles` (ExternalService) [Method: `*`, Path: `*`]
  - `netstatend` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `netstatend` — (Direct) -> `bq-routes-calculation` (Project, Service) [Method: `*`, Path: `*`]
  - `netstatend` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 3
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/consts.ts:48`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/consts.ts:49`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.at-systems.co`` in `src/consts.ts:51`

---

### 3.36 Service: `networks`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 23 endpoints | **Unique Routes:** 23
- **Ground Truth in Code:** 21 endpoints.
- **Matched:** 21 | **Missing (FN):** 0 | **Phantom (FP):** 2

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/findAll` (Controller: ``)
- `QUERY /api/getStatistics` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.id`, `PostgreSQL.networks`, `PostgreSQL.pg_catalog`
- **Tables in CE:** 13 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 3 calls.
  - `networks` — (Direct) -> `redisroutes` (ExternalService) [Method: `*`, Path: `*`]
  - `networks` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `networks` — (Direct) -> `redis-route` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 2
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${this.serviceName}-${serviceName}.${this.deployment}`` in `src/configs/cluster.config.ts:33`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${this.domain}`` in `src/configs/cluster.config.ts:34`

---

### 3.37 Service: `notifier`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 17 endpoints | **Unique Routes:** 17
- **Ground Truth in Code:** 12 endpoints.
- **Matched:** 12 | **Missing (FN):** 0 | **Phantom (FP):** 5

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `GET /api` (Controller: ``)
- `QUERY /api/getMessageThreads` (Controller: ``)
- `QUERY /api/getTgAlerts` (Controller: ``)
- `QUERY /api/getTgChats` (Controller: ``)
- `QUERY /api/getTgUsers` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.bff`, `PostgreSQL.notifier`, `PostgreSQL.public`
- **Tables in CE:** 4 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 2 calls.
  - `notifier` — (Direct) -> `cf-gateway.at-systems.biz` (ExternalService) [Method: `*`, Path: `*`]
  - `notifier` — (Direct) -> `cf-gateway` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 1
  - **Dynamic SQL Template Interpolation:** `from ${url}`` in `src/modules/system/system.service.ts:34`

---

### 3.38 Service: `nrt`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 8 endpoints | **Unique Routes:** 8
- **Ground Truth in Code:** 6 endpoints.
- **Matched:** 6 | **Missing (FN):** 0 | **Phantom (FP):** 2

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY getMedianeRates` (Controller: ``)
- `QUERY recalculateMedianeRates` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.any`, `PostgreSQL.calc_rate`
- **Tables in CE:** 2 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 1 calls.
  - `nrt` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]

---

### 3.39 Service: `partners-fe`

- **Language / Framework:** typescript | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `FrontendApp`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** None
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 2 calls.
  - `partners-fe` — (Direct) -> `bff.at-systems.dev` (ExternalService) [Method: `*`, Path: `*`]
  - `partners-fe` — (Direct) -> `bff` (Project, Service) [Method: `*`, Path: `*`]

---

### 3.40 Service: `partnerstat`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), Redis, BigQuery` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 5 endpoints | **Unique Routes:** 5
- **Ground Truth in Code:** 5 endpoints.
- **Matched:** 4 | **Missing (FN):** 1 | **Phantom (FP):** 1

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `GET /` (src/routes/upload-tb.route.ts:6)

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `GET /upload-tb` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.defaults`, `Redis`, `PostgreSQL.defaults`, `PostgreSQL.partnerstat`
- **Tables in CE:** 5 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 20 calls.
  - `partnerstat` — (Direct) -> `api.telegram.org` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `bq-routes-calc` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `bundles` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `popunder.net` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `rates` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `API_BASE_URL_PARTNER_EXOCLICK` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `api.publisher.house` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `api.trafficstars.com` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `api3.adsterratools.com` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `API_BASE_URL_PARTNER_PROPUSH` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `POSTBACK_URL` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `API_BASE_URL_PARTNER_OFFERGATE` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `partners.onclicka.com` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `API_BASE_URL_PARTNER_PUSHADVERT` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `API_BASE_URL_PARTNER_CLICKADU` (ExternalService) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `bq-routes-calculation` (Project, Service) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `hub` (Project, Service) [Method: `*`, Path: `*`]
  - `partnerstat` — (Direct) -> `rates` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 4
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/consts.ts:23`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/consts.ts:24`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.at-systems.co`` in `src/consts.ts:26`
  - **Primitive Obsession (Unmodeled string contract field):** `type: string;` in `src/rabbit/queue.element.ts:9`

---

### 3.41 Service: `postback-partner`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), Redis, BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 22 endpoints | **Unique Routes:** 22
- **Ground Truth in Code:** 16 endpoints.
- **Matched:** 16 | **Missing (FN):** 0 | **Phantom (FP):** 6

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY checkPostback` (Controller: ``)
- `QUERY getCacheStorages` (Controller: ``)
- `QUERY getPostbackPlan` (Controller: ``)
- `QUERY refreshBundlesCache` (Controller: ``)
- `QUERY refreshCache` (Controller: ``)
- `QUERY removeFromCache` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.any`, `Redis`, `PostgreSQL.bundles`, `PostgreSQL.id`
- **Tables in CE:** 2 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 3 calls.
  - `postback-partner` — (Direct) -> `sources` (ExternalService) [Method: `*`, Path: `*`]
  - `postback-partner` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]
  - `postback-partner` — (Direct) -> `sources` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 1
  - **Primitive Obsession (Unmodeled string contract field):** `type: string;` in `src/interfaces/conversions/base-conversion.ts:11`

---

### 3.42 Service: `prefetch`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg)` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 3 endpoints | **Unique Routes:** 3
- **Ground Truth in Code:** 3 endpoints.
- **Matched:** 3 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.bundles`, `PostgreSQL.landers`, `PostgreSQL.prefetch`
- **Tables in CE:** 1 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 6 calls.
  - `prefetch` — (Direct) -> `bq-routes-calc` (ExternalService) [Method: `*`, Path: `*`]
  - `prefetch` — (Direct) -> `rule-configurator.at-systems.biz` (ExternalService) [Method: `*`, Path: `*`]
  - `prefetch` — (Direct) -> `rule-tree-updater` (ExternalService) [Method: `*`, Path: `*`]
  - `prefetch` — (Direct) -> `bq-routes-calculation` (Project, Service) [Method: `*`, Path: `*`]
  - `prefetch` — (Direct) -> `rule-configurator` (Project, Service) [Method: `*`, Path: `*`]
  - `prefetch` — (Direct) -> `rule-tree-updater` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 4
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.dev-at-systems.xyz`` in `src/consts/consts.ts:27`
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/consts/consts.ts:31`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/consts/consts.ts:32`
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/models/full-bundles.ts:15`

---

### 3.43 Service: `rates`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 8 endpoints | **Unique Routes:** 8
- **Ground Truth in Code:** 8 endpoints.
- **Matched:** 8 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.rates`, `PostgreSQL.archive`, `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.redis-route`
- **Tables in CE:** 37 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 4 calls.
  - `rates` — (Direct) -> `bundles` (ExternalService) [Method: `*`, Path: `*`]
  - `rates` — (Direct) -> `rule-tree-updater` (ExternalService) [Method: `*`, Path: `*`]
  - `rates` — (Direct) -> `bundle` (Project, Service) [Method: `*`, Path: `*`]
  - `rates` — (Direct) -> `rule-tree-updater` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 2
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/constants.ts:29`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.dev-at-systems.xyz`` in `src/constants.ts:31`

---

### 3.44 Service: `redis-route`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `none` | DB: `PostgreSQL (pg), Redis` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 77 endpoints | **Unique Routes:** 77
- **Ground Truth in Code:** 77 endpoints.
- **Matched:** 77 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `Redis`, `PostgreSQL.defaults`, `PostgreSQL.id`, `PostgreSQL.redis-route`
- **Tables in CE:** 2 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 3
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.dev-at-systems.xyz`` in `src/consts.ts:24`
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/consts.ts:28`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/consts.ts:29`

---

### 3.45 Service: `rule-configurator`

- **Language / Framework:** typescript | Frameworks: NestJS, Fastify
- **Libraries:** HTTP: `none` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 12 endpoints | **Unique Routes:** 12
- **Ground Truth in Code:** 10 endpoints.
- **Matched:** 10 | **Missing (FN):** 0 | **Phantom (FP):** 2

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/getRules` (Controller: ``)
- `QUERY /api/getRulesHistory` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.bundles`, `PostgreSQL.defaults`
- **Tables in CE:** 5 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.46 Service: `rule-tree`

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
- **Databases:** `Redis`, `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.landers`, `PostgreSQL.public`, `PostgreSQL.split`, `PostgreSQL.tree_rules`
- **Tables in CE:** 20 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.47 Service: `rule-tree-updater`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 54 endpoints | **Unique Routes:** 54
- **Ground Truth in Code:** 32 endpoints.
- **Matched:** 29 | **Missing (FN):** 3 | **Phantom (FP):** 25

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `GET /system/sync-land-backends` (src/modules/system/system-sync.controller.ts:62)
- `DELETE /system/land-backends` (src/modules/system/system.controller.ts:70)
- `DELETE /system/all-modules` (src/modules/system/system.controller.ts:142)

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/deleteBbh` (Controller: ``)
- `QUERY /api/deleteBundleRules` (Controller: ``)
- `QUERY /api/deletePopunderMaps` (Controller: ``)
- `QUERY /api/deletePopunders` (Controller: ``)
- `QUERY /api/deleteRateOffsets` (Controller: ``)
- `QUERY /api/deleteRates` (Controller: ``)
- `QUERY /api/deleteSkinRules` (Controller: ``)
- `QUERY /api/deleteSplitBackends` (Controller: ``)
- `QUERY /api/deleteSplitBundles` (Controller: ``)
- `QUERY /api/deleteSplitRegulars` (Controller: ``)
- *...and 15 more.*

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.landers`, `PostgreSQL.split`, `PostgreSQL.tree_rules`
- **Tables in CE:** 11 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 2 calls.
  - `rule-tree-updater` — (Direct) -> `rule-tree` (ExternalService) [Method: `*`, Path: `*`]
  - `rule-tree-updater` — (Direct) -> `rule-tree` (Project, Service) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 1
  - **Dynamic SQL Template Interpolation:** `from ${EModuleNames.SplitRegularsTogether}`` in `src/swagger/dto/split-regular.response.ts:28`

---

### 3.48 Service: `smart-cpa`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg), Redis, BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 15 endpoints | **Unique Routes:** 15
- **Ground Truth in Code:** 12 endpoints.
- **Matched:** 12 | **Missing (FN):** 0 | **Phantom (FP):** 3

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `GET /` (Controller: ``)
- `QUERY get` (Controller: ``)
- `QUERY removeTargetStateCpaCampaigns` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.id`
- **Tables in CE:** 5 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 1 calls.
  - `smart-cpa` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 1
  - **Primitive Obsession (Unmodeled string contract field):** `status: string;` in `src/interfaces/id-gateway/id-gateway.interfaces.ts:29`

---

### 3.49 Service: `sources`

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
- **Databases:** `PostgreSQL.bundles`, `PostgreSQL.defaults`, `PostgreSQL.sources`
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

### 3.50 Service: `splits`

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

### 3.51 Service: `sql`

- **Language / Framework:** unknown | Frameworks: None detected
- **Libraries:** HTTP: `none` | DB: `none` | Queue: `none`
- **Primary Role (CE):** `unknown`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 0 endpoints | **Unique Routes:** 0
- **Ground Truth in Code:** 0 endpoints.
- **Matched:** 0 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** None
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

---

### 3.52 Service: `stage-worker`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), Redis` | Queue: `RabbitMQ (amqp), BullMQ`
- **Primary Role (CE):** `Worker`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 45 endpoints | **Unique Routes:** 45
- **Ground Truth in Code:** 44 endpoints.
- **Matched:** 44 | **Missing (FN):** 0 | **Phantom (FP):** 1

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `GET /api/stages/integration_in_progress/networks/:network` (Controller: ``)

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

### 3.53 Service: `tb-map`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 12 endpoints | **Unique Routes:** 12
- **Ground Truth in Code:** 12 endpoints.
- **Matched:** 12 | **Missing (FN):** 0 | **Phantom (FP):** 0

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

### 3.54 Service: `tracker`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), Redis, BigQuery` | Queue: `RabbitMQ (amqp)`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 3 endpoints | **Unique Routes:** 3
- **Ground Truth in Code:** 3 endpoints.
- **Matched:** 3 | **Missing (FN):** 0 | **Phantom (FP):** 0

#### Layer 4: Databases & Tables
- **Databases:** `BigQuery.tracker`, `Redis`, `PostgreSQL.tracker`
- **Tables in CE:** 0 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 1 calls.
  - `tracker` — (Direct) -> `unknown-service` (ExternalService) [Method: `*`, Path: `*`]

#### Lower-Level Ontology: Primitive Obsession & String Hardcodes
- **Detected occurrences:** 4
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.at-systems.co`` in `src/consts.ts:36`
  - **Dynamic URL Host Interpolation:** ``http://${serviceName}.${CLUSTER_SERVICE_NAME}-${serviceName}.${CLUSTER_DEPLOYMENT}`` in `src/consts.ts:40`
  - **Dynamic URL Host Interpolation:** ``https://${serviceName}.${CLUSTER_DOMAIN}`` in `src/consts.ts:41`
  - **Primitive Obsession (Unmodeled string contract field):** `type: string;` in `src/models/RabbitQueueElement.ts:3`

---

### 3.55 Service: `traffic-types`

- **Language / Framework:** typescript | Frameworks: NestJS
- **Libraries:** HTTP: `axios` | DB: `TypeORM, PostgreSQL (pg)` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 20 endpoints | **Unique Routes:** 20
- **Ground Truth in Code:** 18 endpoints.
- **Matched:** 18 | **Missing (FN):** 0 | **Phantom (FP):** 3

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `QUERY /api/getAll` (Controller: ``)
- `QUERY /api/getAllUnpaged` (Controller: ``)
- `QUERY /api/getByName` (Controller: ``)

#### Layer 4: Databases & Tables
- **Databases:** `PostgreSQL.defaults`, `PostgreSQL.landers`
- **Tables in CE:** 5 tables recorded.

#### Layer 5: Egress Service Dependencies
- **CE Graph Service Calls:** 0 calls.
  - *No outgoing service calls recorded in graph.*

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

