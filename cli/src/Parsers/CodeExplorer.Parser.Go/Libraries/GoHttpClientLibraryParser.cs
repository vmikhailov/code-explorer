using System.Collections.Frozen;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go.Libraries;

public class GoHttpClientLibraryParser : DeclarativeSemanticExtension
{
    private const string DefaultGoogleTranslateUrl = "https://translate.google.com";
    private const string UnknownServiceTarget = "http:unknown-service";

    public GoHttpClientLibraryParser()
    {
        Type = SemanticExtensionType.ApiClient;
        Name = "Go HTTP Client";
        Id = "net-http";
        SupportedPatterns = ["net/http*", "*googletrans*", "*translator*"];
        LibraryRole = LibraryRole.General;
        CustomAstRules =
        [
            new CustomAstRule
            {
                NodeTypes = new[] { TreeSitterSyntax.Go.CallExpression }.ToFrozenSet(),
                MapNodeType = static (node, ctx) => IsHttpClientCall(node) ? OntologyConstants.NodeLabels.ExternalService : null,
                ExtractIdentifier = static (node, _, _, _) => IsHttpClientCall(node) ? ExtractHttpClientTarget(node) : null
            }
        ];
    }

    public static bool IsHttpClientCall(Node node)
    {
        if (!node.Is(TreeSitterSyntax.Go.CallExpression)) return false;
        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        if (func.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (operand.IsValid() && field.IsValid())
            {
                var objName = operand.Text;
                var methodName = field.Text;

                if (objName == "http")
                {
                    return methodName is "Get" or "Post" or "Head" or "PostForm" or "NewRequest" or "NewRequestWithContext";
                }

                if (objName.Contains("header", StringComparison.OrdinalIgnoreCase) ||
                    objName.Contains("query", StringComparison.OrdinalIgnoreCase) ||
                    objName.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
                    objName.Contains("context", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var args = GoAstHelper.GetCallArguments(node);
                if (args.Count >= 2 && methodName is "Get" or "Post" or "Put" or "Delete" or "Patch")
                {
                    var lastArg = args[^1];
                    if (lastArg.Is(TreeSitterSyntax.Go.FuncLiteral) ||
                        lastArg.Text.Contains("Handler", StringComparison.OrdinalIgnoreCase) ||
                        lastArg.Text.Contains("Func", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                var isClientObj = objName.EndsWith("Client", StringComparison.OrdinalIgnoreCase) ||
                                  objName.EndsWith("client", StringComparison.OrdinalIgnoreCase) ||
                                  objName.Contains("httpClient", StringComparison.OrdinalIgnoreCase) ||
                                  objName is "client" or "cli" or "hc" or "trans" or "translator" or "webapi";

                if (isClientObj)
                {
                    return methodName is "Get" or "Post" or "Put" or "Delete" or "Patch" or "Head" or "PostForm" or "Do" or "Translate";
                }
            }
        }
        return false;
    }

    public static string ExtractHttpClientTarget(Node node)
    {
        var func = node.GetFunctionNode();
        var methodName = "";
        var operandName = "";
        if (func.IsValid() && func.Is(TreeSitterSyntax.Go.SelectorExpression))
        {
            var field = func.GetChildForField(TreeSitterSyntax.Fields.Field);
            if (field.IsValid()) methodName = field.Text;
            var operand = func.GetChildForField(TreeSitterSyntax.Fields.Operand);
            if (operand.IsValid()) operandName = operand.Text;
        }

        if (operandName is "trans" or "translator" || methodName.Equals("Translate", StringComparison.OrdinalIgnoreCase))
        {
            if (RouteDictionaryRegistry.TryResolve("translator", out var rPath, out var rSvc))
            {
                return !string.IsNullOrEmpty(rSvc) ? $"{rSvc}{rPath}" : rPath;
            }
            return DefaultGoogleTranslateUrl;
        }

        var args = GoAstHelper.GetCallArguments(node);
        if (args.Count > 0)
        {
            int targetArgIndex = 0;
            if (methodName == "NewRequest" && args.Count > 1)
            {
                targetArgIndex = 1;
            }
            else if (methodName == "NewRequestWithContext" && args.Count > 2)
            {
                targetArgIndex = 2;
            }

            if (targetArgIndex < args.Count)
            {
                var resolved = GoAstHelper.ResolveStringOrVariable(args[targetArgIndex]);
                if (!string.IsNullOrEmpty(resolved)) return resolved;
            }

            foreach (var arg in args)
            {
                var resolved = GoAstHelper.ResolveStringOrVariable(arg);
                if (!string.IsNullOrEmpty(resolved) && (resolved.Contains('/') || resolved.StartWithIC("http")))
                {
                    return resolved;
                }
            }
        }

        return UnknownServiceTarget;
    }
}
