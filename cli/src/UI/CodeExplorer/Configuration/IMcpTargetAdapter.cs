using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration;

public record RuleConfigInfo(string FilePath, string Content, string HeaderMarker);

public interface IMcpTargetAdapter
{
    string TargetName { get; }
    string DisplayName { get; }
    IReadOnlyList<string> Aliases { get; }
    bool SupportsWorkspaceScope { get; }
    bool SupportsGlobalScope { get; }

    bool IsInstalled(string workspaceRoot);
    string GetConfigPath(string workspaceRoot, ConfigScope scope);
    JsonObject GenerateMcpConfig(string workspaceRoot, ConfigScope scope);
    RuleConfigInfo? GetRuleConfigInfo(string workspaceRoot);
    string GetServerKey() => "codeexplorer";
    string GetServersSectionKey() => "mcpServers";
}
