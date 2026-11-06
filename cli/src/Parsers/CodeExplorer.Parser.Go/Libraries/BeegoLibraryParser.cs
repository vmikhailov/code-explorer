using System.Text.Json;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class BeegoLibraryParser : ISemanticExtension
{
    private const string IdBeego = "beego";
    private const string NameBeego = "Beego";
    private const string WebRoleName = "beego-web";
    private const string WebRoleDesc = "Beego Web";
    private const string OrmRoleName = "beego-orm";
    private const string OrmRoleDesc = "Beego ORM";
    private static readonly char[] QuoteSeparators = ['"', '`'];

    public SemanticExtensionType Type => SemanticExtensionType.Framework;
    public string Name => NameBeego;
    public string Id => IdBeego;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "github.com/beego/beego*",
        "github.com/astaxie/beego*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    public IEnumerable<PackageDescriptor> GetPackageDescriptors()
    {
        yield return new PackageDescriptor(WebRoleName, WebRoleDesc, LibraryRole.WebService, SemanticExtensionType.Framework, SupportedPatterns);
        yield return new PackageDescriptor(OrmRoleName, OrmRoleDesc, LibraryRole.OrmOrDatabase, SemanticExtensionType.RelationalDb, SupportedPatterns);
    }

    private static readonly HashSet<string> DirectHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Get", "Post", "Put", "Delete", "Patch", "Head", "Options", "Any"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsBeegoRouteCall(node, out _, out _, out _, out _))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }

        if (IsBeegoRegisterModelCall(node, out _))
        {
            return OntologyConstants.NodeLabels.Table;
        }

        if (IsBeegoTableNameMethod(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Table;
        }

        if (IsBeegoRawCall(node, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }

        if (IsBeegoQueryTableCall(node, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }

        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsBeegoRouteCall(node, out var httpMethod, out var fullRoute, out _, out _))
        {
            fullRoute = ApplyMountPrefix(fullRoute, relativePath);
            return $"{httpMethod}:{fullRoute}";
        }

        if (IsBeegoRegisterModelCall(node, out var modelNames))
        {
            return JsonNamingPolicy.SnakeCaseLower.ConvertName(modelNames[0]);
        }

        if (IsBeegoTableNameMethod(node, out _, out var tableName))
        {
            return tableName;
        }

        if (IsBeegoRawCall(node, out var sqlQuery))
        {
            var clean = NestedSqlParser.CleanQueryText(sqlQuery);
            if (NestedSqlParser.TryParseSql(sqlQuery, out var firstWord, out _))
            {
                return $"{firstWord} Query: {clean}";
            }
            return $"Beego Query: {clean}";
        }

        if (IsBeegoQueryTableCall(node, out var modelOrTable))
        {
            return $"Beego ORM: {modelOrTable}";
        }

        return null;
    }

    public void CollectReferences(Node node, string containerName, List<Reference> references, ParsingContext ctx)
    {
        if (IsBeegoRouteCall(node, out _, out var fullRoute, out _, out var mappings))
        {
            fullRoute = ApplyMountPrefix(fullRoute, null);
            foreach (var m in mappings)
            {
                if (!string.IsNullOrEmpty(m.Handler))
                {
                    references.Add(new Reference(m.Handler, fullRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    var dotIdx = m.Handler.LastIndexOf('.');
                    if (dotIdx > 0)
                    {
                        var shortMethod = m.Handler[(dotIdx + 1)..];
                        references.Add(new Reference(shortMethod, fullRoute.Replace(":", " "), OntologyConstants.Relationships.Implements));
                    }
                }
            }
        }
        else if (IsBeegoRegisterModelCall(node, out var modelNames))
        {
            foreach (var model in modelNames)
            {
                var tbl = JsonNamingPolicy.SnakeCaseLower.ConvertName(model);
                references.Add(new Reference(model, tbl, OntologyConstants.Relationships.PersistedIn));
            }
        }
        else if (IsBeegoTableNameMethod(node, out var structType, out var tableName))
        {
            references.Add(new Reference(structType, tableName, OntologyConstants.Relationships.PersistedIn));
        }
        else if (IsBeegoRawCall(node, out var sqlQuery))
        {
            NestedSqlParser.TryDetectSqlDependencies(sqlQuery, containerName, references);
        }
        else if (IsBeegoQueryTableCall(node, out var modelOrTable))
        {
            references.Add(new Reference(containerName, modelOrTable, OntologyConstants.Relationships.UsesDb));
        }
    }

    private static string ApplyMountPrefix(string route, string? relativePath)
    {
        if (!route.StartsWith("/v", StringComparison.OrdinalIgnoreCase))
        {
            if (RouteDictionaryRegistry.TryGetMountPrefix(relativePath, out var mountPrefix))
            {
                return GoAstHelper.CombineRoutes(mountPrefix, route);
            }
            if (!string.IsNullOrEmpty(relativePath))
            {
                var vPathMatch = System.Text.RegularExpressions.Regex.Match(relativePath.Replace('\\', '/'), @"/(v\d+)/");
                if (vPathMatch.Success)
                {
                    return GoAstHelper.CombineRoutes("/" + vPathMatch.Groups[1].Value, route);
                }
            }
        }
        return route;
    }

    public static bool IsBeegoRouteCall(
        Node node,
        out string httpMethod,
        out string fullRoute,
        out string? handlerName,
        out List<(string Method, string Handler)> allMappings)
    {
        httpMethod = "GET";
        fullRoute = "/";
        handlerName = null;
        allMappings = [];

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid()) return false;

        var methodName = field.Text;
        var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        var opText = operand.IsValid() ? operand.Text : string.Empty;

        var isWebReceiver = opText is "web" or "beego" or "app" or "server" ||
                            opText.EndsWith("App", StringComparison.OrdinalIgnoreCase) ||
                            opText.EndsWith("Server", StringComparison.OrdinalIgnoreCase);

        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count == 0) return false;

        if (methodName is "Router" or "NSRouter")
        {
            var pathArg = args[0];
            var routeVal = GoAstHelper.ResolveStringOrVariable(pathArg);
            if (string.IsNullOrEmpty(routeVal))
            {
                var raw = pathArg.Text.Trim('"', '`');
                if (raw.StartsWith('/')) routeVal = raw;
            }
            if (string.IsNullOrEmpty(routeVal)) return false;

            var controllerName = args.Count > 1 ? ExtractControllerName(args[1]) : string.Empty;
            Node? mappingArg = args.Count > 2 ? args[2] : null;
            allMappings = ExtractMappings(mappingArg, controllerName);

            if (allMappings.Count > 0)
            {
                httpMethod = allMappings[0].Method;
                handlerName = allMappings[0].Handler;
            }
            else
            {
                httpMethod = "ANY";
                handlerName = controllerName;
            }

            if (methodName == "NSRouter")
            {
                var nsPrefix = ResolveParentNamespacePrefix(node);
                fullRoute = GoAstHelper.CombineRoutes(nsPrefix, routeVal);
            }
            else
            {
                fullRoute = routeVal.StartsWith('/') ? routeVal : "/" + routeVal;
            }
            return true;
        }

        if (isWebReceiver && DirectHttpMethods.Contains(methodName))
        {
            var pathArg = args[0];
            var routeVal = GoAstHelper.ResolveStringOrVariable(pathArg);
            if (string.IsNullOrEmpty(routeVal))
            {
                var raw = pathArg.Text.Trim('"', '`');
                if (raw.StartsWith('/')) routeVal = raw;
            }
            if (string.IsNullOrEmpty(routeVal)) return false;

            httpMethod = methodName.Equals("Any", StringComparison.OrdinalIgnoreCase) ? "ANY" : methodName.ToUpperInvariant();
            handlerName = args.Count > 1 ? ExtractHandlerName(args[1]) : null;
            allMappings = [(httpMethod, handlerName ?? string.Empty)];
            fullRoute = routeVal.StartsWith('/') ? routeVal : "/" + routeVal;
            return true;
        }

        return false;
    }

    private static string ResolveParentNamespacePrefix(Node node)
    {
        var prefixes = new List<string>();
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.Go.CallExpression))
            {
                var fn = curr.GetFunctionNode();
                if (fn.IsValid() && fn.Is(TreeSitterSyntax.Go.SelectorExpression))
                {
                    var field = fn.GetChildForField(TreeSitterSyntax.Fields.Field);
                    if (field.IsValid() && (field.Text is "NewNamespace" or "NSNamespace" or "NSPrefix"))
                    {
                        var args = GoAstHelper.GetCallArguments(curr);
                        if (args.Count > 0)
                        {
                            var prefixVal = GoAstHelper.ResolveStringOrVariable(args[0])?.Trim(QuoteSeparators);
                            if (!string.IsNullOrEmpty(prefixVal))
                            {
                                prefixes.Add(prefixVal);
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }

        if (prefixes.Count == 0) return string.Empty;
        prefixes.Reverse();
        var combined = string.Empty;
        foreach (var p in prefixes)
        {
            combined = GoAstHelper.CombineRoutes(combined, p);
        }
        return combined;
    }

    private static List<(string Method, string Handler)> ExtractMappings(Node? mappingArg, string controllerName)
    {
        var result = new List<(string Method, string Handler)>();
        if (!mappingArg.IsValid())
        {
            result.Add(("ANY", controllerName));
            return result;
        }

        var mapText = GoAstHelper.ResolveStringOrVariable(mappingArg)?.Trim(QuoteSeparators);
        if (string.IsNullOrWhiteSpace(mapText))
        {
            result.Add(("ANY", controllerName));
            return result;
        }

        var pairs = mapText.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var pair in pairs)
        {
            var colonIdx = pair.IndexOf(':');
            if (colonIdx > 0)
            {
                var verbs = pair[..colonIdx].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var action = pair[(colonIdx + 1)..].Trim();
                var handler = !string.IsNullOrEmpty(controllerName) ? $"{controllerName}.{action}" : action;
                foreach (var v in verbs)
                {
                    var verb = v.Equals("*", StringComparison.Ordinal) ? "ANY" : v.ToUpperInvariant();
                    result.Add((verb, handler));
                }
            }
            else if (pair.StartsWith("*"))
            {
                result.Add(("ANY", controllerName));
            }
        }

        if (result.Count == 0)
        {
            result.Add(("ANY", controllerName));
        }

        return result;
    }

    private static string ExtractControllerName(Node node)
    {
        var target = node;
        if (target.Is(TreeSitterSyntax.Go.UnaryExpression))
        {
            var operand = target.GetChildForField(TreeSitterSyntax.Fields.Operand);
            if (operand.IsValid()) target = operand;
        }

        if (target.Is(TreeSitterSyntax.Go.CompositeLiteral))
        {
            var typeNode = target.GetChildForField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid())
            {
                if (typeNode.Is(TreeSitterSyntax.Go.SelectorExpression))
                {
                    var field = typeNode.GetChildForField(TreeSitterSyntax.Fields.Field);
                    if (field.IsValid()) return field.Text;
                }
                return typeNode.Text;
            }
        }

        if (target.Is(TreeSitterSyntax.Go.CallExpression))
        {
            var fn = target.GetFunctionNode();
            if (fn.IsValid() && fn.Text == "new")
            {
                var args = GoAstHelper.GetCallArguments(target);
                if (args.Count > 0)
                {
                    var arg = args[0];
                    if (arg.Is(TreeSitterSyntax.Go.SelectorExpression))
                    {
                        var field = arg.GetChildForField(TreeSitterSyntax.Fields.Field);
                        if (field.IsValid()) return field.Text;
                    }
                    return arg.Text;
                }
            }
        }

        if (target.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var field = target.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (field.IsValid()) return field.Text;
        }

        return target.Text.Trim('&', '*', ' ');
    }

    private static string ExtractHandlerName(Node node)
    {
        if (node.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var field = node.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (field.IsValid()) return field.Text;
        }
        return node.Text;
    }

    public static bool IsBeegoRegisterModelCall(Node node, out List<string> modelNames)
    {
        modelNames = [];
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid() || field.Text != "RegisterModel") return false;

        var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        if (operand.IsValid())
        {
            var op = operand.Text.ToLowerInvariant();
            if (op is not ("orm" or "beego_orm" or "beegoorm" or "o") && !op.Contains("orm"))
            {
                return false;
            }
        }

        var args = GoAstHelper.GetCallArguments(node);
        foreach (var arg in args)
        {
            var modelName = ExtractModelName(arg);
            if (!string.IsNullOrEmpty(modelName))
            {
                modelNames.Add(modelName);
            }
        }

        return modelNames.Count > 0;
    }

    private static string? ExtractModelName(Node arg)
    {
        var target = arg;
        if (target.Is(TreeSitterSyntax.Go.UnaryExpression))
        {
            var op = target.GetChildForField(TreeSitterSyntax.Fields.Operand);
            if (op.IsValid()) target = op;
        }

        if (target.Is(TreeSitterSyntax.Go.CompositeLiteral))
        {
            var typeNode = target.GetChildForField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid())
            {
                if (typeNode.Is(TreeSitterSyntax.Go.SelectorExpression))
                {
                    var f = typeNode.GetChildForField(TreeSitterSyntax.Fields.Field);
                    if (f.IsValid()) return f.Text;
                }
                return typeNode.Text;
            }
        }

        if (target.Is(TreeSitterSyntax.Go.CallExpression))
        {
            var fn = target.GetFunctionNode();
            if (fn.IsValid() && fn.Text == "new")
            {
                var args = GoAstHelper.GetCallArguments(target);
                if (args.Count > 0)
                {
                    var firstArg = args[0];
                    if (firstArg.Is(TreeSitterSyntax.Go.SelectorExpression))
                    {
                        var f = firstArg.GetChildForField(TreeSitterSyntax.Fields.Field);
                        if (f.IsValid()) return f.Text;
                    }
                    return firstArg.Text;
                }
            }
        }

        if (target.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var f = target.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (f.IsValid()) return f.Text;
        }

        if (target.IsAny(TreeSitterSyntax.Go.Identifier, TreeSitterSyntax.Go.TypeIdentifier))
        {
            return target.Text;
        }

        return null;
    }

    public static bool IsBeegoTableNameMethod(Node node, out string receiverType, out string tableName)
    {
        receiverType = string.Empty;
        tableName = string.Empty;

        if (!node.Is(TreeSitterSyntax.Go.MethodDeclaration)) return false;

        var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid() || nameNode.Text != "TableName") return false;

        var receiverNode = node.GetChildForField(TreeSitterSyntax.Fields.Receiver);
        if (!receiverNode.IsValid()) return false;

        receiverType = ExtractReceiverType(receiverNode);
        if (string.IsNullOrEmpty(receiverType)) return false;

        var body = node.GetChildForField(TreeSitterSyntax.Fields.Body);
        if (!body.IsValid()) return false;

        var returnStmt = body.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Go.ReturnStatement));
        if (!returnStmt.IsValid()) return false;

        var exprs = returnStmt.Children.Where(c => c.Type != "return" && c.Type != ";" && c.Type != ",").ToList();
        if (exprs.Count > 0)
        {
            var retExpr = exprs[0];
            var resolved = GoAstHelper.ResolveStringOrVariable(retExpr)?.Trim(QuoteSeparators);
            if (!string.IsNullOrEmpty(resolved))
            {
                tableName = resolved;
                return true;
            }

            var text = retExpr.Text.Trim(QuoteSeparators);
            if (!string.IsNullOrEmpty(text))
            {
                if (text.EndsWith("Table", StringComparison.OrdinalIgnoreCase))
                {
                    tableName = JsonNamingPolicy.SnakeCaseLower.ConvertName(text[..^5]);
                }
                else
                {
                    tableName = JsonNamingPolicy.SnakeCaseLower.ConvertName(text);
                }
                return true;
            }
        }

        tableName = JsonNamingPolicy.SnakeCaseLower.ConvertName(receiverType);
        return true;
    }

    private static string ExtractReceiverType(Node receiverNode)
    {
        var paramDecl = receiverNode.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Go.ParameterDeclaration));
        if (paramDecl.IsValid())
        {
            var typeNode = paramDecl.GetChildForField(TreeSitterSyntax.Fields.Type);
            if (typeNode.IsValid())
            {
                if (typeNode.Is(TreeSitterSyntax.Go.PointerType))
                {
                    var inner = typeNode.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Go.TypeIdentifier, TreeSitterSyntax.Go.Identifier));
                    if (inner.IsValid()) return inner.Text;
                }
                return typeNode.Text.TrimStart('*');
            }
        }
        return string.Empty;
    }

    public static bool IsBeegoRawCall(Node node, out string sqlQuery)
    {
        sqlQuery = string.Empty;
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid() || field.Text != "Raw") return false;

        var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        if (operand.IsValid())
        {
            var op = operand.Text.ToLowerInvariant();
            if (op is not ("o" or "orm" or "ormer" or "tx" or "txormer" or "db") &&
                !op.EndsWith("orm") && !op.EndsWith("ormer") && !op.Contains("neworm"))
            {
                return false;
            }
        }

        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count > 0)
        {
            var resolved = GoAstHelper.ResolveStringOrVariable(args[0]);
            if (!string.IsNullOrEmpty(resolved) && (NestedSqlParser.FastCheckSqlCandidate(resolved.AsSpan()) || NestedSqlParser.TryParseSql(resolved, out _, out _)))
            {
                sqlQuery = resolved;
                return true;
            }
            if (!string.IsNullOrEmpty(args[0].Text) && args[0].IsAny(TreeSitterSyntax.Go.InterpretedStringLiteral, TreeSitterSyntax.Go.RawStringLiteral))
            {
                sqlQuery = args[0].Text.Trim('"', '`');
                return true;
            }
        }

        return false;
    }

    public static bool IsBeegoQueryTableCall(Node node, out string modelOrTable)
    {
        modelOrTable = string.Empty;
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid() || field.Text != "QueryTable") return false;

        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count > 0)
        {
            var resolved = GoAstHelper.ResolveStringOrVariable(args[0])?.Trim(QuoteSeparators);
            if (!string.IsNullOrEmpty(resolved))
            {
                modelOrTable = resolved;
                return true;
            }

            var model = ExtractModelName(args[0]);
            if (!string.IsNullOrEmpty(model))
            {
                modelOrTable = JsonNamingPolicy.SnakeCaseLower.ConvertName(model);
                return true;
            }
        }

        return false;
    }
}
