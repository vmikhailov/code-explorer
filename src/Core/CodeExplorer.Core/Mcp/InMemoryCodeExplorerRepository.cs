using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Mcp;

public class InMemoryCodeExplorerRepository(IDatabaseClient dbClient) : ICodeExplorerRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<string, InMemoryGraph> _graphCache = new();

    private class InMemoryGraph
    {
        public Dictionary<string, Node> Nodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<Relationship>> Outgoing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<Relationship>> Incoming { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static string CleanPathForComparison(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        var clean = path.Replace('\\', '/');
        var driveMatch = System.Text.RegularExpressions.Regex.Match(clean, @"^[A-Za-z]:");
        if (driveMatch.Success)
        {
            clean = clean.Substring(driveMatch.Length);
        }
        if (clean.StartsWith("/host", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(5);
        }
        return clean.Trim('/').ToLowerInvariant();
    }

    private async Task<string?> GetWorkspaceIdAsync(string? workspacePath)
    {
        if (string.IsNullOrEmpty(workspacePath)) return null;
        var sqliteClient = (SqliteGraphClient)dbClient;
        var workspaces = await sqliteClient.GetAllWorkspacesAsync();

        // 1. Try exact match
        var normalized = PathTools.NormalizeToHostPath(workspacePath).Replace('\\', '/').TrimEnd('/');
        var match = workspaces.FirstOrDefault(w => string.Equals(w.Path.Replace('\\', '/').TrimEnd('/'), normalized, StringComparison.OrdinalIgnoreCase));
        if (match.Id != null) return match.Id;

        // Fallback: If only one workspace exists
        if (workspaces.Count == 1) return workspaces[0].Id;

        // Clean match
        var inputCleaned = CleanPathForComparison(workspacePath);
        foreach (var ws in workspaces)
        {
            if (CleanPathForComparison(ws.Path) == inputCleaned)
            {
                return ws.Id;
            }
        }

        throw new InvalidOperationException($"Workspace at path '{workspacePath}' is not indexed yet. Please run ingest/index first.");
    }

    private async Task<InMemoryGraph> GetGraphAsync(string? workspacePath)
    {
        var wsId = await GetWorkspaceIdAsync(workspacePath);
        if (wsId == null)
        {
            throw new InvalidOperationException($"Workspace at path '{workspacePath}' is not indexed yet.");
        }

        lock (_lock)
        {
            if (_graphCache.TryGetValue(wsId, out var cached)) return cached;
        }

        var sqliteClient = (SqliteGraphClient)dbClient;
        int.TryParse(wsId, out var wsIdInt);

        var dbNodes = await sqliteClient.FetchAllWorkspaceNodesAsync(wsIdInt);
        var dbRels = await sqliteClient.FetchAllWorkspaceRelationshipsAsync(wsIdInt);

        var graph = new InMemoryGraph();
        foreach (var node in dbNodes)
        {
            graph.Nodes[node.Id] = node;
            graph.Outgoing[node.Id] = new List<Relationship>();
            graph.Incoming[node.Id] = new List<Relationship>();
        }

        foreach (var rel in dbRels)
        {
            if (!graph.Nodes.ContainsKey(rel.From))
            {
                graph.Nodes[rel.From] = new Node(rel.From, "Entity", new Dictionary<string, object>());
                graph.Outgoing[rel.From] = new List<Relationship>();
                graph.Incoming[rel.From] = new List<Relationship>();
            }
            if (!graph.Nodes.ContainsKey(rel.To))
            {
                graph.Nodes[rel.To] = new Node(rel.To, "Entity", new Dictionary<string, object>());
                graph.Outgoing[rel.To] = new List<Relationship>();
                graph.Incoming[rel.To] = new List<Relationship>();
            }

            graph.Outgoing[rel.From].Add(rel);
            graph.Incoming[rel.To].Add(rel);
        }

        lock (_lock)
        {
            _graphCache[wsId] = graph;
        }
        return graph;
    }

    private static string FormatResult(object data)
    {
        return JsonSerializer.Serialize(new { results = data }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> GetArchitectureMapAsync(string? projectName, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var wsId = await GetWorkspaceIdAsync(workspacePath);

        if (!string.IsNullOrEmpty(projectName))
        {
            // Find project
            var projNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.Project && 
                n.Properties.TryGetValue("name", out var nameVal) && string.Equals(nameVal?.ToString(), projectName, StringComparison.OrdinalIgnoreCase));

            if (projNode == null) return FormatResult(Array.Empty<object>());

            var projId = projNode.Id;

            // Find folders located under project (or folders project LOCATED_IN)
            var locatedInFolderId = graph.Outgoing.TryGetValue(projId, out var oRels) 
                ? oRels.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.LocatedIn)?.To 
                : null;

            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<Node>();

            if (locatedInFolderId != null)
            {
                // BFS to get all nested folders and files
                var queue = new Queue<string>();
                queue.Enqueue(locatedInFolderId);
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { locatedInFolderId };

                while (queue.Count > 0)
                {
                    var currId = queue.Dequeue();
                    if (graph.Nodes.TryGetValue(currId, out var currNode))
                    {
                        if (currNode.Kind == OntologyConstants.NodeLabels.Folder)
                        {
                            folders.Add(currNode.Properties.TryGetValue("name", out var fn) ? fn.ToString() ?? "" : "");
                        }
                    }

                    if (graph.Outgoing.TryGetValue(currId, out var outs))
                    {
                        foreach (var rel in outs)
                        {
                            if (rel.Kind == OntologyConstants.Relationships.Contains)
                            {
                                if (!visited.Contains(rel.To))
                                {
                                    visited.Add(rel.To);
                                    if (graph.Nodes.TryGetValue(rel.To, out var targetNode))
                                    {
                                        if (targetNode.Kind == OntologyConstants.NodeLabels.Folder)
                                        {
                                            queue.Enqueue(rel.To);
                                        }
                                        else if (targetNode.Kind == OntologyConstants.NodeLabels.File)
                                        {
                                            files.Add(targetNode);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Project databases (files with USES_DB relationship to databases)
            var projectDbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                if (graph.Outgoing.TryGetValue(file.Id, out var fileOuts))
                {
                    foreach (var rel in fileOuts)
                    {
                        if (rel.Kind == OntologyConstants.Relationships.UsesDb && graph.Nodes.TryGetValue(rel.To, out var dbNode))
                        {
                            projectDbs.Add(dbNode.Properties.TryGetValue("name", out var dbn) ? dbn.ToString() ?? "" : "");
                        }
                    }
                }
            }

            // Project egress (external services whose file_path matches file path)
            var projectEgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var fPath = file.Properties.TryGetValue("path", out var p) ? p.ToString() ?? "" : "";
                var extServices = graph.Nodes.Values.Where(n => n.Kind == OntologyConstants.NodeLabels.ExternalService &&
                    n.Properties.TryGetValue("file_path", out var fp) && string.Equals(fp?.ToString(), fPath, StringComparison.OrdinalIgnoreCase));
                foreach (var es in extServices)
                {
                    projectEgress.Add(es.Properties.TryGetValue("name", out var esn) ? esn.ToString() ?? "" : "");
                }
            }

            // Project ingress (endpoints or entrypoints whose path matches file path)
            var projectIngress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                var fPath = file.Properties.TryGetValue("path", out var p) ? p.ToString() ?? "" : "";
                var ingressNodes = graph.Nodes.Values.Where(n => (n.Kind == OntologyConstants.NodeLabels.Endpoint || n.Kind == OntologyConstants.NodeLabels.EntryPoint) &&
                    n.Properties.TryGetValue("path", out var ip) && string.Equals(ip?.ToString(), fPath, StringComparison.OrdinalIgnoreCase));
                foreach (var ing in ingressNodes)
                {
                    projectIngress.Add(ing.Properties.TryGetValue("name", out var ingn) ? ingn.ToString() ?? "" : "");
                }
            }

            // Dependencies (projects it depends on)
            var projectDeps = new List<string>();
            if (graph.Outgoing.TryGetValue(projId, out var pOuts))
            {
                foreach (var rel in pOuts)
                {
                    if (rel.Kind == OntologyConstants.Relationships.DependsOn && graph.Nodes.TryGetValue(rel.To, out var depNode) && depNode.Kind == OntologyConstants.NodeLabels.Project)
                    {
                        projectDeps.Add(depNode.Properties.TryGetValue("name", out var depn) ? depn.ToString() ?? "" : "");
                    }
                }
            }

            var result = new[]
            {
                new
                {
                    project = projNode.Properties.TryGetValue("name", out var pn) ? pn.ToString() ?? "" : "",
                    language = projNode.Properties.TryGetValue("project_type", out var pt) ? pt.ToString() : "",
                    folders = folders.Where(f => !string.IsNullOrEmpty(f)).ToList(),
                    dependencies = projectDeps,
                    databases = projectDbs.ToList(),
                    ingress = projectIngress.ToList(),
                    egress = projectEgress.ToList()
                }
            };
            return FormatResult(result);
        }
        else
        {
            // Return top-level workspace structure
            var workspaceNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.Workspace);
            if (workspaceNode == null) return FormatResult(new { });

            // Collect workspace folders (children of FilesStructure)
            var filesStructure = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.FilesStructure);
            var workspaceFolders = new List<string>();
            if (filesStructure != null && graph.Outgoing.TryGetValue(filesStructure.Id, out var fsOuts))
            {
                foreach (var rel in fsOuts)
                {
                    if (rel.Kind == OntologyConstants.Relationships.Contains && graph.Nodes.TryGetValue(rel.To, out var fNode) && fNode.Kind == OntologyConstants.NodeLabels.Folder)
                    {
                        workspaceFolders.Add(fNode.Properties.TryGetValue("name", out var fn) ? fn.ToString() ?? "" : "");
                    }
                }
            }

            // Collect all projects
            var projects = new List<object>();
            var projectNodes = graph.Nodes.Values.Where(n => n.Kind == OntologyConstants.NodeLabels.Project);
            foreach (var pNode in projectNodes)
            {
                var locatedInFolderId = graph.Outgoing.TryGetValue(pNode.Id, out var oRels) 
                    ? oRels.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.LocatedIn)?.To 
                    : null;

                var files = new List<Node>();
                if (locatedInFolderId != null)
                {
                    var queue = new Queue<string>();
                    queue.Enqueue(locatedInFolderId);
                    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { locatedInFolderId };

                    while (queue.Count > 0)
                    {
                        var currId = queue.Dequeue();
                        if (graph.Outgoing.TryGetValue(currId, out var outs))
                        {
                            foreach (var rel in outs)
                            {
                                if (rel.Kind == OntologyConstants.Relationships.Contains && !visited.Contains(rel.To))
                                {
                                    visited.Add(rel.To);
                                    if (graph.Nodes.TryGetValue(rel.To, out var targetNode))
                                    {
                                        if (targetNode.Kind == OntologyConstants.NodeLabels.Folder) queue.Enqueue(rel.To);
                                        else if (targetNode.Kind == OntologyConstants.NodeLabels.File) files.Add(targetNode);
                                    }
                                }
                            }
                        }
                    }
                }

                var projectDbs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    if (graph.Outgoing.TryGetValue(file.Id, out var fileOuts))
                    {
                        foreach (var rel in fileOuts)
                        {
                            if (rel.Kind == OntologyConstants.Relationships.UsesDb && graph.Nodes.TryGetValue(rel.To, out var dbNode))
                            {
                                projectDbs.Add(dbNode.Properties.TryGetValue("name", out var dbn) ? dbn.ToString() ?? "" : "");
                            }
                        }
                    }
                }

                var projectEgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    var fPath = file.Properties.TryGetValue("path", out var p) ? p.ToString() ?? "" : "";
                    var extServices = graph.Nodes.Values.Where(n => n.Kind == OntologyConstants.NodeLabels.ExternalService &&
                        n.Properties.TryGetValue("file_path", out var fp) && string.Equals(fp?.ToString(), fPath, StringComparison.OrdinalIgnoreCase));
                    foreach (var es in extServices)
                    {
                        projectEgress.Add(es.Properties.TryGetValue("name", out var esn) ? esn.ToString() ?? "" : "");
                    }
                }

                var projectIngress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in files)
                {
                    var fPath = file.Properties.TryGetValue("path", out var p) ? p.ToString() ?? "" : "";
                    var ingressNodes = graph.Nodes.Values.Where(n => (n.Kind == OntologyConstants.NodeLabels.Endpoint || n.Kind == OntologyConstants.NodeLabels.EntryPoint) &&
                        n.Properties.TryGetValue("path", out var ip) && string.Equals(ip?.ToString(), fPath, StringComparison.OrdinalIgnoreCase));
                    foreach (var ing in ingressNodes)
                    {
                        projectIngress.Add(ing.Properties.TryGetValue("name", out var ingn) ? ingn.ToString() ?? "" : "");
                    }
                }

                var projectDeps = new List<string>();
                if (graph.Outgoing.TryGetValue(pNode.Id, out var pOuts))
                {
                    foreach (var rel in pOuts)
                    {
                        if (rel.Kind == OntologyConstants.Relationships.DependsOn && graph.Nodes.TryGetValue(rel.To, out var depNode) && depNode.Kind == OntologyConstants.NodeLabels.Project)
                        {
                            projectDeps.Add(depNode.Properties.TryGetValue("name", out var depn) ? depn.ToString() ?? "" : "");
                        }
                    }
                }

                projects.Add(new
                {
                    name = pNode.Properties.TryGetValue("name", out var pn) ? pn.ToString() ?? "" : "",
                    language = pNode.Properties.TryGetValue("project_type", out var pt) ? pt.ToString() : "",
                    dependencies = projectDeps,
                    databases = projectDbs.ToList(),
                    ingress = projectIngress.ToList(),
                    egress = projectEgress.ToList()
                });
            }

            var wsMap = new
            {
                workspace = workspaceNode.Properties.TryGetValue("name", out var wn) ? wn.ToString() ?? "" : "",
                path = workspaceNode.Properties.TryGetValue("path", out var wp) ? wp.ToString() ?? "" : "",
                workspaceFolders,
                projects
            };
            return JsonSerializer.Serialize(new { results = new[] { wsMap } }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public async Task<string> GetProjectDependenciesAsync(string? projectFilter, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var wsId = await GetWorkspaceIdAsync(workspacePath);

        if (!string.IsNullOrEmpty(projectFilter))
        {
            var pNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.Project &&
                n.Properties.TryGetValue("name", out var nameVal) && string.Equals(nameVal?.ToString(), projectFilter, StringComparison.OrdinalIgnoreCase));

            if (pNode == null) return FormatResult(Array.Empty<object>());

            var outgoing = new List<string>();
            if (graph.Outgoing.TryGetValue(pNode.Id, out var outs))
            {
                foreach (var rel in outs)
                {
                    if (rel.Kind == OntologyConstants.Relationships.DependsOn && graph.Nodes.TryGetValue(rel.To, out var target))
                    {
                        outgoing.Add(target.Properties.TryGetValue("name", out var tn) ? tn.ToString() ?? "" : "");
                    }
                }
            }

            var incoming = new List<string>();
            if (graph.Incoming.TryGetValue(pNode.Id, out var ins))
            {
                foreach (var rel in ins)
                {
                    if (rel.Kind == OntologyConstants.Relationships.DependsOn && graph.Nodes.TryGetValue(rel.From, out var src))
                    {
                        incoming.Add(src.Properties.TryGetValue("name", out var sn) ? sn.ToString() ?? "" : "");
                    }
                }
            }

            var row = new
            {
                project = pNode.Properties.TryGetValue("name", out var pn) ? pn.ToString() ?? "" : "",
                outgoingDependencies = outgoing,
                incomingDependencies = incoming
            };
            return FormatResult(new[] { row });
        }
        else
        {
            var results = new List<object>();
            var projects = graph.Nodes.Values.Where(n => n.Kind == OntologyConstants.NodeLabels.Project);
            foreach (var p in projects)
            {
                if (graph.Outgoing.TryGetValue(p.Id, out var outs))
                {
                    foreach (var rel in outs)
                    {
                        if (rel.Kind == OntologyConstants.Relationships.DependsOn && graph.Nodes.TryGetValue(rel.To, out var dep))
                        {
                            results.Add(new
                            {
                                project = p.Properties.TryGetValue("name", out var pn) ? pn.ToString() ?? "" : "",
                                dependency = dep.Properties.TryGetValue("name", out var dn) ? dn.ToString() ?? "" : "",
                                dependencyType = dep.Kind
                            });
                        }
                    }
                }
            }
            return FormatResult(results);
        }
    }

    public async Task<string> GetFileOutlineAsync(string filePath, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var fileNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.File &&
            (n.Properties.TryGetValue("path", out var p) && string.Equals(p?.ToString(), filePath, StringComparison.OrdinalIgnoreCase) ||
             n.Properties.TryGetValue("file_path", out var fp) && string.Equals(fp?.ToString(), filePath, StringComparison.OrdinalIgnoreCase)));

        if (fileNode == null)
        {
            // Fallback: search by ends with
            fileNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.File &&
                n.Properties.TryGetValue("path", out var pathVal) && pathVal != null && pathVal.ToString()!.EndsWith(filePath, StringComparison.OrdinalIgnoreCase));
        }

        if (fileNode == null) return FormatResult(Array.Empty<object>());

        // Find child declarations
        var children = new List<Node>();
        var queue = new Queue<string>();
        queue.Enqueue(fileNode.Id);
        var visited = new HashSet<string> { fileNode.Id };

        while (queue.Count > 0)
        {
            var curr = queue.Dequeue();
            if (graph.Incoming.TryGetValue(curr, out var ins))
            {
                foreach (var rel in ins)
                {
                    if ((rel.Kind == OntologyConstants.Relationships.DeclaredIn || rel.Kind == OntologyConstants.Relationships.Contains) && !visited.Contains(rel.From))
                    {
                        visited.Add(rel.From);
                        if (graph.Nodes.TryGetValue(rel.From, out var childNode))
                        {
                            if (childNode.Kind == OntologyConstants.NodeLabels.Type ||
                                childNode.Kind == OntologyConstants.NodeLabels.Function ||
                                childNode.Kind == OntologyConstants.NodeLabels.Member ||
                                childNode.Kind == OntologyConstants.NodeLabels.Query)
                            {
                                children.Add(childNode);
                            }
                            queue.Enqueue(rel.From);
                        }
                    }
                }
            }
        }

        var results = children.Select(child =>
        {
            var typeStr = child.Kind;
            if (child.Kind == OntologyConstants.NodeLabels.Type)
            {
                var kindVal = child.Properties.TryGetValue("kind", out var kVal) ? kVal?.ToString() ?? "" : "";
                typeStr = kindVal switch
                {
                    "class" => "Class",
                    "interface" => "Interface",
                    _ => kindVal
                };
            }

            int.TryParse(child.Properties.TryGetValue("start_line", out var sl) ? sl?.ToString() : "0", out var startLine);
            int.TryParse(child.Properties.TryGetValue("end_line", out var el) ? el?.ToString() : "0", out var endLine);

            return new
            {
                name = child.Properties.TryGetValue("name", out var nVal) ? nVal?.ToString() ?? "" : "",
                type = typeStr,
                startLine,
                endLine,
                symbol = child.Properties.TryGetValue("symbol", out var sVal) ? sVal?.ToString() ?? "" : ""
            };
        }).OrderBy(x => x.startLine).ToList();

        return FormatResult(results);
    }

    public async Task<string> FindSymbolAsync(string name, string? symbolType, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var targets = graph.Nodes.Values.Where(n =>
            (n.Kind == OntologyConstants.NodeLabels.Function || n.Kind == OntologyConstants.NodeLabels.Type) &&
            n.Properties.TryGetValue("name", out var nVal) && nVal != null && nVal.ToString()!.Contains(name, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(symbolType))
        {
            targets = targets.Where(n => string.Equals(n.Kind, symbolType, StringComparison.OrdinalIgnoreCase) ||
                (symbolType == "Class" && n.Kind == OntologyConstants.NodeLabels.Type && n.Properties.TryGetValue("kind", out var kv) && kv?.ToString() == "class") ||
                (symbolType == "Interface" && n.Kind == OntologyConstants.NodeLabels.Type && n.Properties.TryGetValue("kind", out var iv) && iv?.ToString() == "interface"));
        }

        var results = new List<object>();
        foreach (var n in targets.Take(10))
        {
            var typeStr = n.Kind;
            if (n.Kind == OntologyConstants.NodeLabels.Type)
            {
                var kindVal = n.Properties.TryGetValue("kind", out var kVal) ? kVal?.ToString() ?? "" : "";
                typeStr = kindVal switch
                {
                    "class" => "Class",
                    "interface" => "Interface",
                    _ => kindVal
                };
            }

            // Find containing File
            string? filePath = null;
            if (graph.Outgoing.TryGetValue(n.Id, out var outs))
            {
                var fileRel = outs.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.DeclaredIn);
                if (fileRel != null && graph.Nodes.TryGetValue(fileRel.To, out var fileNode))
                {
                    var fileRelPath = fileNode.Properties.TryGetValue("path", out var pVal) ? pVal?.ToString() : "";
                    var workspaceNode = graph.Nodes.Values.FirstOrDefault(wn => wn.Kind == OntologyConstants.NodeLabels.Workspace);
                    var wsPath = workspaceNode?.Properties.TryGetValue("path", out var wVal) == true ? wVal?.ToString() ?? "" : "";
                    filePath = string.IsNullOrEmpty(wsPath) ? fileRelPath : $"{wsPath}/{fileRelPath}";
                }
            }

            if (string.IsNullOrEmpty(filePath) && n.Properties.TryGetValue("file_path", out var fpVal))
            {
                filePath = fpVal?.ToString();
            }

            results.Add(new
            {
                type = typeStr,
                name = n.Properties.TryGetValue("name", out var nameObj) ? nameObj?.ToString() ?? "" : "",
                fullName = n.Properties.TryGetValue("symbol", out var symObj) ? symObj?.ToString() ?? "" : "",
                filePath = filePath ?? ""
            });
        }

        return FormatResult(results);
    }

    public async Task<string> GetCallChainAsync(string startFunction, string endFunction, int maxDepth, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var startNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.Function &&
            n.Properties.TryGetValue("symbol", out var s) && string.Equals(s?.ToString(), startFunction, StringComparison.OrdinalIgnoreCase));

        var endNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.Function &&
            n.Properties.TryGetValue("symbol", out var s) && string.Equals(s?.ToString(), endFunction, StringComparison.OrdinalIgnoreCase));

        if (startNode == null || endNode == null) return FormatResult(Array.Empty<object>());

        var depth = Math.Max(1, Math.Min(10, maxDepth));
        var paths = new List<List<string>>();

        // BFS pathfinder
        var queue = new Queue<(string CurrentId, List<string> Path)>();
        queue.Enqueue((startNode.Id, new List<string> { startNode.Id }));

        while (queue.Count > 0)
        {
            var (curr, currentPath) = queue.Dequeue();
            if (currentPath.Count > depth + 1) continue;

            if (curr == endNode.Id)
            {
                paths.Add(currentPath);
                continue;
            }

            if (graph.Outgoing.TryGetValue(curr, out var outs))
            {
                foreach (var rel in outs)
                {
                    if (rel.Kind == OntologyConstants.Relationships.Calls && !currentPath.Contains(rel.To))
                    {
                        var newPath = new List<string>(currentPath) { rel.To };
                        queue.Enqueue((rel.To, newPath));
                    }
                }
            }
        }

        var results = paths.Select(p =>
        {
            var chainNodes = p.Select(id =>
            {
                var node = graph.Nodes[id];
                return new
                {
                    id = node.Id,
                    labels = new[] { node.Kind },
                    props = node.Properties
                };
            }).ToList();

            return new { chain = chainNodes };
        }).ToList();

        return FormatResult(results);
    }

    public async Task<string> ResolveCallTargetAsync(string interfaceName, string methodName, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var interfaceNodes = graph.Nodes.Values.Where(n => n.Kind == OntologyConstants.NodeLabels.Type &&
            n.Properties.TryGetValue("kind", out var k) && k?.ToString() == "interface" &&
            n.Properties.TryGetValue("name", out var nameVal) && string.Equals(nameVal?.ToString(), interfaceName, StringComparison.OrdinalIgnoreCase));

        var results = new List<object>();

        foreach (var iNode in interfaceNodes)
        {
            // Find implementations (incoming IMPLEMENTS relationship)
            if (graph.Incoming.TryGetValue(iNode.Id, out var ins))
            {
                foreach (var rel in ins)
                {
                    if (rel.Kind == OntologyConstants.Relationships.Implements && graph.Nodes.TryGetValue(rel.From, out var implNode))
                    {
                        // Look for HAS_METHOD method
                        if (graph.Outgoing.TryGetValue(implNode.Id, out var implOuts))
                        {
                            foreach (var methodRel in implOuts)
                            {
                                if (methodRel.Kind == OntologyConstants.Relationships.HasMethod && graph.Nodes.TryGetValue(methodRel.To, out var fNode))
                                {
                                    if (fNode.Properties.TryGetValue("name", out var mn) && string.Equals(mn?.ToString(), methodName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        int.TryParse(fNode.Properties.TryGetValue("start_line", out var sl) ? sl?.ToString() : "0", out var startLine);
                                        results.Add(new
                                        {
                                            className = implNode.Properties.TryGetValue("name", out var cn) ? cn?.ToString() ?? "" : "",
                                            methodName = fNode.Properties.TryGetValue("name", out var fn) ? fn?.ToString() ?? "" : "",
                                            methodSymbol = fNode.Properties.TryGetValue("symbol", out var fs) ? fs?.ToString() ?? "" : "",
                                            filePath = fNode.Properties.TryGetValue("file_path", out var fp) ? fp?.ToString() ?? "" : "",
                                            startLine
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return FormatResult(results);
    }

    public async Task<string> AnalyzeCodeImpactAsync(string symbolName, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var targetNodes = graph.Nodes.Values.Where(n =>
            (n.Kind == OntologyConstants.NodeLabels.Type || n.Kind == OntologyConstants.NodeLabels.Function) &&
            (string.Equals(n.Properties.TryGetValue("symbol", out var s) ? s?.ToString() : "", symbolName, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(n.Properties.TryGetValue("name", out var nameVal) ? nameVal?.ToString() : "", symbolName, StringComparison.OrdinalIgnoreCase)));

        var results = new List<object>();

        foreach (var target in targetNodes)
        {
            if (graph.Incoming.TryGetValue(target.Id, out var ins))
            {
                foreach (var rel in ins)
                {
                    if (rel.Kind == OntologyConstants.Relationships.UsesType || rel.Kind == OntologyConstants.Relationships.Calls)
                    {
                        if (graph.Nodes.TryGetValue(rel.From, out var dependent))
                        {
                            var typeStr = dependent.Kind;
                            if (dependent.Kind == OntologyConstants.NodeLabels.Type)
                            {
                                var kindVal = dependent.Properties.TryGetValue("kind", out var kVal) ? kVal?.ToString() ?? "" : "";
                                typeStr = kindVal switch
                                {
                                    "class" => "Class",
                                    "interface" => "Interface",
                                    _ => kindVal
                                };
                            }

                            // Optional containing file
                            string? filePath = null;
                            if (graph.Outgoing.TryGetValue(dependent.Id, out var depOuts))
                            {
                                var fileRel = depOuts.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.DeclaredIn);
                                if (fileRel != null && graph.Nodes.TryGetValue(fileRel.To, out var fileNode))
                                {
                                    var fileRelPath = fileNode.Properties.TryGetValue("path", out var pVal) ? pVal?.ToString() : "";
                                    var workspaceNode = graph.Nodes.Values.FirstOrDefault(wn => wn.Kind == OntologyConstants.NodeLabels.Workspace);
                                    var wsPath = workspaceNode?.Properties.TryGetValue("path", out var wVal) == true ? wVal?.ToString() ?? "" : "";
                                    filePath = string.IsNullOrEmpty(wsPath) ? fileRelPath : $"{wsPath}/{fileRelPath}";
                                }
                            }

                            results.Add(new
                            {
                                dependentType = typeStr,
                                dependentName = dependent.Properties.TryGetValue("name", out var dnObj) ? dnObj?.ToString() ?? "" : "",
                                dependentSymbol = dependent.Properties.TryGetValue("symbol", out var dsObj) ? dsObj?.ToString() ?? "" : "",
                                filePath = filePath ?? ""
                            });
                        }
                    }
                }
            }
        }

        return FormatResult(results);
    }

    public async Task<string> InspectDataLineageAsync(string tableName, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var tableNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.Table &&
            string.Equals(n.Properties.TryGetValue("name", out var tn) ? tn?.ToString() : "", tableName, StringComparison.OrdinalIgnoreCase));

        if (tableNode == null) return FormatResult(Array.Empty<object>());

        var results = new List<object>();

        // Find Queries depending on Table
        if (graph.Incoming.TryGetValue(tableNode.Id, out var ins))
        {
            foreach (var rel in ins)
            {
                if (rel.Kind == OntologyConstants.Relationships.DependsOn && graph.Nodes.TryGetValue(rel.From, out var qNode) && qNode.Kind == OntologyConstants.NodeLabels.Query)
                {
                    // Find parents (methods defining/declaring Query)
                    var parents = new List<Node>();
                    if (graph.Incoming.TryGetValue(qNode.Id, out var qIns))
                    {
                        foreach (var qRel in qIns)
                        {
                            if ((qRel.Kind == OntologyConstants.Relationships.Defines || qRel.Kind == OntologyConstants.Relationships.Declares || qRel.Kind == OntologyConstants.Relationships.Contains) &&
                                graph.Nodes.TryGetValue(qRel.From, out var parentNode))
                            {
                                parents.Add(parentNode);
                            }
                        }
                    }

                    // For each parent, find its recursive callers
                    var callingSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var parent in parents)
                    {
                        var queue = new Queue<string>();
                        queue.Enqueue(parent.Id);
                        var visited = new HashSet<string> { parent.Id };

                        while (queue.Count > 0)
                        {
                            var currId = queue.Dequeue();
                            if (graph.Incoming.TryGetValue(currId, out var parentIns))
                            {
                                foreach (var callerRel in parentIns)
                                {
                                    if ((callerRel.Kind == OntologyConstants.Relationships.Calls || callerRel.Kind == OntologyConstants.Relationships.DependsOn) &&
                                        !visited.Contains(callerRel.From))
                                    {
                                        visited.Add(callerRel.From);
                                        if (graph.Nodes.TryGetValue(callerRel.From, out var callerNode))
                                        {
                                            callingSymbols.Add(callerNode.Properties.TryGetValue("name", out var cnObj) ? cnObj?.ToString() ?? "" : "");
                                            queue.Enqueue(callerRel.From);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    foreach (var parent in parents)
                    {
                        results.Add(new
                        {
                            tableName = tableNode.Properties.TryGetValue("name", out var tnObj) ? tnObj?.ToString() ?? "" : "",
                            queryName = qNode.Properties.TryGetValue("name", out var qnObj) ? qnObj?.ToString() ?? "" : "",
                            queryText = qNode.Properties.TryGetValue("query_text", out var qtObj) ? qtObj?.ToString() ?? "" : "",
                            filePath = qNode.Properties.TryGetValue("path", out var qpObj) ? qpObj?.ToString() ?? "" : "",
                            parentName = new[] { parent.Properties.TryGetValue("name", out var pnObj) ? pnObj?.ToString() ?? "" : "" },
                            parentType = parent.Kind,
                            callingSymbols = callingSymbols.ToList()
                        });
                    }
                }
            }
        }

        return FormatResult(results);
    }

    public async Task<string> GetProjectEntryPointsAsync(string projectName, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var projNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.Project &&
            n.Properties.TryGetValue("name", out var nameVal) && string.Equals(nameVal?.ToString(), projectName, StringComparison.OrdinalIgnoreCase));

        if (projNode == null) return FormatResult(Array.Empty<object>());

        // Find all files in project
        var locatedInFolderId = graph.Outgoing.TryGetValue(projNode.Id, out var oRels) 
            ? oRels.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.LocatedIn)?.To 
            : null;

        var files = new List<Node>();
        if (locatedInFolderId != null)
        {
            var queue = new Queue<string>();
            queue.Enqueue(locatedInFolderId);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { locatedInFolderId };

            while (queue.Count > 0)
            {
                var currId = queue.Dequeue();
                if (graph.Outgoing.TryGetValue(currId, out var outs))
                {
                    foreach (var rel in outs)
                    {
                        if (rel.Kind == OntologyConstants.Relationships.Contains && !visited.Contains(rel.To))
                        {
                            visited.Add(rel.To);
                            if (graph.Nodes.TryGetValue(rel.To, out var targetNode))
                            {
                                if (targetNode.Kind == OntologyConstants.NodeLabels.Folder) queue.Enqueue(rel.To);
                                else if (targetNode.Kind == OntologyConstants.NodeLabels.File) files.Add(targetNode);
                            }
                        }
                    }
                }
            }
        }

        var results = new List<object>();

        foreach (var f in files)
        {
            var fPath = f.Properties.TryGetValue("path", out var pObj) ? pObj?.ToString() ?? "" : "";
            var isEntryPath = fPath.Contains("Controller", StringComparison.OrdinalIgnoreCase) ||
                              fPath.Contains("Endpoint", StringComparison.OrdinalIgnoreCase) ||
                              fPath.Contains("Handler", StringComparison.OrdinalIgnoreCase) ||
                              fPath.Contains("Resolver", StringComparison.OrdinalIgnoreCase);

            // Find all functions declared in file
            if (graph.Incoming.TryGetValue(f.Id, out var ins))
            {
                foreach (var rel in ins)
                {
                    if (rel.Kind == OntologyConstants.Relationships.DeclaredIn && graph.Nodes.TryGetValue(rel.From, out var funcNode) && funcNode.Kind == OntologyConstants.NodeLabels.Function)
                    {
                        var funcName = funcNode.Properties.TryGetValue("name", out var fnObj) ? fnObj?.ToString() ?? "" : "";
                        var isEntryFunc = funcName.StartsWith("On", StringComparison.Ordinal) || funcName.StartsWith("Handle", StringComparison.Ordinal);

                        if (isEntryPath || isEntryFunc)
                        {
                            // Optional class parent
                            string? className = null;
                            if (graph.Incoming.TryGetValue(funcNode.Id, out var funcIns))
                            {
                                var classRel = funcIns.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.HasMethod);
                                if (classRel != null && graph.Nodes.TryGetValue(classRel.From, out var classNode))
                                {
                                    className = classNode.Properties.TryGetValue("name", out var cnObj) ? cnObj?.ToString() : "";
                                }
                            }

                            int.TryParse(funcNode.Properties.TryGetValue("start_line", out var slObj) ? slObj?.ToString() : "0", out var startLine);

                            results.Add(new
                            {
                                entryPoint = funcName,
                                symbol = funcNode.Properties.TryGetValue("symbol", out var sObj) ? sObj?.ToString() ?? "" : "",
                                className = className ?? "",
                                filePath = fPath,
                                startLine
                            });
                        }
                    }
                }
            }
        }

        return FormatResult(results);
    }

    public async Task<string> FindRefactoringOpportunitiesAsync(string projectName, string metricType, string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);
        var projNode = graph.Nodes.Values.FirstOrDefault(n => n.Kind == OntologyConstants.NodeLabels.Project &&
            n.Properties.TryGetValue("name", out var nameVal) && string.Equals(nameVal?.ToString(), projectName, StringComparison.OrdinalIgnoreCase));

        if (projNode == null) return JsonSerializer.Serialize(new { results = Array.Empty<object>() });

        // Get all files in project
        var locatedInFolderId = graph.Outgoing.TryGetValue(projNode.Id, out var oRels) 
            ? oRels.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.LocatedIn)?.To 
            : null;

        var files = new List<Node>();
        if (locatedInFolderId != null)
        {
            var queue = new Queue<string>();
            queue.Enqueue(locatedInFolderId);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { locatedInFolderId };

            while (queue.Count > 0)
            {
                var currId = queue.Dequeue();
                if (graph.Outgoing.TryGetValue(currId, out var outs))
                {
                    foreach (var rel in outs)
                    {
                        if (rel.Kind == OntologyConstants.Relationships.Contains && !visited.Contains(rel.To))
                        {
                            visited.Add(rel.To);
                            if (graph.Nodes.TryGetValue(rel.To, out var targetNode))
                            {
                                if (targetNode.Kind == OntologyConstants.NodeLabels.Folder) queue.Enqueue(rel.To);
                                else if (targetNode.Kind == OntologyConstants.NodeLabels.File) files.Add(targetNode);
                            }
                        }
                    }
                }
            }
        }

        var fileIds = new HashSet<string>(files.Select(f => f.Id), StringComparer.OrdinalIgnoreCase);
        var results = new List<object>();

        if (metricType == "dead_code" || metricType == "all")
        {
            // Find functions/types declared in project files that have 0 incoming CALLS or USES_TYPE edges
            var declaredItems = graph.Nodes.Values.Where(n => (n.Kind == OntologyConstants.NodeLabels.Function || n.Kind == OntologyConstants.NodeLabels.Type) &&
                graph.Outgoing.TryGetValue(n.Id, out var outs) && outs.Any(r => r.Kind == OntologyConstants.Relationships.DeclaredIn && fileIds.Contains(r.To)));

            foreach (var item in declaredItems)
            {
                var hasCallers = false;
                if (graph.Incoming.TryGetValue(item.Id, out var ins))
                {
                    hasCallers = ins.Any(r => r.Kind == OntologyConstants.Relationships.Calls || r.Kind == OntologyConstants.Relationships.UsesType);
                }

                if (!hasCallers)
                {
                    var typeStr = item.Kind;
                    if (item.Kind == OntologyConstants.NodeLabels.Type)
                    {
                        var kindVal = item.Properties.TryGetValue("kind", out var kVal) ? kVal?.ToString() ?? "" : "";
                        typeStr = kindVal switch
                        {
                            "class" => "Class",
                            "interface" => "Interface",
                            _ => kindVal
                        };
                    }

                    string? fPath = null;
                    if (graph.Outgoing.TryGetValue(item.Id, out var outs2))
                    {
                        var fileRel = outs2.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.DeclaredIn);
                        if (fileRel != null && graph.Nodes.TryGetValue(fileRel.To, out var fileNode))
                        {
                            fPath = fileNode.Properties.TryGetValue("path", out var pObj) ? pObj?.ToString() : "";
                        }
                    }

                    results.Add(new
                    {
                        name = item.Properties.TryGetValue("name", out var nObj) ? nObj?.ToString() ?? "" : "",
                        type = typeStr,
                        filePath = fPath ?? "",
                        anomalyType = "dead_code",
                        symbol = item.Properties.TryGetValue("symbol", out var sObj) ? sObj?.ToString() ?? "" : ""
                    });
                }
            }
        }

        if (metricType == "god_objects" || metricType == "all")
        {
            // Find Type nodes of kind class declared in project files that have more than 15 methods/members
            var classTypes = graph.Nodes.Values.Where(n => n.Kind == OntologyConstants.NodeLabels.Type &&
                n.Properties.TryGetValue("kind", out var k) && k?.ToString() == "class" &&
                graph.Outgoing.TryGetValue(n.Id, out var outs) && outs.Any(r => r.Kind == OntologyConstants.Relationships.DeclaredIn && fileIds.Contains(r.To)));

            foreach (var c in classTypes)
            {
                var memberCount = 0;
                if (graph.Outgoing.TryGetValue(c.Id, out var outs))
                {
                    memberCount = outs.Count(r => r.Kind == OntologyConstants.Relationships.HasMethod || r.Kind == OntologyConstants.Relationships.HasMember);
                }

                if (memberCount > 15)
                {
                    string? fPath = null;
                    if (graph.Outgoing.TryGetValue(c.Id, out var outs2))
                    {
                        var fileRel = outs2.FirstOrDefault(r => r.Kind == OntologyConstants.Relationships.DeclaredIn);
                        if (fileRel != null && graph.Nodes.TryGetValue(fileRel.To, out var fileNode))
                        {
                            fPath = fileNode.Properties.TryGetValue("path", out var pObj) ? pObj?.ToString() : "";
                        }
                    }

                    results.Add(new
                    {
                        name = c.Properties.TryGetValue("name", out var nObj) ? nObj?.ToString() ?? "" : "",
                        type = "Class",
                        filePath = fPath ?? "",
                        anomalyType = "god_object",
                        metricValue = memberCount,
                        symbol = c.Properties.TryGetValue("symbol", out var sObj) ? sObj?.ToString() ?? "" : ""
                    });
                }
            }
        }

        var limitedResults = results.Take(50).ToList();
        return JsonSerializer.Serialize(new { results = limitedResults }, new JsonSerializerOptions { WriteIndented = true });
    }

    public Task<string> ExecuteCustomReadCypherAsync(string query, string? workspacePath)
    {
        throw new NotSupportedException("Custom Cypher queries are not supported when running in SQLite/In-memory mode.");
    }

    public async Task<string> GetWorkspaceContentAsync(string? workspacePath, string? type)
    {
        var graph = await GetGraphAsync(workspacePath);
        IEnumerable<Node> nodes = graph.Nodes.Values;
        if (!string.IsNullOrEmpty(type))
        {
            nodes = nodes.Where(n => string.Equals(n.Kind, type, StringComparison.OrdinalIgnoreCase));
        }

        var results = nodes.Take(1000).Select(n => new
        {
            id = n.Id,
            labels = new[] { n.Kind },
            props = n.Properties
        }).ToList();

        return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
    }

    public Task<string> ExecuteRawQueryAsync(string query, Dictionary<string, object?>? parameters = null)
    {
        throw new NotSupportedException("Raw Cypher/Gremlin queries are not supported when running in SQLite/In-memory mode.");
    }

    public async Task<string> GetTaxonomyAsync(string? workspacePath)
    {
        var graph = await GetGraphAsync(workspacePath);

        // Compute triplets (fromLabel -> relType -> toLabel)
        var triplets = new HashSet<(string From, string Rel, string To)>();
        var properties = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes.Values)
        {
            if (!properties.ContainsKey(node.Kind)) properties[node.Kind] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in node.Properties.Keys)
            {
                properties[node.Kind].Add(key);
            }
        }

        foreach (var node in graph.Nodes.Values)
        {
            if (graph.Outgoing.TryGetValue(node.Id, out var outs))
            {
                foreach (var rel in outs)
                {
                    if (graph.Nodes.TryGetValue(rel.To, out var target))
                    {
                        triplets.Add((node.Kind, rel.Kind, target.Kind));
                    }
                }
            }
        }

        var propList = properties.SelectMany(kv => kv.Value.Select(k => new Dictionary<string, string>
        {
            ["label"] = kv.Key,
            ["key"] = k
        })).ToList();

        var tripList = triplets.Select(t => new Dictionary<string, string>
        {
            ["fromLabel"] = t.From,
            ["relType"] = t.Rel,
            ["toLabel"] = t.To
        }).ToList();

        var taxonomy = MemgraphCodeExplorerRepository.BuildTaxonomy(tripList, propList);
        return JsonSerializer.Serialize(new { taxonomy }, new JsonSerializerOptions { WriteIndented = true });
    }

    public async Task<string> FetchCodeSnippetsAsync(string nodesJson, string? workspacePath)
    {
        // Leverage the helper logic in MemgraphCodeExplorerRepository directly as it is independent of the database query structure.
        var helper = new MemgraphCodeExplorerRepository(dbClient);
        return await helper.FetchCodeSnippetsAsync(nodesJson, workspacePath);
    }

    public string GetNodeDefinition(string kind)
    {
        return OntologyRegistry.GetNodeDefinition(kind);
    }
}
