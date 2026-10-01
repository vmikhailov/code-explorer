using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class ClaudeCodeAdapter : IMcpTargetAdapter
{
    public string TargetName => "claude-code";
    public string DisplayName => "Anthropic Claude Code (CLI)";
    public IReadOnlyList<string> Aliases => ["claudecode"];
    public bool SupportsWorkspaceScope => true;
    public bool SupportsGlobalScope => true;

    public bool IsInstalled(string workspaceRoot)
    {
        var localConfig = Path.Combine(workspaceRoot, ".claude.json");
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var globalConfig = Path.Combine(userProfile, ".claude.json");
        return File.Exists(localConfig) || File.Exists(globalConfig) || ToolDetector.IsExecutableInPath("claude");
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (scope == ConfigScope.Global)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".claude.json");
        }
        return Path.Combine(workspaceRoot, ".claude.json");
    }

    public JsonObject GenerateMcpConfig(string workspaceRoot, ConfigScope scope)
    {
        var config = new JsonObject
        {
            ["command"] = "ce",
            ["args"] = new JsonArray("mcp")
        };

        if (scope == ConfigScope.Workspace)
        {
            config["cwd"] = "${workspaceFolder}";
        }
        else if (!string.IsNullOrEmpty(workspaceRoot))
        {
            config["cwd"] = workspaceRoot;
        }

        return config;
    }

    public RuleConfigInfo? GetRuleConfigInfo(string workspaceRoot)
    {
        var rulePath = Path.Combine(workspaceRoot, "CLAUDE.md");
        return new RuleConfigInfo(rulePath, StandardRules.GenerateRuleContent(), StandardRules.HeaderMarker);
    }
}
