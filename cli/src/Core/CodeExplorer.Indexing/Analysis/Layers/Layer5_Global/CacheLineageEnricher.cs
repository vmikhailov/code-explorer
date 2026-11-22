using CodeExplorer.Core.Common;
using CodeExplorer.Core.Common.Nodes;
using CodeExplorer.Core.Common.Nodes.Layer1_Physical;
using CodeExplorer.Core.Common.Nodes.Layer3_Syntactic;
using CodeExplorer.Core.Common.Nodes.Layer4_Semantic;
using CodeExplorer.Core.Common.Relationships;
using CodeExplorer.Cypher.Common;

namespace CodeExplorer.Core.Parser.Layers;

internal static class CacheLineageEnricher
{
    private const string CacheableToken = "Cacheable";
    private const string CachePutToken = "CachePut";
    private const string CacheEvictToken = "CacheEvict";
    private const string OutputCacheToken = "OutputCache";
    private const string ResponseCacheToken = "ResponseCache";
    private const string CacheInterceptorToken = "CacheInterceptor";
    private const string LruCacheToken = "lru_cache";
    private const string MemoryCacheToken = "MemoryCache";
    private const string DistributedCacheToken = "DistributedCache";
    private const string HybridCacheToken = "HybridCache";
    private const string RedisToken = "Redis";
    private const string RedissonToken = "Redisson";
    private const string MemcachedToken = "Memcached";
    private const string CacheManagerToken = "CacheManager";
    private const string CacheFacadePrefix = "Cache::";
    private const string DefaultRegionSuffix = ":cache";
    private const string FrameworkSpringCache = "spring-cache";
    private const string FrameworkAspNetCache = "aspnetcore-cache";
    private const string FrameworkRedis = "redis";
    private const string FrameworkMemory = "memory-cache";
    private const string FrameworkDistributed = "distributed-cache";
    private const string FrameworkHybrid = "hybrid-cache";
    private const string FrameworkMemcached = "memcached";
    private const string FrameworkPythonCache = "functools-cache";

    private static readonly HashSet<string> KnownCacheReceiverNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cache", "_cache", "memoryCache", "_memoryCache", "distributedCache", "_distributedCache",
        "hybridCache", "_hybridCache", "redis", "_redis", "redisClient", "_redisClient",
        "redisTemplate", "stringRedisTemplate", "cacheManager", "_cacheManager", "memcache", "memcached"
    };

    private static readonly HashSet<string> ReadOnlyCacheMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Get", "GetAsync", "TryGetValue", "GetString", "GetStringAsync", "GetByte",
        "StringGet", "StringGetAsync", "HashGet", "HashGetAsync", "HashGetAll", "HashGetAllAsync",
        "hget", "hgetall", "mget", "getBuffer"
    };

    private static readonly HashSet<string> ReadThroughCacheMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GetOrCreate", "GetOrCreateAsync", "GetOrSet", "GetOrSetAsync", "Remember", "rememberForever"
    };

    private static readonly HashSet<string> WriteCacheMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Set", "SetAsync", "SetString", "SetStringAsync", "StringSet", "StringSetAsync",
        "HashSet", "HashSetAsync", "Put", "PutAsync", "setex", "psetex", "hset", "mset", "put"
    };

    private static readonly HashSet<string> InvalidateCacheMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "Remove", "RemoveAsync", "KeyDelete", "KeyDeleteAsync", "HashDelete", "HashDeleteAsync",
        "Delete", "DeleteAsync", "Evict", "EvictAsync", "Invalidate", "InvalidateAsync",
        "Forget", "Unlink", "del", "unlink", "forget", "evict", "clearCache"
    };

    internal sealed record CacheEnrichmentOutput(List<CacheNode> Caches, List<Relationship> Relationships);

    public static CacheEnrichmentOutput EnrichCaches(
        Layer4Result? l4Result,
        ParsingContext ctx,
        Dictionary<string, List<Reference>> refsByScopeId,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var cachesById = new Dictionary<string, CacheNode>(StringComparer.Ordinal);
        var rels = new List<Relationship>();
        if (l4Result == null) return new CacheEnrichmentOutput([], rels);

        var syntaxStruct = l4Result.Prev?.SyntaxStructure ?? ctx.SyntaxStructure;
        if (syntaxStruct == null) return new CacheEnrichmentOutput([], rels);

        var filesByPath = SemanticEnrichmentHelpers.CollectFilesByPath(l4Result);
        var modules = SemanticEnrichmentHelpers.CollectModules(l4Result.SemanticStructure);
        var typesAndMethods = SemanticEnrichmentHelpers.CollectTypesAndMethods(syntaxStruct);
        var receiverEngines = BuildCacheReceiverMap(ctx, typesAndMethods);

        foreach (var (typeId, tn) in typesAndMethods.TypesById)
        {
            if (tn.IsTest || !typesAndMethods.MethodsByTypeId.TryGetValue(typeId, out var methods)) continue;
            var fileNorm = tn.FilePath.Replace('\\', '/');

            for (var i = 0; i < methods.Count; i++)
            {
                var fn = methods[i];
                if (fn.IsTest) continue;

                EnrichFromAnnotations(ctx, tn, fn, filesByPath, modules, cachesById, rels, existingRefRels);
                EnrichFromCallSites(ctx, tn, fn, fileNorm, receiverEngines, refsByScopeId, filesByPath, modules, cachesById, rels, existingRefRels);
            }
        }

        for (var i = 0; i < typesAndMethods.TopLevelFunctions.Count; i++)
        {
            var fn = typesAndMethods.TopLevelFunctions[i];
            if (fn.IsTest) continue;
            var fileNorm = fn.FilePath.Replace('\\', '/');
            EnrichFromAnnotations(ctx, null, fn, filesByPath, modules, cachesById, rels, existingRefRels);
            EnrichFromCallSites(ctx, null, fn, fileNorm, receiverEngines, refsByScopeId, filesByPath, modules, cachesById, rels, existingRefRels);
        }

        return new CacheEnrichmentOutput(cachesById.Values.ToList(), rels);
    }

    private static Dictionary<(string FileNorm, string Receiver), (CacheEngine Engine, string Framework)> BuildCacheReceiverMap(
        ParsingContext ctx,
        SemanticEnrichmentHelpers.SyntaxTypeMethodIndex index)
    {
        var map = new Dictionary<(string, string), (CacheEngine, string)>();

        foreach (var (_, tn) in index.TypesById)
        {
            var fileNorm = tn.FilePath.Replace('\\', '/');
            for (var i = 0; i < tn.Children.Count; i++)
            {
                if (tn.Children[i] is MemberNode mn && !string.IsNullOrWhiteSpace(mn.DataType))
                {
                    if (TryClassifyCacheType(mn.DataType, out var eng, out var fw))
                    {
                        map[(fileNorm, mn.Name)] = (eng, fw);
                    }
                }
                else if (tn.Children[i] is FunctionNode fn)
                {
                    for (var p = 0; p < fn.Children.Count; p++)
                    {
                        if (fn.Children[p] is ParameterNode pn && !string.IsNullOrWhiteSpace(pn.DataType) &&
                            TryClassifyCacheType(pn.DataType, out var pEng, out var pFw))
                        {
                            map[(fileNorm, pn.Name)] = (pEng, pFw);
                        }
                    }
                }
            }
        }

        for (var i = 0; i < ctx.RawTypeBindings.Count; i++)
        {
            var b = ctx.RawTypeBindings[i];
            if (!string.IsNullOrWhiteSpace(b.VariableName) && !string.IsNullOrWhiteSpace(b.TypeName) &&
                TryClassifyCacheType(b.TypeName, out var eng, out var fw))
            {
                map[(b.FilePath.Replace('\\', '/'), b.VariableName)] = (eng, fw);
            }
        }

        return map;
    }

    private static bool TryClassifyCacheType(string typeName, out CacheEngine engine, out string framework)
    {
        if (typeName.Contains(RedisToken, StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains(RedissonToken, StringComparison.OrdinalIgnoreCase))
        {
            engine = CacheEngine.Redis;
            framework = FrameworkRedis;
            return true;
        }
        if (typeName.Contains(HybridCacheToken, StringComparison.OrdinalIgnoreCase))
        {
            engine = CacheEngine.Hybrid;
            framework = FrameworkHybrid;
            return true;
        }
        if (typeName.Contains(DistributedCacheToken, StringComparison.OrdinalIgnoreCase))
        {
            engine = CacheEngine.Distributed;
            framework = FrameworkDistributed;
            return true;
        }
        if (typeName.Contains(MemcachedToken, StringComparison.OrdinalIgnoreCase))
        {
            engine = CacheEngine.Memcached;
            framework = FrameworkMemcached;
            return true;
        }
        if (typeName.Contains(MemoryCacheToken, StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains(CacheManagerToken, StringComparison.OrdinalIgnoreCase))
        {
            engine = CacheEngine.Memory;
            framework = FrameworkMemory;
            return true;
        }
        engine = CacheEngine.Unspecified;
        framework = string.Empty;
        return false;
    }

    private static void EnrichFromAnnotations(
        ParsingContext ctx,
        TypeNode? ownerType,
        FunctionNode fn,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, CacheNode> cachesById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var attrs = $"{ownerType?.Attributes} {fn.Attributes}".Trim();
        if (attrs.Length == 0) return;

        var defaultRegion = ExtractAnnotationLiteralOnHeader(fn) ?? NormalizeRegionName(ownerType?.Name ?? fn.Name);

        if (attrs.Contains(CacheableToken, StringComparison.Ordinal))
        {
            var node = GetOrCreateCache(ctx, fn, defaultRegion, CacheEngine.Distributed, FrameworkSpringCache, filesByPath, modules, cachesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new ReadsCacheRelationship(fn.Id, node.Id), rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new WritesCacheRelationship(fn.Id, node.Id), rels, existingRefRels);
        }
        if (attrs.Contains(CachePutToken, StringComparison.Ordinal))
        {
            var node = GetOrCreateCache(ctx, fn, defaultRegion, CacheEngine.Distributed, FrameworkSpringCache, filesByPath, modules, cachesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new WritesCacheRelationship(fn.Id, node.Id), rels, existingRefRels);
        }
        if (attrs.Contains(CacheEvictToken, StringComparison.Ordinal))
        {
            var node = GetOrCreateCache(ctx, fn, defaultRegion, CacheEngine.Distributed, FrameworkSpringCache, filesByPath, modules, cachesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new InvalidatesCacheRelationship(fn.Id, node.Id), rels, existingRefRels);
        }
        if (attrs.Contains(OutputCacheToken, StringComparison.Ordinal) ||
            attrs.Contains(ResponseCacheToken, StringComparison.Ordinal) ||
            attrs.Contains(CacheInterceptorToken, StringComparison.Ordinal))
        {
            var node = GetOrCreateCache(ctx, fn, defaultRegion, CacheEngine.HttpOutput, FrameworkAspNetCache, filesByPath, modules, cachesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new ReadsCacheRelationship(fn.Id, node.Id), rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new WritesCacheRelationship(fn.Id, node.Id), rels, existingRefRels);
        }
        if (attrs.Contains(LruCacheToken, StringComparison.OrdinalIgnoreCase))
        {
            var node = GetOrCreateCache(ctx, fn, defaultRegion, CacheEngine.Memory, FrameworkPythonCache, filesByPath, modules, cachesById, rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new ReadsCacheRelationship(fn.Id, node.Id), rels, existingRefRels);
            SemanticEnrichmentHelpers.AddUniqueRel(new WritesCacheRelationship(fn.Id, node.Id), rels, existingRefRels);
        }
    }

    private static void EnrichFromCallSites(
        ParsingContext ctx,
        TypeNode? ownerType,
        FunctionNode fn,
        string fileNorm,
        Dictionary<(string FileNorm, string Receiver), (CacheEngine Engine, string Framework)> receiverEngines,
        Dictionary<string, List<Reference>> refsByScopeId,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, CacheNode> cachesById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var keyByLineAndCallee = new Dictionary<(int Line, string Method), string>();
        var keyByLine = new Dictionary<int, string>();
        for (var i = 0; i < fn.Children.Count; i++)
        {
            if (fn.Children[i] is CallArgumentNode ca && ca.ArgIndex == 0)
            {
                var candidate = !string.IsNullOrWhiteSpace(ca.ResolvedValue) ? ca.ResolvedValue : ca.RawExpression;
                var normKey = NormalizeCacheKey(candidate);
                if (!string.IsNullOrEmpty(normKey))
                {
                    var shortCallee = SemanticEnrichmentHelpers.ExtractMethodTail(ca.Callee);
                    keyByLineAndCallee.TryAdd((ca.Line, shortCallee), normKey);
                    keyByLine.TryAdd(ca.Line, normKey);
                }
            }
            else if (fn.Children[i] is CallLiteralNode lit && lit.ArgIndex == 0)
            {
                var normKey = NormalizeCacheKey(lit.Value);
                if (!string.IsNullOrEmpty(normKey))
                {
                    var shortCallee = SemanticEnrichmentHelpers.ExtractMethodTail(lit.Callee);
                    keyByLineAndCallee.TryAdd((lit.Line, shortCallee), normKey);
                    keyByLine.TryAdd(lit.Line, normKey);
                }
            }
        }

        var scopeRefs = SemanticEnrichmentHelpers.GetScopeReferences(fn.Id, fn.References, refsByScopeId);
        for (var i = 0; i < scopeRefs.Count; i++)
        {
            var r = scopeRefs[i];
            if (r.Kind != RelationshipKind.Calls) continue;
            if (!SemanticEnrichmentHelpers.TryGetReceiverAndMethod(r, out var receiver, out var method)) continue;

            if (!TryResolveCacheEngine(fileNorm, receiver, receiverEngines, out var engine, out var fw)) continue;

            var isReadThrough = ReadThroughCacheMethods.Contains(method);
            var isRead = !isReadThrough && ReadOnlyCacheMethods.Contains(method);
            var isWrite = !isReadThrough && !isRead && WriteCacheMethods.Contains(method);
            var isInvalidate = !isReadThrough && !isRead && !isWrite && InvalidateCacheMethods.Contains(method);
            if (!isReadThrough && !isRead && !isWrite && !isInvalidate) continue;

            if ((!keyByLineAndCallee.TryGetValue((r.Line, method), out var region) &&
                 !keyByLine.TryGetValue(r.Line, out region)) ||
                string.IsNullOrEmpty(region))
            {
                region = NormalizeRegionName(ownerType?.Name ?? fn.Name);
            }

            var cacheNode = GetOrCreateCache(ctx, fn, region, engine, fw, filesByPath, modules, cachesById, rels, existingRefRels);
            if (isReadThrough)
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new ReadsCacheRelationship(fn.Id, cacheNode.Id), rels, existingRefRels);
                SemanticEnrichmentHelpers.AddUniqueRel(new WritesCacheRelationship(fn.Id, cacheNode.Id), rels, existingRefRels);
            }
            else if (isRead)
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new ReadsCacheRelationship(fn.Id, cacheNode.Id), rels, existingRefRels);
            }
            else if (isWrite)
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new WritesCacheRelationship(fn.Id, cacheNode.Id), rels, existingRefRels);
            }
            else if (isInvalidate)
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new InvalidatesCacheRelationship(fn.Id, cacheNode.Id), rels, existingRefRels);
            }
        }
    }

    private static bool TryResolveCacheEngine(
        string fileNorm,
        string receiver,
        Dictionary<(string FileNorm, string Receiver), (CacheEngine Engine, string Framework)> receiverEngines,
        out CacheEngine engine,
        out string framework)
    {
        if (receiverEngines.TryGetValue((fileNorm, receiver), out var hit))
        {
            engine = hit.Engine;
            framework = hit.Framework;
            return true;
        }
        if (receiver.StartsWith(CacheFacadePrefix, StringComparison.Ordinal) || KnownCacheReceiverNames.Contains(receiver))
        {
            var isRedis = receiver.Contains(RedisToken, StringComparison.OrdinalIgnoreCase);
            engine = isRedis ? CacheEngine.Redis : CacheEngine.Memory;
            framework = isRedis ? FrameworkRedis : FrameworkMemory;
            return true;
        }
        engine = CacheEngine.Unspecified;
        framework = string.Empty;
        return false;
    }

    private static bool TrySplitReceiverAndMethod(string targetName, out string receiver, out string method)
    {
        receiver = string.Empty;
        method = string.Empty;
        if (string.IsNullOrEmpty(targetName)) return false;

        if (targetName.StartsWith(CacheFacadePrefix, StringComparison.Ordinal))
        {
            receiver = CacheFacadePrefix;
            method = targetName[CacheFacadePrefix.Length..];
            return method.Length > 0;
        }

        var dot = targetName.LastIndexOf('.');
        if (dot <= 0 || dot >= targetName.Length - 1) return false;
        receiver = SemanticEnrichmentHelpers.CleanReceiverName(targetName[..dot]);
        method = targetName[(dot + 1)..];
        return receiver.Length > 0 && method.Length > 0;
    }

    private static string? ExtractAnnotationLiteralOnHeader(FunctionNode fn)
    {
        for (var i = 0; i < fn.Children.Count; i++)
        {
            if (fn.Children[i] is CallLiteralNode lit && lit.Line <= fn.StartLine + 1 && !string.IsNullOrWhiteSpace(lit.Value))
            {
                var k = NormalizeCacheKey(lit.Value);
                if (!string.IsNullOrEmpty(k)) return k;
            }
        }
        return null;
    }

    private static string NormalizeCacheKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = raw.Trim().Trim('"', '\'', '`', '$');
        var colon = s.IndexOf(':');
        if (colon > 1) s = s[..colon];
        var brace = s.IndexOf('{');
        if (brace > 1) s = s[..brace];
        var plus = s.IndexOf('+');
        if (plus > 1) s = s[..plus];
        s = s.Trim('_', '-', '.', '/', ' ');
        return s.Length is >= 2 and <= 64 && char.IsLetter(s[0]) ? s.ToLowerInvariant() : string.Empty;
    }

    private static string NormalizeRegionName(string ownerOrFn)
    {
        var clean = ownerOrFn;
        var dot = clean.LastIndexOf('.');
        if (dot >= 0) clean = clean[(dot + 1)..];
        return clean.ToLowerInvariant() + DefaultRegionSuffix;
    }

    private static CacheNode GetOrCreateCache(
        ParsingContext ctx,
        FunctionNode fn,
        string region,
        CacheEngine engine,
        string framework,
        Dictionary<string, FileNode> filesByPath,
        List<ModuleNode> modules,
        Dictionary<string, CacheNode> cachesById,
        List<Relationship> rels,
        HashSet<(string From, string To, string Kind)> existingRefRels)
    {
        var engineKey = engine.ToCanonicalString();
        var cacheId = $"{ctx.WorkspaceId}:{OntologyConstants.IdPrefixes.Cache}:{engineKey}:{region}";
        var fileNorm = fn.FilePath.Replace('\\', '/');

        if (!cachesById.TryGetValue(cacheId, out var node))
        {
            node = new CacheNode(cacheId, region, fileNorm, engine)
            {
                KeyPattern = region,
                Framework = framework,
                FilePath = fileNorm,
                StartLine = fn.StartLine
            };
            cachesById[cacheId] = node;

            if (filesByPath.TryGetValue(fileNorm, out var fNode))
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new DeclaredInRelationship(cacheId, fNode.Id), rels, existingRefRels);
            }
            var ownerMod = SemanticEnrichmentHelpers.FindOwningModule(fileNorm, modules);
            if (ownerMod != null)
            {
                SemanticEnrichmentHelpers.AddUniqueRel(new ContainsRelationship(ownerMod.Id, cacheId), rels, existingRefRels);
            }
        }

        SemanticEnrichmentHelpers.AddUniqueRel(new AccessedByRelationship(cacheId, fn.Id), rels, existingRefRels);
        return node;
    }
}
