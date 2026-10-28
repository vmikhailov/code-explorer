using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the SeaORM entity library in Rust.
/// Detects structs annotated with DeriveEntityModel and #[sea_orm(table_name = "...")].
/// Emits Table nodes and PERSISTED_IN / USES_DB relationships.
/// </summary>
public class SeaOrmLibraryParser : ISemanticExtension
{
    public string Id => "sea-orm";
    public string Name => "SeaORM";
    public string Type => "orm";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;
    public IReadOnlyList<string> SupportedPatterns => ["sea-orm*", "sea_orm*"];

    private static readonly Regex TableNameRegex = new(
        @"table_name\s*=\s*""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsSeaOrmEntity(node, out _))
        {
            return OntologyConstants.NodeLabels.Table;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsSeaOrmEntity(node, out var tableName))
        {
            return tableName;
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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
        if (node.Type == TreeSitterSyntax.Rust.StructItem)
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
                               node.Children.FirstOrDefault(c => c.Type == TreeSitterSyntax.Rust.TypeIdentifier);
                if (nameNode.IsValid())
                {
                    tableName = nameNode.Text.ToLowerInvariant();
                    return true;
                }
            }
        }

        // 2. Target standalone attribute_item if not inside struct_item
        if (node.Type == TreeSitterSyntax.Rust.AttributeItem)
        {
            if (node.Parent.IsValid() && node.Parent.Type == TreeSitterSyntax.Rust.StructItem)
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
