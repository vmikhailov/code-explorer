using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
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

            var result = await _dbClient.ExecuteCypherAsync(query, paramDict, cancellationToken);
            violations = ParseViolations(gate.Id, result, gate.Message, gate.Target);

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
        => ParseViolations(gateId, rawJson, messageTemplate: null, targetTemplate: null);

    public static List<GateViolation> ParseViolations(string gateId, string rawJson, string? messageTemplate)
        => ParseViolations(gateId, rawJson, messageTemplate, targetTemplate: null);

    public static List<GateViolation> ParseViolations(
        string gateId,
        string rawJson,
        string? messageTemplate,
        string? targetTemplate)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return [];
        try
        {
            return ParseViolations(gateId, CypherQueryResult.FromJson(rawJson), messageTemplate, targetTemplate);
        }
        catch
        {
            return [];
        }
    }

    public static List<GateViolation> ParseViolations(
        string gateId,
        CypherQueryResult result,
        string? messageTemplate,
        string? targetTemplate)
    {
        var violations = new List<GateViolation>();
        if (result.Count == 0) return violations;

        var occurrenceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in result.Rows)
        {
            var explicitFile = ExtractStringProperty(row, "file", "filePath", "path");
            var explicitLine = ExtractIntProperty(row, "line", "startLine", "row", "startRow");
            var explicitColumn = ExtractIntProperty(row, "column", "col", "startCol", "startColumn", "char");
            var file = explicitFile;
            var line = explicitLine;
            var column = explicitColumn;
            var symbol = ExtractStringProperty(row, "symbol", "name", "source");
            var target = ExtractStringProperty(row, "target", "destination", "illegal_dependency", "dependency", "blocking_target", "callee");
            var message = ExtractStringProperty(row, "message", "description", "details");
            string? nodeId = null;
            string? targetNodeId = null;

            // Discover graph nodes and graph relationships returned by query (e.g. 'RETURN fn, r, target')
            var graphNodes = new List<(string PropName, string? Id, CodeExplorer.Cypher.Common.NodeKind Kind, CypherValue Properties)>();
            var graphEdges = new List<(string PropName, string? FromId, string? ToId, string? RelType, CypherValue Properties)>();

            var colCount = Math.Min(row.Columns.Count, row.Values.Count);
            for (var i = 0; i < colCount; i++)
            {
                var propName = row.Columns[i];
                var propVal = row.Values[i];
                if (TryGetGraphRelationship(propVal, out var fId, out var tId, out var rType, out var rProps))
                {
                    graphEdges.Add((propName, fId, tId, rType, rProps));
                }
                else if (TryGetGraphNode(propVal, out var nId, out var nKind, out var nProps))
                {
                    graphNodes.Add((propName, nId, nKind, nProps));
                }
            }

            if (graphNodes.Count > 0)
            {
                // Prefer non-CallLiteral node as the primary source symbol when multiple nodes are returned
                var nonLiteralNodes = graphNodes.Where(n => !IsLiteralEvidenceNode(n.PropName, n.Kind)).ToList();
                var candidateSourcePool = nonLiteralNodes.Count > 0 ? nonLiteralNodes : graphNodes;

                var srcNode = candidateSourcePool.FirstOrDefault(n => IsSourceNodeName(n.PropName));
                if (srcNode.Properties.Kind == CypherValueKind.Null)
                {
                    srcNode = candidateSourcePool.FirstOrDefault(n =>
                        ExtractStringProperty(n.Properties, "file_path", "path", "filePath") != null ||
                        ExtractIntProperty(n.Properties, "start_line", "startLine", "line") > 0);
                    if (srcNode.Properties.Kind == CypherValueKind.Null)
                    {
                        srcNode = candidateSourcePool[0];
                    }
                }

                if (srcNode.Properties.Kind == CypherValueKind.Object)
                {
                    file ??= ExtractStringProperty(srcNode.Properties, "file_path", "path", "filePath");
                    if (line == 0) line = ExtractIntProperty(srcNode.Properties, "start_line", "startLine", "line", "line_number", "row");
                    if (column == 0) column = ExtractIntProperty(srcNode.Properties, "start_col", "startCol", "column", "col", "char");
                    symbol ??= ExtractStringProperty(srcNode.Properties, "name", "exception_type", "symbol", "id") ?? srcNode.Id;
                    nodeId = srcNode.Id;
                }

                // If a CallLiteral evidence node (e.g. 'lit') is also returned, use its exact call-site coordinates
                var litNode = graphNodes.FirstOrDefault(n => n.PropName != srcNode.PropName && IsLiteralEvidenceNode(n.PropName, n.Kind));
                if (litNode.Properties.Kind == CypherValueKind.Object)
                {
                    file ??= ExtractStringProperty(litNode.Properties, "file_path", "path", "filePath");
                    if (explicitLine == 0)
                    {
                        var litLine = ExtractIntProperty(litNode.Properties, "line", "start_line");
                        if (litLine > 0) line = litLine;
                    }
                    if (explicitColumn == 0)
                    {
                        var litCol = ExtractIntProperty(litNode.Properties, "column", "col", "start_col");
                        if (litCol > 0) column = litCol;
                    }
                }

                // Identify target node (distinct from source node and CallLiteral/File context nodes unless no other target exists)
                var fileNode = graphNodes.FirstOrDefault(n => n.PropName != srcNode.PropName && n.Kind == CodeExplorer.Cypher.Common.NodeKind.File);
                if (file == null && fileNode.Properties.Kind == CypherValueKind.Object)
                {
                    file = ExtractStringProperty(fileNode.Properties, "path", "file_path", "name");
                }

                if (file == null && srcNode.Properties.Kind == CypherValueKind.Object)
                {
                    file = ExtractStringProperty(srcNode.Properties, "symbol", "name");
                }

                var targetCandidates = graphNodes
                    .Where(n => n.PropName != srcNode.PropName
                             && !IsLiteralEvidenceNode(n.PropName, n.Kind)
                             && n.Kind != CodeExplorer.Cypher.Common.NodeKind.File)
                    .ToList();
                var tgtNode = targetCandidates.FirstOrDefault(n => IsTargetNodeName(n.PropName));
                if (tgtNode.Properties.Kind == CypherValueKind.Null && targetCandidates.Count > 0)
                {
                    tgtNode = targetCandidates[0];
                }

                if (tgtNode.Properties.Kind == CypherValueKind.Object)
                {
                    target ??= ExtractStringProperty(tgtNode.Properties, "name", "full_name", "symbol", "path", "id") ?? tgtNode.Id;
                    targetNodeId = tgtNode.Id;
                }
                else if (litNode.Properties.Kind == CypherValueKind.Object && target == null)
                {
                    target = ExtractStringProperty(litNode.Properties, "callee", "value", "name");
                    targetNodeId = litNode.Id;
                }
            }

            // Extract call-site coordinates (including multi-call-site unrolling) from returned relationship edge
            var callSiteLocations = new List<(int Line, int Column)>();
            if (graphEdges.Count > 0 && explicitLine == 0)
            {
                var relEdge = graphEdges.FirstOrDefault(e => IsRelationshipAlias(e.PropName));
                if (relEdge.Properties.Kind == CypherValueKind.Null)
                {
                    relEdge = graphEdges[0];
                }

                if (relEdge.Properties.Kind == CypherValueKind.Object)
                {
                    ExtractEdgeCallSites(relEdge.Properties, callSiteLocations);
                }
            }

            if (callSiteLocations.Count == 0)
            {
                callSiteLocations.Add((line, column));
            }

            foreach (var (siteLine, siteCol) in callSiteLocations)
            {
                var resolvedTarget = target;
                if (!string.IsNullOrWhiteSpace(targetTemplate))
                {
                    resolvedTarget = InterpolateMessage(targetTemplate, row, graphNodes, graphEdges, file, siteLine, siteCol, symbol, target);
                }

                var resolvedMessage = message;
                if (!string.IsNullOrWhiteSpace(messageTemplate))
                {
                    resolvedMessage = InterpolateMessage(messageTemplate, row, graphNodes, graphEdges, file, siteLine, siteCol, symbol, resolvedTarget);
                }
                else if (string.IsNullOrWhiteSpace(resolvedMessage))
                {
                    resolvedMessage = !string.IsNullOrEmpty(resolvedTarget)
                        ? $"Invariant violation: {symbol ?? "element"} references {resolvedTarget}"
                        : $"Invariant violation in {symbol ?? file ?? "element"}";
                }

                var tupleKey = $"{GateFingerprintGenerator.NormalizeFilePath(file)}|{(symbol ?? "").Trim()}|{(resolvedTarget ?? "").Trim()}";
                var occurrence = occurrenceCounts.GetValueOrDefault(tupleKey, 0) + 1;
                occurrenceCounts[tupleKey] = occurrence;

                var fp = GateFingerprintGenerator.Generate(gateId, file, symbol, resolvedTarget, occurrence);

                violations.Add(new GateViolation
                {
                    GateId = gateId,
                    File = file,
                    Line = siteLine,
                    Column = siteCol,
                    Symbol = symbol,
                    Target = resolvedTarget,
                    Message = resolvedMessage,
                    NodeId = nodeId,
                    TargetNodeId = targetNodeId,
                    Fingerprint = fp
                });
            }
        }

        return violations;
    }

    private static void ExtractEdgeCallSites(CypherValue edgeProps, List<(int Line, int Column)> destinations)
    {
        if (edgeProps.TryGetProperty("call_sites", out var csElem))
        {
            if (csElem.Kind == CypherValueKind.Array)
            {
                foreach (var item in csElem.AsArray())
                {
                    if (item.Kind == CypherValueKind.String &&
                        Analysis.EvidenceProvenanceHelper.TryParseSite(item.AsString(), out var l, out var c))
                    {
                        destinations.Add((l, c));
                    }
                }
            }
            else if (csElem.Kind == CypherValueKind.String)
            {
                var raw = csElem.AsString();
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    try
                    {
                        var list = JsonSerializer.Deserialize<List<string>>(raw);
                        if (list != null)
                        {
                            foreach (var site in list)
                            {
                                if (Analysis.EvidenceProvenanceHelper.TryParseSite(site, out var l, out var c))
                                {
                                    destinations.Add((l, c));
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Fallback below
                    }
                }
            }
        }

        if (destinations.Count == 0)
        {
            var eLine = ExtractIntProperty(edgeProps, "line", "start_line", "row");
            var eCol = ExtractIntProperty(edgeProps, "column", "col", "start_col");
            if (eLine > 0)
            {
                destinations.Add((eLine, eCol));
            }
        }
    }

    private static bool IsLiteralEvidenceNode(string propName, CodeExplorer.Cypher.Common.NodeKind kind)
        => kind == CodeExplorer.Cypher.Common.NodeKind.CallLiteral
        || string.Equals(propName, "lit", StringComparison.OrdinalIgnoreCase)
        || string.Equals(propName, "literal", StringComparison.OrdinalIgnoreCase);

    private static bool IsRelationshipAlias(string name)
        => name.Equals("r", StringComparison.OrdinalIgnoreCase)
        || name.Equals("rel", StringComparison.OrdinalIgnoreCase)
        || name.Equals("edge", StringComparison.OrdinalIgnoreCase)
        || name.Equals("call", StringComparison.OrdinalIgnoreCase);

    private static bool IsSourceNodeName(string name)
        => name.Equals("fn", StringComparison.OrdinalIgnoreCase)
        || name.Equals("func", StringComparison.OrdinalIgnoreCase)
        || name.Equals("function", StringComparison.OrdinalIgnoreCase)
        || name.Equals("f", StringComparison.OrdinalIgnoreCase)
        || name.Equals("src", StringComparison.OrdinalIgnoreCase)
        || name.Equals("source", StringComparison.OrdinalIgnoreCase)
        || name.Equals("caller", StringComparison.OrdinalIgnoreCase)
        || name.Equals("cls", StringComparison.OrdinalIgnoreCase)
        || name.Equals("class", StringComparison.OrdinalIgnoreCase)
        || name.Equals("type", StringComparison.OrdinalIgnoreCase)
        || name.Equals("t", StringComparison.OrdinalIgnoreCase)
        || name.Equals("m", StringComparison.OrdinalIgnoreCase)
        || name.Equals("c", StringComparison.OrdinalIgnoreCase)
        || name.Equals("d", StringComparison.OrdinalIgnoreCase)
        || name.Equals("e", StringComparison.OrdinalIgnoreCase)
        || name.Equals("e1", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ep", StringComparison.OrdinalIgnoreCase)
        || name.Equals("es", StringComparison.OrdinalIgnoreCase)
        || name.Equals("node", StringComparison.OrdinalIgnoreCase)
        || name.Equals("n", StringComparison.OrdinalIgnoreCase)
        || name.Equals("from", StringComparison.OrdinalIgnoreCase)
        || name.Equals("p", StringComparison.OrdinalIgnoreCase)
        || name.Equals("p1", StringComparison.OrdinalIgnoreCase)
        || name.Equals("project", StringComparison.OrdinalIgnoreCase)
        || name.Equals("driver", StringComparison.OrdinalIgnoreCase)
        || name.Equals("parser", StringComparison.OrdinalIgnoreCase)
        || name.Equals("visitor", StringComparison.OrdinalIgnoreCase)
        || name.Equals("layer", StringComparison.OrdinalIgnoreCase);

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
        || name.Equals("d", StringComparison.OrdinalIgnoreCase)
        || name.Equals("contract", StringComparison.OrdinalIgnoreCase)
        || name.Equals("tbl", StringComparison.OrdinalIgnoreCase)
        || name.Equals("table", StringComparison.OrdinalIgnoreCase)
        || name.Equals("io", StringComparison.OrdinalIgnoreCase)
        || name.Equals("concreteDb", StringComparison.OrdinalIgnoreCase)
        || name.Equals("domain", StringComparison.OrdinalIgnoreCase)
        || name.Equals("lib", StringComparison.OrdinalIgnoreCase)
        || name.Equals("q", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ep", StringComparison.OrdinalIgnoreCase)
        || name.Equals("e2", StringComparison.OrdinalIgnoreCase)
        || name.Equals("p2", StringComparison.OrdinalIgnoreCase)
        || name.Equals("parse", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetGraphRelationship(
        CypherValue element,
        out string? fromId,
        out string? toId,
        out string? relType,
        out CypherValue properties)
    {
        if (element.Kind == CypherValueKind.Object &&
            element.TryGetProperty("properties", out properties) &&
            properties.Kind == CypherValueKind.Object &&
            (element.TryGetProperty("from", out _) || element.TryGetProperty("to", out _) || element.TryGetProperty("source", out _)) &&
            !element.TryGetProperty("id", out _))
        {
            fromId = ExtractStringProperty(element, "from", "source", "from_id");
            toId = ExtractStringProperty(element, "to", "target", "to_id");
            relType = ExtractStringProperty(element, "type", "kind");
            return true;
        }

        fromId = null;
        toId = null;
        relType = null;
        properties = CypherValue.Null;
        return false;
    }

    private static bool TryGetGraphNode(CypherValue element, out string? id, out CodeExplorer.Cypher.Common.NodeKind kind, out CypherValue properties)
    {
        if (element.Kind == CypherValueKind.Object &&
            element.TryGetProperty("properties", out properties) &&
            properties.Kind == CypherValueKind.Object &&
            !element.TryGetProperty("from", out _) &&
            !element.TryGetProperty("to", out _))
        {
            id = element.TryGetProperty("id", out var idElem) ? idElem.AsNullableString() : null;
            var rawKind = element.TryGetProperty("kind", out var kindElem) ? kindElem.AsNullableString() : null;
            kind = CodeExplorer.Cypher.Common.EnumExtensions.ParseNodeKind(rawKind);
            return true;
        }

        id = null;
        kind = CodeExplorer.Cypher.Common.NodeKind.Unspecified;
        properties = CypherValue.Null;
        return false;
    }

    private static readonly Regex TokenRegex = new(@"\{([a-zA-Z0-9_\.]+)(?:\|([^}]*))?\}", RegexOptions.Compiled);

    public static string InterpolateMessage(
        string template,
        CypherRow row,
        List<(string PropName, string? Id, CodeExplorer.Cypher.Common.NodeKind Kind, CypherValue Properties)> graphNodes,
        string? file,
        int line,
        int column,
        string? symbol,
        string? target)
        => InterpolateMessage(template, row, graphNodes, [], file, line, column, symbol, target);

    public static string InterpolateMessage(
        string template,
        CypherRow row,
        List<(string PropName, string? Id, CodeExplorer.Cypher.Common.NodeKind Kind, CypherValue Properties)> graphNodes,
        List<(string PropName, string? FromId, string? ToId, string? RelType, CypherValue Properties)> graphEdges,
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
            var hasFallback = match.Groups[2].Success;
            var fallback = hasFallback ? match.Groups[2].Value : null;

            string? resolved = ResolveTokenValue(token, row, graphNodes, graphEdges, file, line, column, symbol, target);
            if (!string.IsNullOrEmpty(resolved))
            {
                return resolved;
            }

            if (hasFallback && fallback != null)
            {
                return fallback;
            }

            return match.Value;
        });
    }

    private static string? ResolveTokenValue(
        string token,
        CypherRow row,
        List<(string PropName, string? Id, CodeExplorer.Cypher.Common.NodeKind Kind, CypherValue Properties)> graphNodes,
        List<(string PropName, string? FromId, string? ToId, string? RelType, CypherValue Properties)> graphEdges,
        string? file,
        int line,
        int column,
        string? symbol,
        string? target)
    {
        // Direct standard aliases
        if (string.Equals(token, "file", StringComparison.OrdinalIgnoreCase)) return file ?? "";
        if (string.Equals(token, "line", StringComparison.OrdinalIgnoreCase)) return line > 0 ? line.ToString() : "";
        if (string.Equals(token, "column", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "col", StringComparison.OrdinalIgnoreCase)) return column > 0 ? column.ToString() : "";
        if (string.Equals(token, "symbol", StringComparison.OrdinalIgnoreCase)) return symbol ?? "";
        if (string.Equals(token, "target", StringComparison.OrdinalIgnoreCase)) return target ?? "";
        if (string.Equals(token, "location", StringComparison.OrdinalIgnoreCase))
        {
            if (line > 0 && column > 0) return $"{file}:{line}:{column}";
            return line > 0 ? $"{file}:{line}" : (file ?? "");
        }

        // Property access on node or relationship: e.g. {fn.name}, {target.name}, {r.line}, {d.id}
        if (token.Contains('.'))
        {
            var dotIdx = token.IndexOf('.');
            var entityKey = token[..dotIdx];
            var propKey = token[(dotIdx + 1)..];

            var matchedNode = graphNodes.FirstOrDefault(n => string.Equals(n.PropName, entityKey, StringComparison.OrdinalIgnoreCase));
            if (matchedNode.Properties.Kind == CypherValueKind.Object)
            {
                if (string.Equals(propKey, "id", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(matchedNode.Id))
                {
                    return matchedNode.Id;
                }
                if (string.Equals(propKey, "label", StringComparison.OrdinalIgnoreCase) && matchedNode.Kind != CodeExplorer.Cypher.Common.NodeKind.Unspecified)
                {
                    return CodeExplorer.Cypher.Common.EnumExtensions.ToCypherLabel(matchedNode.Kind);
                }
                if (matchedNode.Properties.TryGetProperty(propKey, out var pVal) && !pVal.IsNull)
                {
                    return FormatCypherValue(pVal);
                }
                if (string.Equals(propKey, "kind", StringComparison.OrdinalIgnoreCase) && matchedNode.Kind != CodeExplorer.Cypher.Common.NodeKind.Unspecified)
                {
                    return CodeExplorer.Cypher.Common.EnumExtensions.ToCypherLabel(matchedNode.Kind);
                }

                // Automatic path <-> file_path and name <-> full_name fallback
                if (string.Equals(propKey, "path", StringComparison.OrdinalIgnoreCase) &&
                    matchedNode.Properties.TryGetProperty("file_path", out var fpVal) && !fpVal.IsNull)
                {
                    return FormatCypherValue(fpVal);
                }
                if (string.Equals(propKey, "file_path", StringComparison.OrdinalIgnoreCase) &&
                    matchedNode.Properties.TryGetProperty("path", out var pathVal) && !pathVal.IsNull)
                {
                    return FormatCypherValue(pathVal);
                }
                if (string.Equals(propKey, "full_name", StringComparison.OrdinalIgnoreCase) &&
                    matchedNode.Properties.TryGetProperty("name", out var nameVal) && !nameVal.IsNull)
                {
                    return FormatCypherValue(nameVal);
                }
                if (string.Equals(propKey, "text", StringComparison.OrdinalIgnoreCase) &&
                    matchedNode.Properties.TryGetProperty("value", out var valProp) && !valProp.IsNull)
                {
                    return FormatCypherValue(valProp);
                }
                if (string.Equals(propKey, "value", StringComparison.OrdinalIgnoreCase) &&
                    matchedNode.Properties.TryGetProperty("text", out var txtProp) && !txtProp.IsNull)
                {
                    return FormatCypherValue(txtProp);
                }
            }

            var matchedEdge = graphEdges.FirstOrDefault(e => string.Equals(e.PropName, entityKey, StringComparison.OrdinalIgnoreCase));
            if (matchedEdge.Properties.Kind == CypherValueKind.Object)
            {
                if (string.Equals(propKey, "type", StringComparison.OrdinalIgnoreCase) || string.Equals(propKey, "kind", StringComparison.OrdinalIgnoreCase))
                {
                    return matchedEdge.RelType;
                }
                if (matchedEdge.Properties.TryGetProperty(propKey, out var eVal) && !eVal.IsNull)
                {
                    return FormatCypherValue(eVal);
                }
            }
        }

        // Flat column lookup in row: e.g. {illegal_dependency}, {loc}, {method_count}
        if (row.TryGetProperty(token, out var colVal) && !colVal.IsNull)
        {
            return FormatCypherValue(colVal);
        }

        // Check if token matches a node property directly on the source node (e.g. {name})
        if (graphNodes.Count > 0 && graphNodes[0].Properties.TryGetProperty(token, out var defaultPropVal) && !defaultPropVal.IsNull)
        {
            return FormatCypherValue(defaultPropVal);
        }

        return null;
    }

    private static string FormatCypherValue(CypherValue val)
    {
        if (val.Kind == CypherValueKind.String) return val.AsString();
        if (val.Kind == CypherValueKind.Array)
        {
            var items = new List<string>();
            foreach (var item in val.AsArray())
            {
                items.Add(FormatCypherValue(item));
            }
            return string.Join(", ", items);
        }
        return val.ToString();
    }

    private static string? ExtractStringProperty(CypherRow row, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (row.TryGetProperty(prop, out var val) && !val.IsNull && val.Kind is CypherValueKind.String or CypherValueKind.Int64 or CypherValueKind.Float64 or CypherValueKind.Boolean)
            {
                return val.AsString();
            }
        }
        return null;
    }

    private static string? ExtractStringProperty(CypherValue element, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (element.TryGetProperty(prop, out var val) && !val.IsNull && val.Kind is CypherValueKind.String or CypherValueKind.Int64 or CypherValueKind.Float64 or CypherValueKind.Boolean)
            {
                return val.AsString();
            }
        }
        return null;
    }

    private static int ExtractIntProperty(CypherRow row, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            var i = row.GetNullableIntProp(prop);
            if (i.HasValue) return i.Value;
        }
        return 0;
    }

    private static int ExtractIntProperty(CypherValue element, params string[] propertyNames)
    {
        foreach (var prop in propertyNames)
        {
            if (element.TryGetProperty(prop, out var val))
            {
                var i = val.AsNullableInt32();
                if (i.HasValue) return i.Value;
            }
        }
        return 0;
    }
}

