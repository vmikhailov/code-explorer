using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeExplorer.Core.Parser;
using TreeSitter;

[assembly: ParserAssembly]

namespace CodeExplorer.Parser.Java;

public class JavaParser : IProjectParser, IFileParser
{
    static JavaParser()
    {
        LibraryConfigurationRegistry.Register(new Libraries.SpringFrameworkConfigurationDescriptor());
    }

    public JavaParser()
    {
        LibraryConfigurationRegistry.Register(new Libraries.SpringFrameworkConfigurationDescriptor());
    }

    public string LanguageName => "java";

    public string ProjectType => "java";

    public IReadOnlyCollection<string> ExcludedFolders =>
    [
        "target", "build", ".gradle", ".mvn", "bin", "out", ".idea", ".settings", ".metadata"
    ];

    public bool IsConfigurationFile(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return lower.StartsWith("application") && (lower.EndsWith(".properties") || lower.EndsWith(".yml") || lower.EndsWith(".yaml"));
    }

    public IReadOnlyList<ILibraryConfigurationDescriptor> ConfigurationDescriptors { get; } =
    [
        new Libraries.SpringFrameworkConfigurationDescriptor()
    ];

    public IReadOnlyList<PackageDescriptor> Packages { get; } =
    [
        // Frameworks
        new PackageDescriptor("spring-boot", "Spring Boot", LibraryRole.WebFramework, "maven", ["org.springframework.boot", "org.springframework"]),
        new PackageDescriptor("quarkus", "Quarkus", LibraryRole.WebFramework, "maven", ["io.quarkus"]),
        new PackageDescriptor("micronaut", "Micronaut", LibraryRole.WebFramework, "maven", ["io.micronaut"]),
        new PackageDescriptor("vertx", "Eclipse Vert.x", LibraryRole.WebFramework, "maven", ["io.vertx"]),
        new PackageDescriptor("grpc", "gRPC Java", LibraryRole.WebFramework, "maven", ["io.grpc"]),

        // Databases & ORM
        new PackageDescriptor("postgres", "PostgreSQL", LibraryRole.OrmOrDatabase, "maven", ["org.postgresql"]),
        new PackageDescriptor("mysql", "MySQL", LibraryRole.OrmOrDatabase, "maven", ["com.mysql.cj.jdbc", "com.mysql.jdbc"]),
        new PackageDescriptor("oracle", "Oracle DB", LibraryRole.OrmOrDatabase, "maven", ["oracle.jdbc", "com.oracle.database.jdbc"]),
        new PackageDescriptor("h2", "H2 Database", LibraryRole.OrmOrDatabase, "maven", ["org.h2"]),
        new PackageDescriptor("sqlite", "SQLite", LibraryRole.OrmOrDatabase, "maven", ["org.sqlite"]),
        new PackageDescriptor("clickhouse", "ClickHouse", LibraryRole.OrmOrDatabase, "maven", ["com.clickhouse.jdbc", "com.clickhouse"]),
        new PackageDescriptor("bigquery", "BigQuery", LibraryRole.OrmOrDatabase, "maven", ["com.google.cloud.bigquery"]),
        new PackageDescriptor("mongodb", "MongoDB", LibraryRole.OrmOrDatabase, "maven", ["org.springframework.data.mongodb", "com.mongodb"]),
        new PackageDescriptor("redis", "Redis", LibraryRole.OrmOrDatabase, "maven", ["org.springframework.data.redis", "redis.clients.jedis", "io.lettuce"]),
        new PackageDescriptor("elasticsearch", "Elasticsearch", LibraryRole.OrmOrDatabase, "maven", ["org.elasticsearch", "co.elastic.clients", "org.opensearch"]),
        new PackageDescriptor("cassandra", "Cassandra", LibraryRole.OrmOrDatabase, "maven", ["com.datastax.oss", "org.springframework.data.cassandra"]),
        new PackageDescriptor("mybatis", "MyBatis", LibraryRole.OrmOrDatabase, "maven", ["org.mybatis", "org.apache.ibatis"]),

        // Messaging & Events
        new PackageDescriptor("kafka", "Apache Kafka", LibraryRole.MessageBroker, "maven", ["org.apache.kafka", "org.springframework.kafka"]),
        new PackageDescriptor("rabbitmq", "RabbitMQ", LibraryRole.MessageBroker, "maven", ["com.rabbitmq", "org.springframework.amqp"]),
        new PackageDescriptor("pulsar", "Apache Pulsar", LibraryRole.MessageBroker, "maven", ["org.apache.pulsar"]),
        new PackageDescriptor("activemq", "ActiveMQ", LibraryRole.MessageBroker, "maven", ["org.apache.activemq"]),

        // Cloud & External Services
        new PackageDescriptor("aws", "AWS Java SDK", LibraryRole.CloudSdk, "maven", ["software.amazon.awssdk", "com.amazonaws"]),
        new PackageDescriptor("gcp", "GCP Java SDK", LibraryRole.CloudSdk, "maven", ["com.google.cloud", "com.google.firebase"]),
        new PackageDescriptor("azure", "Azure Java SDK", LibraryRole.CloudSdk, "maven", ["com.azure", "com.microsoft.azure"]),
        new PackageDescriptor("stripe", "Stripe Java", LibraryRole.CloudSdk, "maven", ["com.stripe"]),

        // Test Frameworks
        new PackageDescriptor("junit", "JUnit", LibraryRole.TestFramework, "maven", ["junit", "org.junit.*", "org.junit.jupiter.*"]),
        new PackageDescriptor("testng", "TestNG", LibraryRole.TestFramework, "maven", ["org.testng"])
    ];

    public IReadOnlyList<ISemanticExtension> SemanticExtensions { get; } =
    [
        new Libraries.SpringMvcLibraryParser(),
        new Libraries.SpringGraphQlLibraryParser(),
        new Libraries.GrpcJavaLibraryParser(),
        new Libraries.SpringEventsLibraryParser(),
        new Libraries.JpaLibraryParser(),
        new Libraries.JdbcTemplateLibraryParser(),
        new Libraries.HttpClientJavaLibraryParser()
    ];

    public bool UsesTreeSitter => true;

    public LanguageSyntaxProfile SyntaxProfile => JavaSyntaxProfile.Instance;

    public bool CanParse(string fileExtension)
    {
        return fileExtension.Equals(".java", StringComparison.OrdinalIgnoreCase);
    }

    public void ExtractDeclarations(Node rootNode, Action<string, Node?, string?> registerDeclaration)
    {
        JavaDeclarationExtractor.Extract(rootNode, registerDeclaration);
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

    public string GetProjectName(string directoryPath, string[] filesInDirectory)
    {
        var folderName = Path.GetFileName(directoryPath.TrimEnd('/', '\\'));

        // 1. If pom.xml exists, extract artifactId
        var pomPath = Path.Combine(directoryPath, "pom.xml");
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
                    if (!string.IsNullOrEmpty(artifactId))
                    {
                        return artifactId;
                    }
                }
            }
            catch
            {
                // Fallback on XML parse error
            }
        }

        // 2. If settings.gradle or settings.gradle.kts exists, extract rootProject.name
        var settingsGradle = Path.Combine(directoryPath, "settings.gradle");
        var settingsGradleKts = Path.Combine(directoryPath, "settings.gradle.kts");
        var settingsFile = File.Exists(settingsGradle) ? settingsGradle : (File.Exists(settingsGradleKts) ? settingsGradleKts : null);
        if (settingsFile != null)
        {
            try
            {
                var match = Regex.Match(File.ReadAllText(settingsFile), @"rootProject\.name\s*=\s*['""]([^'""]+)['""]");
                if (match.Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    return match.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Fallback on file read error
            }
        }

        // 3. If build.gradle or build.gradle.kts exists, check archivesBaseName
        var buildGradle = Path.Combine(directoryPath, "build.gradle");
        var buildGradleKts = Path.Combine(directoryPath, "build.gradle.kts");
        var gradleFile = File.Exists(buildGradle) ? buildGradle : (File.Exists(buildGradleKts) ? buildGradleKts : null);
        if (gradleFile != null)
        {
            try
            {
                var text = File.ReadAllText(gradleFile);
                var baseNameMatch = Regex.Match(text, @"(?:archivesBaseName|base\.archivesName)\s*=\s*['""]([^'""]+)['""]");
                if (baseNameMatch.Success && !string.IsNullOrWhiteSpace(baseNameMatch.Groups[1].Value))
                {
                    return baseNameMatch.Groups[1].Value.Trim();
                }
            }
            catch
            {
                // Fallback on file read error
            }
        }

        // 4. Disambiguate generic module folder names ("app", "src", "main", "core", "api")
        // when nested inside an enclosing parent folder (e.g. "android/app" -> "android-app")
        if (folderName.Equals("app", StringComparison.OrdinalIgnoreCase) ||
            folderName.Equals("src", StringComparison.OrdinalIgnoreCase) ||
            folderName.Equals("main", StringComparison.OrdinalIgnoreCase) ||
            folderName.Equals("core", StringComparison.OrdinalIgnoreCase) ||
            folderName.Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            var parentDir = Path.GetDirectoryName(directoryPath.TrimEnd('/', '\\'));
            if (!string.IsNullOrEmpty(parentDir))
            {
                var parentName = Path.GetFileName(parentDir);
                if (!string.IsNullOrEmpty(parentName))
                {
                    return $"{parentName}-{folderName}".ToLowerInvariant();
                }
            }
        }

        return folderName;
    }

    public Dictionary<string, string> ExtractManifestProperties(string directoryPath, string[] filesInDirectory)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var buildGradle = Path.Combine(directoryPath, "build.gradle");
        var buildGradleKts = Path.Combine(directoryPath, "build.gradle.kts");
        var gradleFile = File.Exists(buildGradle) ? buildGradle : (File.Exists(buildGradleKts) ? buildGradleKts : null);

        if (gradleFile != null)
        {
            try
            {
                var content = File.ReadAllText(gradleFile);

                if (content.Contains("com.android.application", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("com.android.library", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("dev.flutter.flutter-gradle-plugin", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("dev.flutter.flutter-plugin-loader", StringComparison.OrdinalIgnoreCase))
                {
                    props["manifest_type"] = "mobile";
                    props["framework_type"] = "mobile";
                    props["sdk"] = "android";
                }
            }
            catch
            {
            }
        }

        var settingsGradle = Path.Combine(directoryPath, "settings.gradle");
        var settingsGradleKts = Path.Combine(directoryPath, "settings.gradle.kts");
        var settingsFile = File.Exists(settingsGradle) ? settingsGradle : (File.Exists(settingsGradleKts) ? settingsGradleKts : null);
        if (settingsFile != null && !props.ContainsKey("framework_type"))
        {
            try
            {
                var content = File.ReadAllText(settingsFile);
                if (content.Contains("dev.flutter.flutter-plugin-loader", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("com.android.application", StringComparison.OrdinalIgnoreCase))
                {
                    props["manifest_type"] = "mobile";
                    props["framework_type"] = "mobile";
                    props["sdk"] = "android";
                }
            }
            catch
            {
            }
        }

        var pomPath = Path.Combine(directoryPath, "pom.xml");
        if (File.Exists(pomPath))
        {
            try
            {
                var content = File.ReadAllText(pomPath);
                if (content.Contains("spring-boot-starter-web", StringComparison.OrdinalIgnoreCase))
                {
                    props["manifest_type"] = "application";
                    props["framework_type"] = "web";
                }
            }
            catch
            {
            }
        }

        return props;
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

        return Task.FromResult(new ProjectDependencyInfo([.. localProjects.Distinct(StringComparer.OrdinalIgnoreCase)],
            externalPackages
        ));
    }

    public BaseParserVisitor CreateVisitor(
        Node rootNode,
        List<ISemanticExtension> activeExtensions,
        string relativePath,
        string absoluteWorkspacePath,
        IFileParser fileParser,
        SemanticExtensionRegistry extensionRegistry)
    {
        return new JavaFileVisitor(
            rootNode,
            activeExtensions,
            this,
            relativePath,
            absoluteWorkspacePath,
            fileParser,
            extensionRegistry
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

    public ISyntaxEnricher GetSyntaxEnricher(SyntaxTree syntaxTree) => new SyntaxEnricher(SemanticExtensions, syntaxTree, Packages);

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
