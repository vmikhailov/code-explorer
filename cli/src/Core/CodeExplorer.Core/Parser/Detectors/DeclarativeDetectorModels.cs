using System.Collections.Concurrent;
using YamlDotNet.Serialization;

namespace CodeExplorer.Core.Parser;

public enum DetectorOrigin
{
    BuiltIn = 0,
    Global = 1,
    Workspace = 2
}

public sealed record DetectorValidationIssue(
    string FilePath,
    string Message,
    bool IsWarning = false);

public sealed class DetectorValidationException : InvalidOperationException
{
    public IReadOnlyList<DetectorValidationIssue> Errors { get; }

    public DetectorValidationException(IReadOnlyList<DetectorValidationIssue> errors)
        : base(FormatMessage(errors))
    {
        Errors = errors;
    }

    private static string FormatMessage(IReadOnlyList<DetectorValidationIssue> errors)
    {
        var lines = new List<string>(errors.Count + 1)
        {
            $"Invalid custom YAML detector configuration ({errors.Count} error(s)):"
        };
        for (var i = 0; i < errors.Count; i++)
        {
            lines.Add($"  - {errors[i].FilePath}: {errors[i].Message}");
        }
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed record DetectorDescriptorSummary(
    string Id,
    string Name,
    DetectorOrigin Origin,
    IReadOnlyList<string> Languages,
    SemanticExtensionType ExtensionDomain,
    LibraryRole Role,
    IReadOnlyList<string> SupportedPatterns,
    bool OverridesBuiltIn,
    bool ShadowsImperativeRules,
    string? SourcePath);

public sealed class YamlDeclarativeSemanticExtension : DeclarativeSemanticExtension
{
    private const char HashSeparator = '@';

    public required DetectorOrigin Origin { get; init; }
    public required string SourceFilePath { get; init; }
    public required string ContentHash8 { get; init; }
    public required IReadOnlyList<string> TargetLanguages { get; init; }
    public bool OverridesBuiltIn { get; init; }
    public bool ShadowsImperativeRules { get; init; }

    internal override string CacheKeyToken => string.Concat(Id, HashSeparator, ContentHash8);

    public bool AppliesToLanguage(string languageName)
    {
        for (var i = 0; i < TargetLanguages.Count; i++)
        {
            var target = TargetLanguages[i];
            if (target == "*" || string.Equals(target, languageName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}

public sealed class WorkspaceDetectorCatalog
{
    private const string EmptyHashValue = "none";
    private const int MaxCachedParsers = 64;
    private static readonly AsyncLocal<WorkspaceDetectorCatalog?> AmbientScope = new();

    public static readonly WorkspaceDetectorCatalog Empty = new([], [], EmptyHashValue);

    public static WorkspaceDetectorCatalog? CurrentScope
    {
        get => AmbientScope.Value;
        internal set => AmbientScope.Value = value;
    }

    private readonly IReadOnlyList<YamlDeclarativeSemanticExtension> _customDetectors;
    private readonly ConcurrentDictionary<IFileParser, (List<ISemanticExtension> Active, SemanticExtensionRegistry Registry)> _effectiveCache = new();

    public IReadOnlyList<YamlDeclarativeSemanticExtension> CustomDetectors => _customDetectors;
    public IReadOnlyList<DetectorValidationIssue> Warnings { get; }
    public string CombinedHash { get; }
    public bool HasCustomDetectors => _customDetectors.Count > 0;

    public WorkspaceDetectorCatalog(
        IReadOnlyList<YamlDeclarativeSemanticExtension> customDetectors,
        IReadOnlyList<DetectorValidationIssue> warnings,
        string combinedHash)
    {
        _customDetectors = customDetectors;
        Warnings = warnings;
        CombinedHash = string.IsNullOrEmpty(combinedHash) ? EmptyHashValue : combinedHash;
    }

    public (List<ISemanticExtension> Active, SemanticExtensionRegistry Registry) GetEffectiveRegistry(IFileParser fileParser)
    {
        if (_effectiveCache.Count > MaxCachedParsers)
        {
            _effectiveCache.Clear();
        }

        return _effectiveCache.GetOrAdd(fileParser, BuildEffectiveForParser);
    }

    public IReadOnlyList<ISemanticExtension> GetEffectiveExtensions(IFileParser fileParser)
    {
        if (!HasCustomDetectors)
        {
            return fileParser.SemanticExtensions;
        }

        var applicable = new List<YamlDeclarativeSemanticExtension>();
        var overriddenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < _customDetectors.Count; i++)
        {
            var custom = _customDetectors[i];
            if (custom.AppliesToLanguage(fileParser.LanguageName))
            {
                applicable.Add(custom);
                overriddenIds.Add(custom.Id);
            }
        }

        if (applicable.Count == 0)
        {
            return fileParser.SemanticExtensions;
        }

        var merged = new List<ISemanticExtension>(fileParser.SemanticExtensions.Count + applicable.Count);
        for (var i = 0; i < fileParser.SemanticExtensions.Count; i++)
        {
            var builtIn = fileParser.SemanticExtensions[i];
            if (!overriddenIds.Contains(builtIn.Id))
            {
                merged.Add(builtIn);
            }
        }

        merged.AddRange(applicable);
        return merged;
    }

    public IReadOnlyList<PackageDescriptor> GetEffectivePackages(IProjectParser projectParser)
    {
        if (!HasCustomDetectors)
        {
            return projectParser.Packages;
        }

        var canonicalLang = WorkspaceDetectorLanguageMap.NormalizeProjectLanguage(projectParser.ProjectType);
        var applicable = new List<YamlDeclarativeSemanticExtension>();
        var overriddenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < _customDetectors.Count; i++)
        {
            var custom = _customDetectors[i];
            if (custom.AppliesToLanguage(canonicalLang))
            {
                applicable.Add(custom);
                overriddenIds.Add(custom.Id);
            }
        }

        if (applicable.Count == 0)
        {
            return projectParser.Packages;
        }

        var result = new List<PackageDescriptor>(projectParser.Packages.Count + applicable.Count);
        for (var i = 0; i < projectParser.Packages.Count; i++)
        {
            var pkg = projectParser.Packages[i];
            if (!overriddenIds.Contains(pkg.Id))
            {
                result.Add(pkg);
            }
        }

        for (var i = 0; i < applicable.Count; i++)
        {
            result.AddRange(applicable[i].GetPackageDescriptors());
        }

        return result;
    }

    private (List<ISemanticExtension> Active, SemanticExtensionRegistry Registry) BuildEffectiveForParser(IFileParser fp)
    {
        var effective = GetEffectiveExtensions(fp);
        var active = new List<ISemanticExtension>();
        for (var i = 0; i < effective.Count; i++)
        {
            var lp = effective[i];
            if (lp.IsImplemented && lp.IsBuiltIn)
            {
                active.Add(lp);
            }
        }

        var reg = new SemanticExtensionRegistry(effective);
        return (active, reg);
    }
}

public sealed class DeclarativeDetectorYaml
{
    public int SchemaVersion { get; set; }
    public string? Id { get; set; }
    public string? Name { get; set; }
    public List<string>? Languages { get; set; }

    [YamlMember(Alias = "type")]
    public string? ExtensionDomain { get; set; }

    public string? LibraryRole { get; set; }
    public bool Override { get; set; }
    public List<string>? SupportedPatterns { get; set; }

    public List<AnnotationEndpointRuleYaml>? AnnotationEndpoints { get; set; }
    public List<InboundRouteCallRuleYaml>? InboundRouteCalls { get; set; }
    public List<OutboundHttpRuleYaml>? OutboundHttp { get; set; }
    public List<SqlSinkRuleYaml>? SqlSinks { get; set; }
    public List<OrmTableRuleYaml>? OrmTables { get; set; }
    public List<MessagingBrokerRuleYaml>? MessagingBrokers { get; set; }
    public List<DataStoreAccessRuleYaml>? DataStoreAccess { get; set; }
    public List<ServiceCallSinkRuleYaml>? ServiceCallSinks { get; set; }
    public List<AnnotationSinkRuleYaml>? AnnotationSinks { get; set; }
    public List<ClassHierarchySinkRuleYaml>? ClassHierarchySinks { get; set; }
}

public sealed class AnnotationEndpointRuleYaml
{
    public Dictionary<string, string>? AnnotationToHttpMethod { get; set; }
    public List<string>? AnnotationNodeTypes { get; set; }
    public List<string>? PathArgumentKeys { get; set; }
    public List<string>? RoutePrefixAnnotationNames { get; set; }
    public List<string>? WebSocketAnnotationNames { get; set; }
    public bool? EnrichJavaSecurityAndSchemas { get; set; }
}

public sealed class InboundRouteCallRuleYaml
{
    public Dictionary<string, string>? MethodToHttpVerb { get; set; }
    public List<string>? ReceiverSubstrings { get; set; }
    public List<string>? ExcludedReceivers { get; set; }
    public List<string>? GroupMethodNames { get; set; }
    public bool? ResolveChainedRouteGroups { get; set; }
    public int? PathArgumentIndex { get; set; }
    public string? StaticRoute { get; set; }
    public string? RoutePrefix { get; set; }
    public bool? EmitEntryPointNode { get; set; }
    public bool? RequireLeadingSlashOnPath { get; set; }
    public bool? EmitHandlerTriggers { get; set; }
    public List<string>? CallNodeTypes { get; set; }
}

public sealed class OutboundHttpRuleYaml
{
    public List<string>? CallMethodNames { get; set; }
    public List<string>? ClientAnnotationSuffixes { get; set; }
    public List<string>? CallNodeTypes { get; set; }
    public List<string>? AnnotationNodeTypes { get; set; }
    public string? FallbackTarget { get; set; }
    public bool? ResolveFluentChains { get; set; }
}

public sealed class SqlSinkRuleYaml
{
    public List<string>? CallMethodNames { get; set; }
    public List<string>? QueryAnnotationSuffixes { get; set; }
    public bool? RequireExtractableSqlOnCall { get; set; }
    public bool? AppendMethodNameWhenNoSql { get; set; }
    public string? FallbackQueryPrefix { get; set; }
    public List<string>? CallNodeTypes { get; set; }
    public List<string>? AnnotationNodeTypes { get; set; }
}

public sealed class OrmTableRuleYaml
{
    public List<string>? TableAnnotationNames { get; set; }
    public List<string>? NameArgumentKeys { get; set; }
    public List<string>? AnnotationNodeTypes { get; set; }
    public List<string>? ClassDeclarationNodeTypes { get; set; }
    public List<string>? BaseClassNames { get; set; }
    public List<string>? RepositoryInterfaceNames { get; set; }
}

public sealed class MessagingBrokerRuleYaml
{
    public string? Protocol { get; set; }
    public List<string>? ListenerAnnotationNames { get; set; }
    public List<string>? ProducerAnnotationNames { get; set; }
    public List<string>? ChannelAnnotationNames { get; set; }
    public List<string>? MethodLevelEventAnnotations { get; set; }
    public List<string>? TopicArgumentKeys { get; set; }
    public string? FallbackTopicName { get; set; }
    public List<string>? PublishMethodNames { get; set; }
    public List<string>? PublishReceiverSubstrings { get; set; }
    public bool? ExtractEventTypeFromObjectCreationArg { get; set; }
    public bool? PreferSecondArgWhenThreeOrMoreArgs { get; set; }
    public int? PublishTopicArgumentIndex { get; set; }
    public string? StaticPublishTopic { get; set; }
    public List<string>? SubscribeMethodNames { get; set; }
    public List<string>? SubscribeReceiverSubstrings { get; set; }
    public List<string>? ConsumerInterfaceSubstrings { get; set; }
    public bool? ResolveFluentChains { get; set; }
    public List<string>? CallNodeTypes { get; set; }
    public List<string>? AnnotationNodeTypes { get; set; }
    public List<string>? ClassDeclarationNodeTypes { get; set; }
}

public sealed class DataStoreAccessRuleYaml
{
    public List<string>? CallMethodNames { get; set; }
    public List<string>? ReceiverSubstrings { get; set; }
    public List<string>? TargetSelectorMethods { get; set; }
    public List<string>? ReadMethodNames { get; set; }
    public List<string>? WriteMethodNames { get; set; }
    public string? DefaultTargetRelation { get; set; }
    public bool? ResolveFluentChains { get; set; }
    public int? TableArgumentIndex { get; set; }
    public string? StaticTargetName { get; set; }
    public bool? ParseSqlFromTarget { get; set; }
    public List<string>? CallNodeTypes { get; set; }
}

public sealed class ServiceCallSinkRuleYaml
{
    public List<string>? CallMethodNames { get; set; }
    public List<string>? ReceiverSubstrings { get; set; }
    public string? TargetRelation { get; set; }
    public string? StaticTargetName { get; set; }
    public int? TargetArgumentIndex { get; set; }
    public bool? CollectAllIdentifierArguments { get; set; }
    public bool? RequireUrlOrPathArgument { get; set; }
    public bool? UseMethodNameAsTarget { get; set; }
    public bool? LowercaseMethodNameTarget { get; set; }
    public bool? AllowIdentifierArgument { get; set; }
    public string? TargetPrefix { get; set; }
    public List<string>? CallNodeTypes { get; set; }
}

public sealed class AnnotationSinkRuleYaml
{
    public List<string>? AnnotationNames { get; set; }
    public List<string>? AnnotationNodeTypes { get; set; }
    public string? TargetRelation { get; set; }
    public string? TargetPrefix { get; set; }
    public bool? UseAnnotationNameAsTarget { get; set; }
    public string? StaticTargetName { get; set; }
}

public sealed class ClassHierarchySinkRuleYaml
{
    public List<string>? BaseTypeSubstrings { get; set; }
    public List<string>? ClassDeclarationNodeTypes { get; set; }
    public string? TargetRelation { get; set; }
    public string? TargetPrefix { get; set; }
    public string? TargetSuffix { get; set; }
    public bool? LowercaseClassName { get; set; }
    public bool? EmitTableNode { get; set; }
}
