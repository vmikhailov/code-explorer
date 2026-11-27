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
    private const string EntityMarker = "@Entity";
    private const string InterfaceDeclType = "interface_declaration";
    private const string ClassDeclNode = "class_declaration";
    private const string MethodDeclType = "method_declaration";
    private const string MethodInvocationType = "method_invocation";
    private const string CallExpressionType = "call_expression";
    private const string ThisDotPrefix = "this.";
    private const string SuperReceiver = "super";
    private const string ThisReceiver = "this";
    private const string NewKeywordPrefix = "new ";
    private const string DotClassSuffix = ".class";
    private const string IdGenericParam = "ID";
    private const string ObjectField = "object";
    private const string NameField = "name";

    internal static readonly string[] RepositoryNodeTypes = [InterfaceDeclType, ClassDeclNode, MethodDeclType, MethodInvocationType, CallExpressionType];

    private static readonly FrozenSet<string> SessionReceiverNames = new[]
    {
        "session", "getSession()", "entityManager", "getEntityManager()", "em", "statelessSession", "getStatelessSession()"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] ReadMethodPrefixes =
    [
        "find", "get", "read", "query", "count", "exists", "search", "stream"
    ];

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
        "save", "saveOrUpdate", "makePersistent", "makeTransient", "delete", "remove", "insert", "update", "upsert", "flush", "create", "persist", "merge"
    ];

    private static readonly string[] QueryFactoryPrefixes =
    [
        "createQuery", "createNativeQuery", "createNamedQuery", "createSQLQuery", "createCriteria", "createFilter"
    ];

    // Matches @Table(name = "orders") or @Document(collection = "orders") followed by class OrderEntity
    [GeneratedRegex(@"@(?:Table|Document)\s*\((?:[^)]*?\b(?:name|collection|value)\s*=\s*)?""([^""]+)""[^)]*\)(?:[\s\S]{0,400}?)\b(?:class|record)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled)]
    private static partial Regex EntityTableRegex();

    // Matches @Entity classes without explicit @Table(name = "...") (JPA defaults table name to entity class name)
    [GeneratedRegex(@"@Entity\b(?:[\s\S]{0,600}?)\bclass\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled)]
    private static partial Regex PlainEntityClassRegex();

    [GeneratedRegex(@"\b(?:interface|class)\s+([A-Za-z_][A-Za-z0-9_]*)\s+(?:extends|implements)\s+[^{]*?\b(?:JpaRepository|CrudRepository|PagingAndSortingRepository|ListCrudRepository|ReactiveCrudRepository|R2dbcRepository|MongoRepository|ReactiveMongoRepository|[A-Za-z0-9_]*(?:DAO|Dao|Repository|EntityHome)[A-Za-z0-9_]*)\s*<\s*([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled)]
    private static partial Regex RepoInterfaceRegex();

    [GeneratedRegex(@"\b([A-Za-z_][A-Za-z0-9_]*)\s+([a-zA-Z_][a-zA-Z0-9_]*)\s*(?:=|;|,|\))", RegexOptions.Compiled)]
    private static partial Regex FieldOrParamBindingRegex();

    [GeneratedRegex(@"\b(?:class|interface)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled)]
    private static partial Regex EnclosingTypeHeaderRegex();

    internal sealed class WorkspaceEntityMaps
    {
        public ConcurrentDictionary<string, string> EntityToTable { get; } = new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<string, string> RepoToEntity { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static WeakReference<WorkspaceEntityMaps>? _lastContextMaps;

    internal static WorkspaceEntityMaps GetOrCreateContextMaps(
        SemanticParseContext? ctx,
        System.Runtime.CompilerServices.ConditionalWeakTable<SemanticParseContext, WorkspaceEntityMaps> store)
    {
        if (ctx == null)
        {
            if (_lastContextMaps != null && _lastContextMaps.TryGetTarget(out var lastMaps))
            {
                return lastMaps;
            }
            return new WorkspaceEntityMaps();
        }
        var maps = store.GetValue(ctx, static _ => new WorkspaceEntityMaps());
        _lastContextMaps = new WeakReference<WorkspaceEntityMaps>(maps);
        return maps;
    }

    internal static void DiscoverEntityTables(
        string content,
        ConcurrentDictionary<string, string> entityToTable,
        ConcurrentDictionary<string, string>? repoToEntity = null)
    {
        if (string.IsNullOrEmpty(content)) return;
        if (content.Contains(TableOrDocumentMarker, StringComparison.Ordinal) ||
            content.Contains(DocumentMarker, StringComparison.Ordinal))
        {
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

        if (content.Contains(EntityMarker, StringComparison.Ordinal))
        {
            foreach (Match match in PlainEntityClassRegex().Matches(content))
            {
                var entityName = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(entityName))
                {
                    entityToTable.TryAdd(entityName, entityName);
                }
            }
        }

        if (repoToEntity != null)
        {
            foreach (Match m in RepoInterfaceRegex().Matches(content))
            {
                var repoName = m.Groups[1].Value.Trim();
                var entityName = m.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(repoName) &&
                    entityName.Length > 1 &&
                    !string.Equals(entityName, IdGenericParam, StringComparison.Ordinal))
                {
                    repoToEntity[repoName] = entityName;
                }
            }
        }
    }

    internal static void CollectRepositoryCallReferences(
        Node node,
        string scopeSymbolId,
        List<Reference> references,
        ConcurrentDictionary<string, string> entityToTable,
        ConcurrentDictionary<string, string> repoToEntity)
    {
        if (node is not CallExprSyntax call) return;
        string? receiverName = null;
        string? methodName = null;
        if (call.Callee is MemberAccessExprSyntax ma)
        {
            methodName = ma.MemberName;
            receiverName = (ma.Object ?? (ma.Children.Count > 0 ? ma.Children[0] : null))?.Text.Trim();
        }
        else
        {
            receiverName = node.GetField(ObjectField)?.Text.Trim();
            methodName = node.GetField(NameField)?.Text.Trim();
        }
        if (receiverName != null && receiverName.StartsWith(ThisDotPrefix, StringComparison.Ordinal))
        {
            receiverName = receiverName[ThisDotPrefix.Length..];
        }
        if (string.IsNullOrEmpty(methodName)) return;

        var isWrite = IsWriteMethod(methodName);
        var isRead = !isWrite && IsReadMethod(methodName);
        if (!isWrite && !isRead) return;

        var enclosingClass = FindEnclosingClassText(node);
        if (string.IsNullOrEmpty(enclosingClass)) return;

        string? entityType = null;

        if (string.IsNullOrEmpty(receiverName) ||
            string.Equals(receiverName, SuperReceiver, StringComparison.Ordinal) ||
            string.Equals(receiverName, ThisReceiver, StringComparison.Ordinal))
        {
            var headerMatch = EnclosingTypeHeaderRegex().Match(enclosingClass);
            if (headerMatch.Success && repoToEntity.TryGetValue(headerMatch.Groups[1].Value, out var selfDaoEntity))
            {
                entityType = selfDaoEntity;
            }
        }
        else
        {
            foreach (Match bm in FieldOrParamBindingRegex().Matches(enclosingClass))
            {
                if (string.Equals(bm.Groups[2].Value, receiverName, StringComparison.Ordinal))
                {
                    var declaredType = bm.Groups[1].Value;
                    if (repoToEntity.TryGetValue(declaredType, out var mappedEntity))
                    {
                        entityType = mappedEntity;
                        break;
                    }
                }
            }

            if (entityType == null && SessionReceiverNames.Contains(receiverName) && call.Arguments.Count > 0)
            {
                entityType = TryResolveSessionCallEntity(call.Arguments[0].Text, enclosingClass, entityToTable, repoToEntity);
            }
        }

        if (string.IsNullOrEmpty(entityType)) return;
        var tableName = ResolveTableName(entityType, entityToTable);
        var relKind = isWrite ? OntologyConstants.Relationships.WritesTo : OntologyConstants.Relationships.ReadsFrom;
        references.Add(new Reference(scopeSymbolId, tableName, relKind, node.StartPosition.Row + 1, node.StartPosition.Column + 1));
    }

    private static string? TryResolveSessionCallEntity(
        string rawArgText,
        string enclosingClass,
        ConcurrentDictionary<string, string> entityToTable,
        ConcurrentDictionary<string, string> repoToEntity)
    {
        var arg = rawArgText.Trim();
        if (string.IsNullOrEmpty(arg) || arg[0] == '"' || arg[0] == '\'') return null;

        if (arg.EndsWith(DotClassSuffix, StringComparison.Ordinal))
        {
            var cls = arg[..^DotClassSuffix.Length].Trim();
            var dotIdx = cls.LastIndexOf('.');
            if (dotIdx >= 0 && dotIdx < cls.Length - 1) cls = cls[(dotIdx + 1)..];
            return entityToTable.ContainsKey(cls) ? cls : null;
        }

        if (arg.StartsWith(NewKeywordPrefix, StringComparison.Ordinal))
        {
            var rest = arg[NewKeywordPrefix.Length..].TrimStart();
            var parenIdx = rest.IndexOfAny(['(', '<', ' ']);
            var constructed = parenIdx > 0 ? rest[..parenIdx].Trim() : rest;
            return entityToTable.ContainsKey(constructed) ? constructed : null;
        }

        if (arg.StartsWith(ThisDotPrefix, StringComparison.Ordinal))
        {
            arg = arg[ThisDotPrefix.Length..];
        }

        foreach (Match bm in FieldOrParamBindingRegex().Matches(enclosingClass))
        {
            if (string.Equals(bm.Groups[2].Value, arg, StringComparison.Ordinal))
            {
                var declaredType = bm.Groups[1].Value;
                if (entityToTable.ContainsKey(declaredType))
                {
                    return declaredType;
                }
            }
        }

        var headerMatch = EnclosingTypeHeaderRegex().Match(enclosingClass);
        if (headerMatch.Success && repoToEntity.TryGetValue(headerMatch.Groups[1].Value, out var enclosingDaoEntity))
        {
            return enclosingDaoEntity;
        }

        return null;
    }

    internal static void EnrichRepositorySymbol(
        Node node,
        SyntacticSymbol symbol,
        FrozenSet<string> repositoryInterfaces,
        ConcurrentDictionary<string, string> entityToTable,
        ConcurrentDictionary<string, string>? repoToEntity = null)
    {
        if (node.Type == InterfaceDeclType || node is TypeDeclSyntax { Kind: TypeSyntaxKind.Interface })
        {
            var entityType = DeclarativeSemanticExtension.ExtractGenericTypeFromInterfaces(node, repositoryInterfaces);
            if (string.IsNullOrEmpty(entityType) && (repoToEntity == null || !repoToEntity.TryGetValue(symbol.Name, out entityType)))
            {
                return;
            }

            var tableName = ResolveTableName(entityType!, entityToTable);
            symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.ReadsFrom));
            symbol.References.Add(new Reference(symbol.Name, tableName, OntologyConstants.Relationships.WritesTo));
            return;
        }

        if (node.Type == ClassDeclNode || node is TypeDeclSyntax { Kind: TypeSyntaxKind.Class })
        {
            if (repoToEntity == null || !repoToEntity.TryGetValue(symbol.Name, out var daoEntity) || string.IsNullOrEmpty(daoEntity))
            {
                return;
            }

            var tableName = ResolveTableName(daoEntity, entityToTable);
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

    private static string? FindEnclosingClassText(Node node)
    {
        var p = node.Parent;
        while (p.IsValid())
        {
            if (p is TypeDeclSyntax) return p.Text;
            p = p.Parent;
        }
        return null;
    }

    private static bool IsReadMethod(string? methodName)
    {
        if (string.IsNullOrEmpty(methodName)) return false;
        for (var i = 0; i < ReadMethodPrefixes.Length; i++)
        {
            if (methodName.StartsWith(ReadMethodPrefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsWriteMethod(string? methodName)
    {
        if (string.IsNullOrEmpty(methodName)) return false;
        for (var i = 0; i < QueryFactoryPrefixes.Length; i++)
        {
            if (methodName.StartsWith(QueryFactoryPrefixes[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
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
