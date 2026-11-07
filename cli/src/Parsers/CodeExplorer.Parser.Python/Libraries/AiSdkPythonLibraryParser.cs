using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public sealed class AiSdkPythonLibraryParser : DeclarativeSemanticExtension
{
    public AiSdkPythonLibraryParser()
    {
        Id = "ai-sdk-python";
        Name = "AI & LLM SDKs (OpenAI / Anthropic / LangChain / LlamaIndex)";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
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
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "create" }.ToFrozenSet(),
                ReceiverSubstrings = new[] { "completions", "responses" }.ToFrozenSet(),
                StaticTargetName = "llm:openai",
                TargetRelation = OntologyConstants.Relationships.Calls
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "create", "stream" }.ToFrozenSet(),
                ReceiverSubstrings = new[] { "messages" }.ToFrozenSet(),
                StaticTargetName = "llm:anthropic",
                TargetRelation = OntologyConstants.Relationships.Calls
            },
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "invoke", "ainvoke" }.ToFrozenSet(),
                ReceiverSubstrings = new[] { "chain", "llm", "agent" }.ToFrozenSet(),
                StaticTargetName = "llm:langchain",
                TargetRelation = OntologyConstants.Relationships.Calls
            }
        ];
    }
}
