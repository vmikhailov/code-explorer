using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Parser.Kotlin;

public partial class KotlinFileParser
{
    private const string ChannelType = "channel";
    private static readonly Regex ChannelRegex = new(@"MethodChannel\s*\(\s*[^,]+,\s*(?:['""]([^'""]+)['""]|([a-zA-Z0-9_]+))\s*\)", RegexOptions.Compiled);

    private static void ParseChannelsAndMessaging(
        string content,
        FileNode fileNode,
        Dictionary<string, string> stringVars,
        string workspaceId,
        string relativePath,
        List<ClassScope> classScopes)
    {
        var primaryClassNode = classScopes.Count > 0 ? classScopes[0].Node : null;

        var channelMatches = ChannelRegex.Matches(content);
        var registeredChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match chm in channelMatches)
        {
            var literal = chm.Groups[1].Success ? chm.Groups[1].Value.Trim() : null;
            var idVar = chm.Groups[2].Success ? chm.Groups[2].Value.Trim() : null;

            var channelName = literal;
            if (string.IsNullOrEmpty(channelName) && !string.IsNullOrEmpty(idVar) && stringVars.TryGetValue(idVar, out var resolved))
            {
                channelName = resolved;
            }

            if (!string.IsNullOrEmpty(channelName) && registeredChannels.Add(channelName))
            {
                var channelId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:channel:{channelName.ToLowerInvariant()}";
                var channelEp = new EntryPointNode(
                    channelId,
                    $"MethodChannel {channelName}",
                    relativePath,
                    ChannelType
                );
                fileNode.Children.Add(channelEp);
            }
        }

        foreach (var (varName, varVal) in stringVars)
        {
            if (varName.EndsWith("Channel", StringComparison.OrdinalIgnoreCase) && varVal.Contains('/') && registeredChannels.Add(varVal))
            {
                var channelId = $"{workspaceId}:{OntologyConstants.IdPrefixes.EntryPoint}:channel:{varVal.ToLowerInvariant()}";
                var channelEp = new EntryPointNode(
                    channelId,
                    $"MethodChannel {varVal}",
                    relativePath,
                    ChannelType
                );
                fileNode.Children.Add(channelEp);
            }
        }

        var kafkaMatches = KafkaSubscribeRegex.Matches(content);
        foreach (Match km in kafkaMatches)
        {
            var literal = km.Groups[1].Success ? km.Groups[1].Value.Trim() : null;
            var idVar = km.Groups[2].Success ? km.Groups[2].Value.Trim() : null;
            var tName = literal;
            if (string.IsNullOrEmpty(tName) && !string.IsNullOrEmpty(idVar))
            {
                if (stringVars.TryGetValue(idVar, out var res)) tName = res;
                else
                {
                    var fallbackMatch = Regex.Match(content, $@"{idVar}\s*:[^=]+=\s*.*?\?:\s*['""]([^'""]+)['""]");
                    if (fallbackMatch.Success) tName = fallbackMatch.Groups[1].Value;
                    else tName = idVar;
                }
            }

            if (!string.IsNullOrEmpty(tName))
            {
                var target = $"kafka:{tName}";
                var scopeId = primaryClassNode?.Id ?? fileNode.Id;
                fileNode.References.Add(new Reference(scopeId, target, OntologyConstants.Relationships.SubscribesTo, 1, 1));
            }
        }
    }
}
