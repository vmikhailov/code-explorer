using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer2_Boundaries;

namespace CodeExplorer.Core.Parser;

public partial class ResourceSemanticAnalyzer
{
    private static readonly ConcurrentDictionary<string, (string? Engine, string? DbType)> _projectDriverCache = new(StringComparer.OrdinalIgnoreCase);

    private static (string? Engine, string? DbType) DetectProjectDatabaseDriver(ProjectNode projectNode, ParsingContext ctx)
    {
        if (projectNode == null) return (null, null);
        var cacheKey = $"{ctx.AbsoluteWorkspacePath}|{projectNode.Id}";
        if (_projectDriverCache.TryGetValue(cacheKey, out var cached)) return cached;

        if (!string.IsNullOrEmpty(projectNode.DbDriverEngine))
        {
            var extType = projectNode.DbDriverFamily ?? "relational";
            var res = (projectNode.DbDriverEngine, extType);
            _projectDriverCache[cacheKey] = res;
            return res;
        }
        if (projectNode.TryGetExtension(Layers.Layer3FileSignalEnricher.ExtDbDriverEngine, out var extEngineVal) &&
            !string.IsNullOrEmpty(extEngineVal))
        {
            projectNode.TryGetExtension(Layers.Layer3FileSignalEnricher.ExtDbDriverType, out var extTypeVal);
            var res = (extEngineVal, extTypeVal ?? "relational");
            _projectDriverCache[cacheKey] = res;
            return res;
        }
        if (projectNode.DbDriverChecked == true || projectNode.TryGetExtension(Layers.Layer3FileSignalEnricher.ExtDbDriverChecked, out _))
        {
            if (ctx.ResourceRegistry != null)
            {
                var thisProjRel = ctx.ResourceRegistry.AllResources
                    .FirstOrDefault(r => r.BelongsToProject(projectNode.Id) &&
                                         r.DbType == CodeExplorer.Cypher.Common.DatabaseType.Relational &&
                                         !ResourceNamingHelper.IsGenericConfigKey(r.Engine) &&
                                         !string.Equals(r.Name, "Database", StringComparison.OrdinalIgnoreCase));
                if (thisProjRel != null && !string.IsNullOrEmpty(thisProjRel.Engine))
                {
                    var res = (thisProjRel.Engine, "relational");
                    _projectDriverCache[cacheKey] = res;
                    return res;
                }
            }
            _projectDriverCache[cacheKey] = (null, null);
            return (null, null);
        }
        // Fast path: check in-memory PackageNodes parsed in Layer 2 before hitting the filesystem
        foreach (var pkg in projectNode.Children.OfType<PackageNode>())
        {
            var pLower = pkg.Name.ToLowerInvariant();
            if (pLower.Contains("npgsql") || pLower is "pg" or "pg-promise" or "@types/pg" or "postgres" or "tokio-postgres" || pLower.Contains("psycopg") || pLower.Contains("asyncpg"))
            {
                var res = ("PostgreSQL", "relational");
                _projectDriverCache[cacheKey] = res;
                return res;
            }
            if (pLower.Contains("sqlclient") || pLower.Contains("entityframeworkcore.sqlserver") || pLower is "mssql" or "tedious")
            {
                var res = ("SQL Server", "relational");
                _projectDriverCache[cacheKey] = res;
                return res;
            }
            if (pLower.Contains("mysql") || pLower.Contains("pomelo") || pLower.Contains("pymysql"))
            {
                var res = ("MySQL", "relational");
                _projectDriverCache[cacheKey] = res;
                return res;
            }
            if (pLower.Contains("sqlite"))
            {
                var res = ("SQLite", "relational");
                _projectDriverCache[cacheKey] = res;
                return res;
            }
            if (pLower.Contains("oracle"))
            {
                var res = ("Oracle", "relational");
                _projectDriverCache[cacheKey] = res;
                return res;
            }
            if (pLower.Contains("mongodb"))
            {
                var res = ("MongoDB", "document");
                _projectDriverCache[cacheKey] = res;
                return res;
            }
            if (pLower is "redis" or "ioredis" || pLower.Contains("stackexchange.redis"))
            {
                var res = ("Redis", "cache");
                _projectDriverCache[cacheKey] = res;
                return res;
            }
        }

        // Check if THIS project has a concrete relational database configured in ResourceRegistry
        if (ctx.ResourceRegistry != null)
        {
            var thisProjRelational = ctx.ResourceRegistry.AllResources
                .FirstOrDefault(r => r.BelongsToProject(projectNode.Id) &&
                                     r.DbType == CodeExplorer.Cypher.Common.DatabaseType.Relational &&
                                     !ResourceNamingHelper.IsGenericConfigKey(r.Engine) &&
                                     !string.Equals(r.Name, "Database", StringComparison.OrdinalIgnoreCase));
            if (thisProjRelational != null && !string.IsNullOrEmpty(thisProjRelational.Engine))
            {
                var res = (thisProjRelational.Engine, "relational");
                _projectDriverCache[cacheKey] = res;
                return res;
            }
        }

        _projectDriverCache[cacheKey] = (null, null);
        return (null, null);
    }

    private static (string? Engine, string? DbType) DetectFileDatabaseDriver(
        CodeExplorer.Core.Common.Nodes.Layer1_Physical.FileNode? fileNode,
        SyntaxTree? syntaxTree)
    {
        if (fileNode != null &&
            fileNode.TryGetExtension(Layers.Layer3FileSignalEnricher.ExtDbDriverEngine, out var extEngine) &&
            !string.IsNullOrEmpty(extEngine))
        {
            fileNode.TryGetExtension(Layers.Layer3FileSignalEnricher.ExtDbDriverType, out var extType);
            return (extEngine, extType ?? "relational");
        }

        if (fileNode != null)
        {
            var fromRefs = FindEngineInNodeReferences(fileNode);
            if (fromRefs.Engine != null) return fromRefs;
        }

        if (syntaxTree?.RawImports != null)
        {
            foreach (var imp in syntaxTree.RawImports)
            {
                var p = imp.Path;
                if (string.IsNullOrEmpty(p)) continue;
                if (p.Contains("postgres", StringComparison.OrdinalIgnoreCase) || p.Contains("npgsql", StringComparison.OrdinalIgnoreCase) || p.Contains("pgx", StringComparison.OrdinalIgnoreCase))
                    return ("PostgreSQL", "relational");
                if (p.Contains("mysql", StringComparison.OrdinalIgnoreCase))
                    return ("MySQL", "relational");
                if (p.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
                    return ("SQLite", "relational");
            }
        }

        return (null, null);
    }

    private static (string? Engine, string? DbType) FindEngineInNodeReferences(CodeExplorer.Core.Common.Nodes.IOntologyNode node)
    {
        foreach (var r in node.References)
        {
            if (r.Kind != OntologyConstants.Relationships.UsesDb || string.IsNullOrEmpty(r.TargetName)) continue;
            var t = r.TargetName.ToLowerInvariant();
            if (t is "postgres" or "postgresql") return ("PostgreSQL", "relational");
            if (t is "mysql" or "mariadb") return ("MySQL", "relational");
            if (t is "sqlite" or "sqlite3") return ("SQLite", "relational");
            if (t is "mssql" or "sqlserver" or "sql server") return ("SQL Server", "relational");
            if (t is "oracle") return ("Oracle", "relational");
            if (t is "mongodb" or "mongo") return ("MongoDB", "document");
            if (t is "redis") return ("Redis", "cache");
        }

        foreach (var child in node.Children)
        {
            var found = FindEngineInNodeReferences(child);
            if (found.Engine != null) return found;
        }

        return (null, null);
    }
}
