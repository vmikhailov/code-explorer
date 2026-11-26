using System.Collections.Frozen;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CodeExplorer.Core.Parser;

public static class WorkspaceDetectorLanguageMap
{
    private const string WildcardLang = "*";
    private const string LangCSharp = "c-sharp";
    private const string LangTypeScript = "typescript";
    private const string LangJavaScript = "javascript";
    private const string LangPython = "python";
    private const string LangJava = "java";
    private const string LangKotlin = "kotlin";
    private const string LangGo = "go";
    private const string LangRust = "rust";
    private const string LangPhp = "php";
    private const string LangPerl = "perl";
    private const string LangColdFusion = "coldfusion";

    private static readonly FrozenDictionary<string, string[]> AliasToCanonical =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [WildcardLang] = [WildcardLang],
            ["all"] = [WildcardLang],
            ["csharp"] = [LangCSharp],
            ["c-sharp"] = [LangCSharp],
            ["c#"] = [LangCSharp],
            ["cs"] = [LangCSharp],
            ["dotnet"] = [LangCSharp],
            ["typescript"] = [LangTypeScript],
            ["ts"] = [LangTypeScript],
            ["tsx"] = [LangTypeScript],
            ["javascript"] = [LangJavaScript],
            ["js"] = [LangJavaScript],
            ["jsx"] = [LangJavaScript],
            ["js/ts"] = [LangTypeScript, LangJavaScript],
            ["ts/js"] = [LangTypeScript, LangJavaScript],
            ["python"] = [LangPython],
            ["py"] = [LangPython],
            ["java"] = [LangJava],
            ["kotlin"] = [LangKotlin],
            ["kt"] = [LangKotlin],
            ["go"] = [LangGo],
            ["golang"] = [LangGo],
            ["rust"] = [LangRust],
            ["rs"] = [LangRust],
            ["php"] = [LangPhp],
            ["perl"] = [LangPerl],
            ["pl"] = [LangPerl],
            ["coldfusion"] = [LangColdFusion],
            ["cfml"] = [LangColdFusion]
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static bool TryExpandLanguages(string rawLanguage, out string[] canonicalLanguages)
    {
        if (string.IsNullOrWhiteSpace(rawLanguage))
        {
            canonicalLanguages = [];
            return false;
        }
        return AliasToCanonical.TryGetValue(rawLanguage.Trim(), out canonicalLanguages!);
    }

    public static string NormalizeProjectLanguage(string projectType)
    {
        if (TryExpandLanguages(projectType, out var langs) && langs.Length > 0)
        {
            return langs[0];
        }
        return projectType.Trim().ToLowerInvariant();
    }

    public static IReadOnlyList<string> SupportedCanonicalLanguages { get; } =
    [
        LangCSharp, LangJava, LangKotlin, LangGo, LangPython,
        LangTypeScript, LangJavaScript, LangRust, LangPhp, LangPerl, LangColdFusion, WildcardLang
    ];
}

public static class WorkspaceDetectorLoader
{
    private static class Constants
    {
        public const int SupportedSchemaVersion = 1;
        public const int MaxYamlFileBytes = 256 * 1024;
        public const int MaxSupportedPatterns = 128;
        public const int MaxPatternLength = 256;
        public const int MaxWildcardsPerPattern = 6;
        public const string DotCodeExplorer = ".codeexplorer";
        public const string DetectorsFolder = "detectors";
        public const string YamlExt = ".yaml";
        public const string YmlExt = ".yml";
        public const string EmptyHash = "none";
        public const string FieldType = "type";
        public const string FieldLibraryRole = "library_role";
        public const string FieldDataStoreRelation = "data_store_access.default_target_relation";
        public const string FieldServiceCallRelation = "service_call_sinks.target_relation";
        public const string FieldAnnotationSinkRelation = "annotation_sinks.target_relation";
        public const string FieldHierarchySinkRelation = "class_hierarchy_sinks.target_relation";
        public const string ErrEmptyYaml = "YAML document is empty.";
        public const string ErrInvalidId = "Field 'id' is required and must match '[a-z0-9][a-z0-9._-]{0,127}'.";
        public const string ErrMissingName = "Field 'name' is required.";
        public const string ErrMissingLanguages = "Field 'languages' is required and must list at least one supported language (or '*').";
        public const string ErrMissingPatterns = "Field 'supported_patterns' is required and must contain at least one import/package pattern.";
        public const string ErrEmptyPatternItem = "Field 'supported_patterns' cannot contain empty patterns.";
        public const string ErrUnboundedWildcard = "Unbounded wildcard '*' is not allowed in 'supported_patterns'; specify a package/namespace pattern.";
        public const string ErrMissingAnnotMap = "annotation_endpoints rule requires non-empty 'annotation_to_http_method'.";
        public const string ErrMissingMethodMap = "inbound_route_calls rule requires non-empty 'method_to_http_verb'.";
        public const string ErrMissingDataStoreMethods = "data_store_access rule requires non-empty 'call_method_names'.";
        public const string ErrMissingServiceCallMethods = "service_call_sinks rule requires non-empty 'call_method_names'.";
        public const string ErrMissingAnnotNames = "annotation_sinks rule requires non-empty 'annotation_names'.";
        public const string ErrMissingBaseTypes = "class_hierarchy_sinks rule requires non-empty 'base_type_substrings'.";
    }

    private static readonly Regex ValidDetectorIdRegex = new(
        @"^[a-z0-9][a-z0-9._-]{0,127}$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(50));

    private static readonly FrozenSet<string> ValidRelations = LoadValidRelations();

    private static readonly FrozenSet<string> UniversalAnnotationNodeTypes = new[]
    {
        "annotation", "marker_annotation", "attribute",
        "attribute_group", "attribute_item", "decorator"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> UniversalClassDeclarationNodeTypes = new[]
    {
        "class_declaration", "class_definition", "record_declaration",
        "struct_declaration", "struct_item", "interface_declaration", "type_spec"
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

    public static WorkspaceDetectorCatalog LoadForWorkspace(
        string? workspaceRoot,
        bool strict = true,
        string? globalDetectorsDirOverride = null)
    {
        var (catalog, errors) = LoadInternal(workspaceRoot, globalDetectorsDirOverride);
        if (strict && errors.Count > 0)
        {
            throw new DetectorValidationException(errors);
        }
        return catalog;
    }

    public static (WorkspaceDetectorCatalog Catalog, IReadOnlyList<DetectorValidationIssue> Issues) ValidateWorkspace(
        string? workspaceRoot,
        string? globalDetectorsDirOverride = null)
    {
        var (catalog, errors) = LoadInternal(workspaceRoot, globalDetectorsDirOverride);
        var allIssues = new List<DetectorValidationIssue>(errors.Count + catalog.Warnings.Count);
        allIssues.AddRange(errors);
        allIssues.AddRange(catalog.Warnings);
        return (catalog, allIssues);
    }

    public static IReadOnlyList<DetectorDescriptorSummary> ListAllDetectors(
        string? workspaceRoot,
        bool includeBuiltIn = true,
        string? globalDetectorsDirOverride = null)
    {
        ParserRegistry.AutoRegisterParsers();
        var (catalog, _) = LoadInternal(workspaceRoot, globalDetectorsDirOverride);
        var summaries = new List<DetectorDescriptorSummary>();

        var customById = catalog.CustomDetectors.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);

        if (includeBuiltIn)
        {
            var builtInMap = CollectBuiltInExtensionsById();
            foreach (var (id, entry) in builtInMap.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                var ext = entry.Extension;
                summaries.Add(new DetectorDescriptorSummary(
                    ext.Id,
                    ext.Name,
                    DetectorOrigin.BuiltIn,
                    entry.Languages,
                    ext.Type,
                    ext.LibraryRole,
                    ext.SupportedPatterns,
                    OverridesBuiltIn: customById.ContainsKey(id),
                    ShadowsImperativeRules: false,
                    SourcePath: null));
            }
        }

        foreach (var custom in catalog.CustomDetectors.OrderBy(c => c.Id, StringComparer.OrdinalIgnoreCase))
        {
            summaries.Add(new DetectorDescriptorSummary(
                custom.Id,
                custom.Name,
                custom.Origin,
                custom.TargetLanguages,
                custom.Type,
                custom.LibraryRole,
                custom.SupportedPatterns,
                custom.OverridesBuiltIn,
                custom.ShadowsImperativeRules,
                custom.SourceFilePath));
        }

        return summaries;
    }

    private static (WorkspaceDetectorCatalog Catalog, List<DetectorValidationIssue> Errors) LoadInternal(
        string? workspaceRoot,
        string? globalDetectorsDirOverride)
    {
        ParserRegistry.AutoRegisterParsers();
        var builtInMap = CollectBuiltInExtensionsById();
        var errors = new List<DetectorValidationIssue>();
        var warnings = new List<DetectorValidationIssue>();

        var globalDir = globalDetectorsDirOverride ?? GetDefaultGlobalDetectorsDir();
        var workspaceDir = !string.IsNullOrWhiteSpace(workspaceRoot)
            ? Path.Combine(workspaceRoot, Constants.DotCodeExplorer, Constants.DetectorsFolder)
            : null;

        // Avoid double-loading if workspaceRoot happens to be the user home directory
        if (!string.IsNullOrWhiteSpace(workspaceDir) && !string.IsNullOrWhiteSpace(globalDir) &&
            string.Equals(Path.GetFullPath(workspaceDir), Path.GetFullPath(globalDir), StringComparison.OrdinalIgnoreCase))
        {
            globalDir = null;
        }

        var globalFiles = EnumerateDetectorYamlFiles(globalDir);
        var workspaceFiles = EnumerateDetectorYamlFiles(workspaceDir);

        if (globalFiles.Count == 0 && workspaceFiles.Count == 0)
        {
            return (WorkspaceDetectorCatalog.Empty, errors);
        }

        using var sha = SHA256.Create();
        var hashBuilder = new StringBuilder();

        var globalById = new Dictionary<string, YamlDeclarativeSemanticExtension>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in globalFiles)
        {
            var ext = ParseAndValidateFile(file, DetectorOrigin.Global, builtInMap, errors, warnings, hashBuilder);
            if (ext != null)
            {
                if (!globalById.TryAdd(ext.Id, ext))
                {
                    errors.Add(new DetectorValidationIssue(file, $"Duplicate global detector id '{ext.Id}'."));
                }
            }
        }

        var workspaceById = new Dictionary<string, YamlDeclarativeSemanticExtension>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in workspaceFiles)
        {
            var ext = ParseAndValidateFile(file, DetectorOrigin.Workspace, builtInMap, errors, warnings, hashBuilder);
            if (ext != null)
            {
                if (!workspaceById.TryAdd(ext.Id, ext))
                {
                    errors.Add(new DetectorValidationIssue(file, $"Duplicate workspace detector id '{ext.Id}'."));
                }
            }
        }

        // Workspace detectors override global detectors with the same ID
        var merged = new Dictionary<string, YamlDeclarativeSemanticExtension>(globalById, StringComparer.OrdinalIgnoreCase);
        foreach (var (id, wsExt) in workspaceById)
        {
            merged[id] = wsExt;
        }

        var combinedBytes = Encoding.UTF8.GetBytes(hashBuilder.ToString());
        var combinedHash = Convert.ToHexStringLower(sha.ComputeHash(combinedBytes))[..16];

        var list = merged.Values.OrderBy(d => d.Id, StringComparer.OrdinalIgnoreCase).ToList();
        return (new WorkspaceDetectorCatalog(list, warnings, combinedHash), errors);
    }

    private static string? GetDefaultGlobalDetectorsDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(home)
            ? null
            : Path.Combine(home, Constants.DotCodeExplorer, Constants.DetectorsFolder);
    }

    private static List<string> EnumerateDetectorYamlFiles(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(dir)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f);
                    return ext.Equals(Constants.YamlExt, StringComparison.OrdinalIgnoreCase) ||
                           ext.Equals(Constants.YmlExt, StringComparison.OrdinalIgnoreCase);
                })
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static YamlDeclarativeSemanticExtension? ParseAndValidateFile(
        string filePath,
        DetectorOrigin origin,
        Dictionary<string, (ISemanticExtension Extension, List<string> Languages)> builtInMap,
        List<DetectorValidationIssue> errors,
        List<DetectorValidationIssue> warnings,
        StringBuilder hashBuilder)
    {
        var info = new FileInfo(filePath);
        if (info.Length > Constants.MaxYamlFileBytes)
        {
            errors.Add(new DetectorValidationIssue(filePath, $"YAML file exceeds maximum size of {Constants.MaxYamlFileBytes} bytes."));
            return null;
        }

        string rawText;
        try
        {
            rawText = File.ReadAllText(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add(new DetectorValidationIssue(filePath, $"Cannot read detector file: {ex.Message}"));
            return null;
        }

        var fileHashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawText));
        var hash8 = Convert.ToHexStringLower(fileHashBytes)[..8];
        hashBuilder.Append(origin).Append(':').Append(Path.GetFileName(filePath)).Append(':').Append(hash8).Append('\n');

        DeclarativeDetectorYaml? dto;
        try
        {
            dto = YamlDeserializer.Deserialize<DeclarativeDetectorYaml>(rawText);
        }
        catch (YamlException yex)
        {
            errors.Add(new DetectorValidationIssue(
                filePath,
                $"YAML syntax/schema error at line {yex.Start.Line}, col {yex.Start.Column}: {yex.InnerException?.Message ?? yex.Message}"));
            return null;
        }

        if (dto == null)
        {
            errors.Add(new DetectorValidationIssue(filePath, Constants.ErrEmptyYaml));
            return null;
        }

        var initialErrCount = errors.Count;

        if (dto.SchemaVersion != Constants.SupportedSchemaVersion)
        {
            errors.Add(new DetectorValidationIssue(filePath, $"Unsupported 'schema_version: {dto.SchemaVersion}'. Expected 'schema_version: {Constants.SupportedSchemaVersion}'."));
        }

        var id = dto.Id?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(id) || !ValidDetectorIdRegex.IsMatch(id))
        {
            errors.Add(new DetectorValidationIssue(filePath, Constants.ErrInvalidId));
        }

        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            errors.Add(new DetectorValidationIssue(filePath, Constants.ErrMissingName));
        }

        var canonicalLangs = new List<string>();
        if (dto.Languages is not { Count: > 0 })
        {
            errors.Add(new DetectorValidationIssue(filePath, Constants.ErrMissingLanguages));
        }
        else
        {
            foreach (var rawLang in dto.Languages)
            {
                if (!WorkspaceDetectorLanguageMap.TryExpandLanguages(rawLang, out var expanded))
                {
                    errors.Add(new DetectorValidationIssue(
                        filePath,
                        $"Unknown language '{rawLang}'. Supported: {string.Join(", ", WorkspaceDetectorLanguageMap.SupportedCanonicalLanguages)}."));
                }
                else
                {
                    foreach (var c in expanded)
                    {
                        if (!canonicalLangs.Contains(c, StringComparer.OrdinalIgnoreCase))
                        {
                            canonicalLangs.Add(c);
                        }
                    }
                }
            }
        }

        var patterns = ValidateSupportedPatterns(filePath, dto.SupportedPatterns, errors);
        var extType = ParseEnumField(filePath, Constants.FieldType, dto.ExtensionDomain, SemanticExtensionType.Framework, errors);
        var libRole = ParseEnumField(filePath, Constants.FieldLibraryRole, dto.LibraryRole, LibraryRole.General, errors);

        var overridesBuiltIn = false;
        var shadowsImperative = false;
        if (!string.IsNullOrEmpty(id) && builtInMap.TryGetValue(id, out var builtInEntry))
        {
            if (!dto.Override)
            {
                errors.Add(new DetectorValidationIssue(
                    filePath,
                    $"Detector id '{id}' collides with built-in detector '{id}'. Use a new id to add rules alongside it, or set 'override: true' to replace it."));
            }
            else
            {
                overridesBuiltIn = true;
                if (builtInEntry.Extension is DeclarativeSemanticExtension dec && dec.CustomAstRules.Count > 0)
                {
                    shadowsImperative = true;
                    warnings.Add(new DetectorValidationIssue(
                        filePath,
                        $"Overriding built-in detector '{id}' replaces {dec.CustomAstRules.Count} imperative C# AST rule(s). Consider using a separate id with matching supported_patterns to extend instead of replacing.",
                        IsWarning: true));
                }
            }
        }

        var annotEndpoints = BuildAnnotationEndpoints(filePath, dto.AnnotationEndpoints, errors);
        var inboundRoutes = BuildInboundRouteCalls(filePath, dto.InboundRouteCalls, errors);
        var outboundHttp = BuildOutboundHttp(dto.OutboundHttp);
        var sqlSinks = BuildSqlSinks(dto.SqlSinks);
        var ormTables = BuildOrmTables(dto.OrmTables);
        var messaging = BuildMessagingBrokers(dto.MessagingBrokers);
        var dataStores = BuildDataStoreAccess(filePath, dto.DataStoreAccess, errors);
        var serviceCalls = BuildServiceCallSinks(filePath, dto.ServiceCallSinks, errors);
        var annotSinks = BuildAnnotationSinks(filePath, dto.AnnotationSinks, errors);
        var hierarchySinks = BuildClassHierarchySinks(filePath, dto.ClassHierarchySinks, errors);

        if (errors.Count > initialErrCount)
        {
            return null;
        }

        return new YamlDeclarativeSemanticExtension
        {
            Id = id,
            Name = name,
            Type = extType,
            LibraryRole = libRole,
            IsBuiltIn = false,
            IsImplemented = true,
            SupportedPatterns = patterns,
            Origin = origin,
            SourceFilePath = filePath,
            ContentHash8 = hash8,
            TargetLanguages = canonicalLangs,
            OverridesBuiltIn = overridesBuiltIn,
            ShadowsImperativeRules = shadowsImperative,
            AnnotationEndpointRules = annotEndpoints,
            InboundRouteCallRules = inboundRoutes,
            OutboundHttpRules = outboundHttp,
            SqlSinkRules = sqlSinks,
            OrmTableRules = ormTables,
            MessagingBrokerRules = messaging,
            DataStoreAccessRules = dataStores,
            ServiceCallSinkRules = serviceCalls,
            AnnotationSinkRules = annotSinks,
            ClassHierarchySinkRules = hierarchySinks
        };
    }

    private static List<string> ValidateSupportedPatterns(
        string filePath,
        List<string>? rawPatterns,
        List<DetectorValidationIssue> errors)
    {
        var cleaned = new List<string>();
        if (rawPatterns is not { Count: > 0 })
        {
            errors.Add(new DetectorValidationIssue(filePath, Constants.ErrMissingPatterns));
            return cleaned;
        }

        if (rawPatterns.Count > Constants.MaxSupportedPatterns)
        {
            errors.Add(new DetectorValidationIssue(filePath, $"Field 'supported_patterns' exceeds maximum of {Constants.MaxSupportedPatterns} entries."));
            return cleaned;
        }

        foreach (var raw in rawPatterns)
        {
            var p = raw?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(p))
            {
                errors.Add(new DetectorValidationIssue(filePath, Constants.ErrEmptyPatternItem));
                continue;
            }
            if (p == "*")
            {
                errors.Add(new DetectorValidationIssue(filePath, Constants.ErrUnboundedWildcard));
                continue;
            }
            if (p.Length > Constants.MaxPatternLength)
            {
                errors.Add(new DetectorValidationIssue(filePath, $"Pattern '{p[..32]}...' exceeds maximum length of {Constants.MaxPatternLength}."));
                continue;
            }
            var starCount = p.Count(ch => ch == '*');
            if (starCount > Constants.MaxWildcardsPerPattern)
            {
                errors.Add(new DetectorValidationIssue(filePath, $"Pattern '{p}' contains {starCount} wildcards (max {Constants.MaxWildcardsPerPattern})."));
                continue;
            }
            cleaned.Add(p);
        }

        return cleaned;
    }

    private static TEnum ParseEnumField<TEnum>(
        string filePath,
        string fieldName,
        string? rawValue,
        TEnum defaultValue,
        List<DetectorValidationIssue> errors) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return defaultValue;
        }

        var compact = rawValue.Trim().Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        if (Enum.TryParse<TEnum>(compact, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        var validNames = string.Join(", ", Enum.GetNames<TEnum>().Select(ToSnakeCase));
        errors.Add(new DetectorValidationIssue(filePath, $"Invalid '{fieldName}: {rawValue}'. Valid values: {validNames}."));
        return defaultValue;
    }

    private static string ValidateRelation(
        string filePath,
        string fieldName,
        string? rawRelation,
        string defaultRelation,
        List<DetectorValidationIssue> errors)
    {
        if (string.IsNullOrWhiteSpace(rawRelation))
        {
            return defaultRelation;
        }

        var normalized = rawRelation.Trim().ToUpperInvariant();
        if (!ValidRelations.Contains(normalized))
        {
            errors.Add(new DetectorValidationIssue(
                filePath,
                $"Unknown ontology relationship '{rawRelation}' in '{fieldName}'. Example valid values: CALLS, DEPENDS_ON, EXPOSES, USES_DB, READS_FROM, WRITES_TO, PUBLISHES_TO, SUBSCRIBES_TO, REFERENCES."));
            return defaultRelation;
        }

        return normalized;
    }

    private static List<AnnotationEndpointRule> BuildAnnotationEndpoints(
        string filePath,
        List<AnnotationEndpointRuleYaml>? items,
        List<DetectorValidationIssue> errors)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<AnnotationEndpointRule>(items.Count);
        foreach (var y in items)
        {
            if (y.AnnotationToHttpMethod is not { Count: > 0 })
            {
                errors.Add(new DetectorValidationIssue(filePath, Constants.ErrMissingAnnotMap));
                continue;
            }

            var rule = new AnnotationEndpointRule
            {
                AnnotationToHttpMethod = y.AnnotationToHttpMethod.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase),
                AnnotationNodeTypes = ToFrozenOrDefault(y.AnnotationNodeTypes, UniversalAnnotationNodeTypes, StringComparer.Ordinal),
                EnrichJavaSecurityAndSchemas = y.EnrichJavaSecurityAndSchemas ?? true
            };
            if (y.PathArgumentKeys is { Count: > 0 })
                rule = rule with { PathArgumentKeys = y.PathArgumentKeys.ToFrozenSet(StringComparer.Ordinal) };
            if (y.RoutePrefixAnnotationNames is { Count: > 0 })
                rule = rule with { RoutePrefixAnnotationNames = y.RoutePrefixAnnotationNames.ToFrozenSet(StringComparer.OrdinalIgnoreCase) };
            if (y.WebSocketAnnotationNames is { Count: > 0 })
                rule = rule with { WebSocketAnnotationNames = y.WebSocketAnnotationNames.ToFrozenSet(StringComparer.OrdinalIgnoreCase) };
            list.Add(rule);
        }
        return list;
    }

    private static List<InboundRouteCallRule> BuildInboundRouteCalls(
        string filePath,
        List<InboundRouteCallRuleYaml>? items,
        List<DetectorValidationIssue> errors)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<InboundRouteCallRule>(items.Count);
        foreach (var y in items)
        {
            if (y.MethodToHttpVerb is not { Count: > 0 })
            {
                errors.Add(new DetectorValidationIssue(filePath, Constants.ErrMissingMethodMap));
                continue;
            }

            var rule = new InboundRouteCallRule
            {
                MethodToHttpVerb = y.MethodToHttpVerb.ToFrozenDictionary(StringComparer.Ordinal),
                ReceiverSubstrings = ToFrozenOrEmpty(y.ReceiverSubstrings, StringComparer.Ordinal),
                ExcludedReceivers = ToFrozenOrEmpty(y.ExcludedReceivers, StringComparer.Ordinal),
                GroupMethodNames = ToFrozenOrEmpty(y.GroupMethodNames, StringComparer.Ordinal),
                ResolveChainedRouteGroups = y.ResolveChainedRouteGroups ?? true,
                PathArgumentIndex = y.PathArgumentIndex ?? 0,
                StaticRoute = y.StaticRoute,
                RoutePrefix = y.RoutePrefix ?? string.Empty,
                EmitEntryPointNode = y.EmitEntryPointNode ?? false,
                RequireLeadingSlashOnPath = y.RequireLeadingSlashOnPath ?? false,
                EmitHandlerTriggers = y.EmitHandlerTriggers ?? false
            };
            if (y.CallNodeTypes is { Count: > 0 })
                rule = rule with { CallNodeTypes = y.CallNodeTypes.ToFrozenSet(StringComparer.Ordinal) };
            list.Add(rule);
        }
        return list;
    }

    private static List<OutboundHttpRule> BuildOutboundHttp(List<OutboundHttpRuleYaml>? items)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<OutboundHttpRule>(items.Count);
        foreach (var y in items)
        {
            var rule = new OutboundHttpRule
            {
                CallMethodNames = ToFrozenOrEmpty(y.CallMethodNames, StringComparer.Ordinal),
                ClientAnnotationSuffixes = ToFrozenOrEmpty(y.ClientAnnotationSuffixes, StringComparer.OrdinalIgnoreCase),
                AnnotationNodeTypes = ToFrozenOrDefault(y.AnnotationNodeTypes, UniversalAnnotationNodeTypes, StringComparer.Ordinal),
                FallbackTarget = y.FallbackTarget ?? "http:unknown-service",
                ResolveFluentChains = y.ResolveFluentChains ?? true
            };
            if (y.CallNodeTypes is { Count: > 0 })
                rule = rule with { CallNodeTypes = y.CallNodeTypes.ToFrozenSet(StringComparer.Ordinal) };
            list.Add(rule);
        }
        return list;
    }

    private static List<SqlSinkRule> BuildSqlSinks(List<SqlSinkRuleYaml>? items)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<SqlSinkRule>(items.Count);
        foreach (var y in items)
        {
            var rule = new SqlSinkRule
            {
                CallMethodNames = ToFrozenOrEmpty(y.CallMethodNames, StringComparer.Ordinal),
                QueryAnnotationSuffixes = ToFrozenOrEmpty(y.QueryAnnotationSuffixes, StringComparer.OrdinalIgnoreCase),
                RequireExtractableSqlOnCall = y.RequireExtractableSqlOnCall ?? true,
                AppendMethodNameWhenNoSql = y.AppendMethodNameWhenNoSql ?? false,
                FallbackQueryPrefix = y.FallbackQueryPrefix ?? "SQL Query",
                AnnotationNodeTypes = ToFrozenOrDefault(y.AnnotationNodeTypes, UniversalAnnotationNodeTypes, StringComparer.Ordinal)
            };
            if (y.CallNodeTypes is { Count: > 0 })
                rule = rule with { CallNodeTypes = y.CallNodeTypes.ToFrozenSet(StringComparer.Ordinal) };
            list.Add(rule);
        }
        return list;
    }

    private static List<OrmTableRule> BuildOrmTables(List<OrmTableRuleYaml>? items)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<OrmTableRule>(items.Count);
        foreach (var y in items)
        {
            var rule = new OrmTableRule
            {
                AnnotationNodeTypes = ToFrozenOrDefault(y.AnnotationNodeTypes, UniversalAnnotationNodeTypes, StringComparer.Ordinal),
                ClassDeclarationNodeTypes = ToFrozenOrDefault(y.ClassDeclarationNodeTypes, UniversalClassDeclarationNodeTypes, StringComparer.Ordinal),
                BaseClassNames = ToFrozenOrEmpty(y.BaseClassNames, StringComparer.Ordinal),
                RepositoryInterfaceNames = ToFrozenOrEmpty(y.RepositoryInterfaceNames, StringComparer.Ordinal)
            };
            if (y.TableAnnotationNames is { Count: > 0 })
                rule = rule with { TableAnnotationNames = y.TableAnnotationNames.ToFrozenSet(StringComparer.OrdinalIgnoreCase) };
            if (y.NameArgumentKeys is { Count: > 0 })
                rule = rule with { NameArgumentKeys = y.NameArgumentKeys.ToFrozenSet(StringComparer.Ordinal) };
            list.Add(rule);
        }
        return list;
    }

    private static List<MessagingBrokerRule> BuildMessagingBrokers(List<MessagingBrokerRuleYaml>? items)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<MessagingBrokerRule>(items.Count);
        foreach (var y in items)
        {
            var rule = new MessagingBrokerRule
            {
                Protocol = y.Protocol ?? string.Empty,
                ListenerAnnotationNames = ToFrozenOrEmpty(y.ListenerAnnotationNames, StringComparer.OrdinalIgnoreCase),
                ProducerAnnotationNames = ToFrozenOrEmpty(y.ProducerAnnotationNames, StringComparer.OrdinalIgnoreCase),
                ChannelAnnotationNames = ToFrozenOrEmpty(y.ChannelAnnotationNames, StringComparer.OrdinalIgnoreCase),
                MethodLevelEventAnnotations = ToFrozenOrEmpty(y.MethodLevelEventAnnotations, StringComparer.OrdinalIgnoreCase),
                FallbackTopicName = y.FallbackTopicName ?? "unknown-topic",
                PublishMethodNames = ToFrozenOrEmpty(y.PublishMethodNames, StringComparer.Ordinal),
                PublishReceiverSubstrings = ToFrozenOrEmpty(y.PublishReceiverSubstrings, StringComparer.OrdinalIgnoreCase),
                ExtractEventTypeFromObjectCreationArg = y.ExtractEventTypeFromObjectCreationArg ?? false,
                PreferSecondArgWhenThreeOrMoreArgs = y.PreferSecondArgWhenThreeOrMoreArgs ?? false,
                PublishTopicArgumentIndex = y.PublishTopicArgumentIndex ?? 0,
                StaticPublishTopic = y.StaticPublishTopic,
                SubscribeMethodNames = ToFrozenOrEmpty(y.SubscribeMethodNames, StringComparer.Ordinal),
                SubscribeReceiverSubstrings = ToFrozenOrEmpty(y.SubscribeReceiverSubstrings, StringComparer.OrdinalIgnoreCase),
                ConsumerInterfaceSubstrings = ToFrozenOrEmpty(y.ConsumerInterfaceSubstrings, StringComparer.OrdinalIgnoreCase),
                ResolveFluentChains = y.ResolveFluentChains ?? true,
                AnnotationNodeTypes = ToFrozenOrDefault(y.AnnotationNodeTypes, UniversalAnnotationNodeTypes, StringComparer.Ordinal),
                ClassDeclarationNodeTypes = ToFrozenOrDefault(y.ClassDeclarationNodeTypes, UniversalClassDeclarationNodeTypes, StringComparer.Ordinal)
            };
            if (y.TopicArgumentKeys is { Count: > 0 })
                rule = rule with { TopicArgumentKeys = y.TopicArgumentKeys.ToFrozenSet(StringComparer.OrdinalIgnoreCase) };
            if (y.CallNodeTypes is { Count: > 0 })
                rule = rule with { CallNodeTypes = y.CallNodeTypes.ToFrozenSet(StringComparer.Ordinal) };
            list.Add(rule);
        }
        return list;
    }

    private static List<DataStoreAccessRule> BuildDataStoreAccess(
        string filePath,
        List<DataStoreAccessRuleYaml>? items,
        List<DetectorValidationIssue> errors)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<DataStoreAccessRule>(items.Count);
        foreach (var y in items)
        {
            if (y.CallMethodNames is not { Count: > 0 })
            {
                errors.Add(new DetectorValidationIssue(filePath, Constants.ErrMissingDataStoreMethods));
                continue;
            }
            var rel = ValidateRelation(filePath, Constants.FieldDataStoreRelation, y.DefaultTargetRelation, OntologyConstants.Relationships.UsesDb, errors);
            var rule = new DataStoreAccessRule
            {
                CallMethodNames = y.CallMethodNames.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = ToFrozenOrEmpty(y.ReceiverSubstrings, StringComparer.OrdinalIgnoreCase),
                TargetSelectorMethods = ToFrozenOrEmpty(y.TargetSelectorMethods, StringComparer.Ordinal),
                ReadMethodNames = ToFrozenOrEmpty(y.ReadMethodNames, StringComparer.Ordinal),
                WriteMethodNames = ToFrozenOrEmpty(y.WriteMethodNames, StringComparer.Ordinal),
                DefaultTargetRelation = rel,
                ResolveFluentChains = y.ResolveFluentChains ?? true,
                TableArgumentIndex = y.TableArgumentIndex ?? 0,
                StaticTargetName = y.StaticTargetName,
                ParseSqlFromTarget = y.ParseSqlFromTarget ?? false
            };
            if (y.CallNodeTypes is { Count: > 0 })
                rule = rule with { CallNodeTypes = y.CallNodeTypes.ToFrozenSet(StringComparer.Ordinal) };
            list.Add(rule);
        }
        return list;
    }

    private static List<ServiceCallSinkRule> BuildServiceCallSinks(
        string filePath,
        List<ServiceCallSinkRuleYaml>? items,
        List<DetectorValidationIssue> errors)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<ServiceCallSinkRule>(items.Count);
        foreach (var y in items)
        {
            if (y.CallMethodNames is not { Count: > 0 })
            {
                errors.Add(new DetectorValidationIssue(filePath, Constants.ErrMissingServiceCallMethods));
                continue;
            }
            var rel = ValidateRelation(filePath, Constants.FieldServiceCallRelation, y.TargetRelation, OntologyConstants.Relationships.Calls, errors);
            var rule = new ServiceCallSinkRule
            {
                CallMethodNames = y.CallMethodNames.ToFrozenSet(StringComparer.Ordinal),
                ReceiverSubstrings = ToFrozenOrEmpty(y.ReceiverSubstrings, StringComparer.OrdinalIgnoreCase),
                TargetRelation = rel,
                StaticTargetName = y.StaticTargetName,
                TargetArgumentIndex = y.TargetArgumentIndex ?? 0,
                CollectAllIdentifierArguments = y.CollectAllIdentifierArguments ?? false,
                RequireUrlOrPathArgument = y.RequireUrlOrPathArgument ?? false,
                UseMethodNameAsTarget = y.UseMethodNameAsTarget ?? false,
                LowercaseMethodNameTarget = y.LowercaseMethodNameTarget ?? false,
                AllowIdentifierArgument = y.AllowIdentifierArgument ?? false,
                TargetPrefix = y.TargetPrefix ?? string.Empty
            };
            if (y.CallNodeTypes is { Count: > 0 })
                rule = rule with { CallNodeTypes = y.CallNodeTypes.ToFrozenSet(StringComparer.Ordinal) };
            list.Add(rule);
        }
        return list;
    }

    private static List<AnnotationSinkRule> BuildAnnotationSinks(
        string filePath,
        List<AnnotationSinkRuleYaml>? items,
        List<DetectorValidationIssue> errors)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<AnnotationSinkRule>(items.Count);
        foreach (var y in items)
        {
            if (y.AnnotationNames is not { Count: > 0 })
            {
                errors.Add(new DetectorValidationIssue(filePath, Constants.ErrMissingAnnotNames));
                continue;
            }
            var rel = ValidateRelation(filePath, Constants.FieldAnnotationSinkRelation, y.TargetRelation, OntologyConstants.Relationships.Calls, errors);
            list.Add(new AnnotationSinkRule
            {
                AnnotationNames = y.AnnotationNames.ToFrozenSet(StringComparer.OrdinalIgnoreCase),
                AnnotationNodeTypes = ToFrozenOrDefault(y.AnnotationNodeTypes, UniversalAnnotationNodeTypes, StringComparer.Ordinal),
                TargetRelation = rel,
                TargetPrefix = y.TargetPrefix ?? string.Empty,
                UseAnnotationNameAsTarget = y.UseAnnotationNameAsTarget ?? true,
                StaticTargetName = y.StaticTargetName
            });
        }
        return list;
    }

    private static List<ClassHierarchySinkRule> BuildClassHierarchySinks(
        string filePath,
        List<ClassHierarchySinkRuleYaml>? items,
        List<DetectorValidationIssue> errors)
    {
        if (items is not { Count: > 0 }) return [];
        var list = new List<ClassHierarchySinkRule>(items.Count);
        foreach (var y in items)
        {
            if (y.BaseTypeSubstrings is not { Count: > 0 })
            {
                errors.Add(new DetectorValidationIssue(filePath, Constants.ErrMissingBaseTypes));
                continue;
            }
            var rel = ValidateRelation(filePath, Constants.FieldHierarchySinkRelation, y.TargetRelation, OntologyConstants.Relationships.Exposes, errors);
            list.Add(new ClassHierarchySinkRule
            {
                BaseTypeSubstrings = y.BaseTypeSubstrings.ToFrozenSet(StringComparer.Ordinal),
                ClassDeclarationNodeTypes = ToFrozenOrDefault(y.ClassDeclarationNodeTypes, UniversalClassDeclarationNodeTypes, StringComparer.Ordinal),
                TargetRelation = rel,
                TargetPrefix = y.TargetPrefix ?? string.Empty,
                TargetSuffix = y.TargetSuffix ?? string.Empty,
                LowercaseClassName = y.LowercaseClassName ?? false,
                EmitTableNode = y.EmitTableNode ?? false
            });
        }
        return list;
    }

    private static FrozenSet<string> ToFrozenOrEmpty(List<string>? values, StringComparer comparer) =>
        values is { Count: > 0 } ? values.ToFrozenSet(comparer) : FrozenSet<string>.Empty;

    private static FrozenSet<string> ToFrozenOrDefault(List<string>? values, FrozenSet<string> fallback, StringComparer comparer) =>
        values is { Count: > 0 } ? values.ToFrozenSet(comparer) : fallback;

    private static Dictionary<string, (ISemanticExtension Extension, List<string> Languages)> CollectBuiltInExtensionsById()
    {
        var map = new Dictionary<string, (ISemanticExtension Extension, List<string> Languages)>(StringComparer.OrdinalIgnoreCase);
        foreach (var fp in ParserRegistry.GetAllFileParsers())
        {
            foreach (var ext in fp.SemanticExtensions)
            {
                if (string.IsNullOrEmpty(ext.Id)) continue;
                if (!map.TryGetValue(ext.Id, out var existing))
                {
                    map[ext.Id] = (ext, [fp.LanguageName]);
                }
                else if (!existing.Languages.Contains(fp.LanguageName, StringComparer.OrdinalIgnoreCase))
                {
                    existing.Languages.Add(fp.LanguageName);
                }
            }
        }
        return map;
    }

    private static FrozenSet<string> LoadValidRelations()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = typeof(OntologyConstants.Relationships).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        foreach (var f in fields)
        {
            if (f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string) && f.GetRawConstantValue() is string val)
            {
                set.Add(val);
            }
        }
        return set.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    internal static string ToSnakeCase(string pascal)
    {
        var sb = new StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
