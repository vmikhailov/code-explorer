using System.Text.RegularExpressions;
using CodeExplorer.Core.Analysis;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Parser;

public partial class SyntaxEnricher : ISyntaxEnricher
{
    private readonly IReadOnlyList<ISemanticExtension> _semanticExtensions;
    private readonly SemanticExtensionRegistry _trieRegistry;
    private readonly SyntaxTree _syntaxTree;

    public SyntaxEnricher(
        IReadOnlyList<ISemanticExtension> semanticExtensions,
        SyntaxTree syntaxTree,
        IReadOnlyList<PackageDescriptor>? packages = null)
    {
        var allExtensions = new List<ISemanticExtension>(semanticExtensions);
        if (packages != null)
        {
            var existingIds = semanticExtensions.Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var pkg in packages)
            {
                if (!existingIds.Contains(pkg.Id))
                {
                    allExtensions.Add(new PackageSemanticExtensionAdapter(pkg));
                }
            }
        }

        _semanticExtensions = allExtensions;
        _trieRegistry = new SemanticExtensionRegistry(allExtensions);
        _syntaxTree = syntaxTree;
    }

    private sealed record PackageSemanticExtensionAdapter(PackageDescriptor Package) : ISemanticExtension
    {
        public string Id => Package.Id;
        public string Name => Package.Name;
        public string Type => Package.Role switch
        {
            LibraryRole.CloudSdk => "cloud",
            LibraryRole.OrmOrDatabase => Package.Type.StartsWith("db") ? Package.Type : "db",
            LibraryRole.WebFramework or LibraryRole.FrontendFramework => OntologyConstants.LibraryTypes.Framework,
            LibraryRole.MessageBroker => "messaging",
            LibraryRole.General => "api",
            _ => "library"
        };
        public LibraryRole Role => Package.Role;
        public IReadOnlyList<string> SupportedPatterns => Package.SupportedPatterns;
        public bool IsImplemented => false;
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
        ArgumentNullException.ThrowIfNull(projectNode);
        var packageNames = projectNode.Children
            .OfType<PackageNode>()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Detect and enrich project-level framework using the Trie
        ISemanticExtension? frameworkParser = null;
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
                .Where(i => i.Type == ImportType.External || _trieRegistry.Match(i.Path) != null)
                .Select(i => i.Path)
                .ToList();

            var matchedParsers = new List<ISemanticExtension>();
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

            if (!matchedParsers.Any(p => p.Type.StartsWith("db", StringComparison.OrdinalIgnoreCase)))
            {
                if (_syntaxTree.RawImports.Any(i => i.Path.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
                                                    i.Path.Contains("lib/pq", StringComparison.OrdinalIgnoreCase) ||
                                                    i.Path.Contains("database/sql", StringComparison.OrdinalIgnoreCase) ||
                                                    i.Path.Contains("sqlx", StringComparison.OrdinalIgnoreCase)) &&
                    DetectDeclaredSchemas(projectNode, fileNode, _syntaxTree, "relational").Count > 0)
                {
                    var ext = _semanticExtensions.FirstOrDefault(e => e.Id.Contains("sqlx", StringComparison.OrdinalIgnoreCase) ||
                                                                      (e.Type.StartsWith("db:relational", StringComparison.OrdinalIgnoreCase) && e.Name.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)))
                              ?? new PackageSemanticExtensionAdapter(new PackageDescriptor("postgres-client", "PostgreSQL", LibraryRole.OrmOrDatabase, "db:relational", ["*postgres*"]));
                    matchedParsers.Add(ext);
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
                    {
                        var dbEngine = parser.Name;
                        var dbType = "unknown";
                        var parts = parser.Type.Split(':');
                        if (parts.Length > 1)
                        {
                            dbType = parts[1];
                        }

                        var isOrm = IsOrmLibrary(parser.Id);

                        // Always record USES_API for the ORM / DB driver library
                        var apiPrefix = isOrm ? "orm" : "api";
                        var ormApiId = $"{projectNode.Id}{apiPrefix}:{parser.Id}";
                        var semanticNode = ctx.SemanticStructure;
                        if (semanticNode != null && !semanticNode.Children.Any(c => c.Id == ormApiId))
                        {
                            var apiNode = new ApiInUseNode(ormApiId, parser.Name, fileNode.Path);
                            semanticNode.Children.Add(apiNode);
                        }
                        var ormUsesApiRel = new UsesApiRelationship(fileNode.Id, ormApiId);
                        ctx.AddGlobalProjectDependency(Relationship.FromRelationship(ormUsesApiRel));

                        var (detectedEngine, detectedType) = isOrm
                            ? DetectProjectDatabaseDriver(projectNode, ctx)
                            : (null, null);

                        if (parser.Id.Equals("sqlx", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(detectedEngine))
                        {
                            var fileText = _syntaxTree.FileNode?.FullPath != null && File.Exists(_syntaxTree.FileNode.FullPath)
                                ? File.ReadAllText(_syntaxTree.FileNode.FullPath)
                                : string.Empty;
                            if (fileText.Contains("PgPool", StringComparison.OrdinalIgnoreCase) ||
                                fileText.Contains("postgres", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedEngine = "PostgreSQL";
                                detectedType = "relational";
                            }
                            else if (fileText.Contains("MySqlPool", StringComparison.OrdinalIgnoreCase) ||
                                     fileText.Contains("mysql", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedEngine = "MySQL";
                                detectedType = "relational";
                            }
                            else if (fileText.Contains("SqlitePool", StringComparison.OrdinalIgnoreCase) ||
                                     fileText.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
                            {
                                detectedEngine = "SQLite";
                                detectedType = "relational";
                            }
                        }

                        var targetEngine = !string.IsNullOrEmpty(detectedEngine)
                            ? detectedEngine
                            : (!string.IsNullOrEmpty(dbEngine) && !dbEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                                ? ResourceReconciliationService.NormalizeEngineName(dbEngine)
                                : null);

                        if (!string.IsNullOrEmpty(detectedType)) dbType = detectedType;
                        else if (dbType == "unknown") dbType = "relational";

                        // Step A: Check DetectDeclaredSchemas(projectNode, fileNode, _syntaxTree, dbType)
                        var declaredSchemas = (dbType.Equals("relational", StringComparison.OrdinalIgnoreCase) || dbType.Equals("document", StringComparison.OrdinalIgnoreCase))
                            ? DetectDeclaredSchemas(projectNode, fileNode, _syntaxTree, dbType).ToList()
                            : new List<string>();

                        var declaredSchema = declaredSchemas.FirstOrDefault();
                        CanonicalResource? canonicalRes = null;

                        // Step B: If declaredSchemas.Count == 0, check if THIS exact project (r.ProjectId == projectNode.Id) ALREADY has a concrete configured :Database resource
                        if (declaredSchemas.Count == 0 && ctx.ResourceRegistry != null)
                        {
                            var projRes = ctx.ResourceRegistry.AllResources
                                .FirstOrDefault(r => r.Kind == OntologyConstants.NodeLabels.Database &&
                                                     (string.IsNullOrEmpty(targetEngine) || string.Equals(r.Engine, targetEngine, StringComparison.OrdinalIgnoreCase)) &&
                                                     r.ProjectId != null && string.Equals(r.ProjectId, projectNode.Id, StringComparison.OrdinalIgnoreCase) &&
                                                     !r.Name.Equals(targetEngine, StringComparison.OrdinalIgnoreCase) &&
                                                     !ResourceReconciliationService.IsGenericConfigKey(r.Name));

                            if (projRes == null && !string.IsNullOrEmpty(projectNode.Path))
                            {
                                var projDir = Path.GetDirectoryName(projectNode.Path)?.Replace('\\', '/').TrimEnd('/');
                                if (!string.IsNullOrEmpty(projDir))
                                {
                                    projRes = ctx.ResourceRegistry.AllResources
                                        .FirstOrDefault(r => r.Kind == OntologyConstants.NodeLabels.Database &&
                                                             (string.IsNullOrEmpty(targetEngine) || string.Equals(r.Engine, targetEngine, StringComparison.OrdinalIgnoreCase)) &&
                                                             !r.Name.Equals(targetEngine, StringComparison.OrdinalIgnoreCase) &&
                                                             !ResourceReconciliationService.IsGenericConfigKey(r.Name) &&
                                                             r.DeclaringFilePath != null &&
                                                             r.DeclaringFilePath.Replace('\\', '/').StartsWith(projDir + "/", StringComparison.OrdinalIgnoreCase));
                                }
                            }

                            if (projRes != null)
                            {
                                if (string.IsNullOrEmpty(targetEngine) && !string.IsNullOrEmpty(projRes.Engine))
                                {
                                    targetEngine = projRes.Engine;
                                }

                                var cfgSchema = projRes.Properties.TryGetValue("schema", out var s) ? s : null;
                                if (string.IsNullOrEmpty(cfgSchema) && projRes.Name.Contains('.'))
                                {
                                    cfgSchema = projRes.Name.Split('.', 2)[1];
                                }
                                else if (string.IsNullOrEmpty(cfgSchema) && !projRes.Name.Equals(targetEngine, StringComparison.OrdinalIgnoreCase) && !ResourceReconciliationService.IsGenericConfigKey(projRes.Name))
                                {
                                    cfgSchema = projRes.Name;
                                }

                                if (IsValidSchemaName(cfgSchema) && cfgSchema != null)
                                {
                                    declaredSchemas.Add(cfgSchema);
                                    declaredSchema = cfgSchema;
                                    canonicalRes = projRes;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(targetEngine) && targetEngine.Equals("SQLite", StringComparison.OrdinalIgnoreCase) &&
                            (declaredSchemas.Count == 0 || declaredSchema == null || declaredSchema.Equals("dbo", StringComparison.OrdinalIgnoreCase) || declaredSchema.Equals("default", StringComparison.OrdinalIgnoreCase)))
                        {
                            declaredSchemas.Clear();
                            declaredSchemas.Add("main");
                            declaredSchema = "main";
                        }

                        var isRelational = string.Equals(dbType, "relational", StringComparison.OrdinalIgnoreCase);

                        if (isRelational && declaredSchemas.Count == 0 && canonicalRes == null && !projectNode.IsLibrary && HasEntityOrTableDeclaration(fileNode, _syntaxTree))
                        {
                            var domain = CleanProjectNameToDomain(projectNode.Name);
                            if (!string.IsNullOrEmpty(domain) && IsValidSchemaName(domain))
                            {
                                declaredSchemas.Add(domain);
                            }
                            else
                            {
                                declaredSchemas.Add(GetDefaultSchemaForEngine(targetEngine ?? "PostgreSQL", dbType));
                            }
                        }

                        var explicitSchemas = new HashSet<string>(declaredSchemas, StringComparer.OrdinalIgnoreCase);

                        if (isRelational && declaredSchemas.Count == 0 && canonicalRes == null)
                        {
                            var implicitSqlSchema = FindImplicitSchemaInNode(fileNode);
                            if (!string.IsNullOrEmpty(implicitSqlSchema) && IsValidSchemaName(implicitSqlSchema))
                            {
                                declaredSchemas.Add(implicitSqlSchema);
                            }
                        }

                        // Step C: Strict Evidence Gate in SyntaxEnricher.cs:
                        // If after Step A and Step B we STILL have no concrete declaredSchema AND no resolved concrete canonicalRes from ResourceRegistry
                        // for relational databases (i.e. mere imports without entity/table definitions or schema):
                        // DO NOT create a DatabaseNode, DO NOT register an engine-only resource in ResourceRegistry, and DO NOT emit UsesDbRelationship! Simply break; out of case "db":!
                        if (string.IsNullOrEmpty(targetEngine) || (isRelational && declaredSchemas.Count == 0 && canonicalRes == null))
                        {
                            break;
                        }

                        var schemasToProcess = declaredSchemas.Count > 0 ? declaredSchemas : new List<string> { null! };

                        foreach (var schemaItem in schemasToProcess)
                        {
                            var currentSchema = schemaItem;
                            var isExplicitSchema = !string.IsNullOrEmpty(currentSchema) && explicitSchemas.Contains(currentSchema);

                            var canonicalDbName = canonicalRes != null
                                ? canonicalRes.Name
                                : (!string.IsNullOrEmpty(currentSchema) ? $"{targetEngine}.{currentSchema}" : targetEngine);

                            var resForSchema = canonicalRes;
                            if (resForSchema == null && !string.IsNullOrEmpty(currentSchema))
                            {
                                resForSchema = ctx.ResourceRegistry?.ResolveResource(canonicalDbName, expectedDbType: dbType, expectedEngine: targetEngine);
                                if (resForSchema == null)
                                {
                                    resForSchema = ctx.ResourceRegistry?.ResolveResource(currentSchema, expectedDbType: dbType, expectedEngine: targetEngine);
                                }
                                if (resForSchema != null)
                                {
                                    canonicalDbName = resForSchema.Name;
                                }
                            }

                            if (string.IsNullOrEmpty(canonicalDbName) ||
                                (isRelational && (canonicalDbName.Equals(targetEngine, StringComparison.OrdinalIgnoreCase) ||
                                                  canonicalDbName.Equals("Database", StringComparison.OrdinalIgnoreCase) ||
                                                  canonicalDbName.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                                                  IsOrmLibrary(canonicalDbName) ||
                                                  PostIndexAnalyzer.IsAbstractEngineOnlyDatabase(canonicalDbName, currentSchema, dbType, null))))
                            {
                                continue;
                            }

                            if (resForSchema == null && ctx.ResourceRegistry != null &&
                                (isExplicitSchema || string.IsNullOrEmpty(currentSchema) ||
                                 (!currentSchema.Equals("public", StringComparison.OrdinalIgnoreCase) &&
                                  !currentSchema.Equals("dbo", StringComparison.OrdinalIgnoreCase) &&
                                  !currentSchema.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                                  !currentSchema.Equals("main", StringComparison.OrdinalIgnoreCase))))
                            {
                                var aliasList = new List<string> { canonicalDbName };
                                if (!string.IsNullOrEmpty(currentSchema) &&
                                    !currentSchema.Equals("public", StringComparison.OrdinalIgnoreCase) &&
                                    !currentSchema.Equals("dbo", StringComparison.OrdinalIgnoreCase) &&
                                    !currentSchema.Equals("default", StringComparison.OrdinalIgnoreCase) &&
                                    !currentSchema.Equals("main", StringComparison.OrdinalIgnoreCase))
                                {
                                    aliasList.Add(currentSchema);
                                }

                                resForSchema = ctx.ResourceRegistry.RegisterResource(
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

                            var dbId = resForSchema?.Id ?? ResourceReconciliationService.BuildCanonicalDatabaseId(ctx.WorkspaceId, dbType, canonicalDbName);

                            var extensions = new Dictionary<string, string>
                            {
                                ["name"] = canonicalDbName,
                                ["engine"] = targetEngine,
                                ["provider"] = parser.Name
                            };
                            if (!string.IsNullOrEmpty(currentSchema))
                            {
                                extensions["schema"] = currentSchema;
                            }
                            if (isExplicitSchema)
                            {
                                extensions["is_explicit_schema"] = "true";
                            }
                            else if (!string.IsNullOrEmpty(currentSchema))
                            {
                                extensions["is_implicit_schema"] = "true";
                            }
                            if (projectNode.IsTest || WorkspaceConventions.IsTestFilePath(fileNode.Path))
                            {
                                extensions["is_test"] = "true";
                                extensions["role"] = "Test";
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
                                ["provider"] = parser.Id,
                                ["file_path"] = fileNode.Path
                            };
                            if (!string.IsNullOrEmpty(currentSchema))
                            {
                                relExt["schema"] = currentSchema;
                                if (!isExplicitSchema)
                                {
                                    relExt["is_implicit_schema"] = "true";
                                }
                            }
                            if (isOrm)
                            {
                                relExt["is_orm"] = "true";
                            }

                            var usesDbRel = new UsesDbRelationship(fileNode.Id, dbId, relExt);
                            ctx.AddGlobalProjectDependency(Relationship.FromRelationship(usesDbRel));
                            if (projectNode.IsTest || !WorkspaceConventions.IsTestFilePath(fileNode.Path))
                            {
                                var projUsesDbRel = new UsesDbRelationship(projectNode.Id, dbId, relExt);
                                ctx.AddGlobalProjectDependency(Relationship.FromRelationship(projUsesDbRel));
                            }
                        }
                        break;
                    }

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
                var isProperty = rawVar.MemberKind == "property";
                var isField = rawVar.MemberKind == "field";

                if (isConfig || isEtl || isConstant || isGlobal || isProperty || isField)
                {
                    var varTypeStr = new List<string>();
                    if (isConfig) varTypeStr.Add("config");
                    if (isEtl) varTypeStr.Add("etl");
                    if (isConstant) varTypeStr.Add("constant");
                    if (isGlobal) varTypeStr.Add("global");
                    if (isProperty) varTypeStr.Add("property");
                    if (isField) varTypeStr.Add("field");

                    var varType = string.Join(",", varTypeStr);
                    var varId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Symbol}:{relativePath}:Member:{rawVar.Name}:{rawVar.StartLine}";

                    var ext = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["variable_type"] = varType,
                        ["initializer_expression"] = rawVar.InitializerText,
                        ["is_constant"] = isConstant ? "true" : "false"
                    };
                    if (!string.IsNullOrEmpty(rawVar.TypeName))
                    {
                        ext["data_type"] = rawVar.TypeName;
                    }

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
                        rawVar.MemberKind ?? "variable",
                        rawVar.TypeName,
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
               "peewee" or "gorm" or "jpa" or "jdbctemplate" or "knex" or
               "sqlx" or "sea-orm" or "diesel";
    }

    private static readonly Regex ScopePrefixRegex = new(
        @"^(?:internal[-_]+service[-_]+|integration[-_]+service[-_]+|external[-_]+service[-_]+|internal[-_]+|integration[-_]+|external[-_]+|service[-_]+|srv[-_]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ScopeSuffixRegex = new(
        @"(?:[-_]+service|[-_]+api|[-_]+srv|[-_]+app|[-_]+worker)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> TechnicalLayerSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "logic", "graphql", "grapql", "contracts", "contract", "services", "service",
        "host", "api", "client", "tests", "test", "grpc", "common", "infrastructure", "infra",
        "repository", "repositories", "core", "models", "dto"
    };

    public static string CleanProjectNameToDomain(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName)) return string.Empty;
        var pName = projectName.Trim();

        var parts = pName.Split('.', StringSplitOptions.RemoveEmptyEntries).ToList();
        while (parts.Count > 1 && TechnicalLayerSuffixes.Contains(parts[^1]))
        {
            parts.RemoveAt(parts.Count - 1);
        }

        var servicesIdx = parts.FindLastIndex(p => p.Equals("services", StringComparison.OrdinalIgnoreCase) || p.Equals("service", StringComparison.OrdinalIgnoreCase));
        string segment;
        if (parts.Count >= 2 && (parts[^1].Equals("Bff", StringComparison.OrdinalIgnoreCase) || parts[^1].Equals("Bffs", StringComparison.OrdinalIgnoreCase)))
        {
            segment = parts[^2] + "-" + parts[^1];
        }
        else if (servicesIdx >= 0 && servicesIdx + 1 < parts.Count)
        {
            segment = parts[servicesIdx + 1];
        }
        else
        {
            segment = parts[^1];
        }

        var cleaned = WorkspaceConventions.NormalizeServiceName(segment);
        if (!cleaned.EndsWith("-bff", StringComparison.OrdinalIgnoreCase) && !cleaned.EndsWith("bff", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = ScopeSuffixRegex.Replace(cleaned, "");
        }
        cleaned = Regex.Replace(cleaned.ToLowerInvariant(), @"[-_]{2,}", "-").Trim('_', '-');
        return cleaned;
    }

    public static string? DetectDeclaredSchema(ProjectNode? projectNode, FileNode? fileNode, SyntaxTree? syntaxTree, string? dbType = null)
    {
        // 1. Check if any symbol in fileNode has explicit schema extension
        if (fileNode != null)
        {
            var found = FindSchemaInNode(fileNode);
            if (!string.IsNullOrEmpty(found))
            {
                if (ConstantRegistry.TryResolve(projectNode?.Name ?? fileNode.Path, found, out var resolved))
                {
                    found = resolved;
                }
                if (IsValidSchemaName(found))
                {
                    return found;
                }
            }
        }

        // 2. Scan source file text if available for explicit schema declarations
        if (syntaxTree != null && !string.IsNullOrEmpty(syntaxTree.FilePath) && File.Exists(syntaxTree.FilePath))
        {
            var fileSchema = ScanFileTextForSchema(syntaxTree.FilePath);
            if (!string.IsNullOrEmpty(fileSchema))
            {
                if (ConstantRegistry.TryResolve(projectNode?.Name ?? syntaxTree.FilePath, fileSchema, out var resolved))
                {
                    fileSchema = resolved;
                }
                if (IsValidSchemaName(fileSchema))
                {
                    return fileSchema;
                }
            }
        }

        return null;
    }

    public static bool IsValidSchemaName(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema)) return false;
        var clean = schema.Trim('\'', '"', '`', ' ', ';');
        if (clean.Length < 2 || clean.Length > 64) return false;
        if (clean.Contains('\n') || clean.Contains('\r') || clean.Contains(';') ||
            clean.Contains('(') || clean.Contains(')') || clean.Contains(' ') ||
            clean.Contains(',') || clean.Contains('=') || clean.Contains('[') || clean.Contains(']'))
        {
            return false;
        }
        if (IsSchemaStopWord(clean)) return false;
        if (clean.Equals("default", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("dbo", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("public", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("main", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("order", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("user", StringComparison.OrdinalIgnoreCase) ||
            clean.Equals("group", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (NestedSqlParser.IsSqlKeyword(clean)) return false;
        if (clean.EndsWith("Schema", StringComparison.OrdinalIgnoreCase) && 
            !clean.Equals("schema", StringComparison.OrdinalIgnoreCase) &&
            !clean.Equals("public", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return Regex.IsMatch(clean, @"^[a-zA-Z_][a-zA-Z0-9_-]*$");
    }

    private static bool IsSchemaStopWord(string? schema)
    {
        if (string.IsNullOrWhiteSpace(schema)) return true;
        var clean = schema.Trim('\'', '"', ' ', ';');
        return clean.Equals("DefaultSchemaName", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("DefaultSchema", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("SchemaName", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("Schema", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("String", StringComparison.OrdinalIgnoreCase) ||
               clean.Equals("str", StringComparison.OrdinalIgnoreCase);
    }
}
