using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Parser.Syntax;
using Node = CodeExplorer.Core.Parser.Syntax.SyntaxNode;

namespace CodeExplorer.Parsers.Java.Libraries;

/// <summary>
/// Shared helper for Spring Data repositories (JPA, MongoDB, R2DBC) to map entity classes
/// to their explicit @Table/@Document names and emit READS_FROM / WRITES_TO lineage edges
/// on repository interfaces and their derived query methods.
/// </summary>
internal static partial class SpringDataRepositoryHelper
{
    private const string TableOrDocumentMarker = "@Table";
    private const string DocumentMarker = "@Document";
    private const string InterfaceDeclType = "interface_declaration";
    private const string MethodDeclType = "method_declaration";

    internal static readonly string[] RepositoryNodeTypes = [InterfaceDeclType, MethodDeclType];

    internal static readonly FrozenSet<string> JpaRepositoryInterfaces = new[]
    {
        "JpaRepository",
        "CrudRepository",
        "PagingAndSortingRepository",
        "ListCrudRepository",
        "ListPagingAndSortingRepository",
        "ReactiveCrudRepository",
        "ReactiveSortingRepository",
        "R2dbcRepository",
        "JpaSpecificationExecutor",
        "Repository"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    internal static readonly FrozenSet<string> MongoRepositoryInterfaces = new[]
    {
        "MongoRepository",
        "ReactiveMongoRepository",
        "CrudRepository",
        "PagingAndSortingRepository",
        "ReactiveCrudRepository"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] WriteMethodPrefixes =
    [
        "save", "delete", "remove", "insert", "update", "upsert", "flush", "create", "persist", "merge"
    ];

    // Matches @Table(name = "orders") or @Document(collection = "orders") followed by class OrderEntity
    [GeneratedRegex(@"@(?:Table|Document)\s*\((?:[^)]*?\b(?:name|collection|value)\s*=\s*)?""([^""]+)""[^)]*\)(?:[\s\S]{0,400}?)\b(?:class|record)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled)]
    private static partial Regex EntityTableRegex();

    internal static void DiscoverEntityTables(string content, ConcurrentDictionary<string, string> entityToTable)
    {
        if (string.IsNullOrEmpty(content)) return;
        if (!content.Contains(TableOrDocumentMarker, StringComparison.Ordinal) &&
            !content.Contains(DocumentMarker, StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in EntityTableRegex().Matches(content))
        {
            var tableName = match.Groups[1].Value.Trim();
            var entityName = match.Groups[2].Value.Trim();
            if (!string.IsNullOrEmpty(tableName) && !string.IsNullOrEmpty(entityName))
            {
                entityToTable[entityName] = tableName;
            }
        }
    }

    internal static void EnrichRepositorySymbol(
        Node node,
        SyntacticSymbol symbol,
        FrozenSet<string> repositoryInterfaces,
        ConcurrentDictionary<string, string> entityToTable)
    {
        if (node.Type == InterfaceDeclType || node is TypeDeclSyntax { Kind: TypeSyntaxKind.Interface })
        {
            var entityType = DeclarativeSemanticExtension.ExtractGenericTypeFromInterfaces(node, repositoryInterfaces);
            if (string.IsNullOrEmpty(entityType)) return;

            var tableName = ResolveTableName(entityType, entityToTable);
            symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.ReadsFrom));
            symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.WritesTo));
            return;
        }

        if (node.Type == MethodDeclType || node is FunctionDeclSyntax)
        {
            var enclosingInterface = FindEnclosingInterface(node);
            if (enclosingInterface == null) return;

            var entityType = DeclarativeSemanticExtension.ExtractGenericTypeFromInterfaces(enclosingInterface, repositoryInterfaces);
            if (string.IsNullOrEmpty(entityType)) return;

            var tableName = ResolveTableName(entityType, entityToTable);
            var relation = IsWriteMethod(symbol.Name)
                ? OntologyConstants.Relationships.WritesTo
                : OntologyConstants.Relationships.ReadsFrom;
            symbol.References.Add(new Reference(symbol.Name, tableName, relation));
        }
    }

    private static string ResolveTableName(string entityType, ConcurrentDictionary<string, string> entityToTable) =>
        entityToTable.TryGetValue(entityType, out var mapped) && !string.IsNullOrEmpty(mapped)
            ? mapped
            : entityType;

    private static Node? FindEnclosingInterface(Node methodNode)
    {
        var parent = methodNode.Parent;
        while (parent.IsValid())
        {
            if (parent.Type == InterfaceDeclType || parent is TypeDeclSyntax { Kind: TypeSyntaxKind.Interface })
            {
                return parent;
            }
            if (parent is TypeDeclSyntax)
            {
                return null;
            }
            parent = parent.Parent;
        }
        return null;
    }

    private static bool IsWriteMethod(string? methodName)
    {
        if (string.IsNullOrEmpty(methodName)) return false;
        for (var i = 0; i < WriteMethodPrefixes.Length; i++)
        {
            if (methodName.StartsWith(WriteMethodPrefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
