using System.Text.RegularExpressions;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Diesel ORM in Rust.
/// Detects schema declarations via table! { ... } macros and #[diesel(table_name = ...)] attributes.
/// Emits Table nodes and USES_DB relationships.
/// </summary>
public class DieselLibraryParser : DeclarativeSemanticExtension
{
    private static readonly Regex DieselTableAttrRegex = new(
        @"table_name\s*=\s*""?([a-zA-Z0-9_]+)""?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public DieselLibraryParser()
    {
        Id = "diesel";
        Name = "Diesel";
        Type = SemanticExtensionType.RelationalDb;
        LibraryRole = LibraryRole.OrmOrDatabase;
        SupportedPatterns = ["diesel*"];
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = System.Collections.Frozen.FrozenSet.ToFrozenSet(
                [
                    TreeSitterSyntax.Rust.MacroInvocation,
                    TreeSitterSyntax.Rust.AttributeItem,
                    TreeSitterSyntax.Rust.StructItem
                ]),
                MapNodeType = (node, ctx) => IsDieselTable(node, out _) ? OntologyConstants.NodeLabels.Table : null,
                ExtractIdentifier = (node, _, _, _) => IsDieselTable(node, out var tableName) ? tableName : null,
                CollectReferences = CollectReferencesCore
            }
        ];
    }

    private static void CollectReferencesCore(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsDieselTable(node, out var tableName) && !string.IsNullOrEmpty(tableName))
        {
            references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.UsesDb));
            references.Add(new Reference(scopeSymbolId, tableName, OntologyConstants.Relationships.PersistedIn));
        }
    }

    public static bool IsDieselTable(Node node, out string tableName)
    {
        tableName = string.Empty;

        // 1. table! { ... } macro invocation
        if (node.Is(TreeSitterSyntax.Rust.MacroInvocation))
        {
            if (RustAstHelper.GetMethodCallInfo(node, out _, out var macroName, out var args))
            {
                if (macroName == "table!")
                {
                    if (args.Count > 0)
                    {
                        var firstChild = args[0];
                        if (firstChild.Is(TreeSitterSyntax.Rust.Identifier))
                        {
                            tableName = firstChild.Text;
                            return true;
                        }
                    }

                    var text = node.Text;
                    var match = Regex.Match(text, @"table!\s*\{\s*([a-zA-Z0-9_]+)");
                    if (match.Success)
                    {
                        tableName = match.Groups[1].Value;
                        return true;
                    }
                }
            }
        }

        // 2. #[diesel(table_name = ...)] attribute
        if (node.Is(TreeSitterSyntax.Rust.AttributeItem) || node.Is(TreeSitterSyntax.Rust.StructItem))
        {
            var text = node.Text;
            if (text.Contains("diesel", StringComparison.OrdinalIgnoreCase) && text.Contains("table_name", StringComparison.OrdinalIgnoreCase))
            {
                var match = DieselTableAttrRegex.Match(text);
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
