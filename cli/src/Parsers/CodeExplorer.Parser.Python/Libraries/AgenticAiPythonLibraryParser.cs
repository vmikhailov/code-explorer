using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class AgenticAiPythonLibraryParser : DeclarativeSemanticExtension
{
    public AgenticAiPythonLibraryParser()
    {
        Id = "agentic-ai-python";
        Name = "Pydantic AI, CrewAI & AutoGen";
        Type = SemanticExtensionType.Library;
        LibraryRole = LibraryRole.General;
        SupportedPatterns = ["pydantic_ai*", "crewai*", "autogen*"];
        ServiceCallSinkRules =
        [
            new ServiceCallSinkRule
            {
                CallMethodNames = new[] { "Agent", "Crew", "AssistantAgent", "run_sync", "kickoff" }.ToFrozenSet(StringComparer.Ordinal),
                TargetRelation = OntologyConstants.Relationships.Calls,
                StaticTargetName = "llm:python-agent"
            }
        ];
    }
}
