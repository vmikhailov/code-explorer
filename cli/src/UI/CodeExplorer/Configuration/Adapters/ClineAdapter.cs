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

    private const string KeyCommand = "command";
    private const string KeyArgs = "args";
    private const string KeyCwd = "cwd";
    private const string KeyEnv = "env";
    private const string EnvWorkspaceRoot = "WORKSPACE_ROOT";
    private const string ArgMcp = "mcp";
    private const string ArgQuiet = "-q";
    private const string WorkspaceFolderPlaceholder = "${workspaceFolder}";

    public JsonObject GenerateMcpConfig(string workspaceRoot, ConfigScope scope)
    {
        var config = new JsonObject
        {
            [KeyCommand] = McpCommandResolver.ResolveCommand(scope),
            [KeyArgs] = new JsonArray(ArgMcp, ArgQuiet)
        };

        if (scope == ConfigScope.Workspace)
        {
            config[KeyCwd] = WorkspaceFolderPlaceholder;
            config[KeyEnv] = new JsonObject
            {
                [EnvWorkspaceRoot] = WorkspaceFolderPlaceholder
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
