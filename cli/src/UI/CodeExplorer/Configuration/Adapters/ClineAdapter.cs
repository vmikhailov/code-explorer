using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class ClineAdapter : IMcpTargetAdapter
{
    public string TargetName => "cline";
    public string DisplayName => "Cline / Roo Code";
    public IReadOnlyList<string> Aliases => ["roo", "roo-code"];
    public bool SupportsWorkspaceScope => true;
    public bool SupportsGlobalScope => true;

    public bool IsInstalled(string workspaceRoot)
    {
        var localDir = Path.Combine(workspaceRoot, ".cline");
        var globalPath = GetConfigPath(workspaceRoot, ConfigScope.Global);
        var dir = Path.GetDirectoryName(globalPath);
        return Directory.Exists(localDir) || (!string.IsNullOrEmpty(dir) && Directory.Exists(dir));
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (scope == ConfigScope.Workspace)
        {
            return Path.Combine(workspaceRoot, ".cline", "mcp.json");
        }

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json");
        }
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json");
        }
        var configHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(configHome, ".config", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json");
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
        var rulePath = Path.Combine(workspaceRoot, ".clinerules");
        return new RuleConfigInfo(rulePath, StandardRules.GenerateRuleContent(), StandardRules.HeaderMarker);
    }
}
