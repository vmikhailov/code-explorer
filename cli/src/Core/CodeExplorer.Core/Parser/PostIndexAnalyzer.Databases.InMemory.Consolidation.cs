using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Layers;
using CodeExplorer.Cypher.Common;

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

                var targetDb = allDbNodes.FirstOrDefault(d => d.Id == rel.To || rel.To.StartsWith(d.Id, StringComparison.OrdinalIgnoreCase));
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

            var targetDb = allDbNodes.FirstOrDefault(d => d.Id == rel.To || rel.To.StartsWith(d.Id, StringComparison.OrdinalIgnoreCase));
            var isConfigDb = targetDb?.Extensions != null && targetDb.Extensions.TryGetValue("from_config", out var fc) && fc == "true";
            if (!isConfigDb || targetDb == null) continue;

            var owner = projects.FirstOrDefault(p => p.Id == rel.From || rel.From.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase))
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
        Func<string, string?, bool> isTestSource)
    {
        // 2. Gather project references and consolidate databases per service/project
        var libraryProjects = projects.Where(p => 
            p.Role is "Library" or "SharedLibrary" ||
            p.IsLibrary ||
            p.EntityKind == ProjectEntityKind.Library ||
            p.Path.Contains("/Infrastructure/", StringComparison.OrdinalIgnoreCase) ||
            p.Path.Contains("\\Infrastructure\\", StringComparison.OrdinalIgnoreCase)
        ).ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);

        var serviceToReferencedLibs = new Dictionary<string, List<ProjectNode>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in allRelationships.Where(r => r.Kind == OntologyConstants.Relationships.DependsOn))
        {
            if (libraryProjects.TryGetValue(rel.To, out var libProj))
            {
                if (!serviceToReferencedLibs.TryGetValue(rel.From, out var libList))
                {
                    libList = [];
                    serviceToReferencedLibs[rel.From] = libList;
                }
                libList.Add(libProj);
            }
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

        bool IsGenericOrLibraryDbId(string targetId)
        {
            if (targetId.EndsWith(":relational:postgresql", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":document:mongodb", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":keyvalue:redis", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":relational:database", StringComparison.OrdinalIgnoreCase) ||
                targetId.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            var node = canonicalDbNodes.GetValueOrDefault(targetId);
            if (node == null) return true;
            var eng = node.Extensions?.GetValueOrDefault("engine") ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[0] : node.Name);
            var sch = node.Extensions?.GetValueOrDefault("schema") ?? (node.Name.Contains('.') ? node.Name.Split('.', 2)[1] : "");
            return string.IsNullOrWhiteSpace(sch) ||
                   sch.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                   sch.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase) ||
                   sch.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                   node.Name.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                   node.Name.EndsWith(".integrationeventlogef", StringComparison.OrdinalIgnoreCase);
        }

        var concreteEnginesCount = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                return !string.IsNullOrWhiteSpace(s) &&
                       !s.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                       !s.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase);
            })
            .GroupBy(n => n.Extensions?.GetValueOrDefault("engine") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var projectDbEdges = new List<(ProjectNode Owner, string CanonicalTarget, Relationship OriginalRel)>();

        foreach (var rel in allRelationships)
        {
            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          rawToCanonical.ContainsKey(rel.To);
            if (!isDbRel) continue;

            if (rawToCanonical.TryGetValue(rel.To, out var canonicalTarget))
            {
                var owner = projects.FirstOrDefault(p => p.Id == rel.From || rel.From.StartsWith(p.Id, StringComparison.OrdinalIgnoreCase))
                            ?? (fileToProject.TryGetValue(rel.From, out var fp) ? fp : null);

                if (owner != null && owner.Id != canonicalTarget)
                {
                    if (isTestSource(owner.Id, owner.Path))
                    {
                        continue;
                    }

                    var isMultiDomainLib = multiDomainSharedLibs.Contains(owner.Id);
                    var isGenericTarget = IsGenericOrLibraryDbId(canonicalTarget);

                    // A shared library referenced by >1 distinct service domains that has no database of its own
                    // must not create a project-level USES_DB edge to a generic db or service db.
                    if (!isMultiDomainLib || !isGenericTarget)
                    {
                        projectDbEdges.Add((owner, canonicalTarget, rel));
                    }

                    // Also attribute this DB target to any service that references this owner library
                    // BUT: when owner is a multi-domain shared library with a generic target, do NOT propagate it
                    // because each calling service domain will use its own database!
                    if (!isMultiDomainLib || !isGenericTarget)
                    {
                        foreach (var (svcId, libs) in serviceToReferencedLibs)
                        {
                            if (libs.Any(l => l.Id == owner.Id))
                            {
                                var svcProj = projects.FirstOrDefault(p => p.Id == svcId);
                                if (svcProj != null && !isTestSource(svcProj.Id, svcProj.Path))
                                {
                                    projectDbEdges.Add((svcProj, canonicalTarget, rel));
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

            var localRemap = ResolveDatabaseConsolidations(candidateNodes);
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

        // Remove any generic engine-only or library-only database nodes when concrete database nodes exist
        var concreteEngines = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                return !string.IsNullOrWhiteSpace(s) && !s.Equals("default", StringComparison.OrdinalIgnoreCase);
            })
            .Select(n => n.Extensions?.GetValueOrDefault("engine") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var genericOrLibraryDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var eng = n.Extensions?.GetValueOrDefault("engine") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[0] : n.Name);
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isGenericOrLibrary = string.IsNullOrWhiteSpace(s) ||
                                         s.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                         s.Equals("integrationeventlogef", StringComparison.OrdinalIgnoreCase) ||
                                         n.Name.Equals(eng, StringComparison.OrdinalIgnoreCase) ||
                                         n.Name.EndsWith(".integrationeventlogef", StringComparison.OrdinalIgnoreCase);
                return isGenericOrLibrary && concreteEngines.Contains(eng);
            })
            .ToList();

        foreach (var purgeNode in genericOrLibraryDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        // Remove any abstract engine-only database nodes
        var abstractDbsToPurge = canonicalDbNodes.Values
            .Where(n => IsAbstractEngineOnlyDatabase(n.Name, n.Extensions?.GetValueOrDefault("schema"), n.DbType, n.Id))
            .ToList();

        foreach (var purgeNode in abstractDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        // Remove any test database nodes (name/schema ends with -test, _test, or test config with 0 non-test callers)
        var testDbsToPurge = canonicalDbNodes.Values
            .Where(n => {
                var s = n.Extensions?.GetValueOrDefault("schema") ?? (n.Name.Contains('.') ? n.Name.Split('.', 2)[1] : "");
                var isTestName = s.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 s.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("-test", StringComparison.OrdinalIgnoreCase) ||
                                 n.Name.EndsWith("_test", StringComparison.OrdinalIgnoreCase);
                var isExplicitTest = (n.Extensions != null && n.Extensions.TryGetValue("is_test", out var it) && (it == "true" || it == "1")) ||
                                     (n.Extensions != null && n.Extensions.TryGetValue("role", out var r) && r.Equals("Test", StringComparison.OrdinalIgnoreCase)) ||
                                     WorkspaceConventions.IsTestFilePath(n.Path);
                return (isTestName || isExplicitTest) && !nonTestAccessedTargets.Contains(n.Id);
            })
            .ToList();

        foreach (var purgeNode in testDbsToPurge)
        {
            canonicalDbNodes.Remove(purgeNode.Id);
            rawToCanonical.Remove(purgeNode.Id);
        }

        foreach (var cNode in canonicalDbNodes.Values)
        {
            var eng = cNode.Extensions?.GetValueOrDefault("engine") ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[0] : cNode.Name);
            var sch = cNode.Extensions?.GetValueOrDefault("schema") ?? (cNode.Name.Contains('.') ? cNode.Name.Split('.', 2)[1] : "");

            cNode.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
            foreach (var child in cNode.Children)
            {
                if (child.Extensions != null)
                {
                    child.Extensions["engine"] = eng;
                    if (!string.IsNullOrEmpty(sch)) child.Extensions["schema"] = sch;
                }
                if (child is DataSetNode ds)
                {
                    ds.Children.RemoveAll(c => c is TableNode tn && !IsValidTableIdentifier(tn.Name));
                    foreach (var tc in ds.Children)
                    {
                        if (tc.Extensions != null)
                        {
                            tc.Extensions["engine"] = eng;
                            tc.Extensions["schema"] = !string.IsNullOrEmpty(ds.Name) ? ds.Name : sch;
                        }
                    }
                }
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
        }

        if (canonicalDbNodes.Count > 0 && ctx.DbClient != null)
        {
            ctx.LogDebug($"[PostIndexAnalyzer] Materializing {canonicalDbNodes.Count} canonical Database nodes into graph...");
            await ctx.DbClient.UploadNodesAsync([.. canonicalDbNodes.Values.Select(Node.FromNode)]);
            ctx.AddNodesCount(canonicalDbNodes.Count);
        }

        // 4. Link projects to canonical database nodes
        var canonicalRels = new List<Relationship>();
        var seen = new HashSet<(string From, string To)>();

        foreach (var (owner, rawTarget, rel) in projectDbEdges)
        {
            var domain = SyntaxEnricher.CleanProjectNameToDomain(owner.Name);
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
            var targetEng = targetNode?.Extensions?.GetValueOrDefault("engine") ?? (targetNode?.Name.Contains('.') == true ? targetNode.Name.Split('.', 2)[0] : targetNode?.Name ?? "");
            if (IsGenericOrLibraryDbId(target) && concreteEnginesCount.GetValueOrDefault(targetEng) > 0)
            {
                continue;
            }

            if (owner.Id != target && seen.Add((owner.Id, target)))
            {
                var props = new Dictionary<string, object>
                {
                    ["dependency_type"] = "database",
                    ["is_semantic"] = "true",
                    ["is_canonical"] = "true"
                };
                if (rel.Properties != null)
                {
                    foreach (var (k, v) in rel.Properties)
                    {
                        if (v != null && !props.ContainsKey(k))
                        {
                            props[k] = v;
                        }
                    }
                }
                canonicalRels.Add(new Relationship(
                    owner.Id,
                    target,
                    OntologyConstants.Relationships.UsesDb,
                    props
                ));
            }
            else if (rel.Properties != null)
            {
                var existing = canonicalRels.FirstOrDefault(r => r.From == owner.Id && r.To == target);
                if (existing?.Properties != null)
                {
                    foreach (var (k, v) in rel.Properties)
                    {
                        if (v != null && !existing.Properties.ContainsKey(k))
                        {
                            existing.Properties[k] = v;
                        }
                    }
                }
            }
        }

        if (canonicalRels.Count > 0 && ctx.DbClient != null)
        {
            ctx.LogDebug($"[PostIndexAnalyzer] Materializing {canonicalRels.Count} direct project->canonical database relationships...");
            await ctx.DbClient.UploadRelationshipsAsync(canonicalRels);
            ctx.AddRelsCount(canonicalRels.Count);
        }

        if (ctx.DbClient != null)
        {
            // Apply domain-scoped remappings to edges
            foreach (var (domain, dMap) in domainScopedRemap)
            {
                var domProjs = projectsByDomain.GetValueOrDefault(domain, []);
                var domProjIds = domProjs.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var (loserId, winnerId) in dMap)
                {
                    foreach (var pId in domProjIds)
                    {
                        await ctx.DbClient.ExecuteWriteAsync(
                            "UPDATE OR IGNORE edges SET to_id = @winnerId WHERE to_id = @loserId AND from_id LIKE @pIdPrefix;",
                            new { winnerId, loserId, pIdPrefix = $"{pId}%" });
                    }
                }
            }

            // Clean up duplicate non-canonical database nodes
            foreach (var (rawId, canonicalId) in rawToCanonical)
            {
                if (!string.Equals(rawId, canonicalId, StringComparison.Ordinal))
                {
                    await ctx.DbClient.ExecuteWriteAsync("UPDATE OR IGNORE edges SET to_id = @canonicalId WHERE to_id = @rawId;", new { canonicalId, rawId });
                    await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE to_id = @rawId;", new { rawId });
                    await ctx.DbClient.ExecuteWriteAsync("UPDATE OR IGNORE edges SET from_id = @canonicalId WHERE from_id = @rawId;", new { canonicalId, rawId });
                    await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE from_id = @rawId;", new { rawId });
                    await ctx.DbClient.ExecuteWriteAsync("DELETE FROM nodes WHERE id = @rawId;", new { rawId });
                }
            }

            // Remove any improper CONTAINS edges pointing to Database nodes (Databases are infrastructure, only contained in semantic_structure)
            await ctx.DbClient.ExecuteWriteAsync(
                "DELETE FROM edges WHERE kind = 'CONTAINS' AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database') AND from_id NOT LIKE '%semantic_structure%';");

            // Purge any remaining phantom Database nodes that were not canonicalized
            var validIds = canonicalDbNodes.Keys.ToList();
            if (validIds.Count > 0)
            {
                var placeholders = string.Join(",", validIds.Select((_, i) => $"@p{i}"));
                var paramDict = new Dictionary<string, object>();
                for (int i = 0; i < validIds.Count; i++) paramDict[$"p{i}"] = validIds[i];

                await ctx.DbClient.ExecuteWriteAsync(
                    $"DELETE FROM edges WHERE (to_id NOT IN ({placeholders}) AND to_id IN (SELECT id FROM nodes WHERE kind = 'Database')) OR (from_id NOT IN ({placeholders}) AND from_id IN (SELECT id FROM nodes WHERE kind = 'Database'));",
                    paramDict);
                await ctx.DbClient.ExecuteWriteAsync(
                    $"DELETE FROM nodes WHERE kind = 'Database' AND id NOT IN ({placeholders});",
                    paramDict);
            }
            else
            {
                await ctx.DbClient.ExecuteWriteAsync("DELETE FROM edges WHERE to_id IN (SELECT id FROM nodes WHERE kind = 'Database') OR from_id IN (SELECT id FROM nodes WHERE kind = 'Database');");
                await ctx.DbClient.ExecuteWriteAsync("DELETE FROM nodes WHERE kind = 'Database';");
            }
        }

        return canonicalRels;
    }
}
