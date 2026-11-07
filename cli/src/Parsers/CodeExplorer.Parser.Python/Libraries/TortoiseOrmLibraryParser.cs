using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class TortoiseOrmLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> QueryMethods = new(StringComparer.Ordinal)
    {
        "filter", "all", "get", "get_or_none", "first", "create", "bulk_create", "update", "delete", "raw"
    };

    public TortoiseOrmLibraryParser()
    {
        Name = "Tortoise ORM";
        Id = "tortoise-orm";
        Type = SemanticExtensionType.RelationalDb;
        SupportedPatterns = ["tortoise", "tortoise.*", "aerich", "aerich.*"];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Python.ClassDefinition, TreeSitterSyntax.Python.Call }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => TryExtractTortoiseTable(node, out _) ? OntologyConstants.NodeLabels.Table : null,
                ExtractIdentifier = static (node, _, _, _) => TryExtractTortoiseTable(node, out var table) ? table : null,
                CollectReferences = CollectTortoiseReferences
            }
        ];
    }

    private static void CollectTortoiseReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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
