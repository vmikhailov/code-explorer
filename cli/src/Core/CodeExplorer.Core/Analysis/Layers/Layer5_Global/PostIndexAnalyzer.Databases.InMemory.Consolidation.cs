using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    internal static void PruneUnverifiedConfigDbEdges(
        ParsingContext ctx,
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyList<FileNode> files,
        IReadOnlyDictionary<string, ProjectNode> fileToProject,
        List<Relationship> allRelationships,
        List<DatabaseNode> allDbNodes,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        Dictionary<string, string> rawToCanonical,
        Dictionary<string, List<ProjectNode>> projectsByDomain,
        Func<string, string?, bool> isTestSource)
    {
        var domainVerifiedEngines = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var domainVerifiedDbTypes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var domainVerifiedKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var domainHasCodeFiles = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var (domain, domainProjs) in projectsByDomain)
        {
            var domainProjIds = domainProjs.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Also include referenced projects via DEPENDS_ON within the domain or related
            foreach (var rel in allRelationships.Where(r => r.Kind == OntologyConstants.Relationships.DependsOn))
            {
                if (domainProjIds.Contains(rel.From)) domainProjIds.Add(rel.To);
            }

            var domainFileNodes = files.Where(f => fileToProject.TryGetValue(f.Id, out var p) && domainProjIds.Contains(p.Id) && !isTestSource(f.Id, f.Path)).ToList();
            var domainCodeFiles = domainFileNodes.Where(f =>
                !f.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) &&
                !f.Path.EndsWith(".config", StringComparison.OrdinalIgnoreCase)).ToList();

            domainHasCodeFiles[domain] = domainCodeFiles.Count > 0;

            var vEngines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vDbTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            domainVerifiedEngines[domain] = vEngines;
            domainVerifiedDbTypes[domain] = vDbTypes;
            domainVerifiedKeys[domain] = vKeys;

            if (domainCodeFiles.Count == 0)
            {
                // Synthetic / config-only test: all config DBs are considered verified
                continue;
            }

            // 1. AST usages from non-config database relationships
            foreach (var rel in allRelationships)
            {
                if (rel.Kind == OntologyConstants.Relationships.Contains) continue;
                var isDbUsageRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                                   rel.Kind == OntologyConstants.Relationships.ReadsFrom ||
                                   rel.Kind == OntologyConstants.Relationships.WritesTo ||
                                   rel.Kind == OntologyConstants.Relationships.QueriedBy;
                if (!isDbUsageRel) continue;

                var isFromDomain = domainProjIds.Contains(rel.From) ||
                                   (fileToProject.TryGetValue(rel.From, out var pOwner) && domainProjIds.Contains(pOwner.Id));
                if (!isFromDomain) continue;

                var targetDb = allDbNodes.FirstOrDefault(d => d.Id == rel.To || rel.To.StartWithIC(d.Id));
                var isFromConfig = (targetDb?.Extensions != null && targetDb.Extensions.TryGetValue("from_config", out var fc) && fc == "true") ||
                                   (rel.Properties != null && rel.Properties.TryGetValue("from_config", out var rfc) && rfc?.ToString() == "true");

                if (!isFromConfig && targetDb != null)
                {
                    var eng = targetDb.Extensions?.GetValueOrDefault("engine") ?? (targetDb.Name.Contains('.') ? targetDb.Name.Split('.', 2)[0] : targetDb.Name);
                    if (!string.IsNullOrWhiteSpace(eng) && !eng.Equals("Database", StringComparison.OrdinalIgnoreCase) && !eng.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        vEngines.Add(ResourceReconciliationService.NormalizeEngineName(eng));
                    }
                    if (!string.IsNullOrWhiteSpace(targetDb.DbType))
                    {
                        vDbTypes.Add(targetDb.DbType);
                    }
                }
            }

            // 2. Package references (NuGet/NPM) in projects of this domain
            foreach (var proj in domainProjs)
            {
                foreach (var pkg in proj.Children.OfType<PackageNode>())
                {
                    var pkgLower = pkg.Name.ToLowerInvariant();
                    if (pkgLower.Contains("npgsql") || pkgLower.Contains("postgres"))
                    {
                        vEngines.Add("PostgreSQL");
                        vDbTypes.Add("relational");
                    }
                    if (pkgLower.Contains("mongo"))
                    {
                        vEngines.Add("MongoDB");
                        vDbTypes.Add("document");
                    }
                    if (pkgLower.Contains("redis") || pkgLower.Contains("stackexchange.redis"))
                    {
                        vEngines.Add("Redis");
                        vDbTypes.Add("cache");
                    }
                    if (pkgLower.Contains("mysql") || pkgLower.Contains("pomelo"))
                    {
                        vEngines.Add("MySQL");
                        vDbTypes.Add("relational");
                    }
                    if (pkgLower.Contains("sqlclient") || pkgLower.Contains("sqlserver"))
                    {
                        vEngines.Add("SQL Server");
                        vDbTypes.Add("relational");
                    }
                    if (pkgLower.Contains("sqlite"))
                    {
                        vEngines.Add("SQLite");
                        vDbTypes.Add("relational");
                    }
                    if (pkgLower.Contains("marten"))
                    {
                        vEngines.Add("PostgreSQL");
                        vDbTypes.Add("relational");
                        vDbTypes.Add("document");
                    }
                    if (pkgLower.Contains("entityframeworkcore") || pkgLower.Contains("dapper"))
                    {
                        vDbTypes.Add("relational");
                    }
                }
            }

            // 3. Scan code file content for config keys or driver usage
            foreach (var cf in domainCodeFiles)
            {
                var absPath = Path.IsPathRooted(cf.Path)
                    ? cf.Path
                    : Path.Combine(ctx.AbsoluteWorkspacePath, cf.Path);

                if (File.Exists(absPath))
                {
                    try
                    {
                        var text = File.ReadAllText(absPath);
                        if (text.Contains("PostgresOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("PostgresOptions");
                            vEngines.Add("PostgreSQL");
                            vDbTypes.Add("relational");
                        }
                        if (text.Contains("MongoOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("MongoDB", StringComparison.OrdinalIgnoreCase) || text.Contains("MongoDbContext", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("MongoOptions");
                            vEngines.Add("MongoDB");
                            vDbTypes.Add("document");
                        }
                        if (text.Contains("RedisOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("StackExchange.Redis", StringComparison.OrdinalIgnoreCase) || text.Contains("ConnectionMultiplexer", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("RedisOptions");
                            vEngines.Add("Redis");
                            vDbTypes.Add("cache");
                        }
                        if (text.Contains("MySqlOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("MySql", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("MySqlOptions");
                            vEngines.Add("MySQL");
                            vDbTypes.Add("relational");
                        }
                        if (text.Contains("SqliteOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("SqliteOptions");
                            vEngines.Add("SQLite");
                            vDbTypes.Add("relational");
                        }
                        if (text.Contains("SqlServerOptions", StringComparison.OrdinalIgnoreCase) || text.Contains("SqlClient", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("SqlServerOptions");
                            vEngines.Add("SQL Server");
                            vDbTypes.Add("relational");
                        }
                        if (text.Contains("ConnectionStrings", StringComparison.OrdinalIgnoreCase) || text.Contains("GetConnectionString", StringComparison.OrdinalIgnoreCase))
                        {
                            vKeys.Add("ConnectionStrings");
                        }
                    }
                    catch { }
                }
            }
        }

        // Prune unverified config USES_DB edges
        var unverifiedEdges = new List<Relationship>();
        foreach (var rel in allRelationships)
        {
            if (rel.Kind != OntologyConstants.Relationships.UsesDb) continue;

            var targetDb = allDbNodes.FirstOrDefault(d => d.Id == rel.To || rel.To.StartWithIC(d.Id));
            var isConfigDb = targetDb?.Extensions != null && targetDb.Extensions.TryGetValue("from_config", out var fc) && fc == "true";
            if (!isConfigDb || targetDb == null) continue;

            var owner = projects.FirstOrDefault(p => p.Id == rel.From || rel.From.StartWithIC(p.Id))
                        ?? (fileToProject.TryGetValue(rel.From, out var fp) ? fp : null);
            if (owner == null) continue;

            var domain = SyntaxEnricher.CleanProjectNameToDomain(owner.Name);
            if (string.IsNullOrEmpty(domain)) domain = owner.Id;

            if (domainHasCodeFiles.TryGetValue(domain, out var hasFiles) && !hasFiles)
            {
                // Synthetic / config-only test project: keep
                continue;
            }

            var eng = targetDb.Extensions?.GetValueOrDefault("engine") ?? (targetDb.Name.Contains('.') ? targetDb.Name.Split('.', 2)[0] : targetDb.Name);
            var normEng = ResourceReconciliationService.NormalizeEngineName(eng);
            var key = targetDb.Extensions?.GetValueOrDefault("config_key");

            var vEngines = domainVerifiedEngines.GetValueOrDefault(domain, []);
            var vDbTypes = domainVerifiedDbTypes.GetValueOrDefault(domain, []);
            var vKeys = domainVerifiedKeys.GetValueOrDefault(domain, []);

            var isVerified = vEngines.Contains(normEng) ||
                             (!string.IsNullOrEmpty(key) && (vKeys.Contains(key) || key.Split([':', '.']).Any(part => vKeys.Contains(part)))) ||
                             (targetDb.DbType.Equals("relational", StringComparison.OrdinalIgnoreCase) && vDbTypes.Contains("relational"));

            if (!isVerified)
            {
                unverifiedEdges.Add(rel);
            }
        }

        if (unverifiedEdges.Count > 0)
        {
            foreach (var unv in unverifiedEdges)
            {
                allRelationships.Remove(unv);
                ctx.TreeRelationships.Remove(unv);
                ctx.GlobalProjectDependencies.Remove(unv);
            }

            // Remove any unverified config databases that now have 0 non-test incoming edges
            var remainingTargets = allRelationships.Where(r => r.Kind == OntologyConstants.Relationships.UsesDb).Select(r => r.To).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var orphanedConfigDbs = allDbNodes.Where(d => d.Extensions != null && d.Extensions.TryGetValue("from_config", out var fc) && fc == "true" && !remainingTargets.Contains(d.Id)).ToList();
            foreach (var oDb in orphanedConfigDbs)
            {
                allDbNodes.Remove(oDb);
                canonicalDbNodes.Remove(oDb.Id);
                if (rawToCanonical.TryGetValue(oDb.Id, out var cId))
                {
                    canonicalDbNodes.Remove(cId);
                    rawToCanonical.Remove(oDb.Id);
                }
            }
        }
    }

    internal static async Task<List<Relationship>> ConsolidateServiceDatabasesAsync(
        ParsingContext ctx,
        Layer4Result l4Result,
        IReadOnlyList<ProjectNode> projects,
        IReadOnlyDictionary<string, ProjectNode> fileToProject,
        List<Relationship> allRelationships,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        Dictionary<string, string> rawToCanonical,
        HashSet<string> nonTestAccessedTargets,
        string? primaryRelationalEngine,
        Dictionary<string, List<ProjectNode>> projectsByDomain,
        Func<string, string?, bool> isTestSource,
        HashSet<string>? rawImplicitDbIds = null)
    {
        // 2. Gather project references and consolidate databases per service/project
        var libraryProjects = projects.Where(p => 
            p.Role is "Library" or "SharedLibrary" ||
            p.IsLibrary ||
            p.EntityKind == ProjectEntityKind.Library ||
            p.Path.Contains("/Infrastructure/", StringComparison.OrdinalIgnoreCase) ||
            p.Path.Contains("\\Infrastructure\\", StringComparison.OrdinalIgnoreCase)
        ).ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);

        var directReferencedLibs = new Dictionary<string, List<ProjectNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in allRelationships.Where(r => r.Kind == OntologyConstants.Relationships.DependsOn))
        {
            if (libraryProjects.TryGetValue(rel.To, out var libProj))
            {
                if (!directReferencedLibs.TryGetValue(rel.From, out var libList))
                {
                    libList = [];
                    directReferencedLibs[rel.From] = libList;
                }
                if (!libList.Any(l => l.Id.Equals(libProj.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    libList.Add(libProj);
                }
            }
        }

        // Compute multi-hop transitive closure across library dependencies (e.g. Service -> Logic -> Core -> Models)
        var serviceToReferencedLibs = new Dictionary<string, List<ProjectNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (var startProjId in directReferencedLibs.Keys)
        {
            var visitedLibs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resultLibs = new List<ProjectNode>();
            var queue = new Queue<(ProjectNode Lib, int Depth)>();

            foreach (var directLib in directReferencedLibs[startProjId])
            {
                if (visitedLibs.Add(directLib.Id))
                {
                    resultLibs.Add(directLib);
                    queue.Enqueue((directLib, 1));
                }
            }

            while (queue.Count > 0)
            {
                var (currLib, depth) = queue.Dequeue();
                if (depth >= 8) continue;

                if (directReferencedLibs.TryGetValue(currLib.Id, out var nextLibs))
                {
                    foreach (var nextLib in nextLibs)
                    {
                        if (visitedLibs.Add(nextLib.Id))
                        {
                            resultLibs.Add(nextLib);
                            queue.Enqueue((nextLib, depth + 1));
                        }
                    }
                }
            }

            serviceToReferencedLibs[startProjId] = resultLibs;
        }

        // Detect libraries referenced by multiple distinct service domains (e.g. IntegrationEventLogEF)
        var libReferencingDomains = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (svcId, libs) in serviceToReferencedLibs)
        {
            var svcProj = projects.FirstOrDefault(p => p.Id == svcId) ?? (fileToProject.TryGetValue(svcId, out var fp) ? fp : null);
            if (svcProj == null || isTestSource(svcProj.Id, svcProj.Path)) continue;
            var dom = SyntaxEnricher.CleanProjectNameToDomain(svcProj.Name);
            if (string.IsNullOrEmpty(dom)) dom = svcProj.Id;
            foreach (var l in libs)
            {
                if (!libReferencingDomains.TryGetValue(l.Id, out var domSet))
                {
                    domSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    libReferencingDomains[l.Id] = domSet;
                }
                domSet.Add(dom);
            }
        }

        var multiDomainSharedLibs = libReferencingDomains
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => kvp.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var dbOwners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in allRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.Contains) continue;
            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          rawToCanonical.ContainsKey(rel.To);
            if (!isDbRel) continue;

            if (rawToCanonical.TryGetValue(rel.To, out var canonicalTarget))
            {
                var owner = projects.FirstOrDefault(p => p.Id == rel.From || rel.From.StartWithIC(p.Id))
                            ?? (fileToProject.TryGetValue(rel.From, out var fp) ? fp : null);
                var relFilePath = rel.Properties != null && rel.Properties.TryGetValue("file_path", out var rfp) ? rfp?.ToString() : null;
                if (owner != null && !isTestSource(owner.Id, owner.Path) && !isTestSource(rel.From, relFilePath))
                {
                    if (!dbOwners.TryGetValue(canonicalTarget, out var owners))
                    {
                        owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        dbOwners[canonicalTarget] = owners;
                    }
                    owners.Add(owner.Id);
                }
            }
        }

        var multiDomainSharedLibDbIds = canonicalDbNodes.Keys
            .Where(dbId => {
                if (dbOwners.TryGetValue(dbId, out var owners) && owners.Count > 0)
                {
                    return owners.All(oId => multiDomainSharedLibs.Contains(oId));
                }
                return false;
            })
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var implicitSchemaIds = canonicalDbNodes.Values
            .Where(n => n.Extensions?.GetValueOrDefault("is_implicit_schema") == "true")
            .Select(n => n.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool IsGenericOrLibraryDbId(string targetId) =>
            IsGenericOrLibraryDbIdInMemory(targetId, canonicalDbNodes, multiDomainSharedLibDbIds);

        var concreteEnginesCount = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isImplicit = n.Extensions?.GetValueOrDefault("is_implicit_schema") == "true";
                var isMultiDomainLibDb = multiDomainSharedLibDbIds.Contains(n.Id);
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !isImplicit &&
                       !isMultiDomainLibDb;
            })
            .GroupBy(n => n.Extensions?.GetValueOrDefault("engine") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var projectDbEdges = new List<(ProjectNode Owner, string CanonicalTarget, Relationship OriginalRel, bool IsImplicitEdge)>();

        // Pre-pass: determine which services already have their own single-domain concrete database per engine
        var serviceOwnDomainConcreteEngines = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        void RecordOwnConcreteEngine(string projId, string engine)
        {
            if (string.IsNullOrWhiteSpace(engine)) return;
            if (!serviceOwnDomainConcreteEngines.TryGetValue(projId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                serviceOwnDomainConcreteEngines[projId] = set;
            }
            set.Add(engine);
        }

        foreach (var rel in allRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.Contains) continue;
            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          rawToCanonical.ContainsKey(rel.To);
            if (!isDbRel) continue;
            if (!rawToCanonical.TryGetValue(rel.To, out var canonicalTarget)) continue;

            var owner = projects.FirstOrDefault(p => p.Id == rel.From || rel.From.StartWithIC(p.Id))
                        ?? (fileToProject.TryGetValue(rel.From, out var fp) ? fp : null);
            if (owner == null || owner.Id == canonicalTarget) continue;

            var relFilePath = rel.Properties != null && rel.Properties.TryGetValue("file_path", out var rfp) ? rfp?.ToString() : null;
            if (isTestSource(owner.Id, owner.Path) || isTestSource(rel.From, relFilePath)) continue;

            var isRawImplicitEdge = (rawImplicitDbIds != null && rawImplicitDbIds.Contains(rel.To)) ||
                                    (rel.Properties != null && rel.Properties.TryGetValue("is_implicit_schema", out var impObj) && impObj?.ToString() == "true") ||
                                    rel.To.EndsWith(":db:default:dbo", StringComparison.OrdinalIgnoreCase);
            if (isRawImplicitEdge || implicitSchemaIds.Contains(canonicalTarget) || multiDomainSharedLibs.Contains(owner.Id))
            {
                continue;
            }

            if (canonicalDbNodes.TryGetValue(canonicalTarget, out var cNode))
            {
                var eng = cNode.Extensions?.GetValueOrDefault("engine") ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[0] : cNode.Name);
                var sch = cNode.Extensions?.GetValueOrDefault("schema") ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[1] : "");
                if (!string.IsNullOrWhiteSpace(sch) && !sch.Equals("default", StringComparison.OrdinalIgnoreCase) && !sch.Equals(eng, StringComparison.OrdinalIgnoreCase))
                {
                    // Only the owner's direct edges count; library-derived edges must not mark consumers as owning a DB.
                    RecordOwnConcreteEngine(owner.Id, eng);
                }
            }
        }

        foreach (var rel in allRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.Contains) continue;
            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          rawToCanonical.ContainsKey(rel.To);
            if (!isDbRel) continue;

            if (rawToCanonical.TryGetValue(rel.To, out var canonicalTarget))
            {
                var owner = projects.FirstOrDefault(p => p.Id == rel.From || rel.From.StartWithIC(p.Id))
                            ?? (fileToProject.TryGetValue(rel.From, out var fp) ? fp : null);

                if (owner != null && owner.Id != canonicalTarget)
                {
                    var relFilePath = rel.Properties != null && rel.Properties.TryGetValue("file_path", out var rfp) ? rfp?.ToString() : null;
                    if (isTestSource(owner.Id, owner.Path) || isTestSource(rel.From, relFilePath))
                    {
                        continue;
                    }

                    var isRawImplicitEdge = (rawImplicitDbIds != null && rawImplicitDbIds.Contains(rel.To)) ||
                                            (rel.Properties != null && rel.Properties.TryGetValue("is_implicit_schema", out var impObj) && impObj?.ToString() == "true") ||
                                            rel.To.EndsWith(":db:default:dbo", StringComparison.OrdinalIgnoreCase);

                    var isLibraryOwner = libraryProjects.ContainsKey(owner.Id) || owner.IsLibrary;
                    if (isRawImplicitEdge && isLibraryOwner)
                    {
                        continue;
                    }

                    var isMultiDomainLib = multiDomainSharedLibs.Contains(owner.Id);
                    var isGenericTarget = IsGenericOrLibraryDbId(canonicalTarget) || isRawImplicitEdge;

                    var targetNode = canonicalDbNodes.GetValueOrDefault(canonicalTarget);
                    var targetEng = targetNode?.Extensions?.GetValueOrDefault("engine") ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
                    var targetSch = targetNode?.Extensions?.GetValueOrDefault("schema") ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[1] : "");
                    var isTrulyEngineOnlyTarget = string.IsNullOrWhiteSpace(targetSch) ||
                                                  targetSch.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                                  targetSch.Equals(targetEng, StringComparison.OrdinalIgnoreCase) ||
                                                  (targetNode != null && targetNode.Name.Equals(targetEng, StringComparison.OrdinalIgnoreCase));

                    // Preserve the edge on the owner unless it is a multi-domain shared library pointing to a generic/implicit target
                    // where all referencing services already have their own domain databases.
                    if (!isMultiDomainLib || !isGenericTarget || !isTrulyEngineOnlyTarget)
                    {
                        projectDbEdges.Add((owner, canonicalTarget, rel, isRawImplicitEdge));
                    }

                    if (!isRawImplicitEdge && rel.From.Contains(":f:", StringComparison.OrdinalIgnoreCase))
                    {
                        var normFrom = rel.From.Replace('\\', '/');
                        var binIdx = normFrom.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase);
                        if (binIdx >= 0)
                        {
                            var afterBin = normFrom[(binIdx + 5)..];
                            var slashIdx = afterBin.IndexOf('/');
                            if (slashIdx > 0)
                            {
                                var subBinName = afterBin[..slashIdx];
                                var matchingBinProj = projects.FirstOrDefault(p =>
                                    !p.IsLibrary && !p.IsTest && p.Id != owner.Id &&
                                    string.Equals(p.Name, subBinName, StringComparison.OrdinalIgnoreCase));
                                if (matchingBinProj != null && !isTestSource(matchingBinProj.Id, matchingBinProj.Path))
                                {
                                    projectDbEdges.Add((matchingBinProj, canonicalTarget, rel, false));
                                }
                            }
                        }
                    }

                    // Propagate library DB target to referencing services (multi-hop):
                    // If owner is a multi-domain shared library, only suppress propagation to services that ALREADY have
                    // their own single-domain concrete database for the same engine (e.g. eShop microservices referencing IntegrationEventLogEF).
                    foreach (var (svcId, libs) in serviceToReferencedLibs)
                    {
                        if (libs.Any(l => l.Id == owner.Id))
                        {
                            var svcProj = projects.FirstOrDefault(p => p.Id == svcId);
                            if (svcProj != null && !isTestSource(svcProj.Id, svcProj.Path))
                            {
                                var svcHasOwnDbForEngine = serviceOwnDomainConcreteEngines.TryGetValue(svcProj.Id, out var svcEngs) &&
                                                           svcEngs.Contains(targetEng);
                                // A service with its own concrete DB for this engine must not inherit a shared library's DB.
                                var skipLibInheritance = svcHasOwnDbForEngine && isLibraryOwner && !isTrulyEngineOnlyTarget;
                                if (!skipLibInheritance && (!isMultiDomainLib || !isGenericTarget || !svcHasOwnDbForEngine))
                                {
                                    projectDbEdges.Add((svcProj, canonicalTarget, rel, isRawImplicitEdge));
                                }
                            }
                        }
                    }
                }
            }
        }

        var edgesByService = projectDbEdges
            .GroupBy(e => {
                var domain = SyntaxEnricher.CleanProjectNameToDomain(e.Owner.Name);
                return !string.IsNullOrEmpty(domain) ? domain : e.Owner.Id;
            }, StringComparer.OrdinalIgnoreCase);

        var globalConsolidationRemap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var domainScopedRemap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var sGroup in edgesByService)
        {
            var domainKey = sGroup.Key;
            var candidateNodes = sGroup
                .Select(e => canonicalDbNodes.GetValueOrDefault(e.CanonicalTarget))
                .Where(n => n != null)
                .Select(n => {
                    var name = n!.Name;
                    var engine = (n.Extensions != null && n.Extensions.TryGetValue("engine", out var eng) && !string.IsNullOrWhiteSpace(eng))
                        ? eng
                        : (name.Contains('.') ? name.Split('.', 2)[0] : name);
                    var schema = (n.Extensions != null && n.Extensions.TryGetValue("schema", out var sc) && !string.IsNullOrWhiteSpace(sc))
                        ? sc
                        : (name.Contains('.') ? name.Split('.', 2)[1] : "");
                    return (n.Id, name, engine, schema);
                })
                .DistinctBy(t => t.Id)
                .ToList();

            var localRemap = ResolveDatabaseConsolidations(candidateNodes, implicitSchemaIds);
            foreach (var (loser, winner) in localRemap)
            {
                var loserNode = canonicalDbNodes.GetValueOrDefault(loser);
                var loserEng = loserNode?.Extensions?.GetValueOrDefault("engine") ?? (loserNode?.Name.Contains('.') == true ? loserNode.Name.Split('.', 2)[0] : loserNode?.Name ?? "");
                var isLoserGeneric = IsGenericOrLibraryDbId(loser) || IsAbstractEngineOnlyDatabase(loserNode?.Name, loserNode?.Extensions?.GetValueOrDefault("schema"), loserNode?.DbType, loser);

                if (isLoserGeneric)
                {
                    // Multi-database engine or abstract engine node: do NOT remap generic node globally! Scope to domain.
                    if (!domainScopedRemap.TryGetValue(domainKey, out var dMap))
                    {
                        dMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        domainScopedRemap[domainKey] = dMap;
                    }
                    dMap[loser] = winner;
                }
                else
                {
                    globalConsolidationRemap[loser] = winner;
                }
            }

            // Consolidate single configured database with AST schema nodes for the service
            foreach (var engGroup in candidateNodes.GroupBy(n => n.engine, StringComparer.OrdinalIgnoreCase))
            {
                var engList = engGroup.ToList();
                if (engList.Count <= 1) continue;

                var cfgNodes = engList.Where(n => {
                    var nodeObj = canonicalDbNodes.GetValueOrDefault(n.Id);
                    return nodeObj?.Extensions != null && nodeObj.Extensions.TryGetValue("from_config", out var fc) && fc == "true";
                }).ToList();

                var astNodes = engList.Where(n => !cfgNodes.Any(c => c.Id == n.Id)).ToList();

                if (cfgNodes.Count == 1 && astNodes.Count > 0)
                {
                    var cfg = cfgNodes[0];
                    foreach (var ast in astNodes)
                    {
                        var astStem = ResourceReconciliationService.StripDatabaseSuffix(ast.schema);
                        var cfgStem = ResourceReconciliationService.StripDatabaseSuffix(cfg.schema);
                        if ((string.Equals(astStem, cfgStem, StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(cfgStem, astStem + "s", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(cfgStem, astStem + "es", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(astStem, cfgStem + "s", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(astStem, cfgStem + "es", StringComparison.OrdinalIgnoreCase)) &&
                            !string.Equals(ast.schema, cfg.schema, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!cfg.schema.Equals(cfgStem, StringComparison.OrdinalIgnoreCase) && ast.schema.Equals(astStem, StringComparison.OrdinalIgnoreCase))
                            {
                                globalConsolidationRemap[cfg.Id] = ast.Id;
                            }
                            else
                            {
                                globalConsolidationRemap[ast.Id] = cfg.Id;
                            }
                        }
                    }
                }
            }
        }

        // Guard: globalConsolidationRemap must NEVER map an engine-only ID
        foreach (var k in globalConsolidationRemap.Keys.ToList())
        {
            var node = canonicalDbNodes.GetValueOrDefault(k);
            if (IsGenericOrLibraryDbId(k) || IsAbstractEngineOnlyDatabase(node?.Name, node?.Extensions?.GetValueOrDefault("schema"), node?.DbType, k))
            {
                globalConsolidationRemap.Remove(k);
            }
        }

        if (globalConsolidationRemap.Count > 0)
        {
            foreach (var (loserId, winnerId) in globalConsolidationRemap)
            {
                rawToCanonical[loserId] = winnerId;
            }

            foreach (var k in rawToCanonical.Keys.ToList())
            {
                var cur = rawToCanonical[k];
                if (globalConsolidationRemap.TryGetValue(cur, out var win))
                {
                    rawToCanonical[k] = win;
                }
            }

            foreach (var (loserId, winnerId) in globalConsolidationRemap)
            {
                if (canonicalDbNodes.TryGetValue(loserId, out var loserNode) &&
                    canonicalDbNodes.TryGetValue(winnerId, out var winnerNode))
                {
                    foreach (var child in loserNode.Children)
                    {
                        if (!winnerNode.Children.Any(c => c.Id == child.Id))
                        {
                            winnerNode.Children.Add(child);
                        }
                    }
                }
                canonicalDbNodes.Remove(loserId);
            }
        }

        var ownerConcreteEngines = PurgeRedundantAndTestDatabasesInMemory(
            canonicalDbNodes,
            rawToCanonical,
            projectDbEdges,
            domainScopedRemap,
            globalConsolidationRemap,
            implicitSchemaIds,
            multiDomainSharedLibDbIds,
            nonTestAccessedTargets);

        // 3. Propagate schema, add canonical nodes to SemanticStructure and upload
        await PropagateSchemaAndUploadNodesInMemoryAsync(ctx, l4Result, canonicalDbNodes, primaryRelationalEngine);

        var projectToWorkloadMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (l4Result.SemanticRelationships != null)
        {
            foreach (var r in l4Result.SemanticRelationships.Where(rel => rel.Kind == OntologyConstants.Relationships.Deploys))
            {
                projectToWorkloadMap[r.From] = r.To;
                projectToWorkloadMap[r.From.TrimEnd(':')] = r.To;
                projectToWorkloadMap[r.From.TrimEnd(':') + ":"] = r.To;
            }
        }

        // 4. Link projects to canonical database nodes and upload
        var canonicalRels = await UploadCanonicalRelationshipsInMemoryAsync(
            ctx,
            projectDbEdges,
            canonicalDbNodes,
            ownerConcreteEngines,
            domainScopedRemap,
            globalConsolidationRemap,
            implicitSchemaIds,
            multiDomainSharedLibDbIds,
            concreteEnginesCount,
            projectsByDomain,
            projectToWorkloadMap);

        // 5. Clean up duplicate non-canonical database nodes
        if (ctx.DbClient != null)
        {
            await CleanupDatabaseEntitiesInMemoryAsync(ctx, rawToCanonical, canonicalDbNodes);
        }

        return canonicalRels;
    }
}

