using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;

namespace CodeExplorer.Core.Parser;

public partial class ResourceSemanticAnalyzer
{
    private const string OpCall = "CALL";

    private static readonly ConcurrentDictionary<IProjectParser, (IReadOnlyList<ISemanticExtension> Extensions, SemanticExtensionRegistry Registry)> _parserRegistryCache = new();
    private static readonly ConcurrentDictionary<string, string?> _projectFrameworkCache = new(StringComparer.OrdinalIgnoreCase);

    public static void ResetCache()
    {
        _parserRegistryCache.Clear();
        _projectFrameworkCache.Clear();
        _projectDriverCache.Clear();
    }

    private readonly IReadOnlyList<ISemanticExtension> _semanticExtensions;
    private readonly SemanticExtensionRegistry _trieRegistry;
    private readonly SyntaxTree _syntaxTree;

    public ResourceSemanticAnalyzer(
        IReadOnlyList<ISemanticExtension> semanticExtensions,
        SemanticExtensionRegistry registry,
        SyntaxTree syntaxTree)
    {
        _semanticExtensions = semanticExtensions;
        _trieRegistry = registry;
        _syntaxTree = syntaxTree;
    }

    public ResourceSemanticAnalyzer(
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

    public static Task EnrichAsync(
        SyntaxTree syntaxTree,
        ProjectNode projectNode,
        IProjectParser parser,
        ParsingContext ctx,
        IOntologyNode? workloadNode = null)
    {
        var (extensions, registry) = _parserRegistryCache.GetOrAdd(parser, p =>
        {
            var allExtensions = new List<ISemanticExtension>(p.SemanticExtensions);
            if (p.Packages != null)
            {
                var existingIds = p.SemanticExtensions.Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var pkg in p.Packages)
                {
                    if (!existingIds.Contains(pkg.Id))
                    {
                        allExtensions.Add(new PackageSemanticExtensionAdapter(pkg));
                    }
                }
            }
            var reg = new SemanticExtensionRegistry(allExtensions);
            return (allExtensions, reg);
        });

        var analyzer = new ResourceSemanticAnalyzer(extensions, registry, syntaxTree);
        return analyzer.EnrichAsync(projectNode, ctx, workloadNode);
    }

    private sealed record PackageSemanticExtensionAdapter(PackageDescriptor Package) : ISemanticExtension
    {
        public string Id => Package.Id;
        public string Name => Package.Name;
        public SemanticExtensionType Type => Package.Role switch
        {
            LibraryRole.CloudSdk => SemanticExtensionType.Cloud,
            LibraryRole.OrmOrDatabase => Package.Type.IsDatabase() ? Package.Type : SemanticExtensionType.RelationalDb,
            LibraryRole.WebFramework or LibraryRole.FrontendFramework => SemanticExtensionType.Framework,
            LibraryRole.MessageBroker => SemanticExtensionType.Queue,
            LibraryRole.General => SemanticExtensionType.ApiClient,
            _ => SemanticExtensionType.Library
        };
        public LibraryRole Role => Package.Role;
        public IReadOnlyList<string> SupportedPatterns => Package.SupportedPatterns;
        public bool IsImplemented => false;
    }

    private static string ResolveWorkloadId(ProjectNode projectNode, ParsingContext ctx, IOntologyNode? workloadNode)
    {
        if (workloadNode != null) return workloadNode.Id;

        if (ctx.SemanticStructure != null)
        {
            var match = ctx.SemanticStructure.Children.FirstOrDefault(c =>
                c is CompositeNode comp &&
                string.Equals(comp.ProjectId, projectNode.Id, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match.Id;
        }

        var prefix = projectNode.IsTest
            ? OntologyConstants.IdPrefixes.TestSuite
            : projectNode.IsLibrary
                ? OntologyConstants.IdPrefixes.Library
                : OntologyConstants.IdPrefixes.Service;
        return $"{ctx.WorkspaceId}:{prefix}:{projectNode.Name}";
    }

    private static Relationship EmitGroundingEdges(
        string resourceId,
        FileNode fileNode,
        int usageLine,
        string? operation,
        string? evidenceKind,
        string? via,
        string? provider,
        string? schema,
        bool isImplicitSchema,
        bool isOrm,
        ParsingContext ctx)
    {
        var declaredRel = Relationship.FromRelationship(new DeclaredInRelationship(resourceId, fileNode.Id)
        {
            FilePath = fileNode.Path,
            Operation = operation,
            EvidenceKind = evidenceKind,
            Via = via,
            Provider = provider,
            Schema = schema,
            IsImplicitSchema = isImplicitSchema,
            IsOrm = isOrm,
            Line = usageLine > 0 ? usageLine : 0,
            LineEnd = usageLine > 0 ? usageLine : 0
        });
        ctx.AddGlobalProjectDependency(declaredRel);

        Relationship primaryGroundingRel = declaredRel;
        var targetSymbols = new List<IOntologyNode>();
        if (usageLine > 0)
        {
            foreach (var child in fileNode.Children)
            {
                if (child is TypeNode tn && usageLine >= tn.StartLine && usageLine <= tn.EndLine)
                {
                    var matchingFn = tn.Children.OfType<FunctionNode>()
                        .FirstOrDefault(fn => usageLine >= fn.StartLine && usageLine <= fn.EndLine);
                    targetSymbols.Add(matchingFn != null ? matchingFn : tn);
                }
                else if (child is FunctionNode fn && usageLine >= fn.StartLine && usageLine <= fn.EndLine)
                {
                    targetSymbols.Add(fn);
                }
            }
        }

        if (targetSymbols.Count == 0)
        {
            foreach (var child in fileNode.Children)
            {
                if (child is TypeNode or FunctionNode)
                {
                    targetSymbols.Add(child);
                }
            }
        }

        foreach (var sym in targetSymbols)
        {
            var accessedRel = Relationship.FromRelationship(new AccessedByRelationship(resourceId, sym.Id)
            {
                FilePath = fileNode.Path,
                Operation = operation,
                EvidenceKind = evidenceKind,
                Via = via,
                Provider = provider,
                Schema = schema,
                IsImplicitSchema = isImplicitSchema,
                IsOrm = isOrm,
                Line = usageLine > 0 ? usageLine : 0,
                LineEnd = usageLine > 0 ? usageLine : 0
            });
            ctx.AddGlobalProjectDependency(accessedRel);
            primaryGroundingRel = accessedRel;
        }

        return primaryGroundingRel;
    }

    public virtual async Task EnrichAsync(ProjectNode projectNode, ParsingContext ctx, IOntologyNode? workloadNode = null)
    {
        ArgumentNullException.ThrowIfNull(projectNode);
        var workloadId = ResolveWorkloadId(projectNode, ctx, workloadNode);

        // Detect and enrich project-level framework using the Trie (evaluated once per project)
        ISemanticExtension? frameworkParser = null;
        if (!_projectFrameworkCache.TryGetValue(projectNode.Id, out var cachedFwName))
        {
            foreach (var child in projectNode.Children)
            {
                if (child is PackageNode pkg)
                {
                    var match = _trieRegistry.Match(pkg.Name);
                    if (match is { Type: SemanticExtensionType.Framework })
                    {
                        frameworkParser = match;
                        cachedFwName = match.Name;
                        break;
                    }
                }
            }
            _projectFrameworkCache[projectNode.Id] = cachedFwName;
        }

        if (frameworkParser != null || cachedFwName != null)
        {
            projectNode.Framework = frameworkParser?.Name ?? cachedFwName!;
        }

        var fileNode = _syntaxTree.FileNode;
        var relativePath = fileNode.Path;

        var matchedParsers = new List<ISemanticExtension>();
        foreach (var import in _syntaxTree.RawImports)
        {
            var match = _trieRegistry.Match(import.Path);
            if (match != null)
            {
                if (!matchedParsers.Contains(match))
                {
                    matchedParsers.Add(match);
                }

                if (frameworkParser == null && cachedFwName == null && match.Type == SemanticExtensionType.Framework)
                {
                    frameworkParser = match;
                    cachedFwName = match.Name;
                    _projectFrameworkCache[projectNode.Id] = match.Name;
                    projectNode.Framework = match.Name;
                }
            }
        }

        if (!matchedParsers.Any(p => p.Type.IsDatabase()))
        {
            if (_syntaxTree.RawImports.Any(i => i.Path.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
                                                i.Path.Contains("lib/pq", StringComparison.OrdinalIgnoreCase) ||
                                                i.Path.Contains("database/sql", StringComparison.OrdinalIgnoreCase) ||
                                                i.Path.Contains("sqlx", StringComparison.OrdinalIgnoreCase)) &&
                DetectDeclaredSchemas(projectNode, fileNode, _syntaxTree, "relational").Count > 0)
            {
                var ext = _semanticExtensions.FirstOrDefault(e => e.Id.Contains("sqlx", StringComparison.OrdinalIgnoreCase) ||
                                                                  (e.Type.IsDatabase() && e.Name.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase)))
                          ?? new PackageSemanticExtensionAdapter(new PackageDescriptor("postgres-client", "PostgreSQL", LibraryRole.OrmOrDatabase, SemanticExtensionType.RelationalDb, ["*postgres*"]));
                matchedParsers.Add(ext);
            }
        }

        if (frameworkParser != null)
        {
            projectNode.Framework = frameworkParser.Name;
        }

        foreach (var parser in matchedParsers)
        {
            if (parser.Type.IsDatabase())
            {
                var dbEngine = parser.Name;
                var dbType = parser.Type switch
                {
                    SemanticExtensionType.DocumentDb => "document",
                    SemanticExtensionType.KeyValueDb => "keyvalue",
                    SemanticExtensionType.AnalyticsDb => "analytics",
                    SemanticExtensionType.SearchDb => "search",
                    SemanticExtensionType.VectorDb => "vector",
                    SemanticExtensionType.GraphDb => "graph",
                    SemanticExtensionType.TimeSeriesDb => "timeseries",
                    _ => "relational"
                };

                    var isOrm = IsOrmLibrary(parser.Id);

                    // Record USES_API (L4 -> L4) and downward grounding (L4 -> L3/L1) for the ORM / DB driver library
                    var apiPrefix = isOrm ? "orm" : "api";
                    var ormApiId = $"{projectNode.Id}{apiPrefix}:{parser.Id}";
                    var semanticNode = ctx.SemanticStructure;
                    if (semanticNode != null && !semanticNode.Children.Any(c => c.Id == ormApiId))
                    {
                        var apiNode = new ApiInUseNode(ormApiId, parser.Name, fileNode.Path);
                        semanticNode.Children.Add(apiNode);
                    }
                    EmitGroundingEdges(ormApiId, fileNode, 0, null, null, null, null, null, false, false, ctx);
                    var ormUsesApiRel = new UsesApiRelationship(workloadId, ormApiId);
                    ctx.AddGlobalProjectDependency(Relationship.FromRelationship(ormUsesApiRel));

                    var (detectedEngine, detectedType) = isOrm
                        ? DetectProjectDatabaseDriver(projectNode, ctx)
                        : (null, null);

                    if (isOrm && string.IsNullOrEmpty(detectedEngine))
                    {
                        (detectedEngine, detectedType) = DetectFileDatabaseDriver(fileNode, _syntaxTree);
                    }

                    var targetEngine = !string.IsNullOrEmpty(detectedEngine)
                        ? detectedEngine
                        : (!string.IsNullOrEmpty(dbEngine) && !dbEngine.Equals("Database", StringComparison.OrdinalIgnoreCase)
                            ? ResourceNamingHelper.NormalizeEngineName(dbEngine)
                            : null);

                    if (!string.IsNullOrEmpty(detectedType)) dbType = detectedType;
                    else if (dbType == "unknown") dbType = "relational";

                    // Step A: Check DetectDeclaredSchemas(projectNode, fileNode, _syntaxTree, dbType)
                    var declaredSchemas = (dbType.Equals("relational", StringComparison.OrdinalIgnoreCase) || dbType.Equals("document", StringComparison.OrdinalIgnoreCase))
                        ? DetectDeclaredSchemas(projectNode, fileNode, _syntaxTree, dbType).ToList()
                        : new List<string>();

                    var declaredSchema = declaredSchemas.FirstOrDefault();
                    CanonicalResource? canonicalRes = null;

                    // Step B: If declaredSchemas.Count == 0, check if THIS exact project ALREADY has a concrete configured :Database resource
                    if (declaredSchemas.Count == 0 && ctx.ResourceRegistry != null)
                    {
                        var projRes = ctx.ResourceRegistry.AllResources
                            .FirstOrDefault(r => r.Kind == CodeExplorer.Cypher.Common.NodeKind.Database &&
                                                 (string.IsNullOrEmpty(targetEngine) || string.Equals(r.Engine, targetEngine, StringComparison.OrdinalIgnoreCase)) &&
                                                 r.BelongsToProject(projectNode.Id) &&
                                                 !r.Name.Equals(targetEngine, StringComparison.OrdinalIgnoreCase) &&
                                                 !ResourceNamingHelper.IsGenericConfigKey(r.Name));

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
                            else if (string.IsNullOrEmpty(cfgSchema) && !projRes.Name.Equals(targetEngine, StringComparison.OrdinalIgnoreCase) && !ResourceNamingHelper.IsGenericConfigKey(projRes.Name))
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

                    var isRelational = string.Equals(dbType, "relational", StringComparison.OrdinalIgnoreCase);

                    // Step B.2: Check for explicit DbContext declarations (and base("name=...") connection keys) in C# files
                    if (isRelational && declaredSchemas.Count == 0 && canonicalRes == null)
                    {
                        var dbContexts = DetectDeclaredDbContextInfos(fileNode);
                        foreach (var (ctxClass, ctxStem, connKey) in dbContexts)
                        {
                            CanonicalResource? matchedCtxRes = null;
                            if (ctx.ResourceRegistry != null)
                            {
                                if (!string.IsNullOrWhiteSpace(connKey))
                                {
                                    matchedCtxRes = ctx.ResourceRegistry.ResolveResource(connKey, expectedDbType: dbType, expectedEngine: targetEngine);
                                }
                                matchedCtxRes ??= ctx.ResourceRegistry.ResolveResource(ctxClass, expectedDbType: dbType, expectedEngine: targetEngine)
                                                  ?? ctx.ResourceRegistry.ResolveResource(ctxStem, expectedDbType: dbType, expectedEngine: targetEngine);
                            }

                            if (matchedCtxRes != null)
                            {
                                var cfgSchema = matchedCtxRes.Properties.TryGetValue("schema", out var s) ? s : matchedCtxRes.Name;
                                if (IsValidSchemaName(cfgSchema) && !string.IsNullOrEmpty(cfgSchema))
                                {
                                    declaredSchemas.Add(cfgSchema);
                                    declaredSchema ??= cfgSchema;
                                    canonicalRes ??= matchedCtxRes;
                                    if (string.IsNullOrEmpty(targetEngine) && !string.IsNullOrEmpty(matchedCtxRes.Engine))
                                    {
                                        targetEngine = matchedCtxRes.Engine;
                                    }
                                    break;
                                }
                            }
                            else if (!IsGenericDbContextStem(ctxStem) && IsValidSchemaName(ctxStem))
                            {
                                if (!declaredSchemas.Contains(ctxStem, StringComparer.OrdinalIgnoreCase))
                                {
                                    declaredSchemas.Add(ctxStem);
                                    declaredSchema ??= ctxStem;
                                }
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

                    if (isRelational && declaredSchemas.Count == 0 && canonicalRes == null && !projectNode.IsLibrary && HasEntityOrTableDeclaration(fileNode))
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

                    // Step C: Strict Evidence Gate
                    if (string.IsNullOrEmpty(targetEngine) || (isRelational && declaredSchemas.Count == 0 && canonicalRes == null))
                    {
                        break;
                    }

                    IEnumerable<string?> schemasToProcess = declaredSchemas.Count > 0 ? (IEnumerable<string?>)declaredSchemas : new string?[] { null };

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
                                              ResourceNamingHelper.IsAbstractEngineOnlyDatabase(canonicalDbName, currentSchema, dbType, null))))
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

                        var dbId = resForSchema?.Id ?? ResourceNamingHelper.BuildCanonicalDatabaseId(ctx.WorkspaceId, dbType, canonicalDbName);

                        var semanticNodeForDb = ctx.SemanticStructure;
                        if (semanticNodeForDb != null)
                        {
                            semanticNodeForDb.Children.RemoveAll(c => c is DatabaseNode dn && (string.Equals(dn.Name, "Database", StringComparison.OrdinalIgnoreCase) || dn.Id.EndsWith(":database:relational:database", StringComparison.OrdinalIgnoreCase) || dn.Id.EndsWith($":{OntologyConstants.IdPrefixes.Database}:relational:database", StringComparison.OrdinalIgnoreCase)));

                            if (!semanticNodeForDb.Children.Any(c => c.Id == dbId))
                            {
                                var isTestDb = projectNode.IsTest || WorkspaceConventions.IsTestFilePath(fileNode.Path);
                                var dbNode = new DatabaseNode(dbId, canonicalDbName, fileNode.Path, dbType)
                                {
                                    Engine = targetEngine,
                                    Provider = parser.Name,
                                    Schema = !string.IsNullOrEmpty(currentSchema) ? currentSchema : null,
                                    IsExplicitSchema = isExplicitSchema ? true : null,
                                    IsImplicitSchema = (!isExplicitSchema && !string.IsNullOrEmpty(currentSchema)) ? true : null,
                                    IsTest = isTestDb ? true : null,
                                    Role = isTestDb ? "Test" : null,
                                    IsOrm = isOrm ? true : null
                                };
                                semanticNodeForDb.Children.Add(dbNode);
                                ctx.AddGlobalSymbol(OntologyConstants.NodeLabels.Database, canonicalDbName, dbId);
                            }
                        }

                        var opStr = isOrm ? "ORM" : "SQL";
                        var evKind = isOrm
                            ? OntologyConstants.EvidenceKinds.Orm
                            : OntologyConstants.EvidenceKinds.Query;
                        var isImpSchema = !string.IsNullOrEmpty(currentSchema) && !isExplicitSchema;
                        var usageLine = FindDatabaseUsageLine(fileNode, currentSchema, parser.Id);

                        var groundingRel = EmitGroundingEdges(
                            dbId,
                            fileNode,
                            usageLine,
                            opStr,
                            evKind,
                            parser.Name,
                            parser.Id,
                            currentSchema,
                            isImpSchema,
                            isOrm,
                            ctx);
                        if (projectNode.IsTest || !WorkspaceConventions.IsTestFilePath(fileNode.Path))
                        {
                            var workloadUsesDbRel = Relationship.FromRelationship(new UsesDbRelationship(workloadId, dbId)
                            {
                                Via = parser.Name,
                                Provider = parser.Id,
                                FilePath = fileNode.Path,
                                Schema = currentSchema,
                                IsImplicitSchema = isImpSchema,
                                IsOrm = isOrm,
                                Operation = opStr,
                                EvidenceKind = evKind,
                                Line = usageLine > 0 ? usageLine : 0,
                                LineEnd = usageLine > 0 ? usageLine : 0
                            });
                            EvidenceProvenanceHelper.RecordContributingEdge(workloadUsesDbRel, groundingRel);
                            ctx.AddGlobalProjectDependency(workloadUsesDbRel);
                        }
                    }
            }
            else if (parser.Type is SemanticExtensionType.ApiClient or SemanticExtensionType.HttpClient or SemanticExtensionType.Queue)
            {
                var apiId = $"{projectNode.Id}api:{parser.Id}";
                var semanticNodeForApi = ctx.SemanticStructure;
                if (semanticNodeForApi != null && !semanticNodeForApi.Children.Any(c => c.Id == apiId))
                {
                    var apiNode = new ApiInUseNode(apiId, parser.Name, apiId);
                    semanticNodeForApi.Children.Add(apiNode);
                }

                var apiGroundingRel = EmitGroundingEdges(
                    apiId,
                    fileNode,
                    0,
                    OpCall,
                    OntologyConstants.EvidenceKinds.LibraryCall,
                    null,
                    null,
                    null,
                    false,
                    false,
                    ctx);
                var workloadUsesApiRel = Relationship.FromRelationship(new UsesApiRelationship(workloadId, apiId)
                {
                    FilePath = fileNode.Path,
                    Operation = OpCall,
                    EvidenceKind = OntologyConstants.EvidenceKinds.LibraryCall
                });
                EvidenceProvenanceHelper.RecordContributingEdge(workloadUsesApiRel, apiGroundingRel);
                ctx.AddGlobalProjectDependency(workloadUsesApiRel);
            }
            else if (parser.Type == SemanticExtensionType.Cloud)
            {
                var cloudService = parser.Name;
                var cloudId = $"{projectNode.Id}cloud:{parser.Id}";
                var semanticNodeForCloud = ctx.SemanticStructure;
                if (semanticNodeForCloud != null && !semanticNodeForCloud.Children.Any(c => c.Id == cloudId))
                {
                    var cloudNode = new CloudServiceNode(cloudId, cloudService, "CloudService", cloudId);
                    semanticNodeForCloud.Children.Add(cloudNode);
                }

                var cloudGroundingRel = EmitGroundingEdges(
                    cloudId,
                    fileNode,
                    0,
                    OpCall,
                    OntologyConstants.EvidenceKinds.HttpCall,
                    null,
                    null,
                    null,
                    false,
                    false,
                    ctx);
                var workloadUsesCloudRel = Relationship.FromRelationship(new UsesCloudRelationship(workloadId, cloudId)
                {
                    FilePath = fileNode.Path,
                    Operation = OpCall,
                    EvidenceKind = OntologyConstants.EvidenceKinds.HttpCall
                });
                EvidenceProvenanceHelper.RecordContributingEdge(workloadUsesCloudRel, cloudGroundingRel);
                ctx.AddGlobalProjectDependency(workloadUsesCloudRel);
            }
        }

        await Task.CompletedTask;
    }

    private static int FindDatabaseUsageLine(FileNode fileNode, string? schema, string parserId)
    {
        if (!string.IsNullOrWhiteSpace(schema) &&
            fileNode.TryGetSignals()?.TryGetSchemaLine(schema, out var parsedLine) == true &&
            parsedLine > 0)
        {
            return parsedLine;
        }

        foreach (var r in fileNode.References)
        {
            if (r.Line > 0 &&
                (r.RelationshipKind == RelationshipKind.UsesDb ||
                 (!string.IsNullOrWhiteSpace(parserId) && r.TargetName.Contains(parserId, StringComparison.OrdinalIgnoreCase))))
            {
                return r.Line;
            }
        }

        foreach (var child in fileNode.Children)
        {
            if (child is TypeNode tn && tn.StartLine > 0) return tn.StartLine;
            if (child is FunctionNode fn && fn.StartLine > 0) return fn.StartLine;
        }

        return 0;
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

        var filePath = fileNode?.FullPath ?? fileNode?.Path ?? syntaxTree?.FilePath ?? string.Empty;
        var fileSchema = ResolveDeclaredSchemaFromFileExtensions(fileNode ?? syntaxTree?.FileNode, filePath);
        if (!string.IsNullOrEmpty(fileSchema))
        {
            if (ConstantRegistry.TryResolve(projectNode?.Name ?? filePath, fileSchema, out var resolved))
            {
                fileSchema = resolved;
            }
            if (IsValidSchemaName(fileSchema))
            {
                return fileSchema;
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
