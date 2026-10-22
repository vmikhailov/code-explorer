using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            violations = ParseViolations(gate.Id, rawJson, gate.Message);

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
        var allGates = gates ?? (await _gateRepository.GetAllGatesAsync(cancellationToken));

        if (scopeFilter.HasValue)
        {
            allGates = allGates.Where(g => g.Scope == scopeFilter.Value);
        }
        if (dimFilter.HasValue)
        {
            allGates = allGates.Where(g => g.Dimension == dimFilter.Value);
        }
        if (categoryFilter.HasValue)
        {
            allGates = allGates.Where(g => g.Category == categoryFilter.Value);
        }
        if (!string.IsNullOrWhiteSpace(platformFilter))
        {
            allGates = allGates.Where(g => string.Equals(g.Platform, platformFilter.Trim(), StringComparison.OrdinalIgnoreCase));
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

    public static List<GateViolation> ParseViolations(string gateId, string rawJson)
        => ParseViolations(gateId, rawJson, messageTemplate: null);

    public static List<GateViolation> ParseViolations(string gateId, string rawJson, string? messageTemplate)
    {
        var violations = new List<GateViolation>();
        if (string.IsNullOrWhiteSpace(rawJson)) return violations;

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return violations;

            var occurrenceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) continue;

                var file = ExtractStringProperty(row, "file", "filePath", "path");
                var line = ExtractIntProperty(row, "line", "startLine", "row", "startRow");
                var column = ExtractIntProperty(row, "column", "col", "startCol", "startColumn", "char");
                var symbol = ExtractStringProperty(row, "symbol", "name", "source");
                var target = ExtractStringProperty(row, "target", "destination", "illegal_dependency", "dependency", "blocking_target", "callee");
                var message = ExtractStringProperty(row, "message", "description", "details");
                string? nodeId = null;
                string? targetNodeId = null;

                // Discover graph nodes returned by query (e.g. 'RETURN fn, target' or 'RETURN src, tgt')
                var graphNodes = new List<(string PropName, string? Id, string? Kind, JsonElement Properties)>();
                foreach (var prop in row.EnumerateObject())
                {
                    if (TryGetGraphNode(prop.Value, out var nId, out var nKind, out var nProps))
                    {
                        graphNodes.Add((prop.Name, nId, nKind, nProps));
                    }
                }

                if (graphNodes.Count > 0)
                {
                    // Identify source node
                    var srcNode = graphNodes.FirstOrDefault(n => IsSourceNodeName(n.PropName));
                    if (srcNode.Properties.ValueKind == JsonValueKind.Undefined)
                    {
                        srcNode = graphNodes.FirstOrDefault(n =>
                            ExtractStringProperty(n.Properties, "file_path", "path", "filePath") != null ||
                            ExtractIntProperty(n.Properties, "start_line", "startLine", "line") > 0);
                        if (srcNode.Properties.ValueKind == JsonValueKind.Undefined)
                        {
                            srcNode = graphNodes[0];
                        }
                    }

                    if (srcNode.Properties.ValueKind == JsonValueKind.Object)
                    {
                        file ??= ExtractStringProperty(srcNode.Properties, "file_path", "path", "filePath");
                        if (line == 0) line = ExtractIntProperty(srcNode.Properties, "start_line", "startLine", "line", "row");
                        if (column == 0) column = ExtractIntProperty(srcNode.Properties, "start_col", "startCol", "column", "col", "char");
                        symbol ??= ExtractStringProperty(srcNode.Properties, "name", "symbol", "id");
                        nodeId = srcNode.Id;
                    }

                    // Identify target node (distinct from source node)
                    var targetCandidates = graphNodes.Where(n => n.PropName != srcNode.PropName).ToList();
                    var tgtNode = targetCandidates.FirstOrDefault(n => IsTargetNodeName(n.PropName));
                    if (tgtNode.Properties.ValueKind == JsonValueKind.Undefined && targetCandidates.Count > 0)
                    {
                        tgtNode = targetCandidates[0];
                    }

                    if (tgtNode.Properties.ValueKind == JsonValueKind.Object)
                    {
                        target ??= ExtractStringProperty(tgtNode.Properties, "name", "full_name", "symbol", "id");
                        targetNodeId = tgtNode.Id;
                    }
                }

                // If parameterized message template is supplied, format it
                if (!string.IsNullOrWhiteSpace(messageTemplate))
                {
                    message = InterpolateMessage(messageTemplate, row, graphNodes, file, line, column, symbol, target);
                }
                else if (string.IsNullOrWhiteSpace(message))
                {
                    message = !string.IsNullOrEmpty(target)
                        ? $"Invariant violation: {symbol ?? "element"} references {target}"
                        : $"Invariant violation in {symbol ?? file ?? "element"}";
                }

                var tupleKey = $"{GateFingerprintGenerator.NormalizeFilePath(file)}|{(symbol ?? "").Trim()}|{(target ?? "").Trim()}";
                var occurrence = occurrenceCounts.GetValueOrDefault(tupleKey, 0) + 1;
                occurrenceCounts[tupleKey] = occurrence;

                var fp = GateFingerprintGenerator.Generate(gateId, file, symbol, target, occurrence);

                violations.Add(new GateViolation
                {
                    GateId = gateId,
                    File = file,
                    Line = line,
                    Column = column,
                    Symbol = symbol,
                    Target = target,
                    Message = message,
                    NodeId = nodeId,
                    TargetNodeId = targetNodeId,
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

    private static bool IsSourceNodeName(string name)
        => name.Equals("fn", StringComparison.OrdinalIgnoreCase)
        || name.Equals("func", StringComparison.OrdinalIgnoreCase)
        || name.Equals("function", StringComparison.OrdinalIgnoreCase)
        || name.Equals("src", StringComparison.OrdinalIgnoreCase)
        || name.Equals("source", StringComparison.OrdinalIgnoreCase)
        || name.Equals("caller", StringComparison.OrdinalIgnoreCase)
        || name.Equals("cls", StringComparison.OrdinalIgnoreCase)
        || name.Equals("class", StringComparison.OrdinalIgnoreCase)
        || name.Equals("type", StringComparison.OrdinalIgnoreCase)
        || name.Equals("node", StringComparison.OrdinalIgnoreCase)
        || name.Equals("from", StringComparison.OrdinalIgnoreCase)
        || name.Equals("p", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project", StringComparison.OrdinalIgnoreCase);

    private static bool IsTargetNodeName(string name)
        => name.Equals("target", StringComparison.OrdinalIgnoreCase)
        || name.Equals("tgt", StringComparison.OrdinalIgnoreCase)
        || name.Equals("callee", StringComparison.OrdinalIgnoreCase)
        || name.Equals("destination", StringComparison.OrdinalIgnoreCase)
        || name.Equals("to", StringComparison.OrdinalIgnoreCase)
        || name.Equals("pkg", StringComparison.OrdinalIgnoreCase)
        || name.Equals("package", StringComparison.OrdinalIgnoreCase)
        || name.Equals("dep", StringComparison.OrdinalIgnoreCase)
        || name.Equals("dependency", StringComparison.OrdinalIgnoreCase)
        || name.Equals("contract", StringComparison.OrdinalIgnoreCase)
        || name.Equals("tbl", StringComparison.OrdinalIgnoreCase)
        || name.Equals("table", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetGraphNode(JsonElement element, out string? id, out string? kind, out JsonElement properties)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty("properties", out properties) &&
            properties.ValueKind == JsonValueKind.Object)
        {
            id = element.TryGetProperty("id", out var idElem) ? idElem.GetString() : null;
            kind = element.TryGetProperty("kind", out var kindElem) ? kindElem.GetString() : null;
            return true;
        }

        id = null;
        kind = null;
        properties = default;
        return false;
    }

    private static readonly Regex TokenRegex = new(@"\{([a-zA-Z0-9_\.]+)\}", RegexOptions.Compiled);

    public static string InterpolateMessage(
        string template,
        JsonElement row,
        List<(string PropName, string? Id, string? Kind, JsonElement Properties)> graphNodes,
        string? file,
        int line,
        int column,
        string? symbol,
        string? target)
    {
        if (string.IsNullOrWhiteSpace(template)) return "";

        return TokenRegex.Replace(template, match =>
        {
            var token = match.Groups[1].Value;

            // Direct standard aliases
            if (string.Equals(token, "file", StringComparison.OrdinalIgnoreCase)) return file ?? "";
            if (string.Equals(token, "line", StringComparison.OrdinalIgnoreCase)) return line > 0 ? line.ToString() : "";
            if (string.Equals(token, "column", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "col", StringComparison.OrdinalIgnoreCase)) return column > 0 ? column.ToString() : "";
            if (string.Equals(token, "symbol", StringComparison.OrdinalIgnoreCase)) return symbol ?? "";
            if (string.Equals(token, "target", StringComparison.OrdinalIgnoreCase)) return target ?? "";
            if (string.Equals(token, "location", StringComparison.OrdinalIgnoreCase)) return line > 0 ? $"{file}:{line}" : (file ?? "");

            // Property access on node: e.g. {fn.name}, {target.name}, {pkg.name}, {fn.start_line}
            if (token.Contains('.'))
            {
                var dotIdx = token.IndexOf('.');
                var nodeKey = token[..dotIdx];
                var propKey = token[(dotIdx + 1)..];

                var matchedNode = graphNodes.FirstOrDefault(n => string.Equals(n.PropName, nodeKey, StringComparison.OrdinalIgnoreCase));
                if (matchedNode.Properties.ValueKind == JsonValueKind.Object)
                {
                    if (matchedNode.Properties.TryGetProperty(propKey, out var pVal))
                    {
                        return pVal.ToString();
                    }
                }
            }

            // Flat column lookup in row: e.g. {illegal_dependency}, {blocking_target}
            if (row.TryGetProperty(token, out var colVal))
            {
                if (colVal.ValueKind == JsonValueKind.String) return colVal.GetString() ?? "";
                return colVal.ToString();
            }

            // Check if token matches a node property directly on the source node (e.g. {name})
            if (graphNodes.Count > 0 && graphNodes[0].Properties.TryGetProperty(token, out var defaultPropVal))
            {
                return defaultPropVal.ToString();
            }

            return match.Value;
        });
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
