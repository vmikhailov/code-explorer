using System.Text.Json;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Conventions.Models;
using CodeExplorer.Core.Conventions.Services;

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
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("topics", out var tEl) && tEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in tEl.EnumerateObject())
                    {
                        topics[prop.Name] = prop.Value.GetString() ?? prop.Name;
                    }
                }

                if (root.TryGetProperty("service_prefixes", out var spEl) && spEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in spEl.EnumerateArray())
                    {
                        var s = p.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) customPrefixes.Add(s.Trim().ToLowerInvariant());
                    }
                }

                if (root.TryGetProperty("route_functions", out var rfEl) && rfEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in rfEl.EnumerateArray())
                    {
                        var fn = r.GetString();
                        if (!string.IsNullOrWhiteSpace(fn)) routeFunctions.Add(fn.Trim());
                    }
                }

                if (root.TryGetProperty("database_aliases", out var daEl) && daEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in daEl.EnumerateObject())
                    {
                        var tgt = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(tgt)) dbAliases[prop.Name.Trim()] = tgt.Trim();
                    }
                }

                if (root.TryGetProperty("service_aliases", out var saEl) && saEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in saEl.EnumerateObject())
                    {
                        var tgt = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(tgt)) svcAliases[prop.Name.Trim()] = tgt.Trim();
                    }
                }

                if (root.TryGetProperty("garbage_hosts", out var ghEl) && ghEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in ghEl.EnumerateArray())
                    {
                        var host = h.GetString();
                        if (!string.IsNullOrWhiteSpace(host)) garbageHosts.Add(host.Trim().ToLowerInvariant());
                    }
                }

                if (root.TryGetProperty("placeholder_names", out var pnEl) && pnEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in pnEl.EnumerateArray())
                    {
                        var name = p.GetString();
                        if (!string.IsNullOrWhiteSpace(name)) placeholderNames.Add(name.Trim());
                    }
                }

                if (root.TryGetProperty("domains", out var dEl) && dEl.ValueKind == JsonValueKind.Object)
                {
                    ParseDomainMappings(dEl, projectToDomain, domainIcons, patterns);
                }

                if (root.TryGetProperty("overrides", out var ovrEl) && ovrEl.ValueKind == JsonValueKind.Object)
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
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("domains", out var domainsEl))
                {
                    if (domainsEl.ValueKind == JsonValueKind.Array)
                        ParseDomainDefinitionsArray(domainsEl, projectToDomain, domainIcons, patterns);
                    else if (domainsEl.ValueKind == JsonValueKind.Object)
                        ParseDomainMappings(domainsEl, projectToDomain, domainIcons, patterns);
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    ParseDomainDefinitionsArray(root, projectToDomain, domainIcons, patterns);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    ParseDomainMappings(root, projectToDomain, domainIcons, patterns);
                }

                if (root.TryGetProperty("overrides", out var ovrEl) && ovrEl.ValueKind == JsonValueKind.Object)
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
        JsonElement overridesEl,
        Dictionary<string, string> serviceOverrides,
        Dictionary<string, string> projectToDomain)
    {
        foreach (var prop in overridesEl.EnumerateObject())
        {
            var serviceName = prop.Name.Trim();
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var targetDomain = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(targetDomain))
                {
                    serviceOverrides[serviceName] = targetDomain.Trim();
                    projectToDomain[serviceName] = targetDomain.Trim();
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                if (prop.Value.TryGetProperty("domain", out var domProp) && domProp.ValueKind == JsonValueKind.String)
                {
                    var targetDomain = domProp.GetString();
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
        JsonElement root,
        Dictionary<string, string> projectToDomain,
        Dictionary<string, string> domainIcons,
        List<(Regex Regex, string Domain)> patterns)
    {
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            if (item.TryGetProperty("name", out var nameProp))
            {
                var domainName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(domainName)) continue;

                if (item.TryGetProperty("icon", out var iconProp) && iconProp.ValueKind == JsonValueKind.String)
                {
                    var icon = iconProp.GetString();
                    if (!string.IsNullOrWhiteSpace(icon)) domainIcons[domainName.Trim()] = icon.Trim();
                }

                if (item.TryGetProperty("projects", out var projsProp) && projsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in projsProp.EnumerateArray())
                    {
                        var proj = p.GetString();
                        if (!string.IsNullOrWhiteSpace(proj)) projectToDomain[proj.Trim()] = domainName.Trim();
                    }
                }

                if (item.TryGetProperty("services", out var srvsProp) && srvsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in srvsProp.EnumerateArray())
                    {
                        var srv = s.GetString();
                        if (!string.IsNullOrWhiteSpace(srv)) projectToDomain[srv.Trim()] = domainName.Trim();
                    }
                }

                if (item.TryGetProperty("patterns", out var patsProp) && patsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var pat in patsProp.EnumerateArray())
                    {
                        var patternStr = pat.GetString();
                        if (!string.IsNullOrWhiteSpace(patternStr))
                            AddPattern(patternStr, domainName.Trim(), patterns);
                    }
                }
            }
        }
    }

    private static void ParseDomainMappings(
        JsonElement root,
        Dictionary<string, string> projectToDomain,
        Dictionary<string, string> domainIcons,
        List<(Regex Regex, string Domain)> patterns)
    {
        foreach (var prop in root.EnumerateObject())
        {
            var domainName = prop.Name;
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray())
                {
                    var proj = item.GetString();
                    if (!string.IsNullOrWhiteSpace(proj)) projectToDomain[proj.Trim()] = domainName;
                }
            }
            else if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var val = prop.Value.GetString();
                if (!string.IsNullOrWhiteSpace(val)) projectToDomain[domainName] = val.Trim();
            }
            else if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                if (prop.Name.Equals("projects", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var projProp in prop.Value.EnumerateObject())
                    {
                        var assignedDomain = projProp.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(assignedDomain))
                            projectToDomain[projProp.Name.Trim()] = assignedDomain.Trim();
                    }
                }
                else
                {
                    if (prop.Value.TryGetProperty("patterns", out var pats) && pats.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var pat in pats.EnumerateArray())
                        {
                            var patStr = pat.GetString();
                            if (!string.IsNullOrWhiteSpace(patStr))
                                AddPattern(patStr, domainName.Trim(), patterns);
                        }
                    }
                    if (prop.Value.TryGetProperty("services", out var srvs) && srvs.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var srv in srvs.EnumerateArray())
                        {
                            var srvStr = srv.GetString();
                            if (!string.IsNullOrWhiteSpace(srvStr))
                                projectToDomain[srvStr.Trim()] = domainName.Trim();
                        }
                    }
                    if (prop.Value.TryGetProperty("icon", out var iconProp) && iconProp.ValueKind == JsonValueKind.String)
                    {
                        var iconStr = iconProp.GetString();
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
