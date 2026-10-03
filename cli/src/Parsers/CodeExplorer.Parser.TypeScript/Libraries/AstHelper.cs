using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public static class AstHelper
{
    private const int MaxRecursionDepth = 8;

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

            if (WorkspaceConventions.IsPlaceholderName(varName))
            {
                return null;
            }

            if (ConstantRegistry.TryResolve(contextOrProject, varName, out var cVal) && !string.IsNullOrEmpty(cVal))
            {
                return cVal;
            }

            var val = FindVariableInitializerInAst(argNode, varName, depth + 1, visitedVars);
            if (val != null)
            {
                var subDecomp = TryDecomposeTemplateString(argNode, val);
                return NormalizeResolvedUrl(subDecomp ?? val);
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

            if (argNode.Text.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
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
                var classVal = FindClassFieldInitializerInAst(argNode, propText);
                if (!string.IsNullOrEmpty(classVal))
                {
                    var subDecomp = TryDecomposeTemplateString(argNode, classVal);
                    return NormalizeResolvedUrl(subDecomp ?? classVal);
                }

                var val = FindVariableInitializerInAst(argNode, propText, depth + 1, visitedVars);
                if (val != null)
                {
                    var subDecomp = TryDecomposeTemplateString(argNode, val);
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

    public static bool IsValidTopicOrQueueLiteral(string? s)
    {
        return WorkspaceConventions.IsValidTopicOrQueueName(s);
    }

    public static string? ResolveTopicOrQueue(Node? argNode, string? contextOrProject = null)
    {
        if (!argNode.IsValid()) return null;

        // 1. Literal string or template
        if (IsStringLiteralNode(argNode))
        {
            var text = argNode.Text.Trim('\'', '"', '`');
            if (IsValidTopicOrQueueLiteral(text))
            {
                if (WorkspaceConventions.TryGetTopicAlias(text, out var mapped)) return mapped;
                return text;
            }
            return null;
        }

        // 2. MemberExpression (process.env.ORDERS_TOPIC, this.config.ruleTreeTopic, this.queueName)
        if (argNode.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var envMatch = Regex.Match(argNode.Text, @"(?:process\.env|env\??|config(?:\.get)?)\.([A-Za-z0-9_]+)");
            if (envMatch.Success)
            {
                var envKey = envMatch.Groups[1].Value;

                // Look up in ConstantRegistry (including .env)
                if (ConstantRegistry.TryResolve(contextOrProject, envKey, out var envVal) && !string.IsNullOrEmpty(envVal))
                {
                    if (IsValidTopicOrQueueLiteral(envVal))
                    {
                        if (WorkspaceConventions.TryGetTopicAlias(envVal, out var mapped)) return mapped;
                        return envVal;
                    }
                }

                // If not in .env, derive only if it's an explicit messaging key convention
                if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(envKey, out var derivedVal))
                {
                    return derivedVal;
                }

                if (WorkspaceConventions.TryGetTopicAlias(envKey, out var aliasVal))
                {
                    return aliasVal;
                }

                return null;
            }

            // Normal member expression: this.topicName, this.ruleTreeTopic
            var memberVal = ResolveStringOrTemplate(argNode, contextOrProject);
            if (!string.IsNullOrEmpty(memberVal) && IsValidTopicOrQueueLiteral(memberVal))
            {
                if (WorkspaceConventions.TryGetTopicAlias(memberVal, out var mapped)) return mapped;
                return memberVal;
            }

            return null;
        }

        // 3. CallExpression (configService.getString('RULE_TREE_TOPIC'), getTopic('orders'))
        if (argNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var firstArg = ExtractFirstStringArgument(argNode);
            if (!string.IsNullOrEmpty(firstArg))
            {
                if (ConstantRegistry.TryResolve(contextOrProject, firstArg, out var resVal) && !string.IsNullOrEmpty(resVal))
                {
                    if (IsValidTopicOrQueueLiteral(resVal))
                    {
                        if (WorkspaceConventions.TryGetTopicAlias(resVal, out var mapped)) return mapped;
                        return resVal;
                    }
                }

                if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(firstArg, out var derived))
                {
                    return derived;
                }

                if (WorkspaceConventions.TryGetTopicAlias(firstArg, out var alias))
                {
                    return alias;
                }

                if (IsValidTopicOrQueueLiteral(firstArg))
                {
                    return firstArg;
                }
            }

            var callVal = ResolveStringOrTemplate(argNode, contextOrProject);
            if (!string.IsNullOrEmpty(callVal) && IsValidTopicOrQueueLiteral(callVal))
            {
                return callVal;
            }

            return null;
        }

        // 4. Identifier (EVENT_BUS_TOPIC_NAME, topicName)
        if (argNode.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            var varName = argNode.Text;

            // A. Check ConstantRegistry (file constants, project constants, exported constants)
            if (ConstantRegistry.TryResolve(contextOrProject, varName, out var constVal) && !string.IsNullOrEmpty(constVal))
            {
                if (IsValidTopicOrQueueLiteral(constVal))
                {
                    if (WorkspaceConventions.TryGetTopicAlias(constVal, out var mapped)) return mapped;
                    return constVal;
                }
            }

            // B. Check AST variable or field initializer in current scope
            var astVal = FindVariableInitializerInAst(argNode, varName);
            if (!string.IsNullOrEmpty(astVal) && IsValidTopicOrQueueLiteral(astVal))
            {
                if (WorkspaceConventions.TryGetTopicAlias(astVal, out var mapped)) return mapped;
                return astVal;
            }

            // C. Configured alias in conventions.json
            if (WorkspaceConventions.TryGetTopicAlias(varName, out var alias))
            {
                return alias;
            }

            // D. ONLY if the identifier is explicitly named in SCREAMING_SNAKE_CASE ending in _TOPIC / _QUEUE
            if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(varName, out var derivedTopic))
            {
                return derivedTopic;
            }

            // Unresolved parameter, type annotation, or local var without initializer -> NEVER guess!
            return null;
        }

        // 5. Object literal: { topicName: ... }
        if (argNode.Is(TreeSitterSyntax.TypeScript.Object))
        {
            if (TryGetObjectProperty(argNode, "topicName", out var tp) ||
                TryGetObjectProperty(argNode, "topic", out tp) ||
                TryGetObjectProperty(argNode, "queue", out tp) ||
                TryGetObjectProperty(argNode, "queueName", out tp))
            {
                return ResolveTopicOrQueue(tp, contextOrProject);
            }
        }

        return null;
    }

    public static string NormalizeResolvedUrl(string raw)
    {
        return RouteDictionaryRegistry.NormalizeResolvedUrl(raw);
    }

    public static string? ExtractFirstStringArgument(Node? callNode, string? contextOrProject = null)
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

            var firstArgExpr = argList.Children.FirstOrDefault(c =>
                c.IsValid() && (c.Is(TreeSitterSyntax.TypeScript.Identifier) || c.Is(TreeSitterSyntax.TypeScript.MemberExpression)));
            if (firstArgExpr.IsValid())
            {
                return ResolveStringOrTemplate(firstArgExpr, contextOrProject);
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

    public static string? FindVariableInitializerInAst(Node node, string varName)
    {
        return FindVariableInitializerInAst(node, varName, 0, null);
    }

    public static string? FindVariableInitializerInAst(Node node, string varName, int depth, HashSet<string>? visitedVars)
    {
        if (depth > MaxRecursionDepth) return null;
        visitedVars ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visitedVars.Add(varName)) return null;

        try
        {
            var cleanVar = varName.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? varName[5..].Trim() : varName;
            var curr = node.Parent;
            var maxScopeSteps = 50;
            var steps = 0;
            while (curr.IsValid() && ++steps <= maxScopeSteps)
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
                                if (!nameNode.IsValid())
                                {
                                    nameNode = decl.FindChildOfType("object_pattern") ??
                                               decl.Children.FirstOrDefault(c => c.Is("object_pattern"));
                                }
                                if (nameNode.IsValid())
                                {
                                    var valNode = decl.GetField(TreeSitterSyntax.Fields.Value);

                                    // Handle direct variable name match: const foo = ...
                                    if (nameNode.Text == varName || nameNode.Text == cleanVar)
                                    {
                                        if (valNode.IsValid())
                                        {
                                            if (IsNodeContainedWithin(node, valNode))
                                            {
                                                continue;
                                            }

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
                                                var callRes = ResolveStringOrTemplate(valNode, null, depth + 1, visitedVars);
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
                                                var leftRes = left.IsValid() ? ResolveStringOrTemplate(left, null, depth + 1, visitedVars) : null;
                                                if (!string.IsNullOrEmpty(leftRes) && (leftRes.StartsWith("http") || leftRes.Contains('.'))) return leftRes;
                                                var rightRes = right.IsValid() ? ResolveStringOrTemplate(right, null, depth + 1, visitedVars) : null;
                                                if (!string.IsNullOrEmpty(rightRes)) return rightRes;
                                                if (right.IsValid() && IsStringLiteralNode(right))
                                                {
                                                    return right.Text.Trim('\'', '"', '`');
                                                }
                                            }
                                        }
                                    }
                                    // Handle destructuring: const { GLOBAL_PREFIX, START_PREFIX } = HTTP_API_PREFIX_CONFIG.SMART_CPA
                                    else if (nameNode.Is("object_pattern") && valNode.IsValid())
                                    {
                                        foreach (var patChild in nameNode.Children)
                                        {
                                            var matchProp = false;
                                            string? propAlias = null;

                                            if (patChild.Is("shorthand_property_identifier") ||
                                                patChild.Is(TreeSitterSyntax.TypeScript.Identifier))
                                            {
                                                if (patChild.Text == varName || patChild.Text == cleanVar)
                                                {
                                                    matchProp = true;
                                                    propAlias = patChild.Text;
                                                }
                                            }
                                            else if (patChild.Is("pair_pattern") || patChild.Is(TreeSitterSyntax.TypeScript.Pair))
                                            {
                                                var key = patChild.GetField(TreeSitterSyntax.Fields.Key);
                                                var val = patChild.GetField(TreeSitterSyntax.Fields.Value);
                                                if (val.IsValid() && (val.Text == varName || val.Text == cleanVar))
                                                {
                                                    matchProp = true;
                                                    propAlias = key.IsValid() ? key.Text : val.Text;
                                                }
                                                else if (key.IsValid() && (key.Text == varName || key.Text == cleanVar))
                                                {
                                                    matchProp = true;
                                                    propAlias = key.Text;
                                                }
                                            }

                                            if (matchProp && !string.IsNullOrEmpty(propAlias))
                                            {
                                                var rhsText = valNode.Text.Trim();
                                                var fullLookupKey = $"{rhsText}.{propAlias}";

                                                if (ConstantRegistry.TryResolve(null, fullLookupKey, out var resolvedVal) && !string.IsNullOrEmpty(resolvedVal))
                                                {
                                                    return resolvedVal;
                                                }

                                                // Check if any constant in ProjectConstants or GlobalConstants ends with .fullLookupKey or :fullLookupKey
                                                var suffixWithDot = "." + fullLookupKey;
                                                var suffixWithColon = ":" + fullLookupKey;
                                                foreach (var kvp in ConstantRegistry.ProjectConstants)
                                                {
                                                    if (kvp.Key.EndsWith(suffixWithDot, StringComparison.OrdinalIgnoreCase) ||
                                                        kvp.Key.EndsWith(suffixWithColon, StringComparison.OrdinalIgnoreCase) ||
                                                        kvp.Key.Equals(fullLookupKey, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        return kvp.Value;
                                                    }
                                                }
                                                foreach (var kvp in ConstantRegistry.GlobalConstants)
                                                {
                                                    if (kvp.Key.EndsWith(suffixWithDot, StringComparison.OrdinalIgnoreCase) ||
                                                        kvp.Key.Equals(fullLookupKey, StringComparison.OrdinalIgnoreCase))
                                                    {
                                                        return kvp.Value;
                                                    }
                                                }

                                                // If RHS is an identifier/member, resolve it and check
                                                var resolvedRhs = ResolveStringOrTemplate(valNode, null, depth + 1, visitedVars);
                                                if (!string.IsNullOrEmpty(resolvedRhs))
                                                {
                                                    var resolvedFullKey = $"{resolvedRhs}.{propAlias}";
                                                    if (ConstantRegistry.TryResolve(null, resolvedFullKey, out var valFromResolved) && !string.IsNullOrEmpty(valFromResolved))
                                                    {
                                                        return valFromResolved;
                                                    }

                                                    var resSuffixWithDot = "." + resolvedFullKey;
                                                    var resSuffixWithColon = ":" + resolvedFullKey;
                                                    foreach (var kvp in ConstantRegistry.ProjectConstants)
                                                    {
                                                        if (kvp.Key.EndsWith(resSuffixWithDot, StringComparison.OrdinalIgnoreCase) ||
                                                            kvp.Key.EndsWith(resSuffixWithColon, StringComparison.OrdinalIgnoreCase) ||
                                                            kvp.Key.Equals(resolvedFullKey, StringComparison.OrdinalIgnoreCase))
                                                        {
                                                            return kvp.Value;
                                                        }
                                                    }
                                                    foreach (var kvp in ConstantRegistry.GlobalConstants)
                                                    {
                                                        if (kvp.Key.EndsWith(resSuffixWithDot, StringComparison.OrdinalIgnoreCase) ||
                                                            kvp.Key.Equals(resolvedFullKey, StringComparison.OrdinalIgnoreCase))
                                                        {
                                                            return kvp.Value;
                                                        }
                                                    }
                                                }
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
        finally
        {
            visitedVars.Remove(varName);
        }
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
                    var parentTypeOrVal = ResolveTemplateExpression(node, parentExpr);
                    if (!string.IsNullOrEmpty(parentTypeOrVal))
                    {
                        var leafVal = FindClassFieldInitializerInAst(node, $"{parentTypeOrVal}.{leafProp}");
                        if (string.IsNullOrEmpty(leafVal)) leafVal = FindClassFieldInitializerInAst(node, leafProp);
                        if (!string.IsNullOrEmpty(leafVal))
                        {
                            if (leafVal.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                leafVal.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
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
                // Third pass: member / identifier with non-empty resolution
                foreach (var part in parts)
                {
                    if (part.Contains('.') && !part.StartsWith("process.env", StringComparison.OrdinalIgnoreCase) && !part.StartsWith("env.", StringComparison.OrdinalIgnoreCase) && !part.StartsWith("env?.", StringComparison.OrdinalIgnoreCase))
                    {
                        var resolved = ResolveTemplateExpression(node, part, depth + 1);
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
                foreach (var part in parts)
                {
                    var resolved = ResolveTemplateExpression(node, part, depth + 1);
                    if (!string.IsNullOrEmpty(resolved)) return resolved;
                }
            }
        }

        // 1b. ConstantRegistry lookup: TELEGRAM_BOT_API_BASE_URL, BQ_ROUTES_CALC_API_HOST, ID_HELPER_NEST, etc.
        if (ConstantRegistry.TryResolve(null, expr, out var constVal) && !string.IsNullOrEmpty(constVal))
        {
            return constVal;
        }

        if (expr.StartsWith("this.", StringComparison.OrdinalIgnoreCase) &&
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
        if (expr.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ||
            (expr.Contains('.') && !expr.Contains('(') && !expr.StartsWith("process.env", StringComparison.OrdinalIgnoreCase) && !expr.StartsWith("env.", StringComparison.OrdinalIgnoreCase) && !expr.StartsWith("env?.", StringComparison.OrdinalIgnoreCase)))
        {
            var propName = expr.Split('.').Last().Trim();
            var classVal = FindClassFieldInitializerInAst(node, propName);
            if (!string.IsNullOrEmpty(classVal)) return classVal;
            if (expr.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
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
        string? targetClassName = null;
        var cleanProp = propName.Trim();
        if (cleanProp.Contains('.'))
        {
            var parts = cleanProp.Split('.');
            targetClassName = parts[0];
            cleanProp = parts[^1];
        }

        var curr = node;
        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.TypeScript.ClassDeclaration, TreeSitterSyntax.TypeScript.ClassBody))
            {
                var classDecl = curr.Is(TreeSitterSyntax.TypeScript.ClassDeclaration)
                    ? curr
                    : curr.Parent.Is(TreeSitterSyntax.TypeScript.ClassDeclaration) ? curr.Parent : default;
                if (!string.IsNullOrEmpty(targetClassName) && classDecl.IsValid())
                {
                    var cNameNode = classDecl.GetField(TreeSitterSyntax.Fields.Name) ??
                                    classDecl.GetChildForField(TreeSitterSyntax.Fields.Name);
                    if (cNameNode.IsValid() && !string.Equals(cNameNode.Text, targetClassName, StringComparison.OrdinalIgnoreCase))
                    {
                        curr = curr.Parent;
                        continue;
                    }
                }

                var body = curr.Is(TreeSitterSyntax.TypeScript.ClassBody) ? curr : curr.FindChildOfType(TreeSitterSyntax.TypeScript.ClassBody) ?? curr;
                foreach (var member in body.Children)
                {
                    if (member.Type.Contains("field") || member.Type.Contains("property"))
                    {
                        var nameNode = member.GetField(TreeSitterSyntax.Fields.Name) ??
                                       member.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                       member.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.PropertyIdentifier, TreeSitterSyntax.TypeScript.Identifier));
                        if (nameNode.IsValid() && string.Equals(nameNode.Text, cleanProp, StringComparison.OrdinalIgnoreCase))
                        {
                            var valNode = member.GetField(TreeSitterSyntax.Fields.Value) ??
                                          member.GetChildForField(TreeSitterSyntax.Fields.Value);

                            if (!valNode.IsValid())
                            {
                                var eq = member.Children.FirstOrDefault(c => c.Text == "=");
                                if (eq.IsValid() && eq.NextSibling.IsValid())
                                {
                                    valNode = eq.NextSibling;
                                }
                            }

                            if (valNode.IsValid())
                            {
                                if (valNode.Is(TreeSitterSyntax.TypeScript.TypeAnnotation) || valNode.Type.Contains("type") || valNode.Text.Trim().StartsWith(':'))
                                {
                                    continue;
                                }
                                if (IsStringLiteralNode(valNode))
                                {
                                    return valNode.Text.Trim('\'', '"', '`');
                                }
                                var identText = valNode.Text.Trim();
                                if (identText.StartsWith(':') || identText.Equals("Topic", StringComparison.OrdinalIgnoreCase) || identText.Equals("string", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                                if (RouteDictionaryRegistry.TryResolve(identText, out var rp, out var rs))
                                {
                                    return !string.IsNullOrEmpty(rs) ? $"{rs}{rp}" : rp;
                                }
                                var varVal = FindVariableInitializerInAst(curr, identText);
                                if (!string.IsNullOrEmpty(varVal)) return varVal;
                                return identText;
                            }

                            // If no inline initializer, check constructor for this.<cleanProp> = ...
                            var ctor = body.Children.FirstOrDefault(c => c.Is("method_definition") && (c.Text.StartsWith("constructor") || c.GetField(TreeSitterSyntax.Fields.Name)?.Text == "constructor"));
                            if (ctor.IsValid())
                            {
                                var match = Regex.Match(ctor.Text, $@"(?:this\.)?{Regex.Escape(cleanProp)}\s*=\s*([^;]+)");
                                if (match.Success)
                                {
                                    var rhs = match.Groups[1].Value.Trim();

                                    // 1. Direct string literal
                                    if ((rhs.StartsWith('\'') && rhs.EndsWith('\'')) ||
                                        (rhs.StartsWith('"') && rhs.EndsWith('"')) ||
                                        (rhs.StartsWith('`') && rhs.EndsWith('`')))
                                    {
                                        var lit = rhs.Trim('\'', '"', '`');
                                        if (!string.IsNullOrEmpty(lit) && !lit.StartsWith(':') && !lit.Equals("string", StringComparison.OrdinalIgnoreCase))
                                        {
                                            return lit;
                                        }
                                    }

                                    // 2. Chained .topic(...) call
                                    var topicMatch = Regex.Match(rhs, @"\.topic\s*\(\s*([^,\)]+)");
                                    if (topicMatch.Success)
                                    {
                                        var topicArg = topicMatch.Groups[1].Value.Trim().Trim('\'', '"', '`');
                                        var cleanTopicArg = topicArg.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ? topicArg[5..] : topicArg;
                                        if (ConstantRegistry.TryResolve(null, cleanTopicArg, out var resTopic))
                                        {
                                            return resTopic;
                                        }
                                        if (!string.IsNullOrEmpty(topicArg) && !topicArg.StartsWith(':') && !topicArg.Equals("Topic", StringComparison.OrdinalIgnoreCase))
                                        {
                                            return topicArg;
                                        }
                                    }

                                    // 3. Config call
                                    var cfgMatch = Regex.Match(rhs, @"(?:getString|get)\s*\(\s*['""]([^'""]+)['""]\s*\)");
                                    if (cfgMatch.Success)
                                    {
                                        var key = cfgMatch.Groups[1].Value.Trim();
                                        if (ConstantRegistry.TryResolve(null, key, out var resKey))
                                        {
                                            return resKey;
                                        }
                                        return key;
                                    }

                                    // 4. Template string or variable on RHS: e.g. `${BUNDLES_HOST}/api/trafficbacks` or `foo`
                                    if (rhs.StartsWith('`') && rhs.EndsWith('`'))
                                    {
                                        var rawTpl = rhs.Trim('`');
                                        var decomp = TryDecomposeTemplateString(curr, rawTpl);
                                        if (!string.IsNullOrEmpty(decomp)) return decomp;
                                    }
                                    else
                                    {
                                        var resExpr = ResolveTemplateExpression(curr, rhs);
                                        if (!string.IsNullOrEmpty(resExpr)) return resExpr;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (curr.Is(TreeSitterSyntax.TypeScript.Program))
            {
                // Search direct classes and exported classes
                var classDecls = curr.Children
                    .SelectMany(c => c.Is(TreeSitterSyntax.TypeScript.ClassDeclaration) 
                        ? [c] 
                        : (c.Is("export_statement") ? c.Children.Where(sub => sub.Is(TreeSitterSyntax.TypeScript.ClassDeclaration)) : []));

                foreach (var classDecl in classDecls)
                {
                    if (!string.IsNullOrEmpty(targetClassName))
                    {
                        var cNameNode = classDecl.GetField(TreeSitterSyntax.Fields.Name) ??
                                        classDecl.GetChildForField(TreeSitterSyntax.Fields.Name);
                        if (cNameNode.IsValid() && !string.Equals(cNameNode.Text, targetClassName, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    var body = classDecl.FindChildOfType(TreeSitterSyntax.TypeScript.ClassBody) ?? classDecl;
                    foreach (var member in body.Children)
                    {
                        if (member.Type.Contains("field") || member.Type.Contains("property"))
                        {
                            var nameNode = member.GetField(TreeSitterSyntax.Fields.Name) ??
                                           member.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                                           member.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.PropertyIdentifier, TreeSitterSyntax.TypeScript.Identifier));
                            if (nameNode.IsValid() && string.Equals(nameNode.Text, cleanProp, StringComparison.OrdinalIgnoreCase))
                            {
                                var valNode = member.GetField(TreeSitterSyntax.Fields.Value) ??
                                              member.GetChildForField(TreeSitterSyntax.Fields.Value);

                                if (!valNode.IsValid())
                                {
                                    var eq = member.Children.FirstOrDefault(c => c.Text == "=");
                                    if (eq.IsValid() && eq.NextSibling.IsValid())
                                    {
                                        valNode = eq.NextSibling;
                                    }
                                }

                                if (valNode.IsValid())
                                {
                                    if (valNode.Is(TreeSitterSyntax.TypeScript.TypeAnnotation) || valNode.Type.Contains("type") || valNode.Text.Trim().StartsWith(':'))
                                    {
                                        continue;
                                    }
                                    if (IsStringLiteralNode(valNode))
                                    {
                                        return valNode.Text.Trim('\'', '"', '`');
                                    }
                                    var identText = valNode.Text.Trim();
                                    if (identText.StartsWith(':') || identText.Equals("Topic", StringComparison.OrdinalIgnoreCase) || identText.Equals("string", StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue;
                                    }
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
                            var res = CheckMethodNode(m, cleanMethod, depth);
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
                                    var res = CheckMethodNode(m, cleanMethod, depth);
                                    if (res != null) return res;
                                }
                            }
                            var expCheck = CheckMethodNode(expChild, cleanMethod, depth);
                            if (expCheck != null) return expCheck;
                        }
                    }
                    var checkRes = CheckMethodNode(child, cleanMethod, depth);
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
                if (ch.Type == TreeSitterSyntax.TypeScript.ReturnStatement) return ch;
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

    private static bool IsNodeContainedWithin(Node inner, Node outer) => inner.IsContainedWithin(outer);
}
