using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Python.Libraries;

public class AiSdkPythonLibraryParser : ISemanticExtension
{
    public string Name => "AI & LLM SDKs (OpenAI / Anthropic / LangChain / LlamaIndex)";
    public string Id => "ai-sdk-python";
    public SemanticExtensionType Type => SemanticExtensionType.ApiClient;
    public IReadOnlyList<string> SupportedPatterns =>
    [
        "openai",
        "openai.*",
        "anthropic",
        "anthropic.*",
        "langchain",
        "langchain*",
        "langgraph*",
        "llama_index*",
        "llama-index*"
    ];
    public bool IsImplemented => true;
    public LibraryRole LibraryRole => LibraryRole.General;

    public string? MapNodeType(Node node, ParsingContext ctx)
    {
        return TryExtractPythonLlmCall(node, out _) ? OntologyConstants.NodeLabels.ExternalService : null;
    }

    public string? ExtractIdentifier(Node node, ParsingContext ctx)
    {
        return TryExtractPythonLlmCall(node, out var target) ? target : null;
    }

    public void CollectReferences(Node node, string scopeSymbolId, List<Reference> references, ParsingContext ctx)
    {
        if (TryExtractPythonLlmCall(node, out var target))
        {
            references.Add(new Reference(scopeSymbolId, target, OntologyConstants.Relationships.Calls));
        }
    }

    private static bool TryExtractPythonLlmCall(Node node, out string target)
    {
        target = string.Empty;
        if (!node.Is(TreeSitterSyntax.Python.Call)) return false;

        var func = node.GetFunctionNode();
        if (!func.IsValid()) return false;

        var fnText = func.Text;
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

        if ((fnText.EndsWith(".invoke", StringComparison.Ordinal) ||
             fnText.EndsWith(".ainvoke", StringComparison.Ordinal)) &&
            (fnText.Contains("chain", StringComparison.OrdinalIgnoreCase) ||
             fnText.Contains("llm", StringComparison.OrdinalIgnoreCase) ||
             fnText.Contains("agent", StringComparison.OrdinalIgnoreCase)))
        {
            target = "llm:langchain";
            return true;
        }

        return false;
    }
}
