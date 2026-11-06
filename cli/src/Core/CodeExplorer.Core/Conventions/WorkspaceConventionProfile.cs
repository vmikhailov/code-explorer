using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Conventions.Services;
using CodeExplorer.Core.Database;

namespace CodeExplorer.Core.Conventions;

/// <summary>
/// Workspace convention profile holding loaded configuration and specialized convention services.
/// </summary>
public sealed class WorkspaceConventionProfile : IWorkspaceConventions
{
    private static readonly Regex DefaultRouteFunctionRegex = new(
        @"(?:resolveRoute|routeFor|serviceRoute|getRoute)\s*\(\s*['""]([^'""]+)['""]",
        RegexOptions.Compiled);

    public ServiceIdentityResolver ServiceResolver { get; }
    public MessagingConventionService Messaging { get; }
    public DomainTaxonomyEngine Domains { get; }
    public NetworkEntityValidator NetworkValidator { get; }

    private readonly Dictionary<string, string> _databaseAliases;
    private readonly List<string> _customRouteFunctions;

    public WorkspaceConventionProfile(
        ServiceIdentityResolver serviceResolver,
        MessagingConventionService messaging,
        DomainTaxonomyEngine domains,
        NetworkEntityValidator networkValidator,
        IDictionary<string, string>? databaseAliases = null,
        IEnumerable<string>? customRouteFunctions = null)
    {
        ServiceResolver = serviceResolver;
        Messaging = messaging;
        Domains = domains;
        NetworkValidator = networkValidator;
        _databaseAliases = new Dictionary<string, string>(databaseAliases ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        _customRouteFunctions = [..(customRouteFunctions ?? [])];
    }

    public static WorkspaceConventionProfile Default => new(
        new ServiceIdentityResolver(),
        new MessagingConventionService(),
        new DomainTaxonomyEngine(),
        new NetworkEntityValidator()
    );

    public bool TryGetDatabaseAlias(string name, out string alias)
    {
        return _databaseAliases.TryGetValue(name, out alias!);
    }

    public bool TryMatchRouteFunction(string text, out string routeKey)
    {
        routeKey = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = DefaultRouteFunctionRegex.Match(text);
        if (match.Success)
        {
            routeKey = match.Groups[1].Value;
            return true;
        }

        foreach (var fn in _customRouteFunctions)
        {
            var m = Regex.Match(text, Regex.Escape(fn) + @"\s*\(\s*['""]([^'""]+)['""]");
            if (m.Success)
            {
                routeKey = m.Groups[1].Value;
                return true;
            }
        }

        return false;
    }

    public bool IsTestFilePath(string? path) => SourcePathClassifier.IsTestFilePath(path);
    public bool IsFrontendFilePath(string? path) => SourcePathClassifier.IsFrontendFilePath(path);

    public static string? LoadDomainsConfigJson(string? workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
            return null;

        var candidates = new[]
        {
            Path.Combine(workspaceRoot, ".codeexplorer", "domains.json"),
            Path.Combine(workspaceRoot, "domains.json")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    return File.ReadAllText(path);
                }
                catch
                {
                    // Ignore read errors
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Loads a workspace convention profile from .codeexplorer/conventions.json and .codeexplorer/domains.json.
    /// </summary>
    public static WorkspaceConventionProfile Load(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot)) return Default;

        var topics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var customPrefixes = new List<string>();
        var routeFunctions = new List<string>();
        var dbAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var svcAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var garbageHosts = new List<string>();
        var placeholderNames = new List<string>();

        var projectToDomain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var serviceOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var domainIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var patterns = new List<(Regex Regex, string Domain)>();

        var configPath = Path.Combine(workspaceRoot, ".codeexplorer", "conventions.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var root = CypherValue.TryParseJsonText(json);

                if (root.TryGetProperty("topics", out var tEl) && tEl.Kind == CypherValueKind.Object)
                {
                    foreach (var prop in tEl.AsObject())
                    {
                        topics[prop.Key] = prop.Value.AsString(prop.Key);
                    }
                }

                if (root.TryGetProperty("service_prefixes", out var spEl) && spEl.Kind == CypherValueKind.Array)
                {
                    foreach (var p in spEl.AsArray())
                    {
                        var s = p.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(s)) customPrefixes.Add(s.Trim().ToLowerInvariant());
                    }
                }

                if (root.TryGetProperty("route_functions", out var rfEl) && rfEl.Kind == CypherValueKind.Array)
                {
                    foreach (var r in rfEl.AsArray())
                    {
                        var fn = r.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(fn)) routeFunctions.Add(fn.Trim());
                    }
                }

                if (root.TryGetProperty("database_aliases", out var daEl) && daEl.Kind == CypherValueKind.Object)
                {
                    foreach (var prop in daEl.AsObject())
                    {
                        var tgt = prop.Value.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(tgt)) dbAliases[prop.Key.Trim()] = tgt.Trim();
                    }
                }

                if (root.TryGetProperty("service_aliases", out var saEl) && saEl.Kind == CypherValueKind.Object)
                {
                    foreach (var prop in saEl.AsObject())
                    {
                        var tgt = prop.Value.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(tgt)) svcAliases[prop.Key.Trim()] = tgt.Trim();
                    }
                }

                if (root.TryGetProperty("garbage_hosts", out var ghEl) && ghEl.Kind == CypherValueKind.Array)
                {
                    foreach (var h in ghEl.AsArray())
                    {
                        var host = h.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(host)) garbageHosts.Add(host.Trim().ToLowerInvariant());
                    }
                }

                if (root.TryGetProperty("placeholder_names", out var pnEl) && pnEl.Kind == CypherValueKind.Array)
                {
                    foreach (var p in pnEl.AsArray())
                    {
                        var name = p.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(name)) placeholderNames.Add(name.Trim());
                    }
                }

                if (root.TryGetProperty("domains", out var dEl) && dEl.Kind == CypherValueKind.Object)
                {
                    ParseDomainMappings(dEl, projectToDomain, domainIcons, patterns);
                }

                if (root.TryGetProperty("overrides", out var ovrEl) && ovrEl.Kind == CypherValueKind.Object)
                {
                    ParseOverrides(ovrEl, serviceOverrides, projectToDomain);
                }
            }
            catch
            {
                // Ignore malformed config files
            }
        }

        var domainsPath = Path.Combine(workspaceRoot, ".codeexplorer", "domains.json");
        if (File.Exists(domainsPath))
        {
            try
            {
                var json = File.ReadAllText(domainsPath);
                var root = CypherValue.TryParseJsonText(json);

                if (root.TryGetProperty("domains", out var domainsEl))
                {
                    if (domainsEl.Kind == CypherValueKind.Array)
                        ParseDomainDefinitionsArray(domainsEl, projectToDomain, domainIcons, patterns);
                    else if (domainsEl.Kind == CypherValueKind.Object)
                        ParseDomainMappings(domainsEl, projectToDomain, domainIcons, patterns);
                }
                else if (root.Kind == CypherValueKind.Array)
                {
                    ParseDomainDefinitionsArray(root, projectToDomain, domainIcons, patterns);
                }
                else if (root.Kind == CypherValueKind.Object)
                {
                    ParseDomainMappings(root, projectToDomain, domainIcons, patterns);
                }

                if (root.TryGetProperty("overrides", out var ovrEl) && ovrEl.Kind == CypherValueKind.Object)
                {
                    ParseOverrides(ovrEl, serviceOverrides, projectToDomain);
                }
            }
            catch
            {
                // Ignore malformed domains files
            }
        }

        var svcResolver = new ServiceIdentityResolver(customPrefixes, svcAliases);
        var messaging = new MessagingConventionService(topics, placeholderNames);
        var domainTaxonomy = new DomainTaxonomyEngine(projectToDomain, serviceOverrides, domainIcons, patterns, svcResolver);
        var networkValidator = new NetworkEntityValidator(garbageHosts);

        return new WorkspaceConventionProfile(
            svcResolver,
            messaging,
            domainTaxonomy,
            networkValidator,
            dbAliases,
            routeFunctions
        );
    }

    private static void ParseOverrides(
        CypherValue overridesEl,
        Dictionary<string, string> serviceOverrides,
        Dictionary<string, string> projectToDomain)
    {
        foreach (var prop in overridesEl.AsObject())
        {
            var serviceName = prop.Key.Trim();
            if (prop.Value.Kind == CypherValueKind.String)
            {
                var targetDomain = prop.Value.AsNullableString();
                if (!string.IsNullOrWhiteSpace(targetDomain))
                {
                    serviceOverrides[serviceName] = targetDomain.Trim();
                    projectToDomain[serviceName] = targetDomain.Trim();
                }
            }
            else if (prop.Value.Kind == CypherValueKind.Object)
            {
                if (prop.Value.TryGetProperty("domain", out var domProp) && domProp.Kind == CypherValueKind.String)
                {
                    var targetDomain = domProp.AsNullableString();
                    if (!string.IsNullOrWhiteSpace(targetDomain))
                    {
                        serviceOverrides[serviceName] = targetDomain.Trim();
                        projectToDomain[serviceName] = targetDomain.Trim();
                    }
                }
            }
        }
    }

    private static void ParseDomainDefinitionsArray(
        CypherValue root,
        Dictionary<string, string> projectToDomain,
        Dictionary<string, string> domainIcons,
        List<(Regex Regex, string Domain)> patterns)
    {
        foreach (var item in root.AsArray())
        {
            if (item.Kind != CypherValueKind.Object) continue;

            if (item.TryGetProperty("name", out var nameProp))
            {
                var domainName = nameProp.AsNullableString();
                if (string.IsNullOrWhiteSpace(domainName)) continue;

                if (item.TryGetProperty("icon", out var iconProp) && iconProp.Kind == CypherValueKind.String)
                {
                    var icon = iconProp.AsNullableString();
                    if (!string.IsNullOrWhiteSpace(icon)) domainIcons[domainName.Trim()] = icon.Trim();
                }

                if (item.TryGetProperty("projects", out var projsProp) && projsProp.Kind == CypherValueKind.Array)
                {
                    foreach (var p in projsProp.AsArray())
                    {
                        var proj = p.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(proj)) projectToDomain[proj.Trim()] = domainName.Trim();
                    }
                }

                if (item.TryGetProperty("services", out var srvsProp) && srvsProp.Kind == CypherValueKind.Array)
                {
                    foreach (var s in srvsProp.AsArray())
                    {
                        var srv = s.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(srv)) projectToDomain[srv.Trim()] = domainName.Trim();
                    }
                }

                if (item.TryGetProperty("patterns", out var patsProp) && patsProp.Kind == CypherValueKind.Array)
                {
                    foreach (var pat in patsProp.AsArray())
                    {
                        var patternStr = pat.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(patternStr))
                            AddPattern(patternStr, domainName.Trim(), patterns);
                    }
                }
            }
        }
    }

    private static void ParseDomainMappings(
        CypherValue root,
        Dictionary<string, string> projectToDomain,
        Dictionary<string, string> domainIcons,
        List<(Regex Regex, string Domain)> patterns)
    {
        foreach (var prop in root.AsObject())
        {
            var domainName = prop.Key;
            if (prop.Value.Kind == CypherValueKind.Array)
            {
                foreach (var item in prop.Value.AsArray())
                {
                    var proj = item.AsNullableString();
                    if (!string.IsNullOrWhiteSpace(proj)) projectToDomain[proj.Trim()] = domainName;
                }
            }
            else if (prop.Value.Kind == CypherValueKind.String)
            {
                var val = prop.Value.AsNullableString();
                if (!string.IsNullOrWhiteSpace(val)) projectToDomain[domainName] = val.Trim();
            }
            else if (prop.Value.Kind == CypherValueKind.Object)
            {
                if (prop.Key.Equals("projects", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var projProp in prop.Value.AsObject())
                    {
                        var assignedDomain = projProp.Value.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(assignedDomain))
                            projectToDomain[projProp.Key.Trim()] = assignedDomain.Trim();
                    }
                }
                else
                {
                    if (prop.Value.TryGetProperty("patterns", out var pats) && pats.Kind == CypherValueKind.Array)
                    {
                        foreach (var pat in pats.AsArray())
                        {
                            var patStr = pat.AsNullableString();
                            if (!string.IsNullOrWhiteSpace(patStr))
                                AddPattern(patStr, domainName.Trim(), patterns);
                        }
                    }
                    if (prop.Value.TryGetProperty("services", out var srvs) && srvs.Kind == CypherValueKind.Array)
                    {
                        foreach (var srv in srvs.AsArray())
                        {
                            var srvStr = srv.AsNullableString();
                            if (!string.IsNullOrWhiteSpace(srvStr))
                                projectToDomain[srvStr.Trim()] = domainName.Trim();
                        }
                    }
                    if (prop.Value.TryGetProperty("icon", out var iconProp) && iconProp.Kind == CypherValueKind.String)
                    {
                        var iconStr = iconProp.AsNullableString();
                        if (!string.IsNullOrWhiteSpace(iconStr))
                            domainIcons[domainName.Trim()] = iconStr.Trim();
                    }
                }
            }
        }
    }

    private static void AddPattern(string globPattern, string domainName, List<(Regex Regex, string Domain)> patterns)
    {
        if (string.IsNullOrWhiteSpace(globPattern) || string.IsNullOrWhiteSpace(domainName)) return;
        try
        {
            var pattern = "^" + Regex.Escape(globPattern.Trim().Replace('\\', '/'))
                .Replace(@"\*\*", ".*")
                .Replace(@"\*", @"[^/]*")
                .Replace(@"\?", ".") + "$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            patterns.Add((regex, domainName.Trim()));
        }
        catch
        {
            // Ignore invalid glob regexes
        }
    }
}
