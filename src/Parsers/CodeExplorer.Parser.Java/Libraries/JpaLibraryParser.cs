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
        if (IsJpaTableAnnotation(node))
        {
            return OntologyConstants.NodeLabels.Table;
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
        if (IsJpaTableAnnotation(node))
        {
            return ExtractTableName(node);
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
        else if (IsJpaTableAnnotation(node))
        {
            var tableName = ExtractTableName(node);
            if (!string.IsNullOrEmpty(tableName))
            {
                references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.PersistedIn));
            }
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (node.Is(TreeSitterSyntax.Java.ClassDeclaration))
        {
            var annotations = GetClassAnnotations(node);
            var isEntity = annotations.Any(a => GetAnnotationName(a) is "Entity" or "Table");
            if (isEntity)
            {
                var tableAnnot = annotations.FirstOrDefault(a => GetAnnotationName(a) == "Table");
                var tableName = tableAnnot.IsValid() ? ExtractTableName(tableAnnot) : symbol.Name;
                if (!string.IsNullOrEmpty(tableName))
                {
                    symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.PersistedIn));
                }
            }
        }
    }

    public static bool IsJpaTableAnnotation(Node node)
    {
        if (!node.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation)) return false;
        var name = GetAnnotationName(node);
        return name is "Table" or "Entity";
    }

    private static List<Node> GetClassAnnotations(Node classDecl)
    {
        var result = new List<Node>();
        foreach (var child in classDecl.Children)
        {
            if (child.IsAny(TreeSitterSyntax.Java.Annotation, TreeSitterSyntax.Java.MarkerAnnotation))
            {
                result.Add(child);
            }
            else if (child.Is(TreeSitterSyntax.Java.Modifiers))
            {
                foreach (var modChild in child.Children)
                {
                    if (modChild.IsAny(TreeSitterSyntax.Java.Annotation, TreeSitterSyntax.Java.MarkerAnnotation))
                    {
                        result.Add(modChild);
                    }
                }
            }
        }
        return result;
    }

    private static string? GetAnnotationName(Node annot)
    {
        var nameNode = annot.FindChildOfType(TreeSitterSyntax.Java.Identifier)
                       ?? annot.FindChildOfType(TreeSitterSyntax.Java.ScopedIdentifier);
        if (!nameNode.IsValid()) return null;
        var name = nameNode.Text;
        if (name.Contains('.')) name = name.Substring(name.LastIndexOf('.') + 1);
        return name;
    }

    public static string? ExtractTableName(Node tableAnnot)
    {
        foreach (var child in tableAnnot.Children)
        {
            if (child.Is(TreeSitterSyntax.Java.ElementValuePair))
            {
                var key = child.FindChildOfType(TreeSitterSyntax.Java.Identifier);
                if (key.IsValid() && (key.Text is "name" or "value"))
                {
                    var val = child.FindChildOfType(TreeSitterSyntax.Java.StringLiteral);
                    if (val.IsValid()) return val.Text.Trim('"');
                }
            }
            else if (child.Is(TreeSitterSyntax.Java.StringLiteral))
            {
                return child.Text.Trim('"');
            }
        }
        var text = tableAnnot.Text;
        if (text.Contains('"'))
        {
            var f = text.IndexOf('"');
            var l = text.LastIndexOf('"');
            if (l > f) return text.Substring(f + 1, l - f - 1);
        }
        return null;
    }

    public static bool IsJpaQuery(Node node)
    {
        // Case 1: @Query annotation on method
        if (node.IsAny(TreeSitterSyntax.Java.MarkerAnnotation, TreeSitterSyntax.Java.Annotation))
        {
            var name = GetAnnotationName(node);
            if (name != null && name.EndsWith("Query", StringComparison.OrdinalIgnoreCase))
            {
                return true;
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
            var strNode = node.FindChildOfType(TreeSitterSyntax.Java.StringLiteral)
                          ?? node.FindChildOfType(TreeSitterSyntax.Java.TextBlock);
            if (strNode.IsValid())
            {
                return strNode.Text.Trim('"').Trim();
            }

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
