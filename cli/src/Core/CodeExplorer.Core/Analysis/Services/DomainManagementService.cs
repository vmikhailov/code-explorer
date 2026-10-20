using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common;
using CodeExplorer.Core.Database;

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
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var result = new WorkspaceDomainsConfigDto();

            if (root.TryGetProperty("domains", out var domainsEl))
            {
                if (domainsEl.ValueKind == JsonValueKind.Array)
                {
                    result.Domains = JsonSerializer.Deserialize<List<DomainDefinitionDto>>(domainsEl.GetRawText(), JsonOpts) ??
                                     [];
                }
                else if (domainsEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in domainsEl.EnumerateObject())
                    {
                        var def = new DomainDefinitionDto { Name = prop.Name };
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            def.Services = JsonSerializer.Deserialize<List<string>>(prop.Value.GetRawText(), JsonOpts);
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            if (prop.Value.TryGetProperty("displayName", out var dn)) def.DisplayName = dn.GetString();
                            if (prop.Value.TryGetProperty("description", out var desc)) def.Description = desc.GetString();
                            if (prop.Value.TryGetProperty("icon", out var ic)) def.Icon = ic.GetString();
                            if (prop.Value.TryGetProperty("color", out var clr)) def.Color = clr.GetString();
                            if (prop.Value.TryGetProperty("services", out var srvs) && srvs.ValueKind == JsonValueKind.Array)
                                def.Services = JsonSerializer.Deserialize<List<string>>(srvs.GetRawText(), JsonOpts);
                            if (prop.Value.TryGetProperty("patterns", out var pats) && pats.ValueKind == JsonValueKind.Array)
                                def.Patterns = JsonSerializer.Deserialize<List<string>>(pats.GetRawText(), JsonOpts);
                        }
                        result.Domains.Add(def);
                    }
                }
            }

            if (root.TryGetProperty("overrides", out var overridesEl) && overridesEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in overridesEl.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        result.Overrides[prop.Name] = new ServiceOverrideDto { Domain = prop.Value.GetString() ?? "" };
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        var ovr = JsonSerializer.Deserialize<ServiceOverrideDto>(prop.Value.GetRawText(), JsonOpts);
                        if (ovr != null)
                        {
                            result.Overrides[prop.Name] = ovr;
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
