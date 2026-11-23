using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

public class QuartzJobRunrJavaLibraryParser : DeclarativeSemanticExtension
{
    private const string ExtensionId = "quartz-jobrunr";
    private const string ExtensionName = "Quartz Scheduler & JobRunr";
    private const string ProtocolScheduler = "scheduler";
    private const string FallbackJobTopic = "scheduled-job";
    private const string JobAnnotation = "Job";
    private const string RecurringAnnotation = "Recurring";
    private const string ScheduledAnnotation = "Scheduled";
    private const string NameKey = "name";
    private const string IdKey = "id";
    private const string ValueKey = "value";

    public QuartzJobRunrJavaLibraryParser()
    {
        Id = ExtensionId;
        Name = ExtensionName;
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.WorkerService;
        SupportedPatterns =
        [
            "org.quartz*",
            "org.jobrunr*",
            "com.github.kagkarlsson:db-scheduler*",
            "net.javacrumbs.shedlock*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = ProtocolScheduler,
                ListenerAnnotationNames = new[] { JobAnnotation, RecurringAnnotation, ScheduledAnnotation }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { NameKey, IdKey, ValueKey }.ToFrozenSet(StringComparer.Ordinal),
                FallbackTopicName = FallbackJobTopic,
                PublishMethodNames = new[]
                {
                    "scheduleJob", "triggerJob", "enqueue", "schedule", "scheduleRecurrently", "createOrReplace"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[]
                {
                    "scheduler", "jobScheduler", "BackgroundJob", "BackgroundJobRequest"
                }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }
}
