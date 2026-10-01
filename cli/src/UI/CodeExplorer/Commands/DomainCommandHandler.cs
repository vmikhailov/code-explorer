using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Options;

namespace CodeExplorer.Commands;

public static class DomainCommandHandler
{
    public static async Task<int> HandleAsync(DomainOptions opts)
    {
        try
        {
            var ws = WorkspaceLocator.FindWithFallbacks(opts.Root);
            var wsRoot = ws?.RootDirectory ?? (opts.Root != null ? Path.GetFullPath(opts.Root) : Directory.GetCurrentDirectory());

            var action = (opts.Action ?? "list").ToLowerInvariant();

            switch (action)
            {
                case "list":
                    PrintDomainList(wsRoot);
                    return 0;

                case "add":
                    if (string.IsNullOrWhiteSpace(opts.Target))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("Error: Domain name is required. Usage: code-explorer domain add <name> [--display <display>] [--desc <description>] [--icon <icon>] [--color <color>]");
                        Console.ResetColor();
                        return 1;
                    }
                    var domain = DomainManagementService.AddOrUpdateDomain(wsRoot, new DomainDefinitionDto
                    {
                        Name = opts.Target.Trim(),
                        DisplayName = opts.DisplayName,
                        Description = opts.Description,
                        Icon = opts.Icon,
                        Color = opts.Color
                    });
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"✓ Domain '{domain.Name}' added/updated in {DomainManagementService.GetConfigFilePath(wsRoot)}.");
                    Console.ResetColor();
                    return 0;

                case "remove":
                case "delete":
                    if (string.IsNullOrWhiteSpace(opts.Target))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("Error: Domain name is required. Usage: code-explorer domain remove <name> [--reassign <targetDomain>]");
                        Console.ResetColor();
                        return 1;
                    }
                    var removed = DomainManagementService.RemoveDomain(wsRoot, opts.Target.Trim(), opts.ReassignTo);
                    if (removed)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Domain '{opts.Target}' removed.");
                        Console.ResetColor();
                        return 0;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Domain '{opts.Target}' not found in configuration.");
                        Console.ResetColor();
                        return 1;
                    }

                case "assign":
                    if (string.IsNullOrWhiteSpace(opts.Target) || string.IsNullOrWhiteSpace(opts.Domain))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("Error: Both service name and target domain are required. Usage: code-explorer domain assign <service> --domain <domain> [--context <context>]");
                        Console.ResetColor();
                        return 1;
                    }

                    SqliteGraphClient? dbClient = null;
                    if (ws != null && File.Exists(ws.DbPath))
                    {
                        dbClient = new SqliteGraphClient(ws.DbPath);
                    }

                    try
                    {
                        DomainManagementService.AssignServiceDomain(wsRoot, opts.Target.Trim(), opts.Domain.Trim(), opts.Context, dbClient);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Assigned service '{opts.Target}' to domain '{opts.Domain}'" +
                            (string.IsNullOrWhiteSpace(opts.Context) ? "" : $" (bounded context: '{opts.Context}')") + ".");
                        Console.ResetColor();
                        return 0;
                    }
                    finally
                    {
                        if (dbClient != null) await dbClient.DisposeAsync();
                    }

                case "unassign":
                    if (string.IsNullOrWhiteSpace(opts.Target))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine("Error: Service name is required. Usage: code-explorer domain unassign <service>");
                        Console.ResetColor();
                        return 1;
                    }
                    var unassigned = DomainManagementService.RemoveServiceOverride(wsRoot, opts.Target.Trim());
                    if (unassigned)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Override removed for service '{opts.Target}'. Reverted to automatic detection.");
                        Console.ResetColor();
                        return 0;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"No active override found for service '{opts.Target}'.");
                        Console.ResetColor();
                        return 1;
                    }

                default:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"Unknown action '{action}'. Valid actions are 'list', 'add', 'remove', 'assign', 'unassign'.");
                    Console.ResetColor();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Domain Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintDomainList(string wsRoot)
    {
        var config = DomainManagementService.LoadConfig(wsRoot);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"=== Configured Domains in {wsRoot} ===");
        Console.ResetColor();

        if (config.Domains.Count == 0)
        {
            Console.WriteLine("No custom domain definitions configured in .codeexplorer/domains.json.");
            Console.WriteLine("Add one with: code-explorer domain add <name> --display <displayName>");
        }
        else
        {
            foreach (var d in config.Domains)
            {
                var icon = !string.IsNullOrWhiteSpace(d.Icon) ? $"{d.Icon} " : "";
                var display = !string.IsNullOrWhiteSpace(d.DisplayName) ? $" ({d.DisplayName})" : "";
                Console.WriteLine($"• {icon}{d.Name}{display}");
                if (!string.IsNullOrWhiteSpace(d.Description))
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"    Description: {d.Description}");
                    Console.ResetColor();
                }
                if (d.Services is { Count: > 0 })
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"    Services: {string.Join(", ", d.Services)}");
                    Console.ResetColor();
                }
                if (d.Patterns is { Count: > 0 })
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"    Patterns: {string.Join(", ", d.Patterns)}");
                    Console.ResetColor();
                }
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== Service Overrides ===");
        Console.ResetColor();

        if (config.Overrides.Count == 0)
        {
            Console.WriteLine("No active service domain overrides.");
            Console.WriteLine("Assign a service with: code-explorer domain assign <service> --domain <domain>");
        }
        else
        {
            foreach (var (svc, ovr) in config.Overrides)
            {
                var ctx = !string.IsNullOrWhiteSpace(ovr.Context) ? $" [Context: {ovr.Context}]" : "";
                Console.WriteLine($"  {svc} -> {ovr.Domain}{ctx}");
            }
        }
    }
}
