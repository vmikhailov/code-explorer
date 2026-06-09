using System.Threading.Channels;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class WorkspaceIndexer
{
    internal static readonly List<IProjectParser> _projectParsers = [];
    internal static readonly List<IFileParser> _fileParsers = [];

    public static void Register(object parser)
    {
        if (parser is IProjectParser projectParser)
        {
            if (_projectParsers.All(p => p.GetType() != projectParser.GetType()))
                _projectParsers.Add(projectParser);
        }

        if (parser is IFileParser fileParser)
        {
            if (_fileParsers.All(p => p.GetType() != fileParser.GetType()))
                _fileParsers.Add(fileParser);
        }
    }

    private readonly MemgraphClient _dbClient;

    public WorkspaceIndexer(MemgraphClient dbClient)
    {
        _dbClient = dbClient;
    }

    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync(
        string hostWorkspacePath,
        string containerWorkspacePath,
        bool clear,
        CancellationToken cancellationToken = default,
        Action<ParsingContext>? onContextCreated = null)
    {
        var ctx = CreateContext(hostWorkspacePath, containerWorkspacePath, clear, cancellationToken);
        onContextCreated?.Invoke(ctx);

        await RunLayer1ScanAsync(ctx);
        await UploadProjectDependenciesAsync(ctx);
        await ResolveAndUploadGlobalReferencesAsync(ctx);
        await RunLayer2SemanticAnalysisAsync(ctx);

        ctx.Log(
            $"[WorkspaceIndexer] Indexing process completed successfully! Total Nodes: {ctx.TotalNodesCount}, Total Relationships: {ctx.TotalRelsCount}.");

        return (ctx.TotalNodesCount, ctx.TotalRelsCount, ctx.NodesByKind);
    }

    private ParsingContext CreateContext(string hostWorkspacePath, string containerWorkspacePath, bool clear, CancellationToken cancellationToken)
    {
        var resolvedPath = PathTools.TranslateHostPathToContainerPath(containerWorkspacePath);

        if (!Directory.Exists(resolvedPath))
        {
            throw new DirectoryNotFoundException(
                $"Directory '{containerWorkspacePath}' (resolved as '{resolvedPath}') does not exist.");
        }

        var absoluteWorkspacePath = Path.GetFullPath(resolvedPath).Replace('\\', '/');

        var sharedChannel = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

        return new ParsingContext(absoluteWorkspacePath, hostWorkspacePath, _dbClient, sharedChannel, clear, cancellationToken: cancellationToken);
    }

    private async Task RunLayer1ScanAsync(ParsingContext ctx)
    {
        ctx.Log("[WorkspaceIndexer] Starting background database persistence loop...");

        var consumerTask = Task.Run(async () =>
        {
            await foreach (var writeFunc in ctx.SharedChannel.Reader.ReadAllAsync())
            {
                try
                {
                    await writeFunc();
                }
                catch (Exception ex)
                {
                    ctx.Log($"[PersistenceConsumer] Error writing to database: {ex.Message}");
                }
            }
        });

        // Clear database sequentially at root to avoid contentions
        if (ctx.Clear)
        {
            ctx.Log($"[WorkspaceIndexer] Clearing previous root workspace data for '{ctx.HostWorkspacePath}'...");
            await _dbClient.ClearWorkspaceAsync(ctx.HostWorkspacePath);
        }

        // Create root indices sequentially
        await _dbClient.CreateIndicesAsync();

        try
        {
            // Run WorkspaceParser
            var scanner = new WorkspaceParser(ctx);
            await scanner.ParseAsync();
        }
        finally
        {
            // Complete persistence channel & await background consumer
            ctx.SharedChannel.Writer.Complete();
            try
            {
                await consumerTask;
            }
            catch (Exception ex)
            {
                ctx.Log($"[WorkspaceIndexer] Consumer task finished with error: {ex.Message}");
            }
        }

        ctx.Log(
            $"[WorkspaceIndexer] All background channel persistence writes completed! Total parsed: {ctx.GetTotalNodesPersisted()} nodes, {ctx.GetTotalRelsPersisted()} relationships.");
    }

    private async Task UploadProjectDependenciesAsync(ParsingContext ctx)
    {
        if (ctx.GlobalProjectDependencies.Count > 0)
        {
            ctx.Log(
                $"[WorkspaceIndexer] Uploading {ctx.GlobalProjectDependencies.Count} local project dependency relationships...");
            await _dbClient.UploadRelationshipsAsync(ctx.GlobalProjectDependencies);
            ctx.TotalRelsCount += ctx.GlobalProjectDependencies.Count;
        }
    }

    private async Task ResolveAndUploadGlobalReferencesAsync(ParsingContext ctx)
    {
        var totalReferences = ctx.GlobalReferences.Count;
        ctx.Log($"[WorkspaceIndexer] Resolving {totalReferences} global cross-references...");
        var referenceRelationships = new List<Relationship>();
        var inheritanceRels = new HashSet<(string From, string To)>();

        // Pass 1: Resolve all inheritance (Implements / InheritsFrom) relationships first and cache them in a HashSet.
        foreach (var refItem in ctx.GlobalReferences)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();

            if (refItem.Kind == OntologyConstants.Relationships.Implements ||
                refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
            {
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Type, refItem.TargetName),
                        out var targetNodeId))
                {
                    if (refItem.Kind == OntologyConstants.Relationships.Implements)
                    {
                        IOntologyRelationship rel = new ImplementsRelationship(refItem.ScopeSymbolId, targetNodeId);
                        referenceRelationships.Add(Relationship.FromRelationship(rel));
                    }
                    else
                    {
                        IOntologyRelationship rel = new InheritsFromRelationship(refItem.ScopeSymbolId, targetNodeId);
                        referenceRelationships.Add(Relationship.FromRelationship(rel));
                    }
                    inheritanceRels.Add((refItem.ScopeSymbolId, targetNodeId));
                }
                else if (refItem.Kind == OntologyConstants.Relationships.Implements)
                {
                    if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Endpoint, refItem.TargetName),
                             out var targetEndpointId))
                    {
                        referenceRelationships.Add(
                            Relationship.FromRelationship(new ExposedByRelationship(targetEndpointId, refItem.ScopeSymbolId)));
                    }
                    else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.EntryPoint, refItem.TargetName),
                             out var targetEpId))
                    {
                        referenceRelationships.Add(
                            Relationship.FromRelationship(new ImplementedByRelationship(targetEpId,
                                refItem.ScopeSymbolId)));
                        inheritanceRels.Add((targetEpId, refItem.ScopeSymbolId));
                    }
                }
            }
        }

        // Pass 2: Resolve all other relationships using the cached inheritance relationships.
        var resolvedCount = 0;

        foreach (var refItem in ctx.GlobalReferences)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            resolvedCount++;

            if (resolvedCount % 100000 == 0)
            {
                ctx.Log($"[WorkspaceIndexer] Resolving global cross-references: {resolvedCount}/{totalReferences}...");
            }

            if (refItem.Kind == OntologyConstants.Relationships.Calls)
            {
                var targetName = refItem.TargetName;

                if (targetName.Contains('.'))
                {
                    var dotIdx = targetName.LastIndexOf('.');
                    var varName = targetName.Substring(0, dotIdx);
                    var methodName = targetName.Substring(dotIdx + 1);

                    string? filePath = null;
                    var scopeParts = refItem.ScopeSymbolId.Split(':');

                    if (scopeParts.Length > 2 && scopeParts[1] == "symbol")
                    {
                        filePath = scopeParts[2];
                    }

                    if (filePath != null)
                    {
                        RawTypeBinding? binding = null;

                        // Priority 1: Match by scope name
                        binding = ctx.RawTypeBindings.FirstOrDefault(b =>
                            b.FilePath == filePath && b.VariableName == varName &&
                            refItem.ScopeSymbolId.Contains($":{b.ScopeId}:"));

                        // Priority 2: Fallback to any binding in the same file
                        if (binding == null)
                        {
                            binding = ctx.RawTypeBindings.FirstOrDefault(b =>
                                b.FilePath == filePath && b.VariableName == varName);
                        }

                        if (binding != null)
                        {
                            targetName = $"{binding.TypeName}.{methodName}";
                        }
                        else
                        {
                            targetName = methodName;
                        }
                    }
                    else
                    {
                        targetName = methodName;
                    }
                }

                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Function, targetName),
                        out var targetNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, targetNodeId)));
                }
                else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Procedure, targetName),
                             out var targetProcId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new CalledByRelationship(targetProcId, refItem.ScopeSymbolId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.DependsOn)
            {
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Table, refItem.TargetName),
                        out var targetTableId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new QueriedByRelationship(targetTableId, refItem.ScopeSymbolId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.UsesType)
            {
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Type, refItem.TargetName),
                        out var targetNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new UsesTypeRelationship(refItem.ScopeSymbolId, targetNodeId)));
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.PotentialType)
            {
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Type, refItem.TargetName),
                        out var targetNodeId))
                {
                    if (refItem.ScopeSymbolId != targetNodeId)
                    {
                        if (!inheritanceRels.Contains((refItem.ScopeSymbolId, targetNodeId)))
                        {
                            referenceRelationships.Add(
                                Relationship.FromRelationship(new UsesTypeRelationship(refItem.ScopeSymbolId,
                                    targetNodeId)));
                        }
                    }
                }
            }
            else if (refItem.Kind == OntologyConstants.Relationships.Triggers)
            {
                if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Function, refItem.TargetName),
                        out var targetNodeId))
                {
                    referenceRelationships.Add(
                        Relationship.FromRelationship(new TriggersRelationship(refItem.ScopeSymbolId, targetNodeId)));
                }
            }
        }

        if (referenceRelationships.Count > 0)
        {
            ctx.Log($"[WorkspaceIndexer] Uploading {referenceRelationships.Count} resolved reference relationships...");
            await _dbClient.UploadRelationshipsAsync(referenceRelationships);
            ctx.TotalRelsCount += referenceRelationships.Count;
        }
    }

    private async Task RunLayer2SemanticAnalysisAsync(ParsingContext ctx)
    {
        ctx.Log($"[WorkspaceIndexer] Running Layer 2 semantic analysis via PostIndexAnalyzer...");
        var postAnalyzer = new PostIndexAnalyzer(_dbClient);
        await postAnalyzer.RunAsync(ctx.WorkspaceId);
    }
}
