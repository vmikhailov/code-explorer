using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class AntigravityAdapter : IMcpTargetAdapter
{
    public string TargetName => "antigravity";
    public string DisplayName => "Google Antigravity & Gemini Code Assist";
    public IReadOnlyList<string> Aliases => ["gemini", "agy"];
    public bool SupportsWorkspaceScope => true;
    public bool SupportsGlobalScope => true;

    private const string DirGemini = ".gemini";
    private const string DirAntigravity = "antigravity";
    private const string DirConfig = "config";
    private const string DirAgents = ".agents";
    private const string FileMcpConfig = "mcp_config.json";

    public bool IsInstalled(string workspaceRoot)
    {
        var agentsDir = Path.Combine(workspaceRoot, DirAgents);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var geminiDir = Path.Combine(userProfile, DirGemini);
        return Directory.Exists(agentsDir) || Directory.Exists(geminiDir);
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (scope == ConfigScope.Global)
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var agyDir = Path.Combine(userProfile, DirGemini, DirAntigravity);
            if (Directory.Exists(agyDir))
            {
                return Path.Combine(agyDir, FileMcpConfig);
            }

            return Path.Combine(userProfile, DirGemini, DirConfig, FileMcpConfig);
        }
        return Path.Combine(workspaceRoot, DirAgents, FileMcpConfig);
    }

    private const string KeyCommand = "command";
    private const string KeyArgs = "args";
    private const string KeyEnv = "env";
    private const string EnvWorkspaceRoot = "WORKSPACE_ROOT";
    private const string DefaultCommand = "ce";
    private const string ArgMcp = "mcp";
    private const string ArgQuiet = "-q";
    private const string WorkspaceFolderPlaceholder = "${workspaceFolder}";

    public JsonObject GenerateMcpConfig(string workspaceRoot, ConfigScope scope)
    {
        var procPath = Environment.ProcessPath;
        var command = !string.IsNullOrEmpty(procPath) &&
                      string.Equals(Path.GetFileNameWithoutExtension(procPath), DefaultCommand, StringComparison.OrdinalIgnoreCase)
            ? procPath
            : DefaultCommand;

        var config = new JsonObject
        {
            [KeyCommand] = command,
            [KeyArgs] = new JsonArray(ArgMcp, ArgQuiet)
        };

        if (scope == ConfigScope.Workspace)
        {
            config[KeyEnv] = new JsonObject
            {
                [EnvWorkspaceRoot] = WorkspaceFolderPlaceholder
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
