using CodeExplorer.Core.Common;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Layer 4 semantic enricher for Terraform/Terragrunt, invoked once per project through
/// <see cref="IProjectParser.EnrichProjectAsync"/> after all of the project's files are parsed.
///
/// Reads only the <see cref="TerraformSyntaxTree"/>s it is given, so it can see sibling .tf files
/// (unlike a per-file ISemanticExtension) without holding state between scans. Resolves the
/// traversal references harvested during parsing (var.X / local.X / module.X[.Y] / data.T.N /
/// &lt;type&gt;.N) to the matching declaration node, scoped to the module directory, and emits one
/// typed DEPENDS_ON edge per (source, target). The edge source is the node the reference is made
/// from (enclosing declaration, local value, or attribute anchor); the target is the resolved
/// declaration node.
/// </summary>
public static class TerraformSyntaxEnricher
{
    public static async Task EnrichAsync(ProjectNode projectNode, IReadOnlyList<SyntaxTree> projectTrees, ParsingContext ctx)
    {
        var projectDir = projectNode.Path.NormalizeSlashes().Trim('/');

        // Only files directly in the module directory: a Terraform module is a single directory,
        // so files in a subdirectory belonging to this project are not part of its namespace.
        var projectFiles = projectTrees
            .OfType<TerraformSyntaxTree>()
            .Where(t => DirOf(t.RelativePath) == projectDir)
            .Select(t => (RelPath: t.RelativePath, File: t))
            .ToList();
        if (projectFiles.Count == 0) return;

        // 1. Build a declaration lookup: refKey -> nodeId, scoped to this project's module dir.
        // Attribute anchors are only reference sources, so they are looked up per file instead.
        var declByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, file) in projectFiles)
        {
            foreach (var sym in file.Symbols)
            {
                if (sym.Kind == TerraformSymbolKind.Attribute) continue;

                // First declaration wins on duplicate keys (HCL would reject dupes anyway).
                declByKey.TryAdd(sym.RefKey, sym.NodeId);
            }
        }

        // 2. Resolve references from files in this project into DEPENDS_ON edges, one per
        // (source, target). The edge is marked as a depends_on dependency when any reference behind
        // it is an explicit `depends_on` entry.
        var dependsOn = new Dictionary<(string From, string To), DependsOnRelationship>();

        foreach (var (_, file) in projectFiles)
        {
            var anchors = file.Symbols
                .Where(sym => sym.Kind == TerraformSymbolKind.Attribute)
                .ToDictionary(sym => sym.RefKey, sym => sym.NodeId, StringComparer.Ordinal);

            foreach (var reference in file.References)
            {
                if (ResolveKey(reference) is not { } r) continue;
                if (!declByKey.TryGetValue(r.Key, out var targetId)) continue;

                // The edge starts at the node that made the reference: its attribute anchor or
                // enclosing declaration. A reference with neither (only in malformed blocks) is
                // dropped rather than attributed to the file.
                if (reference.EnclosingKey == null) continue;
                if (!anchors.TryGetValue(reference.EnclosingKey, out var fromId)
                    && !declByKey.TryGetValue(reference.EnclosingKey, out fromId))
                {
                    continue;
                }

                if (fromId == targetId) continue; // never self-link

                var key = (fromId, targetId);
                if (dependsOn.TryGetValue(key, out var existing))
                {
                    if (reference.Explicit && existing.EvidenceKind == null)
                    {
                        dependsOn[key] = existing with { EvidenceKind = DependsOnEvidence };
                    }
                    continue;
                }

                dependsOn[key] = new DependsOnRelationship(fromId, targetId)
                {
                    Via = r.Kind == TerraformReferenceKind.Dependency ? ViaTerragrunt : ViaTerraform,
                    Line = reference.Line,
                    EvidenceKind = reference.Explicit ? DependsOnEvidence : null
                };
            }
        }

        var edges = dependsOn.Values.Select(Relationship.FromRelationship).ToList();

        // A .tfvars assignment depends on the variable of the same name declared in this module
        // directory. No edge when the variable is not declared here.
        foreach (var (_, file) in projectFiles)
        {
            foreach (var cfg in file.Configures)
            {
                if (!declByKey.TryGetValue($"var.{cfg.VariableName}", out var variableId)) continue;

                edges.Add(Relationship.FromRelationship(
                    new DependsOnRelationship(cfg.NodeId, variableId) { Via = ViaTfvars, Line = cfg.Line }));
            }
        }

        // Map known cloud resource types to Layer 4 semantic nodes. A resource whose name is a
        // literal gets the canonical ID application parsers build from that name, so infrastructure
        // and the app code that uses it reconcile by shared node ID. A computed name cannot be
        // reconciled at parse time, so the node is named and identified by module directory and
        // label, keeping copies of a module deployed from different directories apart.
        // Each node links back to the resource block and project that deploy it (DEPLOYED_BY).
        var semanticNodes = new List<IOntologyNode>();
        var createdSemanticIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var deployedByProject = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (relPath, file) in projectFiles)
        {
            foreach (var sym in file.Symbols)
            {
                if (sym.Kind != TerraformSymbolKind.Resource || sym.ResourceType is not { } resourceType || sym.Name == null) continue;

                var cls = CloudResourceCatalog.Classify(resourceType);
                if (cls.Kind == CloudResourceCatalog.SemanticKind.None) continue;

                var scopedName = string.IsNullOrEmpty(projectDir) ? sym.Name : $"{projectDir}/{sym.Name}";
                var provider = CloudResourceCatalog.ProviderOf(resourceType);

                string semanticId;
                IOntologyNode node;

                switch (cls.Kind)
                {
                    case CloudResourceCatalog.SemanticKind.Database:
                    {
                        var name = sym.DeployedName ?? scopedName;
                        semanticId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Database}:{cls.DbType.ToCanonicalString()}:{name}";
                        node = new DatabaseNode(semanticId, name, relPath, cls.DbType) { Provider = provider, Engine = sym.Engine };
                        break;
                    }

                    case CloudResourceCatalog.SemanticKind.Topic:
                    {
                        var name = sym.DeployedName ?? scopedName;
                        semanticId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Topic}:{cls.BrokerType.ToCanonicalString()}:{name}";
                        node = new TopicNode(semanticId, name, relPath, cls.BrokerType);
                        break;
                    }

                    default: // CloudService
                    {
                        // CloudService ids are always directory-scoped (not a reconciliation target).
                        var scope = string.IsNullOrEmpty(projectDir) ? RootScope : projectDir;
                        semanticId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.CloudService}:{resourceType.Name}:{scope}:{sym.Name}";
                        node = new CloudServiceNode(semanticId, sym.DeployedName ?? sym.Name, cls.CloudType, relPath) { Provider = provider };
                        break;
                    }
                }

                if (createdSemanticIds.Add(semanticId))
                {
                    semanticNodes.Add(node);
                }

                edges.Add(Relationship.FromRelationship(
                    new DeployedByRelationship(semanticId, sym.NodeId) { Via = ViaTerraform, Line = sym.Line }));
                if (deployedByProject.Add(semanticId))
                {
                    edges.Add(Relationship.FromRelationship(
                        new DeployedByRelationship(semanticId, projectNode.Id) { Via = ViaTerraform }));
                }
            }
        }

        if (semanticNodes.Count > 0 && ctx.SemanticStructure != null)
        {
            // Add each semantic node to the structure once, skipping IDs already present.
            var existingChildIds = new HashSet<string>(
                ctx.SemanticStructure.Children.Select(c => c.Id), StringComparer.Ordinal);
            foreach (var n in semanticNodes)
            {
                if (existingChildIds.Add(n.Id))
                {
                    ctx.SemanticStructure.Children.Add(n);
                }
            }
        }

        if (edges.Count > 0)
        {
            await ctx.EnqueueUploadRelationshipsAsync(edges);
            ctx.AddRelsCount(edges.Count);
        }
    }

    // Normalized directory of a workspace-relative path ("" for the workspace root).
    private static string DirOf(string relativePath) =>
        Path.GetDirectoryName(relativePath).NormalizeSlashes().Trim('/');

    /// <summary>
    /// Map a harvested reference to the declaration key it should resolve against and its kind.
    /// Returns null when the reference targets nothing we model.
    /// </summary>
    private static ResolvedRef? ResolveKey(TerraformReference reference)
    {
        var path = reference.Path;
        var head = reference.Head;

        switch (head)
        {
            case "var":
                return path.Count >= 2 ? new ResolvedRef($"var.{path[1]}", TerraformReferenceKind.Var) : null;
            case "local":
                return path.Count >= 2 ? new ResolvedRef($"local.{path[1]}", TerraformReferenceKind.Local) : null;
            case "module":
                // module.X or module.X.Y → depends on the module declaration "module.X".
                return path.Count >= 2 ? new ResolvedRef($"module.{path[1]}", TerraformReferenceKind.Module) : null;
            case "data":
                return path.Count >= 3 ? new ResolvedRef($"data.{path[1]}.{path[2]}", TerraformReferenceKind.Data) : null;
            case "dependency":
                // Terragrunt dependency.X or dependency.X.outputs.Y → depends on the dependency block.
                return path.Count >= 2 ? new ResolvedRef($"dependency.{path[1]}", TerraformReferenceKind.Dependency) : null;
            default:
                // <resource_type>.<name> (resource reference), e.g. aws_security_group.internal
                return path.Count >= 2 ? new ResolvedRef($"{head}.{path[1]}", TerraformReferenceKind.Resource) : null;
        }
    }

    // Edge provenance (`via`): the tool whose syntax produced the edge.
    private const string ViaTerraform = "terraform";
    private const string ViaTerragrunt = "terragrunt";
    private const string ViaTfvars = "tfvars";

    // Scope segment of a CloudService id for a project at the workspace root.
    private const string RootScope = "root";

    // EvidenceKind of a DEPENDS_ON edge declared by an explicit `depends_on` meta-argument.
    private const string DependsOnEvidence = "depends_on";

    private enum TerraformReferenceKind { Var, Local, Module, Data, Dependency, Resource }

    private readonly record struct ResolvedRef(string Key, TerraformReferenceKind Kind);
}
