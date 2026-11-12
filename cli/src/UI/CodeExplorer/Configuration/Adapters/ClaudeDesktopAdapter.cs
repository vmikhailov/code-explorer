using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class ClaudeDesktopAdapter : IMcpTargetAdapter
{
    public string TargetName => "claude";
    public string DisplayName => "Anthropic Claude Desktop";
    public IReadOnlyList<string> Aliases => ["claude-desktop", "anthropic"];
    public bool SupportsWorkspaceScope => false;
    public bool SupportsGlobalScope => true;

    public bool IsInstalled(string workspaceRoot)
    {
        var configPath = GetConfigPath(workspaceRoot, ConfigScope.Global);
        var dir = Path.GetDirectoryName(configPath);
        return !string.IsNullOrEmpty(dir) && Directory.Exists(dir);
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Claude", "claude_desktop_config.json");
        }
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "Claude", "claude_desktop_config.json");
        }
        var configHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(configHome, ".config", "Claude", "claude_desktop_config.json");
    }

    private const string KeyCommand = "command";
    private const string KeyArgs = "args";
    private const string ArgMcp = "mcp";
    private const string ArgQuiet = "-q";

    public JsonObject GenerateMcpConfig(string workspaceRoot, ConfigScope scope)
    {
        return new JsonObject
        {
            [KeyCommand] = McpCommandResolver.ResolveCommand(ConfigScope.Global),
            [KeyArgs] = new JsonArray(ArgMcp, ArgQuiet)
        };
    }

    public RuleConfigInfo? GetRuleConfigInfo(string workspaceRoot) => null;
}
