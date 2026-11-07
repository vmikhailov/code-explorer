using System.Collections.Frozen;

namespace CodeExplorer.Parser.TypeScript.Libraries;

public sealed class AiSdkTypeScriptLibraryParser : DeclarativeSemanticExtension
{
    public AiSdkTypeScriptLibraryParser()
    {
        Id = "ai-sdk-ts";
        Name = "AI & LLM SDKs (OpenAI / Anthropic / Vercel AI / LangChain)";
        Type = SemanticExtensionType.ApiClient;
        LibraryRole = LibraryRole.General;
        SupportedPatterns =
        [
            "openai",
            "openai/*",
            "@anthropic-ai/sdk*",
            "ai",
            "@ai-sdk/*",
            "@langchain/*",
            "langchain*"
        ];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "generateText", "streamText", "generateObject", "streamObject", "embed", "embedMany"
                }.ToFrozenSet(),
                TargetPrefix = "llm:",
                UseMethodNameAsTarget = true,
                TargetRelation = OntologyConstants.Relationships.Calls
            },
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
            }
        ];
    }
}
