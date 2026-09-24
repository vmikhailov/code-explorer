using CodeExplorer.Commands;
using CodeExplorer.Common;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser;
using CodeExplorer.Options;
using CodeExplorer.Parser.ColdFusion;
using CodeExplorer.Parser.CSharp;
using CodeExplorer.Parser.Go;
using CodeExplorer.Parser.Java;
using CodeExplorer.Parser.Python;
using CodeExplorer.Parser.SQL;
using CodeExplorer.Parser.TypeScript;
using CommandLine;
using CommandLineParser = CommandLine.Parser;
using Microsoft.AspNetCore.Builder;

namespace CodeExplorer;

public class Program
{
    public static WebApplication? App { get; set; }

    public static async Task<int> Main(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            HelpDisplay.ShowWelcomeAndHelp();
            return 0;
        }

        WorkspaceIndexer.Register(new CSharpParser());
        WorkspaceIndexer.Register(new JavaParser());
        WorkspaceIndexer.Register(new GoParser());
        WorkspaceIndexer.Register(new PythonParser());
        WorkspaceIndexer.Register(new TypeScriptParser());
        WorkspaceIndexer.Register(new JavaScriptParser());
        WorkspaceIndexer.Register(new SqlParser());
        WorkspaceIndexer.Register(new ColdFusionProjectParser());
        WorkspaceIndexer.Register(new ColdFusionFileParser());

        var parseResult = CommandLineParser.Default.ParseArguments(args, [
            typeof(InitOptions),
            typeof(ScanOptions),
            typeof(IndexOptions),
            typeof(StatusOptions),
            typeof(InfoOptions),
            typeof(ClearOptions),
            typeof(QueriesOptions),
            typeof(QueryOptions),
            typeof(McpOptions),
            typeof(IngestOptions),
            typeof(ExportOptions),
            typeof(ServeOptions),
            typeof(ViewOptions),
            typeof(DependenciesOptions),
            typeof(DepsOptions),
            typeof(ContractsOptions),
            typeof(TraceOptions)
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
                IngestOptions opts => await IngestCommandHandler.HandleAsync(opts),
                ExportOptions opts => await ExportCommandHandler.HandleAsync(opts),
                ServeOptions opts => await ServeCommandHandler.HandleAsync(opts),
                ViewOptions opts => await ViewCommandHandler.HandleAsync(opts),
                DependenciesOptions opts => await DependenciesCommandHandler.HandleAsync(opts),
                ContractsOptions opts => await ContractsCommandHandler.HandleAsync(opts),
                TraceOptions opts => await TraceCommandHandler.HandleAsync(opts),
                _ => 1
            };
        }

        return 1;
    }

    public static WebApplication CreateWebApplication(ServeOptions opts, string wsRoot, SqliteGraphClient client, string[]? args = null)
        => ServeCommandHandler.CreateWebApplication(opts, wsRoot, client, args);
}
