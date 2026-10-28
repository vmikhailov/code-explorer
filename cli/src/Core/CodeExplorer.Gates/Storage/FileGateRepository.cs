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

        var exampleFile = Path.Combine(_gatesDir, "example-gate.yaml.example");
        if (!File.Exists(exampleFile) || overwrite)
        {
            var exampleYaml = """
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
                  WHERE loc > 500 AND NOT coalesce(f.is_test, false)
                  RETURN f, loc
                ratchet:
                  enabled: true
                  baseline: baselines/SIG-M-I-002.json
                """;
            try
            {
                await File.WriteAllTextAsync(exampleFile, exampleYaml, cancellationToken);
                return 1;
            }
            catch (IOException) when (!overwrite && File.Exists(exampleFile))
            {
                // Another instance concurrently created the starter example file
                return 0;
            }
        }

        return 0;
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
