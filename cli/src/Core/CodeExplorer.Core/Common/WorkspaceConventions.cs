using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeExplorer.Core.Common;

/// <summary>
/// Configurable workspace naming conventions and mapping rules (topics, routing patterns, domains).
/// Loads optional custom rules from .codeexplorer/conventions.json if present in the workspace root.
/// </summary>
public static class WorkspaceConventions
{
    private static readonly ConcurrentDictionary<string, string> TopicAliases = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> ProjectToDomain = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> DomainIcons = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex RouteFunctionRegex = new(
        @"(?:get(?:ServiceDomainBy)?Route|resolveRoute|routeFor|serviceRoute)\s*\(\s*['""]([^'""]+)['""]",
        RegexOptions.Compiled);

    /// <summary>
    /// Loads custom conventions from .codeexplorer/conventions.json and .codeexplorer/domains.json if present.
    /// </summary>
    public static void LoadFromWorkspace(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot)) return;

        var configPath = Path.Combine(workspaceRoot, ".codeexplorer", "conventions.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);

                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("topics", out var topicsEl) &&
                        topicsEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in topicsEl.EnumerateObject())
                        {
                            TopicAliases[prop.Name] = prop.Value.GetString() ?? prop.Name;
                        }
                    }

                    if (doc.RootElement.TryGetProperty("domains", out var domainsEl) &&
                        domainsEl.ValueKind == JsonValueKind.Object)
                    {
                        ParseDomainMappings(domainsEl);
                    }
                }
            }
            catch
            {
                // Ignore malformed custom convention files
            }
        }

        var domainsPath = Path.Combine(workspaceRoot, ".codeexplorer", "domains.json");
        if (File.Exists(domainsPath))
        {
            try
            {
                var json = File.ReadAllText(domainsPath);

                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;

                    if (root.TryGetProperty("domains", out var domainsEl))
                    {
                        if (domainsEl.ValueKind == JsonValueKind.Array)
                        {
                            ParseDomainDefinitionsArray(domainsEl);
                        }
                        else if (domainsEl.ValueKind == JsonValueKind.Object)
                        {
                            ParseDomainMappings(domainsEl);
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Array)
                    {
                        ParseDomainDefinitionsArray(root);
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        ParseDomainMappings(root);
                    }
                }
            }
            catch
            {
                // Ignore malformed custom domains files
            }
        }
    }

    private static void ParseDomainDefinitionsArray(JsonElement root)
    {
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            if (item.TryGetProperty("name", out var nameProp))
            {
                var domainName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(domainName)) continue;

                if (item.TryGetProperty("icon", out var iconProp))
                {
                    var icon = iconProp.GetString();
                    if (!string.IsNullOrWhiteSpace(icon))
                    {
                        DomainIcons[domainName.Trim()] = icon.Trim();
                    }
                }

                if (item.TryGetProperty("projects", out var projsProp) && projsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in projsProp.EnumerateArray())
                    {
                        var proj = p.GetString();
                        if (!string.IsNullOrWhiteSpace(proj))
                        {
                            ProjectToDomain[proj.Trim()] = domainName.Trim();
                        }
                    }
                }

                if (item.TryGetProperty("services", out var srvsProp) && srvsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in srvsProp.EnumerateArray())
                    {
                        var srv = s.GetString();
                        if (!string.IsNullOrWhiteSpace(srv))
                        {
                            ProjectToDomain[srv.Trim()] = domainName.Trim();
                        }
                    }
                }
            }
        }
    }

    private static void ParseDomainMappings(JsonElement root)
    {
        foreach (var prop in root.EnumerateObject())
        {
            var domainName = prop.Name;
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray())
                {
                    var proj = item.GetString();
                    if (!string.IsNullOrWhiteSpace(proj))
                    {
                        ProjectToDomain[proj.Trim()] = domainName;
                    }
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var val = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                {
                    ProjectToDomain[domainName] = val.Trim();
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.Object && prop.Name.Equals("projects", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var projProp in prop.Value.EnumerateObject())
                {
                    var assignedDomain = projProp.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(assignedDomain))
                    {
                        ProjectToDomain[projProp.Name.Trim()] = assignedDomain.Trim();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Resets all loaded convention aliases.
    /// </summary>
    public static void Clear()
    {
        TopicAliases.Clear();
        ProjectToDomain.Clear();
        DomainIcons.Clear();
    }

    /// <summary>
    /// Attempts to resolve an aliased topic name configured in conventions.json.
    /// </summary>
    public static bool TryGetTopicAlias(string key, out string alias)
    {
        return TopicAliases.TryGetValue(key, out alias!);
    }

    /// <summary>
    /// Attempts to resolve a user-configured domain icon from domains.json.
    /// </summary>
    public static bool TryGetDomainIcon(string domainName, out string icon)
    {
        return DomainIcons.TryGetValue(domainName, out icon!);
    }

    /// <summary>
    /// Attempts to resolve a user-configured domain for a project name or path from domains.json or conventions.json.
    /// </summary>
    public static bool TryGetConfiguredDomain(string? projectName, string? filePath, out string domain)
    {
        domain = string.Empty;
        if (!string.IsNullOrWhiteSpace(projectName))
        {
            var p = projectName.Trim();
            if (ProjectToDomain.TryGetValue(p, out var d))
            {
                domain = d;
                return true;
            }
            var clean = NormalizeServiceName(p);
            if (ProjectToDomain.TryGetValue(clean, out d))
            {
                domain = d;
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            var normPath = filePath.Replace('\\', '/');
            foreach (var (key, d) in ProjectToDomain)
            {
                if (normPath.Contains("/" + key + "/", StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + key, StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + key + ".csproj", StringComparison.OrdinalIgnoreCase) ||
                    normPath.EndsWith("/" + key + ".fsproj", StringComparison.OrdinalIgnoreCase))
                {
                    domain = d;
                    return true;
                }
            }
        }

        return false;
    }

    private static readonly Regex ValidTopicNameRegex = new(
        @"^[A-Za-z0-9][A-Za-z0-9_\-\.:/]*[A-Za-z0-9_]$|^[A-Za-z0-9]$",
        RegexOptions.Compiled);

    /// <summary>
    /// Checks whether an identifier, token, or string is a placeholder dummy name (e.g. QUEUE_NAME, TOPIC_NAME, etc.).
    /// </summary>
    public static bool IsPlaceholderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;
        var lower = name.Trim().Trim('\'', '"', '`').ToLowerInvariant().Replace('_', '-').Replace('.', '-');
        return lower is "topic" or "topic-name" or "topicname" or "topicid" or "topic-id" or "topic-name-or-id" or "topicnameorid"
            or "queue" or "queue-name" or "queuename" or "queueid" or "queue-id"
            or "sub" or "subid" or "sub-id" or "subname" or "sub-name" or "subscription" or "subscription-name" or "subscriptionname"
            or "subscriber" or "subscriber-name" or "subscribername" or "event-subscriber-name"
            or "default-topic" or "default-queue" or "default-sub-id" or "default-subscription-name"
            or "target" or "target-name" or "destination"
            or "message" or "msg" or "send-data" or "senddata" or "payload" or "data" or "body"
            or "exchange" or "exchangename" or "exchange-name" or "exchangekey" or "exchange-key" or "routingkey" or "routing-key"
            or "worker-name" or "workername"
            or "placeholder" or "dummy" or "test"
            or "undefined" or "null" or "string" or "void" or "any" or "unknown" or "never" or "object" or "boolean" or "number"
            or "appmodule" or "app-module" or "other" or "broken";
    }

    /// <summary>
    /// Strictly validates whether a string represents a valid messaging topic or queue identifier.
    /// Rejects code expressions, ternary statements, function calls, URLs, emojis, and placeholders.
    /// </summary>
    public static bool IsValidTopicOrQueueName(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim().Trim('\'', '"', '`');

        if (t.Length is < 2 or > 120) return false;

        // Disallow leading / trailing separators
        if (t.StartsWith(':') || t.StartsWith('/') || t.StartsWith('.') || t.StartsWith('-') || t.StartsWith('_'))
            return false;

        // Disallow URLs
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;

        // Disallow code syntax / operators / delimiters / whitespace
        if (t.IndexOfAny(['(', ')', '[', ']', '{', '}', ';', ',', '?', '=', '<', '>', '!', '|', '&', '+', '*', '%', '^', '~', '\\', '\'', '"', '`', ' ', '\t', '\r', '\n']) >= 0)
            return false;

        // Disallow placeholder and type names
        if (IsPlaceholderName(t)) return false;

        // Disallow code keywords and member prefix calls
        if (t.StartsWith("options.", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("config.", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("function", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("return", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("const", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("let", StringComparison.OrdinalIgnoreCase) ||
            t.Equals("var", StringComparison.OrdinalIgnoreCase))
            return false;

        // Must match valid topic/queue regex (alphanumeric, dot, underscore, hyphen, colon, slash)
        if (!ValidTopicNameRegex.IsMatch(t)) return false;

        return true;
    }

    /// <summary>
    /// Normalizes raw topic names or constants using configured aliases or algorithmic kebab-casing.
    /// </summary>
    public static string NormalizeTopicName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var t = raw.Trim().Trim('\'', '"', '`');

        if (!IsValidTopicOrQueueName(t))
        {
            return string.Empty;
        }

        if (TopicAliases.TryGetValue(t, out var mapped))
        {
            return mapped;
        }

        // Algorithmic fallback: convert SCREAMING_SNAKE_CASE or camelCase to kebab-case
        var normalized = AlgorithmicTopicKebabCase(t);
        return IsValidTopicOrQueueName(normalized) ? normalized : string.Empty;
    }

    /// <summary>
    /// Matches route resolution function calls (e.g., getServiceDomainByRoute('auth'), resolveRoute('billing')).
    /// </summary>
    public static bool TryMatchRouteFunction(string text, out string routeKey)
    {
        routeKey = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = RouteFunctionRegex.Match(text);
        if (match.Success)
        {
            routeKey = match.Groups[1].Value;
            return true;
        }

        return false;
    }

    private static string AlgorithmicTopicKebabCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // If it already contains hyphens and lowercase letters, assume it's already a normalized topic
        if (name.Contains('-') && name.Any(char.IsLower)) return name;

        // Replace underscores with hyphens
        var clean = name.Replace('_', '-');

        // Split camelCase into words
        clean = Regex.Replace(clean, @"([a-z0-9])([A-Z])", "$1-$2");

        var lower = clean.ToLowerInvariant();

        // Strip trailing "-name" if it ends in "-topic-name" or "-sub-name"
        if (lower.EndsWith("-topic-name")) lower = lower[..^5];
        else if (lower.EndsWith("-sub-name")) lower = lower[..^5];

        return lower;
    }

    /// <summary>
    /// Normalizes service and project names: converts to lowercase and strictly strips prefixes
    /// such as "internal--", "integration--", "service-", "internal-service-", etc.
    /// </summary>
    public static string NormalizeServiceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var clean = name.Trim().ToLowerInvariant();

        bool changed;
        do
        {
            changed = false;
            if (clean.StartsWith("internal--"))
            {
                clean = clean["internal--".Length..];
                changed = true;
            }
            else if (clean.StartsWith("integration--"))
            {
                clean = clean["integration--".Length..];
                changed = true;
            }
            else if (clean.StartsWith("external--"))
            {
                clean = clean["external--".Length..];
                changed = true;
            }
            else if (clean.StartsWith("internal-service-"))
            {
                clean = clean["internal-service-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("integration-service-"))
            {
                clean = clean["integration-service-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("external-service-"))
            {
                clean = clean["external-service-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("service-"))
            {
                clean = clean["service-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("internal-"))
            {
                clean = clean["internal-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("integration-"))
            {
                clean = clean["integration-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("external-"))
            {
                clean = clean["external-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("srv-"))
            {
                clean = clean["srv-".Length..];
                changed = true;
            }
            else if (clean.StartsWith("internalservice-") || clean.StartsWith("internalservice_") || (clean.StartsWith("internalservice") && clean.Length > "internalservice".Length))
            {
                var len = clean.StartsWith("internalservice-") ? "internalservice-".Length :
                          clean.StartsWith("internalservice_") ? "internalservice_".Length : "internalservice".Length;
                clean = clean[len..];
                changed = true;
            }
            else if (clean.StartsWith("integrationservice-") || clean.StartsWith("integrationservice_") || (clean.StartsWith("integrationservice") && clean.Length > "integrationservice".Length))
            {
                var len = clean.StartsWith("integrationservice-") ? "integrationservice-".Length :
                          clean.StartsWith("integrationservice_") ? "integrationservice_".Length : "integrationservice".Length;
                clean = clean[len..];
                changed = true;
            }
            else if (clean.StartsWith("externalservice-") || clean.StartsWith("externalservice_") || (clean.StartsWith("externalservice") && clean.Length > "externalservice".Length))
            {
                var len = clean.StartsWith("externalservice-") ? "externalservice-".Length :
                          clean.StartsWith("externalservice_") ? "externalservice_".Length : "externalservice".Length;
                clean = clean[len..];
                changed = true;
            }
            else if (clean.StartsWith("ats-") || clean.StartsWith("ats_"))
            {
                var len = clean.StartsWith("ats-") ? "ats-".Length : "ats_".Length;
                clean = clean[len..];
                changed = true;
            }
        } while (changed);

        clean = clean.Trim('-', '_');
        return string.IsNullOrEmpty(clean) ? name.Trim().ToLowerInvariant() : clean;
    }

    /// <summary>
    /// Converts a delimited or mixed-case string into PascalCase.
    /// </summary>
    public static string ToPascalCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Default";
        var parts = text.Split(['_', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries);
        var sb = new System.Text.StringBuilder();
        foreach (var p in parts)
        {
            if (p.Length > 0)
            {
                sb.Append(char.ToUpperInvariant(p[0]));
                if (p.Length > 1) sb.Append(p[1..]);
            }
        }
        return sb.Length > 0 ? sb.ToString() : text;
    }

    private static readonly HashSet<string> BillingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "billing", "billings", "payment", "payments", "payout", "payouts", "settler", "settlement", "settlements",
        "invoice", "invoices", "invoicing", "cpm", "cpa", "rate", "rates", "pricing", "charge", "charges",
        "wallet", "wallets", "balance", "balances", "finance", "financial", "transaction", "transactions",
        "money", "subscription", "subscriptions"
    };

    private static readonly HashSet<string> AdvertisingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "adhub", "ad-hub", "ad_hub", "hub", "advert", "advertising", "advertiser", "advertisers", "partner",
        "partners", "partnership", "conversion", "conversions", "tbmap", "adserver", "ad-server", "affiliate",
        "affiliates", "publisher", "publishers", "click", "clicks"
    };

    private static readonly HashSet<string> CampaignKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "campaign", "campaigns", "bundle", "bundles", "bundl", "bundling", "split", "splits", "smartcpa",
        "smart-cpa", "smart_cpa", "landing", "landings", "lander", "landers", "staging", "creative",
        "creatives", "offer", "offers", "promo", "promotions", "targeting", "postback", "postbacks"
    };

    private static readonly HashSet<string> TrafficKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "traffic", "routing", "router", "routers", "routes", "route", "tracker", "tracking", "tracker-v2",
        "trackers", "gateway", "gateways", "edge", "proxy", "proxies", "redirect", "redirector", "redirects",
        "telecom", "carrier", "network", "networks", "skin", "skins", "ingress", "egress", "cdn"
    };

    private static readonly HashSet<string> DomainMgmtKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "domain", "domains", "domvain", "domvains", "dns", "nameserver", "nameservers", "registrar",
        "checker", "template", "ssl", "certificate", "certificates", "whois", "zone", "zones"
    };

    private static readonly HashSet<string> ConfigKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "config", "configs", "configuration", "configurations", "settings", "setting", "preference",
        "preferences", "kv", "keyvalue", "key-value", "kvv2", "kv-v2", "bindings", "binding", "cfworker",
        "cf-worker", "cloudflare-worker", "featureflag", "featureflags", "flags", "source", "sources", "rules"
    };

    private static readonly HashSet<string> AnalyticsKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "analytic", "analytics", "calc", "calculation", "calculations", "calculator", "stat", "stats",
        "statistic", "statistics", "metric", "metrics", "measure", "measures", "telemetry", "monitoring",
        "monitor", "journal", "journals", "log", "logs", "logging", "logger", "nrt", "stream", "streaming",
        "epm", "counter", "counters", "report", "reports", "reporting", "audit", "auditing", "benchmark",
        "browser", "browserversion", "browserversiontypes"
    };

    private static readonly HashSet<string> OperationsKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "approval", "approvals", "approve", "notifier", "notification", "notifications", "alert", "alerts",
        "action", "actions", "scheduler", "schedule", "schedules", "scheduling", "cron", "workflow",
        "workflows", "orchestration", "orchestrator", "task", "tasks", "job", "jobs", "queue", "queues",
        "worker", "workers", "dispatch", "dispatcher", "workerpool"
    };

    private static readonly HashSet<string> IdentityKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "auth", "authentication", "authorize", "authorization", "identity", "iam", "oauth", "token",
        "tokens", "credential", "credentials", "session", "sessions", "user", "users", "account", "accounts",
        "role", "roles", "permission", "permissions", "security", "sso"
    };

    private static readonly HashSet<string> ContentKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "content", "media", "asset", "assets", "image", "images", "video", "videos", "upload", "uploads",
        "storage", "file", "files", "document", "documents", "blob"
    };

    private static readonly HashSet<string> SupportKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "support", "ticket", "tickets", "helpdesk", "crm", "customer", "customers", "feedback"
    };

    /// <summary>
    /// Canonicalizes any raw domain, project name, or file path into a canonical DDD Problem Space (Macro-Domain).
    /// Prevents single-service micro-domains and database table names from becoming domains.
    /// </summary>
    public static string CanonicalizeDomain(
        string? rawDomain,
        string? serviceOrProjectName = null,
        string? filePath = null,
        string? role = null)
    {
        // 1. Explicit user configuration takes absolute precedence
        if (TryGetConfiguredDomain(serviceOrProjectName ?? rawDomain, filePath, out var userConfigured))
        {
            return ToPascalCase(userConfigured);
        }

        // 2. Ontology role classification
        if (role is "SharedLibrary" or "Library") return "SharedKernel";
        if (role is "CliTool") return "DeveloperTooling";
        if (role is "Test") return "TestingInfrastructure";

        // 3. Technical file path heuristics
        var normPath = (filePath ?? "").Replace('\\', '/');
        if (!string.IsNullOrEmpty(normPath))
        {
            if (normPath.StartsWith("ui/", StringComparison.OrdinalIgnoreCase) ||
                normPath.StartsWith("frontend/", StringComparison.OrdinalIgnoreCase) ||
                normPath.StartsWith("client/", StringComparison.OrdinalIgnoreCase) ||
                normPath.StartsWith("web/", StringComparison.OrdinalIgnoreCase) ||
                normPath.Contains("/ui/") ||
                normPath.Contains("/components/") ||
                normPath.Contains("/packages/ui"))
            {
                return "UserInterface";
            }

            if (normPath.StartsWith("tools/", StringComparison.OrdinalIgnoreCase) ||
                normPath.StartsWith("cli/", StringComparison.OrdinalIgnoreCase) ||
                normPath.Contains("/tools/") ||
                normPath.Contains("/cli/"))
            {
                return "DeveloperTooling";
            }

            if (normPath.StartsWith("tests/", StringComparison.OrdinalIgnoreCase) ||
                normPath.Contains("/tests/"))
            {
                return "TestingInfrastructure";
            }
        }

        // 4. Check if rawDomain already matches a canonical macro-domain
        var cleanRaw = (rawDomain ?? "").Trim();
        if (cleanRaw.StartsWith("domain:", StringComparison.OrdinalIgnoreCase))
            cleanRaw = cleanRaw["domain:".Length..];
        if (cleanRaw.StartsWith("dom:", StringComparison.OrdinalIgnoreCase))
            cleanRaw = cleanRaw["dom:".Length..];

        var rawPascal = ToPascalCase(cleanRaw);
        if (rawPascal is "BillingAndPayments" or "AdvertisingAndPartners" or "CampaignsAndBundling" or
                        "TrafficAndRouting" or "DomainManagement" or "ConfigurationAndSettings" or
                        "AnalyticsAndMonitoring" or "OperationsAndWorkflows" or "IdentityAndAccess" or
                        "UserInterface" or "SharedKernel" or "DeveloperTooling" or "TestingInfrastructure" or
                        "CustomerSupport" or "ContentAndMedia")
        {
            return rawPascal;
        }

        // Canonical aliases
        if (rawPascal is "Billing" or "Payments" or "Payment" or "Settlement") return "BillingAndPayments";
        if (rawPascal is "Advertising" or "Partners" or "Partner" or "AdHub" or "AdHubAndPartners" or "Conversion" or "Tbmap") return "AdvertisingAndPartners";
        if (rawPascal is "Campaigns" or "Campaign" or "CampaignManagement" or "Bundling" or "Bundles" or "Landing" or "Staging" or "Postback") return "CampaignsAndBundling";
        if (rawPascal is "Traffic" or "Routing" or "Routes" or "Tracker" or "Telecom" or "Gateways") return "TrafficAndRouting";
        if (rawPascal is "Domains" or "Domain" or "Domvains" or "Dns") return "DomainManagement";
        if (rawPascal is "Configuration" or "Settings" or "Config" or "Kv" or "KvV2" or "Bindings") return "ConfigurationAndSettings";
        if (rawPascal is "Analytics" or "Monitoring" or "Statistics" or "Calc" or "Journal" or "Stats") return "AnalyticsAndMonitoring";
        if (rawPascal is "Operations" or "Workflows" or "Workflow" or "Approval" or "Notifier" or "Scheduler") return "OperationsAndWorkflows";
        if (rawPascal is "Identity" or "Auth" or "Security" or "Iam") return "IdentityAndAccess";
        if (rawPascal is "PresentationComponents" or "Presentation" or "Components") return "UserInterface";

        // 5. Token-based multi-criteria scoring across rawDomain, service name, and path
        var normalizedService = NormalizeServiceName(serviceOrProjectName);
        var tokens = new List<string>();

        if (!string.IsNullOrWhiteSpace(cleanRaw))
        {
            tokens.AddRange(cleanRaw.Split(['_', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries));
        }
        if (!string.IsNullOrWhiteSpace(normalizedService))
        {
            tokens.AddRange(normalizedService.Split(['_', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries));
        }
        if (!string.IsNullOrEmpty(normPath))
        {
            var segments = normPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var seg in segments)
            {
                if (!seg.Equals("src", StringComparison.OrdinalIgnoreCase) &&
                    !seg.Equals("services", StringComparison.OrdinalIgnoreCase) &&
                    !seg.Equals("apps", StringComparison.OrdinalIgnoreCase) &&
                    !seg.Equals("packages", StringComparison.OrdinalIgnoreCase))
                {
                    tokens.AddRange(seg.Split(['_', '-', '.', ' '], StringSplitOptions.RemoveEmptyEntries));
                }
            }
        }

        var domainScores = new Dictionary<string, int>
        {
            ["BillingAndPayments"] = ScoreTokens(tokens, BillingKeywords, cleanRaw, normalizedService),
            ["AdvertisingAndPartners"] = ScoreTokens(tokens, AdvertisingKeywords, cleanRaw, normalizedService),
            ["CampaignsAndBundling"] = ScoreTokens(tokens, CampaignKeywords, cleanRaw, normalizedService),
            ["TrafficAndRouting"] = ScoreTokens(tokens, TrafficKeywords, cleanRaw, normalizedService),
            ["DomainManagement"] = ScoreTokens(tokens, DomainMgmtKeywords, cleanRaw, normalizedService),
            ["ConfigurationAndSettings"] = ScoreTokens(tokens, ConfigKeywords, cleanRaw, normalizedService),
            ["AnalyticsAndMonitoring"] = ScoreTokens(tokens, AnalyticsKeywords, cleanRaw, normalizedService),
            ["OperationsAndWorkflows"] = ScoreTokens(tokens, OperationsKeywords, cleanRaw, normalizedService),
            ["IdentityAndAccess"] = ScoreTokens(tokens, IdentityKeywords, cleanRaw, normalizedService),
            ["ContentAndMedia"] = ScoreTokens(tokens, ContentKeywords, cleanRaw, normalizedService),
            ["CustomerSupport"] = ScoreTokens(tokens, SupportKeywords, cleanRaw, normalizedService)
        };

        var best = domainScores.OrderByDescending(kv => kv.Value).FirstOrDefault();
        if (best.Value >= 2)
        {
            return best.Key;
        }

        // 6. If no canonical domain matched, check if rawDomain is a valid custom domain name
        if (!string.IsNullOrWhiteSpace(cleanRaw))
        {
            var genericDomainWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "core", "service", "services", "app", "application", "default", "internal", "microservice",
                "table", "tables", "database", "sql"
            };
            if (!genericDomainWords.Contains(cleanRaw))
            {
                return ToPascalCase(cleanRaw);
            }
        }

        // 7. Fallback to directory namespace if present
        if (!string.IsNullOrEmpty(normPath))
        {
            var segments = normPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length - 1; i++)
            {
                var s = segments[i];
                if (!s.Equals("src", StringComparison.OrdinalIgnoreCase) &&
                    !s.Equals("services", StringComparison.OrdinalIgnoreCase) &&
                    !s.Equals("apps", StringComparison.OrdinalIgnoreCase) &&
                    !s.Equals("packages", StringComparison.OrdinalIgnoreCase))
                {
                    return ToPascalCase(s);
                }
            }
        }

        return "CoreDomain";
    }

    private static int ScoreTokens(
        List<string> tokens,
        HashSet<string> keywords,
        string rawDomain,
        string normalizedService)
    {
        var score = 0;
        foreach (var t in tokens)
        {
            if (keywords.Contains(t)) score += 3;
            else if (keywords.Any(kw => kw.Contains(t, StringComparison.OrdinalIgnoreCase) || t.Contains(kw, StringComparison.OrdinalIgnoreCase)))
            {
                score += 1;
            }
        }

        if (!string.IsNullOrEmpty(rawDomain) && keywords.Contains(rawDomain)) score += 5;
        if (!string.IsNullOrEmpty(normalizedService) && keywords.Contains(normalizedService)) score += 4;

        return score;
    }

    /// <summary>
    /// Formats a canonical PascalCase domain name into a clean, human-readable display name.
    /// E.g. "BillingAndPayments" -&gt; "Billing &amp; Payments"
    /// </summary>
    public static string FormatDomainDisplayName(string canonicalDomain)
    {
        return canonicalDomain switch
        {
            "BillingAndPayments" => "Billing & Payments",
            "AdvertisingAndPartners" => "Advertising & Partners",
            "CampaignsAndBundling" => "Campaigns & Bundling",
            "TrafficAndRouting" => "Traffic & Routing",
            "DomainManagement" => "Domain Management",
            "ConfigurationAndSettings" => "Configuration & Settings",
            "AnalyticsAndMonitoring" => "Analytics & Monitoring",
            "OperationsAndWorkflows" => "Operations & Workflows",
            "IdentityAndAccess" => "Identity & Access",
            "UserInterface" => "User Interface",
            "SharedKernel" => "Shared Kernel",
            "DeveloperTooling" => "Developer Tooling",
            "TestingInfrastructure" => "Testing Infrastructure",
            "CustomerSupport" => "Customer Support",
            "ContentAndMedia" => "Content & Media",
            _ => Regex.Replace(canonicalDomain, "([a-z])([A-Z])", "$1 $2")
        };
    }
}
