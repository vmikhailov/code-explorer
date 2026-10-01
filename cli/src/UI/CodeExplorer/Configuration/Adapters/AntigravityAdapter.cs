using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class AntigravityAdapter : IMcpTargetAdapter
{
    public string TargetName => "antigravity";
    public string DisplayName => "Google Antigravity & Gemini Code Assist";
    public IReadOnlyList<string> Aliases => ["gemini", "agy"];
    public bool SupportsWorkspaceScope => true;
    public bool SupportsGlobalScope => true;

    public bool IsInstalled(string workspaceRoot)
    {
        var agentsDir = Path.Combine(workspaceRoot, ".agents");
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var geminiDir = Path.Combine(userProfile, ".gemini");
        return Directory.Exists(agentsDir) || Directory.Exists(geminiDir);
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (scope == ConfigScope.Global)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, ".gemini", "config", "mcp_config.json");
        }
        return Path.Combine(workspaceRoot, ".agents", "mcp_config.json");
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
            config["env"] = new JsonObject
            {
                ["WORKSPACE_ROOT"] = "${workspaceFolder}"
            };
        }
        else if (!string.IsNullOrEmpty(workspaceRoot))
        {
            config["cwd"] = workspaceRoot;
            config["env"] = new JsonObject
            {
                ["WORKSPACE_ROOT"] = workspaceRoot
            };
        }

        return config;
    }

    public RuleConfigInfo? GetRuleConfigInfo(string workspaceRoot)
    {
        var rulePath = Path.Combine(workspaceRoot, ".agents", "rules", "code-explorer.md");
        return new RuleConfigInfo(rulePath, StandardRules.GenerateRuleContent(), StandardRules.HeaderMarker);
    }
}
