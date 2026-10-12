# CodeExplorer Monorepo Multi-Layer Ontology Audit Report

**Audit Timestamp:** 2026-10-12T10:36:38.459Z
**Workspace:** `C:\Work\ATS\src\services`
**Total Audited Services:** 15

> [!IMPORTANT]
> This report provides an in-depth, multi-layer verification of CodeExplorer's extracted ontology knowledge graph vs. the ground-truth codebase in the ATS microservice monorepo. It details discrepancies across all 5 ontology layers, identifies recurring primitive obsession and string hardcodes, and catalogs specific parser blind spots.

## 1. Executive Summary & Scorecard

| Service | Language | Role | CE Total Endpoints | CE Unique Routes | Duplicate Pairs | Code Endpoints | Matched | Missing (FN) | Phantom (FP) | Phantom Tables | Egress Calls (CE) |
|---|---|---|---|---|---|---|---|---|---|---|---|
| **bundle-update-adapter** | typescript | Service | 14 | 14 | 0 | 11 | 11 | 0 | 4 | 0 | 1 |
| **cf-landings** | typescript | Worker | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4 |
| **cf-worker-rule-tree** | typescript | Worker | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 4 |
| **cf-worker-service-bindings** | typescript | Worker | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **domain-log** | typescript | Service | 5 | 5 | 0 | 4 | 4 | 0 | 1 | 0 | 0 |
| **edge-decision-engine** | typescript | Worker | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| **kv-v2** | typescript | Service | 3 | 3 | 0 | 3 | 3 | 0 | 0 | 0 | 2 |
| **landers** | typescript | Service | 25 | 25 | 0 | 20 | 20 | 0 | 5 | 0 | 0 |
| **netstatend** | typescript | Service | 7 | 7 | 0 | 7 | 7 | 0 | 0 | 0 | 5 |
| **nrt** | typescript | Service | 8 | 8 | 0 | 6 | 6 | 0 | 2 | 0 | 1 |
| **postback-partner** | typescript | Service | 22 | 22 | 0 | 16 | 16 | 0 | 6 | 0 | 3 |
| **prefetch** | typescript | Service | 3 | 3 | 0 | 3 | 3 | 0 | 0 | 0 | 6 |
| **rates** | typescript | Service | 8 | 8 | 0 | 8 | 7 | 1 | 1 | 0 | 4 |
| **redis-route** | typescript | Service | 77 | 77 | 0 | 77 | 76 | 1 | 1 | 0 | 0 |
| **rule-configurator** | typescript | Service | 12 | 12 | 0 | 10 | 10 | 0 | 2 | 0 | 0 |

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

### 3.1 Service: `bundle-update-adapter`

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

### 3.2 Service: `cf-landings`

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

### 3.3 Service: `cf-worker-rule-tree`

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

### 3.4 Service: `cf-worker-service-bindings`

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

### 3.5 Service: `domain-log`

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

### 3.6 Service: `edge-decision-engine`

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

### 3.7 Service: `kv-v2`

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

### 3.8 Service: `landers`

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

### 3.9 Service: `netstatend`

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

### 3.10 Service: `nrt`

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

### 3.11 Service: `postback-partner`

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

### 3.12 Service: `prefetch`

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

### 3.13 Service: `rates`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `axios` | DB: `PostgreSQL (pg), BigQuery` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 8 endpoints | **Unique Routes:** 8
- **Ground Truth in Code:** 8 endpoints.
- **Matched:** 7 | **Missing (FN):** 1 | **Phantom (FP):** 1

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `GET /` (src/routers/ping.router.ts:6)

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `GET /ping` (Controller: ``)

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

### 3.14 Service: `redis-route`

- **Language / Framework:** typescript | Frameworks: Express
- **Libraries:** HTTP: `none` | DB: `PostgreSQL (pg), Redis` | Queue: `none`
- **Primary Role (CE):** `Service`

#### Layer 4: Ingress HTTP Endpoints
- **CE Total Recorded:** 77 endpoints | **Unique Routes:** 77
- **Ground Truth in Code:** 77 endpoints.
- **Matched:** 76 | **Missing (FN):** 1 | **Phantom (FP):** 1

##### Missing Endpoints (Found in Code, Missing in CE Graph):
- `PATCH /` (src/routers/networks.router.ts:7)

##### Phantom Endpoints (Present in CE Graph, Not Found in Code Controllers):
- `PATCH /networks` (Controller: ``)

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

### 3.15 Service: `rule-configurator`

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

