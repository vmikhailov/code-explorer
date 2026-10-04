using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Python.Libraries;

public class FastApiLibraryParser : ISemanticExtension
{
    public string Name => "FastAPI";
    public string Id => "fastapi";
    public string Type => "framework";
    public IReadOnlyList<string> SupportedPatterns => ["fastapi", "fastapi.*"];
    public bool IsImplemented => true;
    public bool IsBuiltIn => true;
    public LibraryRole LibraryRole => LibraryRole.WebService;

    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "options", "head", "trace"
    };

    private static readonly HashSet<string> FastApiRouteMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "options", "head", "trace", "api_route", "websocket", "route"
    };

    private static readonly HashSet<string> PrimitiveAndFrameworkTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "str", "float", "bool", "bytes", "dict", "list", "set", "tuple", "Any", "None", "object",
        "Request", "Response", "BackgroundTasks", "UploadFile", "File", "WebSocket", "Session",
        "Header", "Cookie", "Query", "Path", "Body", "Depends", "Security", "HTTPException"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsFastApiDecorator(node))
        {
            return OntologyConstants.NodeLabels.EntryPoint;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsFastApiDecorator(node))
        {
            return ExtractRoute(node);
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!IsFastApiDecorator(node)) return;

        var fn = GetDecoratedFunction(node);
        if (!fn.IsValid()) return;

        var nameNode = fn.GetChildForField(TreeSitterSyntax.Fields.Name);
        if (nameNode.IsValid() && !string.IsNullOrEmpty(nameNode.Text))
        {
            references.Add(new Reference(scopeSymbolId, nameNode.Text, OntologyConstants.Relationships.Triggers));
        }

        var paramList = fn.GetChildForField(TreeSitterSyntax.Fields.Parameters);
        if (paramList.IsValid())
        {
            foreach (var param in paramList.Children)
            {
                CollectParameterDependencies(param, scopeSymbolId, references);
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (!IsFastApiDecorator(node)) return;

        var call = node.FindChildOfType(TreeSitterSyntax.Python.Call);
        if (!call.IsValid()) return;

        var func = call.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() && call.Children.Count > 0) func = call.Children[0];

        string? attrName = null;
        if (func.IsValid() && func.Is(TreeSitterSyntax.Python.Attribute))
        {
            var attr = func.GetChildForField(TreeSitterSyntax.Python.Attribute);
            if (attr.IsValid()) attrName = attr.Text;
        }

        symbol.Protocol = string.Equals(attrName, "websocket", StringComparison.OrdinalIgnoreCase) ? "WS" : "REST";

        var argList = call.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (argList.IsValid())
        {
            foreach (var child in argList.Children)
            {
                if (child.Is(TreeSitterSyntax.Python.KeywordArgument))
                {
                    var kwName = child.Children.FirstOrDefault()?.Text;
                    var valNode = child.Children.Skip(2).FirstOrDefault() ?? child.Children.LastOrDefault();
                    if (!valNode.IsValid()) continue;

                    if (kwName == "response_model")
                    {
                        var cleanType = CleanTypeName(valNode.Text);
                        if (!string.IsNullOrEmpty(cleanType))
                        {
                            symbol.ResponseType = cleanType;
                        }
                    }
                    else if (kwName == "status_code")
                    {
                        symbol.Properties["status_code"] = CleanStatusCode(valNode.Text);
                    }
                    else if (kwName == "summary")
                    {
                        symbol.Properties["summary"] = valNode.Text.Trim('\'', '"');
                    }
                    else if (kwName == "description")
                    {
                        symbol.Properties["description"] = valNode.Text.Trim('\'', '"');
                    }
                    else if (kwName == "tags")
                    {
                        symbol.Properties["tags"] = valNode.Text;
                    }
                    else if (kwName == "operation_id")
                    {
                        symbol.Properties["operation_id"] = valNode.Text.Trim('\'', '"');
                    }
                    else if (kwName == "deprecated")
                    {
                        if (valNode.Text.Equals("True", StringComparison.OrdinalIgnoreCase))
                        {
                            symbol.Properties["deprecated"] = "true";
                        }
                    }
                }
            }
        }

        var fn = GetDecoratedFunction(node);
        if (fn.IsValid())
        {
            if (string.IsNullOrEmpty(symbol.ResponseType))
            {
                var retNode = fn.GetChildForField(TreeSitterSyntax.Fields.ReturnType);
                if (!retNode.IsValid())
                {
                    retNode = fn.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Python.Type);
                }
                if (retNode.IsValid())
                {
                    var cleanType = CleanTypeName(retNode.Text);
                    if (!string.IsNullOrEmpty(cleanType) && cleanType != "None")
                    {
                        symbol.ResponseType = cleanType;
                    }
                }
            }

            var paramList = fn.GetChildForField(TreeSitterSyntax.Fields.Parameters);
            if (paramList.IsValid())
            {
                foreach (var param in paramList.Children)
                {
                    var (pType, isBody) = InspectParameter(param);
                    if (isBody || (!string.IsNullOrEmpty(pType) && !IsPrimitiveOrFrameworkType(pType) && symbol.RequestType == null))
                    {
                        symbol.RequestType = pType;
                        if (isBody) break;
                    }
                }

                var scopes = ExtractSecurityScopes(paramList);
                if (scopes.Count > 0)
                {
                    symbol.RequiredRoles = string.Join(",", scopes);
                }
            }
        }
    }

    public static bool IsFastApiDecorator(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Python.Decorator)) return false;
        var call = node.FindChildOfType(TreeSitterSyntax.Python.Call);
        if (!call.IsValid()) return false;
        var func = call.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() && call.Children.Count > 0) func = call.Children[0];
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.Python.Attribute))
        {
            var attr = func.GetChildForField(TreeSitterSyntax.Python.Attribute);
            if (attr.IsValid())
            {
                var attrName = attr.Text;
                return FastApiRouteMethods.Contains(attrName);
            }
        }
        return false;
    }

    public static string? ExtractRoute(Node decoratorNode)
    {
        var call = decoratorNode.FindChildOfType(TreeSitterSyntax.Python.Call);
        if (!call.IsValid()) return null;
        var func = call.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() && call.Children.Count > 0) func = call.Children[0];
        if (!func.IsValid()) return null;

        var method = "GET";
        string? targetObjName = null;

        if (func.Is(TreeSitterSyntax.Python.Attribute))
        {
            var obj = func.GetChildForField(TreeSitterSyntax.Fields.Object) ??
                      func.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                      (func.Children.Count > 0 ? func.Children[0] : null);
            if (obj.IsValid())
            {
                targetObjName = obj.Text;
            }

            var attr = func.GetChildForField(TreeSitterSyntax.Python.Attribute);
            if (attr.IsValid())
            {
                var attrName = attr.Text;
                if (string.Equals(attrName, "websocket", StringComparison.OrdinalIgnoreCase))
                {
                    method = "WS";
                }
                else if (string.Equals(attrName, "api_route", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(attrName, "route", StringComparison.OrdinalIgnoreCase))
                {
                    method = ExtractApiRouteMethods(call) ?? "GET";
                }
                else if (HttpMethods.Contains(attrName))
                {
                    method = attrName.ToUpperInvariant();
                }
            }
        }

        var pathVal = ExtractPathArgument(call) ?? "/";

        if (!string.IsNullOrEmpty(targetObjName) && targetObjName != "app")
        {
            var routerPrefix = FindRouterPrefix(decoratorNode, targetObjName);
            if (!string.IsNullOrEmpty(routerPrefix))
            {
                pathVal = CombineRoutes(routerPrefix, pathVal);
            }
        }

        pathVal = NormalizeRoute(pathVal);
        return $"{method}:{pathVal}";
    }

    public static Node? GetDecoratedFunction(Node decoratorNode)
    {
        var parent = decoratorNode.Parent;
        if (!parent.IsValid()) return null;
        if (parent.Is(TreeSitterSyntax.Python.DecoratedDefinition))
        {
            return parent.FindChildOfType(TreeSitterSyntax.Python.FunctionDefinition);
        }
        return null;
    }

    public static string CombineRoutes(string prefix, string path)
    {
        if (string.IsNullOrEmpty(prefix)) return NormalizeRoute(path);
        if (string.IsNullOrEmpty(path)) return NormalizeRoute(prefix);

        var p1 = prefix.Trim();
        var p2 = path.Trim();

        if (!p1.StartsWith('/')) p1 = "/" + p1;
        p1 = p1.TrimEnd('/');

        if (!p2.StartsWith('/')) p2 = "/" + p2;

        var combined = p1 + p2;
        return NormalizeRoute(combined);
    }

    public static string NormalizeRoute(string route)
    {
        if (string.IsNullOrEmpty(route) || route == "/") return "/";
        var r = route.Trim();
        if (!r.StartsWith('/')) r = "/" + r;
        while (r.Contains("//", StringComparison.Ordinal))
        {
            r = r.Replace("//", "/");
        }
        if (r.Length > 1 && r.EndsWith('/'))
        {
            r = r.TrimEnd('/');
        }
        return r;
    }

    public static string CleanTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType)) return string.Empty;
        var t = rawType.Trim();
        if (t.StartsWith("->", StringComparison.Ordinal)) t = t[2..].Trim();

        while (true)
        {
            if (t.StartsWith("typing.", StringComparison.OrdinalIgnoreCase))
            {
                t = t[7..].Trim();
                continue;
            }

            var bracketIdx = t.IndexOf('[');
            if (bracketIdx > 0 && t.EndsWith(']'))
            {
                var outer = t[..bracketIdx].Trim();
                if (outer is "List" or "list" or "Optional" or "Set" or "set" or "Sequence" or "Iterable" or "Union" or "Awaitable" or "Coroutine")
                {
                    var inner = t[(bracketIdx + 1)..^1].Trim();
                    if (outer == "Union" && inner.Contains(','))
                    {
                        var parts = inner.Split(',').Select(p => p.Trim()).Where(p => p != "None" && p != "NoneType").ToList();
                        if (parts.Count > 0)
                        {
                            t = parts[0];
                            continue;
                        }
                    }
                    t = inner;
                    continue;
                }
            }
            break;
        }

        var pipeIdx = t.IndexOf('|');
        if (pipeIdx > 0)
        {
            var parts = t.Split('|').Select(p => p.Trim()).Where(p => p != "None" && p != "NoneType").ToList();
            if (parts.Count > 0)
            {
                t = parts[0];
            }
        }

        return t;
    }

    public static bool IsPrimitiveOrFrameworkType(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return true;
        if (PrimitiveAndFrameworkTypes.Contains(type)) return true;
        if (type.EndsWith("[]", StringComparison.Ordinal)) return true;
        return false;
    }

    private static string? ExtractPathArgument(Node callNode)
    {
        var argList = callNode.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (!argList.IsValid()) return null;

        foreach (var child in argList.Children)
        {
            if (child.Is(TreeSitterSyntax.Python.KeywordArgument))
            {
                var kwName = child.Children.FirstOrDefault()?.Text;
                if (kwName == "path")
                {
                    var valNode = child.Children.Skip(2).FirstOrDefault() ?? child.Children.LastOrDefault();
                    return ExtractStringLiteral(valNode);
                }
            }
            else if (child.Is(TreeSitterSyntax.Python.String))
            {
                return ExtractStringLiteral(child);
            }
            else if (child.IsAny(TreeSitterSyntax.Python.Identifier,
                                 TreeSitterSyntax.Python.BinaryOperator,
                                 TreeSitterSyntax.Python.FormatString))
            {
                var resolved = PythonAstHelper.ResolveStringOrVariable(child);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }
        }

        return null;
    }

    private static string? ExtractStringLiteral(Node? node)
    {
        if (!node.IsValid()) return null;
        if (node.Is(TreeSitterSyntax.Python.String))
        {
            var text = node.Text.Trim('\'', '"');
            if (text.StartsWith("f'", StringComparison.OrdinalIgnoreCase) || text.StartsWith("f\"", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("r'", StringComparison.OrdinalIgnoreCase) || text.StartsWith("r\"", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("b'", StringComparison.OrdinalIgnoreCase) || text.StartsWith("b\"", StringComparison.OrdinalIgnoreCase))
            {
                text = text[1..].Trim('\'', '"');
            }
            return text;
        }
        return PythonAstHelper.ResolveStringOrVariable(node);
    }

    private static string? FindRouterPrefix(Node node, string routerVarName)
    {
        var root = node;
        while (root.Parent.IsValid())
        {
            root = root.Parent;
        }

        foreach (var statement in root.Children)
        {
            if (statement.Is(TreeSitterSyntax.Python.Assignment))
            {
                var left = statement.GetChildForField(TreeSitterSyntax.Fields.Left) ??
                           statement.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier));
                if (left.IsValid() && left.Text == routerVarName)
                {
                    var right = statement.GetChildForField(TreeSitterSyntax.Fields.Right) ??
                                statement.Children.LastOrDefault(c => c.Is(TreeSitterSyntax.Python.Call));
                    if (right.IsValid() && right.Is(TreeSitterSyntax.Python.Call))
                    {
                        var callFunc = right.GetChildForField(TreeSitterSyntax.Fields.Function) ??
                                       (right.Children.Count > 0 ? right.Children[0] : null);
                        if (callFunc.IsValid() && callFunc.Text.Contains("APIRouter"))
                        {
                            var argList = right.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
                            if (argList.IsValid())
                            {
                                foreach (var arg in argList.Children)
                                {
                                    if (arg.Is(TreeSitterSyntax.Python.KeywordArgument))
                                    {
                                        var kwName = arg.Children.FirstOrDefault()?.Text;
                                        if (kwName == "prefix")
                                        {
                                            var valNode = arg.Children.Skip(2).FirstOrDefault() ?? arg.Children.LastOrDefault();
                                            return ExtractStringLiteral(valNode);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (statement.Is(TreeSitterSyntax.Python.ExpressionStatement) || statement.Is(TreeSitterSyntax.Python.Call))
            {
                var callNode = statement.Is(TreeSitterSyntax.Python.Call) ? statement : statement.FindChildOfType(TreeSitterSyntax.Python.Call);
                if (callNode.IsValid())
                {
                    var callFunc = callNode.GetChildForField(TreeSitterSyntax.Fields.Function) ??
                                   (callNode.Children.Count > 0 ? callNode.Children[0] : null);
                    if (callFunc.IsValid() && callFunc.Text.EndsWith("include_router", StringComparison.Ordinal))
                    {
                        var argList = callNode.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
                        if (argList.IsValid())
                        {
                            var hasRouterArg = argList.Children.Any(c => c.Text == routerVarName);
                            if (hasRouterArg)
                            {
                                foreach (var arg in argList.Children)
                                {
                                    if (arg.Is(TreeSitterSyntax.Python.KeywordArgument))
                                    {
                                        var kwName = arg.Children.FirstOrDefault()?.Text;
                                        if (kwName == "prefix")
                                        {
                                            var valNode = arg.Children.Skip(2).FirstOrDefault() ?? arg.Children.LastOrDefault();
                                            return ExtractStringLiteral(valNode);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    private static string? ExtractApiRouteMethods(Node callNode)
    {
        var argList = callNode.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (!argList.IsValid()) return null;

        var keywordArg = argList.Children.FirstOrDefault(c =>
            c.Is(TreeSitterSyntax.Python.KeywordArgument) &&
            c.Children.FirstOrDefault()?.Text == "methods");

        if (keywordArg.IsValid())
        {
            var listNode = keywordArg.FindChildOfType(TreeSitterSyntax.Python.List);
            if (listNode.IsValid())
            {
                var methods = new List<string>();
                foreach (var child in listNode.Children)
                {
                    if (child.Is(TreeSitterSyntax.Python.String))
                    {
                        var m = child.Text.Trim('\'', '"').ToUpperInvariant();
                        if (!string.IsNullOrEmpty(m)) methods.Add(m);
                    }
                }
                if (methods.Count > 0)
                {
                    return string.Join(",", methods);
                }
            }
        }
        return null;
    }

    private static (string? TypeName, bool IsBody) InspectParameter(Node paramNode)
    {
        if (!paramNode.IsValid()) return (null, false);

        string? paramName = null;
        string? typeName = null;
        Node? defaultValue = null;

        if (paramNode.Is(TreeSitterSyntax.Python.TypedDefaultParameter))
        {
            var nameNode = paramNode.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           paramNode.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier));
            paramName = nameNode?.Text;

            var typeNode = paramNode.GetChildForField(TreeSitterSyntax.Fields.Type) ??
                           paramNode.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Python.Type);
            typeName = typeNode?.Text;

            defaultValue = paramNode.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                           paramNode.Children.LastOrDefault();
        }
        else if (paramNode.Is(TreeSitterSyntax.Python.TypedParameter))
        {
            var nameNode = paramNode.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           paramNode.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier));
            paramName = nameNode?.Text;

            var typeNode = paramNode.GetChildForField(TreeSitterSyntax.Fields.Type) ??
                           paramNode.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Python.Type);
            typeName = typeNode?.Text;
        }
        else if (paramNode.Is(TreeSitterSyntax.Python.DefaultParameter))
        {
            var nameNode = paramNode.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                           paramNode.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.Identifier));
            paramName = nameNode?.Text;

            defaultValue = paramNode.GetChildForField(TreeSitterSyntax.Fields.Value) ??
                           paramNode.Children.LastOrDefault();
        }

        if (string.IsNullOrEmpty(typeName) && !string.IsNullOrEmpty(paramName))
        {
            return (null, false);
        }

        var cleanType = CleanTypeName(typeName ?? "");

        var isBody = false;
        var isDependency = false;

        if (defaultValue.IsValid())
        {
            var defaultText = defaultValue.Text;
            if (defaultText.StartsWith("Depends(", StringComparison.Ordinal) ||
                defaultText.StartsWith("Security(", StringComparison.Ordinal))
            {
                isDependency = true;
            }
            else if (defaultText.StartsWith("Body(", StringComparison.Ordinal))
            {
                isBody = true;
            }
            else if (defaultText.StartsWith("Query(", StringComparison.Ordinal) ||
                     defaultText.StartsWith("Path(", StringComparison.Ordinal) ||
                     defaultText.StartsWith("Header(", StringComparison.Ordinal) ||
                     defaultText.StartsWith("Cookie(", StringComparison.Ordinal))
            {
                return (cleanType, false);
            }
        }

        if (isDependency)
        {
            return (cleanType, false);
        }

        if (isBody)
        {
            return (cleanType, true);
        }

        if (!string.IsNullOrEmpty(cleanType) && !IsPrimitiveOrFrameworkType(cleanType))
        {
            return (cleanType, true);
        }

        return (cleanType, false);
    }

    private static void CollectParameterDependencies(Node paramNode, string scopeSymbolId, List<Reference> references)
    {
        if (!paramNode.IsValid()) return;

        var calls = new List<Node>();
        if (paramNode.Is(TreeSitterSyntax.Python.Call)) calls.Add(paramNode);
        calls.AddRange(paramNode.FindChildrenOfType(TreeSitterSyntax.Python.Call));

        foreach (var child in paramNode.Children)
        {
            if (child.Is(TreeSitterSyntax.Python.Call) && !calls.Contains(child))
            {
                calls.Add(child);
            }
        }

        foreach (var call in calls)
        {
            var func = call.GetChildForField(TreeSitterSyntax.Fields.Function) ??
                       (call.Children.Count > 0 ? call.Children[0] : null);
            if (!func.IsValid()) continue;

            var funcName = func.Text;
            if (funcName is "Depends" or "Security")
            {
                var argList = call.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
                if (argList.IsValid())
                {
                    var firstArg = argList.Children.FirstOrDefault(c =>
                        c.IsAny(TreeSitterSyntax.Python.Identifier, TreeSitterSyntax.Python.Attribute));
                    if (firstArg.IsValid())
                    {
                        var depName = firstArg.Text;
                        if (depName.Contains('.'))
                        {
                            depName = depName[(depName.LastIndexOf('.') + 1)..];
                        }
                        if (!string.IsNullOrEmpty(depName))
                        {
                            references.Add(new Reference(scopeSymbolId, depName, OntologyConstants.Relationships.Calls));
                        }
                    }
                }
            }
        }
    }

    private static List<string> ExtractSecurityScopes(Node paramList)
    {
        var scopes = new List<string>();
        foreach (var param in paramList.Children)
        {
            var calls = new List<Node>();
            if (param.Is(TreeSitterSyntax.Python.Call)) calls.Add(param);
            calls.AddRange(param.FindChildrenOfType(TreeSitterSyntax.Python.Call));

            foreach (var child in param.Children)
            {
                if (child.Is(TreeSitterSyntax.Python.Call) && !calls.Contains(child))
                {
                    calls.Add(child);
                }
            }

            foreach (var call in calls)
            {
                var func = call.GetChildForField(TreeSitterSyntax.Fields.Function) ??
                           (call.Children.Count > 0 ? call.Children[0] : null);
                if (!func.IsValid() || func.Text != "Security") continue;

                var argList = call.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
                if (!argList.IsValid()) continue;

                var scopesArg = argList.Children.FirstOrDefault(c =>
                    c.Is(TreeSitterSyntax.Python.KeywordArgument) &&
                    c.Text.StartsWith("scopes", StringComparison.Ordinal));

                if (scopesArg.IsValid())
                {
                    var listNode = scopesArg.FindChildOfType(TreeSitterSyntax.Python.List) ??
                                   scopesArg.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Python.List));
                    if (listNode.IsValid())
                    {
                        foreach (var item in listNode.Children)
                        {
                            if (item.Is(TreeSitterSyntax.Python.String))
                            {
                                var s = item.Text.Trim('\'', '"');
                                if (!string.IsNullOrEmpty(s)) scopes.Add(s);
                            }
                        }
                    }
                }
            }
        }
        return scopes;
    }

    private static string CleanStatusCode(string raw)
    {
        var match = Regex.Match(raw, @"\b([1-5][0-9]{2})\b");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return raw.Trim('\'', '"');
    }
}
