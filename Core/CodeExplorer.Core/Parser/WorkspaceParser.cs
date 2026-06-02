using System.Threading.Channels;
using CodeExplorer.Common;
using CodeExplorer.Database;

namespace CodeExplorer.Parser;

public class WorkspaceParser
{
    internal static readonly List<IProjectParser> ProjectParsers = new();
    internal static readonly List<IFileParser> FileParsers = new();

    public static void Register(object parser)
    {
        if (parser is IProjectParser projectParser)
        {
            lock (ProjectParsers)
            {
                if (!ProjectParsers.Any(p => p.GetType() == projectParser.GetType()))
                    ProjectParsers.Add(projectParser);
            }
        }
        if (parser is IFileParser fileParser)
        {
            lock (FileParsers)
            {
                if (!FileParsers.Any(p => p.GetType() == fileParser.GetType()))
                    FileParsers.Add(fileParser);
            }
        }
    }

    private readonly string _absoluteWorkspacePath;
    private readonly MemgraphClient _dbClient;
    private readonly bool _clear;

    public WorkspaceParser(string dirPath, MemgraphClient dbClient, bool clear)
    {
        _absoluteWorkspacePath = Path.GetFullPath(dirPath).Replace('\\', '/');
        _dbClient = dbClient;
        _clear = clear;
    }

    public async Task<(int NodesCount, int RelationshipsCount, Dictionary<string, int> NodesByKind)> IndexAsync()
    {
        // 1. Clear/Create root indices sequentially
        await _dbClient.CreateIndicesAsync();

        // 2. Setup the background persistence channel & consumer task
        var sharedChannel = Channel.CreateUnbounded<Func<Task>>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = false }
        );

        await Console.Error.WriteLineAsync("[WorkspaceParser] Starting background database persistence loop...");
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
                    await Console.Error.WriteLineAsync($"[PersistenceConsumer] Error writing to database: {ex.Message}");
                }
            }
        });

        // 3. Create shared context and run WorkspaceLevelParser
        var ctx = new ParsingContext(_absoluteWorkspacePath, _dbClient, sharedChannel, _clear);
        var scanner = new WorkspaceLevelParser(ctx);
        await scanner.ParseAsync();

        // 4. Complete persistence channel & await background consumer
        sharedChannel.Writer.Complete();
        await consumerTask;
        await Console.Error.WriteLineAsync($"[WorkspaceParser] All background channel persistence writes completed! Total parsed: {ctx.GetTotalNodesPersisted()} nodes, {ctx.GetTotalRelsPersisted()} relationships.");

        // 5. Upload local cross-project dependencies
        if (ctx.GlobalProjectDependencies.Count > 0)
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Uploading {ctx.GlobalProjectDependencies.Count} local project dependency relationships...");
            await _dbClient.UploadRelationshipsAsync(ctx.GlobalProjectDependencies);
            ctx.TotalRelsCount += ctx.GlobalProjectDependencies.Count;
        }

        // 6. Deferred Global Reference Resolution & Final Reference Upload
        await Console.Error.WriteLineAsync($"[WorkspaceParser] Resolving {ctx.GlobalReferences.Count} global cross-references...");
        var referenceRelationships = new List<Relationship>();

        lock (ctx.GlobalReferences)
        {
            foreach (var refItem in ctx.GlobalReferences)
            {
                if (refItem.Kind == OntologyConstants.Relationships.Calls)
                {
                    lock (ctx.GlobalSymbols)
                    {
                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Function, refItem.TargetName), out var targetNodeId))
                        {
                            referenceRelationships.Add(Relationship.FromRelationship(new CallsRelationship(refItem.ScopeSymbolId, targetNodeId)));
                        }
                        else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Procedure, refItem.TargetName), out var targetProcId))
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
                else if (refItem.Kind == OntologyConstants.Relationships.Implements || refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
                {
                    lock (ctx.GlobalSymbols)
                    {
                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Interface, refItem.TargetName), out var targetNodeId))
                        {
                            IOntologyRelationship rel = refItem.Kind == OntologyConstants.Relationships.Implements
                                ? new ImplementsRelationship(refItem.ScopeSymbolId, targetNodeId)
                                : new InheritsFromRelationship(refItem.ScopeSymbolId, targetNodeId);
                            referenceRelationships.Add(Relationship.FromRelationship(rel));
                        }
                        else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Class, refItem.TargetName), out var targetClassId))
                        {
                            IOntologyRelationship rel = refItem.Kind == OntologyConstants.Relationships.Implements
                                ? new ImplementsRelationship(refItem.ScopeSymbolId, targetClassId)
                                : new InheritsFromRelationship(refItem.ScopeSymbolId, targetClassId);
                            referenceRelationships.Add(Relationship.FromRelationship(rel));
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
                                bool hasInheritance = referenceRelationships.Any(r =>
                                    r.From == refItem.ScopeSymbolId &&
                                    r.To == targetNodeId &&
                                    (r.Kind == OntologyConstants.Relationships.Implements || r.Kind == OntologyConstants.Relationships.InheritsFrom));

                                if (!hasInheritance)
                                {
                                    referenceRelationships.Add(Relationship.FromRelationship(new UsesTypeRelationship(refItem.ScopeSymbolId, targetNodeId)));
                                }
                            }
                        }
                    }
                }
            }
        }

        if (referenceRelationships.Count > 0)
        {
            await Console.Error.WriteLineAsync($"[WorkspaceParser] Uploading {referenceRelationships.Count} resolved reference relationships...");
            await _dbClient.UploadRelationshipsAsync(referenceRelationships);
            ctx.TotalRelsCount += referenceRelationships.Count;
        }

        return (ctx.TotalNodesCount, ctx.TotalRelsCount, ctx.NodesByKind);
    }
}
