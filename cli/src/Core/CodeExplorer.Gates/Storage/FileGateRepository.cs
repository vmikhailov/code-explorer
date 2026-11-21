using CodeExplorer.Core.Gates.Models;

namespace CodeExplorer.Core.Gates.Storage;

/// <summary>
/// File-based gate repository managing workspace gates in .codeexplorer/gates/*.yaml
/// and baselines in .codeexplorer/baselines/*.json, merged with standard BuiltIn gates.
/// </summary>
public class FileGateRepository : IGateRepository
{
    private readonly string _workspacePath;
    private readonly string _gatesDir;
    private readonly string _baselinesDir;

    public FileGateRepository(string workspacePath)
    {
        _workspacePath = Path.GetFullPath(workspacePath);
        _gatesDir = Path.Combine(_workspacePath, ".codeexplorer", "gates");
        _baselinesDir = Path.Combine(_workspacePath, ".codeexplorer", "baselines");
    }

    public async Task<IReadOnlyList<SemanticGate>> GetAllGatesAsync(CancellationToken cancellationToken = default)
    {
        var gatesById = new Dictionary<string, SemanticGate>(StringComparer.OrdinalIgnoreCase);

        // Discover workspace gates in .codeexplorer/gates/
        if (Directory.Exists(_gatesDir))
        {
            var files = Directory.GetFiles(_gatesDir, "*.yaml", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(_gatesDir, "*.yml", SearchOption.AllDirectories));

            foreach (var file in files)
            {
                var relFile = Path.GetRelativePath(_workspacePath, file).Replace('\\', '/');
                try
                {
                    var content = await File.ReadAllTextAsync(file, cancellationToken);
                    var gate = GateSerializer.DeserializeGate(content, relFile);
                    if (gatesById.TryGetValue(gate.Id, out var existing))
                    {
                        Console.Error.WriteLine($"[Gate Warning] Duplicate gate ID '{gate.Id}' found in '{relFile}' (overriding earlier gate from '{existing.Source}').");
                    }
                    gatesById[gate.Id] = gate;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Gate Warning] Failed to load gate from '{relFile}': {ex.Message}");
                }
            }
        }

        return [.. gatesById.Values.OrderBy(g => g.Id)];
    }

    public async Task<SemanticGate?> GetGateByIdAsync(string gateId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gateId)) return null;

        var all = await GetAllGatesAsync(cancellationToken);
        var found = all.FirstOrDefault(g => string.Equals(g.Id, gateId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (found != null) return found;

        return BuiltInGateCatalog.GetDefaultGates().FirstOrDefault(g => string.Equals(g.Id, gateId.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? BuiltInGateCatalog.GetExampleGates().FirstOrDefault(g => string.Equals(g.Id, gateId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<GateBaseline?> LoadBaselineAsync(SemanticGate gate, CancellationToken cancellationToken = default)
    {
        var baselinePath = ResolveBaselinePath(gate);
        if (!File.Exists(baselinePath)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(baselinePath, cancellationToken);
            return GateSerializer.DeserializeBaseline(json);
        }
        catch (Exception ex)
        {
            var relPath = Path.GetRelativePath(_workspacePath, baselinePath).Replace('\\', '/');
            Console.Error.WriteLine($"[Gate Warning] Failed to load baseline from '{relPath}': {ex.Message}");
            return null;
        }
    }

    public async Task SaveBaselineAsync(GateBaseline baseline, SemanticGate gate, CancellationToken cancellationToken = default)
    {
        var baselinePath = ResolveBaselinePath(gate);
        var dir = Path.GetDirectoryName(baselinePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = GateSerializer.SerializeBaseline(baseline);
        await File.WriteAllTextAsync(baselinePath, json, cancellationToken);
    }

    public async Task<int> InitStarterGatesAsync(bool overwrite = false, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_gatesDir))
        {
            Directory.CreateDirectory(_gatesDir);
        }
        if (!Directory.Exists(_baselinesDir))
        {
            Directory.CreateDirectory(_baselinesDir);
        }

        var starterTemplates = new (string FileName, string Content)[]
        {
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
                # Ensure query handlers and query services never directly or transitively write to tables or publish events.
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
                message: "Query handler '{t.name}.{q.name}' in '{q.file_path}' triggers state mutation on '{target.name}' via '{w.name}'"
                # Query side is identified by framework semantics first (implements IQueryHandler / QueryHandler,
                # @QueryHandler decorator or annotation), with a folder convention fallback for languages
                # without interface or decorator markers (e.g. Go packages named query/queries).
                query: |
                  MATCH (t:Type)-[:HAS_METHOD]->(q:Function)-[:CALLS*0..4]->(w:Function)-[r:WRITES_TO|PUBLISHES]->(target)
                  WHERE NOT coalesce(t.is_test, false)
                    AND NOT coalesce(q.is_test, false)
                    AND NOT coalesce(w.is_test, false)
                    AND (coalesce(t.implements, '') CONTAINS 'QueryHandler'
                      OR coalesce(t.attributes, '') CONTAINS 'QueryHandler'
                      OR (coalesce(t.implements, '') CONTAINS 'RequestHandler<' AND (t.implements CONTAINS 'Query,' OR t.implements CONTAINS 'Query>'))
                      OR ((toLower(t.file_path) CONTAINS '/queries/' OR toLower(t.file_path) CONTAINS '/query/')
                        AND t.name ENDS WITH 'Handler'))
                  RETURN DISTINCT t, q, w, r, target
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
                message: "Query handler '{t.name}.{q.name}' in '{q.file_path}' invokes command handler '{ct.name}.{c.name}' in '{c.file_path}'"
                query: |
                  MATCH (t:Type)-[:HAS_METHOD]->(q:Function)-[r:CALLS]->(c:Function)<-[:HAS_METHOD]-(ct:Type)
                  WHERE NOT coalesce(t.is_test, false)
                    AND NOT coalesce(ct.is_test, false)
                    AND (coalesce(t.implements, '') CONTAINS 'QueryHandler'
                      OR coalesce(t.attributes, '') CONTAINS 'QueryHandler'
                      OR (coalesce(t.implements, '') CONTAINS 'RequestHandler<' AND (t.implements CONTAINS 'Query,' OR t.implements CONTAINS 'Query>'))
                      OR ((toLower(t.file_path) CONTAINS '/queries/' OR toLower(t.file_path) CONTAINS '/query/')
                        AND t.name ENDS WITH 'Handler'))
                    AND (coalesce(ct.implements, '') CONTAINS 'CommandHandler'
                      OR coalesce(ct.attributes, '') CONTAINS 'CommandHandler'
                      OR (coalesce(ct.implements, '') CONTAINS 'RequestHandler<' AND (ct.implements CONTAINS 'Command,' OR ct.implements CONTAINS 'Command>'))
                      OR ((toLower(ct.file_path) CONTAINS '/commands/' OR toLower(ct.file_path) CONTAINS '/command/')
                        AND ct.name ENDS WITH 'Handler'))
                  RETURN DISTINCT t, q, r, c, ct
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
                  MATCH (ep:Endpoint)-[:EXPOSED_BY|TRIGGERS]->(fn:Function)-[:CALLS*0..5]->(w:Function)-[r:WRITES_TO|PUBLISHES]->(target)
                  WHERE ep.http_method = 'GET'
                    AND NOT coalesce(fn.is_test, false)
                    AND NOT coalesce(w.is_test, false)
                  RETURN DISTINCT fn, ep, w, r, target
                ratchet:
                  enabled: true
                  baseline: baselines/SIG-CQRS-003.json
                """)
        };

        var createdAny = false;
        foreach (var (fileName, content) in starterTemplates)
        {
            var filePath = Path.Combine(_gatesDir, fileName);
            if (!File.Exists(filePath) || overwrite)
            {
                try
                {
                    await File.WriteAllTextAsync(filePath, content, cancellationToken);
                    createdAny = true;
                }
                catch (IOException) when (!overwrite && File.Exists(filePath))
                {
                    // Another instance concurrently created the starter example file
                }
            }
        }

        return createdAny ? 1 : 0;
    }

    private string ResolveBaselinePath(SemanticGate gate)
    {
        if (!string.IsNullOrWhiteSpace(gate.Ratchet.Baseline))
        {
            var customPath = gate.Ratchet.Baseline.Replace('\\', '/');
            return Path.IsPathRooted(customPath)
                ? customPath
                : Path.Combine(_workspacePath, ".codeexplorer", customPath.TrimStart('/'));
        }

        return Path.Combine(_baselinesDir, $"{gate.Id}.json");
    }

    public async Task<string> SaveGateAsync(SemanticGate gate, string? targetPath = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gate);
        if (string.IsNullOrWhiteSpace(gate.Id))
        {
            throw new ArgumentException("Gate ID cannot be empty.", nameof(gate));
        }

        string fullPath;
        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            fullPath = Path.IsPathRooted(targetPath)
                ? targetPath
                : Path.Combine(_workspacePath, targetPath.TrimStart('/', '\\'));
        }
        else if (!string.IsNullOrWhiteSpace(gate.Source) && !string.Equals(gate.Source, "BuiltIn", StringComparison.OrdinalIgnoreCase) && gate.Source.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
        {
            fullPath = Path.IsPathRooted(gate.Source)
                ? gate.Source
                : Path.Combine(_workspacePath, gate.Source.TrimStart('/', '\\'));
        }
        else
        {
            var categorySubdir = gate.Category.ToString().ToLowerInvariant();
            var categoryDir = Path.Combine(_gatesDir, categorySubdir);
            var safeId = string.Join("-", gate.Id.ToLowerInvariant().Split(Path.GetInvalidFileNameChars()));
            fullPath = Path.Combine(categoryDir, $"{safeId}.yaml");
        }

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var yaml = GateSerializer.SerializeGate(gate);
        await File.WriteAllTextAsync(fullPath, yaml, cancellationToken);

        var relPath = Path.GetRelativePath(_workspacePath, fullPath).Replace('\\', '/');
        return relPath;
    }

    public Task<bool> DeleteGateAsync(string gateId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gateId) || !Directory.Exists(_gatesDir))
        {
            return Task.FromResult(false);
        }

        var files = Directory.GetFiles(_gatesDir, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_gatesDir, "*.yml", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                var gate = GateSerializer.DeserializeGate(content, file);
                if (string.Equals(gate.Id, gateId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                    return Task.FromResult(true);
                }
            }
            catch
            {
                // ignore
            }
        }

        return Task.FromResult(false);
    }
}
