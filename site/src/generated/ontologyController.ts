// Auto-generated Ontology Page interactive controller for React SPA
// @ts-nocheck
export function initOntologyController(): () => void {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return () => {};
  }

      var SCHEMA = {"schemaVersion":"2.0","layers":[{"layerId":5,"key":"L5_logical","name":"Logical Structure","title":"Layer 5: Logical Structure","description":"Business domains, bounded contexts, and strategic cross-context boundaries grouping modules, data stores, and domain types."},{"layerId":4,"key":"L4_semantic","name":"Semantic Architecture","title":"Layer 4: Semantic Structure","description":"Runtime workloads, API & broker contracts, databases, SQL queries, tables, and external or cloud dependencies."},{"layerId":3,"key":"L3_syntactic","name":"Syntactic UAST & Dataflow","title":"Layer 3: Syntax Structure","description":"Language-agnostic AST declarations, call graph, semantic parameters, sliced call arguments, literals, and catch blocks."},{"layerId":2,"key":"L2_boundaries","name":"Project & Package Boundaries","title":"Layer 2: Project Structure","description":"Compilation units, solution projects, external package dependencies, and executable entry points."},{"layerId":1,"key":"L1_physical","name":"Physical File System","title":"Layer 1: File Structure","description":"Workspace folders, source files, and repository Git metadata."}],"edgeCategories":[{"id":"control_flow","label":"Control & Call Flow","description":"Direct calls, endpoint triggers, entrypoints, and cross-service RPC/HTTP invocations."},{"id":"data_flow","label":"Data, SQL & Messaging","description":"Parameter-to-argument slicing, SQL table reads/writes, database access, and broker pub/sub."},{"id":"type_dependency","label":"Types, Packages & Cloud","description":"Inheritance, type usage, project/package dependencies, test coverage, and cloud bindings."},{"id":"structural","label":"Containment & Location","description":"Physical file declarations, folder hierarchy, and resource ownership."}],"commonProperties":[{"name":"id","sourceName":"Id","type":"string","description":"Globally unique deterministic URN identifier of the node."},{"name":"name","sourceName":"Name","type":"string","description":"Human-readable symbol, contract, or resource name."},{"name":"path","sourceName":"Path","type":"string","description":"Workspace-relative path of the container or declaration."},{"name":"project_id","sourceName":"ProjectId","type":"string","description":"URN of the enclosing compilation project, when applicable."}],"nodes":[{"kind":"Domain","pluralLabel":"Domains","layerId":5,"layerKey":"L5_logical","icon":"globe","order":1,"idScheme":"{workspaceId}:dom:{domainKey}","purpose":"Represents a high-level business domain (e.g. Core, Supporting, Generic, Shared) containing one or more bounded contexts in Level 5 (Logical Structure).","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The canonical domain name."},{"name":"display_name","sourceName":"DisplayName","type":"string","description":"The human-readable display name of the domain."},{"name":"domain_type","sourceName":"DomainType","type":"enum (DomainType)","description":"The DDD classification of the domain (Core, Supporting, Generic, Shared)."},{"name":"summary","sourceName":"Summary","type":"string","description":"A concise summary of the domain's business responsibility."},{"name":"path","sourceName":"Path","type":"string","description":"Primary path associated with the domain."}],"outEdges":[{"rel":"DEPENDS_ON","toKind":"Domain","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"CONTAINS","toKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"inEdges":[{"rel":"DEPENDS_ON","fromKind":"Domain","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."}],"sampleCypher":"MATCH (n:Domain)-[:DEPENDS_ON]->(t:Domain)\nRETURN n.name, n.display_name, n.domain_type, t.name\nLIMIT 25"},{"kind":"BoundedContext","pluralLabel":"Bounded Contexts","layerId":5,"layerKey":"L5_logical","icon":"layers","order":2,"idScheme":"{workspaceId}:bc:{contextKey}","purpose":"Represents a DDD Bounded Context in Level 5 (Logical Structure) that encapsulates semantic workloads, data stores, topics, and domain types.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The canonical bounded context name."},{"name":"display_name","sourceName":"DisplayName","type":"string","description":"The human-readable display name of the bounded context."},{"name":"domain_id","sourceName":"DomainId","type":"string","description":"The parent domain ID that owns this bounded context."},{"name":"summary","sourceName":"Summary","type":"string","description":"A concise summary of the bounded context's business responsibility."},{"name":"purity_percentage","sourceName":"PurityPercentage","type":"number","description":"Domain purity percentage (0-100)."},{"name":"path","sourceName":"Path","type":"string","description":"Primary path associated with the bounded context."}],"outEdges":[{"rel":"DEPENDS_ON","toKind":"BoundedContext","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"CONTAINS","toKind":"Database","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"CONTAINS","toKind":"Module","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"CONTAINS","toKind":"Topic","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"DEFINES","toKind":"Type","category":"structural","description":"Indicates that an entity defines a particular property or configuration."}],"inEdges":[{"rel":"DEPENDS_ON","fromKind":"BoundedContext","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"CONTAINS","fromKind":"Domain","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (n:BoundedContext)-[:DEPENDS_ON]->(t:BoundedContext)\nRETURN n.name, n.display_name, n.domain_id, t.name\nLIMIT 25"},{"kind":"Module","pluralLabel":"Software Modules","layerId":4,"layerKey":"L4_semantic","icon":"package","order":0,"idScheme":"{workspaceId}:{kindPrefix}:{moduleName}","purpose":"Abstract base software module encompassing executable workloads, shared libraries, and automated test suites.","isAbstract":true,"subtypes":["Workload","Library","TestSuite"],"properties":[{"name":"name","sourceName":"Name","type":"string","description":"The module name."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the module directory relative to the workspace."},{"name":"project_type","sourceName":"ProjectType","type":"enum (ProjectLanguage)","description":"The project type or language."},{"name":"role","sourceName":"Role","type":"string","description":"The primary semantic role of the module."},{"name":"framework","sourceName":"Framework","type":"string","description":"The detected application, service, or test framework."}],"outEdges":[{"rel":"DEPENDS_ON","toKind":"Library","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"DEPENDS_ON","toKind":"Package","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"IMPLEMENTED_BY","toKind":"Project","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it."}],"inEdges":[{"rel":"TESTS","fromKind":"TestSuite","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies."},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (n:Module)-[:DEPENDS_ON]->(t:Library)\nRETURN n.name, n.project_type, n.role, t.name\nLIMIT 25"},{"kind":"Workload","pluralLabel":"Runtime Workloads","layerId":4,"layerKey":"L4_semantic","icon":"server","order":1,"idScheme":"{workspaceId}:{workloadPrefix}:{workloadName}","purpose":"Abstract executable runtime workload encompassing backend services, client applications, background workers, and CLI tools.","isAbstract":true,"extends":"Module","ancestors":["Module"],"subtypes":["App","Service","Worker","CliTool"],"properties":[{"name":"name","sourceName":"Name","type":"string","description":"The module name.","inheritedFrom":"Module"},{"name":"path","sourceName":"Path","type":"string","description":"The path of the module directory relative to the workspace.","inheritedFrom":"Module"},{"name":"project_type","sourceName":"ProjectType","type":"enum (ProjectLanguage)","description":"The project type or language.","inheritedFrom":"Module"},{"name":"role","sourceName":"Role","type":"string","description":"The primary semantic role of the module.","inheritedFrom":"Module"},{"name":"framework","sourceName":"Framework","type":"string","description":"The detected application, service, or test framework.","inheritedFrom":"Module"}],"outEdges":[{"rel":"SERVICE_CALL","toKind":"ExternalService","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{"rel":"SERVICE_CALL","toKind":"Service","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{"rel":"PUBLISHES_TO","toKind":"Topic","category":"data_flow","description":"Links a service or function to a message queue or topic it publishes messages or events to."},{"rel":"USES_DB","toKind":"Database","category":"data_flow","description":"Links a project to a database catalog or instance it utilizes."},{"rel":"HOSTED_IN","toKind":"Cloud","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."}],"inEdges":[],"inheritedOutEdges":[{"rel":"DEPENDS_ON","toKind":"Library","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"DEPENDS_ON","toKind":"Package","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"IMPLEMENTED_BY","toKind":"Project","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it.","inheritedFrom":"Module"}],"inheritedInEdges":[{"rel":"TESTS","fromKind":"TestSuite","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.","inheritedFrom":"Module"},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements.","inheritedFrom":"Module"}],"sampleCypher":"MATCH (n:Workload)-[:SERVICE_CALL]->(t:ExternalService)\nRETURN n.name, n.project_type, n.role, t.name\nLIMIT 25"},{"kind":"App","pluralLabel":"Applications","layerId":4,"layerKey":"L4_semantic","icon":"browser","order":2,"idScheme":"{workspaceId}:app:{appName}","purpose":"Represents a single-page application, web frontend, mobile client, or desktop app (e.g. React, Next.js, Vue, Angular, Electron).","extends":"Workload","ancestors":["Workload","Module"],"properties":[{"name":"name","sourceName":"Name","type":"string","description":"The module name.","inheritedFrom":"Module"},{"name":"path","sourceName":"Path","type":"string","description":"The path of the module directory relative to the workspace.","inheritedFrom":"Module"},{"name":"project_type","sourceName":"ProjectType","type":"enum (ProjectLanguage)","description":"The project type or language.","inheritedFrom":"Module"},{"name":"role","sourceName":"Role","type":"string","description":"The primary semantic role of the module.","inheritedFrom":"Module"},{"name":"framework","sourceName":"Framework","type":"string","description":"The detected application, service, or test framework.","inheritedFrom":"Module"}],"outEdges":[],"inEdges":[],"inheritedOutEdges":[{"rel":"SERVICE_CALL","toKind":"ExternalService","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.","inheritedFrom":"Workload"},{"rel":"SERVICE_CALL","toKind":"Service","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.","inheritedFrom":"Workload"},{"rel":"PUBLISHES_TO","toKind":"Topic","category":"data_flow","description":"Links a service or function to a message queue or topic it publishes messages or events to.","inheritedFrom":"Workload"},{"rel":"USES_DB","toKind":"Database","category":"data_flow","description":"Links a project to a database catalog or instance it utilizes.","inheritedFrom":"Workload"},{"rel":"DEPENDS_ON","toKind":"Library","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"DEPENDS_ON","toKind":"Package","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"HOSTED_IN","toKind":"Cloud","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.","inheritedFrom":"Workload"},{"rel":"IMPLEMENTED_BY","toKind":"Project","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it.","inheritedFrom":"Module"}],"inheritedInEdges":[{"rel":"TESTS","fromKind":"TestSuite","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.","inheritedFrom":"Module"},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements.","inheritedFrom":"Module"}],"sampleCypher":"MATCH (n:App)-[:SERVICE_CALL]->(t:ExternalService)\nRETURN n.name, n.project_type, n.role, t.name\nLIMIT 25"},{"kind":"Service","pluralLabel":"Services","layerId":4,"layerKey":"L4_semantic","icon":"server-process","order":3,"idScheme":"{workspaceId}:s:{serviceName}","purpose":"Represents an executable backend service, microservice, or API daemon (e.g. ASP.NET Core Web API, NestJS, Express, Go HTTP server).","extends":"Workload","ancestors":["Workload","Module"],"properties":[{"name":"name","sourceName":"Name","type":"string","description":"The module name.","inheritedFrom":"Module"},{"name":"path","sourceName":"Path","type":"string","description":"The path of the module directory relative to the workspace.","inheritedFrom":"Module"},{"name":"project_type","sourceName":"ProjectType","type":"enum (ProjectLanguage)","description":"The project type or language.","inheritedFrom":"Module"},{"name":"role","sourceName":"Role","type":"string","description":"The primary semantic role of the module.","inheritedFrom":"Module"},{"name":"framework","sourceName":"Framework","type":"string","description":"The detected application, service, or test framework.","inheritedFrom":"Module"}],"outEdges":[{"rel":"CALLED_BY","toKind":"Function","category":"control_flow","description":"Links a service, external service, or database to the function that invokes or queries it."},{"rel":"EXPOSES","toKind":"Endpoint","category":"control_flow","description":"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."},{"rel":"COORDINATES_WITH","toKind":"PeerMesh","category":"type_dependency","description":"Links a workload to the symmetric peer mesh it coordinates with."}],"inEdges":[{"rel":"SERVICE_CALL","fromKind":"Workload","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."}],"inheritedOutEdges":[{"rel":"SERVICE_CALL","toKind":"ExternalService","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.","inheritedFrom":"Workload"},{"rel":"SERVICE_CALL","toKind":"Service","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.","inheritedFrom":"Workload"},{"rel":"PUBLISHES_TO","toKind":"Topic","category":"data_flow","description":"Links a service or function to a message queue or topic it publishes messages or events to.","inheritedFrom":"Workload"},{"rel":"USES_DB","toKind":"Database","category":"data_flow","description":"Links a project to a database catalog or instance it utilizes.","inheritedFrom":"Workload"},{"rel":"DEPENDS_ON","toKind":"Library","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"DEPENDS_ON","toKind":"Package","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"HOSTED_IN","toKind":"Cloud","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.","inheritedFrom":"Workload"},{"rel":"IMPLEMENTED_BY","toKind":"Project","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it.","inheritedFrom":"Module"}],"inheritedInEdges":[{"rel":"TESTS","fromKind":"TestSuite","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.","inheritedFrom":"Module"},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements.","inheritedFrom":"Module"}],"sampleCypher":"MATCH (n:Service)-[:CALLED_BY]->(t:Function)\nRETURN n.name, n.project_type, n.role, t.name\nLIMIT 25"},{"kind":"Worker","pluralLabel":"Workers","layerId":4,"layerKey":"L4_semantic","icon":"gear","order":4,"idScheme":"{workspaceId}:w:{workerName}","purpose":"Represents a background job processor, queue consumer, or scheduled task (e.g. Hangfire, Celery, Worker Service).","extends":"Workload","ancestors":["Workload","Module"],"properties":[{"name":"name","sourceName":"Name","type":"string","description":"The module name.","inheritedFrom":"Module"},{"name":"path","sourceName":"Path","type":"string","description":"The path of the module directory relative to the workspace.","inheritedFrom":"Module"},{"name":"project_type","sourceName":"ProjectType","type":"enum (ProjectLanguage)","description":"The project type or language.","inheritedFrom":"Module"},{"name":"role","sourceName":"Role","type":"string","description":"The primary semantic role of the module.","inheritedFrom":"Module"},{"name":"framework","sourceName":"Framework","type":"string","description":"The detected application, service, or test framework.","inheritedFrom":"Module"}],"outEdges":[{"rel":"SUBSCRIBES_TO","toKind":"Topic","category":"data_flow","description":"Links a service or handler function to a message queue or topic it listens to or consumes events from."}],"inEdges":[],"inheritedOutEdges":[{"rel":"SERVICE_CALL","toKind":"ExternalService","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.","inheritedFrom":"Workload"},{"rel":"SERVICE_CALL","toKind":"Service","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.","inheritedFrom":"Workload"},{"rel":"PUBLISHES_TO","toKind":"Topic","category":"data_flow","description":"Links a service or function to a message queue or topic it publishes messages or events to.","inheritedFrom":"Workload"},{"rel":"USES_DB","toKind":"Database","category":"data_flow","description":"Links a project to a database catalog or instance it utilizes.","inheritedFrom":"Workload"},{"rel":"DEPENDS_ON","toKind":"Library","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"DEPENDS_ON","toKind":"Package","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"HOSTED_IN","toKind":"Cloud","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.","inheritedFrom":"Workload"},{"rel":"IMPLEMENTED_BY","toKind":"Project","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it.","inheritedFrom":"Module"}],"inheritedInEdges":[{"rel":"TESTS","fromKind":"TestSuite","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.","inheritedFrom":"Module"},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements.","inheritedFrom":"Module"}],"sampleCypher":"MATCH (n:Worker)-[:SUBSCRIBES_TO]->(t:Topic)\nRETURN n.name, n.project_type, n.role, t.name\nLIMIT 25"},{"kind":"CliTool","pluralLabel":"CLI Tools","layerId":4,"layerKey":"L4_semantic","icon":"terminal","order":5,"idScheme":"{workspaceId}:cli:{toolName}","purpose":"Represents a command-line tool, developer script, or administrative CLI utility.","extends":"Workload","ancestors":["Workload","Module"],"properties":[{"name":"name","sourceName":"Name","type":"string","description":"The module name.","inheritedFrom":"Module"},{"name":"path","sourceName":"Path","type":"string","description":"The path of the module directory relative to the workspace.","inheritedFrom":"Module"},{"name":"project_type","sourceName":"ProjectType","type":"enum (ProjectLanguage)","description":"The project type or language.","inheritedFrom":"Module"},{"name":"role","sourceName":"Role","type":"string","description":"The primary semantic role of the module.","inheritedFrom":"Module"},{"name":"framework","sourceName":"Framework","type":"string","description":"The detected application, service, or test framework.","inheritedFrom":"Module"}],"outEdges":[],"inEdges":[],"inheritedOutEdges":[{"rel":"SERVICE_CALL","toKind":"ExternalService","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.","inheritedFrom":"Workload"},{"rel":"SERVICE_CALL","toKind":"Service","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.","inheritedFrom":"Workload"},{"rel":"PUBLISHES_TO","toKind":"Topic","category":"data_flow","description":"Links a service or function to a message queue or topic it publishes messages or events to.","inheritedFrom":"Workload"},{"rel":"USES_DB","toKind":"Database","category":"data_flow","description":"Links a project to a database catalog or instance it utilizes.","inheritedFrom":"Workload"},{"rel":"DEPENDS_ON","toKind":"Library","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"DEPENDS_ON","toKind":"Package","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"HOSTED_IN","toKind":"Cloud","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.","inheritedFrom":"Workload"},{"rel":"IMPLEMENTED_BY","toKind":"Project","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it.","inheritedFrom":"Module"}],"inheritedInEdges":[{"rel":"TESTS","fromKind":"TestSuite","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.","inheritedFrom":"Module"},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements.","inheritedFrom":"Module"}],"sampleCypher":"MATCH (n:CliTool)-[:SERVICE_CALL]->(t:ExternalService)\nRETURN n.name, n.project_type, n.role, t.name\nLIMIT 25"},{"kind":"Library","pluralLabel":"Libraries & SDKs","layerId":4,"layerKey":"L4_semantic","icon":"library","order":6,"idScheme":"{workspaceId}:lib:{libraryName}","purpose":"Represents a shared library, utility module, DTO package, or domain contract reused across services.","extends":"Module","ancestors":["Module"],"properties":[{"name":"name","sourceName":"Name","type":"string","description":"The module name.","inheritedFrom":"Module"},{"name":"path","sourceName":"Path","type":"string","description":"The path of the module directory relative to the workspace.","inheritedFrom":"Module"},{"name":"project_type","sourceName":"ProjectType","type":"enum (ProjectLanguage)","description":"The project type or language.","inheritedFrom":"Module"},{"name":"role","sourceName":"Role","type":"string","description":"The primary semantic role of the module.","inheritedFrom":"Module"},{"name":"framework","sourceName":"Framework","type":"string","description":"The detected application, service, or test framework.","inheritedFrom":"Module"}],"outEdges":[],"inEdges":[{"rel":"DEPENDS_ON","fromKind":"Module","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."}],"inheritedOutEdges":[{"rel":"DEPENDS_ON","toKind":"Library","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"DEPENDS_ON","toKind":"Package","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"IMPLEMENTED_BY","toKind":"Project","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it.","inheritedFrom":"Module"}],"inheritedInEdges":[{"rel":"TESTS","fromKind":"TestSuite","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.","inheritedFrom":"Module"},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements.","inheritedFrom":"Module"}],"sampleCypher":"MATCH (n:Library)-[:DEPENDS_ON]->(t:Library)\nRETURN n.name, n.project_type, n.role, t.name\nLIMIT 25"},{"kind":"TestSuite","pluralLabel":"Test Suites","layerId":4,"layerKey":"L4_semantic","icon":"beaker","order":7,"idScheme":"{workspaceId}:suite:{suiteName}","purpose":"Represents an automated test project, runner suite, or test module (e.g. NUnit/xUnit test project, Jest test suite, pytest suite).","extends":"Module","ancestors":["Module"],"properties":[{"name":"name","sourceName":"Name","type":"string","description":"The module name.","inheritedFrom":"Module"},{"name":"path","sourceName":"Path","type":"string","description":"The path of the module directory relative to the workspace.","inheritedFrom":"Module"},{"name":"project_type","sourceName":"ProjectType","type":"enum (ProjectLanguage)","description":"The project type or language.","inheritedFrom":"Module"},{"name":"role","sourceName":"Role","type":"string","description":"The primary semantic role of the module.","inheritedFrom":"Module"},{"name":"framework","sourceName":"Framework","type":"string","description":"The detected application, service, or test framework.","inheritedFrom":"Module"}],"outEdges":[{"rel":"TESTS","toKind":"Module","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies."}],"inEdges":[],"inheritedOutEdges":[{"rel":"DEPENDS_ON","toKind":"Library","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"DEPENDS_ON","toKind":"Package","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","inheritedFrom":"Module"},{"rel":"IMPLEMENTED_BY","toKind":"Project","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it.","inheritedFrom":"Module"}],"inheritedInEdges":[{"rel":"TESTS","fromKind":"TestSuite","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.","inheritedFrom":"Module"},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements.","inheritedFrom":"Module"}],"sampleCypher":"MATCH (n:TestSuite)-[:TESTS]->(t:Module)\nRETURN n.name, n.project_type, n.role, t.name\nLIMIT 25"},{"kind":"Endpoint","pluralLabel":"API & Message Endpoints (REST, gRPC, WS, MSG)","layerId":4,"layerKey":"L4_semantic","icon":"radio-tower","order":10,"idScheme":"{workspaceId}:ep:{httpMethod}:{routeTemplate}","purpose":"Represents an exposed external API or message contract endpoint (REST, gRPC, GraphQL, WebSocket, WCF, or Broker Consumer).","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The endpoint name (e.g. GET /api/orders, MSG:SubmitOrder)."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"http_method","sourceName":"HttpMethod","type":"string","description":"The HTTP Verb or contract verb (GET, POST, PUT, DELETE, ALL, MSG, WS, RPC)."},{"name":"route_template","sourceName":"RouteTemplate","type":"string","description":"The declared route template or message contract name."},{"name":"protocol","sourceName":"Protocol","type":"string","description":"The contract protocol (REST, gRPC, GraphQL, WS, WCF, MSG, RPC)."},{"name":"is_anonymous","sourceName":"IsAnonymous","type":"boolean","description":"Whether endpoint allows unauthenticated anonymous access."},{"name":"required_roles","sourceName":"RequiredRoles","type":"string","description":"Comma-separated required roles or permissions."},{"name":"policies","sourceName":"Policies","type":"string","description":"Authorization policies guarding the endpoint."},{"name":"request_type","sourceName":"RequestType","type":"string","description":"The request payload type (e.g. StartRideRequest, CreateOrderDto)."},{"name":"response_type","sourceName":"ResponseType","type":"string","description":"The response payload type (e.g. RideDto, OrderResponse)."},{"name":"operation_type","sourceName":"OperationType","type":"enum (OperationType)","description":"The operation type for GraphQL (Query, Mutation, Subscription) or gRPC (Unary, Streaming)."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"Workspace-relative source file declaring the endpoint."},{"name":"start_line","sourceName":"StartLine","type":"integer","description":"1-indexed starting line number of the endpoint declaration."},{"name":"line","sourceName":"Line","type":"integer","description":"1-indexed source line number of the route or contract mapping."},{"name":"operation","sourceName":"Operation","type":"string","description":"Operation or handler action name associated with this endpoint."},{"name":"evidence_kind","sourceName":"EvidenceKind","type":"string","description":"Extraction provenance kind (endpoint, grpc, graphql, ws, consumer)."},{"name":"graphql_field","sourceName":"GraphqlField","type":"string","description":"Exposed GraphQL root field name when protocol is GraphQL."},{"name":"graphql_sdl","sourceName":"IsGraphqlSdl","type":"boolean","description":"Indicates whether the endpoint was extracted from a GraphQL SDL schema."},{"name":"status_code","sourceName":"StatusCode","type":"string","description":"Declared HTTP response status codes (e.g. 200, 201, 404)."},{"name":"summary","sourceName":"Summary","type":"string","description":"OpenAPI / Swagger or docstring endpoint summary."}],"outEdges":[{"rel":"CALLS","toKind":"Function","category":"control_flow","description":"Links a calling function to the function it directly invokes."},{"rel":"EXPOSED_BY","toKind":"Function","category":"control_flow","description":"Links an entrypoint or endpoint to the type or function that exposes it."},{"rel":"EXPOSED_BY","toKind":"Type","category":"control_flow","description":"Links an entrypoint or endpoint to the type or function that exposes it."},{"rel":"TRIGGERS","toKind":"Function","category":"control_flow","description":"Links an entry point or API endpoint to the handler function it triggers."},{"rel":"ATTRIBUTED_TO","toKind":"Database","category":"data_flow","description":"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{"rel":"ATTRIBUTED_TO","toKind":"ExternalService","category":"data_flow","description":"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{"rel":"ATTRIBUTED_TO","toKind":"Query","category":"data_flow","description":"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{"rel":"PASSES_LITERAL","toKind":"CallLiteral","category":"data_flow","description":"Links a calling function or scope to a literal argument passed directly into an invocation."},{"rel":"SUBSCRIBES_TO","toKind":"Topic","category":"data_flow","description":"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{"rel":"TARGETED_BY","toKind":"CallArgument","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","toKind":"Project","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARES","toKind":"CatchClause","category":"structural","description":"Indicates that a container entity declares a sub-entity."}],"inEdges":[{"rel":"CALLS_ENDPOINT","fromKind":"ExternalService","category":"control_flow","description":"Links an external API call to the target HTTP endpoint it invokes."},{"rel":"EXPOSES","fromKind":"Service","category":"control_flow","description":"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes."}],"sampleCypher":"MATCH (n:Endpoint)-[:CALLS]->(t:Function)\nRETURN n.name, n.http_method, n.route_template, t.name\nLIMIT 25"},{"kind":"Topic","pluralLabel":"Message Topics & Queues","layerId":4,"layerKey":"L4_semantic","icon":"mail","order":11,"idScheme":"{workspaceId}:top:{brokerType}:{topicName}","purpose":"Represents a message queue, event exchange, or topic boundary.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the topic or exchange."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"broker_type","sourceName":"BrokerType","type":"enum (BrokerType)","description":"The broker system type (rabbitmq, kafka, sqs, in-memory)."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"Workspace-relative source file declaring or referencing the topic."},{"name":"line","sourceName":"Line","type":"integer","description":"1-indexed source line of the publish or subscribe site."},{"name":"messaging_direction","sourceName":"MessagingDirection","type":"string","description":"Direction of broker interaction (publish, subscribe, both)."},{"name":"scope","sourceName":"Scope","type":"string","description":"Broker scope or virtual host / namespace."},{"name":"message_type","sourceName":"MessageType","type":"string","description":"Payload or event contract type published/consumed on this topic."}],"outEdges":[{"rel":"PUBLISHED_BY","toKind":"Function","category":"data_flow","description":"Links a topic to the function that publishes to it."},{"rel":"SUBSCRIBED_BY","toKind":"Function","category":"data_flow","description":"Links a topic to the function that subscribes to it."},{"rel":"TARGETED_BY","toKind":"CallArgument","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"HOSTED_IN","toKind":"Cloud","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"PUBLISHES_TO","fromKind":"Workload","category":"data_flow","description":"Links a service or function to a message queue or topic it publishes messages or events to."},{"rel":"SUBSCRIBES_TO","fromKind":"Endpoint","category":"data_flow","description":"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{"rel":"SUBSCRIBES_TO","fromKind":"Worker","category":"data_flow","description":"Links a service or handler function to a message queue or topic it listens to or consumes events from."},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (n:Topic)-[:PUBLISHED_BY]->(t:Function)\nRETURN n.name, n.broker_type, n.file_path, t.name\nLIMIT 25"},{"kind":"ExternalService","pluralLabel":"External Services & Resources","layerId":4,"layerKey":"L4_semantic","icon":"globe","order":12,"idScheme":"{workspaceId}:es:{protocol}:{host}","purpose":"Represents an external API, managed cloud resource, or remote service dependency outside the workspace (REST, gRPC, GraphQL, S3, Stripe, Auth0).","properties":[{"name":"name","sourceName":"Name","type":"string","description":"Logical host or external client dependency name."},{"name":"protocol","sourceName":"Protocol","type":"string","description":"Outbound transport protocol (HTTP, HTTPS, gRPC, GraphQL, WS, WCF)."},{"name":"domain_or_service","sourceName":"DomainOrService","type":"string","description":"Target domain hostname or logical service identifier."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"Workspace-relative source file issuing the outbound call."},{"name":"start_line","sourceName":"StartLine","type":"integer","description":"1-indexed line number of the outbound client call."},{"name":"base_url","sourceName":"BaseUrl","type":"string","description":"Configured base URL or service address."},{"name":"url","sourceName":"Url","type":"string","description":"Resolved outbound request URL or path template."},{"name":"config_key","sourceName":"ConfigKey","type":"string","description":"Configuration setting or environment variable supplying the host URL."},{"name":"graphql_field","sourceName":"GraphqlField","type":"string","description":"Queried GraphQL field name on the remote service."},{"name":"operation_type","sourceName":"OperationType","type":"string","description":"Remote operation kind (Query, Mutation, Unary, Streaming)."},{"name":"method","sourceName":"Method","type":"string","description":"Outbound HTTP verb or RPC method name."},{"name":"provider","sourceName":"Provider","type":"string","description":"Cloud platform or SaaS vendor hosting this external service (e.g. AWS, GCP, Azure, Stripe, Auth0)."},{"name":"resource_role","sourceName":"ResourceRole","type":"string","description":"Operational role when representing a managed cloud resource (storage, secrets, auth, compute, cdn, ai)."},{"name":"is_external","sourceName":"IsExternal","type":"boolean","description":"True when the target resolves outside workspace services."}],"outEdges":[{"rel":"CALLED_BY","toKind":"Function","category":"control_flow","description":"Links a service, external service, or database to the function that invokes or queries it."},{"rel":"CALLS_ENDPOINT","toKind":"Endpoint","category":"control_flow","description":"Links an external API call to the target HTTP endpoint it invokes."},{"rel":"TRANSITIVELY_CALLS","toKind":"Function","category":"control_flow","description":"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{"rel":"ACCESSED_BY","toKind":"Function","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","toKind":"Type","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"TARGETED_BY","toKind":"CallArgument","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"HOSTED_IN","toKind":"Cloud","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","toKind":"Project","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"SERVICE_CALL","fromKind":"Workload","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API."},{"rel":"ATTRIBUTED_TO","fromKind":"Endpoint","category":"data_flow","description":"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."}],"sampleCypher":"MATCH (n:ExternalService)-[:CALLED_BY]->(t:Function)\nRETURN n.name, n.protocol, n.domain_or_service, t.name\nLIMIT 25"},{"kind":"Cloud","pluralLabel":"Cloud Platforms & Vendors","layerId":4,"layerKey":"L4_semantic","icon":"cloud","order":13,"idScheme":"{workspaceId}:cloud:{provider}","purpose":"Represents a cloud platform, managed infrastructure provider, or SaaS vendor hosting external services, databases, brokers, or workloads (e.g. AWS, GCP, Azure, Cloudflare, Stripe, Supabase).","properties":[{"name":"name","sourceName":"Name","type":"string","description":"Cloud platform or vendor display name (e.g. AWS, GCP, Azure, Cloudflare, Stripe)."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"provider","sourceName":"Provider","type":"string","description":"Normalized cloud or vendor identifier (aws, gcp, azure, cloudflare, stripe, auth0, supabase, firebase)."},{"name":"region","sourceName":"Region","type":"string","description":"Configured cloud region when declared (e.g. us-east-1, europe-west1)."},{"name":"category","sourceName":"Category","type":"string","description":"Vendor category (hyperscaler, saas, edge, managed_data)."}],"outEdges":[{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"HOSTED_IN","fromKind":"Database","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{"rel":"HOSTED_IN","fromKind":"ExternalService","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{"rel":"HOSTED_IN","fromKind":"Topic","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{"rel":"HOSTED_IN","fromKind":"Workload","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."}],"sampleCypher":"MATCH (n:Cloud)-[:DECLARED_IN]->(t:File)\nRETURN n.name, n.provider, n.region, t.name\nLIMIT 25"},{"kind":"PeerMesh","pluralLabel":"Peer Meshes","layerId":4,"layerKey":"L4_semantic","icon":"share-alt","order":14,"idScheme":"{workspaceId}:mesh:peer-network","purpose":"Represents a symmetric peer-to-peer cluster or collective topology.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the peer mesh."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"member_count","sourceName":"MemberCount","type":"integer","description":"The number of configured peer members, when known."},{"name":"protocols","sourceName":"Protocols","type":"string","description":"The detected communication protocols."}],"outEdges":[],"inEdges":[{"rel":"COORDINATES_WITH","fromKind":"Service","category":"type_dependency","description":"Links a workload to the symmetric peer mesh it coordinates with."}],"sampleCypher":"MATCH (s:Service)-[:COORDINATES_WITH]->(n:PeerMesh)\nRETURN s.name, n.name, n.member_count, n.protocols\nLIMIT 25"},{"kind":"Database","pluralLabel":"Databases & Storage","layerId":4,"layerKey":"L4_semantic","icon":"database","order":20,"idScheme":"{workspaceId}:db:{dbType}:{dbName}","purpose":"Represents a database server instance or catalog configured by a workload and containing one or more DbSchema namespaces.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The database engine or catalog name."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"db_type","sourceName":"DbType","type":"enum (DatabaseType)","description":"The database system type (sqlserver, postgres, sqlite, mongodb, neo4j)."},{"name":"display_name","sourceName":"DisplayName","type":"string","description":"Human-readable database or catalog display name."},{"name":"engine","sourceName":"Engine","type":"string","description":"Detected database engine (e.g. postgresql, sqlserver, sqlite, mysql, mongodb, redis)."},{"name":"technology","sourceName":"Technology","type":"string","description":"ORM, driver, or client technology accessing the database (e.g. EF Core, Dapper, Prisma, TypeORM)."},{"name":"schema","sourceName":"Schema","type":"string","description":"Physical database schema name when declared (e.g. public, dbo, billing)."},{"name":"provider","sourceName":"Provider","type":"string","description":"Database driver or provider package identifier."},{"name":"role","sourceName":"Role","type":"string","description":"Storage role classification (primary, cache, analytics, read-replica)."},{"name":"config_key","sourceName":"ConfigKey","type":"string","description":"Configuration key or connection string name bound to this database."},{"name":"from_config","sourceName":"FromConfig","type":"boolean","description":"Indicates whether the database connection was discovered from configuration files."},{"name":"is_test","sourceName":"IsTest","type":"boolean","description":"Indicates whether this database instance is used exclusively in tests."},{"name":"is_orm","sourceName":"IsOrm","type":"boolean","description":"Indicates whether access was inferred via an ORM entity/DbContext mapping."}],"outEdges":[{"rel":"TARGETED_BY","toKind":"CallArgument","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"HOSTED_IN","toKind":"Cloud","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it."},{"rel":"CONTAINS","toKind":"DbSchema","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"ATTRIBUTED_TO","fromKind":"Endpoint","category":"data_flow","description":"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{"rel":"USES_DB","fromKind":"Workload","category":"data_flow","description":"Links a project to a database catalog or instance it utilizes."},{"rel":"CONTAINS","fromKind":"BoundedContext","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (n:Database)-[:TARGETED_BY]->(t:CallArgument)\nRETURN n.name, n.db_type, n.display_name, t.name\nLIMIT 25"},{"kind":"DbSchema","pluralLabel":"Database Schemas","layerId":4,"layerKey":"L4_semantic","icon":"symbol-namespace","order":21,"idScheme":"{workspaceId}:db:{dbType}:{dbName}:schema:{schemaName}","purpose":"Represents a logical or physical schema namespace within a database catalog owning tables, views, and stored procedures (e.g. public, dbo, billing, main).","properties":[{"name":"name","sourceName":"Name","type":"string","description":"Database schema name (e.g. public, dbo, billing, main)."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"engine","sourceName":"Engine","type":"string","description":"Database engine owning this schema (e.g. PostgreSQL, SqlServer, SQLite)."},{"name":"database_id","sourceName":"DatabaseId","type":"string","description":"URN of the parent Database catalog containing this schema."},{"name":"is_default","sourceName":"IsDefault","type":"boolean","description":"True when synthesized for the engine's default schema (public, dbo, main)."}],"outEdges":[{"rel":"CONTAINS","toKind":"Procedure","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"CONTAINS","toKind":"Table","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"CONTAINS","fromKind":"Database","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (n:DbSchema)-[:CONTAINS]->(t:Procedure)\nRETURN n.name, n.engine, n.database_id, t.name\nLIMIT 25"},{"kind":"Table","pluralLabel":"Tables & Views","layerId":4,"layerKey":"L4_semantic","icon":"table","order":22,"idScheme":"{workspaceId}:tbl:{tableName}","purpose":"Represents a physical database table.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"Physical database table or view name."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"engine","sourceName":"Engine","type":"string","description":"Database engine hosting this table (e.g. postgresql, sqlserver, sqlite)."},{"name":"schema","sourceName":"Schema","type":"string","description":"Database schema qualifying this table (e.g. dbo, public, core)."},{"name":"entity","sourceName":"EntityName","type":"string","description":"Mapped ORM entity or domain model type name, if applicable."}],"outEdges":[{"rel":"ACCESSED_BY","toKind":"Function","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","toKind":"Query","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","toKind":"Type","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"TARGETED_BY","toKind":"CallArgument","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."}],"inEdges":[{"rel":"READS_FROM","fromKind":"Query","category":"data_flow","description":"Links a query or workload to the table or cloud resource it reads from."},{"rel":"WRITES_TO","fromKind":"Query","category":"data_flow","description":"Links a query or workload to the table or cloud resource it writes to."},{"rel":"DEPENDS_ON","fromKind":"Query","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"CONTAINS","fromKind":"DbSchema","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (n:Table)-[:ACCESSED_BY]->(t:Function)\nRETURN n.name, n.engine, n.schema, t.name\nLIMIT 25"},{"kind":"Procedure","pluralLabel":"Stored Procedures","layerId":4,"layerKey":"L4_semantic","icon":"database","order":23,"idScheme":"{workspaceId}:proc:{procedureName}","purpose":"Represents a stored procedure in a database.","properties":[],"outEdges":[{"rel":"ACCESSED_BY","toKind":"Function","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"TARGETED_BY","toKind":"CallArgument","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"CONTAINS","toKind":"Query","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"inEdges":[{"rel":"CONTAINS","fromKind":"DbSchema","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (n:Procedure)-[:ACCESSED_BY]->(t:Function)\nRETURN n.name, n.path, t.name\nLIMIT 25"},{"kind":"Query","pluralLabel":"SQL Queries","layerId":4,"layerKey":"L4_semantic","icon":"search","order":30,"idScheme":"{workspaceId}:q:{queryHash}","purpose":"Represents a SQL query.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"Short query identifier or operation summary."},{"name":"query_text","sourceName":"QueryText","type":"string","description":"Extracted SQL or query statement text."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"start_line","sourceName":"StartLine","type":"integer","description":"1-indexed starting line number of the query in source."},{"name":"verb","sourceName":"Verb","type":"string","description":"SQL DML/DDL verb (SELECT, INSERT, UPDATE, DELETE, MERGE, EXEC)."},{"name":"operation","sourceName":"Operation","type":"string","description":"Normalized data access operation (read, write, schema)."},{"name":"evidence_kind","sourceName":"EvidenceKind","type":"string","description":"Extraction provenance kind (query, orm, migration, procedure)."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"Workspace-relative source file path declaring the query."},{"name":"line","sourceName":"Line","type":"integer","description":"1-indexed source line of the query call site."}],"outEdges":[{"rel":"TRANSITIVELY_CALLS","toKind":"Function","category":"control_flow","description":"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{"rel":"ACCESSED_BY","toKind":"Function","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","toKind":"Type","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"READS_FROM","toKind":"Table","category":"data_flow","description":"Links a query or workload to the table or cloud resource it reads from."},{"rel":"WRITES_TO","toKind":"Table","category":"data_flow","description":"Links a query or workload to the table or cloud resource it writes to."},{"rel":"DEPENDS_ON","toKind":"Table","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"ACCESSED_BY","fromKind":"Table","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ATTRIBUTED_TO","fromKind":"Endpoint","category":"data_flow","description":"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain."},{"rel":"CONTAINS","fromKind":"Procedure","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (n:Query)-[:TRANSITIVELY_CALLS]->(t:Function)\nRETURN n.name, n.query_text, n.start_line, t.name\nLIMIT 25"},{"kind":"Type","pluralLabel":"Types (Classes, Interfaces)","layerId":3,"layerKey":"L3_syntactic","icon":"symbol-class","order":1,"idScheme":"{workspaceId}:sym:{filePath}:Type:{name}:{line}","purpose":"Represents a type declaration (Class, Interface, Struct, Record, Enum, or Union type).","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the entity."},{"name":"symbol","sourceName":"Symbol","type":"string","description":"A globally unique ID for this symbol scope."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"The relative path of the declaring file."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"start_line","sourceName":"StartLine","type":"integer","description":"The starting line number (1-indexed) of the declaration."},{"name":"kind","sourceName":"TypeKind","type":"enum (TypeKind)","description":"The specific type kind (class, interface, struct, record, enum)."},{"name":"is_test","sourceName":"IsTest","type":"boolean","description":"Indicates whether the type is declared within test code."},{"name":"project_kind","sourceName":"ProjectKind","type":"enum (ProjectEntityKind)","description":"The architectural kind of the enclosing project."},{"name":"project_name","sourceName":"ProjectName","type":"string","description":"The name of the enclosing project."},{"name":"loc","sourceName":"Loc","type":"integer","description":"Lines of code span for this type declaration."},{"name":"is_exported","sourceName":"IsExported","type":"boolean","description":"Indicates whether the type is publicly exported from its module."},{"name":"extends","sourceName":"Extends","type":"string","description":"Declared base type or component inherited by this type."},{"name":"implements","sourceName":"Implements","type":"string","description":"Declared interfaces implemented by this type."},{"name":"attributes","sourceName":"Attributes","type":"string","description":"Comma-separated annotations, attributes, or decorators applied to this type."}],"outEdges":[{"rel":"CALLS","toKind":"Function","category":"control_flow","description":"Links a calling function to the function it directly invokes."},{"rel":"IMPLEMENTS","toKind":"Type","category":"control_flow","description":"Links a class/struct declaration to the interface it implements."},{"rel":"PASSES_LITERAL","toKind":"CallLiteral","category":"data_flow","description":"Links a calling function or scope to a literal argument passed directly into an invocation."},{"rel":"INHERITS_FROM","toKind":"Type","category":"type_dependency","description":"Links a class/interface to its base class or inherited interface."},{"rel":"POTENTIAL_TYPE","toKind":"Type","category":"type_dependency","description":"Links a variable or parameter to concrete classes that implement its declared interface type."},{"rel":"USES_TYPE","toKind":"Type","category":"type_dependency","description":"Links a calling function or type reference to the type it instantiates or references."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARES","toKind":"Type","category":"structural","description":"Indicates that a container entity declares a sub-entity."},{"rel":"HAS_MEMBER","toKind":"Member","category":"structural","description":"Links a type declaration to its declared member variables or fields."},{"rel":"HAS_METHOD","toKind":"Function","category":"structural","description":"Links a type declaration to its declared methods or functions."}],"inEdges":[{"rel":"CALLS","fromKind":"Function","category":"control_flow","description":"Links a calling function to the function it directly invokes."},{"rel":"EXPOSED_BY","fromKind":"Endpoint","category":"control_flow","description":"Links an entrypoint or endpoint to the type or function that exposes it."},{"rel":"IMPLEMENTS","fromKind":"Type","category":"control_flow","description":"Links a class/struct declaration to the interface it implements."},{"rel":"INSTANTIATES","fromKind":"Function","category":"control_flow","description":"Links a host component to a runtime UI or subsystem component it instantiates."},{"rel":"ACCESSED_BY","fromKind":"ExternalService","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","fromKind":"Query","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","fromKind":"Table","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"INHERITS_FROM","fromKind":"Type","category":"type_dependency","description":"Links a class/interface to its base class or inherited interface."},{"rel":"OF_TYPE","fromKind":"Member","category":"type_dependency","description":"Links a member variable or field to its declared type."},{"rel":"POTENTIAL_TYPE","fromKind":"Type","category":"type_dependency","description":"Links a variable or parameter to concrete classes that implement its declared interface type."},{"rel":"USES_TYPE","fromKind":"Function","category":"type_dependency","description":"Links a calling function or type reference to the type it instantiates or references."},{"rel":"USES_TYPE","fromKind":"Type","category":"type_dependency","description":"Links a calling function or type reference to the type it instantiates or references."},{"rel":"DECLARES","fromKind":"Function","category":"structural","description":"Indicates that a container entity declares a sub-entity."},{"rel":"DECLARES","fromKind":"Type","category":"structural","description":"Indicates that a container entity declares a sub-entity."},{"rel":"DEFINES","fromKind":"BoundedContext","category":"structural","description":"Indicates that an entity defines a particular property or configuration."}],"sampleCypher":"MATCH (n:Type)-[:CALLS]->(t:Function)\nRETURN n.name, n.symbol, n.file_path, t.name\nLIMIT 25"},{"kind":"Function","pluralLabel":"Functions & Methods","layerId":3,"layerKey":"L3_syntactic","icon":"symbol-method","order":2,"idScheme":"{workspaceId}:sym:{filePath}:Function:{name}:{line}","purpose":"Represents a parsed method, function, subroutine, or procedure.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the entity."},{"name":"symbol","sourceName":"Symbol","type":"string","description":"A globally unique ID for this symbol scope."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"The relative path of the declaring file."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"start_line","sourceName":"StartLine","type":"integer","description":"The starting line number (1-indexed) of the declaration."},{"name":"is_test","sourceName":"IsTest","type":"boolean","description":"Indicates whether the function is declared within test code."},{"name":"test_framework","sourceName":"TestFramework","type":"string","description":"Detected test runner or framework (e.g. xunit, nunit, mstest, jest, pytest, go-test, junit)."},{"name":"project_kind","sourceName":"ProjectKind","type":"enum (ProjectEntityKind)","description":"The architectural kind of the enclosing project."},{"name":"project_name","sourceName":"ProjectName","type":"string","description":"The name of the enclosing project."},{"name":"loc","sourceName":"Loc","type":"integer","description":"Lines of code span for this function."},{"name":"is_exported","sourceName":"IsExported","type":"boolean","description":"Indicates whether the function is publicly exported from its module."},{"name":"is_module_init","sourceName":"IsModuleInit","type":"boolean","description":"Indicates whether this node represents top-level module initialization code."},{"name":"is_async","sourceName":"IsAsync","type":"boolean","description":"Indicates whether the function is asynchronous."},{"name":"is_unsafe","sourceName":"IsUnsafe","type":"boolean","description":"Indicates whether the function uses unsafe code blocks or modifiers."},{"name":"visibility","sourceName":"Visibility","type":"string","description":"Declared visibility or access modifier of the function (e.g. public, private, remote)."},{"name":"graphql_operation","sourceName":"GraphqlOperation","type":"string","description":"GraphQL client operation name or flag."},{"name":"operation_type","sourceName":"OperationType","type":"string","description":"Operation type for GraphQL or RPC operations."},{"name":"graphql_fields","sourceName":"GraphqlFields","type":"string","description":"Comma-separated GraphQL root selection fields."},{"name":"attributes","sourceName":"Attributes","type":"string","description":"Comma-separated annotations, attributes, or decorators applied to this function."}],"outEdges":[{"rel":"CALLS","toKind":"Function","category":"control_flow","description":"Links a calling function to the function it directly invokes."},{"rel":"CALLS","toKind":"Type","category":"control_flow","description":"Links a calling function to the function it directly invokes."},{"rel":"IMPLEMENTS","toKind":"EntryPoint","category":"control_flow","description":"Links a class/struct declaration to the interface it implements."},{"rel":"INSTANTIATES","toKind":"EntryPoint","category":"control_flow","description":"Links a host component to a runtime UI or subsystem component it instantiates."},{"rel":"INSTANTIATES","toKind":"Function","category":"control_flow","description":"Links a host component to a runtime UI or subsystem component it instantiates."},{"rel":"INSTANTIATES","toKind":"Type","category":"control_flow","description":"Links a host component to a runtime UI or subsystem component it instantiates."},{"rel":"HAS_PARAMETER","toKind":"Parameter","category":"data_flow","description":"Links a function or method to one of its semantically colored parameters."},{"rel":"PASSES_ARGUMENT","toKind":"CallArgument","category":"data_flow","description":"Links a calling function or scope to a semantically colored call argument."},{"rel":"PASSES_LITERAL","toKind":"CallLiteral","category":"data_flow","description":"Links a calling function or scope to a literal argument passed directly into an invocation."},{"rel":"USES_TYPE","toKind":"Type","category":"type_dependency","description":"Links a calling function or type reference to the type it instantiates or references."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARES","toKind":"CatchClause","category":"structural","description":"Indicates that a container entity declares a sub-entity."},{"rel":"DECLARES","toKind":"Function","category":"structural","description":"Indicates that a container entity declares a sub-entity."},{"rel":"DECLARES","toKind":"Member","category":"structural","description":"Indicates that a container entity declares a sub-entity."},{"rel":"DECLARES","toKind":"Type","category":"structural","description":"Indicates that a container entity declares a sub-entity."}],"inEdges":[{"rel":"CALLED_BY","fromKind":"ExternalService","category":"control_flow","description":"Links a service, external service, or database to the function that invokes or queries it."},{"rel":"CALLED_BY","fromKind":"Service","category":"control_flow","description":"Links a service, external service, or database to the function that invokes or queries it."},{"rel":"CALLS","fromKind":"Endpoint","category":"control_flow","description":"Links a calling function to the function it directly invokes."},{"rel":"CALLS","fromKind":"Function","category":"control_flow","description":"Links a calling function to the function it directly invokes."},{"rel":"CALLS","fromKind":"Type","category":"control_flow","description":"Links a calling function to the function it directly invokes."},{"rel":"EXPOSED_BY","fromKind":"Endpoint","category":"control_flow","description":"Links an entrypoint or endpoint to the type or function that exposes it."},{"rel":"INSTANTIATES","fromKind":"Function","category":"control_flow","description":"Links a host component to a runtime UI or subsystem component it instantiates."},{"rel":"TRANSITIVELY_CALLS","fromKind":"ExternalService","category":"control_flow","description":"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{"rel":"TRANSITIVELY_CALLS","fromKind":"Query","category":"control_flow","description":"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph."},{"rel":"TRIGGERS","fromKind":"Endpoint","category":"control_flow","description":"Links an entry point or API endpoint to the handler function it triggers."},{"rel":"ACCESSED_BY","fromKind":"ExternalService","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","fromKind":"Member","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","fromKind":"Procedure","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","fromKind":"Query","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"ACCESSED_BY","fromKind":"Table","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"PUBLISHED_BY","fromKind":"Topic","category":"data_flow","description":"Links a topic to the function that publishes to it."},{"rel":"SUBSCRIBED_BY","fromKind":"Topic","category":"data_flow","description":"Links a topic to the function that subscribes to it."},{"rel":"DECLARES","fromKind":"Function","category":"structural","description":"Indicates that a container entity declares a sub-entity."},{"rel":"HAS_METHOD","fromKind":"Type","category":"structural","description":"Links a type declaration to its declared methods or functions."}],"sampleCypher":"MATCH (n:Function)-[:CALLS]->(t:Function)\nRETURN n.name, n.symbol, n.file_path, t.name\nLIMIT 25"},{"kind":"Member","pluralLabel":"Members & Fields","layerId":3,"layerKey":"L3_syntactic","icon":"symbol-field","order":3,"idScheme":"{workspaceId}:sym:{filePath}:Member:{name}:{line}","purpose":"Represents a declared field, property, parameter, or local variable.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the entity."},{"name":"symbol","sourceName":"Symbol","type":"string","description":"A globally unique ID for this symbol scope."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"The relative path of the declaring file."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"start_line","sourceName":"StartLine","type":"integer","description":"The starting line number (1-indexed) of the declaration."},{"name":"kind","sourceName":"MemberKind","type":"enum (MemberKind)","description":"The specific member kind (field, property, parameter, variable)."},{"name":"data_type","sourceName":"DataType","type":"string","description":"The declared data type of the member."},{"name":"parameter_index","sourceName":"ParameterIndex","type":"integer","description":"0-based formal parameter index when the member is a function parameter."},{"name":"variable_type","sourceName":"VariableClassification","type":"string","description":"Variable classification tags (e.g. parameter, config, etl, constant, global, property, field)."},{"name":"initializer_expression","sourceName":"InitializerExpression","type":"string","description":"Bounded initializer expression text."},{"name":"is_constant","sourceName":"IsConstant","type":"boolean","description":"Indicates whether the member is a compile-time or immutable constant."},{"name":"attributes","sourceName":"Attributes","type":"string","description":"Comma-separated annotations, attributes, or decorators applied to this member."}],"outEdges":[{"rel":"ACCESSED_BY","toKind":"Function","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it."},{"rel":"OF_TYPE","toKind":"Type","category":"type_dependency","description":"Links a member variable or field to its declared type."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"DECLARES","fromKind":"Function","category":"structural","description":"Indicates that a container entity declares a sub-entity."},{"rel":"HAS_MEMBER","fromKind":"Type","category":"structural","description":"Links a type declaration to its declared member variables or fields."}],"sampleCypher":"MATCH (n:Member)-[:ACCESSED_BY]->(t:Function)\nRETURN n.name, n.symbol, n.file_path, t.name\nLIMIT 25"},{"kind":"Parameter","pluralLabel":"Semantic Parameters","layerId":3,"layerKey":"L3_syntactic","icon":"symbol-parameter","order":38,"idScheme":"{workspaceId}:param:{functionSymbol}:{index}:{name}","purpose":"Represents a semantically colored function or method parameter that flows into an architectural sink.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The parameter identifier name."},{"name":"function_symbol","sourceName":"FunctionSymbol","type":"string","description":"The symbol ID of the declaring function or method."},{"name":"index","sourceName":"ParamIndex","type":"integer","description":"The 0-indexed position of the parameter in the function signature."},{"name":"data_type","sourceName":"DataType","type":"string","description":"The declared parameter type annotation, if present."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"The relative file path of the declaring function."},{"name":"line","sourceName":"Line","type":"integer","description":"The 1-indexed declaration line number."},{"name":"nature","sourceName":"Nature","type":"enum (ParameterNature)","description":"The semantic nature inferred from intra- or inter-procedural sink usage."},{"name":"sink_callee","sourceName":"SinkCallee","type":"string","description":"The primary sink callee that determined the parameter's semantic nature."},{"name":"is_test","sourceName":"IsTest","type":"boolean","description":"Indicates whether the parameter belongs to test code."},{"name":"project_kind","sourceName":"ProjectKind","type":"enum (ProjectEntityKind)","description":"The architectural kind of the enclosing project."},{"name":"project_name","sourceName":"ProjectName","type":"string","description":"The name of the enclosing project."}],"outEdges":[{"rel":"FLOWS_TO","toKind":"CallArgument","category":"data_flow","description":"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"FLOWS_TO","fromKind":"CallArgument","category":"data_flow","description":"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{"rel":"HAS_PARAMETER","fromKind":"Function","category":"data_flow","description":"Links a function or method to one of its semantically colored parameters."}],"sampleCypher":"MATCH (n:Parameter)-[:FLOWS_TO]->(t:CallArgument)\nRETURN n.name, n.function_symbol, n.index, t.name\nLIMIT 25"},{"kind":"CallArgument","pluralLabel":"Semantic Arguments","layerId":3,"layerKey":"L3_syntactic","icon":"symbol-variable","order":39,"idScheme":"{workspaceId}:arg:{filePath}:{line}:{col}:{slot}","purpose":"Represents a semantically colored invocation argument sliced backward to its provenance origin.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"Display label or slot representation of the argument."},{"name":"callee","sourceName":"Callee","type":"string","description":"The method, function, or constructor being invoked."},{"name":"arg_index","sourceName":"ArgIndex","type":"integer","description":"The 0-indexed positional slot of the argument."},{"name":"property_key","sourceName":"PropertyKey","type":"string","description":"The object property key when passed via an options object (e.g. sql, text, url, topic)."},{"name":"raw_expression","sourceName":"RawExpression","type":"string","description":"The syntactic expression passed at the call site."},{"name":"resolved_value","sourceName":"ResolvedValue","type":"string","description":"The concrete value resolved via backward constant/template slicing, if determinable."},{"name":"nature","sourceName":"Nature","type":"enum (ParameterNature)","description":"The semantic nature of the argument slot."},{"name":"provenance","sourceName":"Provenance","type":"enum (ArgumentProvenance)","description":"Where the argument's value originates (InternalConstant, CallerParameter, ConfigOrEnv, MemberOrField, etc.)."},{"name":"expression_role","sourceName":"Role","type":"enum (ExpressionRole)","description":"How the expression participates in constructing the argument value."},{"name":"value_kind","sourceName":"ValueKind","type":"enum (ConstantValueKind)","description":"Fine-grained classification of the resolved constant or template value."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"The relative file path where the call occurs."},{"name":"line","sourceName":"Line","type":"integer","description":"The 1-indexed line number of the call argument."},{"name":"origin_file","sourceName":"OriginFile","type":"string","description":"The relative file path where the value was originally defined, if distinct."},{"name":"origin_line","sourceName":"OriginLine","type":"integer","description":"The 1-indexed line number where the value was originally defined."},{"name":"origin_symbol","sourceName":"OriginSymbol","type":"string","description":"The variable, constant, field, parameter, or config key from which the value originated."},{"name":"is_test","sourceName":"IsTest","type":"boolean","description":"Indicates whether the call occurs within test code."},{"name":"project_kind","sourceName":"ProjectKind","type":"enum (ProjectEntityKind)","description":"The architectural kind of the enclosing project."},{"name":"project_name","sourceName":"ProjectName","type":"string","description":"The name of the enclosing project."}],"outEdges":[{"rel":"FLOWS_TO","toKind":"Parameter","category":"data_flow","description":"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"FLOWS_TO","fromKind":"Parameter","category":"data_flow","description":"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops."},{"rel":"PASSES_ARGUMENT","fromKind":"Function","category":"data_flow","description":"Links a calling function or scope to a semantically colored call argument."},{"rel":"TARGETED_BY","fromKind":"Database","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"TARGETED_BY","fromKind":"Endpoint","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"TARGETED_BY","fromKind":"ExternalService","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"TARGETED_BY","fromKind":"Procedure","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"TARGETED_BY","fromKind":"Table","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."},{"rel":"TARGETED_BY","fromKind":"Topic","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it."}],"sampleCypher":"MATCH (n:CallArgument)-[:FLOWS_TO]->(t:Parameter)\nRETURN n.name, n.callee, n.arg_index, t.name\nLIMIT 25"},{"kind":"CallLiteral","pluralLabel":"Call Literals","layerId":3,"layerKey":"L3_syntactic","icon":"quote","order":40,"idScheme":"{workspaceId}:lit:{filePath}:{line}:{col}","purpose":"Represents a raw literal argument passed directly into a method or constructor invocation.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name or representation of the literal argument."},{"name":"value","sourceName":"Value","type":"string","description":"The raw string value of the literal."},{"name":"callee","sourceName":"Callee","type":"string","description":"The method or callee being invoked."},{"name":"arg_index","sourceName":"ArgIndex","type":"integer","description":"The index of the argument (0-indexed)."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"The relative file path where the call occurs."},{"name":"line","sourceName":"Line","type":"integer","description":"The 1-indexed line number of the argument."},{"name":"is_test","sourceName":"IsTest","type":"boolean","description":"Indicates whether the call occurs within test code."},{"name":"project_kind","sourceName":"ProjectKind","type":"enum (ProjectEntityKind)","description":"The architectural kind of the enclosing project."},{"name":"project_name","sourceName":"ProjectName","type":"string","description":"The name of the enclosing project."},{"name":"role","sourceName":"Role","type":"string","description":"The semantic role of the literal argument (e.g. Business, Routing, ParamBinding, Config, Logging, QueryBuilder, DI)."},{"name":"is_decorator","sourceName":"IsDecorator","type":"boolean","description":"Indicates whether the literal argument occurs within a decorator or attribute."},{"name":"nature","sourceName":"Nature","type":"enum (ParameterNature)","description":"The semantic nature of the parameter/argument (e.g. SqlQuery, UrlOrRoute, ConnectionString, FilePath)."}],"outEdges":[{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"PASSES_LITERAL","fromKind":"Endpoint","category":"data_flow","description":"Links a calling function or scope to a literal argument passed directly into an invocation."},{"rel":"PASSES_LITERAL","fromKind":"Function","category":"data_flow","description":"Links a calling function or scope to a literal argument passed directly into an invocation."},{"rel":"PASSES_LITERAL","fromKind":"Type","category":"data_flow","description":"Links a calling function or scope to a literal argument passed directly into an invocation."}],"sampleCypher":"MATCH (n:CallLiteral)-[:DECLARED_IN]->(t:File)\nRETURN n.name, n.value, n.callee, t.name\nLIMIT 25"},{"kind":"CatchClause","pluralLabel":"Catch Clauses","layerId":3,"layerKey":"L3_syntactic","icon":"shield-alert","order":41,"idScheme":"{workspaceId}:catch:{filePath}:{line}:{col}","purpose":"Represents a catch or except block in structured exception handling.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The identifier or descriptor of the catch clause."},{"name":"exception_type","sourceName":"ExceptionType","type":"string","description":"The declared exception type caught by this block, if specified."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"The relative file path where the catch clause occurs."},{"name":"line","sourceName":"Line","type":"integer","description":"The 1-indexed line number where the catch clause starts."},{"name":"is_empty","sourceName":"IsEmpty","type":"boolean","description":"Indicates whether the catch body contains no executable statements."},{"name":"has_comment","sourceName":"HasComment","type":"boolean","description":"Indicates whether an empty catch block contains an explanatory comment."},{"name":"statement_count","sourceName":"StatementCount","type":"integer","description":"The number of executable statements inside the catch body."},{"name":"is_test","sourceName":"IsTest","type":"boolean","description":"Indicates whether the catch clause occurs within test code."},{"name":"project_kind","sourceName":"ProjectKind","type":"enum (ProjectEntityKind)","description":"The architectural kind of the enclosing project."},{"name":"project_name","sourceName":"ProjectName","type":"string","description":"The name of the enclosing project."}],"outEdges":[{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"DECLARES","fromKind":"Endpoint","category":"structural","description":"Indicates that a container entity declares a sub-entity."},{"rel":"DECLARES","fromKind":"Function","category":"structural","description":"Indicates that a container entity declares a sub-entity."}],"sampleCypher":"MATCH (n:CatchClause)-[:DECLARED_IN]->(t:File)\nRETURN n.name, n.exception_type, n.file_path, t.name\nLIMIT 25"},{"kind":"Project","pluralLabel":"Projects","layerId":2,"layerKey":"L2_boundaries","icon":"project","order":1,"idScheme":"{workspaceId}:p:{relativeProjectDir}:","purpose":"Represents a buildable/compilable module or package directory (e.g. C# project, Go module, TS library, Python package).","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the entity."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"project_type","sourceName":"ProjectType","type":"enum (ProjectLanguage)","description":"The language/signature identifier (e.g. 'csharp', 'go', 'python', 'typescript')."},{"name":"language","sourceName":"Language","type":"string","description":"The programming language (e.g. 'csharp', 'go', 'python', 'typescript')."},{"name":"kind","sourceName":"KindValue","type":"string","description":"The physical entity classification kind (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test)."},{"name":"role","sourceName":"Role","type":"string","description":"Architectural role of the project (e.g. Service, SharedLibrary, FrontendApp, Worker, CliTool, Test)."},{"name":"archetype","sourceName":"Archetype","type":"enum (ProjectArchetype)","description":"Architectural archetype of the project (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon)."},{"name":"primary_role","sourceName":"PrimaryRole","type":"string","description":"Primary architectural role of the project (e.g. WebService, Scheduler, WorkerService, SharedLibrary, FrontendFramework, CliFramework, TestFramework)."},{"name":"framework","sourceName":"Framework","type":"string","description":"Detected primary framework of the project."},{"name":"entity_type","sourceName":"EntityType","type":"string","description":"Normalized semantic entity type (app, service, worker, cli, test, library)."},{"name":"external_apis","sourceName":"ExternalApis","type":"string","description":"Comma-separated external APIs or SDKs used by this project."},{"name":"package_name","sourceName":"PackageName","type":"string","description":"Package name declared in the project manifest."},{"name":"package_version","sourceName":"PackageVersion","type":"string","description":"Package version declared in the project manifest."},{"name":"main_file","sourceName":"MainFile","type":"string","description":"Main entry file declared in the project manifest."},{"name":"module_file","sourceName":"ModuleFile","type":"string","description":"ESM module entry file declared in the project manifest."},{"name":"description","sourceName":"Description","type":"string","description":"Project description from manifest."},{"name":"is_library","sourceName":"IsLibrary","type":"boolean","description":"Indicates whether the project is a shared library rather than an executable application."},{"name":"entity_kind","sourceName":"EntityKind","type":"enum (ProjectEntityKind)","description":"The physical entity classification (e.g. Service, App, Worker, FunctionApp, Library, DatabaseMigration, Test)."},{"name":"sub_kind","sourceName":"SubKind","type":"enum (ProjectEntitySubKind)","description":"Sub-classification for apps (e.g. None, Web, Mobile, Desktop, Cli)."},{"name":"is_test","sourceName":"IsTest","type":"boolean","description":"Indicates whether this project is a test project."},{"name":"project_kind","sourceName":"ProjectKind","type":"enum (ProjectEntityKind)","description":"The physical entity classification kind."}],"outEdges":[{"rel":"HAS_ENTRYPOINT","toKind":"EntryPoint","category":"control_flow","description":"Links an executable ProjectNode to its binary or process EntryPointNode."},{"rel":"DEPENDS_ON","toKind":"Package","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"DEPENDS_ON","toKind":"Project","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"PRODUCES","toKind":"Package","category":"type_dependency","description":"Indicates that a library project builds and produces a package."},{"rel":"USES_GIT","toKind":"GitSettings","category":"type_dependency","description":"Links the workspace to its repository configuration."},{"rel":"CONTAINS","toKind":"EntryPoint","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"CONTAINS","toKind":"File","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"CONTAINS","toKind":"Project","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"LOCATED_IN","toKind":"Folder","category":"structural","description":"Links a project to the physical folder or files structure where it is located."}],"inEdges":[{"rel":"DEPENDS_ON","fromKind":"Project","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"IMPLEMENTED_BY","fromKind":"Module","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it."},{"rel":"IMPLEMENTED_BY","fromKind":"Package","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it."},{"rel":"PRODUCES","fromKind":"Package","category":"type_dependency","description":"Indicates that a library project builds and produces a package."},{"rel":"BELONGS_TO","fromKind":"EntryPoint","category":"structural","description":"Links an entity to its parent project or container."},{"rel":"CONTAINS","fromKind":"Project","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"DECLARED_IN","fromKind":"Endpoint","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"ExternalService","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"sampleCypher":"MATCH (n:Project)-[:HAS_ENTRYPOINT]->(t:EntryPoint)\nRETURN n.name, n.project_type, n.language, t.name\nLIMIT 25"},{"kind":"EntryPoint","pluralLabel":"Entry Points","layerId":2,"layerKey":"L2_boundaries","icon":"zap","order":2,"idScheme":"{workspaceId}:entry:{entryType}:{name}","purpose":"Represents the binary, process, or bundle bootstrap entry point of an executable project (e.g. Program.Main, func main, __main__, index.tsx root mount).","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the binary or process entry point."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the entry point file relative to its parent container."},{"name":"entry_type","sourceName":"EntryType","type":"enum (EntryPointType)","description":"The kind of binary/process entry point (e.g. main-method, top-level, script-main, bundle-entry)."},{"name":"file_path","sourceName":"FilePath","type":"string","description":"Relative path of the source file declaring the entry point."},{"name":"start_line","sourceName":"StartLine","type":"integer","description":"1-based starting line number of the entry point."},{"name":"command","sourceName":"Command","type":"string","description":"Shell command executed by a lifecycle or task entry point."},{"name":"hook_kind","sourceName":"HookKind","type":"string","description":"Lifecycle hook classification (e.g. install, build)."},{"name":"task_label","sourceName":"TaskLabel","type":"string","description":"Task label for VS Code folderOpen tasks."}],"outEdges":[{"rel":"TRIGGERS","toKind":"File","category":"control_flow","description":"Links an entry point or API endpoint to the handler function it triggers."},{"rel":"BELONGS_TO","toKind":"Project","category":"structural","description":"Links an entity to its parent project or container."},{"rel":"DECLARED_IN","toKind":"File","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"inEdges":[{"rel":"HAS_ENTRYPOINT","fromKind":"Project","category":"control_flow","description":"Links an executable ProjectNode to its binary or process EntryPointNode."},{"rel":"IMPLEMENTS","fromKind":"Function","category":"control_flow","description":"Links a class/struct declaration to the interface it implements."},{"rel":"INSTANTIATES","fromKind":"Function","category":"control_flow","description":"Links a host component to a runtime UI or subsystem component it instantiates."},{"rel":"CONTAINS","fromKind":"Project","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (n:EntryPoint)-[:TRIGGERS]->(t:File)\nRETURN n.name, n.entry_type, n.file_path, t.name\nLIMIT 25"},{"kind":"Package","pluralLabel":"Packages & Dependencies","layerId":2,"layerKey":"L2_boundaries","icon":"package","order":2,"idScheme":"{workspaceId}:pkg:{ecosystem}:{packageName}","purpose":"Represents an external dependency package or workspace package referenced or produced by projects.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the entity."},{"name":"version","sourceName":"Version","type":"string","description":"The package version."},{"name":"is_internal","sourceName":"IsInternal","type":"boolean","description":"Whether this package is produced internally by a workspace project."},{"name":"ecosystem","sourceName":"Ecosystem","type":"string","description":"The package ecosystem (e.g. nuget, npm, maven, pip, go)."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"archetype","sourceName":"Archetype","type":"enum (ProjectArchetype)","description":"The architectural archetype of the package (e.g. PureUtility, ClientUi, CliTool, Framework, BackendService, DatabaseDriver, NativeAddon)."},{"name":"dependency_scope","sourceName":"DependencyScope","type":"string","description":"Dependency scope (e.g. runtime, dev, test, build)."},{"name":"is_external","sourceName":"IsExternal","type":"boolean","description":"Whether this package is an external third-party dependency."}],"outEdges":[{"rel":"IMPLEMENTED_BY","toKind":"Project","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it."},{"rel":"PRODUCES","toKind":"Project","category":"type_dependency","description":"Indicates that a library project builds and produces a package."}],"inEdges":[{"rel":"DEPENDS_ON","fromKind":"Module","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"DEPENDS_ON","fromKind":"Project","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities."},{"rel":"PRODUCES","fromKind":"Project","category":"type_dependency","description":"Indicates that a library project builds and produces a package."}],"sampleCypher":"MATCH (n:Package)-[:IMPLEMENTED_BY]->(t:Project)\nRETURN n.name, n.version, n.is_internal, t.name\nLIMIT 25"},{"kind":"File","pluralLabel":"Files","layerId":1,"layerKey":"L1_physical","icon":"file-code","order":1,"idScheme":"{workspaceId}:f:{relativeFilePath}","purpose":"Represents a source code file containing parsable content.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the entity."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"is_test","sourceName":"IsTest","type":"boolean","description":"Indicates whether the file is part of a test suite or test project."},{"name":"project_kind","sourceName":"ProjectKind","type":"enum (ProjectEntityKind)","description":"The architectural kind of the enclosing project."},{"name":"project_name","sourceName":"ProjectName","type":"string","description":"The name of the enclosing project."},{"name":"line_count","sourceName":"LineCount","type":"integer","description":"Total lines of code in this file."},{"name":"size_bytes","sourceName":"SizeBytes","type":"integer","description":"Size of the file in bytes."},{"name":"artifact_role","sourceName":"ArtifactRole","type":"string","description":"The physical artifact role of the file (source, dist, minified, declaration, script)."}],"outEdges":[],"inEdges":[{"rel":"TRIGGERS","fromKind":"EntryPoint","category":"control_flow","description":"Links an entry point or API endpoint to the handler function it triggers."},{"rel":"CONTAINS","fromKind":"Folder","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"CONTAINS","fromKind":"Project","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"DECLARED_IN","fromKind":"CallArgument","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"CallLiteral","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"CatchClause","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"Cloud","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"Database","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"DbSchema","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"Endpoint","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"EntryPoint","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"ExternalService","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"Function","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"Member","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"Parameter","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"Query","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"Topic","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."},{"rel":"DECLARED_IN","fromKind":"Type","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file."}],"sampleCypher":"MATCH (s:EntryPoint)-[:TRIGGERS]->(n:File)\nRETURN s.name, n.name, n.is_test, n.project_kind\nLIMIT 25"},{"kind":"Folder","pluralLabel":"Folders","layerId":1,"layerKey":"L1_physical","icon":"folder","order":2,"idScheme":"{workspaceId}:dir:{relativeDirectoryPath}","purpose":"Represents a directory within the indexed workspace.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the folder."},{"name":"path","sourceName":"Path","type":"string","description":"The path to the folder."}],"outEdges":[{"rel":"CONTAINS","toKind":"File","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"CONTAINS","toKind":"Folder","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"CONTAINS","toKind":"GitSettings","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"inEdges":[{"rel":"CONTAINS","fromKind":"Folder","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."},{"rel":"LOCATED_IN","fromKind":"Project","category":"structural","description":"Links a project to the physical folder or files structure where it is located."}],"sampleCypher":"MATCH (n:Folder)-[:CONTAINS]->(t:File)\nRETURN n.name, n.path, t.name\nLIMIT 25"},{"kind":"GitSettings","pluralLabel":"Git Settings","layerId":1,"layerKey":"L1_physical","icon":"git-commit","order":3,"idScheme":"{workspaceId}:git","purpose":"Represents the Git repository configuration settings for the workspace.","properties":[{"name":"name","sourceName":"Name","type":"string","description":"The name of the entity."},{"name":"branch","sourceName":"Branch","type":"string","description":"The currently checked-out branch name."},{"name":"origin_url","sourceName":"OriginUrl","type":"string","description":"The remote origin repository URL."},{"name":"user_name","sourceName":"UserName","type":"string","description":"The git user name."},{"name":"user_email","sourceName":"UserEmail","type":"string","description":"The git user email address."},{"name":"path","sourceName":"Path","type":"string","description":"The path of the folder or file relative to its parent container."},{"name":"commit_hash","sourceName":"CommitHash","type":"string","description":"Current Git HEAD commit hash."},{"name":"head_commit","sourceName":"HeadCommit","type":"string","description":"Current Git HEAD commit hash alias."},{"name":"repo_name","sourceName":"RepoName","type":"string","description":"Git repository name."}],"outEdges":[],"inEdges":[{"rel":"USES_GIT","fromKind":"Project","category":"type_dependency","description":"Links the workspace to its repository configuration."},{"rel":"CONTAINS","fromKind":"Folder","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements."}],"sampleCypher":"MATCH (s:Project)-[:USES_GIT]->(n:GitSettings)\nRETURN s.name, n.name, n.branch, n.origin_url\nLIMIT 25"}],"relationships":[{"rel":"CALLED_BY","category":"control_flow","description":"Links a service, external service, or database to the function that invokes or queries it.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"ExternalService","toKind":"Function"},{"fromKind":"Service","toKind":"Function"}]},{"rel":"CALLS","category":"control_flow","description":"Links a calling function to the function it directly invokes.","properties":[{"name":"line","type":"integer","description":"1-indexed source line of the primary invocation site."},{"name":"call_count","type":"integer","description":"Aggregated number of call sites between caller and callee."},{"name":"in_loop","type":"boolean","description":"True when invoked inside a for/foreach/while/do loop (N+1 risk signal)."},{"name":"loop_depth","type":"integer","description":"Nesting depth of enclosing loops at the call site."},{"name":"is_conditional","type":"boolean","description":"True when guarded by an if/switch/ternary branch."},{"name":"in_try","type":"boolean","description":"True when enclosed inside a try/catch block."},{"name":"arg_flow","type":"string","description":"Parameter-to-argument dataflow slot mapping across the call boundary."}],"pairs":[{"fromKind":"Endpoint","toKind":"Function"},{"fromKind":"Function","toKind":"Function"},{"fromKind":"Function","toKind":"Type"},{"fromKind":"Type","toKind":"Function"}]},{"rel":"CALLS_ENDPOINT","category":"control_flow","description":"Links an external API call to the target HTTP endpoint it invokes.","properties":[{"name":"evidence_kind","type":"string","description":"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying call-site or endpoint URNs proving this link."},{"name":"hops","type":"integer","description":"Transitive call-chain hop count from workload root."}],"pairs":[{"fromKind":"ExternalService","toKind":"Endpoint"}]},{"rel":"EXPOSED_BY","category":"control_flow","description":"Links an entrypoint or endpoint to the type or function that exposes it.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Endpoint","toKind":"Function"},{"fromKind":"Endpoint","toKind":"Type"}]},{"rel":"EXPOSES","category":"control_flow","description":"Links a semantic workload (Service, App, Worker) to an endpoint or entrypoint it exposes.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Service","toKind":"Endpoint"}]},{"rel":"HAS_ENTRYPOINT","category":"control_flow","description":"Links an executable ProjectNode to its binary or process EntryPointNode.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Project","toKind":"EntryPoint"}]},{"rel":"IMPLEMENTS","category":"control_flow","description":"Links a class/struct declaration to the interface it implements.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Function","toKind":"EntryPoint"},{"fromKind":"Type","toKind":"Type"}]},{"rel":"INSTANTIATES","category":"control_flow","description":"Links a host component to a runtime UI or subsystem component it instantiates.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Function","toKind":"EntryPoint"},{"fromKind":"Function","toKind":"Function"},{"fromKind":"Function","toKind":"Type"}]},{"rel":"SERVICE_CALL","category":"control_flow","description":"Represents an HTTP, RPC, or message-based remote service invocation between services or to an external API.","properties":[{"name":"evidence_kind","type":"string","description":"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying call-site or endpoint URNs proving this link."},{"name":"hops","type":"integer","description":"Transitive call-chain hop count from workload root."}],"pairs":[{"fromKind":"Workload","toKind":"ExternalService"},{"fromKind":"Workload","toKind":"Service"}]},{"rel":"TRANSITIVELY_CALLS","category":"control_flow","description":"Links a downstream sink (query, database, external service, or cloud resource) back to functions that reach it transitively across the call graph.","properties":[{"name":"hops","type":"integer","description":"Shortest call-chain hop distance from the calling function to the sink."}],"pairs":[{"fromKind":"ExternalService","toKind":"Function"},{"fromKind":"Query","toKind":"Function"}]},{"rel":"TRIGGERS","category":"control_flow","description":"Links an entry point or API endpoint to the handler function it triggers.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Endpoint","toKind":"Function"},{"fromKind":"EntryPoint","toKind":"File"}]},{"rel":"ACCESSED_BY","category":"data_flow","description":"Links a Layer 4 resource (Database, Table, Query, ExternalService) downward to the Layer 3 function or type that accesses it.","properties":[{"name":"operation","type":"string","description":"Normalized data access mode (read, write, read_write, schema)."},{"name":"evidence_kind","type":"string","description":"Provenance source (query, orm, table, migration, config, library_call)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying UAST/Query node URNs proving this link."},{"name":"hops","type":"integer","description":"Call-chain distance when lifted transitively from helper libraries."}],"pairs":[{"fromKind":"ExternalService","toKind":"Function"},{"fromKind":"ExternalService","toKind":"Type"},{"fromKind":"Member","toKind":"Function"},{"fromKind":"Procedure","toKind":"Function"},{"fromKind":"Query","toKind":"Function"},{"fromKind":"Query","toKind":"Type"},{"fromKind":"Table","toKind":"Function"},{"fromKind":"Table","toKind":"Query"},{"fromKind":"Table","toKind":"Type"}]},{"rel":"ATTRIBUTED_TO","category":"data_flow","description":"Links an ingress endpoint or binary entry point to a downstream database, query, cloud resource, or external service reached along its call chain.","properties":[{"name":"hops","type":"integer","description":"Call-chain hop distance from the entrypoint or endpoint to the sink."},{"name":"sink_kind","type":"string","description":"Target sink classification (Database, Query, ExternalService, Topic)."}],"pairs":[{"fromKind":"Endpoint","toKind":"Database"},{"fromKind":"Endpoint","toKind":"ExternalService"},{"fromKind":"Endpoint","toKind":"Query"}]},{"rel":"FLOWS_TO","category":"data_flow","description":"Traces semantic dataflow between call arguments and parameters across intra- and inter-procedural hops.","properties":[{"name":"line","type":"integer","description":"1-indexed source line where the value transfer occurs."},{"name":"arg_index","type":"integer","description":"0-indexed target parameter or argument slot."}],"pairs":[{"fromKind":"CallArgument","toKind":"Parameter"},{"fromKind":"Parameter","toKind":"CallArgument"}]},{"rel":"HAS_PARAMETER","category":"data_flow","description":"Links a function or method to one of its semantically colored parameters.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Function","toKind":"Parameter"}]},{"rel":"PASSES_ARGUMENT","category":"data_flow","description":"Links a calling function or scope to a semantically colored call argument.","properties":[{"name":"line","type":"integer","description":"1-indexed source line where the value transfer occurs."},{"name":"arg_index","type":"integer","description":"0-indexed target parameter or argument slot."}],"pairs":[{"fromKind":"Function","toKind":"CallArgument"}]},{"rel":"PASSES_LITERAL","category":"data_flow","description":"Links a calling function or scope to a literal argument passed directly into an invocation.","properties":[{"name":"line","type":"integer","description":"1-indexed source line where the literal argument is passed."}],"pairs":[{"fromKind":"Endpoint","toKind":"CallLiteral"},{"fromKind":"Function","toKind":"CallLiteral"},{"fromKind":"Type","toKind":"CallLiteral"}]},{"rel":"PUBLISHED_BY","category":"data_flow","description":"Links a topic to the function that publishes to it.","properties":[{"name":"evidence_kind","type":"string","description":"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying call-site or endpoint URNs proving this link."},{"name":"hops","type":"integer","description":"Transitive call-chain hop count from workload root."}],"pairs":[{"fromKind":"Topic","toKind":"Function"}]},{"rel":"PUBLISHES_TO","category":"data_flow","description":"Links a service or function to a message queue or topic it publishes messages or events to.","properties":[{"name":"evidence_kind","type":"string","description":"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying call-site or endpoint URNs proving this link."},{"name":"hops","type":"integer","description":"Transitive call-chain hop count from workload root."}],"pairs":[{"fromKind":"Workload","toKind":"Topic"}]},{"rel":"READS_FROM","category":"data_flow","description":"Links a query or workload to the table or cloud resource it reads from.","properties":[{"name":"operation","type":"string","description":"Normalized data access mode (read, write, read_write, schema)."},{"name":"evidence_kind","type":"string","description":"Provenance source (query, orm, table, migration, config, library_call)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying UAST/Query node URNs proving this link."},{"name":"hops","type":"integer","description":"Call-chain distance when lifted transitively from helper libraries."}],"pairs":[{"fromKind":"Query","toKind":"Table"}]},{"rel":"SUBSCRIBED_BY","category":"data_flow","description":"Links a topic to the function that subscribes to it.","properties":[{"name":"evidence_kind","type":"string","description":"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying call-site or endpoint URNs proving this link."},{"name":"hops","type":"integer","description":"Transitive call-chain hop count from workload root."}],"pairs":[{"fromKind":"Topic","toKind":"Function"}]},{"rel":"SUBSCRIBES_TO","category":"data_flow","description":"Links a service or handler function to a message queue or topic it listens to or consumes events from.","properties":[{"name":"evidence_kind","type":"string","description":"Provenance source (http_call, grpc_call, publish, subscribe, config)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying call-site or endpoint URNs proving this link."},{"name":"hops","type":"integer","description":"Transitive call-chain hop count from workload root."}],"pairs":[{"fromKind":"Endpoint","toKind":"Topic"},{"fromKind":"Worker","toKind":"Topic"}]},{"rel":"TARGETED_BY","category":"data_flow","description":"Grounds a Layer 4 semantic resource (Table, Procedure, Endpoint, ExternalService, Topic, Database) downward to a Layer 3 CallArgument that targets it.","properties":[{"name":"line","type":"integer","description":"1-indexed source line where the value transfer occurs."},{"name":"arg_index","type":"integer","description":"0-indexed target parameter or argument slot."}],"pairs":[{"fromKind":"Database","toKind":"CallArgument"},{"fromKind":"Endpoint","toKind":"CallArgument"},{"fromKind":"ExternalService","toKind":"CallArgument"},{"fromKind":"Procedure","toKind":"CallArgument"},{"fromKind":"Table","toKind":"CallArgument"},{"fromKind":"Topic","toKind":"CallArgument"}]},{"rel":"USES_DB","category":"data_flow","description":"Links a project to a database catalog or instance it utilizes.","properties":[{"name":"operation","type":"string","description":"Normalized data access mode (read, write, read_write, schema)."},{"name":"evidence_kind","type":"string","description":"Provenance source (query, orm, table, migration, config, library_call)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying UAST/Query node URNs proving this link."},{"name":"hops","type":"integer","description":"Call-chain distance when lifted transitively from helper libraries."}],"pairs":[{"fromKind":"Workload","toKind":"Database"}]},{"rel":"WRITES_TO","category":"data_flow","description":"Links a query or workload to the table or cloud resource it writes to.","properties":[{"name":"operation","type":"string","description":"Normalized data access mode (read, write, read_write, schema)."},{"name":"evidence_kind","type":"string","description":"Provenance source (query, orm, table, migration, config, library_call)."},{"name":"evidence_ids","type":"string","description":"JSON array of underlying UAST/Query node URNs proving this link."},{"name":"hops","type":"integer","description":"Call-chain distance when lifted transitively from helper libraries."}],"pairs":[{"fromKind":"Query","toKind":"Table"}]},{"rel":"COORDINATES_WITH","category":"type_dependency","description":"Links a workload to the symmetric peer mesh it coordinates with.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Service","toKind":"PeerMesh"}]},{"rel":"DEPENDS_ON","category":"type_dependency","description":"Represents a dependency relationship between projects, packages, or other entities.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"BoundedContext","toKind":"BoundedContext"},{"fromKind":"Domain","toKind":"Domain"},{"fromKind":"Module","toKind":"Library"},{"fromKind":"Module","toKind":"Package"},{"fromKind":"Project","toKind":"Package"},{"fromKind":"Project","toKind":"Project"},{"fromKind":"Query","toKind":"Table"}]},{"rel":"HOSTED_IN","category":"type_dependency","description":"Links an external service, database, messaging broker, or runtime workload to the cloud platform or vendor hosting it.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source or config line where the cloud hosting binding is declared."}],"pairs":[{"fromKind":"Database","toKind":"Cloud"},{"fromKind":"ExternalService","toKind":"Cloud"},{"fromKind":"Topic","toKind":"Cloud"},{"fromKind":"Workload","toKind":"Cloud"}]},{"rel":"IMPLEMENTED_BY","category":"type_dependency","description":"Links a library package to the project that implements or encapsulates it.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Module","toKind":"Project"},{"fromKind":"Package","toKind":"Project"}]},{"rel":"INHERITS_FROM","category":"type_dependency","description":"Links a class/interface to its base class or inherited interface.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Type","toKind":"Type"}]},{"rel":"OF_TYPE","category":"type_dependency","description":"Links a member variable or field to its declared type.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Member","toKind":"Type"}]},{"rel":"POTENTIAL_TYPE","category":"type_dependency","description":"Links a variable or parameter to concrete classes that implement its declared interface type.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Type","toKind":"Type"}]},{"rel":"PRODUCES","category":"type_dependency","description":"Indicates that a library project builds and produces a package.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Package","toKind":"Project"},{"fromKind":"Project","toKind":"Package"}]},{"rel":"TESTS","category":"type_dependency","description":"Links a test suite to the runtime service, application, worker, CLI tool, or shared library it verifies.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"TestSuite","toKind":"Module"}]},{"rel":"USES_GIT","category":"type_dependency","description":"Links the workspace to its repository configuration.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Project","toKind":"GitSettings"}]},{"rel":"USES_TYPE","category":"type_dependency","description":"Links a calling function or type reference to the type it instantiates or references.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Function","toKind":"Type"},{"fromKind":"Type","toKind":"Type"}]},{"rel":"BELONGS_TO","category":"structural","description":"Links an entity to its parent project or container.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"EntryPoint","toKind":"Project"}]},{"rel":"CONTAINS","category":"structural","description":"Represents directory structure containment or syntactic scoping of elements.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"BoundedContext","toKind":"Database"},{"fromKind":"BoundedContext","toKind":"Module"},{"fromKind":"BoundedContext","toKind":"Topic"},{"fromKind":"Database","toKind":"DbSchema"},{"fromKind":"DbSchema","toKind":"Procedure"},{"fromKind":"DbSchema","toKind":"Table"},{"fromKind":"Domain","toKind":"BoundedContext"},{"fromKind":"Folder","toKind":"File"},{"fromKind":"Folder","toKind":"Folder"},{"fromKind":"Folder","toKind":"GitSettings"},{"fromKind":"Procedure","toKind":"Query"},{"fromKind":"Project","toKind":"EntryPoint"},{"fromKind":"Project","toKind":"File"},{"fromKind":"Project","toKind":"Project"}]},{"rel":"DECLARED_IN","category":"structural","description":"Links a syntactic type, function, or member declaration to its physical declaring source file.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"CallArgument","toKind":"File"},{"fromKind":"CallLiteral","toKind":"File"},{"fromKind":"CatchClause","toKind":"File"},{"fromKind":"Cloud","toKind":"File"},{"fromKind":"Database","toKind":"File"},{"fromKind":"DbSchema","toKind":"File"},{"fromKind":"Endpoint","toKind":"File"},{"fromKind":"Endpoint","toKind":"Project"},{"fromKind":"EntryPoint","toKind":"File"},{"fromKind":"ExternalService","toKind":"File"},{"fromKind":"ExternalService","toKind":"Project"},{"fromKind":"Function","toKind":"File"},{"fromKind":"Member","toKind":"File"},{"fromKind":"Parameter","toKind":"File"},{"fromKind":"Query","toKind":"File"},{"fromKind":"Topic","toKind":"File"},{"fromKind":"Type","toKind":"File"}]},{"rel":"DECLARES","category":"structural","description":"Indicates that a container entity declares a sub-entity.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Endpoint","toKind":"CatchClause"},{"fromKind":"Function","toKind":"CatchClause"},{"fromKind":"Function","toKind":"Function"},{"fromKind":"Function","toKind":"Member"},{"fromKind":"Function","toKind":"Type"},{"fromKind":"Type","toKind":"Type"}]},{"rel":"DEFINES","category":"structural","description":"Indicates that an entity defines a particular property or configuration.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"BoundedContext","toKind":"Type"}]},{"rel":"HAS_MEMBER","category":"structural","description":"Links a type declaration to its declared member variables or fields.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Type","toKind":"Member"}]},{"rel":"HAS_METHOD","category":"structural","description":"Links a type declaration to its declared methods or functions.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Type","toKind":"Function"}]},{"rel":"LOCATED_IN","category":"structural","description":"Links a project to the physical folder or files structure where it is located.","properties":[{"name":"line","type":"integer","description":"Optional 1-indexed source line where the relationship is declared."}],"pairs":[{"fromKind":"Project","toKind":"Folder"}]}]};
      var viewMode = "graph";
      var selectedKind = "Endpoint";
      var focusedKind = null;
      var inspectorExpanded = false;
      var hoveredKind = null;
      var hoveredCapsule = null;
      var searchQuery = "";
      var activeCats = {
        control_flow: true,
        data_flow: true,
        type_dependency: true,
        structural: true
      };
      var activeLayers = {
        5: true,
        4: true,
        3: true,
        2: true,
        1: true
      };
      var expandedHierarchies = {};
      for (var hi = 0; hi < SCHEMA.nodes.length; hi++) {
        if (SCHEMA.nodes[hi].subtypes && SCHEMA.nodes[hi].subtypes.length > 0) {
          expandedHierarchies[SCHEMA.nodes[hi].kind] = true;
        }
      }
      var lastVisKey = "";

      function cssToken(name) {
        return getComputedStyle(document.documentElement).getPropertyValue(name).trim();
      }

      var PAL = {
        canvasBg: cssToken("--ont-canvas-bg"),
        gridDot: cssToken("--ont-grid-dot"),
        logical: cssToken("--ont-c-logical"),
        module: cssToken("--ont-c-module"),
        workload: cssToken("--ont-c-workload"),
        contract: cssToken("--ont-c-contract"),
        data: cssToken("--ont-c-data"),
        cloud: cssToken("--ont-c-cloud"),
        syntax: cssToken("--ont-c-syntax"),
        slicing: cssToken("--ont-c-slicing"),
        project: cssToken("--ont-c-project"),
        physical: cssToken("--ont-c-physical"),
        capOuterFill: cssToken("--ont-capsule-outer-fill"),
        capOuterBorder: cssToken("--ont-capsule-outer-border"),
        capInnerFill: cssToken("--ont-capsule-inner-fill"),
        capInnerBorder: cssToken("--ont-capsule-inner-border"),
        capHoverBorder: cssToken("--ont-capsule-hover-border"),
        capHeadBg: cssToken("--ont-capsule-head-bg"),
        capHeadHoverBg: cssToken("--ont-capsule-head-hover-bg"),
        abstractRing: cssToken("--ont-abstract-ring"),
        badgeBg: cssToken("--ont-badge-bg"),
        badgeBorder: cssToken("--ont-badge-border"),
        pillBg: cssToken("--ont-pill-bg"),
        pillBorder: cssToken("--ont-pill-border"),
        pillBorderActive: cssToken("--ont-pill-border-active"),
        edgeIdle: cssToken("--ont-edge-idle"),
        edgeDim: cssToken("--ont-edge-dim"),
        edgeControl: cssToken("--ont-edge-control"),
        edgeData: cssToken("--ont-edge-data"),
        edgeType: cssToken("--ont-edge-type"),
        edgeStruct: cssToken("--ont-edge-struct"),
        nodeRing: cssToken("--ont-node-ring"),
        nodeHalo: cssToken("--ont-node-halo"),
        nodeHover: cssToken("--ont-node-hover"),
        inkBright: cssToken("--ont-ink-bright"),
        inkSoft: cssToken("--ont-ink-soft"),
        inkDim: cssToken("--ont-ink-dim"),
        inkZero: cssToken("--ont-ink-zero")
      };

      var STYLES = {
        Domain: { fill: PAL.logical, glyph: "globe", cluster: 8 },
        BoundedContext: { fill: PAL.logical, glyph: "layers", cluster: 8 },
        Module: { fill: PAL.module, glyph: "package", cluster: 0 },
        Workload: { fill: PAL.workload, glyph: "layers", cluster: 0 },
        App: { fill: PAL.workload, glyph: "browser", cluster: 0 },
        Service: { fill: PAL.workload, glyph: "server", cluster: 0 },
        Worker: { fill: PAL.workload, glyph: "gear", cluster: 0 },
        CliTool: { fill: PAL.workload, glyph: "terminal", cluster: 0 },
        Library: { fill: PAL.module, glyph: "library", cluster: 0 },
        TestSuite: { fill: PAL.module, glyph: "beaker", cluster: 0 },
        Endpoint: { fill: PAL.contract, glyph: "endpoint", cluster: 1 },
        Topic: { fill: PAL.contract, glyph: "topic", cluster: 1 },
        PeerMesh: { fill: PAL.contract, glyph: "mesh", cluster: 1 },
        Database: { fill: PAL.data, glyph: "database", cluster: 2 },
        DbSchema: { fill: PAL.data, glyph: "schema", cluster: 2 },
        Table: { fill: PAL.data, glyph: "table", cluster: 2 },
        Query: { fill: PAL.data, glyph: "query", cluster: 2 },
        Procedure: { fill: PAL.data, glyph: "procedure", cluster: 2 },
        ExternalService: { fill: PAL.cloud, glyph: "cloud", cluster: 3 },
        Cloud: { fill: PAL.cloud, glyph: "cloudUp", cluster: 3 },
        Type: { fill: PAL.syntax, glyph: "type", cluster: 4 },
        Function: { fill: PAL.syntax, glyph: "function", cluster: 4 },
        Member: { fill: PAL.syntax, glyph: "member", cluster: 4 },
        CatchClause: { fill: PAL.syntax, glyph: "catch", cluster: 4 },
        Parameter: { fill: PAL.slicing, glyph: "param", cluster: 5 },
        CallArgument: { fill: PAL.slicing, glyph: "arg", cluster: 5 },
        CallLiteral: { fill: PAL.slicing, glyph: "literal", cluster: 5 },
        Project: { fill: PAL.project, glyph: "project", cluster: 6 },
        EntryPoint: { fill: PAL.project, glyph: "zap", cluster: 6 },
        Package: { fill: PAL.project, glyph: "package", cluster: 6 },
        File: { fill: PAL.physical, glyph: "file", cluster: 7 },
        Folder: { fill: PAL.physical, glyph: "folder", cluster: 7 },
        GitSettings: { fill: PAL.physical, glyph: "git", cluster: 7 }
      };

      var ANCHORS = [
        { x: 255, y: 235, rx: 145, ry: 105 },
        { x: 635, y: 255, rx: 145, ry: 85 },
        { x: 1125, y: 285, rx: 135, ry: 110 },
        { x: 925, y: 155, rx: 105, ry: 60 },
        { x: 705, y: 495, rx: 165, ry: 115 },
        { x: 1065, y: 575, rx: 125, ry: 95 },
        { x: 305, y: 605, rx: 130, ry: 90 },
        { x: 645, y: 695, rx: 155, ry: 55 },
        { x: 595, y: 115, rx: 85, ry: 24 }
      ];

      var TREE_ANCHORS = {
        App: { x: 115, y: 145 },
        CliTool: { x: 115, y: 265 },
        Workload: { x: 265, y: 215 },
        Service: { x: 405, y: 150 },
        Worker: { x: 405, y: 270 },
        Module: { x: 295, y: 385 },
        Library: { x: 135, y: 385 },
        TestSuite: { x: 155, y: 495 }
      };

      var SUB_HUBS = { App: 1, Worker: 1, CliTool: 1, Library: 1, TestSuite: 1 };
      var ACTIVE_EDGE_COLORS = {
        control_flow: PAL.edgeControl,
        data_flow: PAL.edgeData,
        type_dependency: PAL.edgeType,
        structural: PAL.edgeStruct
      };

      var nodeMap = {};
      for (var i = 0; i < SCHEMA.nodes.length; i++) {
        nodeMap[SCHEMA.nodes[i].kind] = SCHEMA.nodes[i];
      }

      var relMap = {};
      for (var j = 0; j < SCHEMA.relationships.length; j++) {
        relMap[SCHEMA.relationships[j].rel] = SCHEMA.relationships[j];
      }

      function collectDescendants(kind, visited) {
        var nd = nodeMap[kind];
        if (!nd || !nd.subtypes || nd.subtypes.length === 0) return [];
        var acc = [];
        for (var i = 0; i < nd.subtypes.length; i++) {
          var sub = nd.subtypes[i];
          if (visited[sub]) continue;
          visited[sub] = true;
          acc.push(sub);
          var deep = collectDescendants(sub, visited);
          for (var d = 0; d < deep.length; d++) acc.push(deep[d]);
        }
        return acc;
      }

      var hierarchies = [];
      for (var hc = 0; hc < SCHEMA.nodes.length; hc++) {
        var hNode = SCHEMA.nodes[hc];
        if (hNode.subtypes && hNode.subtypes.length > 0) {
          hierarchies.push({
            kind: hNode.kind,
            depth: hNode.ancestors ? hNode.ancestors.length : 0,
            allDescendants: collectDescendants(hNode.kind, {})
          });
        }
      }
      hierarchies.sort(function (a, b) {
        return a.depth - b.depth || (a.kind < b.kind ? -1 : 1);
      });

      function resolveHierarchy(filteredDtos) {
        var filteredSet = {};
        for (var i = 0; i < filteredDtos.length; i++) {
          filteredSet[filteredDtos[i].kind] = true;
        }
        var hiddenKinds = {};
        var redirectMap = {};
        var collapsedCounts = {};

        for (var h = 0; h < hierarchies.length; h++) {
          var hd = hierarchies[h];
          if (expandedHierarchies[hd.kind]) continue;
          if (!filteredSet[hd.kind] || hiddenKinds[hd.kind]) continue;
          var cnt = 0;
          for (var d = 0; d < hd.allDescendants.length; d++) {
            var desc = hd.allDescendants[d];
            if (filteredSet[desc] && !hiddenKinds[desc]) {
              hiddenKinds[desc] = true;
              cnt++;
            }
            if (!redirectMap[desc]) redirectMap[desc] = hd.kind;
          }
          if (cnt > 0) collapsedCounts[hd.kind] = cnt;
        }

        var visibleDtos = [];
        for (var v = 0; v < filteredDtos.length; v++) {
          if (!hiddenKinds[filteredDtos[v].kind]) visibleDtos.push(filteredDtos[v]);
        }
        return {
          visibleDtos: visibleDtos,
          redirectMap: redirectMap,
          collapsedCounts: collapsedCounts
        };
      }

      function escHtml(s) {
        return String(s || "")
          .replace(/&/g, "&amp;")
          .replace(/</g, "&lt;")
          .replace(/>/g, "&gt;")
          .replace(/"/g, "&quot;");
      }

      function matchesQuery(node, q) {
        if (!q) return true;
        if (
          node.kind.toLowerCase().indexOf(q) !== -1 ||
          node.pluralLabel.toLowerCase().indexOf(q) !== -1 ||
          node.purpose.toLowerCase().indexOf(q) !== -1 ||
          node.idScheme.toLowerCase().indexOf(q) !== -1 ||
          (node.extends && node.extends.toLowerCase().indexOf(q) !== -1)
        ) {
          return true;
        }
        if (node.subtypes) {
          for (var st = 0; st < node.subtypes.length; st++) {
            if (node.subtypes[st].toLowerCase().indexOf(q) !== -1) return true;
          }
        }
        for (var i = 0; i < node.properties.length; i++) {
          var p = node.properties[i];
          if (p.name.toLowerCase().indexOf(q) !== -1 || p.description.toLowerCase().indexOf(q) !== -1) return true;
        }
        var outs = node.inheritedOutEdges ? node.outEdges.concat(node.inheritedOutEdges) : node.outEdges;
        for (var o = 0; o < outs.length; o++) {
          var oe = outs[o];
          if (oe.rel.toLowerCase().indexOf(q) !== -1 || oe.toKind.toLowerCase().indexOf(q) !== -1) return true;
        }
        var ins = node.inheritedInEdges ? node.inEdges.concat(node.inheritedInEdges) : node.inEdges;
        for (var k = 0; k < ins.length; k++) {
          var ie = ins[k];
          if (ie.rel.toLowerCase().indexOf(q) !== -1 || ie.fromKind.toLowerCase().indexOf(q) !== -1) return true;
        }
        return false;
      }

      var WORLD_CX = 690;
      var WORLD_CY = 440;
      var ALPHA_DECAY = 0.0228;
      var ALPHA_MIN = 0.001;
      var VEL_RETENTION = 0.6;
      var simBodies = [];
      var simByKind = {};
      var simSprings = [];
      var spacingFactor = 1.0;
      var hubFactor = 1.0;
      var simAlpha = 0;
      var simAlphaTarget = 0;
      var simFrozen = false;
      var simRafId = null;
      var lastSimSig = "";

      try {
        var savedAir = localStorage.getItem("ce_ontology_air");
        if (savedAir !== null) {
          var parsedAir = parseFloat(savedAir);
          if (!isNaN(parsedAir) && parsedAir >= 0.1 && parsedAir <= 2.4) {
            spacingFactor = parsedAir;
          }
        }
        var savedHub = localStorage.getItem("ce_ontology_hub");
        if (savedHub !== null) {
          var parsedHub = parseFloat(savedHub);
          if (!isNaN(parsedHub) && parsedHub >= 0.0 && parsedHub <= 5.0) {
            hubFactor = parsedHub;
          }
        }
      } catch (_err) {}

      function computeSeedMap(visibleDtos) {
        var buckets = {};
        for (var i = 0; i < visibleDtos.length; i++) {
          var n = visibleDtos[i];
          var st = STYLES[n.kind] || { fill: PAL.physical, glyph: "file", cluster: 7 };
          if (!buckets[st.cluster]) buckets[st.cluster] = [];
          buckets[st.cluster].push(n);
        }
        var seeds = {};
        for (var cid in buckets) {
          var list = buckets[cid];
          var anc = ANCHORS[cid] || ANCHORS[0];
          for (var idx = 0; idx < list.length; idx++) {
            var dto = list[idx];
            var style = STYLES[dto.kind] || { fill: PAL.physical, glyph: "file", cluster: 7 };
            var treePos = Number(cid) === 0 && list.length > 1 ? TREE_ANCHORS[dto.kind] : null;
            var ang = list.length === 1 ? 0 : (idx / list.length) * Math.PI * 2 - Math.PI / 2;
            var ax = treePos ? treePos.x : anc.x + (list.length === 1 ? 0 : Math.cos(ang) * anc.rx);
            var ay = treePos ? treePos.y : anc.y + (list.length === 1 ? 0 : Math.sin(ang) * anc.ry);
            seeds[dto.kind] = {
              ax: ax,
              ay: ay,
              style: style,
              anchorStrength: treePos ? 0.072 : 0.048
            };
          }
        }
        return seeds;
      }

      function buildSimSprings(byKindMap, redirectMap) {
        var rawPairs = [];
        var seen = {};
        var counts = {};

        function addPair(fk, tk, isExt) {
          if (fk === tk) return;
          var ba = byKindMap[fk];
          var bb = byKindMap[tk];
          if (!ba || !bb) return;
          var minK = fk < tk ? fk : tk;
          var maxK = fk < tk ? tk : fk;
          var pk = minK + "::" + maxK;
          if (seen[pk]) return;
          seen[pk] = true;
          rawPairs.push({ a: ba, b: bb, isExt: isExt });
          counts[fk] = (counts[fk] || 0) + 1;
          counts[tk] = (counts[tk] || 0) + 1;
        }

        for (var ex = 0; ex < SCHEMA.nodes.length; ex++) {
          var exNode = SCHEMA.nodes[ex];
          if (exNode.extends) addPair(exNode.kind, exNode.extends, true);
        }
        for (var r = 0; r < SCHEMA.relationships.length; r++) {
          var rd = SCHEMA.relationships[r];
          if (!activeCats[rd.category]) continue;
          for (var p = 0; p < rd.pairs.length; p++) {
            var pr = rd.pairs[p];
            var ef = (redirectMap && redirectMap[pr.fromKind]) || pr.fromKind;
            var et = (redirectMap && redirectMap[pr.toKind]) || pr.toKind;
            addPair(ef, et, false);
          }
        }

        for (var bk in byKindMap) {
          byKindMap[bk].degree = Math.max(1, counts[bk] || 1);
        }

        var springs = [];
        for (var s = 0; s < rawPairs.length; s++) {
          var rp = rawPairs[s];
          var ca = rp.a.degree || 1;
          var cb = rp.b.degree || 1;
          var bias = Math.max(0.22, Math.min(0.78, ca / (ca + cb)));
          var minDeg = Math.max(1, Math.min(ca, cb));
          var baseStr = rp.isExt ? 0.54 : 0.48 / Math.sqrt(minDeg);
          var str = Math.max(0.16, Math.min(0.56, baseStr));
          var sameCl = rp.a.style.cluster === rp.b.style.cluster;
          var dist = rp.isExt ? 126 : (sameCl ? 144 : 212);
          springs.push({ source: rp.a, target: rp.b, distance: dist, strength: str, bias: bias });
        }
        return springs;
      }

      function stepSimBodies(bodies, springs, alpha, spFactor, hbFactor) {
        var hf = typeof hbFactor === "number" ? hbFactor : 1.0;
        var maxChargeD2 = Math.pow(540 * spFactor * (1 + hf * 0.25), 2);
        var charge = -620 * spFactor * alpha;

        for (var a = 0; a < bodies.length; a++) {
          var b1 = bodies[a];
          var d1 = b1.degree || 1;
          var w1 = 1 + hf * 0.24 * Math.max(0, d1 - 2);
          for (var c = a + 1; c < bodies.length; c++) {
            var b2 = bodies[c];
            var d2Deg = b2.degree || 1;
            var dx = b2.x - b1.x;
            var dy = b2.y - b1.y;
            var d2 = dx * dx + dy * dy;
            if (d2 > maxChargeD2) continue;
            if (d2 < 1) {
              dx = a % 2 === 0 ? 1 : -1;
              dy = c % 2 === 0 ? 0.5 : -0.5;
              d2 = 1.25;
            }
            var w2 = 1 + hf * 0.24 * Math.max(0, d2Deg - 2);
            var pairHubMult = 0.5 * (w1 + w2) * (1 + hf * 0.08 * Math.min(d1, d2Deg));
            var w = (charge * pairHubMult) / Math.max(d2, 900);
            if (b1.fx === null) { b1.vx += dx * w; b1.vy += dy * w; }
            if (b2.fx === null) { b2.vx -= dx * w; b2.vy -= dy * w; }
          }
        }

        for (var l = 0; l < springs.length; l++) {
          var sp = springs[l];
          var ldx = sp.target.x + sp.target.vx - sp.source.x - sp.source.vx;
          var ldy = sp.target.y + sp.target.vy - sp.source.y - sp.source.vy;
          var ldist = Math.sqrt(ldx * ldx + ldy * ldy) || 1;
          var degEx = Math.max(0, (sp.source.degree || 1) + (sp.target.degree || 1) - 4);
          var hubDistM = 1 + hf * 0.042 * degEx;
          var hubStrM = 1 / (1 + hf * 0.055 * degEx);
          var tDist = sp.distance * spFactor * hubDistM;
          var k = ((ldist - tDist) / ldist) * alpha * sp.strength * hubStrM;
          ldx *= k;
          ldy *= k;
          if (sp.target.fx === null) {
            sp.target.vx -= ldx * sp.bias;
            sp.target.vy -= ldy * sp.bias;
          }
          if (sp.source.fx === null) {
            sp.source.vx += ldx * (1 - sp.bias);
            sp.source.vy += ldy * (1 - sp.bias);
          }
        }

        for (var u = 0; u < bodies.length; u++) {
          var bd = bodies[u];
          if (bd.fx !== null) continue;
          var tax = WORLD_CX + (bd.ax - WORLD_CX) * spFactor;
          var tay = WORLD_CY + (bd.ay - WORLD_CY) * spFactor;
          var effAnc = bd.anchorStrength / (1 + hf * 0.35 + hf * 0.06 * Math.max(0, (bd.degree || 1) - 2));
          bd.vx += (tax - bd.x) * effAnc * alpha;
          bd.vy += (tay - bd.y) * effAnc * alpha;
        }

        var baseHalfRad = 26 * Math.min(spFactor, 2.2);
        for (var pass = 0; pass < 2; pass++) {
          for (var i = 0; i < bodies.length; i++) {
            var ci = bodies[i];
            var ri = baseHalfRad * (1 + hf * 0.035 * Math.max(0, (ci.degree || 1) - 2));
            for (var j = i + 1; j < bodies.length; j++) {
              var cj = bodies[j];
              var rj = baseHalfRad * (1 + hf * 0.035 * Math.max(0, (cj.degree || 1) - 2));
              var minSep = ri + rj;
              var cdx = (cj.x + cj.vx) - (ci.x + ci.vx);
              var cdy = (cj.y + cj.vy) - (ci.y + ci.vy);
              var cd2 = cdx * cdx + cdy * cdy;
              if (cd2 >= minSep * minSep) continue;
              if (cd2 < 0.01) { cdx = 0.5; cdy = 0.5; cd2 = 0.5; }
              var cd = Math.sqrt(cd2);
              var ov = ((minSep - cd) / cd) * 0.42;
              var ox = cdx * ov;
              var oy = cdy * ov;
              if (ci.fx === null && cj.fx === null) {
                ci.vx -= ox; ci.vy -= oy;
                cj.vx += ox; cj.vy += oy;
              } else if (ci.fx !== null && cj.fx === null) {
                cj.vx += ox * 2; cj.vy += oy * 2;
              } else if (cj.fx !== null && ci.fx === null) {
                ci.vx -= ox * 2; ci.vy -= oy * 2;
              }
            }
          }
        }

        for (var m = 0; m < bodies.length; m++) {
          var mb = bodies[m];
          if (mb.fx !== null && mb.fy !== null) {
            mb.x = mb.fx;
            mb.y = mb.fy;
            mb.vx = 0;
            mb.vy = 0;
            continue;
          }
          mb.vx *= VEL_RETENTION;
          mb.vy *= VEL_RETENTION;
          var msp = Math.sqrt(mb.vx * mb.vx + mb.vy * mb.vy);
          if (msp > 28) { mb.vx = (mb.vx / msp) * 28; mb.vy = (mb.vy / msp) * 28; }
          mb.x += mb.vx;
          mb.y += mb.vy;
        }
      }

      function syncGraphNodesFromBodies(collapsedCounts) {
        graphNodes = [];
        graphNodeMap = {};
        for (var m = 0; m < simBodies.length; m++) {
          var item = simBodies[m];
          var hasExpSub = false;
          if (item.dto.subtypes) {
            for (var sIdx = 0; sIdx < item.dto.subtypes.length; sIdx++) {
              if (simByKind[item.dto.subtypes[sIdx]]) { hasExpSub = true; break; }
            }
          }
          var gn = {
            kind: item.dto.kind,
            x: item.x,
            y: item.y,
            ax: item.ax,
            ay: item.ay,
            radius: 25,
            fill: item.style.fill,
            glyph: item.style.glyph,
            isAbstract: Boolean(item.dto.isAbstract),
            extendsKind: item.dto.extends || null,
            collapsedCount: collapsedCounts && collapsedCounts[item.dto.kind] ? collapsedCounts[item.dto.kind] : 0,
            hasExpandedSubtypes: hasExpSub
          };
          graphNodes.push(gn);
          graphNodeMap[gn.kind] = gn;
        }
      }

      var lastCollapsedCounts = {};

      function syncSimulation(visibleDtos, redirectMap, collapsedCounts) {
        lastCollapsedCounts = collapsedCounts || {};
        var isInitial = simBodies.length === 0;
        var prevByKind = simByKind;
        var seeds = computeSeedMap(visibleDtos);
        var nextBodies = [];
        var nextByKind = {};
        var kindList = [];

        for (var i = 0; i < visibleDtos.length; i++) {
          var dto = visibleDtos[i];
          kindList.push(dto.kind);
          var seed = seeds[dto.kind];
          var existing = prevByKind[dto.kind];
          if (existing) {
            existing.dto = dto;
            existing.style = seed.style;
            existing.ax = seed.ax;
            existing.ay = seed.ay;
            existing.anchorStrength = seed.anchorStrength;
            nextBodies.push(existing);
            nextByKind[dto.kind] = existing;
            continue;
          }
          var sax = WORLD_CX + (seed.ax - WORLD_CX) * spacingFactor;
          var say = WORLD_CY + (seed.ay - WORLD_CY) * spacingFactor;
          var sx = sax;
          var sy = say;
          if (!isInitial && dto.extends) {
            var pBody = prevByKind[dto.extends] || nextByKind[dto.extends];
            if (pBody) {
              sx = pBody.x + (sax - pBody.x) * 0.32;
              sy = pBody.y + (say - pBody.y) * 0.32;
            }
          }
          var nb = {
            dto: dto,
            style: seed.style,
            x: sx,
            y: sy,
            ax: seed.ax,
            ay: seed.ay,
            vx: 0,
            vy: 0,
            fx: null,
            fy: null,
            anchorStrength: seed.anchorStrength
          };
          nextBodies.push(nb);
          nextByKind[dto.kind] = nb;
        }

        simBodies = nextBodies;
        simByKind = nextByKind;
        simSprings = buildSimSprings(nextByKind, redirectMap);

        var catParts = [];
        for (var ck in activeCats) {
          if (activeCats[ck]) catParts.push(ck);
        }
        var sig = kindList.join(",") + "|" + catParts.join(",");

        if (isInitial) {
          var warmA = 1.0;
          for (var step = 0; step < 220; step++) {
            warmA += (0 - warmA) * ALPHA_DECAY;
            stepSimBodies(simBodies, simSprings, warmA, spacingFactor, hubFactor);
          }
          for (var z = 0; z < simBodies.length; z++) {
            simBodies[z].vx = 0;
            simBodies[z].vy = 0;
          }
          simAlpha = 0;
        } else if (sig !== lastSimSig && !simFrozen) {
          simAlpha = Math.max(simAlpha, 0.42);
        }
        lastSimSig = sig;
        syncGraphNodesFromBodies(lastCollapsedCounts);
      }

      function resetSimLayout() {
        for (var i = 0; i < simBodies.length; i++) {
          var b = simBodies[i];
          b.x = WORLD_CX + (b.ax - WORLD_CX) * spacingFactor;
          b.y = WORLD_CY + (b.ay - WORLD_CY) * spacingFactor;
          b.vx = 0;
          b.vy = 0;
          b.fx = null;
          b.fy = null;
        }
        var warmA = 1.0;
        for (var step = 0; step < 220; step++) {
          warmA += (0 - warmA) * ALPHA_DECAY;
          stepSimBodies(simBodies, simSprings, warmA, spacingFactor, hubFactor);
        }
        for (var z = 0; z < simBodies.length; z++) {
          simBodies[z].vx = 0;
          simBodies[z].vy = 0;
        }
        simAlpha = 0;
        simAlphaTarget = 0;
        syncGraphNodesFromBodies(lastCollapsedCounts);
      }

      function computeCapsules() {
        return [];
      }

      function computeEdges(visibleSet, redirectMap) {
        var dedup = {};
        var raw = [];
        for (var en = 0; en < SCHEMA.nodes.length; en++) {
          var ndDef = SCHEMA.nodes[en];
          if (!ndDef.extends) continue;
          if (!visibleSet[ndDef.kind] || !visibleSet[ndDef.extends]) continue;
          var extRk = ndDef.kind + "|EXTENDS|" + ndDef.extends;
          var extEntry = {
            fromKind: ndDef.kind,
            toKind: ndDef.extends,
            rel: "EXTENDS",
            category: "type_dependency",
            isExtends: true,
            priority: 1
          };
          dedup[extRk] = extEntry;
          raw.push(extEntry);
        }
        for (var r = 0; r < SCHEMA.relationships.length; r++) {
          var rd = SCHEMA.relationships[r];
          if (!activeCats[rd.category]) continue;
          for (var p = 0; p < rd.pairs.length; p++) {
            var pair = rd.pairs[p];
            var effFrom = (redirectMap && redirectMap[pair.fromKind]) || pair.fromKind;
            var effTo = (redirectMap && redirectMap[pair.toKind]) || pair.toKind;
            if (!visibleSet[effFrom] || !visibleSet[effTo]) continue;
            if (effFrom === effTo && pair.fromKind !== pair.toKind) continue;
            var prio = 1;
            if (rd.rel === "DECLARED_IN" || rd.rel === "BELONGS_TO") prio = 4;
            else if (SUB_HUBS[effFrom] || SUB_HUBS[effTo]) prio = 3;
            else if (effFrom === "Service" || effTo === "Service" || effFrom === "Workload" || effTo === "Workload" || rd.category === "structural") prio = 2;
            var rk = effFrom + "|" + rd.rel + "|" + effTo;
            if (dedup[rk]) {
              if (prio < dedup[rk].priority) dedup[rk].priority = prio;
            } else {
              var entry = {
                fromKind: effFrom,
                toKind: effTo,
                rel: rd.rel,
                category: rd.category,
                priority: prio
              };
              dedup[rk] = entry;
              raw.push(entry);
            }
          }
        }

        var selfSlots = {};
        var groups = {};
        var edges = [];
        for (var i = 0; i < raw.length; i++) {
          var e = raw[i];
          if (e.fromKind === e.toKind) {
            var sl = selfSlots[e.fromKind] || 0;
            selfSlots[e.fromKind] = sl + 1;
            edges.push({
              fromKind: e.fromKind,
              toKind: e.toKind,
              rel: e.rel,
              category: e.category,
              isSelf: true,
              slot: sl,
              offset: 0,
              priority: e.priority
            });
          } else {
            var minK = e.fromKind < e.toKind ? e.fromKind : e.toKind;
            var maxK = e.fromKind < e.toKind ? e.toKind : e.fromKind;
            var gk = minK + "::" + maxK;
            if (!groups[gk]) groups[gk] = [];
            groups[gk].push(e);
          }
        }

        for (var gkKey in groups) {
          var grp = groups[gkKey];
          for (var g = 0; g < grp.length; g++) {
            var ge = grp[g];
            var off = 22;
            if (grp.length > 1) {
              off = (g - (grp.length - 1) / 2) * 34;
              if (Math.abs(off) < 6) off = 14;
            }
            edges.push({
              fromKind: ge.fromKind,
              toKind: ge.toKind,
              rel: ge.rel,
              category: ge.category,
              isExtends: Boolean(ge.isExtends),
              isSelf: false,
              slot: 0,
              offset: off,
              priority: ge.priority
            });
          }
        }
        edges.sort(function (a, b) { return a.priority - b.priority; });
        return edges;
      }

      var customPos = {};
      var graphNodes = [];
      var graphNodeMap = {};
      var graphCapsules = [];
      var graphEdges = [];
      var viewport = { panX: 0, panY: 0, zoom: 0.72 };
      var hasInitialFit = false;
      var autoFitEnabled = true;
      var lastFrameW = 0;
      var lastFrameH = 0;
      var dragState = null;

      function roundRect(ctx, x, y, w, h, r) {
        var rad = Math.min(r, w / 2, h / 2);
        ctx.beginPath();
        ctx.moveTo(x + rad, y);
        ctx.arcTo(x + w, y, x + w, y + h, rad);
        ctx.arcTo(x + w, y + h, x, y + h, rad);
        ctx.arcTo(x, y + h, x, y, rad);
        ctx.arcTo(x, y, x + w, y, rad);
        ctx.closePath();
      }

      function drawGlyph(ctx, glyph, x, y) {
        ctx.save();
        ctx.translate(x, y);
        ctx.strokeStyle = PAL.inkBright;
        ctx.fillStyle = PAL.inkBright;
        ctx.lineWidth = 1.65;
        ctx.lineCap = "round";
        ctx.lineJoin = "round";

        if (glyph === "globe") {
          ctx.beginPath(); ctx.arc(0, 0, 6.8, 0, Math.PI * 2); ctx.stroke();
          ctx.beginPath(); ctx.ellipse(0, 0, 3.1, 6.8, 0, 0, Math.PI * 2); ctx.stroke();
          ctx.beginPath(); ctx.moveTo(-6.6, 0); ctx.lineTo(6.6, 0); ctx.stroke();
        } else if (glyph === "database" || glyph === "procedure") {
          ctx.beginPath();
          ctx.ellipse(0, -5, 6.5, 2.6, 0, 0, Math.PI * 2);
          ctx.stroke();
          ctx.beginPath();
          ctx.moveTo(-6.5, -5);
          ctx.lineTo(-6.5, 5);
          ctx.arc(0, 5, 6.5, Math.PI, 0, true);
          ctx.lineTo(6.5, -5);
          ctx.stroke();
        } else if (glyph === "schema") {
          roundRect(ctx, -6.5, -6, 13, 4.5, 1.5); ctx.stroke();
          roundRect(ctx, -6.5, 1.5, 5.2, 4.5, 1.2); ctx.stroke();
          roundRect(ctx, 1.3, 1.5, 5.2, 4.5, 1.2); ctx.stroke();
          ctx.beginPath();
          ctx.moveTo(0, -1.5); ctx.lineTo(0, 0);
          ctx.moveTo(-3.9, 0); ctx.lineTo(3.9, 0);
          ctx.moveTo(-3.9, 0); ctx.lineTo(-3.9, 1.5);
          ctx.moveTo(3.9, 0); ctx.lineTo(3.9, 1.5);
          ctx.stroke();
        } else if (glyph === "layers") {
          ctx.beginPath();
          ctx.moveTo(0, -6.5); ctx.lineTo(7, -3); ctx.lineTo(0, 0.5); ctx.lineTo(-7, -3);
          ctx.closePath(); ctx.stroke();
          ctx.beginPath();
          ctx.moveTo(-7, 0.8); ctx.lineTo(0, 4.2); ctx.lineTo(7, 0.8);
          ctx.moveTo(-7, 4.2); ctx.lineTo(0, 7.2); ctx.lineTo(7, 4.2);
          ctx.stroke();
        } else if (glyph === "table") {
          roundRect(ctx, -7, -6, 14, 12, 2);
          ctx.stroke();
          ctx.beginPath();
          ctx.moveTo(-7, -1.5); ctx.lineTo(7, -1.5);
          ctx.moveTo(-1.5, -1.5); ctx.lineTo(-1.5, 6);
          ctx.stroke();
        } else if (glyph === "query") {
          ctx.beginPath();
          ctx.arc(-1.5, -1.5, 5, 0, Math.PI * 2);
          ctx.moveTo(2.2, 2.2); ctx.lineTo(6.8, 6.8);
          ctx.stroke();
        } else if (glyph === "endpoint") {
          ctx.beginPath(); ctx.arc(0, 0, 2.2, 0, Math.PI * 2); ctx.fill();
          ctx.beginPath(); ctx.arc(0, 0, 5.5, -Math.PI * 0.7, Math.PI * 0.7); ctx.stroke();
          ctx.beginPath(); ctx.arc(0, 0, 5.5, Math.PI * 0.3, Math.PI * 1.7); ctx.stroke();
        } else if (glyph === "topic") {
          roundRect(ctx, -7, -5, 14, 10, 2); ctx.stroke();
          ctx.beginPath(); ctx.moveTo(-6.5, -4.2); ctx.lineTo(0, 1); ctx.lineTo(6.5, -4.2); ctx.stroke();
        } else if (glyph === "cloud" || glyph === "cloudUp") {
          ctx.beginPath();
          ctx.arc(-2.5, 1, 3.8, Math.PI * 0.5, Math.PI * 1.5);
          ctx.arc(1, -1.5, 4.2, Math.PI, Math.PI * 1.95);
          ctx.arc(4.2, 1.5, 3.2, Math.PI * 1.5, Math.PI * 0.5);
          ctx.closePath(); ctx.stroke();
        } else if (glyph === "function") {
          ctx.beginPath();
          ctx.moveTo(2.5, -6.5); ctx.quadraticCurveTo(-1, -6.5, -1, -2); ctx.lineTo(-1, 6.5);
          ctx.moveTo(-4.5, -1); ctx.lineTo(3, -1);
          ctx.stroke();
        } else if (glyph === "type") {
          ctx.beginPath();
          ctx.moveTo(0, -7); ctx.lineTo(6.5, -3.5); ctx.lineTo(6.5, 3.5);
          ctx.lineTo(0, 7); ctx.lineTo(-6.5, 3.5); ctx.lineTo(-6.5, -3.5);
          ctx.closePath(); ctx.stroke();
        } else if (glyph === "param" || glyph === "arg" || glyph === "literal") {
          ctx.beginPath();
          ctx.moveTo(-4, -5.5); ctx.lineTo(-7, 0); ctx.lineTo(-4, 5.5);
          ctx.moveTo(4, -5.5); ctx.lineTo(7, 0); ctx.lineTo(4, 5.5);
          if (glyph !== "param") { ctx.moveTo(-1.5, 0); ctx.lineTo(1.5, 0); }
          ctx.stroke();
        } else if (glyph === "folder") {
          ctx.beginPath();
          ctx.moveTo(-7, -5); ctx.lineTo(-2, -5); ctx.lineTo(0, -3);
          ctx.lineTo(7, -3); ctx.lineTo(7, 5.5); ctx.lineTo(-7, 5.5);
          ctx.closePath(); ctx.stroke();
        } else if (glyph === "file") {
          ctx.beginPath();
          ctx.moveTo(-5, -6.5); ctx.lineTo(2, -6.5); ctx.lineTo(5.5, -3);
          ctx.lineTo(5.5, 6.5); ctx.lineTo(-5, 6.5);
          ctx.closePath(); ctx.stroke();
        } else if (glyph === "package" || glyph === "project") {
          roundRect(ctx, -6.5, -5.5, 13, 11, 2); ctx.stroke();
          ctx.beginPath(); ctx.moveTo(-6.5, -1.5); ctx.lineTo(6.5, -1.5); ctx.stroke();
        } else if (glyph === "zap") {
          ctx.beginPath();
          ctx.moveTo(1, -7); ctx.lineTo(-5, 0.5); ctx.lineTo(0, 0.5);
          ctx.lineTo(-1, 7); ctx.lineTo(5, -0.5); ctx.lineTo(0, -0.5);
          ctx.closePath(); ctx.stroke();
        } else {
          roundRect(ctx, -6.5, -5.5, 13, 11, 2.5); ctx.stroke();
          ctx.beginPath(); ctx.moveTo(-3.5, 0); ctx.lineTo(3.5, 0); ctx.stroke();
        }
        ctx.restore();
      }

      function drawArrow(ctx, tx, ty, ang, color, sz) {
        ctx.save();
        ctx.translate(tx, ty);
        ctx.rotate(ang);
        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.moveTo(0, 0);
        ctx.lineTo(-sz, -sz * 0.54);
        ctx.lineTo(-sz * 0.78, 0);
        ctx.lineTo(-sz, sz * 0.54);
        ctx.closePath();
        ctx.fill();
        ctx.restore();
      }

      function drawHollowArrow(ctx, tx, ty, ang, color, sz) {
        ctx.save();
        ctx.translate(tx, ty);
        ctx.rotate(ang);
        ctx.fillStyle = PAL.canvasBg;
        ctx.strokeStyle = color;
        ctx.lineWidth = 1.5;
        ctx.lineJoin = "round";
        ctx.beginPath();
        ctx.moveTo(0, 0);
        ctx.lineTo(-sz, -sz * 0.58);
        ctx.lineTo(-sz, sz * 0.58);
        ctx.closePath();
        ctx.fill();
        ctx.stroke();
        ctx.restore();
      }

      function fitGraph() {
        var frame = document.getElementById("ont-canvas-frame");
        if (!frame || graphNodes.length === 0) return;
        var w = frame.clientWidth || 760;
        var h = frame.clientHeight || 640;
        var overlay = document.getElementById("ont-filter-overlay");
        var topInset = 0;
        if (overlay && getComputedStyle(overlay).position === "absolute") {
          topInset = (overlay.offsetHeight || 30) + 14;
        }
        var availH = Math.max(160, h - topInset);
        var minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
        for (var i = 0; i < graphNodes.length; i++) {
          var n = graphNodes[i];
          var rx1 = n.x, ry1 = n.y;
          if (simAlpha > 0.05 && typeof n.ax === "number") {
            var tax = WORLD_CX + (n.ax - WORLD_CX) * spacingFactor;
            var tay = WORLD_CY + (n.ay - WORLD_CY) * spacingFactor;
            if (tax - 68 < minX) minX = tax - 68;
            if (tay - 74 < minY) minY = tay - 74;
            if (tax + 68 > maxX) maxX = tax + 68;
            if (tay + 88 > maxY) maxY = tay + 88;
          }
          if (rx1 - 68 < minX) minX = rx1 - 68;
          if (ry1 - 74 < minY) minY = ry1 - 74;
          if (rx1 + 68 > maxX) maxX = rx1 + 68;
          if (ry1 + 108 > maxY) maxY = ry1 + 108;
        }
        var bw = Math.max(200, maxX - minX);
        var bh = Math.max(200, maxY - minY);
        var zm = Math.max(0.22, Math.min(1.85, Math.min((w - 40) / bw, (availH - 36) / bh)));
        viewport.zoom = zm;
        viewport.panX = w / 2 - ((minX + maxX) / 2) * zm;
        viewport.panY = topInset + availH / 2 - ((minY + maxY) / 2) * zm;
      }

      function hitCapsule(ox, oy) {
        var wx = (ox - viewport.panX) / viewport.zoom;
        var wy = (oy - viewport.panY) / viewport.zoom;
        for (var i = graphNodes.length - 1; i >= 0; i--) {
          var n = graphNodes[i];
          var hasTog = Boolean((n.collapsedCount && n.collapsedCount > 0) || n.hasExpandedSubtypes);
          if (!hasTog) continue;
          var px = n.x - n.radius * 0.78 - 14;
          var py = n.y - n.radius * 0.74 - 9;
          if (wx >= px - 4 && wx <= px + 34 && wy >= py - 4 && wy <= py + 22) return n.kind;
        }
        return null;
      }

      function hitNode(ox, oy) {
        var wx = (ox - viewport.panX) / viewport.zoom;
        var wy = (oy - viewport.panY) / viewport.zoom;
        for (var i = graphNodes.length - 1; i >= 0; i--) {
          var n = graphNodes[i];
          var dx = wx - n.x;
          var dy = wy - n.y;
          if (dx * dx + dy * dy <= 32 * 32) return n;
        }
        return null;
      }

      function drawGraph() {
        if (viewMode !== "graph") return;
        var canvas = document.getElementById("ont-graph-canvas");
        var frame = document.getElementById("ont-canvas-frame");
        if (!canvas || !frame) return;

        var w = frame.clientWidth || 760;
        var h = frame.clientHeight || 640;
        if (!hasInitialFit && w > 80 && graphNodes.length > 0) {
          hasInitialFit = true;
          fitGraph();
        }

        var dpr = window.devicePixelRatio || 1;
        canvas.width = Math.max(1, Math.floor(w * dpr));
        canvas.height = Math.max(1, Math.floor(h * dpr));
        var ctx = canvas.getContext("2d");
        if (!ctx) return;

        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.fillStyle = PAL.canvasBg;
        ctx.fillRect(0, 0, w, h);

        ctx.fillStyle = PAL.gridDot;
        var step = 32;
        var ox = ((viewport.panX % step) + step) % step;
        var oy = ((viewport.panY % step) + step) % step;
        for (var gx = ox; gx < w; gx += step) {
          for (var gy = oy; gy < h; gy += step) {
            ctx.fillRect(gx, gy, 1.4, 1.4);
          }
        }

        var focus = focusedKind;
        var focusLineage = {};
        if (focus) {
          focusLineage[focus] = true;
          var curF = graphNodeMap[focus];
          var fGuard = {};
          while (curF && curF.extendsKind && !fGuard[curF.extendsKind]) {
            fGuard[curF.extendsKind] = true;
            focusLineage[curF.extendsKind] = true;
            curF = graphNodeMap[curF.extendsKind];
          }
        }
        var peers = {};
        for (var fl in focusLineage) peers[fl] = true;
        if (focus) {
          for (var pe = 0; pe < graphEdges.length; pe++) {
            if (focusLineage[graphEdges[pe].fromKind]) peers[graphEdges[pe].toKind] = true;
            if (focusLineage[graphEdges[pe].toKind]) peers[graphEdges[pe].fromKind] = true;
          }
        }

        ctx.save();
        ctx.translate(viewport.panX, viewport.panY);
        ctx.scale(viewport.zoom, viewport.zoom);

        var pills = [];
        for (var eIdx = 0; eIdx < graphEdges.length; eIdx++) {
          var ed = graphEdges[eIdx];
          var src = graphNodeMap[ed.fromKind];
          var dst = graphNodeMap[ed.toKind];
          if (!src || !dst) continue;

          var inc = Boolean(focus && (focusLineage[ed.fromKind] || focusLineage[ed.toKind]));
          var dim = Boolean(focus && !inc);
          var stroke = dim
            ? PAL.edgeDim
            : ed.isExtends
              ? (inc ? PAL.capHoverBorder : PAL.abstractRing)
              : inc
                ? (ACTIVE_EDGE_COLORS[ed.category] || PAL.edgeControl)
                : PAL.edgeIdle;

          ctx.strokeStyle = stroke;
          ctx.lineWidth = inc ? 1.9 : (ed.isExtends ? 1.5 : 1.25);

          if (ed.isSelf) {
            var baseA = -Math.PI * 0.64 - ed.slot * 0.55;
            var sA = baseA - 0.42;
            var eA = baseA + 0.42;
            var r = src.radius;
            var sx = src.x + Math.cos(sA) * r;
            var sy = src.y + Math.sin(sA) * r;
            var ex = src.x + Math.cos(eA) * (r + 3);
            var ey = src.y + Math.sin(eA) * (r + 3);
            var reach = r + 42 + ed.slot * 16;
            var c1x = src.x + Math.cos(sA - 0.18) * reach;
            var c1y = src.y + Math.sin(sA - 0.18) * reach;
            var c2x = src.x + Math.cos(eA + 0.18) * reach;
            var c2y = src.y + Math.sin(eA + 0.18) * reach;
            ctx.beginPath();
            ctx.moveTo(sx, sy);
            ctx.bezierCurveTo(c1x, c1y, c2x, c2y, ex, ey);
            ctx.stroke();
            if (!dim) {
              drawArrow(ctx, ex, ey, Math.atan2(ey - c2y, ex - c2x), stroke, 7);
              pills.push({
                mx: 0.125 * sx + 0.375 * c1x + 0.375 * c2x + 0.125 * ex,
                my: 0.125 * sy + 0.375 * c1y + 0.375 * c2y + 0.125 * ey,
                text: ed.rel,
                active: inc,
                prio: inc ? 0 : ed.priority
              });
            }
            continue;
          }

          var minF = ed.fromKind < ed.toKind;
          var ax = minF ? src.x : dst.x;
          var ay = minF ? src.y : dst.y;
          var bx = minF ? dst.x : src.x;
          var by = minF ? dst.y : src.y;
          var cdx = bx - ax;
          var cdy = by - ay;
          var cLen = Math.sqrt(cdx * cdx + cdy * cdy) || 1;
          var cpx = (src.x + dst.x) / 2 + (-cdy / cLen) * ed.offset;
          var cpy = (src.y + dst.y) / 2 + (cdx / cLen) * ed.offset;

          var svx = cpx - src.x, svy = cpy - src.y;
          var sL = Math.sqrt(svx * svx + svy * svy) || 1;
          var psx = src.x + (svx / sL) * src.radius;
          var psy = src.y + (svy / sL) * src.radius;

          var evx = dst.x - cpx, evy = dst.y - cpy;
          var eL = Math.sqrt(evx * evx + evy * evy) || 1;
          var ptx = dst.x - (evx / eL) * (dst.radius + (ed.isExtends ? 4 : 3));
          var pty = dst.y - (evy / eL) * (dst.radius + (ed.isExtends ? 4 : 3));

          ctx.save();
          if (ed.isExtends) ctx.setLineDash([5, 4]);
          ctx.beginPath();
          ctx.moveTo(psx, psy);
          ctx.quadraticCurveTo(cpx, cpy, ptx, pty);
          ctx.stroke();
          ctx.restore();

          if (!dim) {
            if (ed.isExtends) {
              drawHollowArrow(ctx, ptx, pty, Math.atan2(evy, evx), stroke, inc ? 9.5 : 8.4);
            } else {
              drawArrow(ctx, ptx, pty, Math.atan2(evy, evx), stroke, inc ? 7.8 : 6.6);
            }
            pills.push({
              mx: 0.25 * psx + 0.5 * cpx + 0.25 * ptx,
              my: 0.25 * psy + 0.5 * cpy + 0.25 * pty,
              text: ed.rel,
              active: inc,
              isExtends: ed.isExtends,
              prio: inc ? 0 : ed.priority
            });
          }
        }

        var boxes = [];
        for (var nIdx = 0; nIdx < graphNodes.length; nIdx++) {
          var nd = graphNodes[nIdx];
          var isSel = nd.kind === focusedKind;
          var isHov = nd.kind === hoveredKind;
          var isDim = Boolean(focus && !peers[nd.kind]);

          var scx = nd.x * viewport.zoom + viewport.panX;
          var scy = nd.y * viewport.zoom + viewport.panY;
          var scr = (nd.radius + 14) * viewport.zoom;
          boxes.push({ minX: scx - scr, minY: scy - scr, maxX: scx + scr, maxY: scy + scr + 18 * viewport.zoom });

          ctx.save();
          if (isDim) ctx.globalAlpha = 0.34;
          if (isSel || isHov) {
            ctx.beginPath();
            ctx.arc(nd.x, nd.y, nd.radius + 6.5, 0, Math.PI * 2);
            ctx.fillStyle = isSel ? PAL.nodeHalo : PAL.nodeHover;
            ctx.fill();
          }

          if (nd.isAbstract) {
            ctx.save();
            ctx.beginPath();
            ctx.arc(nd.x, nd.y, nd.radius + 4, 0, Math.PI * 2);
            ctx.setLineDash([3.5, 3]);
            ctx.lineWidth = 1.3;
            ctx.strokeStyle = PAL.abstractRing;
            ctx.stroke();
            ctx.restore();
          }

          ctx.beginPath();
          ctx.arc(nd.x, nd.y, nd.radius, 0, Math.PI * 2);
          ctx.fillStyle = nd.fill;
          ctx.fill();
          ctx.lineWidth = isSel ? 2.4 : 1.7;
          ctx.strokeStyle = isSel ? PAL.edgeControl : PAL.nodeRing;
          ctx.stroke();

          drawGlyph(ctx, nd.glyph, nd.x, nd.y);

          if ((nd.collapsedCount && nd.collapsedCount > 0) || nd.hasExpandedSubtypes) {
            var cLbl = nd.collapsedCount && nd.collapsedCount > 0 ? "+" + nd.collapsedCount : "−";
            ctx.font = '400 10px "Atkinson Hyperlegible Mono", ui-monospace, monospace';
            var cpw = Math.max(22, ctx.measureText(cLbl).width + 10);
            var cph = 17;
            var cpx2 = nd.x - nd.radius * 0.78 - cpw / 2;
            var cpy2 = nd.y - nd.radius * 0.74 - cph / 2;
            var isExpHov = hoveredCapsule === nd.kind;
            roundRect(ctx, cpx2, cpy2, cpw, cph, 8.5);
            ctx.fillStyle = isExpHov ? PAL.capHeadHoverBg : PAL.capHeadBg;
            ctx.fill();
            ctx.lineWidth = 1.2;
            ctx.strokeStyle = isExpHov ? PAL.capHoverBorder : PAL.capInnerBorder;
            ctx.stroke();
            ctx.fillStyle = PAL.inkBright;
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";
            ctx.fillText(cLbl, cpx2 + cpw / 2, cpy2 + cph / 2 + 0.5);
          }

          ctx.font = '400 12px "Host Grotesk", system-ui, sans-serif';
          ctx.fillStyle = isDim ? PAL.inkDim : PAL.inkBright;
          ctx.textAlign = "center";
          ctx.textBaseline = "top";
          ctx.fillText(nd.kind, nd.x, nd.y + nd.radius + 7);
          ctx.restore();
        }

        ctx.restore();

        if (viewport.zoom >= 0.42) {
          pills.sort(function (a, b) { return a.prio - b.prio; });
          ctx.font = '400 9.5px "Atkinson Hyperlegible Mono", ui-monospace, monospace';
          ctx.textAlign = "center";
          ctx.textBaseline = "middle";
          for (var pIdx = 0; pIdx < pills.length; pIdx++) {
            var pl = pills[pIdx];
            var px = pl.mx * viewport.zoom + viewport.panX;
            var py = pl.my * viewport.zoom + viewport.panY;
            if (px < -40 || px > w + 40 || py < -20 || py > h + 20) continue;
            var pw = ctx.measureText(pl.text).width + 11;
            var ph = 16;
            var box = { minX: px - pw / 2 - 3, minY: py - ph / 2 - 2, maxX: px + pw / 2 + 3, maxY: py + ph / 2 + 2 };
            var hit = false;
            if (!pl.active) {
              for (var bIdx = 0; bIdx < boxes.length; bIdx++) {
                var ob = boxes[bIdx];
                if (!(box.maxX < ob.minX || box.minX > ob.maxX || box.maxY < ob.minY || box.minY > ob.maxY)) {
                  hit = true;
                  break;
                }
              }
            }
            if (hit) continue;
            boxes.push(box);
            roundRect(ctx, px - pw / 2, py - ph / 2, pw, ph, 5);
            ctx.fillStyle = PAL.pillBg;
            ctx.fill();
            ctx.lineWidth = 1;
            ctx.strokeStyle = pl.active ? PAL.pillBorderActive : (pl.isExtends ? PAL.capInnerBorder : PAL.pillBorder);
            ctx.stroke();
            ctx.fillStyle = pl.active ? PAL.inkBright : (pl.isExtends ? PAL.capHoverBorder : PAL.inkSoft);
            ctx.fillText(pl.text, px, py + 0.5);
          }
        }
      }

      function computeBadges(kind) {
        var badges = {};
        var conns = [];
        var seen = {};
        var node = nodeMap[kind];
        if (!node) return { badges: badges, conns: conns };
        var outs = node.inheritedOutEdges ? node.outEdges.concat(node.inheritedOutEdges) : node.outEdges;
        for (var i = 0; i < outs.length; i++) {
          var oe = outs[i];
          if (!activeCats[oe.category]) continue;
          var ok = "out|" + oe.rel + "|" + oe.toKind;
          if (seen[ok]) continue;
          seen[ok] = true;
          if (!badges[oe.toKind]) badges[oe.toKind] = [];
          badges[oe.toKind].push({ dir: "out", rel: oe.rel, cat: oe.category });
          conns.push({ peer: oe.toKind, rel: oe.rel, dir: "out", cat: oe.category });
        }
        var ins = node.inheritedInEdges ? node.inEdges.concat(node.inheritedInEdges) : node.inEdges;
        for (var j = 0; j < ins.length; j++) {
          var ie = ins[j];
          if (!activeCats[ie.category]) continue;
          var ik = "in|" + ie.rel + "|" + ie.fromKind;
          if (seen[ik]) continue;
          seen[ik] = true;
          if (!badges[ie.fromKind]) badges[ie.fromKind] = [];
          badges[ie.fromKind].push({ dir: "in", rel: ie.rel, cat: ie.category });
          conns.push({ peer: ie.fromKind, rel: ie.rel, dir: "in", cat: ie.category });
        }
        return { badges: badges, conns: conns };
      }

      function renderInspector(node, q) {
        if (!node) return;
        var pill = document.getElementById("insp-layer-pill");
        pill.textContent = "Layer " + node.layerId;
        pill.className = "ont-layer-pill ont-layer-pill--l" + node.layerId;

        var absPill = document.getElementById("insp-abstract-pill");
        if (absPill) absPill.hidden = !node.isAbstract;

        var linEl = document.getElementById("insp-lineage");
        if (linEl) {
          var linHtml = "";
          if (node.ancestors && node.ancestors.length > 0) {
            linHtml += '<div class="ont-lineage-row"><span class="ont-layer-sub">Extends</span>';
            for (var ai = 0; ai < node.ancestors.length; ai++) {
              var anc = node.ancestors[ai];
              linHtml += '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(anc) + '">:' + escHtml(anc) + '</button>';
            }
            linHtml += '</div>';
          }
          if (node.subtypes && node.subtypes.length > 0) {
            linHtml += '<div class="ont-lineage-row"><span class="ont-layer-sub">Subtypes (' + node.subtypes.length + ')</span>';
            for (var si = 0; si < node.subtypes.length; si++) {
              var sub = node.subtypes[si];
              linHtml += '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(sub) + '">:' + escHtml(sub) + '</button>';
            }
            linHtml += '</div>';
          }
          linEl.innerHTML = linHtml;
          linEl.hidden = !linHtml;
        }

        document.getElementById("insp-plural").textContent = node.pluralLabel;
        document.getElementById("insp-title").textContent = ":" + node.kind;
        document.getElementById("insp-purpose").textContent = node.purpose;
        document.getElementById("insp-urn").textContent = node.idScheme || "n/a";
        document.getElementById("insp-props-heading").textContent =
          "Collected properties (" + node.properties.length + " + " + SCHEMA.commonProperties.length + " base)";

        var propsHtml = "";
        for (var i = 0; i < node.properties.length; i++) {
          var p = node.properties[i];
          var isMatch = q && (p.name.toLowerCase().indexOf(q) !== -1 || p.description.toLowerCase().indexOf(q) !== -1);
          var inhProp = p.inheritedFrom ? '<code class="ont-edge-prop">via :' + escHtml(p.inheritedFrom) + '</code>' : "";
          propsHtml +=
            '<div class="ont-prop-item' + (isMatch ? " is-match" : "") + '">' +
              '<div class="ont-prop-top">' +
                '<code class="ont-prop-name">' + escHtml(p.name) + '</code>' +
                '<span class="ont-prop-right">' +
                  inhProp +
                  '<code class="ont-prop-type">' + escHtml(p.type) + '</code>' +
                '</span>' +
              '</div>' +
              '<span class="ont-prop-desc">' + escHtml(p.description) + '</span>' +
            '</div>';
        }
        document.getElementById("insp-props-list").innerHTML = propsHtml;

        var allOut = node.inheritedOutEdges ? node.outEdges.concat(node.inheritedOutEdges) : node.outEdges;
        var allIn = node.inheritedInEdges ? node.inEdges.concat(node.inheritedInEdges) : node.inEdges;
        document.getElementById("insp-rels-heading").textContent =
          "Typed relationships (" + allOut.length + " out, " + allIn.length + " in)";

        var relsHtml = "";
        for (var o = 0; o < allOut.length; o++) {
          var oe = allOut[o];
          var rMeta = relMap[oe.rel];
          var epHtml = "";
          if (rMeta && rMeta.properties && rMeta.properties.length > 1) {
            epHtml = '<div class="ont-edge-props-row">';
            for (var x = 0; x < rMeta.properties.length; x++) {
              var ep = rMeta.properties[x];
              epHtml += '<code class="ont-edge-prop" title="' + escHtml(ep.description) + '">r.' + escHtml(ep.name) + '</code>';
            }
            epHtml += '</div>';
          }
          var inhOut = oe.inheritedFrom ? '<code class="ont-edge-prop">via :' + escHtml(oe.inheritedFrom) + '</code>' : "";
          relsHtml +=
            '<div class="ont-rel-row">' +
              '<div class="ont-rel-line">' +
                '<code class="ont-edge-tag ont-edge-tag--' + escHtml(oe.category) + '">-[:' + escHtml(oe.rel) + ']-&gt;</code>' +
                '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(oe.toKind) + '">:' + escHtml(oe.toKind) + '</button>' +
                inhOut +
              '</div>' +
              '<span class="ont-prop-desc">' + escHtml(oe.description) + '</span>' +
              epHtml +
            '</div>';
        }
        for (var m = 0; m < allIn.length; m++) {
          var ie = allIn[m];
          var inhIn = ie.inheritedFrom ? '<code class="ont-edge-prop">via :' + escHtml(ie.inheritedFrom) + '</code>' : "";
          relsHtml +=
            '<div class="ont-rel-row">' +
              '<div class="ont-rel-line">' +
                '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(ie.fromKind) + '">:' + escHtml(ie.fromKind) + '</button>' +
                '<code class="ont-edge-tag ont-edge-tag--' + escHtml(ie.category) + '">-[:' + escHtml(ie.rel) + ']-&gt;</code>' +
                inhIn +
              '</div>' +
              '<span class="ont-prop-desc">' + escHtml(ie.description) + '</span>' +
            '</div>';
        }
        document.getElementById("insp-rels-list").innerHTML = relsHtml;
        document.getElementById("insp-cypher").textContent = node.sampleCypher;

        var cPropsLbl = document.getElementById("insp-compact-props-label");
        if (cPropsLbl) {
          cPropsLbl.textContent = "Properties (" + node.properties.length + " + " + SCHEMA.commonProperties.length + " base)";
        }
        var cPropsEl = document.getElementById("insp-compact-props");
        if (cPropsEl) {
          var cpHtml = "";
          var maxCp = Math.min(6, node.properties.length);
          for (var cpi = 0; cpi < maxCp; cpi++) {
            var cp = node.properties[cpi];
            cpHtml += '<code class="ont-edge-prop" title="' + escHtml(cp.description) + '">' + escHtml(cp.name) + ": " + escHtml(cp.type) + "</code>";
          }
          if (node.properties.length > 6) {
            cpHtml += '<code class="ont-edge-prop">+' + (node.properties.length - 6) + " more</code>";
          }
          cPropsEl.innerHTML = cpHtml;
        }

        var cRelsLbl = document.getElementById("insp-compact-rels-label");
        if (cRelsLbl) {
          cRelsLbl.textContent = "Connected entities (" + allOut.length + " out, " + allIn.length + " in)";
        }
        var cRelsEl = document.getElementById("insp-compact-rels");
        if (cRelsEl) {
          var peerMap = {};
          var peerArr = [];
          for (var poi = 0; poi < allOut.length; poi++) {
            var pkOut = allOut[poi].toKind;
            if (!peerMap[pkOut]) {
              peerMap[pkOut] = true;
              peerArr.push(pkOut);
            }
          }
          for (var pii = 0; pii < allIn.length; pii++) {
            var pkIn = allIn[pii].fromKind;
            if (!peerMap[pkIn]) {
              peerMap[pkIn] = true;
              peerArr.push(pkIn);
            }
          }
          var crHtml = "";
          var maxCr = Math.min(6, peerArr.length);
          for (var cri = 0; cri < maxCr; cri++) {
            var pKind = peerArr[cri];
            crHtml += '<button type="button" class="ont-jump-btn" data-select-kind="' + escHtml(pKind) + '">:' + escHtml(pKind) + "</button>";
          }
          if (peerArr.length > 6) {
            crHtml += '<code class="ont-edge-prop">+' + (peerArr.length - 6) + " more</code>";
          }
          cRelsEl.innerHTML = crHtml;
        }

        var inspEl = document.getElementById("entity-inspector");
        var gpEl = document.getElementById("ont-graph-panel");
        var isFsMode = Boolean(gpEl && gpEl.classList.contains("is-fullscreen"));
        if (inspEl) {
          inspEl.hidden = viewMode === "graph" && !focusedKind;
          var showFull = viewMode === "layers" || isFsMode || inspectorExpanded;
          inspEl.classList.toggle("is-compact", !showFull);
          inspEl.classList.toggle("is-expanded-view", showFull);
        }
        var toggleModeBtn = document.getElementById("insp-toggle-mode-btn");
        if (toggleModeBtn) {
          toggleModeBtn.hidden = viewMode === "layers" || isFsMode;
          toggleModeBtn.textContent = inspectorExpanded ? "Compact" : "Full";
        }
        var closeInspBtn = document.getElementById("insp-close-btn");
        if (closeInspBtn) {
          closeInspBtn.hidden = viewMode === "layers";
        }
      }

      function renderFocusStrip(conns) {
        var labelEl = document.getElementById("ont-focus-label");
        var countEl = document.getElementById("ont-focus-count");
        var linksEl = document.getElementById("ont-focus-links");
        if (!labelEl || !countEl || !linksEl) return;
        labelEl.textContent = "Active connections for :" + selectedKind;
        countEl.textContent = conns.length + " active links";
        if (conns.length === 0) {
          linksEl.innerHTML = '<span class="ont-layer-sub">Enable relationship category filters above to display connected entities.</span>';
          return;
        }
        var max = Math.min(12, conns.length);
        var html = "";
        for (var i = 0; i < max; i++) {
          var c = conns[i];
          var txt = c.dir === "out"
            ? "out: " + escHtml(c.rel) + " :" + escHtml(c.peer)
            : "in: :" + escHtml(c.peer) + " " + escHtml(c.rel);
          html += '<button type="button" class="ont-focus-chip ont-focus-chip--' + escHtml(c.cat) + '" data-select-kind="' + escHtml(c.peer) + '"><code>' + txt + '</code></button>';
        }
        linksEl.innerHTML = html;
      }

      function isNodeVisible(nd, q) {
        return Boolean(activeLayers[nd.layerId]) && matchesQuery(nd, q);
      }

      function ensureSimLoop() {
        if (simRafId !== null || simFrozen) return;
        function tickLoop() {
          simRafId = null;
          if (simFrozen || viewMode !== "graph") return;
          simAlpha += (simAlphaTarget - simAlpha) * ALPHA_DECAY;
          if (simAlpha < ALPHA_MIN && simAlphaTarget === 0) {
            simAlpha = 0;
            return;
          }
          stepSimBodies(simBodies, simSprings, simAlpha, spacingFactor, hubFactor);
          syncGraphNodesFromBodies(lastCollapsedCounts);
          drawGraph();
          simRafId = requestAnimationFrame(tickLoop);
        }
        simRafId = requestAnimationFrame(tickLoop);
      }

      function rebuildGraphModel(q) {
        var filteredDtos = [];
        for (var i = 0; i < SCHEMA.nodes.length; i++) {
          var nd = SCHEMA.nodes[i];
          if (isNodeVisible(nd, q)) filteredDtos.push(nd);
        }
        var res = resolveHierarchy(filteredDtos);
        var visDtos = res.visibleDtos;
        var visSet = {};
        var visParts = [];
        for (var v = 0; v < visDtos.length; v++) {
          visSet[visDtos[v].kind] = true;
          visParts.push(visDtos[v].kind);
        }
        var capParts = [];
        for (var hi = 0; hi < hierarchies.length; hi++) {
          var hk = hierarchies[hi].kind;
          capParts.push(hk + ":" + (expandedHierarchies[hk] ? "1" : "0"));
        }
        var visKey = visParts.join(",") + "|" + capParts.join(",");
        var changed = Boolean(lastVisKey && lastVisKey !== visKey);
        lastVisKey = visKey;
        if (focusedKind && !visSet[focusedKind]) {
          focusedKind = res.redirectMap[focusedKind] || null;
        }
        syncSimulation(visDtos, res.redirectMap, res.collapsedCounts);
        graphCapsules = computeCapsules(graphNodes);
        graphEdges = computeEdges(visSet, res.redirectMap);
        if (changed && hasInitialFit && graphNodes.length > 0) {
          autoFitEnabled = true;
          fitGraph();
        }
        if (!simFrozen && (simAlpha >= ALPHA_MIN || simAlphaTarget > 0)) {
          ensureSimLoop();
        }
      }

      function render() {
        var q = searchQuery.trim().toLowerCase();
        var curNode = nodeMap[selectedKind];
        if (!curNode || !activeLayers[curNode.layerId]) {
          for (var s = 0; s < SCHEMA.nodes.length; s++) {
            if (isNodeVisible(SCHEMA.nodes[s], q)) {
              selectedKind = SCHEMA.nodes[s].kind;
              break;
            }
          }
          if (!nodeMap[selectedKind] || !activeLayers[nodeMap[selectedKind].layerId]) {
            for (var s2 = 0; s2 < SCHEMA.nodes.length; s2++) {
              if (activeLayers[SCHEMA.nodes[s2].layerId]) {
                selectedKind = SCHEMA.nodes[s2].kind;
                break;
              }
            }
          }
        }

        var ego = computeBadges(selectedKind);
        var cards = document.querySelectorAll(".ont-node-btn");
        var catList = ["control_flow", "data_flow", "type_dependency", "structural"];

        for (var i = 0; i < cards.length; i++) {
          var btn = cards[i];
          var kind = btn.getAttribute("data-kind");
          var node = nodeMap[kind];
          if (!node) continue;
          var visible = isNodeVisible(node, q);
          btn.hidden = !visible;
          var isSel = kind === selectedKind;
          var bList = ego.badges[kind] || [];
          var isNeighbor = bList.length > 0;
          var primaryCat = isNeighbor ? bList[0].cat : "";
          btn.setAttribute("aria-selected", isSel ? "true" : "false");
          btn.classList.toggle("is-neighbor", !isSel && isNeighbor);
          btn.classList.toggle("is-dimmed", !isSel && !isNeighbor);
          for (var c = 0; c < catList.length; c++) {
            btn.classList.toggle("is-neighbor--" + catList[c], !isSel && isNeighbor && primaryCat === catList[c]);
          }
        }

        var layerBands = document.querySelectorAll(".ont-layer[data-layer-id]");
        for (var lb = 0; lb < layerBands.length; lb++) {
          var lid = Number(layerBands[lb].getAttribute("data-layer-id"));
          layerBands[lb].hidden = !activeLayers[lid];
        }

        var layerBtns = document.querySelectorAll(".ont-layer-btn");
        for (var lbi = 0; lbi < layerBtns.length; lbi++) {
          var blid = Number(layerBtns[lbi].getAttribute("data-layer"));
          layerBtns[lbi].setAttribute("aria-pressed", activeLayers[blid] ? "true" : "false");
        }

        var capGroup = document.getElementById("ont-capsule-group");
        if (capGroup) {
          capGroup.hidden = viewMode !== "graph" || !activeLayers[4];
        }
        var capBtns = document.querySelectorAll(".ont-capsule-btn");
        for (var cbi = 0; cbi < capBtns.length; cbi++) {
          var hAttr = capBtns[cbi].getAttribute("data-hierarchy");
          var isExp = Boolean(expandedHierarchies[hAttr]);
          var hMeta = nodeMap[hAttr];
          if (hMeta && hMeta.ancestors) {
            for (var hai = 0; hai < hMeta.ancestors.length; hai++) {
              if (!expandedHierarchies[hMeta.ancestors[hai]]) isExp = false;
            }
          }
          capBtns[cbi].setAttribute("aria-pressed", isExp ? "true" : "false");
        }

        var graphPanel = document.getElementById("ont-graph-panel");
        var canvasFrame = document.getElementById("ont-canvas-frame");
        var graphFoot = document.getElementById("ont-graph-foot");
        var zoomGroup = document.getElementById("ont-graph-zoom-group");
        var swimlanes = document.getElementById("ont-swimlanes");
        var wasHidden = canvasFrame ? canvasFrame.hidden : false;
        if (graphPanel) {
          graphPanel.classList.toggle("is-layers-mode", viewMode === "layers");
        }
        if (canvasFrame) canvasFrame.hidden = viewMode !== "graph";
        if (graphFoot) graphFoot.hidden = viewMode !== "graph";
        if (zoomGroup) zoomGroup.hidden = viewMode !== "graph";
        if (swimlanes) swimlanes.hidden = viewMode !== "layers";

        var showAllBtn = document.getElementById("graph-show-all");
        if (showAllBtn) {
          showAllBtn.hidden = !focusedKind;
        }

        var modeBtns = document.querySelectorAll(".ont-mode-btn");
        for (var mb = 0; mb < modeBtns.length; mb++) {
          var mAttr = modeBtns[mb].getAttribute("data-mode");
          modeBtns[mb].setAttribute("aria-pressed", mAttr === viewMode ? "true" : "false");
        }

        rebuildGraphModel(q);
        if (wasHidden && viewMode === "graph") {
          autoFitEnabled = true;
          fitGraph();
        }
        drawGraph();
        renderFocusStrip(ego.conns);
        renderInspector(nodeMap[selectedKind], q);
      }

      function toggleHierarchy(hKind) {
        if (!hKind) return;
        var next = !expandedHierarchies[hKind];
        var hm = nodeMap[hKind];
        if (hm && hm.ancestors) {
          for (var a = 0; a < hm.ancestors.length; a++) {
            if (!expandedHierarchies[hm.ancestors[a]]) {
              next = true;
              expandedHierarchies[hm.ancestors[a]] = true;
            }
          }
        }
        expandedHierarchies[hKind] = next;
        autoFitEnabled = true;
        render();
        fitGraph();
        drawGraph();
      }

      document.addEventListener("click", function (ev) {
        var closeInsp = ev.target.closest("#insp-close-btn");
        if (closeInsp) {
          focusedKind = null;
          inspectorExpanded = false;
          render();
          return;
        }

        var toggleInspMode = ev.target.closest("#insp-toggle-mode-btn");
        if (toggleInspMode) {
          inspectorExpanded = !inspectorExpanded;
          render();
          return;
        }

        var modeBtn = ev.target.closest(".ont-mode-btn");
        if (modeBtn) {
          viewMode = modeBtn.getAttribute("data-mode") || "graph";
          render();
          return;
        }

        var capBtn = ev.target.closest(".ont-capsule-btn");
        if (capBtn) {
          toggleHierarchy(capBtn.getAttribute("data-hierarchy"));
          return;
        }

        var layerBtn = ev.target.closest(".ont-layer-btn");
        if (layerBtn) {
          var tlid = Number(layerBtn.getAttribute("data-layer"));
          activeLayers[tlid] = !activeLayers[tlid];
          if (!activeLayers[5] && !activeLayers[4] && !activeLayers[3] && !activeLayers[2] && !activeLayers[1]) {
            activeLayers = { 5: true, 4: true, 3: true, 2: true, 1: true };
          }
          render();
          return;
        }

        var card = ev.target.closest(".ont-node-btn");
        if (card) {
          selectedKind = card.getAttribute("data-kind");
          render();
          return;
        }

        var jump = ev.target.closest("[data-select-kind]");
        if (jump) {
          selectedKind = jump.getAttribute("data-select-kind");
          var jNode = nodeMap[selectedKind];
          if (jNode) {
            activeLayers[jNode.layerId] = true;
            if (jNode.ancestors) {
              for (var ja = 0; ja < jNode.ancestors.length; ja++) {
                expandedHierarchies[jNode.ancestors[ja]] = true;
              }
            }
          }
          if (viewMode === "graph") focusedKind = selectedKind;
          render();
          return;
        }

        var catBtn = ev.target.closest(".ont-cat-btn");
        if (catBtn) {
          var cat = catBtn.getAttribute("data-cat");
          activeCats[cat] = !activeCats[cat];
          catBtn.setAttribute("aria-pressed", activeCats[cat] ? "true" : "false");
          render();
          return;
        }
      });

      var canvasEl = document.getElementById("ont-graph-canvas");
      if (canvasEl) {
        canvasEl.style.cursor = "grab";
        canvasEl.addEventListener("pointerdown", function (e) {
          var rect = canvasEl.getBoundingClientRect();
          var ox = e.clientX - rect.left;
          var oy = e.clientY - rect.top;
          var hitCap = hitCapsule(ox, oy);
          if (hitCap) {
            canvasEl.setPointerCapture(e.pointerId);
            dragState = { mode: "capsule", kind: hitCap, sx: e.clientX, sy: e.clientY, ox: 0, oy: 0, moved: false };
            return;
          }
          var hit = hitNode(ox, oy);
          canvasEl.setPointerCapture(e.pointerId);
          canvasEl.style.cursor = "grabbing";
          if (hit) {
            dragState = { mode: "node", kind: hit.kind, sx: e.clientX, sy: e.clientY, ox: hit.x, oy: hit.y, moved: false };
            var sb = simByKind[hit.kind];
            if (sb) {
              sb.fx = sb.x;
              sb.fy = sb.y;
            }
            if (!simFrozen) {
              simAlphaTarget = 0.3;
              simAlpha = Math.max(simAlpha, 0.3);
              ensureSimLoop();
            }
          } else {
            dragState = { mode: "pan", kind: null, sx: e.clientX, sy: e.clientY, ox: viewport.panX, oy: viewport.panY, moved: false };
          }
        });

        canvasEl.addEventListener("pointermove", function (e) {
          var rect = canvasEl.getBoundingClientRect();
          if (dragState) {
            var dx = e.clientX - dragState.sx;
            var dy = e.clientY - dragState.sy;
            if (Math.abs(dx) > 4 || Math.abs(dy) > 4) dragState.moved = true;
            if (dragState.mode === "pan") {
              if (dragState.moved) autoFitEnabled = false;
              viewport.panX = dragState.ox + dx;
              viewport.panY = dragState.oy + dy;
              drawGraph();
            } else if (dragState.mode === "node" && dragState.kind) {
              var nx = dragState.ox + dx / viewport.zoom;
              var ny = dragState.oy + dy / viewport.zoom;
              var mb = simByKind[dragState.kind];
              if (mb) {
                mb.fx = nx;
                mb.fy = ny;
                mb.x = nx;
                mb.y = ny;
              }
              if (graphNodeMap[dragState.kind]) {
                graphNodeMap[dragState.kind].x = nx;
                graphNodeMap[dragState.kind].y = ny;
              }
              if (!simFrozen) {
                simAlpha = Math.max(simAlpha, 0.3);
                ensureSimLoop();
              }
              drawGraph();
            }
            return;
          }
          var mx = e.clientX - rect.left;
          var my = e.clientY - rect.top;
          var hCap = hitCapsule(mx, my);
          var hNode = hCap ? null : hitNode(mx, my);
          var nextHov = hNode ? hNode.kind : null;
          canvasEl.style.cursor = (hCap || nextHov) ? "pointer" : "grab";
          if (hCap !== hoveredCapsule || nextHov !== hoveredKind) {
            hoveredCapsule = hCap;
            hoveredKind = nextHov;
            drawGraph();
          }
        });

        canvasEl.addEventListener("pointerup", function (e) {
          if (canvasEl.hasPointerCapture(e.pointerId)) canvasEl.releasePointerCapture(e.pointerId);
          var d = dragState;
          dragState = null;
          canvasEl.style.cursor = (hoveredCapsule || hoveredKind) ? "pointer" : "grab";
          if (d && d.mode === "node" && d.kind) {
            simAlphaTarget = 0;
            var rb = simByKind[d.kind];
            if (rb && !simFrozen) {
              rb.fx = null;
              rb.fy = null;
            }
            if (!simFrozen) ensureSimLoop();
          }
          if (!d || d.moved) return;
          if (d.mode === "capsule" && d.kind) {
            hoveredCapsule = null;
            toggleHierarchy(d.kind);
          } else if (d.mode === "node" && d.kind) {
            selectedKind = d.kind;
            focusedKind = focusedKind === d.kind ? null : d.kind;
            render();
          } else if (focusedKind) {
            focusedKind = null;
            render();
          }
        });

        canvasEl.addEventListener("pointerleave", function () {
          if (!dragState && (hoveredKind || hoveredCapsule)) {
            hoveredKind = null;
            hoveredCapsule = null;
            drawGraph();
          }
        });

        canvasEl.addEventListener("wheel", function (e) {
          e.preventDefault();
          autoFitEnabled = false;
          var rect = canvasEl.getBoundingClientRect();
          var mx = e.clientX - rect.left;
          var my = e.clientY - rect.top;
          var factor = e.deltaY < 0 ? 1.12 : 0.89;
          var nextZ = Math.max(0.22, Math.min(2.8, viewport.zoom * factor));
          var sc = nextZ / viewport.zoom;
          viewport.zoom = nextZ;
          viewport.panX = mx - (mx - viewport.panX) * sc;
          viewport.panY = my - (my - viewport.panY) * sc;
          drawGraph();
        }, { passive: false });
      }

      function zoomBy(factor) {
        autoFitEnabled = false;
        var frame = document.getElementById("ont-canvas-frame");
        var cx = (frame ? frame.clientWidth : 760) / 2;
        var cy = (frame ? frame.clientHeight : 640) / 2;
        var nextZ = Math.max(0.22, Math.min(2.8, viewport.zoom * factor));
        var sc = nextZ / viewport.zoom;
        viewport.zoom = nextZ;
        viewport.panX = cx - (cx - viewport.panX) * sc;
        viewport.panY = cy - (cy - viewport.panY) * sc;
        drawGraph();
      }

      var zIn = document.getElementById("graph-zoom-in");
      var zOut = document.getElementById("graph-zoom-out");
      var zFit = document.getElementById("graph-zoom-fit");
      var zAll = document.getElementById("graph-show-all");
      var zFs = document.getElementById("graph-fullscreen");
      var gPanel = document.getElementById("ont-graph-panel");
      var cFrame = document.getElementById("ont-canvas-frame");

      function handleFrameResize() {
        if (!cFrame) return;
        var fw = cFrame.clientWidth || 0;
        var fh = cFrame.clientHeight || 0;
        if (fw <= 0 || fh <= 0) return;
        if (fw === lastFrameW && fh === lastFrameH) return;
        lastFrameW = fw;
        lastFrameH = fh;
        if (autoFitEnabled) {
          fitGraph();
        }
        drawGraph();
      }

      function syncFullscreenState(active) {
        if (!gPanel) return;
        autoFitEnabled = true;
        gPanel.classList.toggle("is-fullscreen", active);
        document.body.classList.toggle("has-fullscreen-graph", active);
        if (zFs) {
          zFs.setAttribute("aria-pressed", active ? "true" : "false");
          zFs.textContent = active ? "Exit" : "Expand";
        }
        render();
        fitGraph();
        drawGraph();
        requestAnimationFrame(function () {
          fitGraph();
          drawGraph();
        });
      }

      if (zFs && gPanel) {
        zFs.addEventListener("click", function () {
          var isFs = gPanel.classList.contains("is-fullscreen");
          syncFullscreenState(!isFs);
        });
      }

      document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && gPanel && gPanel.classList.contains("is-fullscreen")) {
          syncFullscreenState(false);
        }
      });

      document.addEventListener("mousedown", function (e) {
        if (gPanel && gPanel.classList.contains("is-fullscreen") && !gPanel.contains(e.target)) {
          syncFullscreenState(false);
        }
      });

      var zAir = document.getElementById("graph-air-slider");
      var zAirVal = document.getElementById("graph-air-val");
      var zHub = document.getElementById("graph-hub-slider");
      var zHubVal = document.getElementById("graph-hub-val");
      var zFreeze = document.getElementById("graph-freeze");
      if (zAir) zAir.value = String(spacingFactor);
      if (zAirVal) zAirVal.textContent = spacingFactor.toFixed(1) + "x";
      if (zHub) zHub.value = String(hubFactor);
      if (zHubVal) zHubVal.textContent = hubFactor.toFixed(1) + "x";

      if (zAir) {
        zAir.addEventListener("input", function () {
          var nextSp = parseFloat(zAir.value) || 1.0;
          spacingFactor = Math.max(0.1, Math.min(2.4, nextSp));
          if (zAirVal) zAirVal.textContent = spacingFactor.toFixed(1) + "x";
          try { localStorage.setItem("ce_ontology_air", String(spacingFactor)); } catch (_e) {}
          if (!simFrozen) {
            simAlpha = Math.max(simAlpha, 0.38);
            ensureSimLoop();
          }
        });
      }

      if (zHub) {
        zHub.addEventListener("input", function () {
          var parsedHb = parseFloat(zHub.value);
          var nextHb = isNaN(parsedHb) ? 1.0 : parsedHb;
          hubFactor = Math.max(0.0, Math.min(5.0, nextHb));
          if (zHubVal) zHubVal.textContent = hubFactor.toFixed(1) + "x";
          try { localStorage.setItem("ce_ontology_hub", String(hubFactor)); } catch (_e) {}
          if (!simFrozen) {
            simAlpha = Math.max(simAlpha, 0.42);
            ensureSimLoop();
          }
        });
      }

      if (zFreeze) {
        zFreeze.addEventListener("click", function () {
          simFrozen = !simFrozen;
          zFreeze.setAttribute("aria-pressed", simFrozen ? "true" : "false");
          zFreeze.textContent = simFrozen ? "Frozen" : "Freeze";
          if (simFrozen) {
            simAlpha = 0;
            simAlphaTarget = 0;
            for (var fi = 0; fi < simBodies.length; fi++) {
              simBodies[fi].vx = 0;
              simBodies[fi].vy = 0;
            }
          } else {
            for (var ufi = 0; ufi < simBodies.length; ufi++) {
              simBodies[ufi].fx = null;
              simBodies[ufi].fy = null;
            }
            simAlpha = Math.max(simAlpha, 0.28);
            ensureSimLoop();
          }
        });
      }

      if (zIn) zIn.addEventListener("click", function () { zoomBy(1.18); });
      if (zOut) zOut.addEventListener("click", function () { zoomBy(0.84); });
      if (zFit) zFit.addEventListener("click", function () {
        focusedKind = null;
        autoFitEnabled = true;
        resetSimLayout();
        render();
        fitGraph();
        drawGraph();
      });
      if (zAll) zAll.addEventListener("click", function () {
        focusedKind = null;
        render();
      });

      if (cFrame && typeof ResizeObserver !== "undefined") {
        var frameObserver = new ResizeObserver(function () {
          handleFrameResize();
        });
        frameObserver.observe(cFrame);
      }

      window.addEventListener("resize", function () {
        handleFrameResize();
        drawGraph();
      });

      var searchEl = document.getElementById("ont-search");
      if (searchEl) {
        searchEl.addEventListener("input", function () {
          searchQuery = searchEl.value || "";
          render();
        });
      }

      var copyBtn = document.getElementById("copy-cypher-btn");
      if (copyBtn) {
        copyBtn.addEventListener("click", function () {
          var text = document.getElementById("insp-cypher").textContent;
          if (navigator.clipboard) {
            navigator.clipboard.writeText(text).then(function () {
              var orig = copyBtn.textContent;
              copyBtn.textContent = "Copied";
              setTimeout(function () { copyBtn.textContent = orig; }, 1500);
            });
          }
        });
      }

      render();
    
  return () => {
    document.body.classList.remove("has-fullscreen-graph");
    if (typeof simRafId !== "undefined" && simRafId !== null) {
      cancelAnimationFrame(simRafId);
    }
  };
}
