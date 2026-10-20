using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public static partial class AstHelper
{
    public static string? ResolveStringOrTemplate(Node? argNode, string? contextOrProject = null)
    {
        return ResolveStringOrTemplate(argNode, contextOrProject, 0, null);
    }

    public static string? ResolveStringOrTemplate(Node? argNode, string? contextOrProject, int depth, HashSet<string>? visitedVars)
    {
        if (depth > MaxRecursionDepth || !argNode.IsValid()) return null;

        if (IsStringLiteralNode(argNode))
        {
            var text = argNode.Text.Trim('\'', '"', '`');
            if (text.Contains('\n') || text.Length > 500) return null;

            if (WorkspaceConventions.TryMatchRouteFunction(text, out var routeKey))
            {
                if (RouteDictionaryRegistry.TryResolve(routeKey, out var rPath, out var rService))
                {
                    var cleanPath = rPath.Split('?')[0];
                    return CombineServiceAndPath(rService, cleanPath);
                }
            }

            var decomposed = TryDecomposeTemplateString(argNode, text, depth, visitedVars);
            if (!string.IsNullOrEmpty(decomposed))
            {
                return NormalizeResolvedUrl(decomposed);
            }

            return NormalizeResolvedUrl(text);
        }

        if (argNode.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var varName = argNode.Text;
            if (RouteDictionaryRegistry.TryResolve(varName, out var rPath, out var rService))
            {
                var cleanPath = rPath.Split('?')[0];
                return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
            }

            if (WorkspaceConventions.IsPlaceholderName(varName))
            {
                return null;
            }

            // 1. Check local AST lexical scope first (file-local and function-local bindings)
            var val = FindVariableInitializerInAst(argNode, varName, depth, visitedVars, contextOrProject);
            if (val != null)
            {
                var subDecomp = TryDecomposeTemplateString(argNode, val, depth, visitedVars);
                return NormalizeResolvedUrl(subDecomp ?? val);
            }

            // 2. Fall back to ConstantRegistry
            if (ConstantRegistry.TryResolve(contextOrProject, varName, out var cVal) && !string.IsNullOrEmpty(cVal))
            {
                return cVal;
            }

            if (WorkspaceConventions.IsGenericUrlStemOrVariable(varName))
            {
                return null;
            }

            if (varName.Equals("Topic", StringComparison.OrdinalIgnoreCase) ||
                varName.Equals("string", StringComparison.OrdinalIgnoreCase) ||
                varName.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
                varName.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                varName.Length <= 3)
            {
                return null;
            }

            if (Regex.IsMatch(varName, @"^[A-Z0-9_]{3,}$"))
            {
                return varName;
            }

            var stripped = CleanIdentifierSuffix(varName);
            if (stripped != varName)
            {
                if (WorkspaceConventions.IsGenericUrlStemOrVariable(stripped))
                {
                    return null;
                }
                return stripped;
            }
        }

        if (argNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            if (RouteDictionaryRegistry.TryResolve(argNode.Text, out var rPath, out var rService))
            {
                var cleanPath = rPath.Split('?')[0];
                return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
            }

            if (ConstantRegistry.TryResolve(contextOrProject, argNode.Text, out var directConst) && !string.IsNullOrEmpty(directConst))
            {
                return directConst;
            }

            if (argNode.Text.StartWithIC("this."))
            {
                var propName = argNode.Text[5..].Trim();
                if (ConstantRegistry.TryResolve(contextOrProject, argNode.Text, out var thisConst) && !string.IsNullOrEmpty(thisConst))
                {
                    return thisConst;
                }
                if (ConstantRegistry.TryResolve(contextOrProject, propName, out var propConst) && !string.IsNullOrEmpty(propConst))
                {
                    return propConst;
                }

                var classVal = FindClassFieldInitializerInAst(argNode, propName, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(classVal))
                {
                    var subDecomp = TryDecomposeTemplateString(argNode, classVal, depth, visitedVars);
                    return NormalizeResolvedUrl(subDecomp ?? classVal);
                }
            }

            var envMatch = Regex.Match(argNode.Text, @"(?:process\.env|env\??|config(?:\.get)?)\.([A-Za-z0-9_]+)");
            if (envMatch.Success)
            {
                var envKey = envMatch.Groups[1].Value;
                if (WorkspaceConventions.IsPlaceholderName(envKey))
                {
                    return null;
                }
                if (ConstantRegistry.TryResolve(contextOrProject, envKey, out var envVal) && !string.IsNullOrEmpty(envVal))
                {
                    return envVal;
                }
                return envKey;
            }

            var prop = argNode.GetField(TreeSitterSyntax.Fields.Property);
            if (prop.IsValid())
            {
                if (RouteDictionaryRegistry.TryResolve(prop.Text, out rPath, out rService))
                {
                    var cleanPath = rPath.Split('?')[0];
                    return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
                }

                var propText = prop.Text;
                var classVal = FindClassFieldInitializerInAst(argNode, propText, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(classVal))
                {
                    var subDecomp = TryDecomposeTemplateString(argNode, classVal, depth, visitedVars);
                    return NormalizeResolvedUrl(subDecomp ?? classVal);
                }

                var val = FindVariableInitializerInAst(argNode, propText, depth, visitedVars);
                if (val != null)
                {
                    var subDecomp = TryDecomposeTemplateString(argNode, val, depth, visitedVars);
                    return NormalizeResolvedUrl(subDecomp ?? val);
                }

                if (Regex.IsMatch(propText, @"^[A-Z0-9_]{3,}$"))
                {
                    return propText;
                }

                var strippedProp = CleanIdentifierSuffix(propText);
                if (strippedProp != propText)
                {
                    return strippedProp;
                }
            }
        }

        if (argNode.Is(TreeSitterSyntax.TypeScript.Object))
        {
            if (TryGetObjectProperty(argNode, "topicName", out var tp) ||
                TryGetObjectProperty(argNode, "topic", out tp) ||
                TryGetObjectProperty(argNode, "queue", out tp) ||
                TryGetObjectProperty(argNode, "queueName", out tp))
            {
                var resolved = ResolveStringOrTemplate(tp, contextOrProject, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }
        }

        if (argNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var func = argNode.GetFunctionNode();
            if (func.IsValid())
            {
                var funcText = func.Text;
                if (funcText.StartWithIC("this."))
                {
                    funcText = funcText[5..];
                }

                if (RouteDictionaryRegistry.TryResolve(funcText, out var rPath, out var rService))
                {
                    var cleanPath = rPath.Split('?')[0];
                    return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
                }

                if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    var prop = func.GetField(TreeSitterSyntax.Fields.Property);
                    if (prop.IsValid() && RouteDictionaryRegistry.TryResolve(prop.Text, out rPath, out rService))
                    {
                        var cleanPath = rPath.Split('?')[0];
                        return NormalizeResolvedUrl(CombineServiceAndPath(rService, cleanPath));
                    }
                }

                var propNode = func.Is(TreeSitterSyntax.TypeScript.MemberExpression)
                    ? func.GetField(TreeSitterSyntax.Fields.Property)
                    : default;
                var funcName = propNode.IsValid() ? propNode.Text : funcText;
                var methodRet = FindMethodReturnInAst(argNode, funcName, depth, visitedVars);
                if (!string.IsNullOrEmpty(methodRet))
                {
                    return NormalizeResolvedUrl(methodRet);
                }
            }

            var argList = argNode.GetField(TreeSitterSyntax.Fields.Arguments)
                ?? argNode.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
            if (argList.IsValid())
            {
                var firstArg = argList.Children.FirstOrDefault(c => c.IsValid() && IsStringLiteralNode(c));
                if (firstArg.IsValid())
                {
                    var text = firstArg.Text.Trim('\'', '"', '`');
                    if (text.StartWithIC("http://") ||
                        text.StartWithIC("https://") ||
                        text.StartWithIC("ws://") ||
                        text.StartWithIC("wss://") ||
                        text.StartsWith('/'))
                    {
                        return NormalizeResolvedUrl(text);
                    }
                }
            }
        }

        return null;
    }

    public static string NormalizeResolvedUrl(string raw)
    {
        return RouteDictionaryRegistry.NormalizeResolvedUrl(raw);
    }

    private static string? TryDecomposeTemplateString(Node node, string raw, int depth = 0, HashSet<string>? visitedVars = null)
    {
        if (depth > MaxRecursionDepth || string.IsNullOrWhiteSpace(raw)) return null;

        // 1. https://bundles.${domain}/... or http://service.${domain}/...
        var subMatch = Regex.Match(raw, @"^(https?://[a-zA-Z0-9_\-]+)\.\$\{");
        if (subMatch.Success)
        {
            var tailIdx = raw.IndexOf("}/", StringComparison.Ordinal);
            var tail = tailIdx >= 0 ? raw[(tailIdx + 1)..] : "/";
            return $"{subMatch.Groups[1].Value}{tail}";
        }

        // 2. Leading template substitution: ${expr}tail
        var tplMatch = Regex.Match(raw, @"^\$\{([^}]+)\}(.*)");
        if (tplMatch.Success)
        {
            var expr = tplMatch.Groups[1].Value.Trim();
            var tail = tplMatch.Groups[2].Value.Trim();

            var resolvedBase = ResolveTemplateExpression(node, expr, depth, visitedVars);
            if (!string.IsNullOrEmpty(resolvedBase))
            {
                if (WorkspaceConventions.IsGenericUrlStemOrVariable(resolvedBase))
                {
                    if (!string.IsNullOrWhiteSpace(tail))
                    {
                        var cleanTail = tail.TrimStart('/');
                        return !string.IsNullOrEmpty(cleanTail) ? $"/{cleanTail}" : null;
                    }
                    return null;
                }

                if (string.IsNullOrWhiteSpace(tail) || tail.StartsWith('?'))
                {
                    return resolvedBase;
                }

                if (resolvedBase.StartWithIC("http://") ||
                    resolvedBase.StartWithIC("https://"))
                {
                    return $"{resolvedBase.TrimEnd('/')}/{tail.TrimStart('/')}";
                }
                if (resolvedBase.Contains('.'))
                {
                    return $"https://{resolvedBase}/{tail.TrimStart('/')}";
                }
                return $"{resolvedBase}/{tail.TrimStart('/')}";
            }

            // Fallback for nested property access: ${this.kvConfig.baseUrl}zones
            if (expr.Contains('.'))
            {
                var dotParts = expr.Split('.');
                if (dotParts.Length >= 2)
                {
                    var parentExpr = string.Join('.', dotParts[..^1]);
                    var leafProp = dotParts[^1];
                    var parentTypeOrVal = ResolveTemplateExpression(node, parentExpr, depth + 1, visitedVars);
                    if (!string.IsNullOrEmpty(parentTypeOrVal))
                    {
                        var leafVal = FindClassFieldInitializerInAst(node, $"{parentTypeOrVal}.{leafProp}", depth + 1, visitedVars);
                        if (string.IsNullOrEmpty(leafVal)) leafVal = FindClassFieldInitializerInAst(node, leafProp, depth + 1, visitedVars);
                        if (!string.IsNullOrEmpty(leafVal))
                        {
                            if (WorkspaceConventions.IsGenericUrlStemOrVariable(leafVal))
                            {
                                if (!string.IsNullOrWhiteSpace(tail))
                                {
                                    var cleanTail = tail.TrimStart('/');
                                    return !string.IsNullOrEmpty(cleanTail) ? $"/{cleanTail}" : null;
                                }
                                return null;
                            }

                            if (leafVal.StartWithIC("http://") ||
                                leafVal.StartWithIC("https://"))
                            {
                                return $"{leafVal.TrimEnd('/')}/{tail.TrimStart('/')}";
                            }
                            if (leafVal.Contains('.'))
                            {
                                return $"https://{leafVal}/{tail.TrimStart('/')}";
                            }
                            return $"{leafVal}/{tail.TrimStart('/')}";
                        }
                    }
                }
            }

            if (WorkspaceConventions.IsGenericUrlStemOrVariable(expr) || (expr.Contains('.') && WorkspaceConventions.IsGenericUrlStemOrVariable(expr.Split('.').Last())))
            {
                if (!string.IsNullOrWhiteSpace(tail))
                {
                    var cleanTail = tail.TrimStart('/');
                    return !string.IsNullOrEmpty(cleanTail) ? $"/{cleanTail}" : null;
                }
                return null;
            }
        }

        // 3. General template string interpolation: /api/${apiVersion}/campaigns or ${prefix}/items
        if (raw.Contains("${"))
        {
            var resolved = Regex.Replace(raw, @"\$\{([a-zA-Z0-9_\.]+)\}", m =>
            {
                var expr = m.Groups[1].Value.Trim();
                var val = ResolveTemplateExpression(node, expr, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(val)) return val;
                if (ConstantRegistry.TryResolve(null, expr, out var cVal)) return cVal;
                var shortName = expr.Contains('.') ? expr.Split('.')[^1] : expr;
                if (shortName.Equals("apiVersion", StringComparison.OrdinalIgnoreCase) ||
                    shortName.Equals("version", StringComparison.OrdinalIgnoreCase) ||
                    shortName.Equals("ver", StringComparison.OrdinalIgnoreCase))
                {
                    return "v1";
                }
                return m.Value;
            });
            if (!resolved.Contains("${") || resolved != raw)
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? ResolveTemplateExpression(Node node, string expr, int depth = 0, HashSet<string>? visitedVars = null)
    {
        if (depth > 4 || string.IsNullOrWhiteSpace(expr)) return null;

        visitedVars ??= new HashSet<string>(StringComparer.Ordinal);
        var exprKey = $"expr:{expr.Trim()}";
        if (!visitedVars.Add(exprKey)) return null;

        try
        {
            // 1. Binary OR / Nullish coalescing: options.apiUrl || 'https://bundles.${domain}'
            foreach (var op in new[] { "||", "??" })
            {
                if (expr.Contains(op))
                {
                    var parts = expr.Split([op], StringSplitOptions.TrimEntries);
                    // First pass: look for string literal or absolute URL
                    foreach (var part in parts)
                    {
                        var strMatch = Regex.Match(part, @"['""`]([^'""`]+)['""`]");
                        if (strMatch.Success)
                        {
                            var s = strMatch.Groups[1].Value;
                            var subMatch = Regex.Match(s, @"^(https?://[a-zA-Z0-9_\-]+)\.\$\{");
                            if (subMatch.Success) return subMatch.Groups[1].Value;
                            if (s.StartWithIC("http://") || s.StartWithIC("https://"))
                                return s;
                        }
                    }

                    // Evaluate each part at most ONCE and reuse across priority passes
                    var resolvedParts = new string?[parts.Length];
                    for (var i = 0; i < parts.Length; i++)
                    {
                        resolvedParts[i] = ResolveTemplateExpression(node, parts[i], depth + 1, visitedVars);
                    }

                    // Second pass: concrete url resolved from member or identifier
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var resolved = resolvedParts[i];
                        if (!string.IsNullOrEmpty(resolved) &&
                            (resolved.StartWithIC("http://") ||
                             resolved.StartWithIC("https://")))
                        {
                            return resolved;
                        }
                    }
                    // Third pass: member / identifier with non-empty resolution
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var part = parts[i];
                        if (part.Contains('.') && !part.StartWithIC("process.env") && !part.StartWithIC("env.") && !part.StartWithIC("env?."))
                        {
                            var resolved = resolvedParts[i];
                            if (!string.IsNullOrEmpty(resolved)) return resolved;
                        }
                    }
                    // Fourth pass: env or config
                    foreach (var part in parts)
                    {
                        var envMatch = Regex.Match(part, @"(?:process\.env|env\??|config(?:\.get)?)\.([A-Za-z0-9_]+)");
                        if (envMatch.Success) return envMatch.Groups[1].Value;
                    }
                    // Fifth pass: general fallback
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var resolved = resolvedParts[i];
                        if (!string.IsNullOrEmpty(resolved)) return resolved;
                    }
                }
            }

            // 1b. ConstantRegistry lookup: TELEGRAM_BOT_API_BASE_URL, BQ_ROUTES_CALC_API_HOST, ID_HELPER_NEST, etc.
            if (ConstantRegistry.TryResolve(null, expr, out var constVal) && !string.IsNullOrEmpty(constVal))
            {
                return constVal;
            }

            if (expr.StartWithIC("this.") &&
                ConstantRegistry.TryResolve(null, expr[5..], out var strippedVal) && !string.IsNullOrEmpty(strippedVal))
            {
                return strippedVal;
            }

            if (expr.Contains('.') &&
                ConstantRegistry.TryResolve(null, expr.Split('.').Last(), out var leafVal) && !string.IsNullOrEmpty(leafVal))
            {
                return leafVal;
            }

            // 1c. RouteDictionaryRegistry lookup
            if (RouteDictionaryRegistry.TryResolve(expr, out var rPath, out var rSvc))
            {
                var cleanPath = rPath.Split('?')[0];
                return CombineServiceAndPath(rSvc, cleanPath);
            }

            // 2. Member / field access: this.<prop>, ClassName.<prop>
            if (expr.StartWithIC("this.") ||
                (expr.Contains('.') && !expr.Contains('(') && !expr.StartWithIC("process.env") && !expr.StartWithIC("env.") && !expr.StartWithIC("env?.")))
            {
                var propName = expr.Split('.').Last().Trim();
                var classVal = FindClassFieldInitializerInAst(node, propName, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(classVal)) return classVal;
                if (expr.StartWithIC("this."))
                {
                    return CleanIdentifierSuffix(propName);
                }
            }

            // 3. Env / config: process.env.HUB_INGEST_URL, env?.HUB_INGEST_URL, config.get('HUB_INGEST_URL')
            var envDirect = Regex.Match(expr, @"(?:process\.env|env\??|config(?:\.get)?)\.([A-Za-z0-9_]+)");
            if (envDirect.Success)
            {
                return envDirect.Groups[1].Value;
            }

            var configGetMatch = Regex.Match(expr, @"config\.get\(['""]([A-Za-z0-9_]+)['""]\)");
            if (configGetMatch.Success)
            {
                return configGetMatch.Groups[1].Value;
            }

            // 4. Method call: e.g. getHubUrl(env) or TelemetryReporter.getHubUrl(env)
            var callMatch = Regex.Match(expr, @"^(?:[A-Za-z0-9_]+\.)?([A-Za-z0-9_]+)\(");
            if (callMatch.Success)
            {
                var methodName = callMatch.Groups[1].Value;
                var methodRet = FindMethodReturnInAst(node, methodName, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(methodRet)) return methodRet;
            }

            // 5. Variable access: strip trailing method calls like baseUrl.replace(...)
            var cleanIdent = Regex.Replace(expr, @"\.[a-zA-Z0-9_]+\(.*?\)", "").Trim();
            if (!Regex.IsMatch(cleanIdent, @"^[A-Za-z_$][A-Za-z0-9_$.]*$"))
            {
                return null;
            }

            // Check if there is a variable in the local scope
            var localVal = FindVariableInitializerInAst(node, cleanIdent, depth, visitedVars);
            if (!string.IsNullOrEmpty(localVal))
            {
                if (localVal.StartWithIC("http://") ||
                    localVal.StartWithIC("https://"))
                {
                    return localVal;
                }
                var subDecomp = TryDecomposeTemplateString(node, localVal, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(subDecomp)) return subDecomp;

                return localVal;
            }

            // Check if file has a fallback definition for *apiUrl* or *baseUrl*
            if (depth == 0 &&
                (cleanIdent.EndsWith("url", StringComparison.OrdinalIgnoreCase) ||
                 cleanIdent.EndsWith("client", StringComparison.OrdinalIgnoreCase) ||
                 cleanIdent.EndsWith("service", StringComparison.OrdinalIgnoreCase)))
            {
                var fileFallback = FindFileLevelFallbackUrl(node, cleanIdent, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(fileFallback)) return fileFallback;
            }

            // 6. Suffix cleanup: hubUrl -> hub
            return CleanIdentifierSuffix(cleanIdent);
        }
        finally
        {
            visitedVars.Remove(exprKey);
        }
    }

    public static string CleanIdentifierSuffix(string ident)
    {
        var suffixes = new[] { "BaseUrl", "BaseURL", "Url", "URL", "Host", "Domain", "Client", "Endpoint", "Service" };
        foreach (var s in suffixes)
        {
            if (ident.EndsWith(s, StringComparison.OrdinalIgnoreCase) && ident.Length > s.Length)
            {
                var stripped = ident[..^s.Length].TrimEnd('_', '-');
                if (stripped.Length >= 2)
                {
                    if (WorkspaceConventions.IsGenericUrlStemOrVariable(stripped))
                    {
                        return ident;
                    }
                    return stripped;
                }
            }
        }
        return ident;
    }

    private static string? FindMethodReturnInAst(Node node, string methodName, int depth = 0, HashSet<string>? visitedVars = null)
    {
        if (depth > 4) return null;
        string? targetClassName = null;
        var cleanMethod = methodName.Trim();
        if (cleanMethod.Contains('.'))
        {
            var parts = cleanMethod.Split('.');
            targetClassName = parts[0];
            cleanMethod = parts[^1];
        }

        var curr = node;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.TypeScript.ClassDeclaration) || curr.Is(TreeSitterSyntax.TypeScript.Program))
            {
                if (!string.IsNullOrEmpty(targetClassName) && curr.Is(TreeSitterSyntax.TypeScript.ClassDeclaration))
                {
                    var cNameNode = curr.GetField(TreeSitterSyntax.Fields.Name) ??
                                    curr.GetChildForField(TreeSitterSyntax.Fields.Name);
                    if (cNameNode.IsValid() && !string.Equals(cNameNode.Text, targetClassName, StringComparison.OrdinalIgnoreCase))
                    {
                        curr = curr.Parent;
                        continue;
                    }
                }

                foreach (var child in curr.Children)
                {
                    if (child.Is("class_body"))
                    {
                        foreach (var m in child.Children)
                        {
                            var res = CheckMethodNode(m, cleanMethod, depth, visitedVars);
                            if (res != null) return res;
                        }
                    }
                    else if (child.Is("export_statement"))
                    {
                        foreach (var expChild in child.Children)
                        {
                            if (expChild.Is(TreeSitterSyntax.TypeScript.ClassDeclaration))
                            {
                                if (!string.IsNullOrEmpty(targetClassName))
                                {
                                    var cNameNode = expChild.GetField(TreeSitterSyntax.Fields.Name) ??
                                                    expChild.GetChildForField(TreeSitterSyntax.Fields.Name);
                                    if (cNameNode.IsValid() && !string.Equals(cNameNode.Text, targetClassName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }
                                }
                                var expBody = expChild.FindChildOfType(TreeSitterSyntax.TypeScript.ClassBody) ?? expChild;
                                foreach (var m in expBody.Children)
                                {
                                    var res = CheckMethodNode(m, cleanMethod, depth, visitedVars);
                                    if (res != null) return res;
                                }
                            }
                            var expCheck = CheckMethodNode(expChild, cleanMethod, depth, visitedVars);
                            if (expCheck != null) return expCheck;
                        }
                    }
                    var checkRes = CheckMethodNode(child, cleanMethod, depth, visitedVars);
                    if (checkRes != null) return checkRes;
                }
            }
            curr = curr.Parent;
        }
        return null;

        static string? CheckMethodNode(Node mNode, string mName, int d, HashSet<string>? visited)
        {
            if (mNode.Type.Contains("method") || mNode.Type.Contains("function"))
            {
                var n = mNode.GetField(TreeSitterSyntax.Fields.Name) ??
                        mNode.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                        mNode.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.Identifier, TreeSitterSyntax.TypeScript.PropertyIdentifier));
                if (n.IsValid() && string.Equals(n.Text, mName, StringComparison.OrdinalIgnoreCase))
                {
                    var ret = FindReturnStatement(mNode);
                    if (ret.IsValid())
                    {
                        var exprNode = ret.Children.LastOrDefault(c => c.IsValid() && c.Type != ";" && c.Type != TreeSitterSyntax.Fields.Return);
                        if (exprNode.IsValid())
                        {
                            return ResolveTemplateExpression(mNode, exprNode.Text, d + 1, visited);
                        }
                    }
                }
            }
            return null;
        }

        static Node? FindReturnStatement(Node block)
        {
            foreach (var ch in block.Children)
            {
                if (ch.Type == TreeSitterSyntax.TypeScript.ReturnStatement) return ch;
                var nested = FindReturnStatement(ch);
                if (nested.IsValid()) return nested;
            }
            return null;
        }
    }

    private static string? FindFileLevelFallbackUrl(Node node, string hint, int depth = 0, HashSet<string>? visitedVars = null)
    {
        if (depth > 2) return null;
        visitedVars ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visitedVars.Add("fallback:file")) return null;

        try
        {
            var root = node;
            while (root.Parent.IsValid()) root = root.Parent;

            foreach (var decl in FindAllDeclarations(root))
            {
                var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ?? decl.GetChildForField("name");
                if (nameNode.IsValid())
                {
                    var nameText = nameNode.Text;
                    if (string.Equals(nameText, hint, StringComparison.OrdinalIgnoreCase)) continue;

                    if (nameText.Contains("Url", StringComparison.OrdinalIgnoreCase) ||
                        nameText.Contains("Endpoint", StringComparison.OrdinalIgnoreCase))
                    {
                        var valNode = decl.GetField(TreeSitterSyntax.Fields.Value) ?? decl.GetChildForField("value");
                        if (valNode.IsValid())
                        {
                            var text = valNode.Text.Trim();
                            var res = ResolveTemplateExpression(root, text, depth + 1, visitedVars);
                            if (!string.IsNullOrEmpty(res) &&
                                (res.StartWithIC("http://") ||
                                 res.StartWithIC("https://")))
                            {
                                return res;
                            }
                        }
                    }
                }
            }
            return null;
        }
        finally
        {
            visitedVars.Remove("fallback:file");
        }

        static IEnumerable<Node> FindAllDeclarations(Node parent)
        {
            foreach (var ch in parent.Children)
            {
                if (ch.Is(TreeSitterSyntax.TypeScript.VariableDeclarator)) yield return ch;
                foreach (var sub in FindAllDeclarations(ch)) yield return sub;
            }
        }
    }

    private static string CombineServiceAndPath(string? service, string cleanPath)
    {
        if (string.IsNullOrEmpty(service) ||
            cleanPath.StartWithIC("http://") ||
            cleanPath.StartWithIC("https://") ||
            cleanPath.StartWithIC("ws://") ||
            cleanPath.StartWithIC("wss://"))
        {
            return cleanPath;
        }
        return $"{service}{cleanPath}";
    }
}
