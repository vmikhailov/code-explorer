using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class MotorMongoLibraryParser : ISemanticExtension
{
    private const string MongoTarget = "mongodb";

    public string Name => "Motor (AsyncIO MongoDB)";
    public string Id => "motor-mongo";
    public SemanticExtensionType Type => SemanticExtensionType.DocumentDb;
    public IReadOnlyList<string> SupportedPatterns => ["motor", "motor.*", "beanie", "beanie.*"];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly HashSet<string> MongoMethods = new(StringComparer.Ordinal)
    {
        "find", "find_one", "insert_one", "insert_many", "update_one", "update_many",
        "delete_one", "delete_many", "aggregate", "count_documents", "find_one_and_update"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return IsMotorCall(node, out _) ? OntologyConstants.NodeLabels.Query : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return IsMotorCall(node, out var method) ? $"MongoDB: {method}" : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (IsMotorCall(node, out _))
        {
            references.Add(new Reference(scopeSymbolId, MongoTarget, OntologyConstants.Relationships.UsesDb));
        }
    }

    private static bool IsMotorCall(Node node, out string methodName)
    {
        methodName = string.Empty;
        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;

        if (PythonAstHelper.TryGetMemberAccess(node, out _, out var method) &&
            !string.IsNullOrEmpty(method) &&
            MongoMethods.Contains(method))
        {
            methodName = method;
            return true;
        }

        return false;
    }
}
