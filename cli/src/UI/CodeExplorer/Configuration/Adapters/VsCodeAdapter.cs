using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class VsCodeAdapter : IMcpTargetAdapter
{
    public string TargetName => "vscode";
    public string DisplayName => "Visual Studio Code (Copilot / Native MCP)";
    public IReadOnlyList<string> Aliases => ["code", "copilot"];
    public bool SupportsWorkspaceScope => true;
    public bool SupportsGlobalScope => true;

    public string GetServersSectionKey() => SectionServers;

    private const string SectionServers = "servers";
    private const string DirVsCode = ".vscode";
    private const string DirCode = "Code";
    private const string DirUser = "User";
    private const string DirLibrary = "Library";
    private const string DirAppSupport = "Application Support";
    private const string DirDotConfig = ".config";
    private const string FileMcpJson = "mcp.json";
    private const string ExeCode = "code";
    private const string KeyType = "type";
    private const string TypeStdio = "stdio";
    private const string KeyCommand = "command";
    private const string KeyArgs = "args";
    private const string KeyCwd = "cwd";
    private const string KeyEnv = "env";
    private const string EnvWorkspaceRoot = "WORKSPACE_ROOT";
    private const string ArgMcp = "mcp";
    private const string ArgQuiet = "-q";
    private const string WorkspaceFolderPlaceholder = "${workspaceFolder}";

    public bool IsInstalled(string workspaceRoot)
    {
        var globalDir = Path.GetDirectoryName(GetConfigPath(workspaceRoot, ConfigScope.Global));
        return Directory.Exists(Path.Combine(workspaceRoot, DirVsCode)) ||
               (!string.IsNullOrEmpty(globalDir) && Directory.Exists(globalDir)) ||
               ToolDetector.IsExecutableInPath(ExeCode);
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        if (scope == ConfigScope.Global)
        {
            if (OperatingSystem.IsWindows())
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, DirCode, DirUser, FileMcpJson);
            }
            if (OperatingSystem.IsMacOS())
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(home, DirLibrary, DirAppSupport, DirCode, DirUser, FileMcpJson);
            }
            var linuxHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(linuxHome, DirDotConfig, DirCode, DirUser, FileMcpJson);
        }
        return Path.Combine(workspaceRoot, DirVsCode, FileMcpJson);
    }

    public JsonObject GenerateMcpConfig(string workspaceRoot, ConfigScope scope)
    {
        var config = new JsonObject
        {
            [KeyType] = TypeStdio,
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
        var rulePath = Path.Combine(workspaceRoot, ".github", "copilot-instructions.md");
        return new RuleConfigInfo(rulePath, StandardRules.GenerateRuleContent(), StandardRules.HeaderMarker);
    }
}
