using System.Threading.Channels;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class WorkspaceParser
{
    internal static readonly List<IProjectParser> ProjectParsers = [];
    internal static readonly List<IFileParser> FileParsers = [];

    public static void Register(object parser)
    {
        if (parser is IProjectParser projectParser)
        {
            lock (ProjectParsers)
            {
                if (ProjectParsers.All(p => p.GetType() != projectParser.GetType()))
                    ProjectParsers.Add(projectParser);
            }
        }
        if (parser is IFileParser fileParser)
        {
            lock (FileParsers)
            {
                if (FileParsers.All(p => p.GetType() != fileParser.GetType()))
                    FileParsers.Add(fileParser);
            }
        }
    }

    private readonly string _hostWorkspacePath;
    private readonly string _absoluteWorkspacePath;
    private readonly MemgraphClient _dbClient;
    private readonly bool _clear;

    public WorkspaceParser(string hostWorkspacePath, string containerWorkspacePath, MemgraphClient dbClient, bool clear)
    {
        _hostWorkspacePath = hostWorkspacePath;
        _absoluteWorkspacePath = Path.GetFullPath(containerWorkspacePath).Replace('\\', '/');
        _dbClient = dbClient;
        _clear = clear;
    }

    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync()
    {
        // 1. Setup the background persistence channel & consumer task
        var sharedChannel = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );

        var ctx = new ParsingContext(_absoluteWorkspacePath, _hostWorkspacePath, _dbClient, sharedChannel, _clear);

        ctx.Log("[WorkspaceParser] Starting background database persistence loop...");
        var consumerTask = Task.Run(async () =>
        {
            await foreach (var writeFunc in sharedChannel.Reader.ReadAllAsync())
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

        // 2. Create root indices sequentially
        await _dbClient.CreateIndicesAsync();

        // 3. Run WorkspaceLevelParser
        var scanner = new WorkspaceLevelParser(ctx);
        await scanner.ParseAsync();

        // 4. Complete persistence channel & await background consumer
        sharedChannel.Writer.Complete();
        await consumerTask;
        ctx.Log($"[WorkspaceParser] All background channel persistence writes completed! Total parsed: {ctx.GetTotalNodesPersisted()} nodes, {ctx.GetTotalRelsPersisted()} relationships.");

        // 5. Upload local cross-project dependencies
        if (ctx.GlobalProjectDependencies.Count > 0)
        {
            ctx.Log($"[WorkspaceParser] Uploading {ctx.GlobalProjectDependencies.Count} local project dependency relationships...");
            await _dbClient.UploadRelationshipsAsync(ctx.GlobalProjectDependencies);
            ctx.TotalRelsCount += ctx.GlobalProjectDependencies.Count;
        }

        // 6. Deferred Global Reference Resolution & Final Reference Upload
        var totalReferences = ctx.GlobalReferences.Count;
        ctx.Log($"[WorkspaceParser] Resolving {totalReferences} global cross-references...");
        var referenceRelationships = new List<Relationship>();
        var inheritanceRels = new HashSet<(string From, string To)>();

        lock (ctx.GlobalReferences)
        {
            // Pass 1: Resolve all inheritance (Implements / InheritsFrom) relationships first and cache them in a HashSet.
            foreach (var refItem in ctx.GlobalReferences)
            {
                if (refItem.Kind == OntologyConstants.Relationships.Implements || refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
                {
                    lock (ctx.GlobalSymbols)
                    {
                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Interface, refItem.TargetName), out var targetNodeId))
                        {
                            IOntologyRelationship rel = new ImplementsRelationship(refItem.ScopeSymbolId, targetNodeId);
                            referenceRelationships.Add(Relationship.FromRelationship(rel));
                            inheritanceRels.Add((refItem.ScopeSymbolId, targetNodeId));
                        }
                        else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Class, refItem.TargetName), out var targetClassId))
                        {
                            IOntologyRelationship rel = new InheritsFromRelationship(refItem.ScopeSymbolId, targetClassId);
                            referenceRelationships.Add(Relationship.FromRelationship(rel));
                            inheritanceRels.Add((refItem.ScopeSymbolId, targetClassId));
                        }
                        else if (refItem.Kind == OntologyConstants.Relationships.Implements &&
                                 ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.EntryPoint, refItem.TargetName), out var targetEpId))
                        {
                            referenceRelationships.Add(Relationship.FromRelationship(new ImplementedByRelationship(targetEpId, refItem.ScopeSymbolId)));
                            inheritanceRels.Add((targetEpId, refItem.ScopeSymbolId));
                        }
                    }
                }
            }

            // Pass 2: Resolve all other relationships using the cached inheritance relationships.
            var resolvedCount = 0;
            foreach (var refItem in ctx.GlobalReferences)
            {
                resolvedCount++;
                if (resolvedCount % 100000 == 0)
                {
                    ctx.Log($"[WorkspaceParser] Resolving global cross-references: {resolvedCount}/{totalReferences}...");
                }

                if (refItem.Kind == OntologyConstants.Relationships.Calls)
                {
                    lock (ctx.GlobalSymbols)
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
                                lock (ctx.RawTypeBindings)
                                {
                                    // Priority 1: Match by scope name
                                    binding = ctx.RawTypeBindings.FirstOrDefault(b =>
                                        b.FilePath == filePath &&
                                        b.VariableName == varName &&
                                        refItem.ScopeSymbolId.Contains($":{b.ScopeId}:"));

                                    // Priority 2: Fallback to any binding in the same file
                                    if (binding == null)
                                    {
                                        binding = ctx.RawTypeBindings.FirstOrDefault(b =>
                                            b.FilePath == filePath &&
                                            b.VariableName == varName);
                                    }
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

                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Function, targetName), out var targetNodeId))
                        {
                            referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, targetNodeId)));
                        }
                        else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Procedure, targetName), out var targetProcId))
                        {
                            referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, targetProcId)));
                        }
                    }
                }
                else if (refItem.Kind == OntologyConstants.Relationships.DependsOn)
                {
                    lock (ctx.GlobalSymbols)
                    {
                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Table, refItem.TargetName), out var targetTableId))
                        {
                            referenceRelationships.Add(Relationship.FromRelationship(new DependsOnRelationship(refItem.ScopeSymbolId, targetTableId)));
                        }
                    }
                }
                else if (refItem.Kind == OntologyConstants.Relationships.UsesType)
                {
                    lock (ctx.GlobalSymbols)
                    {
                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Interface, refItem.TargetName), out var targetNodeId))
                        {
                            referenceRelationships.Add(Relationship.FromRelationship(new UsesTypeRelationship(refItem.ScopeSymbolId, targetNodeId)));
                        }
                        else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Class, refItem.TargetName), out var targetClassId))
                        {
                            referenceRelationships.Add(Relationship.FromRelationship(new UsesTypeRelationship(refItem.ScopeSymbolId, targetClassId)));
                        }
                    }
                }
                else if (refItem.Kind == OntologyConstants.Relationships.PotentialType)
                {
                    lock (ctx.GlobalSymbols)
                    {
                        string? targetNodeId = null;
                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Interface, refItem.TargetName), out var targetIntfId))
                        {
                            targetNodeId = targetIntfId;
                        }
                        else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Class, refItem.TargetName), out var targetClassId))
                        {
                            targetNodeId = targetClassId;
                        }

                        if (targetNodeId != null)
                        {
                            if (refItem.ScopeSymbolId != targetNodeId)
                            {
                                if (!inheritanceRels.Contains((refItem.ScopeSymbolId, targetNodeId)))
                                {
                                    referenceRelationships.Add(Relationship.FromRelationship(new UsesTypeRelationship(refItem.ScopeSymbolId, targetNodeId)));
                                }
                            }
                        }
                    }
                }
                else if (refItem.Kind == OntologyConstants.Relationships.Triggers)
                {
                    lock (ctx.GlobalSymbols)
                    {
                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Function, refItem.TargetName), out var targetNodeId))
                        {
                            referenceRelationships.Add(Relationship.FromRelationship(new TriggersRelationship(refItem.ScopeSymbolId, targetNodeId)));
                        }
                    }
                }
            }
        }

        if (referenceRelationships.Count > 0)
        {
            ctx.Log($"[WorkspaceParser] Uploading {referenceRelationships.Count} resolved reference relationships...");
            await _dbClient.UploadRelationshipsAsync(referenceRelationships);
            ctx.TotalRelsCount += referenceRelationships.Count;
        }

        // Run Layer 2 PostIndexAnalyzer
        ctx.Log($"[WorkspaceParser] Running Layer 2 semantic analysis via PostIndexAnalyzer...");
        var postAnalyzer = new PostIndexAnalyzer(_dbClient);
        await postAnalyzer.RunAsync(ctx.WorkspaceId);

        ctx.Log($"[WorkspaceParser] Indexing process completed successfully! Total Nodes: {ctx.TotalNodesCount}, Total Relationships: {ctx.TotalRelsCount}.");

        return (ctx.TotalNodesCount, ctx.TotalRelsCount, ctx.NodesByKind);
    }
}
