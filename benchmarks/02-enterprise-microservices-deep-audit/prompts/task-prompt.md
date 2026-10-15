Perform a comprehensive, cross-service architectural audit on the enterprise microservices monorepo (`src/` contains 320+ services and 90+ shared libraries across TypeScript/NestJS and Go). Answer all four architectural questions below with 100% completeness (do not guess or omit items; provide exact symbol/class/function names and relative file paths using forward slashes `/`):

1. **Cross-Service Database Table Persistence Coupling (Shared ORM Tables)**:
   Identify every database table (`Table`) in the monorepo that has **3 or more distinct ORM entity classes (`@Entity` / model classes)** mapped/persisted to it across different services or libraries.
   For each such table, provide:
   - The exact table name
   - The total count of distinct entity classes mapped to it
   - Every mapped entity class name and its relative file path

2. **Cross-Service Synchronous Outbound Call Fan-In (`SERVICE_CALL`)**:
   Identify every caller service, function/method name, and relative file path across the monorepo that makes an outbound synchronous service call (`linkGenerator` / HTTP client service call) to any of these 5 internal target services:
   - `svc-worker-core`
   - `svc-alert-notifier`
   - `svc-network-telemetry`
   - `svc-exchange-hub`
   - `svc-pricing-engine`
   Group your results by target service.

3. **Polyglot Asynchronous Messaging & Queue Topology (TypeScript & Go)**:
   For each of the following 5 messaging topics / queue channels across the TypeScript and Go services:
   - `topic-routing-rule-updated`
   - `PUB_SUB_PARTNER_NETWORK_TOPIC`
   - `REALTIME_BID_CACHE_QUEUE`
   - ` TelemetryImpressionTopic` (`ImpressionTopic`)
   - `TelemetryConversionTopic` (`ConversionTopic`)
   Identify every function/method name and its declared relative file path that:
   (a) **Publishes** messages/events to the topic, and
   (b) **Subscribes / listens** to messages/events from the topic.

4. **Multi-Hop Inheritance & Strategy Realization Across Microservices**:
   List every class/type and its relative file path across the monorepo that:
   (a) Inherits (directly or transitively) from `BaseEventController`
   (b) Implements the `ICloudPubSubService` interface
   (c) Implements the `IOptimizationCampaignStrategy` interface

> **Confidentiality Note**: All proprietary service, table, topic, class, and interface identifiers in this benchmark artifact have been systematically anonymized to preserve commercial confidentiality while maintaining a 1-to-1 structural correspondence with the underlying enterprise monorepo.
