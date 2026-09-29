using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using TreeSitter;

namespace CodeExplorer.Parser.Go.Libraries;

public class GormLibraryParser : ISemanticExtension
{
    public string Type => "db:relational";
    public string Name => "GORM";
    public string Id => "gorm";
    public IReadOnlyList<string> SupportedPatterns => ["gorm.io/gorm", "github.com/jinzhu/gorm"];
    public bool IsImplemented => true;

    private static readonly HashSet<string> GormMethods = new(StringComparer.Ordinal)
    {
        "Find", "First", "Take", "Last", "Where", "Create", "Save", "Update", "Updates", "Delete",
        "Raw", "Exec", "Table", "Model", "Select", "Joins", "Group", "Having", "Order"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsGormCall(node, out _, out _))
        {
            return OntologyConstants.NodeLabels.Query;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        if (IsGormCall(node, out var method, out var target))
        {
            if (method is "Raw" or "Exec" && !string.IsNullOrEmpty(target))
            {
                var clean = NestedSqlParser.CleanQueryText(target);
                if (NestedSqlParser.TryParseSql(target, out var firstWord, out _))
                {
                    return $"{firstWord} Query: {clean}";
                }
                return $"GORM: {clean}";
            }

            if (method is "Table" && !string.IsNullOrEmpty(target))
            {
                return $"GORM Table: {target}";
            }

            if (!string.IsNullOrEmpty(target))
            {
                return $"GORM: {target}.{method}";
            }

            return $"GORM: {method}";
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsGormCall(node, out var method, out var target))
        {
            if (method is "Raw" or "Exec" && !string.IsNullOrEmpty(target))
            {
                NestedSqlParser.TryDetectSqlDependencies(target, scopeSymbolId, references);
            }
            else if (method is "Table" && !string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.UsesDb));
            }
            else if (!string.IsNullOrEmpty(target))
            {
                references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.UsesDb));
            }
            else
            {
                references.Add(new Reference(scopeSymbolId, "database", OntologyConstants.Relationships.UsesDb));
            }
        }
    }

    private static bool IsGormCall(Node node, out string? methodName, out string? targetInfo)
    {
        methodName = null;
        targetInfo = null;

        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var funcText = func.Text;
        foreach (var m in GormMethods)
        {
            if (funcText.EndsWith("." + m, StringComparison.Ordinal) || funcText == m)
            {
                methodName = m;
                var args = GoAstHelper.GetCallArguments(node);
                if (args.Count > 0)
                {
                    if (m is "Raw" or "Exec" or "Table")
                    {
                        targetInfo = GoAstHelper.ResolveStringOrVariable(args[0]);
                    }
                    else
                    {
                        targetInfo = ExtractModelName(args[0]);
                    }
                }
                return true;
            }
        }

        return false;
    }

    private static string? ExtractModelName(Node argNode)
    {
        var text = argNode.Text.Trim('&', '*');
        if (text.Contains('{'))
        {
            var braceIdx = text.IndexOf('{');
            text = text[..braceIdx].Trim();
        }
        if (text.Contains('.'))
        {
            text = text[(text.LastIndexOf('.') + 1)..];
        }
        return !string.IsNullOrWhiteSpace(text) && char.IsLetter(text[0]) ? text : null;
    }
}
