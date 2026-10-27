using CodeExplorer.Common;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Parser;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class VsCodeLibraryParser : ISemanticExtension
{
    public string Id => "vscode";
    public string Name => "VS Code Extension API";
    public string Type => "framework";
    public LibraryRole LibraryRole => LibraryRole.FrontendFramework;
    public IReadOnlyList<string> SupportedPatterns => ["vscode", "@types/vscode"];
    public bool IsImplemented => true;

    public string? MapNodeType(Node node, ParsingContext ctx) => null;

    public string? ExtractIdentifier(Node node, ParsingContext ctx) => null;

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return;

        var callText = func.Text;
        if (string.IsNullOrEmpty(callText)) return;

        var line = (int)node.StartPosition.Row + 1;
        var col = (int)node.StartPosition.Column + 1;

        // 1. asWebviewUri(uri)
        if (callText.Equals("asWebviewUri", StringComparison.Ordinal) ||
            callText.EndsWith(".asWebviewUri", StringComparison.Ordinal))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0)
            {
                var assetPath = ExtractAssetPathFromNode(args[0]);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    AddInstantiatesReference(scopeSymbolId, assetPath, line, col, references);
                    return;
                }
            }
        }

        // 2. vscode.Uri.joinPath(extensionUri, 'dist', 'webview.js') or Uri.joinPath(...)
        if (callText.Equals("joinPath", StringComparison.Ordinal) ||
            callText.EndsWith(".joinPath", StringComparison.Ordinal))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count >= 2)
            {
                var stringParts = new List<string>();
                foreach (var arg in args)
                {
                    if (arg.Is(TreeSitterSyntax.TypeScript.String))
                    {
                        var s = arg.Text.Trim('\'', '"', '`');
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            stringParts.Add(s);
                        }
                    }
                }

                if (stringParts.Count > 0)
                {
                    var joined = string.Join('/', stringParts);
                    if (joined.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                        joined.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase) ||
                        joined.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                        joined.Contains("webview", StringComparison.OrdinalIgnoreCase))
                    {
                        AddInstantiatesReference(scopeSymbolId, joined, line, col, references);
                        return;
                    }
                }
            }
        }

        // 3. vscode.window.registerWebviewViewProvider(viewId, provider)
        if (callText.Equals("registerWebviewViewProvider", StringComparison.Ordinal) ||
            callText.EndsWith(".registerWebviewViewProvider", StringComparison.Ordinal))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].Is(TreeSitterSyntax.TypeScript.String))
            {
                var viewId = args[0].Text.Trim('\'', '"', '`');
                if (!string.IsNullOrWhiteSpace(viewId))
                {
                    AddInstantiatesReference(scopeSymbolId, viewId, line, col, references);
                }
            }
        }

        // 4. vscode.window.createWebviewPanel(viewType, title, ...)
        if (callText.Equals("createWebviewPanel", StringComparison.Ordinal) ||
            callText.EndsWith(".createWebviewPanel", StringComparison.Ordinal))
        {
            var args = AstHelper.GetCallArguments(node);
            if (args.Count > 0 && args[0].Is(TreeSitterSyntax.TypeScript.String))
            {
                var viewType = args[0].Text.Trim('\'', '"', '`');
                if (!string.IsNullOrWhiteSpace(viewType))
                {
                    AddInstantiatesReference(scopeSymbolId, viewType, line, col, references);
                }
            }
        }
    }

    private static string? ExtractAssetPathFromNode(Node node)
    {
        if (node.Is(TreeSitterSyntax.TypeScript.String))
        {
            return node.Text.Trim('\'', '"', '`');
        }

        if (node.Is(TreeSitterSyntax.TypeScript.CallExpression))
        {
            var args = AstHelper.GetCallArguments(node);
            var stringParts = new List<string>();
            foreach (var arg in args)
            {
                if (arg.Is(TreeSitterSyntax.TypeScript.String))
                {
                    var s = arg.Text.Trim('\'', '"', '`');
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        stringParts.Add(s);
                    }
                }
            }

            if (stringParts.Count > 0)
            {
                return string.Join('/', stringParts);
            }
        }

        return null;
    }

    private static void AddInstantiatesReference(string scopeSymbolId, string target, int line, int col, List<Reference> references)
    {
        if (references.Any(r => r.Kind == OntologyConstants.Relationships.Instantiates && r.TargetName == target))
        {
            return;
        }

        references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Instantiates, line, col));
    }
}

