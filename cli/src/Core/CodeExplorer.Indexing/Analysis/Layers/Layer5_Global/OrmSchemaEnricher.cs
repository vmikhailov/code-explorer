using System.Text.Json;
using System.Text.Json.Serialization;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser.Layers;

internal static class OrmSchemaEnricher
{
    private const string TableSubKindValue = "table";
    private const string IdColumnName = "id";
    private const string IdSuffix = "Id";
    private const string SnakeIdSuffix = "_id";
    private const string NullableMarker = "?";
    private const string UnionNullMarker = "| null";
    private const string UnionUndefinedMarker = "| undefined";
    private const string OptionalPrefix = "Optional[";
    private const string KeyAttr = "Key";
    private const string PrimaryAttr = "Primary";
    private const string JpaIdAttr = "Id";
    private const string RequiredAttr = "Required";
    private const string NotNullAttr = "NotNull";
    private const string ForeignKeyAttr = "ForeignKey";
    private const string JoinColumnAttr = "JoinColumn";
    private const string ManyToOneAttr = "ManyToOne";
    private const string OneToOneAttr = "OneToOne";
    private const string UniqueAttr = "Unique";

    private static readonly string[] CollectionMarkers =
    [
        "List<", "ICollection<", "IEnumerable<", "IList<", "HashSet<", "Set<", "Array<", "[]"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed record OrmColumnDto(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("type")] string SqlFormat,
        [property: JsonPropertyName("pk")] bool? IsPrimaryKey = null,
        [property: JsonPropertyName("nullable")] bool? IsNullable = null,
        [property: JsonPropertyName("unique")] bool? IsUnique = null,
        [property: JsonPropertyName("fk")] string? ForeignKey = null);

    public static List<Node> EnrichOrmTablesAndForeignKeys(
        Layer4Result? l4Result,
        ParsingContext ctx,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var updatedNodes = new List<Node>();
        if (l4Result == null) return updatedNodes;

        var tablesById = new Dictionary<string, TableNode>(StringComparer.OrdinalIgnoreCase);
        CollectTables(l4Result.SemanticStructure, tablesById);
        if (l4Result.Prev?.Prev?.Prev?.FilesStructure != null)
        {
            CollectTables(l4Result.Prev.Prev.Prev.FilesStructure, tablesById);
        }
        if (tablesById.Count == 0) return updatedNodes;

        var typesById = new Dictionary<string, TypeNode>(StringComparer.Ordinal);
        var typesByName = new Dictionary<string, TypeNode>(StringComparer.OrdinalIgnoreCase);
        var syntaxStruct = l4Result.Prev?.SyntaxStructure ?? ctx.SyntaxStructure;
        if (syntaxStruct != null)
        {
            CollectTypes(syntaxStruct, typesById, typesByName);
        }
        if (typesById.Count == 0) return updatedNodes;

        var tableToEntity = MapTablesToEntities(tablesById, typesById, typesByName, referenceRelationships, ctx.TreeRelationships);
        if (tableToEntity.Count == 0) return updatedNodes;

        var entityNameToTable = new Dictionary<string, TableNode>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tableId, entity) in tableToEntity)
        {
            if (tablesById.TryGetValue(tableId, out var tbl))
            {
                entityNameToTable.TryAdd(entity.Name, tbl);
            }
        }

        foreach (var (tableId, entity) in tableToEntity)
        {
            if (!tablesById.TryGetValue(tableId, out var tableNode)) continue;

            var members = entity.Children.OfType<MemberNode>().Where(m => m.IsConstant != true).ToList();
            if (members.Count == 0) continue;

            var cols = new List<OrmColumnDto>();
            foreach (var member in members)
            {
                var rawType = member.DataType?.Trim() ?? string.Empty;
                if (IsCollectionType(rawType)) continue;

                var cleanType = StripNullable(rawType);
                var attrs = string.IsNullOrWhiteSpace(member.Attributes)
                    ? Array.Empty<string>()
                    : member.Attributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                // Check singular navigation property pointing to another ORM entity
                if (!string.IsNullOrEmpty(cleanType) &&
                    entityNameToTable.TryGetValue(cleanType, out var navTarget) &&
                    !string.Equals(navTarget.Id, tableNode.Id, StringComparison.OrdinalIgnoreCase))
                {
                    EmitForeignKeyEdge(tableNode, navTarget, member.Name, referenceRelationships, existingRefRels);
                    continue;
                }

                // Check FK convention (e.g. CustomerId / customer_id -> Customer entity)
                TableNode? fkTarget = ResolveConventionFkTarget(member.Name, attrs, entityNameToTable, tableNode.Id);
                string? fkLabel = null;
                if (fkTarget != null)
                {
                    fkLabel = $"{fkTarget.Name}.{IdColumnName}";
                    EmitForeignKeyEdge(tableNode, fkTarget, member.Name, referenceRelationships, existingRefRels);
                }

                var isPk = IsPrimaryKeyMember(member.Name, entity.Name, attrs);
                var isNullable = !isPk && IsNullableMember(rawType, attrs);
                var isUnique = HasAttributeContaining(attrs, UniqueAttr);

                cols.Add(new OrmColumnDto(
                    member.Name,
                    string.IsNullOrEmpty(cleanType) ? "unknown" : cleanType,
                    isPk ? true : null,
                    isNullable ? null : false,
                    isUnique ? true : null,
                    fkLabel));
            }

            if (string.IsNullOrWhiteSpace(tableNode.ColumnsJson) && cols.Count > 0)
            {
                tableNode.ColumnsJson = JsonSerializer.Serialize(cols, JsonOptions);
                tableNode.SubKind ??= TableSubKindValue;
                updatedNodes.Add(Node.FromNode(tableNode));
            }
        }

        return updatedNodes;
    }

    private static void CollectTables(IOntologyNode node, Dictionary<string, TableNode> tablesById)
    {
        if (node is TableNode tbl)
        {
            tablesById.TryAdd(tbl.Id, tbl);
        }
        foreach (var child in node.Children)
        {
            CollectTables(child, tablesById);
        }
    }

    private static void CollectTypes(
        IOntologyNode node,
        Dictionary<string, TypeNode> typesById,
        Dictionary<string, TypeNode> typesByName)
    {
        if (node is TypeNode t)
        {
            typesById.TryAdd(t.Id, t);
            typesByName.TryAdd(t.Name, t);
        }
        foreach (var child in node.Children)
        {
            CollectTypes(child, typesById, typesByName);
        }
    }

    private static Dictionary<string, TypeNode> MapTablesToEntities(
        Dictionary<string, TableNode> tablesById,
        Dictionary<string, TypeNode> typesById,
        Dictionary<string, TypeNode> typesByName,
        List<Relationship> referenceRelationships,
        IEnumerable<Relationship> treeRelationships)
    {
        var map = new Dictionary<string, TypeNode>(StringComparer.OrdinalIgnoreCase);

        void InspectRel(Relationship rel)
        {
            if (rel.Kind == RelationshipKind.ImplementedBy &&
                tablesById.ContainsKey(rel.From) &&
                typesById.TryGetValue(rel.To, out var entity))
            {
                map.TryAdd(rel.From, entity);
            }
        }

        foreach (var rel in referenceRelationships) InspectRel(rel);
        foreach (var rel in treeRelationships) InspectRel(rel);

        foreach (var (tableId, tbl) in tablesById)
        {
            if (map.ContainsKey(tableId)) continue;
            if (!string.IsNullOrWhiteSpace(tbl.EntityName) && typesByName.TryGetValue(tbl.EntityName, out var entity))
            {
                map[tableId] = entity;
            }
        }

        return map;
    }

    private static TableNode? ResolveConventionFkTarget(
        string memberName,
        IReadOnlyList<string> attrs,
        Dictionary<string, TableNode> entityNameToTable,
        string selfTableId)
    {
        string? stem = null;
        if (memberName.Length > IdSuffix.Length && memberName.EndsWith(IdSuffix, StringComparison.Ordinal))
        {
            stem = memberName[..^IdSuffix.Length];
        }
        else if (memberName.Length > SnakeIdSuffix.Length && memberName.EndsWith(SnakeIdSuffix, StringComparison.OrdinalIgnoreCase))
        {
            stem = memberName[..^SnakeIdSuffix.Length];
        }

        if (!string.IsNullOrEmpty(stem) &&
            entityNameToTable.TryGetValue(stem, out var target) &&
            !string.Equals(target.Id, selfTableId, StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        if (HasAttributeContaining(attrs, ForeignKeyAttr) ||
            HasAttributeContaining(attrs, JoinColumnAttr) ||
            HasAttributeContaining(attrs, ManyToOneAttr) ||
            HasAttributeContaining(attrs, OneToOneAttr))
        {
            foreach (var (entityName, tbl) in entityNameToTable)
            {
                if (string.Equals(tbl.Id, selfTableId, StringComparison.OrdinalIgnoreCase)) continue;
                if (attrs.Any(a => a.Contains(entityName, StringComparison.OrdinalIgnoreCase)))
                {
                    return tbl;
                }
            }
        }

        return null;
    }

    private static void EmitForeignKeyEdge(
        TableNode childTable,
        TableNode parentTable,
        string fromColumn,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        if (!existingRefRels.Add((childTable.Id, parentTable.Id, OntologyConstants.Relationships.References)))
        {
            return;
        }

        var rel = Relationship.FromRelationship(new ReferencesRelationship(childTable.Id, parentTable.Id)
        {
            Operation = $"{fromColumn}->{parentTable.Name}.{IdColumnName}",
            EvidenceKind = OntologyConstants.EvidenceKinds.Orm
        });
        referenceRelationships.Add(rel);
    }

    private static bool IsCollectionType(string rawType)
    {
        if (string.IsNullOrEmpty(rawType)) return false;
        foreach (var marker in CollectionMarkers)
        {
            if (rawType.Contains(marker, StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static string StripNullable(string rawType)
    {
        if (string.IsNullOrEmpty(rawType)) return string.Empty;
        var s = rawType.Trim();
        if (s.EndsWith(NullableMarker, StringComparison.Ordinal))
        {
            s = s[..^1].Trim();
        }
        if (s.StartsWith(OptionalPrefix, StringComparison.Ordinal) && s.EndsWith(']'))
        {
            s = s[OptionalPrefix.Length..^1].Trim();
        }
        var pipeIdx = s.IndexOf('|');
        if (pipeIdx > 0)
        {
            s = s[..pipeIdx].Trim();
        }
        return s;
    }

    private static bool IsPrimaryKeyMember(string memberName, string entityName, IReadOnlyList<string> attrs)
    {
        if (memberName.Equals(IdColumnName, StringComparison.OrdinalIgnoreCase) ||
            memberName.Equals($"{entityName}{IdSuffix}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return HasAttributeContaining(attrs, KeyAttr) ||
               HasAttributeContaining(attrs, PrimaryAttr) ||
               attrs.Any(a => a.Equals(JpaIdAttr, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNullableMember(string rawType, IReadOnlyList<string> attrs)
    {
        if (HasAttributeContaining(attrs, RequiredAttr) || HasAttributeContaining(attrs, NotNullAttr))
        {
            return false;
        }
        return rawType.EndsWith(NullableMarker, StringComparison.Ordinal) ||
               rawType.Contains(UnionNullMarker, StringComparison.OrdinalIgnoreCase) ||
               rawType.Contains(UnionUndefinedMarker, StringComparison.OrdinalIgnoreCase) ||
               rawType.StartsWith(OptionalPrefix, StringComparison.Ordinal);
    }

    private static bool HasAttributeContaining(IReadOnlyList<string> attrs, string needle)
    {
        foreach (var attr in attrs)
        {
            if (attr.Contains(needle, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
