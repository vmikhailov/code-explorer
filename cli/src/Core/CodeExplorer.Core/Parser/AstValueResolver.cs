using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using TreeSitter;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Universal AST value evaluator and expression resolver across all supported languages (C#, TypeScript, Go, Python, Java).
/// Replaces heuristic regex searches with AST-driven resolution of literals, binary concatenations,
/// template strings, configuration readers, and local reaching definitions.
/// </summary>
public static class AstValueResolver
{
    private const int MaxRecursionDepth = 15;

    public static string? ResolveString(Node? node, string? contextOrProject = null)
    {
        return TryResolveString(node, contextOrProject, out var result) ? result : null;
    }

    public static bool TryResolveString(Node? node, string? contextOrProject, out string result)
    {
        return TryResolveStringInternal(node, contextOrProject, 0, null, out result);
    }

    public static bool TryResolveExpression(Node? node, string? contextOrProject, HashSet<string>? visitedVars, out string result)
    {
        return TryResolveStringInternal(node, contextOrProject, 0, visitedVars, out result);
    }

    public static bool TryResolveExpression(Node? node, string? contextOrProject, out string result)
    {
        return TryResolveStringInternal(node, contextOrProject, 0, null, out result);
    }

    private static bool TryResolveStringInternal(
        Node? node,
        string? contextOrProject,
        int depth,
        HashSet<string>? visitedVars,
        out string result)
    {
        result = string.Empty;
        if (!node.IsValid() || depth > MaxRecursionDepth) return false;

        var type = node.Type;

        // 1. Direct String Literals (only if it does not contain interpolation substitutions)
        var hasInterpolationChildren = node.Children.Any(c => c.IsValid() && c.Type is "interpolation" or "template_substitution");
        if (!hasInterpolationChildren && (IsStringLiteralType(type) || (node.Children.Count == 0 && IsQuoted(node.Text))))
        {
            result = Unquote(node.Text);
            return true;
        }

        // 2. Numeric and Boolean Literals
        if (type is "number" or "integer" or "float" or "number_literal" or "integer_literal" or "float_literal" or
                    "true" or "false" or "boolean_literal")
        {
            result = node.Text.Trim();
            return true;
        }

        // 2b. Argument / Attribute Argument wrapper: unwrap inner expression
        if (type is "argument" or "attribute_argument")
        {
            var exprChild = node.GetChildForField(TreeSitterSyntax.Fields.Expression) ??
                            node.Children.FirstOrDefault(c => c.IsValid() && c.Type is not ":" and not "," and not "identifier");
            if (!exprChild.IsValid())
            {
                exprChild = node.Children.LastOrDefault(c => c.IsValid());
            }
            if (exprChild.IsValid() && exprChild.Id != node.Id)
            {
                return TryResolveStringInternal(exprChild, contextOrProject, depth + 1, visitedVars, out result);
            }
        }

        // 3. Parenthesized Expression: unwrap inner
        if (type is "parenthesized_expression")
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                var c = node.Children[i];
                if (c.IsValid() && c.Type is not "(" and not ")")
                {
                    return TryResolveStringInternal(c, contextOrProject, depth + 1, visitedVars, out result);
                }
            }
        }

        // 4. Binary Expressions (String Concatenation: Left + Right)
        if (type is "binary_expression" or "binary_operator")
        {
            var op = node.GetChildForField("operator")?.Text ??
                     node.Children.FirstOrDefault(c => c.Type is "+" or ".")?.Text;

            if (op == "+" || op == ".")
            {
                var left = node.GetChildForField(TreeSitterSyntax.Fields.Left);
                var right = node.GetChildForField(TreeSitterSyntax.Fields.Right);

                if (!left.IsValid() && node.Children.Count >= 2)
                {
                    left = node.Children[0];
                    right = node.Children[^1];
                }

                var leftResolved = TryResolveStringInternal(left, contextOrProject, depth + 1, visitedVars, out var leftVal);
                var rightResolved = TryResolveStringInternal(right, contextOrProject, depth + 1, visitedVars, out var rightVal);

                if (leftResolved && rightResolved)
                {
                    result = leftVal + rightVal;
                    return true;
                }
                if (leftResolved && !rightResolved && right.IsValid() && right.Type == TreeSitterSyntax.Common.Identifier)
                {
                    if (ConstantRegistry.TryResolve(contextOrProject, right.Text, out var rVal))
                    {
                        result = leftVal + rVal;
                        return true;
                    }
                }
                if (!leftResolved && rightResolved && left.IsValid() && left.Type == TreeSitterSyntax.Common.Identifier)
                {
                    if (ConstantRegistry.TryResolve(contextOrProject, left.Text, out var lVal))
                    {
                        result = lVal + rightVal;
                        return true;
                    }
                }
            }
        }

        // 5. Universal Interpolated / Template / Format String ($"...", `...`, f"...")
        if (hasInterpolationChildren ||
            type is TreeSitterSyntax.CSharp.InterpolatedStringExpression or
                    TreeSitterSyntax.CSharp.InterpolatedVerbatimStringExpression or
                    TreeSitterSyntax.CSharp.InterpolatedRawStringExpression or
                    TreeSitterSyntax.TypeScript.TemplateString or
                    "interpolated_string_expression" or "template_string" or "format_string")
        {
            var sb = new System.Text.StringBuilder();
            var allResolved = true;

            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                if (!child.IsValid()) continue;

                var cType = child.Type;
                if (cType is "$\"" or "@$\"" or "$@\"" or "\"" or "\"\"\"" or "`" or "f\"" or "f'" or "'" or "f" or "string_start" or "string_end" or "interpolation_start") continue;

                if (cType is "interpolation" or "template_substitution")
                {
                    // Find expression child inside { ... } or ${ ... }
                    var expr = child.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "{" and not "}" and not "${" and not "interpolation_brace");
                    if (expr.IsValid() && TryResolveStringInternal(expr, contextOrProject, depth + 1, visitedVars, out var part))
                    {
                        sb.Append(part);
                    }
                    else if (expr.IsValid() && ConstantRegistry.TryResolve(contextOrProject, expr.Text, out var cVal))
                    {
                        sb.Append(cVal);
                    }
                    else
                    {
                        allResolved = false;
                        sb.Append('{').Append(expr.IsValid() ? expr.Text : "").Append('}');
                    }
                }
                else if (cType is "interpolation_text" or "string_fragment" or "string_content" or "escape_sequence")
                {
                    sb.Append(child.Text);
                }
                else
                {
                    sb.Append(child.Text);
                }
            }

            result = sb.ToString();
            return allResolved;
        }

        // 7. Member Expression / Selector Expression / Attribute
        // (Constants.Topics.OrderCreated, this.topicName, _config.Topic, process.env.ORDER_TOPIC)
        if (type is TreeSitterSyntax.Common.MemberExpression or
                    TreeSitterSyntax.CSharp.MemberAccessExpression or
                    TreeSitterSyntax.Go.SelectorExpression or
                    TreeSitterSyntax.Python.Attribute or
                    TreeSitterSyntax.Java.ScopedIdentifier)
        {
            var rawText = node.Text;

            // 7a. Check direct ConstantRegistry lookup
            if (ConstantRegistry.TryResolve(contextOrProject, rawText, out var constVal))
            {
                result = constVal;
                return true;
            }

            // 7b. Check environment variables: process.env.KEY
            var envMatch = Regex.Match(rawText, @"(?:process\.env|env\??)\.([A-Za-z0-9_]+)");
            if (envMatch.Success)
            {
                var envKey = envMatch.Groups[1].Value;
                if (ConstantRegistry.TryResolve(contextOrProject, envKey, out var evVal))
                {
                    result = evVal;
                    return true;
                }
                if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(envKey, out var der))
                {
                    result = der;
                    return true;
                }
            }

            // 7c. Check this.property or self.property
            if (rawText.StartsWith("this.", StringComparison.OrdinalIgnoreCase) ||
                rawText.StartsWith("self.", StringComparison.OrdinalIgnoreCase))
            {
                var propName = rawText[5..].Trim();
                if (ConstantRegistry.TryResolve(contextOrProject, propName, out var propVal))
                {
                    result = propVal;
                    return true;
                }

                var fieldInit = FindClassFieldInitializer(node, propName);
                if (fieldInit.IsValid())
                {
                    visitedVars ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (visitedVars.Add(rawText))
                    {
                        if (TryResolveStringInternal(fieldInit, contextOrProject, depth + 1, visitedVars, out result))
                        {
                            return true;
                        }
                    }
                }
            }

            // 7d. Check rightmost property
            var propNode = node.GetChildForField(TreeSitterSyntax.Fields.Property) ??
                           node.GetChildForField(TreeSitterSyntax.Fields.Field) ??
                           node.GetChildForField(TreeSitterSyntax.Fields.Name);

            if (propNode.IsValid() && ConstantRegistry.TryResolve(contextOrProject, propNode.Text, out var pVal))
            {
                result = pVal;
                return true;
            }
        }

        // 8. Subscript / Element Access (config["Key"], _configuration["Key"], process.env["KEY"])
        if (type is "subscript_expression" or "element_access_expression" or "bracket_expression" or
                    TreeSitterSyntax.Python.Subscript or "index_expression" or "array_access")
        {
            Node? argNode = null;
            var bracketList = node.FindChildOfType("bracketed_argument_list");
            if (bracketList.IsValid())
            {
                var arg = bracketList.FindChildOfType("argument") ?? bracketList.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "[" and not "]");
                if (arg.IsValid())
                {
                    argNode = (arg.Type == "argument" && arg.Children.Count > 0) ? arg.Children[0] : arg;
                }
            }
            else
            {
                argNode = node.GetChildForField("index") ?? node.GetChildForField("subscript");
                if (!argNode.IsValid())
                {
                    argNode = node.Children.LastOrDefault(c => c.IsValid() && c.Type is not "[" and not "]" and not ")" and not "(");
                }
            }

            if (argNode.IsValid() && TryResolveStringInternal(argNode, contextOrProject, depth + 1, visitedVars, out var keyStr))
            {
                if (ConstantRegistry.TryResolve(contextOrProject, keyStr, out var cfgVal))
                {
                    result = cfgVal;
                    return true;
                }
                if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(keyStr, out var derived))
                {
                    result = derived;
                    return true;
                }
            }
        }

        // 9. Call Expression (configService.get("KEY"), os.Getenv("KEY"), etc.)
        if (type is TreeSitterSyntax.Common.CallExpression or TreeSitterSyntax.CSharp.InvocationExpression or
                    TreeSitterSyntax.Python.Call or TreeSitterSyntax.Java.MethodInvocation)
        {
            var func = node.GetFunctionNode();
            var funcText = func.IsValid() ? func.Text : "";

            var argsList = node.FindChildOfType(TreeSitterSyntax.Common.ArgumentList) ??
                           node.FindChildOfType(TreeSitterSyntax.Common.Arguments);

            var firstArg = argsList.IsValid() && argsList.Children.Count > 0
                ? argsList.Children.FirstOrDefault(c => c.IsValid() && c.Type is not "(" and not ")" and not ",")
                : null;

            if (firstArg.IsValid())
            {
                // Wrapper calls: String(x), ToString(x)
                if (funcText is "String" or "ToString")
                {
                    return TryResolveStringInternal(firstArg, contextOrProject, depth + 1, visitedVars, out result);
                }

                // Configuration readers
                if (funcText.EndsWith(".get", StringComparison.OrdinalIgnoreCase) ||
                    funcText.EndsWith(".getString", StringComparison.OrdinalIgnoreCase) ||
                    funcText.EndsWith(".GetValue", StringComparison.OrdinalIgnoreCase) ||
                    funcText.EndsWith(".GetConnectionString", StringComparison.OrdinalIgnoreCase) ||
                    funcText.Equals("os.Getenv", StringComparison.OrdinalIgnoreCase) ||
                    funcText.Equals("viper.GetString", StringComparison.OrdinalIgnoreCase) ||
                    funcText.Equals("os.getenv", StringComparison.OrdinalIgnoreCase) ||
                    funcText.Equals("environ.get", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryResolveStringInternal(firstArg, contextOrProject, depth + 1, visitedVars, out var cfgKey))
                    {
                        if (ConstantRegistry.TryResolve(contextOrProject, cfgKey, out var cfgVal))
                        {
                            result = cfgVal;
                            return true;
                        }
                        if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(cfgKey, out var der))
                        {
                            result = der;
                            return true;
                        }
                    }
                }
            }
        }

        // 10. Plain Identifier: Check ConstantRegistry first, then scope reaching definition
        if (type is TreeSitterSyntax.Common.Identifier or TreeSitterSyntax.Common.TypeIdentifier or
                    TreeSitterSyntax.CSharp.VariableName or TreeSitterSyntax.TypeScript.VariableName or
                    TreeSitterSyntax.Go.VariableName or TreeSitterSyntax.Python.VariableName)
        {
            var varName = node.Text;

            // 10a. Registry lookup
            if (ConstantRegistry.TryResolve(contextOrProject, varName, out var regVal))
            {
                result = regVal;
                return true;
            }

            // 10b. Check RouteDictionaryRegistry
            if (RouteDictionaryRegistry.TryResolve(varName, out var rPath, out var rService))
            {
                result = string.IsNullOrEmpty(rService) ? rPath : $"{rService}:{rPath}";
                return true;
            }

            // 10c. Reaching Definition: Search backwards in enclosing scope
            visitedVars ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (visitedVars.Add(varName))
            {
                var declNode = FindVariableDeclarationInScope(node, varName);
                if (declNode.IsValid())
                {
                    if (TryResolveStringInternal(declNode, contextOrProject, depth + 1, visitedVars, out result))
                    {
                        // Cache resolved value for subsequent lookups
                        ConstantRegistry.Register(contextOrProject, varName, result);
                        return true;
                    }
                }

                // Also check class field initializer
                var fieldInit = FindClassFieldInitializer(node, varName);
                if (fieldInit.IsValid())
                {
                    if (TryResolveStringInternal(fieldInit, contextOrProject, depth + 1, visitedVars, out result))
                    {
                        ConstantRegistry.Register(contextOrProject, varName, result);
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool TryResolveTopicOrQueue(Node? node, string? contextOrProject, out string result)
    {
        result = string.Empty;
        if (!node.IsValid()) return false;

        if (TryResolveString(node, contextOrProject, out var resolved) && IsValidTopicOrQueueLiteral(resolved))
        {
            if (WorkspaceConventions.TryGetTopicAlias(resolved, out var mapped))
            {
                result = mapped;
                return true;
            }
            result = resolved;
            return true;
        }

        // Fallback: check if node text matches known environment variables
        var text = node.Text;
        if (ConstantRegistry.TryDeriveTopicOrQueueFromEnvVar(text, out var derived))
        {
            result = derived;
            return true;
        }

        return false;
    }

    public static bool IsValidTopicOrQueueLiteral(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        if (t.Length < 2) return false;
        if (t.StartsWith(':')) return false;
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
        if (t.Equals("string", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("Topic", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("undefined", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("void", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("any", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("unknown", StringComparison.OrdinalIgnoreCase)) return false;
        if (WorkspaceConventions.IsPlaceholderName(t)) return false;
        return true;
    }

    public static Node? FindVariableDeclarationInScope(Node node, string varName)
    {
        var targetRow = node.StartPosition.Row;
        var curr = node.Parent;

        while (curr.IsValid())
        {
            if (IsScopeBoundary(curr.Type))
            {
                foreach (var child in curr.Children)
                {
                    if (child.StartPosition.Row > targetRow) break;

                    // C# local declaration
                    if (child.IsAny(TreeSitterSyntax.CSharp.LocalDeclarationStatement, TreeSitterSyntax.CSharp.VariableDeclaration))
                    {
                        var declarators = child.FindChildrenOfType(TreeSitterSyntax.CSharp.VariableDeclarator);
                        foreach (var decl in declarators)
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ??
                                           decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);

                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                                if (valNode.IsValid()) return valNode;

                                var eq = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                                if (eq.IsValid() && eq.Children.Count > 1)
                                {
                                    return eq.Children[1];
                                }
                            }
                        }
                    }

                    // TS / JS lexical or variable declaration
                    if (child.IsAny(TreeSitterSyntax.TypeScript.LexicalDeclaration, TreeSitterSyntax.TypeScript.VariableDeclaration))
                    {
                        var declarators = child.FindChildrenOfType(TreeSitterSyntax.TypeScript.VariableDeclarator);
                        foreach (var decl in declarators)
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ??
                                           decl.FindChildOfType(TreeSitterSyntax.TypeScript.Identifier);

                            if (nameNode.IsValid() && nameNode.Text == varName)
                            {
                                var valNode = decl.GetField(TreeSitterSyntax.Fields.Value);
                                if (valNode.IsValid()) return valNode;

                                if (decl.Children.Count >= 3 && decl.Children[1].Type == "=")
                                {
                                    return decl.Children[2];
                                }
                            }
                        }
                    }

                    // Go short_var_declaration (topic := "orders")
                    if (child.Is(TreeSitterSyntax.Go.ShortVarDeclaration))
                    {
                        var left = child.GetField(TreeSitterSyntax.Fields.Left);
                        var right = child.GetField(TreeSitterSyntax.Fields.Right);

                        if (left.IsValid() && left.Text == varName && right.IsValid())
                        {
                            return right;
                        }
                    }

                    // Python / JS Assignment Expression
                    if (child.IsAny("assignment", TreeSitterSyntax.Common.AssignmentExpression))
                    {
                        var left = child.GetField(TreeSitterSyntax.Fields.Left);
                        var right = child.GetField(TreeSitterSyntax.Fields.Right);

                        if (left.IsValid() && left.Text == varName && right.IsValid())
                        {
                            return right;
                        }
                    }
                }
            }

            curr = curr.Parent;
        }

        return null;
    }

    public static Node? FindClassFieldInitializer(Node node, string fieldName)
    {
        var cleanField = fieldName.TrimStart('_');
        var curr = node.Parent;

        while (curr.IsValid())
        {
            if (curr.IsAny(TreeSitterSyntax.CSharp.ClassDeclaration, TreeSitterSyntax.CSharp.StructDeclaration,
                           TreeSitterSyntax.CSharp.RecordDeclaration, TreeSitterSyntax.TypeScript.ClassDeclaration,
                           TreeSitterSyntax.Java.ClassDeclaration, TreeSitterSyntax.Python.ClassDefinition))
            {
                foreach (var child in curr.Children)
                {
                    // C# field or property
                    if (child.Is(TreeSitterSyntax.CSharp.FieldDeclaration))
                    {
                        var declarators = child.FindChildrenOfType(TreeSitterSyntax.CSharp.VariableDeclarator);
                        foreach (var decl in declarators)
                        {
                            var nameNode = decl.GetField(TreeSitterSyntax.Fields.Name) ??
                                           decl.FindChildOfType(TreeSitterSyntax.Common.Identifier);

                            if (nameNode.IsValid() && (nameNode.Text == fieldName || nameNode.Text.TrimStart('_') == cleanField))
                            {
                                var eq = decl.FindChildOfType(TreeSitterSyntax.CSharp.EqualsValueClause);
                                if (eq.IsValid() && eq.Children.Count > 1) return eq.Children[1];
                            }
                        }
                    }
                    if (child.Is(TreeSitterSyntax.CSharp.PropertyDeclaration))
                    {
                        var nameNode = child.GetField(TreeSitterSyntax.Fields.Name);
                        if (nameNode.IsValid() && (nameNode.Text == fieldName || nameNode.Text.TrimStart('_') == cleanField))
                        {
                            var arrow = child.FindChildOfType("arrow_expression_clause");
                            if (arrow.IsValid() && arrow.Children.Count > 1) return arrow.Children[1];
                        }
                    }

                    // TS / JS property / field definition
                    if (child.IsAny(TreeSitterSyntax.TypeScript.PropertyDefinition, TreeSitterSyntax.TypeScript.PublicFieldDefinition))
                    {
                        var nameNode = child.GetField(TreeSitterSyntax.Fields.Property) ??
                                       child.GetField(TreeSitterSyntax.Fields.Name);

                        if (nameNode.IsValid() && (nameNode.Text == fieldName || nameNode.Text.TrimStart('_') == cleanField))
                        {
                            var valNode = child.GetField(TreeSitterSyntax.Fields.Value);
                            if (valNode.IsValid()) return valNode;
                        }
                    }
                }
            }

            curr = curr.Parent;
        }

        return null;
    }

    private static bool IsScopeBoundary(string type) =>
        type is TreeSitterSyntax.CSharp.Block or
                TreeSitterSyntax.CSharp.MethodDeclaration or
                TreeSitterSyntax.CSharp.LocalFunctionStatement or
                TreeSitterSyntax.CSharp.CompilationUnit or
                TreeSitterSyntax.TypeScript.StatementBlock or
                TreeSitterSyntax.TypeScript.FunctionDeclaration or
                TreeSitterSyntax.TypeScript.ArrowFunction or
                TreeSitterSyntax.TypeScript.Program or
                TreeSitterSyntax.Go.Block or
                TreeSitterSyntax.Go.FunctionDeclaration or
                TreeSitterSyntax.Python.FunctionDefinition or
                TreeSitterSyntax.Java.Block or
                TreeSitterSyntax.Java.MethodDeclaration;

    private static bool IsStringLiteralType(string type) =>
        type is TreeSitterSyntax.Common.String or
                TreeSitterSyntax.Common.StringLiteral or
                TreeSitterSyntax.CSharp.StringLiteral or
                TreeSitterSyntax.CSharp.VerbatimStringLiteral or
                TreeSitterSyntax.Go.RawStringLiteral or
                TreeSitterSyntax.Go.InterpretedStringLiteral or
                TreeSitterSyntax.Java.StringLiteral or
                TreeSitterSyntax.Java.TextBlock;

    private static bool IsQuoted(string text)
    {
        var t = text.Trim();
        return (t.StartsWith('"') && t.EndsWith('"')) ||
               (t.StartsWith('\'') && t.EndsWith('\'')) ||
               (t.StartsWith('`') && t.EndsWith('`')) ||
               (t.StartsWith("@\"") && t.EndsWith('"')) ||
               (t.StartsWith("\"\"\"") && t.EndsWith("\"\"\""));
    }

    public static string Unquote(string raw)
    {
        var t = raw.Trim();
        if (t.StartsWith("\"\"\"") && t.EndsWith("\"\"\"") && t.Length >= 6)
        {
            return t[3..^3];
        }
        if (t.StartsWith("@\"") && t.EndsWith('"') && t.Length >= 3)
        {
            return t[2..^1].Replace("\"\"", "\"");
        }
        if ((t.StartsWith('"') && t.EndsWith('"') && t.Length >= 2) ||
            (t.StartsWith('\'') && t.EndsWith('\'') && t.Length >= 2) ||
            (t.StartsWith('`') && t.EndsWith('`') && t.Length >= 2))
        {
            var inner = t[1..^1];
            return Regex.Unescape(inner);
        }
        return t;
    }
}
