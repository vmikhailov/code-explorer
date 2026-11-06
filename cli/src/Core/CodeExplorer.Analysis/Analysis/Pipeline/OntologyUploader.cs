using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Cypher.Common;

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

        ctx.PipelineLog.QueuedToPersistencePipeline(collectedNodes.Count, collectedRelationships.Count);

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
        await ctx.FlushScanIssuesAsync();
        {
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

            if (parentNode.Kind is NodeKind.Endpoint or NodeKind.EntryPoint &&
                node.Kind == NodeKind.Function)
            {
                var triggersRel = Relationship.FromRelationship(new TriggersRelationship(parentNode.Id, node.Id));
                if (visitedRelKeys.Add((triggersRel.From, triggersRel.To, triggersRel.Kind)))
                {
                    collectedRelationships.Add(triggersRel);
                    ctx.AddRelsCount(1);
                }

                IOntologyRelationship handlerRel = parentNode.Kind == NodeKind.Endpoint
                    ? new ExposedByRelationship(parentNode.Id, node.Id)
                    : new ImplementedByRelationship(parentNode.Id, node.Id);
                var dbHandlerRel = Relationship.FromRelationship(handlerRel);
                if (visitedRelKeys.Add((dbHandlerRel.From, dbHandlerRel.To, dbHandlerRel.Kind)))
                {
                    collectedRelationships.Add(dbHandlerRel);
                    ctx.AddRelsCount(1);
                }

                var fnFilePath = !string.IsNullOrEmpty(node.Path) ? node.Path : (node as FunctionNode)?.FilePath;
                if (!string.IsNullOrEmpty(fnFilePath))
                {
                    var fnFileId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{fnFilePath}";
                    var declaredInRel = Relationship.FromRelationship(new DeclaredInRelationship(node.Id, fnFileId));
                    if (visitedRelKeys.Add((declaredInRel.From, declaredInRel.To, declaredInRel.Kind)))
                    {
                        collectedRelationships.Add(declaredInRel);
                        ctx.AddRelsCount(1);
                    }
                }
            }
        }

        if (!isDuplicate)
        {
            visitedNodeIds.Add(node.Id);

            // 1. Convert and collect the current node
            var dbNode = Node.FromNode(node);
            collectedNodes.Add(dbNode);
            ctx.IncrementNodeKind(node.Kind.ToCypherLabel());
            ctx.AddNodesCount(1);

        // 2. Map global symbols for reference resolution
        if (node.Kind is NodeKind.Type or
            NodeKind.Function or
            NodeKind.Procedure or
            NodeKind.Table or
            NodeKind.EntryPoint or
            NodeKind.Endpoint or
            NodeKind.ExternalService)
        {
            var nameStr = node.GetName();
            if (!string.IsNullOrEmpty(nameStr))
            {
                ctx.AddGlobalSymbol(node.Kind, nameStr, node.Id);
                var filePath = node.Path ?? (node as FunctionNode)?.FilePath ?? (node as TypeNode)?.FilePath;
                if (!string.IsNullOrEmpty(filePath))
                {
                    ctx.AddFileScopedSymbol(filePath.Replace('\\', '/'), node.Kind.ToCypherLabel(), nameStr, node.Id);
                }
                if (node.Kind is NodeKind.Table or NodeKind.Procedure)
                {
                    var parts = node.Id.Split(':');
                    var datasetIdx = Array.IndexOf(parts, "dataset");
                    if (datasetIdx != -1 && datasetIdx + 1 < parts.Length)
                    {
                        var schemaName = parts[datasetIdx + 1];
                        if (!string.Equals(schemaName, "dbo", StringComparison.OrdinalIgnoreCase))
                        {
                            ctx.AddGlobalSymbol(node.Kind, $"{schemaName}.{nameStr}", node.Id);
                        }
                    }
                }
                if (node is FunctionNode fnWithGql)
                {
                    var gqlField = fnWithGql.TryGetSignals()?.GraphqlField;
                    if (!string.IsNullOrEmpty(gqlField))
                    {
                        ctx.AddGlobalSymbol(node.Kind, gqlField, node.Id);
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            ctx.AddFileScopedSymbol(filePath.Replace('\\', '/'), node.Kind.ToCypherLabel(), gqlField, node.Id);
                        }
                    }
                    var gqlAliases = fnWithGql.TryGetSignals()?.GraphqlAliases;
                    if (!string.IsNullOrEmpty(gqlAliases))
                    {
                        foreach (var alias in gqlAliases.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            ctx.AddGlobalSymbol(node.Kind, alias, node.Id);
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                ctx.AddFileScopedSymbol(filePath.Replace('\\', '/'), node.Kind.ToCypherLabel(), alias, node.Id);
                            }
                        }
                    }
                }
                if (node.Kind is NodeKind.Endpoint or NodeKind.EntryPoint)
                {
                    ctx.AddGlobalSymbol(node.Kind, nameStr.Replace(":", " "), node.Id);
                }
                if (node.Kind == NodeKind.Function && parentNode != null &&
                    parentNode.Kind == NodeKind.Type)
                {
                    var parentName = parentNode.GetName();
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        ctx.AddGlobalSymbol(node.Kind, $"{parentName}.{nameStr}", node.Id);
                        if (!string.IsNullOrEmpty(filePath))
                        {
                            ctx.AddFileScopedSymbol(filePath.Replace('\\', '/'), node.Kind.ToCypherLabel(), $"{parentName}.{nameStr}", node.Id);
                        }
                    }
                }
            }
        }

        // Special: If CallLiteral, CatchClause, or EntryPoint, link it to File via DECLARED_IN
        if (node.Kind is NodeKind.CallLiteral or NodeKind.CatchClause or NodeKind.EntryPoint or NodeKind.ScanIssue)
        {
            var filePath = node.Path;
            if (string.IsNullOrEmpty(filePath) && node is CallLiteralNode cln)
            {
                filePath = cln.FilePath;
            }
            else if (string.IsNullOrEmpty(filePath) && node is CatchClauseNode ccn)
            {
                filePath = ccn.FilePath;
            }
            if (!string.IsNullOrEmpty(filePath))
            {
                var fileId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.File}:{filePath}";
                var declaredInRel = Relationship.FromRelationship(new DeclaredInRelationship(node.Id, fileId));
                if (visitedRelKeys.Add((declaredInRel.From, declaredInRel.To, declaredInRel.Kind)))
                {
                    collectedRelationships.Add(declaredInRel);
                    ctx.AddRelsCount(1);
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

                var projNode = (ProjectNode)node;
                var projSignals = projNode.Signals();
                if (!string.IsNullOrEmpty(gitSettings.Branch))
                    projSignals.GitBranch = gitSettings.Branch;
                if (!string.IsNullOrEmpty(gitSettings.OriginUrl))
                    projSignals.GitOrigin = gitSettings.OriginUrl;
                if (!string.IsNullOrEmpty(gitSettings.UserName))
                    projSignals.GitUser = gitSettings.UserName;
                if (!string.IsNullOrEmpty(gitSettings.CommitHash))
                    projSignals.GitCommit = gitSettings.CommitHash;
                if (!string.IsNullOrEmpty(gitSettings.RepoName))
                    projSignals.GitRepo = gitSettings.RepoName;
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

            // 5. Recursively collect all children
            foreach (var child in node.Children)
            {
                CollectTreeElements(child, node, ctx, collectedNodes, collectedRelationships, visitedNodeIds, visitedRelKeys);
            }
        }
        else if (node is DatabaseNode or DataSetNode)
        {
            // Multiple QueryNodes construct separate DatabaseNode/DataSetNode instances sharing the same canonical ID,
            // each holding its own TableNode/ProcedureNode children. Always traverse children of duplicate DB/DataSet containers.
            foreach (var child in node.Children)
            {
                CollectTreeElements(child, node, ctx, collectedNodes, collectedRelationships, visitedNodeIds, visitedRelKeys);
            }
        }
    }

    private static IOntologyRelationship GetRelationship(string parentId, IOntologyNode child)
    {
        if (parentId.EndsWith($":{OntologyConstants.IdPrefixes.FilesStructure}") ||
            parentId.EndsWith($":{OntologyConstants.IdPrefixes.SyntaxStructure}") ||
            parentId.EndsWith($":{OntologyConstants.IdPrefixes.SemanticStructure}") ||
            parentId.EndsWith($":{OntologyConstants.IdPrefixes.ProjectsStructure}") ||
            parentId.EndsWith($":{OntologyConstants.IdPrefixes.LogicalStructure}") ||
            parentId.Contains("files_structure") ||
            parentId.Contains("syntax_structure") ||
            parentId.Contains("semantic_structure") ||
            parentId.Contains("logical_structure") ||
            parentId.EndsWith(":syntax"))
        {
            return new ContainsRelationship(parentId, child.Id);
        }

        if (child.Kind == NodeKind.Package)
        {
            if (child is PackageNode pn && (!pn.IsExternal || !string.IsNullOrEmpty(pn.Path)) && IsProjectNodeId(parentId))
            {
                return new ImplementedByRelationship(child.Id, parentId);
            }
            return new DependsOnRelationship(parentId, child.Id) { DependencyType = "package", Category = "library" };
        }
        if (child is ProjectNode)
        {
            if (IsPackageNodeId(parentId))
            {
                return new ImplementedByRelationship(parentId, child.Id);
            }
            return new LocatedInRelationship(child.Id, parentId);
        }

        // Layer 4 semantic nodes: enforce downward grounding when parent is Layer 1 (File), Layer 2 (Project), or Layer 3 (Symbol),
        // and horizontal edges when parent is Layer 4 (Workload or Database/DataSet).
        if (child.Kind == NodeKind.Database)
        {
            if (IsFileNodeId(parentId)) return new DeclaredInRelationship(child.Id, parentId);
            if (IsProjectNodeId(parentId)) return new DeclaredInRelationship(child.Id, parentId);
            if (IsSymbolOrCallableId(parentId) || IsCodeEntityId(parentId))
            {
                return new AccessedByRelationship(child.Id, parentId);
            }
            return new UsesDbRelationship(parentId, child.Id);
        }
        if (child.Kind is NodeKind.Table or NodeKind.Procedure or NodeKind.DataSet or NodeKind.Query)
        {
            if (IsFileNodeId(parentId)) return new DeclaredInRelationship(child.Id, parentId);
            if (IsProjectNodeId(parentId)) return new DeclaredInRelationship(child.Id, parentId);
            if (IsSymbolOrCallableId(parentId) || IsCodeEntityId(parentId))
            {
                return new AccessedByRelationship(child.Id, parentId);
            }
            return new ContainsRelationship(parentId, child.Id);
        }
        if (child.Kind == NodeKind.Topic)
        {
            var isSubscriber = child is TopicNode topicNode &&
                               string.Equals(topicNode.MessagingDirection,
                                   OntologyConstants.EvidenceKinds.Subscribe, StringComparison.OrdinalIgnoreCase);
            if (IsFileNodeId(parentId) || IsProjectNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            if (IsWorkloadNodeId(parentId))
            {
                return isSubscriber
                    ? new SubscribesToRelationship(parentId, child.Id)
                    : new PublishesToRelationship(parentId, child.Id);
            }
            return isSubscriber
                ? new SubscribedByRelationship(child.Id, parentId)
                : new PublishedByRelationship(child.Id, parentId);
        }
        if (child.Kind == NodeKind.Endpoint)
        {
            if (IsFileNodeId(parentId) || IsProjectNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            if (IsWorkloadNodeId(parentId))
            {
                return new ExposesRelationship(parentId, child.Id);
            }
            return new ExposedByRelationship(child.Id, parentId);
        }
        if (child.Kind == NodeKind.ApiInUse)
        {
            if (IsFileNodeId(parentId) || IsProjectNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            if (IsSymbolOrCallableId(parentId) || IsCodeEntityId(parentId))
            {
                return new AccessedByRelationship(child.Id, parentId);
            }
            return new UsesApiRelationship(parentId, child.Id);
        }
        if (child.Kind == NodeKind.CloudService)
        {
            if (IsFileNodeId(parentId) || IsProjectNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            if (IsSymbolOrCallableId(parentId) || IsCodeEntityId(parentId))
            {
                return new AccessedByRelationship(child.Id, parentId);
            }
            return new UsesCloudRelationship(parentId, child.Id);
        }
        if (child.Kind == NodeKind.EntryPoint)
        {
            if (IsFileNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            if (IsProjectNodeId(parentId))
            {
                return new HasEntrypointRelationship(parentId, child.Id);
            }
            if (IsWorkloadNodeId(parentId))
            {
                return new ContainsRelationship(parentId, child.Id);
            }
            if (parentId.Contains(":entrypoints") || parentId.Contains(":entry"))
            {
                return new ExposedByRelationship(child.Id, parentId);
            }
            return new ImplementedByRelationship(child.Id, parentId);
        }
        if (child.Kind == NodeKind.ExternalService)
        {
            if (IsFileNodeId(parentId) || IsProjectNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            if (IsWorkloadNodeId(parentId))
            {
                return new ContainsRelationship(parentId, child.Id);
            }
            return new CalledByRelationship(child.Id, parentId);
        }

        if (child.Kind == NodeKind.CallLiteral)
        {
            if (IsFileNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            return new PassesLiteralRelationship(parentId, child.Id);
        }

        if (child.Kind == NodeKind.CatchClause)
        {
            if (IsFileNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            return new DeclaresRelationship(parentId, child.Id);
        }

        if (IsCodeEntityKind(child.Kind))
        {
            if (IsFileNodeId(parentId))
            {
                return new DeclaredInRelationship(child.Id, parentId);
            }
            if (IsCodeEntityId(parentId))
            {
                // Parent is Type
                if (parentId.Contains(":type:") || parentId.Contains(":Type:") || parentId.Contains(":class:") || parentId.Contains(":interface:"))
                {
                    if (child.Kind == NodeKind.Function)
                    {
                        return new HasMethodRelationship(parentId, child.Id);
                    }
                    if (child.Kind == NodeKind.Member)
                    {
                        return new HasMemberRelationship(parentId, child.Id);
                    }
                }
                // Parent is Function
                if (parentId.Contains(":function:"))
                {
                    if (child.Kind == NodeKind.Member)
                    {
                        return new HasVariableRelationship(parentId, child.Id);
                    }
                }
                return new DeclaresRelationship(parentId, child.Id);
            }
        }

        return new ContainsRelationship(parentId, child.Id);
    }

    private static bool IsWorkloadNodeId(string id)
    {
        if (!Urn.TryParse(id, out var urn)) return false;
        var d = urn.Domain.ToLowerInvariant();
        return d is OntologyConstants.IdPrefixes.Service or "service"
                 or OntologyConstants.IdPrefixes.App or "app"
                 or OntologyConstants.IdPrefixes.Worker or "worker"
                 or OntologyConstants.IdPrefixes.CliTool or "cli"
                 or OntologyConstants.IdPrefixes.Library or "library"
                 or OntologyConstants.IdPrefixes.TestSuite or "testsuite";
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
               lower.Contains($":{OntologyConstants.IdPrefixes.CallLiteral}:") ||
               lower.Contains(":lit:") ||
               lower.Contains($":{OntologyConstants.IdPrefixes.CatchClause}:") ||
               lower.Contains(":catch:");
    }

    private static bool IsCodeEntityKind(NodeKind kind)
    {
        return kind == NodeKind.Type ||
               kind == NodeKind.Function ||
               kind == NodeKind.Member ||
               kind == NodeKind.CallLiteral ||
               kind == NodeKind.CatchClause;
    }

    private static bool IsProjectNodeId(string id)
    {
        return !id.EndsWith(":syntax", StringComparison.OrdinalIgnoreCase) &&
               Urn.TryParse(id, out var urn) &&
               (urn.Domain.Equals(OntologyConstants.IdPrefixes.Project, StringComparison.OrdinalIgnoreCase) || urn.Domain.Equals("project", StringComparison.OrdinalIgnoreCase));
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
                     or OntologyConstants.IdPrefixes.CallLiteral or "lit"
                     or OntologyConstants.IdPrefixes.CatchClause or "catch"
                     || id.Contains(":function:") || id.Contains(":Function:");
        }
        return false;
    }
}

