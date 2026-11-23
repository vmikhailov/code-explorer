namespace CodeExplorer.Core.Gates.Storage;

internal static class StarterGateTemplates
{
    public static readonly (string FileName, string Content)[] All =
    [
        ("example-gate.yaml.example", """
            # Semantic Invariant Gate Example Template (v2 Node-First & Edge-Aware)
            # To activate this gate, rename this file to .yaml and customize the Cypher query.
            id: SIG-M-I-002
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Rules
            name: Method Length Limitation
            severity: Error
            description: Methods must not exceed 500 lines of code.
            remediation: Refactor long methods into smaller focused helper functions or handlers.
            target: "{loc} lines"
            message: "Method {f.name} exceeds maximum allowed length of 500 lines ({loc} lines)"
            query: |
              MATCH (f:Function)
              WITH f, coalesce(f.loc, f.end_line - f.start_line + 1) AS loc
              WHERE loc > 500 AND NOT coalesce(f.is_test, false) AND f.name <> '<module_init>'
              RETURN f, loc
            ratchet:
              enabled: true
              baseline: baselines/SIG-M-I-002.json
            """),
        ("table-write-boundary.yaml.example", """
            # Starter Gate: Table Write Boundary
            # Only designated folders or services may write to a protected database table.
            # Rename to .yaml and replace 'user_balance' and 'billing/' with your table and owner path.
            id: SIG-D-B-001
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: Protected Table Write Boundary
            severity: Error
            description: Only the designated owner module may execute write queries against the protected table.
            remediation: Route table mutations through the owning service or repository instead of writing directly.
            target: "{t.name}"
            message: "Unauthorized write to table '{t.name}' from '{fn.file_path}'"
            query: |
              MATCH (fn:Function)-[:WRITES_TO]->(t:Table)
              WHERE t.name = 'user_balance'
                AND NOT coalesce(fn.is_test, false)
                AND NOT coalesce(fn.file_path, '') STARTS WITH 'billing/'
              RETURN fn, t
            ratchet:
              enabled: true
              baseline: baselines/SIG-D-B-001.json
            """),
        ("external-service-boundary.yaml.example", """
            # Starter Gate: Forbidden Direct ExternalService Call
            # Prevent modules outside the designated integration layer from calling an external service directly.
            # Rename to .yaml and customize the service domain/command and allowed folder prefix.
            id: SIG-S-B-001
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: Forbidden Direct External Service Call
            severity: Error
            description: Application modules must not invoke the backend command socket directly; use the command service adapter.
            remediation: Call the shared integration client or command service instead of issuing direct socket/HTTP calls.
            target: "{es.name}"
            message: "Direct call to external service '{es.name}' from '{caller.file_path}' bypasses the integration boundary"
            query: |
              MATCH (caller)-[r:CALLS]->(es:ExternalService)
              WHERE (caller:Function OR caller:Type)
                AND NOT coalesce(caller.is_test, false)
                AND es.domain_or_service = 'mreg'
                AND NOT coalesce(caller.file_path, '') STARTS WITH 'command-service/'
              RETURN caller, r, es
            ratchet:
              enabled: true
              baseline: baselines/SIG-S-B-001.json
            """),
        ("no-raw-sql-outside-dal.yaml.example", """
            # Starter Gate: No Raw SQL Outside Data-Access Layer
            # Ensure raw SQL queries are confined to data-access, repository, or migration folders.
            # Rename to .yaml and adjust the allowed path patterns for your workspace.
            id: SIG-D-B-002
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: No Raw SQL Outside Data-Access Layer
            severity: Warning
            description: Raw SQL queries must reside inside data-access, repository, model, or migration folders.
            remediation: Move raw SQL statements into a repository/datasource class in the data-access layer.
            target: "{q.name}"
            message: "Raw SQL query '{q.name}' found outside data-access layer in '{q.file_path}'"
            query: |
              MATCH (q:Query)
              WHERE NOT coalesce(q.file_path, '') CONTAINS '/models/'
                AND NOT coalesce(q.file_path, '') CONTAINS '/repositories/'
                AND NOT coalesce(q.file_path, '') CONTAINS '/datasource/'
                AND NOT coalesce(q.file_path, '') CONTAINS '/db/'
                AND NOT coalesce(q.file_path, '') CONTAINS '/migrations/'
              RETURN q
            ratchet:
              enabled: true
              baseline: baselines/SIG-D-B-002.json
            """),
        ("12factor-dependencies-no-unpinned-packages.yaml.example", """
            # Starter Gate: Twelve-Factor II (Dependencies) - Explicit & Pinned Dependencies
            # Ensure all external package dependencies declare an explicit pinned version.
            # Rename to .yaml to activate for your workspace.
            id: SIG-12F-002
            category: Architectural
            platform: agnostic
            scope: Project
            dimension: Boundaries
            name: Twelve-Factor II - No Unpinned Package Dependencies
            severity: Warning
            description: Package dependencies must specify an explicit version rather than wildcard ('*', 'latest') or empty version specifiers.
            remediation: Pin the dependency in the project manifest to an explicit semantic version or lockfile version.
            target: "{p.name} -> {pkg.name}"
            message: "Project '{p.name}' depends on unpinned package '{pkg.name}' (version: '{pkg.version}')"
            query: |
              MATCH (p:Project)-[:DEPENDS_ON]->(pkg:Package)
              WHERE coalesce(pkg.version, '') IN ['', '*', 'latest', 'LATEST']
              RETURN p, pkg
            ratchet:
              enabled: true
              baseline: baselines/SIG-12F-002.json
            """),
        ("12factor-config-no-hardcoded-connections.yaml.example", """
            # Starter Gate: Twelve-Factor III & IV (Config & Backing Services) - Externalized Config
            # Ensure connection strings, secrets, and service URLs come from environment/config rather than literals.
            # Rename to .yaml to activate for your workspace.
            id: SIG-12F-003
            category: Implementation
            platform: agnostic
            scope: Method
            dimension: Implementation
            name: Twelve-Factor III - No Hardcoded Connection Strings or Secrets
            severity: Error
            description: Backing service connection strings, credentials, and URLs must be injected via environment variables or configuration, never hardcoded as string literals.
            remediation: Load the connection string or secret from environment variables or a bound configuration provider.
            target: "{fn.name} -> {ca.callee}"
            message: "Function '{fn.name}' passes hardcoded {ca.nature} '{ca.resolved_value}' to '{ca.callee}' in '{fn.file_path}'"
            query: |
              MATCH (fn:Function)-[:PASSES_ARGUMENT]->(ca:CallArgument)
              WHERE NOT coalesce(fn.is_test, false)
                AND ca.nature IN ['ConnectionString', 'SecretOrHeader']
                AND ca.provenance IN ['Literal', 'InternalConstant']
              RETURN fn, ca
            ratchet:
              enabled: true
              baseline: baselines/SIG-12F-003.json
            """),
        ("12factor-processes-no-mutable-static-state.yaml.example", """
            # Starter Gate: Twelve-Factor VI (Processes) - Stateless Share-Nothing Processes
            # Detect mutable static/module-global fields and properties that hold in-process state across requests.
            # Rename to .yaml to activate for your workspace.
            id: SIG-12F-006
            category: Architectural
            platform: agnostic
            scope: Class
            dimension: Responsibility
            name: Twelve-Factor VI - No Mutable Static Process State
            severity: Warning
            description: Twelve-factor processes are stateless and share-nothing; mutable static fields or properties retain sticky state across requests in a single process.
            remediation: Make the static member immutable (const / static readonly / final) or move shared state to an external backing service (e.g. Redis, database).
            target: "{t.name}.{m.name}"
            message: "Type '{t.name}' declares mutable static member '{m.name}' in '{t.file_path}'"
            query: |
              MATCH (t:Type)-[:HAS_MEMBER]->(m:Member)
              WHERE NOT coalesce(t.is_test, false)
                AND m.is_static = true
                AND coalesce(m.is_constant, false) = false
              RETURN t, m
            ratchet:
              enabled: true
              baseline: baselines/SIG-12F-006.json
            """),
        ("12factor-logs-no-file-log-sinks.yaml.example", """
            # Starter Gate: Twelve-Factor XI (Logs) - Treat Logs as Event Streams
            # Ensure application code does not configure local log file sinks or rolling file appenders directly.
            # Rename to .yaml to activate for your workspace.
            id: SIG-12F-011
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: Twelve-Factor XI - No Direct File Log Sinks
            severity: Warning
            description: Twelve-factor apps write logs unbuffered to stdout/stderr and let the execution environment route and archive streams, rather than managing local log files.
            remediation: Replace file-based logging sinks (WriteTo.File, FileAppender, FileHandler, winston.transports.File) with stdout/console output.
            target: "{fn.name} -> {ca.callee}"
            message: "Function '{fn.name}' configures file log sink '{ca.callee}' ({ca.resolved_value}) in '{fn.file_path}'"
            query: |
              MATCH (fn:Function)-[:PASSES_ARGUMENT]->(ca:CallArgument)
              WHERE NOT coalesce(fn.is_test, false)
                AND ca.nature = 'FilePath'
                AND (ca.callee IN ['WriteTo.File', 'AddFile', 'FileAppender', 'RollingFileAppender', 'FileHandler', 'RotatingFileHandler', 'TimedRotatingFileHandler', 'transports.File', 'winston.transports.File', 'pino.destination', 'error_log']
                  OR ca.callee ENDS WITH '.WriteTo.File'
                  OR ca.callee ENDS WITH '.AddFile'
                  OR ca.callee ENDS WITH '.FileAppender'
                  OR ca.callee ENDS WITH '.RollingFileAppender'
                  OR ca.callee ENDS WITH '.FileHandler'
                  OR ca.callee ENDS WITH '.RotatingFileHandler'
                  OR ca.callee ENDS WITH '.TimedRotatingFileHandler'
                  OR ca.callee ENDS WITH '.transports.File'
                  OR ca.callee ENDS WITH 'pino.destination')
              RETURN fn, ca
            ratchet:
              enabled: true
              baseline: baselines/SIG-12F-011.json
            """),
        ("cqrs-query-no-writes.yaml.example", """
            # Starter Gate: CQRS - Query Handlers Must Be Side-Effect Free
            # Ensure query handlers never directly or transitively write to database tables or publish events.
            # Rename to .yaml to activate for your workspace.
            id: SIG-CQRS-001
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Responsibility
            name: CQRS - Query Handlers Must Be Side-Effect Free
            severity: Error
            description: In Command Query Responsibility Segregation (CQRS), queries must only return data and never mutate database tables or publish events.
            remediation: Move database writes and event publishing out of the query path into an explicit Command Handler.
            target: "{w.name} -> {target.name}"
            message: "Query handler '{q.name}' ({msg.name}) in '{q.file_path}' triggers state mutation on '{target.name}' via '{w.name}'"
            query: |
              MATCH (q:Function)-[:HANDLES_QUERY]->(msg:Message),
                    (q)-[:CALLS*0..4]->(w:Function)-[r:WRITES_TO|PUBLISHES]->(target)
              WHERE NOT q.is_test AND NOT w.is_test
              RETURN DISTINCT q, msg, w, r, target
            ratchet:
              enabled: true
              baseline: baselines/SIG-CQRS-001.json
            """),
        ("cqrs-no-query-to-command-calls.yaml.example", """
            # Starter Gate: CQRS - Queries Must Not Invoke Command Handlers
            # Ensure query handlers never dispatch or call command handlers.
            # Rename to .yaml to activate for your workspace.
            id: SIG-CQRS-002
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: CQRS - Queries Must Not Invoke Command Handlers
            severity: Error
            description: Query handlers must remain strictly separated from command execution paths and never dispatch or invoke command handlers.
            remediation: Decouple the read model from command execution; execute commands explicitly from the caller or application workflow.
            target: "{q.name} -> {c.name}"
            message: "Query handler '{q.name}' ({qmsg.name}) in '{q.file_path}' invokes command handler '{c.name}' ({cmd.name}) in '{c.file_path}'"
            query: |
              MATCH (q:Function)-[:HANDLES_QUERY]->(qmsg:Message),
                    (q)-[:CALLS*1..3]->(c:Function)-[:HANDLES_COMMAND]->(cmd:Message)
              WHERE NOT q.is_test AND NOT c.is_test
              RETURN DISTINCT q, qmsg, c, cmd
            ratchet:
              enabled: true
              baseline: baselines/SIG-CQRS-002.json
            """),
        ("cqrs-http-get-read-only.yaml.example", """
            # Starter Gate: CQRS - HTTP GET Endpoints Must Be Read-Only
            # Ensure HTTP GET endpoints never trigger database writes or event publications across the call chain.
            # Rename to .yaml to activate for your workspace.
            id: SIG-CQRS-003
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: CQRS - HTTP GET Endpoints Must Be Read-Only
            severity: Error
            description: HTTP GET endpoints represent queries and must be safe and idempotent without mutating database tables or publishing domain/integration events.
            remediation: Route state-changing operations through POST/PUT/PATCH/DELETE command endpoints and keep GET endpoints strictly read-only.
            target: "{ep.name} -> {w.name} -> {target.name}"
            message: "HTTP GET endpoint '{ep.name}' handled by '{fn.name}' in '{fn.file_path}' mutates '{target.name}' via '{w.name}'"
            query: |
              MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)
                    -[:CALLS*0..5]->(w:Function)-[r:WRITES_TO|PUBLISHES]->(target)
              WHERE ep.http_method = 'GET' AND NOT fn.is_test AND NOT w.is_test
              RETURN DISTINCT fn, ep, w, r, target
            ratchet:
              enabled: true
              baseline: baselines/SIG-CQRS-003.json
            """),
        ("cache-write-without-invalidation.yaml.example", """
            # Starter Gate: Cache Coherence - Table Write Without Cache Invalidation
            # Detect command handlers mutating a cached database table without evicting or updating the corresponding cache region.
            # Rename to .yaml to activate for your workspace.
            id: SIG-CACHE-001
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Responsibility
            name: Cache Coherence - Write Without Cache Invalidation
            severity: Warning
            description: Commands mutating a database table read alongside a Cache region should evict or refresh that cache region to avoid serving stale reads.
            remediation: Call cache invalidation (INVALIDATES_CACHE) or refresh (WRITES_CACHE) in the command handler or domain event handler after mutating the table.
            target: "{tbl.name} (cache: {c.name})"
            message: "Command handler '{cmd.name}' ({m.name}) writes to '{tbl.name}' without invalidating cache '{c.name}'"
            query: |
              MATCH (rFn:Function)-[:READS_CACHE]->(c:Cache),
                    (rFn)-[:READS_FROM]->(tbl:Table),
                    (cmd:Function)-[:HANDLES_COMMAND]->(m:Message),
                    (cmd)-[:CALLS*0..4]->(wFn:Function)-[:WRITES_TO]->(wTbl:Table)
              WHERE toLower(wTbl.name) = toLower(tbl.name)
                AND NOT rFn.is_test AND NOT cmd.is_test AND NOT wFn.is_test
                AND NOT EXISTS((cmd)-[:INVALIDATES_CACHE]->(:Cache))
                AND NOT EXISTS((cmd)-[:WRITES_CACHE]->(:Cache))
                AND NOT EXISTS((cmd)-[:CALLS]->(:Function)-[:INVALIDATES_CACHE]->(:Cache))
                AND NOT EXISTS((cmd)-[:CALLS]->(:Function)-[:WRITES_CACHE]->(:Cache))
              RETURN DISTINCT cmd, m, tbl, c
            ratchet:
              enabled: true
              baseline: baselines/SIG-CACHE-001.json
            """),
        ("tx-no-external-http-in-transaction.yaml.example", """
            # Starter Gate: Transactions - No External Network IO Inside Database Transaction
            # Prevent outbound HTTP/gRPC calls while holding an open database transaction.
            # Rename to .yaml to activate for your workspace.
            id: SIG-TX-001
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: Transactions - No External Network IO Inside DB Transaction
            severity: Error
            description: Invoking external HTTP/gRPC services inside an open database transaction holds row locks across network timeouts and exhausts connection pools.
            remediation: Move outbound network calls outside the transaction scope or decouple them via a transactional Outbox event.
            target: "{tx.name} -> {es.name}"
            message: "Function '{fn.name}' inside transaction '{tx.name}' calls external service '{es.name}'"
            query: |
              MATCH (fn:Function)-[:EXECUTES_IN_TX]->(tx:Transaction),
                    (fn)-[:CALLS|CALLED_BY|ACCESSED_BY]-(es:ExternalService)
              WHERE NOT fn.is_test
              RETURN DISTINCT fn, tx, es
            ratchet:
              enabled: true
              baseline: baselines/SIG-TX-001.json
            """),
        ("tx-multi-write-command-requires-tx.yaml.example", """
            # Starter Gate: Transactions - Multi-Write Command Requires Transaction Boundary
            # Ensure command handlers mutating two or more distinct database tables execute inside a transaction.
            # Rename to .yaml to activate for your workspace.
            id: SIG-TX-002
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Responsibility
            name: Transactions - Multi-Table Write Without Transaction Boundary
            severity: Warning
            description: Command handlers writing to multiple distinct tables without a transaction boundary leave partial state on mid-flight failures.
            remediation: Wrap multi-table command execution in an explicit database transaction or a transactional pipeline behavior.
            target: "{cmd.name} ({tableCount} tables)"
            message: "Command handler '{cmd.name}' ({m.name}) writes to {tableCount} distinct tables outside an active transaction"
            query: |
              MATCH (cmd:Function)-[:HANDLES_COMMAND]->(m:Message),
                    (cmd)-[:CALLS*0..4]->(w:Function)-[:WRITES_TO]->(tbl:Table)
              WHERE NOT cmd.is_test AND NOT w.is_test
                AND NOT EXISTS((cmd)-[:EXECUTES_IN_TX]->(:Transaction))
              WITH cmd, m, count(DISTINCT tbl) AS tableCount
              WHERE tableCount >= 2
              RETURN cmd, m, tableCount
            ratchet:
              enabled: true
              baseline: baselines/SIG-TX-002.json
            """),
        ("tx-dual-write-without-outbox.yaml.example", """
            # Starter Gate: Transactions - No Dual Write to DB and Message Broker Without Outbox
            # Detect functions mutating a database table and publishing directly to a broker topic without an Outbox table write.
            # Rename to .yaml to activate for your workspace.
            id: SIG-TX-003
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Responsibility
            name: Transactions - Dual Write to DB and Broker Without Outbox
            severity: Warning
            description: Writing to a database table and publishing directly to a message broker risks lost or phantom messages when one succeeds and the other fails.
            remediation: Persist outgoing integration events to a transactional Outbox table in the same DB transaction and relay asynchronously.
            target: "{tbl.name} + {top.name}"
            message: "Function '{fn.name}' performs dual write to table '{tbl.name}' and broker topic '{top.name}' without Outbox"
            query: |
              MATCH (fn:Function)-[:WRITES_TO]->(tbl:Table),
                    (fn)-[:PUBLISHES|PUBLISHES_TO|PUBLISHED_BY]-(top:Topic)
              WHERE NOT fn.is_test
                AND NOT toLower(tbl.name) CONTAINS 'outbox'
              RETURN DISTINCT fn, tbl, top
            ratchet:
              enabled: true
              baseline: baselines/SIG-TX-003.json
            """),
        ("sec-unguarded-state-mutation.yaml.example", """
            # Starter Gate: Security - Mutating Endpoints Must Declare Authorization Policy
            # Ensure every POST/PUT/PATCH/DELETE endpoint writing to storage is protected by a Policy (or explicit Anonymous policy).
            # Rename to .yaml to activate for your workspace.
            id: SIG-SEC-001
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: Security - Unguarded Mutating Endpoint
            severity: Error
            description: State-mutating HTTP endpoints (POST, PUT, PATCH, DELETE) must declare an authentication/authorization Policy or an explicit public-access marker.
            remediation: Attach [Authorize], @UseGuards, @PreAuthorize, or an explicit [AllowAnonymous] / @Public policy to the endpoint or controller.
            target: "{ep.name} -> {tbl.name}"
            message: "Mutating endpoint '{ep.name}' writes to '{tbl.name}' via '{w.name}' without a GUARDED_BY authorization policy"
            query: |
              MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)
                    -[:CALLS*0..4]->(w:Function)-[:WRITES_TO]->(tbl:Table)
              WHERE ep.http_method IN ['POST', 'PUT', 'PATCH', 'DELETE']
                AND NOT fn.is_test AND NOT w.is_test
                AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy))
              RETURN DISTINCT fn, ep, w, tbl
            ratchet:
              enabled: true
              baseline: baselines/SIG-SEC-001.json
            """),
        ("sec-unprotected-pii-table-access.yaml.example", """
            # Starter Gate: Security - Unprotected Access to PII / PCI / Secret Tables
            # Ensure no anonymous or unguarded endpoint reads or writes tables classified as pii, pci, or secret.
            # Rename to .yaml to activate for your workspace.
            id: SIG-SEC-002
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: Security - Unprotected Access to Sensitive (PII/PCI/Secret) Tables
            severity: Warning
            description: Endpoints accessing tables classified as pii, pci, or secret must be protected by a non-anonymous authorization Policy.
            remediation: Protect the endpoint with an authenticated, role, permission, or custom_guard Policy, or verify the table classification.
            target: "{ep.name} -> {tbl.name} ({tbl.sensitivity})"
            message: "Unprotected or anonymous endpoint '{ep.name}' accesses sensitive table '{tbl.name}' ({tbl.sensitivity}) via '{fn.name}'"
            query: |
              MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)
                    -[:CALLS*0..4]->(acc:Function)-[:READS_FROM|WRITES_TO]->(tbl:Table)
              WHERE tbl.sensitivity IN ['pii', 'pci', 'secret']
                AND NOT fn.is_test AND NOT acc.is_test
                AND (NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy))
                     OR EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'anonymous'})))
              RETURN DISTINCT fn, ep, acc, tbl
            ratchet:
              enabled: true
              baseline: baselines/SIG-SEC-002.json
            """),
        ("sec-privileged-route-requires-role.yaml.example", """
            # Starter Gate: Security - Privileged Routes Must Require Role or Permission Policy
            # Ensure admin, internal, billing, and role-management endpoints require an explicit role, permission, or custom guard policy.
            # Rename to .yaml to activate for your workspace.
            id: SIG-SEC-003
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: Security - Privileged Route Without Role or Permission Policy
            severity: Error
            description: Privileged routes (/admin, /internal, /billing, /roles, /permissions, /audit) must enforce role, permission, or custom_guard authorization rather than bare authentication or no guard.
            remediation: Add a role or permission policy (e.g. [Authorize(Roles = "Admin")], @PreAuthorize, @Roles, or a dedicated custom guard) to the endpoint.
            target: "{ep.name}"
            message: "Privileged endpoint '{ep.name}' in '{ep.file_path}' lacks a role, permission, or custom_guard authorization Policy"
            query: |
              MATCH (ep:Endpoint)
              WHERE (toLower(ep.route_template) CONTAINS '/admin'
                  OR toLower(ep.route_template) CONTAINS '/internal'
                  OR toLower(ep.route_template) CONTAINS '/billing'
                  OR toLower(ep.route_template) CONTAINS '/roles'
                  OR toLower(ep.route_template) CONTAINS '/permissions')
                AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'role'}))
                AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'permission'}))
                AND NOT EXISTS((ep)-[:GUARDED_BY]->(:Policy {sub_kind: 'custom_guard'}))
              RETURN DISTINCT ep
            ratchet:
              enabled: true
              baseline: baselines/SIG-SEC-003.json
            """),
        ("sec-anonymous-db-write.yaml.example", """
            # Starter Gate: Security - Explicit Anonymous Endpoint Writes to Database
            # Flag endpoints marked Anonymous/Public that mutate database tables so every unauthenticated write path is explicitly reviewed.
            # Rename to .yaml to activate for your workspace.
            id: SIG-SEC-004
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Boundaries
            name: Security - Anonymous Endpoint Mutates Database
            severity: Warning
            description: Explicitly anonymous endpoints that write to database tables are prime targets for abuse, spam, and unauthorized state mutation.
            remediation: Confirm the write is intentionally public (e.g. registration or webhook with signature verification) and ratchet it in the baseline, or require authentication.
            target: "{ep.name} -> {tbl.name}"
            message: "Anonymous endpoint '{ep.name}' writes to table '{tbl.name}' via '{w.name}'"
            query: |
              MATCH (ep:Endpoint)-[:GUARDED_BY]->(pol:Policy {sub_kind: 'anonymous'}),
                    (ep)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)-[:CALLS*0..4]->(w:Function)-[:WRITES_TO]->(tbl:Table)
              WHERE NOT fn.is_test AND NOT w.is_test
              RETURN DISTINCT fn, ep, pol, w, tbl
            ratchet:
              enabled: true
              baseline: baselines/SIG-SEC-004.json
            """),
        ("sec-auth-guard-empty-catch.yaml.example", """
            # Starter Gate: Security - No Empty Catch Blocks Inside Authorization Guards
            # Prevent fail-open authorization bypasses caused by silently swallowed exceptions inside Policy enforcement functions.
            # Rename to .yaml to activate for your workspace.
            id: SIG-SEC-005
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Implementation
            name: Security - Silent Exception Swallowing in Auth Guard
            severity: Error
            description: Empty catch blocks inside authorization guard or requirement handler functions can silently fail open and bypass access control.
            remediation: Explicitly deny access (return false / throw Unauthorized) and log the failure inside the catch block.
            target: "{pol.name} -> {fn.name}"
            message: "Authorization guard '{fn.name}' enforcing policy '{pol.name}' contains an empty catch block at line {c.line}"
            query: |
              MATCH (pol:Policy)-[:ENFORCED_BY]->(fn:Function)-[:DECLARES]->(c:CatchClause)
              WHERE c.is_empty = true AND NOT fn.is_test
              RETURN DISTINCT fn, pol, c
            ratchet:
              enabled: true
              baseline: baselines/SIG-SEC-005.json
            """),
        ("job-cron-mutation-audit.yaml.example", """
            # Starter Gate: Background Jobs - Scheduled Cron State Mutations
            # Surface scheduled cron jobs that directly or transitively mutate database tables or publish events.
            # Rename to .yaml to activate for your workspace.
            id: SIG-JOB-001
            category: Architectural
            platform: agnostic
            scope: Method
            dimension: Responsibility
            name: Background Jobs - Scheduled Cron Mutation Audit
            severity: Warning
            description: Scheduled cron jobs running across multiple replicas can race or duplicate writes unless coordinated with idempotency keys or distributed locks.
            remediation: Verify distributed locking (e.g. ShedLock, Redis lock, advisory lock) and idempotent upserts on cron-triggered write paths.
            target: "{job.name} -> {target.name}"
            message: "Cron job '{job.name}' ({job.framework}) mutates '{target.name}' via '{w.name}'"
            query: |
              MATCH (job:Job)-[:TRIGGERS]->(fn:Function)
                    -[:CALLS*0..4]->(w:Function)-[r:WRITES_TO|PUBLISHES]->(target)
              WHERE job.sub_kind = 'cron' AND NOT fn.is_test AND NOT w.is_test
              RETURN DISTINCT fn, job, w, r, target
            ratchet:
              enabled: true
              baseline: baselines/SIG-JOB-001.json
            """),
        ("ddd-no-shared-table-writes-across-contexts.yaml.example", """
            # Starter Gate: DDD - No Shared Table Writes Across Services
            # Prevent two distinct Services from directly writing to the same database table.
            # Rename to .yaml to activate for your workspace.
            id: SIG-DDD-001
            category: Architectural
            platform: agnostic
            scope: Module
            dimension: Boundaries
            name: DDD - No Shared Table Writes Across Services
            severity: Error
            description: Each database table's write lifecycle should belong to a single Service or Bounded Context; shared multi-service table writes create tight schema coupling.
            remediation: Consolidate writes inside the owning Service and integrate across boundaries via Commands, Events, or APIs.
            target: "{tbl.name} ({m1.name} & {m2.name})"
            message: "Table '{tbl.name}' is written by both '{m1.name}' ('{w1.name}') and '{m2.name}' ('{w2.name}')"
            query: |
              MATCH (m1:Service)-[:IMPLEMENTED_BY]->(p1:Project),
                    (m2:Service)-[:IMPLEMENTED_BY]->(p2:Project),
                    (w1:Function)-[:WRITES_TO]->(tbl:Table),
                    (w2:Function)-[:WRITES_TO]->(tbl2:Table)
              WHERE m1.name <> m2.name
                AND toLower(tbl.name) = toLower(tbl2.name)
                AND NOT toLower(tbl.name) IN ['outbox', 'outbox_messages', '__efmigrationshistory', 'flyway_schema_history', 'schema_migrations']
                AND w1.project_name = p1.name AND w2.project_name = p2.name
                AND NOT w1.is_test AND NOT w2.is_test
              RETURN DISTINCT w1, tbl, m1, w2, m2
            ratchet:
              enabled: true
              baseline: baselines/SIG-DDD-001.json
            """),
        ("ddd-no-cross-module-table-bypass.yaml.example", """
            # Starter Gate: DDD - No Cross-Module Direct Table Bypass
            # Prevent a module/service from directly reading or writing a database table owned by another module.
            # Rename to .yaml to activate for your workspace.
            id: SIG-DDD-002
            category: Architectural
            platform: agnostic
            scope: Module
            dimension: Boundaries
            name: DDD - No Cross-Module Direct Table Bypass
            severity: Error
            description: Modules must access another module's owned tables only through its public API endpoints or CQRS message contracts, never via direct SQL/ORM queries.
            remediation: Replace direct cross-module table access with an API call, Query contract, or integration event projection.
            target: "{callerMod.name} -> {tbl.name} (owned by {ownerMod.name})"
            message: "Function '{fn.name}' in '{callerMod.name}' directly accesses table '{tbl.name}' owned by '{ownerMod.name}'"
            query: |
              MATCH (ownerMod:Service)-[:OWNS]->(tbl:Table),
                    (callerMod:Service)-[:IMPLEMENTED_BY]->(p:Project),
                    (fn:Function)-[:READS_FROM|WRITES_TO]->(callerTbl:Table)
              WHERE ownerMod.name <> callerMod.name
                AND toLower(tbl.name) = toLower(callerTbl.name)
                AND NOT toLower(tbl.name) IN ['outbox', 'outbox_messages', '__efmigrationshistory', 'flyway_schema_history', 'schema_migrations']
                AND fn.project_name = p.name
                AND NOT fn.is_test
              RETURN DISTINCT fn, callerMod, tbl, ownerMod
            ratchet:
              enabled: true
              baseline: baselines/SIG-DDD-002.json
            """)
    ];
}
