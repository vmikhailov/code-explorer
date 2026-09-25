using System.Collections.Concurrent;
using CodeExplorer.Core.Analysis;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
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

                        CanonicalResource? canonicalRes = null;

                        if (isOrm)
                        {
                            // For an ORM, parser.Name (e.g. "TypeORM") is an abstraction library, NOT a physical database server.
                            // Attempt to detect the actual database driver / engine for the project (e.g., PostgreSQL from 'pg' package or config).
                            var (detectedEngine, detectedType) = DetectProjectDatabaseDriver(projectNode, ctx);
                            if (!string.IsNullOrEmpty(detectedEngine))
                            {
                                dbEngine = detectedEngine;
                                if (!string.IsNullOrEmpty(detectedType)) dbType = detectedType;
                                canonicalRes = ctx.ResourceRegistry.ResolveResource(dbEngine, expectedDbType: dbType, expectedEngine: dbEngine)
                                               ?? ctx.ResourceRegistry.ResolveResource(null, expectedDbType: dbType, expectedEngine: dbEngine);
                            }

                            // If driver not detected, check if workspace/project already has an existing relational database
                            canonicalRes ??= ctx.ResourceRegistry.ResolveResource(null, expectedDbType: dbType, expectedEngine: null);

                            if (canonicalRes == null)
                            {
                                var targetEngine = !string.IsNullOrEmpty(detectedEngine) ? detectedEngine : "Database";
                                canonicalRes = ctx.ResourceRegistry.RegisterResource(
                                    ctx.WorkspaceId,
                                    targetEngine,
                                    targetEngine,
                                    dbType,
                                    OntologyConstants.NodeLabels.Database,
                                    fileNode.Path,
                                    projectNode.Id,
                                    [!string.IsNullOrEmpty(detectedEngine) ? detectedEngine.ToLowerInvariant() : "database", "relational", parser.Id.ToLowerInvariant()]
                                );
                            }
                        }
                        else
                        {
                            // Direct database driver (e.g. pg, mysql2, sqlite3, redis)
                            canonicalRes = ctx.ResourceRegistry.ResolveResource(parser.Id, expectedDbType: dbType, expectedEngine: dbEngine)
                                           ?? ctx.ResourceRegistry.ResolveResource(parser.Name, expectedDbType: dbType, expectedEngine: dbEngine)
                                           ?? ctx.ResourceRegistry.ResolveResource(null, expectedDbType: dbType, expectedEngine: dbEngine);

                            if (canonicalRes == null)
                            {
                                canonicalRes = ctx.ResourceRegistry.RegisterResource(
                                    ctx.WorkspaceId,
                                    dbEngine,
                                    dbEngine,
                                    dbType,
                                    OntologyConstants.NodeLabels.Database,
                                    fileNode.Path,
                                    projectNode.Id,
                                    [parser.Id, parser.Name]
                                );
                            }
                        }

                        var dbId = canonicalRes.Id;
                        var canonicalDbName = canonicalRes.Name;

                        var extensions = new Dictionary<string, string>
                        {
                            ["engine"] = canonicalRes.Engine,
                            ["provider"] = parser.Name
                        };
                        if (isOrm)
                        {
                            extensions["is_orm"] = "true";
                        }

                        var semanticNodeForDb = ctx.SemanticStructure;
                        if (semanticNodeForDb != null)
                        {
                            var isConcreteDb = !string.Equals(canonicalDbName, "Database", StringComparison.OrdinalIgnoreCase) &&
                                               !string.Equals(canonicalRes.Engine, "Database", StringComparison.OrdinalIgnoreCase);

                            if (isConcreteDb)
                            {
                                // Remove any previous generic placeholder Database node that might have been registered before a driver was detected
                                semanticNodeForDb.Children.RemoveAll(c => c is DatabaseNode dn && (string.Equals(dn.Name, "Database", StringComparison.OrdinalIgnoreCase) || dn.Id.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase)));
                            }

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
                    var varId = $"{ctx.WorkspaceId}:symbol:{relativePath}:Member:{rawVar.Name}:{rawVar.StartLine}";

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
