using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Template and macro argument resolver for formatted string patterns (Rust format!, Go fmt.Sprintf, etc.).
/// </summary>
public static class AstTemplateResolver
{
    public static List<(Node PrimaryNode, string JoinedText)> ExtractMacroArguments(Node tokenTree)
    {
        var result = new List<(Node PrimaryNode, string JoinedText)>();
        int depth = 0;
        var currentTokens = new List<Node>();

        for (int i = 0; i < tokenTree.Children.Count; i++)
        {
            var child = tokenTree.Children[i];
            if (!child.IsValid()) continue;
            var txt = child.Text;

            if (i == 0 && (txt is "(" or "{" or "[")) continue;
            if (i == tokenTree.Children.Count - 1 && (txt is ")" or "}" or "]")) continue;

            if (txt is "(" or "{" or "[")
            {
                depth++;
                currentTokens.Add(child);
            }
            else if (txt is ")" or "}" or "]")
            {
                depth--;
                currentTokens.Add(child);
            }
            else if (txt == "," && depth == 0)
            {
                if (currentTokens.Count > 0)
                {
                    var joined = string.Concat(currentTokens.Select(t => t.Text)).Trim();
                    result.Add((currentTokens[0], joined));
                    currentTokens.Clear();
                }
                continue;
            }
            else
            {
                currentTokens.Add(child);
            }
        }

        if (currentTokens.Count > 0)
        {
            var joined = string.Concat(currentTokens.Select(t => t.Text)).Trim();
            result.Add((currentTokens[0], joined));
        }

        return result;
    }

    public static bool ResolveRustFormatTemplate(
        string fmtStr,
        IReadOnlyList<(Node PrimaryNode, string JoinedText)> argGroups,
        string? contextOrProject,
        int depth,
        HashSet<string>? visitedVars,
        out string result)
    {
        result = string.Empty;
        if (string.IsNullOrEmpty(fmtStr)) return false;

        bool anyResolved = false;
        var argIndex = 0;
        var res = Regex.Replace(fmtStr, @"\{\s*\}", m =>
        {
            if (argIndex < argGroups.Count)
            {
                var (arg, joinedText) = argGroups[argIndex++];
                var cleanText = joinedText.Trim().TrimStart('&');
                if (IsRuntimeDeploymentOrInstanceArg(cleanText))
                {
                    return "";
                }
                if (AstValueResolver.LocalBindings != null && AstValueResolver.LocalBindings.TryGetValue(cleanText, out var bVal))
                {
                    if (!string.IsNullOrEmpty(bVal)) anyResolved = true;
                    return bVal;
                }
                if (cleanText.Contains('.'))
                {
                    if (ConstantRegistry.TryResolve(contextOrProject, cleanText, out var dotVal) && !string.IsNullOrEmpty(dotVal))
                    {
                        anyResolved = true;
                        return dotVal;
                    }
                    var fieldPart = cleanText[(cleanText.LastIndexOf('.') + 1)..].TrimEnd('(', ')');
                    if (ConstantRegistry.TryResolve(contextOrProject, $"self.{fieldPart}", out var selfFieldVal) && !string.IsNullOrEmpty(selfFieldVal))
                    {
                        anyResolved = true;
                        return selfFieldVal;
                    }
                    if ((fieldPart.Contains("schema", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(fieldPart, @"^[A-Z0-9_]{3,}$")) &&
                        ConstantRegistry.TryResolve(contextOrProject, fieldPart, out var fpVal) && !string.IsNullOrEmpty(fpVal))
                    {
                        anyResolved = true;
                        return fpVal;
                    }
                    return "";
                }
                if (AstValueResolver.TryResolveStringInternal(arg, contextOrProject, depth + 1, visitedVars, out var val))
                {
                    if (!string.IsNullOrEmpty(val)) anyResolved = true;
                    return val;
                }
                if (!AstScopeResolver.IsEnclosingFunctionParameter(arg, cleanText) && !AstScopeResolver.FindVariableDeclarationInScope(arg, cleanText).IsValid())
                {
                    if (ConstantRegistry.TryResolve(contextOrProject, cleanText, out var cVal) && !string.IsNullOrEmpty(cVal))
                    {
                        anyResolved = true;
                        return cVal;
                    }
                    if (ConstantRegistry.TryResolve(contextOrProject, $"self.{cleanText}", out var selfVal) && !string.IsNullOrEmpty(selfVal))
                    {
                        anyResolved = true;
                        return selfVal;
                    }
                }
            }
            return "";
        });

        if (anyResolved)
        {
            res = Regex.Replace(res, @"_{2,}", "_");
            res = res.TrimEnd('_', '-');
            if (!string.IsNullOrWhiteSpace(res))
            {
                result = res;
                return true;
            }
        }

        return false;
    }

    public static bool ResolveFormatTemplate(
        string fmtStr,
        IReadOnlyList<Node> argNodes,
        string? contextOrProject,
        int depth,
        HashSet<string>? visitedVars,
        bool isRustFormat,
        out string result)
    {
        result = string.Empty;
        if (string.IsNullOrEmpty(fmtStr)) return false;

        bool anyResolved = false;
        string res;

        if (isRustFormat)
        {
            var argIndex = 0;
            res = Regex.Replace(fmtStr, @"\{\s*\}", m =>
            {
                if (argIndex < argNodes.Count)
                {
                    var arg = argNodes[argIndex++];
                    var cleanText = arg.Text.Trim().TrimStart('&');
                    if (AstValueResolver.LocalBindings != null && AstValueResolver.LocalBindings.TryGetValue(cleanText, out var bVal))
                    {
                        if (!string.IsNullOrEmpty(bVal)) anyResolved = true;
                        return bVal;
                    }
                    if (AstValueResolver.TryResolveStringInternal(arg, contextOrProject, depth + 1, visitedVars, out var val))
                    {
                        if (!string.IsNullOrEmpty(val)) anyResolved = true;
                        return val;
                    }
                    if (!AstScopeResolver.IsEnclosingFunctionParameter(arg, cleanText) && !AstScopeResolver.FindVariableDeclarationInScope(arg, cleanText).IsValid())
                    {
                        if (ConstantRegistry.TryResolve(contextOrProject, cleanText, out var cVal) && !string.IsNullOrEmpty(cVal))
                        {
                            anyResolved = true;
                            return cVal;
                        }
                        if (ConstantRegistry.TryResolve(contextOrProject, $"self.{cleanText}", out var selfVal) && !string.IsNullOrEmpty(selfVal))
                        {
                            anyResolved = true;
                            return selfVal;
                        }
                    }
                }
                return "";
            });
        }
        else
        {
            var argIndex = 0;
            res = Regex.Replace(fmtStr, @"%[-+0-9]*[a-zA-Z]", m =>
            {
                if (argIndex < argNodes.Count)
                {
                    var arg = argNodes[argIndex++];
                    var cleanText = arg.Text.Trim();
                    if (IsRuntimeDeploymentOrInstanceArg(cleanText))
                    {
                        return "";
                    }
                    if (AstValueResolver.TryResolveStringInternal(arg, contextOrProject, depth + 1, visitedVars, out var val) && !string.IsNullOrEmpty(val))
                    {
                        anyResolved = true;
                        return val;
                    }
                    if (ConstantRegistry.TryResolve(contextOrProject, cleanText, out var cVal) && !string.IsNullOrEmpty(cVal))
                    {
                        anyResolved = true;
                        return cVal;
                    }
                }
                return "";
            });
        }

        if (anyResolved)
        {
            res = Regex.Replace(res, @"_{2,}", "_");
            res = res.TrimEnd('_', '-');
            if (!string.IsNullOrWhiteSpace(res))
            {
                result = res;
                return true;
            }
        }

        return false;
    }

    public static bool IsRuntimeDeploymentOrInstanceArg(string argText)
    {
        if (string.IsNullOrWhiteSpace(argText)) return false;
        var leaf = argText.Contains('.')
            ? argText[(argText.LastIndexOf('.') + 1)..]
            : argText;
        leaf = leaf.Trim().TrimStart('&').TrimEnd('(', ')');
        return leaf.Equals("environment", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals("env", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals("party_id", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals("partyid", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals("party_idx", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals("node_id", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals("nodeid", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals("instance_id", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals("replica_id", StringComparison.OrdinalIgnoreCase) ||
               leaf.Equals("shard_id", StringComparison.OrdinalIgnoreCase);
    }
}
