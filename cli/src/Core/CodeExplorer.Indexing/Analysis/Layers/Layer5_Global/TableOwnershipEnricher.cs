using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser.Layers;

internal static class TableOwnershipEnricher
{
    private const string DbIdSegment = ":db:";
    private const string SymbolIdSegment = ":Symbol:";
    private const string SqlExtension = ".sql";
    private const string PrismaExtension = ".prisma";
    private const string DbmlExtension = ".dbml";
    private const string CqlExtension = ".cql";
    private const string HbmXmlExtension = ".hbm.xml";
    private const string MigrationsSegment = "/migrations/";
    private const string MigrationsPrefix = "migrations/";

    public static Dictionary<string, string> EnrichTableOwnership(
        Layer4Result? l4Result,
        ParsingContext ctx,
        List<Relationship> referenceRelationships,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var tableOwnerModuleId = new Dictionary<string, string>(StringComparer.Ordinal);
        if (l4Result == null) return tableOwnerModuleId;

        var syntaxStruct = l4Result.Prev?.SyntaxStructure ?? ctx.SyntaxStructure;
        if (syntaxStruct == null) return tableOwnerModuleId;

        var modules = SemanticEnrichmentHelpers.CollectModules(l4Result.SemanticStructure);
        if (modules.Count == 0) return tableOwnerModuleId;

        var tablesById = new Dictionary<string, TableNode>(StringComparer.Ordinal);
        CollectTables(l4Result.SemanticStructure, tablesById);
        CollectTables(l4Result.Prev?.Prev?.Prev?.FilesStructure, tablesById);
        if (tablesById.Count == 0) return tableOwnerModuleId;

        var index = SemanticEnrichmentHelpers.CollectTypesAndMethods(syntaxStruct);

        var entityByTableId = new Dictionary<string, string>(StringComparer.Ordinal);
        var writerModulesByTable = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        var writerModulesByName = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < referenceRelationships.Count; i++)
        {
            var r = referenceRelationships[i];
            if (r.Kind == RelationshipKind.ImplementedBy && tablesById.ContainsKey(r.From) && index.TypesById.ContainsKey(r.To))
            {
                entityByTableId.TryAdd(r.From, r.To);
            }
            else if (r.Kind == RelationshipKind.WritesTo && tablesById.TryGetValue(r.To, out var writtenTbl) &&
                     index.FunctionsById.TryGetValue(r.From, out var writerFn) && !writerFn.IsTest)
            {
                var wMod = SemanticEnrichmentHelpers.FindOwningModule(writerFn.FilePath.Replace('\\', '/'), modules);
                if (wMod != null)
                {
                    if (!writerModulesByTable.TryGetValue(r.To, out var counts))
                    {
                        counts = new Dictionary<string, int>(StringComparer.Ordinal);
                        writerModulesByTable[r.To] = counts;
                    }
                    counts[wMod.Id] = counts.GetValueOrDefault(wMod.Id) + 1;

                    if (!writerModulesByName.TryGetValue(writtenTbl.Name, out var nameMods))
                    {
                        nameMods = new HashSet<string>(StringComparer.Ordinal);
                        writerModulesByName[writtenTbl.Name] = nameMods;
                    }
                    nameMods.Add(wMod.Id);
                }
            }
        }

        foreach (var r in ctx.TreeRelationships)
        {
            if (r.Kind == RelationshipKind.ImplementedBy && tablesById.ContainsKey(r.From) && index.TypesById.ContainsKey(r.To))
            {
                entityByTableId.TryAdd(r.From, r.To);
            }
        }

        var explicitlyOwnedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ownedByModAndName = new HashSet<(string ModId, string TableName)>();
        var entityOwnerByTableName = new Dictionary<string, ModuleNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var (tId, entId) in entityByTableId)
        {
            if (tablesById.TryGetValue(tId, out var tNode) &&
                !string.IsNullOrEmpty(tNode.Name) &&
                index.TypesById.TryGetValue(entId, out var entType))
            {
                var entOwner = SemanticEnrichmentHelpers.FindOwningModule(entType.FilePath.Replace('\\', '/'), modules);
                if (entOwner != null)
                {
                    entityOwnerByTableName.TryAdd(tNode.Name, entOwner);
                }
            }
        }

        // Order ORM-mapped table nodes first, then canonical ':db:' table nodes before raw ':tbl:' nodes
        var orderedTables = tablesById
            .OrderByDescending(kv => entityByTableId.ContainsKey(kv.Key))
            .ThenByDescending(kv => kv.Key.Contains(DbIdSegment, StringComparison.Ordinal) && !kv.Key.Contains(SymbolIdSegment, StringComparison.Ordinal))
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();

        // Pass 1: explicit ownership from mapped ORM entities or SQL DDL / migration files
        foreach (var (tableId, table) in orderedTables)
        {
            ModuleNode? owner = null;

            if (entityByTableId.TryGetValue(tableId, out var entityId) && index.TypesById.TryGetValue(entityId, out var entityType))
            {
                owner = SemanticEnrichmentHelpers.FindOwningModule(entityType.FilePath.Replace('\\', '/'), modules);
            }
            else if (!string.IsNullOrEmpty(table.Name) && entityOwnerByTableName.TryGetValue(table.Name, out var mappedOwner))
            {
                owner = mappedOwner;
            }

            if (owner == null &&
                !explicitlyOwnedNames.Contains(table.Name) &&
                !string.IsNullOrEmpty(table.Path) &&
                table.Path != "." &&
                IsSchemaOrDdlFilePath(table.Path))
            {
                owner = SemanticEnrichmentHelpers.FindOwningModule(table.Path.Replace('\\', '/'), modules);
            }

            if (owner != null)
            {
                explicitlyOwnedNames.Add(table.Name);
                tableOwnerModuleId[tableId] = owner.Id;
                if (ownedByModAndName.Add((owner.Id, table.Name.ToLowerInvariant())))
                {
                    SemanticEnrichmentHelpers.AddUniqueRel(
                        new OwnsRelationship(owner.Id, tableId),
                        referenceRelationships,
                        existingRefRels);
                }
            }
        }

        // Pass 2: single exclusive writer module when no explicit entity/DDL owner exists for that table name
        foreach (var (tableId, table) in orderedTables)
        {
            if (tableOwnerModuleId.ContainsKey(tableId) || explicitlyOwnedNames.Contains(table.Name)) continue;

            string? singleModId = null;
            if (writerModulesByTable.TryGetValue(tableId, out var wCounts) && wCounts.Count == 1 &&
                (!writerModulesByName.TryGetValue(table.Name, out var nameMods) || nameMods.Count == 1))
            {
                singleModId = wCounts.Keys.First();
            }
            else if (writerModulesByName.TryGetValue(table.Name, out var exclusiveMods) && exclusiveMods.Count == 1)
            {
                singleModId = exclusiveMods.First();
            }

            if (singleModId != null)
            {
                var owner = modules.FirstOrDefault(m => m.Id == singleModId);
                if (owner != null)
                {
                    explicitlyOwnedNames.Add(table.Name);
                    tableOwnerModuleId[tableId] = owner.Id;
                    if (ownedByModAndName.Add((owner.Id, table.Name.ToLowerInvariant())))
                    {
                        SemanticEnrichmentHelpers.AddUniqueRel(
                            new OwnsRelationship(owner.Id, tableId),
                            referenceRelationships,
                            existingRefRels);
                    }
                }
            }
        }

        return tableOwnerModuleId;
    }

    private static bool IsSchemaOrDdlFilePath(string path)
    {
        var norm = path.Replace('\\', '/');
        return norm.EndsWith(SqlExtension, StringComparison.OrdinalIgnoreCase) ||
               norm.EndsWith(PrismaExtension, StringComparison.OrdinalIgnoreCase) ||
               norm.EndsWith(DbmlExtension, StringComparison.OrdinalIgnoreCase) ||
               norm.EndsWith(CqlExtension, StringComparison.OrdinalIgnoreCase) ||
               norm.EndsWith(HbmXmlExtension, StringComparison.OrdinalIgnoreCase) ||
               norm.Contains(MigrationsSegment, StringComparison.OrdinalIgnoreCase) ||
               norm.StartsWith(MigrationsPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static void CollectTables(IOntologyNode? node, Dictionary<string, TableNode> tablesById)
    {
        if (node == null) return;
        if (node is TableNode tbl) tablesById.TryAdd(tbl.Id, tbl);
        for (var i = 0; i < node.Children.Count; i++) CollectTables(node.Children[i], tablesById);
    }
}
