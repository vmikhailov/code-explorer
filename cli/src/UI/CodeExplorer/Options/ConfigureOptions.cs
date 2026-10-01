using CommandLine;

namespace CodeExplorer.Options;

[Verb("configure", HelpText = "Configure MCP servers and agent rules for AI tools (antigravity, cursor, claude, claude-code, windsurf, vscode, cline, zed, all).")]
public class ConfigureOptions : ITargetOption
{
    [Value(0, MetaName = "category", Required = false, HelpText = "Category to configure (e.g. 'mcp') or target tool directly.")]
    public string? Category { get; set; }

    [Value(1, MetaName = "target", Required = false, HelpText = "Target AI tool: antigravity, cursor, claude, claude-code, windsurf, vscode, cline, zed, all.")]
    public string? Target { get; set; }

    [Option("scope", Default = "workspace", HelpText = "Configuration scope: 'workspace' (default) or 'global'.")]
    public string Scope { get; set; } = "workspace";

    [Option("rules", Default = true, HelpText = "Automatically provision AI agent rules / instructions (e.g. .cursorrules, CLAUDE.md).")]
    public bool Rules { get; set; } = true;

    [Option("no-rules", Required = false, HelpText = "Disable automatic agent rule generation.")]
    public bool NoRules { get; set; }

    [Option("remove", Required = false, HelpText = "Remove CodeExplorer configuration from the specified tool.")]
    public bool Remove { get; set; }

    [Option("dry-run", Required = false, HelpText = "Show proposed configuration changes without modifying files.")]
    public bool DryRun { get; set; }

    [Option("force", Required = false, HelpText = "Overwrite existing configurations without confirmation.")]
    public bool Force { get; set; }

    [Option("verify", Required = false, HelpText = "Test JSON-RPC communication with ce mcp server.")]
    public bool Verify { get; set; }

    [Option("root", Required = false, HelpText = "Workspace root directory (defaults to current workspace).")]
    public string? Root { get; set; }
}
