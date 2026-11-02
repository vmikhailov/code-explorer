using CodeExplorer.Core.Analysis.Layers.Layer4_Semantic.Analyzers;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Parser.Layers;

namespace CodeExplorer.Core.Parser;

public partial class PostIndexAnalyzer
{
    private static Dictionary<string, ProjectNode> BuildWorkloadToProjectMap(
        Layer4Result l4Result,
        IReadOnlyList<ProjectNode> projects)
    {
        var workloadToProject = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        if (l4Result.SemanticStructure != null)
        {
            foreach (var wl in l4Result.SemanticStructure.Children)
            {
                var projId = wl.Extensions?.GetValueOrDefault("project_id");
                var proj = !string.IsNullOrEmpty(projId) ? projects.FirstOrDefault(p => p.Id == projId) : null;
                if (proj != null)
                {
                    workloadToProject[wl.Id] = proj;
                }
            }
        }
        if (l4Result.SemanticRelationships != null)
        {
            foreach (var rel in l4Result.SemanticRelationships)
            {
                if (rel.Kind == OntologyConstants.Relationships.ImplementedBy)
                {
                    var proj = projects.FirstOrDefault(p => p.Id == rel.To || p.Id.TrimEnd(':') == rel.To.TrimEnd(':'));
                    if (proj != null) workloadToProject[rel.From] = proj;
                }
                else if (rel.Kind == OntologyConstants.Relationships.Deploys)
                {
                    var proj = projects.FirstOrDefault(p => p.Id == rel.From || p.Id.TrimEnd(':') == rel.From.TrimEnd(':'));
                    if (proj != null) workloadToProject[rel.To] = proj;
                }
            }
        }
        return workloadToProject;
    }

    private static (
        Dictionary<string, ProjectNode> LibraryProjects,
        Dictionary<string, List<ProjectNode>> ServiceToReferencedLibs,
        HashSet<string> MultiDomainSharedLibs)
    ComputeMultiDomainSharedLibs(
        IReadOnlyList<ProjectNode> projects,
        List<Relationship> allRelationships,
        Func<string, ProjectNode?> resolveOwner,
        Func<string, string?, bool> isTestSource)
    {
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
            var svcProj = resolveOwner(svcId);
            if (svcProj == null || isTestSource(svcProj.Id, svcProj.Path)) continue;
            var dom = ResourceSemanticAnalyzer.CleanProjectNameToDomain(svcProj.Name);
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

        return (libraryProjects, serviceToReferencedLibs, multiDomainSharedLibs);
    }

    private static (
        HashSet<string> MultiDomainSharedLibDbIds,
        HashSet<string> ImplicitSchemaIds,
        Dictionary<string, int> ConcreteEnginesCount,
        Dictionary<string, HashSet<string>> ServiceOwnDomainConcreteEngines)
    AnalyzeDatabaseOwnersAndEngines(
        List<Relationship> allRelationships,
        Dictionary<string, DatabaseNode> canonicalDbNodes,
        Dictionary<string, string> rawToCanonical,
        HashSet<string> multiDomainSharedLibs,
        HashSet<string>? rawImplicitDbIds,
        Func<string, ProjectNode?> resolveOwner,
        Func<string, string?, bool> isTestSource)
    {
        var dbOwners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var rel in allRelationships)
        {
            if (rel.Kind == OntologyConstants.Relationships.Contains) continue;
            var isReverseDbRel = rel.Kind == OntologyConstants.Relationships.AccessedBy ||
                                 rel.Kind == OntologyConstants.Relationships.QueriedBy ||
                                 rel.Kind == OntologyConstants.Relationships.DeclaredIn;
            var relDbId = isReverseDbRel ? rel.From : rel.To;
            var relCallerId = isReverseDbRel ? rel.To : rel.From;
            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          isReverseDbRel ||
                          rawToCanonical.ContainsKey(relDbId);
            if (!isDbRel) continue;

            if (rawToCanonical.TryGetValue(relDbId, out var canonicalTarget))
            {
                var owner = resolveOwner(relCallerId);
                var relFilePath = rel.Properties != null && rel.Properties.TryGetValue("file_path", out var rfp) ? rfp?.ToString() : null;
                if (owner != null && !isTestSource(owner.Id, owner.Path) && !isTestSource(relCallerId, relFilePath))
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
            var isReverseDbRel = rel.Kind == OntologyConstants.Relationships.AccessedBy ||
                                 rel.Kind == OntologyConstants.Relationships.QueriedBy ||
                                 rel.Kind == OntologyConstants.Relationships.DeclaredIn;
            var relDbId = isReverseDbRel ? rel.From : rel.To;
            var relCallerId = isReverseDbRel ? rel.To : rel.From;
            var isDbRel = rel.Kind == OntologyConstants.Relationships.UsesDb ||
                          rel.Kind == OntologyConstants.Relationships.Configures ||
                          isReverseDbRel ||
                          rawToCanonical.ContainsKey(relDbId);
            if (!isDbRel) continue;
            if (!rawToCanonical.TryGetValue(relDbId, out var canonicalTarget)) continue;

            var owner = resolveOwner(relCallerId);
            if (owner == null || owner.Id == canonicalTarget) continue;

            var relFilePath = rel.Properties != null && rel.Properties.TryGetValue("file_path", out var rfp) ? rfp?.ToString() : null;
            if (isTestSource(owner.Id, owner.Path) || isTestSource(relCallerId, relFilePath)) continue;

            var isRawImplicitEdge = (rawImplicitDbIds != null && rawImplicitDbIds.Contains(relDbId)) ||
                                    (rel.Properties != null && rel.Properties.TryGetValue("is_implicit_schema", out var impObj) && impObj?.ToString() == "true") ||
                                    relDbId.EndsWith(":db:default:dbo", StringComparison.OrdinalIgnoreCase);
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
                    RecordOwnConcreteEngine(owner.Id, eng);
                }
            }
        }

        return (multiDomainSharedLibDbIds, implicitSchemaIds, concreteEnginesCount, serviceOwnDomainConcreteEngines);
    }

    private static Dictionary<string, string> BuildProjectToWorkloadMap(Layer4Result l4Result)
    {
        var projectToWorkloadMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (l4Result.SemanticStructure != null)
        {
            foreach (var wl in l4Result.SemanticStructure.Children)
            {
                var projId = wl.Extensions?.GetValueOrDefault("project_id");
                if (!string.IsNullOrEmpty(projId))
                {
                    projectToWorkloadMap[projId] = wl.Id;
                    projectToWorkloadMap[projId.TrimEnd(':')] = wl.Id;
                    projectToWorkloadMap[projId.TrimEnd(':') + ":"] = wl.Id;
                }
            }
        }
        if (l4Result.SemanticRelationships != null)
        {
            foreach (var r in l4Result.SemanticRelationships)
            {
                if (r.Kind == OntologyConstants.Relationships.Deploys)
                {
                    projectToWorkloadMap[r.From] = r.To;
                    projectToWorkloadMap[r.From.TrimEnd(':')] = r.To;
                    projectToWorkloadMap[r.From.TrimEnd(':') + ":"] = r.To;
                }
                else if (r.Kind == OntologyConstants.Relationships.ImplementedBy &&
                         (r.To.Contains(":p:") || r.To.Contains(":project:")))
                {
                    projectToWorkloadMap[r.To] = r.From;
                    projectToWorkloadMap[r.To.TrimEnd(':')] = r.From;
                    projectToWorkloadMap[r.To.TrimEnd(':') + ":"] = r.From;
                }
            }
        }
        return projectToWorkloadMap;
    }
}
