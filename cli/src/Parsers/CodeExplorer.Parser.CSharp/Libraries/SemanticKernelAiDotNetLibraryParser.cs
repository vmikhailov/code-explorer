using System.Collections.Frozen;

namespace CodeExplorer.Parser.CSharp.Libraries;

public class SemanticKernelAiDotNetLibraryParser : DeclarativeSemanticExtension
{
    public SemanticKernelAiDotNetLibraryParser()
    {
        Id = "semantic-kernel-ai";
        Name = "Semantic Kernel & Microsoft.Extensions.AI";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["Microsoft.SemanticKernel*", "Microsoft.Extensions.AI*"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[]
                {
                    "InvokePromptAsync",
                    "InvokeAsync",
                    "GetChatMessageContentsAsync",
                    "GetResponseAsync",
                    "CompleteAsync"
                }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "llm:semantic-kernel"
            }
        ];
    }
}
