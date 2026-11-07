using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class TortoiseOrmLibraryParser : ISemanticExtension
{
    public string Name => "Tortoise ORM";
    public string Id => "tortoise-orm";
    public SemanticExtensionType Type => SemanticExtensionType.RelationalDb;
    public IReadOnlyList<string> SupportedPatterns => ["tortoise", "tortoise.*", "aerich", "aerich.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> QueryMethods = new(StringComparer.Ordinal)
    {
        "filter", "all", "get", "get_or_none", "first", "create", "bulk_create", "update", "delete", "raw"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractTortoiseTable(node, out _) ? OntologyConstants.NodeLabels.Table : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractTortoiseTable(node, out var table) ? table : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryExtractTortoiseTable(node, out var tableName))
        {
            references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.PersistedIn));
            return;
        }

        if (!node.Is(TreeSitterSyntax.Python.Call)) return;

        if (PythonAstHelper.TryGetMemberAccess(node, out var objNode, out var methodName) &&
            !string.IsNullOrEmpty(methodName) &&
            QueryMethods.Contains(methodName) &&
            objNode.IsValid())
        {
            var modelName = objNode.Text.Split('.')[0].Trim();
            if (!string.IsNullOrEmpty(modelName) && char.IsUpper(modelName[0]))
            {
                references.Add(new Reference(scopeSymbolId, modelName.ToLowerInvariant(), OntologyConstants.Relationships.UsesDb));
            }
        }
    }

    private static bool TryExtractTortoiseTable(Node node, out string tableName)
    {
        tableName = string.Empty;
        if (!node.Is(TreeSitterSyntax.Python.ClassDefinition)) return false;

        var argList = node.FindChildOfType(TreeSitterSyntax.Python.ArgumentList);
        if (!argList.IsValid() || !argList.Text.Contains("Model", StringComparison.Ordinal)) return false;

        var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name);
        if (!nameNode.IsValid() || string.IsNullOrEmpty(nameNode.Text)) return false;

        tableName = nameNode.Text.ToLowerInvariant() + "s";
        return true;
    }
}
