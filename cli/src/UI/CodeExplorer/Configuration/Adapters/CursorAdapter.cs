using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class CursorAdapter : IMcpTargetAdapter
{
    public string TargetName => "cursor";
    public string DisplayName => "Cursor IDE";
    public IReadOnlyList<string> Aliases => [];
    public bool SupportsWorkspaceScope => true;
    public bool SupportsGlobalScope => true;

    public bool IsInstalled(string workspaceRoot)
    {
        if (Directory.Exists(Path.Combine(workspaceRoot, ".cursor"))) return true;

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (Directory.Exists(Path.Combine(appData, "Cursor")) ||
                Directory.Exists(Path.Combine(localAppData, "Programs", "cursor")))
            {
                return true;
            }
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (Directory.Exists(Path.Combine(home, ".cursor"))) return true;
        }

        return false;
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (scope == ConfigScope.Global)
        {
            if (OperatingSystem.IsWindows())
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, "Cursor", "User", "globalStorage", "rooveterinaryinc.cursor-mcp", "mcp.json");
            }
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".cursor", "mcp.json");
        }
        return Path.Combine(workspaceRoot, ".cursor", "mcp.json");
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
        var cursorRulesDir = Path.Combine(workspaceRoot, ".cursor", "rules");
        string rulePath = Directory.Exists(cursorRulesDir)
            ? Path.Combine(cursorRulesDir, "code-explorer.mdc")
            : Path.Combine(workspaceRoot, ".cursorrules");

        return new RuleConfigInfo(rulePath, StandardRules.GenerateRuleContent(), StandardRules.HeaderMarker);
    }
}
