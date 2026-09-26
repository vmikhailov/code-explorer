using System.Collections.Concurrent;
using CodeExplorer.Core.Analysis;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public class SyntaxEnricher : ISyntaxEnricher
{
    private readonly IReadOnlyList<ILibraryParser> _libraryParsers;
    private readonly LibraryTrieRegistry _trieRegistry;
    private readonly SyntaxTree _syntaxTree;

    public SyntaxEnricher(IReadOnlyList<ILibraryParser> libraryParsers, SyntaxTree syntaxTree)
    {
        _libraryParsers = libraryParsers;
        _trieRegistry = new LibraryTrieRegistry(libraryParsers);
        _syntaxTree = syntaxTree;
    }

    private static readonly Regex ConfigRegex = new(
        @"(?i)(config|settings?|cfg|\benv\b|db_?conn|\burl\b|\buri\b|\bport\b|\bhost\b|user(name)?|pass(word)?|token|secret|\bkey\b|auth|api_?key|connection_?string)",
        RegexOptions.Compiled
    );

    private static readonly Regex ConfigInitializerRegex = new(
        @"(?i)(process\.env|Configuration\[|Environment\.GetEnvironmentVariable|System\.Environment|import\.meta\.env|dotenv|require\(['""]dotenv['""]\))",
        RegexOptions.Compiled
    );

    private static readonly Regex EtlRegex = new(
        @"(?i)(\betl\b|\bsql\b|\bquery\b|\bselect\b|\binsert\b|\bupsert\b|\bschema\b|\btable\b|\bcolumn\b|\bdatabase\b|\bmigration\b|\bextract\b|\btransform\b|\bload\b)",
        RegexOptions.Compiled
    );

    private static readonly Regex SqlQueryRegex = new(
        @"(?i)^[\s@$""'\`]*\s*(select|insert|update|delete|create\s+table|drop\s+table|merge|alter\s+table)\b",
        RegexOptions.Compiled
    );

    public virtual async Task EnrichAsync(ProjectNode projectNode, ParsingContext ctx)
    {
        var packageNames = projectNode.Children
            .OfType<PackageNode>()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Detect and enrich project-level framework using the Trie
        ILibraryParser? frameworkParser = null;
        if (packageNames.Count > 0)
        {
            foreach (var pkg in packageNames)
            {
                var match = _trieRegistry.Match(pkg);
                if (match is { Type: OntologyConstants.LibraryTypes.Framework } && frameworkParser == null)
                {
                    frameworkParser = match;
                    break;
                }
            }
        }

        var fileNode = _syntaxTree.FileNode;
        if (fileNode != null)
        {
            var relativePath = fileNode.Path;
            // Extract libraries used as list of string
            var fileImports = _syntaxTree.RawImports
                .Where(i => i.Type == ImportType.External)
                .Select(i => i.Path)
                .ToList();

            var matchedParsers = new List<ILibraryParser>();
            foreach (var import in fileImports)
            {
                var match = _trieRegistry.Match(import);
                if (match != null && !matchedParsers.Contains(match))
                {
                    matchedParsers.Add(match);
                }

                if (match is { Type: OntologyConstants.LibraryTypes.Framework } && frameworkParser == null)
                {
                    frameworkParser = match;
                }
            }

            if (frameworkParser != null)
            {
                projectNode.SetExtension(OntologyConstants.LibraryTypes.Framework, frameworkParser.Name);
            }

            foreach (var parser in matchedParsers)
            {
                var mainType = parser.Type.Split(':')[0].ToLowerInvariant();
                switch (mainType)
                {
                    case "db":
                        var dbEngine = parser.Name;
                        var dbType = "unknown";
                        var parts = parser.Type.Split(':');
                        if (parts.Length > 1)
                        {
                            dbType = parts[1];
                        }

                        var isOrm = IsOrmLibrary(parser.Id);
                        
                        // If it's an ORM, record API usage
                        if (isOrm)
                        {
                            var ormApiId = $"{projectNode.Id}orm:{parser.Id}";
                            var semanticNode = ctx.SemanticStructure;
                            if (semanticNode != null && !semanticNode.Children.Any(c => c.Id == ormApiId))
                            {
                                var ormNode = new ApiInUseNode(ormApiId, parser.Name, fileNode.Path);
                                semanticNode.Children.Add(ormNode);
                            }
                            var usesOrmRel = new UsesApiRelationship(fileNode.Id, ormApiId);
                            ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesOrmRel));
                        }

                        var (detectedEngine, detectedType) = isOrm
                            ? DetectProjectDatabaseDriver(projectNode, ctx)
                            : (null, null);

                        var targetEngine = !string.IsNullOrEmpty(detectedEngine)
                            ? detectedEngine
                            : (!string.IsNullOrEmpty(dbEngine) && !dbEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                                ? ResourceReconciliationService.NormalizeEngineName(dbEngine)
                                : "PostgreSQL");

                        if (!string.IsNullOrEmpty(detectedType)) dbType = detectedType;
                        else if (dbType == "unknown") dbType = "relational";

                        var declaredSchema = dbType.Equals("relational", StringComparison.OrdinalIgnoreCase)
                            ? (DetectDeclaredSchema(projectNode, fileNode, _syntaxTree) ?? GetDefaultSchemaForEngine(targetEngine, dbType))
                            : null;

                        var canonicalDbName = !string.IsNullOrEmpty(declaredSchema)
                            ? $"{targetEngine}.{declaredSchema}"
                            : targetEngine;

                        CanonicalResource? canonicalRes = ctx.ResourceRegistry.ResolveResource(canonicalDbName, expectedDbType: dbType, expectedEngine: targetEngine);

                        if (canonicalRes == null)
                        {
                            var aliasList = new List<string> { canonicalDbName, targetEngine.ToLowerInvariant(), parser.Id.ToLowerInvariant() };
                            if (!string.IsNullOrEmpty(declaredSchema)) aliasList.Add(declaredSchema);

                            canonicalRes = ctx.ResourceRegistry.RegisterResource(
                                ctx.WorkspaceId,
                                canonicalDbName,
                                targetEngine,
                                dbType,
                                OntologyConstants.NodeLabels.Database,
                                fileNode.Path,
                                projectNode.Id,
                                aliasList
                            );
                        }

                        var dbId = canonicalRes.Id;

                        var extensions = new Dictionary<string, string>
                        {
                            ["name"] = canonicalDbName,
                            ["engine"] = targetEngine,
                            ["provider"] = parser.Name
                        };
                        if (!string.IsNullOrEmpty(declaredSchema))
                        {
                            extensions["schema"] = declaredSchema;
                        }
                        if (isOrm)
                        {
                            extensions["is_orm"] = "true";
                        }

                        var semanticNodeForDb = ctx.SemanticStructure;
                        if (semanticNodeForDb != null)
                        {
                            // Remove any previous generic placeholder Database node that might have been registered before a driver was detected
                            semanticNodeForDb.Children.RemoveAll(c => c is DatabaseNode dn && (string.Equals(dn.Name, "Database", StringComparison.OrdinalIgnoreCase) || dn.Id.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase) || dn.Id.EndsWith($":{OntologyConstants.IdPrefixes.Database}:relational:database", StringComparison.OrdinalIgnoreCase)));

                            if (!semanticNodeForDb.Children.Any(c => c.Id == dbId))
                            {
                                var dbNode = new DatabaseNode(dbId, canonicalDbName, fileNode.Path, dbType, extensions);
                                semanticNodeForDb.Children.Add(dbNode);
                                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, canonicalDbName, dbId);
                            }
                        }

                        var relExt = new Dictionary<string, string>
                        {
                            ["via"] = parser.Name,
                            ["provider"] = parser.Id
                        };
                        if (!string.IsNullOrEmpty(declaredSchema))
                        {
                            relExt["schema"] = declaredSchema;
                        }
                        if (isOrm)
                        {
                            relExt["is_orm"] = "true";
                        }

                        var usesDbRel = new UsesDbRelationship(fileNode.Id, dbId, relExt);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesDbRel));
                        var projUsesDbRel = new UsesDbRelationship(projectNode.Id, dbId, relExt);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(projUsesDbRel));
                        break;

                    case "api":
                        var apiId = $"{projectNode.Id}api:{parser.Id}";
                        var semanticNodeForApi = ctx.SemanticStructure;
                        if (semanticNodeForApi != null && !semanticNodeForApi.Children.Any(c => c.Id == apiId))
                        {
                            var apiNode = new ApiInUseNode(apiId, parser.Name, apiId);
                            semanticNodeForApi.Children.Add(apiNode);
                        }

                        var usesApiRel = new UsesApiRelationship(fileNode.Id, apiId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesApiRel));
                        var projUsesApiRel = new UsesApiRelationship(projectNode.Id, apiId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(projUsesApiRel));
                        break;

                    case "cloud":
                        var cloudService = parser.Name;
                        var cloudId = $"{projectNode.Id}cloud:{parser.Id}";
                        var semanticNodeForCloud = ctx.SemanticStructure;
                        if (semanticNodeForCloud != null && !semanticNodeForCloud.Children.Any(c => c.Id == cloudId))
                        {
                            var cloudNode = new CloudServiceNode(cloudId, cloudService, "CloudService", cloudId);
                            semanticNodeForCloud.Children.Add(cloudNode);
                        }

                        var usesCloudRel = new UsesCloudRelationship(fileNode.Id, cloudId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesCloudRel));
                        var projUsesCloudRel = new UsesCloudRelationship(projectNode.Id, cloudId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(projUsesCloudRel));
                        break;
                }
            }

            var fileVariables = _syntaxTree.RawVariables;
            foreach (var rawVar in fileVariables)
            {
                var isConfig = ConfigRegex.IsMatch(rawVar.Name) ||
                               ConfigInitializerRegex.IsMatch(rawVar.InitializerText);
                var isEtl = EtlRegex.IsMatch(rawVar.Name) || SqlQueryRegex.IsMatch(rawVar.InitializerText);
                var isConstant = rawVar.IsConstant;
                var isGlobal = rawVar.Scope == "global";

                if (isConfig || isEtl || isConstant || isGlobal)
                {
                    var varTypeStr = new List<string>();
                    if (isConfig) varTypeStr.Add("config");
                    if (isEtl) varTypeStr.Add("etl");
                    if (isConstant) varTypeStr.Add("constant");
                    if (isGlobal) varTypeStr.Add("global");

                    var varType = string.Join(",", varTypeStr);
                    var varId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Member:{rawVar.Name}:{rawVar.StartLine}";

                    var ext = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["variable_type"] = varType,
                        ["initializer_expression"] = rawVar.InitializerText,
                        ["is_constant"] = isConstant ? "true" : "false"
                    };

                    var varNode = new MemberNode(
                        varId,
                        rawVar.Name,
                        varId,
                        fileNode.FullPath,
                        varId,
                        rawVar.StartLine,
                        rawVar.EndLine,
                        rawVar.StartCol,
                        rawVar.EndCol,
                        "variable",
                        ext
                    );

                    TryInsertVariable(fileNode, varNode, rawVar.StartLine);
                }
            }
        }

        await Task.CompletedTask;
    }

    private static bool TryInsertVariable(IOntologyNode parentNode, MemberNode varNode, int line)
    {
        foreach (var child in parentNode.Children)
        {
            if (child is TypeNode cn && line >= cn.StartLine && line <= cn.EndLine)
            {
                if (TryInsertVariable(cn, varNode, line)) return true;
            }

            if (child is FunctionNode fn && line >= fn.StartLine && line <= fn.EndLine)
            {
                if (TryInsertVariable(fn, varNode, line)) return true;
            }
        }

        parentNode.Children.Add(varNode);
        return true;
    }

    public static bool IsOrmLibrary(string parserId)
    {
        var lower = (parserId ?? "").ToLowerInvariant();
        return lower is "ef-core" or "microsoft.entityframeworkcore" or "dapper" or "typeorm" or
               "prisma" or "hibernate" or "nhibernate" or "sequelize" or "drizzle" or "sqlalchemy" or
               "peewee" or "gorm" or "jpa" or "jdbctemplate" or "knex";
    }

    private static readonly ConcurrentDictionary<string, string> _projectSchemaCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex SchemaCallRegex = new(@"HasDefaultSchema\s*\(\s*[""']([^""']+)[""']\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex SchemaConstRegex = new(@"(?:SchemaName|DefaultSchemaName)\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TypeOrmSchemaRegex = new(@"@Entity\s*\(\s*\{[^}]*schema\s*:\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ToTableSchemaRegex = new(@"ToTable\s*\(\s*[""'][^""']+[""']\s*,\s*(?:schema:\s*)?[""']([^""']+)[""']\s*\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? DetectDeclaredSchema(ProjectNode? projectNode, FileNode? fileNode, SyntaxTree? syntaxTree)
    {
        // 1. Check if any symbol in fileNode has schema extension
        if (fileNode != null)
        {
            var found = FindSchemaInNode(fileNode);
            if (!string.IsNullOrEmpty(found))
            {
                if (projectNode != null) _projectSchemaCache[projectNode.Id] = found;
                return found;
            }
        }

        // 2. Scan source file text if available
        if (syntaxTree != null && !string.IsNullOrEmpty(syntaxTree.FilePath) && File.Exists(syntaxTree.FilePath))
        {
            var fileSchema = ScanFileTextForSchema(syntaxTree.FilePath);
            if (!string.IsNullOrEmpty(fileSchema))
            {
                if (projectNode != null) _projectSchemaCache[projectNode.Id] = fileSchema;
                return fileSchema;
            }
        }

        // 3. Check project cache
        if (projectNode != null && _projectSchemaCache.TryGetValue(projectNode.Id, out var cachedSchema))
        {
            return cachedSchema;
        }

        // 4. Infer from DbContext or Model class name in the file name
        if (fileNode != null && !string.IsNullOrEmpty(fileNode.Name))
        {
            var fileName = Path.GetFileNameWithoutExtension(fileNode.Name);
            var lower = fileName.ToLowerInvariant();
            if (lower.EndsWith("dbcontext") && lower.Length > 9)
            {
                var inferred = lower[..^9].Trim('_', '-');
                if (!string.IsNullOrEmpty(inferred) && !ResourceReconciliationService.IsGenericConfigKey(inferred))
                {
                    if (projectNode != null) _projectSchemaCache[projectNode.Id] = inferred;
                    return inferred;
                }
            }
            if (lower.EndsWith("context") && lower.Length > 7)
            {
                var inferred = lower[..^7].Trim('_', '-');
                if (!string.IsNullOrEmpty(inferred) && !ResourceReconciliationService.IsGenericConfigKey(inferred))
                {
                    if (projectNode != null) _projectSchemaCache[projectNode.Id] = inferred;
                    return inferred;
                }
            }
        }

        // 5. Infer from project name if project is named e.g. Lidoma.Tournament or tournament-service
        if (projectNode != null && !string.IsNullOrEmpty(projectNode.Name))
        {
            var pName = projectNode.Name;
            var lastDot = pName.LastIndexOf('.');
            var segment = lastDot >= 0 ? pName[(lastDot + 1)..] : pName;
            var cleanSegment = segment.ToLowerInvariant().Replace("service", "").Replace("api", "").Trim('_', '-');
            if (!string.IsNullOrEmpty(cleanSegment) && !ResourceReconciliationService.IsGenericConfigKey(cleanSegment) && cleanSegment.Length >= 3)
            {
                return cleanSegment;
            }
        }

        return null;
    }

    private static string? ScanFileTextForSchema(string filePath)
    {
        try
        {
            var text = File.ReadAllText(filePath);
            var m1 = SchemaCallRegex.Match(text);
            if (m1.Success) return m1.Groups[1].Value.Trim();

            var m2 = SchemaConstRegex.Match(text);
            if (m2.Success) return m2.Groups[1].Value.Trim();

            var m3 = TypeOrmSchemaRegex.Match(text);
            if (m3.Success) return m3.Groups[1].Value.Trim();

            var m4 = ToTableSchemaRegex.Match(text);
            if (m4.Success) return m4.Groups[1].Value.Trim();
        }
        catch { }
        return null;
    }

    private static string? FindSchemaInNode(IOntologyNode node)
    {
        if (node.Extensions != null && node.Extensions.TryGetValue("schema", out var s) && !string.IsNullOrWhiteSpace(s))
        {
            return s;
        }
        foreach (var child in node.Children)
        {
            var found = FindSchemaInNode(child);
            if (!string.IsNullOrEmpty(found)) return found;
        }
        return null;
    }

    public static string GetDefaultSchemaForEngine(string engine, string dbType)
    {
        var lower = (engine ?? "").ToLowerInvariant();
        if (lower.Contains("postgres")) return "public";
        if (lower.Contains("sqlserver") || lower.Contains("sql server") || lower.Contains("mssql")) return "dbo";
        if (lower.Contains("sqlite")) return "main";
        if (lower.Contains("redis")) return "cache";
        if (lower.Contains("mongo")) return "default";
        if (dbType.Equals("cache", StringComparison.OrdinalIgnoreCase)) return "cache";
        return "public";
    }

    private static readonly ConcurrentDictionary<string, (string? Engine, string? DbType)> _projectDriverCache = new(StringComparer.OrdinalIgnoreCase);

    private static (string? Engine, string? DbType) DetectProjectDatabaseDriver(ProjectNode projectNode, ParsingContext ctx)
    {
        if (projectNode == null) return (null, null);
        if (_projectDriverCache.TryGetValue(projectNode.Id, out var cached)) return cached;

        try
        {
            var projectAbsDir = Path.IsPathRooted(projectNode.Path)
                ? projectNode.Path
                : Path.GetFullPath(Path.Combine(ctx.AbsoluteWorkspacePath, projectNode.Path)).Replace('\\', '/');

            if (!Directory.Exists(projectAbsDir))
            {
                _projectDriverCache[projectNode.Id] = (null, null);
                return (null, null);
            }

            // 1. TypeScript / JavaScript: package.json
            var pkgJsonPath = Path.Combine(projectAbsDir, "package.json");
            if (File.Exists(pkgJsonPath))
            {
                var content = File.ReadAllText(pkgJsonPath);
                var lower = content.ToLowerInvariant();
                if (lower.Contains("\"pg\"") || lower.Contains("\"pg-promise\"") || lower.Contains("\"@types/pg\"") || lower.Contains("\"postgres\""))
                {
                    var res = ("PostgreSQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"mysql\"") || lower.Contains("\"mysql2\""))
                {
                    var res = ("MySQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"sqlite3\"") || lower.Contains("\"better-sqlite3\""))
                {
                    var res = ("SQLite", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"mssql\"") || lower.Contains("\"tedious\""))
                {
                    var res = ("SQL Server", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"oracledb\""))
                {
                    var res = ("Oracle", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"mongodb\""))
                {
                    var res = ("MongoDB", "document");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("\"redis\"") || lower.Contains("\"ioredis\""))
                {
                    var res = ("Redis", "cache");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }

            // Fallback for monorepos: check workspace root package.json
            var rootPkgJson = Path.Combine(ctx.AbsoluteWorkspacePath, "package.json");
            if (File.Exists(rootPkgJson))
            {
                var rootContent = File.ReadAllText(rootPkgJson).ToLowerInvariant();
                if (rootContent.Contains("\"pg\"") || rootContent.Contains("\"pg-promise\"") || rootContent.Contains("\"postgres\""))
                {
                    var res = ("PostgreSQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }

            // 2. C# (.csproj files, transitive ProjectReferences, Directory.Build.props)
            var csprojFiles = Directory.GetFiles(projectAbsDir, "*.csproj", SearchOption.TopDirectoryOnly);
            if (csprojFiles.Length > 0)
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var csproj in csprojFiles)
                {
                    var res = DetectDriverFromCsproj(csproj, ctx.AbsoluteWorkspacePath, visited);
                    if (res.Engine != null)
                    {
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                }

                var propsRes = DetectDriverFromDirectoryBuildProps(projectAbsDir, ctx.AbsoluteWorkspacePath);
                if (propsRes.Engine != null)
                {
                    _projectDriverCache[projectNode.Id] = propsRes;
                    return propsRes;
                }
            }

            // Also check appsettings*.json in project directory
            var appsettingsFiles = Directory.GetFiles(projectAbsDir, "appsettings*.json", SearchOption.TopDirectoryOnly);
            foreach (var appsetting in appsettingsFiles)
            {
                try
                {
                    var content = File.ReadAllText(appsetting).ToLowerInvariant();
                    if (content.Contains("port=5432") || content.Contains("username=postgres") || content.Contains("npgsql") || content.Contains("database=postgres"))
                    {
                        var res = ("PostgreSQL", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (content.Contains("initial catalog=") || content.Contains("trusted_connection=") || content.Contains("server=localhost;database="))
                    {
                        var res = ("SQL Server", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (content.Contains("port=3306") || content.Contains("uid=root") || content.Contains("user id=root"))
                    {
                        var res = ("MySQL", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                    if (content.Contains("data source=") && (content.Contains(".db") || content.Contains(".sqlite")))
                    {
                        var res = ("SQLite", "relational");
                        _projectDriverCache[projectNode.Id] = res;
                        return res;
                    }
                }
                catch { }
            }

            // 3. Python (requirements.txt, pyproject.toml)
            var reqTxt = Path.Combine(projectAbsDir, "requirements.txt");
            if (File.Exists(reqTxt))
            {
                var lower = File.ReadAllText(reqTxt).ToLowerInvariant();
                if (lower.Contains("psycopg") || lower.Contains("asyncpg"))
                {
                    var res = ("PostgreSQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("pymysql") || lower.Contains("mysql"))
                {
                    var res = ("MySQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (lower.Contains("sqlite"))
                {
                    var res = ("SQLite", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }

            // 4. Also scan files in config folder for db.config.ts / data-source.ts / etc.
            var configFiles = Directory.GetFiles(projectAbsDir, "*config*.ts", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(projectAbsDir, "*datasource*.ts", SearchOption.AllDirectories))
                .Take(10);
            foreach (var cfg in configFiles)
            {
                var text = File.ReadAllText(cfg).ToLowerInvariant();
                if (text.Contains("type: 'postgres'") || text.Contains("type: \"postgres\"") ||
                    text.Contains("dialect: 'postgres'") || text.Contains("dialect: \"postgres\""))
                {
                    var res = ("PostgreSQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (text.Contains("type: 'mysql'") || text.Contains("type: \"mysql\"") ||
                    text.Contains("dialect: 'mysql'") || text.Contains("dialect: \"mysql\""))
                {
                    var res = ("MySQL", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
                if (text.Contains("type: 'sqlite'") || text.Contains("type: \"sqlite\"") ||
                    text.Contains("dialect: 'sqlite'") || text.Contains("dialect: \"sqlite\""))
                {
                    var res = ("SQLite", "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }

            // 5. Fallback: check if another project in the workspace or config already registered a concrete relational database
            if (ctx.ResourceRegistry != null)
            {
                var existingConcreteRelational = ctx.ResourceRegistry.AllResources
                    .Where(r => string.Equals(r.DbType, "relational", StringComparison.OrdinalIgnoreCase) &&
                                !CodeExplorer.Core.Analysis.ResourceReconciliationService.IsGenericConfigKey(r.Engine) &&
                                !string.Equals(r.Name, "Database", StringComparison.OrdinalIgnoreCase))
                    .Select(r => r.Engine)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (existingConcreteRelational.Count == 1)
                {
                    var res = (existingConcreteRelational[0], "relational");
                    _projectDriverCache[projectNode.Id] = res;
                    return res;
                }
            }
        }
        catch (Exception ex)
        {
            ctx.LogWarning($"[SyntaxEnricher] Driver detection failed for {projectNode.Id}: {ex.Message}");
        }

        _projectDriverCache[projectNode.Id] = (null, null);
        return (null, null);
    }

    private static (string? Engine, string? DbType) DetectDriverFromCsproj(
        string csprojPath,
        string workspaceRoot,
        HashSet<string> visited,
        int depth = 0)
    {
        if (depth > 5 || string.IsNullOrEmpty(csprojPath)) return (null, null);

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(csprojPath).Replace('\\', '/');
        }
        catch
        {
            return (null, null);
        }

        if (!visited.Add(fullPath) || !File.Exists(fullPath)) return (null, null);

        try
        {
            var content = File.ReadAllText(fullPath);
            var lower = content.ToLowerInvariant();

            if (lower.Contains("npgsql"))
                return ("PostgreSQL", "relational");
            if (lower.Contains("sqlclient") || lower.Contains("entityframeworkcore.sqlserver"))
                return ("SQL Server", "relational");
            if (lower.Contains("mysql") || lower.Contains("pomelo"))
                return ("MySQL", "relational");
            if (lower.Contains("sqlite"))
                return ("SQLite", "relational");
            if (lower.Contains("oracle"))
                return ("Oracle", "relational");

            // Follow ProjectReference
            var dir = Path.GetDirectoryName(fullPath) ?? "";
            var matches = Regex.Matches(content, @"<ProjectReference\s+Include=""([^""]+)""", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var relRef = m.Groups[1].Value.Trim();
                relRef = Regex.Replace(relRef, @"\$\([A-Za-z0-9_]+\)[\\/]*", "");
                var candidate = Path.Combine(dir, relRef);
                var found = DetectDriverFromCsproj(candidate, workspaceRoot, visited, depth + 1);
                if (found.Engine != null) return found;

                if (!File.Exists(candidate) && !string.IsNullOrEmpty(workspaceRoot))
                {
                    var wsCandidate = Path.Combine(workspaceRoot, relRef);
                    found = DetectDriverFromCsproj(wsCandidate, workspaceRoot, visited, depth + 1);
                    if (found.Engine != null) return found;

                    // Fallback: look up by filename in workspace
                    var csprojMap = GetWorkspaceCsprojMap(workspaceRoot);
                    var projFileName = Path.GetFileName(relRef);
                    if (csprojMap.TryGetValue(projFileName, out var mappedPath))
                    {
                        found = DetectDriverFromCsproj(mappedPath, workspaceRoot, visited, depth + 1);
                        if (found.Engine != null) return found;
                    }
                }
            }
        }
        catch { }

        return (null, null);
    }

    private static (string? Engine, string? DbType) DetectDriverFromDirectoryBuildProps(string projectAbsDir, string workspaceRoot)
    {
        try
        {
            var curr = new DirectoryInfo(projectAbsDir);
            var root = new DirectoryInfo(workspaceRoot);
            while (curr != null)
            {
                var p1 = Path.Combine(curr.FullName, "Directory.Packages.props");
                var p2 = Path.Combine(curr.FullName, "Directory.Build.props");
                foreach (var p in new[] { p1, p2 })
                {
                    if (File.Exists(p))
                    {
                        var content = File.ReadAllText(p).ToLowerInvariant();
                        if (content.Contains("npgsql")) return ("PostgreSQL", "relational");
                        if (content.Contains("sqlclient") || content.Contains("entityframeworkcore.sqlserver")) return ("SQL Server", "relational");
                        if (content.Contains("mysql") || content.Contains("pomelo")) return ("MySQL", "relational");
                        if (content.Contains("sqlite")) return ("SQLite", "relational");
                        if (content.Contains("oracle")) return ("Oracle", "relational");
                    }
                }
                if (string.Equals(curr.FullName, root.FullName, StringComparison.OrdinalIgnoreCase))
                    break;
                curr = curr.Parent;
            }
        }
        catch { }

        return (null, null);
    }

    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> _workspaceCsprojCache = new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> GetWorkspaceCsprojMap(string workspaceRoot)
    {
        if (string.IsNullOrEmpty(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return _workspaceCsprojCache.GetOrAdd(workspaceRoot, root =>
        {
            try
            {
                var files = Directory.GetFiles(root, "*.csproj", SearchOption.AllDirectories);
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in files)
                {
                    var name = Path.GetFileName(f);
                    map.TryAdd(name, f.Replace('\\', '/'));
                }
                return map;
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        });
    }
}
