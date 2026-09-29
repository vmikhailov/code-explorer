using System.Collections.Concurrent;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Go;

public class GoParser : IProjectParser, IFileParser
{
    public string LanguageName => "go";

    public string ProjectType => "go";

    public IReadOnlyCollection<string> ExcludedFolders => ["vendor"];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith(".env");
    }

    public IReadOnlyList<PackageDescriptor> Packages { get; } =
    [
        new PackageDescriptor("elasticsearch", "Elasticsearch", LibraryRole.OrmOrDatabase, "go", ["github.com/elastic/go-elasticsearch*"]),
        new PackageDescriptor("redis", "Redis", LibraryRole.OrmOrDatabase, "go", ["github.com/go-redis/redis*", "gopkg.in/redis*"]),
        new PackageDescriptor("gorm", "GORM", LibraryRole.OrmOrDatabase, "go", ["gorm.io/gorm*"]),
        new PackageDescriptor("mysql", "MySQL", LibraryRole.OrmOrDatabase, "go", ["github.com/go-sql-driver/mysql*"]),
        new PackageDescriptor("sqlite3", "SQLite3", LibraryRole.OrmOrDatabase, "go", ["github.com/mattn/go-sqlite3*"]),
        new PackageDescriptor("sql", "database/sql", LibraryRole.OrmOrDatabase, "go", ["database/sql*"]),
        new PackageDescriptor("libpq", "PostgreSQL (lib/pq)", LibraryRole.OrmOrDatabase, "go", ["github.com/lib/pq*"]),
        new PackageDescriptor("mongo", "MongoDB", LibraryRole.OrmOrDatabase, "go", ["go.mongodb.org/mongo-driver*"]),

        // Additional Databases
        new PackageDescriptor("clickhouse", "ClickHouse", LibraryRole.OrmOrDatabase, "go", ["github.com/ClickHouse/clickhouse-go*"]),
        new PackageDescriptor("postgres-pgx", "PostgreSQL (pgx)", LibraryRole.OrmOrDatabase, "go", ["github.com/jackc/pgx*"]),
        new PackageDescriptor("bigquery", "BigQuery", LibraryRole.OrmOrDatabase, "go", ["cloud.google.com/go/bigquery*"]),

        // Additional Cloud & Message Services
        new PackageDescriptor("pubsub", "Google Cloud Pub/Sub", LibraryRole.MessageBroker, "go", ["cloud.google.com/go/pubsub*"]),
        new PackageDescriptor("rabbitmq", "RabbitMQ", LibraryRole.MessageBroker, "go", ["github.com/streadway/amqp*", "github.com/rabbitmq/amqp091-go*"]),

        // Generic Cloud Services
        new PackageDescriptor("stripe", "Stripe", LibraryRole.CloudSdk, "go", ["github.com/stripe/stripe-go*"]),
        new PackageDescriptor("aws", "AWS", LibraryRole.CloudSdk, "go", ["github.com/aws/aws-sdk-go*"]),
        new PackageDescriptor("gcp", "GCP", LibraryRole.CloudSdk, "go", ["cloud.google.com/*", "firebase.google.com/*"]),
        new PackageDescriptor("azure", "Azure", LibraryRole.CloudSdk, "go", ["*Azure*", "*azure-sdk-for-go*"]),

        // Frameworks
        new PackageDescriptor("gin", "Gin", LibraryRole.WebService, "go", ["github.com/gin-gonic/gin*"]),
        new PackageDescriptor("echo", "Echo", LibraryRole.WebService, "go", ["github.com/labstack/echo*"]),
        new PackageDescriptor("fiber", "Fiber", LibraryRole.WebService, "go", ["github.com/gofiber/fiber*"]),

        // Test Frameworks
        new PackageDescriptor("testify", "Testify", LibraryRole.TestFramework, "go", ["github.com/stretchr/testify*"]),

        // API Clients
        new PackageDescriptor("net/http", "http/https", LibraryRole.General, "go", ["net/http*"]),
        new PackageDescriptor("resty", "Resty", LibraryRole.General, "go", ["github.com/go-resty/resty*"]),
        new PackageDescriptor("req", "req", LibraryRole.General, "go", ["github.com/imroc/req*"]),
        new PackageDescriptor("grequests", "grequests", LibraryRole.General, "go", ["github.com/levigross/grequests*"]),
        new PackageDescriptor("gorequest", "gorequest", LibraryRole.General, "go", ["github.com/parnurzeal/gorequest*"]),
        new PackageDescriptor("surf", "surf", LibraryRole.General, "go", ["github.com/go-surf/surf*"])
    ];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
    [
        new Libraries.ElasticsearchGoLibraryParser(),
        new Libraries.GoRedisLegacyLibraryParser(),
        new Libraries.GoRedisLibraryParser(),
        new Libraries.GormLibraryParser(),
        new Libraries.GoSqlDriverMysqlLibraryParser(),
        new Libraries.GoSqlite3LibraryParser(),
        new Libraries.GoSqlLibraryParser(),
        new Libraries.LibPqLibraryParser(),
        new Libraries.MongoGoLibraryParser(),
        new Libraries.PubSubGoLibraryParser(),
        new Libraries.RabbitMqGoLibraryParser()
    ];

    public bool UsesTreeSitter => true;

    public LanguageSyntaxProfile SyntaxProfile => GoSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".go", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        GoDeclarationExtractor.Extract(rootNode, registerDeclaration);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName == "go.mod")
            {
                return true;
            }
        }
        return false;
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
            this,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
        );
    }

    public async Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath)) return null;

        try
        {
            var lines = await File.ReadAllLinesAsync(goModPath);
            var moduleLine = lines.FirstOrDefault(l => l.Trim().StartsWith("module ", StringComparison.Ordinal));
            if (moduleLine != null)
            {
                var modName = moduleLine.Trim()["module ".Length..].Trim();
                if (!string.IsNullOrEmpty(modName))
                {
                    var version = "unknown";
                    var versionFilePath = Path.Combine(projectDirectory, "VERSION");
                    if (File.Exists(versionFilePath))
                    {
                        version = (await File.ReadAllTextAsync(versionFilePath)).Trim();
                    }
                    return new ProducedPackageInfo(modName, version, "go");
                }
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    public async Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjectPaths = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        var goModPath = Path.Combine(projectDirectory, "go.mod");
        if (!File.Exists(goModPath))
        {
            return new ProjectDependencyInfo(localProjectPaths, externalPackages);
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(goModPath);
            var inRequireBlock = false;
            var inReplaceBlock = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//", StringComparison.Ordinal)) continue;

                // Handle replace directives (e.g. "replace example.com/pkg => ../pkg" or inside "replace (...)")
                if (line.StartsWith("replace (", StringComparison.Ordinal))
                {
                    inReplaceBlock = true;
                    continue;
                }
                if (inReplaceBlock && line == ")")
                {
                    inReplaceBlock = false;
                    continue;
                }

                if (inReplaceBlock || line.StartsWith("replace ", StringComparison.Ordinal))
                {
                    var replaceContent = line.StartsWith("replace ", StringComparison.Ordinal)
                        ? line["replace ".Length..].Trim()
                        : line;

                    if (replaceContent.Contains("=>"))
                    {
                        var arrowIdx = replaceContent.IndexOf("=>", StringComparison.Ordinal);
                        var target = replaceContent[(arrowIdx + 2)..].Trim();
                        var targetParts = target.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                        if (targetParts.Length >= 1)
                        {
                            var targetPath = targetParts[0];
                            if (targetPath.StartsWith('.') || targetPath.StartsWith('/') || targetPath.StartsWith('\\'))
                            {
                                localProjectPaths.Add(targetPath);
                            }
                        }
                    }
                    continue;
                }

                // Handle single-line require
                if (line.StartsWith("require ", StringComparison.Ordinal) && !line.EndsWith('('))
                {
                    var content = line["require ".Length..].Trim();
                    var parts = content.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : "1.0.0";
                        externalPackages.Add(new ProducedPackageInfo(name, version, "go"));
                    }
                }
                else if (line.StartsWith("require (", StringComparison.Ordinal))
                {
                    inRequireBlock = true;
                }
                else if (line == ")")
                {
                    inRequireBlock = false;
                }
                else if (inRequireBlock)
                {
                    // Line inside require block
                    var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        var name = parts[0];
                        var version = parts.Length >= 2 ? parts[1] : "1.0.0";
                        externalPackages.Add(new ProducedPackageInfo(name, version, "go"));
                    }
                }
            }
        }
        catch
        {
            // Ignore
        }

        return new ProjectDependencyInfo(localProjectPaths, externalPackages);
    }

    public async Task<SyntaxTree> ParseAsync(string filePath, string parentNodeId, string workspaceId, string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(SemanticExtensions, syntaxTree, Packages);

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
            var moduleName = _goModCache.GetOrAdd(goModFile, f => LoadGoModuleName(f));
            if (!string.IsNullOrEmpty(moduleName))
            {
                if (importPath.Equals(moduleName, StringComparison.OrdinalIgnoreCase) ||
                    importPath.StartsWith(moduleName + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return ImportType.Internal;
                }
            }
        }

        return ImportType.External;
    }

    private string? FindGoModFile(string? dir)
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

    private string LoadGoModuleName(string goModFile)
    {
        try
        {
            var lines = File.ReadLines(goModFile);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("module ", StringComparison.Ordinal))
                {
                    return trimmed["module ".Length..].Trim();
                }
            }
        }
        catch
        {
            // Ignore
        }
        return string.Empty;
    }

}
