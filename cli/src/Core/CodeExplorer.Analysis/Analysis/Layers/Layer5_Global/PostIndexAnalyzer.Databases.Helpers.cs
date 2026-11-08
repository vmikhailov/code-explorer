using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    private const string DefaultRelational = "relational";
    private const string DefaultDatabase = "Database";

    internal static bool IsGenericOrLibraryDbIdStatic(
        string targetId,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        HashSet<string> implicitSchemaIds,
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
        if (!canonicalNodes.TryGetValue(targetId, out var info)) return true;
        var eng = info.Engine;
        var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
        var isImplicit = implicitSchemaIds.Contains(targetId);
        var isMultiDomainLibDb = multiDomainSharedLibDbIds.Contains(targetId);
        return string.IsNullOrWhiteSpace(sch) ||
               sch.Equals("default", StringComparison.OrdinalIgnoreCase) ||
               isImplicit ||
               isMultiDomainLibDb ||
               sch.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
               info.Name.Equals(eng, StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<(string? PrimaryRelationalEngine, HashSet<string> ImplicitSchemaIds, HashSet<string> RawImplicitDbIds)> QueryDatabasesAndBuildCanonicalNodesAsync(
        IGraphClient db,
        string widPrefix,
        HashSet<string> nonTestAccessedTargets,
        Dictionary<string, string> rawToCanonical,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        CancellationToken cancellationToken)
    {
        var (rawDatabases, dataSets) = await db.LoadRawDatabasesAndDataSetsAsync(cancellationToken);
        string? primaryRelationalEngine = null;
        var implicitCanonicalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var explicitCanonicalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rawImplicitDbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rawDatabases)
        {
            var dt = string.IsNullOrEmpty(row.DbClassification) ? DefaultRelational : row.DbClassification;
            if (!string.Equals(dt, DefaultRelational, StringComparison.OrdinalIgnoreCase)) continue;

            var eng = row.Engine ?? string.Empty;
            var nm = row.Name;
            var candidate = !string.IsNullOrWhiteSpace(eng) && !eng.Equals(DefaultDatabase, StringComparison.OrdinalIgnoreCase) && !eng.Equals("default", StringComparison.OrdinalIgnoreCase)
                ? eng
                : (nm.Contains('.') ? nm.Split('.', 2)[0] : nm);

            if (!string.IsNullOrWhiteSpace(candidate) &&
                !candidate.Equals(DefaultDatabase, StringComparison.OrdinalIgnoreCase) &&
                !candidate.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                !ResourceReconciliationService.IsGenericConfigKey(candidate) &&
                IsRelationalEngine(candidate))
            {
                primaryRelationalEngine = candidate;
                break;
            }
        }

        foreach (var row in rawDatabases)
        {
            var id = row.Id;
            var name = string.IsNullOrEmpty(row.Name) ? id : row.Name;
            var rawEngine = row.Engine;
            if (!IsLikelyDatabaseName(name, rawEngine) && !IsLikelyDatabaseName(id, rawEngine)) continue;

            var dPath = row.Path;
            var isDeclaredInProduction = !string.IsNullOrEmpty(dPath) && !WorkspaceConventions.IsTestFilePath(dPath);
            var isExplicitTest = row.IsTest ||
                                 string.Equals(row.Role, "Test", StringComparison.OrdinalIgnoreCase) ||
                                 WorkspaceConventions.IsTestFilePath(dPath);

            var hasNonTestAccess = (!isExplicitTest && isDeclaredInProduction) || nonTestAccessedTargets.Contains(id);
            if (!hasNonTestAccess)
            {
                continue;
            }

            var dbType = string.IsNullOrEmpty(row.DbClassification) ? DefaultDatabase : row.DbClassification;
            var rawSchema = row.Schema;

            var (cName, cType, cKey) = CanonicalizeDatabase(name, dbType, rawEngine, rawSchema, primaryRelationalEngine);
            var canonicalId = BuildCanonicalDatabaseId(id, cType, cKey, widPrefix);
            rawToCanonical[id] = canonicalId;
            nonTestAccessedTargets.Add(canonicalId);

            var isRawImplicit = row.IsImplicitSchema;
            var isRawExplicit = row.IsExplicitSchema || row.FromConfig;

            if (isRawImplicit && !isRawExplicit)
            {
                rawImplicitDbIds.Add(id);
            }

            if (isRawExplicit || (!isRawImplicit && !string.IsNullOrEmpty(rawSchema)))
            {
                explicitCanonicalIds.Add(canonicalId);
                implicitCanonicalIds.Remove(canonicalId);
            }
            else if (isRawImplicit && !explicitCanonicalIds.Contains(canonicalId))
            {
                implicitCanonicalIds.Add(canonicalId);
            }

            var engineToUse = !string.IsNullOrWhiteSpace(rawEngine) && !rawEngine.Equals(DefaultDatabase, StringComparison.OrdinalIgnoreCase)
                ? rawEngine
                : (cName.Contains('.') ? cName.Split('.', 2)[0] : cName);

            if (!canonicalNodes.TryGetValue(canonicalId, out var existing) ||
                (existing.Engine.Equals(existing.Name, StringComparison.OrdinalIgnoreCase) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)))
            {
                canonicalNodes[canonicalId] = (cName, cType, cKey, engineToUse);
            }
        }

        // Also map any dataset (schema) nodes
        foreach (var (dsId, dsName) in dataSets)
        {
            if (string.IsNullOrEmpty(dsId) || string.IsNullOrEmpty(dsName)) continue;
            if (!nonTestAccessedTargets.Contains(dsId)) continue;

            var targetEngine = !string.IsNullOrWhiteSpace(primaryRelationalEngine) ? primaryRelationalEngine : "PostgreSQL";
            var (cName, cType, cKey) = CanonicalizeDatabase(targetEngine, DefaultRelational, null, dsName, primaryRelationalEngine);
            var canonicalId = BuildCanonicalDatabaseId(dsId, cType, cKey, widPrefix);
            rawToCanonical[dsId] = canonicalId;

            var isImplicitDs = dsId.Contains(":db:default:dbo", StringComparison.OrdinalIgnoreCase) ||
                               dsName.Equals("dbo", StringComparison.OrdinalIgnoreCase) ||
                               dsName.Equals("default", StringComparison.OrdinalIgnoreCase);
            if (isImplicitDs)
            {
                rawImplicitDbIds.Add(dsId);
                if (!explicitCanonicalIds.Contains(canonicalId))
                {
                    implicitCanonicalIds.Add(canonicalId);
                }
            }
            else
            {
                explicitCanonicalIds.Add(canonicalId);
                implicitCanonicalIds.Remove(canonicalId);
            }

            if (!canonicalNodes.ContainsKey(canonicalId))
            {
                canonicalNodes[canonicalId] = (cName, cType, cKey, targetEngine);
            }
        }

        return (primaryRelationalEngine, implicitCanonicalIds, rawImplicitDbIds);
    }

    internal static Dictionary<string, HashSet<string>> PurgeRedundantAndTestDatabases(
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        Dictionary<string, string> rawToCanonical,
        List<((string Id, string Name) Owner, string CanonicalTarget, Relationship Rel, bool IsImplicitEdge)> projectDbEdges,
        Dictionary<string, Dictionary<string, string>> domainScopedRemap,
        Dictionary<string, string> globalConsolidationRemap,
        HashSet<string> implicitSchemaIds,
        HashSet<string> multiDomainSharedLibDbIds,
        HashSet<string> nonTestAccessedTargets)
    {
        var ownerConcreteEngines = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (owner, rawTarget, _, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;
            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
                target = dWin;
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
                target = win;

            if (!IsGenericOrLibraryDbIdStatic(target, canonicalNodes, implicitSchemaIds, multiDomainSharedLibDbIds) && !implicitSchemaIds.Contains(target) && !isImplicitEdge)
            {
                if (canonicalNodes.TryGetValue(target, out var tInfo))
                {
                    var targetEng = tInfo.Engine ?? (tInfo.Name.Contains('.') ? tInfo.Name.Split('.', 2)[0] : tInfo.Name);
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
        }

        var activelyNeededImplicitDbIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (owner, rawTarget, propsElem, isImplicitEdge) in projectDbEdges)
        {
            var domain = ResourceSemanticAnalyzer.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;
            var target = rawTarget;
            if (domainScopedRemap.TryGetValue(domain, out var dMap) && dMap.TryGetValue(target, out var dWin))
                target = dWin;
            else if (globalConsolidationRemap.TryGetValue(target, out var win))
                target = win;

            if (implicitSchemaIds.Contains(target) || isImplicitEdge)
            {
                if (canonicalNodes.TryGetValue(target, out var tInfo))
                {
                    var targetEng = tInfo.Engine ?? (tInfo.Name.Contains('.') ? tInfo.Name.Split('.', 2)[0] : tInfo.Name);
                    var hasConcrete = ownerConcreteEngines.TryGetValue(owner.Id, out var oEngs) && oEngs.Contains(targetEng);
                    if (!hasConcrete)
                    {
                        activelyNeededImplicitDbIds.Add(target);
                    }
                }
            }
        }

        // Remove any generic engine-only or library-only database nodes when concrete database nodes exist
        var concreteEngines = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                var isImplicit = implicitSchemaIds.Contains(id);
                var isMultiDomainLibDb = multiDomainSharedLibDbIds.Contains(id);
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !isImplicit &&
                       !isMultiDomainLibDb;
            })
            .Select(kvp => kvp.Value.Engine)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genericOrLibraryDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var s = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                var isImplicit = implicitSchemaIds.Contains(id);
                var isMultiDomainLib = multiDomainSharedLibDbIds.Contains(id);
                var isEngineOnlyOrLibrary = string.IsNullOrWhiteSpace(s) ||
                                           s.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                           isMultiDomainLib ||
                                           info.Name.Equals(info.Engine, StringComparison.OrdinalIgnoreCase);

                if (isEngineOnlyOrLibrary && concreteEngines.Contains(info.Engine)) return true;
                if (isImplicit && concreteEngines.Contains(info.Engine) && !activelyNeededImplicitDbIds.Contains(id)) return true;
                return false;
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var purgeId in genericOrLibraryDbsToPurge)
        {
            canonicalNodes.Remove(purgeId);
            rawToCanonical.Remove(purgeId);
        }

        // Remove any abstract engine-only database nodes
        var abstractDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                return IsAbstractEngineOnlyDatabase(info.Name, sch, info.DbType, id);
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var purgeId in abstractDbsToPurge)
        {
            canonicalNodes.Remove(purgeId);
            rawToCanonical.Remove(purgeId);
        }

        // Remove any test database nodes
        var testDbsToPurge = canonicalNodes
            .Where(kvp => {
                var (id, info) = kvp;
                var sch = info.Name.Contains('.') ? info.Name.Split('.', 2)[1] : "";
                var isTestName = sch.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 sch.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                                 info.Name.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 info.Name.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
                return isTestName && !nonTestAccessedTargets.Contains(id);
            })
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var purgeId in testDbsToPurge)
        {
            canonicalNodes.Remove(purgeId);
            rawToCanonical.Remove(purgeId);
        }

        return ownerConcreteEngines;
    }

    internal static async Task<int> UploadCanonicalDatabaseNodesAsync(
        IGraphClient db,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes)
    {
        var nodesToUpload = new List<Node>();
        foreach (var (canonicalId, (cName, cType, _, engineToUse)) in canonicalNodes)
        {
            var dispName = cName.Contains('.')
                ? (cName.Equals("SQLite.main", StringComparison.OrdinalIgnoreCase) ? "SQLite" : cName)
                : (!string.IsNullOrWhiteSpace(engineToUse) && !engineToUse.Equals(cName, StringComparison.OrdinalIgnoreCase)
                    ? $"{cName} ({engineToUse}) [{cType}]"
                    : $"{cName} [{cType}]");

            var schemaToUse = cName.Contains('.') ? cName.Split('.', 2)[1] : "";

            var dbNode = new DatabaseNode(canonicalId, cName, string.Empty, EnumExtensions.ParseDatabaseType(cType))
            {
                DisplayName = dispName,
                Role = "database",
                Engine = engineToUse,
                Technology = engineToUse,
                Schema = schemaToUse,
                IsCanonical = true,
                IsSemanticEntity = true,
                Layer = StandardLayers.Foundation.LayerId,
                LayerId = StandardLayers.Foundation.LayerId,
                LayerName = StandardLayers.Foundation.LayerName,
                LayerColor = StandardLayers.Foundation.Color,
                LayerIcon = StandardLayers.Foundation.Icon
            };
            nodesToUpload.Add(Node.FromNode(dbNode));
        }

        if (nodesToUpload.Count > 0)
        {
            await db.UploadNodesAsync(nodesToUpload);
        }

        return nodesToUpload.Count;
    }

    internal static async Task<int> UploadProjectDatabaseRelationshipsAsync(
        IGraphClient db,
        List<((string Id, string Name) Owner, string CanonicalTarget, Relationship Rel, bool IsImplicitEdge)> projectDbEdges,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        Dictionary<string, HashSet<string>> ownerConcreteEngines,
        Dictionary<string, Dictionary<string, string>> domainScopedRemap,
        Dictionary<string, string> globalConsolidationRemap,
        HashSet<string> implicitSchemaIds,
        Dictionary<string, int> concreteEnginesCount,
        List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)> projectList,
        CancellationToken cancellationToken,
        Dictionary<string, string>? projectToWorkloadMap = null)
    {
        var relationshipsToUpload = new List<Relationship>();
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

            if (!canonicalNodes.ContainsKey(target))
            {
                continue;
            }

            var targetNode = canonicalNodes.TryGetValue(target, out var tInfo) ? tInfo : default;
            var targetEng = targetNode.Engine ?? (targetNode.Name?.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode.Name ?? "");
            var isImplicit = implicitSchemaIds.Contains(target) || isImplicitEdge;
            var hasConcreteForEngine = ownerConcreteEngines.TryGetValue(owner.Id, out var oEngs) && oEngs.Contains(targetEng);

            if (isImplicit && hasConcreteForEngine)
            {
                continue;
            }
            if (IsGenericOrLibraryDbIdStatic(target, canonicalNodes, implicitSchemaIds, new HashSet<string>()) && !isImplicit && concreteEnginesCount.GetValueOrDefault(targetEng) > 0)
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

                relationshipsToUpload.Add(created);
            }
        }

        var remaps = new List<DomainEdgeTargetRemap>();
        foreach (var (domain, dMap) in domainScopedRemap)
        {
            var domProjIds = projectList.Where(p => {
                var d = ResourceSemanticAnalyzer.CleanProjectNameToDomain(p.Name);
                if (string.IsNullOrEmpty(d)) d = p.Id;
                return d.Equals(domain, StringComparison.OrdinalIgnoreCase);
            }).Select(p => p.Id).ToList();

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

        await db.ReplaceProjectDatabaseRelationshipsAndRemapAsync(relationshipsToUpload, remaps, cancellationToken);

        return relationshipsToUpload.Count;
    }

    internal static async Task CleanupAndPropagateDatabaseEntitiesAsync(
        IGraphClient db,
        Dictionary<string, string> rawToCanonical,
        Dictionary<string, (string Name, string DbType, string Key, string Engine)> canonicalNodes,
        CancellationToken cancellationToken)
    {
        var propagationInfos = canonicalNodes
            .Select(kvp => new CanonicalDatabasePropagationInfo(kvp.Key, kvp.Value.Name, kvp.Value.Engine))
            .ToList();

        await db.CleanupAndPropagateDatabaseEntitiesAsync(
            rawToCanonical,
            propagationInfos,
            propagateChildSchemaProperties: true,
            cancellationToken);
    }

    internal static async Task<List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)>> QueryProjectsForCanonicalizationAsync(
        IGraphClient db,
        CancellationToken cancellationToken)
    {
        var rawProjects = await db.LoadProjectsForDbCanonicalizationAsync(cancellationToken);
        var projectList = new List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)>();
        foreach (var row in rawProjects)
        {
            var pId = row.Id;
            var pName = row.Name;
            var pPath = row.Path;
            var pRole = row.Role ?? string.Empty;
            var isTestProp = row.IsTestProp ?? string.Empty;
            var entityKind = row.EntityRole ?? string.Empty;

            var isTest = string.Equals(pRole, "Test", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(pRole, "TestSuite", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(isTestProp, "true", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(isTestProp, "1", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(entityKind, "Test", StringComparison.OrdinalIgnoreCase) ||
                         WorkspaceConventions.IsTestFilePath(pPath) ||
                         (!string.IsNullOrEmpty(pId) && (pId.Contains("/tests/", StringComparison.OrdinalIgnoreCase) || pId.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase) || pId.Contains(".Tests", StringComparison.OrdinalIgnoreCase)));

            var isLibrary = string.Equals(pRole, "Library", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(pRole, "SharedLibrary", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(entityKind, "Library", StringComparison.OrdinalIgnoreCase) ||
                            (!string.IsNullOrEmpty(pPath) && (pPath.Contains("/Infrastructure/", StringComparison.OrdinalIgnoreCase) || pPath.Contains("\\Infrastructure\\", StringComparison.OrdinalIgnoreCase)));

            if (!string.IsNullOrEmpty(pId))
            {
                projectList.Add((pId, pName, pPath, isTest, isLibrary));
            }
        }
        return projectList;
    }

    internal static async Task<HashSet<string>> QueryNonTestAccessedTargetsAsync(
        IGraphClient db,
        List<(string Id, string Name, string? Path, bool IsTest, bool IsLibrary)> projectList,
        CancellationToken cancellationToken)
    {
        var edges = await db.LoadAccessEdgesForDbCanonicalizationAsync(cancellationToken);
        var nonTestAccessedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in edges)
        {
            var src = row.Source;
            var tgt = row.Target;
            var kind = row.Kind;
            if (string.IsNullOrEmpty(src) || string.IsNullOrEmpty(tgt)) continue;

            var isReverse = kind is RelationshipKind.AccessedBy
                            or RelationshipKind.QueriedBy
                            or RelationshipKind.DeclaredIn;

            var dbTarget = isReverse ? src : tgt;
            var callerSrc = isReverse ? tgt : src;
            var callerRole = (isReverse ? row.TgtRole : row.SrcRole) ?? string.Empty;
            var callerPath = (isReverse ? row.TgtPath : row.SrcPath) ?? string.Empty;
            var callerIsTest = (isReverse ? row.TgtIsTest : row.SrcIsTest) ?? string.Empty;

            var matchingProj = projectList.FirstOrDefault(p => p.Id == callerSrc || callerSrc.StartWithIC(p.Id) || (!string.IsNullOrEmpty(p.Path) && callerSrc.Contains(p.Path, StringComparison.OrdinalIgnoreCase)));
            var isTestSrc = string.Equals(callerRole, "Test", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(callerRole, "TestSuite", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(callerIsTest, "true", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(callerIsTest, "1", StringComparison.OrdinalIgnoreCase) ||
                            WorkspaceConventions.IsTestFilePath(callerPath) ||
                            WorkspaceConventions.IsTestFilePath(callerSrc) ||
                            (matchingProj != default && matchingProj.IsTest);

            if (!isTestSrc)
            {
                nonTestAccessedTargets.Add(dbTarget);
            }
        }
        return nonTestAccessedTargets;
    }
}
