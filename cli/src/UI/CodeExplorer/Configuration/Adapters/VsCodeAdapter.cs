using System.Text.Json.Nodes;

namespace CodeExplorer.Configuration.Adapters;

public class VsCodeAdapter : IMcpTargetAdapter
{
    public string TargetName => "vscode";
    public string DisplayName => "Visual Studio Code (Copilot / Native MCP)";
    public IReadOnlyList<string> Aliases => ["code", "copilot"];
    public bool SupportsWorkspaceScope => true;
    public bool SupportsGlobalScope => false;

    public bool IsInstalled(string workspaceRoot)
    {
        return Directory.Exists(Path.Combine(workspaceRoot, ".vscode")) || ToolDetector.IsExecutableInPath("code");
    }

    public string GetConfigPath(string workspaceRoot, ConfigScope scope)
    {
        return Path.Combine(workspaceRoot, ".vscode", "mcp.json");
    }

    public JsonObject GenerateMcpConfig(string workspaceRoot, ConfigScope scope)
    {
        return new JsonObject
        {
            ["command"] = "ce",
            ["args"] = new JsonArray("mcp"),
            ["cwd"] = "${workspaceFolder}",
            ["env"] = new JsonObject
            {
                ["WORKSPACE_ROOT"] = "${workspaceFolder}"
            }
        };
    }

    public RuleConfigInfo? GetRuleConfigInfo(string workspaceRoot)
    {
        var rulePath = Path.Combine(workspaceRoot, ".github", "copilot-instructions.md");
        return new RuleConfigInfo(rulePath, StandardRules.GenerateRuleContent(), StandardRules.HeaderMarker);
    }
}
