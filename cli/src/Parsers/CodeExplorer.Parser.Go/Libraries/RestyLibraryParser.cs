using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class RestyLibraryParser : DeclarativeSemanticExtension
{
    private static readonly HashSet<string> HttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Get", "Post", "Put", "Delete", "Patch", "Head", "Options", "Execute"
    };

    public RestyLibraryParser()
    {
        Type = SemanticExtensionType.ApiClient;
        Name = "Resty";
        Id = "resty";
        SupportedPatterns = ["github.com/go-resty/resty*"];
        LibraryRole = LibraryRole.General;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsRestyCall(node, out _) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = static (node, _, _, _) => IsRestyCall(node, out var targetUrl) ? targetUrl : null
            }
        ];
    }

    public static bool IsRestyCall(Node node, out string? targetUrl)
    {
        targetUrl = null;
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid() || !func.Is(TreeSitterSyntax.Go.SelectorExpression)) return false;

        var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
        if (!field.IsValid() || !HttpMethods.Contains(field.Text)) return false;

        var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
        if (!operand.IsValid()) return false;

        var operandText = operand.Text;
        if (!operandText.Contains(".R()") &&
            !operandText.Contains(".NewRequest()") &&
            !operandText.StartsWith("resty.", StringComparison.Ordinal) &&
            !operandText.Equals("r", StringComparison.OrdinalIgnoreCase) &&
            !operandText.Equals("req", StringComparison.OrdinalIgnoreCase) &&
            !operandText.Equals("request", StringComparison.OrdinalIgnoreCase) &&
            !operandText.Contains("client", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count == 0) return false;

        var urlArgIndex = 0;
        if (field.Text.Equals("Execute", StringComparison.OrdinalIgnoreCase) && args.Count > 1)
        {
            urlArgIndex = 1;
        }

        if (urlArgIndex < args.Count)
        {
            targetUrl = GoAstHelper.ResolveStringOrVariable(args[urlArgIndex]);
        }

        if (string.IsNullOrEmpty(targetUrl))
        {
            foreach (var arg in args)
            {
                var resolved = GoAstHelper.ResolveStringOrVariable(arg);
                if (!string.IsNullOrEmpty(resolved) && (resolved.Contains('/') || resolved.StartWithIC("http")))
                {
                    targetUrl = resolved;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(targetUrl))
        {
            targetUrl = "resty:request";
        }

        return true;
    }
}
