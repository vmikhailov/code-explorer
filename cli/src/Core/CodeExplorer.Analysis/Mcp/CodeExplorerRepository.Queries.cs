using CodeExplorer.Core.Gates.Engine;
using CodeExplorer.Core.Gates.Models;
using CodeExplorer.Core.Gates.Storage;
using CodeExplorer.Core.Mcp.Models;
using CodeExplorer.Cypher.Parser;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    private const string PropParamDefaultType = "string";
    private const string PropParamRequired = "required";
    private const string PropParamDescription = "description";
    private const string QueryGetWorkspaceContent = "get_workspace_content";
    private const string ParamWorkspaceId = "workspaceId";
    private const string ParamWorkspaceIdPrefix = "workspaceIdPrefix";
    private const string ParamProjectName = "projectName";
    private const string ParamProjectFilter = "projectFilter";
    private const string CypherFilePattern = "*.cypher";
    private const string JsonPropId = "id";
    private const string JsonPropLabel = "kind";
    private const string JsonPropRelAlias = "type";
    private const string JsonPropProperties = "properties";
    private const string JsonPropFromId = "from_id";
    private const string JsonPropToId = "to_id";

    public async Task<string> ExecuteCustomReadCypherAsync(string query, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var trimmed = query.Trim();
        if (!trimmed.Contains(' ') && !trimmed.Contains('\n') && Queries.Exists(trimmed))
        {
            return await ExecuteProjectQueryAsync(trimmed, null, workspacePath, cancellationToken);
        }

        CypherSecurityValidator.ValidateReadOnly(query);
        return await ExecuteAndFormatQueryAsync(query, new Dictionary<string, object?>(), workspacePath, cancellationToken);
    }

    public async Task<string> GetWorkspaceContentAsync(string? workspacePath = null, string? type = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        var query = Queries.Get(QueryGetWorkspaceContent);
        var res = await client.ExecuteCypherAsync(query, new { type = string.IsNullOrEmpty(type) ? null : type }, cancellationToken);
        return res.ToJson();
    }

    public async Task<string> ExecuteRawQueryAsync(string query, Dictionary<string, object?>? parameters = null, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        var res = await client.ExecuteCypherAsync(query, parameters, cancellationToken);
        return res.ToJson();
    }


    public async Task<string> ListProjectQueriesAsync(string? workspacePath, CancellationToken cancellationToken = default)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
        var queries = _queryManager.ListQueries(effectiveWsPath);
        return ToonYamlSerializer.SerializeObject(new
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
        });
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
            var parsedVal = ToonYamlSerializer.ParseYamlOrJson(parametersJson);
            if (parsedVal.IsNull)
            {
                throw new ArgumentException("Failed to parse parameters.", nameof(parametersJson));
            }

            if (parsedVal.Kind == CypherValueKind.Array)
            {
                foreach (var item in parsedVal.AsArray())
                {
                    var pName = item.GetStringProp(ColName);
                    if (string.IsNullOrEmpty(pName)) continue;
                    var pType = item.GetStringProp(ColType, PropParamDefaultType);
                    var pReq = !item.TryGetProperty(PropParamRequired, out var reqVal) || reqVal.AsBool(true);
                    var pDesc = item.GetStringProp(PropParamDescription);
                    paramList.Add(new ProjectQueryParameter(pName, pType, pReq, pDesc));
                }
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
        return ToonYamlSerializer.SerializeObject(new
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
        });
    }

    public async Task<string> ExecuteProjectQueryAsync(string name, string? parametersJson, string? workspacePath, CancellationToken cancellationToken = default)
    {
        var effectiveWsPath = await ResolveWorkspacePathAsync(workspacePath);
        var queryItem = _queryManager.GetQuery(effectiveWsPath, name);
        string cypherText;
        IReadOnlyList<ProjectQueryParameter> queryParams;

        if (queryItem != null)
        {
            cypherText = queryItem.Cypher;
            queryParams = queryItem.Parameters;
        }
        else if (Queries.Exists(name))
        {
            cypherText = Queries.Get(name);
            queryParams = [];
        }
        else
        {
            throw new FileNotFoundException(
                $"Project query '{name}' not found in '{_queryManager.GetQueriesDirectory(effectiveWsPath)}' or built-in queries. " +
                "Pass 'listSaved=true' to execute_custom_read_cypher to see available saved queries.");
        }

        var paramDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(parametersJson))
        {
            var parsedParams = ToonYamlSerializer.ParseYamlOrJson(parametersJson);
            if (parsedParams.IsNull || parsedParams.Kind != CypherValueKind.Object)
            {
                throw new ArgumentException("Failed to parse parameters.", nameof(parametersJson));
            }

            foreach (var (k, v) in parsedParams.AsObject())
            {
                paramDict[k] = v.Kind switch
                {
                    CypherValueKind.String => v.AsString(),
                    CypherValueKind.Int64 => v.AsInt64(),
                    CypherValueKind.Float64 => v.AsDouble(),
                    CypherValueKind.Boolean => v.AsBool(),
                    CypherValueKind.Null => null,
                    _ => v.ToJsonString()
                };
            }
        }

        foreach (var p in queryParams)
        {
            if (p.Required && (!paramDict.ContainsKey(p.Name) || paramDict[p.Name] == null))
            {
                throw new ArgumentException($"Required parameter '{p.Name}' is missing for query '{name}'.");
            }
        }

        if (cypherText.Contains("$workspaceId") && !paramDict.ContainsKey(ParamWorkspaceId))
        {
            paramDict[ParamWorkspaceId] = OntologyConstants.IdPrefixes.Workspace;
        }

        if (cypherText.Contains("$workspaceIdPrefix") && !paramDict.ContainsKey(ParamWorkspaceIdPrefix))
        {
            paramDict[ParamWorkspaceIdPrefix] = $"{OntologyConstants.IdPrefixes.Workspace}:";
        }

        if (cypherText.Contains("$projectName", StringComparison.OrdinalIgnoreCase) && !paramDict.ContainsKey(ParamProjectName))
        {
            paramDict[ParamProjectName] = string.Empty;
        }

        if (cypherText.Contains("$projectFilter", StringComparison.OrdinalIgnoreCase) && !paramDict.ContainsKey(ParamProjectFilter))
        {
            paramDict[ParamProjectFilter] = string.Empty;
        }

        return await ExecuteAndFormatQueryAsync(cypherText, paramDict, workspacePath, cancellationToken);
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
            return ToonYamlSerializer.SerializeObject(new
            {
                status = "exists",
                message = $"Workspace already exists at '{existing.RootDirectory}'. Set force=true to reinitialize.",
                workspacePath = existing.RootDirectory,
                dbPath = existing.DbPath
            });
        }

        var ws = WorkspaceLocator.Initialize(targetDir, wsName);
        await using (var client = new SqliteGraphClient(ws.DbPath))
        {
            await client.ExecuteWriteAsync(
                $"INSERT INTO nodes (id, kind, properties) VALUES ('workspace', {GraphKindCodec.EncodeNodeKind(NodeKind.Workspace)}, jsonb(json_object('name', @name, 'path', @path))) " +
                "ON CONFLICT(id) DO UPDATE SET properties = jsonb(json_object('name', @name, 'path', @path));",
                new { name = wsName, path = ws.RootDirectory }, cancellationToken);
        }

        return ToonYamlSerializer.SerializeObject(new
            {
                status = "initialized",
                name = wsName,
                workspacePath = ws.RootDirectory,
                dbPath = ws.DbPath,
                queriesPath = ws.QueriesDirectory
            });
    }

    public async Task<string> ScanWorkspaceAsync(
        string? path = null,
        bool clear = false,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var baseDir = Path.GetFullPath(workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var targetDir = string.IsNullOrWhiteSpace(path)
            ? baseDir
            : Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path));
        var ws = WorkspaceLocator.Find(targetDir) ?? WorkspaceLocator.Find(baseDir);
        if (ws == null)
        {
            var defaultName = new DirectoryInfo(baseDir).Name;
            ws = WorkspaceLocator.Initialize(baseDir, defaultName);
        }

        var hadActiveWatcher = HasActiveWatcher(ws.RootDirectory);
        var client = await ResolveClientAsync(ws.RootDirectory);
        var indexer = GetOrCreateIndexer(ws.RootDirectory, client);

        int nodesCount;
        int relsCount;
        Dictionary<string, int> nodesByKind;

        if (!clear && hadActiveWatcher && PathsEqualNormalized(targetDir, ws.RootDirectory))
        {
            (nodesCount, relsCount) = await client.GetGraphCountsAsync(cancellationToken);
            nodesByKind = await client.GetNodesBreakdownAsync(cancellationToken);
        }
        else
        {
            if (clear)
            {
                indexer.ResetFileRegistry();
            }

            (nodesCount, relsCount, nodesByKind) = await indexer.IndexAsync(
                targetDir,
                ws.RootDirectory,
                clear: clear,
                cancellationToken: cancellationToken);
        }

        var visibleNodesByKind = nodesByKind
            .Where(kv => !IsTemporarilyHiddenTaxonomyLabel(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);

        InvalidateCache();

        return ToonYamlSerializer.SerializeObject(new
        {
            status = "success",
            workspacePath = ws.RootDirectory,
            scannedPath = targetDir,
            nodesCount,
            relationshipsCount = relsCount,
            nodesByKind = visibleNodesByKind
        });
    }

    public async Task<string> GetWorkspaceStatusAsync(
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetFullPath(workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory());
        var ws = WorkspaceLocator.Find(targetDir);
        if (ws == null || !File.Exists(ws.DbPath))
        {
            return ToonYamlSerializer.SerializeObject(new
            {
                status = "not_initialized",
                message = $"No indexed workspace found for '{targetDir}'. Call scan_workspace first."
            });
        }

        var fileInfo = new FileInfo(ws.DbPath);
        var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
        var client = await ResolveClientAsync(ws.RootDirectory);

        const string projectsStatusQuery = "MATCH (p:Project) RETURN p.name AS name, p.language AS language, p.project_type AS type, p.path AS path";
        const string nodeCountsQuery = "MATCH (n) WHERE NOT n:Domain AND NOT n:BoundedContext AND NOT n:LogicalStructure RETURN labels(n)[0] AS kind, count(n) AS count ORDER BY count DESC";
        var projRes = await client.ExecuteCypherAsync(projectsStatusQuery, null, cancellationToken);
        var countRes = await client.ExecuteCypherAsync(nodeCountsQuery, null, cancellationToken);

        var customQueriesCount = Directory.Exists(ws.QueriesDirectory)
            ? Directory.GetFiles(ws.QueriesDirectory, CypherFilePattern).Length
            : 0;

        return ToonYamlSerializer.SerializeObject(
            new
            {
                status = "ready",
                workspacePath = ws.RootDirectory,
                dbPath = ws.DbPath,
                databaseSizeMb = Math.Round(sizeMb, 2),
                projects = projRes.Rows,
                nodeCounts = countRes.Rows,
                customQueriesCount
            });
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
            return ToonYamlSerializer.SerializeObject(new
            {
                status = "not_found",
                message = "Workspace or database not found."
            });
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

        return ToonYamlSerializer.SerializeObject(new
        {
            status = "cleared",
            targetPath = targetDir
        });
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
            return StandbyMessageYaml;
        }

        var nodesRoot = ToonYamlSerializer.ParseYamlOrJson(nodesJson);

        var nodesIngested = 0;
        var relsIngested = 0;

        if (nodesRoot.Kind == CypherValueKind.Array)
        {
            foreach (var node in nodesRoot.AsArray())
            {
                if (node.TryGetProperty(JsonPropId, out var idProp) && node.TryGetProperty(JsonPropLabel, out var kindProp))
                {
                    var id = idProp.AsNullableString();
                    var kind = kindProp.AsNullableString();
                    if (id == null || kind == null) continue;
                    var props = node.TryGetProperty(JsonPropProperties, out var p) && !p.IsNull ? p.ToJsonString() : "{}";

                    await client.ExecuteWriteAsync(
                        "INSERT INTO nodes (id, kind, properties) VALUES (@id, @kind, jsonb(@props)) ON CONFLICT(id) DO UPDATE SET kind = @kind, properties = jsonb(@props);",
                        new { id, kind = GraphKindCodec.EncodeNodeKind(kind), props },
                        cancellationToken);
                    nodesIngested++;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(relationshipsJson))
        {
            var relsRoot = ToonYamlSerializer.ParseYamlOrJson(relationshipsJson);

            if (relsRoot.Kind == CypherValueKind.Array)
            {
                foreach (var rel in relsRoot.AsArray())
                {
                    var kind = rel.TryGetProperty(JsonPropLabel, out var kProp)
                        ? kProp.AsNullableString()
                        : (rel.TryGetProperty(JsonPropRelAlias, out var tProp) ? tProp.AsNullableString() : null);

                    if (!string.IsNullOrEmpty(kind) && rel.TryGetProperty(JsonPropFromId, out var fProp) &&
                        rel.TryGetProperty(JsonPropToId, out var toProp))
                    {
                        var fromId = fProp.AsNullableString();
                        var toId = toProp.AsNullableString();
                        if (fromId == null || toId == null) continue;
                        var props = rel.TryGetProperty(JsonPropProperties, out var p) && !p.IsNull ? p.ToJsonString() : "{}";

                        await client.ExecuteWriteAsync(
                            "INSERT OR IGNORE INTO nodes (id, kind, properties) VALUES (@fromId, 0, jsonb('{}')), (@toId, 0, jsonb('{}')); " +
                            "INSERT OR IGNORE INTO edges (from_id, to_id, kind, properties) VALUES ((SELECT rowid FROM nodes WHERE id = @fromId), (SELECT rowid FROM nodes WHERE id = @toId), @kind, jsonb(@props));",
                            new { kind = GraphKindCodec.EncodeRelKind(kind), fromId, toId, props }, cancellationToken);
                        relsIngested++;
                    }
                }
            }
        }

        InvalidateCache();

        return ToonYamlSerializer.SerializeObject(
            new { status = "success", nodesIngested, relationshipsIngested = relsIngested });
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
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory();
        var ws = WorkspaceLocator.Find(targetDir);
        var effectivePath = ws?.RootDirectory ?? Path.GetFullPath(targetDir);

        var client = await ResolveClientAsync(effectivePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var gateRepo = new FileGateRepository(effectivePath);
        var engine = new GateExecutionEngine(client, gateRepo);

        InvariantScope? scopeFilter = !string.IsNullOrWhiteSpace(scope) && Enum.TryParse<InvariantScope>(scope, true, out var s) ? s : null;
        InvariantDimension? dimFilter = !string.IsNullOrWhiteSpace(dimension) && Enum.TryParse<InvariantDimension>(dimension, true, out var d) ? d : null;
        GateCategory? categoryFilter = !string.IsNullOrWhiteSpace(category) && Enum.TryParse<GateCategory>(category, true, out var c) ? c : null;
        var platformFilter = !string.IsNullOrWhiteSpace(platform) ? platform.Trim() : null;

        if (!string.IsNullOrWhiteSpace(gateId))
        {
            var singleGate = await gateRepo.GetGateByIdAsync(gateId, cancellationToken);
            if (singleGate == null)
            {
                return ToonYamlSerializer.SerializeObject(new { error = $"Gate '{gateId}' not found." });
            }

            var singleResult = await engine.CheckGateAsync(singleGate, cancellationToken);
            return McpGateFormatter.FormatSingleGateResult(singleResult, includeTolerated, limit, fileFilter, groupBy);
        }

        var suiteResult = await engine.CheckSuiteAsync(
            null,
            scopeFilter,
            dimFilter,
            categoryFilter,
            platformFilter,
            cancellationToken);
        return McpGateFormatter.FormatSuiteResult(suiteResult, includeTolerated, limit, fileFilter, groupBy);
    }

    public async Task<string> ListSemanticGatesAsync(
        string? category = null,
        string? platform = null,
        string? workspacePath = null,
        CancellationToken cancellationToken = default,
        string? scope = null,
        string? dimension = null)
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
        if (!string.IsNullOrWhiteSpace(scope) && Enum.TryParse<InvariantScope>(scope, true, out var parsedScope))
        {
            allGates = [.. allGates.Where(g => g.Scope == parsedScope)];
        }
        if (!string.IsNullOrWhiteSpace(dimension) && Enum.TryParse<InvariantDimension>(dimension, true, out var parsedDim))
        {
            allGates = [.. allGates.Where(g => g.Dimension == parsedDim)];
        }

        var payload = new
        {
            totalGates = allGates.Count,
            gates = allGates.Select(g => new
            {
                id = g.Id,
                category = g.Category.ToString(),
                platform = g.Platform,
                scope = g.Scope.ToString(),
                dimension = g.Dimension.ToString(),
                coordinate = g.CoordinateCode,
                name = g.Name,
                severity = g.Severity.ToString(),
                source = g.Source.ToString(),
                description = g.Description,
                remediation = g.Remediation
            })
        };

        return ToonYamlSerializer.SerializeObject(payload);
    }

    public async Task<string> GetTaskContextAsync(
        string query,
        int maxAnchors = 8,
        bool includeGates = true,
        string? workspacePath = null,
        CancellationToken cancellationToken = default)
    {
        var targetDir = workspacePath ?? DefaultWorkspacePath ?? Directory.GetCurrentDirectory();
        var ws = WorkspaceLocator.Find(targetDir);
        var effectivePath = ws?.RootDirectory ?? Path.GetFullPath(targetDir);

        var client = await ResolveClientAsync(effectivePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        return await McpContextPackBuilder.BuildTaskContextYamlAsync(
            client,
            query,
            maxAnchors,
            includeGates,
            effectivePath,
            cancellationToken);
    }

}
