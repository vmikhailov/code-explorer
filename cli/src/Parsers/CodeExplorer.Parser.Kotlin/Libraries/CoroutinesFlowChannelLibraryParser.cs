using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;

namespace CodeExplorer.Parser.Kotlin.Libraries;

public sealed class CoroutinesFlowChannelLibraryParser : DeclarativeSemanticExtension
{
    private const string FlowPrefix = "coroutines:";
    private const string ImportFlow = "kotlinx.coroutines.flow";
    private const string ImportChannels = "kotlinx.coroutines.channels";

    private static readonly Regex EmitSendRegex = new(
        @"(?<target>[a-zA-Z0-9_]+)\s*\.\s*(?:emit|tryEmit|send|trySend)\s*\(",
        RegexOptions.Compiled);

    private static readonly Regex CollectRegex = new(
        @"(?<target>[a-zA-Z0-9_]+)\s*\.\s*(?:collect|collectLatest|receive|consumeEach)\b",
        RegexOptions.Compiled);

    public CoroutinesFlowChannelLibraryParser()
    {
        Id = "coroutines-flow";
        Name = "Kotlinx Coroutines Flow & Channels";
        Type = SemanticExtensionType.Queue;
        LibraryRole = LibraryRole.MessageBroker;
        SupportedPatterns =
        [
            "kotlinx.coroutines.flow*",
            "kotlinx.coroutines.channels*"
        ];
    }

    public static void AnalyzeSource(string content, FileNode fileNode)
    {
        if (!content.Contains(ImportFlow, StringComparison.Ordinal) &&
            !content.Contains(ImportChannels, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in EmitSendRegex.Matches(content))
        {
            var target = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(target))
            {
                fileNode.References.Add(new Reference(fileNode.Id, FlowPrefix + target, OntologyConstants.Relationships.PublishesTo));
            }
        }

        foreach (Match match in CollectRegex.Matches(content))
        {
            var target = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(target))
            {
                fileNode.References.Add(new Reference(fileNode.Id, FlowPrefix + target, OntologyConstants.Relationships.SubscribesTo));
            }
        }
    }
}
