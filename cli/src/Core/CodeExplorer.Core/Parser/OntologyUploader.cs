using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public static class OntologyUploader
{
    public static async Task UploadNodeTreeAsync(IOntologyNode node, string? parentId, ParsingContext ctx)
    {
        var collectedNodes = new List<Node>();
        var collectedRelationships = new List<Relationship>();
        var visitedNodeIds = new HashSet<string>();
        var visitedRelKeys = new HashSet<(string From, string To, string Kind)>();

        CollectTreeElements(node, null, ctx, collectedNodes, collectedRelationships, visitedNodeIds, visitedRelKeys);

        // Store all tree relationships in ctx for post-indexing and layer 5 analysis
        ctx.TreeRelationships.AddRange(collectedRelationships);

        // Upload nodes in chunks of 10000
        for (var i = 0; i < collectedNodes.Count; i += 10000)
        {
            var chunk = collectedNodes.GetRange(i, Math.Min(10000, collectedNodes.Count - i));
            await ctx.EnqueueUploadNodesAsync(chunk);
        }

        // Upload relationships in chunks of 10000
        for (var i = 0; i < collectedRelationships.Count; i += 10000)
        {
            var chunk = collectedRelationships.GetRange(i, Math.Min(10000, collectedRelationships.Count - i));
            await ctx.EnqueueUploadRelationshipsAsync(chunk);
        }
    }

    private static void CollectTreeElements(
        IOntologyNode node,
        IOntologyNode? parentNode,
        ParsingContext ctx,
        List<Node> collectedNodes,
        List<Relationship> collectedRelationships,
        HashSet<string> visitedNodeIds,
        HashSet<(string From, string To, string Kind)> visitedRelKeys)
    {
        var isDuplicate = visitedNodeIds.Contains(node.Id);

        // 3. Link to parent if present (we still link, even if node is duplicate, to capture secondary parent relationships)
        if (parentNode != null && parentNode.Id != node.Id)
        {
            var ontologyRel = GetRelationship(parentNode.Id, node);
            var dbRel = Relationship.FromRelationship(ontologyRel);
            if (visitedRelKeys.Add((dbRel.From, dbRel.To, dbRel.Kind)))
            {
                collectedRelationships.Add(dbRel);
                ctx.AddRelsCount(1);
            }
        }

        if (!isDuplicate)
        {
            visitedNodeIds.Add(node.Id);

            // 1. Convert and collect the current node
            var dbNode = Node.FromNode(node);
            collectedNodes.Add(dbNode);
            ctx.IncrementNodeKind(node.Kind);
            ctx.AddNodesCount(1);

        // 2. Map global symbols for reference resolution
        if (node.Kind == OntologyConstants.NodeLabels.Type ||
            node.Kind == OntologyConstants.NodeLabels.Function ||
            node.Kind == OntologyConstants.NodeLabels.Procedure ||
            node.Kind == OntologyConstants.NodeLabels.Table ||
            node.Kind == OntologyConstants.NodeLabels.EntryPoint ||
            node.Kind == OntologyConstants.NodeLabels.Endpoint)
        {
            if (dbNode.Properties.TryGetValue("name", out var nameVal) && nameVal is string nameStr)
            {
                ctx.AddGlobalSymbol(node.Kind, nameStr, node.Id);
                if (node.Kind == OntologyConstants.NodeLabels.Endpoint || node.Kind == OntologyConstants.NodeLabels.EntryPoint)
                {
                    ctx.AddGlobalSymbol(node.Kind, nameStr.Replace(":", " "), node.Id);
                }
                if (node.Kind == OntologyConstants.NodeLabels.Function && parentNode != null &&
                    parentNode.Kind == OntologyConstants.NodeLabels.Type)
                {
                    var parentName = parentNode.GetType().GetProperty("Name")?.GetValue(parentNode) as string;
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        ctx.AddGlobalSymbol(node.Kind, $"{parentName}.{nameStr}", node.Id);
                    }
                }
            }
        }

        // Special: If Project (or subtype Service/App/Library/Worker/CliTool), link it to GitSettings via USES_GIT and Folder/Workspace via LOCATED_IN
        if (node is ProjectNode)
        {
            var projectAbsDir = string.IsNullOrEmpty(node.Path) || node.Path == "."
                ? ctx.AbsoluteWorkspacePath
                : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, node.Path));

            var gitSettings = ctx.FindGitSettingsForPath(projectAbsDir);
            if (gitSettings != null)
            {
                var usesGitRel = Relationship.FromRelationship(new UsesGitRelationship(node.Id, gitSettings.Id));
                collectedRelationships.Add(usesGitRel);
                ctx.AddRelsCount(1);

                if (!string.IsNullOrEmpty(gitSettings.Branch))
                    node.SetExtension("git_branch", gitSettings.Branch);
                if (!string.IsNullOrEmpty(gitSettings.OriginUrl))
                    node.SetExtension("git_origin", gitSettings.OriginUrl);
                if (!string.IsNullOrEmpty(gitSettings.UserName))
                    node.SetExtension("git_user", gitSettings.UserName);
                if (gitSettings.Extensions?.TryGetValue("commit_hash", out var commit) == true && !string.IsNullOrEmpty(commit))
                    node.SetExtension("git_commit", commit);
                if (gitSettings.Extensions?.TryGetValue("repo_name", out var repo) == true && !string.IsNullOrEmpty(repo))
                    node.SetExtension("git_repo", repo);
            }

            // Emit LOCATED_IN relationship to Folder or Workspace
            string targetId;
            var projectPath = node.Path;
            if (string.IsNullOrEmpty(projectPath) || projectPath == ".")
            {
                targetId = ctx.WorkspaceId;
            }
            else
            {
                var absoluteFolderPath = Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, projectPath)).Replace('\\', '/');
                targetId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Folder}:{absoluteFolderPath}";
            }

            var locatedInRel = Relationship.FromRelationship(new LocatedInRelationship(node.Id, targetId));
            collectedRelationships.Add(locatedInRel);
            ctx.AddRelsCount(1);
        }

            // 4. Collect unresolved references/dependencies
            if (node.References.Count > 0)
            {
                ctx.AddGlobalReferences(node.References);
            }
        }

        // 5. Recursively collect all children
        foreach (var child in node.Children)
        {
            CollectTreeElements(child, node, ctx, collectedNodes, collectedRelationships, visitedNodeIds, visitedRelKeys);
        }
    }

    private static IOntologyRelationship GetRelationship(string parentId, IOntologyNode child)
    {
        if (parentId.EndsWith($":{OntologyConstants.IdPrefixes.FilesStructure}") ||
            parentId.EndsWith($":{OntologyConstants.IdPrefixes.SyntaxStructure}") ||
            parentId.EndsWith($":{OntologyConstants.IdPrefixes.SemanticStructure}") ||
            parentId.EndsWith($":{OntologyConstants.IdPrefixes.ProjectsStructure}") ||
            parentId.Contains("files_structure") ||
            parentId.Contains("syntax_structure") ||
            parentId.Contains("semantic_structure") ||
            parentId.EndsWith(":syntax"))
        {
            return new ContainsRelationship(parentId, child.Id);
        }

        if (child.Kind == OntologyConstants.NodeLabels.Package)
        {
            if (child is PackageNode pn && (!pn.IsExternal || !string.IsNullOrEmpty(pn.Path)) && IsProjectNodeId(parentId))
            {
                return new ImplementedByRelationship(child.Id, parentId);
            }
            return new DependsOnRelationship(parentId, child.Id);
        }
        if (child is ProjectNode)
        {
            if (IsPackageNodeId(parentId))
            {
                return new ImplementedByRelationship(parentId, child.Id);
            }
            return new LocatedInRelationship(child.Id, parentId);
        }
        if (child.Kind == OntologyConstants.NodeLabels.Database)
        {
            if (IsSymbolOrCallableId(parentId))
            {
                return new QueriedByRelationship(child.Id, parentId);
            }
            return new UsesDbRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.Topic)
        {
            if (IsProjectNodeId(parentId))
            {
                return new PublishesToRelationship(parentId, child.Id);
            }
            return new PublishedByRelationship(child.Id, parentId);
        }
        if (child.Kind == OntologyConstants.NodeLabels.Endpoint)
        {
            if (IsProjectNodeId(parentId))
            {
                return new ContainsRelationship(parentId, child.Id);
            }
            return new ExposedByRelationship(child.Id, parentId);
        }
        if (child.Kind == OntologyConstants.NodeLabels.ApiInUse)
        {
            if (IsProjectNodeId(parentId))
            {
                return new ContainsRelationship(parentId, child.Id);
            }
            return new UsesApiRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.CloudService)
        {
            if (IsProjectNodeId(parentId))
            {
                return new ContainsRelationship(parentId, child.Id);
            }
            return new UsesCloudRelationship(parentId, child.Id);
        }
        if (child.Kind == OntologyConstants.NodeLabels.EntryPoint)
        {
            if (IsProjectNodeId(parentId))
            {
                return new ContainsRelationship(parentId, child.Id);
            }
            if (parentId.Contains(":entrypoints") || parentId.Contains(":entry"))
            {
                return new ExposedByRelationship(child.Id, parentId); // EntryPoint -> EXPOSED_BY -> EntryPoints
            }
            return new ImplementedByRelationship(child.Id, parentId); // EntryPoint -> IMPLEMENTED_BY -> Function
        }
        if (child.Kind == OntologyConstants.NodeLabels.ExternalService)
        {
            if (IsProjectNodeId(parentId))
            {
                return new ContainsRelationship(parentId, child.Id);
            }
            return new CalledByRelationship(child.Id, parentId); // ExternalService -> CALLED_BY -> Function
        }

        if (IsCodeEntityKind(child.Kind))
        {
            if (IsFileNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            if (IsProjectNodeId(parentId))
            {
                if (child.Kind == OntologyConstants.NodeLabels.Type)
                {
                    return new DeclaresTypeRelationship(parentId, child.Id);
                }
            }
            if (IsCodeEntityId(parentId))
            {
                // Parent is Type
                if (parentId.Contains(":type:") || parentId.Contains(":Type:") || parentId.Contains(":class:") || parentId.Contains(":interface:"))
                {
                    if (child.Kind == OntologyConstants.NodeLabels.Function)
                    {
                        return new HasMethodRelationship(parentId, child.Id);
                    }
                    if (child.Kind == OntologyConstants.NodeLabels.Member)
                    {
                        return new HasMemberRelationship(parentId, child.Id);
                    }
                }
                // Parent is Function
                if (parentId.Contains(":function:"))
                {
                    if (child.Kind == OntologyConstants.NodeLabels.Member)
                    {
                        return new HasVariableRelationship(parentId, child.Id);
                    }
                }
                return new DeclaresRelationship(parentId, child.Id);
            }
        }

        return new ContainsRelationship(parentId, child.Id);
    }

    private static bool IsCodeEntityId(string id)
    {
        var lower = id.ToLowerInvariant();
        return lower.Contains($":{OntologyConstants.IdPrefixes.Symbol}:") ||
               lower.Contains(":symbol:") ||
               lower.Contains(":class:") ||
               lower.Contains(":interface:") ||
               lower.Contains(":type:") ||
               lower.Contains(":function:") ||
               lower.Contains(":variable:") ||
               lower.Contains(":member:") ||
               lower.Contains($":{OntologyConstants.IdPrefixes.Procedure}:") ||
               lower.Contains(":procedure:") ||
               lower.Contains($":{OntologyConstants.IdPrefixes.Query}:") ||
               lower.Contains(":query:") ||
               lower.Contains($":{OntologyConstants.IdPrefixes.Table}:") ||
               lower.Contains(":table:");
    }

    private static bool IsCodeEntityKind(string kind)
    {
        return kind == OntologyConstants.NodeLabels.Type ||
               kind == OntologyConstants.NodeLabels.Function ||
               kind == OntologyConstants.NodeLabels.Member ||
               kind == OntologyConstants.NodeLabels.Query ||
               kind == OntologyConstants.NodeLabels.Procedure ||
               kind == OntologyConstants.NodeLabels.Table;
    }

    private static bool IsProjectNodeId(string id)
    {
        return Urn.TryParse(id, out var urn) && (urn.Domain.Equals(OntologyConstants.IdPrefixes.Project, StringComparison.OrdinalIgnoreCase) || urn.Domain.Equals("project", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPackageNodeId(string id)
    {
        return Urn.TryParse(id, out var urn) && (urn.Domain.Equals(OntologyConstants.IdPrefixes.Package, StringComparison.OrdinalIgnoreCase) || urn.Domain.Equals("package", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFileNodeId(string id)
    {
        return Urn.TryParse(id, out var urn) && (urn.Domain.Equals(OntologyConstants.IdPrefixes.File, StringComparison.OrdinalIgnoreCase) || urn.Domain.Equals("file", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSymbolOrCallableId(string id)
    {
        if (Urn.TryParse(id, out var urn))
        {
            var d = urn.Domain.ToLowerInvariant();
            return d is OntologyConstants.IdPrefixes.Symbol or "symbol"
                     or OntologyConstants.IdPrefixes.Query or "query"
                     or OntologyConstants.IdPrefixes.Procedure or "procedure"
                     || id.Contains(":function:") || id.Contains(":Function:");
        }
        return false;
    }
}

