using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public static class AstHelper
{
    public static string? ResolveStringOrTemplate(Node? argNode)
    {
        if (!argNode.IsValid()) return null;

        if (IsStringLiteralNode(argNode))
        {
            var text = argNode.Text.Trim('\'', '"', '`');
            if (text.Contains('\n') || text.Length > 500) return null;

            var routeMatch = Regex.Match(text, @"getServiceDomainByRoute\s*\(\s*['""]([^'""]+)['""]");
            if (routeMatch.Success)
            {
                var routeKey = routeMatch.Groups[1].Value;
                if (RouteDictionaryRegistry.TryResolve(routeKey, out var rPath, out var rService))
                {
                    var cleanPath = rPath.Split('?')[0];
                    return CombineServiceAndPath(rService, cleanPath);
                }
            }

            var decomposed = TryDecomposeTemplateString(argNode, text);
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

            var val = FindVariableInitializerInAst(argNode, varName);
            if (val != null)
            {
                var subDecomp = TryDecomposeTemplateString(argNode, val);
                return NormalizeResolvedUrl(subDecomp ?? val);
            }

            if (Regex.IsMatch(varName, @"^[A-Z0-9_]{3,}$") ||
                varName.EndsWith("Topic", StringComparison.OrdinalIgnoreCase) ||
                varName.EndsWith("TopicName", StringComparison.OrdinalIgnoreCase) ||
                varName.EndsWith("Queue", StringComparison.OrdinalIgnoreCase) ||
                varName.EndsWith("QueueName", StringComparison.OrdinalIgnoreCase) ||
                varName.EndsWith("Sub", StringComparison.OrdinalIgnoreCase) ||
                varName.EndsWith("SubName", StringComparison.OrdinalIgnoreCase) ||
                varName.EndsWith("Subscription", StringComparison.OrdinalIgnoreCase) ||
                varName.EndsWith("SubscriptionName", StringComparison.OrdinalIgnoreCase))
            {
                return varName;
            }

            var stripped = CleanIdentifierSuffix(varName);
            if (stripped != varName)
            {
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

            if (argNode.Text.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
            {
                var propName = argNode.Text[5..].Trim();
                var classVal = FindClassFieldInitializerInAst(argNode, propName);
                if (!string.IsNullOrEmpty(classVal))
                {
                    var subDecomp = TryDecomposeTemplateString(argNode, classVal);
                    return NormalizeResolvedUrl(subDecomp ?? classVal);
                }
            }

            var envMatch = Regex.Match(argNode.Text, @"(?:process\.env|env\??|config(?:\.get)?)\.([A-Za-z0-9_]+)");
            if (envMatch.Success)
            {
                return envMatch.Groups[1].Value;
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
                var val = FindVariableInitializerInAst(argNode, propText);
                if (val != null)
                {
                    var subDecomp = TryDecomposeTemplateString(argNode, val);
                    return NormalizeResolvedUrl(subDecomp ?? val);
                }

                if (Regex.IsMatch(propText, @"^[A-Z0-9_]{3,}$") ||
                    propText.EndsWith("Topic", StringComparison.OrdinalIgnoreCase) ||
                    propText.EndsWith("TopicName", StringComparison.OrdinalIgnoreCase) ||
                    propText.EndsWith("Queue", StringComparison.OrdinalIgnoreCase) ||
                    propText.EndsWith("QueueName", StringComparison.OrdinalIgnoreCase) ||
                    propText.EndsWith("Sub", StringComparison.OrdinalIgnoreCase) ||
                    propText.EndsWith("SubName", StringComparison.OrdinalIgnoreCase) ||
                    propText.EndsWith("Subscription", StringComparison.OrdinalIgnoreCase) ||
                    propText.EndsWith("SubscriptionName", StringComparison.OrdinalIgnoreCase))
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

        if (argNode.Is(TreeSitterSyntax.TypeScript.Object) || argNode.Type == "object")
        {
            if (TryGetObjectProperty(argNode, "topicName", out var tp) ||
                TryGetObjectProperty(argNode, "topic", out tp) ||
                TryGetObjectProperty(argNode, "queue", out tp) ||
                TryGetObjectProperty(argNode, "queueName", out tp))
            {
                var resolved = ResolveStringOrTemplate(tp);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }
        }

        if (argNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var firstArg = ExtractFirstStringArgument(argNode);
            if (!string.IsNullOrEmpty(firstArg))
            {
                return NormalizeResolvedUrl(firstArg);
            }

            var func = argNode.GetFunctionNode();
            if (func.IsValid())
            {
                var funcText = func.Text;
                if (funcText.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
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
            }
        }

        return null;
    }

    public static string NormalizeResolvedUrl(string raw)
    {
        return RouteDictionaryRegistry.NormalizeResolvedUrl(raw);
    }

    public static string? ExtractFirstStringArgument(Node? callNode)
    {
        if (!callNode.IsValid()) return null;

        var argList = callNode.GetField(TreeSitterSyntax.Fields.Arguments)
            ?? callNode.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);

        if (argList.IsValid())
        {
            var firstArg = argList.Children.FirstOrDefault(c =>
                c.IsValid() && IsStringLiteralNode(c));

            if (firstArg.IsValid())
            {
                return firstArg.Text.Trim('\'', '"', '`');
            }

            var firstIdent = argList.Children.FirstOrDefault(c => c.IsValid() && c.Is(TreeSitterSyntax.TypeScript.Identifier));
            if (firstIdent.IsValid())
            {
                return ResolveStringOrTemplate(firstIdent);
            }
        }

        return null;
    }

    public static List<Node> GetCallArguments(Node? callNode)
    {
        var result = new List<Node>();
        if (!callNode.IsValid()) return result;

        var argList = callNode.GetField(TreeSitterSyntax.Fields.Arguments)
            ?? callNode.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);

        if (argList.IsValid())
        {
            foreach (var child in argList.Children)
            {
                if (child.IsValid() && !string.IsNullOrEmpty(child.Type) && (char.IsLetter(child.Type[0]) || child.Type[0] == '_'))
                {
                    result.Add(child);
                }
            }
        }

        return result;
    }

    public static bool TryGetMemberAccess(Node? callNode, out Node? objNode, out string? propName)
    {
        objNode = null;
        propName = null;
        if (!callNode.IsValid()) return false;

        var func = callNode.GetFunctionNode();
        if (func.IsValid() && func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            objNode = func.GetField(TreeSitterSyntax.Fields.Object);
            var propNode = func.GetField(TreeSitterSyntax.Fields.Property);
            if (propNode.IsValid())
            {
                propName = propNode.Text;
                return true;
            }
        }

        return false;
    }

    public static bool IsStringLiteralNode(Node node)
    {
        return node.IsAny(TreeSitterSyntax.TypeScript.String,
                           TreeSitterSyntax.TypeScript.TemplateString,
                           TreeSitterSyntax.Common.StringLiteral,
                           TreeSitterSyntax.Go.InterpretedStringLiteral);
    }

    public static bool TryGetObjectProperty(Node? objectNode, string propertyName, out Node? valueNode)
    {
        valueNode = null;
        if (!objectNode.IsValid()) return false;

        foreach (var child in objectNode.Children)
        {
            if (child.IsAny(TreeSitterSyntax.TypeScript.Pair, TreeSitterSyntax.TypeScript.Property))
            {
                var keyNode = child.GetField(TreeSitterSyntax.Fields.Key) ??
                              child.GetField(TreeSitterSyntax.Fields.Name) ??
                              child.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.PropertyIdentifier, TreeSitterSyntax.TypeScript.Identifier));

                if (keyNode.IsValid() && string.Equals(keyNode.Text, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    valueNode = child.GetField(TreeSitterSyntax.Fields.Value);
                    if (!valueNode.IsValid() && child.Children.Count >= 3)
                    {
                        valueNode = child.Children[^1];
                    }
                    return valueNode.IsValid();
                }
            }
        }

        return false;
    }

    private static string? FindVariableInitializerInAst(Node node, string varName)
    {
        var cleanVar = varName.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? varName[5..].Trim() : varName;
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.Is("class_body") || curr.Is("class_declaration") || curr.Type.Contains("class"))
            {
                var fieldVal = FindClassFieldInitializerInAst(curr, cleanVar);
                if (fieldVal != null) return fieldVal;
            }

            if (curr.IsAny(TreeSitterSyntax.TypeScript.StatementBlock, TreeSitterSyntax.TypeScript.Program))
            {
                foreach (var child in curr.Children)
                {
                    if (child.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration))
                    {
                        foreach (var decl in child.Children.Where(c => c.Is(TreeSitterSyntax.TypeScript.VariableDeclarator)))
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name);
                            if (nameNode.IsValid() && (nameNode.Text == varName || nameNode.Text == cleanVar))
                            {
                                var valNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                                if (valNode.IsValid())
                                {
                                    if (IsStringLiteralNode(valNode))
                                    {
                                        var text = valNode.Text.Trim('\'', '"', '`');
                                        if (!text.Contains('\n') && text.Length <= 500)
                                        {
                                            return text;
                                        }
                                    }
                                    else if (valNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
                                    {
                                        var callRes = ResolveStringOrTemplate(valNode);
                                        if (!string.IsNullOrEmpty(callRes))
                                        {
                                            return callRes;
                                        }

                                        var funcNode = valNode.GetFunctionNode();
                                        if (funcNode.IsValid())
                                        {
                                            var propNode = funcNode.Is(TreeSitterSyntax.TypeScript.MemberExpression)
                                                ? funcNode.GetField(TreeSitterSyntax.Fields.Property)
                                                : default;
                                            var funcName = propNode.IsValid() ? propNode.Text : funcNode.Text;
                                            var methodRet = FindMethodReturnInAst(valNode, funcName);
                                            if (!string.IsNullOrEmpty(methodRet)) return methodRet;
                                        }
                                    }
                                    else if (valNode.Is(TreeSitterSyntax.Common.BinaryExpression))
                                    {
                                        var left = valNode.GetField(TreeSitterSyntax.Fields.Left);
                                        var right = valNode.GetField(TreeSitterSyntax.Fields.Right);
                                        var leftRes = left.IsValid() ? ResolveStringOrTemplate(left) : null;
                                        if (!string.IsNullOrEmpty(leftRes) && (leftRes.StartsWith("http") || leftRes.Contains('.'))) return leftRes;
                                        var rightRes = right.IsValid() ? ResolveStringOrTemplate(right) : null;
                                        if (!string.IsNullOrEmpty(rightRes)) return rightRes;
                                        if (right.IsValid() && IsStringLiteralNode(right))
                                        {
                                            return right.Text.Trim('\'', '"', '`');
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            curr = curr.Parent;
        }
        return null;
    }

    private static string? TryDecomposeTemplateString(Node node, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

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
            var resolvedBase = ResolveTemplateExpression(node, expr);
            if (!string.IsNullOrEmpty(resolvedBase))
            {
                if (resolvedBase.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    resolvedBase.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{resolvedBase.TrimEnd('/')}/{tail.TrimStart('/')}";
                }
                return $"https://{resolvedBase}/{tail.TrimStart('/')}";
            }
        }

        return null;
    }

    private static string? ResolveTemplateExpression(Node node, string expr, int depth = 0)
    {
        if (depth > 4 || string.IsNullOrWhiteSpace(expr)) return null;

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
                        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            return s;
                    }
                }
                // Second pass: concrete url resolved from member or identifier
                foreach (var part in parts)
                {
                    var resolved = ResolveTemplateExpression(node, part, depth + 1);
                    if (!string.IsNullOrEmpty(resolved) &&
                        (resolved.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         resolved.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        return resolved;
                    }
                }
                // Third pass: env or config
                foreach (var part in parts)
                {
                    var envMatch = Regex.Match(part, @"(?:process\.env|env\??|config(?:\.get)?)\.([A-Za-z0-9_]+)");
                    if (envMatch.Success) return envMatch.Groups[1].Value;
                }
                // Fourth pass: member / identifier
                foreach (var part in parts)
                {
                    var resolved = ResolveTemplateExpression(node, part, depth + 1);
                    if (!string.IsNullOrEmpty(resolved)) return resolved;
                }
            }
        }

        // 2. Env / config: process.env.HUB_INGEST_URL, env?.HUB_INGEST_URL, config.get('HUB_INGEST_URL')
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

        // 3. Member / field access: this.<prop>, ClassName.<prop>
        if (expr.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ||
            (expr.Contains('.') && !expr.Contains('(')))
        {
            var propName = expr.Split('.').Last().Trim();
            var classVal = FindClassFieldInitializerInAst(node, propName);
            if (!string.IsNullOrEmpty(classVal)) return classVal;
            if (expr.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
            {
                return CleanIdentifierSuffix(propName);
            }
        }

        // 4. Method call: e.g. getHubUrl(env) or TelemetryReporter.getHubUrl(env)
        var callMatch = Regex.Match(expr, @"^(?:[A-Za-z0-9_]+\.)?([A-Za-z0-9_]+)\(");
        if (callMatch.Success)
        {
            var methodName = callMatch.Groups[1].Value;
            var methodRet = FindMethodReturnInAst(node, methodName, depth + 1);
            if (!string.IsNullOrEmpty(methodRet)) return methodRet;
        }

        // 5. Variable access: strip trailing method calls like baseUrl.replace(...)
        var cleanIdent = Regex.Replace(expr, @"\.[a-zA-Z0-9_]+\(.*?\)", "").Trim();

        // Check if there is a variable in the local scope
        var localVal = FindVariableInitializerInAst(node, cleanIdent);
        if (!string.IsNullOrEmpty(localVal))
        {
            if (localVal.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                localVal.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return localVal;
            }
            var subDecomp = TryDecomposeTemplateString(node, localVal);
            if (!string.IsNullOrEmpty(subDecomp)) return subDecomp;

            return localVal;
        }

        // Check if file has a fallback definition for *apiUrl* or *baseUrl*
        if (depth == 0 &&
            (cleanIdent.EndsWith("url", StringComparison.OrdinalIgnoreCase) ||
             cleanIdent.EndsWith("client", StringComparison.OrdinalIgnoreCase) ||
             cleanIdent.EndsWith("service", StringComparison.OrdinalIgnoreCase)))
        {
            var fileFallback = FindFileLevelFallbackUrl(node, cleanIdent, depth + 1);
            if (!string.IsNullOrEmpty(fileFallback)) return fileFallback;
        }

        // 6. Suffix cleanup: hubUrl -> hub
        return CleanIdentifierSuffix(cleanIdent);
    }

    private static string CleanIdentifierSuffix(string ident)
    {
        var suffixes = new[] { "BaseUrl", "BaseURL", "Url", "URL", "Host", "Domain", "Client", "Endpoint", "Service" };
        foreach (var s in suffixes)
        {
            if (ident.EndsWith(s, StringComparison.OrdinalIgnoreCase) && ident.Length > s.Length)
            {
                var stripped = ident[..^s.Length].TrimEnd('_', '-');
                if (stripped.Length >= 2) return stripped;
            }
        }
        return ident;
    }

    private static string? FindClassFieldInitializerInAst(Node node, string propName)
    {
        var cleanProp = propName.Contains('.') ? propName.Split('.').Last().Trim() : propName.Trim();
        var curr = node;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.TypeScript.ClassDeclaration) || curr.Type == "class_declaration" || curr.Type == "class_body")
            {
                var body = curr.Is("class_body") ? curr : curr.FindChildOfType("class_body") ?? curr;
                foreach (var member in body.Children)
                {
                    if (member.Type.Contains("field") || member.Type.Contains("property"))
                    {
                        var nameNode = member.GetField(TreeSitterSyntax.Fields.Name) ??
                                       member.GetChildForField("name") ??
                                       member.Children.FirstOrDefault(c => c.Is("property_identifier") || c.Is(TreeSitterSyntax.TypeScript.Identifier));
                        if (nameNode.IsValid() && string.Equals(nameNode.Text, cleanProp, StringComparison.OrdinalIgnoreCase))
                        {
                            var valNode = member.GetField(TreeSitterSyntax.Fields.Value) ??
                                          member.GetChildForField("value") ??
                                          member.Children.LastOrDefault();
                            if (valNode.IsValid())
                            {
                                if (IsStringLiteralNode(valNode))
                                {
                                    return valNode.Text.Trim('\'', '"', '`');
                                }
                                var identText = valNode.Text.Trim();
                                if (RouteDictionaryRegistry.TryResolve(identText, out var rp, out var rs))
                                {
                                    return !string.IsNullOrEmpty(rs) ? $"{rs}{rp}" : rp;
                                }
                                var varVal = FindVariableInitializerInAst(curr, identText);
                                if (!string.IsNullOrEmpty(varVal)) return varVal;
                                return identText;
                            }
                        }
                    }
                }
            }

            if (curr.Is(TreeSitterSyntax.TypeScript.Program) || curr.Type == "program")
            {
                foreach (var classDecl in curr.Children.Where(c => c.Is(TreeSitterSyntax.TypeScript.ClassDeclaration) || c.Type == "class_declaration"))
                {
                    var body = classDecl.FindChildOfType("class_body") ?? classDecl;
                    foreach (var member in body.Children)
                    {
                        if (member.Type.Contains("field") || member.Type.Contains("property"))
                        {
                            var nameNode = member.GetField(TreeSitterSyntax.Fields.Name) ??
                                           member.GetChildForField("name") ??
                                           member.Children.FirstOrDefault(c => c.Is("property_identifier") || c.Is(TreeSitterSyntax.TypeScript.Identifier));
                            if (nameNode.IsValid() && string.Equals(nameNode.Text, cleanProp, StringComparison.OrdinalIgnoreCase))
                            {
                                var valNode = member.GetField(TreeSitterSyntax.Fields.Value) ??
                                              member.GetChildForField("value") ??
                                              member.Children.LastOrDefault();
                                if (valNode.IsValid())
                                {
                                    if (IsStringLiteralNode(valNode))
                                    {
                                        return valNode.Text.Trim('\'', '"', '`');
                                    }
                                    var identText = valNode.Text.Trim();
                                    if (RouteDictionaryRegistry.TryResolve(identText, out var rp, out var rs))
                                    {
                                        return !string.IsNullOrEmpty(rs) ? $"{rs}{rp}" : rp;
                                    }
                                    var varVal = FindVariableInitializerInAst(curr, identText);
                                    if (!string.IsNullOrEmpty(varVal)) return varVal;
                                    return identText;
                                }
                            }
                        }
                    }
                }
            }

            curr = curr.Parent;
        }
        return null;
    }

    private static string? FindMethodReturnInAst(Node node, string methodName, int depth = 0)
    {
        if (depth > 4) return null;
        var curr = node;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.TypeScript.ClassDeclaration) || curr.Is(TreeSitterSyntax.TypeScript.Program))
            {
                foreach (var child in curr.Children)
                {
                    if (child.Is("class_body"))
                    {
                        foreach (var m in child.Children)
                        {
                            var res = CheckMethodNode(m, methodName, depth);
                            if (res != null) return res;
                        }
                    }
                    var checkRes = CheckMethodNode(child, methodName, depth);
                    if (checkRes != null) return checkRes;
                }
            }
            curr = curr.Parent;
        }
        return null;

        static string? CheckMethodNode(Node mNode, string mName, int d)
        {
            if (mNode.Type.Contains("method") || mNode.Type.Contains("function"))
            {
                var n = mNode.GetField(TreeSitterSyntax.Fields.Name) ??
                        mNode.GetChildForField("name") ??
                        mNode.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.TypeScript.Identifier) || c.Is("property_identifier"));
                if (n.IsValid() && string.Equals(n.Text, mName, StringComparison.OrdinalIgnoreCase))
                {
                    var ret = FindReturnStatement(mNode);
                    if (ret.IsValid())
                    {
                        var exprNode = ret.Children.LastOrDefault(c => c.IsValid() && c.Type != ";" && c.Type != "return");
                        if (exprNode.IsValid())
                        {
                            return ResolveTemplateExpression(mNode, exprNode.Text, d + 1);
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
                if (ch.Type == "return_statement") return ch;
                var nested = FindReturnStatement(ch);
                if (nested.IsValid()) return nested;
            }
            return null;
        }
    }

    private static string? FindFileLevelFallbackUrl(Node node, string hint, int depth = 0)
    {
        if (depth > 2) return null;
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
                        var res = ResolveTemplateExpression(root, text, depth + 1);
                        if (!string.IsNullOrEmpty(res) &&
                            (res.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             res.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                        {
                            return res;
                        }
                    }
                }
            }
        }
        return null;

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
            cleanPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            cleanPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            cleanPath.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
            cleanPath.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
        {
            return cleanPath;
        }
        return $"{service}{cleanPath}";
    }
}
