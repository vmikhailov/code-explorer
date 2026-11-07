using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public class AiSdkTypeScriptLibraryParser : ISemanticExtension
{
    public string Name => "AI & LLM SDKs (OpenAI / Anthropic / Vercel AI / LangChain)";
    public string Id => "ai-sdk-ts";
    public SemanticExtensionType Type => SemanticExtensionType.ApiClient;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "openai",
        "openai/*",
        "@anthropic-ai/sdk*",
        "ai",
        "@ai-sdk/*",
        "@langchain/*",
        "langchain*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    private static readonly HashSet<string> VercelAiFunctions = new(StringComparer.Ordinal)
    {
        "generateText", "streamText", "generateObject", "streamObject", "embed", "embedMany"
    };

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractLlmCall(node, out _) ? OntologyConstants.NodeLabels.ExternalService : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractLlmCall(node, out var target) ? target : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryExtractLlmCall(node, out var target))
        {
            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
        }
    }

    private static bool TryExtractLlmCall(Node node, out string target)
    {
        target = string.Empty;
        if (!node.Is(TreeSitterSyntax.TypeScript.CallExpression)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var fnText = func.Text;
        if (VercelAiFunctions.Contains(fnText))
        {
            target = "llm:" + fnText;
            return true;
        }

        if (fnText.EndsWith("completions.create", StringComparison.Ordinal) ||
            fnText.EndsWith("responses.create", StringComparison.Ordinal))
        {
            target = "llm:openai";
            return true;
        }

        if (fnText.EndsWith("messages.create", StringComparison.Ordinal) ||
            fnText.EndsWith("messages.stream", StringComparison.Ordinal))
        {
            target = "llm:anthropic";
            return true;
        }

        return false;
    }
}
