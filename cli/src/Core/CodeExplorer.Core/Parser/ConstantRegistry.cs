using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common;

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

    public static void Clear()
    {
        _projectConstants.Clear();
        _globalConstants.Clear();
    }

    public static void Register(string? projectName, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value)) return;

        var cleanKey = key.Trim();
        var cleanVal = value.Trim().Trim('\'', '"', '`');

        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var pKey = $"{projectName.Trim()}:{cleanKey}";
            _projectConstants[pKey] = cleanVal;
            _globalConstants.TryAdd(cleanKey, cleanVal);
        }
        else
        {
            _globalConstants[cleanKey] = cleanVal;
        }
    }

    public static bool TryResolve(string? projectNameOrFilePath, string key, out string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            value = null!;
            return false;
        }

        var cleanKey = key.Trim().Trim('\'', '"', '`');

        // Extract project name if a file path was passed
        var projectName = ExtractProjectName(projectNameOrFilePath);

        // 1. Try project-specific lookup
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            if (_projectConstants.TryGetValue($"{projectName}:{cleanKey}", out var pVal))
            {
                value = pVal;
                return true;
            }
        }

        // 2. Try global lookup with exact key
        if (_globalConstants.TryGetValue(cleanKey, out var gVal))
        {
            value = gVal;
            return true;
        }

        // 3. Fallback: strip leading "this.config.", "config.", "this.", or "self." if present
        if (cleanKey.StartsWith("this.config.", StringComparison.OrdinalIgnoreCase))
        {
            var subKey = cleanKey["this.config.".Length..];
            if (TryResolve(projectName, subKey, out value)) return true;
        }
        else if (cleanKey.StartsWith("config.", StringComparison.OrdinalIgnoreCase))
        {
            var subKey = cleanKey["config.".Length..];
            if (TryResolve(projectName, subKey, out value)) return true;
        }
        else if (cleanKey.StartsWith("this.", StringComparison.OrdinalIgnoreCase))
        {
            var subKey = cleanKey[5..];
            if (TryResolve(projectName, subKey, out value)) return true;
        }
        else if (cleanKey.StartsWith("self.", StringComparison.OrdinalIgnoreCase))
        {
            var subKey = cleanKey[5..];
            if (TryResolve(projectName, subKey, out value)) return true;
        }

        // 4. Fallback: if key is an unqualified member (e.g. "SourcesPlacementsBlack" instead of "ModelNames.SourcesPlacementsBlack")
        if (!cleanKey.Contains('.'))
        {
            var suffix = "." + cleanKey;
            string? matchedVal = null;
            var matchCount = 0;

            foreach (var kvp in _globalConstants)
            {
                if (kvp.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    matchedVal = kvp.Value;
                    matchCount++;
                    if (matchCount > 1) break;
                }
            }

            if (matchCount == 1 && !string.IsNullOrEmpty(matchedVal))
            {
                value = matchedVal;
                return true;
            }
        }

        value = null!;
        return false;
    }

    private static string? ExtractProjectName(string? projectNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(projectNameOrPath)) return null;

        var s = projectNameOrPath.Replace('\\', '/').Trim();
        if (!s.Contains('/')) return s;

        // Given a path like "traffic-types/src/modules/foo.ts", extract "traffic-types"
        var parts = s.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            // If path contains "services/xyz", extract xyz
            var svcIdx = Array.IndexOf(parts, "services");
            if (svcIdx >= 0 && svcIdx + 1 < parts.Length)
            {
                return parts[svcIdx + 1];
            }
            return parts[0];
        }

        return null;
    }

    public static void ScanAndRegister(string filePath, string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (fileName.StartsWith(".env") || fileName.EndsWith(".env") || fileName.Contains(".env.") ||
            (fileName.Contains("env") && ext is not (".ts" or ".tsx" or ".js" or ".jsx" or ".cs" or ".go" or ".json" or ".yaml" or ".yml")))
        {
            ScanAndRegisterEnv(content, projectName);
            return;
        }

        switch (ext)
        {
            case ".ts":
            case ".tsx":
            case ".js":
            case ".jsx":
                ScanAndRegisterTypeScript(content, projectName);
                break;

            case ".cs":
                ScanAndRegisterCSharp(content, projectName);
                break;

            case ".go":
                ScanAndRegisterGo(content, projectName);
                break;
        }
    }

    public static void ScanAndRegisterEnv(string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;
        var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        // Supports: [export ]KEY="value", KEY='value', KEY=unquoted_value # comment
        var envRegex = new Regex(@"^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:([""'])(.*?)\2|([^#\r\n]*))", RegexOptions.Compiled);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || trimmed.StartsWith("//")) continue;

            var match = envRegex.Match(trimmed);
            if (!match.Success) continue;

            var key = match.Groups[1].Value.Trim();
            var val = match.Groups[3].Success ? match.Groups[3].Value.Trim() : match.Groups[4].Value.Trim();

            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val))
            {
                Register(projectName, key, val);
            }
        }
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
                var norm = WorkspaceConventions.NormalizeTopicName(asTopic);
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
            var norm = WorkspaceConventions.NormalizeTopicName(raw);
            if (!string.IsNullOrEmpty(norm) && !WorkspaceConventions.IsPlaceholderName(norm))
            {
                derived = norm;
                return true;
            }
        }

        return false;
    }

    public static void ScanAndRegisterTypeScript(string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        // 1. Enum declarations: enum Name { Key = 'val', Key2 = "val2" }
        var enumMatches = Regex.Matches(content,
            @"(?:export\s+)?(?:const\s+)?enum\s+([A-Za-z0-9_]+)\s*\{([\s\S]*?)\}",
            RegexOptions.Multiline);

        foreach (Match em in enumMatches)
        {
            var enumName = em.Groups[1].Value.Trim();
            var body = em.Groups[2].Value;

            var memberMatches = Regex.Matches(body,
                @"([A-Za-z0-9_]+)\s*=\s*['""`]([^'""`\r\n]+)['""`]");

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
            RegexOptions.Multiline);

        foreach (Match com in constObjMatches)
        {
            var objName = com.Groups[1].Value.Trim();
            var body = com.Groups[2].Value;

            var propMatches = Regex.Matches(body,
                @"['""]?([A-Za-z0-9_]+)['""]?\s*:\s*['""`]([^'""`\r\n]+)['""`]");

            foreach (Match pm in propMatches)
            {
                var propName = pm.Groups[1].Value.Trim();
                var propVal = pm.Groups[2].Value.Trim();
                Register(projectName, $"{objName}.{propName}", propVal);
            }
        }

        // 3. Top-level const string declarations: export const TABLE_NAME = 'val';
        var topConstMatches = Regex.Matches(content,
            @"(?:export\s+)?const\s+([A-Za-z0-9_]+)\s*(?::\s*string)?\s*=\s*['""`]([^'""`\r\n]+)['""`]\s*;?",
            RegexOptions.Multiline);

        foreach (Match tcm in topConstMatches)
        {
            var constName = tcm.Groups[1].Value.Trim();
            var constVal = tcm.Groups[2].Value.Trim();
            Register(projectName, constName, constVal);
        }

        // 4. ConfigService / process.env field and constant assignments:
        // this.ruleTreeTopic = configService.getString('RULE_TREE_TOPIC');
        // this.topicName = configService.getString('EVENT_BUS_TOPIC_NAME');
        // export const TOPIC_NAME = String(process.env.EVENT_BUS_TOPIC_NAME);
        var configServiceMatches = Regex.Matches(content,
            @"(?:(?:export\s+)?(?:const|let|var)\s+|this\.)?([A-Za-z0-9_]+)\s*=\s*(?:(?:String|Number)\s*\(\s*)?(?:(?:configService|config)\.(?:getString|get)|process\.env)\s*(?:\(\s*['""`]([A-Za-z0-9_]+)['""`]\s*\)|\.([A-Za-z0-9_]+))",
            RegexOptions.Multiline);

        foreach (Match csm in configServiceMatches)
        {
            var fieldName = csm.Groups[1].Value.Trim();
            var envVar = !string.IsNullOrEmpty(csm.Groups[2].Value) ? csm.Groups[2].Value.Trim() : csm.Groups[3].Value.Trim();
            if (!string.IsNullOrEmpty(envVar) && envVar.Length > 2)
            {
                string resolvedValue;
                if (TryResolve(projectName, envVar, out var knownVal) && !string.Equals(knownVal, envVar, StringComparison.OrdinalIgnoreCase))
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
                Register(projectName, $"this.{fieldName}", resolvedValue);
                Register(projectName, $"config.{fieldName}", resolvedValue);
                Register(projectName, $"this.config.{fieldName}", resolvedValue);
            }
        }
    }

    public static void ScanAndRegisterCSharp(string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        // 1. Static classes with const strings: class TableNames { public const string Users = "users"; }
        var classMatches = Regex.Matches(content,
            @"(?:public|internal|private)?\s*(?:static\s+)?class\s+([A-Za-z0-9_]+)\s*\{([\s\S]*?)\}",
            RegexOptions.Multiline);

        foreach (Match cm in classMatches)
        {
            var className = cm.Groups[1].Value.Trim();
            var body = cm.Groups[2].Value;

            var constMatches = Regex.Matches(body,
                @"const\s+string\s+([A-Za-z0-9_]+)\s*=\s*@?[""']([^""'\r\n]+)[""']\s*;");

            foreach (Match ctm in constMatches)
            {
                var constName = ctm.Groups[1].Value.Trim();
                var constVal = ctm.Groups[2].Value.Trim();
                Register(projectName, $"{className}.{constName}", constVal);
            }
        }

        // 2. Top-level or field const strings
        var fieldConstMatches = Regex.Matches(content,
            @"const\s+string\s+([A-Za-z0-9_]+)\s*=\s*@?[""']([^""'\r\n]+)[""']\s*;",
            RegexOptions.Multiline);

        foreach (Match fm in fieldConstMatches)
        {
            var constName = fm.Groups[1].Value.Trim();
            var constVal = fm.Groups[2].Value.Trim();
            Register(projectName, constName, constVal);
        }
    }

    public static void ScanAndRegisterGo(string content, string? projectName = null)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        // 1. Const block: const ( UsersTable = "users" )
        var blockMatches = Regex.Matches(content,
            @"const\s*\(([\s\S]*?)\)",
            RegexOptions.Multiline);

        foreach (Match bm in blockMatches)
        {
            var body = bm.Groups[1].Value;
            var constMatches = Regex.Matches(body,
                @"([A-Za-z0-9_]+)\s*(?:string)?\s*=\s*""([^""\r\n]+)""");

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
            RegexOptions.Multiline);

        foreach (Match sm in singleMatches)
        {
            var constName = sm.Groups[1].Value.Trim();
            var constVal = sm.Groups[2].Value.Trim();
            Register(projectName, constName, constVal);
        }
    }
}
