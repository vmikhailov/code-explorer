using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class FirestoreGoLibraryParser : ISemanticExtension
{
    public SemanticExtensionType Type => SemanticExtensionType.DocumentDb;
    public string Name => "Google Cloud Firestore Go";
    public string Id => "firestore-go";
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "cloud.google.com/go/firestore*",
        "cloud.google.com/go/firestore"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private const string FirestoreEngine = "Firestore";
    private const string FirestoreDbName = "firestore";

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        if (IsFirestoreCollectionCall(node, out _))
        {
            return OntologyConstants.NodeLabels.Table;
        }
        if (IsFirestoreClientInit(node))
        {
            return OntologyConstants.NodeLabels.Database;
        }
        return null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => ExtractIdentifier(node, ctx, null, null);
    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName) => ExtractIdentifier(node, ctx, projectName, null);

    public string? ExtractIdentifier(Node node, ParsingContext ctx, string? projectName, string? relativePath)
    {
        if (IsFirestoreCollectionCall(node, out var collectionName))
        {
            return collectionName;
        }
        if (IsFirestoreClientInit(node))
        {
            return FirestoreDbName;
        }
        return null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsFirestoreCollectionCall(node, out var collectionName) && !string.IsNullOrEmpty(collectionName))
        {
            references.Add(new Reference(scopeSymbolId, collectionName, OntologyConstants.Relationships.UsesDb));
            references.Add(new Reference(scopeSymbolId, FirestoreDbName, OntologyConstants.Relationships.UsesDb));
        }
    }

    public void EnrichSymbol(Node node, SyntacticSymbol symbol, ParsingContext ctx)
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
