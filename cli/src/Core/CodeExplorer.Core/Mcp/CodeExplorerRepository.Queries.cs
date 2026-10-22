using System.Text.Json;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Core.Gates.Storage;
using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Core.Parser;
using CodeExplorer.Cypher.Parser;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    public async Task<string> ExecuteCustomReadCypherAsync(string query, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        CypherSecurityValidator.ValidateReadOnly(query);
        return await ExecuteAndFormatQueryAsync(query, new Dictionary<string, object?>(), workspacePath, cancellationToken);
    }

    public async Task<string> GetWorkspaceContentAsync(string? workspacePath = null, string? type = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        var parameters = new Dictionary<string, object?>
        {
            ["type"] = string.IsNullOrEmpty(type) ? null : type
        };
        var query = Queries.Get("get_workspace_content");
        return await client.ExecuteQueryAsync(query, parameters, cancellationToken);
    }

    public async Task<string> ExecuteRawQueryAsync(string query, Dictionary<string, object?>? parameters = null, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        return await client.ExecuteQueryAsync(query, parameters, cancellationToken);
    }


    public async Task<string> ListProjectQueriesAsync(string? workspacePath, CancellationToken cancellationToken = default)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
        var queries = _queryManager.ListQueries(effectiveWsPath);
        return JsonSerializer.Serialize(new
        {
            workspace_path = effectiveWsPath,
            queries_count = queries.Count,
            queries = queries.Select(q => new
            {
                name = q.Name,
                description = q.Description,
                parameters = q.Parameters,
                returns = q.Returns,
                tags = q.Tags
            })
        }, CompactJsonOptions);
    }

    public async Task<string> SaveProjectQueryAsync(
        string name,
        string description,
        string cypher,
        string? parametersJson,
        string? returns,
        string? tags,
        string? workspacePath,
        CancellationToken cancellationToken = default)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);

        var paramList = new List<ProjectQueryParameter>();
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<ProjectQueryParameter>>(parametersJson);
                if (parsed != null) paramList = parsed;
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse parametersJson: {ex.Message}", nameof(parametersJson), ex);
            }
        }

        var tagList = string.IsNullOrWhiteSpace(tags)
            ? []
            : tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var metadata = new ProjectQueryMetadata
        {
            Name = name,
            Description = description,
            Parameters = paramList,
            Returns = returns,
            Tags = tagList
        };

        var saved = _queryManager.SaveQuery(effectiveWsPath, name, cypher, metadata);
        return JsonSerializer.Serialize(new
        {
            success = true,
            message = $"Project query '{saved.Name}' successfully validated and saved to .codeexplorer/queries/",
            query = new
            {
                name = saved.Name,
                description = saved.Description,
                parameters = saved.Parameters,
                returns = saved.Returns,
                tags = saved.Tags,
                cypher_file = saved.CypherPath,
                metadata_file = saved.MetadataPath
            }
        }, CompactJsonOptions);
    }

    public async Task<string> ExecuteProjectQueryAsync(string name, string? parametersJson, string? workspacePath, CancellationToken cancellationToken = default)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
        var queryItem = _queryManager.GetQuery(effectiveWsPath, name);
        if (queryItem == null)
        {
            throw new FileNotFoundException(
                $"Project query '{name}' not found in '{_queryManager.GetQueriesDirectory(effectiveWsPath)}'. " +
                "Use 'list_project_queries' to see available queries or 'save_project_query' to register a new one.");
        }

        var paramDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(parametersJson);

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        paramDict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => prop.Value.GetRawText()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Failed to parse parametersJson: {ex.Message}", nameof(parametersJson), ex);
            }
        }

        foreach (var p in queryItem.Parameters)
        {
            if (p.Required && (!paramDict.ContainsKey(p.Name) || paramDict[p.Name] == null))
            {
                throw new ArgumentException($"Required parameter '{p.Name}' is missing for query '{name}'.");
            }
        }

        if (queryItem.Cypher.Contains("$workspaceId") && !paramDict.ContainsKey("workspaceId"))
        {
            paramDict["workspaceId"] = OntologyConstants.IdPrefixes.Workspace;
        }

        if (queryItem.Cypher.Contains("$workspaceIdPrefix") && !paramDict.ContainsKey("workspaceIdPrefix"))
        {
            paramDict["workspaceIdPrefix"] = $"{OntologyConstants.IdPrefixes.Workspace}:";
        }

        return await ExecuteAndFormatQueryAsync(queryItem.Cypher, paramDict, workspacePath, cancellationToken);
    }


    public async Task<string> InitWorkspaceAsync(
        string? name = null,
        string? workspacePath = null,
        bool force = false,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetFullPath(workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var wsName = string.IsNullOrWhiteSpace(name) ? new DirectoryInfo(targetDir).Name : name.Trim();

        var existing = WorkspaceLocator.Find(targetDir);
        if (existing != null && existing.RootDirectory.Equals(targetDir, StringComparison.OrdinalIgnoreCase) && !force)
        {
            return JsonSerializer.Serialize(new
            {
                status = "exists",
                message = $"Workspace already exists at '{existing.RootDirectory}'. Set force=true to reinitialize.",
                workspacePath = existing.RootDirectory,
                dbPath = existing.DbPath
            }, CompactJsonOptions);
        }

        var ws = WorkspaceLocator.Initialize(targetDir, wsName);
        await using (var client = new SqliteGraphClient(ws.DbPath))
        {
            await client.ExecuteWriteAsync(
                "INSERT INTO nodes (id, kind, properties) VALUES ('workspace', 'Workspace', json_object('id', 'workspace', 'name', @name, 'path', @path)) " +
                "ON CONFLICT(id) DO UPDATE SET properties = json_object('id', 'workspace', 'name', @name, 'path', @path);",
                new Dictionary<string, object?> { ["name"] = wsName, ["path"] = ws.RootDirectory }, cancellationToken);
        }

        return JsonSerializer.Serialize(new
            {
                status = "initialized",
                name = wsName,
                workspacePath = ws.RootDirectory,
                dbPath = ws.DbPath,
                queriesPath = ws.QueriesDirectory
            }, CompactJsonOptions);
    }

    public async Task<string> ScanWorkspaceAsync(
        string? path = null,
        bool clear = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetFullPath(path ?? workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null)
        {
            var defaultName = new DirectoryInfo(targetDir).Name;
            ws = WorkspaceLocator.Initialize(targetDir, defaultName);
        }

        var client = await ResolveClientAsync(ws.RootDirectory);
        if (clear)
        {
            await client.ClearWorkspaceAsync(targetDir);
        }

        var indexer = new WorkspaceIndexer(client);
        var (nodesCount, relsCount, nodesByKind) = await indexer.IndexAsync(targetDir, ws.RootDirectory, clear: false, cancellationToken: cancellationToken);

        InvalidateCache();

        return JsonSerializer.Serialize(new
        {
            status = "success",
            workspacePath = ws.RootDirectory,
            scannedPath = targetDir,
            nodesCount,
            relationshipsCount = relsCount,
            nodesByKind
        }, CompactJsonOptions);
    }

    public async Task<string> GetWorkspaceStatusAsync(
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetFullPath(workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null || !File.Exists(ws.DbPath))
        {
            return JsonSerializer.Serialize(new
            {
                status = "not_initialized",
                message = $"No indexed workspace found for '{targetDir}'. Call scan_workspace or init_workspace first."
            }, CompactJsonOptions);
        }

        var fileInfo = new FileInfo(ws.DbPath);
        var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
        var client = await ResolveClientAsync(ws.RootDirectory);

        var projResult = await client.ExecuteQueryAsync("MATCH (p:Project) RETURN p.name AS name, p.language AS language, p.project_type AS type, p.path AS path", null, cancellationToken);

        using var projDoc = JsonDocument.Parse(projResult);

        var countResult = await client.ExecuteQueryAsync(
            "MATCH (n) RETURN labels(n)[0] AS kind, count(n) AS count ORDER BY count DESC", null,
            cancellationToken);

        using var countDoc = JsonDocument.Parse(countResult);

        var customQueriesCount = Directory.Exists(ws.QueriesDirectory)
            ? Directory.GetFiles(ws.QueriesDirectory, "*.cypher").Length
            : 0;

        return JsonSerializer.Serialize(
            new
            {
                status = "ready",
                workspacePath = ws.RootDirectory,
                dbPath = ws.DbPath,
                databaseSizeMb = Math.Round(sizeMb, 2),
                projects = projDoc.RootElement,
                nodeCounts = countDoc.RootElement,
                customQueriesCount
            }, CompactJsonOptions);
    }

    public async Task<string> ClearWorkspaceIndexAsync(
        string? path = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetFullPath(path ?? workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null || !File.Exists(ws.DbPath))
        {
            return JsonSerializer.Serialize(new
            {
                status = "not_found",
                message = "Workspace or database not found."
            }, CompactJsonOptions);
        }

        var client = await ResolveClientAsync(ws.RootDirectory);
        if (string.IsNullOrWhiteSpace(path) || path.TrimEnd('/', '\\').Equals(ws.RootDirectory.TrimEnd('/', '\\'), StringComparison.OrdinalIgnoreCase))
        {
            await client.ClearDatabaseAsync();
        }
        else
        {
            await client.ClearWorkspaceAsync(targetDir);
        }

        InvalidateCache();

        return JsonSerializer.Serialize(new
        {
            status = "cleared",
            targetPath = targetDir
        }, CompactJsonOptions);
    }


    public async Task<string> IngestGraphDataAsync(
        string nodesJson,
        string? relationshipsJson = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageJson;
        }

        using var nodesDoc = JsonDocument.Parse(nodesJson);

        int nodesIngested = 0;
        int relsIngested = 0;

        if (nodesDoc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var node in nodesDoc.RootElement.EnumerateArray())
            {
                if (node.TryGetProperty("id", out var idProp) && node.TryGetProperty("kind", out var kindProp))
                {
                    var id = idProp.GetString()!;
                    var kind = kindProp.GetString()!;
                    var props = node.TryGetProperty("properties", out var p) ? p.GetRawText() : "{}";

                    await client.ExecuteWriteAsync(
                        "INSERT INTO nodes (id, kind, properties) VALUES (@id, @kind, json(@props)) ON CONFLICT(id) DO UPDATE SET kind = @kind, properties = json(@props);",
                        new Dictionary<string, object?> { ["id"] = id, ["kind"] = kind, ["props"] = props },
                        cancellationToken);
                    nodesIngested++;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(relationshipsJson))
        {
            using var relsDoc = JsonDocument.Parse(relationshipsJson);

            if (relsDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var rel in relsDoc.RootElement.EnumerateArray())
                {
                    var kind = rel.TryGetProperty("kind", out var kProp)
                        ? kProp.GetString()
                        : (rel.TryGetProperty("type", out var tProp) ? tProp.GetString() : null);

                    if (!string.IsNullOrEmpty(kind) && rel.TryGetProperty("from_id", out var fProp) &&
                        rel.TryGetProperty("to_id", out var toProp))
                    {
                        var fromId = fProp.GetString()!;
                        var toId = toProp.GetString()!;
                        var props = rel.TryGetProperty("properties", out var p) ? p.GetRawText() : "{}";

                        await client.ExecuteWriteAsync(
                            "INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties) VALUES (@fromId, @toId, @kind, json(@props));",
                            new Dictionary<string, object?>
                            {
                                ["kind"] = kind, ["fromId"] = fromId, ["toId"] = toId, ["props"] = props
                            }, cancellationToken);
                        relsIngested++;
                    }
                }
            }
        }

        InvalidateCache();

        return JsonSerializer.Serialize(
            new { status = "success", nodesIngested, relationshipsIngested = relsIngested }, CompactJsonOptions);
    }

    public async Task<string> CheckSemanticGatesAsync(
        string? gateId = null,
        string? category = null,
        string? platform = null,
        string? scope = null,
        string? dimension = null,
        bool includeTolerated = false,
        int limit = 50,
        string? fileFilter = null,
        string? groupBy = null,
        string format = "markdown",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory();
        var ws = WorkspaceLocator.Find(targetDir);
        var effectivePath = ws?.RootDirectory ?? Path.GetFullPath(targetDir);

        var client = await ResolveClientAsync(effectivePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return GetStandbyMessage(format);
        }

        var gateRepo = new FileGateRepository(effectivePath);
        var engine = new GateExecutionEngine(client, gateRepo);

        InvariantScope? scopeFilter = !string.IsNullOrWhiteSpace(scope) && Enum.TryParse<InvariantScope>(scope, true, out var s) ? s : null;
        InvariantDimension? dimFilter = !string.IsNullOrWhiteSpace(dimension) && Enum.TryParse<InvariantDimension>(dimension, true, out var d) ? d : null;
        GateCategory? categoryFilter = !string.IsNullOrWhiteSpace(category) && Enum.TryParse<GateCategory>(category, true, out var c) ? c : null;
        string? platformFilter = !string.IsNullOrWhiteSpace(platform) ? platform.Trim() : null;

        if (!string.IsNullOrWhiteSpace(gateId))
        {
            var singleGate = await gateRepo.GetGateByIdAsync(gateId, cancellationToken);
            if (singleGate == null)
            {
                return format.Equals("json", StringComparison.OrdinalIgnoreCase)
                    ? JsonSerializer.Serialize(new { error = $"Gate '{gateId}' not found." }, CompactJsonOptions)
                    : $"⚠️ **Gate Not Found**: No gate with ID '{gateId}' is registered.";
            }

            var singleResult = await engine.CheckGateAsync(singleGate, cancellationToken);
            return FormatSingleGateResult(singleResult, includeTolerated, limit, fileFilter, groupBy, format);
        }

        var suiteResult = await engine.CheckSuiteAsync(
            null,
            scopeFilter,
            dimFilter,
            categoryFilter,
            platformFilter,
            cancellationToken);
        return FormatSuiteResult(suiteResult, format);
    }

    public async Task<string> ListSemanticGatesAsync(
        string? category = null,
        string? platform = null,
        string format = "markdown",
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory();
        var ws = WorkspaceLocator.Find(targetDir);
        var effectivePath = ws?.RootDirectory ?? Path.GetFullPath(targetDir);

        var gateRepo = new FileGateRepository(effectivePath);
        var allGates = await gateRepo.GetAllGatesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<GateCategory>(category, true, out var cat))
        {
            allGates = [.. allGates.Where(g => g.Category == cat)];
        }
        if (!string.IsNullOrWhiteSpace(platform))
        {
            allGates =
            [
                .. allGates.Where(g => string.Equals(g.Platform, platform.Trim(), StringComparison.OrdinalIgnoreCase))
            ];
        }

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                totalGates = allGates.Count,
                gates = allGates.Select(g => new
                {
                    g.Id,
                    Category = g.Category.ToString(),
                    g.Platform,
                    Scope = g.Scope.ToString(),
                    Dimension = g.Dimension.ToString(),
                    Coordinate = g.CoordinateCode,
                    g.Name,
                    Severity = g.Severity.ToString(),
                    g.Source,
                    g.Description,
                    g.Remediation
                })
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Semantic Invariant Gates ({allGates.Count})\n");
        sb.AppendLine(GateCatalogTableHeader);
        sb.AppendLine(GateCatalogTableSeparator);
        foreach (var g in allGates)
        {
            sb.AppendLine($"| `{g.Id}` | {g.Category} | `{g.Platform}` | {g.Scope} | {g.Dimension} | **{g.Name}** | `{g.Severity}` | {g.Source} |");
        }
        sb.AppendLine();
        sb.AppendLine("> Use `check_semantic_gates` tool with `gateId` or `scope` to evaluate these architectural invariants.");
        return sb.ToString().TrimEnd();
    }

    private static string FormatSingleGateResult(GateCheckResult result, bool includeTolerated, int limit, string? fileFilter, string? groupBy, string format)
    {
        var toleratedFp = new HashSet<string>(result.ToleratedViolations.Select(t => t.Fingerprint));
        var candidates = result.Violations.Where(v =>
        {
            var isTol = toleratedFp.Contains(v.Fingerprint);
            if (!includeTolerated && isTol) return false;
            if (!string.IsNullOrWhiteSpace(fileFilter) && (v.File == null || !v.File.Contains(fileFilter, StringComparison.OrdinalIgnoreCase))) return false;
            return true;
        }).ToList();

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(groupBy))
            {
                var gb = groupBy.Trim().ToLowerInvariant();
                var groups = gb switch
                {
                    "callee" or "target" => candidates.GroupBy(v => ExtractCallee(v.Target)).Select(g => new { Key = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).ToList(),
                    "symbol" => candidates.GroupBy(v => v.Symbol ?? "(unknown)").Select(g => new { Key = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).ToList(),
                    _ => candidates.GroupBy(v => v.File ?? "(unknown)").Select(g => new { Key = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).ToList()
                };

                return JsonSerializer.Serialize(new
                {
                    gateId = result.Gate.Id,
                    name = result.Gate.Name,
                    status = result.Status.ToString(),
                    groupBy = gb,
                    totalViolations = candidates.Count,
                    groups = groups.Take(limit > 0 ? limit : int.MaxValue)
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            return JsonSerializer.Serialize(new
            {
                result.Gate,
                Status = result.Status.ToString(),
                result.BaselineCount,
                ViolationsCount = result.Violations.Count,
                NewViolationsCount = result.NewViolations.Count,
                ToleratedCount = result.ToleratedViolations.Count,
                Violations = candidates.Take(limit > 0 ? limit : int.MaxValue)
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        var sb = new System.Text.StringBuilder();
        var icon = result.Status switch
        {
            GateCheckStatus.Passed => "✅",
            GateCheckStatus.PassedTolerated => "⚠️",
            GateCheckStatus.Warning => "🟡",
            GateCheckStatus.FailedNew => "❌",
            _ => "💥"
        };

        sb.AppendLine($"### {icon} Gate `{result.Gate.Id}`: {result.Gate.Name}");
        sb.AppendLine($"- **Status**: `{result.Status}`");
        sb.AppendLine($"- **Scope & Dimension**: {result.Gate.Scope} / {result.Gate.Dimension} ({result.Gate.CoordinateCode})");
        sb.AppendLine($"- **Severity**: `{result.Gate.Severity}`");
        sb.AppendLine($"- **Violations**: {result.Violations.Count} (New: {result.NewViolations.Count}, Tolerated Baseline: {result.ToleratedViolations.Count})");
        sb.AppendLine($"- **Duration**: {result.Duration.TotalMilliseconds:F1}ms\n");

        if (!string.IsNullOrEmpty(result.Gate.Description))
        {
            sb.AppendLine($"> {result.Gate.Description}\n");
        }

        if (!string.IsNullOrWhiteSpace(groupBy))
        {
            var gb = groupBy.Trim().ToLowerInvariant();
            var groups = gb switch
            {
                "callee" or "target" => candidates.GroupBy(v => ExtractCallee(v.Target)).Select(g => new { Key = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).ToList(),
                "symbol" => candidates.GroupBy(v => v.Symbol ?? "(unknown)").Select(g => new { Key = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).ToList(),
                _ => candidates.GroupBy(v => v.File ?? "(unknown)").Select(g => new { Key = g.Key, Count = g.Count() }).OrderByDescending(g => g.Count).ToList()
            };

            sb.AppendLine($"#### Violations Summary Grouped by `{gb}` ({candidates.Count} total):");
            sb.AppendLine($"| Count | {gb.ToUpperInvariant()} |");
            sb.AppendLine("| :--- | :--- |");
            foreach (var g in groups.Take(limit > 0 ? limit : 50))
            {
                sb.AppendLine($"| {g.Count} | `{g.Key}` |");
            }
            if (groups.Count > (limit > 0 ? limit : 50))
            {
                sb.AppendLine($"\n*... and {groups.Count - (limit > 0 ? limit : 50)} more groups.*");
            }
            sb.AppendLine();
        }
        else if (result.NewViolations.Count > 0 || (includeTolerated && candidates.Count > 0))
        {
            var displayList = candidates.Take(limit > 0 ? limit : 50).ToList();
            var title = result.NewViolations.Count > 0 && !includeTolerated ? "#### ❌ New Violations (Blocking):" : $"#### Architectural Violations (Showing {displayList.Count} of {candidates.Count}):";
            sb.AppendLine(title);
            foreach (var v in displayList)
            {
                var isTol = toleratedFp.Contains(v.Fingerprint);
                var tag = isTol ? "[TOL]" : "[NEW]";
                var loc = v.Location != "-" ? $"`{v.Location}`" : "";
                var target = !string.IsNullOrEmpty(v.Target) ? $" → `{v.Target}`" : "";
                sb.AppendLine($"- {tag} {loc} **{v.Symbol}**{target}: {v.Message}");
            }
            if (candidates.Count > displayList.Count)
            {
                sb.AppendLine($"- *... and {candidates.Count - displayList.Count} more violations (use limit parameter for more)*");
            }
            sb.AppendLine();
        }
        else if (result.Violations.Count > 0)
        {
            sb.AppendLine($"> 💡 **Tolerated Debt**: {result.Violations.Count} violations are tolerated by baseline. Run with `includeTolerated: true` or `groupBy: 'file'` to inspect.");
        }

        if (!string.IsNullOrEmpty(result.Gate.Remediation) && result.NewViolations.Count > 0)
        {
            sb.AppendLine($"💡 **Remediation**: {result.Gate.Remediation}\n");
        }

        if (result.CanRatchetDown)
        {
            sb.AppendLine($"> 📉 **Ratchet Opportunity**: Active violations ({result.Violations.Count}) are less than baseline ({result.BaselineCount}). Run `ce gate ratchet --update` to lock down technical debt.");
        }

        return sb.ToString().TrimEnd();
    }

    private static string FormatSuiteResult(GateSuiteResult suite, string format)
    {
        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(suite, new JsonSerializerOptions { WriteIndented = true });
        }

        var sb = new System.Text.StringBuilder();
        var banner = suite.IsSuccess
            ? (suite.ToleratedViolationsCount > 0 ? "⚠️ **Semantic Gates: Passed with Tolerated Debt**" : "✅ **Semantic Gates: All Invariants Satisfied**")
            : "❌ **Semantic Gates: Architectural Violations Detected**";

        sb.AppendLine($"# {banner}\n");
        sb.AppendLine($"- **Total Gates Evaluated**: {suite.TotalGates}");
        sb.AppendLine($"- **Passed**: {suite.PassedCount} | **Failed**: {suite.FailedCount}");
        sb.AppendLine($"- **New Violations**: {suite.NewViolationsCount} | **Tolerated Baseline Debt**: {suite.ToleratedViolationsCount}");
        sb.AppendLine($"- **Evaluation Time**: {suite.TotalDuration.TotalMilliseconds:F1}ms\n");

        sb.AppendLine(GateSuiteTableHeader);
        sb.AppendLine(GateSuiteTableSeparator);
        foreach (var r in suite.Results)
        {
            var stIcon = r.Status switch
            {
                GateCheckStatus.Passed => "✅ PASS",
                GateCheckStatus.PassedTolerated => "⚠️ TOLERATED",
                GateCheckStatus.Warning => "🟡 WARN",
                GateCheckStatus.FailedNew => "❌ FAIL",
                _ => "💥 ERR"
            };
            var vCount = r.NewViolations.Count > 0 ? $"**{r.NewViolations.Count} new**" : $"{r.Violations.Count}";
            sb.AppendLine($"| `{r.Gate.Id}` | {r.Gate.Category} | `{r.Gate.Platform}` | {r.Gate.Scope} | {r.Gate.Dimension} | {r.Gate.Name} | {stIcon} | {vCount} |");
        }
        sb.AppendLine();

        var failedGates = suite.Results.Where(r => r.Status is GateCheckStatus.FailedNew or GateCheckStatus.Error).ToList();
        if (failedGates.Count > 0)
        {
            sb.AppendLine("## 🚨 Blocking Architectural Violations:\n");
            foreach (var fg in failedGates)
            {
                sb.AppendLine($"### `{fg.Gate.Id}`: {fg.Gate.Name}");
                if (!string.IsNullOrEmpty(fg.ErrorMessage))
                {
                    sb.AppendLine($"- **Error**: `{fg.ErrorMessage}`");
                }
                foreach (var v in fg.NewViolations.Take(10))
                {
                    var loc = v.Location != "-" ? $"`{v.Location}`" : "";
                    var target = !string.IsNullOrEmpty(v.Target) ? $" → `{v.Target}`" : "";
                    sb.AppendLine($"- {loc} **{v.Symbol}**{target}: {v.Message}");
                }
                if (fg.NewViolations.Count > 10)
                {
                    sb.AppendLine($"- *...and {fg.NewViolations.Count - 10} more violations*");
                }
                if (!string.IsNullOrEmpty(fg.Gate.Remediation))
                {
                    sb.AppendLine($"💡 **Remediation**: {fg.Gate.Remediation}\n");
                }
            }
        }

        if (suite.RatchetOpportunities > 0)
        {
            sb.AppendLine($"> 📉 **{suite.RatchetOpportunities} gate(s) have fewer violations than allowed by baseline.** Run `ce gate ratchet --update` to ratchet down technical debt.");
        }

        return sb.ToString().TrimEnd();
    }

}
