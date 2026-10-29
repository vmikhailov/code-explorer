using System.Text;
using CodePassport.Cli.Commands;
using CodePassport.Cli.Options;
using CommandLine;
using CommandLineParser = CommandLine.Parser;

namespace CodePassport.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Ignore console encoding errors in redirected streams
        }

        if (args.Length == 0 || (args.Length == 1 && args[0] is "--help" or "-h" or "help"))
        {
            ShowHelpBanner();
            return 0;
        }

        using var parser = new CommandLineParser(settings =>
        {
            settings.HelpWriter = Console.Error;
            settings.CaseInsensitiveEnumValues = true;
        });

        var result = parser.ParseArguments(args,
        [
            typeof(InspectOptions),
            typeof(DiffOptions),
            typeof(AuditOptions)
        ]);

        if (result is Parsed<object> parsed)
        {
            return parsed.Value switch
            {
                InspectOptions opts => await InspectCommandHandler.HandleAsync(opts),
                DiffOptions opts => await DiffCommandHandler.HandleAsync(opts),
                AuditOptions opts => await AuditCommandHandler.HandleAsync(opts),
                _ => 1
            };
        }

        return 1;
    }

    private static void ShowHelpBanner()
    {
        Console.WriteLine("CodePassport CLI (cp) — Supply-Chain Behavioral Passport & Capability Drift Engine");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  cp inspect [path] [--json] [--out <file>]       Generate 10D Behavioral Passport for a package");
        Console.WriteLine("  cp diff <fromPath> <toPath> [--fail-on-drift]   Compute capability drift between two versions");
        Console.WriteLine("  cp audit [path] [--json]                        Audit workspace or node_modules for toxic drift");
    }
}
