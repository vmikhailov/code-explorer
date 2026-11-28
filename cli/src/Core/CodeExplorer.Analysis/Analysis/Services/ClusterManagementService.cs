using System.Text.Json;

namespace CodeExplorer.Core.Analysis;

public static class ClusterManagementService
{
    private const string CodeExplorerFolderName = ".codeexplorer";
    private const string ClustersConfigFileName = "clusters.json";
    private const string LegacyDomainsConfigFileName = "domains.json";
    private const string PropClusters = "clusters";
    private const string PropLegacyDomains = "domains";
    private const string PropName = "name";
    private const string PropOverrides = "overrides";
    private const string PropDisplayName = "displayName";
    private const string PropDescription = "description";
    private const string PropIcon = "icon";
    private const string PropColor = "color";
    private const string PropMembers = "members";
    private const string PropLegacyServices = "services";
    private const string PropPatterns = "patterns";
    private const string PropCluster = "cluster";
    private const string PropLegacyDomain = "domain";
    private const string PropContext = "context";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetConfigFilePath(string workspaceRoot) =>
        Path.Combine(workspaceRoot, CodeExplorerFolderName, ClustersConfigFileName);

    private static string GetLegacyConfigFilePath(string workspaceRoot) =>
        Path.Combine(workspaceRoot, CodeExplorerFolderName, LegacyDomainsConfigFileName);

    public static WorkspaceClustersConfigDto LoadConfig(string? workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            return new WorkspaceClustersConfigDto();
        }

        var primaryPath = GetConfigFilePath(workspaceRoot);
        var legacyPath = GetLegacyConfigFilePath(workspaceRoot);
        var targetPath = File.Exists(primaryPath) ? primaryPath : (File.Exists(legacyPath) ? legacyPath : null);
        if (targetPath == null)
        {
            return new WorkspaceClustersConfigDto();
        }

        try
        {
            var json = File.ReadAllText(targetPath);
            var root = CypherValue.TryParseJsonText(json);
            var result = new WorkspaceClustersConfigDto();

            if (root.TryGetProperty(PropClusters, out var clustersEl) ||
                root.TryGetProperty(PropLegacyDomains, out clustersEl))
            {
                ParseClusterDefinitions(clustersEl, result.Clusters);
            }

            if (root.TryGetProperty(PropOverrides, out var overridesEl) && overridesEl.Kind == CypherValueKind.Object)
            {
                ParseClusterOverrides(overridesEl, result.Overrides);
            }

            return result;
        }
        catch (Exception)
        {
            // Ignore malformed user config files and return empty defaults
            return new WorkspaceClustersConfigDto();
        }
    }

    public static void SaveConfig(string workspaceRoot, WorkspaceClustersConfigDto config)
    {
        var configPath = GetConfigFilePath(workspaceRoot);
        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(config, JsonOpts);
        File.WriteAllText(configPath, json);

        WorkspaceConventions.LoadFromWorkspace(workspaceRoot);
        ArchitectureViewEngine.InvalidateCache();
    }

    public static ClusterDefinitionDto AddOrUpdateCluster(string workspaceRoot, ClusterDefinitionDto cluster)
    {
        var config = LoadConfig(workspaceRoot);
        ClusterDefinitionDto? existing = null;
        foreach (var item in config.Clusters)
        {
            if (string.Equals(item.Name, cluster.Name, StringComparison.OrdinalIgnoreCase))
            {
                existing = item;
                break;
            }
        }

        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(cluster.DisplayName)) existing.DisplayName = cluster.DisplayName;
            if (!string.IsNullOrWhiteSpace(cluster.Description)) existing.Description = cluster.Description;
            if (!string.IsNullOrWhiteSpace(cluster.Icon)) existing.Icon = cluster.Icon;
            if (!string.IsNullOrWhiteSpace(cluster.Color)) existing.Color = cluster.Color;
            if (cluster.Members != null) existing.Members = cluster.Members;
            if (cluster.Patterns != null) existing.Patterns = cluster.Patterns;
        }
        else
        {
            config.Clusters.Add(cluster);
        }

        SaveConfig(workspaceRoot, config);
        return existing ?? cluster;
    }

    public static bool RemoveCluster(string workspaceRoot, string clusterName, string? reassignTo = null)
    {
        var config = LoadConfig(workspaceRoot);
        var idx = config.Clusters.FindIndex(c => string.Equals(c.Name, clusterName, StringComparison.OrdinalIgnoreCase));
        var matchingOverrideKeys = new List<string>();
        foreach (var (key, ovr) in config.Overrides)
        {
            if (string.Equals(ovr.Cluster, clusterName, StringComparison.OrdinalIgnoreCase))
            {
                matchingOverrideKeys.Add(key);
            }
        }

        if (idx == -1 && matchingOverrideKeys.Count == 0)
        {
            return false;
        }

        if (idx != -1)
        {
            config.Clusters.RemoveAt(idx);
        }

        foreach (var key in matchingOverrideKeys)
        {
            if (!string.IsNullOrWhiteSpace(reassignTo))
            {
                config.Overrides[key].Cluster = reassignTo.Trim();
            }
            else
            {
                config.Overrides.Remove(key);
            }
        }

        SaveConfig(workspaceRoot, config);
        return true;
    }

    public static void AssignMemberCluster(
        string workspaceRoot,
        string memberName,
        string clusterName,
        string? contextName = null)
    {
        var config = LoadConfig(workspaceRoot);
        config.Overrides[memberName.Trim()] = new ClusterMemberOverrideDto
        {
            Cluster = clusterName.Trim(),
            Context = string.IsNullOrWhiteSpace(contextName) ? null : contextName.Trim()
        };
        SaveConfig(workspaceRoot, config);
    }

    public static bool RemoveMemberOverride(string workspaceRoot, string memberName)
    {
        var config = LoadConfig(workspaceRoot);
        if (config.Overrides.Remove(memberName.Trim()))
        {
            SaveConfig(workspaceRoot, config);
            return true;
        }
        return false;
    }

    private static void ParseClusterDefinitions(CypherValue clustersEl, List<ClusterDefinitionDto> target)
    {
        if (clustersEl.Kind == CypherValueKind.Array)
        {
            foreach (var el in clustersEl.AsArray())
            {
                if (el.Kind != CypherValueKind.Object) continue;
                var name = el.GetStringProp(PropName);
                if (string.IsNullOrWhiteSpace(name)) continue;
                target.Add(ParseClusterObject(name, el));
            }
        }
        else if (clustersEl.Kind == CypherValueKind.Object)
        {
            foreach (var prop in clustersEl.AsObject())
            {
                if (prop.Value.Kind == CypherValueKind.Array)
                {
                    target.Add(new ClusterDefinitionDto
                    {
                        Name = prop.Key,
                        Members = prop.Value.AsStringList().ToList()
                    });
                }
                else if (prop.Value.Kind == CypherValueKind.Object)
                {
                    target.Add(ParseClusterObject(prop.Key, prop.Value));
                }
            }
        }
    }

    private static ClusterDefinitionDto ParseClusterObject(string name, CypherValue obj)
    {
        var def = new ClusterDefinitionDto { Name = name };
        if (obj.TryGetProperty(PropDisplayName, out var dn)) def.DisplayName = dn.AsNullableString();
        if (obj.TryGetProperty(PropDescription, out var desc)) def.Description = desc.AsNullableString();
        if (obj.TryGetProperty(PropIcon, out var ic)) def.Icon = ic.AsNullableString();
        if (obj.TryGetProperty(PropColor, out var clr)) def.Color = clr.AsNullableString();
        if (obj.TryGetProperty(PropMembers, out var members) && members.Kind == CypherValueKind.Array)
        {
            def.Members = members.AsStringList().ToList();
        }
        else if (obj.TryGetProperty(PropLegacyServices, out var srvs) && srvs.Kind == CypherValueKind.Array)
        {
            def.Members = srvs.AsStringList().ToList();
        }
        if (obj.TryGetProperty(PropPatterns, out var pats) && pats.Kind == CypherValueKind.Array)
        {
            def.Patterns = pats.AsStringList().ToList();
        }
        return def;
    }

    private static void ParseClusterOverrides(CypherValue overridesEl, Dictionary<string, ClusterMemberOverrideDto> target)
    {
        foreach (var prop in overridesEl.AsObject())
        {
            if (prop.Value.Kind == CypherValueKind.String)
            {
                target[prop.Key] = new ClusterMemberOverrideDto { Cluster = prop.Value.AsString() };
            }
            else if (prop.Value.Kind == CypherValueKind.Object)
            {
                var clusterName = prop.Value.TryGetProperty(PropCluster, out var cVal)
                    ? cVal.AsString()
                    : (prop.Value.TryGetProperty(PropLegacyDomain, out var dVal) ? dVal.AsString() : string.Empty);
                if (!string.IsNullOrWhiteSpace(clusterName))
                {
                    target[prop.Key] = new ClusterMemberOverrideDto
                    {
                        Cluster = clusterName,
                        Context = prop.Value.TryGetProperty(PropContext, out var ctxVal) ? ctxVal.AsNullableString() : null
                    };
                }
            }
        }
    }
}
