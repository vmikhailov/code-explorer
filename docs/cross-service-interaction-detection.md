# Proposal: Cross-Service Interaction Detection

This proposal outlines the design and implementation roadmap for enabling **CodeExplorer** to detect and trace complex cross-service communication channels—specifically **Google Cloud Pub/Sub (Event Bus)**, **Socket.io (WebSockets)**, **Dynamic HTTP URLs (template strings/constants)**, and **NestJS Controller Route prefixes**—using Tree-sitter AST queries and Late Binding.

---

## 1. Problem Statement

Modern microservice architectures rely heavily on decoupled communication. In the current implementation of CodeExplorer, cross-service interactions are frequently missed or incorrectly mapped due to four major limitations:

1.  **Dynamic HTTP Resolution Blind Spots**: The `AxiosLibraryParser` and `FetchLibraryParser` only extract static URL strings. Dynamic templates (e.g., `` `${HOST_VAR}/api/v1/resource` `` or `` `${this.host}${endpoint}` ``) fail parsing via `Uri.TryCreate`, resulting in unresolved targets.
2.  **Disconnected NestJS Route Templates**: `@Controller("prefix")` class decorators and `@Post("subroute")` method decorators are parsed independently, yielding disconnected entry points (e.g., `GET:prefix` and `POST:subroute` instead of `POST:/prefix/subroute`).
3.  **No Message-Broker / Pub-Sub Support**: `GcpLibraryParser` is an empty stub throwing `NotImplementedException`, meaning all Google Cloud Pub/Sub `publish` and `subscribe` patterns are completely invisible to the dependency graph.
4.  **No Socket-Based (WebSocket) Event Trace**: Real-time event emitters and listeners (e.g., `socket.on` and `socket.emit`) are not recognized, hiding WebSocket flows.

---

## 2. Technical Proposals

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          INGESTION & PARSING                            │
│                                                                         │
│   [TypeScript/Go/C# Source]                                             │
│       │                                                                 │
│       ├─► NestJsLibraryParser (Combines @Controller + @Get/@Post)       │
│       │                                                                 │
│       ├─► GcpPubSubLibraryParser (Extracts Publish/Subscribe Topics)    │
│       │                                                                 │
│       └─► AxiosLibraryParser (Resolves Dynamic Path Suffixes)           │
└────────────────────────────────────┬────────────────────────────────────┘
                                     │
                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                      LATE-BINDING RESOLUTION (Layer 5)                  │
│                                                                         │
│   [ExternalServices] ──► Suffix Path Matcher ──► [Endpoints]            │
│   [PubSubPublishers] ──► Topic String Match ───► [PubSubConsumers]      │
│   [SocketEmitters] ────► Event Name Match ─────► [SocketReceivers]      │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.1 Resolution of Dynamic HTTP Endpoints
Instead of forcing strict absolute URL checks in `AxiosLibraryParser`/`FetchLibraryParser`, we will capture the unresolved template strings and leverage suffix pattern matching during late binding:

1.  **Parser Update**:
    *   If the URL argument is a `template_string` or a variable reference, capture the literal text (e.g., `"${HOST_VAR}/api/v1/resource"`).
    *   Normalize out variable indicators (e.g. replacing `${...}` with wildcard operators like `*`).
2.  **Late Binding Suffix Matcher**:
    *   In `Layer5AnalysisParser.cs`, update `IsMatch(ExternalServiceNode ext, EndpointNode endpoint)` to perform a suffix-aware check:
    ```csharp
    private bool IsMatch(ExternalServiceNode extService, EndpointNode endpoint)
    {
        var servicePath = NormalizePath(extService.Path);
        var routeTemplate = NormalizePath(endpoint.RouteTemplate);
        
        // Match if one path ends with the other (e.g. "*/api/v1/resource" matches "/api/v1/resource")
        if (servicePath.EndsWith(routeTemplate, StringComparison.OrdinalIgnoreCase) ||
            routeTemplate.EndsWith(servicePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }
    ```

### 2.2 NestJS Route Prefix Aggregation
To generate accurate endpoints, `NestJsLibraryParser` must aggregate class-level prefixes with method-level routes:

1.  **Stateful Traversal**:
    *   Modify `TypeScriptFileVisitor` to maintain the context of the active `class_declaration`.
    *   When entering a class, check if it has a `@Controller` decorator, and store its route argument (e.g., `/api/v1`).
    *   When entering a method decorated with `@Get`/`@Post`, prepend the class controller prefix to the method route (e.g., `/api/v1` + `/users` = `POST:/api/v1/users`).
2.  **AST Query Pattern**:
    *   Match class decorators and pass the values down to child method decorators.

### 2.3 Event-Driven Pub/Sub Ingestion (GCP & RabbitMQ)
Implement `GcpLibraryParser` and create `RabbitMqLibraryParser` to detect event-driven interactions:

1.  **Ontology Additions**:
    *   **Node Labels**: `PubSubTopic` (replaces `ExternalService` for message queues).
    *   **Relationships**: `PUBLISHES` (Publisher to Topic), `SUBSCRIBES_TO` (Topic to Subscriber).
2.  **Parser Queries**:
    *   **Publishers**: Detect expressions like `pubSubService.publishMessage(...)`, `sendMessageToTopic(msg, TOPIC_NAME)`, or `amqp.publish(...)`. Extract the topic name argument.
    *   **Subscribers**: Detect `pubSubService.subscribeToMessages(subscriptionName, handler)` or `amqp.consume(...)`.
3.  **Late Binding**:
    *   Resolve event channels by matching `PUBLISHES` targets to `SUBSCRIBES_TO` topics on matching Topic names.

### 2.4 WebSocket & Socket.io Flow Detection
Map real-time event systems:

1.  **WebSocket Parser**:
    *   Create a `SocketIoLibraryParser` to match:
        *   `socket.on("event", handler)` -> Map as `EntryPoint` (Identifier: `ws:event`).
        *   `socket.emit("event", payload)` -> Map as `ExternalService` (Identifier: `ws:event`).
2.  **Ontology Mapping**:
    *   Map `ws:` prefixed routes in Late Binding to produce `CallsEndpoint` relationships from emitters to listeners.

---

## 3. Implementation Roadmap

### Phase 1 — NestJS Route Ingress Aggregation
*   **Goal**: Ensure controllers construct fully-qualified endpoint paths.
*   **Tasks**:
    *   Update `TypeScriptFileVisitor.cs` to capture class `@Controller` annotations.
    *   Update `NestJsLibraryParser.cs` to concatenate parent controller routes onto method-level routing nodes.
    *   Verify C# `AspNetCoreLibraryParser.cs` also supports similar class-level `[Route("prefix")]` attribute prefixing.

### Phase 2 — Suffix-Aware Late Binding
*   **Goal**: Connect dynamic Axios client requests with server routes.
*   **Tasks**:
    *   Modify `AxiosLibraryParser` to emit the raw path template containing wildcards instead of discarding it.
    *   Update `Layer5AnalysisParser::IsMatch` to support wildcard and suffix matches.

### Phase 3 — GCP Pub/Sub and RabbitMQ Parsers
*   **Goal**: Track async message flows across services.
*   **Tasks**:
    *   Replace `GcpLibraryParser` stubs with logic detecting Google Cloud PubSub `Topic` and `Subscription` clients.
    *   Register Pub/Sub topics/event types as Ontological Topic Nodes.
    *   Connect publishers to subscribers using Late Binding.

### Phase 4 — WebSocket / Socket.io Integrations
*   **Goal**: Map real-time duplex connections.
*   **Tasks**:
    *   Create `SocketIoLibraryParser` matching `socket.on` and `socket.emit`.
    *   Add late-binding rules for `ws:` namespaces.
