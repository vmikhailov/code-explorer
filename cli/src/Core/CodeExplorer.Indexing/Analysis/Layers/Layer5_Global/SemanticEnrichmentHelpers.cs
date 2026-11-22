using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser.Layers;

internal static class SemanticEnrichmentHelpers
{
    private const string ThisDotPrefix = "this.";
    private const string SelfDotPrefix = "self.";
    private const string PhpArrow = "->";
    private const string ScopeDoubleColon = "::";

    internal sealed record SyntaxTypeMethodIndex(
        Dictionary<string, TypeNode> TypesById,
        Dictionary<string, TypeNode> TypesByName,
        Dictionary<string, List<FunctionNode>> MethodsByTypeId,
        Dictionary<string, FunctionNode> FunctionsById,
        List<FunctionNode> TopLevelFunctions);

    public static SyntaxTypeMethodIndex CollectTypesAndMethods(IOntologyNode syntaxRoot)
    {
        var typesById = new Dictionary<string, TypeNode>(StringComparer.Ordinal);
        var typesByName = new Dictionary<string, TypeNode>(StringComparer.OrdinalIgnoreCase);
        var methodsByType = new Dictionary<string, List<FunctionNode>>(StringComparer.Ordinal);
        var functionsById = new Dictionary<string, FunctionNode>(StringComparer.Ordinal);
        var topLevelFunctions = new List<FunctionNode>();

        WalkSyntax(syntaxRoot, parentType: null, typesById, typesByName, methodsByType, functionsById, topLevelFunctions);
        return new SyntaxTypeMethodIndex(typesById, typesByName, methodsByType, functionsById, topLevelFunctions);
    }

    private static void WalkSyntax(
        IOntologyNode node,
        TypeNode? parentType,
        Dictionary<string, TypeNode> typesById,
        Dictionary<string, TypeNode> typesByName,
        Dictionary<string, List<FunctionNode>> methodsByType,
        Dictionary<string, FunctionNode> functionsById,
        List<FunctionNode> topLevelFunctions)
    {
        var currentType = parentType;
        if (node is TypeNode tn)
        {
            typesById[tn.Id] = tn;
            if (!string.IsNullOrEmpty(tn.Name) && (!typesByName.ContainsKey(tn.Name) || !tn.IsTest))
            {
                typesByName[tn.Name] = tn;
            }
            if (!methodsByType.ContainsKey(tn.Id))
            {
                methodsByType[tn.Id] = [];
            }
            currentType = tn;
        }
        else if (node is FunctionNode fn)
        {
            functionsById[fn.Id] = fn;
            if (currentType != null)
            {
                methodsByType[currentType.Id].Add(fn);
            }
            else
            {
                topLevelFunctions.Add(fn);
            }
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            WalkSyntax(node.Children[i], currentType, typesById, typesByName, methodsByType, functionsById, topLevelFunctions);
        }
    }

    public static Dictionary<string, FileNode> CollectFilesByPath(Layer4Result l4Result)
    {
        var map = new Dictionary<string, FileNode>(StringComparer.OrdinalIgnoreCase);
        var fs = l4Result.Prev?.Prev?.Prev?.FilesStructure;
        if (fs != null) WalkFiles(fs, map);
        return map;
    }

    private static void WalkFiles(IOntologyNode node, Dictionary<string, FileNode> map)
    {
        if (node is FileNode fn && !string.IsNullOrEmpty(fn.Path))
        {
            map[fn.Path.Replace('\\', '/')] = fn;
        }
        for (var i = 0; i < node.Children.Count; i++)
        {
            WalkFiles(node.Children[i], map);
        }
    }

    public static List<ModuleNode> CollectModules(IOntologyNode? semStructure)
    {
        var list = new List<ModuleNode>();
        if (semStructure == null) return list;
        for (var i = 0; i < semStructure.Children.Count; i++)
        {
            if (semStructure.Children[i] is ModuleNode mn and not TestSuiteNode)
            {
                list.Add(mn);
            }
        }
        return list;
    }

    public static ModuleNode? FindOwningModule(string normFilePath, List<ModuleNode> modules)
    {
        ModuleNode? best = null;
        var bestLen = -1;
        for (var i = 0; i < modules.Count; i++)
        {
            var m = modules[i];
            var mp = m.Path.Replace('\\', '/').TrimEnd('/');
            if (mp.Length == 0 ||
                normFilePath.StartsWith(mp + '/', StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normFilePath, mp, StringComparison.OrdinalIgnoreCase))
            {
                if (mp.Length > bestLen)
                {
                    best = m;
                    bestLen = mp.Length;
                }
            }
        }
        return best;
    }

    public static string ExtractMethodTail(string callee)
    {
        if (string.IsNullOrEmpty(callee)) return string.Empty;
        var s = callee.Replace(PhpArrow, ".");
        var colons = s.LastIndexOf(ScopeDoubleColon, StringComparison.Ordinal);
        if (colons >= 0) s = s[(colons + ScopeDoubleColon.Length)..];
        var dot = s.LastIndexOf('.');
        return dot >= 0 ? s[(dot + 1)..].Trim() : s.Trim();
    }

    public static string CleanReceiverName(string rawReceiver)
    {
        var s = rawReceiver.Trim().Replace(PhpArrow, ".");
        if (s.StartsWith(ThisDotPrefix, StringComparison.Ordinal)) s = s[ThisDotPrefix.Length..];
        else if (s.StartsWith(SelfDotPrefix, StringComparison.Ordinal)) s = s[SelfDotPrefix.Length..];
        s = s.TrimStart('$');
        var dot = s.LastIndexOf('.');
        return dot >= 0 ? s[(dot + 1)..].Trim() : s;
    }

    public static Dictionary<string, List<Reference>> BuildRefsByScopeId(List<Reference> globalReferences)
    {
        var map = new Dictionary<string, List<Reference>>(StringComparer.Ordinal);
        for (var i = 0; i < globalReferences.Count; i++)
        {
            var r = globalReferences[i];
            if (string.IsNullOrEmpty(r.ScopeSymbolId)) continue;
            if (!map.TryGetValue(r.ScopeSymbolId, out var list))
            {
                list = [];
                map[r.ScopeSymbolId] = list;
            }
            list.Add(r);
        }
        return map;
    }

    public static IReadOnlyList<Reference> GetScopeReferences(
        string scopeId,
        List<Reference> inlineRefs,
        Dictionary<string, List<Reference>> refsByScopeId)
    {
        if (inlineRefs.Count > 0) return inlineRefs;
        return refsByScopeId.TryGetValue(scopeId, out var list) ? list : Array.Empty<Reference>();
    }

    public static bool TryGetReceiverAndMethod(Reference r, out string receiver, out string method)
    {
        if (r.HasExplicitReceiver && !string.IsNullOrEmpty(r.ReceiverName))
        {
            receiver = CleanReceiverName(r.ReceiverName);
            method = ExtractMethodTail(r.MemberName);
            return receiver.Length > 0 && method.Length > 0;
        }

        var targetName = r.TargetName;
        receiver = string.Empty;
        method = string.Empty;
        if (string.IsNullOrEmpty(targetName)) return false;

        var colons = targetName.IndexOf(ScopeDoubleColon, StringComparison.Ordinal);
        if (colons > 0 && colons + ScopeDoubleColon.Length < targetName.Length)
        {
            receiver = CleanReceiverName(targetName[..colons]);
            method = targetName[(colons + ScopeDoubleColon.Length)..];
            return receiver.Length > 0 && method.Length > 0;
        }

        var dot = targetName.LastIndexOf('.');
        if (dot <= 0 || dot >= targetName.Length - 1) return false;
        receiver = CleanReceiverName(targetName[..dot]);
        method = targetName[(dot + 1)..];
        return receiver.Length > 0 && method.Length > 0;
    }

    public static void AddUniqueRel(
        IOntologyRelationship ontRel,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var r = Relationship.FromRelationship(ontRel);
        if (existingRefRels.Add((r.From, r.To, GraphKindCodec.DecodeRelKind((int)r.Kind))))
        {
            rels.Add(r);
        }
    }
}
