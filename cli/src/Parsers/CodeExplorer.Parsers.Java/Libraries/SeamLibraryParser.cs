using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace CodeExplorer.Parsers.Java.Libraries;

/// <summary>
/// Declarative semantic extension for JBoss Seam 2 (component @Name registry, @In/@Out bijection, @Observer/@RaiseEvent event bus, @WebRemote).
/// </summary>
public partial class SeamLibraryParser : DeclarativeSemanticExtension
{
    [GeneratedRegex(@"@(?:Name|Named)\s*\(\s*(?:value\s*=\s*)?""([^""]+)""\s*\)(?:[\s\S]{0,600}?)\bclass\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled)]
    private static partial Regex SeamNamedClassRegex();

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

    public override void DiscoverDeclarations(string filePath, string content, string? projectName, ParsingContext? ctx)
    {
        RegisterNamedBeans(content, ctx);
    }

    public override void DiscoverDeclarations(CodeExplorer.Core.Parser.Syntax.SyntaxNode rootNode, string relativePath, string? projectName, ParsingContext? ctx)
    {
        var sourceText = (rootNode as CodeExplorer.Core.Parser.Syntax.SyntaxDocument)?.SourceText
                         ?? rootNode.Document?.SourceText
                         ?? rootNode.Text;
        RegisterNamedBeans(sourceText, ctx);
    }

    private static void RegisterNamedBeans(string content, ParsingContext? ctx)
    {
        if (ctx == null || string.IsNullOrEmpty(content) || !content.Contains('@')) return;

        foreach (Match m in SeamNamedClassRegex().Matches(content))
        {
            var beanName = m.Groups[1].Value.Trim();
            var className = m.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(beanName) && !string.IsNullOrEmpty(className))
            {
                ctx.NamedBeanTypes[beanName] = className;
            }
        }
    }
}
