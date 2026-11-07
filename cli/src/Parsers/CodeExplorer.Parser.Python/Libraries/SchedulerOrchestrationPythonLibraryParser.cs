using System.Collections.Frozen;

namespace CodeExplorer.Parser.Python.Libraries;

public class SchedulerOrchestrationPythonLibraryParser : DeclarativeSemanticExtension
{
    public SchedulerOrchestrationPythonLibraryParser()
    {
        Id = "python-scheduler-orchestration";
        Name = "APScheduler, Prefect & Apache Airflow";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns = ["apscheduler*", "prefect*", "airflow*"];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "",
                PublishMethodNames = new[] { "add_job", "submit" }.ToFrozenSet(StringComparer.Ordinal),
                StaticPublishTopic = "scheduled-job"
            }
        ];
    }
}
