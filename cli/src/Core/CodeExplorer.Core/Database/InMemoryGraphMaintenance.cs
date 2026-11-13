using System.Buffers;
using System.Text;
using System.Text.Json;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Database;

/// <summary>
/// Pure in-memory graph maintenance operations shared by <see cref="InMemoryGraphRepository"/>
/// and <see cref="IGraphClient"/> bulk loading so that Layer 1..5 indexing executes
/// 100% in memory without mid-pipeline SQLite flushes.
/// </summary>
public static class InMemoryGraphMaintenance
{
    private const string PropName = "name";
    private const string PropPath = "path";
    private const string PropRole = "role";
    private const string PropIsLibrary = "is_library";
    private const string PropRouteTemplate = "route_template";
    private const string PropHttpMethod = "http_method";
    private const string PropDomainOrService = "domain_or_service";
    private const string PropInferredFromPath = "inferred_from_path";
    private const string PropIsRelativePath = "is_relative_path";
    private const string PropIsExternal = "is_external";
    private const string PropInferredFromIdentifier = "inferred_from_identifier";
    private const string PropDatabaseId = "database_id";
    private const string PropEngine = "engine";
    private const string PropSchema = "schema";
    private const string TrueLiteral = "true";
    private const string NullSentinel = "\0";
    private const string SemanticStructureToken = "semantic_structure";
    private const string ProjectPrefixShort = ":p:";
    private const string ProjectPrefixLong = ":project:";
    private const string ProjectSuffixLong = ":project";
    private const string ServicePrefix = ":svc:";
    private const string AppPrefix = ":app:";
    private const string WorkerPrefix = ":wrk:";
    private const string LibraryPrefix = ":lib:";

    public static void UpsertRelationship(
        Dictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships,
        Relationship rel)
    {
        var key = (rel.From, rel.To, rel.Kind);
        if (!relationships.TryGetValue(key, out var existing))
        {
            relationships[key] = rel;
        }
        else if (rel.HasTelemetry)
        {
            EvidenceProvenanceHelper.MergeRelationships(existing, rel);
        }
    }

    public static EdgeNormalizationSnapshot LoadEdgeNormalizationSnapshot(
        IReadOnlyDictionary<string, Node> nodes,
        IReadOnlyDictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships)
    {
        var nodeMap = new Dictionary<string, (NodeKind Kind, bool IsLibrary)>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes.Values)
        {
            var kind = node.NodeKind;
            if (kind is not (NodeKind.Project or NodeKind.Service or NodeKind.App or NodeKind.Worker or
                NodeKind.Library or NodeKind.Package or NodeKind.Database or NodeKind.Topic or
                NodeKind.Endpoint or NodeKind.EntryPoint or NodeKind.ExternalService or
                NodeKind.CloudService or NodeKind.PeerMesh))
            {
                continue;
            }

            var isLib = kind is NodeKind.Package or NodeKind.Library;
            var role = GetNodeStringProperty(node, PropRole);
            if (!isLib)
            {
                isLib = GetNodeBoolProperty(node, PropIsLibrary) || role is "SharedLibrary" or "Test";
            }

            nodeMap[node.Id] = (kind, isLib);
        }

        var edges = new List<MacroEdgeSnapshot>();
        var buffer = new ArrayBufferWriter<byte>(256);
        using var writer = new Utf8JsonWriter(buffer);
        foreach (var rel in relationships.Values)
        {
            if (rel.Kind is not (RelationshipKind.UsesDb or RelationshipKind.Configures or
                RelationshipKind.DependsOn or RelationshipKind.ServiceCall or
                RelationshipKind.IntegratesWith or RelationshipKind.CallsEndpoint or
                RelationshipKind.PublishesTo or RelationshipKind.Triggers or
                RelationshipKind.SubscribesTo or RelationshipKind.Library))
            {
                continue;
            }

            if (string.IsNullOrEmpty(rel.From) || string.IsNullOrEmpty(rel.To)) continue;

            var props = ExtractRelationshipStringProperties(rel, buffer, writer);
            edges.Add(new MacroEdgeSnapshot(rel.From, rel.To, rel.Kind, props));
        }

        return new EdgeNormalizationSnapshot(nodeMap, edges);
    }

    public static int ApplyEdgeNormalizationUpdates(
        Dictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships,
        IReadOnlyList<EdgeNormalizationUpdate> updates)
    {
        if (updates.Count == 0) return 0;

        var updatedCount = 0;
        foreach (var u in updates)
        {
            var oldKey = (u.Source, u.Target, u.RawKind);
            if (!relationships.TryGetValue(oldKey, out var existing))
            {
                continue;
            }

            var newKey = (u.Source, u.Target, u.NormalizedKind);
            if (u.NormalizedKind != u.RawKind)
            {
                relationships.Remove(oldKey);
                if (relationships.ContainsKey(newKey))
                {
                    // UPDATE OR IGNORE semantics: if normalized edge already exists, keep existing
                    updatedCount++;
                    continue;
                }
            }

            if (!existing.HasMaterializedProperties)
            {
                var updatedRel = u.NormalizedKind != u.RawKind ? existing with { Kind = u.NormalizedKind } : existing;
                ApplyJsonPropertiesToTypedRelationship(updatedRel, u.PropsJson);
                relationships[newKey] = updatedRel;
            }
            else
            {
                var propsDict = DeserializeJsonToObjectDictionary(u.PropsJson);
                var updatedRel = new Relationship(u.Source, u.Target, u.NormalizedKind, propsDict);
                relationships[newKey] = updatedRel;
            }
            updatedCount++;
        }

        return updatedCount;
    }

    public static ExternalServicePurgeSnapshot LoadExternalServicePurgeSnapshot(
        IReadOnlyDictionary<string, Node> nodes,
        IReadOnlyDictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships)
    {
        var projects = new List<(string Id, string Name, string? Path)>();
        var endpoints = new List<EndpointCandidateSnapshot>();
        var externalServices = new List<ExternalServiceCandidateSnapshot>();

        foreach (var node in nodes.Values)
        {
            switch (node.NodeKind)
            {
                case NodeKind.Project:
                {
                    var name = GetNodeStringProperty(node, PropName);
                    var path = GetNodeNullableStringProperty(node, PropPath);
                    projects.Add((node.Id, name, path));
                    break;
                }
                case NodeKind.Endpoint:
                {
                    var name = GetNodeStringProperty(node, PropName);
                    var route = GetNodeStringProperty(node, PropRouteTemplate);
                    var method = GetNodeStringProperty(node, PropHttpMethod);
                    var path = GetNodeStringProperty(node, PropPath);
                    endpoints.Add(new EndpointCandidateSnapshot(node.Id, name, route, method, path));
                    break;
                }
                case NodeKind.ExternalService:
                {
                    var name = GetNodeStringProperty(node, PropName);
                    var domain = GetNodeStringProperty(node, PropDomainOrService);
                    var path = GetNodeStringProperty(node, PropPath);
                    var inferredFromPath = GetNodeStringProperty(node, PropInferredFromPath);
                    var isRelativePath = GetNodeStringProperty(node, PropIsRelativePath);
                    var isExt = GetNodeStringProperty(node, PropIsExternal);
                    var inferredFromIdent = GetNodeStringProperty(node, PropInferredFromIdentifier);
                    externalServices.Add(new ExternalServiceCandidateSnapshot(
                        node.Id, name, domain, path, inferredFromPath, isRelativePath, isExt, inferredFromIdent));
                    break;
                }
            }
        }

        var servicesCallingEndpoints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in relationships.Values)
        {
            if (rel.Kind == RelationshipKind.CallsEndpoint &&
                nodes.TryGetValue(rel.From, out var srcNode) && srcNode.NodeKind == NodeKind.ExternalService &&
                nodes.TryGetValue(rel.To, out var tgtNode) && tgtNode.NodeKind == NodeKind.Endpoint)
            {
                servicesCallingEndpoints.Add(rel.From);
            }
        }

        return new ExternalServicePurgeSnapshot(projects, endpoints, servicesCallingEndpoints, externalServices);
    }

    public static List<(string Id, string Name)> LoadTopicsForPurge(IReadOnlyDictionary<string, Node> nodes)
    {
        var list = new List<(string Id, string Name)>();
        foreach (var node in nodes.Values)
        {
            if (node.NodeKind == NodeKind.Topic)
            {
                list.Add((node.Id, GetNodeStringProperty(node, PropName)));
            }
        }
        return list;
    }

    public static void DeleteNodesAndConnectedEdges(
        Dictionary<string, Node> nodes,
        Dictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships,
        IReadOnlyList<string> nodeIds)
    {
        if (nodeIds.Count == 0) return;

        var idSet = new HashSet<string>(nodeIds, StringComparer.Ordinal);
        foreach (var id in idSet)
        {
            nodes.Remove(id);
        }

        var edgeKeysToRemove = new List<(string From, string To, RelationshipKind Kind)>();
        foreach (var key in relationships.Keys)
        {
            if (idSet.Contains(key.From) || idSet.Contains(key.To))
            {
                edgeKeysToRemove.Add(key);
            }
        }

        foreach (var key in edgeKeysToRemove)
        {
            relationships.Remove(key);
        }
    }

    public static void DeduplicateLegacyProjectNodes(
        Dictionary<string, Node> nodes,
        Dictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships)
    {
        var canonicalProjectPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes.Values)
        {
            if (node.NodeKind == NodeKind.Project && node.Id.Contains(ProjectPrefixShort, StringComparison.Ordinal))
            {
                var path = GetNodeStringProperty(node, PropPath);
                if (!string.IsNullOrEmpty(path))
                {
                    canonicalProjectPaths.Add(path);
                }
            }
        }

        var toDelete = new List<string>();
        foreach (var node in nodes.Values)
        {
            if (node.NodeKind == NodeKind.Project &&
                (node.Id.Contains(ProjectPrefixLong, StringComparison.Ordinal) || node.Id.EndsWith(ProjectSuffixLong, StringComparison.Ordinal)))
            {
                var path = GetNodeStringProperty(node, PropPath);
                if (!string.IsNullOrEmpty(path) && canonicalProjectPaths.Contains(path))
                {
                    toDelete.Add(node.Id);
                }
            }
            else if (node.NodeKind is NodeKind.ProjectSemantic or NodeKind.ProjectSyntax &&
                     node.Id.Contains(ProjectPrefixLong, StringComparison.Ordinal))
            {
                toDelete.Add(node.Id);
            }
        }

        if (toDelete.Count > 0)
        {
            DeleteNodesAndConnectedEdges(nodes, relationships, toDelete);
        }
    }

    public static void ReplaceProjectDatabaseRelationshipsAndRemap(
        Dictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships,
        IReadOnlyList<Relationship> relationshipsToUpload,
        IReadOnlyList<DomainEdgeTargetRemap> domainRemaps)
    {
        var keysToRemove = new List<(string From, string To, RelationshipKind Kind)>();
        foreach (var key in relationships.Keys)
        {
            if (key.Kind == RelationshipKind.UsesDb && IsProjectOrWorkloadId(key.From))
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            relationships.Remove(key);
        }

        foreach (var rel in relationshipsToUpload)
        {
            UpsertRelationship(relationships, rel);
        }

        foreach (var remap in domainRemaps)
        {
            var prefix = remap.Prefix.TrimEnd('%');
            var matchingEdges = relationships.Values
                .Where(r => string.Equals(r.To, remap.LoserId, StringComparison.Ordinal) &&
                            r.From.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var edge in matchingEdges)
            {
                var oldKey = (edge.From, edge.To, edge.Kind);
                relationships.Remove(oldKey);
                var newKey = (edge.From, remap.WinnerId, edge.Kind);
                if (!relationships.ContainsKey(newKey))
                {
                    relationships[newKey] = edge with { To = remap.WinnerId };
                }
            }
        }
    }

    public static void CleanupAndPropagateDatabaseEntities(
        Dictionary<string, Node> nodes,
        Dictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships,
        IReadOnlyDictionary<string, string> rawToCanonical,
        IReadOnlyList<CanonicalDatabasePropagationInfo> canonicalDatabases,
        bool propagateChildSchemaProperties)
    {
        var activeRawRemaps = new Dictionary<string, string>(StringComparer.Ordinal);
        var rawToRemoveWithoutCanonical = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (rawId, canonicalId) in rawToCanonical)
        {
            if (string.Equals(rawId, canonicalId, StringComparison.Ordinal)) continue;
            if (nodes.ContainsKey(canonicalId))
            {
                activeRawRemaps[rawId] = canonicalId;
            }
            else
            {
                rawToRemoveWithoutCanonical.Add(rawId);
            }
            nodes.Remove(rawId);
        }

        if (activeRawRemaps.Count > 0 || rawToRemoveWithoutCanonical.Count > 0)
        {
            var edgesToMutate = new List<Relationship>();
            var keysToRemove = new List<(string From, string To, RelationshipKind Kind)>();
            foreach (var (key, rel) in relationships)
            {
                if (rawToRemoveWithoutCanonical.Contains(key.From) || rawToRemoveWithoutCanonical.Contains(key.To))
                {
                    keysToRemove.Add(key);
                }
                else if (activeRawRemaps.ContainsKey(key.From) || activeRawRemaps.ContainsKey(key.To))
                {
                    edgesToMutate.Add(rel);
                }
            }

            foreach (var k in keysToRemove)
            {
                relationships.Remove(k);
            }

            foreach (var edge in edgesToMutate)
            {
                relationships.Remove((edge.From, edge.To, edge.Kind));
                var newFrom = activeRawRemaps.TryGetValue(edge.From, out var mappedFrom) ? mappedFrom : edge.From;
                var newTo = activeRawRemaps.TryGetValue(edge.To, out var mappedTo) ? mappedTo : edge.To;
                var newKey = (newFrom, newTo, edge.Kind);
                if (!relationships.ContainsKey(newKey))
                {
                    relationships[newKey] = edge with { From = newFrom, To = newTo };
                }
            }
        }

        // Delete CONTAINS edges to Database nodes unless from semantic_structure
        var invalidDbContains = new List<(string From, string To, RelationshipKind Kind)>();
        foreach (var k in relationships.Keys)
        {
            if (k.Kind == RelationshipKind.Contains &&
                nodes.TryGetValue(k.To, out var tgt) && tgt.NodeKind == NodeKind.Database &&
                !k.From.Contains(SemanticStructureToken, StringComparison.OrdinalIgnoreCase))
            {
                invalidDbContains.Add(k);
            }
        }
        foreach (var k in invalidDbContains)
        {
            relationships.Remove(k);
        }

        var validIds = new HashSet<string>(canonicalDatabases.Select(c => c.Id), StringComparer.Ordinal);
        var dbOrSchemaToDelete = new List<string>();
        foreach (var node in nodes.Values)
        {
            if (node.NodeKind == NodeKind.Database)
            {
                if (validIds.Count == 0 || !validIds.Contains(node.Id))
                {
                    dbOrSchemaToDelete.Add(node.Id);
                }
            }
            else if (node.NodeKind == NodeKind.DbSchema)
            {
                var dbId = GetNodeStringProperty(node, PropDatabaseId);
                if (validIds.Count == 0 || !validIds.Contains(dbId))
                {
                    dbOrSchemaToDelete.Add(node.Id);
                }
            }
        }

        if (dbOrSchemaToDelete.Count > 0)
        {
            DeleteNodesAndConnectedEdges(nodes, relationships, dbOrSchemaToDelete);
        }

        // Reroute any direct Database -[:CONTAINS]-> Table/Procedure edges onto the Database's DbSchema node
        var dbToSchemas = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var rel in relationships.Values)
        {
            if (rel.Kind == RelationshipKind.Contains &&
                nodes.TryGetValue(rel.From, out var dbNode) && dbNode.NodeKind == NodeKind.Database &&
                nodes.TryGetValue(rel.To, out var schNode) && schNode.NodeKind == NodeKind.DbSchema)
            {
                if (!dbToSchemas.TryGetValue(dbNode.Id, out var list))
                {
                    list = [];
                    dbToSchemas[dbNode.Id] = list;
                }
                list.Add(schNode.Id);
            }
        }

        if (dbToSchemas.Count > 0)
        {
            var directTableEdges = new List<Relationship>();
            foreach (var r in relationships.Values)
            {
                if (r.Kind == RelationshipKind.Contains &&
                    dbToSchemas.ContainsKey(r.From) &&
                    nodes.TryGetValue(r.To, out var tbl) && tbl.NodeKind is NodeKind.Table or NodeKind.Procedure)
                {
                    directTableEdges.Add(r);
                }
            }

            foreach (var edge in directTableEdges)
            {
                relationships.Remove((edge.From, edge.To, edge.Kind));
                foreach (var schId in dbToSchemas[edge.From])
                {
                    var newKey = (schId, edge.To, RelationshipKind.Contains);
                    if (!relationships.ContainsKey(newKey))
                    {
                        relationships[newKey] = edge with { From = schId };
                    }
                }
            }
        }

        if (propagateChildSchemaProperties)
        {
            var invalidTables = nodes.Values
                .Where(n => n.NodeKind == NodeKind.Table && GetNodeStringProperty(n, PropName).Contains(' '))
                .Select(n => n.Id)
                .ToList();
            if (invalidTables.Count > 0)
            {
                DeleteNodesAndConnectedEdges(nodes, relationships, invalidTables);
            }
        }

        if (canonicalDatabases.Count > 0)
        {
            LinkOrphanTablesToCanonicalDatabases(nodes, relationships, canonicalDatabases, dbToSchemas);
        }

        if (propagateChildSchemaProperties)
        {
            PropagateDatabaseEngineAndSchemaToChildren(nodes, relationships, canonicalDatabases);
        }
    }

    private static void LinkOrphanTablesToCanonicalDatabases(
        Dictionary<string, Node> nodes,
        Dictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships,
        IReadOnlyList<CanonicalDatabasePropagationInfo> canonicalDatabases,
        Dictionary<string, List<string>> dbToSchemas)
    {
        var tableNodes = nodes.Values.Where(n => n.NodeKind == NodeKind.Table).ToList();
        if (tableNodes.Count == 0) return;

        var containedTableIds = new HashSet<string>(StringComparer.Ordinal);
        var fileToProject = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var fileToTables = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var rel in relationships.Values)
        {
            if (rel.Kind != RelationshipKind.Contains) continue;
            if (!nodes.TryGetValue(rel.From, out var fromNode) || !nodes.TryGetValue(rel.To, out var toNode)) continue;

            if (fromNode.NodeKind is NodeKind.Database or NodeKind.DbSchema or NodeKind.DataSet &&
                toNode.NodeKind == NodeKind.Table)
            {
                containedTableIds.Add(toNode.Id);
            }
            else if (fromNode.NodeKind == NodeKind.Project && toNode.NodeKind == NodeKind.File)
            {
                if (!fileToProject.TryGetValue(toNode.Id, out var pList))
                {
                    pList = [];
                    fileToProject[toNode.Id] = pList;
                }
                pList.Add(fromNode.Id);
            }
            else if (fromNode.NodeKind == NodeKind.File && toNode.NodeKind == NodeKind.Table)
            {
                if (!fileToTables.TryGetValue(fromNode.Id, out var tList))
                {
                    tList = [];
                    fileToTables[fromNode.Id] = tList;
                }
                tList.Add(toNode.Id);
            }
        }

        foreach (var dbInfo in canonicalDatabases)
        {
            if (!nodes.ContainsKey(dbInfo.Id)) continue;
            var cName = dbInfo.Name ?? "";
            var cleanDbName = cName.Contains('.') ? cName.Split('.', 2)[1] : cName;
            if (string.IsNullOrWhiteSpace(cleanDbName) || cleanDbName.Length < 3) continue;

            var cleanLower = cleanDbName.ToLowerInvariant();
            var targetContainerId = dbToSchemas.TryGetValue(dbInfo.Id, out var schList) && schList.Count > 0
                ? schList[0]
                : dbInfo.Id;

            foreach (var t in tableNodes)
            {
                if (containedTableIds.Contains(t.Id)) continue;

                var tPath = GetNodeStringProperty(t, PropPath).Replace('\\', '/').ToLowerInvariant();
                var tIdLower = t.Id.ToLowerInvariant();
                if (tPath.Contains($"/{cleanLower}/", StringComparison.Ordinal) ||
                    tPath.Contains($"/{cleanLower}-", StringComparison.Ordinal) ||
                    tPath.Contains($"/{cleanLower}_", StringComparison.Ordinal) ||
                    tIdLower.Contains(cleanLower, StringComparison.Ordinal))
                {
                    var key = (targetContainerId, t.Id, RelationshipKind.Contains);
                    if (!relationships.ContainsKey(key))
                    {
                        relationships[key] = new Relationship(targetContainerId, t.Id, RelationshipKind.Contains);
                    }
                    containedTableIds.Add(t.Id);
                }
            }
        }

        var primaryCanonicalId = canonicalDatabases[0].Id;
        if (nodes.ContainsKey(primaryCanonicalId))
        {
            var primaryContainerId = dbToSchemas.TryGetValue(primaryCanonicalId, out var pSchList) && pSchList.Count > 0
                ? pSchList[0]
                : primaryCanonicalId;

            foreach (var t in tableNodes)
            {
                if (containedTableIds.Contains(t.Id)) continue;
                var key = (primaryContainerId, t.Id, RelationshipKind.Contains);
                if (!relationships.ContainsKey(key))
                {
                    relationships[key] = new Relationship(primaryContainerId, t.Id, RelationshipKind.Contains);
                }
                containedTableIds.Add(t.Id);
            }
        }

        foreach (var (fileId, tIds) in fileToTables)
        {
            if (!fileToProject.TryGetValue(fileId, out var pIds)) continue;
            foreach (var pId in pIds)
            {
                foreach (var tId in tIds)
                {
                    var key = (pId, tId, RelationshipKind.Defines);
                    if (!relationships.ContainsKey(key))
                    {
                        relationships[key] = new Relationship(pId, tId, RelationshipKind.Defines);
                    }
                }
            }
        }
    }

    private static void PropagateDatabaseEngineAndSchemaToChildren(
        Dictionary<string, Node> nodes,
        Dictionary<(string From, string To, RelationshipKind Kind), Relationship> relationships,
        IReadOnlyList<CanonicalDatabasePropagationInfo> canonicalDatabases)
    {
        var containsChildrenByParent = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var rel in relationships.Values)
        {
            if (rel.Kind != RelationshipKind.Contains) continue;
            if (!nodes.TryGetValue(rel.From, out var parentNode) ||
                parentNode.NodeKind is not (NodeKind.Database or NodeKind.DataSet or NodeKind.DbSchema))
            {
                continue;
            }

            if (!containsChildrenByParent.TryGetValue(rel.From, out var childList))
            {
                childList = [];
                containsChildrenByParent[rel.From] = childList;
            }
            childList.Add(rel.To);
        }

        foreach (var dbInfo in canonicalDatabases)
        {
            var cName = dbInfo.Name ?? "";
            var sch = cName.Contains('.') ? cName.Split('.', 2)[1] : "";
            var engineToUse = dbInfo.Engine;
            if (string.IsNullOrWhiteSpace(engineToUse)) continue;
            if (!containsChildrenByParent.TryGetValue(dbInfo.Id, out var directChildIds)) continue;

            foreach (var childId in directChildIds)
            {
                if (!nodes.TryGetValue(childId, out var childNode)) continue;
                if (childNode.NodeKind is NodeKind.DataSet or NodeKind.DbSchema)
                {
                    var childName = GetNodeStringProperty(childNode, PropName);
                    SetNodeStringProperty(childNode, PropEngine, engineToUse);
                    SetNodeStringProperty(childNode, PropSchema, !string.IsNullOrEmpty(childName) ? childName : sch);

                    if (containsChildrenByParent.TryGetValue(childId, out var grandChildIds))
                    {
                        foreach (var gcId in grandChildIds)
                        {
                            if (nodes.TryGetValue(gcId, out var gcNode) && gcNode.NodeKind == NodeKind.Table)
                            {
                                SetNodeStringProperty(gcNode, PropEngine, engineToUse);
                                SetNodeStringProperty(gcNode, PropSchema, !string.IsNullOrEmpty(childName) ? childName : sch);
                            }
                        }
                    }
                }
                else if (childNode.NodeKind == NodeKind.Table)
                {
                    SetNodeStringProperty(childNode, PropEngine, engineToUse);
                    SetNodeStringProperty(childNode, PropSchema, sch);
                }
            }
        }
    }

    private static bool IsProjectOrWorkloadId(string id) =>
        id.Contains(ProjectPrefixShort, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(ProjectPrefixLong, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(ServicePrefix, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(AppPrefix, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(WorkerPrefix, StringComparison.OrdinalIgnoreCase) ||
        id.Contains(LibraryPrefix, StringComparison.OrdinalIgnoreCase);

    public static string GetNodeStringProperty(Node node, string key, string fallback = "")
    {
        if (node.OntologyNode != null && !node.HasMaterializedProperties)
        {
            var ont = node.OntologyNode;
            switch (key)
            {
                case "name":
                    return ont.GetName();
                case "path":
                    return ont.Path ?? fallback;
                case "role":
                    return ont switch
                    {
                        ProjectNode pn => pn.Role ?? fallback,
                        DatabaseNode dn => dn.Role ?? fallback,
                        _ => GetCustomOrMaterializedString(node, key, fallback)
                    };
                case "is_library":
                    return ont switch
                    {
                        ProjectNode pn => pn.IsLibrary ? "true" : "false",
                        LibraryNode => "true",
                        _ => GetCustomOrMaterializedString(node, key, fallback)
                    };
                case "route_template":
                    return ont is EndpointNode ep ? ep.RouteTemplate ?? fallback : GetCustomOrMaterializedString(node, key, fallback);
                case "http_method":
                    return ont is EndpointNode ep2 ? ep2.HttpMethod ?? fallback : GetCustomOrMaterializedString(node, key, fallback);
                case "domain_or_service":
                    return ont is ExternalServiceNode es ? es.DomainOrService ?? fallback : GetCustomOrMaterializedString(node, key, fallback);
                case "inferred_from_path":
                    return ont is ExternalServiceNode es2 && es2.InferredFromPath == true ? "true" : GetCustomOrMaterializedString(node, key, fallback);
                case "is_relative_path":
                    return ont is ExternalServiceNode es3 && es3.IsRelativePath == true ? "true" : GetCustomOrMaterializedString(node, key, fallback);
                case "is_external":
                    return ont is ExternalServiceNode es4 && es4.IsExternal == true ? "true" : GetCustomOrMaterializedString(node, key, fallback);
                case "inferred_from_identifier":
                    return ont is ExternalServiceNode es5 && es5.InferredFromIdentifier == true ? "true" : GetCustomOrMaterializedString(node, key, fallback);
                case "database_id":
                    return ont is DbSchemaNode ds ? ds.DatabaseId ?? fallback : GetCustomOrMaterializedString(node, key, fallback);
            }
        }

        return GetCustomOrMaterializedString(node, key, fallback);
    }

    private static string GetCustomOrMaterializedString(Node node, string key, string fallback)
    {
        if (node.OntologyNode?.CustomProperties != null &&
            node.OntologyNode.CustomProperties.TryGetValue(key, out var customVal) &&
            customVal != null)
        {
            return customVal;
        }

        if ((node.HasMaterializedProperties || node.OntologyNode == null) &&
            node.Properties.TryGetValue(key, out var val) && val != null)
        {
            if (val is bool b) return b ? "true" : "false";
            if (val is JsonElement je)
            {
                return je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString() ?? fallback,
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null or JsonValueKind.Undefined => fallback,
                    _ => je.GetRawText()
                };
            }
            return val.ToString() ?? fallback;
        }

        return fallback;
    }

    public static string? GetNodeNullableStringProperty(Node node, string key)
    {
        var val = GetNodeStringProperty(node, key, NullSentinel);
        return val == NullSentinel ? null : val;
    }

    public static bool GetNodeBoolProperty(Node node, string key)
    {
        if (node.OntologyNode != null && !node.HasMaterializedProperties)
        {
            if (key == PropIsLibrary && node.OntologyNode is ProjectNode pn)
            {
                return pn.IsLibrary;
            }
        }

        var str = GetNodeStringProperty(node, key);
        return str.Equals(TrueLiteral, StringComparison.OrdinalIgnoreCase) || str == "1";
    }

    public static void SetNodeStringProperty(Node node, string key, string value)
    {
        if (node.OntologyNode != null)
        {
            switch (node.OntologyNode)
            {
                case TableNode tn when key == "engine":
                    tn.Engine = value;
                    break;
                case TableNode tn when key == "schema":
                    tn.Schema = value;
                    break;
                case DataSetNode ds when key == "engine":
                    ds.Engine = value;
                    break;
                case DataSetNode ds when key == "schema":
                    ds.Schema = value;
                    break;
                case DbSchemaNode sch when key == "engine":
                    sch.Engine = value;
                    break;
            }
        }

        if (node.HasMaterializedProperties || node.OntologyNode == null)
        {
            node.Properties[key] = value;
        }
    }

    private static Dictionary<string, string> ExtractRelationshipStringProperties(
        Relationship rel,
        ArrayBufferWriter<byte> buffer,
        Utf8JsonWriter writer)
    {
        var props = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!rel.HasTelemetry) return props;

        buffer.Clear();
        writer.Reset(buffer);
        rel.WriteJsonProperties(writer);
        writer.Flush();

        using var doc = JsonDocument.Parse(buffer.WrittenMemory);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            props[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => prop.Value.GetRawText()
            };
        }

        return props;
    }

    private static void ApplyJsonPropertiesToTypedRelationship(Relationship rel, string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name == "dependency_type")
            {
                rel.DependencyType = prop.Value.GetString();
            }
            else if (prop.Name == "category")
            {
                rel.Category = prop.Value.GetString();
            }
        }
    }

    private static Dictionary<string, object> DeserializeJsonToObjectDictionary(string json)
    {
        var dict = new Dictionary<string, object>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json)) return dict;

        using var doc = JsonDocument.Parse(json);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            object? val = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? string.Empty,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when prop.Value.TryGetInt32(out var i) => i,
                JsonValueKind.Number when prop.Value.TryGetInt64(out var l) => l,
                JsonValueKind.Number when prop.Value.TryGetDouble(out var d) => d,
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => prop.Value.GetRawText()
            };
            if (val != null)
            {
                dict[prop.Name] = val;
            }
        }

        return dict;
    }
}

public static class InMemoryGraphRepositoryExtensions
{
    private const string PropExternalApis = "external_apis";

    public static Task CreateIndicesAsync(this InMemoryGraphRepository _) => Task.CompletedTask;

    public static Task<bool> ClearWorkspaceAsync(this InMemoryGraphRepository _, string workspacePath) => Task.FromResult(true);

    public static Task ExecuteWriteAsync(this InMemoryGraphRepository _, string query, object? parameters = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public static Task SavePostIndexResultsAsync(this InMemoryGraphRepository repo, PostIndexAnalysisResult result)
    {
        lock (repo.SyncLock)
        {
            foreach (var (projectId, apis) in result.ProjectExternalApis)
            {
                repo.ProjectExternalApis[projectId] = apis;
                if (repo.NodesById.TryGetValue(projectId, out var projNode))
                {
                    if (projNode.OntologyNode != null)
                    {
                        projNode.OntologyNode.CustomProperties ??= new Dictionary<string, string>(StringComparer.Ordinal);
                        projNode.OntologyNode.CustomProperties[PropExternalApis] = JsonSerializer.Serialize(apis);
                    }
                    else
                    {
                        projNode.Properties[PropExternalApis] = apis;
                    }
                }
            }

            foreach (var tc in result.TransitivelyCalls)
            {
                repo.UpsertRelationshipUnlocked(new Relationship(tc.From, tc.To, RelationshipKind.TransitivelyCalls)
                {
                    Hops = tc.Hops
                });
            }

            foreach (var attr in result.AttributedTo)
            {
                repo.UpsertRelationshipUnlocked(new Relationship(attr.EpId, attr.SinkId, RelationshipKind.AttributedTo)
                {
                    Hops = attr.Hops,
                    TargetSinkClassification = attr.SinkKind
                });
            }
        }

        return Task.CompletedTask;
    }

    public static Task<EdgeNormalizationSnapshot> LoadEdgeNormalizationSnapshotAsync(this InMemoryGraphRepository repo)
    {
        lock (repo.SyncLock)
        {
            return Task.FromResult(InMemoryGraphMaintenance.LoadEdgeNormalizationSnapshot(repo.NodesById, repo.RelationshipsByKey));
        }
    }

    public static Task<int> ApplyEdgeNormalizationUpdatesAsync(this InMemoryGraphRepository repo, IReadOnlyList<EdgeNormalizationUpdate> updates)
    {
        lock (repo.SyncLock)
        {
            var changed = InMemoryGraphMaintenance.ApplyEdgeNormalizationUpdates(repo.RelationshipsByKey, updates);
            if (changed > 0)
            {
                RebuildEdgeIndicesInPlace(repo);
            }
            return Task.FromResult(changed);
        }
    }

    public static Task<ExternalServicePurgeSnapshot> LoadExternalServicePurgeSnapshotAsync(this InMemoryGraphRepository repo)
    {
        lock (repo.SyncLock)
        {
            return Task.FromResult(InMemoryGraphMaintenance.LoadExternalServicePurgeSnapshot(repo.NodesById, repo.RelationshipsByKey));
        }
    }

    public static Task<List<(string Id, string Name)>> LoadTopicsForPurgeAsync(this InMemoryGraphRepository repo)
    {
        lock (repo.SyncLock)
        {
            return Task.FromResult(InMemoryGraphMaintenance.LoadTopicsForPurge(repo.NodesById));
        }
    }

    public static Task DeleteNodesAndConnectedEdgesAsync(this InMemoryGraphRepository repo, IReadOnlyList<string> nodeIds)
    {
        if (nodeIds.Count == 0) return Task.CompletedTask;

        lock (repo.SyncLock)
        {
            var nodes = repo.NodesById.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
            InMemoryGraphMaintenance.DeleteNodesAndConnectedEdges(nodes, repo.RelationshipsByKey, nodeIds);
            RebuildNodeIndicesInPlace(repo, nodes);
            RebuildEdgeIndicesInPlace(repo);
        }

        return Task.CompletedTask;
    }

    public static Task DeduplicateLegacyProjectNodesAsync(this InMemoryGraphRepository repo)
    {
        lock (repo.SyncLock)
        {
            var nodes = repo.NodesById.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
            var prevNodeCount = nodes.Count;
            InMemoryGraphMaintenance.DeduplicateLegacyProjectNodes(nodes, repo.RelationshipsByKey);
            if (nodes.Count != prevNodeCount)
            {
                RebuildNodeIndicesInPlace(repo, nodes);
                RebuildEdgeIndicesInPlace(repo);
            }
        }

        return Task.CompletedTask;
    }

    public static Task ReplaceProjectDatabaseRelationshipsAndRemapAsync(
        this InMemoryGraphRepository repo,
        IReadOnlyList<Relationship> relationshipsToUpload,
        IReadOnlyList<DomainEdgeTargetRemap> domainRemaps)
    {
        lock (repo.SyncLock)
        {
            InMemoryGraphMaintenance.ReplaceProjectDatabaseRelationshipsAndRemap(repo.RelationshipsByKey, relationshipsToUpload, domainRemaps);
            RebuildEdgeIndicesInPlace(repo);
        }

        return Task.CompletedTask;
    }

    public static Task CleanupAndPropagateDatabaseEntitiesAsync(
        this InMemoryGraphRepository repo,
        IReadOnlyDictionary<string, string> rawToCanonical,
        IReadOnlyList<CanonicalDatabasePropagationInfo> canonicalDatabases,
        bool propagateChildSchemaProperties)
    {
        lock (repo.SyncLock)
        {
            var nodes = repo.NodesById.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
            InMemoryGraphMaintenance.CleanupAndPropagateDatabaseEntities(nodes, repo.RelationshipsByKey, rawToCanonical, canonicalDatabases, propagateChildSchemaProperties);
            RebuildNodeIndicesInPlace(repo, nodes);
            RebuildEdgeIndicesInPlace(repo);
        }

        return Task.CompletedTask;
    }

    public static Task<Dictionary<string, int>> GetNodesBreakdownAsync(this InMemoryGraphRepository repo, CancellationToken cancellationToken = default)
    {
        lock (repo.SyncLock)
        {
            var breakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in repo.NodesById.Values)
            {
                var label = node.Kind.ToCypherLabel();
                breakdown[label] = breakdown.GetValueOrDefault(label) + 1;
            }
            return Task.FromResult(breakdown);
        }
    }

    public static Task<(int NodesCount, int RelationshipsCount)> GetGraphCountsAsync(this InMemoryGraphRepository repo, CancellationToken cancellationToken = default)
    {
        lock (repo.SyncLock)
        {
            return Task.FromResult((repo.NodesById.Count, repo.AllRelationships.Count));
        }
    }

    public static async Task SaveToAsync(this InMemoryGraphRepository repo, IGraphClient targetClient, CancellationToken cancellationToken = default)
    {
        List<Node> nodesSnapshot;
        List<Relationship> relsSnapshot;

        lock (repo.SyncLock)
        {
            nodesSnapshot = repo.NodesById.Values.ToList();
            relsSnapshot = [.. repo.AllRelationships];
        }

        await targetClient.BeginBulkLoadAsync(cancellationToken);
        if (nodesSnapshot.Count > 0)
        {
            await targetClient.UploadNodesAsync(nodesSnapshot);
        }
        if (relsSnapshot.Count > 0)
        {
            await targetClient.UploadRelationshipsAsync(relsSnapshot);
        }
        await targetClient.CommitBulkLoadAsync(cancellationToken);
    }

    private static void RebuildNodeIndicesInPlace(InMemoryGraphRepository repo, Dictionary<string, Node> nodes)
    {
        repo.NodesById.Clear();
        repo.NodesByKind.Clear();
        repo.NodesByKindEnum.Clear();
        foreach (var node in nodes.Values)
        {
            repo.UpsertNodeUnlocked(node);
        }
    }

    private static void RebuildEdgeIndicesInPlace(InMemoryGraphRepository repo)
    {
        repo.OutgoingEdges.Clear();
        repo.IncomingEdges.Clear();
        repo.AllRelationships.Clear();
        foreach (var rel in repo.RelationshipsByKey.Values)
        {
            repo.AllRelationships.Add(rel);
            if (!repo.OutgoingEdges.TryGetValue(rel.From, out var outList))
            {
                outList = [];
                repo.OutgoingEdges[rel.From] = outList;
            }
            outList.Add(rel);

            if (!repo.IncomingEdges.TryGetValue(rel.To, out var inList))
            {
                inList = [];
                repo.IncomingEdges[rel.To] = inList;
            }
            inList.Add(rel);
        }
    }
}
