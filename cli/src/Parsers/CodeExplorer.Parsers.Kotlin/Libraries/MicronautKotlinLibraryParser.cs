using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parsers.Kotlin.Libraries;

public class MicronautKotlinLibraryParser : DeclarativeSemanticExtension
{
    private const string MarkerMicronaut = "io.micronaut";
    private const string KafkaPrefix = "kafka:";
    private const string RabbitPrefix = "rabbitmq:";

    private static readonly Regex MicronautTopicRegex = new(
        @"@(?:Topic|Queue)\s*\(\s*(?:(?:value|name)\s*=\s*)?[""'](?<topic>[^""']+)[""']\s*\)",
        RegexOptions.Compiled);

    public MicronautKotlinLibraryParser()
    {
        Id = "micronaut-kotlin";
        Name = "Micronaut Kotlin";
        Type = SemanticExtensionType.Framework;
        LibraryRole = LibraryRole.WebService;
        SupportedPatterns = ["io.micronaut.*", "io.micronaut:micronaut*"];
        AnnotationEndpointRules =
        [
            new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Get"] = "GET",
                    ["Post"] = "POST",
                    ["Put"] = "PUT",
                    ["Delete"] = "DELETE",
                    ["Patch"] = "PATCH",
                    ["Controller"] = "ALL"
                }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                RoutePrefixAnnotationNames = new[] { "Controller" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase)
            }
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(MarkerMicronaut, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in MicronautTopicRegex.Matches(content))
        {
            var topic = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(topic))
            {
                var isRabbit = content.Contains("Rabbit", StringComparison.OrdinalIgnoreCase);
                var prefix = isRabbit ? RabbitPrefix : KafkaPrefix;
                var rel = content.Contains("Client", StringComparison.OrdinalIgnoreCase) && !content.Contains("Listener", StringComparison.OrdinalIgnoreCase)
                    ? OntologyConstants.Relationships.PublishesTo
                    : OntologyConstants.Relationships.SubscribesTo;
                fileNode.References.Add(new Reference(fileNode.Id, prefix + topic, rel));
            }
        }
    }
}
