using System.Collections.Frozen;

namespace CodeExplorer.Parser.Java.Libraries;

public class LangChain4jSpringAiLibraryParser : DeclarativeSemanticExtension
{
    public LangChain4jSpringAiLibraryParser()
    {
        Id = "langchain4j-spring-ai";
        Name = "LangChain4j & Spring AI";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["dev.langchain4j.*", "org.springframework.ai.*"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "generate", "chat", "prompt" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "llm:java-ai"
            }
        ];
    }
}
