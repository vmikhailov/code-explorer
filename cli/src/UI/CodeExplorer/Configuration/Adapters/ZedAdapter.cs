using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class ZedAdapter : IMcpTargetAdapter
{
    public string TargetName => "zed";
    public string DisplayName => "Zed Editor";
    public IReadOnlyList<string> Aliases => [];
    public bool SupportsWorkspaceScope => true;
    public bool SupportsGlobalScope => true;

    public string GetServersSectionKey() => "context_servers";

    public bool IsInstalled(string workspaceRoot)
    {
        var localDir = Path.Combine(workspaceRoot, ".zed");
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var globalDir = Path.Combine(userProfile, ".config", "zed");
        return Directory.Exists(localDir) || Directory.Exists(globalDir) || ToolDetector.IsExecutableInPath("zed");
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (scope == ConfigScope.Workspace)
        {
            return Path.Combine(workspaceRoot, ".zed", "settings.json");
        }

        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Zed", "settings.json");
        }
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "zed", "settings.json");
    }

    public JsonObject GenerateMcpConfig(string workspaceRoot, ConfigScope scope)
    {
        return new JsonObject
        {
            ["command"] = new JsonObject
            {
                ["path"] = "ce",
                ["args"] = new JsonArray("mcp")
            }
        };
    }

    public RuleConfigInfo? GetRuleConfigInfo(string workspaceRoot) => null;
}
