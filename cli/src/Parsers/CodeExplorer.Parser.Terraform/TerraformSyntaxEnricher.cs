using CodeExplorer.Core.Common;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Parser.Terraform.Hcl;

namespace CodeExplorer.Parser.Terraform;

/// <summary>
/// Layer 4 semantic enricher for Terraform/Terragrunt.
///
/// Runs after all files in a project are parsed, so it can see sibling .tf files (unlike a
/// per-file ISemanticExtension). Resolves the traversal references harvested during parsing
/// (var.X / local.X / module.X[.Y] / data.T.N / &lt;type&gt;.N) to the matching declaration node,
/// scoped to the module directory, and emits DEPENDS_ON edges with via/ref/line properties.
///
/// Resolution runs once per project (the enricher is invoked once per syntax tree, so a
/// processed-project set guards against repeats). The edge source is the file node that contains
/// the reference; the target is the resolved declaration node.
/// </summary>
public sealed class TerraformSyntaxEnricher : ISyntaxEnricher
{
    private readonly TerraformParser _parser;

    // Tracks which projects have already been resolved, per scan. Keyed on the ParsingContext so
    // the state is scoped to a single scan and collected with it, rather than living for the
    // process lifetime. EnrichAsync is invoked once per syntax tree, so this guards repeats.
    private static readonly ConditionalWeakTable<ParsingContext, ConcurrentDictionary<string, byte>> _processedByContext = new();

    public TerraformSyntaxEnricher(TerraformParser parser) => _parser = parser;

    public async Task EnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        var processed = _processedByContext.GetValue(ctx, _ => new ConcurrentDictionary<string, byte>());
        if (!processed.TryAdd(projectNode.Id, 0))
        {
            return; // already resolved for this project in this scan
        }

        var projectDir = (projectNode.Path ?? string.Empty).Replace('\\', '/').Trim('/');

        // Read only the files parsed in this project's directory, within this workspace. This
        // scopes resolution to the project (no cross-workspace or cross-project leakage) and
        // avoids rescanning every parsed file in the workspace per project.
        var projectFiles = _parser.FilesInDirectory(ctx.WorkspaceId, projectDir);
        if (projectFiles.Count == 0) return;

        // 1. Build a declaration lookup: refKey -> nodeId, scoped to this project's module dir.
        var declByKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (_, file) in projectFiles)
        {
            foreach (var sym in file.Symbols)
            {
                // First declaration wins on duplicate keys (HCL would reject dupes anyway).
                declByKey.TryAdd(sym.RefKey, sym.NodeId);
            }
        }

        // 2. Resolve references from files in this project and enqueue DEPENDS_ON edges.
        var edges = new List<Relationship>();
        var seen = new HashSet<(string From, string To)>();

        foreach (var (relPath, file) in projectFiles)
        {
            var fileNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{relPath}";

            foreach (var reference in file.References)
            {
                var resolved = ResolveKey(reference);
                if (resolved is not { } r) continue;

                if (!declByKey.TryGetValue(r.Key, out var targetId)) continue;

                // Anchor the edge at the enclosing declaration (e.g. the resource that made the
                // reference) when known and resolvable; otherwise fall back to the file node.
                var fromId = fileNodeId;
                if (reference.EnclosingKey != null && declByKey.TryGetValue(reference.EnclosingKey, out var enclosingId))
                {
                    fromId = enclosingId;
                }

                if (fromId == targetId) continue; // never self-link

                // Dedup per (from, to, output): distinct outputs of the same target are kept.
                if (!seen.Add((fromId, $"{targetId}#{r.OutputName}"))) continue;

                var props = new Dictionary<string, object>
                {
                    ["via"] = r.Via,
                    ["ref"] = reference.Explicit ? "explicit" : r.RefKind,
                    ["line"] = reference.Line
                };
                if (!string.IsNullOrEmpty(r.OutputName))
                {
                    props["out"] = r.OutputName!;
                }

                edges.Add(new Relationship(
                    fromId, targetId, OntologyConstants.Relationships.DependsOn, props));
            }
        }

        // Configuration linkage: a .tfvars assignment or a Terragrunt `inputs` key links to the
        // target variable declaration — same module directory for tfvars, the wrapped module (via
        // the unit's `terraform { source }`) for `inputs`. No edge when the target variable is not
        // in the workspace. Modeled as a tagged DEPENDS_ON (ref=configures); see DESIGN_DECISIONS.md (D1).
        foreach (var (relPath, file) in projectFiles)
        {
            if (file.Configures.Count == 0) continue;

            var fileNodeId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{relPath}";

            foreach (var cfg in file.Configures)
            {
                string? variableId;
                bool inferred;

                if (cfg.Via == "terragrunt")
                {
                    // Resolve the wrapped module directory and look up the variable there.
                    var targetDir = TerraformParser.ResolveModuleSourceDir(cfg.SourceDir, projectDir, ctx.AbsoluteWorkspacePath);
                    if (targetDir == null) continue; // remote/unresolvable module: no target in graph
                    variableId = LookupVariable(ctx.WorkspaceId, targetDir, cfg.VariableName);
                    inferred = true; // cross-dir: the unit does not state a -var-file
                }
                else // tfvars
                {
                    declByKey.TryGetValue($"var.{cfg.VariableName}", out variableId);
                    inferred = false;
                }

                if (variableId == null) continue; // target variable not declared in the workspace
                if (!seen.Add((fileNodeId, $"cfg#{variableId}"))) continue;

                var props = new Dictionary<string, object>
                {
                    ["via"] = cfg.Via,          // "tfvars" | "terragrunt"
                    ["ref"] = "configures",
                    ["line"] = cfg.Line
                };
                if (inferred) props["inferred"] = true;

                edges.Add(new Relationship(
                    fileNodeId, variableId, OntologyConstants.Relationships.DependsOn, props));
            }
        }

        // Map known cloud resource types to Layer 4 semantic nodes using the same canonical ID
        // scheme the application parsers use, so infrastructure and the app code that uses it
        // reconcile by shared node ID. Emits DEPLOYS and a project usage edge per resource.
        var semanticNodes = new List<IOntologyNode>();
        var createdSemanticIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (relPath, file) in projectFiles)
        {
            foreach (var sym in file.Symbols)
            {
                if (sym.Kind != "resource" || sym.ResourceType == null || sym.Name == null) continue;

                var cls = CloudResourceCatalog.Classify(sym.ResourceType);
                if (cls.Kind == CloudResourceCatalog.SemanticKind.None) continue;

                // The node is named from the Terraform resource label (stable). The `name` attribute
                // is usually a computed interpolation and cannot be reconciled at parse time.
                var name = sym.Name;

                string semanticId;
                IOntologyNode node;
                string edgeKind;

                switch (cls.Kind)
                {
                    case CloudResourceCatalog.SemanticKind.Database:
                        var dbType = cls.SubType ?? "relational";
                        semanticId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Database}:{dbType}:{name}";
                        node = new DatabaseNode(semanticId, name, relPath, dbType,
                            new Dictionary<string, string> { ["provisioned_by"] = "terraform", ["resource_type"] = sym.ResourceType });
                        edgeKind = OntologyConstants.Relationships.UsesDb;
                        break;

                    case CloudResourceCatalog.SemanticKind.Topic:
                        var broker = cls.SubType ?? "broker";
                        semanticId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Topic}:{broker}:{name}";
                        node = new TopicNode(semanticId, name, relPath, broker,
                            new Dictionary<string, string> { ["provisioned_by"] = "terraform", ["resource_type"] = sym.ResourceType });
                        edgeKind = OntologyConstants.Relationships.PublishesTo;
                        break;

                    default: // CloudService
                        // CloudService id is dir-scoped (plumbing, not a reconciliation target);
                        // Database/Topic are workspace-global.
                        var scope = string.IsNullOrEmpty(projectDir) ? "root" : projectDir;
                        semanticId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.CloudService}:{sym.ResourceType}:{scope}:{name}";
                        node = new CloudServiceNode(semanticId, name, sym.ResourceType, relPath,
                            new Dictionary<string, string> { ["provisioned_by"] = "terraform" });
                        edgeKind = OntologyConstants.Relationships.UsesCloud;
                        break;
                }

                if (createdSemanticIds.Add(semanticId))
                {
                    semanticNodes.Add(node);
                }

                // DEPLOYS: the Terraform resource provisions this semantic entity.
                edges.Add(new Relationship(
                    sym.NodeId, semanticId, OntologyConstants.Relationships.Deploys,
                    new Dictionary<string, object> { ["via"] = "terraform", ["resource_type"] = sym.ResourceType }));

                // Also link the project to the semantic entity with the usage-shaped edge.
                edges.Add(new Relationship(
                    projectNode.Id, semanticId, edgeKind,
                    new Dictionary<string, object> { ["via"] = "terraform" }));
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

    // Finds the node id of `variable "<name>"` declared in the given workspace directory, or null.
    private string? LookupVariable(string workspaceId, string directory, string variableName)
    {
        var files = _parser.FilesInDirectory(workspaceId, directory);
        var key = $"var.{variableName}";
        foreach (var (_, file) in files)
        {
            foreach (var sym in file.Symbols)
            {
                if (sym.RefKey == key) return sym.NodeId;
            }
        }
        return null;
    }

    /// <summary>
    /// Map a harvested reference to the declaration key it should resolve against, the edge's
    /// via/ref provenance, and (for module/dependency output reads) the referenced output name.
    /// Returns null Key when the reference targets nothing we model.
    /// </summary>
    private static ResolvedRef? ResolveKey(HclReference reference)
    {
        var path = reference.Path;
        var head = reference.Head;

        switch (head)
        {
            case "var":
                return path.Count >= 2 ? new ResolvedRef($"var.{path[1]}", "terraform", "var", null) : null;
            case "local":
                return path.Count >= 2 ? new ResolvedRef($"local.{path[1]}", "terraform", "local", null) : null;
            case "module":
                // module.X or module.X.Y → depends on the module declaration "module.X" (out=Y if present).
                if (path.Count < 2) return null;
                return new ResolvedRef($"module.{path[1]}", "terraform", "module",
                    path.Count >= 3 ? path[2] : null);
            case "data":
                return path.Count >= 3 ? new ResolvedRef($"data.{path[1]}.{path[2]}", "terraform", "data", null) : null;
            case "dependency":
                // Terragrunt dependency.X or dependency.X.outputs.Y → DEPENDS_ON the dependency block.
                // Path shape: ["dependency","X"] or ["dependency","X","outputs","Y", ...].
                if (path.Count < 2) return null;
                string? outName = null;
                if (path.Count >= 4 && string.Equals(path[2], "outputs", StringComparison.Ordinal))
                {
                    outName = path[3];
                }
                return new ResolvedRef($"dependency.{path[1]}", "terragrunt", "dependency", outName);
            default:
                // <resource_type>.<name> (resource reference), e.g. aws_security_group.internal
                return path.Count >= 2 ? new ResolvedRef($"{head}.{path[1]}", "terraform", "resource", null) : null;
        }
    }

    private readonly record struct ResolvedRef(string Key, string Via, string RefKind, string? OutputName);
}
