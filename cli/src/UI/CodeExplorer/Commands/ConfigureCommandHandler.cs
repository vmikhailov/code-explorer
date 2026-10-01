using System.Diagnostics;
using System.Text.Json.Nodes;
using CodeExplorer.Configuration;
using CodeExplorer.Core.Common;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public class ConfigureCommandHandler : BaseCommandHandler
{
    public static async Task<int> HandleAsync(ConfigureOptions opts)
    {
        var wsRoot = opts.Root ?? WorkspaceLocator.Find()?.RootDirectory ?? Directory.GetCurrentDirectory();
        var scope = IsOneOf(opts.Scope, "global")
            ? ConfigScope.Global
            : ConfigScope.Workspace;

        var target = ResolveTarget(opts);

        if (opts.Verify && string.IsNullOrEmpty(target))
        {
            return await RunDoctorAsync(wsRoot);
        }

        if (string.IsNullOrEmpty(target))
        {
            var detected = ToolDetector.DetectInstalledTools(wsRoot);
            if (Console.IsInputRedirected || detected.Count == 0)
            {
                if (detected.Count > 0)
                {
                    return ConfigureMultiple(detected, wsRoot, scope, opts);
                }

                ShowAvailableTargetsHelp();
                return 0;
            }

            return RunInteractiveWizard(detected, wsRoot, scope, opts);
        }

        if (HasTarget(target, "all", "auto"))
        {
            var detected = ToolDetector.DetectInstalledTools(wsRoot);
            if (detected.Count == 0)
            {
                Console.WriteLine("⚠️ No installed AI tools were automatically detected on this system.");
                Console.WriteLine("You can configure any tool explicitly by specifying its name:");
                ShowAvailableTargetsHelp();
                return 0;
            }

            return ConfigureMultiple(detected, wsRoot, scope, opts);
        }

        var adapter = ToolDetector.FindAdapter(target);
        if (adapter == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: Unknown AI tool target '{target}'.");
            Console.ResetColor();
            ShowAvailableTargetsHelp();
            return 1;
        }

        var success = ConfigureSingle(adapter, wsRoot, scope, opts);

        if (opts.Verify)
        {
            await RunDoctorAsync(wsRoot);
        }

        return success ? 0 : 1;
    }

    private static string? ResolveTarget(ConfigureOptions opts)
    {
        if (IsOneOf(opts.Category, "mcp"))
        {
            return opts.Target;
        }

        if (!string.IsNullOrEmpty(opts.Category))
        {
            return opts.Category;
        }

        return opts.Target;
    }

    private static bool ConfigureSingle(
        IMcpTargetAdapter adapter,
        string wsRoot,
        ConfigScope scope,
        ConfigureOptions opts)
    {
        var effectiveScope = scope;
        if (scope == ConfigScope.Workspace && !adapter.SupportsWorkspaceScope)
        {
            effectiveScope = ConfigScope.Global;
        }
        else if (scope == ConfigScope.Global && !adapter.SupportsGlobalScope)
        {
            effectiveScope = ConfigScope.Workspace;
        }

        var configPath = adapter.GetConfigPath(wsRoot, effectiveScope);
        var serverConfig = adapter.GenerateMcpConfig(wsRoot, effectiveScope);

        var mergeResult = ConfigMerger.Merge(
            configPath,
            adapter.GetServerKey(),
            adapter.GetServersSectionKey(),
            serverConfig,
            remove: opts.Remove,
            dryRun: opts.DryRun
        );

        RuleResult? ruleResult = null;
        var shouldHandleRules = opts is { Rules: true, NoRules: false } || opts.Remove;
        var ruleInfo = adapter.GetRuleConfigInfo(wsRoot);
        if (ruleInfo != null && shouldHandleRules)
        {
            ruleResult = RuleProvisioner.Provision(ruleInfo, remove: opts.Remove, dryRun: opts.DryRun);
        }

        PrintResult(adapter, effectiveScope, wsRoot, mergeResult, ruleResult, opts.DryRun, opts.Remove);
        return true;
    }

    private static int ConfigureMultiple(
        IReadOnlyList<IMcpTargetAdapter> adapters,
        string wsRoot,
        ConfigScope scope,
        ConfigureOptions opts)
    {
        Console.WriteLine($"{(opts.Remove ? "Removing" : "Configuring")} CodeExplorer MCP for {adapters.Count} tool(s)...");
        Console.WriteLine();

        var allOk = true;
        foreach (var adapter in adapters)
        {
            if (!ConfigureSingle(adapter, wsRoot, scope, opts))
            {
                allOk = false;
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✓ MCP configuration complete!");
        Console.ResetColor();
        return allOk ? 0 : 1;
    }

    private static int RunInteractiveWizard(
        IReadOnlyList<IMcpTargetAdapter> detected,
        string wsRoot,
        ConfigScope scope,
        ConfigureOptions opts)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== CodeExplorer MCP Configuration Wizard ===");
        Console.ResetColor();
        Console.WriteLine($"Workspace: {wsRoot}");
        Console.WriteLine();
        Console.WriteLine("Detected AI tools on your system:");

        for (int i = 0; i < detected.Count; i++)
        {
            Console.WriteLine($"  [{i + 1}] {detected[i].DisplayName} ({detected[i].TargetName})");
        }
        Console.WriteLine("  [A] Configure ALL detected tools (Recommended)");
        Console.WriteLine("  [Q] Cancel / Quit");
        Console.WriteLine();
        Console.Write("Select an option [A]: ");

        var input = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(input) || IsOneOf(input, "A"))
        {
            return ConfigureMultiple(detected, wsRoot, scope, opts);
        }

        if (IsOneOf(input, "Q"))
        {
            Console.WriteLine("Operation cancelled.");
            return 0;
        }

        if (int.TryParse(input, out var idx) && idx >= 1 && idx <= detected.Count)
        {
            var chosen = detected[idx - 1];
            ConfigureSingle(chosen, wsRoot, scope, opts);
            return 0;
        }

        Console.WriteLine("Invalid selection.");
        return 1;
    }

    private static void PrintResult(
        IMcpTargetAdapter adapter,
        ConfigScope scope,
        string wsRoot,
        MergeResult mergeResult,
        RuleResult? ruleResult,
        bool dryRun,
        bool remove)
    {
        var prefix = dryRun ? "[Dry Run] " : "";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{prefix}{(remove ? "Unregistered" : "Configured")} ");
        Console.ResetColor();
        Console.WriteLine($"{adapter.DisplayName} ({scope.ToString().ToLower()} scope):");

        var relConfig = GetDisplayPath(mergeResult.FilePath, wsRoot);
        Console.WriteLine($"  • Config: {relConfig} ({mergeResult.Details ?? mergeResult.Status.ToString()})");

        if (ruleResult != null)
        {
            var relRule = GetDisplayPath(ruleResult.FilePath, wsRoot);
            Console.WriteLine($"  • Rules:  {relRule} ({ruleResult.Details ?? ruleResult.Status.ToString()})");
        }
    }

    private static string GetDisplayPath(string fullPath, string wsRoot)
    {
        try
        {
            if (fullPath.StartsWith(wsRoot, StringComparison.OrdinalIgnoreCase))
            {
                var rel = Path.GetRelativePath(wsRoot, fullPath);
                return rel.StartsWith('.') ? rel : "./" + rel;
            }
        }
        catch { }
        return fullPath;
    }

    private static async Task<int> RunDoctorAsync(string wsRoot)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== CodeExplorer MCP Doctor ===");
        Console.ResetColor();

        // 1. Check ce in PATH
        var ceInPath = ToolDetector.IsExecutableInPath("ce");
        Console.Write("  • 'ce' command in PATH: ");
        if (ceInPath)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("OK");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Warning (ensure ~/.dotnet/tools or binary is in PATH)");
        }
        Console.ResetColor();

        // 2. Check workspace database
        var ws = WorkspaceLocator.Find(wsRoot);
        Console.Write("  • Workspace Knowledge Graph: ");
        if (ws != null && File.Exists(ws.DbPath))
        {
            var size = new FileInfo(ws.DbPath).Length / 1024;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Found ({ws.DbPath}, {size} KB)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Not initialized (run 'ce init && ce scan' in {wsRoot})");
        }
        Console.ResetColor();

        // 3. Test stdio MCP ping
        Console.Write("  • MCP stdio protocol handshake: ");
        var pingOk = await TestMcpPingAsync(wsRoot);
        if (pingOk)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("OK (JSON-RPC handshake passed)");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to respond over stdio");
        }
        Console.ResetColor();

        Console.WriteLine();
        return (ceInPath && pingOk) ? 0 : 1;
    }

    private static async Task<bool> TestMcpPingAsync(string wsRoot)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ce",
                Arguments = "mcp",
                WorkingDirectory = wsRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                EnvironmentVariables = { ["WORKSPACE_ROOT"] = wsRoot }
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;

            var initRequest = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = 1,
                ["method"] = "initialize",
                ["params"] = new JsonObject
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new JsonObject(),
                    ["clientInfo"] = new JsonObject
                    {
                        ["name"] = "ce-doctor",
                        ["version"] = "1.0.0"
                    }
                }
            };

            await proc.StandardInput.WriteLineAsync(initRequest.ToJsonString());
            await proc.StandardInput.FlushAsync();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var lineTask = proc.StandardOutput.ReadLineAsync(cts.Token);
            var line = await lineTask;

            proc.Kill();
            return !string.IsNullOrEmpty(line) && line.Contains("\"result\"");
        }
        catch
        {
            return false;
        }
    }

    private static void ShowAvailableTargetsHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Available AI tool targets:");
        foreach (var a in ToolDetector.GetAllAdapters())
        {
            var aliases = a.Aliases.Count > 0 ? $" (aliases: {string.Join(", ", a.Aliases)})" : "";
            Console.WriteLine($"  • {a.TargetName,-14} - {a.DisplayName}{aliases}");
        }
        Console.WriteLine("  • all / auto     - Configure all detected tools on this system");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  ce configure mcp antigravity");
        Console.WriteLine("  ce configure mcp cursor");
        Console.WriteLine("  ce configure mcp claude");
        Console.WriteLine("  ce configure mcp --all");
        Console.WriteLine("  ce configure mcp cursor --remove");
    }
}
