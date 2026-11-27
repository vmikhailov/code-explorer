using System.Reflection;
using System.Xml;

namespace CodeExplorer.Core.Analysis;

/// <summary>
/// Diagnoses workspace index health, ScanIssue parsing errors, and unsupported file extensions,
/// XML dialects, libraries/frameworks, and graph coverage anomalies for self-improvement.
/// </summary>
public sealed class WorkspaceDoctorService
{
    private const int MaxXmlFilesToInspect = 500;
    private const int MaxFilesToEnumerate = 50_000;

    private const string ColKind = "kind";
    private const string ColName = "name";
    private const string XmlExt = ".xml";
    private const string XhtmlExt = ".xhtml";
    private const string ColLanguage = "language";
    private const string ColEcosystem = "ecosystem";
    private const string ColIsInternal = "is_internal";
    private const string ColPath = "path";
    private const string ColStage = "stage";
    private const string ColCategory = "category";
    private const string ColSeverity = "severity";
    private const string ColMessage = "message";
    private const string ColStartLine = "start_line";
    private const string ColId = "id";
    private const string UnknownValue = "unknown";
    private const string EmptyNamespace = "(no xmlns)";
    private const string RedactedPath = "<redacted>";

    private const string ColCount = "cnt";
    private const string SyntaxIdSuffix = "syntax";

    private const string QueryProjects =
        "MATCH (p:Project) RETURN p.id AS id, p.name AS name, coalesce(p.path, '') AS path, coalesce(p.language, '') AS language";

    private const string QueryScanIssues =
        "MATCH (i:ScanIssue) RETURN coalesce(i.path, '') AS path, coalesce(i.stage, 'unknown') AS stage, coalesce(i.category, 'unknown') AS category, coalesce(i.severity, 'warning') AS severity, coalesce(i.language, 'unknown') AS language, coalesce(i.message, '') AS message, coalesce(i.start_line, 0) AS start_line";

    private const string QueryPackages =
        "MATCH (p:Package) RETURN DISTINCT p.name AS name, coalesce(p.ecosystem, 'unknown') AS ecosystem, coalesce(p.is_internal, false) AS is_internal";

    private const string QueryProjectPackages =
        "MATCH (proj:Project)-[:DEPENDS_ON]->(pkg:Package) RETURN proj.id AS id, proj.name AS name, coalesce(proj.language, '') AS language, pkg.name AS kind";

    private const string QueryProjectFileCounts =
        "MATCH (proj:Project)-[:CONTAINS]->(f:File) RETURN proj.id AS id, count(f) AS cnt";

    private const string QueryProjectDirectFunctionCounts =
        "MATCH (proj:Project)<-[:BELONGS_TO]-(:ProjectSyntax)-[:CONTAINS]->(fn:Function) RETURN proj.id AS id, count(fn) AS cnt";

    private const string QueryProjectHasNestedFunction =
        "MATCH (ps:ProjectSyntax {id: $psId})-[:CONTAINS]->(:Type)-[:HAS_METHOD]->(fn:Function) RETURN fn.id AS id LIMIT 1";

    private const string QueryWorkloadEndpointCounts =
        "MATCH (w:Workload)-[:EXPOSES]->(e:Endpoint) RETURN w.name AS id, count(e) AS cnt";

    private const string QueryModuleEndpointCounts =
        "MATCH (m:Module)-[:EXPOSES]->(e:Endpoint) RETURN m.name AS id, count(e) AS cnt";

    private const string QueryWorkloadDbCounts =
        "MATCH (w:Workload)-[:USES_DB]->(d:Database) RETURN w.name AS id, count(d) AS cnt";

    private const string QueryWorkloadTableCounts =
        "MATCH (w:Workload)-[:USES_DB]->(t:Table) RETURN w.name AS id, count(t) AS cnt";

    private const string QueryModuleDbCounts =
        "MATCH (m:Module)-[:USES_DB]->(d:Database) RETURN m.name AS id, count(d) AS cnt";

    private const string QueryModuleTableCounts =
        "MATCH (m:Module)-[:USES_DB]->(t:Table) RETURN m.name AS id, count(t) AS cnt";

    private const string QueryWorkloadTopicCounts =
        "MATCH (w:Workload)-[:PUBLISHES_TO|SUBSCRIBES_TO]->(t:Topic) RETURN w.name AS id, count(t) AS cnt";

    private const string QueryModuleTopicCounts =
        "MATCH (m:Module)-[:PUBLISHES_TO|SUBSCRIBES_TO]->(t:Topic) RETURN m.name AS id, count(t) AS cnt";

    private const string QueryEndpointPaths =
        "MATCH (e:Endpoint) RETURN DISTINCT coalesce(e.file_path, e.path, '') AS path";

    private const string QueryTablePaths =
        "MATCH (t:Table) RETURN DISTINCT coalesce(t.file_path, t.path, '') AS path";

    private const string QueryQueryPaths =
        "MATCH (q:Query) RETURN DISTINCT coalesce(q.file_path, q.path, '') AS path";

    private const string QueryTopicPaths =
        "MATCH (t:Topic) RETURN DISTINCT coalesce(t.file_path, t.path, '') AS path";

    private static readonly HashSet<string> KeyKindsToReport = new(StringComparer.OrdinalIgnoreCase)
    {
        "Project", "Service", "App", "Worker", "Library", "File",
        "Type", "Function", "Endpoint", "Database", "Table", "Query",
        "Topic", "ExternalService", "Package", "ScanIssue"
    };

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".hg", ".svn", ".codeexplorer", ".vs", ".vscode", ".idea",
        "node_modules", "bin", "obj", "target", "dist", "build", "out",
        "vendor", "packages", "__pycache__", ".venv", "venv", ".next", ".nuxt",
        "coverage", ".gradle", ".mvn", ".Packages", ".Build"
    };

    private static readonly HashSet<string> IgnoredNonCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".txt", ".rst", ".adoc", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".webp", ".avif", ".tiff",
        ".woff", ".woff2", ".ttf", ".otf", ".eot",
        ".mp3", ".mp4", ".wav", ".ogg", ".webm", ".mov", ".avi",
        ".zip", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".7z", ".rar", ".jar", ".war", ".ear", ".nupkg", ".vsix", ".whl",
        ".exe", ".dll", ".so", ".dylib", ".pdb", ".lib", ".a", ".o", ".class", ".pyc", ".pyo",
        ".db", ".sqlite", ".sqlite3", ".db-wal", ".db-shm", ".log", ".bak", ".tmp", ".swp",
        ".lock", ".sum", ".map", ".min.js", ".min.css", ".csv", ".tsv", ".parquet",
        ".gitignore", ".gitattributes", ".gitmodules", ".git", ".editorconfig", ".npmrc", ".nvmrc",
        ".prettierignore", ".eslintignore", ".dockerignore", ".vscodeignore", ".browserslistrc",
        ".license", ".cert", ".pem", ".key", ".crt", ".p12", ".jks", ".keystore",
        ".patch", ".diff", ".trx", ".pubxml", ".sln", ".slnx", ".props", ".targets", ".webmanifest",
        ".css", ".scss", ".sass", ".less", ".styl", ".html", ".htm", ".json", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".properties", ".env", ".sh", ".bash", ".zsh", ".ps1", ".cmd", ".bat"
    };

    private static readonly HashSet<string> KnownHandledXmlRoots = new(StringComparer.OrdinalIgnoreCase)
    {
        "project", "Project", "manifest", "configuration", "packages", "package",
        "settings", "ruleset", "StyleCopSettings", "Directory.Build.props",
        "assembly", "nlog", "log4j2", "logback", "checkstyle", "pmd", "svg",
        "plist", "xcworkspace", "xcodeproj", "idea", "module", "component", "UiBinder",
        "TestRun", "testsuites", "testsuite", "coverage", "cobertura", "report"
    };

    private static readonly string[] CompoundXmlSuffixes =
    [
        ".gwt.xml", ".ui.xml", ".hbm.xml", ".orm.xml", ".cfg.xml", ".taglib.xml",
        ".page.xml", ".pages.xml", ".bpel.xml", ".wsdl.xml", ".xsd.xml"
    ];

    private static readonly (string Keyword, string Category)[] FrameworkHintKeywords =
    [
        ("seam", "web/di-framework"),
        ("jsf", "ui/web-framework"),
        ("faces", "ui/web-framework"),
        ("primefaces", "ui/web-framework"),
        ("richfaces", "ui/web-framework"),
        ("icefaces", "ui/web-framework"),
        ("myfaces", "ui/web-framework"),
        ("wicket", "ui/web-framework"),
        ("vaadin", "ui/web-framework"),
        ("gwt", "ui/rpc-framework"),
        ("struts", "web-framework"),
        ("resteasy", "rest-framework"),
        ("jax-rs", "rest-framework"),
        ("jaxrs", "rest-framework"),
        ("jersey", "rest-framework"),
        ("servlet", "web-framework"),
        ("tomcat", "web-server"),
        ("jetty", "web-server"),
        ("undertow", "web-server"),
        ("wildfly", "app-server"),
        ("jboss", "app-server"),
        ("liquibase", "db-migration"),
        ("flyway", "db-migration"),
        ("alembic", "db-migration"),
        ("knex", "db-migration"),
        ("hibernate", "orm/database"),
        ("eclipselink", "orm/database"),
        ("openjpa", "orm/database"),
        ("mybatis", "orm/database"),
        ("jooq", "orm/database"),
        ("querydsl", "orm/database"),
        ("jdbc", "database-driver"),
        ("mysql", "database-driver"),
        ("postgres", "database-driver"),
        ("oracle", "database-driver"),
        ("mariadb", "database-driver"),
        ("mssql", "database-driver"),
        ("sqlite", "database-driver"),
        ("mongo", "database-driver"),
        ("redis", "cache"),
        ("memcached", "cache"),
        ("hazelcast", "cache"),
        ("ehcache", "cache"),
        ("kafka", "messaging"),
        ("amqp", "messaging"),
        ("rabbit", "messaging"),
        ("activemq", "messaging"),
        ("hornetq", "messaging"),
        ("artemis", "messaging"),
        ("jms", "messaging"),
        ("nats", "messaging"),
        ("pulsar", "messaging"),
        ("grpc", "rpc"),
        ("thrift", "rpc"),
        ("hessian", "rpc"),
        ("cxf", "soap/rest"),
        ("axis", "soap"),
        ("graphql", "graphql"),
        ("quartz", "scheduler"),
        ("camel", "integration"),
        ("drools", "rules-engine"),
        ("jbpm", "workflow"),
        ("activiti", "workflow"),
        ("flowable", "workflow"),
        ("camunda", "workflow"),
        ("temporal", "workflow"),
        ("elasticsearch", "search"),
        ("opensearch", "search"),
        ("solr", "search"),
        ("lucene", "search"),
        ("aws", "cloud-sdk"),
        ("amazon", "cloud-sdk"),
        ("google-cloud", "cloud-sdk"),
        ("azure", "cloud-sdk"),
        ("shiro", "security"),
        ("keycloak", "security"),
        ("oauth", "security"),
        ("jwt", "security"),
        ("spring", "framework"),
        ("quarkus", "framework"),
        ("micronaut", "framework"),
        ("vertx", "framework"),
        ("ktor", "framework"),
        ("playframework", "framework"),
        ("akka", "actor/distributed"),
        ("pekko", "actor/distributed"),
        ("rails", "web-framework"),
        ("django", "web-framework"),
        ("flask", "web-framework"),
        ("fastapi", "web-framework"),
        ("express", "web-framework"),
        ("nestjs", "web-framework"),
        ("fastify", "web-framework"),
        ("next", "web-framework"),
        ("nuxt", "web-framework"),
        ("svelte", "ui-framework"),
        ("angular", "ui-framework"),
        ("vue", "ui-framework"),
        ("react", "ui-framework")
    ];

    private static readonly string[] WebPackageHints =
    [
        "servlet", "jax-rs", "jaxrs", "resteasy", "jersey", "spring-web", "spring-mvc",
        "struts", "seam", "jsf", "faces", "gwt", "express", "fastify", "koa", "hono",
        "nestjs", "flask", "django", "fastapi", "gin", "echo", "fiber", "chi", "gorilla/mux",
        "actix-web", "axum", "rocket", "aspnetcore", "webapi", "mvc"
    ];

    private static readonly string[] DbPackageHints =
    [
        "jdbc", "jpa", "hibernate", "mybatis", "liquibase", "flyway", "mysql", "postgres",
        "npgsql", "sqlclient", "sqlite", "mongodb", "pgx", "gorm", "sqlx", "entgo",
        "prisma", "typeorm", "sequelize", "drizzle", "sqlalchemy", "psycopg", "pymysql",
        "entityframework", "dapper", "nhibernate", "diesel", "sea-orm"
    ];

    private static readonly string[] MessagingPackageHints =
    [
        "kafka", "rabbitmq", "amqp", "jms", "activemq", "artemis", "nats", "pulsar",
        "pubsub", "sqs", "sns", "servicebus", "eventhub", "masstransit", "nservicebus",
        "bullmq", "celery", "watermill", "sarama"
    ];

    public async Task<WorkspaceDoctorReport> AnalyzeAsync(
        IGraphClient db,
        string workspaceRoot,
        WorkspaceDoctorOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new WorkspaceDoctorOptions();

        var previousTimeout = db.CommandTimeoutSeconds;
        if (options.QueryTimeoutSeconds >= 0)
        {
            db.CommandTimeoutSeconds = options.QueryTimeoutSeconds;
        }

        try
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
                          ?? typeof(WorkspaceDoctorService).Assembly.GetName().Version?.ToString(3)
                          ?? UnknownValue;

            var report = new WorkspaceDoctorReport
            {
                CodeExplorerVersion = version,
                Anonymized = options.Anonymize,
                Health = await CollectHealthAsync(db, cancellationToken)
            };

            if (options.IncludeIssues)
            {
                report.Issues = await CollectIssuesAsync(db, options, cancellationToken);
            }

            if (options.IncludeGaps)
            {
                report.Gaps = await CollectGapsAsync(db, workspaceRoot, options, cancellationToken);
            }

            return report;
        }
        finally
        {
            db.CommandTimeoutSeconds = previousTimeout;
        }
    }

    private static async Task<DoctorHealthSummary> CollectHealthAsync(IGraphClient db, CancellationToken ct)
    {
        var (totalNodes, totalRels) = await db.GetGraphCountsAsync(ct);
        var breakdown = await db.GetNodesBreakdownAsync(ct);

        var keyCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (kind, cnt) in breakdown.OrderByDescending(kv => kv.Value))
        {
            if (KeyKindsToReport.Contains(kind))
            {
                keyCounts[kind] = cnt;
            }
        }

        var projectsRes = await db.ExecuteCypherAsync(QueryProjects, null, ct);
        var languages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in projectsRes.Rows)
        {
            var lang = row.GetStringProp(ColLanguage);
            if (!string.IsNullOrWhiteSpace(lang))
            {
                languages.Add(lang.Trim().ToLowerInvariant());
            }
        }

        return new DoctorHealthSummary
        {
            SchemaVersion = db.SchemaVersion,
            CurrentSchemaVersion = IGraphClient.CurrentSchemaVersion,
            IsSchemaOutdated = db.IsSchemaOutdated,
            TotalNodes = totalNodes,
            TotalRelationships = totalRels,
            ProjectCount = projectsRes.Rows.Count,
            LanguagesDetected = [.. languages.OrderBy(l => l, StringComparer.Ordinal)],
            KeyNodeCounts = keyCounts
        };
    }

    private static async Task<DoctorIssuesSummary> CollectIssuesAsync(
        IGraphClient db,
        WorkspaceDoctorOptions options,
        CancellationToken ct)
    {
        var summary = new DoctorIssuesSummary();
        var res = await db.ExecuteCypherAsync(QueryScanIssues, null, ct);
        summary.TotalCount = res.Rows.Count;
        if (res.Rows.Count == 0)
        {
            return summary;
        }

        var groupCounts = new Dictionary<(string Stage, string Category, string Language, string Severity), int>();
        var samples = new List<DoctorIssueSample>();

        foreach (var row in res.Rows)
        {
            var path = row.GetStringProp(ColPath);
            var stage = row.GetStringProp(ColStage);
            var category = row.GetStringProp(ColCategory);
            var severity = row.GetStringProp(ColSeverity);
            var language = row.GetStringProp(ColLanguage);
            var message = row.GetStringProp(ColMessage);
            var startLine = row.GetNullableIntProp(ColStartLine) ?? 0;

            var key = (stage, category, language, severity);
            groupCounts[key] = groupCounts.GetValueOrDefault(key) + 1;

            if (!options.Anonymize && samples.Count < options.MaxSamples)
            {
                samples.Add(new DoctorIssueSample(
                    string.IsNullOrEmpty(path) ? RedactedPath : path,
                    startLine,
                    stage,
                    category,
                    severity,
                    language,
                    message));
            }
        }

        summary.Groups = groupCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.Stage, StringComparer.Ordinal)
            .Select(kv => new DoctorIssueGroup(kv.Key.Stage, kv.Key.Category, kv.Key.Language, kv.Key.Severity, kv.Value))
            .ToList();
        summary.Samples = samples;
        return summary;
    }

    private static async Task<DoctorGapsSummary> CollectGapsAsync(
        IGraphClient db,
        string workspaceRoot,
        WorkspaceDoctorOptions options,
        CancellationToken ct)
    {
        var gaps = new DoctorGapsSummary();

        var fileParsers = ParserRegistry.GetAllFileParsers();
        var projectParsers = ParserRegistry.GetAllProjectParsers();

        // 1. Unhandled file extensions + XML dialect fingerprinting
        ScanWorkspaceFileGaps(workspaceRoot, fileParsers, projectParsers, gaps, options.MaxSamples, ct);

        // 2. Unmatched external packages vs registered ISemanticExtension patterns
        await CollectUnmatchedPackagesAsync(db, workspaceRoot, fileParsers, gaps, options, ct);

        // 3. Graph coverage anomalies (projects with framework deps but 0 extracted semantic nodes)
        await CollectCoverageAnomaliesAsync(db, gaps, options, ct);

        return gaps;
    }

    private static void ScanWorkspaceFileGaps(
        string workspaceRoot,
        IReadOnlyList<IFileParser> fileParsers,
        IReadOnlyList<IProjectParser> projectParsers,
        DoctorGapsSummary gaps,
        int maxSamples,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
        {
            return;
        }

        var manifestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manifestExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pp in projectParsers)
        {
            foreach (var m in pp.ManifestFileNames)
            {
                if (m.StartsWith("*.", StringComparison.Ordinal))
                {
                    manifestExtensions.Add(m[1..]);
                }
                else if (m.StartsWith('.'))
                {
                    manifestExtensions.Add(m);
                }
                else
                {
                    manifestNames.Add(m);
                }
            }

            foreach (var ext in pp.ManifestExtensions)
            {
                if (!string.IsNullOrWhiteSpace(ext))
                {
                    var normalized = ext.Trim();
                    manifestExtensions.Add(normalized.StartsWith('.') ? normalized : "." + normalized);
                }
            }
        }

        var unhandledExtCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var xmlDialectCounts = new Dictionary<(string Root, string Ns), int>();
        var xmlFilesInspected = 0;
        var totalFilesVisited = 0;

        var stack = new Stack<string>();
        stack.Push(workspaceRoot);

        while (stack.Count > 0 && totalFilesVisited < MaxFilesToEnumerate)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();

            IEnumerable<string> subDirs;
            try
            {
                subDirs = Directory.EnumerateDirectories(dir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var sub in subDirs)
            {
                var dirName = Path.GetFileName(sub);
                if (!string.IsNullOrEmpty(dirName) && !IgnoredDirectories.Contains(dirName))
                {
                    stack.Push(sub);
                }
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(dir);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var filePath in files)
            {
                if (++totalFilesVisited > MaxFilesToEnumerate) break;

                var fileName = Path.GetFileName(filePath);
                if (string.IsNullOrEmpty(fileName) || manifestNames.Contains(fileName)) continue;

                var ext = Path.GetExtension(fileName);
                if (string.IsNullOrEmpty(ext) || manifestExtensions.Contains(ext)) continue;

                var isXmlLike = ext.Equals(XmlExt, StringComparison.OrdinalIgnoreCase) ||
                                ext.Equals(XhtmlExt, StringComparison.OrdinalIgnoreCase);

                var handled = false;
                for (var i = 0; i < fileParsers.Count; i++)
                {
                    if (fileParsers[i].CanParseFile(filePath))
                    {
                        handled = true;
                        break;
                    }
                }

                if (handled) continue;

                if (isXmlLike && xmlFilesInspected < MaxXmlFilesToInspect)
                {
                    xmlFilesInspected++;
                    var dialect = TryReadXmlRootDialect(filePath);
                    if (dialect.HasValue && !KnownHandledXmlRoots.Contains(dialect.Value.Root))
                    {
                        var key = (dialect.Value.Root, dialect.Value.Ns);
                        xmlDialectCounts[key] = xmlDialectCounts.GetValueOrDefault(key) + 1;
                    }
                }

                var compoundKey = MatchCompoundXmlSuffix(fileName) ?? ext.ToLowerInvariant();
                if (!IgnoredNonCodeExtensions.Contains(compoundKey) &&
                    !compoundKey.Equals(XmlExt, StringComparison.OrdinalIgnoreCase))
                {
                    unhandledExtCounts[compoundKey] = unhandledExtCounts.GetValueOrDefault(compoundKey) + 1;
                }
            }
        }

        gaps.UnhandledExtensions = unhandledExtCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Take(maxSamples)
            .Select(kv => new DoctorExtensionGap(kv.Key, kv.Value))
            .ToList();

        gaps.UnhandledXmlDialects = xmlDialectCounts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key.Root, StringComparer.OrdinalIgnoreCase)
            .Take(maxSamples)
            .Select(kv => new DoctorXmlDialectGap(kv.Key.Root, kv.Key.Ns, kv.Value))
            .ToList();
    }

    private static string? MatchCompoundXmlSuffix(string fileName)
    {
        for (var i = 0; i < CompoundXmlSuffixes.Length; i++)
        {
            if (fileName.EndsWith(CompoundXmlSuffixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return CompoundXmlSuffixes[i];
            }
        }
        return null;
    }

    private static (string Root, string Ns)? TryReadXmlRootDialect(string filePath)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersInDocument = 64 * 1024
            };
            using var reader = XmlReader.Create(filePath, settings);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    var localName = reader.LocalName;
                    var ns = string.IsNullOrWhiteSpace(reader.NamespaceURI)
                        ? EmptyNamespace
                        : reader.NamespaceURI.Trim();
                    return string.IsNullOrWhiteSpace(localName) ? null : (localName, ns);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            // Ignore malformed or unreadable XML files during dialect sniffing
        }
        return null;
    }

    private static async Task CollectUnmatchedPackagesAsync(
        IGraphClient db,
        string workspaceRoot,
        IReadOnlyList<IFileParser> fileParsers,
        DoctorGapsSummary gaps,
        WorkspaceDoctorOptions options,
        CancellationToken ct)
    {
        var customCatalog = WorkspaceDetectorLoader.LoadForWorkspace(workspaceRoot, strict: false);
        var allExtensions = new List<ISemanticExtension>();
        foreach (var fp in fileParsers)
        {
            allExtensions.AddRange(customCatalog.GetEffectiveExtensions(fp));
        }
        var registry = new SemanticExtensionRegistry(allExtensions);

        var pkgRes = await db.ExecuteCypherAsync(QueryPackages, null, ct);
        var highPriority = new List<DoctorUnmatchedPackage>();
        var otherCount = 0;

        foreach (var row in pkgRes.Rows)
        {
            var name = row.GetStringProp(ColName);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var isInternal = row.GetBoolProp(ColIsInternal);
            if (isInternal) continue;
            if (name.StartsWith("@types/", StringComparison.OrdinalIgnoreCase)) continue;

            if (registry.Match(name) != null) continue;

            var ecosystem = row.GetStringProp(ColEcosystem);
            var hint = ClassifyFrameworkHint(name);
            if (hint != null)
            {
                highPriority.Add(new DoctorUnmatchedPackage(name, ecosystem, hint));
            }
            else
            {
                otherCount++;
            }
        }

        gaps.UnmatchedPackages = highPriority
            .OrderBy(p => p.Role, StringComparer.Ordinal)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxSamples)
            .ToList();
        gaps.OtherUnmatchedPackageCount = otherCount;
    }

    private static readonly char[] PackageTokenSeparators = ['.', '-', '_', '/', ':', '@'];

    private static string? ClassifyFrameworkHint(string packageName)
    {
        var tokens = packageName.Split(PackageTokenSeparators, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < FrameworkHintKeywords.Length; i++)
        {
            if (MatchesPackageHint(packageName, tokens, FrameworkHintKeywords[i].Keyword))
            {
                return FrameworkHintKeywords[i].Category;
            }
        }
        return null;
    }

    private static async Task CollectCoverageAnomaliesAsync(
        IGraphClient db,
        DoctorGapsSummary gaps,
        WorkspaceDoctorOptions options,
        CancellationToken ct)
    {
        var projPkgRes = await db.ExecuteCypherAsync(QueryProjectPackages, null, ct);
        var pkgsByProjectId = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in projPkgRes.Rows)
        {
            var pid = row.GetStringProp(ColId);
            var pkgName = row.GetStringProp(ColKind);
            if (string.IsNullOrEmpty(pid) || string.IsNullOrEmpty(pkgName)) continue;
            if (!pkgsByProjectId.TryGetValue(pid, out var list))
            {
                list = [];
                pkgsByProjectId[pid] = list;
            }
            list.Add(pkgName);
        }

        var projectsRes = await db.ExecuteCypherAsync(QueryProjects, null, ct);
        if (projectsRes.Rows.Count == 0)
        {
            return;
        }

        var projectEntries = new List<(string Id, string Name, string NormPath, string Language)>(projectsRes.Rows.Count);
        foreach (var row in projectsRes.Rows)
        {
            var pid = row.GetStringProp(ColId);
            var name = row.GetStringProp(ColName);
            var rawPath = row.GetStringProp(ColPath).Replace('\\', '/').Trim('/');
            var lang = row.GetStringProp(ColLanguage);
            projectEntries.Add((pid, name, rawPath, lang));
        }

        var sortedByPathDesc = projectEntries
            .OrderByDescending(p => p.NormPath.Length)
            .ToList();

        var filesByProj = await LoadProjectCountMapAsync(db, QueryProjectFileCounts, ct);
        var directFnByProj = await LoadProjectCountMapAsync(db, QueryProjectDirectFunctionCounts, ct);

        var endpointsByProj = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await MergeProjectCountMapAsync(db, QueryWorkloadEndpointCounts, endpointsByProj, ct);
        await MergeProjectCountMapAsync(db, QueryModuleEndpointCounts, endpointsByProj, ct);
        await AttributeNodePathsToProjectsAsync(db, QueryEndpointPaths, sortedByPathDesc, endpointsByProj, ct);

        var dbsByProj = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await MergeProjectCountMapAsync(db, QueryWorkloadDbCounts, dbsByProj, ct);
        await MergeProjectCountMapAsync(db, QueryWorkloadTableCounts, dbsByProj, ct);
        await MergeProjectCountMapAsync(db, QueryModuleDbCounts, dbsByProj, ct);
        await MergeProjectCountMapAsync(db, QueryModuleTableCounts, dbsByProj, ct);
        await AttributeNodePathsToProjectsAsync(db, QueryTablePaths, sortedByPathDesc, dbsByProj, ct);

        var queriesByProj = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await AttributeNodePathsToProjectsAsync(db, QueryQueryPaths, sortedByPathDesc, queriesByProj, ct);

        var topicsByProj = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await MergeProjectCountMapAsync(db, QueryWorkloadTopicCounts, topicsByProj, ct);
        await MergeProjectCountMapAsync(db, QueryModuleTopicCounts, topicsByProj, ct);
        await AttributeNodePathsToProjectsAsync(db, QueryTopicPaths, sortedByPathDesc, topicsByProj, ct);

        var anomalies = new List<DoctorCoverageAnomaly>();
        var anonIndex = 0;

        foreach (var (pid, name, _, lang) in projectEntries)
        {
            var endpoints = endpointsByProj.GetValueOrDefault(name) + endpointsByProj.GetValueOrDefault(pid);
            var directFunctions = directFnByProj.GetValueOrDefault(pid);
            var files = filesByProj.GetValueOrDefault(pid);
            var tables = dbsByProj.GetValueOrDefault(name) + dbsByProj.GetValueOrDefault(pid);
            var queries = queriesByProj.GetValueOrDefault(name) + queriesByProj.GetValueOrDefault(pid);
            var topics = topicsByProj.GetValueOrDefault(name) + topicsByProj.GetValueOrDefault(pid);

            anonIndex++;
            var label = options.Anonymize
                ? $"project#{anonIndex}"
                : (string.IsNullOrEmpty(name) ? pid : name);
            var cleanLang = string.IsNullOrEmpty(lang) ? UnknownValue : lang;

            var projPkgs = pkgsByProjectId.GetValueOrDefault(pid);
            if (projPkgs is { Count: > 0 })
            {
                var webMatch = FindFirstMatchingHint(projPkgs, WebPackageHints);
                if (webMatch != null && endpoints == 0)
                {
                    var webDetail = $"Has web/HTTP dependency ('{webMatch}') but 0 Endpoint nodes were extracted.";
                    anomalies.Add(new DoctorCoverageAnomaly(label, cleanLang, DoctorAnomalyKind.MissingEndpoints, webDetail));
                }

                var dbMatch = FindFirstMatchingHint(projPkgs, DbPackageHints);
                if (dbMatch != null && tables == 0 && queries == 0)
                {
                    var dbDetail = $"Has database/ORM dependency ('{dbMatch}') but 0 Database/Query nodes were extracted.";
                    anomalies.Add(new DoctorCoverageAnomaly(label, cleanLang, DoctorAnomalyKind.MissingDatabaseLineage, dbDetail));
                }

                var msgMatch = FindFirstMatchingHint(projPkgs, MessagingPackageHints);
                if (msgMatch != null && topics == 0)
                {
                    var msgDetail = $"Has messaging dependency ('{msgMatch}') but 0 Topic nodes were extracted.";
                    anomalies.Add(new DoctorCoverageAnomaly(label, cleanLang, DoctorAnomalyKind.MissingTopics, msgDetail));
                }
            }

            if (files >= 20 && directFunctions == 0)
            {
                var psId = $"{pid}{SyntaxIdSuffix}";
                var nestedCheck = await db.ExecuteCypherAsync(QueryProjectHasNestedFunction, new { psId }, ct);
                if (nestedCheck.Rows.Count == 0)
                {
                    var fnDetail = $"Contains {files} indexed files but 0 Function nodes were extracted.";
                    anomalies.Add(new DoctorCoverageAnomaly(label, cleanLang, DoctorAnomalyKind.ZeroFunctions, fnDetail));
                }
            }
        }

        gaps.CoverageAnomalies = anomalies.Take(options.MaxSamples).ToList();
    }

    private static async Task<Dictionary<string, int>> LoadProjectCountMapAsync(
        IGraphClient db,
        string cypher,
        CancellationToken ct)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await MergeProjectCountMapAsync(db, cypher, map, ct);
        return map;
    }

    private static async Task MergeProjectCountMapAsync(
        IGraphClient db,
        string cypher,
        Dictionary<string, int> map,
        CancellationToken ct)
    {
        var res = await db.ExecuteCypherAsync(cypher, null, ct);
        foreach (var row in res.Rows)
        {
            var pid = row.GetStringProp(ColId);
            if (!string.IsNullOrEmpty(pid))
            {
                map[pid] = map.GetValueOrDefault(pid) + (row.GetNullableIntProp(ColCount) ?? 0);
            }
        }
    }

    private static async Task AttributeNodePathsToProjectsAsync(
        IGraphClient db,
        string cypher,
        List<(string Id, string Name, string NormPath, string Language)> projectsSortedByPathDesc,
        Dictionary<string, int> targetCounts,
        CancellationToken ct)
    {
        var res = await db.ExecuteCypherAsync(cypher, null, ct);
        foreach (var row in res.Rows)
        {
            var rawPath = row.GetStringProp(ColPath);
            if (string.IsNullOrWhiteSpace(rawPath)) continue;
            var norm = rawPath.Replace('\\', '/').TrimStart('/');

            for (var i = 0; i < projectsSortedByPathDesc.Count; i++)
            {
                var proj = projectsSortedByPathDesc[i];
                if (string.IsNullOrEmpty(proj.NormPath) ||
                    norm.Equals(proj.NormPath, StringComparison.OrdinalIgnoreCase) ||
                    (norm.Length > proj.NormPath.Length &&
                     norm.StartsWith(proj.NormPath, StringComparison.OrdinalIgnoreCase) &&
                     norm[proj.NormPath.Length] == '/'))
                {
                    var key = !string.IsNullOrEmpty(proj.Id) ? proj.Id : proj.Name;
                    targetCounts[key] = targetCounts.GetValueOrDefault(key) + 1;
                    break;
                }
            }
        }
    }

    private static string? FindFirstMatchingHint(List<string> packages, string[] hints)
    {
        for (var i = 0; i < packages.Count; i++)
        {
            var pkg = packages[i];
            var tokens = pkg.Split(PackageTokenSeparators, StringSplitOptions.RemoveEmptyEntries);
            for (var h = 0; h < hints.Length; h++)
            {
                if (MatchesPackageHint(pkg, tokens, hints[h]))
                {
                    return pkg;
                }
            }
        }
        return null;
    }

    private static bool MatchesPackageHint(string packageName, string[] tokens, string hint)
    {
        if (hint.IndexOfAny(PackageTokenSeparators) >= 0)
        {
            return packageName.Contains(hint, StringComparison.OrdinalIgnoreCase);
        }

        for (var t = 0; t < tokens.Length; t++)
        {
            if (tokens[t].Equals(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
