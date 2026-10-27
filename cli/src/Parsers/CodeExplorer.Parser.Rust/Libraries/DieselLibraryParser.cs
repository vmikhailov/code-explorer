using System.Text.RegularExpressions;
using CodeExplorer.Common;
using CodeExplorer.Core.Parser;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Rust.Libraries;

/// <summary>
/// Semantic extension for the Diesel ORM in Rust.
/// Detects schema declarations via table! { ... } macros and #[diesel(table_name = ...)] attributes.
/// Emits Table nodes and USES_DB relationships.
/// </summary>
public class DieselLibraryParser : ISemanticExtension
{
    public string Id => "diesel";
    public string Name => "Diesel";
    public string Type => "orm";
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;
    public IReadOnlyList<string> SupportedPatterns => ["diesel*"];

    private static readonly Regex DieselTableAttrRegex = new(
        @"table_name\s*=\s*""?([a-zA-Z0-9_]+)""?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsDieselTable(node, out _))
        {
            return OntologyConstants.NodeLabels.Table;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsDieselTable(node, out var tableName))
        {
            return tableName;
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
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
        if (node.Type == TreeSitterSyntax.Rust.MacroInvocation)
        {
            if (RustAstHelper.GetMethodCallInfo(node, out _, out var macroName, out var args))
            {
                if (macroName == "table!")
                {
                    if (args.Count > 0)
                    {
                        var firstChild = args[0];
                        if (firstChild.Type == TreeSitterSyntax.Rust.Identifier)
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
        if (node.Type == TreeSitterSyntax.Rust.AttributeItem || node.Type == TreeSitterSyntax.Rust.StructItem)
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
