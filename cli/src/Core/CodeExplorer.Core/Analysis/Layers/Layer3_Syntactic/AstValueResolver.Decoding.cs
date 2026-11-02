using System.Text;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Core.Parser;

public static partial class AstValueResolver
{
    public static bool TryResolveTopicOrQueue(Node? node, string? contextOrProject, out string result)
    {
        result = string.Empty;
        if (!node.IsValid()) return false;

        if (TryResolveString(node, contextOrProject, out var resolved) &&
            IsValidTopicOrQueueLiteral(resolved))
        {
            result = resolved;
            return true;
        }

        if (node is MemberAccessExprSyntax or IdentifierExprSyntax)
        {
            var propName = node is IdentifierExprSyntax
                ? node.Text
                : ((node as MemberAccessExprSyntax)?.MemberName ??
                   node.GetChildForField(SyntaxFields.Property)?.Text ??
                   node.GetChildForField(SyntaxFields.Name)?.Text ?? "");

            if (ConstantRegistry.TryDeriveTopicOrQueueFromConstantName(propName, out var derived))
            {
                result = derived;
                return true;
            }
        }

        return false;
    }

    public static bool IsValidTopicOrQueueLiteral(string? s)
    {
        return WorkspaceConventions.IsValidTopicOrQueueName(s);
    }

    public static Node? FindVariableDeclarationInScope(Node node, string varName) =>
        AstScopeResolver.FindVariableDeclarationInScope(node, varName);

    public static Node? FindClassFieldInitializer(Node node, string fieldName) =>
        AstScopeResolver.FindClassFieldInitializer(node, fieldName);

    public static bool IsQuoted(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 2) return false;
        var first = text[0];
        var last = text[^1];
        return (first == '"' && last == '"') ||
               (first == '\'' && last == '\'') ||
               (first == '`' && last == '`');
    }

    public static string Unquote(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        var trimmed = raw.Trim();

        if (trimmed.StartsWith("@\"") && trimmed.EndsWith("\"") && trimmed.Length >= 3)
        {
            return trimmed[2..^1].Replace("\"\"", "\"");
        }

        if (trimmed.StartsWith("\"\"\"") && trimmed.EndsWith("\"\"\"") && trimmed.Length >= 6)
        {
            return trimmed[3..^3].Trim('\r', '\n');
        }

        if (trimmed.StartsWith("r#\"") && trimmed.EndsWith("\"#") && trimmed.Length >= 5)
        {
            return trimmed[3..^2];
        }

        if (trimmed.StartsWith("r\"") && trimmed.EndsWith("\"") && trimmed.Length >= 3)
        {
            return trimmed[2..^1];
        }

        if (trimmed.StartsWith('`') && trimmed.EndsWith('`') && trimmed.Length >= 2)
        {
            return trimmed[1..^1].Replace("\r", "");
        }

        if (trimmed.Length >= 2 &&
            ((trimmed[0] == '"' && trimmed[^1] == '"') ||
             (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            var inner = trimmed[1..^1];
            return UnescapeQuotedString(inner);
        }

        return trimmed;
    }

    private static bool TryDecodeConstantCall(
        Node? func,
        string funcText,
        List<Node> argChildren,
        string? contextOrProject,
        int depth,
        HashSet<string>? visitedVars,
        out string result)
    {
        result = string.Empty;
        if (argChildren.Count == 0) return false;

        // 1. `.toString(...)` on a resolved Buffer/string receiver (`Buffer.from(..., 'base64').toString('utf8')`)
        if (func is MemberAccessExprSyntax funcMem)
        {
            var mName = !string.IsNullOrEmpty(funcMem.MemberName)
                ? funcMem.MemberName
                : (func.GetChildForField(SyntaxFields.Property) ?? func.GetChildForField(SyntaxFields.Field))?.Text ?? "";
            if (mName == "toString")
            {
                var recv = funcMem.Object ?? func.GetChildForField(SyntaxFields.Object) ?? func.Children.FirstOrDefault();
                if (recv.IsValid() && TryResolveStringInternal(recv, contextOrProject, depth + 1, visitedVars, out result))
                {
                    return true;
                }
            }
        }

        // 2. `atob(b64)`
        if ((funcText == "atob" || funcText.EndsWith(".atob", StringComparison.Ordinal)) && argChildren.Count >= 1)
        {
            if (TryResolveStringInternal(argChildren[0], contextOrProject, depth + 1, visitedVars, out var b64) &&
                TryDecodeBase64Utf8(b64, out result))
            {
                return true;
            }
        }

        // 3. `Buffer.from(payload, 'base64' | 'hex')`
        if (funcText is "Buffer.from" or "Buffer.alloc" && argChildren.Count >= 1)
        {
            if (TryResolveStringInternal(argChildren[0], contextOrProject, depth + 1, visitedVars, out var payload))
            {
                var encoding = "utf8";
                if (argChildren.Count >= 2 && TryResolveStringInternal(argChildren[1], contextOrProject, depth + 1, visitedVars, out var encArg))
                {
                    encoding = encArg.Trim().ToLowerInvariant();
                }

                if (encoding is "base64" or "base64url" && TryDecodeBase64Utf8(payload, out result))
                {
                    return true;
                }
                if (encoding == "hex" && TryDecodeHexUtf8(payload, out result))
                {
                    return true;
                }
            }
        }

        // 4. `String.fromCharCode(c1, c2, ...)`
        if (funcText == "String.fromCharCode" && argChildren.Count >= 1)
        {
            var sb = new System.Text.StringBuilder(argChildren.Count);
            foreach (var arg in argChildren)
            {
                var raw = arg is ArgumentSyntax { Value: { } v } ? v.Text.Trim() : arg.Text.Trim();
                if (int.TryParse(raw, out var code) && code is >= 0 and <= 0xFFFF)
                {
                    sb.Append((char)code);
                }
                else
                {
                    return false;
                }
            }
            if (sb.Length > 0)
            {
                result = sb.ToString();
                return true;
            }
        }

        return false;
    }

    private static bool TryDecodeBase64Utf8(string input, out string decoded)
    {
        decoded = string.Empty;
        var clean = input.Trim();
        if (clean.Length < 4) return false;
        try
        {
            var bytes = Convert.FromBase64String(clean);
            if (bytes.Length == 0) return false;
            var str = System.Text.Encoding.UTF8.GetString(bytes);
            if (str.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            {
                return false;
            }
            decoded = str;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryDecodeHexUtf8(string input, out string decoded)
    {
        decoded = string.Empty;
        var clean = input.Trim();
        if (clean.Length < 4 || clean.Length % 2 != 0) return false;
        try
        {
            var bytes = Convert.FromHexString(clean);
            if (bytes.Length == 0) return false;
            var str = System.Text.Encoding.UTF8.GetString(bytes);
            if (str.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            {
                return false;
            }
            decoded = str;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool ContainsObfuscatedHexOrUnicodeEscapes(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.IndexOf('\\') < 0) return false;
        var count = 0;
        for (var i = 0; i < raw.Length - 3; i++)
        {
            if (raw[i] == '\\' && (raw[i + 1] == 'x' || raw[i + 1] == 'u'))
            {
                count++;
                if (count >= 2) return true;
            }
        }
        return false;
    }

    public static string UnescapeQuotedString(string inner)
    {
        if (string.IsNullOrEmpty(inner) || inner.IndexOf('\\') < 0)
        {
            return inner;
        }

        var sb = new System.Text.StringBuilder(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            if (c == '\\' && i + 1 < inner.Length)
            {
                var next = inner[i + 1];
                switch (next)
                {
                    case 'n': sb.Append('\n'); i++; break;
                    case 'r': sb.Append('\r'); i++; break;
                    case 't': sb.Append('\t'); i++; break;
                    case '"': sb.Append('"'); i++; break;
                    case '\'': sb.Append('\''); i++; break;
                    case '\\': sb.Append('\\'); i++; break;
                    case '0': sb.Append('\0'); i++; break;
                    case 'a': sb.Append('\a'); i++; break;
                    case 'b': sb.Append('\b'); i++; break;
                    case 'f': sb.Append('\f'); i++; break;
                    case 'v': sb.Append('\v'); i++; break;
                    case 'u' when i + 5 < inner.Length && int.TryParse(inner.AsSpan(i + 2, 4), System.Globalization.NumberStyles.HexNumber, null, out var u):
                        sb.Append((char)u);
                        i += 5;
                        break;
                    case 'x' when i + 3 < inner.Length && byte.TryParse(inner.AsSpan(i + 2, 2), System.Globalization.NumberStyles.HexNumber, null, out var b):
                        sb.Append((char)b);
                        i += 3;
                        break;
                    default:
                        sb.Append('\\');
                        sb.Append(next);
                        i++;
                        break;
                }
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
