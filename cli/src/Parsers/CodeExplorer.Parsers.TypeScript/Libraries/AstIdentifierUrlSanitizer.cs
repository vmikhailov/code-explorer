using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.TypeScript.Libraries;

/// <summary>
/// Sanitizes identifier stems, non-URL GraphQL document constants, and enclosing service fallbacks
/// during HTTP/RPC target extraction without fabricating external scheme prefixes.
/// </summary>
public static class AstIdentifierUrlSanitizer
{
    private const string ExplicitServiceFilePattern = @"(?:^(?:create|make|build)-[a-z0-9\-]+-(?:fetch|client|service|driver)|[-.](?:fetch|client|service|driver|api))$";
    private const string ClassRoleSuffixPattern = @"(Driver|Repository|Service|Client|Controller|Provider|Manager|Helper|Adapter)$";
    private const string KebabBoundaryPattern = @"([a-z0-9])([A-Z])";
    private const string FileRoleAffixPattern = @"^(create|get|make|build|use)-|-?(fetch|client|service|repository|driver|helper|util|utils|api)$";
    private const string SuffixQuery = "_QUERY";
    private const string SuffixMutation = "_MUTATION";
    private const string SuffixSubscription = "_SUBSCRIPTION";
    private const string SuffixFragment = "_FRAGMENT";
    private const string SuffixDocument = "_DOCUMENT";
    private const string SuffixSchema = "_SCHEMA";

    private static readonly string[] UrlIdentifierSuffixes =
    [
        "BaseUrl", "BaseURL", "Url", "URL", "Host", "Domain", "Client", "Endpoint", "Service"
    ];

    public static bool IsNonUrlConstantIdentifier(string ident)
    {
        return ident.EndsWith(SuffixQuery, StringComparison.OrdinalIgnoreCase) ||
               ident.EndsWith(SuffixMutation, StringComparison.OrdinalIgnoreCase) ||
               ident.EndsWith(SuffixSubscription, StringComparison.OrdinalIgnoreCase) ||
               ident.EndsWith(SuffixFragment, StringComparison.OrdinalIgnoreCase) ||
               ident.EndsWith(SuffixDocument, StringComparison.OrdinalIgnoreCase) ||
               ident.EndsWith(SuffixSchema, StringComparison.OrdinalIgnoreCase);
    }

    public static string CleanIdentifierSuffix(string ident)
    {
        var leaf = ident.Contains('.') ? ident[(ident.LastIndexOf('.') + 1)..] : ident;
        for (var i = 0; i < UrlIdentifierSuffixes.Length; i++)
        {
            var suffix = UrlIdentifierSuffixes[i];
            if (leaf.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && leaf.Length > suffix.Length)
            {
                var stripped = leaf[..^suffix.Length].TrimEnd('_', '-');
                if (stripped.Length >= 2)
                {
                    return WorkspaceConventions.IsGenericUrlStemOrVariable(stripped) ? ident : stripped;
                }
            }
        }
        return ident;
    }

    public static string? ExtractEnclosingServiceFallback(Node node, string? relativePath = null)
    {
        var curr = node.Parent;
        while (curr.IsValid())
        {
            if (curr.Is(TreeSitterSyntax.TypeScript.ClassDeclaration))
            {
                var nameNode = curr.GetChildForField(TreeSitterSyntax.Fields.Name);
                var className = nameNode.IsValid() ? nameNode.Text : (curr as CodeExplorer.Core.Parser.Syntax.TypeDeclSyntax)?.Name;
                if (!string.IsNullOrEmpty(className))
                {
                    var stem = Regex.Replace(className, ClassRoleSuffixPattern, "", RegexOptions.IgnoreCase);
                    if (string.IsNullOrEmpty(stem)) stem = className;
                    var kebab = Regex.Replace(stem, KebabBoundaryPattern, "$1-$2").ToLowerInvariant();
                    if (!string.IsNullOrEmpty(kebab) && !WorkspaceConventions.IsGarbageExternalService(kebab))
                    {
                        return kebab;
                    }
                }
                break;
            }
            curr = curr.Parent;
        }

        var path = relativePath ?? node.Document?.RelativePath ?? node.Document?.FilePath;
        if (!string.IsNullOrEmpty(path))
        {
            var rawFileName = Path.GetFileNameWithoutExtension(path);
            if (Regex.IsMatch(rawFileName, ExplicitServiceFilePattern, RegexOptions.IgnoreCase))
            {
                var fileName = rawFileName;
                var dotIdx = fileName.IndexOf('.');
                if (dotIdx > 0) fileName = fileName[..dotIdx];
                var cleanStem = Regex.Replace(fileName, FileRoleAffixPattern, "", RegexOptions.IgnoreCase).Trim('-');
                if (!string.IsNullOrEmpty(cleanStem) && cleanStem.Length >= 3 && !WorkspaceConventions.IsGarbageExternalService(cleanStem))
                {
                    return cleanStem.ToLowerInvariant();
                }
            }
        }

        return null;
    }
}

