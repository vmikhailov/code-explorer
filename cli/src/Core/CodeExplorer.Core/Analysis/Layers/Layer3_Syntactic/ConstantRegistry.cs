using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Parser;

/// <summary>
/// Thread-safe registry for constants, enums, and compile-time configuration symbols across projects.
/// Enables early-phase and late-phase AST resolution of table names, schemas, routes, and identifiers.
/// </summary>
public static class ConstantRegistry
{
    // Project-scoped lookup: "{ProjectName}:{Key}" -> "literal_value"
    private static readonly ConcurrentDictionary<string, string> _projectConstants =
        new(StringComparer.OrdinalIgnoreCase);

    // Global workspace lookup: "{Key}" -> "literal_value"
    private static readonly ConcurrentDictionary<string, string> _globalConstants =
        new(StringComparer.OrdinalIgnoreCase);

    // Fast O(1) index for unqualified member lookup: leaf name -> (unique value or null if ambiguous, match count)
    private static readonly ConcurrentDictionary<string, (string? Value, int MatchCount)> _globalConstantsByLeaf =
        new(StringComparer.OrdinalIgnoreCase);

    // Fast O(1) index for project-scoped leaf lookup: "{ProjectName}:{leaf}" -> (unique value or null if ambiguous, match count)
    private static readonly ConcurrentDictionary<string, (string? Value, int MatchCount)> _projectConstantsByLeaf =
        new(StringComparer.OrdinalIgnoreCase);

    // Fast O(1) index for project-unspecified lookup: key -> (unique value or null if ambiguous, match count)
    private static readonly ConcurrentDictionary<string, (string? Value, int MatchCount)> _projectConstantsByKey =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, string> ProjectConstants => _projectConstants;
    public static IReadOnlyDictionary<string, string> GlobalConstants => _globalConstants;

    public static void Clear()
    {
        _projectConstants.Clear();
        _globalConstants.Clear();
        _globalConstantsByLeaf.Clear();
        _projectConstantsByLeaf.Clear();
        _projectConstantsByKey.Clear();
    }

    private static readonly HashSet<string> TechSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "data", "logic", "graphql", "grapql", "contracts", "contract", "services", "service",
        "host", "api", "client", "tests", "test", "grpc", "common", "infrastructure"
    };

    public static string? ExtractDomainNameFromProject(string? projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName)) return null;
        var parts = projectName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            var filtered = parts.Where(p => !TechSuffixes.Contains(p)).ToList();
            if (filtered.Count > 0)
            {
                return filtered[^1];
            }
        }
        return projectName;
    }

    private static readonly HashSet<string> GenericVariableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "topicNameOrId", "topicName", "subscriptionName", "subName", "queueName",
        "topic", "subscription", "queue", "url", "name", "id", "key", "value",
        "channel", "exchange", "routingKey", "receiveTopicName", "publishTopicName",
        "sendTopicName", "ruleTreeTopic", "ruleTreeSub", "kvTopic",
        "PUBSUB_TOPIC_NAME", "PUBSUB_SUBSCRIPTION_NAME", "TOPIC_NAME", "SUBSCRIPTION_NAME",
        "EVENT_SUBSCRIBER_NAME", "EVENT_RECEIVE_TOPIC_NAME", "EVENT_SEND_TOPIC_NAME"
    };

    public static void Register(string? projectName, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;

        var cleanKey = key.Trim();
        var cleanVal = value.Trim().Trim('\'', '"', '`');

        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var projTrimmed = projectName.Trim();
            _projectConstants[$"{projTrimmed}:{cleanKey}"] = cleanVal;
            IndexProjectLeaf(projTrimmed, cleanKey, cleanVal);
            IndexProjectKey(cleanKey, cleanVal);

            var domain = ExtractDomainNameFromProject(projectName);
            if (!string.IsNullOrWhiteSpace(domain) && !domain.Equals(projTrimmed, StringComparison.OrdinalIgnoreCase))
            {
                _projectConstants[$"{domain}:{cleanKey}"] = cleanVal;
                IndexProjectLeaf(domain, cleanKey, cleanVal);
            }

            var stripped = EndpointScoringEngine.StripCommonAffixes(projectName);
            if (!string.IsNullOrWhiteSpace(stripped) && !stripped.Equals(projTrimmed, StringComparison.OrdinalIgnoreCase))
            {
                _projectConstants[$"{stripped}:{cleanKey}"] = cleanVal;
                IndexProjectLeaf(stripped, cleanKey, cleanVal);
                var normStripped = stripped.Replace("-", "_");
                _projectConstants[$"{normStripped}:{cleanKey}"] = cleanVal;
                IndexProjectLeaf(normStripped, cleanKey, cleanVal);
            }

            var leafKey = cleanKey.Contains('.') ? cleanKey[(cleanKey.LastIndexOf('.') + 1)..] : cleanKey;
            var isGenericKey = GenericVariableNames.Contains(cleanKey) || GenericVariableNames.Contains(leafKey);
            if (!isGenericKey)
            {
                if (cleanKey.Contains(':') || cleanKey.Contains('.') || cleanKey.Contains("__") ||
                    (cleanKey.Length > 0 && char.IsUpper(cleanKey[0])))
                {
                    if (_globalConstants.TryAdd(cleanKey, cleanVal))
                    {
                        IndexGlobalLeaf(cleanKey, cleanVal);
                    }
                }
            }
        }
        else
        {
            var isBareGeneric = !cleanKey.Contains('.') && !cleanKey.Contains(':') && GenericVariableNames.Contains(cleanKey);
            if (!isBareGeneric)
            {
                _globalConstants[cleanKey] = cleanVal;
                IndexGlobalLeaf(cleanKey, cleanVal);
            }
        }
    }

    private static void IndexProjectLeaf(string projKey, string key, string val)
    {
        _projectConstantsByLeaf.AddOrUpdate(
            $"{projKey}:{key}",
            _ => (val, 1),
            (_, existing) => string.Equals(existing.Value, val, StringComparison.OrdinalIgnoreCase)
                ? existing
                : (null, existing.MatchCount + 1));

        var dotIdx = key.IndexOf('.');
        while (dotIdx >= 0 && dotIdx < key.Length - 1)
        {
            var suffix = key[(dotIdx + 1)..];
            _projectConstantsByLeaf.AddOrUpdate(
                $"{projKey}:{suffix}",
                _ => (val, 1),
                (_, existing) => string.Equals(existing.Value, val, StringComparison.OrdinalIgnoreCase)
                    ? existing
                    : (null, existing.MatchCount + 1));
            dotIdx = key.IndexOf('.', dotIdx + 1);
        }
    }

    private static void IndexGlobalLeaf(string key, string val)
    {
        var dotIdx = key.IndexOf('.');
        while (dotIdx >= 0 && dotIdx < key.Length - 1)
        {
            var suffix = key[(dotIdx + 1)..];
            _globalConstantsByLeaf.AddOrUpdate(
                suffix,
                _ => (val, 1),
                (_, existing) => string.Equals(existing.Value, val, StringComparison.OrdinalIgnoreCase)
                    ? existing
                    : (null, existing.MatchCount + 1));
            dotIdx = key.IndexOf('.', dotIdx + 1);
        }
    }

    private static void IndexProjectKey(string key, string val)
    {
        _projectConstantsByKey.AddOrUpdate(
            key,
            _ => (val, 1),
            (_, existing) => string.Equals(existing.Value, val, StringComparison.OrdinalIgnoreCase)
                ? existing
                : (null, existing.MatchCount + 1));
    }

    /// <summary>Resolves a key against the project's own constants only, never the workspace-global ones.</summary>
    public static bool TryResolveProjectScoped(string? projectNameOrFilePath, string key, out string value)
    {
        value = null!;
        if (string.IsNullOrWhiteSpace(key)) return false;
        return TryResolveInProject(ExtractProjectName(projectNameOrFilePath), key.Trim().Trim('\'', '"', '`'), out value);
    }

    private static bool TryResolveInProject(string? projectName, string cleanKey, out string value)
    {
        value = null!;
        if (string.IsNullOrWhiteSpace(projectName)) return false;

        if (TryResolveExactOrLeafInProjectKey(projectName, cleanKey, out value))
        {
            return true;
        }
        var domain = ExtractDomainNameFromProject(projectName);
        if (!string.IsNullOrWhiteSpace(domain) && TryResolveExactOrLeafInProjectKey(domain, cleanKey, out value))
        {
            return true;
        }
        var stripped = EndpointScoringEngine.StripCommonAffixes(projectName);
        if (!string.IsNullOrWhiteSpace(stripped))
        {
            if (TryResolveExactOrLeafInProjectKey(stripped, cleanKey, out value))
            {
                return true;
            }
            var normStripped = stripped.Replace("-", "_");
            if (TryResolveExactOrLeafInProjectKey(normStripped, cleanKey, out value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveExactOrLeafInProjectKey(string projKey, string cleanKey, out string value)
    {
        if (_projectConstants.TryGetValue($"{projKey}:{cleanKey}", out var exactVal))
        {
            value = exactVal;
            return true;
        }

        if (_projectConstantsByLeaf.TryGetValue($"{projKey}:{cleanKey}", out var leafEntry) &&
            leafEntry.MatchCount == 1 &&
            !string.IsNullOrEmpty(leafEntry.Value))
        {
            value = leafEntry.Value;
            return true;
        }

        if (cleanKey.Contains('.'))
        {
            var lastDot = cleanKey.LastIndexOf('.');
            var memberLeaf = cleanKey[(lastDot + 1)..];
            if (_projectConstantsByLeaf.TryGetValue($"{projKey}:{memberLeaf}", out var memberLeafEntry) &&
                memberLeafEntry.MatchCount == 1 &&
                !string.IsNullOrEmpty(memberLeafEntry.Value))
            {
                value = memberLeafEntry.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    public static bool TryResolve(string? projectNameOrFilePath, string key, out string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = null!;
            return false;
        }

        var cleanKey = key.Trim().Trim('\'', '"', '`');
        var projectName = ExtractProjectName(projectNameOrFilePath);

        // 1. Try project-specific lookup (exact and project-scoped leaf index)
        if (TryResolveInProject(projectName, cleanKey, out value))
        {
            return true;
        }

        // 2. Try global lookup with exact key
        if (_globalConstants.TryGetValue(cleanKey, out var gVal))
        {
            value = gVal;
            return true;
        }

        // 2b. Fallback: try converting colon <-> double underscore (ASP.NET Configuration naming)
        if (cleanKey.Contains(':'))
        {
            var dunderKey = cleanKey.Replace(":", "__");
            if (_globalConstants.TryGetValue(dunderKey, out var dgVal))
            {
                value = dgVal;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(projectName) && _projectConstants.TryGetValue($"{projectName}:{dunderKey}", out var dpVal))
            {
                value = dpVal;
                return true;
            }
        }
        else if (cleanKey.Contains("__"))
        {
            var colonKey = cleanKey.Replace("__", ":");
            if (_globalConstants.TryGetValue(colonKey, out var cgVal))
            {
                value = cgVal;
                return true;
            }
            if (!string.IsNullOrWhiteSpace(projectName) && _projectConstants.TryGetValue($"{projectName}:{colonKey}", out var cpVal))
            {
                value = cpVal;
                return true;
            }
        }

        // 3. Fallback: if key is an unqualified member or suffix (e.g. "SourcesPlacementsBlack" or "SMART_CPA.GLOBAL_PREFIX")
        if (_globalConstantsByLeaf.TryGetValue(cleanKey, out var leafEntry) &&
            leafEntry.MatchCount == 1 &&
            !string.IsNullOrEmpty(leafEntry.Value))
        {
            value = leafEntry.Value;
            return true;
        }

        // 4. Fallback: if project is unspecified, search _projectConstants for a matching key
        if (string.IsNullOrWhiteSpace(projectName))
        {
            if (_projectConstantsByKey.TryGetValue(cleanKey, out var pEntry) &&
                pEntry.MatchCount == 1 &&
                !string.IsNullOrEmpty(pEntry.Value))
            {
                value = pEntry.Value;
                return true;
            }
        }

        value = null!;
        return false;
    }

    private static readonly HashSet<string> MonorepoContainers = new(StringComparer.OrdinalIgnoreCase)
    {
        "services", "service", "bin", "apps", "packages", "crates", "libs", "lib"
    };

    public static string? ExtractProjectName(string? projectNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(projectNameOrPath)) return null;

        if (Urn.TryParse(projectNameOrPath, out var urn) && urn.HasDeclaringFilePath)
        {
            projectNameOrPath = urn.DeclaringFilePath!;
        }

        var s = projectNameOrPath.Replace('\\', '/').Trim();
        if (!s.Contains('/')) return s;

        // Given a path like "traffic-types/src/modules/foo.ts" or "src/services/fe/src/app/foo.ts", extract the project folder ("traffic-types", "fe").
        var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            // 1. If there is a project "src" folder (at index > 0) preceded by a project name (not a monorepo container or relative dot),
            //    the segment immediately preceding that "src" is the project directory.
            for (var i = 1; i < parts.Length; i++)
            {
                if (parts[i].Equals("src", StringComparison.OrdinalIgnoreCase))
                {
                    // If "src" is followed by a monorepo container (e.g. "src/services/fe/..."), keep scanning
                    if (i + 1 < parts.Length && MonorepoContainers.Contains(parts[i + 1]))
                    {
                        continue;
                    }

                    var prev = parts[i - 1];
                    if (prev is not (".." or ".") && !prev.Contains(':') && !MonorepoContainers.Contains(prev))
                    {
                        return prev;
                    }
                }
            }

            // 2. Check top-level monorepo containers ("services/xyz", "apps/xyz", "libs/xyz", etc.)
            for (var i = 0; i < parts.Length - 1; i++)
            {
                if (MonorepoContainers.Contains(parts[i]))
                {
                    var next = parts[i + 1];
                    if (i + 1 < parts.Length - 1 || !next.Contains('.'))
                    {
                        return next;
                    }
                }
            }

            // 3. Path starting with "src/<ProjectName>/..."
            if (parts[0].Equals("src", StringComparison.OrdinalIgnoreCase) && parts.Length >= 2)
            {
                if (parts.Length > 2 || !parts[1].Contains('.'))
                {
                    return parts[1];
                }
            }

            // 4. Clean workspace-relative path starting directly with the project folder (e.g. "OrderService.Api/Controllers/Foo.cs")
            if (!s.StartsWith('/') && !parts[0].Contains(':') && parts[0] is not (".." or "."))
            {
                return parts[0];
            }

            if (parts.Length >= 2)
            {
                var parentDir = parts[^2];
                if (!parentDir.Equals("src", StringComparison.OrdinalIgnoreCase) && !parentDir.Contains('.'))
                {
                    return parentDir;
                }
                if (parts.Length >= 3 && !parts[^3].Contains('.'))
                {
                    return parts[^3];
                }
            }
            return parts[0];
        }

        return null;
    }

    public static void ScanAndRegister(string filePath, string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        AstConstantExtractor.ExtractAndRegister(filePath, content, projectName);
        ScanAndRegisterRegexOnly(filePath, content, projectName);
    }

    public static void ScanAndRegisterRegexOnly(string filePath, string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        // (AST extraction handled in ScanAndRegister or directly via SyntaxTree in Layer 3)

        // 2. Run regex scanner for any non-AST / comment / fallback definitions
        switch (ext)
        {
            case ".ts":
            case ".tsx":
            case ".js":
            case ".jsx":
                ScanAndRegisterTypeScript(content, projectName);
                break;

            case ".go":
                ScanAndRegisterGo(content, projectName);
                break;
        }
    }

    public static bool TryDeriveTopicOrQueueFromConstantName(string name, out string derived) =>
        TryDeriveTopicOrQueueFromEnvVar(name, out derived);

    /// <summary>
    /// A subscription key (e.g. EVENT_BUS_SUBSCRIPTION_NAME) only holds the subscription's own name,
    /// so the topic is taken from the parent-topic convention rather than from the key's literal value.
    /// </summary>
    public static bool TryDeriveParentTopicFromSubscriptionKey(string key, out string topic)
    {
        topic = string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return false;
        var upper = key.ToUpperInvariant();
        var isSubscriptionKey = upper.EndsWith("_SUBSCRIPTION_NAME") || upper.EndsWith("_SUBSCRIBER_NAME") ||
                                upper.EndsWith("_SUBSCRIPTION") || upper.EndsWith("_SUBSCRIBER") ||
                                upper.EndsWith("_SUB_NAME") || upper.EndsWith("_SUB");
        return isSubscriptionKey && TryDeriveTopicOrQueueFromEnvVar(key, out topic);
    }
    public static bool TryDeriveTopicOrQueueFromEnvVar(string envVar, out string derived)
    {
        derived = string.Empty;
        if (string.IsNullOrWhiteSpace(envVar)) return false;
        var upper = envVar.ToUpperInvariant();

        // 0. Immediately reject non-messaging keywords (URLs, ports, secrets, DB tables, metrics, flags)
        if (upper.Contains("_URL") || upper.Contains("_URI") || upper.Contains("_PORT") ||
            upper.Contains("_HOST") || upper.Contains("_KEY") || upper.Contains("_SECRET") ||
            upper.Contains("_TOKEN") || upper.Contains("_PASSWORD") || upper.Contains("_TABLE") ||
            upper.Contains("_COLLECTION") || upper.Contains("_DB") || upper.Contains("_DATABASE") ||
            upper.Contains("_COUNT") || upper.Contains("_TIMEOUT") || upper.Contains("_INTERVAL") ||
            upper.Contains("_RETRIES") || upper.Contains("_PATH") || upper.Contains("_BUCKET") ||
            upper.EndsWith("_ENABLED") || upper.StartsWith("ENABLE_") || upper.StartsWith("DISABLE_"))
        {
            return false;
        }

        // 1. Explicit subscription names: EVENT_BUS_SUBSCRIPTION_NAME, ORDER_EVENTS_SUB -> parent topic
        if (upper.EndsWith("_SUBSCRIPTION_NAME") || upper.EndsWith("_SUBSCRIBER_NAME") ||
            upper.EndsWith("_SUBSCRIPTION") || upper.EndsWith("_SUBSCRIBER") ||
            upper.EndsWith("_SUB_NAME") || upper.EndsWith("_SUB") || upper.Contains("_SUB_TOPIC"))
        {
            var asTopic = upper.Replace("_SUBSCRIPTION_NAME", "_TOPIC")
                               .Replace("_SUBSCRIBER_NAME", "_TOPIC")
                               .Replace("_SUB_NAME", "_TOPIC")
                               .Replace("_SUBSCRIPTION", "_TOPIC")
                               .Replace("_SUBSCRIBER", "_TOPIC");
            if (asTopic.EndsWith("_SUB")) asTopic = asTopic[..^4] + "_TOPIC";
            if (asTopic != upper && !asTopic.Equals("TOPIC", StringComparison.OrdinalIgnoreCase))
            {
                var norm = WorkspaceConventions.NormalizeTopicName(Conventions.Services.MessagingConventionService.AlgorithmicTopicKebabCase(asTopic));
                if (!string.IsNullOrEmpty(norm) && !WorkspaceConventions.IsPlaceholderName(norm))
                {
                    derived = norm;
                    return true;
                }
            }
            return false;
        }

        // 2. Explicit topics, queues, and exchanges: EVENT_BUS_TOPIC_NAME -> event-bus-topic
        if (upper.EndsWith("_TOPIC_NAME") || upper.EndsWith("_TOPIC") ||
            upper.EndsWith("_TOPIC_ID") ||
            upper.EndsWith("_QUEUE_NAME") || upper.EndsWith("_QUEUE") ||
            upper.EndsWith("_QUEUE_ID") ||
            upper.EndsWith("_EXCHANGE_NAME") || upper.EndsWith("_EXCHANGE"))
        {
            var raw = upper;
            if (raw.EndsWith("_NAME")) raw = raw[..^5];
            else if (raw.EndsWith("_ID")) raw = raw[..^3];

            if (raw is "TOPIC" or "QUEUE" or "EXCHANGE" or "DEFAULT_TOPIC" or "DEFAULT_QUEUE") return false;
            var norm = WorkspaceConventions.NormalizeTopicName(Conventions.Services.MessagingConventionService.AlgorithmicTopicKebabCase(raw));
            if (!string.IsNullOrEmpty(norm) && !WorkspaceConventions.IsPlaceholderName(norm))
            {
                derived = norm;
                return true;
            }
        }

        return false;
    }

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static void ScanAndRegisterTypeScript(string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        try
        {
            // 1. Enum declarations: enum Name { Key = 'val', Key2 = "val2" }
            var enumMatches = Regex.Matches(content,
                @"(?:export\s+)?(?:const\s+)?enum\s+([A-Za-z0-9_]+)\s*\{([\s\S]*?)\}",
                RegexOptions.Multiline, RegexTimeout);

            foreach (Match em in enumMatches)
            {
                var enumName = em.Groups[1].Value.Trim();
                var body = em.Groups[2].Value;

                var memberMatches = Regex.Matches(body,
                    @"([A-Za-z0-9_]+)\s*=\s*['""`]([^'""`\r\n]+)['""`]",
                    RegexOptions.None, RegexTimeout);

                foreach (Match mm in memberMatches)
                {
                    var memberName = mm.Groups[1].Value.Trim();
                    var memberVal = mm.Groups[2].Value.Trim();
                    Register(projectName, $"{enumName}.{memberName}", memberVal);
                }
            }

            // 2. Const objects: const Name = { Key: 'val' } [as const]
            var constObjMatches = Regex.Matches(content,
                @"(?:export\s+)?(?:const|let|var)\s+([A-Za-z0-9_]+)\s*(?::\s*[^=]+)?\s*=\s*\{([\s\S]*?)\}(?:\s*as\s+const)?(?:\s*;|\n|$)",
                RegexOptions.Multiline, RegexTimeout);

            foreach (Match com in constObjMatches)
            {
                var objName = com.Groups[1].Value.Trim();
                var body = com.Groups[2].Value;

                var propMatches = Regex.Matches(body,
                    @"['""]?([A-Za-z0-9_]+)['""]?\s*:\s*['""`]([^'""`\r\n]+)['""`]",
                    RegexOptions.None, RegexTimeout);

                foreach (Match pm in propMatches)
                {
                    var propName = pm.Groups[1].Value.Trim();
                    var propVal = pm.Groups[2].Value.Trim();
                    Register(projectName, $"{objName}.{propName}", propVal);
                }
            }

            // 3. Const / let / var string declarations: export const TABLE_NAME = 'val'; const apiVersion = 'v1';
            var topConstMatches = Regex.Matches(content,
                @"(?:export\s+)?(?:const|let|var)\s+([A-Za-z0-9_]+)\s*(?::\s*[^=]+)?\s*=\s*['""`]([^'""`\r\n]+)['""`](?:\s*as\s+const)?\s*;?",
                RegexOptions.Multiline, RegexTimeout);

            foreach (Match tcm in topConstMatches)
            {
                var constName = tcm.Groups[1].Value.Trim();
                var constVal = tcm.Groups[2].Value.Trim();
                Register(projectName, constName, constVal);
            }

            // 3b. Function call initialization with string literal argument:
            // export const BQ_ROUTES_CALC_HOST = LINK_GENERATOR('bq-routes-calc');
            // const bundlesHost = getHost('bundles');
            // this.ruleConfiguratorHost = this.linkGenerator('rule-configurator');
            var funcConstMatches = Regex.Matches(content,
                @"(?:(?:export\s+)?(?:const|let|var)\s+|this\.)([A-Za-z0-9_]+)\s*(?::\s*[^=]+)?\s*=\s*(?:this\.)?[A-Za-z0-9_]+\s*\(\s*['""`]([^'""`\r\n]+)['""`]\s*\)\s*;?",
                RegexOptions.Multiline, RegexTimeout);

            foreach (Match fcm in funcConstMatches)
            {
                var constName = fcm.Groups[1].Value.Trim();
                var argVal = fcm.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(argVal))
                {
                    Register(projectName, constName, argVal);
                }
            }

            // 4. ConfigService / process.env field and constant assignments:
            // this.ruleTreeTopic = configService.getString('RULE_TREE_TOPIC');
            // this.topicName = configService.getString('EVENT_BUS_TOPIC_NAME');
            // readonly eventBusSubscriptionName: string = this.configService.getString('EVENT_BUS_SUBSCRIPTION_NAME');
            // export const TOPIC_NAME = String(process.env.EVENT_BUS_TOPIC_NAME);
            var configServiceMatches = Regex.Matches(content,
                @"(?:(?:export\s+)?(?:const|let|var|readonly|public|private|protected)\s+|this\.)*([A-Za-z0-9_]+)\s*(?::\s*[^=]+)?\s*=\s*(?:(?:String|Number)\s*\(\s*)?(?:(?:this\.)?(?:configService|config|envService|env)\.(?:getString|get|getOrThrow)(?:<[^>]+>)?|process\.env)\s*(?:\(\s*['""`]([A-Za-z0-9_]+)['""`]\s*\)|\.([A-Za-z0-9_]+)|\[\s*['""`]([A-Za-z0-9_]+)['""`]\s*\])",
                RegexOptions.Multiline, RegexTimeout);

            foreach (Match csm in configServiceMatches)
            {
                var fieldName = csm.Groups[1].Value.Trim();
                var envVar = !string.IsNullOrEmpty(csm.Groups[2].Value) ? csm.Groups[2].Value.Trim()
                           : !string.IsNullOrEmpty(csm.Groups[3].Value) ? csm.Groups[3].Value.Trim()
                           : csm.Groups[4].Value.Trim();
                if (!string.IsNullOrEmpty(envVar) && envVar.Length > 2)
                {
                    string resolvedValue;
                    if (TryDeriveParentTopicFromSubscriptionKey(envVar, out var parentTopic))
                    {
                        resolvedValue = parentTopic;
                    }
                    else if (TryResolve(projectName, envVar, out var knownVal) && !string.Equals(knownVal, envVar, StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedValue = knownVal;
                    }
                    else if (TryDeriveTopicOrQueueFromEnvVar(envVar, out var derivedVal))
                    {
                        resolvedValue = derivedVal;
                    }
                    else
                    {
                        resolvedValue = envVar;
                    }
                    Register(projectName, fieldName, resolvedValue);
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Safeguard against catastrophic backtracking on complex/minified files
        }
    }

    public static void ScanAndRegisterGo(string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        try
        {
            // 1. Const block: const ( UsersTable = "users" )
            var blockMatches = Regex.Matches(content,
                @"const\s*\(([\s\S]*?)\)",
                RegexOptions.Multiline, RegexTimeout);

            foreach (Match bm in blockMatches)
            {
                var body = bm.Groups[1].Value;
                var constMatches = Regex.Matches(body,
                    @"([A-Za-z0-9_]+)\s*(?:string)?\s*=\s*""([^""\r\n]+)""",
                    RegexOptions.None, RegexTimeout);

                foreach (Match cm in constMatches)
                {
                    var constName = cm.Groups[1].Value.Trim();
                    var constVal = cm.Groups[2].Value.Trim();
                    Register(projectName, constName, constVal);
                }
            }

            // 2. Single const: const UsersTable = "users"
            var singleMatches = Regex.Matches(content,
                @"const\s+([A-Za-z0-9_]+)\s*(?:string)?\s*=\s*""([^""\r\n]+)""\s*",
                RegexOptions.Multiline, RegexTimeout);

            foreach (Match sm in singleMatches)
            {
                var constName = sm.Groups[1].Value.Trim();
                var constVal = sm.Groups[2].Value.Trim();
                Register(projectName, constName, constVal);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Safeguard against catastrophic backtracking
        }
    }
}
