using System.Text.Json;
using CodeExplorer.Core.Analysis.ProjectMap;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Protocol;
using CodeExplorer.Cypher.Common;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Project Map (C3 internal module view). A pure view-layer projection over existing ontology nodes/edges
/// (Project, File, Function, Type, Table, Topic, ExternalService, CALLS, IMPLEMENTS, ...). Nothing is persisted.
/// </summary>
public partial class ArchitectureViewEngine
{
    private const int ProjectMapEdgeRowLimit = 200_000;

    private static readonly string[] ProjectMapLiftedKinds = ["CALLS", "USES_TYPE", "IMPLEMENTS", "INHERITS_FROM", "TRIGGERS"];

    private static readonly string[] ProjectMapResourceLabels =
        ["Table", "Topic", "ExternalService", "Database", "CloudService", "DataSet", "Procedure"];

    private sealed record ProjectMapProjectInfo(
        string Id,
        string Name,
        string Path,
        bool IsTest,
        int FileCount,
        bool IsBuildOrchestrator = false,
        string? ExcludedSubpathsJson = null,
        string? Kind = null);

    public async Task<GraphDataDto> GetProjectMapViewAsync(string? projectScope, CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var projects = await LoadProjectMapProjectsAsync(ct);
        var selected = SelectProjectMapProject(projects, projectScope);

        var graph = new GraphDataDto
        {
            Metadata = new Dictionary<string, string>
            {
                ["view"] = "ProjectMap",
                ["level"] = "C3",
                ["graphType"] = "projectmap",
                ["scope"] = projectScope ?? "",
                ["projects"] = JsonSerializer.Serialize(projects
                    .Where(p => !p.IsTest)
                    .OrderByDescending(IsRunnableProjectKind)
                    .ThenByDescending(p => p.FileCount)
                    .Select(p => new { id = p.Id, name = p.Name, path = p.Path, files = p.FileCount }))
            }
        };

        if (selected == null)
        {
            graph.Metadata["error"] = string.IsNullOrWhiteSpace(projectScope)
                ? "No indexed projects found."
                : $"Project '{projectScope}' not found.";
            return graph;
        }

        var files = await LoadProjectMapFilesAsync(selected.Id, ct);
        var subProjects = await LoadProjectMapSubProjectsAsync(selected.Id, ct);
        var excludedSubpaths = ParseJsonStringList(selected.ExcludedSubpathsJson);

        var allRelevantFiles = new HashSet<string>(files.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        foreach (var sp in subProjects)
        {
            foreach (var sf in sp.Files)
            {
                allRelevantFiles.Add(sf.Path);
            }
        }

        var prefix = ComputeProjectMapQueryPrefix(selected.Path, allRelevantFiles);

        var (types, functions, funcIdToTypeName) = await LoadProjectMapTypesAndFunctionsAsync(prefix, allRelevantFiles, warnings, ct);
        var (symbolEdges, typeEdges, libraryResourceLinks) = await LoadProjectMapSymbolAndTypeEdgesAsync(
            prefix, selected.Name, projects, subProjects, allRelevantFiles, funcIdToTypeName, warnings, ct);
        var resourceLinks = await LoadProjectMapResourceLinksAsync(prefix, allRelevantFiles, funcIdToTypeName, warnings, ct);
        resourceLinks.AddRange(libraryResourceLinks);
        var (entryPointsByFile, entryPointNodes) = await LoadProjectMapEntryPointsAsync(prefix, allRelevantFiles, funcIdToTypeName, warnings, ct);

        var input = new ProjectMapInput
        {
            ProjectId = selected.Id,
            ProjectName = selected.Name,
            ProjectPath = selected.Path,
            IsBuildOrchestrator = selected.IsBuildOrchestrator,
            ExcludedSubpaths = excludedSubpaths,
            SubProjects = subProjects,
            Files = files,
            SymbolEdges = symbolEdges,
            ResourceLinks = resourceLinks,
            EntryPointsByFile = entryPointsByFile,
            EntryPointNodes = entryPointNodes,
            Types = types,
            Functions = functions,
            TypeEdges = typeEdges
        };

        var result = ProjectMapBuilder.Build(input);
        ProjectMapGraphMapper.Populate(graph, result);
        if (warnings.Count > 0) graph.Metadata["warnings"] = JsonSerializer.Serialize(warnings);
        return graph;
    }

    private static bool IsRunnableProjectKind(ProjectMapProjectInfo p) =>
        p.Kind is "Service" or "App" or "CliTool" or "Worker";

    private async Task<List<ProjectMapProjectInfo>> LoadProjectMapProjectsAsync(CancellationToken ct)
    {
        const string query =
            "MATCH (p:Project)-[:CONTAINS]->(f:File) RETURN p.id AS id, p.name AS name, p.path AS path, p.is_test AS is_test, p.is_build_orchestrator AS is_build_orch, p.excluded_subpaths AS excluded_subpaths, coalesce(p.kind, p.role, '') AS kind, count(f) AS files";
        var json = await db.ExecuteQueryAsync(query, null, ct);
        using var doc = JsonDocument.Parse(json);

        var list = doc.RootElement.EnumerateArray()
            .Select(e => new ProjectMapProjectInfo(
                e.GetStringProp("id"),
                e.GetStringProp("name"),
                e.GetStringProp("path"),
                ReadBool(e, "is_test"),
                e.GetNullableIntProp("files") ?? 0,
                ReadBool(e, "is_build_orch"),
                e.GetStringProp("excluded_subpaths"),
                e.GetStringProp("kind")))
            .Where(p => !string.IsNullOrEmpty(p.Id))
            .ToList();

        // Also include parent projects that only contain child SubProjects (:Project)-[:CONTAINS]->(:Project)
        const string parentProjectsQuery =
            "MATCH (p:Project)-[:CONTAINS]->(sub:Project) RETURN p.id AS id, p.name AS name, p.path AS path, p.is_test AS is_test, p.is_build_orchestrator AS is_build_orch, p.excluded_subpaths AS excluded_subpaths, coalesce(p.kind, p.role, '') AS kind, count(sub) AS sub_count";
        try
        {
            var pJson = await db.ExecuteQueryAsync(parentProjectsQuery, null, ct);
            using var pDoc = JsonDocument.Parse(pJson);
            var existingIds = list.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var e in pDoc.RootElement.EnumerateArray())
            {
                var id = e.GetStringProp("id");
                if (!string.IsNullOrEmpty(id) && existingIds.Add(id))
                {
                    list.Add(new ProjectMapProjectInfo(
                        id,
                        e.GetStringProp("name"),
                        e.GetStringProp("path"),
                        ReadBool(e, "is_test"),
                        e.GetNullableIntProp("sub_count") ?? 1,
                        ReadBool(e, "is_build_orch"),
                        e.GetStringProp("excluded_subpaths"),
                        e.GetStringProp("kind")));
                }
            }
        }
        catch
        {
            // Ignore if query returns empty
        }

        return list;
    }

    private async Task<List<ProjectMapSubProject>> LoadProjectMapSubProjectsAsync(string parentProjectId, CancellationToken ct)
    {
        const string query =
            "MATCH (p:Project)-[:CONTAINS]->(sub:Project) WHERE p.id = $pid RETURN sub.id AS id, sub.name AS name, sub.path AS path, sub.kind AS kind";
        var result = new List<ProjectMapSubProject>();
        try
        {
            var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["pid"] = parentProjectId }, ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var subId = e.GetStringProp("id");
                if (string.IsNullOrEmpty(subId)) continue;
                var subFiles = await LoadProjectMapFilesAsync(subId, ct);
                result.Add(new ProjectMapSubProject(
                    subId,
                    e.GetStringProp("name"),
                    e.GetStringProp("path"),
                    e.GetStringProp("kind", "Service"),
                    subFiles));
            }
        }
        catch
        {
            // Ignore if no subprojects exist
        }
        return result;
    }

    private static List<string> ParseJsonStringList(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(rawJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static ProjectMapProjectInfo? SelectProjectMapProject(List<ProjectMapProjectInfo> projects, string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return projects.Where(p => !p.IsTest)
                       .OrderByDescending(IsRunnableProjectKind)
                       .ThenByDescending(p => p.FileCount)
                       .FirstOrDefault()
                   ?? projects.OrderByDescending(p => p.FileCount).FirstOrDefault();
        }

        var s = scope.Trim().Replace('\\', '/').TrimEnd('/');
        return projects.FirstOrDefault(p => p.Id.Equals(scope, StringComparison.OrdinalIgnoreCase))
               ?? projects.FirstOrDefault(p => p.Name.Equals(s, StringComparison.OrdinalIgnoreCase))
               ?? projects.FirstOrDefault(p => p.Path.Replace('\\', '/').TrimEnd('/').Equals(s, StringComparison.OrdinalIgnoreCase))
               ?? projects.Where(p => !p.IsTest).FirstOrDefault(p =>
                   p.Path.Replace('\\', '/').TrimEnd('/').Split('/').Last().Equals(s, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<ProjectMapFile>> LoadProjectMapFilesAsync(string projectId, CancellationToken ct)
    {
        const string query =
            "MATCH (p:Project)-[:CONTAINS]->(f:File) WHERE p.id = $pid RETURN f.path AS path, f.is_test AS is_test, f.line_count AS line_count";
        var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object?> { ["pid"] = projectId }, ct);
        using var doc = JsonDocument.Parse(json);

        return doc.RootElement.EnumerateArray()
            .Select(e => new ProjectMapFile(e.GetStringProp("path").Replace('\\', '/'), ReadBool(e, "is_test"), e.GetNullableIntProp("line_count")))
            .Where(f => f.Path.Length > 0)
            .ToList();
    }

    private async Task<(List<ProjectMapTypeNode> Types, List<ProjectMapFunctionNode> Functions, Dictionary<string, string> FuncIdToTypeName)> LoadProjectMapTypesAndFunctionsAsync(
        string prefix,
        HashSet<string> projectFileSet,
        List<string> warnings,
        CancellationToken ct)
    {
        const string methodsQuery =
            "MATCH (t:Type)-[r]->(fn:Function) WHERE r.kind IN ['HAS_METHOD', 'CONTAINS', 'HAS_MEMBER'] " +
            "AND (t.file_path STARTS WITH $prefix OR t.file_path CONTAINS $slashPrefix OR t.path STARTS WITH $prefix OR t.path CONTAINS $slashPrefix) " +
            "RETURN t.id AS tid, t.name AS tname, fn.id AS fnid, fn.name AS fnname";
        var methodRows = await QueryProjectMapRowsAsync(methodsQuery, prefix, "type methods", warnings, ct);
        var typeMethods = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var funcIdToTypeName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mr in methodRows)
        {
            var tid = mr.GetStringProp("tid");
            var tname = StripGenericArity(mr.GetStringProp("tname"));
            var fnid = mr.GetStringProp("fnid");
            var fnname = mr.GetStringProp("fnname");
            if (tid.Length > 0 && fnname.Length > 0)
            {
                if (!typeMethods.TryGetValue(tid, out var list))
                {
                    list = [];
                    typeMethods[tid] = list;
                }
                list.Add(fnname);
            }
            if (fnid.Length > 0 && tname.Length > 0)
            {
                funcIdToTypeName[fnid] = tname;
            }
        }

        const string typesQuery =
            "MATCH (t:Type) WHERE t.file_path STARTS WITH $prefix OR t.file_path CONTAINS $slashPrefix OR t.path STARTS WITH $prefix OR t.path CONTAINS $slashPrefix " +
            "RETURN t.id AS id, t.name AS name, coalesce(t.sub_kind, t.kind, 'class') AS sub_kind, coalesce(t.file_path, t.path) AS file, t.path AS alt_path, t.start_line AS start_line, coalesce(t.line_count, t.loc) AS loc, t.is_test AS is_test";
        var typeRows = await QueryProjectMapRowsAsync(typesQuery, prefix, "project type declarations", warnings, ct);
        var types = new List<ProjectMapTypeNode>(typeRows.Count);
        foreach (var tr in typeRows)
        {
            var tid = tr.GetStringProp("id");
            var tName = StripGenericArity(tr.GetStringProp("name"));
            var tFile = ResolveSymbolFilePath(tr.GetStringProp("file"), tr.GetStringProp("alt_path"), prefix, projectFileSet);
            if (tName.Length == 0 || tFile.Length == 0) continue;
            types.Add(new ProjectMapTypeNode(
                tid,
                tName,
                tr.GetStringProp("sub_kind", "class"),
                tFile,
                tr.GetNullableIntProp("start_line"),
                tr.GetNullableIntProp("loc"),
                ReadBool(tr, "is_test"),
                typeMethods.GetValueOrDefault(tid)));
        }

        const string funcsQuery =
            "MATCH (fn:Function) WHERE fn.file_path STARTS WITH $prefix OR fn.file_path CONTAINS $slashPrefix OR fn.path STARTS WITH $prefix OR fn.path CONTAINS $slashPrefix " +
            "RETURN fn.id AS id, fn.name AS name, coalesce(fn.file_path, fn.path) AS file, fn.path AS alt_path, fn.start_line AS start_line, coalesce(fn.line_count, fn.loc) AS loc, fn.is_test AS is_test, fn.enclosing_type AS enclosing_type";
        var funcRows = await QueryProjectMapRowsAsync(funcsQuery, prefix, "project function declarations", warnings, ct);
        var functions = new List<ProjectMapFunctionNode>(funcRows.Count);
        foreach (var fr in funcRows)
        {
            var fnId = fr.GetStringProp("id");
            var fnName = fr.GetStringProp("name");
            var fnFile = ResolveSymbolFilePath(fr.GetStringProp("file"), fr.GetStringProp("alt_path"), prefix, projectFileSet);
            if (fnName.Length == 0 || fnFile.Length == 0) continue;
            var encType = funcIdToTypeName.GetValueOrDefault(fnId) ?? StripGenericArity(fr.GetStringProp("enclosing_type"));
            if (!string.IsNullOrEmpty(encType) && fnId.Length > 0)
            {
                funcIdToTypeName.TryAdd(fnId, encType);
            }
            functions.Add(new ProjectMapFunctionNode(
                fnId,
                fnName,
                fnFile,
                fr.GetNullableIntProp("start_line"),
                fr.GetNullableIntProp("loc"),
                ReadBool(fr, "is_test"),
                string.IsNullOrEmpty(encType) ? null : encType));
        }

        return (types, functions, funcIdToTypeName);
    }

    private async Task<(List<ProjectMapSymbolEdge> SymbolEdges, List<ProjectMapTypeEdge> TypeEdges, List<ProjectMapResourceLink> LibraryLinks)> LoadProjectMapSymbolAndTypeEdgesAsync(
        string prefix,
        string projectName,
        List<ProjectMapProjectInfo> allProjects,
        List<ProjectMapSubProject> subProjects,
        HashSet<string> projectFileSet,
        Dictionary<string, string> funcIdToTypeName,
        List<string> warnings,
        CancellationToken ct)
    {
        var kinds = string.Join(", ", ProjectMapLiftedKinds.Select(EscapeCypherStringLiteral));
        var query =
            $"MATCH (a)-[r]->(b) WHERE r.kind IN [{kinds}] " +
            "AND (a.file_path STARTS WITH $prefix OR a.file_path CONTAINS $slashPrefix OR a.path STARTS WITH $prefix OR a.path CONTAINS $slashPrefix) " +
            "AND (b.file_path STARTS WITH $prefix OR b.file_path CONTAINS $slashPrefix OR b.path STARTS WITH $prefix OR b.path CONTAINS $slashPrefix) " +
            $"RETURN a.id AS from_id, b.id AS to_id, coalesce(a.file_path, a.path) AS from_file, a.path AS from_alt, coalesce(b.file_path, b.path) AS to_file, b.path AS to_alt, r.kind AS kind, a.name AS from_name, b.name AS to_name, a.start_line AS line LIMIT {ProjectMapEdgeRowLimit}";

        var rows = await QueryProjectMapRowsAsync(query, prefix, "symbol edges", warnings, ct);
        if (rows.Count >= ProjectMapEdgeRowLimit)
        {
            warnings.Add($"Symbol edges truncated at {ProjectMapEdgeRowLimit} rows.");
        }

        var symbolEdges = new List<ProjectMapSymbolEdge>(rows.Count);
        var typeEdges = new List<ProjectMapTypeEdge>(rows.Count);

        foreach (var e in rows)
        {
            var fromFile = ResolveSymbolFilePath(e.GetStringProp("from_file"), e.GetStringProp("from_alt"), prefix, projectFileSet);
            var toFile = ResolveSymbolFilePath(e.GetStringProp("to_file"), e.GetStringProp("to_alt"), prefix, projectFileSet);
            if (fromFile.Length == 0 || toFile.Length == 0) continue;

            var fromId = e.GetStringProp("from_id");
            var toId = e.GetStringProp("to_id");
            var fromSym = funcIdToTypeName.GetValueOrDefault(fromId) ?? e.GetStringProp("from_name");
            var toSym = funcIdToTypeName.GetValueOrDefault(toId) ?? e.GetStringProp("to_name");
            var kind = e.GetStringProp("kind");
            var line = e.GetNullableIntProp("line");

            if (!fromFile.Equals(toFile, StringComparison.OrdinalIgnoreCase))
            {
                symbolEdges.Add(new ProjectMapSymbolEdge(fromFile, toFile, kind, fromSym, toSym, line));
            }
            if (!string.Equals(fromSym, toSym, StringComparison.OrdinalIgnoreCase) || !fromFile.Equals(toFile, StringComparison.OrdinalIgnoreCase))
            {
                typeEdges.Add(new ProjectMapTypeEdge(fromSym, fromFile, toSym, toFile, kind, line));
            }
        }

        // Synthesize USES_TYPE edges from Type->Member data_type within the project
        const string typesQuery =
            "MATCH (t:Type) WHERE t.file_path STARTS WITH $prefix OR t.file_path CONTAINS $slashPrefix OR t.path STARTS WITH $prefix OR t.path CONTAINS $slashPrefix " +
            "RETURN t.name AS name, coalesce(t.file_path, t.path) AS file, t.path AS alt_path";
        var typeRows = await QueryProjectMapRowsAsync(typesQuery, prefix, "project types", warnings, ct);
        var typeToFiles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var tr in typeRows)
        {
            var tName = StripGenericArity(tr.GetStringProp("name"));
            var tFile = ResolveSymbolFilePath(tr.GetStringProp("file"), tr.GetStringProp("alt_path"), prefix, projectFileSet);
            if (tName.Length == 0 || tFile.Length == 0) continue;
            if (!typeToFiles.TryGetValue(tName, out var list))
            {
                list = [];
                typeToFiles[tName] = list;
            }
            if (!list.Contains(tFile, StringComparer.OrdinalIgnoreCase))
            {
                list.Add(tFile);
            }
        }

        if (typeToFiles.Count > 0)
        {
            const string membersQuery =
                "MATCH (t:Type)-[:HAS_MEMBER]->(m:Member) " +
                "WHERE (t.file_path STARTS WITH $prefix OR t.file_path CONTAINS $slashPrefix OR t.path STARTS WITH $prefix OR t.path CONTAINS $slashPrefix) AND m.data_type <> '' " +
                "RETURN t.name AS from_name, coalesce(t.file_path, t.path) AS from_file, t.path AS from_alt, m.data_type AS dtype, m.start_line AS line";
            var memberRows = await QueryProjectMapRowsAsync(membersQuery, prefix, "member type references", warnings, ct);
            var seenMemberTypeEdges = new HashSet<(string FromFile, string ToFile, string FromSym, string ToSym)>();

            foreach (var mr in memberRows)
            {
                var fromFile = ResolveSymbolFilePath(mr.GetStringProp("from_file"), mr.GetStringProp("from_alt"), prefix, projectFileSet);
                var fromName = StripGenericArity(mr.GetStringProp("from_name"));
                var dtype = mr.GetStringProp("dtype");
                var line = mr.GetNullableIntProp("line");
                if (fromFile.Length == 0 || dtype.Length == 0) continue;

                foreach (var token in ExtractTypeIdentifiers(dtype))
                {
                    if (!typeToFiles.TryGetValue(token, out var targetFiles)) continue;
                    foreach (var toFile in targetFiles)
                    {
                        if (seenMemberTypeEdges.Add((fromFile, toFile, fromName, token)))
                        {
                            if (!fromFile.Equals(toFile, StringComparison.OrdinalIgnoreCase))
                            {
                                symbolEdges.Add(new ProjectMapSymbolEdge(fromFile, toFile, "USES_TYPE", fromName, token, line));
                            }
                            if (!string.Equals(fromName, token, StringComparison.OrdinalIgnoreCase))
                            {
                                typeEdges.Add(new ProjectMapTypeEdge(fromName, fromFile, token, toFile, "USES_TYPE", line));
                            }
                        }
                    }
                }
            }
        }

        // Cross-project outbound library calls (Orbit 5 Library nodes) & inbound external callers (for library BFS seeding)
        var libraryLinks = new List<ProjectMapResourceLink>();
        var subProjectNames = new HashSet<string>(subProjects.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        var nonTestProjectByName = allProjects
            .Where(p => !p.IsTest && !string.IsNullOrEmpty(p.Name) && !subProjectNames.Contains(p.Name))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(projectName))
        {
            var projNameLiteral = EscapeCypherStringLiteral(projectName);
            var crossOutboundQuery =
                $"MATCH (a)-[r]->(b) WHERE r.kind IN [{kinds}] " +
                "AND (a.file_path STARTS WITH $prefix OR a.file_path CONTAINS $slashPrefix OR a.path STARTS WITH $prefix OR a.path CONTAINS $slashPrefix) " +
                $"AND b.project_name <> '' AND b.project_name <> {projNameLiteral} " +
                "RETURN a.id AS from_id, a.name AS from_name, coalesce(a.file_path, a.path) AS from_file, a.path AS from_alt, a.start_line AS line, r.kind AS kind, b.project_name AS target_proj LIMIT 50000";

            var crossOutRows = await QueryProjectMapRowsAsync(crossOutboundQuery, prefix, "cross-project outbound calls", warnings, ct);
            foreach (var cr in crossOutRows)
            {
                var targetProjName = cr.GetStringProp("target_proj");
                if (!nonTestProjectByName.TryGetValue(targetProjName, out var targetProj)) continue;
                var fromFile = ResolveSymbolFilePath(cr.GetStringProp("from_file"), cr.GetStringProp("from_alt"), prefix, projectFileSet);
                if (fromFile.Length == 0 || ProjectMapPartitioner.IsIgnoredProjectMapFile(fromFile)) continue;
                var fromId = cr.GetStringProp("from_id");
                var fromSym = funcIdToTypeName.GetValueOrDefault(fromId) ?? cr.GetStringProp("from_name");
                libraryLinks.Add(new ProjectMapResourceLink(
                    fromFile,
                    targetProj.Id,
                    "Library",
                    targetProj.Name,
                    cr.GetStringProp("kind", "CALLS"),
                    fromSym,
                    cr.GetNullableIntProp("line")));
            }

            var crossInboundQuery =
                $"MATCH (a)-[r]->(b) WHERE r.kind IN [{kinds}] " +
                "AND (b.file_path STARTS WITH $prefix OR b.file_path CONTAINS $slashPrefix OR b.path STARTS WITH $prefix OR b.path CONTAINS $slashPrefix) " +
                $"AND a.project_name <> '' AND a.project_name <> {projNameLiteral} AND a.is_test <> true " +
                "RETURN b.id AS to_id, b.name AS to_name, coalesce(b.file_path, b.path) AS to_file, b.path AS to_alt LIMIT 50000";

            var crossInRows = await QueryProjectMapRowsAsync(crossInboundQuery, prefix, "cross-project inbound calls", warnings, ct);
            foreach (var cr in crossInRows)
            {
                var toFile = ResolveSymbolFilePath(cr.GetStringProp("to_file"), cr.GetStringProp("to_alt"), prefix, projectFileSet);
                var toId = cr.GetStringProp("to_id");
                var toSym = funcIdToTypeName.GetValueOrDefault(toId) ?? cr.GetStringProp("to_name");
                if (toSym.Length == 0 && toFile.Length == 0) continue;
                typeEdges.Add(new ProjectMapTypeEdge("", "", toSym, toFile, "CALLS", null, IsExternalCaller: true));
            }
        }

        return (symbolEdges, typeEdges, libraryLinks);
    }

    private async Task<List<ProjectMapResourceLink>> LoadProjectMapResourceLinksAsync(
        string prefix,
        HashSet<string> projectFileSet,
        Dictionary<string, string> funcIdToTypeName,
        List<string> warnings,
        CancellationToken ct)
    {
        var labels = string.Join(", ", ProjectMapResourceLabels.Select(EscapeCypherStringLiteral));
        const string projection =
            "RETURN res.id AS res_id, labels(res)[0] AS res_kind, res.name AS res_name, r.kind AS kind, coalesce(s.file_path, s.path) AS file, s.path AS alt_path, s.id AS sid, s.name AS sym, s.start_line AS line";

        var queries = new (string Query, string Context)[]
        {
            ($"MATCH (res)-[r]->(s) WHERE labels(res)[0] IN [{labels}] AND NOT (labels(s)[0] IN ['Project', 'Database', 'Domain']) AND (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix OR s.path STARTS WITH $prefix OR s.path CONTAINS $slashPrefix) {projection}", "resource links (inbound)"),
            ($"MATCH (s)-[r]->(res) WHERE labels(res)[0] IN [{labels}] AND NOT (labels(s)[0] IN ['Project', 'Database', 'Domain']) AND (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix OR s.path STARTS WITH $prefix OR s.path CONTAINS $slashPrefix) {projection}", "resource links (outbound)"),
            ("MATCH (res:Table)<-[r]-(q:Query)-[a]->(s) WHERE a.kind IN ['ACCESSED_BY', 'TRANSITIVELY_CALLS'] AND (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix OR s.path STARTS WITH $prefix OR s.path CONTAINS $slashPrefix) " + projection, "query tables (accessed by)"),
            ("MATCH (s)-[a]->(q:Query)-[r]->(res:Table) WHERE (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix OR s.path STARTS WITH $prefix OR s.path CONTAINS $slashPrefix) " + projection, "query tables (declared)")
        };

        var links = new List<ProjectMapResourceLink>();
        foreach (var (query, context) in queries)
        {
            var rows = await QueryProjectMapRowsAsync(query, prefix, context, warnings, ct);
            foreach (var e in rows)
            {
                var normFile = ResolveSymbolFilePath(e.GetStringProp("file"), e.GetStringProp("alt_path"), prefix, projectFileSet);
                if (normFile.Length == 0 || ProjectMapPartitioner.IsIgnoredProjectMapFile(normFile)) continue;
                var sid = e.GetStringProp("sid");
                var symName = funcIdToTypeName.GetValueOrDefault(sid) ?? e.GetStringProp("sym");
                links.Add(new ProjectMapResourceLink(
                    normFile,
                    e.GetStringProp("res_id"),
                    e.GetStringProp("res_kind", "Table"),
                    e.GetStringProp("res_name", e.GetStringProp("res_id")),
                    e.GetStringProp("kind"),
                    symName,
                    e.GetNullableIntProp("line")));
            }
        }

        return links;
    }

    private async Task<(Dictionary<string, int> ByFile, List<ProjectMapEntryPointNode> Nodes)> LoadProjectMapEntryPointsAsync(
        string prefix,
        HashSet<string> projectFileSet,
        Dictionary<string, string> funcIdToTypeName,
        List<string> warnings,
        CancellationToken ct)
    {
        const string edgeOutboundQuery =
            "MATCH (e)-[r]->(s) WHERE labels(e)[0] IN ['Endpoint', 'EntryPoint'] AND r.kind IN ['TRIGGERS', 'EXPOSED_BY', 'CALLS', 'IMPLEMENTED_BY'] " +
            "AND (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix OR s.path STARTS WITH $prefix OR s.path CONTAINS $slashPrefix) " +
            "RETURN e.id AS eid, e.name AS ename, labels(e)[0] AS ekind, coalesce(e.protocol, e.entry_type, e.http_method, '') AS sub_kind, coalesce(e.file_path, e.path) AS efile, e.start_line AS eline, s.id AS sid, s.name AS sname, coalesce(s.file_path, s.path) AS file, s.path AS alt_path";

        const string edgeInboundQuery =
            "MATCH (s)-[r]->(e) WHERE labels(e)[0] IN ['Endpoint', 'EntryPoint'] AND r.kind IN ['IMPLEMENTS', 'EXPOSES'] " +
            "AND (s.file_path STARTS WITH $prefix OR s.file_path CONTAINS $slashPrefix OR s.path STARTS WITH $prefix OR s.path CONTAINS $slashPrefix) " +
            "RETURN e.id AS eid, e.name AS ename, labels(e)[0] AS ekind, coalesce(e.protocol, e.entry_type, e.http_method, '') AS sub_kind, coalesce(e.file_path, e.path) AS efile, e.start_line AS eline, s.id AS sid, s.name AS sname, coalesce(s.file_path, s.path) AS file, s.path AS alt_path";

        const string directQuery =
            "MATCH (e) WHERE labels(e)[0] IN ['Endpoint', 'EntryPoint'] " +
            "AND (e.file_path STARTS WITH $prefix OR e.file_path CONTAINS $slashPrefix OR e.path STARTS WITH $prefix OR e.path CONTAINS $slashPrefix) " +
            "RETURN e.id AS eid, e.name AS ename, labels(e)[0] AS ekind, coalesce(e.protocol, e.entry_type, e.http_method, '') AS sub_kind, coalesce(e.file_path, e.path) AS file, e.path AS alt_path, e.start_line AS eline";

        var outRows = await QueryProjectMapRowsAsync(edgeOutboundQuery, prefix, "entry points (outbound edges)", warnings, ct);
        var inRows = await QueryProjectMapRowsAsync(edgeInboundQuery, prefix, "entry points (inbound edges)", warnings, ct);
        var directRows = await QueryProjectMapRowsAsync(directQuery, prefix, "entry points (direct)", warnings, ct);

        var epMap = new Dictionary<string, (string Name, string Kind, string SubKind, string File, int? Line, List<string> Symbols, List<string> Files)>(StringComparer.OrdinalIgnoreCase);
        var fileEpPairs = new List<(string Id, string File)>();

        void RegisterEp(string eid, string ename, string ekind, string subKind, string epFile, int? line, string? targetSym, string? targetFile)
        {
            if (string.IsNullOrEmpty(eid)) return;
            if (!epMap.TryGetValue(eid, out var entry))
            {
                entry = (
                    string.IsNullOrEmpty(ename) ? eid : ename,
                    string.IsNullOrEmpty(ekind) ? "EntryPoint" : ekind,
                    subKind,
                    epFile,
                    line,
                    [],
                    []);
                epMap[eid] = entry;
            }
            if (!string.IsNullOrEmpty(targetSym))
            {
                entry.Symbols.Add(targetSym);
            }
            if (!string.IsNullOrEmpty(targetFile))
            {
                entry.Files.Add(targetFile);
                fileEpPairs.Add((eid, targetFile));
            }
        }

        foreach (var r in outRows.Concat(inRows))
        {
            var eid = r.GetStringProp("eid");
            var sFile = ResolveSymbolFilePath(r.GetStringProp("file"), r.GetStringProp("alt_path"), prefix, projectFileSet);
            var eFile = ResolveSymbolFilePath(r.GetStringProp("efile"), null, prefix, projectFileSet);
            var sid = r.GetStringProp("sid");
            var targetSym = funcIdToTypeName.GetValueOrDefault(sid) ?? StripGenericArity(r.GetStringProp("sname"));
            RegisterEp(
                eid,
                r.GetStringProp("ename"),
                r.GetStringProp("ekind", "Endpoint"),
                r.GetStringProp("sub_kind"),
                sFile.Length > 0 ? sFile : eFile,
                r.GetNullableIntProp("eline"),
                targetSym,
                sFile);
        }

        foreach (var r in directRows)
        {
            var eid = r.GetStringProp("eid");
            var eFile = ResolveSymbolFilePath(r.GetStringProp("file"), r.GetStringProp("alt_path"), prefix, projectFileSet);
            RegisterEp(
                eid,
                r.GetStringProp("ename"),
                r.GetStringProp("ekind", "EntryPoint"),
                r.GetStringProp("sub_kind"),
                eFile,
                r.GetNullableIntProp("eline"),
                null,
                eFile);
        }

        var byFile = fileEpPairs
            .Where(x => x.Id.Length > 0 && x.File.Length > 0)
            .Distinct()
            .GroupBy(x => x.File, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).Distinct().Count(), StringComparer.OrdinalIgnoreCase);

        var nodes = epMap
            .Select(kvp => new ProjectMapEntryPointNode(
                kvp.Key,
                kvp.Value.Name,
                kvp.Value.Kind,
                kvp.Value.File,
                kvp.Value.Line,
                kvp.Value.SubKind,
                kvp.Value.Symbols,
                kvp.Value.Files))
            .ToList();

        return (byFile, nodes);
    }

    private static string ComputeProjectMapQueryPrefix(string? rawProjectPath, HashSet<string> allRelevantFiles)
    {
        var cleanPath = (rawProjectPath ?? "").Replace('\\', '/').Trim('/');
        if (cleanPath == ".") cleanPath = "";

        if (allRelevantFiles.Count == 0)
        {
            return cleanPath.Length == 0 ? "" : cleanPath + "/";
        }

        if (cleanPath.Length > 0)
        {
            var projPrefix = cleanPath + "/";
            if (allRelevantFiles.All(f => f.Replace('\\', '/').TrimStart('/').StartsWith(projPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                return projPrefix;
            }
        }

        // Compute longest common directory prefix among all relevant project files
        string? commonDir = null;
        foreach (var file in allRelevantFiles)
        {
            var norm = file.Replace('\\', '/').TrimStart('/');
            var lastSlash = norm.LastIndexOf('/');
            var dir = lastSlash > 0 ? norm[..(lastSlash + 1)] : "";
            if (commonDir == null)
            {
                commonDir = dir;
            }
            else
            {
                var minLen = Math.Min(commonDir.Length, dir.Length);
                var matchLen = 0;
                var lastMatchSlash = -1;
                for (var i = 0; i < minLen; i++)
                {
                    if (char.ToLowerInvariant(commonDir[i]) != char.ToLowerInvariant(dir[i])) break;
                    if (commonDir[i] == '/') lastMatchSlash = i;
                    matchLen = i + 1;
                }
                commonDir = lastMatchSlash >= 0 ? commonDir[..(lastMatchSlash + 1)] : "";
            }
            if (commonDir.Length == 0) break;
        }

        return commonDir ?? "";
    }

    private static string ResolveSymbolFilePath(
        string? primaryPath,
        string? altPath,
        string prefix,
        HashSet<string> projectFileSet)
    {
        var normPrimary = NormalizeToProjectPath(primaryPath, prefix);
        var normAlt = NormalizeToProjectPath(altPath, prefix);

        if (projectFileSet.Count > 0)
        {
            if (normPrimary.Length > 0 && projectFileSet.Contains(normPrimary)) return normPrimary;
            if (normAlt.Length > 0 && projectFileSet.Contains(normAlt)) return normAlt;

            // Match suffix when primaryPath or altPath includes an outer workspace root prefix
            if (normPrimary.Length > 0)
            {
                var suffixMatch = projectFileSet.FirstOrDefault(f => normPrimary.EndsWith("/" + f, StringComparison.OrdinalIgnoreCase));
                if (suffixMatch != null) return suffixMatch;
            }
            if (normAlt.Length > 0)
            {
                var suffixMatch = projectFileSet.FirstOrDefault(f => normAlt.EndsWith("/" + f, StringComparison.OrdinalIgnoreCase));
                if (suffixMatch != null) return suffixMatch;
            }

            if (prefix.Length > 0)
            {
                if (normPrimary.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return normPrimary;
                if (normAlt.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return normAlt;
            }
            return "";
        }

        if (normPrimary.Length > 0 && normPrimary.Contains('/')) return normPrimary;
        if (normAlt.Length > 0 && normAlt.Contains('/')) return normAlt;
        return normPrimary.Length > 0 ? normPrimary : normAlt;
    }

    private async Task<List<JsonElement>> QueryProjectMapRowsAsync(
        string query, string prefix, string context, List<string> warnings, CancellationToken ct)
    {
        try
        {
            var slashPrefix = prefix.Length == 0 ? "" : "/" + prefix.TrimStart('/');
            var json = await db.ExecuteQueryAsync(query, new Dictionary<string, object?>
            {
                ["prefix"] = prefix,
                ["slashPrefix"] = slashPrefix
            }, ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "[ProjectMap] {Context} query failed: {Message}", context, ex.Message);
            warnings.Add($"{context}: {ex.Message}");
            return [];
        }
    }

    private static string NormalizeToProjectPath(string? rawPath, string prefix)
    {
        if (string.IsNullOrWhiteSpace(rawPath)) return "";
        var norm = rawPath.Replace('\\', '/').TrimStart('/');
        if (prefix.Length == 0 || norm.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return norm;
        }
        var slashPrefix = "/" + prefix;
        var idx = norm.IndexOf(slashPrefix, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return norm[(idx + 1)..];
        }
        return norm;
    }

    private static string StripGenericArity(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return "";
        var angleIdx = typeName.IndexOf('<');
        if (angleIdx > 0) typeName = typeName[..angleIdx];
        var tickIdx = typeName.IndexOf('`');
        if (tickIdx > 0) typeName = typeName[..tickIdx];
        return typeName.Trim();
    }

    private static IEnumerable<string> ExtractTypeIdentifiers(string rawTypeExpr)
    {
        var start = -1;
        for (var i = 0; i <= rawTypeExpr.Length; i++)
        {
            var ch = i < rawTypeExpr.Length ? rawTypeExpr[i] : '\0';
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                if (start < 0) start = i;
            }
            else if (start >= 0)
            {
                var token = rawTypeExpr[start..i];
                start = -1;
                if (token.Length >= 2 && char.IsUpper(token[0]))
                {
                    yield return token;
                }
            }
        }
    }

    private static bool ReadBool(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var v)) return false;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.Number => v.TryGetInt32(out var n) && n != 0,
            JsonValueKind.String => v.GetString() is "true" or "True" or "1",
            _ => false
        };
    }
}
