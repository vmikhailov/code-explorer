using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class WindsurfAdapter : IMcpTargetAdapter
{
    public string TargetName => "windsurf";
    public string DisplayName => "Codeium Windsurf IDE";
    public IReadOnlyList<string> Aliases => ["codeium"];
    public bool SupportsWorkspaceScope => true;
    public bool SupportsGlobalScope => true;

    public bool IsInstalled(string workspaceRoot)
    {
        var localDir = Path.Combine(workspaceRoot, ".windsurf");
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var globalDir = Path.Combine(userProfile, ".codeium", "windsurf");
        return Directory.Exists(localDir) || Directory.Exists(globalDir) || ToolDetector.IsExecutableInPath("windsurf");
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (scope == ConfigScope.Workspace)
        {
            return Path.Combine(workspaceRoot, ".windsurf", "mcp.json");
        }
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, ".codeium", "windsurf", "mcp_config.json");
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
        var rulePath = Path.Combine(workspaceRoot, ".windsurfrules");
        return new RuleConfigInfo(rulePath, StandardRules.GenerateRuleContent(), StandardRules.HeaderMarker);
    }
}
