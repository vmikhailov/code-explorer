using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class FirestoreGoLibraryParser : DeclarativeSemanticExtension
{
    private const string FirestoreEngine = "Firestore";
    private const string FirestoreDbName = "firestore";

    public FirestoreGoLibraryParser()
    {
        Type = SemanticExtensionType.DocumentDb;
        Name = "Google Cloud Firestore Go";
        Id = "firestore-go";
        SupportedPatterns =
        [
            "cloud.google.com/go/firestore*",
            "cloud.google.com/go/firestore"
        ];
        LibraryRole = LibraryRole.OrmOrDatabase;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) =>
                {
                    if (IsFirestoreCollectionCall(node, out _)) return OntologyConstants.NodeLabels.Table;
                    if (IsFirestoreClientInit(node)) return OntologyConstants.NodeLabels.Database;
                    return null;
                },
                ExtractIdentifier = static (node, _, _, _) =>
                {
                    if (IsFirestoreCollectionCall(node, out var collectionName)) return collectionName;
                    if (IsFirestoreClientInit(node)) return FirestoreDbName;
                    return null;
                },
                CollectReferences = static (node, scopeSymbolId, references, ctx) =>
                {
                    if (IsFirestoreCollectionCall(node, out var collectionName) && !string.IsNullOrEmpty(collectionName))
                    {
                        references.Add(new Reference(scopeSymbolId, collectionName, OntologyConstants.Relationships.UsesDb));
                        references.Add(new Reference(scopeSymbolId, FirestoreDbName, OntologyConstants.Relationships.UsesDb));
                    }
                },
                EnrichSymbol = EnrichFirestoreSymbol
            }
        ];
    }

    private static void EnrichFirestoreSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
    {
        if (IsFirestoreCollectionCall(node, out _))
        {
            symbol.Properties["database_engine"] = FirestoreEngine;
            symbol.Properties["engine"] = FirestoreEngine;
            symbol.Properties["db_engine"] = FirestoreEngine;
            symbol.Properties["type"] = "document";
        }
        else if (IsFirestoreClientInit(node))
        {
            symbol.Properties["engine"] = FirestoreEngine;
            symbol.Properties["database_engine"] = FirestoreEngine;
            symbol.Properties["db_engine"] = FirestoreEngine;
        }
    }

    private static bool IsFirestoreCollectionCall(Node node, out string? collectionName)
    {
        collectionName = null;
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid()) return false;

        var methodName = field.Text;
        if (methodName is not ("Collection" or "CollectionGroup")) return false;

        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count == 0) return false;

        var resolved = GoAstHelper.ResolveStringOrVariable(args[0]);
        if (string.IsNullOrEmpty(resolved) || resolved.Contains('/') || resolved.StartWithIC("http")) return false;

        collectionName = resolved.Trim('"', '`');
        return !string.IsNullOrEmpty(collectionName);
    }

    private static bool IsFirestoreClientInit(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);

        if (!operand.IsValid() || !field.IsValid()) return false;

        return operand.Text == "firestore" && field.Text is "NewClient" or "NewClientWithDatabase";
    }
}
