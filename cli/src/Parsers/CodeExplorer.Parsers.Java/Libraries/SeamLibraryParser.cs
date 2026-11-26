using System.Collections.Frozen;

namespace CodeExplorer.Parsers.Java.Libraries;

/// <summary>
/// Declarative semantic extension for JBoss Seam 2 (component naming, @In/@Out bijection, @Observer/@RaiseEvent event bus, @WebRemote).
/// </summary>
public class SeamLibraryParser : DeclarativeSemanticExtension
{
    public SeamLibraryParser()
    {
        Id = "jboss-seam";
        Name = "JBoss Seam 2";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        IsBuiltIn = true;
        SupportedPatterns =
        [
            "org.jboss.seam*",
            "*jboss-seam*"
        ];
        MessagingBrokerRules =
        [
            new MessagingBrokerRule
            {
                Protocol = "seam-events",
                ListenerAnnotationNames = new[] { "Observer" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                ProducerAnnotationNames = new[] { "RaiseEvent" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TopicArgumentKeys = new[] { "value" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                PublishMethodNames = new[]
                {
                    "raiseEvent", "raiseAsynchronousEvent", "raiseTransactionSuccessEvent", "raiseTimedEvent"
                }.ToFrozenSet(StringComparer.Ordinal),
                PublishReceiverSubstrings = new[] { "Events", "events" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = "seam-event"
            }
        ];
        AnnotationSinkRules =
        [
            new AnnotationSinkRule
            {
                AnnotationNames = new[] { "Name" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.Exposes,
                TargetPrefix = "seam-component:"
            },
            new AnnotationSinkRule
            {
                AnnotationNames = new[] { "In", "Out" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.DependsOn,
                TargetPrefix = "seam-component:"
            },
            new AnnotationSinkRule
            {
                AnnotationNames = new[] { "WebRemote", "Restrict" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                TargetRelation = OntologyConstants.Relationships.Calls,
                TargetPrefix = "seam:"
            }
        ];
    }
}
