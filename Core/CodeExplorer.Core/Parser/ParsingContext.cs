using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using CodeExplorer.Database;

namespace CodeExplorer.Parser;

public class ParsingContext
{
    public string AbsoluteWorkspacePath { get; }
    public MemgraphClient DbClient { get; }
    public Channel<Func<Task>> SharedChannel { get; }
    
    public Dictionary<(string Kind, string Name), string> GlobalSymbols { get; }
    public List<Reference> GlobalReferences { get; }
    public List<Relationship> GlobalProjectDependencies { get; }
    
    public Dictionary<string, int> NodesByKind { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int TotalNodesCount { get; set; }
    public int TotalRelsCount { get; set; }

    public ParsingContext(
        string absoluteWorkspacePath, 
        MemgraphClient dbClient, 
        Channel<Func<Task>> sharedChannel,
        Dictionary<(string Kind, string Name), string>? globalSymbols = null,
        List<Reference>? globalReferences = null,
        List<Relationship>? globalProjectDependencies = null)
    {
        AbsoluteWorkspacePath = absoluteWorkspacePath.Replace('\\', '/');
        DbClient = dbClient;
        SharedChannel = sharedChannel;
        GlobalSymbols = globalSymbols ?? new Dictionary<(string Kind, string Name), string>();
        GlobalReferences = globalReferences ?? new List<Reference>();
        GlobalProjectDependencies = globalProjectDependencies ?? new List<Relationship>();
    }
}
