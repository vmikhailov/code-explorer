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

        return await CommandLineParser.Default
            .ParseArguments<
                InitOptions,
                ScanOptions,
                IndexOptions,
                StatusOptions,
                InfoOptions,
                ClearOptions,
                QueriesOptions,
                QueryOptions,
                McpOptions,
                IngestOptions,
                ExportOptions,
                ServeOptions>(args)
            .MapResult(
                (InitOptions opts) => InitCommandHandler.HandleAsync(opts),
                (ScanOptions opts) => ScanCommandHandler.HandleAsync(opts),
                (IndexOptions opts) => ScanCommandHandler.HandleAsync(opts),
                (StatusOptions opts) => StatusCommandHandler.HandleAsync(opts),
                (InfoOptions opts) => StatusCommandHandler.HandleAsync(opts),
                (ClearOptions opts) => ClearCommandHandler.HandleAsync(opts),
                (QueriesOptions opts) => QueryCommandHandler.HandleAsync(opts),
                (QueryOptions opts) => QueryCommandHandler.HandleAsync(opts),
                (McpOptions opts) => McpCommandHandler.HandleAsync(opts),
                (IngestOptions opts) => IngestCommandHandler.HandleAsync(opts),
                (ExportOptions opts) => ExportCommandHandler.HandleAsync(opts),
                (ServeOptions opts) => ServeCommandHandler.HandleAsync(opts),
                _ => Task.FromResult(1));
    }

    public static WebApplication CreateWebApplication(ServeOptions opts, string wsRoot, SqliteGraphClient client, string[]? args = null)
        => ServeCommandHandler.CreateWebApplication(opts, wsRoot, client, args);
}
