using System.Collections.Concurrent;
using CodeExplorer.Core.Database;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parser.TypeScript;

public class TypeScriptFileParser : IFileParser
{
    public string LanguageName => "typescript";

    public bool ShouldSkipFileName(string fileName)
    {
        var fn = fileName.ToLowerInvariant();
        if (fn.EndsWith(".d.ts") || fn.EndsWith(".d.mts") || fn.EndsWith(".d.cts") ||
            fn.EndsWith(".min.js") || fn.EndsWith(".min.mjs") || fn.EndsWith(".min.cjs") ||
            fn.EndsWith(".bundle.js") || fn.EndsWith(".bundle.min.js") ||
            fn.Contains(".min.") ||
            fn.EndsWith("_debug.ts") || fn.EndsWith("_debug.js"))
        {
            return true;
        }

        return IFileParser.DefaultShouldSkipFileName(fileName);
    }

    public bool ShouldSkipFileBytes(string fileName, ReadOnlySpan<byte> contentBytes) =>
        WorkspaceFileFilter.IsMinifiedBytes(contentBytes, maxLineLength: 5000);

    public bool ShouldSkipFile(FileInfo fileInfo)
    {
        if (PhysicalFileStore.TryGetFileText(fileInfo.FullName, out var cachedText))
        {
            return WorkspaceFileFilter.IsMinifiedText(cachedText, maxLineLength: 5000);
        }
        return false;
    }

    public string ClassifyArtifactRole(string relativePath, string fileName, ReadOnlySpan<byte> contentBytes = default)
    {
        var normPath = relativePath.NormalizeSlashes();
        var lowerFile = fileName.ToLowerInvariant();
        if (lowerFile.EndsWith(".d.ts") || lowerFile.EndsWith(".d.mts") || lowerFile.EndsWith(".d.cts"))
        {
            return "declaration";
        }
        if (lowerFile.Contains(".min.") || lowerFile.Contains(".bundle.") || WorkspaceFileFilter.IsMinifiedBytes(contentBytes, maxLineLength: 500))
        {
            return "minified";
        }
        var padded = "/" + normPath.TrimStart('/');
        if (padded.Contains("/dist/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/build/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/out/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/umd/", StringComparison.OrdinalIgnoreCase) ||
            padded.Contains("/amd/", StringComparison.OrdinalIgnoreCase))
        {
            return "dist";
        }
        return "source";
    }

    public void EnrichFileSignals(
        CodeExplorer.Core.Common.Nodes.Layer1_Physical.FileNode fileNode,
        SyntaxTree syntaxTree,
        CodeExplorer.Core.Common.Nodes.Layer2_Boundaries.ProjectNode? project,
        string sourceText)
    {
        if (project == null ||
            (!fileNode.Name.Contains("config", StringComparison.OrdinalIgnoreCase) &&
             !fileNode.Name.Contains("datasource", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        const string FamilyRelational = "relational";
        var projSignals = CodeExplorer.Core.Common.Nodes.AnalysisSignalStore.Signals(project);
        if (!string.IsNullOrEmpty(projSignals.DbDriverEngine)) return;

        var lower = sourceText.ToLowerInvariant();
        if (lower.Contains("type: 'postgres'") || lower.Contains("type: \"postgres\"") ||
            lower.Contains("dialect: 'postgres'") || lower.Contains("dialect: \"postgres\""))
        {
            projSignals.DbDriverEngine = "PostgreSQL";
            projSignals.DbDriverFamily = FamilyRelational;
        }
        else if (lower.Contains("type: 'mysql'") || lower.Contains("type: \"mysql\"") ||
                 lower.Contains("dialect: 'mysql'") || lower.Contains("dialect: \"mysql\""))
        {
            projSignals.DbDriverEngine = "MySQL";
            projSignals.DbDriverFamily = FamilyRelational;
        }
        else if (lower.Contains("type: 'sqlite'") || lower.Contains("type: \"sqlite\"") ||
                 lower.Contains("dialect: 'sqlite'") || lower.Contains("dialect: \"sqlite\""))
        {
            projSignals.DbDriverEngine = "SQLite";
            projSignals.DbDriverFamily = FamilyRelational;
        }
    }

    internal static readonly IReadOnlyList<ISemanticExtension> DefaultExtensions =
    [
        new Libraries.AxiosLibraryParser(),
        new Libraries.ElasticsearchTsLibraryParser(),
        new Libraries.InfluxDbLibraryParser(),
        new Libraries.KnexLibraryParser(),
        new Libraries.MongodbLibraryParser(),
        new Libraries.MongooseLibraryParser(),
        new Libraries.Mysql2LibraryParser(),
        new Libraries.Neo4jLibraryParser(),
        new Libraries.PgLibraryParser(),
        new Libraries.PrismaLibraryParser(),
        new Libraries.DrizzleLibraryParser(),
        new Libraries.MikroOrmKyselyLibraryParser(),
        new Libraries.TemporalTypeScriptLibraryParser(),
        new Libraries.AiSdkTypeScriptLibraryParser(),
        new Libraries.TestingLibraryParser(),
        new Libraries.NatsMqttTsLibraryParser(),
        new Libraries.EncoreSstLibraryParser(),
        new Libraries.MetaFrameworkRoutesLibraryParser(),
        new Libraries.CloudflareWorkersLibraryParser(),
        new Libraries.EffectTsLibraryParser(),
        new Libraries.GraphQlClientTsLibraryParser(),
        new Libraries.TanStackQuerySwrLibraryParser(),
        new Libraries.AuthTsLibraryParser(),
        new Libraries.SupabaseFirebaseTsLibraryParser(),
        new Libraries.PlaywrightCypressLibraryParser(),
        new Libraries.RedisLibraryParser(),
        new Libraries.GrpcTypeScriptLibraryParser(),
        new Libraries.SequelizeLibraryParser(),
        new Libraries.Sqlite3LibraryParser(),
        new Libraries.TypeOrmLibraryParser(),
        new Libraries.GcpLibraryParser(),
        new Libraries.RabbitMqLibraryParser(),
        new Libraries.KafkaJsLibraryParser(),
        new Libraries.BullMqLibraryParser(),
        new Libraries.BullLegacyLibraryParser(),
        new Libraries.InngestLibraryParser(),
        new Libraries.NestJsLibraryParser(),
        new Libraries.NestJsCqrsLibraryParser(),
        new Libraries.RoutingControllersLibraryParser(),
        new Libraries.ExpressLibraryParser(),
        new Libraries.HonoLibraryParser(),
        new Libraries.TrpcLibraryParser(),
        new Libraries.RemixLibraryParser(),
        new Libraries.GraphQlYogaApolloLibraryParser(),
        new Libraries.FastifyLibraryParser(),
        new Libraries.KoaLibraryParser(),
        new Libraries.NextJsLibraryParser(),
        new Libraries.FetchLibraryParser(),
        new Libraries.GotKyLibraryParser(),
        new Libraries.RequestLibraryParser(),
        new Libraries.UndiciLibraryParser(),
        new Libraries.UrllibLibraryParser(),
        new Libraries.SocketIoLibraryParser(),
        new Libraries.AngularHttpLibraryParser(),
        new Libraries.AngularOidcLibraryParser(),
        new Libraries.SignalRLibraryParser(),
        new Libraries.GraphQLClientLibraryParser(),
        new Libraries.PothosLibraryParser(),
        new Libraries.ClickHouseLibraryParser(),
        new Libraries.BigQueryLibraryParser(),
        new Libraries.StripeLibraryParser(),
        new Libraries.AwsSdkLibraryParser(),
        new Libraries.AzureSdkLibraryParser(),
        new Libraries.ReactLibraryParser(),
        new Libraries.VueLibraryParser(),
        new Libraries.AngularCoreLibraryParser(),
        new Libraries.SvelteLibraryParser(),
        new Libraries.ViteLibraryParser(),
        new Libraries.VsCodeLibraryParser(),
        new Libraries.JestVitestLibraryParser(),
        new Libraries.MochaLibraryParser(),
        new Libraries.CypressLibraryParser(),
        new Libraries.PlaywrightLibraryParser()
    ];

    internal static readonly IReadOnlyList<PackageDescriptor> DefaultPackages =
        [.. DefaultExtensions.SelectMany(e => e.GetPackageDescriptors())];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions => DefaultExtensions;

    public bool UsesTreeSitter => true;

    public SyntaxDocument? ParseDocument(string sourceText, string filePath = "", string relativePath = "") =>
        TypeScriptSyntaxConverter.Instance.Parse(
            sourceText,
            filePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) || relativePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase) ? "tsx" : LanguageName,
            filePath,
            relativePath);

    public LanguageSyntaxProfile SyntaxProfile => TypeScriptSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".mts", StringComparison.OrdinalIgnoreCase) ||
               fileExtension.Equals(".cts", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        TypeScriptDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return new TypeScriptFileVisitor(
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

    private readonly ConcurrentDictionary<string, HashSet<string>> _tsDepsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string?> _dirToProjectDirCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> NodeBuiltInModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "fs", "path", "os", "http", "https", "crypto", "child_process", "dns", "events", "net", "stream", "util", "url", "zlib"
    };

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        return ResolveTsImportType(importPath, filePath, absoluteWorkspacePath);
    }

    public ImportType ResolveTsImportType(string importPath, string filePath, string? absoluteWorkspacePath = null)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        if (importPath.StartsWith('.') || importPath.StartsWith('/') || importPath.StartsWith('\\'))
            return ImportType.Internal;

        if (importPath.StartsWith("@/"))
            return ImportType.Internal;

        // If it matches any registered library parser, it is an external library
        var firstPart = importPath.Contains('/') ? importPath.Split('/')[0] : importPath;
        if (DefaultPackages.Any(p => p.SupportedPatterns.Any(pat =>
            pat.Equals(importPath, StringComparison.OrdinalIgnoreCase) ||
            pat.Equals(firstPart, StringComparison.OrdinalIgnoreCase) ||
            pat.StartWithIC(firstPart + "/"))))
        {
            return ImportType.External;
        }

        var fullFilePath = Path.IsPathRooted(filePath)
            ? filePath
            : (!string.IsNullOrEmpty(absoluteWorkspacePath) ? Path.Combine(absoluteWorkspacePath, filePath).Replace('\\', '/') : filePath);

        var dir = Path.GetDirectoryName(fullFilePath);
        var projectDir = FindProjectDirectoryWithPackageJson(dir);
        if (projectDir != null)
        {
            var deps = _tsDepsCache.GetOrAdd(projectDir, LoadPackageJsonDependencies);

            if (importPath.StartsWith('@'))
            {
                var parts = importPath.Split('/');
                if (parts.Length < 2) return ImportType.Internal;

                var scopeAndPackage = $"{parts[0]}/{parts[1]}";
                if (deps.Contains(scopeAndPackage) || deps.Contains(importPath))
                    return ImportType.External;

                if (deps.Any(d => d.StartsWith(parts[0] + "/")))
                    return ImportType.External;

                if (deps.Count == 0)
                    return ImportType.External;

                return ImportType.Internal;
            }
            else
            {
                var parts = importPath.Split('/');
                var firstSegment = parts[0];
                if (deps.Contains(firstSegment) || deps.Contains(importPath))
                    return ImportType.External;

                if (NodeBuiltInModules.Contains(firstSegment))
                    return ImportType.External;

                if (deps.Count == 0)
                    return ImportType.External;

                return ImportType.Internal;
            }
        }

        return ImportType.External;
    }

    private string? FindProjectDirectoryWithPackageJson(string? dir)
    {
        if (string.IsNullOrEmpty(dir)) return null;
        return _dirToProjectDirCache.GetOrAdd(dir, static startDir =>
        {
            var current = startDir;
            while (current != null)
            {
                if (File.Exists(Path.Combine(current, "package.json")))
                    return current;
                current = Path.GetDirectoryName(current);
            }
            return null;
        });
    }

    private static HashSet<string> LoadPackageJsonDependencies(string projectDir)
    {
        var deps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var path = Path.Combine(projectDir, "package.json");
        if (!File.Exists(path)) return deps;

        try
        {
            var content = PhysicalFileStore.ReadAllText(path);
            var root = CypherValue.TryParseJsonText(content);
            var depProperties = new[] { "dependencies", "devDependencies" };

            foreach (var propName in depProperties)
            {
                if (root.TryGetProperty(propName, out var depsObj) &&
                    depsObj.Kind == CypherValueKind.Object)
                {
                    foreach (var prop in depsObj.AsObject())
                    {
                        deps.Add(prop.Key);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            // Ignore
        }
        return deps;
    }
}
