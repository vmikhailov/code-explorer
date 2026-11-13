using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    internal static bool IsGenericOrLibraryDbIdInMemory(
        string targetId,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        HashSet<string> multiDomainSharedLibDbIds)
    {
        if (targetId.EndsWith(":relational:postgresql", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":relational:mysql", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":relational:h2", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":document:mongodb", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":keyvalue:redis", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":relational:database", StringComparison.OrdinalIgnoreCase) ||
            targetId.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        var node = canonicalDbNodes.GetValueOrDefault(targetId);
        if (node == null) return true;
        var eng = node.Engine ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[0] : node.Name);
        var sch = node.Schema ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[1] : "");
        var isImplicit = node.IsImplicitSchema == true || node.Flags.HasFlag(SymbolFlags.IsImplicitSchema);
        var isMultiDomainLibDb = multiDomainSharedLibDbIds.Contains(targetId);
        return string.IsNullOrWhiteSpace(sch) ||
               sch.Equals("default", StringComparison.OrdinalIgnoreCase) ||
               isImplicit ||
               isMultiDomainLibDb ||
               sch.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
               node.Name.Equals(eng, StringComparison.OrdinalIgnoreCase);
    }

    internal static Dictionary<string, HashSet<string>> PurgeRedundantAndTestDatabasesInMemory(
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        Dictionary<string, string> rawToCanonical,
        List<(ProjectNode Owner, string RawTarget, Relationship Rel, bool IsImplicitEdge)> projectDbEdges,
        Dictionary<string, Dictionary<string, string>> domainScopedRemap,
        Dictionary<string, string> globalConsolidationRemap,
        HashSet<string> implicitSchemaIds,
        HashSet<string> multiDomainSharedLibDbIds,
        HashSet<string> nonTestAccessedTargets)
    {
        var ownerConcreteEngines = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (owner, rawTarget, rel, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;
            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
                target = dWin;
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
                target = win;

            if (!IsGenericOrLibraryDbIdInMemory(target, canonicalDbNodes, multiDomainSharedLibDbIds) && !implicitSchemaIds.Contains(target) && !isImplicitEdge)
            {
                var targetNode = canonicalDbNodes.GetValueOrDefault(target);
                var targetEng = targetNode?.Engine ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
                if (!string.IsNullOrEmpty(targetEng))
                {
                    if (!ownerConcreteEngines.TryGetValue(owner.Id, out var set))
                    {
                        set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        ownerConcreteEngines[owner.Id] = set;
                    }
                    set.Add(targetEng);
                }
            }
        }

        var activelyNeededImplicitDbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (owner, rawTarget, rel, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;
            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
                target = dWin;
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
                target = win;

            if (implicitSchemaIds.Contains(target) || isImplicitEdge || multiDomainSharedLibDbIds.Contains(target))
            {
                var targetNode = canonicalDbNodes.GetValueOrDefault(target);
                var targetEng = targetNode?.Engine ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
                var hasConcrete = ownerConcreteEngines.TryGetValue(owner.Id, out var oEngs) && oEngs.Contains(targetEng);
                if (!hasConcrete && !owner.IsLibrary)
                {
                    activelyNeededImplicitDbIds.Add(target);
                }
            }
        }

        // Remove any generic engine-only or library-only database nodes when concrete database nodes exist
        var concreteEngines = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Schema ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isImplicit = implicitSchemaIds.Contains(n.Id);
                var isMultiDomainLib = multiDomainSharedLibDbIds.Contains(n.Id);
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !isImplicit &&
                       !isMultiDomainLib;
            })
            .Select(n => n.Engine ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genericOrLibraryDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var eng = n.Engine ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name);
                var s = n.Schema ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isImplicit = implicitSchemaIds.Contains(n.Id);
                var isMultiDomainLib = multiDomainSharedLibDbIds.Contains(n.Id);
                var isEngineOnly = string.IsNullOrWhiteSpace(s) ||
                                   s.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                   n.Name.Equals(eng, StringComparison.OrdinalIgnoreCase);

                if (isEngineOnly && concreteEngines.Contains(eng) && !activelyNeededImplicitDbIds.Contains(n.Id)) return true;
                if (isMultiDomainLib && concreteEngines.Contains(eng) && !activelyNeededImplicitDbIds.Contains(n.Id)) return true;
                if (isImplicit && concreteEngines.Contains(eng) && !activelyNeededImplicitDbIds.Contains(n.Id)) return true;
                return false;
            })
            .ToList();

        foreach (var purgeNode in genericOrLibraryDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        // Remove any abstract engine-only database nodes
        var abstractDbsToPurge = canonicalDbNodes.Values
            .Where(n => IsAbstractEngineOnlyDatabase(n.Name, n.Schema, n.DbType.ToCanonicalString(), n.Id))
            .ToList();

        foreach (var purgeNode in abstractDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        // Remove any test database nodes (name/schema ends with -test, _test, or test config with 0 non-test callers)
        var testDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Schema ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isTestName = s.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 s.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
                var isExplicitTest = n.IsTest == true ||
                                     string.Equals(n.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                                     WorkspaceConventions.IsTestFilePath(n.Path);
                return (isTestName || isExplicitTest) && !nonTestAccessedTargets.Contains(n.Id);
            })
            .ToList();

        foreach (var purgeNode in testDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        return ownerConcreteEngines;
    }

    internal static async Task PropagateSchemaAndUploadNodesInMemoryAsync(
        ParsingContext ctx,
        Layer4Result l4Result,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        string? primaryRelationalEngine)
    {
        var schemaNodesToUpload = new Dictionary<string, DbSchemaNode>(StringComparer.OrdinalIgnoreCase);
        var cloudNodesToUpload = new Dictionary<string, CloudNode>(StringComparer.OrdinalIgnoreCase);
        var schemaAndCloudEdges = new List<Relationship>();

        DbSchemaNode GetOrCreateDbSchema(DatabaseNode dbNode, string? eng, string? rawSchema)
        {
            var effectiveSchema = !string.IsNullOrWhiteSpace(rawSchema)
                ? rawSchema
                : ResolveDefaultDbSchemaName(eng);
            var isDefaultSchema = string.IsNullOrWhiteSpace(rawSchema) ||
                                  effectiveSchema.Equals("public", StringComparison.OrdinalIgnoreCase) ||
                                  effectiveSchema.Equals("dbo", StringComparison.OrdinalIgnoreCase) ||
                                  effectiveSchema.Equals("main", StringComparison.OrdinalIgnoreCase) ||
                                  effectiveSchema.Equals("default", StringComparison.OrdinalIgnoreCase);
            var schemaId = $"{dbNode.Id}:{OntologyConstants.IdPrefixes.DbSchema}:{CloudProviderHelper.NormalizeSlug(effectiveSchema)}";
            if (!schemaNodesToUpload.TryGetValue(schemaId, out var schemaNode))
            {
                schemaNode = new DbSchemaNode(schemaId, effectiveSchema, string.Empty)
                {
                    Engine = eng,
                    DatabaseId = dbNode.Id,
                    IsDefault = isDefaultSchema,
                    IsSemanticEntity = true,
                    Layer = StandardLayers.Foundation.LayerId,
                    LayerId = StandardLayers.Foundation.LayerId,
                    LayerName = StandardLayers.Foundation.LayerName,
                    LayerColor = StandardLayers.Foundation.Color,
                    LayerIcon = StandardLayers.Foundation.Icon
                };
                schemaNodesToUpload[schemaId] = schemaNode;
                schemaAndCloudEdges.Add(new Relationship(dbNode.Id, schemaId, RelationshipKind.Contains)
                {
                    IsSemantic = true,
                    IsCanonical = true
                });
            }
            return schemaNode;
        }

        foreach (var cNode in canonicalDbNodes.Values)
        {
            var eng = cNode.Engine ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[0] : cNode.Name);
            var sch = cNode.Schema ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[1] : "");
            var defaultSchemaNode = GetOrCreateDbSchema(cNode, eng, sch);
            if (string.IsNullOrWhiteSpace(cNode.Schema))
            {
                cNode.Schema = defaultSchemaNode.Name;
            }

            cNode.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
            var retainedDbChildren = new List<IOntologyNode>();
            foreach (var child in cNode.Children)
            {
                switch (child)
                {
                    case DataSetNode dsNode:
                        dsNode.Engine = eng;
                        if (!string.IsNullOrEmpty(sch)) dsNode.Schema = sch;
                        retainedDbChildren.Add(dsNode);
                        break;
                    case TableNode tblNode:
                        tblNode.Engine = eng;
                        tblNode.Schema = !string.IsNullOrEmpty(sch) ? sch : defaultSchemaNode.Name;
                        if (!defaultSchemaNode.Children.Any(x => x.Id == tblNode.Id))
                        {
                            defaultSchemaNode.Children.Add(tblNode);
                        }
                        schemaAndCloudEdges.Add(new Relationship(defaultSchemaNode.Id, tblNode.Id, RelationshipKind.Contains)
                        {
                            IsSemantic = true,
                            IsCanonical = true
                        });
                        break;
                    case ProcedureNode procNode:
                        procNode.Engine = eng;
                        procNode.Schema = !string.IsNullOrEmpty(sch) ? sch : defaultSchemaNode.Name;
                        if (!defaultSchemaNode.Children.Any(x => x.Id == procNode.Id))
                        {
                            defaultSchemaNode.Children.Add(procNode);
                        }
                        schemaAndCloudEdges.Add(new Relationship(defaultSchemaNode.Id, procNode.Id, RelationshipKind.Contains)
                        {
                            IsSemantic = true,
                            IsCanonical = true
                        });
                        break;
                    default:
                        retainedDbChildren.Add(child);
                        break;
                }
                if (child is DataSetNode ds)
                {
                    ds.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
                    var childSch = !string.IsNullOrEmpty(ds.Name) ? ds.Name : sch;
                    var dsSchemaNode = GetOrCreateDbSchema(cNode, eng, childSch);
                    foreach (var tc in ds.Children)
                    {
                        switch (tc)
                        {
                            case TableNode tblChild:
                                tblChild.Engine = eng;
                                tblChild.Schema = dsSchemaNode.Name;
                                if (!dsSchemaNode.Children.Any(x => x.Id == tblChild.Id))
                                {
                                    dsSchemaNode.Children.Add(tblChild);
                                }
                                schemaAndCloudEdges.Add(new Relationship(dsSchemaNode.Id, tblChild.Id, RelationshipKind.Contains)
                                {
                                    IsSemantic = true,
                                    IsCanonical = true
                                });
                                break;
                            case ProcedureNode procChild:
                                procChild.Engine = eng;
                                procChild.Schema = dsSchemaNode.Name;
                                if (!dsSchemaNode.Children.Any(x => x.Id == procChild.Id))
                                {
                                    dsSchemaNode.Children.Add(procChild);
                                }
                                schemaAndCloudEdges.Add(new Relationship(dsSchemaNode.Id, procChild.Id, RelationshipKind.Contains)
                                {
                                    IsSemantic = true,
                                    IsCanonical = true
                                });
                                break;
                        }
                    }
                }
            }

            cNode.Children.Clear();
            cNode.Children.AddRange(retainedDbChildren);
            foreach (var dbSch in schemaNodesToUpload.Values.Where(s => s.DatabaseId == cNode.Id))
            {
                if (!cNode.Children.Any(x => x.Id == dbSch.Id))
                {
                    cNode.Children.Add(dbSch);
                }
            }

            if (CloudProviderHelper.TryResolve(cNode.Name, string.IsNullOrWhiteSpace(cNode.Provider) ? eng : $"{eng} {cNode.Provider}") is { } cloudMatch)
            {
                var wsPrefix = cNode.Id.Contains(':') ? cNode.Id.Split(':', 2)[0] : OntologyConstants.IdPrefixes.Workspace;
                var cloudId = CloudProviderHelper.BuildCloudNodeId(wsPrefix, cloudMatch.ProviderId);
                if (!cloudNodesToUpload.ContainsKey(cloudId))
                {
                    cloudNodesToUpload[cloudId] = new CloudNode(cloudId, cloudMatch.DisplayName, string.Empty)
                    {
                        Provider = cloudMatch.ProviderId,
                        Category = cloudMatch.TierSlug,
                        IsSemanticEntity = true,
                        Layer = StandardLayers.Egress.LayerId,
                        LayerId = StandardLayers.Egress.LayerId,
                        LayerName = StandardLayers.Egress.LayerName,
                        LayerColor = StandardLayers.Egress.Color,
                        LayerIcon = StandardLayers.Egress.Icon
                    };
                }
                schemaAndCloudEdges.Add(new Relationship(cNode.Id, cloudId, RelationshipKind.HostedIn)
                {
                    Provider = cloudMatch.ProviderId,
                    IsSemantic = true,
                    IsCanonical = true
                });
            }
        }

        // 3. Add canonical nodes to SemanticStructure and upload
        var semanticStructure = ctx.SemanticStructure ?? l4Result.SemanticStructure;
        if (semanticStructure != null)
        {
            if (!string.IsNullOrWhiteSpace(primaryRelationalEngine))
            {
                semanticStructure.Children.RemoveAll(c => c is DatabaseNode dn &&
                    (string.Equals(dn.Name, "Database", StringComparison.OrdinalIgnoreCase) ||
                     dn.Id.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase) ||
                     dn.Id.EndsWith($":{OntologyConstants.IdPrefixes.Database}:relational:database", StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var node in canonicalDbNodes.Values)
            {
                if (!semanticStructure.Children.Any(c => c.Id == node.Id))
                {
                    semanticStructure.Children.Add(node);
                }
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, node.Name, node.Id);
            }

            foreach (var schNode in schemaNodesToUpload.Values)
            {
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.DbSchema, schNode.Name, schNode.Id);
            }

            foreach (var clNode in cloudNodesToUpload.Values)
            {
                if (!semanticStructure.Children.Any(c => c.Id == clNode.Id))
                {
                    semanticStructure.Children.Add(clNode);
                }
                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Cloud, clNode.Name, clNode.Id);
            }
        }

        if (canonicalDbNodes.Count > 0 && ctx.DbClient != null)
        {
            ctx.PostIndexLog.MaterializingCanonicalDatabaseNodes(canonicalDbNodes.Count);
            var uploadBatch = canonicalDbNodes.Values.Select(Node.FromNode)
                .Concat(schemaNodesToUpload.Values.Select(Node.FromNode))
                .Concat(cloudNodesToUpload.Values.Select(Node.FromNode))
                .ToList();
            await ctx.DbClient.UploadNodesAsync(uploadBatch);
            ctx.AddNodesCount(uploadBatch.Count);

            if (schemaAndCloudEdges.Count > 0)
            {
                await ctx.DbClient.UploadRelationshipsAsync(schemaAndCloudEdges);
                ctx.AddRelsCount(schemaAndCloudEdges.Count);
            }
        }
    }

    internal static async Task<List<Relationship>> UploadCanonicalRelationshipsInMemoryAsync(
        ParsingContext ctx,
        List<(ProjectNode Owner, string RawTarget, Relationship Rel, bool IsImplicitEdge)> projectDbEdges,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        Dictionary<string, HashSet<string>> ownerConcreteEngines,
        Dictionary<string, Dictionary<string, string>> domainScopedRemap,
        Dictionary<string, string> globalConsolidationRemap,
        HashSet<string> implicitSchemaIds,
        HashSet<string> multiDomainSharedLibDbIds,
        Dictionary<string, int> concreteEnginesCount,
        Dictionary<string, List<ProjectNode>> projectsByDomain,
        Dictionary<string, string>? projectToWorkloadMap = null)
    {
        var canonicalRels = new List<Relationship>();
        var seen = new HashSet<(string From, string To)>();

        foreach (var (owner, rawTarget, rel, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;

            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
            {
                target = dWin;
            }
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
            {
                target = win;
            }

            var targetNode = canonicalDbNodes.GetValueOrDefault(target);
            var targetEng = targetNode?.Engine ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
            var isImplicit = implicitSchemaIds.Contains(target) || isImplicitEdge;
            var hasConcreteForEngine = ownerConcreteEngines.TryGetValue(owner.Id, out var oEngs) && oEngs.Contains(targetEng);

            if (isImplicit && hasConcreteForEngine)
            {
                continue;
            }
            if (multiDomainSharedLibDbIds.Contains(target) && hasConcreteForEngine)
            {
                continue;
            }
            if (!multiDomainSharedLibDbIds.Contains(target) &&
                IsGenericOrLibraryDbIdInMemory(target, canonicalDbNodes, multiDomainSharedLibDbIds) &&
                !isImplicit &&
                concreteEnginesCount.GetValueOrDefault(targetEng) > 0)
            {
                continue;
            }

            var effectiveFrom = (projectToWorkloadMap != null && projectToWorkloadMap.TryGetValue(owner.Id, out var semFrom) && !string.IsNullOrEmpty(semFrom))
                ? semFrom
                : owner.Id;

            if (effectiveFrom != target && seen.Add((effectiveFrom, target)))
            {
                var created = new Relationship(effectiveFrom, target, OntologyConstants.Relationships.UsesDb)
                {
                    DependencyType = "database",
                    IsSemantic = true,
                    IsCanonical = true
                };
                EvidenceProvenanceHelper.MergeRelationships(created, rel);
                created.DependencyType = "database";
                created.IsSemantic = true;
                created.IsCanonical = true;
                if (!string.Equals(rel.From, effectiveFrom, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(rel.To, target, StringComparison.OrdinalIgnoreCase))
                {
                    EvidenceProvenanceHelper.RecordContributingEdge(created, rel);
                }
                canonicalRels.Add(created);
            }
            else
            {
                var existing = canonicalRels.FirstOrDefault(r => r.From == effectiveFrom && r.To == target);
                if (existing != null)
                {
                    EvidenceProvenanceHelper.MergeRelationships(existing, rel);
                    if (!string.Equals(rel.From, effectiveFrom, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(rel.To, target, StringComparison.OrdinalIgnoreCase))
                    {
                        EvidenceProvenanceHelper.RecordContributingEdge(existing, rel);
                    }
                }
            }
        }

        if (ctx.DbClient != null)
        {
            if (canonicalRels.Count > 0)
            {
                ctx.PostIndexLog.MaterializingCanonicalDatabaseRelationships(canonicalRels.Count);
                ctx.AddRelsCount(canonicalRels.Count);
            }

            var remaps = new List<DomainEdgeTargetRemap>();
            foreach (var (domain, dMap) in domainScopedRemap)
            {
                var domProjs = projectsByDomain.GetValueOrDefault(domain, []);
                var domProjIds = domProjs.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var (loserId, winnerId) in dMap)
                {
                    foreach (var pId in domProjIds)
                    {
                        remaps.Add(new DomainEdgeTargetRemap(loserId, winnerId, $"{pId}%"));
                        if (projectToWorkloadMap != null && projectToWorkloadMap.TryGetValue(pId, out var wlId) && !string.IsNullOrEmpty(wlId))
                        {
                            remaps.Add(new DomainEdgeTargetRemap(loserId, winnerId, $"{wlId}%"));
                        }
                    }
                }
            }

            await ctx.DbClient.ReplaceProjectDatabaseRelationshipsAndRemapAsync(canonicalRels, remaps);
        }

        return canonicalRels;
    }

    internal static async Task CleanupDatabaseEntitiesInMemoryAsync(
        ParsingContext ctx,
        Dictionary<string, string> rawToCanonical,
        Dictionary<string, DatabaseNode> canonicalDbNodes)
    {
        if (ctx.DbClient == null) return;

        var rootDirName = Path.GetFileName(ctx.AbsoluteWorkspacePath.TrimEnd('/', '\\'));
        var orderedDbs = canonicalDbNodes
            .OrderBy(kvp =>
            {
                var dbName = kvp.Value.Name ?? "";
                var clean = dbName.Contains('.') ? dbName.Split('.', 2)[1] : dbName;
                return (!string.IsNullOrEmpty(rootDirName) && clean.Equals(rootDirName, StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
            })
            .Select(kvp => new CanonicalDatabasePropagationInfo(kvp.Key, kvp.Value.Name ?? "", kvp.Value.Engine ?? ""))
            .ToList();

        await ctx.DbClient.CleanupAndPropagateDatabaseEntitiesAsync(
            rawToCanonical,
            orderedDbs,
            propagateChildSchemaProperties: false);
    }
}
