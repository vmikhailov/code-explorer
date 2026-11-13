using System.Text.Json;
using CodeExplorer.Core.Analysis;

namespace CodeExplorer.Core.Mcp;

public partial class CodeExplorerRepository
{
    private const int MaxDirectoryDirectFiles = 100;
    private const int MaxTopSymbolsPerFile = 8;
    private const string ColType = "type";
    private const string ColName = "name";
    private const string ColFromLabel = "fromLabel";
    private const string ColRelType = "relType";
    private const string ColToLabel = "toLabel";
    private const string ColLabel = "label";
    private const string ColKey = "key";
    private const string ColPath = "path";
    private const string ColLanguage = "language";
    private const string ColLines = "lines";
    private const string ColSymbolName = "symbolName";
    private const string ParamFilePath = "filePath";
    private const string ParamDirPrefix = "dirPrefix";
    private const string ParamNormalizedDir = "normalizedDir";
    private const string QueryGetFileOutline = "get_file_outline";
    private const string WorkspaceRootPathQuery = "MATCH (w:Workspace) RETURN w.path LIMIT 1";
    private const string FastFileOutlineQuery =
        "MATCH (f:File) WHERE f.path IS NOT NULL AND f.path = $filePath " +
        "OPTIONAL MATCH (child)-[:DECLARED_IN]->(f) " +
        "WHERE child:Type OR child:Function OR child:Member OR child:Query " +
        "WITH child WHERE child IS NOT NULL " +
        "RETURN child.name AS name, " +
        "CASE WHEN child:Type THEN (CASE WHEN child.kind = 'class' THEN 'Class' WHEN child.kind = 'interface' THEN 'Interface' ELSE child.kind END) ELSE labels(child)[0] END AS type, " +
        "coalesce(child.start_line, 0) AS startLine, coalesce(child.end_line, 0) AS endLine, coalesce(child.symbol, child.name) AS symbol " +
        "ORDER BY child.start_line";
    private const string RootDirectoryFilesQuery =
        "MATCH (f:File) WHERE f.path IS NOT NULL " +
        "RETURN f.path AS path, coalesce(f.language, '') AS language, coalesce(f.line_count, f.loc, f.lines, 0) AS lines";
    private const string SubDirectoryFilesQuery =
        "MATCH (f:File) WHERE f.path IS NOT NULL AND f.path STARTS WITH $dirPrefix " +
        "RETURN f.path AS path, coalesce(f.language, '') AS language, coalesce(f.line_count, f.loc, f.lines, 0) AS lines";
    private const string RootDirectFileSymbolsQuery =
        "MATCH (f:File) WHERE f.path IS NOT NULL AND NOT (f.path CONTAINS '/') " +
        "MATCH (s)-[:DECLARED_IN]->(f) WHERE s:Type OR s:Function " +
        "RETURN f.path AS path, s.name AS symbolName ORDER BY coalesce(s.start_line, 0)";
    private const string SubDirectFileSymbolsQuery =
        "MATCH (f:File) WHERE f.path IS NOT NULL AND f.path STARTS WITH $dirPrefix " +
        "AND (f.name IS NULL OR f.path = ($normalizedDir + '/' + f.name)) " +
        "MATCH (s)-[:DECLARED_IN]->(f) WHERE s:Type OR s:Function " +
        "RETURN f.path AS path, s.name AS symbolName ORDER BY coalesce(s.start_line, 0)";
    private const string PropTaxonomy = "taxonomy";
    private const string PropProperties = "properties";
    private const string PropOutgoing = "outgoing";
    private const string PropRelationship = "relationship";
    private const string PropTarget = "target";
    private const string PropProps = "props";
    private const string PropFilePath = "file_path";
    private const string PropStartLineSnake = "start_line";
    private const string PropEndLineSnake = "end_line";

    public async Task<string> GetProjectDependenciesAsync(string? projectFilter = null, int limit = 50, string type = "all", string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        if (!string.IsNullOrEmpty(projectFilter))
        {
            var query = Queries.Get("get_project_dependencies_filtered");
            var res = await client.ExecuteCypherAsync(query, new Dictionary<string, string> { ["projectFilter"] = projectFilter }, cancellationToken);
            return ToonYamlSerializer.SerializeYaml(res);
        }
        else
        {
            var query = Queries.Get("get_project_dependencies_all");
            var res = await client.ExecuteCypherAsync(query, null, cancellationToken);
            return ToonYamlSerializer.SerializeYaml(res);
        }
    }

    public async Task<string> GetFileOutlineAsync(string filePath, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var effectiveWsRoot = !string.IsNullOrWhiteSpace(workspacePath) ? workspacePath : DefaultWorkspacePath;
        if (string.IsNullOrWhiteSpace(effectiveWsRoot) && Path.IsPathRooted(filePath))
        {
            effectiveWsRoot = await client.QueryFirstOrDefaultAsync<string>(WorkspaceRootPathQuery, null, cancellationToken);
        }

        var normalizedPath = NormalizeOutlineTargetPath(filePath, effectiveWsRoot);
        if (!string.IsNullOrEmpty(normalizedPath))
        {
            var parameters = new Dictionary<string, string>
            {
                [ParamFilePath] = normalizedPath
            };
            var res = await client.ExecuteCypherAsync(FastFileOutlineQuery, parameters, cancellationToken);
            if (res.Count == 0)
            {
                res = await client.ExecuteCypherAsync(Queries.Get(QueryGetFileOutline), parameters, cancellationToken);
            }
            if (res.Count > 0)
            {
                return ToonYamlSerializer.SerializeYaml(res);
            }
        }

        var directoryOutline = await TryBuildDirectoryOutlineYamlAsync(client, normalizedPath, cancellationToken);
        if (directoryOutline != null)
        {
            return directoryOutline;
        }

        var empty = await client.ExecuteCypherAsync(FastFileOutlineQuery, new Dictionary<string, string> { [ParamFilePath] = normalizedPath }, cancellationToken);
        return ToonYamlSerializer.SerializeYaml(empty);
    }

    private static string NormalizeOutlineTargetPath(string rawPath, string? workspaceRoot)
    {
        var trimmed = rawPath.Trim();
        if (trimmed is "." or "/" or "\\")
        {
            return string.Empty;
        }

        var normalized = (IdentifierSearchEngine.NormalizeSymbolPath(trimmed, workspaceRoot) ?? string.Empty).Trim('/');
        return normalized == "." ? string.Empty : normalized;
    }

    private static async Task<string?> TryBuildDirectoryOutlineYamlAsync(
        IGraphClient client,
        string normalizedDir,
        CancellationToken cancellationToken)
    {
        var isRoot = string.IsNullOrEmpty(normalizedDir);
        var dirPrefix = isRoot ? string.Empty : $"{normalizedDir}/";
        var dirParams = isRoot
            ? null
            : new Dictionary<string, string>
            {
                [ParamDirPrefix] = dirPrefix,
                [ParamNormalizedDir] = normalizedDir
            };

        var fileRows = await client.ExecuteCypherAsync(
            isRoot ? RootDirectoryFilesQuery : SubDirectoryFilesQuery,
            dirParams,
            cancellationToken);
        if (fileRows.Count == 0)
        {
            return null;
        }

        var subfolders = new Dictionary<string, (int FileCount, long TotalLines)>(StringComparer.OrdinalIgnoreCase);
        var directFiles = new List<(string Path, string Language, long Lines)>();
        foreach (var row in fileRows.Rows)
        {
            var path = row.GetStringProp(ColPath);
            if (string.IsNullOrEmpty(path)) continue;
            var rel = isRoot ? path : (path.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase) ? path[dirPrefix.Length..] : path);
            var lines = row.GetLongProp(ColLines);
            var slashIdx = rel.IndexOf('/');
            if (slashIdx >= 0)
            {
                var subName = rel[..slashIdx];
                if (!string.IsNullOrEmpty(subName))
                {
                    var prev = subfolders.GetValueOrDefault(subName);
                    subfolders[subName] = (prev.FileCount + 1, prev.TotalLines + lines);
                }
            }
            else
            {
                directFiles.Add((path, row.GetStringProp(ColLanguage) ?? string.Empty, lines));
            }
        }

        var symbolsByFile = await FetchDirectFileSymbolsAsync(client, isRoot, dirParams, directFiles, cancellationToken);
        var summary = new
        {
            directory = isRoot ? "." : normalizedDir,
            totalFiles = fileRows.Count,
            subdirectories = subfolders
                .OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new { name = kv.Key, fileCount = kv.Value.FileCount, totalLines = kv.Value.TotalLines })
                .ToList(),
            files = directFiles
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .Take(MaxDirectoryDirectFiles)
                .Select(f => new
                {
                    path = f.Path,
                    language = f.Language,
                    lines = f.Lines,
                    symbols = symbolsByFile.GetValueOrDefault(f.Path) ?? []
                })
                .ToList()
        };
        return ToonYamlSerializer.SerializeObject(summary);
    }

    private static async Task<Dictionary<string, List<string>>> FetchDirectFileSymbolsAsync(
        IGraphClient client,
        bool isRoot,
        Dictionary<string, string>? dirParams,
        List<(string Path, string Language, long Lines)> directFiles,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (directFiles.Count == 0)
        {
            return result;
        }

        var directSet = new HashSet<string>(directFiles.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        var symRows = await client.ExecuteCypherAsync(
            isRoot ? RootDirectFileSymbolsQuery : SubDirectFileSymbolsQuery,
            dirParams,
            cancellationToken);
        foreach (var row in symRows.Rows)
        {
            var path = row.GetStringProp(ColPath);
            var sym = row.GetStringProp(ColSymbolName);
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(sym) || !directSet.Contains(path))
            {
                continue;
            }

            if (!result.TryGetValue(path, out var list))
            {
                list = [];
                result[path] = list;
            }
            if (list.Count < MaxTopSymbolsPerFile && !list.Contains(sym, StringComparer.Ordinal))
            {
                list.Add(sym);
            }
        }
        return result;
    }


    public async Task<string> InspectDataLineageAsync(string tableName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var query = Queries.Get("inspect_data_lineage");
        var parameters = new Dictionary<string, string>
        {
            ["tableName"] = tableName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }

    public async Task<string> GetProjectEntryPointsAsync(string projectName, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var query = Queries.Get("get_project_entry_points");
        var parameters = new Dictionary<string, string>
        {
            ["projectName"] = projectName
        };
        return await ExecuteAndFormatQueryAsync(query, parameters, workspacePath, cancellationToken);
    }

    private async Task AppendMetricResultsAsync(List<CypherRow> results, string queryKey, Dictionary<string, string> parameters, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var client = await ResolveClientAsync(workspacePath);
        var query = Queries.Get(queryKey);
        var res = await client.ExecuteCypherAsync(query, parameters, cancellationToken);
        results.AddRange(res.Rows);
    }

    public async Task<string> FindRefactoringOpportunitiesAsync(string projectName, string metricType = "all", string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var results = new List<CypherRow>();
        var parameters = new Dictionary<string, string> { ["projectName"] = projectName };

        if (metricType is "dead_code" or "all")
            await AppendMetricResultsAsync(results, "find_refactor_dead_code", parameters, workspacePath, cancellationToken);

        if (metricType is "god_objects" or "all")
            await AppendMetricResultsAsync(results, "find_refactor_god_objects", parameters, workspacePath, cancellationToken);

        return ToonYamlSerializer.SerializeObject(new { results });
    }

    public async Task<string> GetTaxonomyAsync(string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"taxonomy:{workspacePath ?? ""}";
        if (_macroCache.TryGetValue(cacheKey, out var cachedTaxonomy))
        {
            return cachedTaxonomy;
        }

        var client = await ResolveClientAsync(workspacePath);
        if (await IsEmptyStandbyAsync(client))
        {
            return StandbyMessageYaml;
        }

        var fastTaxonomyJson = await client.GetTaxonomyJsonAsync(cancellationToken);
        if (!string.IsNullOrEmpty(fastTaxonomyJson))
        {
            var fastYaml = FormatTaxonomyYaml(fastTaxonomyJson);
            _macroCache[cacheKey] = fastYaml;
            return fastYaml;
        }

        var query = Queries.Get("get_taxonomy_nodes");
        var propQuery = Queries.Get("get_taxonomy_properties");

        var res = await client.ExecuteCypherAsync(query, null, cancellationToken);
        var parsedTriplets = res.Rows.Select(r => new Dictionary<string, string>
        {
            [ColFromLabel] = r.GetStringProp(ColFromLabel),
            [ColRelType] = r.GetStringProp(ColRelType),
            [ColToLabel] = r.GetStringProp(ColToLabel)
        }).ToList();

        var propRes = await client.ExecuteCypherAsync(propQuery, null, cancellationToken);
        var parsedProperties = propRes.Rows.Select(r => new Dictionary<string, string>
        {
            [ColLabel] = r.GetStringProp(ColLabel),
            [ColKey] = r.GetStringProp(ColKey)
        }).ToList();

        var taxonomy = BuildTaxonomy(parsedTriplets, parsedProperties);
        var finalJson = JsonSerializer.Serialize(new { taxonomy }, CompactJsonOptions);
        var finalYaml = FormatTaxonomyYaml(finalJson);
        _macroCache[cacheKey] = finalYaml;
        return finalYaml;
    }

    private const string HiddenLabelDomain = "Domain";
    private const string HiddenLabelBoundedContext = "BoundedContext";
    private const string HiddenLabelLogicalStructure = "LogicalStructure";
    private const string HiddenPropDomain = "domain";
    private const string HiddenPropBoundedContext = "bounded_context";
    private const string HiddenPropIntentSummary = "intent_summary";
    private const string HiddenRelBelongsToDomain = "BELONGS_TO_DOMAIN";

    public static bool IsTemporarilyHiddenTaxonomyLabel(string? label) =>
        string.Equals(label, HiddenLabelDomain, StringComparison.OrdinalIgnoreCase)
        || string.Equals(label, HiddenLabelBoundedContext, StringComparison.OrdinalIgnoreCase)
        || string.Equals(label, HiddenLabelLogicalStructure, StringComparison.OrdinalIgnoreCase);

    private static bool IsTemporarilyHiddenTaxonomyProperty(string? prop) =>
        string.Equals(prop, HiddenPropDomain, StringComparison.OrdinalIgnoreCase)
        || string.Equals(prop, HiddenPropBoundedContext, StringComparison.OrdinalIgnoreCase)
        || string.Equals(prop, HiddenPropIntentSummary, StringComparison.OrdinalIgnoreCase);

    private static bool IsTemporarilyHiddenTaxonomyRelationship(string? rel) =>
        string.Equals(rel, HiddenRelBelongsToDomain, StringComparison.OrdinalIgnoreCase);

    public static string FormatTaxonomyYaml(string taxonomyJson)
    {
        var root = CypherValue.TryParseJsonText(taxonomyJson);
        if (!root.TryGetProperty(PropTaxonomy, out var taxArr) || taxArr.Kind != CypherValueKind.Array || taxArr.AsArray().Count == 0)
        {
            return "nodes: {}\nrelationships: {}";
        }

        const string relPropsLine = "relationship_properties: [call_count, category, dependency_type, file_path, hops, line, operation]";
        const string semanticsHeader = "semantics:";
        const string semanticsAttributes = "  attributes: Direct AST annotations/decorators declared on that symbol only (comma-separated, never inherited or rolled up from child methods)";
        const string semanticsInheritance = "  inheritance: Type.extends and Type.implements hold declared base/interface names (including external framework types); INHERITS_FROM and IMPLEMENTS edges connect types defined inside the workspace";
        const string semanticsMemberAccess = "  member_access: (Member)-[r:ACCESSED_BY]->(Function) is emitted only when methods/operations are invoked on Member inside Function (r.operation holds comma-separated invoked method names; unused injections or plain argument passing do not emit ACCESSED_BY)";
        const string semanticsCallResolution = "  call_resolution: (caller:Function)-[:CALLS]->(callee:Function) resolves each call site to its declaring Type ((Type)-[:HAS_METHOD]->(callee), distinguishing interface methods from static extension/helper methods and overloads via Function.id)";
        const string semanticsSnippets = "  source_snippets: Use fetch_code_snippets (nodes YAML list of {file_path, start_line, end_line}, workspacePath) to batch-read exact node line ranges";
        const string nodesHeader = "nodes:";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"common_properties: [{string.Join(", ", OntologyRegistry.CommonPropertyKeys)}]");
        sb.AppendLine(relPropsLine);
        sb.AppendLine(semanticsHeader);
        sb.AppendLine(semanticsAttributes);
        sb.AppendLine(semanticsInheritance);
        sb.AppendLine(semanticsMemberAccess);
        sb.AppendLine(semanticsCallResolution);
        sb.AppendLine(semanticsSnippets);
        sb.AppendLine(nodesHeader);

        var nodesByLayer = new SortedDictionary<(int Order, string Key), SortedDictionary<string, string>>();
        var relsByType = new SortedDictionary<string, SortedDictionary<string, SortedSet<string>>>(StringComparer.Ordinal);

        foreach (var entry in taxArr.AsArray())
        {
            var label = entry.GetStringProp(ColLabel);
            if (string.IsNullOrEmpty(label)
                || OntologyRegistry.IsSystemNode(label)
                || IsTemporarilyHiddenTaxonomyLabel(label))
            {
                continue;
            }

            var props = new List<string>();
            if (entry.TryGetProperty(PropProperties, out var propsVal) && propsVal.Kind == CypherValueKind.Array)
            {
                foreach (var p in propsVal.AsArray())
                {
                    var pStr = p.AsString();
                    if (!string.IsNullOrEmpty(pStr)
                        && !OntologyRegistry.IsSystemProperty(pStr)
                        && !IsTemporarilyHiddenTaxonomyProperty(pStr)
                        && !OntologyRegistry.CommonPropertySet.Contains(pStr))
                    {
                        props.Add(pStr);
                    }
                }
            }

            props.Sort(StringComparer.Ordinal);
            var layer = OntologyRegistry.GetTaxonomyLayer(label);
            if (!nodesByLayer.TryGetValue(layer, out var layerNodes))
            {
                layerNodes = new SortedDictionary<string, string>(StringComparer.Ordinal);
                nodesByLayer[layer] = layerNodes;
            }
            layerNodes[label] = $"[{string.Join(", ", props)}]";

            if (entry.TryGetProperty(PropOutgoing, out var outVal) && outVal.Kind == CypherValueKind.Array)
            {
                foreach (var edge in outVal.AsArray())
                {
                    var rel = edge.GetStringProp(PropRelationship);
                    var target = edge.GetStringProp(PropTarget);
                    if (string.IsNullOrEmpty(rel)
                        || string.IsNullOrEmpty(target)
                        || OntologyRegistry.IsSystemNode(target)
                        || IsTemporarilyHiddenTaxonomyLabel(target)
                        || IsTemporarilyHiddenTaxonomyRelationship(rel))
                    {
                        continue;
                    }

                    if (!relsByType.TryGetValue(rel, out var sourcesMap))
                    {
                        sourcesMap = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
                        relsByType[rel] = sourcesMap;
                    }

                    if (!sourcesMap.TryGetValue(label, out var targetsSet))
                    {
                        targetsSet = new SortedSet<string>(StringComparer.Ordinal);
                        sourcesMap[label] = targetsSet;
                    }

                    targetsSet.Add(target);
                }
            }
        }

        foreach (var ((_, layerKey), layerNodes) in nodesByLayer)
        {
            sb.AppendLine($"  {layerKey}:");
            foreach (var (nodeLabel, propsInline) in layerNodes)
            {
                sb.AppendLine($"    {nodeLabel}: {propsInline}");
            }
        }

        sb.AppendLine("relationships:");
        foreach (var (rel, sourcesMap) in relsByType)
        {
            var byTargetsSignature = new List<(string TargetsSig, List<string> Sources)>();
            var sigIndex = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var (src, targets) in sourcesMap)
            {
                var sig = string.Join("|", targets);
                if (!sigIndex.TryGetValue(sig, out var idx))
                {
                    idx = byTargetsSignature.Count;
                    sigIndex[sig] = idx;
                    byTargetsSignature.Add((sig, [src]));
                }
                else
                {
                    byTargetsSignature[idx].Sources.Add(src);
                }
            }

            var pairs = byTargetsSignature
                .Select(g => $"{string.Join("|", g.Sources)} -> {g.TargetsSig}");
            sb.AppendLine($"  {rel}: [{string.Join(", ", pairs)}]");
        }

        return sb.ToString().TrimEnd();
    }


    public async Task<string> FetchCodeSnippetsAsync(string nodesJson, string? workspacePath = null, CancellationToken cancellationToken = default)
    {
        return await FetchCodeSnippetsDirectlyAsync(nodesJson, workspacePath);
    }

    public string GetNodeDefinition(string kind)
    {
        return OntologyRegistry.GetNodeDefinition(kind);
    }
    private static List<McpRAGNode>? ParseRagNodes(string nodesInput)
    {
        var parsed = ToonYamlSerializer.ParseYamlOrJson(nodesInput);
        if (!parsed.IsNull)
        {
            var result = new List<McpRAGNode>();
            var items = parsed.Kind == CypherValueKind.Array ? parsed.AsArray() : [parsed];
            foreach (var item in items)
            {
                var nodeVal = item.TryGetProperty(PropProps, out var propsObj) && propsObj.Kind == CypherValueKind.Object
                    ? propsObj
                    : item;
                if (nodeVal.Kind == CypherValueKind.Object)
                {
                    var fp = nodeVal.TryGetProperty(PropFilePath, out var fpVal) ? fpVal.AsNullableString() : null;
                    var sl = nodeVal.TryGetProperty(PropStartLineSnake, out var slVal) ? (int?)slVal.AsInt64() : null;
                    var el = nodeVal.TryGetProperty(PropEndLineSnake, out var elVal) ? (int?)elVal.AsInt64() : null;
                    if (!string.IsNullOrEmpty(fp))
                    {
                        result.Add(new McpRAGNode
                        {
                            file_path = fp,
                            start_line = sl,
                            end_line = el
                        });
                    }
                }
            }
            if (result.Count > 0) return result;
        }
        return null;
    }

    private static string? ResolveWorkspaceRoot(string? hostWorkspacePath)
    {
        var workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
        if (!string.IsNullOrEmpty(workspaceRoot)) return workspaceRoot;

        if (!string.IsNullOrEmpty(hostWorkspacePath)) return hostWorkspacePath;

        return WorkspaceLocator.Find()?.RootDirectory;
    }

    private static string FormatCodeSnippet(string[] lines, McpRAGNode node)
    {
        var startLine = node.start_line ?? 0;
        var endLine = node.end_line ?? (lines.Length > 0 ? lines.Length - 1 : 0);
        var sIdx = Math.Min(Math.Max(0, startLine), lines.Length);
        var eIdx = Math.Max(sIdx, Math.Min(lines.Length, endLine + 1));
        var snippet = string.Join("\n", lines.Skip(sIdx).Take(eIdx - sIdx));

        var filePath = node.file_path ?? string.Empty;
        var ext = Path.GetExtension(filePath).ToLower().TrimStart('.');
        var lang = ext switch
        {
            "ts" or "tsx" => "typescript",
            "js" or "jsx" => "javascript",
            "cs" => "csharp",
            _ => ext
        };

        return $"### File: `{node.file_path}` (Lines {sIdx + 1}-{eIdx})\n```{lang}\n{snippet}\n```";
    }

    private static async Task<string> ExtractFileSnippetAsync(McpRAGNode node, string? workspaceRoot, string? hostWorkspacePath)
    {
        if (string.IsNullOrEmpty(node.file_path)) return "### Invalid node: missing file_path.";
        var relativePath = PathTools.GetRelativePath(node.file_path, hostWorkspacePath);
        var joinedPath = Path.Combine(workspaceRoot ?? "", relativePath);
        var absPath = Path.GetFullPath(joinedPath);
        var absRoot = Path.GetFullPath(workspaceRoot ?? "");

        if (!absPath.StartsWith(absRoot))
            return $"### Access Denied: `{node.file_path}` is outside the workspace root.";

        if (!File.Exists(absPath))
            return $"### File Not Found: `{node.file_path}`";

        try
        {
            var lines = await File.ReadAllLinesAsync(absPath);
            return FormatCodeSnippet(lines, node);
        }
        catch (Exception ex)
        {
            return $"### Error reading `{node.file_path}`: {ex.Message}";
        }
    }

    private async Task<string> FetchCodeSnippetsDirectlyAsync(string nodesJson, string? hostWorkspacePath)
    {
        List<McpRAGNode>? nodes;
        try
        {
            nodes = ParseRagNodes(nodesJson);
        }
        catch (Exception ex)
        {
            return $"Error parsing nodes JSON: {ex.Message}";
        }

        if (nodes == null || nodes.Count == 0) return "No valid code contexts retrieved.";

        var workspaceRoot = ResolveWorkspaceRoot(hostWorkspacePath);
        var output = new List<string>();

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.file_path) || node.start_line == null || node.end_line == null)
                continue;

            output.Add(await ExtractFileSnippetAsync(node, workspaceRoot, hostWorkspacePath));
        }

        return output.Count == 0 ? "No valid code contexts retrieved." : string.Join("\n\n", output);
    }


    private const string TaxonomyLabelKey = "label";
    private const string TaxonomyKeyKey = "key";
    private const string TaxonomyFromLabelKey = "fromLabel";
    private const string TaxonomyRelTypeKey = "relType";
    private const string TaxonomyToLabelKey = "toLabel";

    private static void PopulateTaxonomyProperties(
        Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing, HashSet<(string relationship, string source)> incoming)> nodes,
        List<Dictionary<string, string>> properties)
    {
        foreach (var prop in properties)
        {
            if (prop.TryGetValue(TaxonomyLabelKey, out var label) && prop.TryGetValue(TaxonomyKeyKey, out var key))
            {
                if (!nodes.ContainsKey(label)) nodes[label] = ([], [], []);
                nodes[label].properties.Add(key);
            }
        }
    }

    private static void PopulateTaxonomyTriplets(
        Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing, HashSet<(string relationship, string source)> incoming)> nodes,
        List<Dictionary<string, string>> triplets)
    {
        foreach (var triplet in triplets)
        {
            if (triplet.TryGetValue(TaxonomyFromLabelKey, out var from) && triplet.TryGetValue(TaxonomyRelTypeKey, out var rel) &&
                triplet.TryGetValue(TaxonomyToLabelKey, out var to))
            {
                if (!nodes.ContainsKey(from)) nodes[from] = ([], [], []);
                if (!nodes.ContainsKey(to)) nodes[to] = ([], [], []);

                nodes[from].outgoing.Add((rel, to));
                nodes[to].incoming.Add((rel, from));
            }
        }
    }

    public static object BuildTaxonomy(
        List<Dictionary<string, string>> triplets,
        List<Dictionary<string, string>> properties)
    {
        var nodes = new Dictionary<string, (List<string> properties, HashSet<(string relationship, string target)> outgoing,
            HashSet<(string relationship, string source)> incoming)>(StringComparer.OrdinalIgnoreCase);

        PopulateTaxonomyProperties(nodes, properties);
        PopulateTaxonomyTriplets(nodes, triplets);

        return nodes.OrderBy(k => k.Key).Select(kvp => new
        {
            label = kvp.Key,
            properties = kvp.Value.properties.OrderBy(p => p).ToList(),
            outgoing = kvp.Value.outgoing.OrderBy(x => x.relationship).Select(x => new { x.relationship, x.target }).ToList(),
            incoming = kvp.Value.incoming.OrderBy(x => x.relationship).Select(x => new { x.relationship, x.source }).ToList()
        }).ToList();
    }

}
