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

    private const string DirCursor = ".cursor";
    private const string FileMcpJson = "mcp.json";
    private const string FileCursorRules = ".cursorrules";
    private const string FileRuleMdc = "code-explorer.mdc";
    private const string DirRules = "rules";
    private const string KeyCommand = "command";
    private const string KeyArgs = "args";
    private const string KeyCwd = "cwd";
    private const string KeyEnv = "env";
    private const string EnvWorkspaceRoot = "WORKSPACE_ROOT";
    private const string ArgMcp = "mcp";
    private const string ArgQuiet = "-q";
    private const string WorkspaceFolderPlaceholder = "${workspaceFolder}";

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (scope == ConfigScope.Global)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, DirCursor, FileMcpJson);
        }
        return Path.Combine(workspaceRoot, DirCursor, FileMcpJson);
    }

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
        var cursorRulesDir = Path.Combine(workspaceRoot, DirCursor, DirRules);
        var rulePath = Directory.Exists(cursorRulesDir)
            ? Path.Combine(cursorRulesDir, FileRuleMdc)
            : Path.Combine(workspaceRoot, FileCursorRules);

        return new RuleConfigInfo(rulePath, StandardRules.GenerateRuleContent(), StandardRules.HeaderMarker);
    }
}
