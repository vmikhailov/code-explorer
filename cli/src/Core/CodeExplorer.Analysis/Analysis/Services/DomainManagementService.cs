using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeExplorer.Core.Analysis;

public class DomainDefinitionDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("icon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; set; }

    [JsonPropertyName("color")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Color { get; set; }

    [JsonPropertyName("services")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Services { get; set; }

    [JsonPropertyName("patterns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Patterns { get; set; }
}

public class ServiceOverrideDto
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Context { get; set; }
}

public class WorkspaceDomainsConfigDto
{
    [JsonPropertyName("domains")]
    public List<DomainDefinitionDto> Domains { get; set; } = [];

    [JsonPropertyName("overrides")]
    public Dictionary<string, ServiceOverrideDto> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public static class DomainManagementService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetConfigFilePath(string workspaceRoot)
    {
        return Path.Combine(workspaceRoot, ".codeexplorer", "domains.json");
    }

    public static WorkspaceDomainsConfigDto LoadConfig(string workspaceRoot)
    {
        var configPath = GetConfigFilePath(workspaceRoot);
        if (!File.Exists(configPath))
        {
            return new WorkspaceDomainsConfigDto();
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var root = CypherValue.TryParseJsonText(json);
            var result = new WorkspaceDomainsConfigDto();

            if (root.TryGetProperty("domains", out var domainsEl))
            {
                if (domainsEl.Kind == CypherValueKind.Array)
                {
                    result.Domains = JsonSerializer.Deserialize<List<DomainDefinitionDto>>(domainsEl.ToJsonString(), JsonOpts) ??
                                     [];
                }
                else if (domainsEl.Kind == CypherValueKind.Object)
                {
                    foreach (var prop in domainsEl.AsObject())
                    {
                        var def = new DomainDefinitionDto { Name = prop.Key };
                        if (prop.Value.Kind == CypherValueKind.Array)
                        {
                            def.Services = prop.Value.AsStringList().ToList();
                        }
                        else if (prop.Value.Kind == CypherValueKind.Object)
                        {
                            if (prop.Value.TryGetProperty("displayName", out var dn)) def.DisplayName = dn.AsNullableString();
                            if (prop.Value.TryGetProperty("description", out var desc)) def.Description = desc.AsNullableString();
                            if (prop.Value.TryGetProperty("icon", out var ic)) def.Icon = ic.AsNullableString();
                            if (prop.Value.TryGetProperty("color", out var clr)) def.Color = clr.AsNullableString();
                            if (prop.Value.TryGetProperty("services", out var srvs) && srvs.Kind == CypherValueKind.Array)
                                def.Services = srvs.AsStringList().ToList();
                            if (prop.Value.TryGetProperty("patterns", out var pats) && pats.Kind == CypherValueKind.Array)
                                def.Patterns = pats.AsStringList().ToList();
                        }
                        result.Domains.Add(def);
                    }
                }
            }

            if (root.TryGetProperty("overrides", out var overridesEl) && overridesEl.Kind == CypherValueKind.Object)
            {
                foreach (var prop in overridesEl.AsObject())
                {
                    if (prop.Value.Kind == CypherValueKind.String)
                    {
                        result.Overrides[prop.Key] = new ServiceOverrideDto { Domain = prop.Value.AsString() };
                    }
                    else if (prop.Value.Kind == CypherValueKind.Object)
                    {
                        var ovr = JsonSerializer.Deserialize<ServiceOverrideDto>(prop.Value.ToJsonString(), JsonOpts);
                        if (ovr != null)
                        {
                            result.Overrides[prop.Key] = ovr;
                        }
                    }
                }
            }

            return result;
        }
        catch
        {
            return new WorkspaceDomainsConfigDto();
        }
    }

    public static void SaveConfig(string workspaceRoot, WorkspaceDomainsConfigDto config)
    {
        var configPath = GetConfigFilePath(workspaceRoot);
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(config, JsonOpts);
        File.WriteAllText(configPath, json);

        // Immediately update in-memory conventions
        WorkspaceConventions.LoadFromWorkspace(workspaceRoot);
        ArchitectureViewEngine.InvalidateCache();
    }

    public static DomainDefinitionDto AddOrUpdateDomain(string workspaceRoot, DomainDefinitionDto domain)
    {
        var config = LoadConfig(workspaceRoot);
        var existing = config.Domains.FirstOrDefault(d => string.Equals(d.Name, domain.Name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(domain.DisplayName)) existing.DisplayName = domain.DisplayName;
            if (!string.IsNullOrWhiteSpace(domain.Description)) existing.Description = domain.Description;
            if (!string.IsNullOrWhiteSpace(domain.Icon)) existing.Icon = domain.Icon;
            if (!string.IsNullOrWhiteSpace(domain.Color)) existing.Color = domain.Color;
            if (domain.Services != null) existing.Services = domain.Services;
            if (domain.Patterns != null) existing.Patterns = domain.Patterns;
        }
        else
        {
            config.Domains.Add(domain);
        }

        SaveConfig(workspaceRoot, config);
        return existing ?? domain;
    }

    public static bool RemoveDomain(string workspaceRoot, string domainName, string? reassignTo = null)
    {
        var config = LoadConfig(workspaceRoot);
        var idx = config.Domains.FindIndex(d => string.Equals(d.Name, domainName, StringComparison.OrdinalIgnoreCase));
        if (idx == -1 && !config.Overrides.Values.Any(o => string.Equals(o.Domain, domainName, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (idx != -1)
        {
            config.Domains.RemoveAt(idx);
        }

        // Handle overrides
        var keysToUpdate = config.Overrides
            .Where(kv => string.Equals(kv.Value.Domain, domainName, StringComparison.OrdinalIgnoreCase))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in keysToUpdate)
        {
            if (!string.IsNullOrWhiteSpace(reassignTo))
            {
                config.Overrides[key].Domain = reassignTo.Trim();
            }
            else
            {
                config.Overrides.Remove(key);
            }
        }

        SaveConfig(workspaceRoot, config);
        return true;
    }

    public static void AssignServiceDomain(
        string workspaceRoot,
        string serviceName,
        string domainName,
        string? contextName = null,
        IGraphClient? db = null,
        CancellationToken ct = default)
    {
        var config = LoadConfig(workspaceRoot);
        config.Overrides[serviceName.Trim()] = new ServiceOverrideDto
        {
            Domain = domainName.Trim(),
            Context = string.IsNullOrWhiteSpace(contextName) ? null : contextName.Trim()
        };

        SaveConfig(workspaceRoot, config);

        if (db != null)
        {
            var cleanDom = WorkspaceConventions.ToPascalCase(domainName.Trim());
            var domainId = $"domain:{cleanDom.ToLowerInvariant()}";
            var displayName = WorkspaceConventions.FormatDomainDisplayName(cleanDom);
            var query = """
                UPDATE nodes SET properties = json_set(
                    COALESCE(properties, '{}'),
                    '$.domain', @domain,
                    '$.domainId', @domainId,
                    '$.domainDisplayName', @domainDisplayName,
                    '$.bounded_context', @boundedContext
                ) WHERE (name = @serviceName OR id = @serviceName OR json_extract(properties, '$.clean_name') = @serviceName);
                """;

            try
            {
                db.ExecuteWriteAsync(query, new
                {
                    domain = cleanDom,
                    domainId,
                    domainDisplayName = displayName,
                    boundedContext = contextName ?? cleanDom,
                    serviceName = serviceName.Trim()
                }, ct).GetAwaiter().GetResult();
            }
            catch
            {
                // Fall back gracefully if direct write fails
            }
        }
    }

    public static bool RemoveServiceOverride(string workspaceRoot, string serviceName)
    {
        var config = LoadConfig(workspaceRoot);
        if (config.Overrides.Remove(serviceName.Trim()))
        {
            SaveConfig(workspaceRoot, config);
            return true;
        }
        return false;
    }
}
