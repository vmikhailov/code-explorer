using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class MongooseLibraryParser : DeclarativeSemanticExtension
{
    public override SemanticExtensionType Type => SemanticExtensionType.DocumentDb;

    public override string Name => "MongoDB";

    public override string Id => "mongoose";

    public override IReadOnlyList<string> SupportedPatterns => ["mongoose"];

    public override LibraryRole LibraryRole => LibraryRole.OrmOrDatabase;

    private static readonly IReadOnlyList<CustomAstRule> Rules =
    [
        new CustomAstRule
        {
            NodeTypes = [TreeSitterSyntax.TypeScript.CallExpression],
            MapNodeType = static (node, ctx) => IsMongooseCall(node) ? OntologyConstants.NodeLabels.Query : null,
            ExtractIdentifier = static (node, ctx, _, _) => ExtractIdentifierCore(node, ctx)
        }
    ];

    public override IReadOnlyList<CustomAstRule> CustomAstRules => Rules;

    private static string? ExtractIdentifierCore(Node node, ParsingContext ctx)
    {
        if (IsMongooseCall(node))
        {
            var func = node.GetFunctionNode();
            if (func.IsValid())
            {
                if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
                {
                    var obj = func.GetField(TreeSitterSyntax.Fields.Object);
                    var prop = func.GetField(TreeSitterSyntax.Fields.Property);
                    if (obj.IsValid() && prop.IsValid())
                    {
                        var objName = obj.Text;
                        var propName = prop.Text;
                        if (objName == "mongoose" && propName == "model")
                        {
                            var modelName = ExtractFirstStringArgument(node);
                            return $"Mongoose Model: {modelName}";
                        }
                        return $"Mongoose: {objName}.{propName}";
                    }
                }
                else if (func.Is(TreeSitterSyntax.TypeScript.Identifier) && func.Text == "model")
                {
                    var modelName = ExtractFirstStringArgument(node);
                    return $"Mongoose Model: {modelName}";
                }
            }
            return "Mongoose Query";
        }
        return null;
    }

    private static bool IsMongooseCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.TypeScript.MemberExpression))
        {
            var obj = func.GetField(TreeSitterSyntax.Fields.Object);
            var prop = func.GetField(TreeSitterSyntax.Fields.Property);
            if (obj.IsValid() && prop.IsValid())
            {
                var objName = obj.Text;
                var propName = prop.Text;

                if (objName == "mongoose" && propName == "model")
                {
                    return true;
                }

                return propName is "find" or "findOne" or "findById" or "findOneAndUpdate"
                                   or "findOneAndDelete" or "create" or "save" or "updateOne"
                                   or "updateMany" or "deleteOne" or "deleteMany" or "countDocuments";
            }
        }
        else if (func.Is(TreeSitterSyntax.TypeScript.Identifier))
        {
            return func.Text == "model";
        }
        return false;
    }

    private static string? ExtractFirstStringArgument(Node node)
    {
        var args = node.FindChildOfType(TreeSitterSyntax.TypeScript.Arguments);
        if (args.IsValid())
        {
            var firstArg = args.Children.FirstOrDefault(c => c.IsAny(TreeSitterSyntax.TypeScript.String, TreeSitterSyntax.TypeScript.TemplateString));
            if (firstArg.IsValid())
            {
                return firstArg.Text.Trim('\'', '"', '`');
            }
        }
        return null;
    }
}
