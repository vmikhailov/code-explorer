using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace CodeExplorer.Core.Database;

public partial class SqliteGraphClient
{
    private const string MetadataKeyTaxonomyJson = "taxonomy_json";
    private const string PropId = "id";
    private const string PropPath = "path";
    private const string PropExternalApis = "external_apis";
    private const string KindWorkspace = "Workspace";
    private const string KindProject = "Project";
    private const string RelTransitivelyCalls = "TRANSITIVELY_CALLS";
    private const string RelAttributedTo = "ATTRIBUTED_TO";
    private const string JsonPropTaxonomy = "taxonomy";
    private const string JsonPropLabel = "label";
    private const string JsonPropProperties = "properties";
    private const string JsonPropOutgoing = "outgoing";
    private const string JsonPropRelationship = "relationship";
    private const string JsonPropTarget = "target";

    private const string SelectTaxonomyMetadataSql =
        "SELECT value FROM metadata WHERE key = 'taxonomy_json' LIMIT 1;";

    private const string UpsertTaxonomyMetadataSql =
        "INSERT INTO metadata (key, value) VALUES ('taxonomy_json', @json) ON CONFLICT(key) DO UPDATE SET value = excluded.value;";

    private const string DeleteTaxonomyMetadataSql =
        "DELETE FROM metadata WHERE key = 'taxonomy_json';";

    private const string SelectAllNodeIdKindsSql =
        "SELECT id, kind FROM nodes;";

    private const string SelectSamplePropertiesByKindSql =
        "SELECT DISTINCT n.kind, j.key FROM (SELECT kind, properties FROM nodes GROUP BY kind) n, json_each(n.properties) j;";

    private const string SelectAllEdgeEndpointsSql =
        "SELECT from_id, kind, to_id FROM edges;";

    private static readonly JsonSerializerOptions CompactTaxonomyJsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly Dictionary<string, string> _nodeKindById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<string>> _taxonomyPropsByKind = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string Kind, Type NodeType)> _registeredOntologyNodeTypes = [];
    private readonly HashSet<(string FromKind, string RelKind, string ToKind)> _taxonomyTriplets = [];
    private readonly List<(string FromId, string RelKind, string ToId)> _pendingTaxonomyEdges = [];

    private string? _cachedTaxonomyJson;
    private bool _taxonomyDirty;
    private bool _taxonomyNeedsRebuild;
    private bool _allDbNodesLoadedInTaxonomy;

    private void RecordNodesInTaxonomy(IReadOnlyList<Node> nodes)
    {
        foreach (var node in nodes)
        {
            _nodeKindById[node.Id] = node.Kind;
            var propSet = GetOrCreateTaxonomyPropSet(node.Kind);
            RecordSingleNodeProperties(node, propSet);
        }

        _taxonomyDirty = true;
        _cachedTaxonomyJson = null;
    }

    private void RecordSingleNodeProperties(Node node, HashSet<string> propSet)
    {
        if (node.OntologyNode != null && !node.HasMaterializedProperties)
        {
            var ont = node.OntologyNode;
            if (_registeredOntologyNodeTypes.Add((node.Kind, ont.GetType())))
            {
                foreach (var key in NodeFactory.GetSchemaPropertyKeys(ont.GetType()))
                {
                    propSet.Add(key);
                }
            }

            if (ont.CustomProperties is { Count: > 0 } customProps)
            {
                foreach (var key in customProps.Keys)
                {
                    propSet.Add(key);
                }
            }

            return;
        }

        propSet.Add(PropId);
        foreach (var key in node.Properties.Keys)
        {
            propSet.Add(key);
        }
    }

    private void RecordWorkspaceNodeInTaxonomy(string id)
    {
        _nodeKindById[id] = KindWorkspace;
        var propSet = GetOrCreateTaxonomyPropSet(KindWorkspace);
        propSet.Add(PropId);
        propSet.Add(PropPath);
        _taxonomyDirty = true;
        _cachedTaxonomyJson = null;
    }

    private void RecordRelationshipsInTaxonomy(IReadOnlyList<Relationship> rels)
    {
        foreach (var rel in rels)
        {
            RecordEdgeEndpointsInTaxonomy(rel.From, rel.Kind, rel.To);
        }

        _taxonomyDirty = true;
        _cachedTaxonomyJson = null;
    }

    private void RecordPostIndexInTaxonomy(PostIndexAnalysisResult result)
    {
        foreach (var rel in result.TransitivelyCalls)
        {
            RecordEdgeEndpointsInTaxonomy(rel.From, RelTransitivelyCalls, rel.To);
        }

        foreach (var rel in result.AttributedTo)
        {
            RecordEdgeEndpointsInTaxonomy(rel.EpId, RelAttributedTo, rel.SinkId);
        }

        if (result.ProjectExternalApis.Count > 0)
        {
            GetOrCreateTaxonomyPropSet(KindProject).Add(PropExternalApis);
        }

        _taxonomyDirty = true;
        _cachedTaxonomyJson = null;
    }

    private void RecordEdgeEndpointsInTaxonomy(string fromId, string relKind, string toId)
    {
        if (_nodeKindById.TryGetValue(fromId, out var fromKind) &&
            _nodeKindById.TryGetValue(toId, out var toKind))
        {
            _taxonomyTriplets.Add((fromKind, relKind, toKind));
        }
        else
        {
            _pendingTaxonomyEdges.Add((fromId, relKind, toId));
        }
    }

    private HashSet<string> GetOrCreateTaxonomyPropSet(string kind)
    {
        if (!_taxonomyPropsByKind.TryGetValue(kind, out var propSet))
        {
            propSet = new HashSet<string>(StringComparer.Ordinal);
            _taxonomyPropsByKind[kind] = propSet;
        }

        return propSet;
    }

    private void ResetTaxonomyInMemory()
    {
        _nodeKindById.Clear();
        _taxonomyPropsByKind.Clear();
        _registeredOntologyNodeTypes.Clear();
        _taxonomyTriplets.Clear();
        _pendingTaxonomyEdges.Clear();
        _cachedTaxonomyJson = null;
        _taxonomyDirty = false;
        _taxonomyNeedsRebuild = false;
        _allDbNodesLoadedInTaxonomy = true;
    }

    private async Task InvalidateTaxonomySnapshotUnlockedAsync(SqliteTransaction? tx = null)
    {
        _cachedTaxonomyJson = null;
        _taxonomyDirty = false;
        _taxonomyNeedsRebuild = true;
        _allDbNodesLoadedInTaxonomy = false;
        _pendingTaxonomyEdges.Clear();
        await _conn.ExecuteAsync(DeleteTaxonomyMetadataSql, transaction: tx);
    }

    public async Task<string?> GetTaxonomyJsonAsync(CancellationToken cancellationToken = default)
    {
        if (!_isBulkLoading && _cachedTaxonomyJson != null && !_taxonomyDirty && !_taxonomyNeedsRebuild)
        {
            return _cachedTaxonomyJson;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isBulkLoading)
            {
                await CommitBulkLoadUnlockedAsync(cancellationToken);
            }

            if (_cachedTaxonomyJson != null && !_taxonomyDirty && !_taxonomyNeedsRebuild)
            {
                return _cachedTaxonomyJson;
            }

            if (_taxonomyDirty && !_taxonomyNeedsRebuild)
            {
                await FlushOrPersistTaxonomyUnlockedAsync(cancellationToken);
                return _cachedTaxonomyJson;
            }

            if (!_taxonomyNeedsRebuild)
            {
                var persisted = await _conn.QueryFirstOrDefaultAsync<string>(
                    new CommandDefinition(SelectTaxonomyMetadataSql, cancellationToken: cancellationToken));
                if (!string.IsNullOrEmpty(persisted))
                {
                    _cachedTaxonomyJson = persisted;
                    return persisted;
                }
            }

            return await RebuildTaxonomyFromDatabaseUnlockedAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task FlushOrPersistTaxonomyUnlockedAsync(CancellationToken cancellationToken = default)
    {
        if (!_taxonomyDirty && !_taxonomyNeedsRebuild) return;

        try
        {
            if (_taxonomyNeedsRebuild)
            {
                await RebuildTaxonomyFromDatabaseUnlockedAsync(cancellationToken);
                return;
            }

            var existingJson = await _conn.QueryFirstOrDefaultAsync<string>(
                new CommandDefinition(SelectTaxonomyMetadataSql, cancellationToken: cancellationToken));
            if (!string.IsNullOrEmpty(existingJson))
            {
                MergePersistedTaxonomyJson(existingJson);
            }

            if (_pendingTaxonomyEdges.Count > 0)
            {
                await ResolvePendingTaxonomyEdgesUnlockedAsync(cancellationToken);
            }

            var json = SerializeCurrentTaxonomy();
            await _conn.ExecuteAsync(
                new CommandDefinition(UpsertTaxonomyMetadataSql, new { json }, cancellationToken: cancellationToken));

            _cachedTaxonomyJson = json;
            _taxonomyDirty = false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to flush in-memory taxonomy snapshot to metadata.");
        }
    }

    private async Task ResolvePendingTaxonomyEdgesUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!_allDbNodesLoadedInTaxonomy)
        {
            var stillMissing = false;
            foreach (var (fromId, _, toId) in _pendingTaxonomyEdges)
            {
                if (!_nodeKindById.ContainsKey(fromId) || !_nodeKindById.ContainsKey(toId))
                {
                    stillMissing = true;
                    break;
                }
            }

            if (stillMissing)
            {
                await LoadAllNodeIdKindsUnlockedAsync(cancellationToken);
            }
        }

        foreach (var (fromId, relKind, toId) in _pendingTaxonomyEdges)
        {
            if (_nodeKindById.TryGetValue(fromId, out var fromKind) &&
                _nodeKindById.TryGetValue(toId, out var toKind))
            {
                _taxonomyTriplets.Add((fromKind, relKind, toKind));
            }
        }

        _pendingTaxonomyEdges.Clear();
    }

    private async Task LoadAllNodeIdKindsUnlockedAsync(CancellationToken cancellationToken)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandTimeout = CommandTimeoutSeconds;
        cmd.CommandText = SelectAllNodeIdKindsSql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(0);
            var kind = reader.GetString(1);
            _nodeKindById[id] = kind;
        }

        _allDbNodesLoadedInTaxonomy = true;
    }

    private async Task<string> RebuildTaxonomyFromDatabaseUnlockedAsync(CancellationToken cancellationToken)
    {
        _nodeKindById.Clear();
        _taxonomyPropsByKind.Clear();
        _taxonomyTriplets.Clear();
        _pendingTaxonomyEdges.Clear();

        await LoadAllNodeIdKindsUnlockedAsync(cancellationToken);
        PopulateDefaultSchemaPropertiesForPresentKinds();
        await LoadSamplePropertiesByKindUnlockedAsync(cancellationToken);
        await LoadAllEdgeTripletsUnlockedAsync(cancellationToken);

        var json = SerializeCurrentTaxonomy();
        await _conn.ExecuteAsync(
            new CommandDefinition(UpsertTaxonomyMetadataSql, new { json }, cancellationToken: cancellationToken));

        _cachedTaxonomyJson = json;
        _taxonomyDirty = false;
        _taxonomyNeedsRebuild = false;
        return json;
    }

    private void PopulateDefaultSchemaPropertiesForPresentKinds()
    {
        var defaults = NodeFactory.GetDefaultPropertiesByKind();
        foreach (var kind in _nodeKindById.Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var propSet = GetOrCreateTaxonomyPropSet(kind);
            propSet.Add(PropId);
            if (defaults.TryGetValue(kind, out var schemaKeys))
            {
                foreach (var key in schemaKeys)
                {
                    propSet.Add(key);
                }
            }
        }
    }

    private async Task LoadSamplePropertiesByKindUnlockedAsync(CancellationToken cancellationToken)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandTimeout = CommandTimeoutSeconds;
        cmd.CommandText = SelectSamplePropertiesByKindSql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var kind = reader.GetString(0);
            var key = reader.GetString(1);
            GetOrCreateTaxonomyPropSet(kind).Add(key);
        }
    }

    private async Task LoadAllEdgeTripletsUnlockedAsync(CancellationToken cancellationToken)
    {
        await using var cmd = _conn.CreateCommand();
        cmd.CommandTimeout = CommandTimeoutSeconds;
        cmd.CommandText = SelectAllEdgeEndpointsSql;

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var fromId = reader.GetString(0);
            var relKind = reader.GetString(1);
            var toId = reader.GetString(2);

            if (_nodeKindById.TryGetValue(fromId, out var fromKind) &&
                _nodeKindById.TryGetValue(toId, out var toKind))
            {
                _taxonomyTriplets.Add((fromKind, relKind, toKind));
            }
        }
    }

    private void MergePersistedTaxonomyJson(string existingJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(existingJson);
            if (!doc.RootElement.TryGetProperty(JsonPropTaxonomy, out var taxArray) ||
                taxArray.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in taxArray.EnumerateArray())
            {
                MergeSingleTaxonomyElement(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignored malformed persisted taxonomy JSON during merge.");
        }
    }

    private void MergeSingleTaxonomyElement(JsonElement item)
    {
        if (!item.TryGetProperty(JsonPropLabel, out var labelEl)) return;
        var label = labelEl.GetString();
        if (string.IsNullOrEmpty(label)) return;

        var propSet = GetOrCreateTaxonomyPropSet(label);
        if (item.TryGetProperty(JsonPropProperties, out var propsEl) && propsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in propsEl.EnumerateArray())
            {
                var pStr = p.GetString();
                if (!string.IsNullOrEmpty(pStr)) propSet.Add(pStr);
            }
        }

        if (item.TryGetProperty(JsonPropOutgoing, out var outEl) && outEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var edge in outEl.EnumerateArray())
            {
                var rel = edge.TryGetProperty(JsonPropRelationship, out var rEl) ? rEl.GetString() : null;
                var target = edge.TryGetProperty(JsonPropTarget, out var tEl) ? tEl.GetString() : null;
                if (!string.IsNullOrEmpty(rel) && !string.IsNullOrEmpty(target))
                {
                    _taxonomyTriplets.Add((label, rel, target));
                }
            }
        }
    }

    private string SerializeCurrentTaxonomy()
    {
        var nodes = new Dictionary<string, (HashSet<string> properties, HashSet<(string relationship, string target)> outgoing, HashSet<(string relationship, string source)> incoming)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var (kind, props) in _taxonomyPropsByKind)
        {
            nodes[kind] = (new HashSet<string>(props, StringComparer.Ordinal), [], []);
        }

        foreach (var (from, rel, to) in _taxonomyTriplets)
        {
            if (!nodes.ContainsKey(from)) nodes[from] = (new HashSet<string>(StringComparer.Ordinal), [], []);
            if (!nodes.ContainsKey(to)) nodes[to] = (new HashSet<string>(StringComparer.Ordinal), [], []);

            nodes[from].outgoing.Add((rel, to));
            nodes[to].incoming.Add((rel, from));
        }

        var taxonomy = nodes.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(kvp => new
        {
            label = kvp.Key,
            properties = kvp.Value.properties.OrderBy(p => p, StringComparer.Ordinal).ToList(),
            outgoing = kvp.Value.outgoing.OrderBy(x => x.relationship, StringComparer.Ordinal).ThenBy(x => x.target, StringComparer.Ordinal).Select(x => new { x.relationship, x.target }).ToList(),
            incoming = kvp.Value.incoming.OrderBy(x => x.relationship, StringComparer.Ordinal).ThenBy(x => x.source, StringComparer.Ordinal).Select(x => new { x.relationship, x.source }).ToList()
        }).ToList();

        return JsonSerializer.Serialize(new { taxonomy }, CompactTaxonomyJsonOptions);
    }
}
