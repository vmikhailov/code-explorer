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

    private const string KeyCommand = "command";
    private const string KeyPath = "path";
    private const string KeyArgs = "args";
    private const string ArgMcp = "mcp";
    private const string ArgQuiet = "-q";

    public JsonObject GenerateMcpConfig(string workspaceRoot, ConfigScope scope)
    {
        return new JsonObject
        {
            [KeyCommand] = new JsonObject
            {
                [KeyPath] = McpCommandResolver.ResolveCommand(scope),
                [KeyArgs] = new JsonArray(ArgMcp, ArgQuiet)
            }
        };
    }

    public RuleConfigInfo? GetRuleConfigInfo(string workspaceRoot) => null;
}
