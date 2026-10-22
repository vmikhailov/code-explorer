using CodeExplorer.Common;

namespace CodeExplorer.Core.Common.Nodes;

public interface IOntologyNode
{
    string Id { get; }
    string Kind { get; }
    string Path { get; }
    Dictionary<string, string>? Extensions { get; set; }
    List<IOntologyNode> Children { get; }
    List<Reference> References { get; }
}

public static class OntologyNodeExtensions
{
    public static void SetExtension(this IOntologyNode node, string key, string value)
    {
        node.Extensions ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        node.Extensions[key] = value;
    }

    public static string GetName(this IOntologyNode node) => node switch
    {
        Layer4_Semantic.ServiceNode s => s.Name,
        Layer4_Semantic.AppNode a => a.Name,
        Layer4_Semantic.WorkerNode w => w.Name,
        Layer4_Semantic.CliToolNode c => c.Name,
        Layer4_Semantic.LibraryNode l => l.Name,
        Layer4_Semantic.TestSuiteNode t => t.Name,
        Layer2_Boundaries.ProjectNode p => p.Name,
        Layer1_Physical.FileNode f => f.Name,
        Layer3_Syntactic.TypeNode ty => ty.Name,
        Layer3_Syntactic.FunctionNode fn => fn.Name,
        Layer3_Syntactic.MemberNode m => m.Name,
        _ => node.Id.Contains(':') ? node.Id[(node.Id.LastIndexOf(':') + 1)..] : node.Id
    };
}
