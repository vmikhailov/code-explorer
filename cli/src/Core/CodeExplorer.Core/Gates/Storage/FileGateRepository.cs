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
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var content = await File.ReadAllTextAsync(file, cancellationToken);
                    var relFile = Path.GetRelativePath(_workspacePath, file).Replace('\\', '/');
                    var gate = GateSerializer.DeserializeGate(content, relFile);
                    gatesById[gate.Id] = gate;
                }
                catch
                {
                    // Ignore or log malformed gate files
                }
            }
        }

        return gatesById.Values.OrderBy(g => g.Id).ToList();
    }

    public async Task<SemanticGate?> GetGateByIdAsync(string gateId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gateId)) return null;

        var all = await GetAllGatesAsync(cancellationToken);
        return all.FirstOrDefault(g => string.Equals(g.Id, gateId.Trim(), StringComparison.OrdinalIgnoreCase));
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
        catch
        {
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
                # Semantic Invariant Gate Example Template
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
                query: |
                  MATCH (f:Function)
                  WHERE coalesce(f.loc, f.end_line - f.start_line + 1) > 500
                    AND NOT coalesce(f.is_test, false)
                  RETURN 
                    coalesce(f.path, f.file_path) AS file,
                    coalesce(f.start_line, 1) AS line,
                    coalesce(f.start_col, 1) AS column,
                    f.name AS symbol,
                    toString(coalesce(f.loc, f.end_line - f.start_line + 1)) + ' lines' AS target,
                    'Method ' + f.name + ' exceeds maximum allowed length of 500 lines' AS message
                ratchet:
                  enabled: true
                  baseline: baselines/SIG-M-I-002.json
                """;
            await File.WriteAllTextAsync(exampleFile, exampleYaml, cancellationToken);
            return 1;
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
}
