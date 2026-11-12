using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OntologyGen;

/// <summary>
/// Renders extracted ontology metadata into a canonical JSON schema (proto/ontology-schema.json)
/// shared across CodeExplorer Server, VS Code Extension, and the public website.
/// </summary>
public static class JsonSchemaRenderer
{
    private const string SchemaVersionValue = "2.0";
    private const string NodeSuffix = "Node";

    private const string Layer1Raw = "Layer 1: File Structure";
    private const string Layer2Raw = "Layer 2: Project Structure";
    private const string Layer3Raw = "Layer 3: Syntax Structure";
    private const string Layer4Raw = "Layer 4: Semantic Structure";
    private const string Layer5Raw = "Layer 5: Logical Structure";

    private const string CatControlFlow = "control_flow";
    private const string CatDataFlow = "data_flow";
    private const string CatTypeDep = "type_dependency";
    private const string CatStructural = "structural";

    private static readonly HashSet<string> ExcludedNodeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Workspace",
        "FilesStructure",
        "ProjectsStructure",
        "SyntaxStructure",
        "ProjectSyntax",
        "SemanticStructure",
        "ProjectSemantic",
        "LogicalStructure",
        "Counter",
        "DataSet",
        "ScanIssue"
    };

    private static readonly HashSet<string> ExcludedRelLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "BELONGS_TO_DOMAIN",
        "EXPOSES_DOMAIN",
        "SUBDOMAIN_OF"
    };

    private static readonly HashSet<string> ExcludedPropertySubstrings = new(StringComparer.OrdinalIgnoreCase)
    {
        "bounded_context",
        "intent_summary"
    };

    private static readonly HashSet<string> ControlFlowRels = new(StringComparer.OrdinalIgnoreCase)
    {
        "CALLS", "TRANSITIVELY_CALLS", "TRIGGERS", "HANDLES", "IMPLEMENTS",
        "HAS_ENTRYPOINT", "SERVICE_CALL", "CALLS_ENDPOINT", "CALLED_BY",
        "EXPOSES", "EXPOSES_ENDPOINT", "EXPOSED_BY", "INSTANTIATES"
    };

    private static readonly HashSet<string> DataFlowRels = new(StringComparer.OrdinalIgnoreCase)
    {
        "FLOWS_TO", "HAS_PARAMETER", "PASSES_ARGUMENT", "PASSES_LITERAL",
        "TARGETED_BY", "ATTRIBUTED_TO", "USES_DB", "QUERIES_DB", "QUERIED_BY", "READS_FROM",
        "WRITES_TO", "ACCESSED_BY", "PUBLISHES_TO", "SUBSCRIBES_TO",
        "PUBLISHED_BY", "SUBSCRIBED_BY", "PERSISTED_IN", "TRANSFORMS_TO"
    };

    private static readonly HashSet<string> StructuralRels = new(StringComparer.OrdinalIgnoreCase)
    {
        "CONTAINS", "DECLARED_IN", "BELONGS_TO", "LOCATED_IN", "DEFINES",
        "DECLARES", "DECLARES_TYPE", "HAS_METHOD", "HAS_MEMBER", "HAS_VARIABLE"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string Render(List<NodeInfo> nodes, List<RelInfo> relationships)
    {
        var classToLabel = nodes.ToDictionary(n => n.ClassName, n => n.Label, StringComparer.Ordinal);
        var relDescByLabel = relationships
            .GroupBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);

        var activeNodes = nodes
            .Where(IsPublicQueryableNode)
            .OrderByDescending(n => ResolveLayerId(n.Layer))
            .ThenBy(n => n.Order)
            .ThenBy(n => n.Label, StringComparer.Ordinal)
            .ToList();

        var activeLabelSet = activeNodes.Select(n => n.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeByClass = activeNodes.ToDictionary(n => n.ClassName, n => n, StringComparer.Ordinal);
        var activeByLabel = activeNodes.ToDictionary(n => n.Label, n => n, StringComparer.Ordinal);
        var allTriples = CollectValidEdgeTriples(activeNodes, classToLabel, activeLabelSet);

        var subtypesByLabel = BuildDirectSubtypesMap(activeNodes, activeByClass);
        var ancestorsByLabel = BuildAncestorsMap(activeNodes, activeByClass);

        var nodeDtos = activeNodes
            .Select(n => BuildNodeSchema(
                n,
                activeByClass,
                activeByLabel,
                subtypesByLabel,
                ancestorsByLabel,
                allTriples,
                relDescByLabel))
            .ToList();

        var relDtos = BuildRelationshipSchemas(relationships, allTriples);

        var root = new OntologySchemaDocument(
            SchemaVersion: SchemaVersionValue,
            Layers: BuildLayers(),
            EdgeCategories: BuildEdgeCategories(),
            CommonProperties: BuildCommonProperties(),
            Nodes: nodeDtos,
            Relationships: relDtos
        );

        return JsonSerializer.Serialize(root, SerializerOptions) + "\n";
    }

    private static bool IsPublicQueryableNode(NodeInfo node)
    {
        if (node.IsSystemNode || string.IsNullOrWhiteSpace(node.Layer))
        {
            return false;
        }
        return !ExcludedNodeLabels.Contains(node.Label);
    }

    private static List<(string FromKind, string Rel, string ToKind)> CollectValidEdgeTriples(
        List<NodeInfo> activeNodes,
        Dictionary<string, string> classToLabel,
        HashSet<string> activeLabelSet)
    {
        var seen = new HashSet<(string, string, string)>();
        var list = new List<(string FromKind, string Rel, string ToKind)>();

        foreach (var node in activeNodes)
        {
            foreach (var edge in node.OutEdges)
            {
                if (string.IsNullOrWhiteSpace(edge.Rel) || ExcludedRelLabels.Contains(edge.Rel))
                {
                    continue;
                }

                var toKind = classToLabel.TryGetValue(edge.ToTypeName, out var resolved)
                    ? resolved
                    : StripNodeSuffix(edge.ToTypeName);

                if (!activeLabelSet.Contains(toKind))
                {
                    continue;
                }

                if (seen.Add((node.Label, edge.Rel, toKind)))
                {
                    list.Add((node.Label, edge.Rel, toKind));
                }
            }
        }

        return list;
    }

    private static Dictionary<string, List<string>> BuildDirectSubtypesMap(
        List<NodeInfo> activeNodes,
        Dictionary<string, NodeInfo> activeByClass)
    {
        var map = activeNodes.ToDictionary(n => n.Label, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var node in activeNodes)
        {
            if (node.BaseClassName is not null &&
                activeByClass.TryGetValue(node.BaseClassName, out var parentNode) &&
                map.TryGetValue(parentNode.Label, out var list))
            {
                list.Add(node.Label);
            }
        }
        return map;
    }

    private static Dictionary<string, List<string>> BuildAncestorsMap(
        List<NodeInfo> activeNodes,
        Dictionary<string, NodeInfo> activeByClass)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var node in activeNodes)
        {
            var ancestors = new List<string>();
            var visited = new HashSet<string>(StringComparer.Ordinal) { node.ClassName };
            var cursor = node.BaseClassName;
            while (cursor is not null &&
                   visited.Add(cursor) &&
                   activeByClass.TryGetValue(cursor, out var anc))
            {
                ancestors.Add(anc.Label);
                cursor = anc.BaseClassName;
            }
            map[node.Label] = ancestors;
        }
        return map;
    }

    private static SchemaNodeDto BuildNodeSchema(
        NodeInfo node,
        Dictionary<string, NodeInfo> activeByClass,
        Dictionary<string, NodeInfo> activeByLabel,
        Dictionary<string, List<string>> subtypesByLabel,
        Dictionary<string, List<string>> ancestorsByLabel,
        List<(string FromKind, string Rel, string ToKind)> allTriples,
        Dictionary<string, string> relDescByLabel)
    {
        var layerId = ResolveLayerId(node.Layer);
        var props = BuildNodeProperties(node.Properties);
        var extendsLabel = node.BaseClassName is not null && activeByClass.TryGetValue(node.BaseClassName, out var parentNode)
            ? parentNode.Label
            : null;
        var ancestors = ancestorsByLabel.GetValueOrDefault(node.Label, []);
        var subtypes = subtypesByLabel.GetValueOrDefault(node.Label, []);

        var outEdges = allTriples
            .Where(t => string.Equals(t.FromKind, node.Label, StringComparison.Ordinal))
            .OrderBy(t => CategorySortOrder(ClassifyRel(t.Rel)))
            .ThenBy(t => t.Rel, StringComparer.Ordinal)
            .ThenBy(t => t.ToKind, StringComparer.Ordinal)
            .Select(t => new SchemaNodeEdgeDto(
                Rel: t.Rel,
                TargetKind: t.ToKind,
                Category: ClassifyRel(t.Rel),
                Description: relDescByLabel.GetValueOrDefault(t.Rel, string.Empty)
            ))
            .ToList();

        var inEdges = allTriples
            .Where(t => string.Equals(t.ToKind, node.Label, StringComparison.Ordinal))
            .OrderBy(t => CategorySortOrder(ClassifyRel(t.Rel)))
            .ThenBy(t => t.Rel, StringComparer.Ordinal)
            .ThenBy(t => t.FromKind, StringComparer.Ordinal)
            .Select(t => new SchemaNodeInboundEdgeDto(
                Rel: t.Rel,
                SourceKind: t.FromKind,
                Category: ClassifyRel(t.Rel),
                Description: relDescByLabel.GetValueOrDefault(t.Rel, string.Empty)
            ))
            .ToList();

        var inheritedOutEdges = new List<SchemaNodeEdgeDto>();
        var inheritedInEdges = new List<SchemaNodeInboundEdgeDto>();
        if (ancestors.Count > 0)
        {
            foreach (var ancLabel in ancestors)
            {
                if (!activeByLabel.ContainsKey(ancLabel)) continue;
                inheritedOutEdges.AddRange(allTriples
                    .Where(t => string.Equals(t.FromKind, ancLabel, StringComparison.Ordinal))
                    .Select(t => new SchemaNodeEdgeDto(
                        Rel: t.Rel,
                        TargetKind: t.ToKind,
                        Category: ClassifyRel(t.Rel),
                        Description: relDescByLabel.GetValueOrDefault(t.Rel, string.Empty),
                        InheritedFrom: ancLabel
                    )));
                inheritedInEdges.AddRange(allTriples
                    .Where(t => string.Equals(t.ToKind, ancLabel, StringComparison.Ordinal))
                    .Select(t => new SchemaNodeInboundEdgeDto(
                        Rel: t.Rel,
                        SourceKind: t.FromKind,
                        Category: ClassifyRel(t.Rel),
                        Description: relDescByLabel.GetValueOrDefault(t.Rel, string.Empty),
                        InheritedFrom: ancLabel
                    )));
            }

            inheritedOutEdges = inheritedOutEdges
                .OrderBy(e => CategorySortOrder(e.Category))
                .ThenBy(e => e.Rel, StringComparer.Ordinal)
                .ThenBy(e => e.TargetKind, StringComparer.Ordinal)
                .ToList();
            inheritedInEdges = inheritedInEdges
                .OrderBy(e => CategorySortOrder(e.Category))
                .ThenBy(e => e.Rel, StringComparer.Ordinal)
                .ThenBy(e => e.SourceKind, StringComparer.Ordinal)
                .ToList();
        }

        var sampleOut = outEdges.Count > 0 ? outEdges : inheritedOutEdges;
        var sampleIn = inEdges.Count > 0 ? inEdges : inheritedInEdges;

        return new SchemaNodeDto(
            Kind: node.Label,
            PluralLabel: string.IsNullOrWhiteSpace(node.PluralLabel) ? node.Label : node.PluralLabel,
            LayerId: layerId,
            LayerKey: ResolveLayerKey(layerId),
            Icon: node.Icon,
            Order: node.Order,
            IdScheme: node.IdScheme,
            Purpose: node.Purpose,
            IsAbstract: node.IsAbstract ? true : null,
            Extends: extendsLabel,
            Ancestors: ancestors.Count > 0 ? ancestors : null,
            Subtypes: subtypes.Count > 0 ? subtypes : null,
            Properties: props,
            OutEdges: outEdges,
            InEdges: inEdges,
            InheritedOutEdges: inheritedOutEdges.Count > 0 ? inheritedOutEdges : null,
            InheritedInEdges: inheritedInEdges.Count > 0 ? inheritedInEdges : null,
            SampleCypher: BuildSampleCypher(node.Label, props, sampleOut, sampleIn)
        );
    }

    private static List<SchemaPropertyDto> BuildNodeProperties(List<PropertyInfo> rawProps)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<SchemaPropertyDto>();

        foreach (var p in rawProps)
        {
            var cypherName = string.IsNullOrWhiteSpace(p.CypherName)
                ? OntologyExtractor.ToSnakeCase(p.Name)
                : p.CypherName;

            if (ExcludedPropertySubstrings.Any(sub => cypherName.Contains(sub, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!seen.Add(cypherName))
            {
                continue;
            }

            list.Add(new SchemaPropertyDto(
                Name: cypherName,
                SourceName: p.Name,
                Type: NormalizePropertyType(p.Type),
                Description: p.Description,
                InheritedFrom: p.InheritedFrom
            ));
        }

        return list;
    }

    private static List<SchemaRelationshipDto> BuildRelationshipSchemas(
        List<RelInfo> relationships,
        List<(string FromKind, string Rel, string ToKind)> allTriples)
    {
        var triplesByRel = allTriples
            .GroupBy(t => t.Rel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.FromKind, StringComparer.Ordinal)
                      .ThenBy(x => x.ToKind, StringComparer.Ordinal)
                      .Select(x => new SchemaEdgePairDto(x.FromKind, x.ToKind))
                      .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var activeRels = relationships
            .Where(r => !ExcludedRelLabels.Contains(r.Label) && triplesByRel.ContainsKey(r.Label))
            .GroupBy(r => r.Label, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(r => CategorySortOrder(ClassifyRel(r.Label)))
            .ThenBy(r => r.Label, StringComparer.Ordinal)
            .Select(r => new SchemaRelationshipDto(
                Rel: r.Label,
                Category: ClassifyRel(r.Label),
                Description: r.Description,
                Properties: GetStandardEdgeProperties(r.Label),
                Pairs: triplesByRel.GetValueOrDefault(r.Label, [])
            ))
            .ToList();

        return activeRels;
    }

    private const string Layer5Key = "L5_logical";
    private const string Layer5Name = "Logical Structure";
    private const string Layer5Desc = "Business domains, bounded contexts, and strategic cross-context boundaries grouping modules, data stores, and domain types.";

    private static List<SchemaLayerMetaDto> BuildLayers() =>
    [
        new(5, Layer5Key, Layer5Name, Layer5Raw, Layer5Desc),
        new(4, "L4_semantic", "Semantic Architecture", "Layer 4: Semantic Structure",
            "Runtime workloads, API & broker contracts, databases, SQL queries, tables, and external or cloud dependencies."),
        new(3, "L3_syntactic", "Syntactic UAST & Dataflow", "Layer 3: Syntax Structure",
            "Language-agnostic AST declarations, call graph, semantic parameters, sliced call arguments, literals, and catch blocks."),
        new(2, "L2_boundaries", "Project & Package Boundaries", "Layer 2: Project Structure",
            "Compilation units, solution projects, external package dependencies, and executable entry points."),
        new(1, "L1_physical", "Physical File System", "Layer 1: File Structure",
            "Workspace folders, source files, and repository Git metadata.")
    ];

    private static List<SchemaEdgeCategoryDto> BuildEdgeCategories() =>
    [
        new(CatControlFlow, "Control & Call Flow", "Direct calls, endpoint triggers, entrypoints, and cross-service RPC/HTTP invocations."),
        new(CatDataFlow, "Data, SQL & Messaging", "Parameter-to-argument slicing, SQL table reads/writes, database access, and broker pub/sub."),
        new(CatTypeDep, "Types, Packages & Cloud", "Inheritance, type usage, project/package dependencies, test coverage, and cloud bindings."),
        new(CatStructural, "Containment & Location", "Physical file declarations, folder hierarchy, and resource ownership.")
    ];

    private static List<SchemaPropertyDto> BuildCommonProperties() =>
    [
        new("id", "Id", "string", "Globally unique deterministic URN identifier of the node."),
        new("name", "Name", "string", "Human-readable symbol, contract, or resource name."),
        new("path", "Path", "string", "Workspace-relative path of the container or declaration."),
        new("project_id", "ProjectId", "string", "URN of the enclosing compilation project, when applicable.")
    ];

    private static List<SchemaEdgePropDto> GetStandardEdgeProperties(string rel) => rel.ToUpperInvariant() switch
    {
        "CALLS" =>
        [
            new("line", "integer", "1-indexed source line of the primary invocation site."),
            new("call_count", "integer", "Aggregated number of call sites between caller and callee."),
            new("in_loop", "boolean", "True when invoked inside a for/foreach/while/do loop (N+1 risk signal)."),
            new("loop_depth", "integer", "Nesting depth of enclosing loops at the call site."),
            new("is_conditional", "boolean", "True when guarded by an if/switch/ternary branch."),
            new("in_try", "boolean", "True when enclosed inside a try/catch block."),
            new("arg_flow", "string", "Parameter-to-argument dataflow slot mapping across the call boundary.")
        ],
        "USES_DB" or "READS_FROM" or "WRITES_TO" or "ACCESSED_BY" or "QUERIES_DB" or "QUERIED_BY" =>
        [
            new("operation", "string", "Normalized data access mode (read, write, read_write, schema)."),
            new("evidence_kind", "string", "Provenance source (query, orm, table, migration, config, library_call)."),
            new("evidence_ids", "string", "JSON array of underlying UAST/Query node URNs proving this link."),
            new("hops", "integer", "Call-chain distance when lifted transitively from helper libraries.")
        ],
        "SERVICE_CALL" or "CALLS_ENDPOINT" or "PUBLISHES_TO" or "SUBSCRIBES_TO" or "PUBLISHED_BY" or "SUBSCRIBED_BY" or "USES_CLOUD" =>
        [
            new("evidence_kind", "string", "Provenance source (http_call, grpc_call, publish, subscribe, config)."),
            new("evidence_ids", "string", "JSON array of underlying call-site or endpoint URNs proving this link."),
            new("hops", "integer", "Transitive call-chain hop count from workload root.")
        ],
        "FLOWS_TO" or "PASSES_ARGUMENT" or "TARGETED_BY" =>
        [
            new("line", "integer", "1-indexed source line where the value transfer occurs."),
            new("arg_index", "integer", "0-indexed target parameter or argument slot.")
        ],
        "PASSES_LITERAL" =>
        [
            new("line", "integer", "1-indexed source line where the literal argument is passed.")
        ],
        "TRANSITIVELY_CALLS" =>
        [
            new("hops", "integer", "Shortest call-chain hop distance from the calling function to the sink.")
        ],
        "ATTRIBUTED_TO" =>
        [
            new("hops", "integer", "Call-chain hop distance from the entrypoint or endpoint to the sink."),
            new("sink_kind", "string", "Target sink classification (Database, Query, ExternalService, Topic).")
        ],
        "HOSTED_IN" =>
        [
            new("line", "integer", "Optional 1-indexed source or config line where the cloud hosting binding is declared.")
        ],
        _ =>
        [
            new("line", "integer", "Optional 1-indexed source line where the relationship is declared.")
        ]
    };

    private static string BuildSampleCypher(
        string kind,
        List<SchemaPropertyDto> props,
        List<SchemaNodeEdgeDto> outEdges,
        List<SchemaNodeInboundEdgeDto> inEdges)
    {
        const string alias = "n";
        var propPick = props
            .Select(p => p.Name)
            .Where(n => n is not ("id" or "name" or "path"))
            .Take(2)
            .ToList();
        var proj = propPick.Count > 0
            ? $"{alias}.name, " + string.Join(", ", propPick.Select(p => $"{alias}.{p}"))
            : $"{alias}.name, {alias}.path";

        var bestOut = outEdges.FirstOrDefault(e => e.Category != CatStructural) ?? outEdges.FirstOrDefault();
        if (bestOut is not null)
        {
            return $"MATCH ({alias}:{kind})-[:{bestOut.Rel}]->(t:{bestOut.TargetKind})\nRETURN {proj}, t.name\nLIMIT 25";
        }

        var bestIn = inEdges.FirstOrDefault(e => e.Category != CatStructural) ?? inEdges.FirstOrDefault();
        if (bestIn is not null)
        {
            return $"MATCH (s:{bestIn.SourceKind})-[:{bestIn.Rel}]->({alias}:{kind})\nRETURN s.name, {proj}\nLIMIT 25";
        }

        return $"MATCH ({alias}:{kind})\nRETURN {proj}\nLIMIT 25";
    }

    private static int ResolveLayerId(string layer) => layer switch
    {
        Layer1Raw => 1,
        Layer2Raw => 2,
        Layer3Raw => 3,
        Layer4Raw => 4,
        Layer5Raw => 5,
        _ => 4
    };

    private static string ResolveLayerKey(int layerId) => layerId switch
    {
        1 => "L1_physical",
        2 => "L2_boundaries",
        3 => "L3_syntactic",
        5 => Layer5Key,
        _ => "L4_semantic"
    };

    private static string ClassifyRel(string rel)
    {
        if (ControlFlowRels.Contains(rel)) return CatControlFlow;
        if (DataFlowRels.Contains(rel)) return CatDataFlow;
        if (StructuralRels.Contains(rel)) return CatStructural;
        return CatTypeDep;
    }

    private static int CategorySortOrder(string category) => category switch
    {
        CatControlFlow => 0,
        CatDataFlow => 1,
        CatTypeDep => 2,
        _ => 3
    };

    private static string NormalizePropertyType(string rawType)
    {
        var clean = rawType.Trim().TrimEnd('?');
        return clean switch
        {
            "string" or "String" => "string",
            "int" or "Int32" or "long" or "Int64" => "integer",
            "double" or "float" or "decimal" => "number",
            "bool" or "Boolean" => "boolean",
            _ => $"enum ({clean})"
        };
    }

    private static string StripNodeSuffix(string typeName) =>
        typeName.EndsWith(NodeSuffix, StringComparison.Ordinal)
            ? typeName[..^NodeSuffix.Length]
            : typeName;
}

public sealed record OntologySchemaDocument(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("layers")] List<SchemaLayerMetaDto> Layers,
    [property: JsonPropertyName("edgeCategories")] List<SchemaEdgeCategoryDto> EdgeCategories,
    [property: JsonPropertyName("commonProperties")] List<SchemaPropertyDto> CommonProperties,
    [property: JsonPropertyName("nodes")] List<SchemaNodeDto> Nodes,
    [property: JsonPropertyName("relationships")] List<SchemaRelationshipDto> Relationships
);

public sealed record SchemaLayerMetaDto(
    [property: JsonPropertyName("layerId")] int LayerId,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description
);

public sealed record SchemaEdgeCategoryDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("description")] string Description
);

public sealed record SchemaNodeDto(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("pluralLabel")] string PluralLabel,
    [property: JsonPropertyName("layerId")] int LayerId,
    [property: JsonPropertyName("layerKey")] string LayerKey,
    [property: JsonPropertyName("icon")] string Icon,
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("idScheme")] string IdScheme,
    [property: JsonPropertyName("purpose")] string Purpose,
    [property: JsonPropertyName("isAbstract")] bool? IsAbstract,
    [property: JsonPropertyName("extends")] string? Extends,
    [property: JsonPropertyName("ancestors")] List<string>? Ancestors,
    [property: JsonPropertyName("subtypes")] List<string>? Subtypes,
    [property: JsonPropertyName("properties")] List<SchemaPropertyDto> Properties,
    [property: JsonPropertyName("outEdges")] List<SchemaNodeEdgeDto> OutEdges,
    [property: JsonPropertyName("inEdges")] List<SchemaNodeInboundEdgeDto> InEdges,
    [property: JsonPropertyName("inheritedOutEdges")] List<SchemaNodeEdgeDto>? InheritedOutEdges,
    [property: JsonPropertyName("inheritedInEdges")] List<SchemaNodeInboundEdgeDto>? InheritedInEdges,
    [property: JsonPropertyName("sampleCypher")] string SampleCypher
);

public sealed record SchemaPropertyDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("sourceName")] string SourceName,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inheritedFrom")] string? InheritedFrom = null
);

public sealed record SchemaNodeEdgeDto(
    [property: JsonPropertyName("rel")] string Rel,
    [property: JsonPropertyName("toKind")] string TargetKind,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inheritedFrom")] string? InheritedFrom = null
);

public sealed record SchemaNodeInboundEdgeDto(
    [property: JsonPropertyName("rel")] string Rel,
    [property: JsonPropertyName("fromKind")] string SourceKind,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inheritedFrom")] string? InheritedFrom = null
);

public sealed record SchemaRelationshipDto(
    [property: JsonPropertyName("rel")] string Rel,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("properties")] List<SchemaEdgePropDto> Properties,
    [property: JsonPropertyName("pairs")] List<SchemaEdgePairDto> Pairs
);

public sealed record SchemaEdgePropDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("description")] string Description
);

public sealed record SchemaEdgePairDto(
    [property: JsonPropertyName("fromKind")] string FromKind,
    [property: JsonPropertyName("toKind")] string ToKind
);
