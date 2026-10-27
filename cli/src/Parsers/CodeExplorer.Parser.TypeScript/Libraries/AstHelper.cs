using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public static partial class AstHelper
{
    private const int MaxRecursionDepth = 12;

    public static bool IsValidTopicOrQueueLiteral(string? s)
    {
        return WorkspaceConventions.IsValidTopicOrQueueName(s);
    }

    public static string? ResolveTopicOrQueue(Node? argNode, string? contextOrProject = null, int depth = 0, HashSet<string>? visitedVars = null)
    {
        if (depth > MaxRecursionDepth || !argNode.IsValid()) return null;

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

                if (ConstantRegistry.TryDeriveParentTopicFromSubscriptionKey(envKey, out var parentTopic))
                {
                    return parentTopic;
                }

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
            var memberVal = ResolveStringOrTemplate(argNode, contextOrProject, depth + 1, visitedVars);
            if (!string.IsNullOrEmpty(memberVal) && IsValidTopicOrQueueLiteral(memberVal))
            {
                if (WorkspaceConventions.TryGetTopicAlias(memberVal, out var mapped)) return mapped;
                return memberVal;
            }

            if (argNode.Text.Contains('.'))
            {
                var fullText = argNode.Text;
                var lastDot = fullText.LastIndexOf('.');
                var objPart = fullText[..lastDot];
                var leafProp = fullText[(lastDot + 1)..];

                if (objPart.Contains("config", StringComparison.OrdinalIgnoreCase) ||
                    objPart.Contains("settings", StringComparison.OrdinalIgnoreCase) ||
                    objPart.Contains("options", StringComparison.OrdinalIgnoreCase) ||
                    objPart.Contains("broker", StringComparison.OrdinalIgnoreCase) ||
                    objPart.Contains("pubsub", StringComparison.OrdinalIgnoreCase) ||
                    objPart.Contains("env", StringComparison.OrdinalIgnoreCase))
                {
                    if (ConstantRegistry.TryDeriveParentTopicFromSubscriptionKey(leafProp, out var leafParent))
                    {
                        return leafParent;
                    }

                    if (ConstantRegistry.TryResolve(contextOrProject, leafProp, out var leafVal) &&
                        !string.IsNullOrEmpty(leafVal) && IsValidTopicOrQueueLiteral(leafVal))
                    {
                        if (WorkspaceConventions.TryGetTopicAlias(leafVal, out var mapped)) return mapped;
                        return leafVal;
                    }

                    if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(leafProp, out var derVal))
                    {
                        return derVal;
                    }
                }
            }

            return null;
        }

        // 3. CallExpression (configService.getString('RULE_TREE_TOPIC'), getTopic('orders'))
        if (argNode.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var firstArg = ExtractFirstStringArgument(argNode, contextOrProject, depth + 1, visitedVars);
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

            var callVal = ResolveStringOrTemplate(argNode, contextOrProject, depth + 1, visitedVars);
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
            var astVal = FindVariableInitializerInAst(argNode, varName, depth + 1, visitedVars, contextOrProject);
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
                return ResolveTopicOrQueue(tp, contextOrProject, depth + 1, visitedVars);
            }
        }

        return null;
    }

    public static string? ExtractFirstStringArgument(Node? callNode, string? contextOrProject = null, int depth = 0, HashSet<string>? visitedVars = null)
    {
        if (depth > MaxRecursionDepth || !callNode.IsValid()) return null;

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
                return ResolveStringOrTemplate(firstArgExpr, contextOrProject, depth + 1, visitedVars);
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

    public static bool IsStringLiteralNode(Node? node)
    {
        if (node is null || !node.IsValid()) return false;
        return node is LiteralExprSyntax { LiteralKind: LiteralSyntaxKind.String or LiteralSyntaxKind.RawString } ||
               node.IsAny(TreeSitterSyntax.TypeScript.String,
                           TreeSitterSyntax.TypeScript.TemplateString,
                           TreeSitterSyntax.Common.StringLiteral);
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

    public static string? FindVariableInitializerInAst(Node node, string varName, string? contextOrProject = null)
    {
        return FindVariableInitializerInAst(node, varName, 0, null, contextOrProject);
    }

    public static string? FindVariableInitializerInAst(Node node, string varName, int depth, HashSet<string>? visitedVars, string? contextOrProject = null)
    {
        if (depth > MaxRecursionDepth) return null;
        visitedVars ??= new HashSet<string>(StringComparer.Ordinal);
        if (!visitedVars.Add(varName)) return null;

        try
        {
            var cleanVar = varName.StartWithIC("this.") ? varName[5..].Trim() : varName;
            var curr = node.Parent;
            var maxScopeSteps = 50;
            var steps = 0;
            while (curr.IsValid() && ++steps <= maxScopeSteps)
            {
                if (curr.Is("class_body") || curr.Is("class_declaration") || curr.Type.Contains("class"))
                {
                    var fieldVal = FindClassFieldInitializerInAst(curr, cleanVar, depth + 1, visitedVars);
                    if (fieldVal != null) return fieldVal;
                }

                if (curr.IsAny(TreeSitterSyntax.TypeScript.StatementBlock, TreeSitterSyntax.TypeScript.Program))
                {
                    foreach (var child in curr.Children)
                    {
                        var targetNode = child;
                        if (targetNode.Is("export_statement"))
                        {
                            var decl = targetNode.GetChildForField("declaration");
                            if (decl.IsValid())
                            {
                                targetNode = decl;
                            }
                            else
                            {
                                targetNode = targetNode.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration));
                            }
                        }

                        if (targetNode.IsValid() && targetNode.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration))
                        {
                            foreach (var decl in targetNode.Children.Where(c => c.Is(TreeSitterSyntax.TypeScript.VariableDeclarator)))
                            {
                                var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name);
                                if (!nameNode.IsValid())
                                {
                                    nameNode = decl.FindChildOfType("object_pattern") ??
                                               decl.Children.FirstOrDefault(c => c.Is("object_pattern"));
                                }
                                if (nameNode.IsValid())
                                {
                                    var valNode = decl.GetField(TreeSitterSyntax.Fields.Value) ?? decl.GetChildForField(TreeSitterSyntax.Fields.Value);
                                    if (!valNode.IsValid() && decl.Children.Count >= 3 && decl.Children[1].Type == "=")
                                    {
                                        valNode = decl.Children[2];
                                    }

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
                                                var funcNode = valNode.GetFunctionNode();
                                                if (funcNode.IsValid())
                                                {
                                                    var propNode = funcNode.Is(TreeSitterSyntax.TypeScript.MemberExpression)
                                                        ? funcNode.GetField(TreeSitterSyntax.Fields.Property)
                                                        : default;
                                                    var funcName = propNode.IsValid() ? propNode.Text : funcNode.Text;
                                                    var methodRet = FindMethodReturnInAst(valNode, funcName, depth, visitedVars);
                                                    if (!string.IsNullOrEmpty(methodRet)) return methodRet;
                                                }

                                                var callRes = ResolveStringOrTemplate(valNode, contextOrProject, depth + 1, visitedVars);
                                                if (!string.IsNullOrEmpty(callRes))
                                                {
                                                    return callRes;
                                                }
                                            }
                                            else if (valNode.Is(TreeSitterSyntax.Common.BinaryExpression))
                                            {
                                                var left = valNode.GetField(TreeSitterSyntax.Fields.Left);
                                                var right = valNode.GetField(TreeSitterSyntax.Fields.Right);
                                                var leftRes = left.IsValid() ? ResolveStringOrTemplate(left, contextOrProject, depth + 1, visitedVars) : null;
                                                if (!string.IsNullOrEmpty(leftRes) && (leftRes.StartsWith("http") || leftRes.Contains('.'))) return leftRes;
                                                var rightRes = right.IsValid() ? ResolveStringOrTemplate(right, contextOrProject, depth + 1, visitedVars) : null;
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
                                                patChild.Is("shorthand_property_identifier_pattern") ||
                                                patChild.Type.Contains("shorthand") ||
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

                                                if (ConstantRegistry.TryResolve(contextOrProject, fullLookupKey, out var resolvedVal) && !string.IsNullOrEmpty(resolvedVal))
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
                                                var resolvedRhs = ResolveStringOrTemplate(valNode, contextOrProject, depth + 1, visitedVars);
                                                if (!string.IsNullOrEmpty(resolvedRhs))
                                                {
                                                    var resolvedFullKey = $"{resolvedRhs}.{propAlias}";
                                                    if (ConstantRegistry.TryResolve(contextOrProject, resolvedFullKey, out var valFromResolved) && !string.IsNullOrEmpty(valFromResolved))
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

    private static string? FindClassFieldInitializerInAst(Node node, string propName, int depth = 0, HashSet<string>? visitedVars = null)
    {
        if (depth > MaxRecursionDepth || string.IsNullOrWhiteSpace(propName)) return null;

        string? targetClassName = null;
        var cleanProp = propName.Trim();
        if (cleanProp.Contains('.'))
        {
            var parts = cleanProp.Split('.');
            targetClassName = parts[0];
            cleanProp = parts[^1];
        }
        if (string.IsNullOrWhiteSpace(cleanProp)) return null;

        visitedVars ??= new HashSet<string>(StringComparer.Ordinal);
        var visitKey = $"field:{targetClassName}:{cleanProp}";
        if (!visitedVars.Add(visitKey)) return null;

        try
        {
            var assignPattern = $@"(?<![A-Za-z0-9_$])this\.{Regex.Escape(cleanProp)}\b\s*(?<![=!<>])=(?!=)\s*([^;\r\n]+)";
            var curr = node;
            var steps = 0;
            while (curr.IsValid() && ++steps <= 50)
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

                                if (valNode is not null &&
                                    valNode.IsValid() &&
                                    !valNode.Is(TreeSitterSyntax.TypeScript.TypeAnnotation) &&
                                    !valNode.Type.Contains("type") &&
                                    !valNode.Text.Trim().StartsWith(':'))
                                {
                                    if (IsStringLiteralNode(valNode))
                                    {
                                        var rawLit = valNode.Text.Trim('\'', '"', '`');
                                        if (rawLit.Contains("${"))
                                        {
                                            var decomp = TryDecomposeTemplateString(curr, rawLit, depth + 1, visitedVars);
                                            if (!string.IsNullOrEmpty(decomp)) return decomp;
                                        }
                                        return rawLit;
                                    }
                                    var identText = valNode.Text.Trim();
                                    if (!identText.StartsWith(':') && !identText.Equals("Topic", StringComparison.OrdinalIgnoreCase) && !identText.Equals("string", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (RouteDictionaryRegistry.TryResolve(identText, out var rp, out var rs))
                                        {
                                            return !string.IsNullOrEmpty(rs) ? $"{rs}{rp}" : rp;
                                        }
                                        var varVal = FindVariableInitializerInAst(curr, identText, depth + 1, visitedVars);
                                        if (!string.IsNullOrEmpty(varVal)) return varVal;
                                        return identText;
                                    }
                                }

                                // If no inline initializer, check constructor for this.<cleanProp> = ...
                                var ctor = body.Children.FirstOrDefault(c => (c.Is("method_definition") || c.Is("constructor") || c.Type.Contains("constructor")) &&
                                                                            (c.Text.StartsWith("constructor") || c.GetField(TreeSitterSyntax.Fields.Name)?.Text == "constructor" || c.Type.Contains("constructor")));
                                if (ctor.IsValid())
                                {
                                    var match = Regex.Match(ctor.Text, assignPattern);
                                    if (match.Success)
                                    {
                                        var res = EvaluateRhs(curr, match.Groups[1].Value.Trim(), depth + 1, visitedVars);
                                        if (!string.IsNullOrEmpty(res)) return res;
                                    }
                                }
                            }
                        }
                    }

                    // Fallback: check class body text for this.<cleanProp> = ... assignment in constructor or methods
                    if (body.EndPosition.Row - body.StartPosition.Row <= 500)
                    {
                        var bodyAssignMatch = Regex.Match(body.Text, assignPattern);
                        if (bodyAssignMatch.Success)
                        {
                            var res = EvaluateRhs(curr, bodyAssignMatch.Groups[1].Value.Trim(), depth + 1, visitedVars);
                            if (!string.IsNullOrEmpty(res)) return res;
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
                                            var rawLit = valNode.Text.Trim('\'', '"', '`');
                                            if (rawLit.Contains("${"))
                                            {
                                                var decomp = TryDecomposeTemplateString(curr, rawLit, depth + 1, visitedVars);
                                                if (!string.IsNullOrEmpty(decomp)) return decomp;
                                            }
                                            return rawLit;
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
                                        var varVal = FindVariableInitializerInAst(curr, identText, depth + 1, visitedVars);
                                        if (!string.IsNullOrEmpty(varVal)) return varVal;
                                        return identText;
                                    }
                                }
                            }
                        }

                        if (body.EndPosition.Row - body.StartPosition.Row <= 500)
                        {
                            var bodyAssignMatch = Regex.Match(body.Text, assignPattern);
                            if (bodyAssignMatch.Success)
                            {
                                var res = EvaluateRhs(curr, bodyAssignMatch.Groups[1].Value.Trim(), depth + 1, visitedVars);
                                if (!string.IsNullOrEmpty(res)) return res;
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
            visitedVars.Remove(visitKey);
        }
    }

    private static string? EvaluateRhs(Node curr, string rhs, int depth = 0, HashSet<string>? visitedVars = null)
    {
        if (depth > MaxRecursionDepth || string.IsNullOrWhiteSpace(rhs)) return null;

        // 1. Template string on RHS: e.g. `${BUNDLES_HOST}/api/trafficbacks`
        if (rhs.StartsWith('`') && rhs.EndsWith('`'))
        {
            var rawTpl = rhs.Trim('`');
            if (rawTpl.Contains("${"))
            {
                var decomp = TryDecomposeTemplateString(curr, rawTpl, depth + 1, visitedVars);
                if (!string.IsNullOrEmpty(decomp)) return decomp;
            }
            if (!string.IsNullOrEmpty(rawTpl) && !rawTpl.StartsWith(':') && !rawTpl.Equals("string", StringComparison.OrdinalIgnoreCase))
            {
                return rawTpl;
            }
            return null;
        }

        // 1b. Direct string literal
        if ((rhs.StartsWith('\'') && rhs.EndsWith('\'')) ||
            (rhs.StartsWith('"') && rhs.EndsWith('"')))
        {
            var lit = rhs.Trim('\'', '"');
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
            var cleanTopicArg = topicArg.StartWithIC("this.") ? topicArg[5..] : topicArg;
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

        // 4. General expression on RHS
        var resExpr = ResolveTemplateExpression(curr, rhs, depth + 1, visitedVars);
        if (!string.IsNullOrEmpty(resExpr)) return resExpr;

        return null;
    }

    private static bool IsNodeContainedWithin(Node inner, Node outer) => inner.IsContainedWithin(outer);
}
