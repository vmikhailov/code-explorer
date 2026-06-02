using System;
using CodeExplorer.Common;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using CodeExplorer.Database;

namespace CodeExplorer.Parser;

public class WorkspaceParser
{
    internal static readonly List<ILanguageParser> Parsers = new();

    public static void Register(ILanguageParser parser)
    {
        lock (Parsers)
        {
            if (!Parsers.Any(p => p.GetType() == parser.GetType()))
                Parsers.Add(parser);
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
                    Console.Error.WriteLine($"[PersistenceConsumer] Error writing to database: {ex.Message}");
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
        Console.Error.WriteLine("[WorkspaceParser] All background channel persistence writes completed successfully!");

        // 5. Upload local cross-project dependencies
        if (ctx.GlobalProjectDependencies.Count > 0)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Uploading {ctx.GlobalProjectDependencies.Count} local project dependency relationships...");
            await _dbClient.UploadRelationshipsAsync(ctx.GlobalProjectDependencies);
            ctx.TotalRelsCount += ctx.GlobalProjectDependencies.Count;
        }

        // 6. Deferred Global Reference Resolution & Final Reference Upload
        Console.Error.WriteLine($"[WorkspaceParser] Resolving {ctx.GlobalReferences.Count} global cross-references...");
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
                            referenceRelationships.Add(new Relationship(refItem.ScopeSymbolId, targetNodeId, OntologyConstants.Relationships.Calls));
                        }
                    }
                }
                else if (refItem.Kind == OntologyConstants.Relationships.UsesType)
                {
                    lock (ctx.GlobalSymbols)
                    {
                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Interface, refItem.TargetName), out var targetNodeId))
                        {
                            referenceRelationships.Add(new Relationship(refItem.ScopeSymbolId, targetNodeId, OntologyConstants.Relationships.UsesType));
                        }
                        else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Class, refItem.TargetName), out var targetClassId))
                        {
                            referenceRelationships.Add(new Relationship(refItem.ScopeSymbolId, targetClassId, OntologyConstants.Relationships.UsesType));
                        }
                    }
                }
                else if (refItem.Kind == OntologyConstants.Relationships.Implements || refItem.Kind == OntologyConstants.Relationships.InheritsFrom)
                {
                    lock (ctx.GlobalSymbols)
                    {
                        if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Interface, refItem.TargetName), out var targetNodeId))
                        {
                            referenceRelationships.Add(new Relationship(refItem.ScopeSymbolId, targetNodeId, refItem.Kind));
                        }
                        else if (ctx.GlobalSymbols.TryGetValue((OntologyConstants.NodeLabels.Class, refItem.TargetName), out var targetClassId))
                        {
                            referenceRelationships.Add(new Relationship(refItem.ScopeSymbolId, targetClassId, refItem.Kind));
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
                                    referenceRelationships.Add(new Relationship(refItem.ScopeSymbolId, targetNodeId, OntologyConstants.Relationships.UsesType));
                                }
                            }
                        }
                    }
                }
            }
        }

        if (referenceRelationships.Count > 0)
        {
            Console.Error.WriteLine($"[WorkspaceParser] Uploading {referenceRelationships.Count} resolved reference relationships...");
            await _dbClient.UploadRelationshipsAsync(referenceRelationships);
            ctx.TotalRelsCount += referenceRelationships.Count;
        }

        return (ctx.TotalNodesCount, ctx.TotalRelsCount, ctx.NodesByKind);
    }
}
