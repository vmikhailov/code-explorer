using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Java;

public class JavaParser : IProjectParser, IFileParser
{
    public string LanguageName => "java";

    public string ProjectType => "java";

    public IReadOnlyCollection<string> ExcludedFolders =>
    [
        "target", "build", ".gradle", ".mvn", "bin", "out", ".idea", ".settings", ".metadata"
    ];

    public IReadOnlyList<ILibraryParser> LibraryParsers { get; } =
    [
        new Libraries.SpringMvcLibraryParser(),
        new Libraries.SpringGraphQlLibraryParser(),
        new Libraries.GrpcJavaLibraryParser(),
        new Libraries.SpringEventsLibraryParser(),
        new Libraries.JpaLibraryParser(),
        new Libraries.JdbcTemplateLibraryParser(),
        new Libraries.HttpClientJavaLibraryParser(),

        // Databases & ORM
        new GenericLibraryParser("postgres", "PostgreSQL", "db:relational", ["org.postgresql"]),
        new GenericLibraryParser("mysql", "MySQL", "db:relational", ["com.mysql.cj.jdbc", "com.mysql.jdbc"]),
        new GenericLibraryParser("oracle", "Oracle DB", "db:relational", ["oracle.jdbc", "com.oracle.database.jdbc"]),
        new GenericLibraryParser("h2", "H2 Database", "db:relational", ["org.h2"]),
        new GenericLibraryParser("sqlite", "SQLite", "db:relational", ["org.sqlite"]),
        new GenericLibraryParser("clickhouse", "ClickHouse", "db:analytics", ["com.clickhouse.jdbc", "com.clickhouse"]),
        new GenericLibraryParser("bigquery", "BigQuery", "db:analytics", ["com.google.cloud.bigquery"]),
        new GenericLibraryParser("mongodb", "MongoDB", "db:document", ["org.springframework.data.mongodb", "com.mongodb"]),
        new GenericLibraryParser("redis", "Redis", "db:cache", ["org.springframework.data.redis", "redis.clients.jedis", "io.lettuce"]),
        new GenericLibraryParser("elasticsearch", "Elasticsearch", "db:search", ["org.elasticsearch", "co.elastic.clients", "org.opensearch"]),
        new GenericLibraryParser("cassandra", "Cassandra", "db:nosql", ["com.datastax.oss", "org.springframework.data.cassandra"]),
        new GenericLibraryParser("mybatis", "MyBatis", "db:relational", ["org.mybatis", "org.apache.ibatis"]),

        // Messaging & Events
        new GenericLibraryParser("kafka", "Apache Kafka", "messaging", ["org.apache.kafka", "org.springframework.kafka"]),
        new GenericLibraryParser("rabbitmq", "RabbitMQ", "messaging", ["com.rabbitmq", "org.springframework.amqp"]),
        new GenericLibraryParser("pulsar", "Apache Pulsar", "messaging", ["org.apache.pulsar"]),
        new GenericLibraryParser("activemq", "ActiveMQ", "messaging", ["org.apache.activemq"]),

        // Cloud & External Services
        new GenericLibraryParser("aws", "AWS Java SDK", "cloud", ["software.amazon.awssdk", "com.amazonaws"]),
        new GenericLibraryParser("gcp", "GCP Java SDK", "cloud", ["com.google.cloud", "com.google.firebase"]),
        new GenericLibraryParser("azure", "Azure Java SDK", "cloud", ["com.azure", "com.microsoft.azure"]),
        new GenericLibraryParser("stripe", "Stripe Java", "cloud", ["com.stripe"]),

        // Frameworks
        new GenericLibraryParser("spring-boot", "Spring Boot", "framework", ["org.springframework.boot", "org.springframework"]),
        new GenericLibraryParser("quarkus", "Quarkus", "framework", ["io.quarkus"]),
        new GenericLibraryParser("micronaut", "Micronaut", "framework", ["io.micronaut"]),
        new GenericLibraryParser("vertx", "Eclipse Vert.x", "framework", ["io.vertx"])
    ];

    public bool UsesTreeSitter => true;

    public LanguageSyntaxProfile SyntaxProfile => JavaSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".java", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsProjectDirectory(string directoryPath, string[] filesInDirectory)
    {
        foreach (var file in filesInDirectory)
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            if (fileName is "pom.xml" or "build.gradle" or "build.gradle.kts" or "settings.gradle" or "settings.gradle.kts")
            {
                return true;
            }
        }
        return false;
    }

    public Task<ProducedPackageInfo?> GetProducedPackageAsync(string projectDirectory)
    {
        var pomPath = Path.Combine(projectDirectory, "pom.xml");
        if (File.Exists(pomPath))
        {
            try
            {
                var doc = XDocument.Load(pomPath);
                var root = doc.Root;
                if (root != null)
                {
                    var ns = root.GetDefaultNamespace();
                    var artifactId = root.Element(ns + "artifactId")?.Value?.Trim();
                    var groupId = root.Element(ns + "groupId")?.Value?.Trim()
                                  ?? root.Element(ns + "parent")?.Element(ns + "groupId")?.Value?.Trim();
                    var version = root.Element(ns + "version")?.Value?.Trim()
                                  ?? root.Element(ns + "parent")?.Element(ns + "version")?.Value?.Trim()
                                  ?? "1.0.0";

                    if (!string.IsNullOrEmpty(artifactId))
                    {
                        var pkgName = !string.IsNullOrEmpty(groupId) ? $"{groupId}:{artifactId}" : artifactId;
                        return Task.FromResult<ProducedPackageInfo?>(new ProducedPackageInfo(pkgName, version, "maven"));
                    }
                }
            }
            catch
            {
                // Fallback if malformed XML
            }
        }

        var buildGradle = Path.Combine(projectDirectory, "build.gradle");
        var buildGradleKts = Path.Combine(projectDirectory, "build.gradle.kts");
        var gradleFile = File.Exists(buildGradle) ? buildGradle : (File.Exists(buildGradleKts) ? buildGradleKts : null);

        if (gradleFile != null)
        {
            try
            {
                var text = File.ReadAllText(gradleFile);
                var groupMatch = Regex.Match(text, @"group\s*=\s*['""]([^'""]+)['""]");
                var versionMatch = Regex.Match(text, @"version\s*=\s*['""]([^'""]+)['""]");

                var group = groupMatch.Success ? groupMatch.Groups[1].Value : "";
                var version = versionMatch.Success ? versionMatch.Groups[1].Value : "1.0.0";
                var name = Path.GetFileName(projectDirectory);

                var pkgName = !string.IsNullOrEmpty(group) ? $"{group}:{name}" : name;
                return Task.FromResult<ProducedPackageInfo?>(new ProducedPackageInfo(pkgName, version, "gradle"));
            }
            catch
            {
                // Fallback
            }
        }

        var folderName = Path.GetFileName(projectDirectory);
        return Task.FromResult<ProducedPackageInfo?>(new ProducedPackageInfo(folderName, "1.0.0", "java"));
    }

    public Task<ProjectDependencyInfo> ParseDependenciesAsync(string projectDirectory)
    {
        var localProjects = new List<string>();
        var externalPackages = new List<ProducedPackageInfo>();

        // 1. Maven parsing
        var pomPath = Path.Combine(projectDirectory, "pom.xml");
        if (File.Exists(pomPath))
        {
            try
            {
                var doc = XDocument.Load(pomPath);
                var root = doc.Root;
                if (root != null)
                {
                    var ns = root.GetDefaultNamespace();

                    // Modules (child projects in Maven multi-module builds)
                    var modulesElem = root.Element(ns + "modules");
                    if (modulesElem != null)
                    {
                        foreach (var moduleElem in modulesElem.Elements(ns + "module"))
                        {
                            var moduleRelPath = moduleElem.Value?.Trim();
                            if (!string.IsNullOrEmpty(moduleRelPath))
                            {
                                var fullModuleDir = Path.GetFullPath(Path.Combine(projectDirectory, moduleRelPath)).Replace('\\', '/');
                                localProjects.Add(fullModuleDir);
                            }
                        }
                    }

                    // Dependencies
                    var depsElem = root.Element(ns + "dependencies");
                    if (depsElem != null)
                    {
                        foreach (var dep in depsElem.Elements(ns + "dependency"))
                        {
                            var g = dep.Element(ns + "groupId")?.Value?.Trim();
                            var a = dep.Element(ns + "artifactId")?.Value?.Trim();
                            var v = dep.Element(ns + "version")?.Value?.Trim() ?? "unknown";

                            if (!string.IsNullOrEmpty(g) && !string.IsNullOrEmpty(a))
                            {
                                externalPackages.Add(new ProducedPackageInfo($"{g}:{a}", v, "maven"));
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore XML parse errors
            }
        }

        // 2. Gradle parsing
        var buildGradle = Path.Combine(projectDirectory, "build.gradle");
        var buildGradleKts = Path.Combine(projectDirectory, "build.gradle.kts");
        var settingsGradle = Path.Combine(projectDirectory, "settings.gradle");
        var settingsGradleKts = Path.Combine(projectDirectory, "settings.gradle.kts");

        // Subproject discovery from settings.gradle
        var settingsFile = File.Exists(settingsGradle) ? settingsGradle : (File.Exists(settingsGradleKts) ? settingsGradleKts : null);
        if (settingsFile != null)
        {
            try
            {
                var settingsContent = File.ReadAllText(settingsFile);
                var includeMatches = Regex.Matches(settingsContent, @"include\s+['"":]([a-zA-Z0-9_-]+)['""]");
                foreach (Match match in includeMatches)
                {
                    var subName = match.Groups[1].Value.TrimStart(':');
                    var subDir = Path.GetFullPath(Path.Combine(projectDirectory, subName)).Replace('\\', '/');
                    if (Directory.Exists(subDir))
                    {
                        localProjects.Add(subDir);
                    }
                }
            }
            catch
            {
            }
        }

        var gradleFile = File.Exists(buildGradle) ? buildGradle : (File.Exists(buildGradleKts) ? buildGradleKts : null);
        if (gradleFile != null)
        {
            try
            {
                var content = File.ReadAllText(gradleFile);

                // Project dependencies: project(':subproject') or project(":subproject")
                var projectDepMatches = Regex.Matches(content, @"project\s*\(\s*['""]:?([a-zA-Z0-9_-]+)['""]\s*\)");
                foreach (Match match in projectDepMatches)
                {
                    var subName = match.Groups[1].Value.TrimStart(':');
                    var subDir = Path.GetFullPath(Path.Combine(projectDirectory, subName)).Replace('\\', '/');
                    if (Directory.Exists(subDir))
                    {
                        localProjects.Add(subDir);
                    }
                }

                // External dependencies: implementation 'group:artifact:version' or implementation("group:artifact:version")
                var depMatches = Regex.Matches(content, @"(?:implementation|api|compileOnly|runtimeOnly|testImplementation)\s*[\('""\s]+([a-zA-Z0-9_.-]+):([a-zA-Z0-9_.-]+)(?::([a-zA-Z0-9_.-]+))?['""\)]");
                foreach (Match match in depMatches)
                {
                    var g = match.Groups[1].Value;
                    var a = match.Groups[2].Value;
                    var v = match.Groups[3].Success ? match.Groups[3].Value : "unknown";
                    externalPackages.Add(new ProducedPackageInfo($"{g}:{a}", v, "gradle"));
                }
            }
            catch
            {
            }
        }

        return Task.FromResult(new ProjectDependencyInfo(
            localProjects.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            externalPackages
        ));
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ILibraryParser> activeLibraryParsers,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        LibraryTrieRegistry libraryRegistry)
    {
        return new JavaFileVisitor(
            rootNode,
            activeLibraryParsers,
            this,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            libraryRegistry
        );
    }

    public async Task<SyntaxTree> ParseAsync(
        string filePath,
        string parentNodeId,
        string workspaceId,
        string absoluteWorkspacePath)
    {
        var relativePath = Path.GetRelativePath(absoluteWorkspacePath, filePath).Replace('\\', '/');
        return await SyntaxTree.ParseAsync(filePath, relativePath, parentNodeId, this, workspaceId, absoluteWorkspacePath);
    }

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(LibraryParsers, syntaxTree);

    private readonly ConcurrentDictionary<string, HashSet<string>> _workspacePackagesCache = new(StringComparer.OrdinalIgnoreCase);

    public ImportType ResolveImportType(string importPath, string filePath, string? absoluteWorkspacePath)
    {
        if (string.IsNullOrEmpty(importPath)) return ImportType.External;

        // Java standard library packages
        if (importPath.StartsWith("java.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("javax.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("jakarta.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("sun.", StringComparison.OrdinalIgnoreCase) ||
            importPath.StartsWith("com.sun.", StringComparison.OrdinalIgnoreCase))
        {
            return ImportType.External;
        }

        if (!string.IsNullOrEmpty(absoluteWorkspacePath))
        {
            // Check if matches any internal workspace source package directory
            var importRelDir = importPath.Replace('.', '/');
            if (importRelDir.Contains('/'))
            {
                var dirPart = importRelDir[..importRelDir.LastIndexOf('/')];
                var possiblePath = Path.Combine(absoluteWorkspacePath, "src/main/java", dirPart);
                if (Directory.Exists(possiblePath)) return ImportType.Internal;

                possiblePath = Path.Combine(absoluteWorkspacePath, "src", dirPart);
                if (Directory.Exists(possiblePath)) return ImportType.Internal;
            }
        }

        return ImportType.External;
    }
}
