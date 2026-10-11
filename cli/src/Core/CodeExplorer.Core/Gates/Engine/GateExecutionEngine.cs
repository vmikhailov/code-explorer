using System.Diagnostics;
using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Core.Gates.Storage;

namespace CodeExplorer.Core.Gates.Engine;

/// <summary>
/// Executes Semantic Invariant Gates against the semantic graph database (graph.db).
/// </summary>
public class GateExecutionEngine
{
    private readonly IGraphClient _dbClient;
    private readonly IGateRepository _gateRepository;

    public GateExecutionEngine(IGraphClient dbClient, IGateRepository gateRepository)
    {
        _dbClient = dbClient ?? throw new ArgumentNullException(nameof(dbClient));
        _gateRepository = gateRepository ?? throw new ArgumentNullException(nameof(gateRepository));
    }

    public async Task<GateCheckResult> CheckGateAsync(SemanticGate gate, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var violations = new List<GateViolation>();

        try
        {
            var query = gate.Query;
            var paramDict = new Dictionary<string, object?>();
            foreach (var (k, v) in gate.Parameters)
            {
                paramDict[k] = v;
            }

            var rawJson = await _dbClient.ExecuteQueryAsync(query, paramDict, cancellationToken);
            violations = ParseViolations(gate.Id, rawJson);

            var baseline = await _gateRepository.LoadBaselineAsync(gate, cancellationToken);
            var (status, newViolations, tolerated, baselineCount) = RatchetService.Evaluate(gate, violations, baseline);

            sw.Stop();
            return new GateCheckResult
            {
                Gate = gate,
                Status = status,
                Violations = violations,
                NewViolations = newViolations,
                ToleratedViolations = tolerated,
                BaselineCount = baselineCount,
                Duration = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new GateCheckResult
            {
                Gate = gate,
                Status = GateCheckStatus.Error,
                Violations = violations,
                Duration = sw.Elapsed,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<GateSuiteResult> CheckSuiteAsync(
        IEnumerable<SemanticGate>? gates = null,
        InvariantScope? scopeFilter = null,
        InvariantDimension? dimFilter = null,
        GateCategory? categoryFilter = null,
        string? platformFilter = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var allGates = gates?.ToList() ?? (await _gateRepository.GetAllGatesAsync(cancellationToken)).ToList();

        if (scopeFilter.HasValue)
        {
            allGates = allGates.Where(g => g.Scope == scopeFilter.Value).ToList();
        }
        if (dimFilter.HasValue)
        {
            allGates = allGates.Where(g => g.Dimension == dimFilter.Value).ToList();
        }
        if (categoryFilter.HasValue)
        {
            allGates = allGates.Where(g => g.Category == categoryFilter.Value).ToList();
        }
        if (!string.IsNullOrWhiteSpace(platformFilter))
        {
            allGates = allGates.Where(g => string.Equals(g.Platform, platformFilter.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var results = new List<GateCheckResult>();
        foreach (var gate in allGates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var res = await CheckGateAsync(gate, cancellationToken);
            results.Add(res);
        }

        sw.Stop();
        return new GateSuiteResult
        {
            Results = results,
            TotalDuration = sw.Elapsed
        };
    }

    private static List<GateViolation> ParseViolations(string gateId, string rawJson)
    {
        var violations = new List<GateViolation>();
        if (string.IsNullOrWhiteSpace(rawJson)) return violations;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return violations;

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) continue;

                var file = ExtractStringProperty(row, "file", "filePath", "path");
                var line = ExtractIntProperty(row, "line", "startLine", "row", "startRow");
                var column = ExtractIntProperty(row, "column", "col", "startCol", "startColumn", "char");
                var symbol = ExtractStringProperty(row, "symbol", "name", "source");
                var target = ExtractStringProperty(row, "target", "destination");
                var message = ExtractStringProperty(row, "message", "description", "details");

                var fp = GateFingerprintGenerator.Generate(gateId, file, symbol, target);

                violations.Add(new GateViolation
                {
                    GateId = gateId,
                    File = file,
                    Line = line,
                    Column = column,
                    Symbol = symbol,
                    Target = target,
                    Message = message,
                    Fingerprint = fp
                });
            }
        }
        catch
        {
            // Ignore JSON parsing errors
        }

        return violations;
    }

    private static string? ExtractStringProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (element.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.String)
                {
                    return val.GetString();
                }
                if (val.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                {
                    return val.ToString();
                }
            }
        }
        return null;
    }

    private static int ExtractIntProperty(JsonElement element, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (element.TryGetProperty(prop, out var val))
            {
                if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var i))
                {
                    return i;
                }
                if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var si))
                {
                    return si;
                }
            }
        }
        return 0;
    }
}
