using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class NextJsLibraryParser : DeclarativeSemanticExtension
{
    private const string FallbackRoute = "route";
    private const string FallbackHandler = "handler";

    private static readonly HashSet<string> HttpMethodFunctions = new(StringComparer.Ordinal)
    {
        "GET", "POST", "PUT", "DELETE", "PATCH", "HEAD", "OPTIONS"
    };

    public NextJsLibraryParser()
    {
        Name = "Next.js";
        Id = "nextjs";
        Type = SemanticExtensionType.Framework;
        SupportedPatterns = ["next", "next/*"];
        IsBuiltIn = true;
        LibraryRole = LibraryRole.FrontendFramework;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.TypeScript.FunctionDeclaration }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsNextRouteHandler(node, node.Document?.RelativePath) ? OntologyConstants.NodeLabels.EntryPoint : null,
                ExtractIdentifier = static (node, _, _, rel) =>
                {
                    var path = !string.IsNullOrEmpty(rel) ? rel : node.Document?.RelativePath;
                    if (!IsNextRouteHandler(node, path)) return null;
                    var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
                    var funcName = nameNode.IsValid() ? nameNode.Text : "";
                    if (HttpMethodFunctions.Contains(funcName))
                    {
                        var route = ExtractNextRouteFromPath(path, FallbackRoute);
                        return $"{funcName}:{route}";
                    }
                    if (string.Equals(funcName, FallbackHandler, StringComparison.OrdinalIgnoreCase))
                    {
                        var route = ExtractNextRouteFromPath(path, FallbackHandler);
                        return $"API:{route}";
                    }
                    return $"{funcName}:{ExtractNextRouteFromPath(path, FallbackRoute)}";
                }
            }
        ];
    }

    private static bool IsNextRouteHandler(Node node, string? relativePath)
    {
        // Only match the function_declaration itself (not the enclosing export_statement)
        if (!node.Is(TreeSitterSyntax.TypeScript.FunctionDeclaration)) return false;

        var nameNode = node.GetField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid()) return false;

        var funcName = nameNode.Text;
        var isHttpMethod = HttpMethodFunctions.Contains(funcName);
        var isHandler = string.Equals(funcName, "handler", StringComparison.OrdinalIgnoreCase);
        if (!isHttpMethod && !isHandler) return false;

        if (IsNextRouteFilePath(relativePath))
        {
            return true;
        }

        return HasNextImport(node);
    }

    private static bool IsNextRouteFilePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        var norm = relativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(norm);
        if (fileName.StartsWith("route.", StringComparison.OrdinalIgnoreCase) &&
            ( norm.StartsWith("app/", StringComparison.OrdinalIgnoreCase) ||
              norm.Contains("/app/", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return norm.StartsWith("pages/api/", StringComparison.OrdinalIgnoreCase) ||
               norm.Contains("/pages/api/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasNextImport(Node node)
    {
        var doc = node.Document;
        if (doc?.SourceText != null)
        {
            return doc.SourceText.Contains("'next/", StringComparison.Ordinal) ||
                   doc.SourceText.Contains("\"next/", StringComparison.Ordinal) ||
                   doc.SourceText.Contains("'next'", StringComparison.Ordinal) ||
                   doc.SourceText.Contains("\"next\"", StringComparison.Ordinal);
        }
        return false;
    }

    private static string ExtractNextRouteFromPath(string? relativePath, string fallback)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return fallback;
        var norm = relativePath.Replace('\\', '/').Trim('/');

        // 1. Next.js App Router: .../app/<segments>/route.(ts|tsx|js|jsx|mjs|cjs)
        var appIdx = norm.StartsWith("app/", StringComparison.OrdinalIgnoreCase)
            ? 0
            : norm.IndexOf("/app/", StringComparison.OrdinalIgnoreCase);
        if (appIdx >= 0)
        {
            var afterApp = norm.StartsWith("app/", StringComparison.OrdinalIgnoreCase)
                ? norm["app/".Length..]
                : norm[(appIdx + "/app/".Length)..];
            var lastSlash = afterApp.LastIndexOf('/');
            var fileName = lastSlash >= 0 ? afterApp[(lastSlash + 1)..] : afterApp;
            if (fileName.StartsWith("route.", StringComparison.OrdinalIgnoreCase))
            {
                var routeDir = lastSlash >= 0 ? afterApp[..lastSlash] : "";
                var segments = routeDir
                    .Split('/', StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => !(s.StartsWith('(') && s.EndsWith(')')) && !s.StartsWith('@'))
                    .ToArray();
                return segments.Length == 0 ? "/" : "/" + string.Join('/', segments);
            }
        }

        // 2. Next.js Pages Router: .../pages/api/<segments>.(ts|tsx|js|jsx)
        var pagesApiIdx = norm.StartsWith("pages/api/", StringComparison.OrdinalIgnoreCase)
            ? 0
            : norm.IndexOf("/pages/api/", StringComparison.OrdinalIgnoreCase);
        if (pagesApiIdx >= 0)
        {
            var afterPages = norm.StartsWith("pages/", StringComparison.OrdinalIgnoreCase)
                ? norm["pages/".Length..]
                : norm[(pagesApiIdx + "/pages/".Length)..];
            var ext = Path.GetExtension(afterPages);
            if (!string.IsNullOrEmpty(ext))
            {
                afterPages = afterPages[..^ext.Length];
            }
            if (afterPages.EndsWith("/index", StringComparison.OrdinalIgnoreCase))
            {
                afterPages = afterPages[..^"/index".Length];
            }
            return "/" + afterPages.Trim('/');
        }

        return fallback;
    }
}
