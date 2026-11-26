using CodeExplorer.Commands;
using CodeExplorer.Options;
using CommandLine;
using Microsoft.AspNetCore.Builder;
using CommandLineParser = CommandLine.Parser;

namespace CodeExplorer;

public class Program
{
    public static WebApplication? App
    {
        get => CodeExplorerServerHost.ActiveApp;
        set => CodeExplorerServerHost.ActiveApp = value;
    }

    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch { }

        if (args == null || args.Length == 0 ||
            (args.Length == 1 && args[0] is "--help" or "-h" or "help"))
        {
            HelpDisplay.ShowWelcomeAndHelp();
            return 0;
        }

        WorkspaceIndexer.AutoRegisterParsers();

        using var parser = new CommandLineParser(settings =>
        {
            settings.HelpWriter = Console.Error;
            settings.CaseInsensitiveEnumValues = true;
        });

        var parseResult = parser.ParseArguments(args, [
            typeof(InitOptions), typeof(ScanOptions), typeof(IndexOptions), typeof(StatusOptions), typeof(InfoOptions),
            typeof(ClearOptions), typeof(QueriesOptions), typeof(QueryOptions), typeof(McpOptions), typeof(IngestOptions),
            typeof(MapOptions), typeof(ViewOptions), typeof(ExportOptions), typeof(ServeOptions),
            typeof(DependenciesOptions), typeof(DepsOptions), typeof(ContractsOptions),
            typeof(OutlineOptions), typeof(SnippetsOptions), typeof(SnippetOptions),
            typeof(TraceOptions), typeof(CallChainOptions), typeof(LineageOptions),
            typeof(TestOptions), typeof(CoverageOptions), typeof(ConfigureOptions), typeof(GateOptions), typeof(CallersOptions),
            typeof(ImpactOptions), typeof(ContextOptions), typeof(SearchOptions), typeof(SymbolOptions), typeof(FindSymbolAliasOptions),
            typeof(TaxonomyOptions), typeof(SchemaOptions), typeof(DoctorOptions), typeof(DetectorOptions)
        ]);

        if (parseResult is Parsed<object> parsed)
        {
            return parsed.Value switch
            {
                InitOptions opts => await InitCommandHandler.HandleAsync(opts),
                ScanOptions opts => await ScanCommandHandler.HandleAsync(opts),
                StatusOptions opts => await StatusCommandHandler.HandleAsync(opts),
                ClearOptions opts => await ClearCommandHandler.HandleAsync(opts),
                QueriesOptions opts => await QueryCommandHandler.HandleAsync(opts),
                QueryOptions opts => await QueryCommandHandler.HandleAsync(opts),
                McpOptions opts => await McpCommandHandler.HandleAsync(opts),
                ConfigureOptions opts => await ConfigureCommandHandler.HandleAsync(opts),
                IngestOptions opts => await IngestCommandHandler.HandleAsync(opts),
                MapOptions opts => await MapCommandHandler.HandleAsync(opts),
                ExportOptions opts => await ExportCommandHandler.HandleAsync(opts),
                ServeOptions opts => await ServeCommandHandler.HandleAsync(opts),
                DependenciesOptions opts => await DependenciesCommandHandler.HandleAsync(opts),
                ContractsOptions opts => await ContractsCommandHandler.HandleAsync(opts),
                OutlineOptions opts => await OutlineCommandHandler.HandleAsync(opts),
                SnippetsOptions opts => await SnippetsCommandHandler.HandleAsync(opts),
                TraceOptions opts => await TraceCommandHandler.HandleAsync(opts),
                LineageOptions opts => await LineageCommandHandler.HandleAsync(opts),
                TestOptions opts => await TestCommandHandler.HandleAsync(opts),
                CoverageOptions opts => await CoverageCommandHandler.HandleAsync(opts),
                GateOptions opts => await GateAnomaliesCommandHandler.HandleOrDelegateAsync(opts),
                CallersOptions opts => await CallersCommandHandler.HandleAsync(opts),
                ImpactOptions opts => await ImpactCommandHandler.HandleAsync(opts),
                ContextOptions opts => await ContextCommandHandler.HandleAsync(opts),
                SearchOptions opts => await SearchCommandHandler.HandleAsync(opts),
                TaxonomyOptions opts => await TaxonomyCommandHandler.HandleAsync(opts),
                DoctorOptions opts => await DoctorCommandHandler.HandleAsync(opts),
                DetectorOptions opts => await DetectorCommandHandler.HandleAsync(opts),
                _ => 1
            };
        }

        if (parseResult is NotParsed<object> notParsed)
        {
            if (notParsed.Errors.Any(e => e.Tag is ErrorType.VersionRequestedError or
                                                  ErrorType.HelpRequestedError or
                                                  ErrorType.HelpVerbRequestedError))
            {
                return 0;
            }
        }

        return 1;
    }

    public static WebApplication CreateWebApplication(
        ServeOptions opts,
        string wsRoot,
        SqliteGraphClient client,
        string[]? args = null)
        => ServeCommandHandler.CreateWebApplication(opts, wsRoot, client, args);
}
