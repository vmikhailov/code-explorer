using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the SeaORM entity library in Rust.
/// Detects structs annotated with DeriveEntityModel and #[sea_orm(table_name = "...")].
/// Emits Table nodes and PERSISTED_IN / USES_DB relationships.
/// </summary>
public class SeaOrmLibraryParser : DeclarativeSemanticExtension
{
    private static readonly Regex TableNameRegex = new(
        @"table_name\s*=\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public SeaOrmLibraryParser()
    {
        Id = "sea-orm";
        Name = "SeaORM";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["sea-orm*", "sea_orm*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet(
                [
                    TreeSitterSyntax.Rust.StructItem,
                    TreeSitterSyntax.Rust.AttributeItem
                ]),
                MapNodeType = (node, ctx) => IsSeaOrmEntity(node, out _) ? OntologyConstants.NodeLabels.Table : null,
                ExtractIdentifier = (node, _, _, _) => IsSeaOrmEntity(node, out var tableName) ? tableName : null,
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsSeaOrmEntity(node, out var tableName) && !string.IsNullOrEmpty(tableName))
        {
            references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.UsesDb));
            references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.PersistedIn));
        }
    }

    public static bool IsSeaOrmEntity(Node node, out string tableName)
    {
        tableName = string.Empty;

        // 1. Target struct_item annotated with DeriveEntityModel or sea_orm
        if (node.Is(TreeSitterSyntax.Rust.StructItem))
        {
            var text = node.Text;
            var isEntity = text.Contains("DeriveEntityModel", StringComparison.Ordinal) ||
                           text.Contains("sea_orm", StringComparison.Ordinal);

            if (isEntity)
            {
                var match = TableNameRegex.Match(text);
                if (match.Success)
                {
                    tableName = match.Groups[1].Value;
                    return true;
                }

                // Fallback to struct name
                var nameNode = node.GetChildForField(TreeSitterSyntax.Fields.Name) ??
                               node.Children.FirstOrDefault(c => c.Is(TreeSitterSyntax.Rust.TypeIdentifier));
                if (nameNode.IsValid())
                {
                    tableName = nameNode.Text.ToLowerInvariant();
                    return true;
                }
            }
        }

        // 2. Target standalone attribute_item if not inside struct_item
        if (node.Is(TreeSitterSyntax.Rust.AttributeItem))
        {
            if (node.Parent.IsValid() && node.Parent.Is(TreeSitterSyntax.Rust.StructItem))
            {
                return false; // Handled by struct_item
            }

            var text = node.Text;
            if (text.Contains("sea_orm", StringComparison.OrdinalIgnoreCase))
            {
                var match = TableNameRegex.Match(text);
                if (match.Success)
                {
                    tableName = match.Groups[1].Value;
                    return true;
                }
            }
        }

        return false;
    }
}
