using System.Collections.Concurrent;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.Go;

public class GoFileParser : IFileParser
{
    public string LanguageName => "go";

    internal static readonly IReadOnlyList<ISemanticExtension> DefaultExtensions =
    [
        new Libraries.BigQueryGoLibraryParser(),
        new Libraries.WatermillLibraryParser(),
        new Libraries.AsynqLibraryParser(),
        new Libraries.ClickHouseGoLibraryParser(),
        new Libraries.ElasticsearchGoLibraryParser(),
        new Libraries.GinLibraryParser(),
        new Libraries.GoZeroLibraryParser(),
        new Libraries.GqlGenLibraryParser(),
        new Libraries.CentrifugeWebSocketGoLibraryParser(),
        new Libraries.ChiLibraryParser(),
        new Libraries.FiberLibraryParser(),
        new Libraries.FasthttpLibraryParser(),
        new Libraries.EchoLibraryParser(),
        new Libraries.GoRedisLegacyLibraryParser(),
        new Libraries.GoRedisLibraryParser(),
        new Libraries.GormLibraryParser(),
        new Libraries.SqlxGoLibraryParser(),
        new Libraries.EntGoLibraryParser(),
        new Libraries.BunGoLibraryParser(),
        new Libraries.AwsSdkGoLibraryParser(),
        new Libraries.TemporalGoLibraryParser(),
        new Libraries.KafkaGoLibraryParser(),
        new Libraries.MachineryRiverGoLibraryParser(),
        new Libraries.DaprGoLibraryParser(),
        new Libraries.ConnectTwirpGoLibraryParser(),
        new Libraries.GoKitKratosLibraryParser(),
        new Libraries.GorillaMuxHttpRouterLibraryParser(),
        new Libraries.SqlcGoLibraryParser(),
        new Libraries.UberFxWireGoLibraryParser(),
        new Libraries.CasbinGoLibraryParser(),
        new Libraries.OtelPrometheusGoLibraryParser(),
        new Libraries.GoSqlDriverMysqlLibraryParser(),
        new Libraries.GoSqlite3LibraryParser(),
        new Libraries.GoSqlLibraryParser(),
        new Libraries.LibPqLibraryParser(),
        new Libraries.MongoGoLibraryParser(),
        new Libraries.FirestoreGoLibraryParser(),
        new Libraries.PubSubGoLibraryParser(),
        new Libraries.RabbitMqGoLibraryParser(),
        new Libraries.NatsGoLibraryParser(),
        new Libraries.PgxLibraryParser(),
        new Libraries.RestyLibraryParser(),
        new Libraries.GoHttpClientLibraryParser(),
        new Libraries.GrpcGoLibraryParser(),
        new Libraries.GrpcGatewayGoLibraryParser(),
        new Libraries.SaramaKafkaLibraryParser(),
        new Libraries.BeegoLibraryParser(),
        Libraries.GoTestLibraryParser.Instance
    ];

    internal static readonly IReadOnlyList<PackageDescriptor> DefaultPackages =
        [.. DefaultExtensions.SelectMany(e => e.GetPackageDescriptors())];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => DefaultExtensions;

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        GoSyntaxConverter.Instance.Parse(sourceText, LanguageName, filePath, relativePath);

    public LanguageSyntaxProfile SyntaxProfile => GoSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".go", StringComparison.OrdinalIgnoreCase);
    }

    public bool ShouldSkipFileName(string fileName)
    {
        var fn = fileName.ToLowerInvariant();
        if (fn.EndsWith(".pb.go", StringComparison.Ordinal) ||
            fn.EndsWith(".pb.gw.go", StringComparison.Ordinal) ||
            fn.EndsWith("_generated.go", StringComparison.Ordinal) ||
            fn.EndsWith(".generated.go", StringComparison.Ordinal))
        {
            return true;
        }
        return IFileParser.DefaultShouldSkipFileName(fileName);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        GoDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return new GoFileVisitor(
            rootNode,
            activeExtensions,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
        );
    }

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    private readonly ConcurrentDictionary<string, string> _goModCache = new(StringComparer.OrdinalIgnoreCase);

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ResolveGoImportType(importPath, filePath);
    }

    public ImportType ResolveGoImportType(string importPath, string filePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        if (importPath.StartsWith('.') || importPath.StartsWith('/') || importPath.StartsWith('\\'))
            return ImportType.Internal;

        var dir = Path.GetDirectoryName(filePath);
        var goModFile = FindGoModFile(dir);
        if (goModFile != null)
        {
            var moduleName = _goModCache.GetOrAdd(goModFile, LoadGoModuleName);
            if (!string.IsNullOrEmpty(moduleName))
            {
                if (importPath.Equals(moduleName, StringComparison.OrdinalIgnoreCase) ||
                    importPath.StartWithIC(moduleName + "/"))
                {
                    return ImportType.Internal;
                }
            }
        }

        return ImportType.External;
    }

    private static string? FindGoModFile(string? dir)
    {
        while (dir != null)
        {
            var path = Path.Combine(dir, "go.mod");
            if (File.Exists(path))
                return path;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }

    private static string LoadGoModuleName(string goModFile)
    {
        try
        {
            var lines = PhysicalFileStore.ReadLines(goModFile);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("module ", StringComparison.Ordinal))
                {
                    return trimmed["module ".Length..].Trim();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ignore
        }
        return string.Empty;
    }
}
