using System.Text.Json;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Protocol;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Server;

public partial class WebSocketServerHandler
{
    public bool TriggerScan(string? targetPath = null, bool clear = false)
    {
        if (Interlocked.CompareExchange(ref _isScanning, 1, 0) != 0)
        {
            return false;
        }

        var target = string.IsNullOrWhiteSpace(targetPath) ? _workspaceRoot : targetPath;

        _ = Task.Run(async () =>
        {
            try
            {
                var startEv = new ScanProgressEventDto
                {
                    Phase = "Starting",
                    Percentage = 5,
                    CurrentFile = target
                };
                _lastProgress = startEv;
                await BroadcastAsync(WsMessageTypes.ScanProgressEvent, startEv, CancellationToken.None);

                var progress = new Progress<IndexingProgress>(p =>
                {
                    var percent = p.Percentage > 0
                        ? Math.Clamp(p.Percentage, 1, 99)
                        : (p.NodesCount > 0
                        ? (int)Math.Min(95, Math.Max(10, (double)p.NodesPersisted / p.NodesCount * 100))
                        : 50);
                    var currentMsg = p.Message ?? (p.TotalFiles > 0
                        ? $"Parsing syntax: {p.ProcessedFiles}/{p.TotalFiles} files ({percent}%)"
                        : $"Saved {p.NodesPersisted} nodes, {p.RelationshipsPersisted} relationships...");
                    var progEv = new ScanProgressEventDto
                    {
                        Phase = p.Phase,
                        Percentage = percent,
                        CurrentFile = currentMsg,
                        TotalFiles = p.TotalFiles > 0 ? p.TotalFiles : p.NodesCount,
                        ProcessedFiles = p.ProcessedFiles > 0 ? p.ProcessedFiles : p.NodesPersisted
                    };
                    _lastProgress = progEv;
                    _ = BroadcastAsync(WsMessageTypes.ScanProgressEvent, progEv, CancellationToken.None);
                });

                var shouldClear = clear || (_graphClient is SqliteGraphClient sqlite && sqlite.IsSchemaOutdated);
                var (nodesCount, relsCount, _) = await _indexer.IndexAsync(target, _workspaceRoot, shouldClear, CancellationToken.None, progress);

                var compEv = new ScanProgressEventDto
                {
                    Phase = "Completed",
                    Percentage = 100,
                    TotalFiles = (int)nodesCount,
                    ProcessedFiles = (int)nodesCount,
                    CurrentFile = $"Indexed {nodesCount} nodes, {relsCount} relationships."
                };
                _lastProgress = null;
                InvalidateGatesCache();
                await BroadcastAsync(WsMessageTypes.ScanProgressEvent, compEv, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Server     ] Background scan failed");
                var failEv = new ScanProgressEventDto
                {
                    Phase = "Failed",
                    Percentage = 0,
                    CurrentFile = ex.Message
                };
                _lastProgress = null;
                await BroadcastAsync(WsMessageTypes.ScanProgressEvent, failEv, CancellationToken.None);
            }
            finally
            {
                Interlocked.Exchange(ref _isScanning, 0);
            }
        });

        return true;
    }

    private async Task HandleHandshakeAsync(ClientSession session, string reqId, JsonElement payload, CancellationToken cancellationToken)
    {
        long nodesCount = 0;
        long edgesCount = 0;
        try
        {
            var nodeCountJson = await _graphClient.ExecuteQueryAsync("MATCH (n) RETURN count(n) AS cnt", null, cancellationToken);

            using var nodeDoc = JsonDocument.Parse(nodeCountJson);

            if (nodeDoc.RootElement.GetArrayLength() > 0)
            {
                nodesCount = nodeDoc.RootElement[0].GetProperty("cnt").GetInt64();
            }

            var edgeCountJson = await _graphClient.ExecuteQueryAsync("MATCH ()-[r]->() RETURN count(r) AS cnt",
                null, cancellationToken);

            using var edgeDoc = JsonDocument.Parse(edgeCountJson);

            if (edgeDoc.RootElement.GetArrayLength() > 0)
            {
                edgesCount = edgeDoc.RootElement[0].GetProperty("cnt").GetInt64();
            }
        }
        catch
        {
            // Ignore if empty/new database
        }

        var sqliteClient = _graphClient as SqliteGraphClient;
        var isScanning = Volatile.Read(ref _isScanning) == 1;

        var response = new HandshakeResponseDto
        {
            ServerVersion = _serverVersion,
            WorkspaceRoot = _workspaceRoot,
            DbPath = sqliteClient?.DbPath ?? "",
            TotalNodes = nodesCount,
            TotalEdges = edgesCount,
            Capabilities = ["architecture", "dependencies", "call_chain", "impact", "cypher", "scan", "graph_patch", "gates"],
            IsSchemaOutdated = sqliteClient?.IsSchemaOutdated ?? false,
            SchemaVersion = sqliteClient?.SchemaVersion ?? 0,
            CurrentSchemaVersion = SqliteGraphClient.CurrentSchemaVersion,
            IsScanning = isScanning,
            ScanProgress = isScanning ? _lastProgress : null
        };

        await SendResponseAsync(session, WsMessageTypes.HandshakeResponse, reqId, response, cancellationToken);
    }

    public async Task<GateSuiteSummaryDto> GetGateDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var repo = new Core.Gates.Storage.FileGateRepository(_workspaceRoot);
        var allGates = await repo.GetAllGatesAsync(cancellationToken);

        return new GateSuiteSummaryDto
        {
            TotalGates = allGates.Count,
            PassedCount = 0,
            WarningCount = 0,
            FailedCount = 0,
            NewViolationsCount = 0,
            ToleratedViolationsCount = 0,
            RatchetOpportunities = 0,
            IsSuccess = true,
            TotalDurationMs = 0,
            Gates = allGates.Select(g => new GateItemDto
            {
                Id = g.Id,
                Name = g.Name,
                Category = g.Category.ToString(),
                Platform = g.Platform,
                Scope = g.Scope.ToString(),
                Dimension = g.Dimension.ToString(),
                Severity = g.Severity.ToString(),
                Description = g.Description,
                Remediation = g.Remediation,
                Message = g.Message,
                Target = g.Target,
                Thresholds = g.Thresholds != null
                    ? new GateThresholdSettingsDto { Warning = g.Thresholds.Warning, Error = g.Thresholds.Error }
                    : null,
                Query = g.Query,
                Source = g.Source,
                RatchetEnabled = g.Ratchet.Enabled,
                BaselinePath = g.Ratchet.Baseline,
                Status = "Pending",
                TotalViolations = 0,
                NewViolationsCount = 0,
                ToleratedViolationsCount = 0,
                BaselineCount = 0,
                CanRatchetDown = false,
                DurationMs = 0,
                Violations = []
            }).ToList()
        };
    }

    public bool HasCachedGateSuite => _cachedGateSuite != null;

    public void InvalidateGatesCache()
    {
        _cachedGateSuite = null;
    }

    public async Task<GateSuiteSummaryDto> EvaluateGatesSuiteAsync(string? gateId = null, CancellationToken cancellationToken = default, bool force = false)
    {
        await _gateEvaluationLock.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(gateId))
            {
                if (!force && _cachedGateSuite != null)
                {
                    return _cachedGateSuite;
                }

                var repo = new Core.Gates.Storage.FileGateRepository(_workspaceRoot);
                var engine = new Core.Gates.Engine.GateExecutionEngine(_graphClient, repo);
                var suite = await engine.CheckSuiteAsync(null, cancellationToken: cancellationToken);
                var dto = MapGateSuiteToDto(suite);
                _cachedGateSuite = dto;
                return dto;
            }
            else
            {
                var repo = new Core.Gates.Storage.FileGateRepository(_workspaceRoot);
                var engine = new Core.Gates.Engine.GateExecutionEngine(_graphClient, repo);
                var single = await repo.GetGateByIdAsync(gateId.Trim(), cancellationToken);
                IEnumerable<Core.Gates.Models.SemanticGate>? targetGates = single != null ? [single] : [];
                var singleSuite = await engine.CheckSuiteAsync(targetGates, cancellationToken: cancellationToken);
                var singleDto = MapGateSuiteToDto(singleSuite);

                if (_cachedGateSuite == null)
                {
                    var allConfigured = await repo.GetAllGatesAsync(cancellationToken);
                    if (allConfigured.Count == 0)
                    {
                        allConfigured = CodeExplorer.Core.Gates.Storage.BuiltInGateCatalog.GetDefaultGates().Concat(CodeExplorer.Core.Gates.Storage.BuiltInGateCatalog.GetExampleGates()).ToList();
                    }
                    _cachedGateSuite = new GateSuiteSummaryDto
                    {
                        TotalGates = allConfigured.Count,
                        Gates = allConfigured.Select(g => new GateItemDto
                        {
                            Id = g.Id,
                            Name = g.Name,
                            Category = g.Category.ToString(),
                            Platform = g.Platform.ToString(),
                            Scope = g.Scope.ToString(),
                            Severity = g.Severity.ToString(),
                            Status = "Pending",
                            Source = g.Source
                        }).ToList()
                    };
                }

                if (_cachedGateSuite != null && singleDto.Gates.Count > 0)
                {
                    var updatedGate = singleDto.Gates[0];
                    var nextGates = _cachedGateSuite.Gates.ToList();
                    var idx = nextGates.FindIndex(g => string.Equals(g.Id, gateId.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0)
                    {
                        nextGates[idx] = updatedGate;
                    }
                    else
                    {
                        nextGates.Add(updatedGate);
                    }

                    int passed = 0;
                    int warning = 0;
                    int failed = 0;
                    int newViolations = 0;
                    int toleratedViolations = 0;
                    int ratchetOpportunities = 0;

                    foreach (var g in nextGates)
                    {
                        if (string.Equals(g.Status, "Passed", StringComparison.OrdinalIgnoreCase))
                        {
                            passed++;
                        }
                        else if (string.Equals(g.Status, "PassedTolerated", StringComparison.OrdinalIgnoreCase))
                        {
                            passed++;
                            toleratedViolations += g.ToleratedViolationsCount;
                        }
                        else if (string.Equals(g.Status, "Warning", StringComparison.OrdinalIgnoreCase))
                        {
                            warning++;
                            newViolations += g.NewViolationsCount;
                        }
                        else if (string.Equals(g.Status, "FailedNew", StringComparison.OrdinalIgnoreCase) || string.Equals(g.Status, "Error", StringComparison.OrdinalIgnoreCase))
                        {
                            failed++;
                            newViolations += g.NewViolationsCount;
                        }

                        if (g.CanRatchetDown) ratchetOpportunities++;
                    }

                    _cachedGateSuite = new GateSuiteSummaryDto
                    {
                        TotalGates = nextGates.Count,
                        PassedCount = passed,
                        WarningCount = warning,
                        FailedCount = failed,
                        NewViolationsCount = newViolations,
                        ToleratedViolationsCount = toleratedViolations,
                        RatchetOpportunities = ratchetOpportunities,
                        IsSuccess = failed == 0,
                        TotalDurationMs = _cachedGateSuite.TotalDurationMs,
                        Gates = nextGates
                    };
                    return _cachedGateSuite;
                }

                return singleDto;
            }
        }
        finally
        {
            _gateEvaluationLock.Release();
        }
    }

    public async Task<RatchetGatesResponseDto> RatchetGatesSuiteAsync(string? gateId = null, bool updateAll = false, CancellationToken cancellationToken = default)
    {
        var repo = new Core.Gates.Storage.FileGateRepository(_workspaceRoot);
        var engine = new Core.Gates.Engine.GateExecutionEngine(_graphClient, repo);

        IEnumerable<Core.Gates.Models.SemanticGate>? targetGates = null;
        if (!string.IsNullOrWhiteSpace(gateId))
        {
            var single = await repo.GetGateByIdAsync(gateId.Trim(), cancellationToken);
            if (single == null)
            {
                return new RatchetGatesResponseDto
                {
                    Success = false,
                    UpdatedGatesCount = 0,
                    Message = $"Gate '{gateId}' not found."
                };
            }
            targetGates = [single];
        }

        var suite = await engine.CheckSuiteAsync(targetGates, cancellationToken: cancellationToken);
        int updated = 0;

        foreach (var res in suite.Results)
        {
            if (res.Status == Core.Gates.Models.GateCheckStatus.Error || !res.Gate.Ratchet.Enabled)
            {
                continue;
            }

            var shouldUpdate = !string.IsNullOrWhiteSpace(gateId) || updateAll || res.CanRatchetDown || res.NewViolations.Count > 0;
            if (!shouldUpdate)
            {
                continue;
            }

            var existingBaseline = await repo.LoadBaselineAsync(res.Gate, cancellationToken);
            var newBaseline = Core.Gates.Engine.RatchetService.CreateUpdatedBaseline(res.Gate, res.Violations, existingBaseline);
            await repo.SaveBaselineAsync(newBaseline, res.Gate, cancellationToken);
            updated++;
        }

        var refreshedSuite = await engine.CheckSuiteAsync(cancellationToken: cancellationToken);
        var refreshedDto = MapGateSuiteToDto(refreshedSuite);
        _cachedGateSuite = refreshedDto;
        return new RatchetGatesResponseDto
        {
            Success = true,
            UpdatedGatesCount = updated,
            Message = updated > 0
                ? $"Updated baseline for {updated} gate(s)."
                : "Baselines are already up to date.",
            Suite = refreshedDto
        };
    }

    public async Task<RatchetGatesResponseDto> InitStarterGatesAsync(bool overwrite = false, CancellationToken cancellationToken = default)
    {
        var repo = new Core.Gates.Storage.FileGateRepository(_workspaceRoot);
        var created = await repo.InitStarterGatesAsync(overwrite, cancellationToken);
        var refreshedSuite = await EvaluateGatesSuiteAsync(null, cancellationToken, force: true);
        return new RatchetGatesResponseDto
        {
            Success = true,
            UpdatedGatesCount = created,
            Message = created > 0
                ? "Initialized .codeexplorer/gates/ with starter template (example-gate.yaml.example)."
                : ".codeexplorer/gates/ starter template already exists.",
            Suite = refreshedSuite
        };
    }

    public async Task<SaveGateResponseDto> SaveGateAsync(GateItemDto gateDto, string? targetPath = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gateDto);
        if (string.IsNullOrWhiteSpace(gateDto.Id))
        {
            return new SaveGateResponseDto
            {
                Success = false,
                Message = "Gate ID must not be empty."
            };
        }

        if (string.IsNullOrWhiteSpace(gateDto.Query))
        {
            return new SaveGateResponseDto
            {
                Success = false,
                Message = "Gate query must not be empty."
            };
        }

        var category = Enum.TryParse<Core.Gates.Models.GateCategory>(gateDto.Category, true, out var cat) ? cat : Core.Gates.Models.GateCategory.Architectural;
        var platform = !string.IsNullOrWhiteSpace(gateDto.Platform) ? gateDto.Platform.Trim().ToLowerInvariant() : "agnostic";
        var scope = Enum.TryParse<Core.Gates.Models.InvariantScope>(gateDto.Scope, true, out var sc) ? sc : Core.Gates.Models.InvariantScope.Module;
        var dim = Enum.TryParse<Core.Gates.Models.InvariantDimension>(gateDto.Dimension, true, out var dm) ? dm : Core.Gates.Models.InvariantDimension.Boundaries;
        var sev = Enum.TryParse<Core.Gates.Models.GateSeverity>(gateDto.Severity, true, out var sv) ? sv : Core.Gates.Models.GateSeverity.Error;

        var thresholds = gateDto.Thresholds != null && (gateDto.Thresholds.Warning.HasValue || gateDto.Thresholds.Error.HasValue)
            ? new Core.Gates.Models.GateThresholdSettings
            {
                Warning = gateDto.Thresholds.Warning,
                Error = gateDto.Thresholds.Error
            }
            : null;

        var gate = new Core.Gates.Models.SemanticGate
        {
            Id = gateDto.Id.Trim(),
            Name = !string.IsNullOrWhiteSpace(gateDto.Name) ? gateDto.Name.Trim() : gateDto.Id.Trim(),
            Category = category,
            Platform = platform,
            Scope = scope,
            Dimension = dim,
            Severity = sev,
            Description = gateDto.Description?.Trim(),
            Remediation = gateDto.Remediation?.Trim(),
            Message = string.IsNullOrWhiteSpace(gateDto.Message) ? null : gateDto.Message.Trim(),
            Target = string.IsNullOrWhiteSpace(gateDto.Target) ? null : gateDto.Target.Trim(),
            Thresholds = thresholds,
            Query = gateDto.Query.Trim(),
            Ratchet = new Core.Gates.Models.GateRatchetSettings
            {
                Enabled = gateDto.RatchetEnabled,
                Baseline = gateDto.BaselinePath
            },
            Source = gateDto.Source ?? string.Empty
        };

        var repo = new Core.Gates.Storage.FileGateRepository(_workspaceRoot);
        var savedPath = await repo.SaveGateAsync(gate, targetPath, cancellationToken);
        InvalidateGatesCache();

        var refreshedSuite = await EvaluateGatesSuiteAsync(null, cancellationToken, force: true);
        var savedGateItem = refreshedSuite.Gates.FirstOrDefault(g => string.Equals(g.Id, gate.Id, StringComparison.OrdinalIgnoreCase)) ?? gateDto;
        savedGateItem.Source = savedPath;

        return new SaveGateResponseDto
        {
            Success = true,
            Message = $"Gate '{gate.Id}' saved successfully to '{savedPath}'.",
            FilePath = savedPath,
            Gate = savedGateItem,
            Suite = refreshedSuite
        };
    }

    public async Task<TestGateResponseDto> TestGateQueryAsync(
        string query,
        string? gateId = null,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default,
        string? messageTemplate = null,
        string? targetTemplate = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new TestGateResponseDto
            {
                Success = false,
                Error = "Query must not be empty."
            };
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var paramDict = new Dictionary<string, object?>();
            if (parameters != null)
            {
                foreach (var (k, v) in parameters)
                {
                    paramDict[k] = v;
                }
            }

            var rawJson = await _graphClient.ExecuteQueryAsync(query, paramDict, cancellationToken);
            sw.Stop();

            var effectiveGateId = !string.IsNullOrWhiteSpace(gateId) ? gateId.Trim() : "TEST_GATE";
            var violations = Core.Gates.Engine.GateExecutionEngine.ParseViolations(effectiveGateId, rawJson, messageTemplate, targetTemplate);

            var projectedCols = new List<string>();
            var hasGraphNode = false;
            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(rawJson);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        var first = doc.RootElement.EnumerateArray().FirstOrDefault();
                        if (first.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in first.EnumerateObject())
                            {
                                projectedCols.Add(prop.Name);
                                if (prop.Value.ValueKind == JsonValueKind.Object &&
                                    prop.Value.TryGetProperty("properties", out _) &&
                                    prop.Value.TryGetProperty("id", out _))
                                {
                                    hasGraphNode = true;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // ignore JSON inspection errors
                }
            }

            var missingCols = new List<string>();
            if (projectedCols.Count > 0 && !hasGraphNode)
            {
                var requiredContract = new List<string> { "file", "line", "symbol" };
                if (string.IsNullOrWhiteSpace(targetTemplate)) requiredContract.Add("target");
                if (string.IsNullOrWhiteSpace(messageTemplate)) requiredContract.Add("message");

                missingCols = requiredContract
                    .Where(req => !projectedCols.Any(c => string.Equals(c, req, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            var vDtos = violations.Take(50).Select(v => new GateViolationDto
            {
                GateId = v.GateId,
                File = v.File,
                Line = v.Line,
                Column = v.Column,
                Symbol = v.Symbol,
                Target = v.Target,
                Message = v.Message,
                NodeId = v.NodeId,
                TargetNodeId = v.TargetNodeId,
                Fingerprint = v.Fingerprint,
                IsTolerated = false
            }).ToList();

            return new TestGateResponseDto
            {
                Success = true,
                DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                ViolationsCount = violations.Count,
                Violations = vDtos,
                ProjectedColumns = projectedCols,
                MissingColumns = missingCols
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TestGateResponseDto
            {
                Success = false,
                DurationMs = Math.Round(sw.Elapsed.TotalMilliseconds, 2),
                Error = ex.Message
            };
        }
    }

    private static GateSuiteSummaryDto MapGateSuiteToDto(Core.Gates.Models.GateSuiteResult suite)
    {
        var items = new List<GateItemDto>(suite.Results.Count);
        foreach (var r in suite.Results)
        {
            var toleratedFps = r.ToleratedViolations
                .Select(v => v.Fingerprint)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var vDtos = r.Violations.Select(v => new GateViolationDto
            {
                GateId = v.GateId,
                File = v.File,
                Line = v.Line,
                Column = v.Column,
                Symbol = v.Symbol,
                Target = v.Target,
                Message = v.Message,
                NodeId = v.NodeId,
                TargetNodeId = v.TargetNodeId,
                Fingerprint = v.Fingerprint,
                IsTolerated = toleratedFps.Contains(v.Fingerprint)
            }).ToList();

            items.Add(new GateItemDto
            {
                Id = r.Gate.Id,
                Name = r.Gate.Name,
                Category = r.Gate.Category.ToString(),
                Platform = r.Gate.Platform,
                Scope = r.Gate.Scope.ToString(),
                Dimension = r.Gate.Dimension.ToString(),
                Severity = r.Gate.Severity.ToString(),
                Description = r.Gate.Description,
                Remediation = r.Gate.Remediation,
                Message = r.Gate.Message,
                Target = r.Gate.Target,
                Thresholds = r.Gate.Thresholds != null
                    ? new GateThresholdSettingsDto { Warning = r.Gate.Thresholds.Warning, Error = r.Gate.Thresholds.Error }
                    : null,
                Query = r.Gate.Query,
                Source = r.Gate.Source,
                RatchetEnabled = r.Gate.Ratchet.Enabled,
                BaselinePath = r.Gate.Ratchet.Baseline,
                Status = r.Status.ToString(),
                TotalViolations = r.Violations.Count,
                NewViolationsCount = r.NewViolations.Count,
                ToleratedViolationsCount = r.ToleratedViolations.Count,
                BaselineCount = r.BaselineCount,
                CanRatchetDown = r.CanRatchetDown,
                DurationMs = Math.Round(r.Duration.TotalMilliseconds, 2),
                ErrorMessage = r.ErrorMessage,
                Violations = vDtos
            });
        }

        return new GateSuiteSummaryDto
        {
            TotalGates = suite.TotalGates,
            PassedCount = suite.PassedCount,
            WarningCount = suite.WarningCount,
            FailedCount = suite.FailedCount,
            NewViolationsCount = suite.NewViolationsCount,
            ToleratedViolationsCount = suite.ToleratedViolationsCount,
            RatchetOpportunities = suite.RatchetOpportunities,
            IsSuccess = suite.IsSuccess,
            TotalDurationMs = Math.Round(suite.TotalDuration.TotalMilliseconds, 2),
            Gates = items
        };
    }
}
