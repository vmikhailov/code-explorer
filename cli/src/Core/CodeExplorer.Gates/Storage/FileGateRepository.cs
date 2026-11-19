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
