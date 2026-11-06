using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class GrpcGatewayGoLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.GrpcFramework;
    public string Name => "grpc-gateway (Go)";
    public string Id => "grpc-gateway-go";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/grpc-ecosystem/grpc-gateway*",
        "github.com/grpc-ecosystem/grpc-gateway/v2*"
    ];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly char[] TrimQuotes = ['"', '`'];
    private const string Slash = "/";
    private const string Colon = ":";
    private const string HandleMethod = "Handle";
    private const string PatternPrefix = "pattern_";

    private static readonly Regex PatternVarRegex = new(@"\bpattern_([A-Za-z0-9]+)_([A-Za-z0-9_]+)_[0-9]+\b", RegexOptions.Compiled);
    private static readonly HashSet<string> HttpVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD"
    };

    private static readonly ConcurrentDictionary<string, Dictionary<string, (string Route, string Service, string Method)>> PatternCache = new();

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGrpcGatewayCall(node, out _, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsGrpcGatewayCall(node, out var verb, out var route, out _, out _))
        {
            return $"{verb}:{route}";
        }
        return null;
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsGrpcGatewayCall(node, out var verb, out _, out var service, out var method))
        {
            symbol.Protocol = "REST";
            symbol.Properties["protocol"] = "REST";
            symbol.Properties["grpc_service"] = service;
            symbol.Properties["grpc_method"] = method;
            symbol.Properties["operation"] = verb;
            symbol.OperationType = CodeExplorer.Cypher.Common.OperationType.Unary;
        }
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsGrpcGatewayCall(node, out _, out _, out var service, out var method))
        {
            var targetRpc = $"RPC:/{service}/{method}";
            references.Add(new Reference(scopeSymbolId, targetRpc, OntologyConstants.Relationships.Calls,
                (int)node.StartPosition.Row + 1,
                (int)node.StartPosition.Column + 1));
        }
    }

    public static bool IsGrpcGatewayCall(Node node, out string verb, out string route, out string service, out string method)
    {
        verb = "";
        route = "";
        service = "";
        method = "";

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid() || field.Text != HandleMethod) return false;

        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count < 2) return false;

        var firstArgVal = GoAstHelper.ResolveStringOrVariable(args[0])?.Trim(TrimQuotes);
        if (string.IsNullOrEmpty(firstArgVal) || !HttpVerbs.Contains(firstArgVal)) return false;
        verb = firstArgVal.ToUpperInvariant();

        var patternArg = args[1].Text;
        var pMatch = PatternVarRegex.Match(patternArg);
        if (!pMatch.Success && !patternArg.StartsWith(PatternPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var root = GetFileRoot(node);
        var rootKey = !string.IsNullOrEmpty(node.Document?.FilePath)
            ? node.Document.FilePath
            : ((root as SyntaxDocument)?.FilePath ?? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(root).ToString());

        var cache = PatternCache.GetOrAdd(rootKey, _ => ScanPatternsInFile(root));

        if (cache.TryGetValue(patternArg, out var info))
        {
            route = info.Route;
            service = info.Service;
            method = info.Method;
            return true;
        }

        if (pMatch.Success)
        {
            service = pMatch.Groups[1].Value;
            method = pMatch.Groups[2].Value;
            route = $"/{service.ToLowerInvariant()}/{method.ToLowerInvariant()}";
            return true;
        }

        return false;
    }

    private static Node GetFileRoot(Node node)
    {
        var curr = node;
        while (curr.Parent.IsValid())
        {
            curr = curr.Parent;
        }
        return curr;
    }

    private static Dictionary<string, (string Route, string Service, string Method)> ScanPatternsInFile(Node root)
    {
        var result = new Dictionary<string, (string Route, string Service, string Method)>(StringComparer.Ordinal);

        foreach (var desc in root.Descendants())
        {
            if (desc.Is(TreeSitterSyntax.Go.VarSpec))
            {
                var nameNode = desc.GetChildForField(TreeSitterSyntax.Fields.Name);
                if (nameNode.IsValid() && nameNode.Text.StartsWith("pattern_", StringComparison.Ordinal))
                {
                    var varName = nameNode.Text;
                    var match = PatternVarRegex.Match(varName);
                    var serviceName = match.Success ? match.Groups[1].Value : "";
                    var methodName = match.Success ? match.Groups[2].Value : "";

                    var route = ParsePatternDefinition(desc);
                    if (!string.IsNullOrEmpty(route))
                    {
                        result[varName] = (route, serviceName, methodName);
                    }
                }
            }
        }

        return result;
    }

    private static string? ParsePatternDefinition(Node varSpecNode)
    {
        // Search for runtime.NewPattern(...) inside the initializer
        Node? newPatternCall = null;
        foreach (var desc in varSpecNode.Descendants())
        {
            if (desc.Is(TreeSitterSyntax.Go.CallExpression))
            {
                var f = desc.GetFunctionNode();
                if (f.IsValid() && f.Text.EndsWith("NewPattern", StringComparison.Ordinal))
                {
                    newPatternCall = desc;
                    break;
                }
            }
        }

        if (newPatternCall == null || !newPatternCall.IsValid()) return null;

        var args = GoAstHelper.GetCallArguments(newPatternCall);
        if (args.Count < 3) return null;

        // args[1] is []int{...} ops
        var ops = ExtractIntLiterals(args[1]);
        // args[2] is []string{...} pool
        var pool = ExtractStringLiterals(args[2]);
        // args[3] optional verb
        var verb = args.Count > 3 ? GoAstHelper.ResolveStringOrVariable(args[3])?.Trim(TrimQuotes) : null;

        return BuildRouteFromPattern(ops, pool, verb);
    }

    private static List<int> ExtractIntLiterals(Node node)
    {
        var result = new List<int>();
        foreach (var desc in node.Descendants())
        {
            if (desc.Is(TreeSitterSyntax.Go.IntLiteral) && int.TryParse(desc.Text, out var val))
            {
                result.Add(val);
            }
        }
        return result;
    }

    private static List<string> ExtractStringLiterals(Node node)
    {
        var result = new List<string>();
        foreach (var desc in node.Descendants())
        {
            if (desc.IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.StringLiteral, TreeSitterSyntax.Go.RawStringLiteral))
            {
                var clean = desc.Text.Trim(TrimQuotes);
                result.Add(clean);
            }
        }
        return result;
    }

    public static string BuildRouteFromPattern(List<int> ops, List<string> pool, string? verb = null)
    {
        var segments = new List<string>();
        var i = 0;
        while (i < ops.Count)
        {
            if (ops[i] == 2 && i + 1 < ops.Count) // literal segment
            {
                var idx = ops[i + 1];
                if (idx >= 0 && idx < pool.Count)
                {
                    segments.Add(pool[idx]);
                }
                i += 2;
            }
            else if ((ops[i] == 1 || ops[i] == 3) && i + 5 < ops.Count && ops[i + 2] == 4 && ops[i + 4] == 5) // variable with name
            {
                var nameIdx = ops[i + 5];
                if (nameIdx >= 0 && nameIdx < pool.Count)
                {
                    segments.Add($"{{{pool[nameIdx]}}}");
                }
                i += 6;
            }
            else if (ops[i] == 5 && i + 1 < ops.Count) // direct variable index
            {
                var nameIdx = ops[i + 1];
                if (nameIdx >= 0 && nameIdx < pool.Count)
                {
                    segments.Add($"{{{pool[nameIdx]}}}");
                }
                i += 2;
            }
            else
            {
                i++;
            }
        }

        if (segments.Count == 0 && pool.Count > 0)
        {
            segments.AddRange(pool);
        }

        var route = Slash + string.Join(Slash, segments);
        if (!string.IsNullOrEmpty(verb))
        {
            route += Colon + verb;
        }
        return route;
    }
}
