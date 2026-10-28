using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Utility helper for inspecting Tree-sitter Rust AST nodes, resolving method call chains,
/// unwrapping async/try expressions, extracting string literals, and combining routes.
/// </summary>
public static class RustAstHelper
{
    private static readonly Regex RouteSlashesRegex = new(@"/{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Unwraps try expressions (`?`) and await expressions (`.await`).
    /// </summary>
    public static Node UnwrapTryAndAwait(Node node)
    {
        var current = node;
        while (current.IsValid())
        {
            if (current.Type == TreeSitterSyntax.Rust.TryExpression)
            {
                var inner = current.Children.FirstOrDefault(c => c.Text != "?");
                if (inner.IsValid() && inner.Id != current.Id)
                {
                    current = inner;
                    continue;
                }
            }
            else if (current.Type == TreeSitterSyntax.Rust.AwaitExpression)
            {
                var inner = current.Children.FirstOrDefault(c => c.Text != "await" && c.Text != ".");
                if (inner.IsValid() && inner.Id != current.Id)
                {
                    current = inner;
                    continue;
                }
            }
            break;
        }
        return current;
    }

    /// <summary>
    /// Unwraps try/await/generic expressions down to an underlying call_expression or macro_invocation if present.
    /// </summary>
    public static Node UnwrapCallExpression(Node node)
    {
        var unwrapped = UnwrapTryAndAwait(node);
        if (unwrapped.Type == TreeSitterSyntax.Rust.GenericFunction)
        {
            var inner = unwrapped.GetChildForField(TreeSitterSyntax.Fields.Function) ??
                        (unwrapped.Children.Count > 0 ? unwrapped.Children[0] : null);
            if (inner.IsValid())
            {
                unwrapped = inner;
            }
        }
        return unwrapped;
    }

    /// <summary>
    /// Removes string literal delimiters including regular quotes and Rust raw string literal hashes (e.g. r#"..."#).
    /// </summary>
    public static string CleanRustString(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var trimmed = text.Trim();

        if (trimmed.StartsWith("r#\"", StringComparison.Ordinal) && trimmed.EndsWith("\"#", StringComparison.Ordinal) && trimmed.Length >= 5)
        {
            return trimmed[3..^2];
        }
        if (trimmed.StartsWith("r##\"", StringComparison.Ordinal) && trimmed.EndsWith("\"##", StringComparison.Ordinal) && trimmed.Length >= 7)
        {
            return trimmed[4..^3];
        }
        if (trimmed.StartsWith('"') && trimmed.EndsWith('"') && trimmed.Length >= 2)
        {
            return trimmed[1..^1];
        }
        return trimmed;
    }

    /// <summary>
    /// Extracts a string literal or raw string literal from an AST node.
    /// Supports direct literals as well as format!("...", ...) macro invocations.
    /// </summary>
    public static string? ExtractStringLiteral(Node node)
    {
        if (!node.IsValid()) return null;

        var unwrapped = UnwrapTryAndAwait(node);

        if (unwrapped.IsAny(TreeSitterSyntax.Rust.StringLiteral, TreeSitterSyntax.Rust.RawStringLiteral))
        {
            return CleanRustString(unwrapped.Text);
        }

        if (unwrapped.Type == TreeSitterSyntax.Rust.MacroInvocation)
        {
            var args = GetCallArguments(unwrapped);
            if (args.Count > 0)
            {
                var first = args[0];
                if (first.IsAny(TreeSitterSyntax.Rust.StringLiteral, TreeSitterSyntax.Rust.RawStringLiteral))
                {
                    return CleanRustString(first.Text);
                }
            }
        }

        if (unwrapped.Type == TreeSitterSyntax.Rust.CallExpression)
        {
            var args = GetCallArguments(unwrapped);
            if (args.Count > 0)
            {
                var first = args[0];
                if (first.IsAny(TreeSitterSyntax.Rust.StringLiteral, TreeSitterSyntax.Rust.RawStringLiteral))
                {
                    return CleanRustString(first.Text);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts call information: receiver node, method/callee name, and argument nodes.
    /// </summary>
    public static bool GetMethodCallInfo(
        Node callNode,
        out Node? receiver,
        out string? methodName,
        out List<Node> arguments)
    {
        receiver = null;
        methodName = null;
        arguments = [];

        var targetNode = UnwrapTryAndAwait(callNode);

        if (targetNode.Type != TreeSitterSyntax.Rust.CallExpression && targetNode.Type != TreeSitterSyntax.Rust.MacroInvocation)
        {
            return false;
        }

        arguments = GetCallArguments(targetNode);

        if (targetNode.Type == TreeSitterSyntax.Rust.MacroInvocation)
        {
            var macroChild = targetNode.GetChildForField("macro") ??
                             targetNode.Children.FirstOrDefault(c => c.Type is TreeSitterSyntax.Rust.Identifier or TreeSitterSyntax.Rust.ScopedIdentifier);
            if (macroChild.IsValid())
            {
                methodName = macroChild.Text + "!";
                return true;
            }
            return false;
        }

        var func = targetNode.GetChildForField(TreeSitterSyntax.Fields.Function);
        if (!func.IsValid() && targetNode.Children.Count > 0)
        {
            func = targetNode.Children[0];
        }
        if (!func.IsValid()) return false;

        func = UnwrapTryAndAwait(func);

        if (func.Type == TreeSitterSyntax.Rust.GenericFunction)
        {
            var innerFunc = func.GetChildForField(TreeSitterSyntax.Fields.Function) ??
                            (func.Children.Count > 0 ? func.Children[0] : null);
            if (innerFunc.IsValid()) func = innerFunc;
        }

        if (func.Type == TreeSitterSyntax.Rust.FieldExpression)
        {
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field) ??
                        func.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.FieldIdentifier);
            var rec = func.GetChildForField("argument") ??
                      (func.Children.Count > 0 ? func.Children[0] : null);

            receiver = rec.IsValid() ? rec : null;
            methodName = field.IsValid() ? field.Text : null;
            return !string.IsNullOrEmpty(methodName);
        }

        if (func.Type == TreeSitterSyntax.Rust.ScopedIdentifier || func.Type == TreeSitterSyntax.Rust.Identifier)
        {
            methodName = func.Text;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets all expression arguments for a call expression or macro invocation.
    /// Excludes structural tokens like parentheses, brackets, and commas.
    /// </summary>
    public static List<Node> GetCallArguments(Node callNode)
    {
        var targetNode = UnwrapTryAndAwait(callNode);

        var argsNode = targetNode.GetChildForField(TreeSitterSyntax.Fields.Arguments) ??
                       targetNode.FindChildOfType("arguments") ??
                       targetNode.FindChildOfType("token_tree");

        if (!argsNode.IsValid())
        {
            return [];
        }

        return argsNode.Children
            .Where(c => c.IsValid() &&
                        c.Type != "(" && c.Type != ")" &&
                        c.Type != "{" && c.Type != "}" &&
                        c.Type != "[" && c.Type != "]" &&
                        c.Type != ",")
            .ToList();
    }

    /// <summary>
    /// Combines a route prefix with a route path into a normalized route starting with a slash.
    /// </summary>
    public static string CombineRoutes(string? prefix, string? route)
    {
        var cleanPrefix = (prefix ?? string.Empty).Trim().TrimEnd('/');
        var cleanRoute = (route ?? string.Empty).Trim().TrimStart('/');

        if (string.IsNullOrEmpty(cleanPrefix) && string.IsNullOrEmpty(cleanRoute))
        {
            return "/";
        }

        if (string.IsNullOrEmpty(cleanPrefix))
        {
            return "/" + cleanRoute;
        }

        if (string.IsNullOrEmpty(cleanRoute))
        {
            return cleanPrefix.StartsWith('/') ? cleanPrefix : "/" + cleanPrefix;
        }

        var combined = $"{cleanPrefix}/{cleanRoute}";
        if (!combined.StartsWith('/'))
        {
            combined = "/" + combined;
        }

        return RouteSlashesRegex.Replace(combined, "/");
    }

    /// <summary>
    /// Extracts a host or clean URL from a full URL string for external service identification.
    /// </summary>
    public static string ExtractHostOrUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl;
        var trimmed = rawUrl.Trim();

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Authority);
        }

        // Return origin up to path if possible
        var slashIdx = trimmed.IndexOf('/', StringComparison.Ordinal);
        if (slashIdx > 0 && !trimmed.StartsWith('/'))
        {
            return trimmed[..slashIdx];
        }

        return trimmed;
    }
}
