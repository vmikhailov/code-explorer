using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Java.Libraries;

public class JpaLibraryParser : ILibraryParser
{
    public string Type => "db:relational";
    public string Name => "JPA / Hibernate / Spring Data";
    public string Id => "jpa";

    public IReadOnlyList<string> SupportedPatterns =>
    [
        "org.springframework.data.jpa",
        "org.springframework.data.repository",
        "jakarta.persistence",
        "javax.persistence",
        "org.hibernate"
    ];

    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsJpaQuery(node))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsJpaQuery(node))
        {
            var sqlText = ExtractSqlText(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                var clean = NestedSqlParser.CleanQueryText(sqlText);
                if (NestedSqlParser.TryParseSql(sqlText, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"JPA Query: {clean}";
            }
            return "JPA Query";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsJpaQuery(node))
        {
            var sqlText = ExtractSqlText(node);
            if (!string.IsNullOrEmpty(sqlText))
            {
                NestedSqlParser.TryDetectSqlDependencies(sqlText, scopeSymbolId, references);
            }
        }
    }

    public static bool IsJpaQuery(Node node)
    {
        // Case 1: @Query annotation on method
        if (node.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
        {
            var nameNode = node.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                           ?? node.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);
            if (nameNode.IsValid())
            {
                var name = nameNode.Text;
                if (name.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // Case 2: entityManager.createQuery(...) / entityManager.createNativeQuery(...)
        if (node.Is(TreeSitterSyntax.Java.MethodInvocation))
        {
            var nameNode = node.GetChildFieldText(TreeSitterSyntax.Fields.Name);
            if (nameNode is "createQuery" or "createNativeQuery" or "createNamedQuery")
            {
                return true;
            }
        }

        return false;
    }

    public static string? ExtractSqlText(Node node)
    {
        if (node.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
        {
            // Direct string literal e.g. @Query("SELECT u FROM User u")
            var strNode = node.FindChildOfType(TreeSitterSyntax.Java.StringLiteral)
                          ?? node.FindChildOfType(TreeSitterSyntax.Java.TextBlock);
            if (strNode.IsValid())
            {
                return strNode.Text.Trim('"').Trim();
            }

            // value = "..."
            foreach (var pair in node.Children)
            {
                if (pair.Is(TreeSitterSyntax.Java.ElementValuePair))
                {
                    var keyNode = pair.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                    if (keyNode.IsValid() && keyNode.Text is "value" or "nativeQuery")
                    {
                        var valStr = pair.FindChildOfType(TreeSitterSyntax.Java.StringLiteral)
                                     ?? pair.FindChildOfType(TreeSitterSyntax.Java.TextBlock);
                        if (valStr.IsValid())
                        {
                            return valStr.Text.Trim('"').Trim();
                        }
                    }
                }
            }
        }

        if (node.Is(TreeSitterSyntax.Java.MethodInvocation))
        {
            var argList = node.FindChildOfType(TreeSitterSyntax.Java.ArgumentList);
            if (argList.IsValid() && argList.Children.Count > 0)
            {
                var firstArg = argList.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.Java.StringLiteral, TreeSitterSyntax.Java.TextBlock));
                if (firstArg.IsValid())
                {
                    return firstArg.Text.Trim('"').Trim();
                }
            }
        }

        return null;
    }
}
